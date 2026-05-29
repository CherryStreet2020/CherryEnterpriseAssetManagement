// Theme B11 Wave R2-5 (2026-05-29) — Tool / Fixture.
//
// The first-class tooling entity. Replaces the loose CSV
// `RoutingOperation.RequiredToolingIds` text with a real record that the
// capability model (R3) and finite scheduler (R4) can reason about: a cutting
// tool, fixture, gauge, jig, mold, or die — with crib location, calibration
// state, controlled-tool flag, and an OPTIONAL bridge to an EAM `Asset` (a
// calibrated gauge or expensive fixture is often tracked as an asset too).
//
// A Tool becomes a schedulable resource by being referenced from a
// `ProductionResource` of `ResourceKind.Tool` (ToolId bridge, R2-5). That keeps
// the dual-identity discipline: the Tool is the master record; the
// ProductionResource is its schedulable identity.
//
// New TABLE ⇒ default initializers are safe with no HasDefaultValue.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

/// <summary>What kind of tooling this is.</summary>
public enum ToolType
{
    CuttingTool = 0,   // end mills, inserts, drills (often perishable)
    Fixture = 1,       // workholding fixture
    Gauge = 2,         // measuring / inspection gauge (calibration-controlled)
    Jig = 3,           // drilling / assembly jig
    Mold = 4,          // injection / casting mold
    Die = 5,           // stamping / forming die
    Other = 99,
}

/// <summary>Operational state of a tool (scheduler avoids non-Available).</summary>
public enum ToolStatus
{
    Available = 0,
    InUse = 1,
    OutForCalibration = 2,
    Maintenance = 3,
    Retired = 4,
}

/// <summary>
/// A tool / fixture / gauge master record. Referenced by a
/// <see cref="ProductionResource"/> (ResourceKind.Tool) to become schedulable,
/// and by R3's capability requirements (replacing the CSV RequiredToolingIds).
/// </summary>
[Table("Tools")]
public class Tool
{
    public int Id { get; set; }

    // ── Tenant trio ─────────────────────────────────────────────
    public int? TenantId { get; set; }
    [Required] public int CompanyId { get; set; }
    public int? SiteId { get; set; }

    // ── Identity ────────────────────────────────────────────────
    public ToolType ToolType { get; set; } = ToolType.CuttingTool;

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;      // e.g. "FIX-TI-BRKT-001", "GAUGE-CMM-0001"

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Tool-crib / storage location (free text or bin reference).</summary>
    [MaxLength(64)]
    public string? CribLocation { get; set; }

    // ── Control / calibration ───────────────────────────────────
    /// <summary>True = a controlled tool (calibration / AS9100 special-process control applies).</summary>
    public bool IsControlled { get; set; } = false;

    /// <summary>True = calibration is required for this tool (gauges, CMM probes).</summary>
    public bool CalibrationRequired { get; set; } = false;

    public DateTime? LastCalibratedUtc { get; set; }
    public DateTime? CalibrationDueUtc { get; set; }

    // ── Quantity (perishable tooling) ───────────────────────────
    /// <summary>On-hand count for perishable/consumable tooling. Null = single durable tool.</summary>
    public int? QuantityOnHand { get; set; }

    // ── EAM bridge (optional) ───────────────────────────────────
    /// <summary>Optional bridge to the EAM <see cref="Asset"/> when the tool is also a tracked asset (calibrated gauge, costly fixture). SET NULL.</summary>
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }

    public ToolStatus Status { get; set; } = ToolStatus.Available;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Audit + concurrency ─────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)] public string? ModifiedBy { get; set; }

    public byte[]? RowVersion { get; set; }
}
