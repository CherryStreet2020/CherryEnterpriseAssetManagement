// Sprint 15.2 PR-8 (2026-05-28) — SubcontractCostingService impl.
//
// Wraps ICostTransactionService.PostCostAsync per §12 cost element. Each
// public method posts 1-N CostTransaction rows scoped to the SubcontractOperation.
// Idempotency is enforced per (sourceTransactionType, sourceTransactionId,
// transactionType) via existence check before posting — duplicate "post freight
// out for shipment #X" calls are no-ops.
//
// No new entities, no migrations. All cost flows through the existing
// Sprint 14.4 cost engine; this service just translates §12 vocabulary into
// CostTransactionType + ProductionCostBucket values.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public class SubcontractCostingService : ISubcontractCostingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICostTransactionService _costSvc;
    private readonly ILogger<SubcontractCostingService> _log;

    public SubcontractCostingService(
        AppDbContext db,
        ITenantContext tenant,
        ICostTransactionService costSvc,
        ILogger<SubcontractCostingService> log)
    {
        _db = db;
        _tenant = tenant;
        _costSvc = costSvc;
        _log = log;
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP-5 ATTACHED: ship-time cost elements
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractCostPostResult>> PostShipmentCostsAsync(
        PostShipmentCostsRequest r, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(r.SubcontractOperationId, ct);
        if (op == null)
            return Result.Failure<SubcontractCostPostResult>(
                $"SubcontractOperation {r.SubcontractOperationId} not found or out of tenant scope.");

        var shipment = await _db.Set<SubcontractShipment>()
            .Where(s => s.Id == r.SubcontractShipmentId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (shipment == null)
            return Result.Failure<SubcontractCostPostResult>(
                $"SubcontractShipment {r.SubcontractShipmentId} not found or out of tenant scope.");
        if (shipment.SubcontractOperationId != op.Id)
            return Result.Failure<SubcontractCostPostResult>(
                "Shipment does not belong to the requested subcontract op.");

        var postedIds = new List<int>();
        decimal total = 0m;

        if (r.FreightOutCost is { } fOut && fOut > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractShipmentId,
                CostTransactionType.SubcontractFreightOut,
                ProductionCostBucket.LandedCost,
                1m, "EA", fOut,
                "SubcontractShipment", r.CurrencyCode, r.PostedBy,
                $"Freight to vendor — shipment {shipment.ShipmentNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        if (r.PackagingCost is { } pkg && pkg > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractShipmentId,
                CostTransactionType.PackagingCrating,
                ProductionCostBucket.Packaging,
                1m, "EA", pkg,
                "SubcontractShipment", r.CurrencyCode, r.PostedBy,
                $"Packaging/crating — shipment {shipment.ShipmentNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        if (r.ExpediteCost is { } exp && exp > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractShipmentId,
                CostTransactionType.SubcontractExpedite,
                ProductionCostBucket.Subcontract,
                1m, "EA", exp,
                "SubcontractShipment", r.CurrencyCode, r.PostedBy,
                $"Expedite charge — shipment {shipment.ShipmentNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        return Result.Success(new SubcontractCostPostResult(
            op.Id, postedIds.Count, total, r.CurrencyCode, postedIds,
            $"Posted {postedIds.Count} shipment-time cost transaction(s) for op #{op.Id}, shipment #{r.SubcontractShipmentId}."));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP-7 ATTACHED: receipt-time cost elements (service + freight + cert + inspection + scrap)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractCostPostResult>> PostReceiptCostsAsync(
        PostReceiptCostsRequest r, CancellationToken ct = default)
    {
        if (r.QuantityAccepted < 0m)
            return Result.Failure<SubcontractCostPostResult>("QuantityAccepted must be ≥ 0.");

        var op = await LoadOpAsync(r.SubcontractOperationId, ct);
        if (op == null)
            return Result.Failure<SubcontractCostPostResult>(
                $"SubcontractOperation {r.SubcontractOperationId} not found or out of tenant scope.");

        var receipt = await _db.Set<SubcontractReceipt>()
            .Where(rc => rc.Id == r.SubcontractReceiptId &&
                         _tenant.VisibleCompanyIds.Contains(rc.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (receipt == null)
            return Result.Failure<SubcontractCostPostResult>(
                $"SubcontractReceipt {r.SubcontractReceiptId} not found or out of tenant scope.");
        if (receipt.SubcontractOperationId != op.Id)
            return Result.Failure<SubcontractCostPostResult>(
                "Receipt does not belong to the requested subcontract op.");

        var unitCost = r.ServiceUnitCost ?? op.ServiceUnitCost;
        var postedIds = new List<int>();
        decimal total = 0m;

        // 1. Service cost — the vendor's processing work (the headline charge)
        if (r.QuantityAccepted > 0m && unitCost > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractReceiptId,
                CostTransactionType.SubcontractService,
                ProductionCostBucket.Subcontract,
                r.QuantityAccepted, "EA", unitCost,
                "SubcontractReceipt", r.CurrencyCode, r.PostedBy,
                $"Service cost (provisional from PO line) — receipt {receipt.ReceiptNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        // 2. Freight from vendor back to us
        if (r.FreightReturnCost is { } fIn && fIn > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractReceiptId,
                CostTransactionType.SubcontractFreightReturn,
                ProductionCostBucket.LandedCost,
                1m, "EA", fIn,
                "SubcontractReceipt", r.CurrencyCode, r.PostedBy,
                $"Freight from vendor — receipt {receipt.ReceiptNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        // 3. Cert fee (supplier provides cert documentation)
        if (r.CertFee is { } cf && cf > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractReceiptId,
                CostTransactionType.TestCertification,
                ProductionCostBucket.Quality,
                1m, "EA", cf,
                "SubcontractReceipt", r.CurrencyCode, r.PostedBy,
                $"Supplier cert fee — receipt {receipt.ReceiptNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        // 4. Inspection fee (incoming inspection on return)
        if (r.InspectionFee is { } insp && insp > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractReceiptId,
                CostTransactionType.QualityInspection,
                ProductionCostBucket.Quality,
                1m, "EA", insp,
                "SubcontractReceipt", r.CurrencyCode, r.PostedBy,
                $"Inspection fee — receipt {receipt.ReceiptNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        // 5. Scrap charge at vendor (we eat the scrap or charge it back)
        if (r.ScrapChargeAtVendor is { } scrap && scrap > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractReceiptId,
                CostTransactionType.SubcontractScrapCharge,
                ProductionCostBucket.Scrap,
                1m, "EA", scrap,
                "SubcontractReceipt", r.CurrencyCode, r.PostedBy,
                $"Vendor scrap charge — receipt {receipt.ReceiptNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        // 6. Vendor credit (reduces cost — sign-handled via negative ExtendedCost)
        if (r.VendorCredit is { } credit && credit > 0m)
        {
            var posted = await PostIfNew(op, r.SubcontractReceiptId,
                CostTransactionType.SubcontractVendorCredit,
                ProductionCostBucket.Subcontract,
                1m, "EA", -credit,    // Negative cost = credit
                "SubcontractReceipt", r.CurrencyCode, r.PostedBy,
                $"Vendor credit — receipt {receipt.ReceiptNumber}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        return Result.Success(new SubcontractCostPostResult(
            op.Id, postedIds.Count, total, r.CurrencyCode, postedIds,
            $"Posted {postedIds.Count} receipt-time cost transaction(s) for op #{op.Id}, receipt #{r.SubcontractReceiptId}."));
    }

    // ════════════════════════════════════════════════════════════════════════
    // INVOICE TRUE-UP: settle PPV + invoice variance when supplier invoice arrives
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractCostPostResult>> PostInvoiceTrueUpAsync(
        PostInvoiceTrueUpRequest r, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(r.SubcontractOperationId, ct);
        if (op == null)
            return Result.Failure<SubcontractCostPostResult>(
                $"SubcontractOperation {r.SubcontractOperationId} not found or out of tenant scope.");

        if (r.QuantityInvoiced <= 0m)
            return Result.Failure<SubcontractCostPostResult>("QuantityInvoiced must be > 0.");

        // Pre-PR P2 #2: a null/blank InvoiceNumber collides under PostIfNew
        // because two anonymous invoices would share the "SubcontractInvoice:(unspecified)"
        // sourceType. Force the caller to supply an invoice number.
        if (string.IsNullOrWhiteSpace(r.InvoiceNumber))
            return Result.Failure<SubcontractCostPostResult>(
                "InvoiceNumber is required (null/blank would collide with other anonymous invoices under the idempotency guard).");

        // Pre-PR P2 #3: SourceTransactionType is MaxLength(64). The longest
        // prefix in this method is "SubcontractInvoicePpv:" (22 chars) — keep
        // 40 chars for the invoice number itself. Realistic invoice IDs fit.
        if (r.InvoiceNumber.Length > 40)
            return Result.Failure<SubcontractCostPostResult>(
                $"InvoiceNumber length {r.InvoiceNumber.Length} exceeds 40 chars (SourceTransactionType column is 64; prefix consumes 22-24).");

        // Codex P2: scope service-cost lookup to THIS subcontract op, not the
        // whole PRO. A PRO with multiple subcontract operations would otherwise
        // pull every op's provisional service cost and miscompute the variance.
        // Join through SubcontractReceipt to filter by SubcontractOperationId.
        var existingService = await (
            from t in _db.Set<CostTransaction>()
            join rc in _db.Set<SubcontractReceipt>()
                on t.SourceTransactionId equals rc.Id
            where t.SourceTransactionType == "SubcontractReceipt" &&
                  t.TransactionType == CostTransactionType.SubcontractService &&
                  rc.SubcontractOperationId == op.Id &&
                  _tenant.VisibleCompanyIds.Contains(t.CompanyId) &&
                  _tenant.VisibleCompanyIds.Contains(rc.CompanyId)
            select t).ToListAsync(ct);

        var provisionalExt = existingService.Sum(t => t.ExtendedCost);
        var invoiceExt = r.InvoicedAmount;

        // Pre-PR P2 #1: invoice arriving before any receipt-time SubcontractService
        // posting → no provisional baseline. Posting the whole invoice as
        // InvoiceVariance would mis-classify the entire operating cost.
        // Reject so the caller posts receipt costs first.
        if (!existingService.Any())
            return Result.Failure<SubcontractCostPostResult>(
                $"No provisional SubcontractService cost found for op #{op.Id} on PRO #{op.ProductionOrderId}. " +
                "Run PostReceiptCostsAsync (Step 7 attached cost posting) before invoice true-up so the variance " +
                "is computed against a real baseline.");

        var provisionalQty = existingService.Sum(t => t.Quantity);
        var delta = invoiceExt - provisionalExt;
        // Pre-PR P3 #7: guard against zero-qty edge — if the provisional has
        // rows but qty sums to zero, fall back to zero unit-price drift.
        var unitPriceDelta = provisionalQty > 0m
            ? (r.InvoicedAmount / r.QuantityInvoiced) - (provisionalExt / provisionalQty)
            : 0m;

        var postedIds = new List<int>();
        decimal total = 0m;

        // 1. Invoice variance (delta vs provisional)
        if (Math.Abs(delta) > 0.01m)
        {
            var posted = await PostIfNew(op, op.Id,    // source ID = op id since invoice doesn't have its own entity in this PR
                CostTransactionType.InvoiceVariance,
                ProductionCostBucket.Variance,
                1m, "EA", delta,
                $"SubcontractInvoice:{r.InvoiceNumber ?? "(unspecified)"}",
                r.CurrencyCode, r.PostedBy,
                $"Invoice variance vs provisional — invoice {r.InvoiceNumber}, delta {delta:N4}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        // 2. Purchase price variance (unit-cost drift)
        if (Math.Abs(unitPriceDelta) > 0.0001m)
        {
            var posted = await PostIfNew(op, op.Id,
                CostTransactionType.PurchasePriceVariance,
                ProductionCostBucket.Variance,
                r.QuantityInvoiced, "EA", unitPriceDelta,
                $"SubcontractInvoicePpv:{r.InvoiceNumber ?? "(unspecified)"}",
                r.CurrencyCode, r.PostedBy,
                $"PPV — invoice {r.InvoiceNumber}, unit-delta {unitPriceDelta:N4}", ct);
            if (posted != null) { postedIds.Add(posted.Id); total += posted.ExtendedCost; }
        }

        if (postedIds.Count == 0)
            return Result.Success(new SubcontractCostPostResult(
                op.Id, 0, 0m, r.CurrencyCode, postedIds,
                "Invoice matches provisional — no variance posted."));

        return Result.Success(new SubcontractCostPostResult(
            op.Id, postedIds.Count, total, r.CurrencyCode, postedIds,
            $"Invoice true-up: {postedIds.Count} variance transaction(s) posted for op #{op.Id}, delta {delta:N4}, PPV {unitPriceDelta:N4}."));
    }

    // ════════════════════════════════════════════════════════════════════════
    // PRO CLOSE SETTLEMENT
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractCostPostResult>> SettleAtCloseAsync(
        SettleAtCloseRequest r, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(r.SubcontractOperationId, ct);
        if (op == null)
            return Result.Failure<SubcontractCostPostResult>(
                $"SubcontractOperation {r.SubcontractOperationId} not found or out of tenant scope.");

        // Idempotency: refuse if a settlement already exists for this op.
        var alreadySettled = await _db.Set<CostTransaction>()
            .AnyAsync(t => t.ProductionOrderId == op.ProductionOrderId &&
                            t.TransactionType == CostTransactionType.VarianceSettlement &&
                            t.SourceTransactionType == "SubcontractClose" &&
                            t.SourceTransactionId == op.Id &&
                            _tenant.VisibleCompanyIds.Contains(t.CompanyId), ct);
        if (alreadySettled)
            return Result.Success(new SubcontractCostPostResult(
                op.Id, 0, 0m, "USD", Array.Empty<int>(),
                "Already settled — idempotent return."));

        // Codex P2 #3 + P3 #5: scope open-balance to THIS op only (not PRO-wide),
        // and include every bucket this service touches.
        // Approach: collect SubcontractShipment + SubcontractReceipt ids for the
        // op, then sum CostTransactions whose SourceTransactionId is in that set
        // (sourceType-prefixed appropriately).
        var opShipmentIds = await _db.Set<SubcontractShipment>()
            .Where(s => s.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .Select(s => s.Id)
            .ToListAsync(ct);
        var opReceiptIds = await _db.Set<SubcontractReceipt>()
            .Where(rc => rc.SubcontractOperationId == op.Id &&
                         _tenant.VisibleCompanyIds.Contains(rc.CompanyId))
            .Select(rc => rc.Id)
            .ToListAsync(ct);

        var shipmentCosts = await _db.Set<CostTransaction>()
            .Where(t => t.SourceTransactionType == "SubcontractShipment" &&
                        opShipmentIds.Contains(t.SourceTransactionId ?? -1) &&
                        _tenant.VisibleCompanyIds.Contains(t.CompanyId))
            .SumAsync(t => (decimal?)t.ExtendedCost, ct) ?? 0m;
        var receiptCosts = await _db.Set<CostTransaction>()
            .Where(t => t.SourceTransactionType == "SubcontractReceipt" &&
                        opReceiptIds.Contains(t.SourceTransactionId ?? -1) &&
                        _tenant.VisibleCompanyIds.Contains(t.CompanyId))
            .SumAsync(t => (decimal?)t.ExtendedCost, ct) ?? 0m;
        // Invoice variance is sourced to op.Id directly (per PostInvoiceTrueUp impl).
        var invoiceCosts = await _db.Set<CostTransaction>()
            .Where(t => t.SourceTransactionType != null &&
                        t.SourceTransactionType.StartsWith("SubcontractInvoice") &&
                        t.SourceTransactionId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(t.CompanyId))
            .SumAsync(t => (decimal?)t.ExtendedCost, ct) ?? 0m;
        var openBalance = shipmentCosts + receiptCosts + invoiceCosts;

        // Codex P1: actually settle the open balance. Post a VarianceSettlement
        // with a negative offset so the residual subcontract WIP is cleared.
        // ExtendedCost = -openBalance. After the settlement entry, the sum of
        // subcontract-sourced cost transactions for this op is 0.
        // If openBalance is 0, still post a marker row (idempotency guard above
        // prevents duplicates).
        var settlementUnitCost = openBalance != 0m ? -openBalance : 0m;

        var posted = await _costSvc.PostCostAsync(
            CostObjectType.ProductionOrder, op.ProductionOrderId,
            CostTransactionType.VarianceSettlement,
            ProductionCostBucket.Variance,
            op.CompanyId, op.SiteId, op.ProductionOrderId,
            null, null, null,
            quantity: 1m, uom: "EA", unitCost: settlementUnitCost,
            sourceTransactionType: "SubcontractClose",
            sourceTransactionId: op.Id,
            lotNumber: null, serialNumber: null, heatNumber: null,
            rollupAdditive: false,
            notes: $"Subcontract close — op #{op.Id} on PRO #{op.ProductionOrderId}. " +
                   $"Open balance cleared: {openBalance:N4} " +
                   $"(ship={shipmentCosts:N4}, recv={receiptCosts:N4}, inv={invoiceCosts:N4}).",
            postedBy: r.PostedBy ?? "subcontract-close", ct: ct);

        if (!posted.IsSuccess)
            return Result.Failure<SubcontractCostPostResult>(
                $"Settlement post failed: {posted.Error}");

        return Result.Success(new SubcontractCostPostResult(
            op.Id, 1, posted.Value!.ExtendedCost, "USD", new[] { posted.Value.Id },
            $"Settled subcontract op #{op.Id}. Cleared open balance of {openBalance:N4} (ship={shipmentCosts:N4}, recv={receiptCosts:N4}, inv={invoiceCosts:N4})."));
    }

    // ════════════════════════════════════════════════════════════════════════
    // READ — aggregate cost summary by §12 element
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractCostSummary>> GetCostSummaryAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null)
            return Result.Failure<SubcontractCostSummary>(
                $"SubcontractOperation {subcontractOperationId} not found or out of tenant scope.");

        // Codex P2: scope summary to THIS op only (not the whole PRO).
        // For multi-subcontract-op PROs, summing PRO-wide would inflate totals
        // across ops. Filter CostTransactions whose SourceTransactionId resolves
        // to the requested SubcontractOperationId.
        var opShipmentIds = await _db.Set<SubcontractShipment>()
            .Where(s => s.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .Select(s => s.Id)
            .ToListAsync(ct);
        var opReceiptIds = await _db.Set<SubcontractReceipt>()
            .Where(rc => rc.SubcontractOperationId == op.Id &&
                         _tenant.VisibleCompanyIds.Contains(rc.CompanyId))
            .Select(rc => rc.Id)
            .ToListAsync(ct);

        var txns = await _db.Set<CostTransaction>()
            .Where(t => _tenant.VisibleCompanyIds.Contains(t.CompanyId) &&
                        (
                            (t.SourceTransactionType == "SubcontractShipment" &&
                                opShipmentIds.Contains(t.SourceTransactionId ?? -1))
                         || (t.SourceTransactionType == "SubcontractReceipt" &&
                                opReceiptIds.Contains(t.SourceTransactionId ?? -1))
                         || (t.SourceTransactionType == "SubcontractClose" &&
                                t.SourceTransactionId == op.Id)
                         || (t.SourceTransactionType != null &&
                                t.SourceTransactionType.StartsWith("SubcontractInvoice") &&
                                t.SourceTransactionId == op.Id)
                        ))
            .ToListAsync(ct);

        decimal SumOf(CostTransactionType type) =>
            txns.Where(t => t.TransactionType == type).Sum(t => t.ExtendedCost);

        var summary = new SubcontractCostSummary(
            op.Id, op.ProductionOrderId, op.OperationSequence,
            ServiceCostPosted: SumOf(CostTransactionType.SubcontractService),
            FreightOutPosted: SumOf(CostTransactionType.SubcontractFreightOut),
            FreightReturnPosted: SumOf(CostTransactionType.SubcontractFreightReturn),
            PackagingPosted: SumOf(CostTransactionType.PackagingCrating),
            ExpeditePosted: SumOf(CostTransactionType.SubcontractExpedite),
            CertFeesPosted: SumOf(CostTransactionType.TestCertification),
            InspectionFeesPosted: SumOf(CostTransactionType.QualityInspection),
            ScrapChargePosted: SumOf(CostTransactionType.SubcontractScrapCharge),
            VendorCreditPosted: SumOf(CostTransactionType.SubcontractVendorCredit),
            OverheadPosted: SumOf(CostTransactionType.SubcontractOverhead),
            PpvPosted: SumOf(CostTransactionType.PurchasePriceVariance),
            InvoiceVariancePosted: SumOf(CostTransactionType.InvoiceVariance),
            TotalSubcontractCost: txns.Sum(t => t.ExtendedCost),
            TotalTransactionCount: txns.Count);

        return Result.Success(summary);
    }

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private async Task<SubcontractOperation?> LoadOpAsync(int id, CancellationToken ct) =>
        await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == id && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Idempotency-guarded post: refuses to post a 2nd cost transaction with
    /// the same (sourceType, sourceId, transactionType) tuple. Returns the
    /// new transaction on first call; returns null if duplicate suppressed.
    /// </summary>
    private async Task<CostTransaction?> PostIfNew(
        SubcontractOperation op,
        int sourceId,
        CostTransactionType transactionType,
        ProductionCostBucket bucket,
        decimal quantity, string uom, decimal unitCost,
        string sourceType, string currencyCode,
        string? postedBy, string notes, CancellationToken ct)
    {
        var existing = await _db.Set<CostTransaction>()
            .Where(t => t.SourceTransactionType == sourceType &&
                        t.SourceTransactionId == sourceId &&
                        t.TransactionType == transactionType &&
                        t.ProductionOrderId == op.ProductionOrderId &&
                        _tenant.VisibleCompanyIds.Contains(t.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            _log.LogInformation(
                "SubcontractCosting idempotent skip: {Type} for {Source}#{SourceId} on PRO #{Pro} (existing txn #{TxnId})",
                transactionType, sourceType, sourceId, op.ProductionOrderId, existing.Id);
            return null;
        }

        var posted = await _costSvc.PostCostAsync(
            CostObjectType.ProductionOrder, op.ProductionOrderId,
            transactionType, bucket,
            op.CompanyId, op.SiteId, op.ProductionOrderId,
            null, null, null,    // operationId / bomLineId / itemId — left null; subcontract is op-level summary
            quantity, uom, unitCost,
            sourceTransactionType: sourceType,
            sourceTransactionId: sourceId,
            lotNumber: null, serialNumber: null, heatNumber: null,
            rollupAdditive: true,
            notes: notes,
            postedBy: postedBy ?? "subcontract-costing", ct: ct);

        if (!posted.IsSuccess)
            throw new InvalidOperationException(
                $"CostTransactionService.PostCostAsync failed for {transactionType} on op #{op.Id}: {posted.Error}");

        return posted.Value;
    }
}
