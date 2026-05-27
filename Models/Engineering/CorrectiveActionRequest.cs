// Sprint 14.3 PR-6 (2026-05-27) — Corrective Action Request (CAR) entity.
//
// FORMAL request for corrective action when a non-conformance,
// customer complaint, audit finding, or process failure is identified.
// The CAR initiates the investigation and drives a CAPA (Corrective
// and Preventive Action) to resolution.
//
// AS9100 §10.2: "The organization shall take action to eliminate the
// cause of nonconformities to prevent recurrence." This is the
// TRIGGER entity — the CAR says "we have a problem"; the CAPA
// entity (paired) says "here's how we fix it and prevent it."
//
// LIFECYCLE: Draft → Issued → UnderInvestigation → RootCauseIdentified
//            → CorrectiveActionPlanned → ImplementationInProgress
//            → VerificationPending → Closed
//            Draft → Cancelled
//
// Industry context: CAR/CAPA is the single most audited process in
// aerospace (AS9100/AS9110/AS9120), automotive (IATF 16949),
// medical devices (ISO 13485), and defense (MIL-STD-1520). Every
// quality audit opens here. SAP QM, Oracle Quality, Plex CAPA,
// ETQ Reliance — all center on this workflow.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    /// <summary>
    /// Source/trigger category for the corrective action request.
    /// </summary>
    public enum CarSource
    {
        InternalAudit = 0,          // Internal quality audit finding
        ExternalAudit = 1,          // Customer/registrar/regulatory audit finding
        CustomerComplaint = 2,      // Customer-reported problem
        SupplierNonConformance = 3, // Incoming inspection failure
        ProcessFailure = 4,         // Internal process deviation detected
        FieldFailure = 5,           // In-service failure report
        InspectionRejection = 6,    // Final inspection / FAI rejection
        WarrantyReturn = 7,         // Warranty claim return
        SafetyIncident = 8,         // Safety-related event
        RegulatoryNotice = 9,       // Government/regulatory authority notice
    }

    /// <summary>
    /// Severity classification for the corrective action request.
    /// </summary>
    public enum CarSeverity
    {
        Minor = 0,          // Non-critical, no product impact
        Major = 1,          // Product impact, customer affected
        Critical = 2,       // Safety/airworthiness/regulatory impact
    }

    /// <summary>
    /// Lifecycle status for corrective action requests.
    /// </summary>
    public enum CarStatus
    {
        Draft = 0,
        Issued = 1,                         // CAR formally issued
        UnderInvestigation = 2,             // Root cause investigation in progress
        RootCauseIdentified = 3,            // Root cause documented
        CorrectiveActionPlanned = 4,        // Fix plan approved
        ImplementationInProgress = 5,       // Fix being implemented
        VerificationPending = 6,            // Fix implemented, awaiting effectiveness verification
        Closed = 7,                         // Effectiveness verified, CAR closed
        Cancelled = 8,
    }

    [Table("CorrectiveActionRequests")]
    public class CorrectiveActionRequest
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        [Required] [StringLength(32)]
        public string CarNumber { get; set; } = string.Empty;

        [Required] [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        public CarSource Source { get; set; } = CarSource.InternalAudit;
        public CarSeverity Severity { get; set; } = CarSeverity.Minor;
        public CarStatus Status { get; set; } = CarStatus.Draft;

        // ----- What is affected -----
        public int? ItemId { get; set; }
        public Item? Item { get; set; }
        public int? ProductionOrderId { get; set; }
        public Production.ProductionOrder? ProductionOrder { get; set; }
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public int? VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        // ----- Linkage to other quality records -----
        public int? OriginatingEcrId { get; set; }
        public EngineeringChangeRequest? OriginatingEcr { get; set; }
        public int? RelatedDeviationId { get; set; }
        public Deviation? RelatedDeviation { get; set; }
        public int? RelatedConcessionId { get; set; }
        public Concession? RelatedConcession { get; set; }

        /// <summary>NCR number that triggered this CAR.</summary>
        [StringLength(50)]
        public string? NcrReference { get; set; }

        /// <summary>Customer complaint reference number.</summary>
        [StringLength(100)]
        public string? CustomerComplaintReference { get; set; }

        /// <summary>Audit finding reference.</summary>
        [StringLength(100)]
        public string? AuditFindingReference { get; set; }

        // ----- Non-conformance detail -----
        [StringLength(4000)]
        public string? NonConformanceDescription { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal? AffectedQuantity { get; set; }

        [StringLength(500)]
        public string? AffectedLotSerials { get; set; }

        // ----- Impact flags -----
        public bool AffectsForm { get; set; }
        public bool AffectsFit { get; set; }
        public bool AffectsFunction { get; set; }
        public bool SafetyImpact { get; set; }
        public bool RegulatoryImpact { get; set; }

        // ----- Root cause -----
        [StringLength(4000)]
        public string? RootCauseAnalysis { get; set; }

        /// <summary>Root cause methodology (5-Why, Fishbone, 8D, FMEA, etc.).</summary>
        [StringLength(100)]
        public string? RootCauseMethodology { get; set; }

        public DateTime? RootCauseIdentifiedAtUtc { get; set; }

        [StringLength(100)]
        public string? RootCauseIdentifiedBy { get; set; }

        // ----- Containment (immediate action) -----
        [StringLength(4000)]
        public string? ContainmentAction { get; set; }

        public DateTime? ContainmentCompletedAtUtc { get; set; }

        // ----- Corrective action plan -----
        [StringLength(4000)]
        public string? CorrectiveActionPlan { get; set; }

        public DateTime? CorrectiveActionDueDate { get; set; }

        [StringLength(4000)]
        public string? PreventiveActionPlan { get; set; }

        // ----- Implementation -----
        [StringLength(4000)]
        public string? ImplementationNotes { get; set; }

        public DateTime? ImplementationCompletedAtUtc { get; set; }

        [StringLength(100)]
        public string? ImplementedBy { get; set; }

        // ----- Effectiveness verification -----
        [StringLength(4000)]
        public string? VerificationMethod { get; set; }

        [StringLength(4000)]
        public string? VerificationResults { get; set; }

        public DateTime? VerifiedAtUtc { get; set; }

        [StringLength(100)]
        public string? VerifiedBy { get; set; }

        /// <summary>True if the corrective action was verified effective.</summary>
        public bool? VerificationEffective { get; set; }

        // ----- Closure -----
        public DateTime? ClosedAtUtc { get; set; }

        [StringLength(100)]
        public string? ClosedBy { get; set; }

        /// <summary>Days from issue to close (computed at closure).</summary>
        public int? DaysToClose { get; set; }

        // ----- Ownership -----
        [StringLength(100)]
        public string? IssuedBy { get; set; }

        public DateTime? IssuedAtUtc { get; set; }

        [StringLength(100)]
        public string? AssignedTo { get; set; }

        [StringLength(100)]
        public string? ResponsibleDepartment { get; set; }

        // ----- Audit -----
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [StringLength(100)] public string? CreatedBy { get; set; }
        [StringLength(100)] public string? UpdatedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
