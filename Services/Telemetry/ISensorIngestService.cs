using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Telemetry;

namespace Abs.FixedAssets.Services.Telemetry
{
    // Sprint 2 PR #118.1 — INTERFACE ONLY. Implementation lands in
    // PR #118.2 (SensorIngestService).
    //
    // The single write path for sensor data. Every reading lands here:
    //   1. INSERT into SensorEvent (hypertable)
    //   2. UPSERT AssetSensorLatest (denormalized read cache)
    //   3. Evaluate SensorProfile thresholds → IsOutOfSpec, Tone
    //   4. Drive SensorAlarm state machine (open / peak-update / clear,
    //      honoring shelving + suppression)
    //   5. Stamp IngestedAt server-side (ignore client-supplied value)
    //   6. (post-commit, async) enqueue domain event for webhook outbox
    //
    // All four side-effects in one EF transaction. Continuous aggregates
    // refresh asynchronously per the policy in the migration.
    //
    // Sources expected to call this:
    //   - REST: POST /api/v1/sensors/events (REST gateway)
    //   - REST: POST /api/v1/sensors/events bulk (Sparkplug B gateway,
    //     OPC UA gateway, manual operator entry, demo seeder)
    //
    // RecordReadingAsync (the single-event path on the legacy
    // IAssetSensorService) is no longer the entry point — call
    // IngestAsync instead. The legacy interface stays alive during
    // the dual-write window (PR #118.2 → PR #118.4).
    public interface ISensorIngestService
    {
        Task IngestAsync(SensorEvent evt, CancellationToken ct = default);

        Task<int> IngestBatchAsync(
            IEnumerable<SensorEvent> events,
            CancellationToken ct = default);
    }
}
