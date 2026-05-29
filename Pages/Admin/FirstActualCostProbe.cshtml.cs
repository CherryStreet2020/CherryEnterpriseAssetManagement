// Theme B7 Wave B PR-6 — admin probe for first-actual cost seeding + the
// StandardCostBasis lifecycle flag (CLOSES Wave B).
//
// A crystallized ETO master is born with its standard cost = first-unit actual
// (§5.4), broken into the 8-element ItemStandardCostElement split by
// ItemCrystallizationService (PR-5 + this PR), and flagged StandardCostBasis=
// FirstActual ("unvalidated for repeat"). This probe surfaces that breakdown and
// exercises the promotion FirstActual → Validated (and back, for a repeatable demo).
//
//   1) Show breakdown   — read: StandardCostBasis + element rows + sum for the
//                          latest (or specified) crystallized Item.
//   2) Promote Validated — write: Item.StandardCostBasis = Validated.
//   3) Reset FirstActual — write: Item.StandardCostBasis = FirstActual.
//   R) Reload
//
// Writes route through AppDbContext (tenant-scoped via ITenantContext), so this
// probe is ControlPlaneExempt. To populate element rows, crystallize a PoFirst
// PRO on /Admin/CrystallizationServiceProbe first (the mint seeds the split).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
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
    "Admin diagnostic probe for Theme B7 PR-6 first-actual cost seeding + StandardCostBasis flag. " +
    "Reads/writes Item + ItemStandardCostElement via AppDbContext, tenant-scoped through " +
    "ITenantContext.VisibleCompanyIds on every read and write.")]
public sealed class FirstActualCostProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<FirstActualCostProbeModel> _logger;

    public FirstActualCostProbeModel(
        AppDbContext db, ITenantContext tenant, ILogger<FirstActualCostProbeModel> logger)
    {
        _db = db; _tenant = tenant; _logger = logger;
    }

    [BindProperty] public int ItemId { get; set; }

    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int ForecastCount { get; private set; }
    public int FirstActualCount { get; private set; }
    public int ValidatedCount { get; private set; }

    public int? ShownItemId { get; private set; }
    public string? ShownPartNumber { get; private set; }
    public StandardCostBasis? ShownBasis { get; private set; }
    public decimal ShownScalarStandardCost { get; private set; }
    public IReadOnlyList<ElementRow> Elements { get; private set; } = Array.Empty<ElementRow>();
    public decimal ElementsTotal { get; private set; }

    public sealed record ElementRow(CostElementType Type, decimal Amount, CostElementSource Source, string? Notes);

    private void Set(bool ok, string? msg) { Outcome = msg; OutcomeIsError = !ok; }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<Item> ScopedItems() =>
        _db.Items.Where(i => i.CompanyId == null || _tenant.VisibleCompanyIds.Contains(i.CompanyId.Value));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        ForecastCount = await ScopedItems().CountAsync(i => i.StandardCostBasis == StandardCostBasis.Forecast, ct);
        FirstActualCount = await ScopedItems().CountAsync(i => i.StandardCostBasis == StandardCostBasis.FirstActual, ct);
        ValidatedCount = await ScopedItems().CountAsync(i => i.StandardCostBasis == StandardCostBasis.Validated, ct);
    }

    // Resolve target item: explicit Id, else the latest crystallized (CreatedNewItem, non-reversed) Item in scope.
    private async Task<int?> ResolveItemIdAsync(CancellationToken ct)
    {
        if (ItemId > 0) return ItemId;
        return await _db.ItemCrystallizations
            .Where(c => _tenant.VisibleCompanyIds.Contains(c.CompanyId)
                && !c.IsReversed
                && c.Outcome == CrystallizationOutcome.CreatedNewItem
                && c.CreatedItemId != null)
            .OrderByDescending(c => c.Id)
            .Select(c => c.CreatedItemId)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Item?> LoadVisibleItemAsync(int id, CancellationToken ct) =>
        await _db.Items.FirstOrDefaultAsync(
            i => i.Id == id && (i.CompanyId == null || _tenant.VisibleCompanyIds.Contains(i.CompanyId.Value)), ct);

    private async Task LoadBreakdownAsync(int itemId, CancellationToken ct)
    {
        var item = await LoadVisibleItemAsync(itemId, ct);
        if (item == null) return;
        ShownItemId = item.Id;
        ShownPartNumber = item.PartNumber;
        ShownBasis = item.StandardCostBasis;
        ShownScalarStandardCost = item.StandardCost;

        var rows = await _db.ItemStandardCostElements.AsNoTracking()
            .Where(e => e.ItemId == itemId && e.IsActive && e.EffectiveToUtc == null)
            .OrderBy(e => e.ElementType)
            .Select(e => new ElementRow(e.ElementType, e.Amount, e.Source, e.CalculationNotes))
            .ToListAsync(ct);
        Elements = rows;
        ElementsTotal = rows.Sum(r => r.Amount);
    }

    // 1) SHOW breakdown
    public async Task<IActionResult> OnPostShowAsync(CancellationToken ct)
    {
        var id = await ResolveItemIdAsync(ct);
        if (id is null or 0) { Set(false, "No crystallized Item found — crystallize a PoFirst PRO on /Admin/CrystallizationServiceProbe first."); await LoadStatsAsync(ct); return Page(); }
        await LoadBreakdownAsync(id.Value, ct);
        if (ShownItemId == null) { Set(false, $"Item {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }
        Set(true, $"Item #{ShownItemId} ({ShownPartNumber}): basis {ShownBasis}, scalar standard ${ShownScalarStandardCost:N4}, " +
                  $"{Elements.Count} cost element(s) summing ${ElementsTotal:N4}/ea.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) PROMOTE to Validated (write)
    public async Task<IActionResult> OnPostPromoteAsync(CancellationToken ct)
    {
        var id = await ResolveItemIdAsync(ct);
        if (id is null or 0) { Set(false, "No crystallized Item found."); await LoadStatsAsync(ct); return Page(); }
        var item = await LoadVisibleItemAsync(id.Value, ct);
        if (item == null) { Set(false, $"Item {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        item.StandardCostBasis = StandardCostBasis.Validated;
        await _db.SaveChangesAsync(ct);
        await LoadBreakdownAsync(id.Value, ct);
        Set(true, $"Item #{item.Id} ({item.PartNumber}) standard-cost basis promoted to Validated — trusted for repeat planning.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) RESET to FirstActual (write — repeatable demo)
    public async Task<IActionResult> OnPostResetAsync(CancellationToken ct)
    {
        var id = await ResolveItemIdAsync(ct);
        if (id is null or 0) { Set(false, "No crystallized Item found."); await LoadStatsAsync(ct); return Page(); }
        var item = await LoadVisibleItemAsync(id.Value, ct);
        if (item == null) { Set(false, $"Item {id} not in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        item.StandardCostBasis = StandardCostBasis.FirstActual;
        await _db.SaveChangesAsync(ct);
        await LoadBreakdownAsync(id.Value, ct);
        Set(true, $"Item #{item.Id} ({item.PartNumber}) standard-cost basis reset to FirstActual (unvalidated for repeat).");
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
