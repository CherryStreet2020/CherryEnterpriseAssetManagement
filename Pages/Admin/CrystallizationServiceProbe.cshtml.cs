// Theme B7 Wave B PR-5 — admin probe for IItemCrystallizationService (the BIC
// differentiator). Exercises the full service surface against a real PoFirst PRO
// (Lock-16 corollary: every action writes or reads through the service):
//
//   1) Preview            — read-only: would-be Item + standard BOM + standard
//                           Routing + seeded cost + dedupe match.
//   2) Crystallize (new)  — ForceCreateNew: mints Item + standard BOM + standard
//                           Routing + first-actual cost; sets CrystallizedItemId.
//   3) Crystallize (gated)— no force: if a dedupe match exists OR the PRO is
//                           already crystallized, surfaces the human-confirm /
//                           guard refusal (decision #3) rather than acting.
//   4) Reverse latest     — reverses the latest non-reversed crystallization.
//   R) Reload
//
// All writes route through IItemCrystallizationService (which writes via the
// scoped AppDbContext, tenant-scoped through ITenantContext.VisibleCompanyIds),
// so this probe is ControlPlaneExempt.

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
    "Admin diagnostic probe for Theme B7 PR-5 IItemCrystallizationService (preview / crystallize / " +
    "dedupe-human-confirm / reverse). Writes route through the service via AppDbContext, tenant-scoped " +
    "through ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class CrystallizationServiceProbeModel : PageModel
{
    private readonly IItemCrystallizationService _svc;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<CrystallizationServiceProbeModel> _logger;

    public CrystallizationServiceProbeModel(
        IItemCrystallizationService svc, AppDbContext db, ITenantContext tenant,
        ILogger<CrystallizationServiceProbeModel> logger)
    {
        _svc = svc; _db = db; _tenant = tenant; _logger = logger;
    }

    [BindProperty] public int ProId { get; set; }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalCrystallizations { get; private set; }
    public int CreatedNewCount { get; private set; }
    public int LinkedCount { get; private set; }
    public int ReversedCount { get; private set; }
    public CrystallizationPreview? Preview { get; private set; }
    public IReadOnlyList<Row> Sample { get; private set; } = Array.Empty<Row>();

    public sealed record Row(
        int Id, string Number, int SourceProId, CrystallizationOutcome Outcome,
        int? CreatedItemId, int? MatchedItemId, decimal? SeededCost, bool IsReversed);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<ItemCrystallization> Scoped() =>
        _db.ItemCrystallizations.Where(c => _tenant.VisibleCompanyIds.Contains(c.CompanyId));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalCrystallizations = await Scoped().CountAsync(ct);
        CreatedNewCount = await Scoped().CountAsync(c => c.Outcome == CrystallizationOutcome.CreatedNewItem, ct);
        LinkedCount = await Scoped().CountAsync(c => c.Outcome == CrystallizationOutcome.LinkedToExisting, ct);
        ReversedCount = await Scoped().CountAsync(c => c.IsReversed, ct);

        Sample = await Scoped()
            .OrderByDescending(c => c.Id).Take(15)
            .Select(c => new Row(
                c.Id, c.CrystallizationNumber, c.SourceProductionOrderId, c.Outcome,
                c.CreatedItemId, c.MatchedItemId, c.SeededStandardCost, c.IsReversed))
            .ToListAsync(ct);
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

    private string Actor() => User.Identity?.Name ?? "B7-PR5-probe";

    // 1) PREVIEW
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id (or create a PoFirst PRO first)."); await LoadStatsAsync(ct); return Page(); }

        var r = await _svc.PreviewCrystallizationAsync(id.Value, ct);
        if (r.IsSuccess)
        {
            Preview = r.Value;
            var dupe = r.Value!.DedupeMatchItemId != null
                ? $" Dedupe match: Item {r.Value.DedupeMatchItemId} ({r.Value.DedupeMatchPartNumber})."
                : " No dedupe match.";
            Set(true, $"Preview for PRO #{id} ({r.Value.OrderNumber}): would mint '{r.Value.ProposedPartNumber}' " +
                      $"with {r.Value.BomLines.Count} BOM line(s) + {r.Value.RoutingOps.Count} routing op(s), " +
                      $"standard cost ${r.Value.SeededStandardCost ?? 0m:N2}.{dupe}");
        }
        else Set(false, r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) CRYSTALLIZE (force new)
    public async Task<IActionResult> OnPostCrystallizeNewAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id."); await LoadStatsAsync(ct); return Page(); }

        var r = await _svc.CrystallizeAsync(
            new CrystallizeRequest(id.Value, Actor(), ForceCreateNew: true), ct);
        if (r.IsSuccess) Set(true, r.Value!.Message);
        else Set(false, r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) CRYSTALLIZE (gated — no force; demonstrates dedupe human-confirm + already-crystallized guard)
    public async Task<IActionResult> OnPostCrystallizeGatedAsync(CancellationToken ct)
    {
        var id = await ResolveProIdAsync(ct);
        if (id == null) { Set(false, "No target PRO — enter a PRO Id."); await LoadStatsAsync(ct); return Page(); }

        var r = await _svc.CrystallizeAsync(
            new CrystallizeRequest(id.Value, Actor(), ForceCreateNew: false), ct);
        if (r.IsSuccess) Set(true, r.Value!.Message);
        else Set(false, "Gate fired (expected when a dedupe match exists or the PRO is already crystallized): " + r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) REVERSE latest non-reversed crystallization
    public async Task<IActionResult> OnPostReverseLatestAsync(CancellationToken ct)
    {
        var latest = await Scoped().Where(c => !c.IsReversed)
            .OrderByDescending(c => c.Id).Select(c => c.Id).FirstOrDefaultAsync(ct);
        if (latest == 0) { Set(false, "No un-reversed crystallization in your tenant scope — crystallize one first."); await LoadStatsAsync(ct); return Page(); }

        var r = await _svc.ReverseCrystallizationAsync(
            latest, "PR-5 probe reversal demo — reversible; as-built untouched.", Actor(), ct);
        if (r.IsSuccess) Set(true, r.Value!.Message);
        else Set(false, r.Error);
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
