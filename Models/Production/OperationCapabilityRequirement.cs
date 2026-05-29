// Theme B11 Wave R3-8 (2026-05-29) — OperationCapabilityRequirement.
//
// Retires the loose CSV `RoutingOperation.RequiredSkillCodes` /
// `RequiredToolingIds` text in favor of a real FK-backed requirement table. A
// routing operation declares the capabilities it REQUIRES — "5-axis
// simultaneous milling", "AWS D17.1 aluminum TIG (qualified)", "CMM ±0.0005in",
// optionally a SPECIFIC tool/fixture (R2-5 `Tool`) — and the R3-9 match service
// (`ICapabilityMatchService`) returns the eligible resources that satisfy ALL
// of them. The CSV columns stay on RoutingOperation (readable) during the
// transition; this table is the source of truth the scheduler reads.
//
// FK shape (the "does the child make sense without the parent?" rule):
//   • RoutingOperation → CASCADE  (a requirement is a pure child of the op).
//   • Capability       → RESTRICT (cannot delete a capability still required;
//                                  deactivate it instead).
//   • Tool (optional)  → SET NULL (the requirement survives a tool's archival,
//                                  degrading to capability-only).
// Three FKs to three DIFFERENT principals, exactly one CASCADE → no Postgres
// multi-cascade-path conflict.
//
// New TABLE ⇒ default-value initializers are safe with no HasDefaultValue.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

/// <summary>What flavor of capability this operation requirement expresses (drives match filtering + UI).</summary>
public enum CapabilityRequirementType
{
    MachineCapability = 0,   // the op needs a machine able to do X (replaces machine-side intent)
    LaborSkill = 1,          // the op needs a qualified operator (replaces RequiredSkillCodes)
    Tooling = 2,             // the op needs a tool/fixture (replaces RequiredToolingIds; ToolId may pin one)
    Inspection = 3,          // the op needs an inspection capability (CMM/FAI/NDT)
    SpecialProcess = 4,      // AS9100/NADCAP special process the resource must be qualified for
}

/// <summary>
/// One capability a <see cref="RoutingOperation"/> requires. The R3-9 match
/// service treats every <see cref="IsMandatory"/> requirement as a hard filter
/// (resource must hold the capability, be current, and — if a parametric
/// envelope is set — satisfy it); non-mandatory rows rank/prefer rather than gate.
/// </summary>
[Table("OperationCapabilityRequirements")]
public class OperationCapabilityRequirement
{
    public int Id { get; set; }

    // ── Tenant trio ─────────────────────────────────────────────
    public int? TenantId { get; set; }
    [Required] public int CompanyId { get; set; }
    public int? SiteId { get; set; }

    // ── Owning routing operation ────────────────────────────────
    /// <summary>The routing-template op that requires this capability. CASCADE — dies with the op.</summary>
    [Required] public int RoutingOperationId { get; set; }
    public RoutingOperation? RoutingOperation { get; set; }

    // ── Required capability ─────────────────────────────────────
    /// <summary>The capability the op requires. RESTRICT — cannot delete a capability still required.</summary>
    [Required] public int CapabilityId { get; set; }
    public Capability? Capability { get; set; }

    /// <summary>Optional pinned tool/fixture (R2-5) for a Tooling requirement. SET NULL — requirement degrades to capability-only on tool archival.</summary>
    public int? ToolId { get; set; }
    public Tool? Tool { get; set; }

    // ── Requirement detail ──────────────────────────────────────
    public CapabilityRequirementType RequirementType { get; set; } = CapabilityRequirementType.MachineCapability;

    /// <summary>Minimum proficiency a resource's qualification must meet (resource's must be ≥ this).</summary>
    public CapabilityProficiency MinProficiency { get; set; } = CapabilityProficiency.Qualified;

    /// <summary>True = hard gate (eligible resources MUST satisfy it). False = preference for ranking only.</summary>
    public bool IsMandatory { get; set; } = true;

    /// <summary>How many of this resource/tool the op needs concurrently (e.g. 2 welders). Null = 1.</summary>
    public int? QuantityRequired { get; set; }

    // ── Parametric envelope bound (optional) ────────────────────
    // For a parameterized capability (Capability.IsParameterized), the op may
    // require the resource's achieved EnvelopeValue to fall within these bounds
    // (e.g. CMM accuracy ≤ 0.001in, press brake ≥ 170 ton). Null = unbounded.

    /// <summary>Lower bound the resource's achieved envelope value must meet. Null = unbounded.</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? RequiredEnvelopeMin { get; set; }

    /// <summary>Upper bound the resource's achieved envelope value must meet. Null = unbounded.</summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? RequiredEnvelopeMax { get; set; }

    [MaxLength(300)]
    public string? Notes { get; set; }

    // ── Audit + concurrency ─────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)] public string? ModifiedBy { get; set; }

    public byte[]? RowVersion { get; set; }
}
