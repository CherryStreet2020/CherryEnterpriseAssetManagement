using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// Sprint 12.8 PR #5c.1 — admin trigger for CooMotionDemoSeeder.
//
// Single page, single button, idempotent. Targets the demo tenant
// (CompanyCode='PWH-CAN'). Lock 14 — runs on dev only.
//
// Authorization: Admin-only via [Authorize(Roles = "Admin")] matching
// the /Admin/AssetImport pattern from PR #337.
[Authorize(Roles = "Admin")]
public sealed class SeedCooMotionDemoModel : PageModel
{
    private readonly ICooMotionDemoSeeder _seeder;
    private readonly ILogger<SeedCooMotionDemoModel> _logger;

    public SeedCooMotionDemoModel(
        ICooMotionDemoSeeder seeder,
        ILogger<SeedCooMotionDemoModel> logger)
    {
        _seeder = seeder;
        _logger = logger;
    }

    public CooMotionDemoSeedResult? Result { get; private set; }

    public void OnGet()
    {
        // Render the page with the button. Result is null until POST.
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        _logger.LogInformation("CooMotionDemoSeeder invoked by {User}",
            User.Identity?.Name ?? "<anonymous>");
        Result = await _seeder.SeedAsync(ct);
        _logger.LogInformation(
            "CooMotionDemoSeeder finished — tenant {Tenant} ({Name}) totalRows {Total}, alreadySeeded {Already}, warnings {WarnCount}",
            Result.CompanyCode, Result.CompanyName,
            Result.TotalRowsCreated, Result.AlreadySeeded, Result.Warnings.Count);
        return Page();
    }
}
