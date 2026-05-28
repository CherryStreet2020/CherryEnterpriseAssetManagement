using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 14.4 PR-4 (2026-05-28) — admin probe for IProductionVarianceCloseService.
//
// NINE WRITE/ACTION BUTTONS per Lock 16 corollary:
//   1. Compute Variances (does NOT post JEs)
//   2. Close PRO (full close workflow — atomic)
//   3. Check Close Readiness
//   4. Reopen PRO (controlled post-close correction)
//   5. Post Estimated Costs (seed estimated data for variance testing)
//   6. Post Actual Costs (seed actual data for variance testing)
//   7. View Variances for PRO
//   8. View Close Events for PRO
//   9. Refresh Summary (via ICostTransactionService)
[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Admin diagnostic probe. All writes flow through IProductionVarianceCloseService + ICostTransactionService.")]
public sealed class VarianceCloseProbeModel : PageModel
{
    private readonly IProductionVarianceCloseService _svc;
    private readonly ICostTransactionService _costSvc;
    private readonly AppDbContext _db;
    private readonly ILogger<VarianceCloseProbeModel> _logger;

    public VarianceCloseProbeModel(
        IProductionVarianceCloseService svc,
        ICostTransactionService costSvc,
        AppDbContext db,
        ILogger<VarianceCloseProbeModel> logger)
    {
        _svc = svc;
        _costSvc = costSvc;
        _db = db;
        _logger = logger;
    }

    [BindProperty] public int ProId { get; set; } = 1;
    [BindProperty] public string ReopenReason { get; set; } = "Post-close cost adjustment required";

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalVariances { get; private set; }
    public int TotalCloseEvents { get; private set; }
    public IReadOnlyList<ProductionVariance> LoadedVariances { get; private set; } = Array.Empty<ProductionVariance>();
    public IReadOnlyList<ProductionCloseEvent> LoadedCloseEvents { get; private set; } = Array.Empty<ProductionCloseEvent>();

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    // 1. Compute Variances
    public async Task<IActionResult> OnPostComputeVariancesAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ComputeVariancesAsync(ProId, by, ct);
        if (r.IsSuccess)
        {
            LoadedVariances = r.Value!.Variances.ToList();
            Set(true,
                $"Computed {r.Value.Variances.Count} variances for PRO #{ProId}: " +
                $"Total=${r.Value.TotalVariance:N2} ({r.Value.FavorableCount} favorable, {r.Value.UnfavorableCount} unfavorable).");
        }
        else Set(false, r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2. Close PRO
    public async Task<IActionResult> OnPostCloseProAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.CloseAsync(ProId, by, ct);
        if (r.IsSuccess)
        {
            LoadedVariances = r.Value!.Variances.ToList();
            Set(true, r.Value.Message ?? "PRO closed.");
        }
        else Set(false, r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3. Check Close Readiness
    public async Task<IActionResult> OnPostCheckReadinessAsync(CancellationToken ct)
    {
        var r = await _svc.CheckCloseReadinessAsync(ProId, ct);
        Set(r.IsSuccess, r.IsSuccess ? $"PRO #{ProId} is READY to close." : $"NOT ready: {r.Error}");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4. Reopen PRO
    public async Task<IActionResult> OnPostReopenProAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _svc.ReopenAsync(ProId, ReopenReason, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"PRO #{ProId} REOPENED. {r.Value!.CloseMessage}"
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5. Seed Estimated Costs (for testing variance computations)
    public async Task<IActionResult> OnPostSeedEstimatedAsync(CancellationToken ct)
    {
        if (ProId <= 0) { Set(false, "PRO Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        // Stamp estimated costs on summary if it exists, or create one
        var summary = await _db.Set<ProductionOrderCostSummary>()
            .FirstOrDefaultAsync(s => s.ProductionOrderId == ProId, ct);

        var pro = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == ProId, ct);
        if (pro == null) { Set(false, $"PRO {ProId} not found."); await LoadStatsAsync(ct); return Page(); }

        if (summary == null)
        {
            summary = new ProductionOrderCostSummary
            {
                CompanyId = pro.CompanyId,
                ProductionOrderId = ProId,
            };
            _db.Set<ProductionOrderCostSummary>().Add(summary);
        }

        // Realistic estimated costs for an aerospace bracket sub-assembly
        summary.EstimatedMaterialCost = 1250.00m;    // Aluminum 7075-T6 plate + fasteners + seals
        summary.EstimatedLaborCost = 680.00m;         // 16 hr machining + 4 hr assembly
        summary.EstimatedMachineCost = 420.00m;        // CNC 5-axis + CMM inspection
        summary.EstimatedBurdenCost = 340.00m;         // 50% labor burden
        summary.EstimatedOutsideProcessingCost = 175.00m; // Anodize + passivate
        summary.EstimatedSubcontractCost = 0m;
        summary.EstimatedFreightLandedCost = 45.00m;
        summary.EstimatedToolingCost = 85.00m;         // Endmill + drill amortization
        summary.EstimatedScrapReworkCost = 60.00m;     // 2% planned scrap allowance
        summary.EstimatedTotalCost = 3055.00m;

        await _db.SaveChangesAsync(ct);

        Set(true, $"Estimated costs seeded for PRO #{ProId}: Total=${summary.EstimatedTotalCost:N2} " +
            $"(Mat=${summary.EstimatedMaterialCost:N2} Lab=${summary.EstimatedLaborCost:N2} " +
            $"Mach=${summary.EstimatedMachineCost:N2} Burden=${summary.EstimatedBurdenCost:N2} " +
            $"OP=${summary.EstimatedOutsideProcessingCost:N2}).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 6. Seed Actual Costs (post real cost transactions for variance testing)
    public async Task<IActionResult> OnPostSeedActualAsync(CancellationToken ct)
    {
        if (ProId <= 0) { Set(false, "PRO Id must be > 0."); await LoadStatsAsync(ct); return Page(); }

        var pro = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == ProId, ct);
        if (pro == null) { Set(false, $"PRO {ProId} not found."); await LoadStatsAsync(ct); return Page(); }

        var by = User.Identity?.Name ?? "admin-probe";

        // Post actual material — 8% over estimated (material price increase)
        await _costSvc.PostCostAsync(CostObjectType.ProductionOrder, ProId,
            CostTransactionType.MaterialIssue, ProductionCostBucket.DirectMaterial,
            pro.CompanyId, pro.LocationId, ProId, 10, 1, null,
            1m, "LOT", 1350.00m, "MaterialTransaction", null,
            "LOT-2026-ALC-7075", null, null, true,
            "7075-T6 plate + hardware — actual 8% over estimate (commodity uplift)", by, ct);

        // Post actual labor — 12% over (rework on first article)
        await _costSvc.PostCostAsync(CostObjectType.ProductionOrder, ProId,
            CostTransactionType.DirectLabor, ProductionCostBucket.DirectLabor,
            pro.CompanyId, pro.LocationId, ProId, 10, null, null,
            22.5m, "HR", 33.78m, "LaborEntry", null,
            null, null, null, true,
            "22.5 hr machining+assembly — 2.5 hr rework on first article bore tolerance", by, ct);

        // Refresh summary to calculate actuals
        await _costSvc.RefreshSummaryAsync(ProId, by, ct);

        Set(true, $"Actual costs posted for PRO #{ProId}: Material=$1,350.00 (8% over) + Labor=$760.05 (12% over). Summary refreshed.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 7. View Variances
    public async Task<IActionResult> OnPostViewVariancesAsync(CancellationToken ct)
    {
        LoadedVariances = await _svc.GetVariancesAsync(ProId, ct);
        Set(true, $"Loaded {LoadedVariances.Count} variances for PRO #{ProId}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 8. View Close Events
    public async Task<IActionResult> OnPostViewCloseEventsAsync(CancellationToken ct)
    {
        LoadedCloseEvents = await _svc.GetCloseEventsAsync(ProId, ct);
        Set(true, $"Loaded {LoadedCloseEvents.Count} close events for PRO #{ProId}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 9. Refresh Summary
    public async Task<IActionResult> OnPostRefreshSummaryAsync(CancellationToken ct)
    {
        var by = User.Identity?.Name ?? "admin-probe";
        var r = await _costSvc.RefreshSummaryAsync(ProId, by, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Summary refreshed for PRO #{ProId}: Actual=${r.Value!.ActualTotalCost:N2} Estimated=${r.Value.EstimatedTotalCost:N2} Variance=${r.Value.CostVariance:N2}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalVariances = await _db.Set<ProductionVariance>().CountAsync(ct);
        TotalCloseEvents = await _db.Set<ProductionCloseEvent>().CountAsync(ct);
    }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
}
