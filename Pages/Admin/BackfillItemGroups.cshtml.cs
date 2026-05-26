using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B6 Foundation Sprint PR-FS-1.5 (2026-05-26) — admin trigger for the
// ItemGroupBackfillSeeder. One-shot button. Idempotent. Lock 14 — dev only.
//
// HOTFIX PR-FS-1.5.1 (2026-05-26): added Reclassify-mode toggle.
[Authorize(Roles = "Admin")]
public sealed class BackfillItemGroupsModel : PageModel
{
    private readonly IItemGroupBackfillSeeder _seeder;
    private readonly ILogger<BackfillItemGroupsModel> _logger;

    public BackfillItemGroupsModel(
        IItemGroupBackfillSeeder seeder,
        ILogger<BackfillItemGroupsModel> logger)
    {
        _seeder = seeder;
        _logger = logger;
    }

    public ItemGroupBackfillResult? Result { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(bool reclassify, CancellationToken ct)
    {
        var mode = reclassify
            ? ItemGroupBackfillMode.ReclassifyLegacyBugRows
            : ItemGroupBackfillMode.FillNullsOnly;

        _logger.LogInformation(
            "ItemGroupBackfillSeeder invoked by {User} in mode {Mode}",
            User.Identity?.Name ?? "<anonymous>", mode);

        Result = await _seeder.BackfillAsync(mode, ct);

        _logger.LogInformation(
            "ItemGroupBackfillSeeder finished — mode {Mode}, scanned {Scanned}, classified {Classified}, reclassified {Reclassified}, skipped {Skipped}.",
            Result.Mode, Result.TotalItemsScanned, Result.ItemsClassified, Result.ItemsReclassified, Result.ItemsSkippedNoMapping);

        return Page();
    }
}
