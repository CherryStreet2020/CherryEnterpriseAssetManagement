using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Plant
{
    // PR #117 — Plant Floor Live View · Per-plant grid.
    //
    // Color-coded asset grid for a single Site. Each card shows:
    //   - Asset number + description
    //   - Health score (green/amber/red color-coded)
    //   - Live sensor readouts (temperature, vibration, pressure)
    //   - Open WO count badge
    //   - Location (building / area)
    //   - Click-anywhere → /Assets/Asset/{id}
    //
    // Filter by health band (All / Green / Amber / Red).
    [Authorize]
    public class FloorModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;

        public FloorModel(AppDbContext db, ITenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        public sealed record AssetCard(
            int Id,
            string AssetNumber,
            string Description,
            string? AssetType,
            string? LocationName,
            decimal? HealthScore,
            decimal? Temperature,
            decimal? Vibration,
            decimal? Pressure,
            int OpenWorkOrders,
            DateTime? SensorReadingsLastUpdated);

        [BindProperty(SupportsGet = true)] public int SiteId { get; set; }
        [BindProperty(SupportsGet = true)] public string Band { get; set; } = "all";  // all | green | amber | red

        public Site? Site { get; private set; }
        public IReadOnlyList<AssetCard> Cards { get; private set; } = new List<AssetCard>();
        public int CountTotal { get; private set; }
        public int CountGreen { get; private set; }
        public int CountAmber { get; private set; }
        public int CountRed { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Site = await _db.Sites
                .FirstOrDefaultAsync(s => s.Id == SiteId
                                       && _tenant.VisibleCompanyIds.Contains(s.CompanyId));
            if (Site == null) return NotFound();

            var assets = await _db.Assets
                .Where(a => a.Active && a.SiteId == SiteId
                         && _tenant.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();

            var assetIds = assets.Select(a => a.Id).ToList();

            // Open WO count per asset — single grouped query.
            var openWoCounts = await _db.MaintenanceEvents
                .Where(m => m.AssetId.HasValue && assetIds.Contains(m.AssetId.Value)
                         && (m.Status == MaintenanceStatus.Scheduled
                          || m.Status == MaintenanceStatus.InProgress
                          || m.Status == MaintenanceStatus.OnHold
                          || m.Status == MaintenanceStatus.Overdue))
                .GroupBy(m => m.AssetId!.Value)
                .Select(g => new { AssetId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AssetId, x => x.Count);

            // Location name lookup (when LocationId set).
            var locationIds = assets.Where(a => a.LocationId.HasValue).Select(a => a.LocationId!.Value).Distinct().ToList();
            var locationNames = await _db.Locations
                .Where(l => locationIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Name);

            var allCards = assets.Select(a => new AssetCard(
                Id: a.Id,
                AssetNumber: a.AssetNumber,
                Description: !string.IsNullOrEmpty(a.Description) ? a.Description : (a.AssetType ?? "—"),
                AssetType: a.AssetType,
                LocationName: a.LocationId.HasValue && locationNames.TryGetValue(a.LocationId.Value, out var n) ? n : null,
                HealthScore: a.PredictiveHealthScore,
                Temperature: a.CurrentTemperature,
                Vibration: a.CurrentVibration,
                Pressure: a.CurrentPressure,
                OpenWorkOrders: openWoCounts.GetValueOrDefault(a.Id, 0),
                SensorReadingsLastUpdated: a.SensorReadingsLastUpdated)).ToList();

            CountTotal = allCards.Count;
            CountGreen = allCards.Count(c => c.HealthScore >= 80m);
            CountAmber = allCards.Count(c => c.HealthScore < 80m && c.HealthScore >= 60m);
            CountRed = allCards.Count(c => c.HealthScore < 60m && c.HealthScore.HasValue);

            Cards = (Band?.ToLowerInvariant() ?? "all") switch
            {
                "green" => allCards.Where(c => c.HealthScore >= 80m).ToList(),
                "amber" => allCards.Where(c => c.HealthScore < 80m && c.HealthScore >= 60m).ToList(),
                "red"   => allCards.Where(c => c.HealthScore < 60m && c.HealthScore.HasValue).ToList(),
                _       => allCards
            };

            // Sort red first within whatever band we're showing, so eyeballs
            // go to the problems immediately.
            Cards = Cards.OrderBy(c => c.HealthScore ?? 100m).ToList();

            return Page();
        }
    }
}
