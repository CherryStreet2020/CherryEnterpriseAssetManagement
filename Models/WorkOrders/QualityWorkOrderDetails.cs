using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.9 — QualityWorkOrderDetails satellite.
    //
    // Holds Quality-only fields for Classification=Quality work orders.
    // Quality covers NCRs (non-conformance reports), CAPAs (corrective
    // and preventive actions), audit findings, customer complaints,
    // supplier issues, and internal QA escapes. The 8D method (D0..D8)
    // is the standard problem-solving structure adopted by Ford in 1987
    // and now baseline for ISO 9001 / IATF 16949 / AS9100.
    //
    // Relationship: 1:0..1 with WorkOrder.
    //   - UNIQUE on WorkOrderId
    //   - ON DELETE CASCADE — satellite owned by the WO
    //   - Two optional self-references to WorkOrder:
    //       CapaWorkOrderId  — the engineering CAPA spawned from this NCR
    //       LinkedNcrId      — the NCR that triggered this engineering CAPA
    //     Both ON DELETE SET NULL so deleting one side doesn't blow up
    //     the audit trail on the other.
    //
    // Field sourcing:
    //   - NcrNumber: human-readable identifier on the report.
    //   - QualityIssueType: routing input — which workflow gates apply.
    //   - Severity (Minor/Major/Critical): drives SLA + approval tier.
    //   - Source (Internal/Customer/Supplier/Audit): drives notification
    //     fan-out (customer-source → required customer notification).
    //   - AffectedQuantity + AffectedLotNumber: the scope of the defect
    //     for traceability (FDA 21 CFR 820.90 — Identification and
    //     Traceability).
    //   - DispositionCode: per ISO 9001 Cl. 8.7 — what to do with the
    //     nonconforming output (UseAsIs requires waiver approval,
    //     Rework/Scrap/Return/SortAndUse follow standard QA flow).
    //   - RootCauseMethod: which RCA technique is being applied.
    //   - RootCauseCategory: 6M categorization for trending. The 6M
    //     framework (Machine/Material/Method/Manpower/Measurement/
    //     Environment) is the de-facto categorization for Ishikawa
    //     fishbone analysis.
    //   - CapaRequired: trigger for spawning an engineering CAPA WO.
    //   - CapaWorkOrderId / LinkedNcrId: the two-way link between
    //     paired Quality and Engineering work orders.
    //   - EffectivenessVerificationDate + Status: per ISO 9001 Cl. 10.2.2
    //     and FDA 21 CFR 820.100(a)(7), CAPA effectiveness must be
    //     verified before close-out.
    //   - RegulatoryReportable: triggers MDR / FDA / OSHA fan-out.
    //   - D0..D8 free-text fields: store the 8D record body per Ford
    //     G8D / Q-101. Unbounded text — these can run long.
    //
    // Source standards:
    //   - ISO 9001:2015 Cl. 8.7 (Control of Nonconforming Outputs),
    //                   Cl. 10.2 (Nonconformity and Corrective Action)
    //   - FDA 21 CFR 820.90 (Nonconforming Product),
    //              820.100 (Corrective and Preventive Action)
    //   - Ford G8D (Global 8D Problem Solving), Ford Q-101
    //   - IATF 16949:2016 (automotive QMS)
    //   - AS9100D (aerospace QMS)
    [Table("QualityWorkOrderDetails")]
    public class QualityWorkOrderDetails
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }

        [Required, StringLength(32)]
        public string NcrNumber { get; set; } = string.Empty;

        public QualityIssueType QualityIssueType { get; set; } =
            QualityIssueType.InternalNcr;

        public QualitySeverity Severity { get; set; } = QualitySeverity.Minor;

        public QualityIssueSource Source { get; set; } = QualityIssueSource.Internal;

        [Column(TypeName = "numeric(18,4)")]
        public decimal? AffectedQuantity { get; set; }

        [StringLength(64)]
        public string? AffectedLotNumber { get; set; }

        public QualityDispositionCode DispositionCode { get; set; } =
            QualityDispositionCode.Pending;

        public RootCauseMethod RootCauseMethod { get; set; } = RootCauseMethod.FiveWhy;

        // 6M / Ishikawa categorization for trending.
        public RootCauseCategory RootCauseCategory { get; set; } =
            RootCauseCategory.Method;

        public bool CapaRequired { get; set; }

        // Self-link to the engineering CAPA WO that addresses this NCR.
        public int? CapaWorkOrderId { get; set; }

        // Inverse self-link: when this row IS an engineering CAPA,
        // points back to the NCR that triggered it.
        public int? LinkedNcrId { get; set; }

        public DateTime? EffectivenessVerificationDate { get; set; }

        public EffectivenessVerificationStatus EffectivenessVerificationStatus { get; set; } =
            EffectivenessVerificationStatus.NotStarted;

        public bool RegulatoryReportable { get; set; }

        // ---- 8D method record (Ford G8D / ASQ) ----
        // Free-text fields, one per 8D discipline. Mark NULL until
        // the team reaches that step.

        [Column(TypeName = "text")]
        public string? D0PrepNotes { get; set; }

        [Column(TypeName = "text")]
        public string? D1Team { get; set; }

        [Column(TypeName = "text")]
        public string? D2ProblemDescription { get; set; }

        [Column(TypeName = "text")]
        public string? D3ContainmentActions { get; set; }

        [Column(TypeName = "text")]
        public string? D4RootCause { get; set; }

        [Column(TypeName = "text")]
        public string? D5PermanentCorrectiveActions { get; set; }

        [Column(TypeName = "text")]
        public string? D6Implementation { get; set; }

        [Column(TypeName = "text")]
        public string? D7Prevention { get; set; }

        [Column(TypeName = "text")]
        public string? D8Recognition { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public enum QualityIssueType
    {
        Defect = 0,
        Deviation = 1,
        AuditFinding = 2,
        CustomerComplaint = 3,
        SupplierIssue = 4,
        InternalNcr = 5,
    }

    public enum QualitySeverity
    {
        Minor = 0,
        Major = 1,
        Critical = 2,
    }

    public enum QualityIssueSource
    {
        Internal = 0,
        Customer = 1,
        Supplier = 2,
        Audit = 3,
    }

    public enum QualityDispositionCode
    {
        Pending = 0,
        UseAsIs = 1,      // requires waiver approval per ISO 9001 Cl. 8.7
        Rework = 2,
        Scrap = 3,
        Return = 4,
        SortAndUse = 5,
    }

    public enum RootCauseMethod
    {
        FiveWhy = 0,
        Fishbone = 1,     // Ishikawa diagram
        FaultTree = 2,    // FTA
        EightD = 3,       // Ford G8D
        IsIsNot = 4,      // Kepner-Tregoe Is/Is-Not
    }

    // 6M / Ishikawa root-cause categorization.
    public enum RootCauseCategory
    {
        Machine = 0,
        Material = 1,
        Method = 2,
        Manpower = 3,
        Measurement = 4,
        Environment = 5,
    }

    public enum EffectivenessVerificationStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Verified = 2,
        Failed = 3,
    }
}
