using System.Threading.Tasks;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Admin
{
    // Sprint 1 fixture seed UI. Admin-only. Provides a single "Seed" button
    // that fires Sprint1FixtureSeeder.SeedAsync and surfaces the result so
    // the operator can confirm what was created before navigating to the
    // upcoming Pareto / MTBF / reliability dashboards (PRs #109/#110/#111).
    [Authorize(Roles = "Admin")]
    public class Sprint1FixtureModel : PageModel
    {
        private readonly Sprint1FixtureSeeder _seeder;

        public Sprint1FixtureModel(Sprint1FixtureSeeder seeder)
        {
            _seeder = seeder;
        }

        public Sprint1FixtureSeeder.SeedResult? Result { get; private set; }
        public string? ErrorMessage { get; private set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                Result = await _seeder.SeedAsync();
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Seed failed: {ex.Message}";
            }
            return Page();
        }
    }
}
