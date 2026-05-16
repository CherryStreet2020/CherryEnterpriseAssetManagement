using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Catalog;
using Abs.FixedAssets.Models.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Telemetry
{
    // Sprint 2 PR #118.2 — Sensor ingest service (minimal core).
    //
    // Single write path for every SensorEvent. The ADR specifies six
    // side-effects in one transaction; this PR ships the first three
    // (insert + upsert + IsOutOfSpec compute + Tone classification).
    //
    // Deferred to follow-up PRs in the #118.x stack:
    //   - SensorAlarm state machine drive (PR #118.3 — needs
    //     SensorAlarmService + AlarmRationalization lookup)
    //   - Domain-event outbox enqueue (PR #118.3)
    //   - Post-commit refresh trigger for app-layer rollup MVs (PR #118.4)
    //
    // IsOutOfSpec + Tone classification mirrors the existing
    // Plant/Floor.cshtml.cs::ClassifyTone logic so behavior is consistent
    // between the legacy AssetSensorReadings read path and the new
    // SensorEvent + AssetSensorLatest write path during the dual-write
    // window (PR #118.2 → PR #118.4).
    //
    // Case-insensitive Asset.AssetType ↔ EquipmentClass.Name lookup is
    // intentional — AppDbContext.CapitalizeStringProperties uppercases
    // AssetType on save but skips Name, so a literal equality check
    // would miss. Lesson from the Lincoln Power Wave S350 saga
    // (PR #117.5 → #117.7).
    public class SensorIngestService : ISensorIngestService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<SensorIngestService> _logger;

        public SensorIngestService(AppDbContext db, ILogger<SensorIngestService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task IngestAsync(SensorEvent evt, CancellationToken ct = default)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            // Server-stamped IngestedAt always wins; clients can't backdate.
            evt.IngestedAt = DateTime.UtcNow;

            // Classify against SensorProfile thresholds.
            var (isOutOfSpec, tone) = await ClassifyAsync(evt.AssetId, evt.ReadingType, evt.Value, ct);
            evt.IsOutOfSpec = isOutOfSpec;

            // 1) Insert into the hypertable.
            _db.SensorEvents.Add(evt);

            // 2) Upsert AssetSensorLatest. Composite PK is (AssetId, ReadingType).
            var existing = await _db.AssetSensorLatest
                .FirstOrDefaultAsync(
                    x => x.AssetId == evt.AssetId && x.ReadingType == evt.ReadingType,
                    ct);

            if (existing == null)
            {
                _db.AssetSensorLatest.Add(new AssetSensorLatest
                {
                    AssetId = evt.AssetId,
                    ReadingType = evt.ReadingType,
                    Value = evt.Value,
                    Unit = evt.Unit,
                    ReadingAt = evt.ReadingAt,
                    QualityCode = evt.QualityCode,
                    IsOutOfSpec = isOutOfSpec,
                    Tone = tone,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else if (evt.ReadingAt >= existing.ReadingAt)
            {
                // Only update the cache when the new reading is at-or-after
                // the cached one. Out-of-order replay from edge gateways
                // should not regress the latest-state view.
                existing.Value = evt.Value;
                existing.Unit = evt.Unit;
                existing.ReadingAt = evt.ReadingAt;
                existing.QualityCode = evt.QualityCode;
                existing.IsOutOfSpec = isOutOfSpec;
                existing.Tone = tone;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task<int> IngestBatchAsync(
            IEnumerable<SensorEvent> events,
            CancellationToken ct = default)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            var list = events.ToList();
            if (list.Count == 0) return 0;

            // Minimal-viable batch: loop the single-event path. PR #118.3
            // will optimize this into a per-(asset, type) pre-resolve of
            // SensorProfiles + a single SaveChanges at end. For the
            // demo seeder volumes this is fine.
            int n = 0;
            foreach (var evt in list)
            {
                await IngestAsync(evt, ct);
                n++;
            }
            _logger.LogInformation(
                "SensorIngestService: batched {Count} events.", n);
            return n;
        }

        // ---- Classification ----
        //
        // Returns (IsOutOfSpec, Tone) for a reading against the asset's
        // EquipmentClass SensorProfile thresholds. Mirrors the read-side
        // ClassifyTone in Pages/Plant/Floor.cshtml.cs so the dual-write
        // window produces consistent values between old AssetSensorReadings
        // and new SensorEvent / AssetSensorLatest.
        //
        // When the asset has no resolvable class or no profile for this
        // reading type → ("muted", false). The Plant Floor tile renders
        // muted in that case rather than guessing.

        private async Task<(bool isOutOfSpec, string tone)> ClassifyAsync(
            int assetId,
            SensorReadingType readingType,
            decimal value,
            CancellationToken ct)
        {
            var asset = await _db.Assets
                .AsNoTracking()
                .Where(a => a.Id == assetId)
                .Select(a => new { a.AssetType })
                .FirstOrDefaultAsync(ct);

            if (asset == null || string.IsNullOrEmpty(asset.AssetType))
                return (false, "muted");

            // Case-insensitive lookup — AssetType is auto-uppercased on
            // save, EquipmentClass.Name is preserved. See class comment.
            var profile = await _db.SensorProfiles
                .AsNoTracking()
                .Where(p => p.ReadingType == readingType)
                .Where(p => p.EquipmentClass != null
                         && p.EquipmentClass.Name.ToLower() == asset.AssetType.ToLower())
                .Select(p => new
                {
                    p.WarningThreshold,
                    p.CriticalThreshold,
                    p.BreachOnHighSide,
                })
                .FirstOrDefaultAsync(ct);

            if (profile == null)
                return (false, "muted");

            // Critical first — if crit, IsOutOfSpec=true (AssetHealthService
            // counts these for the sensor-breach penalty).
            if (profile.CriticalThreshold.HasValue)
            {
                bool crit = profile.BreachOnHighSide
                    ? value >= profile.CriticalThreshold.Value
                    : value <= profile.CriticalThreshold.Value;
                if (crit) return (true, "crit");
            }

            if (profile.WarningThreshold.HasValue)
            {
                bool warn = profile.BreachOnHighSide
                    ? value >= profile.WarningThreshold.Value
                    : value <= profile.WarningThreshold.Value;
                if (warn) return (false, "warn");
            }

            return (false, "ok");
        }
    }
}
