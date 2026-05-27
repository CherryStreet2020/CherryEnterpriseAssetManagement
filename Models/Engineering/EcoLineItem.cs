// Sprint 14.3 PR-1 (2026-05-27) — EcoLineItem.
//
// One row per affected thing in an ECO. The affected thing can be:
//   - An Item (e.g., a part number's spec is changing)
//   - A Document + DocumentVersion (the drawing or spec being revised)
//   - A NewDocumentVersion (the resulting Released version — added when
//     the ECO ReleaseEco flow triggers atomic supersede via IDocumentService)
//
// Disposition tells the floor what to do with in-progress material that
// was built to the OLD spec when this ECO releases (use-as-is, rework,
// scrap, return-to-vendor, etc.).

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    /// <summary>
    /// Disposition for in-process material built to the OLD spec when an
    /// ECO releases. AS9100 §8.7 nonconforming output / customer waiver
    /// language.
    /// </summary>
    public enum EcoLineItemDisposition
    {
        /// <summary>Use as-is — pre-change material accepted at customer + internal QA.</summary>
        UseAsIs = 0,

        /// <summary>Rework — pre-change material reworked to new spec.</summary>
        Rework = 1,

        /// <summary>Scrap — pre-change material destroyed.</summary>
        Scrap = 2,

        /// <summary>Return to vendor — pre-change material returned to supplier.</summary>
        ReturnToVendor = 3,

        /// <summary>Return to customer — pre-change material recalled from customer.</summary>
        ReturnToCustomer = 4,

        /// <summary>Quarantine — pre-change material held pending further decision.</summary>
        Quarantine = 5,

        /// <summary>Not applicable — change has no in-process impact (e.g. doc-only).</summary>
        NotApplicable = 99,
    }

    /// <summary>
    /// One affected Item / Document / DocumentVersion record on an ECO.
    /// </summary>
    [Table("EcoLineItems")]
    public class EcoLineItem
    {
        public int Id { get; set; }

        // ===== Parent + tenant denorm =====================================

        [Required]
        public int EcoId { get; set; }
        public EngineeringChangeOrder? Eco { get; set; }

        [Required]
        public int CompanyId { get; set; }

        public int? LocationId { get; set; }

        // ===== Position + identity ========================================

        [Required]
        public int Sequence { get; set; } = 10;

        // ===== What's affected (any combination of these) ================

        /// <summary>FK to an affected Item. RESTRICT on Item delete — line
        /// references a real change record that should be preserved.</summary>
        public int? AffectedItemId { get; set; }
        public Item? AffectedItem { get; set; }

        /// <summary>FK to an affected Document. RESTRICT preserves audit trail.</summary>
        public int? AffectedDocumentId { get; set; }
        public Document? AffectedDocument { get; set; }

        /// <summary>
        /// FK to the specific DocumentVersion being superseded by this ECO.
        /// When the ECO releases, the service flips this version to
        /// Superseded (via IDocumentService). SET NULL preserves the line
        /// row if engineering archives the old version later.
        /// </summary>
        public int? AffectedDocumentVersionId { get; set; }
        public DocumentVersion? AffectedDocumentVersion { get; set; }

        /// <summary>
        /// FK to the resulting NEW DocumentVersion that becomes Released
        /// by this ECO. The service calls IDocumentService.ReleaseVersion
        /// on this id during ReleaseEco — which atomically supersedes the
        /// AffectedDocumentVersionId. SET NULL preserves the line row.
        /// </summary>
        public int? NewDocumentVersionId { get; set; }
        public DocumentVersion? NewDocumentVersion { get; set; }

        // ===== Change description =========================================

        [StringLength(2000)]
        [Display(Name = "Change Description")]
        public string? ChangeDescription { get; set; }

        [StringLength(500)]
        [Display(Name = "Before Value")]
        public string? BeforeValue { get; set; }

        [StringLength(500)]
        [Display(Name = "After Value")]
        public string? AfterValue { get; set; }

        [Required]
        public EcoLineItemDisposition Disposition { get; set; } = EcoLineItemDisposition.NotApplicable;

        // ===== Audit + concurrency ========================================

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
