using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Reliability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Plant
{
    // PR #117 — Plant Floor Live View · Index.
    //
    // List of plants (Sites) with overall health rollup per plant.
    // Click a plant card → /Plant/Floor/{siteId} for the asset-grid view.
    //
    // Auto-bootstraps health data on first hit if every asset's
    // PredictiveHealthScore is null — so the demo lights up immediately
    // without admin clicks.
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IPlantFloorHealthSeeder _seeder;

        public IndexModel(AppDbContext db, ITenantContext tenant, IPlantFloorHealthSeeder seeder)
        {
            _db = db;
            _tenant = tenant;
            _seeder = seeder;
        }

        public sealed record PlantRow(
            int SiteId,
            string SiteCode,
            string Name,
            string? City,
            string Type,
            int TotalAssets,
            int GreenCount,
            int AmberCount,
            int RedCount,
            decimal AvgHealthScore);

        public IReadOnlyList<PlantRow> Plants { get; private set; } = new List<PlantRow>();
        public int TotalAssetsAcrossPlants { get; private set; }
        public int TotalRedAssets { get; private set; }

        public async Task<IActionResult> OnGetAsync(bool reseed = false)
        {
            // Bootstrap health data on first visit so the demo lights up.
            await _seeder.SeedAsync(forceReseed: reseed);

            var sites = await _db.Sites
                .Where(s => s.Status == SiteStatus.Active
                         && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
                .OrderBy(s => s.Name)
                .ToListAsync();

            var assetsBySite = await _db.Assets
                .Where(a => a.Active && a.SiteId.HasValue
                         && _tenant.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .GroupBy(a => a.SiteId!.Value)
                .Select(g => new
                {
                    SiteId = g.Key,
                    Total = g.Count(),
                    Green = g.Count(a => a.PredictiveHealthScore >= 80m),
                    Amber = g.Count(a => a.PredictiveHealthScore < 80m && a.PredictiveHealthScore >= 60m),
                    Red = g.Count(a => a.PredictiveHealthScore < 60m),
                    Avg = g.Average(a => a.PredictiveHealthScore ?? 0m)
                })
                .ToListAsync();

            var lookup = assetsBySite.ToDictionary(x => x.SiteId);

            Plants = sites.Select(s =>
            {
                lookup.TryGetValue(s.Id, out var stats);
                return new PlantRow(
                    SiteId: s.Id,
                    SiteCode: s.SiteCode,
                    Name: s.Name,
                    City: s.City,
                    Type: s.Type.ToString(),
                    TotalAssets: stats?.Total ?? 0,
                    GreenCount: stats?.Green ?? 0,
                    AmberCount: stats?.Amber ?? 0,
                    RedCount: stats?.Red ?? 0,
                    AvgHealthScore: stats?.Avg ?? 0m);
            }).ToList();

            TotalAssetsAcrossPlants = Plants.Sum(p => p.TotalAssets);
            TotalRedAssets = Plants.Sum(p => p.RedCount);

            return Page();
        }
    }
}
