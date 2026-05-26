using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 12.7 PR #5 — admin trigger for CfoMotionDemoSeeder.
//
// Single page, single button, idempotent. Targets the demo tenant
// (CompanyCode='PWH-CAN'). Lock 14 — runs on dev only.
//
// Authorization: Admin-only via [Authorize(Roles = "Admin")] matching
// the /Admin/AssetImport pattern from PR #337.
[Authorize(Roles = "Admin")]
public sealed class SeedCfoMotionDemoModel : PageModel
{
    private readonly ICfoMotionDemoSeeder _seeder;
    private readonly ILogger<SeedCfoMotionDemoModel> _logger;

    public SeedCfoMotionDemoModel(
        ICfoMotionDemoSeeder seeder,
        ILogger<SeedCfoMotionDemoModel> logger)
    {
        _seeder = seeder;
        _logger = logger;
    }

    public CfoMotionDemoSeedResult? Result { get; private set; }

    public void OnGet()
    {
        // Render the page with the button. Result is null until POST.
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        _logger.LogInformation("CfoMotionDemoSeeder invoked by {User}",
            User.Identity?.Name ?? "<anonymous>");
        Result = await _seeder.SeedAsync(ct);
        _logger.LogInformation(
            "CfoMotionDemoSeeder finished — tenant {Tenant} ({Name}) inserted {Inserted}, skipped {Skipped}, warnings {WarnCount}",
            Result.CompanyCode, Result.CompanyName,
            Result.TotalInserted, Result.TotalSkipped, Result.Warnings.Count);
        return Page();
    }
}
