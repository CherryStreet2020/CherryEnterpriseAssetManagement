// Theme B7 Wave A PR-1 — admin probe for the PO-as-Standard + Make-or-Buy
// duality fields on Item (SourcePattern / MakeBuyPolicy / DefaultSourcePreference
// / IsSourceControlled / break-even). No service ships in PR-1 (these are scalar
// field adds), so the probe writes through AppDbContext directly with tenant
// scoping pushed into every query. Write buttons exercise the new columns AND
// the carve-out guard (Lock-16 corollary: probes must exercise writes).
//
//   1) Set StandardFirst          (classic — always allowed)
//   2) Set PoFirst (valid)        (ETO part — passes the carve-out guard)
//   3) Attempt PoFirst on stock   (rejection demo — guard blocks it)
//   4) Set MakeBuyPolicy
//   5) Set DefaultSourcePreference
//   6) Set source-controlled flag + reason
//   7) Set break-even qty + fixed make investment
// Plus a read button (Reload stats).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe for Theme B7 PR-1 scalar field adds on Item " +
    "(SourcePattern / MakeBuyPolicy / duality). No service exists yet; AppDbContext " +
    "reads + writes are tenant-scoped via ITenantContext.VisibleCompanyIds, and the " +
    "SourcePattern write path enforces Item.ValidateSourcePatternCarveout.")]
