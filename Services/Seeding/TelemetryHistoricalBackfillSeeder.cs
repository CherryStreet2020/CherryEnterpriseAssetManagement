using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Catalog;
using Abs.FixedAssets.Models.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

// PR #118.5.1 — Disambiguate UnitOfMeasure. The root namespace
// Abs.FixedAssets.Models has a pre-existing UnitOfMeasure enum (used
// by legacy AssetSensor channel columns) and the new
// Abs.FixedAssets.Models.Telemetry namespace introduced its own
// expanded UnitOfMeasure enum (PR #118.1). Both `using`s above are
// needed for other types, so we alias the Telemetry one as `Uom`
// throughout this file. This is the only collision in the codebase.
using Uom = Abs.FixedAssets.Models.Telemetry.UnitOfMeasure;

namespace Abs.FixedAssets.Services.Seeding
{
    // Sprint 2 PR #118.5 — Historical backfill for the new telemetry
    // substrate (ADR-011 Layer 1).
    //
    // Generates ~30 days of synthetic-but-realistic sensor history for
    // every (Asset, IsPrimary SensorProfile) pair, plus 7 days of
    // 1-minute-resolution rising-trend data for the three storyline
    // assets (Haas VF-2SS spindle, Lincoln Power Wave S350 arc, KUKA
    // KR 210 servo). The output: ~2M SensorEvent rows and one
    // AssetSensorLatest row per (Asset, ReadingType).
    //
    // Patterns baked in:
    //   - Sinusoidal daily cycle (peak mid-shift, trough off-hours)
    //   - 20% reduction on Saturday + Sunday (weekend dip)
    //   - Gaussian noise (~5% of normal-band width)
    //   - Random out-of-spec spikes (0.5% of events) to seed the
    //     SensorAlarm lifecycle work in PR #118.6
    //   - Storyline-asset linear trend rising over the last 14 days
    //     so the demo can show "predictive maintenance caught this"
    //
    // Idempotent: short-circuits if SensorEvents already contains
    // rows for the seeded date range. Safe to re-run on every startup.
    //
    // Performance: uses Npgsql Binary COPY (NpgsqlBinaryImporter) so
    // ~2M rows insert in 10-30 seconds on Replit Postgres. AssetSensorLatest
    // is updated via a single EF SaveChanges after the COPY completes.
    public interface ITelemetryHistoricalBackfillSeeder
    {
        Task<int> SeedAsync(CancellationToken ct = default);
    }

    public class TelemetryHistoricalBackfillSeeder : ITelemetryHistoricalBackfillSeeder
    {
        private readonly AppDbContext _db;
        private readonly ILogger<TelemetryHistoricalBackfillSeeder> _logger;

        // Tunables. Bump these as the demo matures.
        private const int BackfillDays = 30;
        private const int ResolutionMinutes = 15;        // ~96 events/day per (asset, sensor)
        private const int StorylineDays = 7;             // last N days of high-res storyline data
        private const int StorylineResolutionMinutes = 1; // 1-minute for storylines
        private const double OutOfSpecSpikeProbability = 0.005;  // 0.5% of events spike
        private const double WeekendDipFactor = 0.20;    // production-related signals 20% lower on weekends

        // Storyline assets (matches PR #117.6 / PR #117.7 storylines)
        private static readonly HashSet<string> StorylineModelKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "HAAS VF-2SS",
            "POWER WAVE S350",
            "KR 210 R2700",
        };

