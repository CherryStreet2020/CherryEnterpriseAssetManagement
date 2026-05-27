// Sprint 14.3 PR-5 (2026-05-27) — Customer Notice entity.
//
// OUTBOUND notification to customers when an engineering change,
// deviation, waiver, or concession impacts items they receive.
// Required by AS9100 §8.5.6 (customer notification of changes
// affecting product conformity) and IATF 16949 §8.5.6.1
// (notification of process changes).
//
// Unlike Concession (retroactive customer acceptance), a CustomerNotice
// is PROACTIVE — we are telling the customer about a change before or
// after it takes effect. The customer may need to acknowledge, and may
// dispute the change's impact on their deliverables.
//
// LIFECYCLE: Draft → Pending → Sent → Acknowledged → Closed
//                                   ↘ Disputed → Resolved → Closed
//                            ↘ Cancelled
//
// Ties into webhook outbox via OutboxCorrelationId for delivery tracking.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    /// <summary>
    /// Type of change being communicated to the customer.
    /// </summary>
    public enum CustomerNoticeType
    {
        EngineeringChange = 0,      // ECR/ECO-driven specification change
        Deviation = 1,              // Short-term exception to approved spec
        Waiver = 2,                 // Long-term divergence with prior customer approval
        Concession = 3,             // Retroactive acceptance of non-conforming material
        SpecificationChange = 4,    // Standalone spec change (no ECR/ECO required)
        ObsolescenceNotice = 5,     // Last-time-buy / end-of-life notification
        RecallNotice = 6,           // Product recall or safety alert
    }

    /// <summary>
    /// Status lifecycle for outbound customer notifications.
    /// </summary>
    public enum CustomerNoticeStatus
    {
        Draft = 0,
        Pending = 1,            // Approved for sending but not yet transmitted
        Sent = 2,               // Transmitted via configured delivery method
        Acknowledged = 3,       // Customer confirmed receipt and acceptance
        Disputed = 4,           // Customer challenged the change's impact
        Resolved = 5,           // Dispute resolved — re-enters Acknowledged flow
        Cancelled = 6,
        Closed = 7,             // Administrative close after acknowledgement
    }

    /// <summary>
    /// Delivery mechanism for outbound notifications.
    /// Shared between CustomerNotice and SupplierProcessChangeNotification.
    /// </summary>
    public enum NotificationDeliveryMethod
    {
        Email = 0,
        Portal = 1,             // Customer/supplier self-service portal
        Letter = 2,             // Formal letter (regulatory / contract requirement)
        Fax = 3,                // Legacy — still required by some defense primes
        Api = 4,                // Webhook / EDI / API push
    }

    [Table("CustomerNotices")]
    public class CustomerNotice
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        [Required] [StringLength(32)]
        public string NoticeNumber { get; set; } = string.Empty;

        [Required] [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public CustomerNoticeType Type { get; set; } = CustomerNoticeType.EngineeringChange;
        public CustomerNoticeStatus Status { get; set; } = CustomerNoticeStatus.Draft;

        // ----- Who is being notified -----
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [StringLength(200)]
        public string? CustomerContactName { get; set; }

        [StringLength(200)]
        public string? CustomerContactEmail { get; set; }

        // ----- What item is affected -----
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        // ----- Source of the change -----
        public int? OriginatingEcrId { get; set; }
        public EngineeringChangeRequest? OriginatingEcr { get; set; }
        public int? OriginatingDeviationId { get; set; }
        public Deviation? OriginatingDeviation { get; set; }
        public int? OriginatingWaiverId { get; set; }
        public Waiver? OriginatingWaiver { get; set; }
        public int? OriginatingConcessionId { get; set; }
        public Concession? OriginatingConcession { get; set; }

        // ----- Change detail -----
        [StringLength(4000)]
        public string? ChangeDescription { get; set; }

        [StringLength(2000)]
        public string? ImpactDescription { get; set; }

        /// <summary>Effective date of the engineering change.</summary>
        public DateTime? ChangeEffectiveDate { get; set; }

        // ----- Affected orders / contracts -----
        [StringLength(1000)]
        public string? AffectedSalesOrderReferences { get; set; }

        [StringLength(500)]
        public string? AffectedContractReferences { get; set; }

        // ----- Impact flags -----
        public bool AffectsForm { get; set; }
        public bool AffectsFit { get; set; }
        public bool AffectsFunction { get; set; }
        public bool SafetyImpact { get; set; }

        // ----- Delivery -----
        public NotificationDeliveryMethod DeliveryMethod { get; set; } = NotificationDeliveryMethod.Email;

        [StringLength(100)]
        public string? SentBy { get; set; }
        public DateTime? SentAtUtc { get; set; }

        // ----- Response tracking -----
        public DateTime? RequiredResponseDate { get; set; }

        [StringLength(200)]
        public string? CustomerRespondent { get; set; }
        public DateTime? CustomerResponseDateUtc { get; set; }

        [StringLength(4000)]
        public string? CustomerResponseText { get; set; }

        public DateTime? AcknowledgedAtUtc { get; set; }

        [StringLength(100)]
        public string? AcknowledgedBy { get; set; }

        // ----- Dispute handling -----
        [StringLength(2000)]
        public string? DisputeReason { get; set; }

        [StringLength(2000)]
        public string? DisputeResolution { get; set; }
        public DateTime? DisputeResolvedAtUtc { get; set; }

        [StringLength(100)]
        public string? DisputeResolvedBy { get; set; }

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
