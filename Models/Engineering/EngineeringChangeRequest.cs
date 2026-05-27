// Sprint 14.3 PR-1 (2026-05-27) — Engineering Change Request (ECR).
//
// The "we should change something" record. First step of the ECR/ECO Change
// Control workflow per AS9100 §8.5.6, NADCAP procurement requirements,
// FAA/EASA airworthiness change procedures, and standard mfg engineering
// discipline.
//
// LIFECYCLE: Draft → Submitted → UnderReview → Approved → (creates ECO)
//                                              ↘ Rejected
//                                              ↘ Cancelled
//
// WHAT THIS SUBSTRATE GIVES THE BUSINESS:
//   - A controlled "request for change" intake point. Anyone (engineer,
//     QA, customer liaison, supplier rep, production lead) can file an ECR.
//   - Form/Fit/Function impact assessment flags (AS9145 / AS9102 FAI
//     re-trigger criteria — any F/F/F change requires FAI re-baseline).
//   - Safety + customer + regulatory impact flags for escalation routing.
//   - Linkage to the affected Item / Document / ProductionOrder / Customer
//     so downstream impact analysis can walk the references.
//   - Audit trail: who requested, when submitted, who decided, why rejected.
//
// THIS PR-1 SHIPS:
//   - Entity (this file) + 4 enums (ChangeReason, ChangeUrgency, EcrStatus,
//     plus EcoStatus / EcoEffectivityType / EcoApprovalStatus / Disposition
//     on the ECO side).
//   - Tenant trio + xmin concurrency + enum HasDefaultValue applied
//     prophylactically per HARD LOCKS.
//
// FUTURE PRs (Sprint 14.3 PR-2/3/4):
//   - Deviation / Waiver / Concession (variant change types — short-term
//     exceptions to current released spec without going through full ECO).
//   - Customer Notice + Supplier PCN (downstream notification).
//   - CAR/CAPA (corrective + preventive action wrapper).
//   - Impact analysis service (walks chain: Item → DocumentVersion →
//     ProductionMaterialStructure snapshot → in-flight POs affected).
//   - Redline drawing markup tools.
//   - Closed-loop verification with FAI re-trigger (via IFaiService).
//
// REFERENCES:
//   - memory: feedback_b6_go_big_2026_05_26.md (Sprint 14.3 ~50-70h cascade)
//   - memory: feedback_xmin_pattern_for_concurrency_lock.md (applied)
//   - memory: feedback_b6_enum_defaults_must_match_model.md (applied)
//   - memory: feedback_no_fake_data.md (aerospace-realistic fixtures in tests)
//   - memory: reference_sprint_naming_no_vendor_implication.md (this is
//     ECR/ECO Change Control, NOT vendor PLM integration)
//   - Models/Engineering/Document.cs (sibling DMS substrate, integrates here
//     via EcoLineItem.AffectedDocumentVersionId + NewDocumentVersionId)
//   - Models/Production/ProductionMaterialStructure.cs (future consumer —
//     a snapshot pins the DocumentVersion in force at PRO release; an ECO
//     supersede then bumps in-flight snapshots' linked DocVersion via the
//     impact analysis service)

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Models.Engineering
{
    /// <summary>
    /// Why is this engineering change being requested? Drives routing +
    /// urgency defaults + analytics. AS9100 §8.5.6 categories + standard
    /// mfg practice.
    /// </summary>
    public enum ChangeReason
    {
        /// <summary>Customer-driven — OEM CCN, design directive, ECN cascade.</summary>
        CustomerRequest = 0,

        /// <summary>Design defect discovered post-release (in-house engineering).</summary>
        DesignDefect = 1,

        /// <summary>Manufacturability improvement — DFM optimization, cycle-time reduction.</summary>
        ManufacturabilityImprovement = 2,

        /// <summary>Cost reduction — VAVE, supplier consolidation, alt material.</summary>
        CostReduction = 3,

        /// <summary>Quality issue — escape, NCR root-cause, SCAR/CAR/CAPA driver.</summary>
        QualityIssue = 4,

        /// <summary>Regulatory compliance — FAA AD, EASA, REACH, RoHS, EAR/ITAR.</summary>
        RegulatoryCompliance = 5,

        /// <summary>Supplier issue — second-source qualification, obsolescence,
        /// supplier PCN cascade.</summary>
        SupplierIssue = 6,

        /// <summary>Component obsolescence — last-time-buy, LTB-EOL replacement.</summary>
        Obsolescence = 7,

        /// <summary>Other — free-form per Description + Notes.</summary>
        Other = 99,
    }

    /// <summary>
    /// How urgently must this change propagate? Drives approval-chain
    /// short-circuit rules + customer notification SLA.
    /// </summary>
    public enum ChangeUrgency
    {
        /// <summary>Routine — standard multi-stage approval, no rush.</summary>
        Routine = 0,

        /// <summary>Expedited — compressed approval window, prioritized routing.</summary>
        Expedited = 1,

        /// <summary>Emergency — single-stage approval allowed; flag to leadership.</summary>
        Emergency = 2,

        /// <summary>Stop-Ship — halts in-flight production AND shipments until resolved.</summary>
        StopShip = 3,
    }

    /// <summary>
    /// ECR lifecycle status. Drives which fields are editable + which
    /// service operations are legal.
    /// </summary>
    public enum EcrStatus
    {
        /// <summary>Draft — author working copy.</summary>
        Draft = 0,

        /// <summary>Submitted — out for triage / initial review.</summary>
        Submitted = 1,

        /// <summary>Under review — assigned reviewer is actively assessing.</summary>
        UnderReview = 2,

        /// <summary>Approved — accepted; an ECO has been (or will be) created.</summary>
        Approved = 3,

        /// <summary>Rejected — declined with reason. Terminal.</summary>
        Rejected = 4,

        /// <summary>Cancelled — withdrawn by requester before decision. Terminal.</summary>
        Cancelled = 5,
    }

    /// <summary>
    /// Engineering Change Request — the controlled intake point for any
    /// proposed engineering change. Lifecycle Draft → Submitted →
    /// UnderReview → Approved (creates ECO) / Rejected / Cancelled.
    /// </summary>
    [Table("EngineeringChangeRequests")]
    public class EngineeringChangeRequest
    {
        public int Id { get; set; }

        // ===== Tenant trio =================================================

        [Required]
        public int CompanyId { get; set; }

        public int? LocationId { get; set; }

        // ===== Identity ====================================================

        /// <summary>
        /// Human-facing identifier (e.g., "ECR-2026-00042"). UNIQUE per
        /// (CompanyId, EcrNumber).
        /// </summary>
        [Required, StringLength(50)]
        [Display(Name = "ECR Number")]
        public string EcrNumber { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(4000)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "Change Reason")]
        public ChangeReason ChangeReason { get; set; } = ChangeReason.Other;

        [Required]
        [Display(Name = "Urgency")]
        public ChangeUrgency Urgency { get; set; } = ChangeUrgency.Routine;

        [Required]
        public EcrStatus Status { get; set; } = EcrStatus.Draft;

        // ===== Impact assessment (AS9145 FAI re-trigger criteria) =========

        /// <summary>Affects FORM (geometric envelope, dimensions, appearance) — triggers FAI per AS9102.</summary>
        [Display(Name = "Affects Form")]
        public bool AffectsForm { get; set; } = false;

        /// <summary>Affects FIT (interface, mating, assembly clearance) — triggers FAI per AS9102.</summary>
        [Display(Name = "Affects Fit")]
        public bool AffectsFit { get; set; } = false;

        /// <summary>Affects FUNCTION (performance, behavior, output) — triggers FAI per AS9102.</summary>
        [Display(Name = "Affects Function")]
        public bool AffectsFunction { get; set; } = false;

        /// <summary>Safety-impacting — AS9100 §8.3 / FAA Airworthiness / regulatory escalation required.</summary>
        [Display(Name = "Affects Safety")]
        public bool AffectsSafety { get; set; } = false;

        /// <summary>Customer notification required (OEM CCN, contract clause).</summary>
        [Display(Name = "Affects Customers")]
        public bool AffectsCustomers { get; set; } = false;

        /// <summary>Regulatory body notification required (FAA, EASA, FDA, etc.).</summary>
        [Display(Name = "Affects Regulatory")]
        public bool AffectsRegulatory { get; set; } = false;

        // ===== Linkage to affected things =================================

        public int? LinkedItemId { get; set; }
        public Item? LinkedItem { get; set; }

        public int? LinkedDocumentId { get; set; }
        public Document? LinkedDocument { get; set; }

        public int? LinkedProductionOrderId { get; set; }
        public ProductionOrder? LinkedProductionOrder { get; set; }

        public int? LinkedCustomerId { get; set; }
        public Customer? LinkedCustomer { get; set; }

        /// <summary>
        /// FK to the resulting ECO when Status=Approved. SET NULL on ECO
        /// delete preserves ECR history independent of ECO lifecycle.
        /// </summary>
        public int? ResultingEcoId { get; set; }
        public EngineeringChangeOrder? ResultingEco { get; set; }

        // ===== Lifecycle stamps ===========================================

        [StringLength(100)]
        [Display(Name = "Requested By")]
        public string? RequestedBy { get; set; }

        public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

        [Display(Name = "Submitted At")]
        public DateTime? SubmittedAtUtc { get; set; }

        [Display(Name = "Reviewed At")]
        public DateTime? ReviewedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Reviewed By")]
        public string? ReviewedBy { get; set; }

        [Display(Name = "Decided At")]
        public DateTime? DecidedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Decided By")]
        public string? DecidedBy { get; set; }

        [StringLength(2000)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        // ===== Audit + concurrency ========================================

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
