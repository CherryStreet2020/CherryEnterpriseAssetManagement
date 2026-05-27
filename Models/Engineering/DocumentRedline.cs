// Sprint 14.3 PR-7 (2026-05-27) — DocumentRedline.
//
// Structured markup annotation on a DocumentVersion, linked to the
// originating ECO that drives the change. Each redline captures:
// - WHAT changed (dimension, tolerance, material, process, note, etc.)
// - WHERE on the drawing (area reference, section, view)
// - ORIGINAL vs NEW value (the before/after delta)
// - HOW SEVERE the change is (minor/major/critical)
// - WHO reviewed and approved the redline
//
// Industry context: "Redline markup" is the standard engineering
// workflow where changes are annotated directly on the affected
// drawing before a new revision is formally released. SAP PLM
// stores this as "markup objects." Oracle Agile calls them
// "affected item annotations." Ours link directly to EcoLineItem
// → DocumentVersion with structured field-level deltas — not just
// freeform PDF markup.
//
// AS9100 §7.5.3: "The organization shall ensure that documents
// of external origin... are identified and their distribution
// controlled." Redlines are the mechanism by which interim changes
// are tracked BEFORE the formal revision release.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    // ────────────────────────────────────────────
    // ENUMS
    // ────────────────────────────────────────────

    /// <summary>
    /// Lifecycle status for a document redline annotation.
    /// </summary>
    public enum RedlineStatus
    {
        Draft = 0,              // Created, not yet reviewed
        UnderReview = 1,        // Submitted for engineering review
        Approved = 2,           // Reviewed and approved — incorporated into next rev
        Rejected = 3,           // Reviewed and rejected
        Superseded = 4,         // Replaced by a newer redline or formal revision
        Archived = 5,           // Historical record, no longer active
    }

    /// <summary>
    /// Category of change captured by the redline.
    /// </summary>
    public enum RedlineType
    {
        Dimension = 0,          // Dimensional change (bore, length, diameter, etc.)
        Tolerance = 1,          // Tolerance change (tighter or looser)
        Material = 2,           // Material specification change
        Process = 3,            // Process/manufacturing method change
        SurfaceFinish = 4,      // Surface finish / coating / plating change
        Note = 5,               // Drawing note addition/modification
        Deletion = 6,           // Feature or note removal
        Addition = 7,           // New feature or component added
        Reference = 8,          // Reference/callout update
        GeometricTolerance = 9, // GD&T (geometric dimensioning + tolerancing) change
    }

    /// <summary>
    /// Severity classification for the redline change.
    /// </summary>
    public enum RedlineSeverity
    {
        Minor = 0,              // Cosmetic / editorial — no F/F/F impact
        Major = 1,              // Functional impact — requires engineering review
        Critical = 2,           // Safety / airworthiness / regulatory — requires customer approval
    }

    // ────────────────────────────────────────────
    // ENTITY
    // ────────────────────────────────────────────

    /// <summary>
    /// Structured redline markup annotation on a document version.
    /// </summary>
    [Table("DocumentRedlines")]
    public class DocumentRedline
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        // ----- Identity -----
        [Required] [StringLength(32)]
        public string RedlineNumber { get; set; } = string.Empty;

        // ----- Document version linkage -----
        public int DocumentVersionId { get; set; }
        public DocumentVersion? DocumentVersion { get; set; }

        // ----- ECO linkage (nullable — redlines may exist before ECO) -----
        public int? EcoId { get; set; }
        public EngineeringChangeOrder? Eco { get; set; }

        // ----- Item affected -----
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        // ----- Classification -----
        public RedlineStatus Status { get; set; } = RedlineStatus.Draft;
        public RedlineType Type { get; set; } = RedlineType.Dimension;
        public RedlineSeverity Severity { get; set; } = RedlineSeverity.Minor;

        // ----- What changed -----
        [Required] [StringLength(200)]
        public string AffectedArea { get; set; } = string.Empty;

        [StringLength(500)]
        public string? OriginalValue { get; set; }

        [StringLength(500)]
        public string? NewValue { get; set; }

        [StringLength(4000)]
        public string? MarkupDescription { get; set; }

        // ----- Specification references -----
        [StringLength(200)]
        public string? SpecificationReference { get; set; }

        [StringLength(200)]
        public string? DrawingZone { get; set; }

        [StringLength(200)]
        public string? DrawingView { get; set; }

        // ----- Impact flags -----
        public bool AffectsForm { get; set; }
        public bool AffectsFit { get; set; }
        public bool AffectsFunction { get; set; }
        public bool CustomerApprovalRequired { get; set; }
        public bool RequiresFaiRetrigger { get; set; }

        // ----- Review + approval -----
        [StringLength(120)]
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        [StringLength(2000)]
        public string? ReviewNotes { get; set; }

        [StringLength(120)]
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        [StringLength(2000)]
        public string? ApprovalNotes { get; set; }

        // ----- Audit -----
        [StringLength(4000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        [StringLength(120)]
        public string? UpdatedBy { get; set; }

        // ----- Concurrency -----
        public byte[]? RowVersion { get; set; }
    }
}
