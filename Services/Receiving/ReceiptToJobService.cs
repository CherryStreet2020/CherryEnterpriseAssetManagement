// Sprint 15.1 PR-1 (2026-05-28) — Receipt-to-Job Direct Posting Service.
//
// THE FOUNDATIONAL ETO/MTO ARCHITECTURAL CHANGE.
//
// In Engineer-to-Order and Make-to-Order manufacturing, the majority of raw
// materials are bought specifically for a production order. Making those
// materials flow PO Receipt → Inventory → Issue-to-Job is an extra step
// that adds no value — the material was NEVER destined for general stock.
//
// This service implements the direct path:
//   PO Receipt → CostTransaction(PurchasedToJobReceipt) → PRO BOM line
//
// What it does:
//   1. Validates the receipt line → PO line → BOM line linkage
//   2. Posts a CostTransaction with type PurchasedToJobReceipt and
//      bucket PurchasedToJob directly to the PRO cost object
//   3. Updates the BOM line supply link: SupplyQuantityReceived += qty,
//      MaterialSupplyStatus → Received (or Ordered if partial)
//   4. Updates PO line QuantityReceived
//   5. Posts GR/IR accrual JE (Dr ProductionWipMaterial, Cr GrAccrued)
//   6. Emits chain-of-custody edge: Receipt → ProductionOrder
//   7. Optionally gates on inspection (InspectionRequired flag)
//
// What it does NOT do:
//   - Touch ItemInventory. No inventory valuation layer. No ItemTransaction.
//   - Create a ProductionMaterialTransaction. That's for stock-issued material.
//     Direct-to-job material was never in inventory, so there's no "issue."
//
// LOCKS APPLIED:
//   - Tenant scoping via ITenantContext.VisibleCompanyIds
//   - Idempotent via JE Reference "DTJ-{ReceiptNumber}-L{LineId}"
//   - xmin concurrency on BOM line update
//   - Cost posting through ICostTransactionService (typed pipeline)
//   - GL posting through IGlAccountResolver (ADR-003 central resolver)
//
// REFERENCES:
//   - docs/research/purchasing-cascade-design-2026-05-28.md PR-1
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §13
//   - feedback_b6_go_big_2026_05_26.md — BIC architecture, no shortcuts

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Infrastructure;
using Abs.FixedAssets.Services.Production;
using Abs.FixedAssets.Services.Posting;
using Abs.FixedAssets.Services.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Receiving;

// ── Result record ──────────────────────────────────────────────────────
public sealed record ReceiptToJobResult(
    int GoodsReceiptLineId,
    int? CostTransactionId,
    int? JournalEntryId,
    int BomLineId,
    int ProductionOrderId,
    decimal QuantityPosted,
    decimal AmountPosted,
    bool InspectionHeld,
    string? Message);

// ── Request record ─────────────────────────────────────────────────────
/// <summary>
/// Input for receiving material directly to a production order BOM line.
/// </summary>
public sealed record ReceiveToJobRequest(
    int GoodsReceiptLineId,
    int ProductionOrderId,
    int BomLineId,
    decimal? QuantityOverride = null,
    string? PostedBy = null);

