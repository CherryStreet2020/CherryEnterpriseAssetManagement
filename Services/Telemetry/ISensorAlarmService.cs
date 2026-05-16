using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Telemetry;

namespace Abs.FixedAssets.Services.Telemetry
{
    // Sprint 2 PR #118.1 — INTERFACE ONLY. Implementation lands in
    // PR #118.2 (SensorAlarmService).
    //
    // Owns the SensorAlarm state machine and the operator-facing
    // alarm operations (Acknowledge, Shelve, Clear, Suppress by mode).
    //
    // ISA-18.2 rules enforced in the implementation:
    //   - Shelving REQUIRES a non-null ShelvedUntil with a maximum
    //     window (default 7 days, per-tenant configurable).
    //   - Suppression by mode applies to alarms below a per-mode
    //     priority threshold; P1_Emergency is NEVER suppressible.
    //   - Acknowledgement is per-alarm by default; sets
    //     AcknowledgedByUserId for attribution.
    //   - Clear can be automatic (IsOutOfSpec flips false on a new
    //     SensorEvent) or manual (operator-driven with required
    //     reason text).
    public interface ISensorAlarmService
    {
        Task<SensorAlarm> AcknowledgeAsync(
            long alarmId,
            int userId,
            string? note,
            CancellationToken ct = default);

        Task<SensorAlarm> ShelveAsync(
            long alarmId,
            int userId,
            System.DateTime shelvedUntilUtc,
            string reason,
            CancellationToken ct = default);

        Task<SensorAlarm> ClearAsync(
            long alarmId,
            int? userId,
            string? reason,
            long? clearingEventId,
            CancellationToken ct = default);

        Task<int> SuppressByModeAsync(
            int? siteId,
            AlarmSuppressionMode mode,
            AlarmPriority maxSuppressedPriority,
            CancellationToken ct = default);

        Task<IReadOnlyList<SensorAlarm>> GetOpenForSiteAsync(
            int siteId,
            CancellationToken ct = default);
    }
}
