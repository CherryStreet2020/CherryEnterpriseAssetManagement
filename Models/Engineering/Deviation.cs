// Sprint 14.3 PR-2 (2026-05-27) — Deviation entity.
//
// A controlled SHORT-TERM exception from the currently released engineering
// specification. Authorizes use of non-conforming material, an alternate
// process, or a dimensional out-of-tolerance condition for a LIMITED scope
// (quantity, date range, or specific production orders).
//
// DISTINCTION FROM ECR/ECO:
//   - ECR/ECO = permanent change to the spec (the new spec IS the spec).
//   - Deviation = temporary exception (production returns to original spec
//     when the deviation expires or quantity is consumed).
//   - If a deviation should become permanent → raise an ECR/ECO.
//
// LIFECYCLE: Draft → Submitted → UnderReview → Approved → Active → Expired
//                                              ↘ Rejected
//                                              ↘ Cancelled
//            Active → Closed (manually closed before expiry)
//            Active → Expired (auto-transition when ExpirationDate passes
//                     or ConsumedQuantity >= MaxQuantity)
//
// REGULATORY CONTEXT:
//   - AS9100 §8.5.6 — requires documented authorization for any deviation
//     from released design (even temporary).
//   - NADCAP / AS13100 — deviations must be traceable to the specific
//     lots/serials/POs affected.
//   - FAA Order 8120.23 — deviations on aviation parts require DAR/DER
//     disposition or customer approval depending on criticality.
//
// MODELED AFTER: SAP S/4HANA QM Deviation / Oracle PLM Deviation /
// Siemens Teamcenter Deviation Request / Aras Innovator Deviation /
// Arena PLM Deviation. Design-pattern references only — we do not
// integrate with any of these (per reference_sprint_naming_no_vendor_implication.md).
//
// HARD LOCKS APPLIED:
//   - xmin concurrency: byte[]? RowVersion + MapXminRowVersion in AppDbContext
//   - enum defaults: DeviationStatus default Draft, DeviationType default Material
//   - tenant trio: TenantId + CompanyId + item/PO linkage
//   - realistic mfg data in tests (no Test/Foo/Bar)

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    // ================================================================
    // Enums
    // ================================================================

    /// <summary>
    /// What dimension of the spec is the deviation from?
    /// </summary>
    public enum DeviationType
    {
        /// <summary>Material substitution — alternate alloy, grade, supplier lot.</summary>
        Material = 0,

        /// <summary>Process deviation — alternate routing step, different machine, modified parameters.</summary>
        Process = 1,

        /// <summary>Dimensional — out-of-tolerance condition accepted within revised limits.</summary>
        Dimensional = 2,

        /// <summary>Documentation — missing or outdated cert, spec, or drawing accepted temporarily.</summary>
        Documentation = 3,

        /// <summary>Supplier — use of a non-approved or conditionally-approved supplier source.</summary>
        Supplier = 4,
    }

    /// <summary>
    /// Deviation lifecycle status. Draft through Expired/Closed.
    /// </summary>
    public enum DeviationStatus
    {
        Draft = 0,
        Submitted = 1,
        UnderReview = 2,
        Approved = 3,
        Active = 4,         // Deviation is live — production can use the exception
        Expired = 5,        // Auto or manual — quantity consumed or date passed
        Rejected = 6,
        Cancelled = 7,
        Closed = 8,         // Manually closed before natural expiry
    }

    // ================================================================
    // Entity
    // ================================================================

    [Table("Deviations")]
    public class Deviation
    {
        public int Id { get; set; }

        // ----- Tenant trio -----
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        // ----- Identity -----

        /// <summary>
        /// Human-facing identifier (e.g., "DEV-2026-00015"). Generated via
        /// NumberSequence or assigned by the engineering system.
        /// </summary>
        [Required]
        [StringLength(32)]
        [Display(Name = "Deviation #")]
        public string DeviationNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public DeviationType Type { get; set; } = DeviationType.Material;
        public DeviationStatus Status { get; set; } = DeviationStatus.Draft;

        // ----- Scope: what is affected -----

        /// <summary>The Item this deviation applies to (e.g., BRG-6207-2RS Rev C).</summary>
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        /// <summary>Optional: specific ProductionOrder this deviation is scoped to.</summary>
        public int? ProductionOrderId { get; set; }
        public Production.ProductionOrder? ProductionOrder { get; set; }

        /// <summary>Optional: originating ECR that surfaced the need for this deviation.</summary>
        public int? OriginatingEcrId { get; set; }
        public EngineeringChangeRequest? OriginatingEcr { get; set; }

        // ----- Deviation limits -----

        /// <summary>
        /// Maximum quantity authorized under this deviation. NULL = unlimited
        /// (date-limited only).
        /// </summary>
        [Display(Name = "Max Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MaxQuantity { get; set; }

        /// <summary>
        /// Quantity consumed so far against this deviation. Updated by
        /// production material-issue transactions that reference this deviation.
        /// </summary>
        [Display(Name = "Consumed Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ConsumedQuantity { get; set; } = 0;

        /// <summary>
        /// Deviation valid from this date (UTC). NULL = effective immediately
        /// upon activation.
        /// </summary>
        [Display(Name = "Effective From")]
        public DateTime? EffectiveFromUtc { get; set; }

        /// <summary>
        /// Deviation expires at this date (UTC). NULL = no date limit (quantity-
        /// limited only). When both MaxQuantity and ExpirationDate are NULL, the
        /// deviation is open-ended (unusual but valid for documentation deviations).
        /// </summary>
        [Display(Name = "Expiration Date")]
        public DateTime? ExpirationDateUtc { get; set; }

        /// <summary>Max number of production orders this deviation can be applied to.</summary>
        public int? MaxProductionOrders { get; set; }

        /// <summary>Count of production orders that have used this deviation.</summary>
        public int ConsumedProductionOrders { get; set; } = 0;

        // ----- Impact assessment (mirrors ECR F/F/F pattern) -----

        /// <summary>Does this deviation affect the FORM of the item?</summary>
        [Display(Name = "Affects Form")]
        public bool AffectsForm { get; set; } = false;

        /// <summary>Does this deviation affect the FIT of the item?</summary>
        [Display(Name = "Affects Fit")]
        public bool AffectsFit { get; set; } = false;

        /// <summary>Does this deviation affect the FUNCTION of the item?</summary>
        [Display(Name = "Affects Function")]
        public bool AffectsFunction { get; set; } = false;

        /// <summary>Safety-critical deviation? Triggers additional approval gates.</summary>
        [Display(Name = "Safety Impact")]
        public bool SafetyImpact { get; set; } = false;

        /// <summary>Does the customer need to be notified / approve?</summary>
        [Display(Name = "Customer Approval Required")]
        public bool CustomerApprovalRequired { get; set; } = false;

        /// <summary>Has the customer approved? Only meaningful when CustomerApprovalRequired=true.</summary>
        [Display(Name = "Customer Approval Received")]
        public bool CustomerApprovalReceived { get; set; } = false;

        /// <summary>Customer PO or contract reference for the approval.</summary>
        [StringLength(100)]
        [Display(Name = "Customer Approval Reference")]
        public string? CustomerApprovalReference { get; set; }

        // ----- Deviation detail -----

        /// <summary>
        /// What is the original spec? (e.g., "AMS 6520 Alloy Steel Bar, 0.500 ±0.002 dia")
        /// </summary>
        [StringLength(1000)]
        [Display(Name = "Original Specification")]
        public string? OriginalSpecification { get; set; }

        /// <summary>
        /// What is the deviated condition? (e.g., "AMS 6520 bar, 0.498 dia — 0.002 below nominal")
        /// </summary>
        [StringLength(1000)]
        [Display(Name = "Deviated Condition")]
        public string? DeviatedCondition { get; set; }

        /// <summary>
        /// Engineering justification for accepting the deviation.
        /// </summary>
        [StringLength(4000)]
        [Display(Name = "Justification")]
        public string? Justification { get; set; }

        /// <summary>
        /// Disposition instructions (e.g., "Use as-is for non-critical applications only").
        /// </summary>
        [StringLength(2000)]
        [Display(Name = "Disposition")]
        public string? Disposition { get; set; }

        // ----- Approval -----

        [Display(Name = "Requested By")]
        [StringLength(100)]
        public string? RequestedBy { get; set; }

        [Display(Name = "Requested At")]
        public DateTime? RequestedAtUtc { get; set; }

        [Display(Name = "Approved By")]
        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Approved At")]
        public DateTime? ApprovedAtUtc { get; set; }

        [Display(Name = "Rejected By")]
        [StringLength(100)]
        public string? RejectedBy { get; set; }

        [Display(Name = "Rejected At")]
        public DateTime? RejectedAtUtc { get; set; }

        [Display(Name = "Rejection Reason")]
        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        // ----- Audit -----

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        [StringLength(100)]
        public string? UpdatedBy { get; set; }

        // Concurrency token via Postgres xmin system column.
        // Mapped in AppDbContext via MapXminRowVersion (HARD LOCK from PR #365).
        public byte[]? RowVersion { get; set; }
    }
}
