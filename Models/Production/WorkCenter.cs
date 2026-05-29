using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

// =============================================================================
// Sprint 13.5 PR #5c — WorkCenter (the MES dispatch unit)
//
// ADR-013 §"Manufacturing Domain" + MES research synthesis 2026-05-23 (Oracle
// Fusion WIS_WORK_DEFINITIONS, SAP S/4HANA PP-PI Work Centers, Epicor Kinetic
// Resource Groups, D365 Operations Resources).
//
// A WorkCenter is WHERE production operations get scheduled and executed.
// Sits between Asset (the specific machine) and Location (the building / cell)
// in granularity. One WC typically owns 1-N Assets (the join table
// WorkCenterAssetLink models this) so live machine state (Asset.CurrentOEE,
// Asset.CurrentAvailability, Asset.CurrentPerformance, Asset.CurrentQuality)
// can roll up to the WC card without duplicating the IoT pipeline.
//
// Multi-vertical: applies to Discrete / Repetitive / ETO production. Process-
// batch tenants continue to use Recipe + RecipePhase (PR #1.5 — orthogonal).
//
// Calendar: every WC has its own shift calendar (per SAP/Oracle baseline) —
// FK to the existing WorkCalendar (PRA-2). Without a calendar the scheduler
// can't compute lead times correctly.
//
// Costing: StandardCostRate ($/hr) + OverheadRate ($/hr) — used by the cost
// rollup in PR #5g and by EVM forecasting on ProductionOrder.
// =============================================================================
public enum WorkCenterType
{
    Machine = 0,          // Single piece of equipment (CNC mill, lathe, press)
    Cell = 1,             // Group of machines treated as one dispatch unit (FMS cell)
    ManualStation = 2,    // Operator workbench — assembly, deburr, paint touch-up
    Subcontract = 3,      // Outside-processing send/return point (heat treat vendor, plating shop)
}

public enum WorkCenterStatus
{
    Active = 0,
    Inactive = 1,
    Maintenance = 2,     // Down for scheduled maint — scheduler avoids
    Retired = 3,         // Hard-decommissioned — read-only history
}

public enum WorkCenterCapacityModel
{
    SingleResource = 0,     // Treat WC as one stream of capacity (the simple case)
    MultiResource = 1,      // N machines running in parallel — capacity = N * 1
    InfiniteCapacity = 2,   // Never the bottleneck — used for outside ops, inspections, manual stations
}

// ── B11 R1-2 — scheduling / dispatch field-group enums ──────────────────
// All value-0 defaults align with the EF auto-zero default (same PR-1/PR-3/PR-6
// enum precedent), so they need NO HasDefaultValue override.

/// <summary>Order jobs are pulled at a work center when the finite scheduler (R4) dispatches.</summary>
public enum WorkCenterDispatchRule
{
    FirstInFirstOut = 0,        // FIFO — release order (default)
    EarliestDueDate = 1,        // EDD
    ShortestProcessingTime = 2, // SPT
    CriticalRatio = 3,          // (due − now) / remaining work
    MinimumSlack = 4,           // least time-to-spare first
    HighestPriority = 5,        // order priority field wins
}

/// <summary>How the scheduler picks among eligible resources at a multi-resource work center.</summary>
public enum WorkCenterResourceSelectionRule
{
    PrimaryFirst = 0,   // prefer the designated primary, spill to alternates (default)
    LeastLoaded = 1,    // balance load across resources
    LowestCost = 2,     // cheapest qualified resource
    Fastest = 3,        // highest-efficiency resource
    RoundRobin = 4,     // even rotation
}

/// <summary>How operations are sequenced to minimize changeover/setup at this work center.</summary>
public enum WorkCenterSetupFamilyRule
{
    None = 0,                 // no setup-aware sequencing (default)
    GroupBySetupFamily = 1,   // batch jobs sharing a SetupFamilyCode together
    MinimizeChangeover = 2,   // optimize sequence against a setup matrix (R4)
}

[Table("WorkCenters")]
public class WorkCenter
{
    public int Id { get; set; }

    // Tenancy / org rollup.
    public int CompanyId { get; set; }

    // Identification.
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;          // e.g. "CNC1", "DEBURR-01"

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;          // e.g. "Haas VF-2 CNC Mill"

    [MaxLength(2000)]
    public string? Description { get; set; }

    // Classification.
    public WorkCenterType Type { get; set; } = WorkCenterType.Machine;
    public WorkCenterStatus Status { get; set; } = WorkCenterStatus.Active;
    public WorkCenterCapacityModel CapacityModel { get; set; } = WorkCenterCapacityModel.SingleResource;

    // Calendar — required for scheduling.
    public int? CalendarId { get; set; }  // FK → WorkCalendar (PRA-2)

    // Capacity / scheduling.
    public decimal EfficiencyPct { get; set; } = 100;     // 0-200 (a fast op runs faster than std)
    public decimal UtilizationPct { get; set; } = 100;    // 0-200 (% of shift expected available)
    public int? SimultaneousOperationsMax { get; set; }   // For MultiResource WCs

    // Default time padding (SAP convention — 5-time decomposition).
    public int DefaultQueueTimeMins { get; set; } = 0;
    public int DefaultMoveTimeMins { get; set; } = 0;
    public int DefaultWaitTimeMins { get; set; } = 0;

