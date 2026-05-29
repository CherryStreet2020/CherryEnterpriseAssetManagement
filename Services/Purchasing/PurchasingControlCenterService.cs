// =============================================================================
// CherryAI EAM — PurchasingControlCenterService (Sprint 15.3 PR-10)
//
// Implementation behind the §7 + §21 Purchasing Control Center surface.
//
// Reads from:
//   * ProductionSupplyDemand           (Sprint 15.1 PR-2)
//   * ProductionSupplyAllocation       (Sprint 15.1 PR-2)
//   * PurchaseOrder + PurchaseOrderLine (legacy substrate)
//   * VendorWipBalance                  (Sprint 15.1 PR-5)
//   * SubcontractOperation              (Sprint 15.1 PR-4)
//   * GoodsReceipt                      (legacy)
//
// Writes:
//   * BuyerActionState + audit columns on ProductionSupplyDemand.
//
// No new entity. State machine lives in a static dispatch table; transitions
// are guarded by AllowedFrom checks. Tenant-scoped via ITenantContext on
// every read and write.
// =============================================================================

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

namespace Abs.FixedAssets.Services.Purchasing;

public class PurchasingControlCenterService : IPurchasingControlCenterService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPurchasingRecommendationService? _recommendations;
    private readonly ISupplierPerformanceService? _supplierPerformance;
    private readonly IInvoiceMatchService? _invoiceMatch;
    private readonly ILogger<PurchasingControlCenterService> _log;

    public PurchasingControlCenterService(
        AppDbContext db,
        ITenantContext tenant,
        ILogger<PurchasingControlCenterService> log,
        IPurchasingRecommendationService? recommendations = null,
        ISupplierPerformanceService? supplierPerformance = null,
        IInvoiceMatchService? invoiceMatch = null)
    {
        _db = db;
        _tenant = tenant;
        _recommendations = recommendations;
        _supplierPerformance = supplierPerformance;
        _invoiceMatch = invoiceMatch;
        _log = log;
    }

    // ════════════════════════════════════════════════════════════════════════
    // KPI BAND — 5 tiles (§21 strip)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<PurchasingKpiBand>> GetKpiBandAsync(
        int? siteId = null,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;

        // Demand counts — exclude terminal states (Closed/Cancelled)
        var demandQ = _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Where(d => visible.Contains(d.CompanyId)
                        && d.BuyerActionState != BuyerActionState.Closed
                        && d.BuyerActionState != BuyerActionState.Cancelled);
        if (siteId.HasValue)
            demandQ = demandQ.Where(d => d.SiteId == siteId);

        var openDemandCount = await demandQ.CountAsync(ct);

        // Committed supply value — for demands whose linked PO line is in an
        // ACTIVE (non-Cancelled, non-Closed) PO, sum UnitPrice * (Required - Received).
        // Renamed from "OpenDemandTotalValueUsd" per subagent review #4 — the
        // tile name now matches the math (committed supply, not all open demand).
        // The PO-side filter additionally enforces tenant scope as defense-in-depth.
        var committedSupplyValueUsd = await (
            from d in demandQ
            where d.LinkedPurchaseOrderLineId != null
            join l in _db.Set<PurchaseOrderLine>().AsNoTracking()
                on d.LinkedPurchaseOrderLineId equals l.Id
            join p in _db.Set<PurchaseOrder>().AsNoTracking()
                on l.PurchaseOrderId equals p.Id
            where p.CompanyId != null
                  && visible.Contains(p.CompanyId.Value)
                  && p.Status != POStatus.Cancelled
                  && p.Status != POStatus.Closed
            select (decimal?)(l.UnitPrice * (d.RequiredQuantity - d.ReceivedQuantity))
        ).SumAsync(v => v ?? 0m, ct);

        // PO counts — Approved/Sent/PartiallyReceived are "open"
        var poQ = _db.Set<PurchaseOrder>()
            .AsNoTracking()
            .Where(p => p.CompanyId != null
                        && visible.Contains(p.CompanyId.Value)
                        && (p.Status == POStatus.Approved
                            || p.Status == POStatus.Sent
                            || p.Status == POStatus.PartiallyReceived));
        if (siteId.HasValue)
            poQ = poQ.Where(p => p.ShipToSiteId == siteId);

        var openPoCount = await poQ.CountAsync(ct);
        var openPoValueUsd = await poQ.SumAsync(p => (decimal?)p.Total, ct) ?? 0m;

        // Vendor WIP value — honor siteId per Codex thread #1 (P2). VendorWipBalance
        // has SiteId; without this filter a site-scoped KPI request shows WIP from
        // other sites and the tile becomes inconsistent with the rest of the band.
        var wipQ = _db.Set<VendorWipBalance>()
            .AsNoTracking()
            .Where(w => visible.Contains(w.CompanyId)
                        && w.QuantityAtVendor > 0);
        if (siteId.HasValue)
            wipQ = wipQ.Where(w => w.SiteId == siteId);
        var vendorWipValueUsd = await wipQ.SumAsync(w => (decimal?)w.TotalValueAtVendor, ct) ?? 0m;

        // Late POs — PromiseDate or RequiredDate in past AND still open
        var today = DateTime.UtcNow.Date;
        var latePoCount = await poQ
            .Where(p => (p.PromiseDate != null && p.PromiseDate < today)
                        || (p.PromiseDate == null && p.RequiredDate != null && p.RequiredDate < today))
            .CountAsync(ct);

        // Missing supply count — demands with NotSupplied and not blocked/cancelled
        var missingSupplyCount = await demandQ
            .Where(d => d.SupplyStatus == DemandSupplyStatus.NotSupplied
                        && d.BuyerActionState != BuyerActionState.Blocked)
            .CountAsync(ct);

        return Result.Success(new PurchasingKpiBand(
            OpenDemandCount: openDemandCount,
            CommittedSupplyValueUsd: committedSupplyValueUsd,
            OpenPoCount: openPoCount,
            OpenPoTotalValueUsd: openPoValueUsd,
            VendorWipTotalValueUsd: vendorWipValueUsd,
            LatePoCount: latePoCount,
            MissingSupplyDemandCount: missingSupplyCount,
            SnapshotUtc: DateTime.UtcNow));
    }

    // ════════════════════════════════════════════════════════════════════════
    // SUPPLY DEMAND QUEUE — 13 types
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<PurchasingQueuePage>> GetSupplyDemandQueueAsync(
        PurchasingQueueType queueType,
        PurchasingQueueFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var today = DateTime.UtcNow.Date;

        // Base query — visible tenants only
        var q = _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Where(d => visible.Contains(d.CompanyId));

        // Common filter application
        if (filter.CompanyId.HasValue)
            q = q.Where(d => d.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue)
            q = q.Where(d => d.SiteId == filter.SiteId);
        if (filter.BuyerUserId.HasValue)
            q = q.Where(d => d.BuyerUserId == filter.BuyerUserId);
        if (filter.VendorId.HasValue)
            q = q.Where(d => d.VendorId == filter.VendorId);
        if (filter.ProductionOrderId.HasValue)
            q = q.Where(d => d.ProductionOrderId == filter.ProductionOrderId);
        if (filter.RequiredBefore.HasValue)
            q = q.Where(d => d.RequiredDate != null && d.RequiredDate < filter.RequiredBefore);
        if (!filter.IncludeClosedAndCancelled)
            q = q.Where(d => d.BuyerActionState != BuyerActionState.Closed
                             && d.BuyerActionState != BuyerActionState.Cancelled);

        // Queue-type dispatch — each clause is the filter for that §7 lane
        q = queueType switch
        {
            PurchasingQueueType.SupplyDemand =>
                q.Where(d => d.SupplyStatus != DemandSupplyStatus.FullyFulfilled
                             && d.SupplyStatus != DemandSupplyStatus.Closed),
            PurchasingQueueType.BuyToJob =>
                q.Where(d => (d.SupplyPolicy == SupplyPolicy.BuyDirectToJob
                              || d.SupplyPolicy == SupplyPolicy.BuyDirectToOperation)
                             && d.LinkedPurchaseOrderId == null),
            PurchasingQueueType.BuyToInventory =>
                q.Where(d => d.SupplyPolicy == SupplyPolicy.InventoryFirstThenBuy
                             && d.SupplyStatus == DemandSupplyStatus.NotSupplied),
            PurchasingQueueType.Subcontract =>
                q.Where(d => d.SourceType == DemandSourceType.Subcontract
                             || d.SupplyPolicy == SupplyPolicy.BuyToVendorLocation),
            PurchasingQueueType.VendorWip =>
                q.Where(d => d.SupplyStatus == DemandSupplyStatus.AtVendor),
            // Late = either the demand RequiredDate has passed OR the linked PO's
            // own PromiseDate has slipped past today, regardless of demand date.
            // (Subagent review #3 — RequiredDate-only filter missed supplier
            // commitments that slipped after PO acknowledgment.)
            PurchasingQueueType.LatePoLines =>
                q.Where(d => d.LinkedPurchaseOrderId != null
                             && d.SupplyStatus != DemandSupplyStatus.FullyFulfilled
                             && ((d.RequiredDate != null && d.RequiredDate < today)
                                 || (d.LinkedPurchaseOrder!.PromiseDate != null
                                     && d.LinkedPurchaseOrder.PromiseDate < today))),
            // §7: "PO lines without confirmed promise dates from supplier."
            // (Subagent review #1 P1 — original filter matched EVERY committed PO,
            // exactly opposite of spec. Now requires the linked PO's PromiseDate
            // to actually be NULL.)
            PurchasingQueueType.MissingSupplierPromise =>
                q.Where(d => d.LinkedPurchaseOrderId != null
                             && d.SupplyStatus == DemandSupplyStatus.Committed
                             && d.LinkedPurchaseOrder!.PromiseDate == null),
            PurchasingQueueType.InspectionBlocked =>
                q.Where(d => d.SupplyStatus == DemandSupplyStatus.InInspection
                             || d.ShortageStatus == DemandShortageStatus.OnHold),
            PurchasingQueueType.ChildWoSupplyRisk =>
                q.Where(d => d.SupplyPolicy == SupplyPolicy.MakeDirectToJob
                             && d.LinkedChildProductionOrderId != null
                             && d.ShortageStatus >= DemandShortageStatus.Critical),
            PurchasingQueueType.NoSourceDemand =>
                q.Where(d => d.SourceStatus == DemandSourceStatus.NotDetermined
                             && d.VendorId == null),
            PurchasingQueueType.CostExceptions =>
                q.Where(d => d.CostStatus == DemandCostStatus.VariancePending),
            PurchasingQueueType.ApprovalRequired =>
                q.Where(d => d.BuyerActionState == BuyerActionState.AwaitingApproval),
            PurchasingQueueType.ExpediteRequired =>
                q.Where(d => d.ShortageStatus == DemandShortageStatus.Late
                             || d.ShortageStatus == DemandShortageStatus.Critical),
            PurchasingQueueType.ProjectCriticalSupply =>
                q.Where(d => d.ProjectId != null
                             && (d.ShortageStatus == DemandShortageStatus.Critical
                                 || d.ShortageStatus == DemandShortageStatus.Late
                                 || d.AlertStatus == DemandAlertStatus.Critical)),
            _ => q,
        };

        var totalCount = await q.CountAsync(ct);

        // Take a page + join the PRO + linked PO for the row projection
        var paged = await q
            .OrderBy(d => d.RequiredDate ?? DateTime.MaxValue)
            .ThenBy(d => d.Id)
            .Skip(Math.Max(0, filter.Skip))
            .Take(Math.Clamp(filter.Take, 1, 500))
            .Select(d => new
            {
                Demand = d,
                PoNumber = d.LinkedPurchaseOrder != null ? d.LinkedPurchaseOrder.PONumber : null,
                PromiseDate = d.LinkedPurchaseOrder != null ? d.LinkedPurchaseOrder.PromiseDate : null,
                ProNumber = d.ProductionOrder != null ? d.ProductionOrder.OrderNumber : null,
            })
            .ToListAsync(ct);

        var rows = paged.Select(p =>
        {
            var d = p.Demand;
            var open = d.RequiredQuantity - d.ReceivedQuantity;
            int? daysLate = null;
            if (d.RequiredDate.HasValue && d.RequiredDate.Value.Date < today)
                daysLate = (today - d.RequiredDate.Value.Date).Days;

            // (Sprint 15.3 PR-15) Real §18 recommendation hint when the
            // recommendation service is registered. Falls back to the
            // PR-10 placeholder when running in legacy/test composition.
            //
            // The Supply Demand queue doesn't separately hydrate the linked
            // PO number per row (the page only renders LinkedPurchaseOrderId
            // as a chip). Pass null for linkedPoNumber; the recommendation
            // reason strings reference the PO by status, not by number.
            string hint;
            if (_recommendations != null)
            {
                var rec = _recommendations.BuildFromDemand(
                    d,
                    productionOrderNumber: p.ProNumber,
                    linkedPoNumber: null,
                    suggestedVendorName: null);
                hint = $"{rec.ActionLabel}: {rec.Reason}";
            }
            else
            {
                hint = SuggestNextAction(d, queueType);
            }

            return new PurchasingQueueRow(
                DemandId: d.Id,
                DemandNumber: d.DemandNumber,
                QueueType: queueType,
                ProductionOrderId: d.ProductionOrderId,
                ProductionOrderNumber: p.ProNumber,
                BomLineId: d.BomLineId,
                OperationSequence: d.OperationSequence,
                ProjectId: d.ProjectId,
                CustomerId: d.CustomerId,
                PartNumber: d.PartNumber,
                Revision: d.Revision,
                Description: d.Description,
                Uom: d.Uom,
                RequiredQuantity: d.RequiredQuantity,
                OpenQuantity: open,
                RequiredDate: d.RequiredDate,
                SupplyPolicy: d.SupplyPolicy,
                VendorId: d.VendorId,
                BuyerUserId: d.BuyerUserId,
                LinkedPurchaseOrderId: d.LinkedPurchaseOrderId,
                PromiseDate: p.PromiseDate,
                DaysLate: daysLate,
                SourceStatus: d.SourceStatus,
                SupplyStatus: d.SupplyStatus,
                ShortageStatus: d.ShortageStatus,
                CostStatus: d.CostStatus,
                AlertStatus: d.AlertStatus,
                BuyerActionState: d.BuyerActionState,
                NextActionHint: hint);
        }).ToList();

        return Result.Success(new PurchasingQueuePage(queueType, totalCount, rows));
    }

    // ════════════════════════════════════════════════════════════════════════
    // EXCEPTION LANE — cost exceptions + supplier alerts
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<PurchasingExceptionLane>> GetExceptionLaneAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var rows = new List<PurchasingExceptionRow>();
        var take = Math.Clamp(filter.Take, 1, 500);

        // ─── Cost exceptions — demands with variance pending ────────────
        // Apply the shared filter block (CompanyId/SiteId/VendorId/BuyerUserId/
        // ProductionOrderId) so an exception-lane caller asking "show me the
        // cost exceptions for site 3 only" actually narrows. (Subagent review #8.)
        var costExceptionQ = _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Where(d => visible.Contains(d.CompanyId)
                        && d.CostStatus == DemandCostStatus.VariancePending);
        if (filter.CompanyId.HasValue)
            costExceptionQ = costExceptionQ.Where(d => d.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue)
            costExceptionQ = costExceptionQ.Where(d => d.SiteId == filter.SiteId);
        if (filter.VendorId.HasValue)
            costExceptionQ = costExceptionQ.Where(d => d.VendorId == filter.VendorId);
        if (filter.BuyerUserId.HasValue)
            costExceptionQ = costExceptionQ.Where(d => d.BuyerUserId == filter.BuyerUserId);
        if (filter.ProductionOrderId.HasValue)
            costExceptionQ = costExceptionQ.Where(d => d.ProductionOrderId == filter.ProductionOrderId);

        // Count BEFORE paging so the caller sees the true exception backlog,
        // not just the page-1 size. (Subagent review #9.)
        var costExceptionTotal = await costExceptionQ.CountAsync(ct);
        var costExceptions = await costExceptionQ.Take(take).ToListAsync(ct);

        foreach (var d in costExceptions)
        {
            rows.Add(new PurchasingExceptionRow(
                ExceptionKind: "CostVariancePending",
                DemandId: d.Id,
                PurchaseOrderId: d.LinkedPurchaseOrderId,
                PurchaseOrderLineId: d.LinkedPurchaseOrderLineId,
                VendorId: d.VendorId,
                Severity: d.AlertStatus == DemandAlertStatus.Critical ? "High" : "Medium",
                Description: $"Demand {d.DemandNumber} has unsettled cost variance.",
                DetectedUtc: d.LastRefreshedUtc ?? d.CreatedAt,
                CostImpactUsd: null));
        }

        // ─── Supplier performance proxy — late vendor WIP ───────────────
        var today = DateTime.UtcNow.Date;
        var lateWipQ = _db.Set<VendorWipBalance>()
            .AsNoTracking()
            .Where(w => visible.Contains(w.CompanyId)
                        && w.QuantityAtVendor > 0
                        && w.RequiredReturnDate != null
                        && w.RequiredReturnDate < today);
        if (filter.CompanyId.HasValue)
            lateWipQ = lateWipQ.Where(w => w.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue) // Codex thread #2 (P2) — VendorWipBalance has SiteId; respect it.
            lateWipQ = lateWipQ.Where(w => w.SiteId == filter.SiteId);
        if (filter.VendorId.HasValue)
            lateWipQ = lateWipQ.Where(w => w.SupplierId == filter.VendorId);

        var lateWipTotal = await lateWipQ.CountAsync(ct);
        var lateWip = await lateWipQ.Take(take).ToListAsync(ct);

        foreach (var w in lateWip)
        {
            var daysLate = (today - w.RequiredReturnDate!.Value.Date).Days;
            rows.Add(new PurchasingExceptionRow(
                ExceptionKind: "VendorWipLate",
                DemandId: null,
                PurchaseOrderId: null,
                PurchaseOrderLineId: null,
                VendorId: w.SupplierId,
                Severity: daysLate > 14 ? "High" : daysLate > 3 ? "Medium" : "Low",
                Description: $"Vendor WIP balance #{w.Id} late {daysLate} day(s); qty at vendor {w.QuantityAtVendor:N2}.",
                DetectedUtc: today,
                CostImpactUsd: w.TotalValueAtVendor));
        }

        // ─── Invoice 3-way match exceptions (PR-19) ─────────────────────────
        // Price/qty/date/over-billing breaches surfaced by IInvoiceMatchService.
        // Filtered to a vendor when the lane caller narrows by VendorId.
        var invoiceMatchTotal = 0;
        if (_invoiceMatch is not null)
        {
            // Filters applied in-query before paging (Codex P2) so a filtered
            // lane never drops matching exceptions.
            var matchRows = await _invoiceMatch.GetExceptionRowsAsync(
                take, filter.CompanyId, filter.VendorId, ct);
            invoiceMatchTotal = matchRows.Count;
            foreach (var m in matchRows)
            {
                rows.Add(new PurchasingExceptionRow(
                    ExceptionKind: "InvoiceMatchException",
                    DemandId: null,
                    PurchaseOrderId: null,
                    PurchaseOrderLineId: null,
                    VendorId: null,
                    Severity: m.TotalPriceVariance > 0m ? "High" : "Medium",
                    Description: $"Invoice {m.InvoiceNumber} ({m.VendorName}) — {m.LinesException} line exception(s), "
                                 + $"price variance ${m.TotalPriceVariance:N2} (run {m.MatchRunNumber}).",
                    DetectedUtc: m.RunAtUtc,
                    CostImpactUsd: m.TotalPriceVariance));
            }
        }

        var totalCount = costExceptionTotal + lateWipTotal + invoiceMatchTotal;
        return Result.Success(new PurchasingExceptionLane(totalCount, rows));
    }

    // ════════════════════════════════════════════════════════════════════════
    // LIFECYCLE STATE — read
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<PurchasingLifecycleState>> GetLifecycleStateAsync(
        int demandId,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var d = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Where(x => x.Id == demandId && visible.Contains(x.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (d == null)
            return Result.Failure<PurchasingLifecycleState>(
                $"ProductionSupplyDemand {demandId} not found or out of tenant scope.");

        var allowed = AllowedTransitionsFor(d.BuyerActionState);
        return Result.Success(new PurchasingLifecycleState(
            DemandId: d.Id,
            State: d.BuyerActionState,
            StateLabel: StateLabel(d.BuyerActionState),
            AllowedTransitions: allowed,
            NextActionHint: SuggestNextAction(d, PurchasingQueueType.SupplyDemand)));
    }

    // ════════════════════════════════════════════════════════════════════════
    // LIFECYCLE STATE — transition (guarded write)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<TransitionLifecycleResult>> TransitionLifecycleAsync(
        TransitionLifecycleRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return Result.Failure<TransitionLifecycleResult>("Request was null.");

        var visible = _tenant.VisibleCompanyIds;
        var d = await _db.Set<ProductionSupplyDemand>()
            .Where(x => x.Id == request.DemandId && visible.Contains(x.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (d == null)
            return Result.Failure<TransitionLifecycleResult>(
                $"ProductionSupplyDemand {request.DemandId} not found or out of tenant scope.");

        var allowed = AllowedTransitionsFor(d.BuyerActionState);
        if (!allowed.Contains(request.Action))
            return Result.Failure<TransitionLifecycleResult>(
                $"Transition {request.Action} not allowed from {d.BuyerActionState}. " +
                $"Allowed: {(allowed.Count == 0 ? "(none — terminal state)" : string.Join(", ", allowed))}.");

        var previous = d.BuyerActionState;
        var newState = ApplyTransition(previous, request.Action);
        var nowUtc = DateTime.UtcNow;

        d.BuyerActionState = newState;
        d.BuyerActionStateUpdatedUtc = nowUtc;
        d.BuyerActionStateUpdatedBy = request.UserName;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            // Append rather than overwrite for audit trail. Codex thread #3 (P2):
            // BuyerActionNotes column is StringLength(2000). Without a bound, the
            // append loop eventually hits the DB constraint and SaveChanges throws
            // — violating Result<T> "never throws on expected failures".
            // Cap a single inbound note at 800 chars (defensive); roll the joined
            // buffer back to keep the newest entries when it would exceed 2000.
            const int columnCap = 2000;
            const int noteCap = 800;
            var truncatedIn = request.Notes!.Length > noteCap
                ? request.Notes.Substring(0, noteCap) + "…"
                : request.Notes;
            var stamp = $"[{nowUtc:yyyy-MM-dd HH:mm}Z {request.UserName ?? "system"}] {previous}->{newState}: {truncatedIn}";
            var joined = string.IsNullOrEmpty(d.BuyerActionNotes)
                ? stamp
                : d.BuyerActionNotes + "\n" + stamp;
            if (joined.Length > columnCap)
            {
                // Keep the tail (newest entries) up to the cap.
                joined = "…(audit truncated)\n" + joined.Substring(joined.Length - columnCap + 20);
                if (joined.Length > columnCap)
                    joined = joined.Substring(0, columnCap);
            }
            d.BuyerActionNotes = joined;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Per Result<T> contract: expected failures don't throw. Two buyers
            // racing on the same demand is expected. (Subagent review #7.)
            return Result.Failure<TransitionLifecycleResult>(
                $"Demand {request.DemandId} was modified by another user. Reload and retry the transition.");
        }
        _log.LogInformation(
            "PR-10 lifecycle transition: demand {DemandId} {Prev} -> {New} by user {UserName}",
            d.Id, previous, newState, request.UserName);

        return Result.Success(new TransitionLifecycleResult(
            DemandId: d.Id,
            PreviousState: previous,
            NewState: newState,
            TransitionedAtUtc: nowUtc,
            Notes: request.Notes));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STATE MACHINE — dispatch tables (pure, side-effect free)
    // ════════════════════════════════════════════════════════════════════════

    internal static IReadOnlyList<BuyerActionTransition> AllowedTransitionsFor(BuyerActionState s) => s switch
    {
        BuyerActionState.Open => new[]
        {
            BuyerActionTransition.Assign, BuyerActionTransition.StartWork,
            BuyerActionTransition.Block, BuyerActionTransition.Cancel,
        },
        BuyerActionState.Assigned => new[]
        {
            BuyerActionTransition.StartWork, BuyerActionTransition.Block,
            BuyerActionTransition.Cancel,
        },
        BuyerActionState.InProgress => new[]
        {
            BuyerActionTransition.SendToVendor, BuyerActionTransition.RequestApproval,
            BuyerActionTransition.MarkResolved, BuyerActionTransition.Block,
            BuyerActionTransition.Cancel,
        },
        BuyerActionState.AwaitingVendor => new[]
        {
            BuyerActionTransition.RequestApproval, BuyerActionTransition.MarkResolved,
            BuyerActionTransition.Block, BuyerActionTransition.Cancel,
        },
        BuyerActionState.AwaitingApproval => new[]
        {
            BuyerActionTransition.ApprovalGranted, BuyerActionTransition.ApprovalDenied,
            BuyerActionTransition.Cancel,
        },
        BuyerActionState.Resolved => new[]
        {
            BuyerActionTransition.Close,
            BuyerActionTransition.StartWork, // re-engage if supply slips
            BuyerActionTransition.Cancel,    // PRO/BOM-line cancel cascade (subagent review #2)
        },
        BuyerActionState.Closed => new[]
        {
            BuyerActionTransition.Reopen,
        },
        BuyerActionState.Blocked => new[]
        {
            BuyerActionTransition.Unblock, BuyerActionTransition.Cancel,
        },
        BuyerActionState.Cancelled => new[]
        {
            BuyerActionTransition.Reopen,
        },
        _ => Array.Empty<BuyerActionTransition>(),
    };

    internal static BuyerActionState ApplyTransition(BuyerActionState from, BuyerActionTransition t) => t switch
    {
        BuyerActionTransition.Assign => BuyerActionState.Assigned,
        BuyerActionTransition.StartWork => BuyerActionState.InProgress,
        BuyerActionTransition.SendToVendor => BuyerActionState.AwaitingVendor,
        BuyerActionTransition.RequestApproval => BuyerActionState.AwaitingApproval,
        BuyerActionTransition.ApprovalDenied => BuyerActionState.InProgress,
        BuyerActionTransition.ApprovalGranted => BuyerActionState.AwaitingVendor,
        BuyerActionTransition.MarkResolved => BuyerActionState.Resolved,
        BuyerActionTransition.Close => BuyerActionState.Closed,
        BuyerActionTransition.Block => BuyerActionState.Blocked,
        BuyerActionTransition.Unblock => BuyerActionState.InProgress,
        BuyerActionTransition.Cancel => BuyerActionState.Cancelled,
        BuyerActionTransition.Reopen => BuyerActionState.Open,
        _ => from,
    };

    private static string StateLabel(BuyerActionState s) => s switch
    {
        BuyerActionState.Open => "Open — needs buyer",
        BuyerActionState.Assigned => "Assigned",
        BuyerActionState.InProgress => "In progress",
        BuyerActionState.AwaitingVendor => "Awaiting vendor",
        BuyerActionState.AwaitingApproval => "Awaiting approval",
        BuyerActionState.Resolved => "Resolved",
        BuyerActionState.Closed => "Closed",
        BuyerActionState.Blocked => "Blocked",
        BuyerActionState.Cancelled => "Cancelled",
        _ => s.ToString(),
    };

    // ════════════════════════════════════════════════════════════════════════
    // NEXT-ACTION HINT — preview of §18 buyer recommendation engine (full
    // version ships in PR-15). Keeps the queue UX informative from PR-10.
    // ════════════════════════════════════════════════════════════════════════

    private static string SuggestNextAction(ProductionSupplyDemand d, PurchasingQueueType q)
    {
        if (d.BuyerActionState == BuyerActionState.Blocked)
            return "Unblock: resolve drawing/supplier/approval gate";
        if (d.BuyerActionState == BuyerActionState.AwaitingApproval)
            return "Approval pending — escalate if past SLA";
        if (d.VendorId == null && d.SourceStatus == DemandSourceStatus.NotDetermined)
            return "No source — assign supplier or request sourcing";
        if (d.LinkedPurchaseOrderId == null
            && (d.SupplyPolicy == SupplyPolicy.BuyDirectToJob
                || d.SupplyPolicy == SupplyPolicy.BuyDirectToOperation))
            return "Create PO linked to BOM/operation";
        if (d.ShortageStatus == DemandShortageStatus.Late
            || d.ShortageStatus == DemandShortageStatus.Critical)
            return "Expedite PO — supply is at risk";
        if (d.SupplyStatus == DemandSupplyStatus.AtVendor)
            return "Track vendor processing; coordinate return";
        if (d.SupplyStatus == DemandSupplyStatus.InInspection)
            return "Awaiting inspection sign-off";
        if (d.CostStatus == DemandCostStatus.VariancePending)
            return "Review variance — settle or escalate";
        if (d.SupplyStatus == DemandSupplyStatus.Committed
            && d.SupplyPolicy != SupplyPolicy.Floorstock
            && d.LinkedPurchaseOrderId != null)
            return "Confirm promise date with supplier";
        if (d.SupplyStatus == DemandSupplyStatus.FullyFulfilled)
            return "Close — supply received and accepted";
        return q switch
        {
            PurchasingQueueType.BuyToJob => "Create job-linked PO",
            PurchasingQueueType.Subcontract => "Create subcontract PO + ship WIP",
            PurchasingQueueType.VendorWip => "Monitor vendor processing window",
            PurchasingQueueType.InspectionBlocked => "Push inspection or release hold",
            PurchasingQueueType.ApprovalRequired => "Approve / deny / kick back",
            _ => "Review and assign next action",
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    // PR-12 TAB READS — Subcontract / Vendor WIP / Receipts / Inspection Holds
    //
    // Each tab read enforces:
    //   * Tenant scope via ITenantContext.VisibleCompanyIds on every entity set
    //   * Optional site/buyer/vendor/PRO filters from PurchasingQueueFilter
    //   * Skip/Take with Math.Clamp guards
    //   * Count + page returned together (Count *before* pagination so the UI
    //     header is honest about backlog size)
    //   * Per-row NextActionHint where appropriate (placeholder for §18; PR-15
    //     replaces with full recommendation engine)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractTabPage>> GetSubcontractTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var today = DateTime.UtcNow.Date;
        var take = Math.Clamp(filter.Take, 1, 500);
        var skip = Math.Max(0, filter.Skip);

        // Base query: active subcontract ops only (Closed is terminal).
        var q = _db.Set<SubcontractOperation>()
            .AsNoTracking()
            .Where(o => visible.Contains(o.CompanyId)
                        && o.Status != SubcontractOperationStatus.Closed);

        if (filter.CompanyId.HasValue) q = q.Where(o => o.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue) q = q.Where(o => o.SiteId == filter.SiteId);
        if (filter.VendorId.HasValue) q = q.Where(o => o.SupplierId == filter.VendorId);
        if (filter.ProductionOrderId.HasValue) q = q.Where(o => o.ProductionOrderId == filter.ProductionOrderId);
        if (filter.RequiredBefore.HasValue)
            q = q.Where(o => o.RequiredBackDate != null && o.RequiredBackDate < filter.RequiredBefore);

        var totalCount = await q.CountAsync(ct);

        var paged = await q
            .OrderBy(o => o.RequiredBackDate ?? DateTime.MaxValue)
            .ThenBy(o => o.Id)
            .Skip(skip).Take(take)
            .Select(o => new
            {
                Op = o,
                SupplierName = o.Supplier != null ? o.Supplier.Name : null,
                ProNumber = o.ProductionOrder != null ? o.ProductionOrder.OrderNumber : null,
            })
            .ToListAsync(ct);

        var rows = paged.Select(p =>
        {
            var o = p.Op;
            int? daysLate = null;
            if (o.RequiredBackDate.HasValue
                && o.QuantityReceivedBack < o.QuantityToShip
                && o.RequiredBackDate.Value.Date < today)
            {
                daysLate = (today - o.RequiredBackDate.Value.Date).Days;
            }
            return new SubcontractTabRow(
                SubcontractOperationId: o.Id,
                ProductionOrderId: o.ProductionOrderId,
                ProductionOrderNumber: p.ProNumber,
                OperationSequence: o.OperationSequence,
                OperationCode: o.OperationCode,
                OperationDescription: o.OperationDescription,
                SupplierId: o.SupplierId,
                SupplierName: p.SupplierName,
                ServicePurchaseOrderLineId: o.ServicePurchaseOrderLineId,
                OpStatus: o.Status,
                PoStatus: o.PoCreationStatus,
                ShipmentStatus: o.ShipmentStatus,
                ReceiptStatusForOp: o.ReceiptStatus,
                QuantityToShip: o.QuantityToShip,
                QuantityShipped: o.QuantityShipped,
                QuantityReceivedBack: o.QuantityReceivedBack,
                QuantityAccepted: o.QuantityAccepted,
                QuantityRejected: o.QuantityRejected,
                QuantityScrappedAtVendor: o.QuantityScrappedAtVendor,
                RequiredShipDate: o.RequiredShipDate,
                RequiredBackDate: o.RequiredBackDate,
                DaysLateBack: daysLate,
                CertRequired: o.CertRequired,
                InspectionOnReturn: o.InspectionOnReturn,
                NextActionHint: SuggestSubcontractAction(o));
        }).ToList();

        return Result.Success(new SubcontractTabPage(totalCount, rows));
    }

    private static string SuggestSubcontractAction(SubcontractOperation o)
    {
        return o.Status switch
        {
            SubcontractOperationStatus.NotReady => "Wait for prior op to complete",
            SubcontractOperationStatus.ReadyToBuy => "Create subcontract PO",
            SubcontractOperationStatus.PoCreated => "Approve PO + prepare WIP",
            SubcontractOperationStatus.ReadyToShip => "Ship WIP to vendor",
            SubcontractOperationStatus.ShippedToVendor => "Confirm vendor receipt",
            SubcontractOperationStatus.AtVendor => "Monitor vendor processing",
            SubcontractOperationStatus.PartiallyReceived => "Receive remaining WIP",
            SubcontractOperationStatus.InInspection => "Clear inspection hold",
            SubcontractOperationStatus.Rejected => "Open NCR + return to vendor or scrap",
            SubcontractOperationStatus.ReworkAtVendor => "Track rework cycle at vendor",
            SubcontractOperationStatus.Complete => "Close op + advance routing",
            _ => "Review and assign next action",
        };
    }

    public async Task<Result<VendorWipTabPage>> GetVendorWipTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var today = DateTime.UtcNow.Date;
        var take = Math.Clamp(filter.Take, 1, 500);
        var skip = Math.Max(0, filter.Skip);

        // Base query: balances with material currently at vendor (QtyAtVendor > 0)
        // OR balances awaiting close (qty exhausted but still in active lifecycle).
        // For the tab we focus on QtyAtVendor > 0 — that's the live "at supplier"
        // working inventory the buyer needs to monitor.
        var q = _db.Set<VendorWipBalance>()
            .AsNoTracking()
            .Where(b => visible.Contains(b.CompanyId) && b.QuantityAtVendor > 0);

        if (filter.CompanyId.HasValue) q = q.Where(b => b.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue) q = q.Where(b => b.SiteId == filter.SiteId);
        if (filter.VendorId.HasValue) q = q.Where(b => b.SupplierId == filter.VendorId);
        if (filter.ProductionOrderId.HasValue) q = q.Where(b => b.ProductionOrderId == filter.ProductionOrderId);

        var totalCount = await q.CountAsync(ct);

        // Page-level summaries (computed across the FULL filtered set, not just
        // the page slice — page-aggregate would lie about scope).
        var totalValueAtVendorUsd = await q.SumAsync(b => (decimal?)b.TotalValueAtVendor, ct) ?? 0m;
        var overdueReturnCount = await q
            .Where(b => b.RequiredReturnDate != null && b.RequiredReturnDate < today)
            .CountAsync(ct);

        var paged = await q
            .OrderBy(b => b.RequiredReturnDate ?? DateTime.MaxValue)
            .ThenByDescending(b => b.TotalValueAtVendor)
            .ThenBy(b => b.Id)
            .Skip(skip).Take(take)
            .Select(b => new
            {
                Balance = b,
                SupplierName = b.Supplier != null ? b.Supplier.Name : null,
                ProNumber = b.ProductionOrder != null ? b.ProductionOrder.OrderNumber : null,
                VendorLocationDescription = b.VendorLocation != null
                    ? (b.VendorLocation.LocationCode + " · " + (b.VendorLocation.SupplierSiteCode ?? ""))
                    : b.VendorWipLocationDescription,
            })
            .ToListAsync(ct);

        var rows = paged.Select(p =>
        {
            var b = p.Balance;
            int? daysLateReturn = null;
            if (b.RequiredReturnDate.HasValue && b.RequiredReturnDate.Value.Date < today)
                daysLateReturn = (today - b.RequiredReturnDate.Value.Date).Days;
            return new VendorWipTabRow(
                VendorWipBalanceId: b.Id,
                ProductionOrderId: b.ProductionOrderId,
                ProductionOrderNumber: p.ProNumber,
                OperationSequence: b.OperationSequence,
                SupplierId: b.SupplierId,
                SupplierName: p.SupplierName,
                VendorLocationId: b.VendorLocationId,
                VendorLocationDescription: p.VendorLocationDescription,
                PartNumber: b.PartNumber,
                Revision: b.Revision,
                LotNumber: b.LotNumber,
                QuantityShipped: b.QuantityShipped,
                QuantityAtVendor: b.QuantityAtVendor,
                QuantityReceivedBack: b.QuantityReceivedBack,
                QuantityAccepted: b.QuantityAccepted,
                QuantityRejected: b.QuantityRejected,
                QuantityScrappedAtVendor: b.QuantityScrappedAtVendor,
                UnitValue: b.UnitValue,
                TotalValueAtVendor: b.TotalValueAtVendor,
                InventoryStatus: b.InventoryStatus,
                QualityStatus: b.QualityStatus,
                Ownership: b.Ownership,
                AgingDaysAtVendor: b.AgingDaysAtVendor,
                RequiredReturnDate: b.RequiredReturnDate,
                DaysLateReturn: daysLateReturn,
                LastTransactionUtc: b.LastTransactionUtc);
        }).ToList();

        return Result.Success(new VendorWipTabPage(
            TotalCount: totalCount,
            TotalValueAtVendorUsd: totalValueAtVendorUsd,
            OverdueReturnCount: overdueReturnCount,
            Rows: rows));
    }

    public async Task<Result<ReceiptsTabPage>> GetReceiptsTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var take = Math.Clamp(filter.Take, 1, 500);
        var skip = Math.Max(0, filter.Skip);

        // Base query: SubcontractReceipt headers in any non-Closed lifecycle.
        // We include Reversed so audit + correction workflows surface here too.
        var q = _db.Set<SubcontractReceipt>()
            .AsNoTracking()
            .Where(r => visible.Contains(r.CompanyId)
                        && r.Status != SubcontractReceiptLifecycle.Closed);

        if (filter.CompanyId.HasValue) q = q.Where(r => r.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue) q = q.Where(r => r.SiteId == filter.SiteId);
        if (filter.VendorId.HasValue) q = q.Where(r => r.SupplierId == filter.VendorId);
        if (filter.ProductionOrderId.HasValue) q = q.Where(r => r.ProductionOrderId == filter.ProductionOrderId);

        var totalCount = await q.CountAsync(ct);
        var openDraftCount = await q.CountAsync(r => r.Status == SubcontractReceiptLifecycle.Draft, ct);
        var pendingApprovalCount = await q.CountAsync(
            r => r.Status == SubcontractReceiptLifecycle.PendingApproval, ct);

        var paged = await q
            .OrderByDescending(r => r.ReceiptDate)
            .ThenByDescending(r => r.Id)
            .Skip(skip).Take(take)
            .Select(r => new
            {
                Receipt = r,
                SupplierName = r.Supplier != null ? r.Supplier.Name : null,
                ProNumber = r.ProductionOrder != null ? r.ProductionOrder.OrderNumber : null,
                LineCount = r.Lines.Count,
                TotalReceived = r.Lines.Sum(l => (decimal?)l.QuantityReceived) ?? 0m,
                TotalAccepted = r.Lines.Sum(l => (decimal?)l.QuantityAccepted) ?? 0m,
                TotalRejected = r.Lines.Sum(l => (decimal?)l.QuantityRejected) ?? 0m,
                TotalScrapped = r.Lines.Sum(l => (decimal?)l.QuantityScrappedAtVendor) ?? 0m,
            })
            .ToListAsync(ct);

        var rows = paged.Select(p => new ReceiptsTabRow(
            SubcontractReceiptId: p.Receipt.Id,
            ReceiptNumber: p.Receipt.ReceiptNumber,
            SubcontractOperationId: p.Receipt.SubcontractOperationId,
            ProductionOrderId: p.Receipt.ProductionOrderId,
            ProductionOrderNumber: p.ProNumber,
            OperationSequence: p.Receipt.OperationSequence,
            SupplierId: p.Receipt.SupplierId,
            SupplierName: p.SupplierName,
            VendorPackingSlip: p.Receipt.VendorPackingSlip,
            ReceiptDate: p.Receipt.ReceiptDate,
            Status: p.Receipt.Status,
            CertReceived: p.Receipt.CertReceived,
            InspectionRequired: p.Receipt.InspectionRequired,
            ApprovalRequired: p.Receipt.ApprovalRequired,
            LineCount: p.LineCount,
            TotalReceived: p.TotalReceived,
            TotalAccepted: p.TotalAccepted,
            TotalRejected: p.TotalRejected,
            TotalScrappedAtVendor: p.TotalScrapped,
            PostedUtc: p.Receipt.PostedUtc,
            ApprovedBy: p.Receipt.ApprovedBy)).ToList();

        return Result.Success(new ReceiptsTabPage(
            TotalCount: totalCount,
            OpenDraftCount: openDraftCount,
            PendingApprovalCount: pendingApprovalCount,
            Rows: rows));
    }

    public async Task<Result<InspectionHoldsTabPage>> GetInspectionHoldsTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var today = DateTime.UtcNow.Date;
        var take = Math.Clamp(filter.Take, 1, 500);
        // (Codex thread 0) The previous design split filter.Skip halfway across
        // the two source paths. That breaks the page model's full-page-size Skip
        // advancement (page-2 Skip=50, halfSkip=25 leaves rows 51+ unreachable
        // when one source dominates). Inspection Holds is a UNION of two
        // entity types, which EF can't paginate atomically without TVFs. So we
        // intentionally drop Skip support here: page 1 returns the merged
        // top-of-list, TotalCount is clipped to what's actually reachable, and
        // the page model's HasNext returns false because Skip + RowCount ==
        // TotalCount. Buyers stay accurate; older holds surface as the queue
        // drains. (P2.3 originally fixed, then Codex caught the off-by-source
        // edge case — this is the true fix.)
        var halfTake = Math.Max(1, take / 2);

        var rows = new List<InspectionHoldRow>();

        // ─── Path A: GoodsReceiptLine inspection holds ──────────────────
        // A GR line is on hold when InspectionRequired = true AND
        // DirectToJobPostedUtc IS NULL (direct-to-job path) OR parent receipt
        // status is Inspecting (standard inventory path).
        var grQ = _db.Set<GoodsReceiptLine>()
            .AsNoTracking()
            .Where(l => l.GoodsReceipt != null
                        && l.GoodsReceipt.CompanyId != null
                        && visible.Contains(l.GoodsReceipt.CompanyId.Value)
                        && l.InspectionRequired
                        && ((l.IsDirectToJob && l.DirectToJobPostedUtc == null)
                            || l.GoodsReceipt!.Status == ReceiptStatus.Inspecting
                            || (l.QuantityReceived > 0
                                && l.QuantityAccepted + l.QuantityRejected < l.QuantityReceived)));

        if (filter.CompanyId.HasValue)
            grQ = grQ.Where(l => l.GoodsReceipt!.CompanyId == filter.CompanyId);
        // (Codex thread 1) GoodsReceipt itself has no SiteId; PurchaseOrder
        // carries ShipToSiteId. Honor filter.SiteId via the parent PO so
        // /Purchasing/ControlCenter?tab=inspection-holds&SiteId=… narrows to
        // GR lines for receipts shipped TO the requested site.
        if (filter.SiteId.HasValue)
            grQ = grQ.Where(l => l.GoodsReceipt!.PurchaseOrder != null
                                 && l.GoodsReceipt!.PurchaseOrder!.ShipToSiteId == filter.SiteId);
        if (filter.ProductionOrderId.HasValue)
            grQ = grQ.Where(l => l.DirectToJobProductionOrderId == filter.ProductionOrderId);
        if (filter.VendorId.HasValue)
            grQ = grQ.Where(l => l.GoodsReceipt!.PurchaseOrder != null
                                 && l.GoodsReceipt!.PurchaseOrder!.VendorId == filter.VendorId);

        var grTotal = await grQ.CountAsync(ct);

        var grSlice = await grQ
            .OrderBy(l => l.GoodsReceipt!.ReceiptDate)
            .ThenBy(l => l.Id)
            .Take(halfTake)
            .Select(l => new
            {
                Line = l,
                ReceiptNumber = l.GoodsReceipt!.ReceiptNumber,
                ReceiptId = l.GoodsReceipt!.Id,
                ReceiptDate = l.GoodsReceipt!.ReceiptDate,
                PoId = l.GoodsReceipt!.PurchaseOrderId,
                PoNumber = l.GoodsReceipt!.PurchaseOrder != null
                    ? l.GoodsReceipt!.PurchaseOrder!.PONumber
                    : null,
                SupplierId = l.GoodsReceipt!.PurchaseOrder != null
                    ? l.GoodsReceipt!.PurchaseOrder!.VendorId
                    : (int?)null,
                SupplierName = l.GoodsReceipt!.PurchaseOrder != null && l.GoodsReceipt!.PurchaseOrder!.Vendor != null
                    ? l.GoodsReceipt!.PurchaseOrder!.Vendor!.Name
                    : null,
                ProId = l.DirectToJobProductionOrderId,
                ProNumber = l.DirectToJobProductionOrder != null
                    ? l.DirectToJobProductionOrder!.OrderNumber
                    : null,
                PartNumber = l.PurchaseOrderLine != null
                    ? l.PurchaseOrderLine!.PartNumber
                    : null,
            })
            .ToListAsync(ct);

        foreach (var p in grSlice)
        {
            var line = p.Line;
            var onHold = line.QuantityReceived - line.QuantityAccepted - line.QuantityRejected;
            if (onHold < 0) onHold = 0;
            var days = (today - p.ReceiptDate.Date).Days;
            if (days < 0) days = 0;
            rows.Add(new InspectionHoldRow(
                SourceKind: InspectionHoldSourceKind.PurchaseOrderReceipt,
                SourceLineId: line.Id,
                SourceHeaderId: p.ReceiptId,
                SourceHeaderNumber: p.ReceiptNumber,
                PurchaseOrderId: p.PoId,
                PurchaseOrderNumber: p.PoNumber,
                ProductionOrderId: p.ProId,
                ProductionOrderNumber: p.ProNumber,
                OperationSequence: null,
                SupplierId: p.SupplierId,
                SupplierName: p.SupplierName,
                PartNumber: p.PartNumber,
                Revision: null,
                LotNumber: line.LotNumber,
                QuantityReceived: line.QuantityReceived,
                QuantityAccepted: line.QuantityAccepted,
                QuantityRejected: line.QuantityRejected,
                QuantityOnHold: onHold,
                HoldReason: line.IsDirectToJob && line.DirectToJobPostedUtc == null
                    ? "Direct-to-job: inspection required before cost posts to PRO"
                    : "Standard incoming inspection",
                ReceiptDate: p.ReceiptDate,
                DaysOnHold: days,
                NcrReference: null,
                NextActionHint: line.IsDirectToJob
                    ? "Clear inspection → cost posts to PRO BOM line"
                    : "Clear inspection → release to stock"));
        }

        // ─── Path B: SubcontractReceiptLine holds ───────────────────────
        // (P2.2 fix) PendingApproval lives on the Receipts tab via its
        // own PendingApprovalCount header — exclude here to avoid the same
        // row counting on two tabs with conflicting next-action hints.
        var scQ = _db.Set<SubcontractReceiptLine>()
            .AsNoTracking()
            .Where(l => visible.Contains(l.CompanyId)
                        && (l.Disposition == SubcontractReceiptDisposition.HoldForInspection
                            || l.Disposition == SubcontractReceiptDisposition.HoldForDocs
                            || l.Disposition == SubcontractReceiptDisposition.HoldForQuality));

        if (filter.CompanyId.HasValue) scQ = scQ.Where(l => l.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue) scQ = scQ.Where(l => l.SiteId == filter.SiteId);
        if (filter.ProductionOrderId.HasValue)
            scQ = scQ.Where(l => l.SubcontractReceipt != null
                                 && l.SubcontractReceipt!.ProductionOrderId == filter.ProductionOrderId);
        if (filter.VendorId.HasValue)
            scQ = scQ.Where(l => l.SubcontractReceipt != null
                                 && l.SubcontractReceipt!.SupplierId == filter.VendorId);

        var scTotal = await scQ.CountAsync(ct);

        var scSlice = await scQ
            .OrderBy(l => l.SubcontractReceipt!.ReceiptDate)
            .ThenBy(l => l.Id)
            .Take(halfTake)
            .Select(l => new
            {
                Line = l,
                ReceiptNumber = l.SubcontractReceipt!.ReceiptNumber,
                ReceiptId = l.SubcontractReceipt!.Id,
                ReceiptDate = l.SubcontractReceipt!.ReceiptDate,
                ProId = (int?)l.SubcontractReceipt!.ProductionOrderId,
                ProNumber = l.SubcontractReceipt!.ProductionOrder != null
                    ? l.SubcontractReceipt!.ProductionOrder!.OrderNumber
                    : null,
                OperationSequence = (int?)l.SubcontractReceipt!.OperationSequence,
                SupplierId = (int?)l.SubcontractReceipt!.SupplierId,
                SupplierName = l.SubcontractReceipt!.Supplier != null
                    ? l.SubcontractReceipt!.Supplier!.Name
                    : null,
            })
            .ToListAsync(ct);

        foreach (var p in scSlice)
        {
            var line = p.Line;
            var onHold = line.QuantityReceived - line.QuantityAccepted - line.QuantityRejected
                         - line.QuantityScrappedAtVendor;
            if (onHold < 0) onHold = 0;
            var days = (today - p.ReceiptDate.Date).Days;
            if (days < 0) days = 0;

            var reason = line.Disposition switch
            {
                SubcontractReceiptDisposition.HoldForInspection => "Hold for incoming inspection",
                SubcontractReceiptDisposition.HoldForDocs => "Hold for cert / documentation",
                SubcontractReceiptDisposition.HoldForQuality => "Hold for quality / engineering review",
                SubcontractReceiptDisposition.PendingApproval => "Pending supervisor approval",
                _ => "Hold",
            };
            var hint = line.Scenario switch
            {
                SubcontractReceiptScenario.RejectedReceipt => "Open NCR + decide rework / scrap / return",
                SubcontractReceiptScenario.CertMissing => "Request cert from supplier",
                SubcontractReceiptScenario.WrongRevision => "Engineering: confirm acceptable or reject",
                SubcontractReceiptScenario.OverReceipt => "Supervisor approve over-receipt",
                SubcontractReceiptScenario.WrongJobOrPo => "Reverse + correct PO/job",
                SubcontractReceiptScenario.ReceiptWithInspection => "Inspect + release to next op",
                _ => "Resolve hold",
            };

            rows.Add(new InspectionHoldRow(
                SourceKind: InspectionHoldSourceKind.SubcontractReceipt,
                SourceLineId: line.Id,
                SourceHeaderId: p.ReceiptId,
                SourceHeaderNumber: p.ReceiptNumber,
                PurchaseOrderId: null,
                PurchaseOrderNumber: null,
                ProductionOrderId: p.ProId,
                ProductionOrderNumber: p.ProNumber,
                OperationSequence: p.OperationSequence,
                SupplierId: p.SupplierId,
                SupplierName: p.SupplierName,
                PartNumber: line.PartNumber,
                Revision: line.DrawingRevision,
                LotNumber: line.LotNumber,
                QuantityReceived: line.QuantityReceived,
                QuantityAccepted: line.QuantityAccepted,
                QuantityRejected: line.QuantityRejected,
                QuantityOnHold: onHold,
                HoldReason: reason,
                ReceiptDate: p.ReceiptDate,
                DaysOnHold: days,
                NcrReference: line.NcrReference,
                NextActionHint: hint));
        }

        // Sort merged rows by days-on-hold descending (oldest holds first to surface).
        var sorted = rows.OrderByDescending(r => r.DaysOnHold).ToList();
        var oldCount = sorted.Count(r => r.DaysOnHold >= 7);

        // (Codex thread 0 follow-on) Without paginated Skip support, TotalCount
        // must equal the *reachable* row count so the page's HasNext returns
        // false (Skip + rowCount >= TotalCount). The true backlog is exposed as
        // BacklogCount in the field-by-field doc above the record and via the
        // header "X total · Y aged" copy. Keeping TotalCount honest about
        // reachability is the disciplined fix until UNION-grain pagination is
        // worth building (Wave 4 polish or beyond).
        return Result.Success(new InspectionHoldsTabPage(
            TotalCount: sorted.Count,
            OldHoldsCount: oldCount,
            Rows: sorted));
    }

    // ════════════════════════════════════════════════════════════════════════
    // PR-13 TAB READS — POs / Cost Exceptions
    //
    // Expedites + Approvals reuse GetSupplyDemandQueueAsync with the matching
    // PurchasingQueueType (already implemented in the 13-way dispatch above).
    // The page model just routes those tabs through the existing Queue path
    // — no service additions needed.
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<PosTabPage>> GetPosTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var today = DateTime.UtcNow.Date;
        var take = Math.Clamp(filter.Take, 1, 500);
        var skip = Math.Max(0, filter.Skip);

        // Base query: open lifecycle states only. Closed + Cancelled are
        // terminal and excluded from the active tab. Draft is included so
        // buyers can see in-flight work even before approval.
        var q = _db.Set<PurchaseOrder>()
            .AsNoTracking()
            .Where(p => p.CompanyId != null
                        && visible.Contains(p.CompanyId.Value)
                        && p.Status != POStatus.Closed
                        && p.Status != POStatus.Cancelled);

        if (filter.CompanyId.HasValue) q = q.Where(p => p.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue) q = q.Where(p => p.ShipToSiteId == filter.SiteId);
        if (filter.VendorId.HasValue) q = q.Where(p => p.VendorId == filter.VendorId);
        if (filter.RequiredBefore.HasValue)
            q = q.Where(p => p.RequiredDate != null && p.RequiredDate < filter.RequiredBefore);

        var totalCount = await q.CountAsync(ct);

        // Page-level summaries — across the full filtered set.
        // Open value = sum of Total for non-terminal POs. Currency-agnostic
        // (per IPurchasingControlCenterService.PosTabPage.OpenTotalValue
        // XML doc — multi-currency tenants get a mixed-ccy sum; FX
        // conversion is a Wave-4 polish item). PendingApproval + late
        // counts feed the tab header chips.
        var openTotalValue = await q.SumAsync(p => (decimal?)p.Total, ct) ?? 0m;
        var pendingApprovalCount = await q
            .Where(p => p.Status == POStatus.PendingApproval)
            .CountAsync(ct);
        // P2.1 fix — match the per-row DaysLate semantics so the header
        // chip and the in-table column don't disagree. Received/Invoiced
        // POs whose promise dates have passed are NOT counted as late.
        var lateCount = await q
            .Where(p => p.Status != POStatus.Received
                        && p.Status != POStatus.Invoiced
                        && ((p.PromiseDate != null && p.PromiseDate < today)
                            || (p.PromiseDate == null && p.RequiredDate != null && p.RequiredDate < today)))
            .CountAsync(ct);

        var paged = await q
            .OrderByDescending(p => p.OrderDate)
            .ThenByDescending(p => p.Id)
            .Skip(skip).Take(take)
            .Select(p => new
            {
                Po = p,
                VendorName = p.Vendor != null ? p.Vendor.Name : null,
                LineCount = p.Lines.Count,
            })
            .ToListAsync(ct);

        var rows = paged.Select(x =>
        {
            var p = x.Po;
            int? daysLate = null;
            var effectiveDate = p.PromiseDate ?? p.RequiredDate;
            if (effectiveDate.HasValue
                && effectiveDate.Value.Date < today
                && p.Status != POStatus.Received
                && p.Status != POStatus.Invoiced)
            {
                daysLate = (today - effectiveDate.Value.Date).Days;
            }
            return new PosTabRow(
                PurchaseOrderId: p.Id,
                PoNumber: p.PONumber,
                Status: p.Status,
                VendorId: p.VendorId,
                VendorName: x.VendorName,
                OrderDate: p.OrderDate,
                RequiredDate: p.RequiredDate,
                PromiseDate: p.PromiseDate,
                DaysLate: daysLate,
                LineCount: x.LineCount,
                Subtotal: p.Subtotal,
                Total: p.Total,
                Currency: p.Currency,
                ShipToSiteId: p.ShipToSiteId,
                RequestedById: p.RequestedById,
                ApprovedById: p.ApprovedById,
                ApprovedAt: p.ApprovedAt,
                CipProjectId: p.CipProjectId,
                Notes: p.Notes,
                NextActionHint: SuggestPoAction(p, daysLate));
        }).ToList();

        return Result.Success(new PosTabPage(
            TotalCount: totalCount,
            OpenTotalValue: openTotalValue,
            LateCount: lateCount,
            PendingApprovalCount: pendingApprovalCount,
            Rows: rows));
    }

    private static string SuggestPoAction(PurchaseOrder p, int? daysLate)
    {
        if (p.Status == POStatus.Draft) return "Submit for approval";
        if (p.Status == POStatus.PendingApproval) return "Awaiting approval — escalate if past SLA";
        if (p.Status == POStatus.Approved) return "Send to vendor";
        if (p.Status == POStatus.Sent && daysLate.HasValue) return "Expedite — past promise/required date";
        if (p.Status == POStatus.Sent) return "Awaiting acknowledgment / first receipt";
        if (p.Status == POStatus.PartiallyReceived && daysLate.HasValue) return "Expedite balance — past due";
        if (p.Status == POStatus.PartiallyReceived) return "Track remaining receipts";
        if (p.Status == POStatus.Received) return "Match invoice / close";
        if (p.Status == POStatus.Invoiced) return "Verify match + close";
        return "Review";
    }

    public async Task<Result<CostExceptionsTabPage>> GetCostExceptionsTabAsync(
        PurchasingQueueFilter filter,
        CancellationToken ct = default)
    {
        // Reuse the existing exception lane read (the diagonal stripe on
        // the §21 layout). Wrap with severity-bucketed counters for the
        // tab header. We don't add a new entity scan — exception-lane
        // semantics are exactly what tab 10 is supposed to surface.
        //
        // (Codex review) GetExceptionLaneAsync already computes the TRUE
        // backlog (costExceptionTotal + lateWipTotal) before per-source
        // Take. The pre-PR P2.2 fix that clipped TotalCount to Rows.Count
        // threw away that signal and made remaining exceptions unreachable.
        // True fix: fetch with Take = Skip + Take from the lane so the
        // merged returned set is large enough, then slice in memory with
        // Skip + Take. Preserves lane.TotalCount as the honest backlog
        // signal AND lets pagination advance.
        var skip = Math.Max(0, filter.Skip);
        var take = Math.Clamp(filter.Take, 1, 500);
        // The lane is currently uncapped beyond its per-source Take=500.
        // Cap our overshoot at 500 too — tenants with >500 exceptions hit
        // a known ceiling (documented in the page-level pagination note),
        // worth revisiting in Wave 4 polish via real cross-source skip.
        var laneTake = Math.Clamp(skip + take, take, 500);
        var laneFilter = filter with { Skip = 0, Take = laneTake };

        var laneResult = await GetExceptionLaneAsync(laneFilter, ct);
        if (!laneResult.IsSuccess || laneResult.Value is null)
            return Result.Failure<CostExceptionsTabPage>(
                laneResult.Error ?? "Failed to read exception lane.");

        var lane = laneResult.Value;
        // Apply in-memory skip+take to the merged list so the visible rows
        // match the page the user requested.
        var pagedRows = lane.Rows.Skip(skip).Take(take).ToList();
        var high = pagedRows.Count(r => r.Severity == "High");
        var med = pagedRows.Count(r => r.Severity == "Medium");
        var low = pagedRows.Count(r => r.Severity == "Low");

        return Result.Success(new CostExceptionsTabPage(
            TotalCount: lane.TotalCount,
            HighSeverityCount: high,
            MediumSeverityCount: med,
            LowSeverityCount: low,
            Rows: pagedRows));
    }

    // ════════════════════════════════════════════════════════════════════════
    // PR-18 TAB READ — §21 tab 13 Supplier Performance
    // ════════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public async Task<Result<SupplierPerformanceTabPage>> GetSupplierPerformanceTabAsync(
        SupplierPerformancePeriod period, CancellationToken ct = default)
    {
        if (_supplierPerformance is null)
            return Result.Failure<SupplierPerformanceTabPage>(
                "Supplier performance service is not available.");

        var rows = await _supplierPerformance.GetScorecardAsync(period, ct);

        // At-risk = OTD below 90% (known basis) OR any supplier NCR in the
        // window. Suppliers with no OTD basis are "unknown", not at-risk.
        var atRisk = rows.Count(r =>
            (r.OnTimeDeliveryPct.HasValue && r.OnTimeDeliveryPct.Value < 90m)
            || r.NcrCount > 0);

        return Result.Success(new SupplierPerformanceTabPage(
            TotalCount: rows.Count,
            Period: period,
            AtRiskCount: atRisk,
            Rows: rows));
    }
}
