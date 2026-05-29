// Theme B11 Wave R3-7 (2026-05-29) — ResourceCapability.
//
// The join that says "this resource CAN do this". A `ProductionResource`
// (machine / labor / tool / vendor) holds zero-or-many capabilities; each row
// carries the qualification facts the match service (R3-9) needs to decide
// eligibility: when it was qualified, when it expires (a welder's AWS cert, a
// CMM's calibration — null = never expires, like a machine's geometry), who
// signed it off, and the cert / NADCAP audit reference for traceability.
//
// CASCADE on the owning resource: a ResourceCapability is a PURE CHILD of the
// resource — it has no meaning once the resource is gone, so it dies with it
// (same rule as ResourceCalendarException in R2-6). The FK to the Capability
// master is RESTRICT: you cannot delete a capability that resources still hold
// (deactivate it instead). Two FKs to DIFFERENT principals, only one CASCADE —
// no Postgres multi-cascade-path conflict.
//
// New TABLE ⇒ default-value initializers are safe with no HasDefaultValue.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production;

/// <summary>How well a resource performs a capability (used by R3-9 to RANK eligible resources).</summary>
public enum CapabilityProficiency
{
    Provisional = 0,   // qualified under supervision / probation
    Qualified = 1,     // fully qualified — the normal state
    Expert = 2,        // preferred resource for this capability
}

/// <summary>
/// Assigns a <see cref="Capability"/> to a <see cref="ProductionResource"/> with
/// qualification + expiry. The match service treats a row as ELIGIBLE when it is
/// active, the capability is present, and (if it expires) is not past
/// <see cref="ExpiresOnUtc"/>.
/// </summary>
[Table("ResourceCapabilities")]
public class ResourceCapability
{
    public int Id { get; set; }

    // ── Tenant trio ─────────────────────────────────────────────
    public int? TenantId { get; set; }
    [Required] public int CompanyId { get; set; }
    public int? SiteId { get; set; }

    // ── The pair ────────────────────────────────────────────────
    /// <summary>The resource that holds the capability. CASCADE — dies with the resource.</summary>
    [Required] public int ProductionResourceId { get; set; }
    public ProductionResource? ProductionResource { get; set; }

    /// <summary>The capability held. RESTRICT — cannot delete a capability still in use.</summary>
    [Required] public int CapabilityId { get; set; }
    public Capability? Capability { get; set; }

    // ── Qualification facts ─────────────────────────────────────
    public CapabilityProficiency Proficiency { get; set; } = CapabilityProficiency.Qualified;

    /// <summary>When this resource was qualified for the capability. Null = qualified since inception (geometric capability).</summary>
    public DateTime? QualifiedOnUtc { get; set; }

    /// <summary>
    /// When the qualification lapses. Null = never expires (a machine's geometry,
    /// a durable tool). For special processes this is the cert/calibration due date.
    /// </summary>
    public DateTime? ExpiresOnUtc { get; set; }

    /// <summary>Who certified / signed off the qualification (auditor, supervisor).</summary>
    [MaxLength(100)]
    public string? QualifiedBy { get; set; }

    /// <summary>Cert number / NADCAP audit reference / training record id for traceability.</summary>
    [MaxLength(100)]
    public string? CertificateReference { get; set; }

    /// <summary>
    /// For a parameterized capability: the value this resource actually achieves
    /// (e.g. CMM accuracy 0.0005 in, press brake 175 ton). Compared to the
    /// operation requirement's bound by R3-9. Null for non-parameterized capabilities.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? EnvelopeValue { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(300)]
    public string? Notes { get; set; }

    // ── Audit + concurrency ─────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    [MaxLength(100)] public string? ModifiedBy { get; set; }

    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// True when the qualification is usable as of <paramref name="asOfUtc"/>:
    /// active AND (never-expires OR expiry still in the future). The single
    /// predicate the R3-9 match service evaluates per row.
    /// </summary>
    public bool IsCurrentAsOf(DateTime asOfUtc) =>
        IsActive && (ExpiresOnUtc == null || ExpiresOnUtc > asOfUtc);
}
