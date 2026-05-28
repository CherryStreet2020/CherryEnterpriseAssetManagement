// =============================================================================
// CherryAI EAM — PurchasingRecommendationService (Sprint 15.3 PR-15)
//
// CLOSES Wave 3 of the 20-PR Purchasing Cascade.
//
// Implementation of the §18 buyer recommendation engine. Pure read; no
// writes. Composes IAutoPurchaseService for §16 decision context and
// resolves to one of 11 §18 RecommendedAction patterns plus a
// RecommendationRisk classification.
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

public class PurchasingRecommendationService : IPurchasingRecommendationService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAutoPurchaseService _autoPo;
    private readonly ILogger<PurchasingRecommendationService> _log;

    public PurchasingRecommendationService(
        AppDbContext db,
        ITenantContext tenant,
        IAutoPurchaseService autoPo,
        ILogger<PurchasingRecommendationService> log)
    {
        _db = db;
        _tenant = tenant;
        _autoPo = autoPo;
        _log = log;
    }

    // ═════════════════════════════════════════════════════════════════════
    // PUBLIC ENTRY POINTS
    // ═════════════════════════════════════════════════════════════════════

    public async Task<Result<PurchasingRecommendation>> GetRecommendationAsync(
        int demandId,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;

        var demand = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Include(d => d.ProductionOrder)
            .Where(d => d.Id == demandId && visible.Contains(d.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (demand == null)
            return Result.Failure<PurchasingRecommendation>(
                $"ProductionSupplyDemand {demandId} not found or out of tenant scope.");

        var (vendorName, poNumber) = await HydrateLookupsAsync(demand, ct);

        // (P2-A fix) Actually compose IAutoPurchaseService when the single-
        // demand entry point is hit. The §16 evaluation enriches the
        // recommendation Notes with structured triggers + blockers so
        // buyers see "why" alongside "what to do". Bulk/queue paths
        // intentionally skip this (per-row autoPo round-trip would N+1 the
        // queue render); they get the cheap pure BuildFromDemand path.
        var rec = BuildFromDemand(
            demand,
            productionOrderNumber: demand.ProductionOrder?.OrderNumber,
            linkedPoNumber: poNumber,
            suggestedVendorName: vendorName);

        var auto = await _autoPo.EvaluateDemandAsync(demandId, ct);
        if (auto.IsSuccess && auto.Value is not null)
        {
            var a = auto.Value;
            var noteParts = new List<string>
            {
                $"§16 decision: {a.Decision}",
            };
            if (a.Triggers.Count > 0)
                noteParts.Add($"triggers: {string.Join(", ", a.Triggers)}");
            if (a.Blockers.Count > 0)
                noteParts.Add($"blockers: {string.Join(", ", a.Blockers)}");
            rec = rec with { Notes = string.Join(" · ", noteParts) };
        }

        return Result.Success(rec);
    }

    public async Task<Result<IReadOnlyList<PurchasingRecommendation>>> GetRecommendationsForProAsync(
        int productionOrderId,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var demands = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Include(d => d.ProductionOrder)
            .Where(d => d.ProductionOrderId == productionOrderId
                        && visible.Contains(d.CompanyId)
                        && d.BuyerActionState != BuyerActionState.Closed
                        && d.BuyerActionState != BuyerActionState.Cancelled)
            .ToListAsync(ct);

        var recs = new List<PurchasingRecommendation>(demands.Count);
        var (vendorMap, poMap) = await BulkHydrateAsync(demands, ct);
        foreach (var d in demands)
        {
            vendorMap.TryGetValue(d.VendorId ?? 0, out var vname);
            poMap.TryGetValue(d.LinkedPurchaseOrderId ?? 0, out var pnum);
            recs.Add(BuildFromDemand(
                d,
                productionOrderNumber: d.ProductionOrder?.OrderNumber,
                linkedPoNumber: pnum,
                suggestedVendorName: vname));
        }
        return Result.Success<IReadOnlyList<PurchasingRecommendation>>(recs);
    }

    public async Task<Result<PurchasingRecommendationPage>> GetRecommendationsAsync(
        PurchasingRecommendationFilter filter,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var take = Math.Clamp(filter.Take, 1, 500);
        var skip = Math.Max(0, filter.Skip);

        var q = _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Include(d => d.ProductionOrder)
            .Where(d => visible.Contains(d.CompanyId)
                        && d.BuyerActionState != BuyerActionState.Closed
                        && d.BuyerActionState != BuyerActionState.Cancelled);

        if (filter.CompanyId.HasValue) q = q.Where(d => d.CompanyId == filter.CompanyId);
        if (filter.SiteId.HasValue) q = q.Where(d => d.SiteId == filter.SiteId);
        if (filter.VendorId.HasValue) q = q.Where(d => d.VendorId == filter.VendorId);
        if (filter.ProductionOrderId.HasValue)
            q = q.Where(d => d.ProductionOrderId == filter.ProductionOrderId);
        if (filter.BuyerUserId.HasValue)
            q = q.Where(d => d.BuyerUserId == filter.BuyerUserId);

        // (Codex thread 2 fix) When OnlyAction is set, the post-filter ratio
        // is unknowable up front. Earlier `take * 4 + skip` cap meant a
        // tenant with matching rows past the first 100 due-date-sorted
        // demands would see TotalCount=0 from the API even though matching
        // rows existed deeper. Materialise the full 500-row ceiling for
        // filtered scans so the "all items for an action" interface keeps
        // its contract within reasonable bounds. Demands beyond the 500
        // ceiling still need a streaming or two-pass implementation —
        // documented Wave 4 polish.
        var fetchTake = filter.OnlyAction.HasValue
            ? 500
            : Math.Min(500, skip + take);
        var demandSlice = await q
            .OrderBy(d => d.RequiredDate ?? DateTime.MaxValue)
            .ThenBy(d => d.Id)
            .Take(fetchTake)
            .ToListAsync(ct);

        var (vendorMap, poMap) = await BulkHydrateAsync(demandSlice, ct);

        var all = new List<PurchasingRecommendation>(demandSlice.Count);
        foreach (var d in demandSlice)
        {
            vendorMap.TryGetValue(d.VendorId ?? 0, out var vname);
            poMap.TryGetValue(d.LinkedPurchaseOrderId ?? 0, out var pnum);
            all.Add(BuildFromDemand(
                d,
                productionOrderNumber: d.ProductionOrder?.OrderNumber,
                linkedPoNumber: pnum,
                suggestedVendorName: vname));
        }

        var filtered = filter.OnlyAction.HasValue
            ? all.Where(r => r.Action == filter.OnlyAction.Value).ToList()
            : all;

        var paged = filtered.Skip(skip).Take(take).ToList();
        var high = filtered.Count(r => r.Risk == RecommendationRisk.High);
        var critical = filtered.Count(r => r.Risk == RecommendationRisk.Critical);

        return Result.Success(new PurchasingRecommendationPage(
            TotalCount: filtered.Count,
            HighRiskCount: high,
            CriticalRiskCount: critical,
            Recommendations: paged));
    }

    // ═════════════════════════════════════════════════════════════════════
    // BUILDER — pure, no I/O. Consumed both by the public APIs and by
    // PurchasingControlCenterService.GetSupplyDemandQueueAsync so the
    // Supply Demand queue shows real §18 hints without a per-row round trip.
    // ═════════════════════════════════════════════════════════════════════

    public PurchasingRecommendation BuildFromDemand(
        ProductionSupplyDemand d,
        string? productionOrderNumber = null,
        string? linkedPoNumber = null,
        string? suggestedVendorName = null)
    {
        // (P1-A fix) `productionOrderNumber` is the PRO's OrderNumber for
        // display in `ProductionOrderNumber`. `linkedPoNumber` is the linked
        // PurchaseOrder.PONumber for display in `ExistingPurchaseOrderNumber`.
        // Earlier revision overloaded one param to mean both, which sent the
        // PO number into the PRO slot on three of four call sites.
        var today = DateTime.UtcNow.Date;
        int? daysUntilRequired = d.RequiredDate.HasValue
            ? (int)Math.Round((d.RequiredDate.Value.Date - today).TotalDays)
            : (int?)null;

        var risk = ClassifyRisk(d, daysUntilRequired);
        var (action, reason) = PickAction(d, daysUntilRequired);

        // Suggested order date: required-date minus a notional 14-day lead time
        // when the action is buy-side, otherwise null. Real lead-time math is a
        // sourcing-rule lookup which lives in Wave 4 polish.
        DateTime? suggestedOrderDate = null;
        if ((action == RecommendedAction.CreatePo
             || action == RecommendedAction.CreateRfq
             || action == RecommendedAction.CreateSubcontractPo
             || action == RecommendedAction.CreateTransfer)
            && d.RequiredDate.HasValue)
        {
            suggestedOrderDate = d.RequiredDate.Value.AddDays(-14).Date;
        }

        return new PurchasingRecommendation(
            DemandId: d.Id,
            DemandNumber: d.DemandNumber,
            ProductionOrderId: d.ProductionOrderId,
            ProductionOrderNumber: productionOrderNumber ?? d.ProductionOrder?.OrderNumber,
            OperationSequence: d.OperationSequence,
            PartNumber: d.PartNumber,
            Action: action,
            ActionLabel: Label(action),
            Reason: reason,
            SuggestedVendorId: d.VendorId,
            SuggestedVendorName: suggestedVendorName,
            RecommendedQuantity: d.RemainingQuantity > 0 ? d.RemainingQuantity : d.RequiredQuantity,
            RequiredDate: d.RequiredDate,
            SuggestedOrderDate: suggestedOrderDate,
            ExistingPurchaseOrderId: d.LinkedPurchaseOrderId,
            ExistingPurchaseOrderNumber: linkedPoNumber,
            ExistingChildProductionOrderId: d.LinkedChildProductionOrderId,
            ExistingInventoryHint: string.IsNullOrEmpty(d.LinkedInventoryReservation) ? null : d.LinkedInventoryReservation,
            Risk: risk,
            DaysUntilRequired: daysUntilRequired,
            Notes: null);
    }

    // ═════════════════════════════════════════════════════════════════════
    // §18 PATTERN PICKER — pure, side-effect free. 11 patterns + a few
    // graceful fall-throughs.
    // ═════════════════════════════════════════════════════════════════════

    private static (RecommendedAction Action, string Reason) PickAction(
        ProductionSupplyDemand d,
        int? daysUntilRequired)
    {
        // ─── Terminal states ───────────────────────────────────────────
        if (d.BuyerActionState == BuyerActionState.Closed
            || d.BuyerActionState == BuyerActionState.Cancelled
            || d.SupplyStatus == DemandSupplyStatus.Closed)
        {
            return (RecommendedAction.NoActionSatisfied,
                "Demand is closed — no action required.");
        }

        if (d.SupplyStatus == DemandSupplyStatus.FullyFulfilled
            && d.RemainingQuantity <= 0)
        {
            return (RecommendedAction.NoActionSatisfied,
                "Demand is fully fulfilled — close when ready.");
        }

        // ─── Blocked → unblock ──────────────────────────────────────────
        if (d.BuyerActionState == BuyerActionState.Blocked)
        {
            return (RecommendedAction.Unblock,
                "Demand is blocked — clear the gating issue (drawing / supplier / approval) before next step.");
        }

        // ─── Awaiting approval → wait (Codex thread 1 fix) ──────────────
        // A buy demand with vendor + no linked PO would otherwise fall
        // through to CreatePo and tell the buyer to act before the approval
        // gate has cleared. Surface the approval-pending state explicitly so
        // it survives the rest of the pattern dispatch.
        if (d.BuyerActionState == BuyerActionState.AwaitingApproval)
        {
            return (RecommendedAction.Wait,
                "Demand is awaiting approval — clear the approval gate before next purchasing action.");
        }

        // ─── Cost-variance review (§18 pattern 11) ──────────────────────
        if (d.CostStatus == DemandCostStatus.VariancePending)
        {
            return (RecommendedAction.ReviewCostVariance,
                "Cost variance pending against this demand — settle or escalate.");
        }

        // ─── Inspection-pending (§18 pattern 10) ────────────────────────
        if (d.SupplyStatus == DemandSupplyStatus.InInspection)
        {
            return (RecommendedAction.NotifyQualityInspection,
                "Receipt in incoming inspection — notify quality / push for sign-off.");
        }

        // ─── Subcontract pipeline (§18 patterns 8 + 9) ─────────────────
        if (d.SourceType == DemandSourceType.Subcontract
            || d.SupplyPolicy == SupplyPolicy.BuyToVendorLocation)
        {
            if (d.LinkedPurchaseOrderId == null)
            {
                return (RecommendedAction.CreateSubcontractPo,
                    "Subcontract operation released — create the service PO and prepare WIP shipment.");
            }
            if (d.SupplyStatus == DemandSupplyStatus.AtVendor)
            {
                return (RecommendedAction.Wait,
                    "WIP currently at vendor — monitor processing window.");
            }
            if (d.SupplyStatus == DemandSupplyStatus.Committed
                || d.SupplyStatus == DemandSupplyStatus.PartiallyFulfilled)
            {
                return (RecommendedAction.ShipToVendor,
                    "Subcontract PO open + WIP ready — ship to vendor for outside processing.");
            }
        }

        // ─── Existing linked supply → expedite or wait (§18 pattern 4) ──
        if (d.LinkedPurchaseOrderId.HasValue)
        {
            if (d.ShortageStatus == DemandShortageStatus.Late
                || d.ShortageStatus == DemandShortageStatus.Critical
                || (daysUntilRequired.HasValue && daysUntilRequired.Value < 0))
            {
                return (RecommendedAction.ExpeditePo,
                    "Linked PO exists but supply is late or at risk — expedite the PO.");
            }
            if (d.SupplyStatus == DemandSupplyStatus.Committed
                && d.AlertStatus == DemandAlertStatus.Critical)
            {
                return (RecommendedAction.ExpeditePo,
                    "Linked PO committed but alerted critical — expedite to protect downstream operation.");
            }
            return (RecommendedAction.Wait,
                "Linked PO is in flight — track receipt against promise date.");
        }

        // ─── Inventory reservation pointer (§18 pattern 2) ──────────────
        if (!string.IsNullOrEmpty(d.LinkedInventoryReservation))
        {
            return (RecommendedAction.ReserveAndIssue,
                "Inventory reservation already linked — issue to the job.");
        }

        // ─── Child WO supply ────────────────────────────────────────────
        if (d.LinkedChildProductionOrderId.HasValue)
        {
            return (RecommendedAction.Wait,
                "Child work order is supplying this demand — track its completion.");
        }

        // ─── Transfer recommended (§18 pattern 3) ───────────────────────
        // (Codex thread 0 fix) Internal-transfer demands typically have no
        // VendorId — they're satisfied by inventory motion, not a supplier.
        // Match the policy BEFORE the "no supplier" check below so a transfer
        // demand never gets misrouted as RequestSourcing and stays visible
        // under OnlyAction = CreateTransfer scans.
        if (d.SupplyPolicy == SupplyPolicy.TransferFromWarehouse
            || d.SupplyPolicy == SupplyPolicy.TransferFromJob)
        {
            return (RecommendedAction.CreateTransfer,
                "Demand resolves via internal transfer — create the transfer order.");
        }

        // ─── No supplier (§18 pattern 6) ────────────────────────────────
        if (d.VendorId == null && d.SourceStatus == DemandSourceStatus.NotDetermined)
        {
            return (RecommendedAction.RequestSourcing,
                "No supplier resolved — request sourcing or AVL update.");
        }

        // ─── Inventory-first policy with available stock (§18 pattern 2) ──
        if (d.SupplyPolicy == SupplyPolicy.InventoryFirstThenBuy
            && d.ReservedQuantity > 0 && d.RemainingQuantity > 0)
        {
            return (RecommendedAction.ReserveAndIssue,
                "Partial reservation in place — issue what's reserved, then buy the shortfall.");
        }

        // ─── Buy policies → create PO (§18 pattern 1) ───────────────────
        bool isBuyPolicy = d.SupplyPolicy == SupplyPolicy.BuyDirectToJob
                           || d.SupplyPolicy == SupplyPolicy.BuyDirectToOperation
                           || d.SupplyPolicy == SupplyPolicy.InventoryFirstThenBuy;
        if (isBuyPolicy)
        {
            if (d.VendorId == null)
            {
                return (RecommendedAction.RequestSourcing,
                    "Buy policy but no supplier resolved — request sourcing first.");
            }
            // RFQ pattern (§18 pattern 7): inferred only when explicitly hinted
            // via Notes prefix — full RFQ integration ships in Wave 4 PR-20.
            if (!string.IsNullOrEmpty(d.Notes)
                && d.Notes.StartsWith("RFQ:", StringComparison.OrdinalIgnoreCase))
            {
                return (RecommendedAction.CreateRfq,
                    "Sourcing flagged demand as quote-required — create RFQ before PO.");
            }
            return (RecommendedAction.CreatePo,
                "Buy policy + supplier known + no linked supply — create a job-linked PO.");
        }

        // ─── Make-direct-to-job ────────────────────────────────────────
        if (d.SupplyPolicy == SupplyPolicy.MakeDirectToJob)
        {
            return (RecommendedAction.Wait,
                "Make-direct-to-job — schedule a child work order (no buy action needed).");
        }

        // ─── Floorstock / customer-supplied / consigned ─────────────────
        if (d.SupplyPolicy == SupplyPolicy.Floorstock)
            return (RecommendedAction.NoActionSatisfied,
                "Floorstock policy — consume from kanban, no demand action required.");
        if (d.CustomerOwned)
            return (RecommendedAction.Wait,
                "Customer-owned material — track customer shipment.");
        if (d.Consigned)
            return (RecommendedAction.Wait,
                "Consigned material — coordinate with supplier consignment.");

        // ─── Catch-all ─────────────────────────────────────────────────
        return (RecommendedAction.ReviewManually,
            "No automatic recommendation — buyer review required.");
    }

    private static RecommendationRisk ClassifyRisk(
        ProductionSupplyDemand d,
        int? daysUntilRequired)
    {
        if (d.ShortageStatus == DemandShortageStatus.Critical
            || d.AlertStatus == DemandAlertStatus.Critical
            || (d.ProjectId.HasValue && d.ShortageStatus == DemandShortageStatus.Late))
        {
            return RecommendationRisk.Critical;
        }

        if (d.ShortageStatus == DemandShortageStatus.Late
            || (daysUntilRequired.HasValue && daysUntilRequired.Value < 0))
        {
            return RecommendationRisk.High;
        }

        if (d.ShortageStatus == DemandShortageStatus.Warning
            || (daysUntilRequired.HasValue && daysUntilRequired.Value <= 7))
        {
            return RecommendationRisk.Medium;
        }

        return RecommendationRisk.Low;
    }

    private static string Label(RecommendedAction a) => a switch
    {
        RecommendedAction.CreatePo => "Create PO",
        RecommendedAction.ReserveAndIssue => "Reserve and issue",
        RecommendedAction.CreateTransfer => "Create transfer",
        RecommendedAction.ExpeditePo => "Expedite PO",
        RecommendedAction.LinkPoToDemand => "Link PO",
        RecommendedAction.RequestSourcing => "Request sourcing",
        RecommendedAction.CreateRfq => "Create RFQ",
        RecommendedAction.CreateSubcontractPo => "Create subcontract PO",
        RecommendedAction.ShipToVendor => "Ship to vendor",
        RecommendedAction.NotifyQualityInspection => "Notify quality",
        RecommendedAction.ReviewCostVariance => "Review cost variance",
        RecommendedAction.NoActionSatisfied => "No action",
        RecommendedAction.Unblock => "Unblock",
        RecommendedAction.Wait => "Wait",
        RecommendedAction.ReviewManually => "Review",
        _ => a.ToString(),
    };

    // ═════════════════════════════════════════════════════════════════════
    // HYDRATION — bulk vendor + PO lookups so the recommendation builder
    // doesn't need to N-times round-trip the DB.
    // ═════════════════════════════════════════════════════════════════════

    private async Task<(string? VendorName, string? PoNumber)> HydrateLookupsAsync(
        ProductionSupplyDemand d,
        CancellationToken ct)
    {
        var visible = _tenant.VisibleCompanyIds;
        string? vendorName = null;
        string? poNumber = null;

        if (d.VendorId.HasValue)
        {
            vendorName = await _db.Set<Vendor>()
                .AsNoTracking()
                .Where(v => v.Id == d.VendorId.Value
                            && (v.CompanyId == null || visible.Contains(v.CompanyId.Value)))
                .Select(v => v.Name)
                .FirstOrDefaultAsync(ct);
        }

        if (d.LinkedPurchaseOrderId.HasValue)
        {
            poNumber = await _db.Set<PurchaseOrder>()
                .AsNoTracking()
                .Where(p => p.Id == d.LinkedPurchaseOrderId.Value
                            && p.CompanyId != null
                            && visible.Contains(p.CompanyId.Value))
                .Select(p => p.PONumber)
                .FirstOrDefaultAsync(ct);
        }

        return (vendorName, poNumber);
    }

    private async Task<(IDictionary<int, string> Vendors, IDictionary<int, string> Pos)> BulkHydrateAsync(
        IReadOnlyList<ProductionSupplyDemand> demands,
        CancellationToken ct)
    {
        var visible = _tenant.VisibleCompanyIds;
        var vendorIds = demands.Where(d => d.VendorId.HasValue)
            .Select(d => d.VendorId!.Value).Distinct().ToList();
        var poIds = demands.Where(d => d.LinkedPurchaseOrderId.HasValue)
            .Select(d => d.LinkedPurchaseOrderId!.Value).Distinct().ToList();

        var vendorMap = vendorIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Set<Vendor>()
                .AsNoTracking()
                .Where(v => vendorIds.Contains(v.Id)
                            && (v.CompanyId == null || visible.Contains(v.CompanyId.Value)))
                .ToDictionaryAsync(v => v.Id, v => v.Name, ct);

        var poMap = poIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Set<PurchaseOrder>()
                .AsNoTracking()
                .Where(p => poIds.Contains(p.Id)
                            && p.CompanyId != null
                            && visible.Contains(p.CompanyId.Value))
                .ToDictionaryAsync(p => p.Id, p => p.PONumber, ct);

        return (vendorMap, poMap);
    }
}
