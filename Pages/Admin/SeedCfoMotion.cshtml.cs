using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 12.7 PR #5 — admin trigger for AbsCfoMotionScenarioSeeder.
//
// Single page, single button, idempotent. Shows per-bucket Inserted / Skipped
// counts + any warnings after the seeder runs. Lock 14 — runs on dev only.
//
// Authorization: Admin-only via [Authorize(Roles = "Admin")] matching the
// /Admin/AssetImport pattern from PR #337.
[Authorize(Roles = "Admin")]
public sealed class SeedCfoMotionModel : PageModel
{
    private readonly IAbsCfoMotionScenarioSeeder _seeder;
    private readonly ILogger<SeedCfoMotionModel> _logger;

    public SeedCfoMotionModel(
        IAbsCfoMotionScenarioSeeder seeder,
        ILogger<SeedCfoMotionModel> logger)
    {
        _seeder = seeder;
        _logger = logger;
    }

    public AbsCfoSeedResult? Result { get; private set; }

    public void OnGet()
    {
        // Render the page with the button. Result is null until POST.
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        _logger.LogInformation("AbsCfoMotionScenarioSeeder invoked by {User}",
            User.Identity?.Name ?? "<anonymous>");
        Result = await _seeder.SeedAsync(ct);
        _logger.LogInformation(
            "AbsCfoMotionScenarioSeeder finished — inserted {Inserted}, skipped {Skipped}, warnings {WarnCount}",
            Result.TotalInserted, Result.TotalSkipped, Result.Warnings.Count);
        return Page();
    }
}
