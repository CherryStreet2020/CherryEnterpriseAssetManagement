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

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        _logger.LogInformation("ItemGroupBackfillSeeder invoked by {User}",
            User.Identity?.Name ?? "<anonymous>");
        Result = await _seeder.BackfillAsync(ct);
        _logger.LogInformation(
            "ItemGroupBackfillSeeder finished — scanned {Scanned}, classified {Classified}, skipped {Skipped}.",
            Result.TotalItemsScanned, Result.ItemsClassified, Result.ItemsSkippedNoMapping);
        return Page();
    }
}
