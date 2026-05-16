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

            // 7.5) PR #117.5: seed storyline-specific maintenance history so
            //      the 3 storyline assets have narrative-aligned corrective
            //      WOs + an overdue WO. Combined with their sensor breaches,
            //      this guarantees they all land in the Critical band on the
            //      recompute below.
            await SeedStorylineMaintenanceAsync(storylineFlag, assets, rng);

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

            // PR #117.5.1: re-query assets fresh to avoid DbUpdateConcurrencyException.
            // The original `assets` list was loaded at the top of SeedAsync, then
            // saved at step 4 (Mfr/Model rewrite). The chunked sensor-insert path
            // and Npgsql's xmin concurrency tracking can leave the in-memory
            // entities with stale original values, causing the next UPDATE to
            // mismatch xmin and report "0 rows affected" instead of 1. Reloading
            // the entities here picks up current xmin values cleanly.
            var assetIds = assets.Select(a => a.Id).ToList();
            var freshAssets = await _db.Assets
                .Where(a => assetIds.Contains(a.Id))
                .ToListAsync();

            var latestByAssetType = readings
                .GroupBy(r => (r.AssetId, r.ReadingType))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingAt).First());

            foreach (var asset in freshAssets)
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
        // PR #117.5 — Storyline-specific maintenance history.
        //
        // Each storyline asset gets a small set of corrective WOs that match
        // the failure narrative being told on the sensor side. The penalty
        // math: 2 completed Corrective WOs in last 60d = 12 pts; 1 Overdue
        // WO = 10 pts; sensor breaches = capped 40 pts. Total ≈ 62 pts →
        // HealthScore ≈ 38 → solidly Critical.
        //
        // Idempotent: skipped when matching storyline WO already exists
        // (filtered by WorkOrderNumber prefix "STORY-{assetId}-").
        // -------------------------------------------------------------------

        private static readonly Dictionary<Storyline, (string Title1, string Title2, string OverdueTitle)>
            StorylineNarratives = new()
            {
                [Storyline.SpindleBearing] = (
                    "Spindle bearing vibration alarm — replaced upper bearing race + relubed",
                    "Spindle bearing vibration alarm — retorqued housing bolts + balanced rotor",
                    "Spindle taper inspection PM (overdue 12d)"),
                [Storyline.ArcVoltageDrift] = (
                    "Arc voltage drift on weld station — replaced contact tip + cleaned wire-feed drive rolls",
                    "Weld quality reject batch — checked ground clamp, replaced gas diffuser",
                    "Contact tip + nozzle replacement PM (overdue 18d)"),
                [Storyline.ServoOverheat] = (
                    "Axis-3 servo thermal overload — replaced drive cooling fan + cleared coolant tray",
                    "Joint 3 lubrication low — regrease cycle + harmonic gear inspection",
                    "Battery backup + drive firmware update PM (overdue 22d)"),
            };

        private async Task SeedStorylineMaintenanceAsync(
            Dictionary<int, Storyline> storylineFlag,
            List<Asset> assets,
            Random rng)
        {
            if (storylineFlag.Count == 0) return;

            var now = DateTime.UtcNow;
            var assetById = assets.ToDictionary(a => a.Id);

            var storylineAssetIds = storylineFlag.Keys.ToList();
            var existing = await _db.MaintenanceEvents
                .Where(m => storylineAssetIds.Contains(m.AssetId)
                         && m.WorkOrderNumber != null
                         && m.WorkOrderNumber.StartsWith("STORY-"))
                .Select(m => m.AssetId)
                .ToListAsync();
            var alreadySeeded = new HashSet<int>(existing);

            var toAdd = new List<MaintenanceEvent>();
            foreach (var (assetId, storyline) in storylineFlag)
            {
                if (alreadySeeded.Contains(assetId)) continue;
                if (!StorylineNarratives.TryGetValue(storyline, out var n)) continue;
                if (!assetById.TryGetValue(assetId, out var asset)) continue;

                // Corrective WO #1 — completed ~45 days ago (recent enough to count)
                toAdd.Add(BuildCorrectiveWo(
                    asset, n.Title1, scheduledDaysAgo: 50, completedDaysAgo: 45,
                    laborHours: 6.5m, laborCost: 487.50m, partsCost: rng.Next(120, 380),
                    techName: "MAINTENANCE TECH 1",
                    seq: 1));

                // Corrective WO #2 — completed ~15 days ago (more recent, same failure mode resurfacing)
                toAdd.Add(BuildCorrectiveWo(
                    asset, n.Title2, scheduledDaysAgo: 18, completedDaysAgo: 15,
                    laborHours: 3.0m, laborCost: 225.00m, partsCost: rng.Next(80, 260),
                    techName: "MAINTENANCE TECH 2",
                    seq: 2));

                // Overdue PM — scheduled in the past, status=Overdue
                toAdd.Add(new MaintenanceEvent
                {
                    AssetId = asset.Id,
                    Type = MaintenanceType.Preventative,
                    Description = n.OverdueTitle,
                    ScheduledDate = now.AddDays(-rng.Next(12, 25)).Date,
                    CompletedDate = null,
                    Status = MaintenanceStatus.Overdue,
                    Priority = MaintenancePriority.High,
                    EstimatedCost = 180m,
                    WorkOrderNumber = $"STORY-{asset.Id}-PM-OVERDUE",
                });
            }

            if (toAdd.Count == 0)
            {
                _logger.LogInformation("IndustrialAssetSeeder: storyline maintenance already seeded.");
                return;
            }

            _db.MaintenanceEvents.AddRange(toAdd);
            await _db.SaveChangesAsync();
            _logger.LogInformation("IndustrialAssetSeeder: seeded {Count} storyline WOs ({Assets} storyline assets).",
                toAdd.Count, storylineFlag.Count);
        }

        private static MaintenanceEvent BuildCorrectiveWo(
            Asset asset, string description,
            int scheduledDaysAgo, int completedDaysAgo,
            decimal laborHours, decimal laborCost, decimal partsCost,
            string techName, int seq) => new()
            {
                AssetId = asset.Id,
                Type = MaintenanceType.Corrective,
                Description = description,
                ScheduledDate = DateTime.UtcNow.AddDays(-scheduledDaysAgo).Date,
                CompletedDate = DateTime.UtcNow.AddDays(-completedDaysAgo).Date,
                Status = MaintenanceStatus.Completed,
                Priority = MaintenancePriority.High,
                EstimatedCost = Math.Round(laborCost + partsCost + 50m, 0),
                ActualCost = Math.Round(laborCost + partsCost, 2),
                LaborCost = laborCost,
                PartsCost = partsCost,
                LaborHours = laborHours,
                DowntimeHours = laborHours + 1.5m,
                TechnicianName = techName,
                WorkOrderNumber = $"STORY-{asset.Id}-CORR-{seq}",
            };

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

            var mid = (profile.NormalMin + profile.NormalMax) / 2m;
            var std = (profile.NormalMax - profile.NormalMin) / 4m;  // ~95% within band

            for (int i = 0; i < samples; i++)
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
            // toward (and PAST) the critical threshold near the end of the
            // window. That's "the failure unfolding in real time" on the
            // Plant Floor sparkline.
            //
            // PR #117.5: the previous version targeted the critical threshold
            // exactly. Strict-> IsBreach check + cubic ramp meant only ~2-3
            // readings actually breached, which wasn't enough to drive
            // Lincoln Power Wave S350 into the Critical band. Fix: overshoot
            // the threshold by 30% of the normal range so the last ~5% of
            // the window (≈30+ readings) genuinely breach.
            const int sampleMinutes = 15;
            int samples = (int)(TimeSpan.FromDays(7).TotalMinutes / sampleMinutes);

            var baseValue = (profile.NormalMin + profile.NormalMax) / 2m;
            var rangeSize = profile.NormalMax - profile.NormalMin;
            var overshoot = rangeSize * 0.3m;
            var thresholdAnchor = profile.CriticalThreshold ??
                (profile.BreachOnHighSide ? profile.NormalMax : profile.NormalMin);
            var target = profile.BreachOnHighSide
                ? thresholdAnchor + overshoot
                : thresholdAnchor - overshoot;

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

        private enum Storyline
        {
            None = 0,
            SpindleBearing = 1,    // Haas VF-2SS
            ArcVoltageDrift = 2,   // Lincoln Power Wave S350
            ServoOverheat = 3      // KUKA KR 210 R2700
        }
    }
}
