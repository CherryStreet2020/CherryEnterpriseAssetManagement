using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B8 PRO Cockpit Demo Seeder — admin trigger page.
//
// Seeds 3 interconnected PRO scenarios with full B8 transaction layers
// (material tx, op tx, WIP moves, scrap events, ECR/ECO, CAR, labor,
// documents). Targets demo tenant (CompanyCode='PWH-CAN'). Lock 14 — dev only.
[Authorize(Roles = "Admin")]
public sealed class SeedCockpitDemoModel : PageModel
{
    private readonly IProductionCockpitDemoSeeder _seeder;
    private readonly ILogger<SeedCockpitDemoModel> _logger;

    public SeedCockpitDemoModel(
        IProductionCockpitDemoSeeder seeder,
        ILogger<SeedCockpitDemoModel> logger)
    {
        _seeder = seeder;
        _logger = logger;
    }

    public ProductionCockpitDemoSeedResult? Result { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        _logger.LogInformation("ProductionCockpitDemoSeeder invoked by {User}",
            User.Identity?.Name ?? "<anonymous>");
        Result = await _seeder.SeedAsync(ct);
        _logger.LogInformation(
            "ProductionCockpitDemoSeeder finished — tenant {Tenant}, totalRows {Total}, alreadySeeded {Already}",
            Result.CompanyCode, Result.TotalRowsCreated, Result.AlreadySeeded);
        return Page();
    }
}
