// Theme B9 Wave 6 PR-15 (2026-05-30) — Project change control. OPENS Wave 6.
//
// ProjectChangeRequest is the intake + impact-analysis + approval stage that
// sits UPSTREAM of the existing ProjectAmendment (the customer-facing change
// ORDER / contract amendment). Research §11: "a full customer project module
// without change orders will fail." This wires the disciplined request→order
// path every BIC PSA/ETO system (Acumatica Change Requests→Change Orders,
// SAP PS claim/change, Primavera change documents) has and SAP/Epicor make
// painful.
//
// DESIGN (per the B9 handoff ruling): EXTEND ProjectAmendment, don't rebuild.
//   - ProjectChangeRequest = the new intake/proposal/impact/disposition entity.
//   - ProjectAmendment      = the change ORDER (already has AmendmentNumber,
//     ValueDelta → EffectiveContractValue, the Draft→Submitted→Approved flow).
//   - On approval, the change request is CONVERTED into a ProjectAmendment and
//     the two are cross-linked (ResultingProjectAmendmentId here ↔
//     ProjectAmendment.SourceChangeRequestId there). The §20 gate lives in
//     IProjectChangeService.ConvertToChangeOrderAsync: you cannot apply a
//     customer scope change before its required approval(s) clear.
//
// Conventions (ProjectBilling/ProjectFinancials precedent): tenant-scoped
// THROUGH the parent project (no CompanyId); CASCADE from the project; the
// optional WBS-phase peg is SET NULL (single cascade path project→child); xmin
// concurrency; every enum's 0 member is the CLR/model default == DB default.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectChangeRequest — a proposed change to a customer project, with
    // impact analysis and an approval/disposition workflow. Approving (and
    // converting) it produces a ProjectAmendment (the change order).
    // =====================================================================
    [Table("ProjectChangeRequests")]
    public class ProjectChangeRequest
    {
        public int Id { get; set; }

        // Parent project. CASCADE — change requests are intrinsic to the project.
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Per-project monotonic sequence (1, 2, 3 ...). Human-readable
        // ("CR #3"). NEVER a GUID (research anti-pattern). Service computes
        // MAX(ChangeRequestNumber)+1 inside a row-level lock on the project.
        public int ChangeRequestNumber { get; set; }

        // Short headline ("Customer adds 4th actuator + Ti upgrade"). NOTE:
        // Title is upper-cased by the SaveChanges normalizer (consistent with
        // ProjectContract.Title / ProjectEstimate.Title) — keep it terse.
        [StringLength(200)]
        public string? Title { get; set; }

        // Where the change originated. Drives required-approval defaults.
        public ProjectChangeSource Source { get; set; } = ProjectChangeSource.Customer;

        // The contractual nature of the change (research §11 change-order types).
        public ProjectChangeCategory Category { get; set; } = ProjectChangeCategory.CustomerScope;

        // Disposition / workflow state. Terminal states (Rejected / Cancelled /
        // Converted) have no outgoing transition in the service legal-map.
        public ProjectChangeRequestStatus Status { get; set; } = ProjectChangeRequestStatus.Draft;

        // Who asked for it (snapshot name; case preserved via "requestedby").
        [StringLength(120)]
        public string? RequestedByName { get; set; }

        [DataType(DataType.Date)]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        // Long-form narrative of the proposed change (case preserved).
        public string? Description { get; set; }

        // -----------------------------------------------------------------
        // Impact analysis (the "Estimate change" action). Money defaults to
        // the PARENT project currency (never hard-coded USD). CostImpact and
        // RevenueImpact may be negative (de-scope / credit).
        // -----------------------------------------------------------------
        [Column(TypeName = "decimal(18,4)")]
        public decimal CostImpact { get; set; } = 0m;

        [Column(TypeName = "decimal(18,4)")]
        public decimal RevenueImpact { get; set; } = 0m;

        // Margin impact in percentage POINTS (e.g. +2.5 / -1.0). Nullable =
        // not yet estimated.
        public decimal? MarginImpactPct { get; set; }

        // Schedule impact in DAYS (+ slips later, − pulls in). Nullable = none.
        public int? ScheduleImpactDays { get; set; }

        public ProjectChangeRiskLevel RiskImpact { get; set; } = ProjectChangeRiskLevel.None;

        // Free-text impact narrative — scope/affected-jobs/affected-POs prose.
        // Case preserved via the "impactnarrative" normalizer token.
        public string? ImpactNarrative { get; set; }

        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        // Optional WBS peg — the affected phase. SET NULL (the project is the
        // single cascade owner). Service tenant-scopes this to the project.
        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }

        // -----------------------------------------------------------------
        // Approval routing. RequiresInternalApproval / RequiresCustomerApproval
        // gate the workflow; the set-once stamps below record the sign-offs.
        // -----------------------------------------------------------------
        public bool RequiresInternalApproval { get; set; } = true;
        public bool RequiresCustomerApproval { get; set; } = true;

        public DateTime? InternalApprovedAt { get; set; }
        [StringLength(120)]
        public string? InternalApprovedBy { get; set; }   // case preserved ("approvedby")

        public DateTime? SubmittedToCustomerAt { get; set; }

        public DateTime? CustomerApprovedAt { get; set; }
        [StringLength(120)]
        public string? CustomerApprovedBy { get; set; }    // case preserved ("approvedby")

        public DateTime? RejectedAt { get; set; }
        [StringLength(120)]
        public string? RejectedBy { get; set; }            // case preserved ("rejectedby")

        // Customer's own change-order reference + PO revision (codes — upper-cased).
        [StringLength(100)]
        public string? CustomerReference { get; set; }
        [StringLength(60)]
        public string? CustomerPoRevision { get; set; }

        // How the change flows into billing / cost once applied.
        public ProjectChangeBillingTreatment BillingTreatment { get; set; } = ProjectChangeBillingTreatment.None;
        public ProjectChangeCostTreatment CostTreatment { get; set; } = ProjectChangeCostTreatment.None;

        // -----------------------------------------------------------------
        // Conversion link — the change ORDER (ProjectAmendment) this request
        // became when approved+converted. Set once by the service.
        // -----------------------------------------------------------------
        public long? ResultingProjectAmendmentId { get; set; }
        public ProjectAmendment? ResultingProjectAmendment { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums — the 0 member is the CLR/model default (== the DB default).
    // ---------------------------------------------------------------------

    public enum ProjectChangeRequestStatus : short
    {
        Draft = 0,                 // service creates here
        UnderReview = 1,           // routed for internal review / estimation
        Estimated = 2,             // impact analysis complete
        InternalApproved = 3,      // internal sign-off recorded
        SubmittedToCustomer = 4,   // sent to the customer for approval
        CustomerApproved = 5,      // customer accepted — eligible to convert
        Rejected = 6,              // TERMINAL
        Cancelled = 7,             // TERMINAL (withdrawn before disposition)
        Converted = 8              // TERMINAL — became a ProjectAmendment (change order)
    }

    public enum ProjectChangeSource : short
    {
        Customer = 0,
        Internal = 1,
        Engineering = 2,
        Supplier = 3,
        Field = 4
    }

    // Research §11 change-order types.
    public enum ProjectChangeCategory : short
    {
        CustomerScope = 0,
        Engineering = 1,
        Schedule = 2,
        Quantity = 3,
        Price = 4,
        Specification = 5,
        DrawingRevision = 6,
        MaterialSubstitution = 7,
        SupplierDriven = 8,
        InternalCostOnly = 9,
        Warranty = 10,
        Rework = 11,
        Field = 12,
        ContractAmendment = 13
    }

    public enum ProjectChangeRiskLevel : short
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum ProjectChangeBillingTreatment : short
    {
        None = 0,
        AddToContract = 1,    // increase the contract value (default for scope adds)
        SeparateInvoice = 2,  // bill as a standalone invoice
        NoCharge = 3,         // absorbed, no customer charge
        Credit = 4            // customer credit / de-scope
    }

    public enum ProjectChangeCostTreatment : short
    {
        None = 0,
        AddToBudget = 1,      // fold into the project budget
        SeparateBudget = 2,   // track in a separate control account
        Absorbed = 3          // absorbed into existing budget
    }
}
