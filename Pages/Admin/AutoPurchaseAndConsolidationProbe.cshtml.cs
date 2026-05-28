// Sprint 15.3 PR-14 — admin probe for IAutoPurchaseService + IDemandConsolidationService.
//
// Exercises both PR-14 services end-to-end:
//   * EvaluateDemandAsync   — per-demand §16 trigger/blocker decision
//   * EvaluateProAsync      — every demand on a PRO
//   * GetCandidatesAsync    — tenant-wide eligible-candidate scan
//   * SuggestModeAsync      — heuristic suggesting the right §17 mode
//   * PlanAsync             — explicit-mode consolidation plan
//   * PlanForPro            — convenience wrapper across all PRO demands
//
// Both services are pure READ. Per feedback_lock16_corollary_probes_exercise_writes
// the IService write surface is exercised by IPurchasingControlCenterServiceProbe
// (PR-10/12/13) lifecycle transition buttons; PR-14 adds zero new write paths.

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
    "Admin diagnostic probe. Tenant-scoped reads only. Both PR-14 services " +
    "are pure read; no write paths surfaced here.")]
public sealed class AutoPurchaseAndConsolidationProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAutoPurchaseService _autoPo;
    private readonly IDemandConsolidationService _consolidation;
    private readonly ILogger<AutoPurchaseAndConsolidationProbeModel> _log;

    public AutoPurchaseAndConsolidationProbeModel(
        AppDbContext db,
        ITenantContext tenant,
        IAutoPurchaseService autoPo,
        IDemandConsolidationService consolidation,
        ILogger<AutoPurchaseAndConsolidationProbeModel> log)
    {
        _db = db;
        _tenant = tenant;
        _autoPo = autoPo;
        _consolidation = consolidation;
        _log = log;
    }

    [BindProperty(SupportsGet = true)] public int DemandId { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int ProductionOrderId { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public DemandConsolidationMode Mode { get; set; } = DemandConsolidationMode.StrictJobSpecific;
    [BindProperty(SupportsGet = true)] public string? DemandIdsCsv { get; set; }
    [BindProperty(SupportsGet = true)] public int? SiteId { get; set; }

    public IEnumerable<DemandConsolidationMode> AllModes
        => Enum.GetValues<DemandConsolidationMode>();

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public AutoPoCandidate? EvalResult { get; private set; }
    public IReadOnlyList<AutoPoCandidate>? ProEvalResults { get; private set; }
    public AutoPoCandidatePage? Candidates { get; private set; }
    public ConsolidationPlan? Plan { get; private set; }
    public (DemandConsolidationMode Mode, string Reason)? Suggestion { get; private set; }
    public int DemandTotalInTenant { get; private set; }

    private void Set(bool ok, string? msg) { OutcomeIsError = !ok; Outcome = msg; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostEvaluateDemandAsync(CancellationToken ct)
    {
        var r = await _autoPo.EvaluateDemandAsync(DemandId, ct);
        if (r.IsSuccess && r.Value is not null)
        {
            EvalResult = r.Value;
            Set(true,
                $"Demand #{EvalResult.DemandId} ({EvalResult.DemandNumber}) — decision={EvalResult.Decision}; " +
                $"{EvalResult.Triggers.Count} trigger(s), {EvalResult.Blockers.Count} blocker(s). {EvalResult.Summary}");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostEvaluateProAsync(CancellationToken ct)
    {
        var r = await _autoPo.EvaluateProductionOrderAsync(ProductionOrderId, ct);
        if (r.IsSuccess && r.Value is not null)
        {
            ProEvalResults = r.Value;
            var eligible = ProEvalResults.Count(c => c.Decision == AutoPoDecision.Eligible);
            var blocked = ProEvalResults.Count(c => c.Decision == AutoPoDecision.BlockedReviewRequired);
            Set(true,
                $"PRO #{ProductionOrderId} — {ProEvalResults.Count} demand(s) evaluated; " +
                $"{eligible} eligible, {blocked} blocked-review-required.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostLoadCandidatesAsync(CancellationToken ct)
    {
        var r = await _autoPo.GetCandidatesAsync(
            new AutoPoEvaluationFilter(SiteId: SiteId, Take: 25), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Candidates = r.Value;
            Set(true,
                $"Auto-PO scan — {Candidates.TotalCount} demand(s) considered; " +
                $"{Candidates.EligibleCount} eligible, {Candidates.BlockedCount} blocked-review.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostSuggestModeAsync(CancellationToken ct)
    {
        var ids = ParseDemandIds(DemandIdsCsv);
        if (ids.Count == 0)
        {
            Set(false, "Provide at least one demand ID in DemandIdsCsv (comma-separated).");
            await LoadCommonAsync(ct);
            return Page();
        }
        var r = await _consolidation.SuggestModeAsync(ids, ct);
        if (r.IsSuccess)
        {
            Suggestion = r.Value;
            Set(true, $"Suggested mode: {Suggestion.Value.Mode} — {Suggestion.Value.Reason}");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostPlanAsync(CancellationToken ct)
    {
        var ids = ParseDemandIds(DemandIdsCsv);
        if (ids.Count == 0)
        {
            Set(false, "Provide at least one demand ID in DemandIdsCsv (comma-separated).");
            await LoadCommonAsync(ct);
            return Page();
        }
        var r = await _consolidation.PlanAsync(new ConsolidationRequest(ids, Mode), ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Plan = r.Value;
            Set(true,
                $"Plan: mode={Plan.Mode}; {Plan.InputDemandCount} demand(s) in, " +
                $"{Plan.PlannedLineCount} line(s) out, {Plan.SkippedDemandCount} skipped.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostPlanForProAsync(CancellationToken ct)
    {
        var r = await _consolidation.PlanForProductionOrderAsync(ProductionOrderId, Mode, ct);
        if (r.IsSuccess && r.Value is not null)
        {
            Plan = r.Value;
            Set(true,
                $"PRO #{ProductionOrderId} plan: mode={Plan.Mode}; {Plan.InputDemandCount} demand(s) in, " +
                $"{Plan.PlannedLineCount} line(s) out, {Plan.SkippedDemandCount} skipped.");
        }
        else Set(false, r.Error);
        await LoadCommonAsync(ct);
        return Page();
    }

    private static IReadOnlyList<int> ParseDemandIds(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<int>();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();
    }

    private async Task LoadCommonAsync(CancellationToken ct)
    {
        DemandTotalInTenant = await _db.Set<ProductionSupplyDemand>()
            .Where(d => _tenant.VisibleCompanyIds.Contains(d.CompanyId))
            .CountAsync(ct);
    }
}
