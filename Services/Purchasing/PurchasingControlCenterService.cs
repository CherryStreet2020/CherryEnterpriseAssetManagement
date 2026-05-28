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
    private readonly ILogger<PurchasingControlCenterService> _log;

    public PurchasingControlCenterService(
        AppDbContext db,
        ITenantContext tenant,
        ILogger<PurchasingControlCenterService> log)
    {
        _db = db;
        _tenant = tenant;
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

        // Vendor WIP value
        var wipQ = _db.Set<VendorWipBalance>()
            .AsNoTracking()
            .Where(w => visible.Contains(w.CompanyId)
                        && w.QuantityAtVendor > 0);
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
                NextActionHint: SuggestNextAction(d, queueType));
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

        var totalCount = costExceptionTotal + lateWipTotal;
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
            // Append rather than overwrite for audit trail.
            var stamp = $"[{nowUtc:yyyy-MM-dd HH:mm}Z {request.UserName ?? "system"}] {previous}->{newState}: {request.Notes}";
            d.BuyerActionNotes = string.IsNullOrEmpty(d.BuyerActionNotes)
                ? stamp
                : d.BuyerActionNotes + "\n" + stamp;
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
}
