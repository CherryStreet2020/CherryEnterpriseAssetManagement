// Theme B11 Wave R4-11 (2026-05-29) — Finite scheduler (the real engine).
//
// Supersedes the Sprint-12.8 BackwardSchedulingService STUB, which did pure
// 24-hour wall-clock arithmetic ("a 480-min op subtracts exactly 8 hours, even if
// that lands at 02:00 Saturday") with no calendar and no capacity. This is the
// real thing the whole theme was built toward — it stands on the R1-R4 substrate:
//
//   • CALENDAR-AWARE — operations are floored into real working windows via the
//     R4-10 WorkingTimeEngine (each op's Work Center calendar: week mask + work-day
//     window + IANA TZ + holidays). No more midnight-Saturday placements.
//   • FINITE-CAPACITY — before an op is placed, the engine counts competing demand
//     (other production orders' committed ops + this run's own placements) against
//     the Work Center's SimultaneousOperationsMax. An InfiniteCapacity WC never
//     constrains; a SingleResource WC holds one op at a time.
//   • CAPABILITY-BASED ALTERNATES — when the primary WC is loaded, the engine asks
//     R3-9 ICapabilityMatchService.GetEligibleResourcesAsync WHO ELSE can run the
//     op and re-homes it to the best eligible resource whose WC has room; failing
//     that, it spills to the ordered WorkCenterAlternate list. This is the
//     disruption: the routing says WHAT the op needs, the scheduler picks WHERE by
//     real capability + capacity, instead of a machine hard-pinned on the routing.
//   • BACKWARD + FORWARD + WHAT-IF — backward from the order's ScheduledEnd (due
//     date) or forward from its ScheduledStart; commit:false projects without
//     persisting (the make-or-buy / promise-date probe path).
//
// v1 scope (locked, §6): finite + load + alternate. It does NOT yet re-time ops to
// resolve contention (no time-shift leveling) — an op that can't find a free
// alternate is placed on its primary WC and flagged OnOverloadedResource so the
// signal is visible. Time-shift leveling is a documented follow-up. Tenant scope:
// the parent order's company.

using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Production.Scheduling
{
    /// <summary>Schedule from the due date backward, or from a start date forward.</summary>
    public enum ScheduleDirection { Backward = 0, Forward = 1 }

    /// <summary>Where and when one operation landed, plus how the engine decided.</summary>
    public sealed record OperationPlacement(
        int ProductionOperationId,
        int ProductionOrderId,
        int SequenceNumber,
        string Description,
        int WorkCenterId,
        string WorkCenterCode,
        int? AssetId,
        DateTime PlannedStartUtc,
        DateTime PlannedEndUtc,
        bool MovedToAlternate,
        int? OriginalWorkCenterId,
        string? AlternateReason,
        bool OnOverloadedResource,
        bool WorkingTimeRanOut);

    /// <summary>Full result of one schedule run.</summary>
    public sealed record FiniteScheduleResult(
        int ParentProductionOrderId,
        ScheduleDirection Direction,
        bool Committed,
        DateTime AnchorUtc,
        IReadOnlyList<int> ChildProductionOrderIds,
        IReadOnlyList<OperationPlacement> Placements,
        int OperationsPlaced,
        int OperationsMovedToAlternate,
        int OperationsOnOverloaded,
        int TotalSpannedDays,
        IReadOnlyList<string> Warnings);

    public interface IFiniteSchedulingService
    {
        /// <summary>
        /// Calendar- and capacity-aware schedule of a parent production order's children +
        /// their operations. Backward anchors on the parent's ScheduledEnd; forward on its
        /// ScheduledStart (or now). When <paramref name="commit"/> is false the result is a
        /// what-if projection and nothing is persisted.
        /// </summary>
        Task<Result<FiniteScheduleResult>> ScheduleAsync(
            int parentProductionOrderId,
            ScheduleDirection direction,
            bool commit,
            CancellationToken ct = default);
    }
}
