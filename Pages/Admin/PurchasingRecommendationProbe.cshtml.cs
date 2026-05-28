// Sprint 15.3 PR-15 — admin probe for IPurchasingRecommendationService.
//
// Exercises the §18 buyer recommendation engine end-to-end:
//   * GetRecommendationAsync       — single demand
//   * GetRecommendationsForProAsync — every demand on a PRO
//   * GetRecommendationsAsync       — tenant-wide scan with action filter
//   * BuildFromDemand               — pure helper for inline use
//
// Pure read service. The recommendation engine never writes; PO creation
// stays in IPurchasingService.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe. Tenant-scoped reads only. PR-15 service is " +
    "pure read; no write paths surfaced here.")]
public sealed class PurchasingRecommendationProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPurchasingRecommendationService _recs;
    private readonly ILogger<PurchasingRecommendationProbeModel> _log;

    public PurchasingRecommendationProbeModel(
        AppDbContext db,
        ITenantContext tenant,
        IPurchasingRecommendationService recs,
        ILogger<PurchasingRecommendationProbeModel> log)
    {
        _db = db;
        _tenant = tenant;
        _recs = recs;
        _log = log;
    }

    [BindProperty(SupportsGet = true)] public int DemandId { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int ProductionOrderId { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int? SiteId { get; set; }
    [BindProperty(SupportsGet = true)] public RecommendedAction? OnlyAction { get; set; }

    public IEnumerable<RecommendedAction> AllActions => Enum.GetValues<RecommendedAction>();

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public PurchasingRecommendation? Single { get; private set; }
    public IReadOnlyList<PurchasingRecommendation>? ProRecs { get; private set; }
    public PurchasingRecommendationPage? Scan { get; private set; }
    public int DemandTotalInTenant { get; private set; }

    private void Set(bool ok, string? msg) { OutcomeIsError = !ok; Outcome = msg; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRecommendDemandAsync(CancellationToken ct)
    {
        var r = await _recs.GetRecommendationAsync(DemandId, ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Single = r.Value;
            Set(true,
                $"Demand #{Single.DemandId} ({Single.DemandNumber}) — " +
                $"action={Single.Action} ({Single.ActionLabel}); risk={Single.Risk}; " +
                $"daysUntilRequired={Single.DaysUntilRequired?.ToString() ?? "—"}. {Single.Reason}");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRecommendForProAsync(CancellationToken ct)
    {
        var r = await _recs.GetRecommendationsForProAsync(ProductionOrderId, ct);
        if (r.IsSuccess && r.Value is not null)
        {
            ProRecs = r.Value;
            var critical = ProRecs.Count(x => x.Risk == RecommendationRisk.Critical);
            var high = ProRecs.Count(x => x.Risk == RecommendationRisk.High);
            Set(true,
                $"PRO #{ProductionOrderId}: {ProRecs.Count} recommendation(s); " +
                $"{critical} critical-risk, {high} high-risk.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostScanTenantAsync(CancellationToken ct)
    {
        var r = await _recs.GetRecommendationsAsync(
            new PurchasingRecommendationFilter(SiteId: SiteId, OnlyAction: OnlyAction, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Scan = r.Value;
            Set(true,
                $"Tenant scan: {Scan.TotalCount} recommendation(s); " +
                $"{Scan.CriticalRiskCount} critical-risk, {Scan.HighRiskCount} high-risk; " +
                $"showing {Scan.Recommendations.Count}." +
                (OnlyAction.HasValue ? $" Filtered to action={OnlyAction.Value}." : ""));
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    private async Task LoadCommonAsync(CancellationToken ct)
    {
        DemandTotalInTenant = await _db.Set<ProductionSupplyDemand>()
            .Where(d => _tenant.VisibleCompanyIds.Contains(d.CompanyId))
            .CountAsync(ct);
    }
}
