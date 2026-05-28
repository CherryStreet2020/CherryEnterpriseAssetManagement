// =============================================================================
// CherryAI EAM — DemandConsolidationService (Sprint 15.3 PR-14)
//
// Implementation of the §17 6-mode planner. Pure read; no writes.
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

public class DemandConsolidationService : IDemandConsolidationService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<DemandConsolidationService> _log;

    /// <summary>
    /// Default bucket window for SupplierDate consolidation when the caller
    /// doesn't specify one. 7 days matches the common "weekly buy" cadence;
    /// configurable per-request via ConsolidationRequest.RequiredDateBucketDays.
    /// </summary>
    public const int DefaultRequiredDateBucketDays = 7;

    public DemandConsolidationService(
        AppDbContext db,
        ITenantContext tenant,
        ILogger<DemandConsolidationService> log)
    {
        _db = db;
        _tenant = tenant;
        _log = log;
    }

    // ═════════════════════════════════════════════════════════════════════
    // PLAN — generic entry point
    // ═════════════════════════════════════════════════════════════════════

    public async Task<Result<ConsolidationPlan>> PlanAsync(
        ConsolidationRequest request,
        CancellationToken ct = default)
    {
        if (request == null || request.DemandIds == null || request.DemandIds.Count == 0)
            return Result.Failure<ConsolidationPlan>("ConsolidationRequest needs at least one DemandId.");

        var visible = _tenant.VisibleCompanyIds;
        var ids = request.DemandIds.ToHashSet();

        var demands = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Include(d => d.ProductionOrder)
            .Where(d => ids.Contains(d.Id) && visible.Contains(d.CompanyId))
            .ToListAsync(ct);

        if (demands.Count == 0)
            return Result.Failure<ConsolidationPlan>(
                "No demands found in tenant scope for the given IDs.");

        // Skip demands that can't be planned: already linked / terminal /
        // missing required quantity. Surface them in SkippedDemandIds so the
        // caller can show "X demands skipped" and the reasons.
        var skipped = new List<int>();
        var planable = new List<ProductionSupplyDemand>();
        foreach (var d in demands)
        {
            if (d.RemainingQuantity <= 0
                || d.BuyerActionState == BuyerActionState.Closed
                || d.BuyerActionState == BuyerActionState.Cancelled
                || d.LinkedPurchaseOrderId.HasValue)
            {
                skipped.Add(d.Id);
            }
            else
            {
                planable.Add(d);
            }
        }

        // Dispatch by mode.
        var bucketDays = request.RequiredDateBucketDays
                         ?? DefaultRequiredDateBucketDays;
        var defaultVendorId = request.DefaultVendorId;

        IReadOnlyList<ConsolidationPlanLine> lines = request.Mode switch
        {
            DemandConsolidationMode.StrictJobSpecific or DemandConsolidationMode.None =>
                PlanStrict(planable, defaultVendorId),
            DemandConsolidationMode.SupplierDate =>
                PlanSupplierDate(planable, defaultVendorId, bucketDays),
            DemandConsolidationMode.Project =>
                PlanProject(planable, defaultVendorId),
            DemandConsolidationMode.Inventory =>
                PlanInventory(planable, defaultVendorId),
            DemandConsolidationMode.SubcontractBatch =>
                PlanSubcontractBatch(planable, defaultVendorId),
            _ => Array.Empty<ConsolidationPlanLine>(),
        };

        // (P2-F fix) Drop any plan lines with no resolvable vendor — pushing
        // a VendorId=0 line into IPurchasingService.AddLineAsync would create
        // a PO line attached to a non-existent vendor. Move every demand on
        // those lines into SkippedDemandIds with explicit traceability so the
        // buyer sees "X demands skipped — no vendor resolved".
        var vendorlessLines = lines.Where(l => l.VendorId <= 0).ToList();
        if (vendorlessLines.Count > 0)
        {
            foreach (var l in vendorlessLines)
                foreach (var a in l.Allocations)
                    if (!skipped.Contains(a.DemandId)) skipped.Add(a.DemandId);
            lines = lines.Where(l => l.VendorId > 0).ToList();
        }

        // Fill VendorName for the planned lines so the UI doesn't make N
        // extra round-trips.
        lines = await HydrateVendorNamesAsync(lines, ct);

        var planNotes = vendorlessLines.Count > 0
            ? (request.Notes ?? string.Empty) +
              $" [auto-skip: {vendorlessLines.Count} plan line(s) had no vendor resolved — pass DefaultVendorId or set VendorId on the demands]"
            : request.Notes;

        return Result.Success(new ConsolidationPlan(
            Mode: request.Mode,
            InputDemandCount: demands.Count,
            PlannedLineCount: lines.Count,
            SkippedDemandCount: skipped.Count,
            Lines: lines,
            SkippedDemandIds: skipped,
            Notes: planNotes));
    }

    public async Task<Result<ConsolidationPlan>> PlanForProductionOrderAsync(
        int productionOrderId,
        DemandConsolidationMode mode,
        CancellationToken ct = default)
    {
        var visible = _tenant.VisibleCompanyIds;
        var demandIds = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Where(d => d.ProductionOrderId == productionOrderId
                        && visible.Contains(d.CompanyId)
                        && d.BuyerActionState != BuyerActionState.Closed
                        && d.BuyerActionState != BuyerActionState.Cancelled)
            .Select(d => d.Id)
            .ToListAsync(ct);

        if (demandIds.Count == 0)
            return Result.Failure<ConsolidationPlan>(
                $"PRO {productionOrderId} has no open demands in tenant scope.");

        return await PlanAsync(new ConsolidationRequest(demandIds, mode), ct);
    }

    public async Task<Result<(DemandConsolidationMode Mode, string Reason)>> SuggestModeAsync(
        IReadOnlyList<int> demandIds,
        CancellationToken ct = default)
    {
        if (demandIds == null || demandIds.Count == 0)
            return Result.Failure<(DemandConsolidationMode, string)>(
                "Suggest needs at least one DemandId.");

        var visible = _tenant.VisibleCompanyIds;
        var ids = demandIds.ToHashSet();
        var demands = await _db.Set<ProductionSupplyDemand>()
            .AsNoTracking()
            .Where(d => ids.Contains(d.Id) && visible.Contains(d.CompanyId))
            .ToListAsync(ct);

        if (demands.Count == 0)
            return Result.Failure<(DemandConsolidationMode, string)>(
                "No demands found in tenant scope.");
        if (demands.Count == 1)
            return Result.Success<(DemandConsolidationMode, string)>(
                (DemandConsolidationMode.StrictJobSpecific, "Single demand — strict mode is appropriate."));

        // Heuristics, in priority order:
        //   1. All subcontract → SubcontractBatch
        //   2. All same vendor + RequiredDate within a 7-day window → SupplierDate
        //   3. All same Project/Customer → Project
        //   4. All inventory-stocking demands → Inventory
        //   5. Otherwise → StrictJobSpecific (safer default)
        bool allSubcontract = demands.All(d =>
            d.SourceType == DemandSourceType.Subcontract
            || d.SupplyPolicy == SupplyPolicy.BuyToVendorLocation);
        if (allSubcontract)
            return Result.Success<(DemandConsolidationMode, string)>(
                (DemandConsolidationMode.SubcontractBatch,
                 "All demands are subcontract operations — batch by vendor + service item."));

        var distinctVendors = demands.Select(d => d.VendorId).Where(v => v.HasValue).Distinct().Count();
        var datesWithin = demands.Where(d => d.RequiredDate.HasValue)
            .Select(d => d.RequiredDate!.Value.Date).ToList();
        bool dateBucketTight = datesWithin.Count >= 2
            && (datesWithin.Max() - datesWithin.Min()).TotalDays <= DefaultRequiredDateBucketDays;
        if (distinctVendors == 1 && dateBucketTight)
            return Result.Success<(DemandConsolidationMode, string)>(
                (DemandConsolidationMode.SupplierDate,
                 $"All demands target the same vendor and required dates are within {DefaultRequiredDateBucketDays} days."));

        var distinctProjects = demands.Select(d => d.ProjectId).Where(p => p.HasValue).Distinct().Count();
        if (distinctProjects == 1
            && demands.All(d => d.ProjectId.HasValue))
        {
            return Result.Success<(DemandConsolidationMode, string)>(
                (DemandConsolidationMode.Project,
                 "All demands belong to the same project — consolidate by project."));
        }

        bool allInventory = demands.All(d =>
            d.SupplyPolicy == SupplyPolicy.InventoryFirstThenBuy
            || d.SupplyPolicy == SupplyPolicy.MakeToStockReserve);
        if (allInventory)
            return Result.Success<(DemandConsolidationMode, string)>(
                (DemandConsolidationMode.Inventory,
                 "All demands are inventory-replenishment — one PO line, allocate at receipt."));

        return Result.Success<(DemandConsolidationMode, string)>(
            (DemandConsolidationMode.StrictJobSpecific,
             "No strong consolidation signal — recommend strict job-specific to preserve traceability."));
    }

    // ═════════════════════════════════════════════════════════════════════
    // MODE PLANNERS — each returns ConsolidationPlanLine[] from the same
    // input demand set. Every planner preserves the §17 invariant: each
    // PO line's Allocations sum to its PlannedQuantity.
    // ═════════════════════════════════════════════════════════════════════

    private static IReadOnlyList<ConsolidationPlanLine> PlanStrict(
        List<ProductionSupplyDemand> demands,
        int? defaultVendorId)
    {
        var lines = new List<ConsolidationPlanLine>(demands.Count);
        int n = 1;
        foreach (var d in demands)
        {
            lines.Add(new ConsolidationPlanLine(
                LineNumber: n++,
                ItemId: d.ItemId,
                PartNumber: d.PartNumber,
                Revision: d.Revision,
                VendorId: d.VendorId ?? defaultVendorId ?? 0,
                VendorName: null, // hydrated post-plan
                PlannedQuantity: d.RemainingQuantity,
                Uom: d.Uom,
                RequiredDate: d.RequiredDate,
                PromiseDate: null,
                ProjectId: d.ProjectId,
                ServiceItemId: null,
                Notes: $"Strict job-specific — demand {d.DemandNumber}",
                Allocations: new[] { Alloc(d, d.RemainingQuantity) }));
        }
        return lines;
    }

    private static IReadOnlyList<ConsolidationPlanLine> PlanSupplierDate(
        List<ProductionSupplyDemand> demands,
        int? defaultVendorId,
        int bucketDays)
    {
        // Group by (VendorId ?? default, ItemId, Revision, RequiredDate-bucket).
        // Demands without a required date land in a single "no-date" bucket
        // so they still consolidate.
        DateTime BucketOf(DateTime? d) => d.HasValue
            ? d.Value.Date.AddDays(-(int)(d.Value.DayOfYear % bucketDays))
            : DateTime.MinValue;

        var groups = demands
            .GroupBy(d => new
            {
                Vendor = d.VendorId ?? defaultVendorId ?? 0,
                Item = d.ItemId,
                Rev = d.Revision,
                Bucket = BucketOf(d.RequiredDate),
            })
            .ToList();

        var lines = new List<ConsolidationPlanLine>();
        int n = 1;
        foreach (var g in groups)
        {
            var first = g.First();
            var earliestDate = g.Where(d => d.RequiredDate.HasValue)
                .Min(d => (DateTime?)d.RequiredDate!.Value);
            var totalQty = g.Sum(d => d.RemainingQuantity);
            var allocs = g.Select(d => Alloc(d, d.RemainingQuantity)).ToList();

            lines.Add(new ConsolidationPlanLine(
                LineNumber: n++,
                ItemId: first.ItemId,
                PartNumber: first.PartNumber,
                Revision: first.Revision,
                VendorId: g.Key.Vendor,
                VendorName: null,
                PlannedQuantity: totalQty,
                Uom: first.Uom,
                RequiredDate: earliestDate, // earliest required date wins
                PromiseDate: null,
                ProjectId: null,
                ServiceItemId: null,
                Notes: $"Supplier+Date consolidation ({bucketDays}-day bucket) — {g.Count()} demand(s)",
                Allocations: allocs));
        }
        return lines;
    }

    private static IReadOnlyList<ConsolidationPlanLine> PlanProject(
        List<ProductionSupplyDemand> demands,
        int? defaultVendorId)
    {
        // Group by (ProjectId, ItemId, Revision). Demands with no project
        // fall back to per-demand lines (project consolidation doesn't apply).
        var withProject = demands.Where(d => d.ProjectId.HasValue).ToList();
        var without = demands.Where(d => !d.ProjectId.HasValue).ToList();

        var groups = withProject
            .GroupBy(d => new { Project = d.ProjectId!.Value, Item = d.ItemId, Rev = d.Revision })
            .ToList();

        var lines = new List<ConsolidationPlanLine>();
        int n = 1;
        foreach (var g in groups)
        {
            var first = g.First();
            var earliestDate = g.Where(d => d.RequiredDate.HasValue)
                .Min(d => (DateTime?)d.RequiredDate!.Value);
            var totalQty = g.Sum(d => d.RemainingQuantity);
            var vendorId = first.VendorId ?? defaultVendorId ?? 0;
            var allocs = g.Select(d => Alloc(d, d.RemainingQuantity)).ToList();

            lines.Add(new ConsolidationPlanLine(
                LineNumber: n++,
                ItemId: first.ItemId,
                PartNumber: first.PartNumber,
                Revision: first.Revision,
                VendorId: vendorId,
                VendorName: null,
                PlannedQuantity: totalQty,
                Uom: first.Uom,
                RequiredDate: earliestDate,
                PromiseDate: null,
                ProjectId: g.Key.Project,
                ServiceItemId: null,
                Notes: $"Project consolidation — project {g.Key.Project}, {g.Count()} demand(s)",
                Allocations: allocs));
        }

        // Fallback: demands without project ⇒ strict.
        foreach (var d in without)
        {
            lines.Add(new ConsolidationPlanLine(
                LineNumber: n++,
                ItemId: d.ItemId,
                PartNumber: d.PartNumber,
                Revision: d.Revision,
                VendorId: d.VendorId ?? defaultVendorId ?? 0,
                VendorName: null,
                PlannedQuantity: d.RemainingQuantity,
                Uom: d.Uom,
                RequiredDate: d.RequiredDate,
                PromiseDate: null,
                ProjectId: null,
                ServiceItemId: null,
                Notes: $"No project on demand {d.DemandNumber} — strict line",
                Allocations: new[] { Alloc(d, d.RemainingQuantity) }));
        }
        return lines;
    }

    private static IReadOnlyList<ConsolidationPlanLine> PlanInventory(
        List<ProductionSupplyDemand> demands,
        int? defaultVendorId)
    {
        // Group purely by (ItemId, Revision, VendorId). Required-date sets to
        // the earliest demand's date. Buy-to-stock semantics — receipt
        // allocates to the demands proportionally at GR posting time.
        var groups = demands
            .GroupBy(d => new
            {
                Item = d.ItemId,
                Rev = d.Revision,
                Vendor = d.VendorId ?? defaultVendorId ?? 0,
            })
            .ToList();

        var lines = new List<ConsolidationPlanLine>();
        int n = 1;
        foreach (var g in groups)
        {
            var first = g.First();
            var earliestDate = g.Where(d => d.RequiredDate.HasValue)
                .Min(d => (DateTime?)d.RequiredDate!.Value);
            var totalQty = g.Sum(d => d.RemainingQuantity);
            var allocs = g.Select(d => Alloc(d, d.RemainingQuantity)).ToList();

            lines.Add(new ConsolidationPlanLine(
                LineNumber: n++,
                ItemId: first.ItemId,
                PartNumber: first.PartNumber,
                Revision: first.Revision,
                VendorId: g.Key.Vendor,
                VendorName: null,
                PlannedQuantity: totalQty,
                Uom: first.Uom,
                RequiredDate: earliestDate,
                PromiseDate: null,
                ProjectId: null,
                ServiceItemId: null,
                Notes: $"Inventory consolidation — buy-to-stock for {g.Count()} demand(s)",
                Allocations: allocs));
        }
        return lines;
    }

    private static IReadOnlyList<ConsolidationPlanLine> PlanSubcontractBatch(
        List<ProductionSupplyDemand> demands,
        int? defaultVendorId)
    {
        // Group by (VendorId, "service signature"). For subcontract demands
        // the ItemId is the service item (per §17 + the SubcontractOperation
        // model). Demands that aren't subcontract fall back to strict.
        var subcontract = demands.Where(d =>
            d.SourceType == DemandSourceType.Subcontract
            || d.SupplyPolicy == SupplyPolicy.BuyToVendorLocation).ToList();
        var other = demands.Except(subcontract).ToList();

        var groups = subcontract
            .GroupBy(d => new
            {
                Vendor = d.VendorId ?? defaultVendorId ?? 0,
                Service = d.ItemId,
            })
            .ToList();

        var lines = new List<ConsolidationPlanLine>();
        int n = 1;
        foreach (var g in groups)
        {
            var first = g.First();
            var earliestDate = g.Where(d => d.RequiredDate.HasValue)
                .Min(d => (DateTime?)d.RequiredDate!.Value);
            var totalQty = g.Sum(d => d.RemainingQuantity);
            var allocs = g.Select(d => Alloc(d, d.RemainingQuantity)).ToList();

            lines.Add(new ConsolidationPlanLine(
                LineNumber: n++,
                ItemId: first.ItemId,
                PartNumber: first.PartNumber,
                Revision: first.Revision,
                VendorId: g.Key.Vendor,
                VendorName: null,
                PlannedQuantity: totalQty,
                Uom: first.Uom,
                RequiredDate: earliestDate,
                PromiseDate: null,
                ProjectId: null,
                ServiceItemId: g.Key.Service,
                Notes: $"Subcontract batch — vendor {g.Key.Vendor}, service item {g.Key.Service?.ToString() ?? "?"}, {g.Count()} op(s)",
                Allocations: allocs));
        }

        // Fallback: non-subcontract demands in the batch input ⇒ strict.
        foreach (var d in other)
        {
            lines.Add(new ConsolidationPlanLine(
                LineNumber: n++,
                ItemId: d.ItemId,
                PartNumber: d.PartNumber,
                Revision: d.Revision,
                VendorId: d.VendorId ?? defaultVendorId ?? 0,
                VendorName: null,
                PlannedQuantity: d.RemainingQuantity,
                Uom: d.Uom,
                RequiredDate: d.RequiredDate,
                PromiseDate: null,
                ProjectId: d.ProjectId,
                ServiceItemId: null,
                Notes: $"Non-subcontract demand {d.DemandNumber} — strict line within subcontract batch request",
                Allocations: new[] { Alloc(d, d.RemainingQuantity) }));
        }
        return lines;
    }

    private static ConsolidationAllocation Alloc(ProductionSupplyDemand d, decimal qty)
    {
        return new ConsolidationAllocation(
            DemandId: d.Id,
            DemandNumber: d.DemandNumber,
            ProductionOrderId: d.ProductionOrderId,
            ProductionOrderNumber: d.ProductionOrder?.OrderNumber,
            BomLineId: d.BomLineId,
            OperationSequence: d.OperationSequence,
            ProjectId: d.ProjectId,
            AllocatedQuantity: qty);
    }

    private async Task<IReadOnlyList<ConsolidationPlanLine>> HydrateVendorNamesAsync(
        IReadOnlyList<ConsolidationPlanLine> lines,
        CancellationToken ct)
    {
        if (lines.Count == 0) return lines;
        var vendorIds = lines.Select(l => l.VendorId).Where(v => v > 0).Distinct().ToList();
        if (vendorIds.Count == 0) return lines;

        // (P2-A fix) Tenant-scoped vendor name lookup. A consolidation plan
        // should never display a vendor from another tenant.
        var visible = _tenant.VisibleCompanyIds;
        var names = await _db.Set<Vendor>()
            .AsNoTracking()
            .Where(v => vendorIds.Contains(v.Id)
                        && (v.CompanyId == null || visible.Contains(v.CompanyId.Value)))
            .ToDictionaryAsync(v => v.Id, v => v.Name, ct);

        return lines.Select(l => l with
        {
            VendorName = names.TryGetValue(l.VendorId, out var name) ? name : null,
        }).ToList();
    }
}
