using System;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Reliability
{
    // PR #117 — Plant Floor Live View.
    //
    // The Asset model already has CurrentTemperature/Vibration/Pressure and
    // PredictiveHealthScore fields from the audit's "decorative scaffolding"
    // sweep — no service ever wrote to them. This seeder writes realistic
    // values so the Plant Floor view shows a believable green/amber/red
    // distribution out of the box for demos.
    //
    // Distribution target: ~70% green (80–100), ~22% amber (60–79), ~8% red (<60).
    // Sensor values vary by asset class (CNC, conveyor, motor, etc.) so the
    // numbers look plausible. Idempotent: re-running shifts the distribution
    // slightly to simulate "live" updates between demos.
    //
    // Auto-runs once at startup if every asset's HealthScore is null AND any
    // assets exist — bootstrapping a fresh demo DB without admin clicks.
    public interface IPlantFloorHealthSeeder
    {
        Task<int> SeedAsync(bool forceReseed = false);
    }

    public class PlantFloorHealthSeeder : IPlantFloorHealthSeeder
    {
        private readonly AppDbContext _db;
        private readonly ILogger<PlantFloorHealthSeeder> _logger;

        // Deterministic seeding when forceReseed=false → same distribution
        // across demos. forceReseed=true uses live randomness for variety.
        private const int DeterministicSeed = unchecked((int)0xCAFEF00D);

        public PlantFloorHealthSeeder(AppDbContext db, ILogger<PlantFloorHealthSeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> SeedAsync(bool forceReseed = false)
        {
            var assets = await _db.Assets
                .Where(a => a.Active)
                .ToListAsync();
            if (assets.Count == 0) return 0;

            // If forceReseed=false and *any* asset already has a HealthScore,
            // assume seeded and bail. Idempotent default.
            if (!forceReseed && assets.Any(a => a.PredictiveHealthScore.HasValue))
            {
                _logger.LogInformation(
                    "PlantFloorHealthSeeder: HealthScore already populated on {Count} assets; skipping (use forceReseed=true to refresh).",
                    assets.Count(a => a.PredictiveHealthScore.HasValue));
                return 0;
            }

            var rng = forceReseed ? new Random() : new Random(DeterministicSeed);
            var now = DateTime.UtcNow;
            int updated = 0;

            foreach (var asset in assets)
            {
                // Distribution: roll 0-99
                //   0-77   → green  (80-100)
                //   78-95  → amber  (60-79)
                //   96-99  → red    (30-59)
                var roll = rng.Next(0, 100);
                decimal health = roll switch
                {
                    < 78 => 80m + (decimal)rng.NextDouble() * 20m,
                    < 96 => 60m + (decimal)rng.NextDouble() * 19m,
                    _    => 30m + (decimal)rng.NextDouble() * 29m,
                };
                asset.PredictiveHealthScore = Math.Round(health, 2);
                asset.HealthScoreLastCalculated = now;

                // Sensor readings keyed to health: bad health → high temp + high vibration.
                var class_ = (asset.AssetType ?? "").ToLowerInvariant();

                // Base temperature varies by asset class. Add a multiplier
                // when health < 70 to make the asset look "hot."
                decimal baseTempF = class_ switch
                {
                    var s when s.Contains("cnc") || s.Contains("lathe") || s.Contains("mill") => 145m,
                    var s when s.Contains("motor") || s.Contains("pump") => 170m,
                    var s when s.Contains("conveyor") => 110m,
                    var s when s.Contains("crane") || s.Contains("hoist") => 95m,
                    var s when s.Contains("press") || s.Contains("stamping") => 160m,
                    var s when s.Contains("hvac") => 75m,
                    _ => 120m
                };
                var tempJitter = (decimal)((rng.NextDouble() - 0.5) * 14);
                var healthPenalty = health < 70 ? (70m - health) * 0.7m : 0m;
                asset.CurrentTemperature = Math.Round(baseTempF + tempJitter + healthPenalty, 1);

                // Vibration in mm/s RMS. Healthy <2.8, degraded 4.5+.
                decimal baseVib = health > 80
                    ? 1.0m + (decimal)rng.NextDouble() * 1.8m
                    : health > 60
                        ? 2.8m + (decimal)rng.NextDouble() * 1.7m
                        : 4.5m + (decimal)rng.NextDouble() * 3.0m;
                asset.CurrentVibration = Math.Round(baseVib, 3);

                // Pressure in PSI (rough — depends heavily on asset class but
                // typical industrial ranges).
                decimal basePres = class_ switch
                {
                    var s when s.Contains("hydraulic") || s.Contains("press") => 2200m,
                    var s when s.Contains("pneumatic") || s.Contains("compressor") => 110m,
                    var s when s.Contains("hvac") => 28m,
                    _ => 80m
                };
                // Compute pressure jitter in double then convert to decimal — mixing types directly is a CS0019 error.
                var presJitter = (decimal)((rng.NextDouble() - 0.5) * 0.15) * basePres;
                asset.CurrentPressure = Math.Round(basePres + presJitter, 2);

                asset.SensorReadingsLastUpdated = now;
                updated++;
            }

            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "PlantFloorHealthSeeder: populated HealthScore + sensors on {Count} assets.", updated);
            return updated;
        }
    }
}
