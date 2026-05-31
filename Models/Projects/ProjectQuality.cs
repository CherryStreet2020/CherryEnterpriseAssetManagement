// Theme B9 Wave 6 PR-17 (2026-05-31) — Project quality + acceptance.
//
// ProjectInspection / ProjectNCR / ProjectMRB / ProjectPunchItem /
// ProjectAcceptance (research §14). The quality gate that AS9100 / NADCAP /
// AS9102 shops live by, connected to the project so the §22.4 rule —
// "cannot ship/accept with a blocking NCR / pending MRB / blocking punch item"
// — is enforced at acceptance, and a customer Acceptance with a revenue trigger
// flips the PR-14 billing AcceptanceConfirmed gate (the real entity that the
// #468 set-once placeholder flag was standing in for).
//
// Conventions (ProjectGovernance precedent): tenant-scoped THROUGH the project
// (no CompanyId); CASCADE from the project; cross-entity pegs (phase / NCR /
// MRB / inspection) SET NULL = one cascade path per row; xmin; enum 0 member ==
// model/DB default; per-project monotonic numbers (service MAX+1). Reuses
// ProjectPriority (defined in ProjectGovernance.cs).

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectInspection — an inspection event (FAI / in-process / final / FAT /
    // SAT / receiving / customer-witness) with a result + accept/reject counts.
    // =====================================================================
    [Table("ProjectInspections")]
    public class ProjectInspection
    {
        public int Id { get; set; }
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }
        public int InspectionNumber { get; set; }

        [StringLength(200)] public string? Title { get; set; }
        public ProjectInspectionType InspectionType { get; set; } = ProjectInspectionType.InProcess;
        public ProjectInspectionResult Result { get; set; } = ProjectInspectionResult.Pending;

        public DateTime? InspectionDate { get; set; }
        [StringLength(120)] public string? Inspector { get; set; }

        public int QuantityInspected { get; set; } = 0;
        public int QuantityAccepted { get; set; } = 0;
        public int QuantityRejected { get; set; } = 0;

        [StringLength(200)] public string? ReportReference { get; set; }

        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }

        [StringLength(2000)] public string? Notes { get; set; }

        public DateTime? CompletedAt { get; set; }
        [StringLength(120)] public string? CompletedBy { get; set; }   // case preserved ("completedby")

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectNCR — a nonconformance report. Blocks shipment/acceptance until
    // dispositioned + closed (BlocksShipment). May escalate to an MRB.
    // =====================================================================
    [Table("ProjectNCRs")]
    public class ProjectNCR
    {
        public int Id { get; set; }
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }
        public int NcrNumber { get; set; }

        [StringLength(200)] public string? Title { get; set; }
        public string? Description { get; set; }

        public ProjectNcrSource Source { get; set; } = ProjectNcrSource.Internal;
        public ProjectNcrSeverity Severity { get; set; } = ProjectNcrSeverity.Minor;

        public DateTime? DetectedDate { get; set; }
        public int QuantityAffected { get; set; } = 0;

        public string? ContainmentAction { get; set; }
        public ProjectQualityDisposition Disposition { get; set; } = ProjectQualityDisposition.Pending;
        public string? RootCause { get; set; }
        public string? CorrectiveAction { get; set; }

        public ProjectNcrStatus Status { get; set; } = ProjectNcrStatus.Open;
        // A blocking NCR stops shipment/acceptance until it is Closed.
        public bool BlocksShipment { get; set; } = true;

        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }
        public int? LinkedInspectionId { get; set; }
        public ProjectInspection? LinkedInspection { get; set; }

        [StringLength(2000)] public string? Notes { get; set; }

        public DateTime? ClosedAt { get; set; }
        [StringLength(120)] public string? ClosedBy { get; set; }   // case preserved ("closedby")

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectMRB — material review board record dispositioning an NCR. A
    // Pending MRB blocks shipment/acceptance.
    // =====================================================================
    [Table("ProjectMRBs")]
    public class ProjectMRB
    {
        public int Id { get; set; }
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }
        public int MrbNumber { get; set; }

        [StringLength(200)] public string? Title { get; set; }

        // The NCR this board is reviewing (SET NULL).
        public int? LinkedNcrId { get; set; }
        public ProjectNCR? LinkedNcr { get; set; }

        [StringLength(300)] public string? BoardMembers { get; set; }
        public DateTime? ReviewDate { get; set; }

        public ProjectQualityDisposition Disposition { get; set; } = ProjectQualityDisposition.Pending;
        public string? Justification { get; set; }

        public ProjectMrbStatus Status { get; set; } = ProjectMrbStatus.Pending;

        public bool CustomerApprovalRequired { get; set; } = false;
        public bool CustomerApproved { get; set; } = false;

        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }

        [StringLength(2000)] public string? Notes { get; set; }

        public DateTime? ClosedAt { get; set; }
        [StringLength(120)] public string? ClosedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectPunchItem — a punch-list item with blocking flags for shipment /
    // invoice / acceptance.
    // =====================================================================
    [Table("ProjectPunchItems")]
    public class ProjectPunchItem
    {
        public int Id { get; set; }
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }
        public int PunchNumber { get; set; }

        [StringLength(200)] public string? Title { get; set; }
        [StringLength(120)] public string? Source { get; set; }
        public string? Description { get; set; }

        public ProjectPriority Priority { get; set; } = ProjectPriority.Low;
        [StringLength(120)] public string? Owner { get; set; }
        [DataType(DataType.Date)] public DateTime? DueDate { get; set; }

        public ProjectPunchStatus Status { get; set; } = ProjectPunchStatus.Open;

        public bool CustomerVisible { get; set; } = false;
        public bool BlockingShipment { get; set; } = false;
        public bool BlockingInvoice { get; set; } = false;
        public bool BlockingAcceptance { get; set; } = false;

        public string? CorrectiveAction { get; set; }
        [StringLength(500)] public string? CompletionEvidence { get; set; }

        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }

        [StringLength(2000)] public string? Notes { get; set; }

        public DateTime? ClosedAt { get; set; }
        [StringLength(120)] public string? ClosedBy { get; set; }
        [DataType(DataType.Date)] public DateTime? ClosedDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectAcceptance — a customer (or internal/FAT/SAT) acceptance event.
    // Confirming it is gated on no open blocking NCR / pending MRB / blocking
    // punch item; a RevenueTrigger acceptance flips the PR-14 billing
    // AcceptanceConfirmed gate.
    // =====================================================================
    [Table("ProjectAcceptances")]
    public class ProjectAcceptance
    {
        public int Id { get; set; }
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }
        public int AcceptanceNumber { get; set; }

        public ProjectAcceptanceType AcceptanceType { get; set; } = ProjectAcceptanceType.Customer;
        public ProjectAcceptanceStatus Status { get; set; } = ProjectAcceptanceStatus.Pending;

        [StringLength(120)] public string? CustomerContact { get; set; }
        public string? RequiredCriteria { get; set; }
        public string? RequiredDocuments { get; set; }
        [StringLength(500)] public string? InspectionResult { get; set; }

        public int AcceptedQuantity { get; set; } = 0;
        public int RejectedQuantity { get; set; } = 0;

        public DateTime? AcceptanceDate { get; set; }
        [StringLength(120)] public string? AcceptedBy { get; set; }   // case preserved ("acceptedby")
        // Signature reference / token (never a real captured signature here).
        [StringLength(200)] public string? Signature { get; set; }

        public bool RevenueTrigger { get; set; } = true;
        public bool WarrantyTrigger { get; set; } = false;

        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }

        [StringLength(2000)] public string? Notes { get; set; }

        public DateTime? AcceptedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums — the 0 member is the CLR/model default (== the DB default).
    // ---------------------------------------------------------------------

    public enum ProjectInspectionType : short
    {
        InProcess = 0,
        FirstArticle = 1,
        Final = 2,
        Receiving = 3,
        CustomerWitness = 4,
        Fat = 5,
        Sat = 6
    }

    public enum ProjectInspectionResult : short
    {
        Pending = 0,
        Pass = 1,
        Fail = 2,
        Conditional = 3
    }

    public enum ProjectNcrSource : short
    {
        Internal = 0,
        Supplier = 1,
        Customer = 2,
        Inspection = 3
    }

    public enum ProjectNcrSeverity : short
    {
        Minor = 0,
        Major = 1,
        Critical = 2
    }

    // Shared disposition vocabulary for NCR + MRB (project-scoped name to avoid
    // collision with Models.Production.MrbDisposition).
    public enum ProjectQualityDisposition : short
    {
        Pending = 0,
        UseAsIs = 1,
        Rework = 2,
        Repair = 3,
        ReturnToSupplier = 4,
        Scrap = 5,
        Regrade = 6
    }

    public enum ProjectNcrStatus : short
    {
        Open = 0,
        Contained = 1,
        Dispositioned = 2,
        Closed = 3        // TERMINAL — no longer blocks
    }

    public enum ProjectMrbStatus : short
    {
        Pending = 0,      // blocks until dispositioned
        Dispositioned = 1,
        Closed = 2        // TERMINAL
    }

    public enum ProjectPunchStatus : short
    {
        Open = 0,
        InProgress = 1,
        Done = 2,
        Verified = 3,     // TERMINAL — no longer blocks
        Waived = 4        // TERMINAL — consciously waived
    }

    public enum ProjectAcceptanceType : short
    {
        Customer = 0,
        Internal = 1,
        Fat = 2,
        Sat = 3,
        Final = 4
    }

    public enum ProjectAcceptanceStatus : short
    {
        Pending = 0,
        Accepted = 1,             // TERMINAL
        Rejected = 2,             // TERMINAL
        ConditionallyAccepted = 3
    }
}
