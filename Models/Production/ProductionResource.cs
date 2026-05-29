// Theme B11 Wave R2-4 (2026-05-29) — ProductionResource.
//
// THE schedulable production-resource identity. Per the §0 "three objects,
// three jobs" rule: Department organizes people; Work Center is the
// scheduling/capacity object; Machine/Resource EXECUTES — and a machine is
// ALSO an EAM Asset + telemetry source.
//
// DUAL IDENTITY, NEVER DUPLICATED (decision #3 — a dedicated table, not an
// extension of WorkCenterAssetLink): a ProductionResource carries the
// scheduling/dispatch/cost identity and BRIDGES to the EAM `Asset` via AssetId
// for machine kinds. Labor / Tool / Vendor / Location / Cell resources have no
// Asset (AssetId null) — their concrete source FKs (Employee / Vendor / Tool)
// are wired in R2-5. A rented/bench/virtual resource can schedule without being
// a fixed asset; a fixed asset can be maintained without being schedulable.
//
// New TABLE ⇒ default-value property initializers (EfficiencyPct = 100, etc.)
// are safe and need no HasDefaultValue: there are no existing rows to backfill,
// and EF applies the initializer to every code-created row. (Contrast R1-3's
// WorkCenter.DefaultYieldPct, which had to be nullable because it was ADDED to
// a populated table.)

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

/// <summary>What kind of thing this schedulable resource is.</summary>
public enum ResourceKind
{
    Machine = 0,    // a physical machine — bridges to an EAM Asset (AssetId)
    Labor = 1,      // an operator / labor pool — bridges to Employee/Craft (R2-5)
    Tool = 2,       // a tool / fixture — bridges to Tool (R2-5)
    Vendor = 3,     // an outside-process vendor — bridges to Vendor (R2-5)
    Location = 4,   // a bench / floor location treated as finite capacity
    Cell = 5,       // a grouped multi-machine cell scheduled as one resource
}

/// <summary>Operational status of a production resource (scheduler avoids non-Active).</summary>
public enum ProductionResourceStatus
{
    Active = 0,
    Inactive = 1,
    Down = 2,       // broken / in maintenance — scheduler skips
    Retired = 3,
}

/// <summary>
/// A schedulable production resource (machine / labor / tool / vendor / location /
/// cell) assignable to a Work Center, bridged to an EAM <see cref="Asset"/> for
/// machine kinds. The R4 finite scheduler loads/dispatches against these.
/// </summary>
[Table("ProductionResources")]
public class ProductionResource
{
    public int Id { get; set; }

    // ── Tenant trio ─────────────────────────────────────────────
    public int? TenantId { get; set; }
    [Required] public int CompanyId { get; set; }
    public int? SiteId { get; set; }

    // ── Identity ────────────────────────────────────────────────
    public ResourceKind ResourceKind { get; set; } = ResourceKind.Machine;

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;       // e.g. "HAAS-UMC750-1", "WELDER-AWS-AL-3"

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    // ── EAM bridge (machine kinds) ──────────────────────────────
    /// <summary>FK to the EAM <see cref="Asset"/> this resource IS (machine kinds). Null for labor/tool/vendor. SET NULL on asset delete.</summary>
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }

    // ── R2-5 bridges for non-machine kinds (each null unless the kind matches) ──

    /// <summary>Labor resource → the Employee it represents (ResourceKind.Labor). SET NULL.</summary>
    public int? EmployeeId { get; set; }
    public Abs.FixedAssets.Models.Masters.Employee? Employee { get; set; }

    /// <summary>Vendor resource → the outside-process Vendor (ResourceKind.Vendor). SET NULL.</summary>
    public int? VendorId { get; set; }
    public Abs.FixedAssets.Models.Vendor? Vendor { get; set; }

    /// <summary>Tool resource → the Tool/Fixture master (ResourceKind.Tool). SET NULL.</summary>
    public int? ToolId { get; set; }
    public Tool? Tool { get; set; }

    // ── Work-center assignment ──────────────────────────────────
    /// <summary>The Work Center this resource is currently assigned to. SET NULL — a resource survives a WC retirement.</summary>
    public int? WorkCenterId { get; set; }
    public WorkCenter? WorkCenter { get; set; }

    /// <summary>True when this is the WC's primary resource (others are alternates/overflow).</summary>
    public bool IsPrimary { get; set; } = false;

    /// <summary>Effective window for this assignment (a resource can move WCs over time).</summary>
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }

    // ── Scheduling / capacity ───────────────────────────────────
    public ProductionResourceStatus Status { get; set; } = ProductionResourceStatus.Active;

    /// <summary>Per-resource calendar/shift override (FK → WorkCalendar, SET NULL — R2-6). Null = inherit the WC/site calendar.</summary>
    public int? CalendarId { get; set; }
    public Abs.FixedAssets.Models.Masters.WorkCalendar? Calendar { get; set; }

    /// <summary>Per-resource availability deltas (downtime / maintenance / extra shift) layered on the calendar (R2-6).</summary>
    public ICollection<ResourceCalendarException>? CalendarExceptions { get; set; }

    /// <summary>Capabilities this resource holds — what it can do (R3-7). The R3-9 match service reads these.</summary>
    public ICollection<ResourceCapability>? Capabilities { get; set; }

    /// <summary>True = the scheduler treats this resource as finite capacity. Default true (machines constrain).</summary>
    public bool FiniteCapacityFlag { get; set; } = true;

    /// <summary>True = cannot run concurrent operations (exclusive-use fixture/cell).</summary>
    public bool ExclusiveUse { get; set; } = false;

    /// <summary>Throughput capacity per hour (units/hr) when meaningful. Null = derive from routing times.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? CapacityUnitsPerHour { get; set; }

    public decimal EfficiencyPct { get; set; } = 100;   // safe on a NEW table — no backfill
    public decimal UtilizationPct { get; set; } = 100;

    // ── Finite-capacity envelope (R2-6 — all nullable; ADDED to a populated table) ──

    /// <summary>Per-resource cap on available hours/day, overriding the calendar-derived hours. Null = use the calendar.</summary>
    [Column(TypeName = "decimal(6,2)")]
    public decimal? AvailableHoursPerDay { get; set; }

    /// <summary>Max operations this resource can run at once (a 3-person labor pool, a multi-spindle cell). Null = 1 unless ExclusiveUse expands it. Ignored when ExclusiveUse.</summary>
    public int? MaxConcurrentJobs { get; set; }

    /// <summary>Min batch the resource will run (furnace/plating-tank floor). Null = none.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? MinBatchSize { get; set; }

    /// <summary>Max batch the resource can hold (oven/tank ceiling). Null = none.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxBatchSize { get; set; }

    /// <summary>Smallest job quantity routable to this resource. Null = none.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? MinJobQuantity { get; set; }

    /// <summary>Largest job quantity routable to this resource. Null = none.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxJobQuantity { get; set; }

    // ── Cost ────────────────────────────────────────────────────
    /// <summary>Per-hour cost rate for this resource (labor wage / machine burden / vendor rate).</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal CostRatePerHour { get; set; } = 0;
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Audit + concurrency ─────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)] public string? ModifiedBy { get; set; }

    public byte[]? RowVersion { get; set; }
}
