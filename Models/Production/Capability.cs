// Theme B11 Wave R3-7 (2026-05-29) — Capability master.
//
// THE CROWN-JEWEL DISRUPTOR. Per the §0 rule "capability drives scheduling,
// not hard-coded machines": a routing operation declares a *required
// capability* (R3-8), and the finite scheduler (R4) matches it to *eligible
// resources* by capability + availability + cost + health (R3-9). The
// incumbents (Epicor / MIE / Plex) pin a specific machine onto the routing step
// — when that machine is down or loaded, the job stalls or a planner re-routes
// by hand. Here the routing says WHAT must be true ("5-axis simultaneous
// milling", "AWS D17.1 aluminum TIG", "CMM ±0.0005in", "NADCAP heat-treat
// AMS 2750") and ANY qualified, available, in-cert resource can run it.
//
// A Capability is a master record. A `ResourceCapability` (sibling file) joins a
// `ProductionResource` to the capabilities it HAS — each with a qualification
// date and optional expiry (a welder's AWS cert lapses; a CMM's calibration
// expires; a 5-axis machine's geometric capability does not). Seed/derive hints
// come from `MachineSpecification` (FiveAxisCapable, axis travels, spindle) and
// the existing `Skill`/`Craft` labor data.
//
// New TABLE ⇒ default-value property initializers are safe with no
// HasDefaultValue (no existing rows to backfill).

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

/// <summary>Broad family a capability belongs to (drives UI grouping + match filtering).</summary>
public enum CapabilityCategory
{
    Machining = 0,        // milling, turning, grinding, EDM
    Welding = 1,          // TIG / MIG / spot / e-beam — often special-process
    Inspection = 2,       // CMM, FAI, NDT — metrology
    HeatTreat = 3,        // anneal / age / harden — NADCAP AMS 2750 special-process
    Forming = 4,          // press brake, stamping, roll forming
    Finishing = 5,        // anodize / paint / passivate / plate — special-process
    Assembly = 6,         // mechanical / electrical assembly
    SpecialProcess = 7,   // catch-all AS9100/NADCAP special process not above
    Fabrication = 8,      // laser / waterjet / plasma cutting, sheet
    Other = 99,
}

/// <summary>
/// A capability a production resource can hold and a routing operation can
/// require. The unit of capability-based scheduling — what a resource is
/// *able* to do, decoupled from which physical machine is on the routing.
/// </summary>
[Table("Capabilities")]
public class Capability
{
    public int Id { get; set; }

    // ── Tenant trio ─────────────────────────────────────────────
    public int? TenantId { get; set; }
    [Required] public int CompanyId { get; set; }
    public int? SiteId { get; set; }

    // ── Identity ────────────────────────────────────────────────
    public CapabilityCategory Category { get; set; } = CapabilityCategory.Machining;

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;       // e.g. "MILL-5AX-SIM", "WELD-AWS-D17.1-AL", "INSP-CMM"

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;       // e.g. "5-axis simultaneous milling"

    [MaxLength(500)]
    public string? Description { get; set; }

    // ── Qualification / special-process control ─────────────────
    /// <summary>
    /// True = an AS9100 / NADCAP special process. Holders must carry a dated
    /// qualification, and the match service (R3-9) treats currency as mandatory.
    /// </summary>
    public bool IsSpecialProcess { get; set; } = false;

    /// <summary>
    /// True = a resource must hold a dated qualification to be considered
    /// eligible (welder cert, operator endorsement). False = structural/geometric
    /// capability that doesn't lapse (a 5-axis machine is always 5-axis).
    /// </summary>
    public bool RequiresQualification { get; set; } = false;

    /// <summary>
    /// Default cert lifespan in months, used to compute an expiry when a
    /// resource is qualified without an explicit ExpiresOnUtc. Null = no default.
    /// </summary>
    public int? DefaultQualificationValidityMonths { get; set; }

    // ── Optional parametric envelope ────────────────────────────
    // Some capabilities are parameterized: a press brake "≤170 ton", a CMM at
    // "±0.0005 in", a laser cutting "≤0.250 in mild steel". The capability
    // declares the dimension + the master's envelope; a resource records the
    // value it actually achieves (ResourceCapability.EnvelopeValue). Phased per
    // §0 — model the envelope shape now, layer the long tail later.

    /// <summary>True = this capability carries a numeric parametric envelope.</summary>
    public bool IsParameterized { get; set; } = false;

    /// <summary>Unit of the envelope dimension (e.g. "ton", "in", "mm", "deg C"). Null unless parameterized.</summary>
    [MaxLength(24)]
    public string? EnvelopeUom { get; set; }

    /// <summary>Lower bound of the master envelope (e.g. min thickness). Null = unbounded.</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? EnvelopeMin { get; set; }

    /// <summary>Upper bound of the master envelope (e.g. max tonnage / size). Null = unbounded.</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? EnvelopeMax { get; set; }

    // ── Status ──────────────────────────────────────────────────
    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string? GoverningStandard { get; set; }   // e.g. "AWS D17.1", "AMS 2750", "AS9102"

    // ── Children ────────────────────────────────────────────────
    public ICollection<ResourceCapability>? ResourceCapabilities { get; set; }

    // ── Audit + concurrency ─────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)] public string? ModifiedBy { get; set; }

    public byte[]? RowVersion { get; set; }
}
