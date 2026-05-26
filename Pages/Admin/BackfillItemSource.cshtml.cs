using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B6 Foundation Sprint PR-FS-1.5.1 (2026-05-26) — admin trigger for the
// ItemSourceBackfillSeeder. One-shot button. Idempotent. Lock 14 — dev only.
[Authorize(Roles = "Admin")]
public sealed class BackfillItemSourceModel : PageModel
{
    private readonly IItemSourceBackfillSeeder _seeder;
    private readonly ILogger<BackfillItemSourceModel> _logger;

    public BackfillItemSourceModel(
        IItemSourceBackfillSeeder seeder,
        ILogger<BackfillItemSourceModel> logger)
    {
        _seeder = seeder;
        _logger = logger;
    }

    public ItemSourceBackfillResult? Result { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        _logger.LogInformation("ItemSourceBackfillSeeder invoked by {User}",
            User.Identity?.Name ?? "<anonymous>");
        Result = await _seeder.BackfillAsync(ct);
        _logger.LogInformation(
            "ItemSourceBackfillSeeder finished — scanned {Scanned}, flipped {Flipped}, left-internal {Internal}.",
            Result.TotalItemsScanned, Result.ItemsFlipped, Result.ItemsLeftInternal);
        return Page();
    }
}