        public TelemetryHistoricalBackfillSeeder(
            AppDbContext db,
            ILogger<TelemetryHistoricalBackfillSeeder> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> SeedAsync(CancellationToken ct = default)
        {
            // ---- Idempotency check ----
            var existingCount = await _db.SensorEvents.AsNoTracking().LongCountAsync(ct);
            if (existingCount > 1000)
            {
                _logger.LogInformation(
                    "TelemetryHistoricalBackfillSeeder: skipped — SensorEvents already has {Count} rows.",
                    existingCount);
                return 0;
            }

            var now = DateTime.UtcNow;
            var startUtc = now.AddDays(-BackfillDays);

            _logger.LogInformation(
                "TelemetryHistoricalBackfillSeeder: backfilling {Days} days ({StartUtc:O} to now).",
                BackfillDays, startUtc);

            // ---- Resolve all (Asset, PrimaryProfile) pairs ----
            // Pull AssetType (lowercased) + the asset's installed-model
            // identifier so we can detect storyline assets cheaply.
            var assets = await _db.Assets
                .AsNoTracking()
                .Where(a => a.Active && !string.IsNullOrEmpty(a.AssetType))
                .Select(a => new
                {
                    a.Id,
                    AssetType = a.AssetType!,
                    a.Description,
                    a.CompanyId,
                })
                .ToListAsync(ct);

            if (assets.Count == 0)
            {
                _logger.LogWarning(
                    "TelemetryHistoricalBackfillSeeder: no active assets — nothing to backfill.");
                return 0;
            }

            // Pre-load primary SensorProfiles by EquipmentClass.Name (lowercased)
            var profilesByClass = await _db.SensorProfiles
                .AsNoTracking()
                .Include(p => p.EquipmentClass)
                .Where(p => p.IsPrimary && p.EquipmentClass != null)
                .Select(p => new
                {
                    ClassNameLower = p.EquipmentClass!.Name.ToLower(),
                    p.ReadingType,
                    p.Unit,
                    p.NormalMin,
                    p.NormalMax,
                    p.WarningThreshold,
                    p.CriticalThreshold,
                    p.BreachOnHighSide,
                    p.DisplayOrder,
                })
                .ToListAsync(ct);

            var profileLookup = profilesByClass
                .GroupBy(p => p.ClassNameLower)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.DisplayOrder).ToList());

            // ---- Phase 1: bulk-COPY SensorEvent rows ----
            // Open the raw NpgsqlConnection. We deliberately bypass EF's
            // change tracker — 2M tracked entities is a memory disaster.
            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            var connectionWasClosed = conn.State != ConnectionState.Open;
            if (connectionWasClosed) await conn.OpenAsync(ct);

            int totalRows = 0;
            var rng = new Random(unchecked((int)0xDEC0DE17)); // deterministic for demo repeatability
            var ingestedAt = now;
            var unitFallback = (short)Uom.Percent; // safety default

            // Track latest value per (assetId, readingType) for AssetSensorLatest upsert.
            var latestPerKey = new Dictionary<(int assetId, SensorReadingType), (decimal value, short unit, DateTime readingAt, bool isOutOfSpec, string tone)>();