// ── Interface ──────────────────────────────────────────────────────────
public interface IReceiptToJobService
{
    /// <summary>
    /// Receive a GR line directly to a PRO BOM line, bypassing inventory.
    /// Posts cost, updates supply link, accrues GR/IR, emits chain-of-custody.
    /// </summary>
    Task<Result<ReceiptToJobResult>> ReceiveToJobAsync(
        ReceiveToJobRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Reverse a previously posted direct-to-job receipt (e.g., inspection
    /// rejection, PO cancellation). Reverses the cost + JE, restores BOM
    /// line supply quantities.
    /// </summary>
    Task<Result<ReceiptToJobResult>> ReverseReceiptToJobAsync(
        int goodsReceiptLineId,
        string? reversedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Complete inspection hold and post the direct-to-job cost that was
    /// previously deferred because InspectionRequired = true.
    /// </summary>
    Task<Result<ReceiptToJobResult>> CompleteInspectionAndPostAsync(
        int goodsReceiptLineId,
        decimal quantityAccepted,
        decimal quantityRejected,
        string? completedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Auto-detect direct-to-job receipt lines on a GoodsReceipt and
    /// process them. Called from ReceivingPostingService when it encounters
    /// IsDirectToJob lines.
    /// </summary>
    Task<IReadOnlyList<ReceiptToJobResult>> ProcessDirectToJobLinesAsync(
        int goodsReceiptId,
        CancellationToken ct = default);
}

// ── Implementation ─────────────────────────────────────────────────────
public class ReceiptToJobService : IReceiptToJobService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICostTransactionService _costService;
    private readonly IGlAccountResolver _glResolver;
    private readonly Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService _chainOfCustody;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<ReceiptToJobService> _logger;

    public ReceiptToJobService(
        AppDbContext db,
        ITenantContext tenantContext,
        ICostTransactionService costService,
        IGlAccountResolver glResolver,
        Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService chainOfCustody,
        IOutboxWriter outbox,
        ILogger<ReceiptToJobService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _costService = costService;
        _glResolver = glResolver;
        _chainOfCustody = chainOfCustody;
        _outbox = outbox;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 1. ReceiveToJobAsync — THE core direct-posting path
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<ReceiptToJobResult>> ReceiveToJobAsync(
        ReceiveToJobRequest request,
        CancellationToken ct = default)
    {
        // ── Load receipt line with PO context ──────────────────────
        var grLine = await _db.GoodsReceiptLines
            .Include(l => l.GoodsReceipt)
            .Include(l => l.PurchaseOrderLine)
                .ThenInclude(pl => pl!.Item)
            .Include(l => l.PurchaseOrderLine)
                .ThenInclude(pl => pl!.PurchaseOrder)
            .Where(l => l.Id == request.GoodsReceiptLineId)
            .FirstOrDefaultAsync(ct);

        if (grLine == null)
            return Result.Failure<ReceiptToJobResult>(
                $"GoodsReceiptLine {request.GoodsReceiptLineId} not found.");

        var receipt = grLine.GoodsReceipt;
        if (receipt == null)
            return Result.Failure<ReceiptToJobResult>(
                $"GoodsReceipt parent missing for line {request.GoodsReceiptLineId}.");

        // Tenant guard
        var companyId = receipt.CompanyId ?? _tenantContext.CompanyId ?? 0;
        if (companyId == 0 || !_tenantContext.VisibleCompanyIds.Contains(companyId))
            return Result.Failure<ReceiptToJobResult>("Receipt is out of tenant scope.");

        // ── Load PRO ──────────────────────────────────────────────
        var pro = await _db.Set<ProductionOrder>()
            .Where(p => p.Id == request.ProductionOrderId &&
                        p.CompanyId == companyId)
            .FirstOrDefaultAsync(ct);

        if (pro == null)
            return Result.Failure<ReceiptToJobResult>(
                $"ProductionOrder {request.ProductionOrderId} not found or not in tenant scope.");

        // ── Load BOM line (frozen snapshot) ────────────────────────
        var bomLine = await _db.Set<ProductionMaterialStructure>()
            .Where(b => b.Id == request.BomLineId &&
                        b.ProductionOrderId == request.ProductionOrderId)
            .FirstOrDefaultAsync(ct);

        if (bomLine == null)
            return Result.Failure<ReceiptToJobResult>(
                $"BOM line {request.BomLineId} not found on PRO {request.ProductionOrderId}.");

        // ── Determine quantity + amount ────────────────────────────
        var poLine = grLine.PurchaseOrderLine;
        if (poLine == null)
            return Result.Failure<ReceiptToJobResult>(
                $"PO line missing for GR line {request.GoodsReceiptLineId}.");

        var qty = request.QuantityOverride
                  ?? (grLine.QuantityAccepted > 0 ? grLine.QuantityAccepted : grLine.QuantityReceived);
        if (qty <= 0)
            return Result.Failure<ReceiptToJobResult>("Quantity must be > 0.");

        var unitCost = poLine.UnitPrice;
        var totalAmount = qty * unitCost;

        // ── Idempotency check ─────────────────────────────────────
        var jeReference = $"DTJ-{receipt.ReceiptNumber}-L{grLine.Id}";
        var alreadyPosted = await _db.JournalEntries
            .AnyAsync(j => j.Reference == jeReference && j.Source == "DTJ", ct);
        if (alreadyPosted)
        {
            _logger.LogInformation(
                "ReceiptToJobService: GR line {LineId} already posted as DTJ, skipping.",
                grLine.Id);
            return Result.Success(new ReceiptToJobResult(
                grLine.Id, null, null, bomLine.Id, pro.Id, qty, totalAmount,
                false, "Already posted — idempotent skip."));
        }

        // ── Inspection gate ───────────────────────────────────────
        if (grLine.InspectionRequired && grLine.QuantityAccepted <= 0)
        {
            // Mark the receipt line but defer cost posting
            grLine.IsDirectToJob = true;
            grLine.DirectToJobProductionOrderId = pro.Id;
            grLine.DirectToJobBomLineId = bomLine.Id;

            // Update BOM line supply status to reflect "received but held"
            bomLine.MaterialSupplyStatus = MaterialSupplyStatus.OnHold;
            bomLine.SupplyNotes = $"Received {qty} — inspection hold, pending acceptance.";
            bomLine.LastSupplyRefreshUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ReceiptToJobService: GR line {LineId} → PRO {ProId} BOM {BomLineId}: inspection hold. Cost deferred.",
                grLine.Id, pro.Id, bomLine.Id);

            return Result.Success(new ReceiptToJobResult(
                grLine.Id, null, null, bomLine.Id, pro.Id, qty, totalAmount,
                true, "Inspection required — cost posting deferred until acceptance."));
        }

        // ── Post cost transaction ─────────────────────────────────
        var costResult = await _costService.PostCostAsync(
            costObjectType: CostObjectType.ProductionOrder,
            costObjectId: pro.Id,
            transactionType: CostTransactionType.PurchasedToJobReceipt,
            costBucket: ProductionCostBucket.PurchasedToJob,
            companyId: companyId,
            siteId: null,
            productionOrderId: pro.Id,
            operationId: bomLine.ConsumingOperationSequence,
            bomLineId: bomLine.Id,
            itemId: bomLine.ChildItemId,
            quantity: qty,
            uom: bomLine.Uom,
            unitCost: unitCost,
            sourceTransactionType: "GoodsReceiptLine",
            sourceTransactionId: grLine.Id,
            lotNumber: grLine.LotNumber,
            serialNumber: grLine.SerialNumber,
            heatNumber: null,
            rollupAdditive: false,
            notes: $"Direct-to-job receipt from PO {poLine.PurchaseOrder?.PONumber ?? "?"} line {poLine.Id}",
            postedBy: request.PostedBy ?? "system",
            ct: ct);

        int? costTxnId = costResult.IsSuccess ? costResult.Value?.Id : null;

        // ── Post JE: Dr WIP-Material, Cr GR-Accrued ──────────────
        var ctx = new GlResolveContext(PurchaseOrderLineId: poLine.Id);
        var (wipAccount, wipKeyId) = await _glResolver.ResolveAccountAndKeyAsync(
            companyId, GlAccountKind.ProductionWipMaterial, ctx,
            logger: _logger, logContext: $"dtj-receipt={receipt.ReceiptNumber} line={grLine.Id}");
        var (grAccruedAccount, grAccruedKeyId) = await _glResolver.ResolveAccountAndKeyAsync(
            companyId, GlAccountKind.GrAccrued, new GlResolveContext(),
            logger: _logger, logContext: $"dtj-receipt={receipt.ReceiptNumber} gr-accrued");

        var je = new JournalEntry
        {
            BookId = null,
            Batch = jeReference,
            Period = int.Parse(receipt.ReceiptDate.ToString("yyyyMM")),
            PostingDate = receipt.ReceiptDate.Date,
            Source = "DTJ",
            Reference = jeReference,
            Description = $"Direct-to-job receipt: PRO {pro.OrderNumber ?? pro.Id.ToString()} " +
                          $"from PO {poLine.PurchaseOrder?.PONumber ?? "?"}",
            CreatedUtc = DateTime.UtcNow,
            Lines = new System.Collections.Generic.List<JournalLine>
            {
                new JournalLine
                {
                    LineNo = 1,
                    Account = wipAccount,
                    AccountingKeyId = wipKeyId,
                    Description = $"DTJ WIP {receipt.ReceiptNumber} → PRO {pro.OrderNumber ?? pro.Id.ToString()}",
                    Debit = totalAmount,
                    Credit = 0m
                },
                new JournalLine
                {
                    LineNo = 2,
                    Account = grAccruedAccount,
                    AccountingKeyId = grAccruedKeyId,
                    Description = $"DTJ GR-Accrued {receipt.ReceiptNumber}",
                    Debit = 0m,
                    Credit = totalAmount
                }
            }
        };

        _db.JournalEntries.Add(je);

        // ── Update GR line ────────────────────────────────────────
        grLine.IsDirectToJob = true;
        grLine.DirectToJobProductionOrderId = pro.Id;
        grLine.DirectToJobBomLineId = bomLine.Id;
        grLine.DirectToJobPostedUtc = DateTime.UtcNow;

        // ── Update PO line received qty ───────────────────────────
        poLine.QuantityReceived += qty;
        if (poLine.QuantityReceived >= poLine.QuantityOrdered)
            poLine.IsReceived = true;

        // ── Update BOM line supply link ───────────────────────────
        bomLine.SupplyQuantityReceived += qty;
        bomLine.SupplyQuantityRemaining = Math.Max(0,
            bomLine.SupplyQuantityRequired - bomLine.SupplyQuantityReceived);
        bomLine.MaterialSupplyStatus = bomLine.SupplyQuantityRemaining <= 0
            ? MaterialSupplyStatus.Received
            : MaterialSupplyStatus.Ordered;
        bomLine.LinkedSupplyRecordType = LinkedSupplyRecordType.PurchaseOrder;
        bomLine.LinkedSupplyRecordId = poLine.PurchaseOrder?.Id;
        bomLine.LinkedSupplyLineId = poLine.Id;
        bomLine.LinkedSupplyRecordNumber = $"PO {poLine.PurchaseOrder?.PONumber ?? "?"}";
        bomLine.SupplyAvailableDate = receipt.ReceiptDate;
        bomLine.LastSupplyRefreshUtc = DateTime.UtcNow;
        bomLine.LateToNeedDate = bomLine.SupplyRequiredDate.HasValue &&
                                  receipt.ReceiptDate > bomLine.SupplyRequiredDate.Value;
        bomLine.SupplyRisk = bomLine.LateToNeedDate ? SupplyRisk.Warning : SupplyRisk.None;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ReceiptToJobService: GR line {LineId} → PRO {ProId} BOM {BomLineId}: " +
            "posted {Qty} × ${UnitCost} = ${Total}. CostTxn={CostId}, JE={JeId}",
            grLine.Id, pro.Id, bomLine.Id, qty, unitCost, totalAmount,
            costTxnId, je.Id);

        // ── Chain-of-custody ──────────────────────────────────────
        try
        {
            await _chainOfCustody.RecordEdgeAsync(
                new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                    FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Receipt,
                    FromEntityId: receipt.Id,
                    FromLabel: receipt.ReceiptNumber,
                    ToNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.ProductionOrder,
                    ToEntityId: pro.Id,
                    ToLabel: pro.OrderNumber ?? $"PRO-{pro.Id}",
                    EdgeType: "DIRECT_TO_JOB"));
        }
        catch (Exception chainEx)
        {
            _logger.LogWarning(chainEx,
                "ReceiptToJobService: chain-of-custody emit failed for DTJ GR line {LineId} — " +
                "JE {JeId} still committed; chain can be rebuilt via backfill.",
                grLine.Id, je.Id);
        }

        return Result.Success(new ReceiptToJobResult(
            grLine.Id, costTxnId, je.Id, bomLine.Id, pro.Id,
            qty, totalAmount, false,
            $"Direct-to-job posted: {qty} × ${unitCost:F4} = ${totalAmount:F2} to PRO {pro.OrderNumber ?? pro.Id.ToString()}"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2. ReverseReceiptToJobAsync — undo a direct-to-job posting
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<ReceiptToJobResult>> ReverseReceiptToJobAsync(
        int goodsReceiptLineId,
        string? reversedBy,
        CancellationToken ct = default)
    {
        var grLine = await _db.GoodsReceiptLines
            .Include(l => l.GoodsReceipt)
            .Include(l => l.PurchaseOrderLine)
                .ThenInclude(pl => pl!.PurchaseOrder)
            .Where(l => l.Id == goodsReceiptLineId && l.IsDirectToJob)
            .FirstOrDefaultAsync(ct);

        if (grLine == null)
            return Result.Failure<ReceiptToJobResult>(
                $"GR line {goodsReceiptLineId} not found or is not a direct-to-job receipt.");

        var receipt = grLine.GoodsReceipt!;
        var companyId = receipt.CompanyId ?? _tenantContext.CompanyId ?? 0;

        if (!grLine.DirectToJobProductionOrderId.HasValue || !grLine.DirectToJobBomLineId.HasValue)
            return Result.Failure<ReceiptToJobResult>("GR line has no PRO/BOM linkage to reverse.");

        // Idempotency
        var revRef = $"REV-DTJ-{receipt.ReceiptNumber}-L{grLine.Id}";
        var alreadyReversed = await _db.JournalEntries
            .AnyAsync(j => j.Reference == revRef && j.Source == "DTJ-REV", ct);
        if (alreadyReversed)
            return Result.Success(new ReceiptToJobResult(
                grLine.Id, null, null, grLine.DirectToJobBomLineId.Value,
                grLine.DirectToJobProductionOrderId.Value, 0, 0, false,
                "Already reversed — idempotent skip."));

        var poLine = grLine.PurchaseOrderLine!;
        var qty = grLine.QuantityAccepted > 0 ? grLine.QuantityAccepted : grLine.QuantityReceived;
        var totalAmount = qty * poLine.UnitPrice;

        // ── Reversing JE: Dr GR-Accrued, Cr WIP-Material ─────────
        var ctx = new GlResolveContext(PurchaseOrderLineId: poLine.Id);
        var (wipAccount, wipKeyId) = await _glResolver.ResolveAccountAndKeyAsync(
            companyId, GlAccountKind.ProductionWipMaterial, ctx,
            logger: _logger, logContext: $"dtj-rev line={grLine.Id}");
        var (grAccruedAccount, grAccruedKeyId) = await _glResolver.ResolveAccountAndKeyAsync(
            companyId, GlAccountKind.GrAccrued, new GlResolveContext(),
            logger: _logger, logContext: $"dtj-rev accrued");

        var je = new JournalEntry
        {
            BookId = null,
            Batch = revRef,
            Period = int.Parse(receipt.ReceiptDate.ToString("yyyyMM")),
            PostingDate = DateTime.UtcNow.Date,
            Source = "DTJ-REV",
            Reference = revRef,
            Description = $"Reversal of direct-to-job receipt {receipt.ReceiptNumber} line {grLine.Id}",
            CreatedUtc = DateTime.UtcNow,
            Lines = new System.Collections.Generic.List<JournalLine>
            {
                new JournalLine
                {
                    LineNo = 1,
                    Account = grAccruedAccount,
                    AccountingKeyId = grAccruedKeyId,
                    Description = $"DTJ-REV {receipt.ReceiptNumber}",
                    Debit = totalAmount,
                    Credit = 0m
                },
                new JournalLine
                {
                    LineNo = 2,
                    Account = wipAccount,
                    AccountingKeyId = wipKeyId,
                    Description = $"DTJ-REV WIP {receipt.ReceiptNumber}",
                    Debit = 0m,
                    Credit = totalAmount
                }
            }
        };
        _db.JournalEntries.Add(je);

        // ── Reverse BOM line supply link ──────────────────────────
        var bomLine = await _db.Set<ProductionMaterialStructure>()
            .FindAsync(new object[] { grLine.DirectToJobBomLineId.Value }, ct);
        if (bomLine != null)
        {
            bomLine.SupplyQuantityReceived = Math.Max(0, bomLine.SupplyQuantityReceived - qty);
            bomLine.SupplyQuantityRemaining = Math.Max(0,
                bomLine.SupplyQuantityRequired - bomLine.SupplyQuantityReceived);
            bomLine.MaterialSupplyStatus = bomLine.SupplyQuantityReceived > 0
                ? MaterialSupplyStatus.Ordered
                : MaterialSupplyStatus.Available;
            bomLine.LastSupplyRefreshUtc = DateTime.UtcNow;
        }

        // ── Reverse PO line received qty ──────────────────────────
        poLine.QuantityReceived = Math.Max(0, poLine.QuantityReceived - qty);
        poLine.IsReceived = poLine.QuantityReceived >= poLine.QuantityOrdered;

        // ── Clear GR line DTJ fields ──────────────────────────────
        grLine.DirectToJobPostedUtc = null;

        // Post a reversing cost transaction
        var costResult = await _costService.PostCostAsync(
            costObjectType: CostObjectType.ProductionOrder,
            costObjectId: grLine.DirectToJobProductionOrderId.Value,
            transactionType: CostTransactionType.PurchasedToJobReceipt,
            costBucket: ProductionCostBucket.PurchasedToJob,
            companyId: companyId,
            siteId: null,
            productionOrderId: grLine.DirectToJobProductionOrderId.Value,
            operationId: null,
            bomLineId: grLine.DirectToJobBomLineId.Value,
            itemId: bomLine?.ChildItemId,
            quantity: -qty,
            uom: null,
            unitCost: poLine.UnitPrice,
            sourceTransactionType: "GoodsReceiptLine-Reversal",
            sourceTransactionId: grLine.Id,
            lotNumber: grLine.LotNumber,
            serialNumber: grLine.SerialNumber,
            heatNumber: null,
            rollupAdditive: false,
            notes: $"Reversal of DTJ receipt {receipt.ReceiptNumber} line {grLine.Id}",
            postedBy: reversedBy ?? "system",
            ct: ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ReceiptToJobService: REVERSED DTJ GR line {LineId} from PRO {ProId}: " +
            "{Qty} × ${UnitCost} = ${Total} reversed. JE={JeId}",
            grLine.Id, grLine.DirectToJobProductionOrderId, qty,
            poLine.UnitPrice, totalAmount, je.Id);

        return Result.Success(new ReceiptToJobResult(
            grLine.Id, costResult.IsSuccess ? costResult.Value?.Id : null, je.Id,
            grLine.DirectToJobBomLineId.Value,
            grLine.DirectToJobProductionOrderId.Value,
            qty, totalAmount, false,
            $"Reversed: {qty} × ${poLine.UnitPrice:F4} = ${totalAmount:F2}"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3. CompleteInspectionAndPostAsync — release inspection hold
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<ReceiptToJobResult>> CompleteInspectionAndPostAsync(
        int goodsReceiptLineId,
        decimal quantityAccepted,
        decimal quantityRejected,
        string? completedBy,
        CancellationToken ct = default)
    {
        var grLine = await _db.GoodsReceiptLines
            .Include(l => l.GoodsReceipt)
            .Include(l => l.PurchaseOrderLine)
            .Where(l => l.Id == goodsReceiptLineId && l.IsDirectToJob && l.InspectionRequired)
            .FirstOrDefaultAsync(ct);

        if (grLine == null)
            return Result.Failure<ReceiptToJobResult>(
                $"GR line {goodsReceiptLineId} not found or not an inspection-held DTJ line.");

        if (grLine.DirectToJobPostedUtc.HasValue)
            return Result.Failure<ReceiptToJobResult>(
                $"GR line {goodsReceiptLineId} has already been posted after inspection.");

        if (quantityAccepted <= 0 && quantityRejected <= 0)
            return Result.Failure<ReceiptToJobResult>(
                "At least one of quantityAccepted or quantityRejected must be > 0.");

        // Update acceptance quantities on the GR line
        grLine.QuantityAccepted = quantityAccepted;
        grLine.QuantityRejected = quantityRejected;

        if (quantityAccepted > 0)
        {
            // Post the accepted portion via the main path
            var postResult = await ReceiveToJobAsync(
                new ReceiveToJobRequest(
                    goodsReceiptLineId,
                    grLine.DirectToJobProductionOrderId!.Value,
                    grLine.DirectToJobBomLineId!.Value,
                    QuantityOverride: quantityAccepted,
                    PostedBy: completedBy),
                ct);

            return postResult;
        }

        // All rejected — update BOM line to Short status
        var bomLine = await _db.Set<ProductionMaterialStructure>()
            .FindAsync(new object[] { grLine.DirectToJobBomLineId!.Value }, ct);
        if (bomLine != null)
        {
            bomLine.MaterialSupplyStatus = MaterialSupplyStatus.Short;
            bomLine.SupplyNotes = $"Inspection rejected {quantityRejected} — supply short.";
            bomLine.SupplyRisk = SupplyRisk.Critical;
            bomLine.LastSupplyRefreshUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(new ReceiptToJobResult(
            grLine.Id, null, null, grLine.DirectToJobBomLineId!.Value,
            grLine.DirectToJobProductionOrderId!.Value,
            0, 0, false,
            $"Inspection complete — all {quantityRejected} rejected. BOM line marked Short."));
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4. ProcessDirectToJobLinesAsync — batch DTJ for a receipt
    // ═══════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<ReceiptToJobResult>> ProcessDirectToJobLinesAsync(
        int goodsReceiptId,
        CancellationToken ct = default)
    {
        var lines = await _db.GoodsReceiptLines
            .Where(l => l.GoodsReceiptId == goodsReceiptId && l.IsDirectToJob)
            .ToListAsync(ct);

        var results = new System.Collections.Generic.List<ReceiptToJobResult>();

        foreach (var line in lines)
        {
            if (!line.DirectToJobProductionOrderId.HasValue || !line.DirectToJobBomLineId.HasValue)
            {
                _logger.LogWarning(
                    "ReceiptToJobService: GR line {LineId} is marked IsDirectToJob but has no PRO/BOM linkage.",
                    line.Id);
                continue;
            }

            if (line.DirectToJobPostedUtc.HasValue)
            {
                // Already posted — skip
                continue;
            }

            var result = await ReceiveToJobAsync(
                new ReceiveToJobRequest(
                    line.Id,
                    line.DirectToJobProductionOrderId.Value,
                    line.DirectToJobBomLineId.Value),
                ct);

            if (result.IsSuccess)
                results.Add(result.Value!);
            else
                _logger.LogWarning(
                    "ReceiptToJobService: failed to process DTJ for GR line {LineId}: {Error}",
                    line.Id, result.Error);
        }

        return results;
    }
}
