using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Catalog;
using Abs.FixedAssets.Models.Telemetry;
using Abs.FixedAssets.Services;
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
        private readonly ITenantContext _tenant;
        private readonly ILogger<SensorIngestService> _logger;

        public SensorIngestService(
            AppDbContext db,
            ITenantContext tenant,
            ILogger<SensorIngestService> logger)
        {
            _db = db;
            _tenant = tenant;
            _logger = logger;
        }

        // PR #118.4 — Sentinel thrown when the caller posts an event for
        // an Asset outside their tenant scope. Surfaced as 403 Forbidden
        // by the SensorEventsController. App-layer enforcement until
        // Postgres RLS lands in PR #122; that's defense-in-depth.
        public sealed class TenantScopeViolationException : InvalidOperationException
        {
            public TenantScopeViolationException(string msg) : base(msg) { }
        }

        // Returns the set of AssetIds the caller may legally write to.
        // Empty caller scope means a public/anonymous request to an
        // open API; we don't trust the IngestedAt path with that, so
        // an empty VisibleCompanyIds list rejects every event.
        private async Task<HashSet<int>> VisibleAssetIdsAsync(
            IReadOnlyCollection<int> assetIds, CancellationToken ct)
        {
            if (_tenant.VisibleCompanyIds.Count == 0) return new HashSet<int>();
            var visible = await _db.Assets
                .AsNoTracking()
                .Where(a => assetIds.Contains(a.Id))
                .Where(a => _tenant.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .Select(a => a.Id)
                .ToListAsync(ct);
            return new HashSet<int>(visible);
        }

        public async Task IngestAsync(SensorEvent evt, CancellationToken ct = default)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            // PR #118.4 — Tenant scope check FIRST. Reject before any write.
            var visible = await VisibleAssetIdsAsync(new[] { evt.AssetId }, ct);
            if (!visible.Contains(evt.AssetId))
                throw new TenantScopeViolationException(
                    $"AssetId {evt.AssetId} is not in the caller's tenant scope.");

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

            // PR #118.4 — Tenant scope filter. Resolve once for all
            // distinct AssetIds in the batch; throw if ANY event refers
            // to an asset outside the caller's tenant scope. We don't
            // silently drop because a partial-success batch is hard to
            // reason about for gateway clients.
            var batchAssetIds = list.Select(e => e.AssetId).Distinct().ToList();
            var visibleSet = await VisibleAssetIdsAsync(batchAssetIds, ct);
            var unauthorized = batchAssetIds.Where(id => !visibleSet.Contains(id)).ToList();
            if (unauthorized.Count > 0)
                throw new TenantScopeViolationException(
                    $"AssetIds [{string.Join(", ", unauthorized)}] are not in the caller's tenant scope.");

            // PR #118.3 — optimized batch path. Single SaveChanges + at
            // most 4 round-trips to Postgres regardless of batch size.
            // Replaces the prior implementation that looped the single-
            // event path (5K events → 25 SECONDS at ~5ms per SaveChanges).
            //
            // Strategy:
            //   1. Server-stamp IngestedAt on every event.
            //   2. Pre-resolve EquipmentClass.Name (lowercased) for every
            //      AssetId in the batch — one query.
            //   3. Pre-fetch SensorProfile thresholds for every distinct
            //      (classNameLower, ReadingType) in the batch — one query.
            //   4. Classify IsOutOfSpec on every event in-memory using
            //      the resolved profile map. AddRange into SensorEvents.
            //   5. For AssetSensorLatest: find the latest event per
            //      (assetId, readingType) in this batch, pre-fetch any
            //      existing rows in one query, conditionally insert or
            //      update — picking Tone via the same profile map.
            //   6. Single SaveChanges.

            var now = DateTime.UtcNow;
            foreach (var e in list) e.IngestedAt = now;

            // --- Step 2: AssetId → AssetType (lowercased) ---
            var assetIds = list.Select(e => e.AssetId).Distinct().ToList();
            var assetClassByAssetId = await _db.Assets
                .AsNoTracking()
                .Where(a => assetIds.Contains(a.Id))
                .Select(a => new { a.Id, AssetType = a.AssetType ?? "" })
                .ToDictionaryAsync(
                    a => a.Id,
                    a => a.AssetType.ToLowerInvariant(),
                    ct);

            // --- Step 3: SensorProfile thresholds per (class, type) ---
            var distinctClassNames = assetClassByAssetId.Values
                .Where(c => c.Length > 0)
                .Distinct()
                .ToList();
            var readingTypes = list.Select(e => e.ReadingType).Distinct().ToList();

            var rawProfiles = distinctClassNames.Count == 0
                ? new List<RawProfile>()
                : await _db.SensorProfiles
                    .AsNoTracking()
                    .Where(p => readingTypes.Contains(p.ReadingType))
                    .Where(p => p.EquipmentClass != null
                             && distinctClassNames.Contains(p.EquipmentClass.Name.ToLower()))
                    .OrderBy(p => p.DisplayOrder)
                    .Select(p => new RawProfile
                    {
                        ClassNameLower = p.EquipmentClass!.Name.ToLower(),
                        ReadingType = p.ReadingType,
                        DisplayOrder = p.DisplayOrder,
                        WarningThreshold = p.WarningThreshold,
                        CriticalThreshold = p.CriticalThreshold,
                        BreachOnHighSide = p.BreachOnHighSide,
                    })
                    .ToListAsync(ct);

            // PR #118.3.1 — a single EquipmentClass can have multiple
            // SensorProfile rows sharing a ReadingType (e.g., a CNC has
            // Spindle Temp, Coolant Temp, and Motor Temp, all
            // ReadingType=Temperature). Take the first by DisplayOrder
            // — this matches the read-side pattern in Plant/Floor where
            // the primary tile is the ordered-first profile. The proper
            // long-term fix is per-channel disambiguation via
            // SensorEvent.AssetSensorChannelId FK (PR #128 gateway work).
            var profileMap = rawProfiles
                .GroupBy(p => (p.ClassNameLower, p.ReadingType))
                .ToDictionary(g => g.Key, g => g.First());

            // --- Step 4: Classify IsOutOfSpec on every event in memory ---
            foreach (var e in list)
            {
                e.IsOutOfSpec = false;
                var cls = assetClassByAssetId.GetValueOrDefault(e.AssetId);
                if (string.IsNullOrEmpty(cls)) continue;
                if (!profileMap.TryGetValue((cls, e.ReadingType), out var p)) continue;
                if (!p.CriticalThreshold.HasValue) continue;
                e.IsOutOfSpec = p.BreachOnHighSide
                    ? e.Value >= p.CriticalThreshold.Value
                    : e.Value <= p.CriticalThreshold.Value;
            }

            // Stage all new SensorEvent rows in one AddRange.
            _db.SensorEvents.AddRange(list);

            // --- Step 5: AssetSensorLatest upserts ---
            //
            // Resolve the latest event in the batch per (asset, type).
            // Pre-fetch the existing AssetSensorLatest rows for any
            // (asset, type) keys present in the batch — single query.
            var latestPerKey = list
                .GroupBy(e => (e.AssetId, e.ReadingType))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(e => e.ReadingAt).First());

            var batchKeyAssetIds = latestPerKey.Keys.Select(k => k.AssetId).Distinct().ToList();
            var batchKeyTypes = latestPerKey.Keys.Select(k => k.ReadingType).Distinct().ToList();

            // Load TRACKED rows so we can mutate them and SaveChanges picks them up.
            var existingRows = await _db.AssetSensorLatest
                .Where(x => batchKeyAssetIds.Contains(x.AssetId) && batchKeyTypes.Contains(x.ReadingType))
                .ToListAsync(ct);
            var existingByKey = existingRows.ToDictionary(
                r => (r.AssetId, r.ReadingType),
                r => r);

            foreach (var ((assetId, readingType), latest) in latestPerKey)
            {
                // Tone classification using the same pre-resolved profile map.
                var tone = ClassifyTone(
                    assetClassByAssetId.GetValueOrDefault(assetId),
                    readingType,
                    latest.Value,
                    profileMap);

                if (existingByKey.TryGetValue((assetId, readingType), out var existing))
                {
                    // Only regress if new reading is at-or-after the cached one
                    // (matches single-event path semantics for out-of-order replay).
                    if (latest.ReadingAt >= existing.ReadingAt)
                    {
                        existing.Value = latest.Value;
                        existing.Unit = latest.Unit;
                        existing.ReadingAt = latest.ReadingAt;
                        existing.QualityCode = latest.QualityCode;
                        existing.IsOutOfSpec = latest.IsOutOfSpec;
                        existing.Tone = tone;
                        existing.UpdatedAt = now;
                    }
                }
                else
                {
                    _db.AssetSensorLatest.Add(new AssetSensorLatest
                    {
                        AssetId = assetId,
                        ReadingType = readingType,
                        Value = latest.Value,
                        Unit = latest.Unit,
                        ReadingAt = latest.ReadingAt,
                        QualityCode = latest.QualityCode,
                        IsOutOfSpec = latest.IsOutOfSpec,
                        Tone = tone,
                        UpdatedAt = now,
                    });
                }
            }

            // --- Step 6: Single SaveChanges for the entire batch ---
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "SensorIngestService: batch ingested {Count} events in 4 queries + 1 SaveChanges.",
                list.Count);
            return list.Count;
        }

        private sealed class RawProfile
        {
            public string ClassNameLower { get; set; } = "";
            public SensorReadingType ReadingType { get; set; }
            public int DisplayOrder { get; set; }
            public decimal? WarningThreshold { get; set; }
            public decimal? CriticalThreshold { get; set; }
            public bool BreachOnHighSide { get; set; }
        }

        private static string ClassifyTone(
            string? classNameLower,
            SensorReadingType readingType,
            decimal value,
            Dictionary<(string, SensorReadingType), RawProfile> profileMap)
        {
            if (string.IsNullOrEmpty(classNameLower)) return "muted";
            if (!profileMap.TryGetValue((classNameLower, readingType), out var p)) return "muted";

            if (p.CriticalThreshold.HasValue)
            {
                bool crit = p.BreachOnHighSide
                    ? value >= p.CriticalThreshold.Value
                    : value <= p.CriticalThreshold.Value;
                if (crit) return "crit";
            }
            if (p.WarningThreshold.HasValue)
            {
                bool warn = p.BreachOnHighSide
                    ? value >= p.WarningThreshold.Value
                    : value <= p.WarningThreshold.Value;
                if (warn) return "warn";
            }
            return "ok";
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
                .OrderBy(p => p.DisplayOrder)
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
