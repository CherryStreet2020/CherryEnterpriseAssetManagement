using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Catalog;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Plant
{
    // PR #117.2 — Plant Floor Live View · class-appropriate sensors.
    //
    // Each asset card now renders the SENSORS THAT MATTER for that
    // asset's EquipmentClass — pulled from SensorProfile (IsPrimary=true).
    // A CNC machining center shows Spindle Temp / Vibration / Load.
    // A welder shows Arc Voltage / Current / Duty Cycle. A forklift
    // shows Hour Meter / Battery State / Hydraulic Oil Temp. No more
    // universal Temp/Vib/PSI on every asset (per Dean's correction:
    // "we can't have cranes with temp readings").
    //
    // Color band on each tile is driven by SensorProfile thresholds:
    //   ok    = inside [NormalMin, NormalMax]
    //   warn  = past WarningThreshold but not Critical
    //   crit  = past CriticalThreshold (high or low side per profile.BreachOnHighSide)
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

        public sealed record SensorTile(
            string Label,
            decimal? Value,
            string Unit,
            string Tone,           // "ok" | "warn" | "crit" | "muted"
            int DisplayOrder);

        public sealed record AssetCard(
            int Id,
            string AssetNumber,
            string Description,
            string? AssetType,
            string? LocationName,
            string? ImageUrl,
            decimal? HealthScore,
            IReadOnlyList<SensorTile> SensorTiles,
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

            // Open WO count per asset.
            var openWoCounts = await _db.MaintenanceEvents
                .Where(m => assetIds.Contains(m.AssetId)
                         && (m.Status == MaintenanceStatus.Scheduled
                          || m.Status == MaintenanceStatus.InProgress
                          || m.Status == MaintenanceStatus.OnHold
                          || m.Status == MaintenanceStatus.Overdue))
                .GroupBy(m => m.AssetId)
                .Select(g => new { AssetId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AssetId, x => x.Count);

            var locationIds = assets.Where(a => a.LocationId.HasValue).Select(a => a.LocationId!.Value).Distinct().ToList();
            var locationNames = await _db.Locations
                .Where(l => locationIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => l.Name);

            // -------- PR #117.2: class-appropriate sensor tiles --------
            // 1) Resolve each asset's EquipmentClass via Asset.AssetType ==
            //    EquipmentClass.Name. (Until we add EquipmentClassId FK on
            //    Asset, this is the match; the seeder writes the class name
            //    into AssetType so it lines up.)
            var classes = await _db.EquipmentClasses
                .Include(c => c.SensorProfiles)
                .Where(c => c.Active)
                .ToListAsync();
            var classByName = classes.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

            // 2) Latest reading per (AssetId, ReadingType) — query per type
            //    rather than a 2-key GroupBy (EF Core 9 is far more reliable
            //    at translating "first per group" inside a single partition).
            //    Only fetch types that any primary sensor profile cares about
            //    so the round-trip count stays bounded (~5-8).
            var primaryTypes = classes
                .SelectMany(c => c.SensorProfiles)
                .Where(p => p.IsPrimary)
                .Select(p => p.ReadingType)
                .Distinct()
                .ToList();

            var readingMap = new Dictionary<(int, SensorReadingType), AssetSensorReading>();
            foreach (var type in primaryTypes)
            {
                var perType = await _db.AssetSensorReadings
                    .Where(r => assetIds.Contains(r.AssetId) && r.ReadingType == type)
                    .GroupBy(r => r.AssetId)
                    .Select(g => g.OrderByDescending(x => x.ReadingAt).First())
                    .ToListAsync();
                foreach (var r in perType)
                {
                    readingMap[(r.AssetId, r.ReadingType)] = r;
                }
            }

            var allCards = assets.Select(a =>
            {
                var tiles = BuildTilesForAsset(a, classByName, readingMap);
                return new AssetCard(
                    Id: a.Id,
                    AssetNumber: a.AssetNumber,
                    Description: !string.IsNullOrEmpty(a.Description) ? a.Description : (a.AssetType ?? "—"),
                    AssetType: a.AssetType,
                    LocationName: a.LocationId.HasValue && locationNames.TryGetValue(a.LocationId.Value, out var n) ? n : null,
                    ImageUrl: a.ImageUrl,
                    HealthScore: a.PredictiveHealthScore,
                    SensorTiles: tiles,
                    OpenWorkOrders: openWoCounts.GetValueOrDefault(a.Id, 0),
                    SensorReadingsLastUpdated: a.SensorReadingsLastUpdated);
            }).ToList();

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

            // Sort red first within whatever band we're showing.
            Cards = Cards.OrderBy(c => c.HealthScore ?? 100m).ToList();

            return Page();
        }

        // -------- Tile builder: 3 primary tiles per asset's class --------

        private static IReadOnlyList<SensorTile> BuildTilesForAsset(
            Asset asset,
            Dictionary<string, EquipmentClass> classByName,
            Dictionary<(int, SensorReadingType), AssetSensorReading> readingMap)
        {
            if (string.IsNullOrEmpty(asset.AssetType) ||
                !classByName.TryGetValue(asset.AssetType, out var cls))
            {
                // Asset class unresolved — show legacy denormalized cache
                // columns so the page never goes blank on partial seed.
                return LegacyTiles(asset);
            }

            var primary = cls.SensorProfiles
                .Where(p => p.IsPrimary)
                .OrderBy(p => p.DisplayOrder)
                .Take(3)
                .ToList();

            if (primary.Count == 0)
            {
                primary = cls.SensorProfiles
                    .OrderBy(p => p.DisplayOrder)
                    .Take(3)
                    .ToList();
            }

            return primary.Select(p =>
            {
                readingMap.TryGetValue((asset.Id, p.ReadingType), out var r);
                return new SensorTile(
                    Label: p.SensorName,
                    Value: r?.Value,
                    Unit: p.Unit,
                    Tone: ClassifyTone(p, r?.Value),
                    DisplayOrder: p.DisplayOrder);
            }).ToList();
        }

        private static IReadOnlyList<SensorTile> LegacyTiles(Asset a) => new List<SensorTile>
        {
            new("Temp", a.CurrentTemperature, "°F", "muted", 10),
            new("Vib",  a.CurrentVibration,   "mm/s", "muted", 20),
            new("PSI",  a.CurrentPressure,    "PSI", "muted", 30),
        };

        private static string ClassifyTone(SensorProfile p, decimal? value)
        {
            if (!value.HasValue) return "muted";
            var v = value.Value;
            if (p.CriticalThreshold.HasValue)
            {
                bool crit = p.BreachOnHighSide
                    ? v >= p.CriticalThreshold.Value
                    : v <= p.CriticalThreshold.Value;
                if (crit) return "crit";
            }
            if (p.WarningThreshold.HasValue)
            {
                bool warn = p.BreachOnHighSide
                    ? v >= p.WarningThreshold.Value
                    : v <= p.WarningThreshold.Value;
                if (warn) return "warn";
            }
            return "ok";
        }
    }
}
