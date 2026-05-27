// Sprint 14.3 PR-3 (2026-05-27) — Waiver entity.
//
// A LONGER-TERM customer-approved divergence from released specification.
// Unlike a Deviation (short-term, qty/date limited), a Waiver represents
// a formal customer agreement to accept a known non-conformance across
// multiple production runs or an extended time period.
//
// DISTINCTION:
//   - Deviation = short-term exception, internally authorized, qty/date limited.
//   - Waiver = customer-approved ongoing acceptance of a known condition.
//   - Concession (PR-4) = retroactive customer acceptance of already-produced
//     non-conforming material.
//
// LIFECYCLE: Draft → Submitted → CustomerReview → Approved → Active → Expired
//                                                 ↘ Rejected
//                                                 ↘ Cancelled
//            Active → Revoked (customer withdraws approval)
//            Active → Expired (date or qty limit reached)
//
// REGULATORY: AS9100 §8.7.1 requires customer approval for disposition of
// non-conforming product. Waivers are the formal mechanism. Must be traceable
// to the customer PO/contract reference and the approving authority.
//
// MODELED AFTER: SAP QM Customer Waiver / Oracle PLM Waiver / Boeing D6-82479
// Supplier Waiver process / Airbus AIMS Waiver. Design-pattern references only.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    public enum WaiverType
    {
        Material = 0,       // Non-standard material grade/alloy/supplier
        Process = 1,        // Non-standard process or process parameter
        Dimensional = 2,    // Out-of-tolerance dimensional condition
        Finish = 3,         // Surface finish/coating/plating deviation
        Testing = 4,        // Reduced or alternate testing requirements
        Documentation = 5,  // Missing or substitute documentation accepted
    }

    public enum WaiverStatus
    {
        Draft = 0,
        Submitted = 1,
        CustomerReview = 2,
        Approved = 3,
        Active = 4,
        Expired = 5,
        Rejected = 6,
        Cancelled = 7,
        Revoked = 8,        // Customer withdrew approval
    }

    [Table("Waivers")]
    public class Waiver
    {
        public int Id { get; set; }

        // ----- Tenant trio -----
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        // ----- Identity -----
        [Required]
        [StringLength(32)]
        [Display(Name = "Waiver #")]
        public string WaiverNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public WaiverType Type { get; set; } = WaiverType.Material;
        public WaiverStatus Status { get; set; } = WaiverStatus.Draft;

        // ----- Scope -----
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        public int? ProductionOrderId { get; set; }
        public Production.ProductionOrder? ProductionOrder { get; set; }

        public int? OriginatingEcrId { get; set; }
        public EngineeringChangeRequest? OriginatingEcr { get; set; }

        /// <summary>Related Deviation that escalated to a Waiver.</summary>
        public int? RelatedDeviationId { get; set; }
        public Deviation? RelatedDeviation { get; set; }

        // ----- Customer approval (the key differentiator from Deviation) -----
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [StringLength(100)]
        [Display(Name = "Customer PO/Contract Reference")]
        public string? CustomerContractReference { get; set; }

        [StringLength(200)]
        [Display(Name = "Customer Approving Authority")]
        public string? CustomerApprovingAuthority { get; set; }

        [Display(Name = "Customer Approval Date")]
        public DateTime? CustomerApprovalDateUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Customer Approval Document #")]
        public string? CustomerApprovalDocumentNumber { get; set; }

        // ----- Limits -----
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MaxQuantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal ConsumedQuantity { get; set; } = 0;

        public DateTime? EffectiveFromUtc { get; set; }
        public DateTime? ExpirationDateUtc { get; set; }
        public int? MaxProductionOrders { get; set; }
        public int ConsumedProductionOrders { get; set; } = 0;

        // ----- Impact flags (mirrors ECR/Deviation pattern) -----
        public bool AffectsForm { get; set; }
        public bool AffectsFit { get; set; }
        public bool AffectsFunction { get; set; }
        public bool SafetyImpact { get; set; }

        // ----- Spec detail -----
        [StringLength(1000)]
        public string? OriginalSpecification { get; set; }

        [StringLength(1000)]
        public string? WaivedCondition { get; set; }

        [StringLength(4000)]
        public string? Justification { get; set; }

        [StringLength(2000)]
        public string? Disposition { get; set; }

        // ----- Internal approval -----
        [StringLength(100)] public string? RequestedBy { get; set; }
        public DateTime? RequestedAtUtc { get; set; }
        [StringLength(100)] public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        [StringLength(100)] public string? RejectedBy { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        [StringLength(1000)] public string? RejectionReason { get; set; }
        [StringLength(100)] public string? RevokedBy { get; set; }
        public DateTime? RevokedAtUtc { get; set; }
        [StringLength(1000)] public string? RevocationReason { get; set; }

        // ----- Audit -----
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [StringLength(100)] public string? CreatedBy { get; set; }
        [StringLength(100)] public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