            try
            {
                using var importer = conn.BeginBinaryImport(
                    "COPY \"SensorEvents\" (" +
                    "\"AssetId\", \"AssetSensorChannelId\", \"ReadingType\", \"Value\", \"Unit\", " +
                    "\"ReadingAt\", \"IngestedAt\", \"Source\", \"SourceZone\", \"QualityCode\", " +
                    "\"OpcQualityByte\", \"IsOutOfSpec\", \"SchemaVersion\", \"CorrelationId\"" +
                    ") FROM STDIN (FORMAT BINARY)");

                foreach (var asset in assets)
                {
                    var classKey = asset.AssetType.ToLowerInvariant();
                    if (!profileLookup.TryGetValue(classKey, out var profiles)) continue;
                    if (profiles.Count == 0) continue;

                    bool isStoryline = StorylineModelKeys.Any(k =>
                        (asset.Description ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);

                    foreach (var profile in profiles)
                    {
                        var unitCode = TryParseUnit(profile.Unit, unitFallback);
                        var normalMid = (profile.NormalMin + profile.NormalMax) / 2m;
                        var normalRange = Math.Max(0.1m, profile.NormalMax - profile.NormalMin);
                        var noiseScale = normalRange * 0.05m;

                        // Standard 30-day backfill at 15-min resolution
                        var t = startUtc;
                        while (t < now)
                        {
                            var value = GenerateValue(
                                t, normalMid, normalRange, noiseScale, rng,
                                isStoryline: false, profileType: profile.ReadingType);

                            bool isOutOfSpec = IsOutOfSpec(value, profile.CriticalThreshold, profile.BreachOnHighSide);

                            importer.StartRow();
                            importer.Write(asset.Id, NpgsqlDbType.Integer);
                            importer.WriteNull();                                          // AssetSensorChannelId
                            importer.Write((int)profile.ReadingType, NpgsqlDbType.Integer);
                            importer.Write(value, NpgsqlDbType.Numeric);
                            importer.Write(unitCode, NpgsqlDbType.Smallint);
                            importer.Write(t, NpgsqlDbType.TimestampTz);
                            importer.Write(ingestedAt, NpgsqlDbType.TimestampTz);
                            importer.Write("backfill", NpgsqlDbType.Varchar);
                            importer.Write((short)PurdueZone.L3Operations, NpgsqlDbType.Smallint);
                            importer.Write((short)DeviceHealthCode.Good, NpgsqlDbType.Smallint);
                            importer.Write((short)0, NpgsqlDbType.Smallint);              // OpcQualityByte
                            importer.Write(isOutOfSpec, NpgsqlDbType.Boolean);
                            importer.Write((short)1, NpgsqlDbType.Smallint);              // SchemaVersion
                            importer.WriteNull();                                          // CorrelationId

                            totalRows++;

                            // Track latest
                            var key = (asset.Id, profile.ReadingType);
                            string tone = ClassifyTone(value, profile.WarningThreshold, profile.CriticalThreshold, profile.BreachOnHighSide);
                            latestPerKey[key] = (value, unitCode, t, isOutOfSpec, tone);

                            t = t.AddMinutes(ResolutionMinutes);
                        }

                        // Storyline assets: high-res overlay for the last 7 days, 1-minute
                        if (isStoryline)
                        {
                            var storyStart = now.AddDays(-StorylineDays);
                            var st = storyStart;
                            while (st < now)
                            {
                                var value = GenerateValue(
                                    st, normalMid, normalRange, noiseScale, rng,
                                    isStoryline: true, profileType: profile.ReadingType);

                                bool isOutOfSpec = IsOutOfSpec(value, profile.CriticalThreshold, profile.BreachOnHighSide);

                                importer.StartRow();
                                importer.Write(asset.Id, NpgsqlDbType.Integer);
                                importer.WriteNull();
                                importer.Write((int)profile.ReadingType, NpgsqlDbType.Integer);
                                importer.Write(value, NpgsqlDbType.Numeric);
                                importer.Write(unitCode, NpgsqlDbType.Smallint);
                                importer.Write(st, NpgsqlDbType.TimestampTz);
                                importer.Write(ingestedAt, NpgsqlDbType.TimestampTz);
                                importer.Write("backfill-storyline", NpgsqlDbType.Varchar);
                                importer.Write((short)PurdueZone.L3Operations, NpgsqlDbType.Smallint);
                                importer.Write((short)DeviceHealthCode.Good, NpgsqlDbType.Smallint);
                                importer.Write((short)0, NpgsqlDbType.Smallint);
                                importer.Write(isOutOfSpec, NpgsqlDbType.Boolean);
                                importer.Write((short)1, NpgsqlDbType.Smallint);
                                importer.WriteNull();

                                totalRows++;

                                var key = (asset.Id, profile.ReadingType);
                                string tone = ClassifyTone(value, profile.WarningThreshold, profile.CriticalThreshold, profile.BreachOnHighSide);
                                latestPerKey[key] = (value, unitCode, st, isOutOfSpec, tone);

                                st = st.AddMinutes(StorylineResolutionMinutes);
                            }
                        }
                    }
                }

                importer.Complete();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "TelemetryHistoricalBackfillSeeder: COPY failed after {Rows} rows; transaction rolled back.",
                    totalRows);
                throw;
            }
            finally
            {
                if (connectionWasClosed && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }

            _logger.LogInformation(
                "TelemetryHistoricalBackfillSeeder: copied {Rows} rows into SensorEvents.", totalRows);

            // ---- Phase 2: Update AssetSensorLatest with the latest value per (asset, type) ----
            // Reopen DbContext-managed connection; EF handles its own pooling.
            var now2 = DateTime.UtcNow;
            var existingLatest = await _db.AssetSensorLatest
                .Where(x => latestPerKey.Keys.Select(k => k.assetId).Contains(x.AssetId))
                .ToListAsync(ct);
            var existingByKey = existingLatest.ToDictionary(x => (x.AssetId, x.ReadingType));

            int latestRowsUpserted = 0;
            foreach (var (key, value) in latestPerKey)
            {
                if (existingByKey.TryGetValue(key, out var existing))
                {
                    if (value.readingAt >= existing.ReadingAt)
                    {
                        existing.Value = value.value;
                        existing.Unit = (Uom)value.unit;
                        existing.ReadingAt = value.readingAt;
                        existing.QualityCode = DeviceHealthCode.Good;
                        existing.IsOutOfSpec = value.isOutOfSpec;
                        existing.Tone = value.tone;
                        existing.UpdatedAt = now2;
                        latestRowsUpserted++;
                    }
                }
                else
                {
                    _db.AssetSensorLatest.Add(new AssetSensorLatest
                    {
                        AssetId = key.assetId,
                        ReadingType = key.Item2,
                        Value = value.value,
                        Unit = (Uom)value.unit,
                        ReadingAt = value.readingAt,
                        QualityCode = DeviceHealthCode.Good,
                        IsOutOfSpec = value.isOutOfSpec,
                        Tone = value.tone,
                        UpdatedAt = now2,
                    });
                    latestRowsUpserted++;
                }
            }
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "TelemetryHistoricalBackfillSeeder: upserted {Latest} AssetSensorLatest rows. Done.",
                latestRowsUpserted);

