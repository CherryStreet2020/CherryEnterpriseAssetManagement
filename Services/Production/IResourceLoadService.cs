// Theme B11 Wave R4-10 (2026-05-29) — Resource load profile + calendar engine.
//
// THE PAYOFF of the resource + calendar model. R2-6 gave us WHEN a resource is
// available (WorkCalendar week + Holidays + per-resource ResourceCalendarException
// deltas + the finite envelope). The released ProductionOperations tell us WHAT is
// committed (PlannedSetup/Run mins on PlannedStart..PlannedEnd, on a WorkCenter /
// Asset). This service joins them into the number everything downstream has been
// waiting for: a REAL projected Load% = committed scheduled hours ÷ available
// working hours over a window — per Work Center and per resource — replacing B7's
// coarse make-or-buy proxy with a calendar- and capacity-aware figure.
//
// It also finds the DRUM (the constraint): across a plant's work centers (or
// resources) over a window, the highest projected load is the bottleneck the
// schedule must be built around (Goldratt's drum). R4-11's finite scheduler and
// B7 Wave C's make-or-buy F2 capacity factor both consume this.
//
// THE CALENDAR ENGINE (the reusable core): given a WorkCalendar (week mask +
// work-day window + IANA time zone), its Holidays, and — for a resource — its
// ResourceCalendarException windows, compute the set of WORKING intervals (UTC)
// inside [from,to], then the available hours. Downtime / Maintenance / resource-
// Holiday windows are subtracted; ExtraShift windows are added (even outside
// normal hours); ReducedCapacity windows scale their overlap down by the override
// percentage. UtilizationPct trims the shift to expected-available; an optional
// AvailableHoursPerDay cap floors it further. R4-11 reuses this engine to floor
// op start/finish times to working windows.
//
// Tenant scope: every target resolves its own CompanyId and is guarded against
// ITenantContext.VisibleCompanyIds. ProductionOperation carries CompanyIdSnapshot.

using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Production
{
    /// <summary>What kind of capacity target a load profile describes.</summary>
    public enum ResourceLoadTargetKind
    {
        /// <summary>A Work Center — load aggregated across every op routed to it.</summary>
        WorkCenter = 0,
        /// <summary>A single ProductionResource (machine bridged to an Asset).</summary>
        Resource = 1,
    }

    /// <summary>
    /// Projected load for one capacity target over a window. <see cref="LoadPct"/> =
    /// committed busy hours ÷ available working hours × 100 (clamped to ≥0; can exceed
    /// 100 when over-committed — that is the signal, not an error).
    /// </summary>
    public sealed record ResourceLoadProfile(
        ResourceLoadTargetKind Kind,
        int TargetId,
        string Code,
        string Name,
        DateTime FromUtc,
        DateTime ToUtc,
        decimal AvailableHours,
        decimal CommittedHours,
        decimal LoadPct,
        int CommittedOperationCount,
        string? CalendarCode,
        string? Notes);

    /// <summary>
    /// Plant-wide load: every target's profile ranked by load descending, with the
    /// drum (highest-loaded target that carries committed work) called out.
    /// </summary>
    public sealed record PlantLoadProfile(
        int CompanyId,
        ResourceLoadTargetKind Kind,
        DateTime FromUtc,
        DateTime ToUtc,
        IReadOnlyList<ResourceLoadProfile> Profiles,
        int? DrumTargetId,
        string? DrumCode,
        decimal DrumLoadPct);

    public interface IResourceLoadService
    {
        /// <summary>
        /// Projected load for a single Work Center or resource over [from,to]. Resolves
        /// the target's calendar (resource override → WC calendar → company default),
        /// computes available working hours via the calendar engine, sums committed busy
        /// hours from released operations overlapping the window, and returns the Load%.
        /// </summary>
        Task<Result<ResourceLoadProfile>> GetProjectedLoadAsync(
            ResourceLoadTargetKind kind, int targetId,
            DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

        /// <summary>
        /// Projected load for every active Work Center (or resource) in a company over
        /// [from,to], ranked by load descending, with the drum (the constraint) flagged.
        /// </summary>
        Task<Result<PlantLoadProfile>> GetPlantLoadAsync(
            int companyId, ResourceLoadTargetKind kind,
            DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    }
}
