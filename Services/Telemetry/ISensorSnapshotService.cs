using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Telemetry;

namespace Abs.FixedAssets.Services.Telemetry
{
    // Sprint 2 PR #118.1 — INTERFACE ONLY. Implementation lands in
    // PR #118.6 (SensorSnapshotService + /Admin/Snapshots).
    //
    // Captures and retrieves immutable point-in-time records of asset
    // sensor state. Snapshots are append-only (FDA 21 CFR Part 11 +
    // SOX §404). The implementation:
    //   - reads AssetSensorLatest values for the requested asset(s)
    //   - inserts one SensorSnapshot + N SensorSnapshotValue rows
    //   - computes the SHA-256 signature over the canonical
    //     serialization at insert time
    //   - never mutates an existing snapshot row
    //
    // Captures happen four ways:
    //   1. Scheduled — pg_cron or Hangfire fires CaptureForSiteAsync
    //      with Reason=Scheduled on a schedule (shift-change default)
    //   2. Deviation — the ingest service calls CaptureDeviationAsync
    //      when SensorEvent.IsOutOfSpec flips true
    //   3. ShiftChange — operator hits "End shift" on /Plant/Floor
    //   4. UserRequest — explicit POST /api/v1/sensors/snapshot
    public interface ISensorSnapshotService
    {
        Task<SensorSnapshot> CaptureForAssetAsync(
            int assetId,
            SnapshotReason reason,
            int? capturedByUserId,
            string? notes,
            long? triggerEventId = null,
            CancellationToken ct = default);

        Task<SensorSnapshot> CaptureForSiteAsync(
            int siteId,
            SnapshotReason reason,
            int? capturedByUserId,
            string? notes,
            CancellationToken ct = default);

        Task<SensorSnapshot?> GetAsync(
            long snapshotId,
            CancellationToken ct = default);

        Task<IReadOnlyList<SensorSnapshot>> ListAsync(
            int? siteId,
            SnapshotReason? reason,
            System.DateTime? fromUtc,
            System.DateTime? toUtc,
            int limit = 100,
            CancellationToken ct = default);

        // Re-hashes the snapshot's contents and returns true iff the
        // computed hash matches SignatureHash. The Part 11 tamper
        // detection check.
        Task<bool> VerifySignatureAsync(
            long snapshotId,
            CancellationToken ct = default);
    }
}
