// Theme B11 Wave R1-3 — admin probe for WorkCenter cost + operation-default
// groups (CLOSES Wave R1). Exercises the detailed cost-rate split, CostCenter
// link, and op-default group (Lock-16 corollary: every button writes):
//
//   1) Set cost rates       — labor/machine setup+run, fixed/var OH, quoting rate.
//   2) Assign cost center    — FK to a same-company CostCenter (production posts here).
//   3) Set operation defaults— yield/scrap, count-point, backflush, completion behavior.
//   R) Reload
//
// All writes go through AppDbContext (tenant-scoped via ITenantContext), so this
// probe is ControlPlaneExempt. Company-scoped pickers (R1-2 Codex lesson).

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
    "Admin diagnostic probe for Theme B11 R1-3 WorkCenter cost + operation-default groups. " +
    "Reads/writes WorkCenter via AppDbContext, tenant-scoped through ITenantContext.VisibleCompanyIds " +
    "on every read and write.")]
public sealed class WorkCenterCostProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<WorkCenterCostProbeModel> _logger;

    public WorkCenterCostProbeModel(AppDbContext db, ITenantContext tenant, ILogger<WorkCenterCostProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalWorkCenters { get; private set; }
    public int WithRatesCount { get; private set; }
    public int WithCostCenterCount { get; private set; }
    public int CountPointCount { get; private set; }
    public IReadOnlyList<Row> Sample { get; private set; } = Array.Empty<Row>();

    public sealed record Row(int Id, string Code, decimal RunLabor, decimal RunMachine,
        decimal FixedOh, decimal VarOh, int? CostCenterId, decimal? YieldPct, bool CountPoint,
        WorkCenterCompletionBehavior Completion);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<WorkCenter> ScopedWc() =>
        _db.WorkCenters.Where(w => _tenant.VisibleCompanyIds.Contains(w.CompanyId));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalWorkCenters = await ScopedWc().CountAsync(ct);
        WithRatesCount = await ScopedWc().CountAsync(w => w.RunLaborRatePerHour > 0 || w.RunMachineRatePerHour > 0, ct);
        WithCostCenterCount = await ScopedWc().CountAsync(w => w.CostCenterId != null, ct);
        CountPointCount = await ScopedWc().CountAsync(w => w.IsCountPoint, ct);

        Sample = await ScopedWc()
            .OrderByDescending(w => w.Id).Take(15)
            .Select(w => new Row(w.Id, w.Code, w.RunLaborRatePerHour, w.RunMachineRatePerHour,
                w.FixedOverheadRatePerHour, w.VariableOverheadRatePerHour, w.CostCenterId,
                w.DefaultYieldPct, w.IsCountPoint, w.DefaultCompletionBehavior))
            .ToListAsync(ct);
    }

    private Task<WorkCenter?> LatestWcAsync(CancellationToken ct) =>
        ScopedWc().OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);

    // 1) SET cost rates (real 5-axis CNC fixtures)
    public async Task<IActionResult> OnPostSetRatesAsync(CancellationToken ct)
    {
        var wc = await LatestWcAsync(ct);
        if (wc == null) { Set(false, "No tenant-visible Work Center."); await LoadStatsAsync(ct); return Page(); }

        wc.LaborRateSource = WorkCenterLaborRateSource.WorkCenterRate;
        wc.SetupLaborRatePerHour = 85.00m;
        wc.RunLaborRatePerHour = 85.00m;
        wc.SetupMachineRatePerHour = 175.00m;   // 5-axis machine rate
        wc.RunMachineRatePerHour = 175.00m;
        wc.FixedOverheadRatePerHour = 42.00m;
        wc.VariableOverheadRatePerHour = 28.00m;
        wc.QuotingRatePerHour = 360.00m;         // blended quote rate (labor+machine+OH)
        wc.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        Set(true, $"WC #{wc.Id} ({wc.Code}) cost rates set: labor $85/hr · machine $175/hr (5-axis) · " +
                  "fixed OH $42 + var OH $28 · quoting blended $360/hr. The cost engine + quoting consume these.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) ASSIGN a same-company cost center
    public async Task<IActionResult> OnPostAssignCostCenterAsync(CancellationToken ct)
    {
        var wc = await LatestWcAsync(ct);
        if (wc == null) { Set(false, "No tenant-visible Work Center."); await LoadStatsAsync(ct); return Page(); }

        // Company-scoped (R1-2 Codex lesson): keep the cost center in the WC's company.
        var cc = await _db.Set<CostCenter>()
            .Where(c => c.CompanyId == wc.CompanyId)
            .OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (cc == null) { Set(false, $"No CostCenter in WC #{wc.Id}'s company ({wc.CompanyId})."); await LoadStatsAsync(ct); return Page(); }

        wc.CostCenterId = cc.Id;
        wc.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        Set(true, $"WC #{wc.Id} ({wc.Code}) → CostCenter #{cc.Id} ({cc.Code} {cc.Name}). Production cost now posts to this center.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) SET operation defaults
    public async Task<IActionResult> OnPostSetOpDefaultsAsync(CancellationToken ct)
    {
        var wc = await LatestWcAsync(ct);
        if (wc == null) { Set(false, "No tenant-visible Work Center."); await LoadStatsAsync(ct); return Page(); }

        wc.DefaultYieldPct = 95.00m;
        wc.DefaultScrapPct = 5.00m;
        wc.IsCountPoint = true;
        wc.DefaultBackflushMaterials = true;
        wc.DefaultCompletionBehavior = WorkCenterCompletionBehavior.AutoAdvance;
        wc.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        Set(true, $"WC #{wc.Id} ({wc.Code}) op-defaults set: yield 95% · scrap 5% · count-point · backflush-on-complete · " +
                  "AutoAdvance. These stamp onto ProductionOperation at release.");
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
