// Theme B7 Wave D PR-3 (2026-05-30) — admin probe for the Crystallization Cockpit panel.
//
// Deterministic Lock-16 surface for _CockpitCrystallizationPanel. It targets a real
// PoFirst production order, renders the SAME partial the live Production Cockpit
// "Crystallize" tab uses (via IProductionCockpitService.GetCrystallizationPanelAsync —
// the live read path), and exercises the crystallize / reverse WRITE actions through
// IItemCrystallizationService — so the panel UX is verified end-to-end independent of
// seed data, and every button reads or writes through a service.
//
//   1) Preview + render   — read-only: GetCrystallizationPanelAsync → renders the panel.
//   2) Crystallize new     — ForceCreateNew mint → re-renders the panel (now CRYSTALLIZED).
//   3) Reverse latest      — ReverseLatestForProductionOrderAsync → re-renders (READY again).
//   Reload                 — read-only refresh (siblings write → Lock-16 satisfied).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Pages.Production;
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
    "Admin diagnostic probe for Theme B7 Wave D PR-3 Crystallization Cockpit panel. Renders " +
    "_CockpitCrystallizationPanel via IProductionCockpitService (the live read path) and exercises " +
    "crystallize / reverse through IItemCrystallizationService, tenant-scoped. Writes route through services.")]
public sealed class CrystallizationCockpitPanelProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IProductionCockpitService _cockpit;
    private readonly IItemCrystallizationService _crystallization;
    private readonly ILogger<CrystallizationCockpitPanelProbeModel> _logger;

    public CrystallizationCockpitPanelProbeModel(
        AppDbContext db, ITenantContext tenant, IProductionCockpitService cockpit,
        IItemCrystallizationService crystallization, ILogger<CrystallizationCockpitPanelProbeModel> logger)
    {
        _db = db; _tenant = tenant; _cockpit = cockpit; _crystallization = crystallization; _logger = logger;
    }

    [BindProperty] public int ProId { get; set; }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }
    public int? ProbeProId { get; private set; }
    public string? ProbeOrderNumber { get; private set; }

    /// <summary>The rendered panel — the actual cockpit partial input.</summary>
    public CrystallizationCockpitPanelModel? PanelData { get; private set; }

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }
    private string Actor() => User.Identity?.Name ?? "B7-PR3-cockpit-probe";

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        ProbeProId = await ResolveProIdAsync(ct);
        if (ProbeProId != null)
            ProbeOrderNumber = await _db.ProductionOrders
                .Where(p => p.Id == ProbeProId).Select(p => p.OrderNumber).FirstOrDefaultAsync(ct);
    }

    private async Task<int?> ResolveProIdAsync(CancellationToken ct)
    {
        if (ProId > 0) return ProId;
        return await _db.ProductionOrders
            .Where(p => _tenant.VisibleCompanyIds.Contains(p.CompanyId) && p.IsPoFirst)
            .OrderByDescending(p => p.Id)
            .Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
    }

    // Render the panel via the LIVE cockpit read path (IProductionCockpitService).
    private async Task RenderPanelAsync(int proId, CancellationToken ct)
    {
        var panel = await _cockpit.GetCrystallizationPanelAsync(proId, ct);
        if (panel.IsSuccess && panel.Value!.Data != null)
            PanelData = new CrystallizationCockpitPanelModel { Preview = panel.Value.Data.Preview };
    }

    // 1) Preview + render
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id (or create a PoFirst PRO first)."); await LoadStatsAsync(ct); return Page(); }

        await RenderPanelAsync(id.Value, ct);
        await LoadStatsAsync(ct);
        if (PanelData == null) { Set(false, $"Could not preview PRO #{id}."); return Page(); }
        var pv = PanelData.Preview;
        Set(true, $"Preview for PRO #{id} ({pv.OrderNumber}): would mint '{pv.ProposedPartNumber}' with " +
                  $"{pv.BomLines.Count} BOM line(s) + {pv.RoutingOps.Count} routing op(s), standard cost " +
                  $"${pv.SeededStandardCost ?? 0m:N2}. Panel rendered below.");
        return Page();
    }

    // 2) Crystallize (force new) + render
    public async Task<IActionResult> OnPostCrystallizeNewAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id."); await LoadStatsAsync(ct); return Page(); }

        var r = await _crystallization.CrystallizeAsync(
            new CrystallizeRequest(id.Value, Actor(), ForceCreateNew: true), ct);
        await RenderPanelAsync(id.Value, ct);
        await LoadStatsAsync(ct);
        Set(r.IsSuccess, r.IsSuccess ? r.Value!.Message : r.Error);
        return Page();
    }

    // 3) Reverse latest for this PRO + render
    public async Task<IActionResult> OnPostReverseAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id."); await LoadStatsAsync(ct); return Page(); }

        var r = await _crystallization.ReverseLatestForProductionOrderAsync(
            id.Value, "PR-3 cockpit panel probe reversal — reversible; as-built untouched.", Actor(), ct);
        await RenderPanelAsync(id.Value, ct);
        await LoadStatsAsync(ct);
        Set(r.IsSuccess, r.IsSuccess ? r.Value!.Message : r.Error);
        return Page();
    }

    public async Task<IActionResult> OnPostReloadAsync(CancellationToken ct)
    {
        await LoadStatsAsync(ct);
        Set(true, "Reloaded.");
        return Page();
    }
}
