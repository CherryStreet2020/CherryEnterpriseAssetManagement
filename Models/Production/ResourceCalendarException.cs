// Theme B11 Wave R2-6 (2026-05-29) — ResourceCalendarException.
//
// The per-resource availability DELTA the finite scheduler (R4) applies on top
// of the base WorkCalendar. A WorkCalendar gives the standard week (Mon-Fri
// 8-5, holidays). But a single machine is down for a planned PM Tuesday 2-6pm;
// the heat-treat furnace runs an extra Saturday shift to clear a backlog; the
// CMM is at reduced throughput while a probe is recalibrated. Those are
// RESOURCE-specific, not calendar-wide — so they live here, one row per window.
//
// THE DISRUPTIVE TIE-IN: a MaintenanceWindow exception can bridge to the EAM
// maintenance `WorkOrder` that causes the downtime (SourceWorkOrderId, SET
// NULL). Because production and maintenance share the same resource, the
// scheduler KNOWS the machine is unavailable Tuesday 2-6pm without anyone
// re-keying it — the incumbents (Epicor / Plex / SAP PM) keep these in separate
// modules that never talk. Here it's one graph.
//
// We deliberately do NOT add a Shift table (decision #4 — deferred). A repeating
// extra shift is modelled as ExtraShift exception rows; if that proves too
// chatty we revisit. Per-resource calendar OVERRIDE (a wholly different week) is
// handled by ProductionResource.CalendarId → WorkCalendar (wired in R2-6);
// exceptions are the fine-grained deltas on whichever calendar applies.
//
// New TABLE ⇒ default-value initializers are safe with no HasDefaultValue.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

/// <summary>What kind of availability delta this window represents.</summary>
public enum ResourceCalendarExceptionType
{
    /// <summary>Resource unavailable for an unplanned/general reason.</summary>
    Downtime = 0,
    /// <summary>Planned maintenance window — may bridge to an EAM WorkOrder.</summary>
    MaintenanceWindow = 1,
    /// <summary>Extra availability beyond the base calendar (overtime / weekend shift).</summary>
    ExtraShift = 2,
    /// <summary>Resource-specific non-working day (machine holiday / shutdown).</summary>
    Holiday = 3,
    /// <summary>Available but at reduced throughput (see CapacityOverridePct).</summary>
    ReducedCapacity = 4,
}

/// <summary>
/// A single availability delta on a <see cref="ProductionResource"/> for a time
/// window. The R4 finite scheduler subtracts Downtime/Maintenance/Holiday,
/// adds ExtraShift, and scales ReducedCapacity windows when projecting load.
/// </summary>
[Table("ResourceCalendarExceptions")]
public class ResourceCalendarException
{
    public int Id { get; set; }

    // ── Tenant trio ─────────────────────────────────────────────
    public int? TenantId { get; set; }
    [Required] public int CompanyId { get; set; }
    public int? SiteId { get; set; }

    // ── Owning resource ─────────────────────────────────────────
    /// <summary>The resource this window applies to. CASCADE — exceptions die with the resource.</summary>
    [Required] public int ProductionResourceId { get; set; }
    public ProductionResource? ProductionResource { get; set; }

    // ── Window ──────────────────────────────────────────────────
    public ResourceCalendarExceptionType ExceptionType { get; set; } = ResourceCalendarExceptionType.Downtime;

    /// <summary>Window start (UTC, inclusive).</summary>
    [Required] public DateTime StartUtc { get; set; }
    /// <summary>Window end (UTC, exclusive).</summary>
    [Required] public DateTime EndUtc { get; set; }

    /// <summary>
    /// For <see cref="ResourceCalendarExceptionType.ReducedCapacity"/>: the throughput
    /// available during the window (0–100). Ignored for other types.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? CapacityOverridePct { get; set; }

    // ── EAM maintenance bridge (optional) ───────────────────────
    /// <summary>
    /// For a MaintenanceWindow caused by a planned EAM job: the maintenance
    /// <see cref="Abs.FixedAssets.Models.WorkOrder"/> driving the downtime. SET NULL —
    /// the exception window stays even if the WO is archived. This is the
    /// production↔maintenance graph link the incumbents lack.
    /// </summary>
    public int? SourceWorkOrderId { get; set; }
    public Abs.FixedAssets.Models.WorkOrder? SourceWorkOrder { get; set; }

    [MaxLength(300)]
    public string? Reason { get; set; }

    // ── Audit + concurrency ─────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)] public string? ModifiedBy { get; set; }

    public byte[]? RowVersion { get; set; }
}
