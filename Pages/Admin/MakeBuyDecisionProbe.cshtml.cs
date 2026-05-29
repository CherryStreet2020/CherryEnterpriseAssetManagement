// Theme B7 Wave C PR-7 — admin probe for the MakeBuyDecision schema (OPENS Wave C).
// Lock-16 corollary: both action buttons write. PR-8 fills the real decision logic;
// this probe proves the new tables accept writes and round-trip.
//
//   1) Ensure policy        — upsert the company's MakeBuyDecisionPolicy (defaults).
//   2) Record sample decision — write a MakeBuyDecision row against a real item with
//      realistic snapshots + a FactorBreakdown jsonb (stub of PR-8's output).
//   3) Show                 — list the policy + recent decisions.
//   R) Reload.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B7 Wave C PR-7 make-or-buy schema. Upserts a " +
    "MakeBuyDecisionPolicy and writes a MakeBuyDecision audit row against a real item, " +
    "tenant-scoped. PR-8 supplies the real decision engine.")]
public sealed class MakeBuyDecisionProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<MakeBuyDecisionProbeModel> _logger;

    public MakeBuyDecisionProbeModel(AppDbContext db, ITenantContext tenant, ILogger<MakeBuyDecisionProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public int CompanyId { get; private set; }
    public MakeBuyDecisionPolicy? Policy { get; private set; }
    public List<MakeBuyDecision> Recent { get; private set; } = new();

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
    private int Company() => _tenant.VisibleCompanyIds.FirstOrDefault();

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        CompanyId = Company();
        if (CompanyId <= 0) return;
        Policy = await _db.MakeBuyDecisionPolicies
            .Where(p => p.CompanyId == CompanyId && p.SiteId == null)
            .OrderBy(p => p.Id).FirstOrDefaultAsync(ct);
        Recent = await _db.MakeBuyDecisions
            .Where(d => d.CompanyId == CompanyId)
            .OrderByDescending(d => d.Id).Take(10).ToListAsync(ct);
    }

    public async Task<IActionResult> OnPostEnsurePolicyAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { Set(false, "No visible company."); await LoadStatsAsync(ct); return Page(); }

        var policy = await _db.MakeBuyDecisionPolicies
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.SiteId == null, ct);
        if (policy == null)
        {
            policy = new MakeBuyDecisionPolicy { CompanyId = companyId, CreatedBy = "MakeBuyDecisionProbe" };
            _db.MakeBuyDecisionPolicies.Add(policy);
        }
        else { policy.IsActive = true; policy.ModifiedAt = DateTime.UtcNow; policy.ModifiedBy = "MakeBuyDecisionProbe"; }
        await _db.SaveChangesAsync(ct);

        Set(true, $"Policy ready (company #{companyId}): capacity threshold {policy.CapacityThresholdPct:0}% · buy score ≥ " +
                  $"{policy.BuyDecisionScoreThreshold:0.00} · weights F2 {policy.WeightCapacity:0.00}/F3 {policy.WeightCostDelta:0.00}/" +
                  $"F4 {policy.WeightBreakEven:0.00}/F5 {policy.WeightLeadTime:0.00}/F6 {policy.WeightQualityRisk:0.00} · tie-break {policy.FinalTieBreak}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostRecordDecisionAsync(CancellationToken ct)
    {
        var companyId = Company();
        if (companyId <= 0) { Set(false, "No visible company."); await LoadStatsAsync(ct); return Page(); }

        var itemId = await _db.Items.Where(i => i.CompanyId == companyId)
            .OrderByDescending(i => i.Id).Select(i => (int?)i.Id).FirstOrDefaultAsync(ct);
        if (itemId == null) { Set(false, "No Item in this company — seed master data first."); await LoadStatsAsync(ct); return Page(); }

        // A realistic BUY decision stub (PR-8 produces this for real). Ti-6Al-4V bracket, drum loaded.
        var decision = new MakeBuyDecision
        {
            CompanyId = companyId, ItemId = itemId.Value, Qty = 50m,
            DueDate = DateTime.UtcNow.Date.AddDays(21), DecidedAtUtc = DateTime.UtcNow,
            Context = MakeBuyDecisionContext.ManualWhatIf, SourceType = "MakeBuyDecisionProbe",
            Outcome = MakeBuyOutcome.Buy, BuyScore = 0.62m, Confidence = 0.74m,
            WasHardGated = false,
            RationaleText = "BUY — make path routes through the loaded MILL drum (111% load) and the supplier " +
                            "quote lands 3 days inside the due date at a 6% premium, within the drum-offload tolerance.",
            FactorBreakdown = "[" +
                "{\"code\":\"F2\",\"label\":\"Capacity\",\"score\":0.80,\"weight\":0.25,\"weightedImpact\":0.20,\"reason\":\"MILL drum at 111% over [today,due]\"}," +
                "{\"code\":\"F3\",\"label\":\"Cost delta\",\"score\":0.40,\"weight\":0.30,\"weightedImpact\":0.12,\"reason\":\"buy $3,928 vs make $3,705 (+6%)\"}," +
                "{\"code\":\"F5\",\"label\":\"Lead time\",\"score\":0.70,\"weight\":0.20,\"weightedImpact\":0.14,\"reason\":\"vendor delivers 3d inside due\"}]",
            MakeCostFullyLoaded = 3705.00m, BuyCostLanded = 3928.00m,
            BottleneckWorkCenterCode = "MILL", BottleneckLoadPct = 111.1m, RoutedThroughDrum = true,
            MakeCompletionDate = DateTime.UtcNow.Date.AddDays(24), VendorDeliveryDate = DateTime.UtcNow.Date.AddDays(18),
            CreatedBy = "MakeBuyDecisionProbe",
        };
        _db.MakeBuyDecisions.Add(decision);
        await _db.SaveChangesAsync(ct);

        Set(true, $"Recorded MakeBuyDecision #{decision.Id} for item #{itemId} → {decision.Outcome} " +
                  $"(score {decision.BuyScore:0.00}); make ${decision.MakeCostFullyLoaded:N0} vs buy ${decision.BuyCostLanded:N0}, " +
                  $"drum {decision.BottleneckWorkCenterCode} {decision.BottleneckLoadPct:0.#}%.");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }
}
