// B6 Foundation Sprint PR-FS-6 (2026-05-26) — CustomerItemXref entity.
//
// SAP Customer-Material Info Record (CMIR, transaction VD51/VD52) equivalent.
// Oracle Customer Item Cross Reference. D365 Customer Part Number.
//
// Bidirectional translation:
//   - **At SO ingestion**: customer PO references their own part number (e.g.,
//     GE Aviation calls our `BRG-6207-2RS` something like `GEAV-BRG-A12345-R3`).
//     The system must translate THEIR PN → OUR ItemId before MRP / production
//     / fulfillment can engage.
//   - **At ship + invoice**: packing slip / commercial invoice MUST show the
//     CUSTOMER's PN (not ours) for their receiving + matching systems to
//     process correctly. Reverse translation: OUR ItemId → THEIR PN.
//
// Multi-OEM scoping: the same literal string PN can validly exist at multiple
// Customers without conflict ("BRG-001" might mean different parts at GE
// Aviation vs. Honeywell vs. Boeing). Uniqueness scopes per Customer.
//
// Customer revision tracking: customers rev their drawings. A customer-side
// ECO that bumps R2 → R3 must supersede the prior xref row (effective-dated)
// and stamp the CustomerEcoNumber for audit trace.
//
// Used by:
//   - **Theme B1** SO Line MTO/ETO (Sprint 19+) — at SO ingest, resolves the
//     customer's PN to an Item.
//   - **Theme B8 PR-PO-1** ProductionOrder header (Sprint 14.x) — when a PO is
//     released from a SO line, capture the customer PN + revision into the PO
//     header for downstream display.
//   - **Shipping/Invoice** — render the customer's PN on packing slip + invoice
//     (current placeholder uses Item.PartNumber, which is wrong for OEM
//     customers; this PR unblocks the fix in a later UI sprint).
//   - **Quality / FAI (AS9100 §8.3)** — First Article Inspection reports
//     reference the customer's drawing number + revision, which lives on the
//     xref row.
//   - **CAR / CAPA correspondence** — when an NCR arrives, customer references
//     THEIR PN; this xref is how we find the Item to investigate.
//
// Locks applied prophylactically (all prior B6 Codex catches):
//   - Tenant trio + RowVersion concurrency token.
//   - Service-side NULL-safe uniqueness check (DB partial UNIQUE complements).
//   - All nullable FK columns get nav properties + HasOne config.
//   - Realistic mfg fixtures only in tests (per HARD LOCK no-fake-data).

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Models.Masters
{
    /// <summary>
    /// Lifecycle status of a Customer-Item xref. Different from
    /// <see cref="ItemStatus"/> (that's about the Item itself); this is about
    /// the CUSTOMER'S relationship to the Item.
    /// </summary>
    public enum CustomerXrefStatus
    {
        /// <summary>Active and current.</summary>
        Active = 0,

        /// <summary>
        /// Superseded by a newer revision row. The new row has
        /// <c>SupersededByXrefId</c> pointing back. Read-only for new SOs.
        /// </summary>
        Superseded = 1,

        /// <summary>
        /// Permanently obsolete (customer end-of-life). Cannot be used for
        /// any new SO line. Historical SOs may still reference it for invoice
        /// re-render.
        /// </summary>
        Obsolete = 2,
    }

    /// <summary>
    /// Cross-reference linking a Customer's part-number / drawing / spec
    /// vocabulary to an internal <see cref="Item"/>. SAP CMIR equivalent.
    /// </summary>
    public class CustomerItemXref
    {
        public int Id { get; set; }

        // ===== Identity + tenant trio =====================================

        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Required]
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? TenantId { get; set; }
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        // ===== Customer-side vocabulary ===================================

        /// <summary>
        /// The customer's part number for this Item. UPPER-CASE convention
        /// recommended but not enforced (customer-side conventions vary).
        /// </summary>
        [Required, StringLength(100)]
        [Display(Name = "Customer Part Number")]
        public string CustomerPartNumber { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Customer Part Description")]
        public string? CustomerPartDescription { get; set; }

        [StringLength(20)]
        [Display(Name = "Customer Revision")]
        public string? CustomerRevision { get; set; }

        [StringLength(100)]
        [Display(Name = "Customer Drawing Number")]
        public string? CustomerDrawingNumber { get; set; }

        [StringLength(20)]
        [Display(Name = "Customer Drawing Revision")]
        public string? CustomerDrawingRevision { get; set; }

        [StringLength(100)]
        [Display(Name = "Customer Specification Number")]
        public string? CustomerSpecificationNumber { get; set; }

        /// <summary>
        /// Customer-side ECO/ECN that triggered this revision of the xref. Used
        /// in AS9100 §8.3 traceability + ECR/ECO chain-of-custody (B6 PR-IM-3,
        /// Sprint 14.3).
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Customer ECO Number")]
        public string? CustomerEcoNumber { get; set; }

        // ===== Lifecycle ===================================================

        [Required]
        [Display(Name = "Status")]
        public CustomerXrefStatus Status { get; set; } = CustomerXrefStatus.Active;

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// FK to the xref row that supersedes this one (when this is a prior
        /// revision). Null when this is the current revision.
        /// </summary>
        public int? SupersededByXrefId { get; set; }
        public CustomerItemXref? SupersededByXref { get; set; }

        [Display(Name = "Effective From")]
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        [Display(Name = "Effective To")]
        public DateTime? EffectiveToUtc { get; set; }

        // ===== Concurrency token (PR-FS-4 lesson, PR-XminBackfill fix) =======
        // Postgres xmin system column, mapped in AppDbContext via
        // MapXminRowVersion (project convention, HARD LOCK from PR #365).
        // PR-XminBackfill 2026-05-27: converted from IsRowVersion()+bytea
        // to xmin pattern (was latent 23502 bug on first INSERT).
        public byte[]? RowVersion { get; set; }

        // ===== Audit ========================================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
