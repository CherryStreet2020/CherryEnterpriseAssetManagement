using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Catalog;
using Abs.FixedAssets.Models.Telemetry;
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

            // -------- PR #117.2 + PR #118.6: class-appropriate sensor tiles --------
            //
            // PR #117.2 introduced the SensorProfile-driven tile layout.
            // PR #118.6 cuts the per-tile READ over from AssetSensorReadings
            // (the legacy per-row hot table) to AssetSensorLatest (PR #118.1
            // denormalized 1-row-per-(asset, type) cache). This collapses
            // 5-8 GroupBy-First round trips into a SINGLE point-lookup join,
            // and the per-tile Tone is now stamped at write time by
            // SensorIngestService so the Plant Floor page does zero math.
            //
            // We still load the EquipmentClass + SensorProfile graph because
            // it drives WHICH tiles each asset shows (label, unit, display
            // order). Threshold math is only used in the fallback path for
            // assets that have a profile but no AssetSensorLatest row yet
            // (e.g. brand-new assets before the next ingest tick).

            // 1) Resolve each asset's EquipmentClass via Asset.AssetType ==
            //    EquipmentClass.Name. (Until we add EquipmentClassId FK on
            //    Asset, this is the match; the seeder writes the class name
            //    into AssetType so it lines up.)
            var classes = await _db.EquipmentClasses
                .Include(c => c.SensorProfiles)
                .Where(c => c.Active)
                .ToListAsync();
            var classByName = classes.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

            // 2) Single point-lookup against AssetSensorLatest — keyed
            //    (AssetId, ReadingType). Composite PK on this table makes
            //    this a clustered-index seek per (asset, type). For the
            //    Main Manufacturing Plant view (~320 assets × ~3 primary
            //    types each), this is one query returning ~960 rows.
            var latestRows = await _db.AssetSensorLatest
                .AsNoTracking()
                .Where(x => assetIds.Contains(x.AssetId))
                .ToListAsync();
            var latestMap = latestRows
                .ToDictionary(r => (r.AssetId, r.ReadingType));

            var allCards = assets.Select(a =>
            {
                var tiles = BuildTilesForAsset(a, classByName, latestMap);
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
        //
        // PR #118.6: reads come from AssetSensorLatest (the denormalized
        // cache populated by SensorIngestService on every ingest). The
        // Tone field is stamped at write time, so this method does zero
        // threshold math in the common path. The SensorProfile-driven
        // fallback only fires for assets whose AssetSensorLatest row
        // hasn't been written yet (brand-new asset, between ticks, etc.).

        private static IReadOnlyList<SensorTile> BuildTilesForAsset(
            Asset asset,
            Dictionary<string, EquipmentClass> classByName,
            Dictionary<(int, SensorReadingType), AssetSensorLatest> latestMap)
        {
            if (string.IsNullOrEmpty(asset.AssetType) ||
                !classByName.TryGetValue(asset.AssetType, out var cls))
            {
                // Asset class unresolved — show legacy denormalized cache
                // columns so the page never goes blank on partial seed.
                // (These Asset.Current* columns are deprecated by ADR-011
                // and slated for drop in PR #118.6.1.)
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
                // Common path: AssetSensorLatest has a row for this
                // (asset, type). Trust the pre-stamped Tone.
                if (latestMap.TryGetValue((asset.Id, p.ReadingType), out var latest))
                {
                    return new SensorTile(
                        Label: p.SensorName,
                        Value: latest.Value,
                        Unit: p.Unit,
                        Tone: latest.Tone,
                        DisplayOrder: p.DisplayOrder);
                }

                // Fallback: no telemetry row yet → render the tile shell
                // so the layout doesn't shift, but mark it muted.
                return new SensorTile(
                    Label: p.SensorName,
                    Value: null,
                    Unit: p.Unit,
                    Tone: "muted",
                    DisplayOrder: p.DisplayOrder);
            }).ToList();
        }

        private static IReadOnlyList<SensorTile> LegacyTiles(Asset a) => new List<SensorTile>
        {
            new("Temp", a.CurrentTemperature, "°F", "muted", 10),
            new("Vib",  a.CurrentVibration,   "mm/s", "muted", 20),
            new("PSI",  a.CurrentPressure,    "PSI", "muted", 30),
        };
    }
}
