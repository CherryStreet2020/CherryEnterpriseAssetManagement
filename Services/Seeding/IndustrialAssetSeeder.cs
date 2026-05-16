using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Catalog;
using Abs.FixedAssets.Services.Reliability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding
{
    // Sprint 2 PR #117.2 — Industrial-asset seeder, rewritten on top of
    // the curated Equipment Catalog (EquipmentClasses + EquipmentModels +
    // SensorProfiles tables, seeded from EQUIPMENT_CATALOG.md by
    // EquipmentCatalogSeeder).
    //
    // Per Dean: "DO NOT HARDCODE DATA" + "we can't have cranes with temp
    // readings" + "Best in Class Process to Produce a Best In Class product."
    // This version reads brands, models, costs, service lives, AND sensor
    // profiles from the database — no C# arrays. Sensor readings are
    // generated per-class according to the class's SensorProfile, so a
    // CNC machining center gets spindle temp / vibration / load (not
    // hydraulic ram pressure), a welder gets arc voltage / duty cycle
    // (not coolant pressure), and a forklift gets hour meter / battery.
    //
    // Three seeded failure-mode storylines (per D5 in EQUIPMENT_CATALOG.md):
    //   1) Haas VF-2SS — spindle bearing failure imminent
    //      (vibration rising 0.05 mm/s/day, temp rising 0.4°C/day, 14 days)
    //   2) Lincoln Power Wave S350 — arc voltage drift
    //      (every 4th weld trends out of spec; contact-tip wear pattern)
    //   3) KUKA KR 210 R2700 — servo drive overheat
    //      (axis-3 motor temp + current rising under duty cycle)
    public interface IIndustrialAssetSeeder
    {
        Task<int> SeedAsync(bool forceReseed = false);
    }

    public class IndustrialAssetSeeder : IIndustrialAssetSeeder
    {
        private readonly AppDbContext _db;
        private readonly IAssetSensorService _sensors;
        private readonly IAssetHealthService _health;
        private readonly IEquipmentCatalogSeeder _catalog;
        private readonly ILogger<IndustrialAssetSeeder> _logger;

        // Deterministic seed = repeatable demo state. forceReseed=true wipes + re-runs.
        private const int DeterministicSeed = unchecked((int)0xBEEFCAFE);

        // Tier 1 Automotive Stamping plant archetype (per D1 in
        // EQUIPMENT_CATALOG.md). Class-code → mix weight. Higher = more
        // common in the demo plant. Total weight = ~100 makes the numbers
        // read as percentages.
        private static readonly Dictionary<string, int> PlantMix = new()
        {
            ["STAMPING_PRESS"]            = 18,
            ["PRESS_BRAKE"]                = 6,
            ["WELDING_ROBOT"]             = 16,
            ["WELDING_POWER_SOURCE"]      = 10,
            ["MATERIAL_HANDLING_ROBOT"]   = 8,
            ["CNC_MACHINING_CENTER"]      = 10,
            ["CNC_LATHE"]                  = 6,
            ["CNC_5AXIS"]                  = 3,
            ["INDUSTRIAL_CONVEYOR"]       = 8,
            ["AIR_COMPRESSOR"]            = 4,
            ["FORKLIFT"]                   = 5,
            ["HVAC_UNIT"]                  = 4,
            ["LASER_CUTTER"]               = 1,
            ["CMM"]                        = 1,
        };

        public IndustrialAssetSeeder(
            AppDbContext db,
            IAssetSensorService sensors,
            IAssetHealthService health,
            IEquipmentCatalogSeeder catalog,
            ILogger<IndustrialAssetSeeder> logger)
        {
            _db = db;
            _sensors = sensors;
            _health = health;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task<int> SeedAsync(bool forceReseed = false)
        {
            // 0) Ensure the catalog is seeded first (idempotent).
            await _catalog.SeedAsync();

            var existingReadings = await _db.AssetSensorReadings.CountAsync();
            if (!forceReseed && existingReadings > 5)
            {
                _logger.LogInformation("IndustrialAssetSeeder: {Count} readings already present; skipping (forceReseed=true to override).", existingReadings);
                return 0;
            }

            if (forceReseed && existingReadings > 0)
            {
                _logger.LogInformation("IndustrialAssetSeeder: forceReseed=true → wiping {Count} existing readings.", existingReadings);
                _db.AssetSensorReadings.RemoveRange(_db.AssetSensorReadings);
                await _db.SaveChangesAsync();
            }

            // 1) Load the catalog into memory in one pass — classes with their
            //    models and sensor profiles eager-loaded. ~14 classes / ~50
            //    models / ~80 sensor profiles; cheap.
            var classes = await _db.EquipmentClasses
                .Include(c => c.Models)
                .Include(c => c.SensorProfiles)
                .Where(c => c.Active)
                .ToListAsync();

            if (classes.Count == 0)
            {
                _logger.LogWarning("IndustrialAssetSeeder: catalog is empty. Run EquipmentCatalogSeeder.");
                return 0;
            }

            var classByCode = classes.ToDictionary(c => c.Code);

            // 2) Build the weighted class pool from the plant archetype mix
            //    (or fall back to equal weighting if a code is missing).
            var weightedPool = BuildWeightedClassPool(classes);

            var assets = await _db.Assets.Where(a => a.Active).OrderBy(a => a.Id).ToListAsync();
            if (assets.Count == 0) return 0;

            var rng = new Random(DeterministicSeed);

            // 3) Assign each asset a class + a real EquipmentModel (Mfr/Model).
            //    Three storyline slots are reserved for D5 in the catalog.
            var assetClass = new Dictionary<int, EquipmentClass>();
            var assetModel = new Dictionary<int, EquipmentModel>();
            var storylineFlag = new Dictionary<int, Storyline>();

            ReserveStorylineAssets(assets, classByCode, rng, assetClass, assetModel, storylineFlag);

            foreach (var asset in assets)
            {
                if (assetClass.ContainsKey(asset.Id)) continue;  // storyline already set

                var cls = PickWeighted(weightedPool, rng);
                var model = PickModelWeighted(cls.Models, rng);
                assetClass[asset.Id] = cls;
                assetModel[asset.Id] = model;
            }

            // 4) Rewrite asset attributes from the chosen model.
            foreach (var asset in assets)
            {
                var cls = assetClass[asset.Id];
                var model = assetModel[asset.Id];
                var unitNum = ExtractUnitNumber(asset.AssetNumber, rng);

                asset.Description = string.IsNullOrWhiteSpace(model.DisplayName)
                    ? $"{model.Manufacturer} {model.ModelNumber} #{unitNum}"
                    : $"{model.DisplayName} #{unitNum}";
                asset.AssetType = cls.Name;
                asset.Model = model.ModelNumber;
                if (asset.AcquisitionCost == 0m && model.TypicalAcquisitionCost.HasValue)
                {
                    asset.AcquisitionCost = model.TypicalAcquisitionCost.Value;
                }
                if (model.ImageUrl != null && string.IsNullOrWhiteSpace(asset.ImageUrl))
                {
                    asset.ImageUrl = model.ImageUrl;
                }
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("IndustrialAssetSeeder: rewrote brand/type pairings on {Count} assets from catalog.", assets.Count);

            // 5) Generate sensor readings per class according to its SensorProfile.
            //    PR #117.3: dropped from 30 days @ profile.SampleRateMinutes
            //    to 14 days @ max(profile.SampleRateMinutes, 240min). For
            //    320 assets × ~5 sensors × 84 samples = ~135K rows — fits
            //    in chunked inserts without timing out Replit's request.
            //    Storyline overlays still get higher resolution.
            var now = DateTime.UtcNow;
            var allReadings = new List<AssetSensorReading>(capacity: 150_000);

            // 4) Rewrite asset attributes from the chosen model.
            foreach (var asset in assets)
            {
                var cls = assetClass[asset.Id];
                var profiles = cls.SensorProfiles.OrderBy(p => p.DisplayOrder).ToList();
                if (profiles.Count == 0) continue;

                var storyline = storylineFlag.TryGetValue(asset.Id, out var s) ? s : Storyline.None;
                GenerateReadingsForAsset(asset, profiles, storyline, now, rng, allReadings);
            }
            _logger.LogInformation("IndustrialAssetSeeder: generated {Count} sensor readings in memory; persisting in chunks.", allReadings.Count);

            // 6) Chunked bulk insert. PR #117.3: 25K-row chunks with progress
            //    logging so a stalled seed surfaces in the app log.
            var persisted = await _sensors.RecordBatchChunkedAsync(allReadings, chunkSize: 25_000);
            _logger.LogInformation("IndustrialAssetSeeder: persisted {Count} sensor readings.", persisted);

            // 7) Final cache pass: write the latest reading per (asset, type)
            //    into Asset.Current* columns for the legacy Plant Floor tiles.
            await BackfillAssetCacheAsync(allReadings, assets);

            // 8) Recompute HealthScore for every asset from the real data.
            var nHealth = await _health.RecomputeAllAsync();
            _logger.LogInformation("IndustrialAssetSeeder: recomputed HealthScore for {Count} assets.", nHealth);

            return allReadings.Count;
        }

        // -------------------------------------------------------------------
        // Cache backfill (PR #117.3) — runs once after the chunked insert
        // completes. Writes the latest Temperature / Vibration / Pressure
        // value per asset into the denormalized Asset.Current* columns so
        // the legacy Plant Floor fallback tiles render values too.
        // -------------------------------------------------------------------

        private async Task BackfillAssetCacheAsync(List<AssetSensorReading> readings, List<Asset> assets)
        {
            if (readings.Count == 0 || assets.Count == 0) return;

            var latestByAssetType = readings
                .GroupBy(r => (r.AssetId, r.ReadingType))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingAt).First());

            foreach (var asset in assets)
            {
                if (latestByAssetType.TryGetValue((asset.Id, SensorReadingType.Temperature), out var t))
                {
                    asset.CurrentTemperature = Math.Round(t.Value, 1);
                    if (asset.SensorReadingsLastUpdated == null || t.ReadingAt > asset.SensorReadingsLastUpdated)
                        asset.SensorReadingsLastUpdated = t.ReadingAt;
                }
                if (latestByAssetType.TryGetValue((asset.Id, SensorReadingType.Vibration), out var v))
                {
                    asset.CurrentVibration = Math.Round(v.Value, 3);
                    if (asset.SensorReadingsLastUpdated == null || v.ReadingAt > asset.SensorReadingsLastUpdated)
                        asset.SensorReadingsLastUpdated = v.ReadingAt;
                }
                if (latestByAssetType.TryGetValue((asset.Id, SensorReadingType.Pressure), out var p))
                {
                    asset.CurrentPressure = Math.Round(p.Value, 2);
                    if (asset.SensorReadingsLastUpdated == null || p.ReadingAt > asset.SensorReadingsLastUpdated)
                        asset.SensorReadingsLastUpdated = p.ReadingAt;
                }
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("IndustrialAssetSeeder: backfilled Asset.Current* cache for {Count} assets.", assets.Count);
        }

        // -------------------------------------------------------------------
        // Reading generation
        // -------------------------------------------------------------------

        private static void GenerateReadingsForAsset(
            Asset asset,
            List<SensorProfile> profiles,
            Storyline storyline,
            DateTime now,
            Random rng,
            List<AssetSensorReading> sink)
        {
            // Baseline: 1 reading per profile.SampleRateMinutes × 30 days.
            // Storyline assets ALSO get 1 reading per 15 minutes × 7 days
            // on their storyline-relevant sensors with rising-trend values.
            foreach (var profile in profiles)
            {
                EmitBaselineReadings(asset, profile, now, rng, sink);
                if (storyline != Storyline.None && IsStorylineSensor(storyline, profile))
                {
                    EmitStorylineOverlay(asset, profile, storyline, now, rng, sink);
                }
            }
        }

        private static void EmitBaselineReadings(
            Asset asset, SensorProfile profile, DateTime now, Random rng, List<AssetSensorReading> sink)
        {
            // PR #117.3: floor sample rate at 4 hours, window at 14 days.
            // Yields ~84 readings per (asset, sensor) — enough for sparklines,
            // small enough that 320 assets × 5 sensors fits in the chunked path.
            const int MinSampleMinutes = 240;          // 4 hours
            const int WindowDays = 14;
            var sampleMinutes = Math.Max(MinSampleMinutes, profile.SampleRateMinutes);
            var samples = (int)(TimeSpan.FromDays(WindowDays).TotalMinutes / sampleMinutes);
            samples = Math.Min(samples, WindowDays * 6);  // safety cap (6 per day)

<<<<<<< rel/pr117.3-seeder-chunked
            var mid = (profile.NormalMin + profile.NormalMax) / 2m;
            var std = (profile.NormalMax - profile.NormalMin) / 4m;  // ~95% within band

            for (int i = 0; i < samples; i++)
            {
                var minutesAgo = i * sampleMinutes;
                var at = now.AddMinutes(-minutesAgo);
                var jitter = (decimal)((rng.NextDouble() - 0.5) * 2.0) * std;
                var value = Math.Round(mid + jitter, 3);
=======
            // 6) Bulk insert via the sensor service so the Asset.Current* cache
            //    columns get updated atomically to the latest reading per
            //    (asset, type). This is the WHOLE point of going through the
            //    service rather than direct INSERTs.
            await _sensors.RecordBatchAsync(allReadings);
            _logger.LogInformation("IndustrialAssetSeeder: persisted {Count} sensor readings.", allReadings.Count);

            // 7) Recompute HealthScore for every asset from the real data.
            var nHealth = await _health.RecomputeAllAsync();
            _logger.LogInformation("IndustrialAssetSeeder: recomputed HealthScore for {Count} assets.", nHealth);
>>>>>>> main

                sink.Add(new AssetSensorReading
                {
                    AssetId = asset.Id,
                    ReadingType = profile.ReadingType,
                    Value = value,
                    Unit = profile.Unit,
                    ReadingAt = at,
                    Source = "demo",
                    IsOutOfSpec = IsBreach(profile, value),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

<<<<<<< rel/pr117.3-seeder-chunked
        private static void EmitStorylineOverlay(
            Asset asset, SensorProfile profile, Storyline storyline, DateTime now, Random rng, List<AssetSensorReading> sink)
        {
            // 15-min samples × 7 days = 672 readings, with a rising trend
            // toward (and through) the critical threshold near the end of
            // the window. That's "the failure unfolding in real time" on
            // the Plant Floor sparkline.
            const int sampleMinutes = 15;
            int samples = (int)(TimeSpan.FromDays(7).TotalMinutes / sampleMinutes);

            var baseValue = profile.BreachOnHighSide
                ? (profile.NormalMin + profile.NormalMax) / 2m
                : (profile.NormalMin + profile.NormalMax) / 2m;
            var target = profile.CriticalThreshold ?? (profile.BreachOnHighSide
                ? profile.NormalMax + (profile.NormalMax - profile.NormalMin) * 0.3m
                : profile.NormalMin - (profile.NormalMax - profile.NormalMin) * 0.3m);

            for (int i = samples - 1; i >= 0; i--)
            {
                var minutesAgo = i * sampleMinutes;
                var at = now.AddMinutes(-minutesAgo);
                var progress = 1.0m - ((decimal)i / samples);   // 0..1, recent = closer to 1

                // Sigmoid-ish rise so it stays calm for the first half then
                // accelerates — more believable than a straight line.
                var ramp = progress * progress * progress;
                var value = baseValue + (target - baseValue) * ramp;

                // Light jitter on top of the trend so the sparkline is alive.
                var jitter = (decimal)((rng.NextDouble() - 0.5) * 0.4) * (profile.NormalMax - profile.NormalMin) / 4m;
                value = Math.Round(value + jitter, 3);

                sink.Add(new AssetSensorReading
                {
                    AssetId = asset.Id,
                    ReadingType = profile.ReadingType,
                    Value = value,
                    Unit = profile.Unit,
                    ReadingAt = at,
                    Source = "demo:storyline",
                    IsOutOfSpec = IsBreach(profile, value),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private static bool IsBreach(SensorProfile profile, decimal value)
        {
            if (!profile.CriticalThreshold.HasValue) return false;
            return profile.BreachOnHighSide
                ? value > profile.CriticalThreshold.Value
                : value < profile.CriticalThreshold.Value;
        }

        private static bool IsStorylineSensor(Storyline storyline, SensorProfile profile) =>
            (storyline, profile.ReadingType) switch
            {
                (Storyline.SpindleBearing, SensorReadingType.Vibration) => true,
                (Storyline.SpindleBearing, SensorReadingType.Temperature) => true,
                (Storyline.ArcVoltageDrift, SensorReadingType.Voltage)   => true,
                (Storyline.ServoOverheat, SensorReadingType.Temperature) => true,
                (Storyline.ServoOverheat, SensorReadingType.Current)     => true,
                _ => false
            };

        // -------------------------------------------------------------------
        // Class + model selection
        // -------------------------------------------------------------------

        private static List<EquipmentClass> BuildWeightedClassPool(List<EquipmentClass> classes)
        {
            var pool = new List<EquipmentClass>(capacity: 120);
            foreach (var cls in classes)
            {
                var weight = PlantMix.TryGetValue(cls.Code, out var w) ? w : 1;
                for (int i = 0; i < weight; i++) pool.Add(cls);
            }
            return pool;
        }

        private static EquipmentClass PickWeighted(List<EquipmentClass> pool, Random rng)
            => pool[rng.Next(pool.Count)];

        private static EquipmentModel PickModelWeighted(ICollection<EquipmentModel> models, Random rng)
        {
=======
        // -------------------------------------------------------------------
        // Reading generation
        // -------------------------------------------------------------------

        private static void GenerateReadingsForAsset(
            Asset asset,
            List<SensorProfile> profiles,
            Storyline storyline,
            DateTime now,
            Random rng,
            List<AssetSensorReading> sink)
        {
            // Baseline: 1 reading per profile.SampleRateMinutes × 30 days.
            // Storyline assets ALSO get 1 reading per 15 minutes × 7 days
            // on their storyline-relevant sensors with rising-trend values.
            foreach (var profile in profiles)
            {
                EmitBaselineReadings(asset, profile, now, rng, sink);
                if (storyline != Storyline.None && IsStorylineSensor(storyline, profile))
                {
                    EmitStorylineOverlay(asset, profile, storyline, now, rng, sink);
                }
            }
        }

        private static void EmitBaselineReadings(
            Asset asset, SensorProfile profile, DateTime now, Random rng, List<AssetSensorReading> sink)
        {
            var sampleMinutes = Math.Max(15, profile.SampleRateMinutes);
            var samplesIn30d = (int)((TimeSpan.FromDays(30).TotalMinutes) / sampleMinutes);
            samplesIn30d = Math.Min(samplesIn30d, 30 * 24);  // safety cap

            var mid = (profile.NormalMin + profile.NormalMax) / 2m;
            var std = (profile.NormalMax - profile.NormalMin) / 4m;  // ~95% within band

            for (int i = 0; i < samplesIn30d; i++)
            {
                var minutesAgo = i * sampleMinutes;
                var at = now.AddMinutes(-minutesAgo);
                var jitter = (decimal)((rng.NextDouble() - 0.5) * 2.0) * std;
                var value = Math.Round(mid + jitter, 3);

                sink.Add(new AssetSensorReading
                {
                    AssetId = asset.Id,
                    ReadingType = profile.ReadingType,
                    Value = value,
                    Unit = profile.Unit,
                    ReadingAt = at,
                    Source = "demo",
                    IsOutOfSpec = IsBreach(profile, value),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private static void EmitStorylineOverlay(
            Asset asset, SensorProfile profile, Storyline storyline, DateTime now, Random rng, List<AssetSensorReading> sink)
        {
            // 15-min samples × 7 days = 672 readings, with a rising trend
            // toward (and through) the critical threshold near the end of
            // the window. That's "the failure unfolding in real time" on
            // the Plant Floor sparkline.
            const int sampleMinutes = 15;
            int samples = (int)(TimeSpan.FromDays(7).TotalMinutes / sampleMinutes);

            var baseValue = profile.BreachOnHighSide
                ? (profile.NormalMin + profile.NormalMax) / 2m
                : (profile.NormalMin + profile.NormalMax) / 2m;
            var target = profile.CriticalThreshold ?? (profile.BreachOnHighSide
                ? profile.NormalMax + (profile.NormalMax - profile.NormalMin) * 0.3m
                : profile.NormalMin - (profile.NormalMax - profile.NormalMin) * 0.3m);

            for (int i = samples - 1; i >= 0; i--)
            {
                var minutesAgo = i * sampleMinutes;
                var at = now.AddMinutes(-minutesAgo);
                var progress = 1.0m - ((decimal)i / samples);   // 0..1, recent = closer to 1

                // Sigmoid-ish rise so it stays calm for the first half then
                // accelerates — more believable than a straight line.
                var ramp = progress * progress * progress;
                var value = baseValue + (target - baseValue) * ramp;

                // Light jitter on top of the trend so the sparkline is alive.
                var jitter = (decimal)((rng.NextDouble() - 0.5) * 0.4) * (profile.NormalMax - profile.NormalMin) / 4m;
                value = Math.Round(value + jitter, 3);

                sink.Add(new AssetSensorReading
                {
                    AssetId = asset.Id,
                    ReadingType = profile.ReadingType,
                    Value = value,
                    Unit = profile.Unit,
                    ReadingAt = at,
                    Source = "demo:storyline",
                    IsOutOfSpec = IsBreach(profile, value),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private static bool IsBreach(SensorProfile profile, decimal value)
        {
            if (!profile.CriticalThreshold.HasValue) return false;
            return profile.BreachOnHighSide
                ? value > profile.CriticalThreshold.Value
                : value < profile.CriticalThreshold.Value;
        }

        private static bool IsStorylineSensor(Storyline storyline, SensorProfile profile) =>
            (storyline, profile.ReadingType) switch
            {
                (Storyline.SpindleBearing, SensorReadingType.Vibration) => true,
                (Storyline.SpindleBearing, SensorReadingType.Temperature) => true,
                (Storyline.ArcVoltageDrift, SensorReadingType.Voltage)   => true,
                (Storyline.ServoOverheat, SensorReadingType.Temperature) => true,
                (Storyline.ServoOverheat, SensorReadingType.Current)     => true,
                _ => false
            };

        // -------------------------------------------------------------------
        // Class + model selection
        // -------------------------------------------------------------------

        private static List<EquipmentClass> BuildWeightedClassPool(List<EquipmentClass> classes)
        {
            var pool = new List<EquipmentClass>(capacity: 120);
            foreach (var cls in classes)
            {
                var weight = PlantMix.TryGetValue(cls.Code, out var w) ? w : 1;
                for (int i = 0; i < weight; i++) pool.Add(cls);
            }
            return pool;
        }

        private static EquipmentClass PickWeighted(List<EquipmentClass> pool, Random rng)
            => pool[rng.Next(pool.Count)];

        private static EquipmentModel PickModelWeighted(ICollection<EquipmentModel> models, Random rng)
        {
>>>>>>> main
            var pool = new List<EquipmentModel>();
            foreach (var m in models)
            {
                if (!m.Active) continue;
                var weight = Math.Max(1, m.Weight);
                for (int i = 0; i < weight; i++) pool.Add(m);
            }
            if (pool.Count == 0) return models.First();
            return pool[rng.Next(pool.Count)];
        }

        // -------------------------------------------------------------------
        // Storyline reservation (D5 in EQUIPMENT_CATALOG.md)
        // -------------------------------------------------------------------

        private static void ReserveStorylineAssets(
            List<Asset> assets,
            Dictionary<string, EquipmentClass> classByCode,
            Random rng,
            Dictionary<int, EquipmentClass> assetClass,
            Dictionary<int, EquipmentModel> assetModel,
            Dictionary<int, Storyline> storylineFlag)
        {
            // 1) Haas VF-2SS spindle bearing
            if (TryReserve(assets, classByCode, "CNC_MACHINING_CENTER", "VF-2SS", rng,
                assetClass, assetModel, out var haasId))
            {
                storylineFlag[haasId] = Storyline.SpindleBearing;
            }

            // 2) Lincoln Power Wave S350 arc voltage drift
            if (TryReserve(assets, classByCode, "WELDING_POWER_SOURCE", "Power Wave S350", rng,
                assetClass, assetModel, out var lincolnId))
<<<<<<< rel/pr117.3-seeder-chunked
            {
                storylineFlag[lincolnId] = Storyline.ArcVoltageDrift;
            }

            // 3) KUKA KR 210 servo overheat
            if (TryReserve(assets, classByCode, "WELDING_ROBOT", "KR 210 R2700", rng,
                assetClass, assetModel, out var kukaId))
            {
                storylineFlag[kukaId] = Storyline.ServoOverheat;
            }
        }

        private static bool TryReserve(
            List<Asset> assets,
            Dictionary<string, EquipmentClass> classByCode,
            string classCode,
            string targetModelNumber,
            Random rng,
            Dictionary<int, EquipmentClass> assetClass,
            Dictionary<int, EquipmentModel> assetModel,
            out int assetId)
        {
            assetId = 0;
            if (!classByCode.TryGetValue(classCode, out var cls)) return false;
            var model = cls.Models.FirstOrDefault(m => m.ModelNumber == targetModelNumber);
            if (model == null) return false;

            var candidate = assets.FirstOrDefault(a => !assetClass.ContainsKey(a.Id));
            if (candidate == null) return false;

            assetId = candidate.Id;
            assetClass[candidate.Id] = cls;
            assetModel[candidate.Id] = model;
            return true;
        }

        // -------------------------------------------------------------------
        // Misc
        // -------------------------------------------------------------------

        private static string ExtractUnitNumber(string? assetNumber, Random rng)
        {
            if (string.IsNullOrWhiteSpace(assetNumber)) return rng.Next(10, 999).ToString();
            var digits = new string(assetNumber.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? rng.Next(10, 999).ToString() : digits;
        }

=======
            {
                storylineFlag[lincolnId] = Storyline.ArcVoltageDrift;
            }

            // 3) KUKA KR 210 servo overheat
            if (TryReserve(assets, classByCode, "WELDING_ROBOT", "KR 210 R2700", rng,
                assetClass, assetModel, out var kukaId))
            {
                storylineFlag[kukaId] = Storyline.ServoOverheat;
            }
        }

        private static bool TryReserve(
            List<Asset> assets,
            Dictionary<string, EquipmentClass> classByCode,
            string classCode,
            string targetModelNumber,
            Random rng,
            Dictionary<int, EquipmentClass> assetClass,
            Dictionary<int, EquipmentModel> assetModel,
            out int assetId)
        {
            assetId = 0;
            if (!classByCode.TryGetValue(classCode, out var cls)) return false;
            var model = cls.Models.FirstOrDefault(m => m.ModelNumber == targetModelNumber);
            if (model == null) return false;

            var candidate = assets.FirstOrDefault(a => !assetClass.ContainsKey(a.Id));
            if (candidate == null) return false;

            assetId = candidate.Id;
            assetClass[candidate.Id] = cls;
            assetModel[candidate.Id] = model;
            return true;
        }

        // -------------------------------------------------------------------
        // Misc
        // -------------------------------------------------------------------

        private static string ExtractUnitNumber(string? assetNumber, Random rng)
        {
            if (string.IsNullOrWhiteSpace(assetNumber)) return rng.Next(10, 999).ToString();
            var digits = new string(assetNumber.Where(char.IsDigit).ToArray());
            return string.IsNullOrEmpty(digits) ? rng.Next(10, 999).ToString() : digits;
        }

>>>>>>> main
        private enum Storyline
        {
            None = 0,
            SpindleBearing = 1,    // Haas VF-2SS
            ArcVoltageDrift = 2,   // Lincoln Power Wave S350
            ServoOverheat = 3      // KUKA KR 210 R2700
        }
    }
}
