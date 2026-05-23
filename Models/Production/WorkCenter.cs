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
    public int LocationId { get; set; }
    public int? OwningDepartmentId { get; set; }

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

    // Nav.
    public ICollection<WorkCenterAssetLink> AssetLinks { get; set; } = new List<WorkCenterAssetLink>();
}
