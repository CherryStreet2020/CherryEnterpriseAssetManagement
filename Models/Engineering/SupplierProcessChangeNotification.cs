// Sprint 14.3 PR-5 (2026-05-27) — Supplier Process Change Notification entity.
//
// OUTBOUND notification to suppliers when an engineering change or
// specification change affects purchased components they supply.
// The supplier must acknowledge, assess impact, and potentially
// re-qualify through FAI / PPAP before the change takes effect.
//
// AS9100 §8.4.3: "The organization shall communicate applicable
// requirements to external providers." IATF 16949 §8.5.6.1:
// "The organization shall notify the customer of any planned
// changes to the realization of the product, and obtain customer
// approval before implementing the change."
//
// PCN flow in practice:
//   1. Internal ECR/ECO drives a spec change on a purchased component
//   2. PCN generated and sent to the affected supplier(s)
//   3. Supplier acknowledges receipt and provides impact assessment
//   4. Engineering reviews impact assessment → Approve / Reject
//   5. If approved: supplier implements change, may require FAI/PPAP
//   6. Close PCN after first conforming shipment verified
//
// LIFECYCLE: Draft → Pending → SentToSupplier → SupplierAcknowledged
//            → ImpactAssessmentReceived → Approved → Closed
//                                       ↘ Rejected → (supplier reworks proposal)
//                                    ↘ Cancelled

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    /// <summary>
    /// Nature of the change communicated to the supplier.
    /// </summary>
    public enum PcnType
    {
        ProcessChange = 0,          // Manufacturing process modification
        MaterialChange = 1,         // Raw material or alloy substitution
        SubcontractorChange = 2,    // Supplier changing their sub-tier
        ToolingChange = 3,          // Tooling replacement or modification
        TestMethodChange = 4,       // Inspection / test procedure change
        LocationChange = 5,         // Manufacturing site relocation
        DesignChange = 6,           // Drawing / spec revision on purchased part
        PackagingChange = 7,        // Packaging / preservation change
    }

    /// <summary>
    /// Status lifecycle for supplier PCN tracking.
    /// </summary>
    public enum PcnStatus
    {
        Draft = 0,
        Pending = 1,                    // Approved for sending but not yet transmitted
        SentToSupplier = 2,             // Transmitted via configured delivery method
        SupplierAcknowledged = 3,       // Supplier confirmed receipt
        ImpactAssessmentReceived = 4,   // Supplier returned formal impact analysis
        Approved = 5,                   // Engineering approved the supplier's response
        Rejected = 6,                   // Engineering rejected — supplier must rework
        Cancelled = 7,
        Closed = 8,                     // Change implemented + first conforming shipment verified
    }

    [Table("SupplierProcessChangeNotifications")]
    public class SupplierProcessChangeNotification
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        [Required] [StringLength(32)]
        public string PcnNumber { get; set; } = string.Empty;

        [Required] [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public PcnType Type { get; set; } = PcnType.ProcessChange;
        public PcnStatus Status { get; set; } = PcnStatus.Draft;

        // ----- Who is being notified -----
        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        [StringLength(200)]
        public string? SupplierContactName { get; set; }

        [StringLength(200)]
        public string? SupplierContactEmail { get; set; }

        // ----- What item is affected -----
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        // ----- Source of the change -----
        public int? OriginatingEcrId { get; set; }
        public EngineeringChangeRequest? OriginatingEcr { get; set; }

        // ----- Change detail -----
        [StringLength(4000)]
        public string? ChangeDescription { get; set; }

        [StringLength(4000)]
        public string? ImpactDescription { get; set; }

        /// <summary>Proposed effective date for the change at the supplier.</summary>
        public DateTime? ProposedEffectiveDate { get; set; }

        /// <summary>Current specification / drawing revision.</summary>
        [StringLength(200)]
        public string? CurrentSpecification { get; set; }

        /// <summary>New specification / drawing revision after change.</summary>
        [StringLength(200)]
        public string? ProposedSpecification { get; set; }

        // ----- Impact flags -----
        public bool AffectsForm { get; set; }
        public bool AffectsFit { get; set; }
        public bool AffectsFunction { get; set; }
        public bool SafetyImpact { get; set; }

        // ----- Qualification requirements -----
        /// <summary>Does this change require First Article Inspection?</summary>
        public bool FirstArticleRequired { get; set; }

        /// <summary>Does this change require PPAP (Production Part Approval Process)?</summary>
        public bool PpapRequired { get; set; }

        /// <summary>Does the supplier quality plan need updating?</summary>
        public bool QualityPlanUpdateRequired { get; set; }

        /// <summary>Number of sample parts required for re-qualification.</summary>
        public int? SampleQuantityRequired { get; set; }

        // ----- Delivery -----
        public NotificationDeliveryMethod DeliveryMethod { get; set; } = NotificationDeliveryMethod.Email;

        [StringLength(100)]
        public string? SentBy { get; set; }
        public DateTime? SentAtUtc { get; set; }

        // ----- Response tracking -----
        public DateTime? RequiredResponseDate { get; set; }

        [StringLength(200)]
        public string? SupplierRespondent { get; set; }
        public DateTime? SupplierResponseDateUtc { get; set; }

        [StringLength(4000)]
        public string? SupplierResponseText { get; set; }

        public DateTime? SupplierAcknowledgedAtUtc { get; set; }

        // ----- Impact assessment -----
        [StringLength(4000)]
        public string? SupplierImpactAssessment { get; set; }

        /// <summary>Supplier-estimated cost impact (positive = increase).</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? SupplierEstimatedCostImpact { get; set; }

        /// <summary>Supplier-estimated lead time impact in days (positive = longer).</summary>
        public int? SupplierEstimatedLeadTimeImpactDays { get; set; }

        public DateTime? ImpactAssessmentReceivedAtUtc { get; set; }

        // ----- Approval -----
        public bool ApprovalRequired { get; set; } = true;

        [StringLength(100)]
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }

        [StringLength(100)]
        public string? RejectedBy { get; set; }
        public DateTime? RejectedAtUtc { get; set; }

        [StringLength(1000)]
        public string? RejectionReason { get; set; }

        // ----- Verification -----
        /// <summary>First conforming shipment reference (closes the PCN).</summary>
        [StringLength(100)]
        public string? FirstConformingShipmentRef { get; set; }
        public DateTime? VerifiedAtUtc { get; set; }

        [StringLength(100)]
        public string? VerifiedBy { get; set; }

        // ----- Webhook outbox tie-in -----
        [StringLength(64)]
        public string? OutboxCorrelationId { get; set; }

        // ----- Audit -----
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [StringLength(100)] public string? CreatedBy { get; set; }
        [StringLength(100)] public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