    // Costing.
    public decimal StandardCostRatePerHour { get; set; } = 0;
    public decimal OverheadRatePerHour { get; set; } = 0;
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    // Org / location FKs.
    // PR #5c.1: LocationId is REQUIRED — every WorkCenter physically lives at exactly
    // one site. Multi-site companies can have "CNC-1" at Site A AND Site B without
    // collision because UNIQUE is (CompanyId, LocationId, Code).
    // PR #5c.1 — physical plant. NOTE: per Dean's 2026-05-29 ruling, Site and
    // Location are the SAME real-world thing (one physical plant). Site is now
    // the canonical plant tier (see SiteId below); LocationId is legacy and stays
    // in place (load-bearing: UNIQUE (CompanyId, Code) + MES snapshots) until a
    // dedicated Location→Site collapse cleanup. UNIQUE is (CompanyId, Code).
    public int LocationId { get; set; }

    // B11 R1-2 — SiteId is the CANONICAL plant tier (FK → Site). Aligns the work
    // center with Department.SiteId + Asset.SiteId so the org chain
    // Site ← Department ← WorkCenter joins end-to-end. Nullable during the
    // Location→Site transition; the R4 scheduler scopes by Site.
    public int? SiteId { get; set; }
    public Abs.FixedAssets.Models.Site? Site { get; set; }

    // B11 R1-1 — OwningDepartmentId is now a real FK + nav (was an orphan id).
    // Closes the production-org backbone: Site→Dept→WC.
    public int? OwningDepartmentId { get; set; }
    public Abs.FixedAssets.Models.Department? OwningDepartment { get; set; }

    // ── B11 R1-2 — scheduling / dispatch field group (consumed by the R4 finite scheduler) ──

    /// <summary>True when this WC is a known constraint/drum (TOC). Default false (sentinel).</summary>
    public bool BottleneckFlag { get; set; } = false;

    /// <summary>Drum sequencing priority among constraints (lower = tighter). Null = not a constraint.</summary>
    public int? ConstraintPriority { get; set; }

    /// <summary>
    /// Number of interchangeable parallel MACHINES (MultiResource finite capacity = N streams).
    /// Distinct from <see cref="SimultaneousOperationsMax"/>, which caps concurrent OPERATIONS
    /// dispatched here. The R4 finite scheduler uses ParallelMachineCount for the capacity
    /// denominator and SimultaneousOperationsMax as the concurrency ceiling. Null = derive from
    /// the resource links (R2).
    /// </summary>
    public int? ParallelMachineCount { get; set; }

    /// <summary>Operators required to run one job at this WC (crew loading). Null = 1 implied.</summary>
    [Column(TypeName = "decimal(9,2)")]
    public decimal? CrewSizeRequired { get; set; }

    /// <summary>How jobs are pulled when dispatched. Default FIFO.</summary>
    public WorkCenterDispatchRule DispatchRule { get; set; } = WorkCenterDispatchRule.FirstInFirstOut;

    /// <summary>How the scheduler picks among eligible resources. Default PrimaryFirst.</summary>
    public WorkCenterResourceSelectionRule PrimaryResourceSelectionRule { get; set; } = WorkCenterResourceSelectionRule.PrimaryFirst;

    /// <summary>Setup-aware sequencing rule. Default None.</summary>
    public WorkCenterSetupFamilyRule SetupFamilyRule { get; set; } = WorkCenterSetupFamilyRule.None;

    /// <summary>Setup-family grouping key (jobs sharing it batch together when SetupFamilyRule=GroupBySetupFamily).</summary>
    [MaxLength(32)]
    public string? SetupFamilyCode { get; set; }

    /// <summary>
    /// Whether the finite scheduler (R4) includes this WC. NULLABLE on purpose:
    /// null = default-schedulable (the lock-safe way to ship a "default true"
    /// flag without a backfill that flips existing rows). Scheduler reads
    /// <c>SchedulingEnabled ?? true</c>.
    /// </summary>
    public bool? SchedulingEnabled { get; set; }

    /// <summary>Alternate work centers an operation can spill to (capability-permitting). R4 selection.</summary>
    public ICollection<WorkCenterAlternate> Alternates { get; set; } = new List<WorkCenterAlternate>();

    // Subcontract-specific (only meaningful when Type == Subcontract).
    public int? PreferredVendorId { get; set; }
    public int? DefaultLeadTimeDays { get; set; }

    // Audit.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)]
    public string? ModifiedBy { get; set; }
    public bool IsActive { get; set; } = true;

    // B11 R1-2 — concurrency token via Postgres xmin (closes the WC concurrency
    // gap from the audit; never IsRowVersion()+bytea).
    public byte[]? RowVersion { get; set; }

    // Nav.
    public ICollection<WorkCenterAssetLink> AssetLinks { get; set; } = new List<WorkCenterAssetLink>();
}

// =============================================================================
// B11 R1-2 — WorkCenterAlternate
//
// An ordered alternate-routing link: when a work center is loaded (or down), the
// R4 finite scheduler may spill an operation to a preferred alternate WC,
// capability + availability permitting. Distinct from the capability resolver
// (R3) — this is the explicit "these WCs are interchangeable for this op family"
// preference list the planner curates.
// =============================================================================
[Table("WorkCenterAlternates")]
public class WorkCenterAlternate
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    /// <summary>The primary work center.</summary>
    public int WorkCenterId { get; set; }
    public WorkCenter? WorkCenter { get; set; }

    /// <summary>The alternate work center the primary's work may spill to.</summary>
    public int AlternateWorkCenterId { get; set; }
    public WorkCenter? AlternateWorkCenter { get; set; }

    /// <summary>Preference order (lower = tried first).</summary>
    public int Preference { get; set; } = 10;

    /// <summary>Relative speed of the alternate vs the primary (1.0 = same; 0.8 = 20% slower). Null = same.</summary>
    [Column(TypeName = "decimal(6,3)")]
    public decimal? EfficiencyFactor { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(200)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public byte[]? RowVersion { get; set; }
}