public sealed class SourcePatternProbeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<SourcePatternProbeModel> _logger;

    public SourcePatternProbeModel(
        AppDbContext db,
        ITenantContext tenant,
        ILogger<SourcePatternProbeModel> logger)
    {
        _db = db;
        _tenant = tenant;
        _logger = logger;
    }

    // ── Inputs ──
    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public MakeBuyPolicy MakeBuyPolicy { get; set; } = MakeBuyPolicy.MakeOrBuy;
    [BindProperty] public DefaultSourcePreference DefaultSourcePreference { get; set; }
        = DefaultSourcePreference.LetSystemDecide;
    [BindProperty] public string? SourceControlReason { get; set; }
        = "Customer flight-safety source control — AS9100 §8.4 approved-source only.";
    [BindProperty] public decimal? MakeBreakEvenQty { get; set; } = 25m;
    [BindProperty] public decimal? FixedMakeInvestment { get; set; } = 4800m;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalItems { get; private set; }
    public int StandardFirstCount { get; private set; }
    public int PoFirstCount { get; private set; }
    public int HybridCount { get; private set; }
    public int SourceControlledCount { get; private set; }

    public IReadOnlyList<ItemRow> SampleItems { get; private set; } = Array.Empty<ItemRow>();

    public sealed record ItemRow(
        int Id, string PartNumber, SourcePattern SourcePattern, MakeBuyCode MakeBuyCode,
        MakeBuyPolicy MakeBuyPolicy, bool IsStocked, PlanningPolicy PlanningPolicy,
        bool IsSourceControlled);

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private IQueryable<Item> ScopedItems() =>
        _db.Set<Item>().Where(x =>
            x.CompanyId == null || _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0));

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalItems = await ScopedItems().CountAsync(ct);
        StandardFirstCount = await ScopedItems().CountAsync(x => x.SourcePattern == SourcePattern.StandardFirst, ct);
        PoFirstCount = await ScopedItems().CountAsync(x => x.SourcePattern == SourcePattern.PoFirst, ct);
        HybridCount = await ScopedItems().CountAsync(x => x.SourcePattern == SourcePattern.Hybrid, ct);
        SourceControlledCount = await ScopedItems().CountAsync(x => x.IsSourceControlled, ct);

        SampleItems = await ScopedItems()
            .OrderByDescending(x => x.Id)
            .Take(15)
            .Select(x => new ItemRow(
                x.Id, x.PartNumber, x.SourcePattern, x.MakeBuyCode, x.MakeBuyPolicy,
                x.IsStocked, x.PlanningPolicy, x.IsSourceControlled))
            .ToListAsync(ct);
    }

    private async Task<Item?> LoadScopedItemAsync(int id, CancellationToken ct) =>
        await ScopedItems().FirstOrDefaultAsync(x => x.Id == id, ct);

    // 1) SET StandardFirst (always allowed)
    public async Task<IActionResult> OnPostSetStandardFirstAsync(CancellationToken ct)
    {
        var item = await LoadScopedItemAsync(ItemId, ct);
        if (item == null) { Set(false, $"Item {ItemId} not found in your tenant scope."); await LoadStatsAsync(ct); return Page(); }
        item.SourcePattern = SourcePattern.StandardFirst;
        await _db.SaveChangesAsync(ct);
        Set(true, $"Item #{item.Id} ({item.PartNumber}) → StandardFirst (classic master-first).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 2) SET PoFirst (valid — passes carve-out)
    public async Task<IActionResult> OnPostSetPoFirstAsync(CancellationToken ct)
    {
        var item = await LoadScopedItemAsync(ItemId, ct);
        if (item == null) { Set(false, $"Item {ItemId} not found in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        if (!Item.ValidateSourcePatternCarveout(SourcePattern.PoFirst, item.IsStocked, item.PlanningPolicy, out var error))
        {
            Set(false, $"Carve-out guard blocked PoFirst on Item #{item.Id} ({item.PartNumber}): {error}");
            await LoadStatsAsync(ct);
            return Page();
        }

        item.SourcePattern = SourcePattern.PoFirst;
        await _db.SaveChangesAsync(ct);
        Set(true, $"Item #{item.Id} ({item.PartNumber}) → PoFirst (PO-as-Standard; master crystallizes at ship).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 3) ATTEMPT PoFirst on a stocking item (rejection demo)
    public async Task<IActionResult> OnPostAttemptPoFirstOnStockAsync(CancellationToken ct)
    {
        var item = await LoadScopedItemAsync(ItemId, ct);
        if (item == null) { Set(false, $"Item {ItemId} not found in your tenant scope."); await LoadStatsAsync(ct); return Page(); }

        var ok = Item.ValidateSourcePatternCarveout(SourcePattern.PoFirst, item.IsStocked, item.PlanningPolicy, out var error);
        if (ok)
        {
            Set(true, $"Item #{item.Id} ({item.PartNumber}) is NOT a stocking/replenished part — " +
                      $"PoFirst would be allowed (IsStocked={item.IsStocked}, PlanningPolicy={item.PlanningPolicy}). " +
                      "Use button 2 to apply it. (Pick a stocked item to see the guard fire.)");
        }
        else
        {
            Set(true, $"Guard correctly REJECTED PoFirst on Item #{item.Id} ({item.PartNumber}): {error}");
        }
        await LoadStatsAsync(ct);
        return Page();
    }

    // 4) SET MakeBuyPolicy
    public async Task<IActionResult> OnPostSetMakeBuyPolicyAsync(CancellationToken ct)
    {
        var item = await LoadScopedItemAsync(ItemId, ct);
        if (item == null) { Set(false, $"Item {ItemId} not found in your tenant scope."); await LoadStatsAsync(ct); return Page(); }
        item.MakeBuyPolicy = MakeBuyPolicy;
        await _db.SaveChangesAsync(ct);
        Set(true, $"Item #{item.Id} ({item.PartNumber}) MakeBuyPolicy → {MakeBuyPolicy} " +
                  $"(MakeBuyCode stays {item.MakeBuyCode}; Inherit derives from it).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 5) SET DefaultSourcePreference
    public async Task<IActionResult> OnPostSetDefaultPreferenceAsync(CancellationToken ct)
    {
        var item = await LoadScopedItemAsync(ItemId, ct);
        if (item == null) { Set(false, $"Item {ItemId} not found in your tenant scope."); await LoadStatsAsync(ct); return Page(); }
        item.DefaultSourcePreference = DefaultSourcePreference;
        await _db.SaveChangesAsync(ct);
        Set(true, $"Item #{item.Id} ({item.PartNumber}) DefaultSourcePreference → {DefaultSourcePreference}.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 6) SET source-controlled flag + reason
    public async Task<IActionResult> OnPostSetSourceControlledAsync(CancellationToken ct)
    {
        var item = await LoadScopedItemAsync(ItemId, ct);
        if (item == null) { Set(false, $"Item {ItemId} not found in your tenant scope."); await LoadStatsAsync(ct); return Page(); }
        item.IsSourceControlled = true;
        item.SourceControlReason = SourceControlReason;
        await _db.SaveChangesAsync(ct);
        Set(true, $"Item #{item.Id} ({item.PartNumber}) flagged source-controlled — " +
                  "Make-or-Buy will hard-gate to MAKE (or approved-source buy).");
        await LoadStatsAsync(ct);
        return Page();
    }

    // 7) SET break-even qty + fixed make investment
    public async Task<IActionResult> OnPostSetBreakEvenAsync(CancellationToken ct)
    {
        var item = await LoadScopedItemAsync(ItemId, ct);
        if (item == null) { Set(false, $"Item {ItemId} not found in your tenant scope."); await LoadStatsAsync(ct); return Page(); }
        item.MakeBreakEvenQty = MakeBreakEvenQty;
        item.FixedMakeInvestment = FixedMakeInvestment;
        await _db.SaveChangesAsync(ct);
        Set(true, $"Item #{item.Id} ({item.PartNumber}) break-even qty={MakeBreakEvenQty:N4}, " +
                  $"fixed make investment={FixedMakeInvestment:C}.");
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
