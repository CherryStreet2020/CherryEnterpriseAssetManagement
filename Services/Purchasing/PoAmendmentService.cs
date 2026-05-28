// Sprint 15.4 PR-17 — IPoAmendmentService implementation.
//
// THE BIC DIFFERENTIATOR — demand-link impact preview + atomic auto-resync.
//
// Patterns adopted:
//   - ITenantContext.VisibleCompanyIds gating on every read/write
//   - Two-phase numbering for POAMD-YYYY-NNNNNN (Lesson 2, Session 19)
//   - Apply is wrapped in BeginTransactionAsync — never a partial state
//   - IsCurrent guard on every mutation (PR-16 Codex P2 pattern)
//   - Mirror to ProductionSupplyAllocation (PR-3 pattern from PoLineDemandLinkService)
//   - Vendor re-ack hook calls IPoAcknowledgmentService.RequestAcknowledgmentAsync

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Purchasing;

public class PoAmendmentService : IPoAmendmentService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPoAcknowledgmentService _ackService;
    private readonly ILogger<PoAmendmentService> _logger;

    public PoAmendmentService(
        AppDbContext db,
        ITenantContext tenantContext,
        IPoAcknowledgmentService ackService,
        ILogger<PoAmendmentService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _ackService = ackService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1) DraftAmendmentAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<DraftAmendmentResult>> DraftAmendmentAsync(
        DraftAmendmentRequest request, CancellationToken ct = default)
    {
        if (request.Lines == null || request.Lines.Count == 0)
            return Result.Failure<DraftAmendmentResult>(
                "Cannot draft an amendment with zero line changes.");

        var po = await _db.Set<PurchaseOrder>()
            .Include(p => p.Lines)
            .Where(p => p.Id == request.PurchaseOrderId)
            .FirstOrDefaultAsync(ct);
        if (po == null)
            return Result.Failure<DraftAmendmentResult>(
                $"PurchaseOrder {request.PurchaseOrderId} not found.");

        if (po.CompanyId == null ||
            !_tenantContext.VisibleCompanyIds.Contains(po.CompanyId.Value))
            return Result.Failure<DraftAmendmentResult>(
                $"PurchaseOrder {request.PurchaseOrderId} out of tenant scope.");

        // Only post-approval POs are amendable.
        if (po.Status != POStatus.Approved &&
            po.Status != POStatus.Sent &&
            po.Status != POStatus.PartiallyReceived)
            return Result.Failure<DraftAmendmentResult>(
                $"PO status {po.Status} is not amendable (must be Approved / Sent / PartiallyReceived).");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var nowUtc = DateTime.UtcNow;

            // Flip prior IsCurrent amendments to false.
            var priors = await _db.Set<POChangeHistory>()
                .Where(a => a.PurchaseOrderId == po.Id && a.IsCurrent)
                .ToListAsync(ct);
            foreach (var p in priors)
            {
                p.IsCurrent = false;
                p.UpdatedAt = nowUtc;
            }

            // Phase 1: insert header with Guid placeholder for AmendmentNumber.
            var header = new POChangeHistory
            {
                CompanyId = po.CompanyId,
                PurchaseOrderId = po.Id,
                AmendmentNumber = $"TMP-{Guid.NewGuid():N}",
                Status = POAmendmentStatus.Draft,
                Reason = request.Reason,
                ReasonNarrative = request.ReasonNarrative,
                DraftedByUserId = request.DraftedByUserId,
                VendorReAcknowledgmentRequired = request.VendorReAcknowledgmentRequired,
                IsCurrent = true,
                CreatedAt = nowUtc,
            };
            _db.Add(header);

            // Snapshot the original PO line values + persist the proposed new values.
            var draftedLines = 0;
            foreach (var draft in request.Lines)
            {
                // Codex P2 (PRRT_kwDOSSj3Wc6Fi9FQ): NewLine drafts allow null
                // PurchaseOrderLineId but Apply doesn't yet create a fresh
                // PurchaseOrderLine on commit (would require Description /
                // UOM / ItemId / GLAccountId etc. on the DTO). Reject NewLine
                // at Draft time so the buyer gets a clean message instead of
                // a silent no-op at Apply. Tracked for a v2 enhancement that
                // accepts a richer AmendmentNewLineDraft DTO.
                if (draft.ChangeType == POAmendmentLineChangeType.NewLine)
                    return await FailureAsync(tx, ct,
                        "NewLine amendments are not supported in this revision " +
                        "(requires v2 DTO with Description/UOM/ItemId). Use a " +
                        "separate PurchasingService.AddLineAsync call to add the " +
                        "line first, then amend its qty/price/date.");

                PurchaseOrderLine? poLine = null;
                if (draft.PurchaseOrderLineId.HasValue)
                {
                    poLine = po.Lines.FirstOrDefault(l => l.Id == draft.PurchaseOrderLineId.Value);
                    if (poLine == null)
                        return await FailureAsync(tx, ct,
                            $"PO line {draft.PurchaseOrderLineId.Value} not on PO #{po.Id}.");
                }
                else
                {
                    return await FailureAsync(tx, ct,
                        "Amendment lines must include a PurchaseOrderLineId.");
                }

                // P2-5 fix: snapshot OriginalPromiseDate from PO header (which
                // actually carries PromiseDate), not from the line's RequiredDate.
                // The audit trail now compares like-to-like.
                var line = new POChangeHistoryLine
                {
                    POChangeHistory = header,
                    PurchaseOrderLineId = draft.PurchaseOrderLineId,
                    ChangeType = draft.ChangeType,
                    OriginalQuantity = poLine?.QuantityOrdered ?? 0m,
                    OriginalUnitPrice = poLine?.UnitPrice ?? 0m,
                    OriginalRequiredDate = poLine?.RequiredDate,
                    OriginalPromiseDate = po.PromiseDate,
                    NewQuantity = draft.NewQuantity,
                    NewUnitPrice = draft.NewUnitPrice,
                    NewPromiseDate = draft.NewPromiseDate,
                    NewRequiredDate = draft.NewRequiredDate,
                    LineNarrative = draft.LineNarrative,
                    CreatedAt = nowUtc,
                };
                _db.Add(line);
                draftedLines++;
            }

            await _db.SaveChangesAsync(ct);

            // Phase 2: stamp human-readable AmendmentNumber.
            header.AmendmentNumber = BuildAmendmentNumber(nowUtc.Year, header.Id);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result.Success(new DraftAmendmentResult(
                header.Id,
                header.AmendmentNumber,
                draftedLines,
                header.Status,
                $"Drafted {header.AmendmentNumber} on PO #{po.Id} ({po.PONumber}) " +
                $"with {draftedLines} line change(s)."));
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbex)
            when (IsPostgresUniqueViolation(dbex))
        {
            // P1-3 fix: concurrent Draft on same PO races the filtered unique
            // index UX_POChangeHistories_PurchaseOrderId_IsCurrent — one
            // commits, the loser hits 23505. Return a clean retry message
            // instead of leaking the Postgres error.
            await tx.RollbackAsync(ct);
            _logger.LogWarning(
                "DraftAmendmentAsync concurrent race on PO {PurchaseOrderId} — IsCurrent unique violation",
                request.PurchaseOrderId);
            return Result.Failure<DraftAmendmentResult>(
                "Another amendment is already in progress on this PO. Refresh and retry.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex,
                "DraftAmendmentAsync failed for PO {PurchaseOrderId}", request.PurchaseOrderId);
            return Result.Failure<DraftAmendmentResult>(
                $"Failed to draft amendment: {ex.Message}");
        }
    }

    private static bool IsPostgresUniqueViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        // Walk the inner exception chain for a Npgsql.PostgresException with SqlState 23505.
        for (var e = ex.InnerException; e != null; e = e.InnerException)
        {
            var sqlStateProp = e.GetType().GetProperty("SqlState");
            if (sqlStateProp?.GetValue(e) is string sqlState && sqlState == "23505")
                return true;
        }
        return false;
    }

    // P2-4 fix: replaced sync-over-async Failure helper with FailureAsync.
    private static async Task<Result<DraftAmendmentResult>> FailureAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        CancellationToken ct, string msg)
    {
        await tx.RollbackAsync(ct);
        return Result.Failure<DraftAmendmentResult>(msg);
    }

    private static string BuildAmendmentNumber(int year, int id)
        => $"POAMD-{year:0000}-{id:000000}";

    // ═══════════════════════════════════════════════════════════════════════
    // 2) PreviewAmendmentImpactAsync — THE BIC DIFFERENTIATOR
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<AmendmentImpactReport>> PreviewAmendmentImpactAsync(
        int poChangeHistoryId, CancellationToken ct = default)
    {
        var amendment = await LoadAmendmentWithLinesAsync(poChangeHistoryId, ct);
        if (amendment == null)
            return Result.Failure<AmendmentImpactReport>(
                $"Amendment {poChangeHistoryId} not found or out of tenant scope.");

        // Re-running on already-applied or rejected amendments is allowed (read-only re-compute);
        // we just won't bump Status if it's already terminal.
        if (amendment.Status == POAmendmentStatus.Applied ||
            amendment.Status == POAmendmentStatus.Rejected ||
            amendment.Status == POAmendmentStatus.Cancelled)
        {
            // For terminal states, return the cached impact (header counts + per-line rows).
            return Result.Success(BuildReportFromCache(amendment));
        }

        var po = await _db.Set<PurchaseOrder>()
            .Where(p => p.Id == amendment.PurchaseOrderId)
            .Select(p => new { p.Id, p.PONumber })
            .FirstOrDefaultAsync(ct);

        // Pull all affected demand links for the PO lines this amendment touches.
        var poLineIds = amendment.Lines
            .Where(l => l.PurchaseOrderLineId.HasValue)
            .Select(l => l.PurchaseOrderLineId!.Value)
            .ToList();

        var demandLinks = await _db.Set<PurchaseOrderLineDemandLink>()
            .Where(d => poLineIds.Contains(d.PurchaseOrderLineId) &&
                        d.Status != PoDemandLinkStatus.Released &&
                        d.Status != PoDemandLinkStatus.Cancelled)
            .ToListAsync(ct);

        // Pull PRO numbers for affected production orders.
        var proIds = demandLinks.Select(d => d.ProductionOrderId).Distinct().ToList();
        var proNumberLookup = await _db.Set<ProductionOrder>()
            .Where(p => proIds.Contains(p.Id))
            .Select(p => new { p.Id, p.OrderNumber })
            .ToDictionaryAsync(p => p.Id, p => p.OrderNumber, ct);

        // Build per-line rows + per-line impact counts.
        var rows = new List<AmendmentImpactRow>();
        var totalValueDelta = 0m;
        var totalQuantityDelta = 0m;
        var globalProSet = new HashSet<int>();
        var globalOpSet = new HashSet<(int ProId, int Op)>();
        var shipDateRisk = false;

        foreach (var amLine in amendment.Lines)
        {
            if (!amLine.PurchaseOrderLineId.HasValue) continue; // new lines have no current links

            var linksForLine = demandLinks
                .Where(d => d.PurchaseOrderLineId == amLine.PurchaseOrderLineId.Value)
                .ToList();

            var proSetThisLine = new HashSet<int>();
            int? worstPushOut = null;
            foreach (var link in linksForLine)
            {
                var pushOutDays = ComputePushOutDays(
                    currentPromise: link.PromiseDate,
                    newPromise: amLine.NewPromiseDate,
                    needBy: link.NeedByDate);
                if (pushOutDays.HasValue && (!worstPushOut.HasValue || pushOutDays > worstPushOut))
                    worstPushOut = pushOutDays;

                proSetThisLine.Add(link.ProductionOrderId);
                globalProSet.Add(link.ProductionOrderId);
                if (link.OperationSequence.HasValue)
                    globalOpSet.Add((link.ProductionOrderId, link.OperationSequence.Value));

                var atRisk = pushOutDays.GetValueOrDefault() > 0 && link.NeedByDate.HasValue;
                if (atRisk) shipDateRisk = true;

                proNumberLookup.TryGetValue(link.ProductionOrderId, out var proNumber);
                rows.Add(new AmendmentImpactRow(
                    PurchaseOrderLineId: amLine.PurchaseOrderLineId.Value,
                    PoLineNumber: 0, // populated below from PO line denorm if available
                    ProductionSupplyDemandId: link.ProductionSupplyDemandId,
                    ProductionOrderId: link.ProductionOrderId,
                    ProductionOrderNumber: proNumber,
                    BomLineId: link.BomLineId,
                    OperationSequence: link.OperationSequence,
                    AllocatedQuantity: link.AllocatedQuantity,
                    RemainingQuantity: link.RemainingQuantity,
                    CurrentPromiseDate: link.PromiseDate,
                    NewPromiseDate: amLine.NewPromiseDate,
                    NeedByDate: link.NeedByDate,
                    PushOutDays: pushOutDays,
                    ShipDateAtRisk: atRisk,
                    Narrative: BuildLineNarrative(amLine, link, pushOutDays)));
            }

            var lineValueDelta =
                (amLine.NewQuantity * amLine.NewUnitPrice) -
                (amLine.OriginalQuantity * amLine.OriginalUnitPrice);
            var lineQtyDelta = amLine.NewQuantity - amLine.OriginalQuantity;

            amLine.AffectedDemandLinkCount = linksForLine.Count;
            amLine.AffectedProductionOrderCount = proSetThisLine.Count;
            amLine.AffectedProductionOrderNumbers = string.Join(", ",
                proSetThisLine
                    .Where(proNumberLookup.ContainsKey)
                    .Select(id => proNumberLookup[id])
                    .Take(10));
            amLine.MaxDatePushOutDays = worstPushOut;
            amLine.ValueDelta = lineValueDelta;
            amLine.QuantityDelta = lineQtyDelta;
            amLine.UpdatedAt = DateTime.UtcNow;

            totalValueDelta += lineValueDelta;
            totalQuantityDelta += lineQtyDelta;
        }

        // Cache aggregate counts on the header.
        amendment.AffectedDemandLinkCount = demandLinks.Count;
        amendment.AffectedProductionOrderCount = globalProSet.Count;
        amendment.AffectedOperationCount = globalOpSet.Count;
        amendment.ShipDateRiskFlag = shipDateRisk;
        amendment.TotalValueDelta = totalValueDelta;
        amendment.TotalQuantityDelta = totalQuantityDelta;
        amendment.ImpactNarrative = BuildHeaderNarrative(
            amendment, demandLinks.Count, globalProSet.Count, globalOpSet.Count,
            shipDateRisk, totalValueDelta, totalQuantityDelta);
        amendment.PreviewedAtUtc = DateTime.UtcNow;
        amendment.UpdatedAt = DateTime.UtcNow;
        if (amendment.Status == POAmendmentStatus.Draft)
            amendment.Status = POAmendmentStatus.Previewed;

        await _db.SaveChangesAsync(ct);

        return Result.Success(new AmendmentImpactReport(
            POChangeHistoryId: amendment.Id,
            AmendmentNumber: amendment.AmendmentNumber,
            PurchaseOrderId: amendment.PurchaseOrderId,
            PurchaseOrderNumber: po?.PONumber,
            LinesChanged: amendment.Lines.Count,
            AffectedDemandLinks: demandLinks.Count,
            AffectedProductionOrders: globalProSet.Count,
            AffectedOperations: globalOpSet.Count,
            ShipDateRiskFlag: shipDateRisk,
            TotalValueDelta: totalValueDelta,
            TotalQuantityDelta: totalQuantityDelta,
            ImpactNarrative: amendment.ImpactNarrative,
            Rows: rows));
    }

    private static int? ComputePushOutDays(
        DateTime? currentPromise, DateTime? newPromise, DateTime? needBy)
    {
        if (!currentPromise.HasValue || !newPromise.HasValue) return null;
        var delta = (newPromise.Value.Date - currentPromise.Value.Date).Days;
        return delta;
    }

    private static string BuildLineNarrative(
        POChangeHistoryLine amLine,
        PurchaseOrderLineDemandLink link,
        int? pushOutDays)
    {
        var sb = new StringBuilder();
        if (amLine.NewQuantity != amLine.OriginalQuantity)
            sb.Append($"Qty {amLine.OriginalQuantity}→{amLine.NewQuantity}. ");
        if (amLine.NewUnitPrice != amLine.OriginalUnitPrice)
            sb.Append($"Price {amLine.OriginalUnitPrice:N2}→{amLine.NewUnitPrice:N2}. ");
        if (pushOutDays.HasValue && pushOutDays.Value != 0)
            sb.Append($"Promise {(pushOutDays > 0 ? "+" : "")}{pushOutDays}d. ");
        if (pushOutDays.GetValueOrDefault() > 0 && link.NeedByDate.HasValue)
            sb.Append("⚠ Past NeedBy. ");
        return sb.Length == 0 ? "No measurable line-level impact" : sb.ToString().TrimEnd();
    }

    private static string BuildHeaderNarrative(
        POChangeHistory amendment,
        int linkCount, int proCount, int opCount,
        bool risk, decimal valueDelta, decimal qtyDelta)
    {
        var risksTag = risk ? " ⚠ SHIP-DATE RISK" : "";
        return $"Amendment {amendment.AmendmentNumber}: " +
               $"{amendment.Lines.Count} line(s) changed, " +
               $"{linkCount} demand link(s) across {proCount} PRO(s) ({opCount} op(s)). " +
               $"Δ qty {qtyDelta:N2}, Δ value {valueDelta:N2}.{risksTag}";
    }

    private static AmendmentImpactReport BuildReportFromCache(POChangeHistory a)
    {
        return new AmendmentImpactReport(
            POChangeHistoryId: a.Id,
            AmendmentNumber: a.AmendmentNumber,
            PurchaseOrderId: a.PurchaseOrderId,
            PurchaseOrderNumber: a.PurchaseOrder?.PONumber,
            LinesChanged: a.Lines.Count,
            AffectedDemandLinks: a.AffectedDemandLinkCount,
            AffectedProductionOrders: a.AffectedProductionOrderCount,
            AffectedOperations: a.AffectedOperationCount,
            ShipDateRiskFlag: a.ShipDateRiskFlag,
            TotalValueDelta: a.TotalValueDelta,
            TotalQuantityDelta: a.TotalQuantityDelta,
            ImpactNarrative: a.ImpactNarrative,
            Rows: Array.Empty<AmendmentImpactRow>());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3) SubmitForApprovalAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POChangeHistory>> SubmitForApprovalAsync(
        int poChangeHistoryId, CancellationToken ct = default)
    {
        var a = await LoadAmendmentAsync(poChangeHistoryId, ct);
        if (a == null)
            return Result.Failure<POChangeHistory>("Amendment not found or out of scope.");
        if (!a.IsCurrent)
            return Result.Failure<POChangeHistory>("Amendment is no longer current.");
        if (a.Status != POAmendmentStatus.Previewed && a.Status != POAmendmentStatus.Draft)
            return Result.Failure<POChangeHistory>(
                $"Cannot submit from status {a.Status}.");

        a.Status = POAmendmentStatus.PendingApproval;
        a.SubmittedForApprovalAtUtc = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(a);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4) ApproveAmendmentAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POChangeHistory>> ApproveAmendmentAsync(
        ApproveAmendmentRequest request, CancellationToken ct = default)
    {
        var a = await LoadAmendmentAsync(request.POChangeHistoryId, ct);
        if (a == null)
            return Result.Failure<POChangeHistory>("Amendment not found or out of scope.");
        if (!a.IsCurrent)
            return Result.Failure<POChangeHistory>("Amendment is no longer current.");
        if (a.Status != POAmendmentStatus.Previewed &&
            a.Status != POAmendmentStatus.PendingApproval)
            return Result.Failure<POChangeHistory>(
                $"Cannot approve from status {a.Status} — preview the amendment first.");

        a.Status = POAmendmentStatus.Approved;
        a.ApprovedByUserId = request.ApproverUserId;
        a.ApprovedAtUtc = DateTime.UtcNow;
        a.ApprovalNote = request.ApprovalNote;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(a);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5) RejectAmendmentAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POChangeHistory>> RejectAmendmentAsync(
        RejectAmendmentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<POChangeHistory>("Reason required for rejection.");

        var a = await LoadAmendmentAsync(request.POChangeHistoryId, ct);
        if (a == null)
            return Result.Failure<POChangeHistory>("Amendment not found or out of scope.");
        if (!a.IsCurrent)
            return Result.Failure<POChangeHistory>("Amendment is no longer current.");
        if (a.Status == POAmendmentStatus.Applied ||
            a.Status == POAmendmentStatus.Rejected ||
            a.Status == POAmendmentStatus.Cancelled)
            return Result.Failure<POChangeHistory>(
                $"Cannot reject from terminal status {a.Status}.");

        a.Status = POAmendmentStatus.Rejected;
        a.RejectionReason = request.Reason;
        a.ClosedAtUtc = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(a);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6) ApplyAmendmentAsync — THE ATOMIC CORE OF THE ENHANCEMENT
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<ApplyAmendmentResult>> ApplyAmendmentAsync(
        int poChangeHistoryId, CancellationToken ct = default)
    {
        var a = await LoadAmendmentWithLinesAsync(poChangeHistoryId, ct);
        if (a == null)
            return Result.Failure<ApplyAmendmentResult>(
                "Amendment not found or out of scope.");
        if (!a.IsCurrent)
            return Result.Failure<ApplyAmendmentResult>(
                "Amendment is no longer current.");
        if (a.Status != POAmendmentStatus.Approved)
            return Result.Failure<ApplyAmendmentResult>(
                $"Cannot apply from status {a.Status} — must be Approved.");

        // P2-6 fix: the BIC differentiator is "impact visible BEFORE approval".
        // Reject Apply on amendments that bypassed preview — defends against
        // a future code path that approves without preview.
        if (a.PreviewedAtUtc == null)
            return Result.Failure<ApplyAmendmentResult>(
                "Cannot apply — amendment must be previewed before approval. " +
                "Run PreviewAmendmentImpactAsync first.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var nowUtc = DateTime.UtcNow;
            var po = await _db.Set<PurchaseOrder>()
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == a.PurchaseOrderId, ct);
            if (po == null)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure<ApplyAmendmentResult>("Parent PO vanished.");
            }

            int poLinesUpdated = 0;
            int demandLinksResynced = 0;
            int allocationsResynced = 0;

            foreach (var amLine in a.Lines)
            {
                if (!amLine.PurchaseOrderLineId.HasValue) continue;

                var poLine = po.Lines.FirstOrDefault(l => l.Id == amLine.PurchaseOrderLineId.Value);
                if (poLine == null) continue;

                if (amLine.ChangeType == POAmendmentLineChangeType.RemovedLine)
                {
                    // Codex P2 (PRRT_kwDOSSj3Wc6Fi9FO): close the PO line AND
                    // release any unreceived demand links. Otherwise the
                    // links continue pointing at a closed PO line with stale
                    // AllocatedQuantity, lying about supply state.
                    poLine.IsClosed = true;
                    var orphanedLinks = await _db.Set<PurchaseOrderLineDemandLink>()
                        .Where(d => d.PurchaseOrderLineId == poLine.Id &&
                                    d.Status != PoDemandLinkStatus.Released &&
                                    d.Status != PoDemandLinkStatus.Cancelled)
                        .ToListAsync(ct);
                    foreach (var orphan in orphanedLinks)
                    {
                        // Unreceived → release; partially received → keep
                        // received qty so receipt history isn't lost.
                        if (orphan.ReceivedQuantity == 0m)
                        {
                            orphan.AllocatedQuantity = 0m;
                            orphan.RemainingQuantity = 0m;
                            orphan.Status = PoDemandLinkStatus.Released;
                            orphan.ReleasedUtc = nowUtc;
                        }
                        else
                        {
                            orphan.AllocatedQuantity = orphan.ReceivedQuantity;
                            orphan.RemainingQuantity = 0m;
                        }
                        demandLinksResynced++;
                    }
                    poLinesUpdated++;
                    continue;
                }

                poLine.QuantityOrdered = amLine.NewQuantity;
                poLine.UnitPrice = amLine.NewUnitPrice;
                poLine.LineTotal = amLine.NewQuantity * amLine.NewUnitPrice;
                if (amLine.NewRequiredDate.HasValue)
                    poLine.RequiredDate = amLine.NewRequiredDate;
                poLinesUpdated++;

                // Re-sync demand links for this PO line.
                var links = await _db.Set<PurchaseOrderLineDemandLink>()
                    .Where(d => d.PurchaseOrderLineId == poLine.Id &&
                                d.Status != PoDemandLinkStatus.Released &&
                                d.Status != PoDemandLinkStatus.Cancelled)
                    .ToListAsync(ct);

                // Strategy: scale each link's AllocatedQuantity proportionally to the new total qty.
                // Conservative — preserves consolidation share; never below ReceivedQuantity.
                // P1-2 fix: when NewQuantity = 0 (cancel-via-amend), release any
                // unreceived link rather than silently leaving it at
                // AllocatedQuantity = ReceivedQuantity, which would lie about
                // supply state. Links with receipts stay at the received amount
                // and are kept active.
                var oldTotal = amLine.OriginalQuantity > 0 ? amLine.OriginalQuantity : 1m;
                var ratio = amLine.NewQuantity / oldTotal;
                foreach (var link in links)
                {
                    var rescaled = Math.Max(link.ReceivedQuantity, link.AllocatedQuantity * ratio);
                    if (amLine.NewQuantity == 0m && link.ReceivedQuantity == 0m)
                    {
                        // Cancel-via-amend with no receipts on this link → release.
                        link.AllocatedQuantity = 0m;
                        link.RemainingQuantity = 0m;
                        link.Status = PoDemandLinkStatus.Released;
                        link.ReleasedUtc = nowUtc;
                    }
                    else
                    {
                        link.AllocatedQuantity = rescaled;
                        link.RemainingQuantity = Math.Max(0m, rescaled - link.ReceivedQuantity);
                    }
                    if (amLine.NewPromiseDate.HasValue)
                        link.PromiseDate = amLine.NewPromiseDate;
                    demandLinksResynced++;
                }

                // Mirror to ProductionSupplyAllocation (the generic M:M table).
                // SupplyType=PurchaseOrderLine + SupplyRecordId=poLine.Id is the
                // canonical join key per PR-3's PoLineDemandLinkService mirror.
                var psaIds = links.Select(l => l.ProductionSupplyDemandId).ToList();
                if (psaIds.Count > 0)
                {
                    var allocations = await _db.Set<ProductionSupplyAllocation>()
                        .Where(s => psaIds.Contains(s.ProductionSupplyDemandId) &&
                                    s.SupplyType == AllocationSupplyType.PurchaseOrderLine &&
                                    s.SupplyRecordId == poLine.Id)
                        .ToListAsync(ct);
                    foreach (var alloc in allocations)
                    {
                        // Find the matching link by demand id to copy its new qty + promise date.
                        var matchingLink = links.FirstOrDefault(l =>
                            l.ProductionSupplyDemandId == alloc.ProductionSupplyDemandId);
                        if (matchingLink != null)
                        {
                            alloc.AllocatedQuantity = matchingLink.AllocatedQuantity;
                            alloc.RemainingQuantity = matchingLink.RemainingQuantity;
                            if (matchingLink.PromiseDate.HasValue)
                                alloc.PromiseDate = matchingLink.PromiseDate;
                            allocationsResynced++;
                        }
                    }
                }
            }

            // Recompute PO header totals.
            po.Subtotal = po.Lines.Where(l => !l.IsClosed).Sum(l => l.LineTotal);
            po.Total = po.Subtotal + po.TaxAmount + po.ShippingAmount;

            // Vendor re-ack hook (PR-16 ↔ PR-17 integration).
            int? newAckId = null;
            string? newAckNumber = null;
            bool ackFlipped = false;
            if (a.VendorReAcknowledgmentRequired)
            {
                // RequestAcknowledgmentAsync flips priors; we just need to call it.
                var ackResult = await _ackService.RequestAcknowledgmentAsync(
                    new RequestAcknowledgmentRequest(
                        PurchaseOrderId: po.Id,
                        RequestedMethod: POAcknowledgmentMethod.VendorPortal,
                        ResponseDueByUtc: nowUtc.AddDays(7),
                        RequestedByUserId: a.ApprovedByUserId,
                        BuyerNotes:
                            $"Auto-opened by amendment {a.AmendmentNumber} apply " +
                            $"({a.Reason}) — vendor must re-acknowledge."),
                    ct);
                if (ackResult.IsSuccess)
                {
                    newAckId = ackResult.Value!.POAcknowledgmentId;
                    newAckNumber = ackResult.Value!.AcknowledgmentNumber;
                    ackFlipped = true;
                }
                else
                {
                    _logger.LogWarning(
                        "Vendor re-ack failed during amendment {AmendmentNumber} apply: {Error}",
                        a.AmendmentNumber, ackResult.Error);
                    // Non-fatal: amendment still applies, but flag so probe can show.
                }
            }

            a.Status = POAmendmentStatus.Applied;
            a.AppliedAtUtc = nowUtc;
            a.ClosedAtUtc = nowUtc;
            a.UpdatedAt = nowUtc;

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result.Success(new ApplyAmendmentResult(
                POChangeHistoryId: a.Id,
                PoLinesUpdated: poLinesUpdated,
                DemandLinksResynced: demandLinksResynced,
                ProductionSupplyAllocationsResynced: allocationsResynced,
                VendorAckFlipped: ackFlipped,
                NewVendorAcknowledgmentId: newAckId,
                NewVendorAcknowledgmentNumber: newAckNumber,
                Status: a.Status,
                Message: $"Amendment {a.AmendmentNumber} applied: " +
                         $"{poLinesUpdated} PO line(s) updated, " +
                         $"{demandLinksResynced} demand link(s) resynced, " +
                         $"{allocationsResynced} allocation(s) mirrored" +
                         (ackFlipped ? $", new vendor ack {newAckNumber} opened" : "") + "."));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex,
                "ApplyAmendmentAsync failed for amendment {POChangeHistoryId}",
                poChangeHistoryId);
            return Result.Failure<ApplyAmendmentResult>(
                $"Failed to apply amendment: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7) CancelAmendmentAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<POChangeHistory>> CancelAmendmentAsync(
        int poChangeHistoryId, string? reason, CancellationToken ct = default)
    {
        var a = await LoadAmendmentAsync(poChangeHistoryId, ct);
        if (a == null)
            return Result.Failure<POChangeHistory>("Amendment not found or out of scope.");
        if (!a.IsCurrent)
            return Result.Failure<POChangeHistory>("Amendment is no longer current.");
        if (a.Status == POAmendmentStatus.Applied ||
            a.Status == POAmendmentStatus.Rejected ||
            a.Status == POAmendmentStatus.Cancelled)
            return Result.Failure<POChangeHistory>(
                $"Cannot cancel from terminal status {a.Status}.");

        a.Status = POAmendmentStatus.Cancelled;
        a.RejectionReason = reason;
        a.ClosedAtUtc = DateTime.UtcNow;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(a);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Read methods
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<POChangeHistory?> GetCurrentAsync(
        int purchaseOrderId, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<POChangeHistory>()
            .Include(a => a.Lines)
            .Where(a => a.PurchaseOrderId == purchaseOrderId &&
                        a.IsCurrent &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<POChangeHistory>> GetHistoryAsync(
        int purchaseOrderId, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<POChangeHistory>()
            .Where(a => a.PurchaseOrderId == purchaseOrderId &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<PoAmendmentSummary> GetSummaryAsync(
        int purchaseOrderId, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        var history = await _db.Set<POChangeHistory>()
            .Include(a => a.Lines)
            .Where(a => a.PurchaseOrderId == purchaseOrderId &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .ToListAsync(ct);
        var current = history.FirstOrDefault(a => a.IsCurrent);
        if (current == null)
        {
            return new PoAmendmentSummary(
                null, null, null, null,
                history.Count, 0, 0, 0, false, 0m);
        }
        return new PoAmendmentSummary(
            CurrentAmendmentId: current.Id,
            CurrentAmendmentNumber: current.AmendmentNumber,
            CurrentStatus: current.Status,
            CurrentReason: current.Reason,
            TotalAmendmentsInHistory: history.Count,
            LinesInCurrent: current.Lines.Count,
            AffectedDemandLinksInCurrent: current.AffectedDemandLinkCount,
            AffectedProductionOrdersInCurrent: current.AffectedProductionOrderCount,
            ShipDateRiskFlag: current.ShipDateRiskFlag,
            TotalValueDelta: current.TotalValueDelta);
    }

    public async Task<Result<AmendmentImpactReport>> GetImpactReportAsync(
        int poChangeHistoryId, CancellationToken ct = default)
    {
        var a = await LoadAmendmentWithLinesAsync(poChangeHistoryId, ct);
        if (a == null)
            return Result.Failure<AmendmentImpactReport>(
                $"Amendment {poChangeHistoryId} not found or out of scope.");
        return Result.Success(BuildReportFromCache(a));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<POChangeHistory?> LoadAmendmentAsync(int id, CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<POChangeHistory>()
            .Where(a => a.Id == id &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<POChangeHistory?> LoadAmendmentWithLinesAsync(int id, CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<POChangeHistory>()
            .Include(a => a.Lines)
            .Where(a => a.Id == id &&
                        a.CompanyId != null &&
                        visible.Contains(a.CompanyId.Value))
            .FirstOrDefaultAsync(ct);
    }
}
