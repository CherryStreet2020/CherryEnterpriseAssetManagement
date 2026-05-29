// Theme B7 Wave A PR-3 — admin probe for the estimate-as-standard variance
// baseline. Exercises VarianceBaselineMode + LockedEstimateCapturedUtc on
// ProductionOrderCostSummary and the new LockEstimateBaselineAsync service op,
// then shows that ComputeVariances reports the variance "vs locked PO estimate"
// for a PoFirst order (which has no item-master standard cost).
//
// All writes route through IProductionVarianceCloseService + ICostTransactionService;
// the estimate seed writes the summary via AppDbContext (tenant-scoped), which is
// why this probe is ControlPlaneExempt. Lock-16 corollary: every button writes.
//
//   1) Lock baseline = LockedPoEstimate   (the "lock estimate" decision-#5 op)
//   2) Revert baseline = ItemMasterStandard
//   3) Seed locked PO estimate costs       (real Ti-6Al-4V ETO bracket)
//   4) Seed actual costs + refresh         (over-run vs estimate)
//   5) Compute variances                   (shows baseline mode + estimate-to-actual)
//   R) Reload stats

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B7 PR-3 variance baseline (VarianceBaselineMode / " +
    "LockedEstimateCapturedUtc). Writes route through IProductionVarianceCloseService + " +
    "ICostTransactionService; the estimate seed writes the summary via AppDbContext, " +
    "tenant-scoped through ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class LockedEstimateVarianceProbeModel : PageModel
{
    private readonly IProductionVarianceCloseService _svc;
    private readonly ICostTransactionService _costSvc;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<LockedEstimateVarianceProbeModel> _logger;

    public LockedEstimateVarianceProbeModel(
        IProductionVarianceCloseService svc,
        ICostTransactionService costSvc,
        AppDbContext db,
        ITenantContext tenant,
        ILogger<LockedEstimateVarianceProbeModel> logger)
    {
        _svc = svc;
        _costSvc = costSvc;
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    [BindProperty] public int ProId { get; set; }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalSummaries { get; private set; }
    public int LockedPoEstimateCount { get; private set; }
    public IReadOnlyList<SummaryRow> Sample { get; private set; } = Array.Empty<SummaryRow>();

    public sealed record SummaryRow(
        int ProductionOrderId, VarianceBaselineMode BaselineMode, DateTime? LockedUtc,
        decimal EstimatedTotal, decimal ActualTotal, decimal CostVariance);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<ProductionOrderCostSummary> ScopedSummaries() =>
        _db.Set<ProductionOrderCostSummary>()
            .Where(s => _tenant.VisibleCompanyIds.Contains(s.CompanyId));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalSummaries = await ScopedSummaries().CountAsync(ct);
        LockedPoEstimateCount = await ScopedSummaries()
            .CountAsync(s => s.VarianceBaselineMode == VarianceBaselineMode.LockedPoEstimate, ct);

        Sample = await ScopedSummaries()
            .OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
            .Take(15)
            .Select(s => new SummaryRow(
                s.ProductionOrderId, s.VarianceBaselineMode, s.LockedEstimateCapturedUtc,
                s.EstimatedTotalCost, s.ActualTotalCost, s.CostVariance))
            .ToListAsync(ct);
    }

    // Resolve the target PRO id (input, or default to the latest tenant-visible PoFirst PRO).
    private async Task<int?> ResolveProIdAsync(CancellationToken ct)
    {
        if (ProId > 0) return ProId;
        return await _db.ProductionOrders
            .Where(p => _tenant.VisibleCompanyIds.Contains(p.CompanyId) && p.IsPoFirst)
            .OrderByDescending(p => p.Id)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<bool> ProVisibleAsync(int proId, CancellationToken ct) =>
        await _db.ProductionOrders.AnyAsync(
            p => p.Id == proId && _tenant.VisibleCompanyIds.Contains(p.CompanyId), ct);

    // 1) LOCK baseline = LockedPoEstimate
    public async Task<IActionResult> OnPostLockPoEstimateAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id (or create a PoFirst PRO first)."); await LoadStatsAsync(ct); return Page(); }
        if (!await ProVisibleAsync(id.Value, ct)) { Set(false, $"PRO {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        var r = await _svc.LockEstimateBaselineAsync(id.Value, VarianceBaselineMode.LockedPoEstimate, User.Identity?.Name ?? "B7-PR3-probe", ct);
        if (r.IsSuccess)
            Set(true, $"PRO #{id} variance baseline LOCKED to LockedPoEstimate at {r.Value!.LockedEstimateCapturedUtc:u} — " +
                      "the PO estimate is now the variance standard (no item master required).");
        else Set(false, r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) REVERT baseline = ItemMasterStandard
    public async Task<IActionResult> OnPostRevertStandardAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id."); await LoadStatsAsync(ct); return Page(); }
        if (!await ProVisibleAsync(id.Value, ct)) { Set(false, $"PRO {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        var r = await _svc.LockEstimateBaselineAsync(id.Value, VarianceBaselineMode.ItemMasterStandard, User.Identity?.Name ?? "B7-PR3-probe", ct);
        if (r.IsSuccess)
            Set(true, $"PRO #{id} variance baseline reverted to ItemMasterStandard (lock timestamp cleared).");
        else Set(false, r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) SEED locked PO estimate costs
    public async Task<IActionResult> OnPostSeedEstimateAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id."); await LoadStatsAsync(ct); return Page(); }

        var pro = await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == id.Value && _tenant.VisibleCompanyIds.Contains(p.CompanyId), ct);
        if (pro == null) { Set(false, $"PRO {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        var summary = await _db.Set<ProductionOrderCostSummary>()
            .FirstOrDefaultAsync(s => s.ProductionOrderId == id.Value, ct);
        if (summary == null)
        {
            summary = new ProductionOrderCostSummary { CompanyId = pro.CompanyId, ProductionOrderId = id.Value };
            _db.Set<ProductionOrderCostSummary>().Add(summary);
        }

        // Locked PO estimate for the Ti-6Al-4V ETO engine-mount bracket (real, qty 4).
        summary.EstimatedMaterialCost = 2_400.00m;   // Ti-6Al-4V bar stock + hardware
        summary.EstimatedLaborCost = 1_120.00m;       // 5-axis setup + run + assembly
        summary.EstimatedMachineCost = 700.00m;        // 5-axis @ $175/hr
        summary.EstimatedBurdenCost = 560.00m;
        summary.EstimatedOutsideProcessingCost = 320.00m; // NADCAP anodize + passivate
        summary.EstimatedSubcontractCost = 0m;
        summary.EstimatedFreightLandedCost = 60.00m;
        summary.EstimatedToolingCost = 140.00m;
        summary.EstimatedScrapReworkCost = 100.00m;
        summary.EstimatedTotalCost = 5_400.00m;
        summary.UpdatedAtUtc = DateTime.UtcNow;
        summary.UpdatedBy = "B7-PR3-probe";

        await _db.SaveChangesAsync(ct);
        Set(true, $"Locked PO estimate seeded for PRO #{id}: Total=${summary.EstimatedTotalCost:N2}. " +
                  "Use button 1 to lock it as the variance baseline.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) SEED actual costs + refresh
    public async Task<IActionResult> OnPostSeedActualAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id."); await LoadStatsAsync(ct); return Page(); }

        var pro = await _db.ProductionOrders
            .FirstOrDefaultAsync(p => p.Id == id.Value && _tenant.VisibleCompanyIds.Contains(p.CompanyId), ct);
        if (pro == null) { Set(false, $"PRO {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        var by = User.Identity?.Name ?? "B7-PR3-probe";

        // Actual material — 10% over the locked estimate (Ti-6Al-4V commodity uplift).
        await _costSvc.PostCostAsync(CostObjectType.ProductionOrder, id.Value,
            CostTransactionType.MaterialIssue, ProductionCostBucket.DirectMaterial,
            pro.CompanyId, pro.LocationId, id.Value, 10, 1, null,
            1m, "LOT", 2_640.00m, "MaterialTransaction", null,
            "LOT-2026-TI64-44120", null, null, true,
            "Ti-6Al-4V bar + hardware — 10% over locked PO estimate", by, ct);

        // Actual machining labor — 15% over (first-article rework).
        await _costSvc.PostCostAsync(CostObjectType.ProductionOrder, id.Value,
            CostTransactionType.DirectLabor, ProductionCostBucket.DirectLabor,
            pro.CompanyId, pro.LocationId, id.Value, 10, null, null,
            36.8m, "HR", 35.00m, "LaborEntry", null,
            null, null, null, true,
            "36.8 hr 5-axis + assembly — first-article bore-tolerance rework", by, ct);

        await _costSvc.RefreshSummaryAsync(id.Value, by, ct);
        Set(true, $"Actual costs posted for PRO #{id}: Material $2,640.00 (10% over) + Labor $1,288.00 (15% over). Summary refreshed (Estimated* preserved).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5) COMPUTE variances (shows baseline mode)
    public async Task<IActionResult> OnPostComputeVariancesAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id."); await LoadStatsAsync(ct); return Page(); }
        if (!await ProVisibleAsync(id.Value, ct)) { Set(false, $"PRO {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        var r = await _svc.ComputeVariancesAsync(id.Value, User.Identity?.Name ?? "B7-PR3-probe", ct);
        if (r.IsSuccess)
        {
            var v = r.Value!;
            var baselineLabel = v.BaselineMode == VarianceBaselineMode.LockedPoEstimate
                ? $"locked PO estimate (locked {v.EstimateLockedUtc:u})"
                : "item-master standard";
            Set(true, $"PRO #{id}: {v.Variances.Count} variances, total estimate-to-actual = ${v.TotalVariance:N2} " +
                      $"({v.FavorableCount} favorable, {v.UnfavorableCount} unfavorable) — measured vs {baselineLabel}.");
        }
        else Set(false, r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // R) RELOAD
    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Stats reloaded.");
        return Page();
    }
}
