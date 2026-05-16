using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Reliability;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding
{
    // Sprint 2 PR #117.1 — Industrial-asset seeder.
    //
    // Per Dean's correction on PR #117: brand+type pairings in the demo
    // data were gibberish ("Trane Forklift", "Mazak HVAC Unit", "Kuka Pump").
    // This seeder UPDATES each active asset to a coherent manufacturer +
    // equipment-class combination drawn from real industrial vendors,
    // then INSERTS 30 days of synthetic sensor readings keyed off the
    // equipment class so the Plant Floor numbers read plausibly, then
    // RECOMPUTES HealthScore via AssetHealthService from the resulting
    // data (no more random distribution).
    //
    // Idempotent: bails if any asset already has 5+ AssetSensorReadings
    // rows (assumed already seeded). forceReseed=true wipes + re-runs.
    //
    // Reads as: pick a class for the asset (CNC / robot / welder /
    // press / pump / conveyor / HVAC / forklift / crane / generator),
    // pick a manufacturer that actually makes that class, generate a
    // model number, set asset.Description = "{Brand} {Model} - {Class}".
    public interface IIndustrialAssetSeeder
    {
        Task<int> SeedAsync(bool forceReseed = false);
    }

    public class IndustrialAssetSeeder : IIndustrialAssetSeeder
    {
        private readonly AppDbContext _db;
        private readonly IAssetSensorService _sensors;
        private readonly IAssetHealthService _health;
        private readonly ILogger<IndustrialAssetSeeder> _logger;

        // Deterministic seed = repeatable demo state. Use forceReseed=true
        // with a different seed for variety.
        private const int DeterministicSeed = unchecked((int)0xBEEFCAFE);

        // Per-class catalog: brand list + model-number formula + asset-type
        // label that drives downstream class-keyed thresholds.
        private static readonly EquipmentClass[] Catalog = new[]
        {
            new EquipmentClass(
                AssetType: "CNC Machining Center",
                Brands: new[] { "MAZAK", "DMG MORI", "HAAS", "OKUMA", "DOOSAN", "MAKINO", "MORI SEIKI", "HURCO" },
                ModelPrefix: new[] { "INTEGREX", "VARIAXIS", "VC", "VF", "PUMA", "LB", "MAM", "VTC" },
                Weight: 28),
            new EquipmentClass(
                AssetType: "CNC Lathe",
                Brands: new[] { "MAZAK", "OKUMA", "DOOSAN", "HAAS", "DMG MORI", "TSUGAMI" },
                ModelPrefix: new[] { "QT", "LT", "LB", "ST", "PUMA", "BNA" },
                Weight: 18),
            new EquipmentClass(
                AssetType: "Welding Robot",
                Brands: new[] { "FANUC", "KUKA", "ABB", "YASKAWA", "OTC DAIHEN", "MOTOMAN", "COMAU" },
                ModelPrefix: new[] { "ARC Mate", "KR", "IRB", "MA", "FD", "NX", "SmartArc" },
                Weight: 14),
            new EquipmentClass(
                AssetType: "Material-Handling Robot",
                Brands: new[] { "FANUC", "ABB", "KUKA", "YASKAWA", "EPSON", "STAUBLI" },
                ModelPrefix: new[] { "M-", "IRB", "KR Agilus", "GP", "G-", "TX" },
                Weight: 8),
            new EquipmentClass(
                AssetType: "Welding Power Source",
                Brands: new[] { "LINCOLN ELECTRIC", "MILLER ELECTRIC", "ESAB", "FRONIUS", "OTC DAIHEN", "HOBART" },
                ModelPrefix: new[] { "PowerWave", "Dynasty", "Aristo", "TPS", "DA", "Champion" },
                Weight: 8),
            new EquipmentClass(
                AssetType: "Hydraulic Stamping Press",
                Brands: new[] { "SCHULER", "AIDA", "BLISS", "MINSTER", "KOMATSU" },
                ModelPrefix: new[] { "MSE", "NS2", "C2H", "P2H", "OBS" },
                Weight: 6),
            new EquipmentClass(
                AssetType: "Press Brake",
                Brands: new[] { "AMADA", "TRUMPF", "BYSTRONIC", "LVD", "ACCURPRESS" },
                ModelPrefix: new[] { "HG", "TruBend", "Xpert", "PPEB", "ACCELL" },
                Weight: 4),
            new EquipmentClass(
                AssetType: "Laser Cutter",
                Brands: new[] { "TRUMPF", "AMADA", "BYSTRONIC", "MAZAK", "MITSUBISHI" },
                ModelPrefix: new[] { "TruLaser", "ENSIS", "ByStar", "OPTIPLEX", "ML" },
                Weight: 4),
            new EquipmentClass(
                AssetType: "Industrial Conveyor",
                Brands: new[] { "DORNER", "HYTROL", "INTERROLL", "FlexLink", "BOSCH REXROTH" },
                ModelPrefix: new[] { "3200", "EZLogic", "MultiControl", "X45", "VarioFlow" },
                Weight: 4),
            new EquipmentClass(
                AssetType: "Air Compressor",
                Brands: new[] { "ATLAS COPCO", "INGERSOLL RAND", "KAESER", "SULLAIR", "QUINCY" },
                ModelPrefix: new[] { "GA", "R-Series", "CSD", "ShopTek", "QGS" },
                Weight: 3),
            new EquipmentClass(
                AssetType: "HVAC Unit",
                Brands: new[] { "TRANE", "CARRIER", "YORK", "DAIKIN", "LENNOX" },
                ModelPrefix: new[] { "Voyager", "WeatherMaster", "Sunline", "VRV", "Strategos" },
                Weight: 2),
            new EquipmentClass(
                AssetType: "Forklift",
                Brands: new[] { "TOYOTA", "HYSTER", "CROWN", "RAYMOND", "JUNGHEINRICH" },
                ModelPrefix: new[] { "8FG", "S50FT", "FC", "8410", "EFG" },
                Weight: 1)
        };

        public IndustrialAssetSeeder(
            AppDbContext db,
            IAssetSensorService sensors,
            IAssetHealthService health,
            ILogger<IndustrialAssetSeeder> logger)
        {
            _db = db;
            _sensors = sensors;
            _health = health;
            _logger = logger;
        }

        public async Task<int> SeedAsync(bool forceReseed = false)
        {
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

            var assets = await _db.Assets.Where(a => a.Active).OrderBy(a => a.Id).ToListAsync();
            if (assets.Count == 0) return 0;

            var rng = new Random(DeterministicSeed);

            // 1) Assign each asset a coherent class + brand + model.
            var classByAsset = new Dictionary<int, EquipmentClass>();
            foreach (var asset in assets)
            {
                var cls = PickClassWeighted(rng);
                classByAsset[asset.Id] = cls;

                var brand = cls.Brands[rng.Next(cls.Brands.Length)];
                var prefix = cls.ModelPrefix[rng.Next(cls.ModelPrefix.Length)];
                var modelNum = $"{rng.Next(100, 9999)}";
                var unitNum = asset.AssetNumber?.Replace("AST-", "") ?? rng.Next(10, 999).ToString();

                asset.Description = $"{brand} {prefix}-{modelNum} {ShortClass(cls.AssetType)} #{int.Parse(unitNum)}";
                asset.AssetType = cls.AssetType;
                // Brand is in Description; ManufacturerId FK left untouched
                // to avoid breaking existing manufacturer-based reports.
                asset.Model = $"{prefix}-{modelNum}";
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("IndustrialAssetSeeder: rewrote brand/type pairings on {Count} assets.", assets.Count);

            // 2) Generate 30 days × ~3 readings/day × 3 sensor types per asset.
            var allReadings = new List<AssetSensorReading>(assets.Count * 30 * 3 * 3);
            var now = DateTime.UtcNow;

            foreach (var asset in assets)
            {
                var cls = classByAsset[asset.Id];

                // 4% of assets are "ailing" with rising sensor trend +
                // out-of-spec readings concentrated in the last 7 days.
                // 18% are "watch" — borderline but in spec.
                // 78% are healthy — comfortably in spec.
                var roll = rng.Next(0, 100);
                var ailingMode = roll < 4;
                var watchMode = !ailingMode && roll < 22;

                for (int day = 30; day >= 0; day--)
                {
                    var ageDays = day;
                    var baseAt = now.AddDays(-ageDays);
                    int samplesToday = 3;
                    for (int s = 0; s < samplesToday; s++)
                    {
                        var at = baseAt.AddHours(rng.NextDouble() * 23);
                        var degradationFactor = ailingMode
                            ? Math.Max(0.0, 1.0 - (ageDays / 14.0))
                            : (watchMode ? 0.4 : 0.0);

                        // Temperature — compute jitter in double, then mix with decimal mid/std as decimals.
                        var (tempMid, tempStd) = TempRangeFor(cls.AssetType);
                        var tempJitter = (decimal)((rng.NextDouble() - 0.5) * 2.0) * tempStd;
                        var tempDegrade = (decimal)(degradationFactor * rng.NextDouble() * 35.0);
                        var t = tempMid + tempJitter + tempDegrade;
                        allReadings.Add(MakeReading(asset.Id, SensorReadingType.Temperature, Math.Round(t, 1), "°F", at));

                        // Vibration (mm/s RMS)
                        var (vibMid, vibStd) = VibRangeFor(cls.AssetType);
                        var vibJitter = (decimal)((rng.NextDouble() - 0.5) * 2.0) * vibStd;
                        var vibDegrade = (decimal)(degradationFactor * rng.NextDouble() * 4.5);
                        var v = vibMid + vibJitter + vibDegrade;
                        allReadings.Add(MakeReading(asset.Id, SensorReadingType.Vibration, Math.Round(Math.Max(0m, v), 3), "mm/s", at));

                        // Pressure (PSI)
                        var (presMid, presStd) = PresRangeFor(cls.AssetType);
                        var presJitter = (decimal)((rng.NextDouble() - 0.5) * 2.0) * presStd;
                        var p = presMid + presJitter;
                        // Add a small degradation drift on hydraulic equipment so the demo shows pressure-loss patterns.
                        if (cls.AssetType.Contains("Hydraulic") || cls.AssetType.Contains("Press"))
                            p -= (decimal)(degradationFactor * rng.NextDouble() * 250.0);
                        allReadings.Add(MakeReading(asset.Id, SensorReadingType.Pressure, Math.Round(Math.Max(0m, p), 2), "PSI", at));
                    }
                }
            }

            // 3) Stamp IsOutOfSpec based on per-class thresholds.
            foreach (var r in allReadings)
            {
                var asset = assets.First(a => a.Id == r.AssetId);
                var range = _sensors.GetExpectedRange(r.ReadingType, asset);
                if (r.Value < range.Min || r.Value > range.Max) r.IsOutOfSpec = true;
            }

            // 4) Bulk insert via the service so the Asset.Current* cache
            //    columns get updated atomically to the latest reading per
            //    (asset, type). This is the WHOLE point of going through
            //    the service rather than direct INSERTs.
            await _sensors.RecordBatchAsync(allReadings);
            _logger.LogInformation("IndustrialAssetSeeder: persisted {Count} sensor readings.", allReadings.Count);

            // 5) Recompute HealthScore for every asset from the real data.
            var nHealth = await _health.RecomputeAllAsync();
            _logger.LogInformation("IndustrialAssetSeeder: recomputed HealthScore for {Count} assets.", nHealth);

            return allReadings.Count;
        }

        private static AssetSensorReading MakeReading(int assetId, SensorReadingType type, decimal value, string unit, DateTime at) => new()
        {
            AssetId = assetId,
            ReadingType = type,
            Value = value,
            Unit = unit,
            ReadingAt = at,
            Source = "demo",
            CreatedAt = DateTime.UtcNow
        };

        private static (decimal mid, decimal std) TempRangeFor(string assetType) => assetType switch
        {
            var s when s.Contains("Welding") => (140m, 25m),
            var s when s.Contains("CNC") || s.Contains("Lathe") || s.Contains("Machining") => (120m, 20m),
            var s when s.Contains("Press") || s.Contains("Brake") || s.Contains("Stamping") => (135m, 30m),
            var s when s.Contains("Laser") => (105m, 15m),
            var s when s.Contains("Robot") => (115m, 18m),
            var s when s.Contains("Conveyor") => (95m, 12m),
            var s when s.Contains("Compressor") => (155m, 22m),
            var s when s.Contains("HVAC") => (72m, 10m),
            var s when s.Contains("Forklift") => (110m, 18m),
            _ => (105m, 15m)
        };

        private static (decimal mid, decimal std) VibRangeFor(string assetType) => assetType switch
        {
            var s when s.Contains("CNC") || s.Contains("Lathe") || s.Contains("Machining") => (1.8m, 0.7m),
            var s when s.Contains("Robot") => (1.4m, 0.6m),
            var s when s.Contains("Welding Power") => (0.6m, 0.3m),
            var s when s.Contains("Press") || s.Contains("Brake") || s.Contains("Stamping") => (2.2m, 1.0m),
            var s when s.Contains("Conveyor") => (2.5m, 0.9m),
            var s when s.Contains("Compressor") => (2.0m, 0.8m),
            var s when s.Contains("HVAC") => (1.2m, 0.4m),
            var s when s.Contains("Forklift") => (2.4m, 0.8m),
            _ => (1.8m, 0.6m)
        };

        private static (decimal mid, decimal std) PresRangeFor(string assetType) => assetType switch
        {
            var s when s.Contains("Hydraulic") || s.Contains("Press") || s.Contains("Stamping") || s.Contains("Brake") => (2100m, 220m),
            var s when s.Contains("Compressor") => (115m, 12m),
            var s when s.Contains("HVAC") => (28m, 5m),
            var s when s.Contains("Laser") => (95m, 10m),
            _ => (85m, 15m)
        };

        private static string ShortClass(string assetType) => assetType
            .Replace("Industrial ", "")
            .Replace("Hydraulic ", "");

        private static EquipmentClass PickClassWeighted(Random rng)
        {
            var total = Catalog.Sum(c => c.Weight);
            var roll = rng.Next(0, total);
            int running = 0;
            foreach (var c in Catalog)
            {
                running += c.Weight;
                if (roll < running) return c;
            }
            return Catalog[0];
        }

        private sealed record EquipmentClass(string AssetType, string[] Brands, string[] ModelPrefix, int Weight);
    }
}