            return totalRows;
        }

        // ---- Value generation ----
        //
        // Generates one reading at time `at`, anchored to a daily-cycle
        // + weekend-dip + noise model. Storyline events add a linear
        // rising trend over the storyline window and force occasional
        // out-of-spec values.

        private decimal GenerateValue(
            DateTime at,
            decimal normalMid,
            decimal normalRange,
            decimal noiseScale,
            Random rng,
            bool isStoryline,
            SensorReadingType profileType)
        {
            // 24-hour sinusoid: peak at 14:00 local (assume UTC=local for demo).
            // Drives ±15% of normal range around the midpoint.
            double hour = at.Hour + at.Minute / 60.0;
            double dailyCycle = Math.Sin((hour - 8.0) * Math.PI / 12.0); // peak ~14:00
            decimal cycleDelta = (decimal)dailyCycle * (normalRange * 0.15m);

            // Weekend dip — Sat (DayOfWeek=6) + Sun (DayOfWeek=0)
            decimal weekendFactor = (at.DayOfWeek == DayOfWeek.Saturday || at.DayOfWeek == DayOfWeek.Sunday)
                ? (1m - (decimal)WeekendDipFactor)
                : 1m;

            // Gaussian noise via Box-Muller, scaled to noiseScale
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            double gauss = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            decimal noise = (decimal)gauss * noiseScale;

            decimal value = (normalMid + cycleDelta) * weekendFactor + noise;

            // Storyline overlay: linear rising trend over the storyline window
            if (isStoryline)
            {
                double daysFromNow = (DateTime.UtcNow - at).TotalDays;
                // 0 days ago → trend at max; StorylineDays ago → no trend.
                double trendProgress = Math.Max(0, 1.0 - (daysFromNow / StorylineDays));
                decimal trendDelta = (decimal)trendProgress * normalRange * 0.6m; // up to 60% of normal range
                value += trendDelta;
            }

            // Rare out-of-spec spikes for non-storyline data
            if (!isStoryline && rng.NextDouble() < OutOfSpecSpikeProbability)
            {
                value += normalRange * 1.5m;
            }

            return Math.Round(value, 4);
        }

        private static bool IsOutOfSpec(decimal value, decimal? criticalThreshold, bool breachOnHighSide)
        {
            if (!criticalThreshold.HasValue) return false;
            return breachOnHighSide
                ? value >= criticalThreshold.Value
                : value <= criticalThreshold.Value;
        }

        private static string ClassifyTone(decimal value, decimal? warning, decimal? critical, bool breachOnHighSide)
        {
            if (critical.HasValue)
            {
                bool crit = breachOnHighSide ? value >= critical.Value : value <= critical.Value;
                if (crit) return "crit";
            }
            if (warning.HasValue)
            {
                bool warn = breachOnHighSide ? value >= warning.Value : value <= warning.Value;
                if (warn) return "warn";
            }
            return "ok";
        }

        // Best-effort mapping from SensorProfile.Unit string ("°C", "mm/s",
        // "PSI", "bar", "RPM", ...) to UnitOfMeasure enum code. Falls
        // back to Percent (900) if unknown. PR #118.4's UnitConversion
        // table seeding (deferred to PR #118.6) will make this
        // authoritative.
        private static short TryParseUnit(string unit, short fallback)
        {
            return (unit?.Trim().ToLowerInvariant()) switch
            {
                "°c" or "c" or "celsius" => (short)Uom.DegreesCelsius,
                "°f" or "f" or "fahrenheit" => (short)Uom.DegreesFahrenheit,
                "psi" => (short)Uom.PSI,
                "bar" => (short)Uom.Bar,
                "kpa" => (short)Uom.KiloPascal,
                "rpm" => (short)Uom.RPM,
                "mm/s" => (short)Uom.MillimetersPerSecond,
                "in/s" => (short)Uom.InchesPerSecond,
                "g" => (short)Uom.GravityForce,
                "gpm" => (short)Uom.GallonsPerMinute,
                "l/min" or "lpm" => (short)Uom.LitersPerMinute,
                "v" or "volts" => (short)Uom.Volts,
                "a" or "amps" or "amperes" => (short)Uom.Amperes,
                "kw" => (short)Uom.KiloWatts,
                "kwh" => (short)Uom.KiloWattHours,
                "hz" => (short)Uom.HertzAC,
                "%" or "percent" => (short)Uom.Percent,
                _ => fallback,
            };
        }
    }
}
