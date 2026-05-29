// Theme B11 Wave R1-2 — admin probe for WorkCenter scheduling/dispatch hardening.
// Exercises the new field group + SiteId canonical tier + WorkCenterAlternate
// link (Lock-16 corollary: every button writes):
//
//   1) Configure scheduling — set bottleneck/drum + dispatch rule + resource-
//                             selection + setup-family on the latest WC.
//   2) Assign Site          — set the WC's canonical SiteId (Site == Location).
//   3) Add alternate WC     — link the latest WC to another as a spill target.
//   R) Reload
//
// All writes go through AppDbContext (tenant-scoped via ITenantContext), so this
// probe is ControlPlaneExempt.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
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
    "Admin diagnostic probe for Theme B11 R1-2 WorkCenter scheduling hardening. Reads/writes " +
    "WorkCenter + WorkCenterAlternate via AppDbContext, tenant-scoped through " +
    "ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class WorkCenterHardeningProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<WorkCenterHardeningProbeModel> _logger;

    public WorkCenterHardeningProbeModel(AppDbContext db, ITenantContext tenant, ILogger<WorkCenterHardeningProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalWorkCenters { get; private set; }
    public int BottleneckCount { get; private set; }
    public int WithSiteCount { get; private set; }
    public int AlternateLinks { get; private set; }
    public IReadOnlyList<Row> Sample { get; private set; } = Array.Empty<Row>();

    public sealed record Row(int Id, string Code, bool Bottleneck, int? SiteId,
        WorkCenterDispatchRule Dispatch, WorkCenterSetupFamilyRule SetupRule, string? SetupFamily, bool? SchedulingEnabled);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<WorkCenter> ScopedWc() =>
        _db.WorkCenters.Where(w => _tenant.VisibleCompanyIds.Contains(w.CompanyId));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalWorkCenters = await ScopedWc().CountAsync(ct);
        BottleneckCount = await ScopedWc().CountAsync(w => w.BottleneckFlag, ct);
        WithSiteCount = await ScopedWc().CountAsync(w => w.SiteId != null, ct);
        AlternateLinks = await _db.WorkCenterAlternates
            .CountAsync(a => _tenant.VisibleCompanyIds.Contains(a.CompanyId), ct);

        Sample = await ScopedWc()
            .OrderByDescending(w => w.Id).Take(15)
            .Select(w => new Row(w.Id, w.Code, w.BottleneckFlag, w.SiteId,
                w.DispatchRule, w.SetupFamilyRule, w.SetupFamilyCode, w.SchedulingEnabled))
            .ToListAsync(ct);
    }

    private Task<WorkCenter?> LatestWcAsync(CancellationToken ct) =>
        ScopedWc().OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);

    // 1) CONFIGURE scheduling on the latest WC
    public async Task<IActionResult> OnPostConfigureAsync(CancellationToken ct)
    {
        var wc = await LatestWcAsync(ct);
        if (wc == null) { Set(false, "No tenant-visible Work Center."); await LoadStatsAsync(ct); return Page(); }

        wc.BottleneckFlag = true;
        wc.ConstraintPriority = 1;
        wc.DispatchRule = WorkCenterDispatchRule.EarliestDueDate;
        wc.PrimaryResourceSelectionRule = WorkCenterResourceSelectionRule.LeastLoaded;
        wc.SetupFamilyRule = WorkCenterSetupFamilyRule.GroupBySetupFamily;
        wc.SetupFamilyCode = "TI-5AXIS";   // group Ti-6Al-4V 5-axis jobs to cut changeover
        wc.CrewSizeRequired = 1.0m;
        wc.SchedulingEnabled = true;
        wc.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        Set(true, $"WC #{wc.Id} ({wc.Code}) configured: drum (ConstraintPriority 1) · DispatchRule=EarliestDueDate · " +
                  "ResourceSelection=LeastLoaded · SetupFamily=TI-5AXIS (GroupBySetupFamily). Ready for the R4 finite scheduler.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) ASSIGN Site (canonical plant tier)
    public async Task<IActionResult> OnPostAssignSiteAsync(CancellationToken ct)
    {
        var wc = await LatestWcAsync(ct);
        if (wc == null) { Set(false, "No tenant-visible Work Center."); await LoadStatsAsync(ct); return Page(); }

        var site = await _db.Set<Site>()
            .Where(s => _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (site == null) { Set(false, "No tenant-visible Site to assign (create a Site first)."); await LoadStatsAsync(ct); return Page(); }

        wc.SiteId = site.Id;
        wc.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        Set(true, $"WC #{wc.Id} ({wc.Code}) → Site #{site.Id} ({site.SiteCode} {site.Name}). Canonical plant tier wired (Site ← WorkCenter).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) ADD an alternate WC link
    public async Task<IActionResult> OnPostAddAlternateAsync(CancellationToken ct)
    {
        var primary = await LatestWcAsync(ct);
        if (primary == null) { Set(false, "No tenant-visible Work Center."); await LoadStatsAsync(ct); return Page(); }

        var alt = await ScopedWc()
            .Where(w => w.Id != primary.Id).OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);
        if (alt == null) { Set(false, "Need a second Work Center to link as an alternate."); await LoadStatsAsync(ct); return Page(); }

        var exists = await _db.WorkCenterAlternates
            .AnyAsync(a => a.WorkCenterId == primary.Id && a.AlternateWorkCenterId == alt.Id, ct);
        if (exists) { Set(false, $"WC #{primary.Id} already has #{alt.Id} as an alternate (idempotent)."); await LoadStatsAsync(ct); return Page(); }

        _db.WorkCenterAlternates.Add(new WorkCenterAlternate
        {
            CompanyId = primary.CompanyId,
            WorkCenterId = primary.Id,
            AlternateWorkCenterId = alt.Id,
            Preference = 10,
            EfficiencyFactor = 0.85m,   // alternate runs ~15% slower
            IsActive = true,
            Notes = "Spill target when the primary is loaded (R4 scheduler).",
        });
        await _db.SaveChangesAsync(ct);

        Set(true, $"Linked WC #{primary.Id} ({primary.Code}) → alternate #{alt.Id} ({alt.Code}) at 0.85× efficiency. The R4 scheduler can spill here when the primary is loaded.");
        await LoadStatsAsync(ct);
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Stats reloaded.");
        return Page();
    }
}
