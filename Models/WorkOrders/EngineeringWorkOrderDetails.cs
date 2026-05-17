using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.10 — EngineeringWorkOrderDetails satellite.
    //
    // Engineering-only fields for Classification=Engineering work orders.
    // Covers ECO (Engineering Change Orders), MOC (Management of Change
    // per OSHA 29 CFR 1910.119(l)), BOM revisions, procedure updates,
    // and engineering CAPAs spawned from Quality NCRs.
    //
    // Relationship: 1:0..1 with WorkOrder.
    //   - UNIQUE on WorkOrderId, ON DELETE CASCADE
    //   - LinkedNcrWorkOrderId — optional self-FK to the Quality NCR
    //     that triggered this engineering CAPA. ON DELETE SET NULL.
    //
    // Field sourcing:
    //   - EcoNumber: engineering change order identifier (nullable
    //     because MOC-only or BOM-only WOs may not carry an ECO).
    //   - EngineeringIssueType: which engineering workflow gates apply.
    //     PSSR + MOC review gates fire for ManagementOfChange; BOM gates
    //     fire for BomRevision; etc.
    //   - ChangeTypeFFF: Form / Fit / Function / Documentation / Safety.
    //     Per ASME Y14.35, FFF impact drives whether downstream BOMs
    //     and assemblies need re-validation. Documentation-only changes
    //     skip downstream impact analysis.
    //   - RiskLevel: drives approval threshold + PSSR scope.
    //   - IsReplacementInKind: per OSHA 1910.119(l)(1), a true RIK
    //     ("identical replacement of an existing component with
    //     equivalent specification") may bypass full MOC review. The
    //     RIK flag is auditable + reviewed by EHS during PSSR.
    //   - MocPshUpdated: PSH (Process Safety Information) updated per
    //     1910.119(d). Required Yes for non-RIK MOCs.
    //   - MocOperatingProceduresUpdated: SOPs updated per 1910.119(f).
    //   - MocTrainingRequired: operator/maintainer retraining required
    //     per 1910.119(g)(2).
    //   - PssrCompleted + PssrCompletedAt: Pre-Startup Safety Review
    //     per 1910.119(i). MUST be true before startup post-MOC.
    //   - LinkedNcrWorkOrderId: when this engineering CAPA was spawned
    //     by a Quality NCR, the linked NCR's WorkOrder Id.
    //   - EffectiveDate: when the engineering change takes effect on
    //     manufacturing / operations. Drives BOM cutover.
    //   - CutInSerial: serial-number cutover point for the change.
    //     "Effective at serial #XYZ-1234."
    //   - RegulatoryReview: whether the change requires submission to
    //     an external regulator (FDA, FAA, etc.) before effective date.
    //   - AffectedItems: jsonb array. Each entry = `{itemType,
    //     oldRevision, newRevision, dispositionOfInStock}`. The
    //     dispositionOfInStock takes values like "Use existing, then
    //     transition to new" / "Scrap existing" / "Rework existing".
    //
    // Source standards:
    //   - ASME Y14.35 (Revision of Engineering Drawings and Associated
    //     Documents)
    //   - OSHA 29 CFR 1910.119(l) (Management of Change),
    //     1910.119(i) (Pre-Startup Safety Review)
    //   - ISO 9001:2015 Cl. 8.5.6 (Control of Changes)
    //   - AS9100D Cl. 8.5.6 (aerospace QMS change control)
    [Table("EngineeringWorkOrderDetails")]
    public class EngineeringWorkOrderDetails
    {
        public int Id { get; set; }

        public int WorkOrderId { get; set; }

        [StringLength(32)]
        public string? EcoNumber { get; set; }

        public EngineeringIssueType EngineeringIssueType { get; set; } =
            EngineeringIssueType.EngineeringChangeOrder;

        public ChangeTypeFFF ChangeTypeFFF { get; set; } = ChangeTypeFFF.Documentation;

        public EngineeringRiskLevel RiskLevel { get; set; } = EngineeringRiskLevel.Low;

        // RIK = Replacement In Kind. Per OSHA 1910.119(l)(1), a true
        // RIK ("identical or equivalent" replacement) may bypass full
        // MOC review. EHS reviews this claim during PSSR.
        public bool IsReplacementInKind { get; set; }

        // MOC checklist items (OSHA 1910.119(l), (d), (f), (g)).
        public bool MocPshUpdated { get; set; }
        public bool MocOperatingProceduresUpdated { get; set; }
        public bool MocTrainingRequired { get; set; }

        // PSSR — Pre-Startup Safety Review per OSHA 1910.119(i).
        // Required before resuming production after any non-RIK MOC.
        public bool PssrCompleted { get; set; }
        public DateTime? PssrCompletedAt { get; set; }

        // Self-FK to the Quality NCR that triggered this engineering CAPA.
        public int? LinkedNcrWorkOrderId { get; set; }

        public DateTime? EffectiveDate { get; set; }

        [StringLength(64)]
        public string? CutInSerial { get; set; }

        public bool RegulatoryReview { get; set; }

        // jsonb array of `{itemType, oldRevision, newRevision,
        // dispositionOfInStock}` entries documenting which items are
        // affected by the change and how existing inventory is treated.
        [Column(TypeName = "jsonb")]
        public string? AffectedItems { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public enum EngineeringIssueType
    {
        EngineeringChangeOrder = 0,
        ManagementOfChange = 1,   // OSHA 1910.119(l)
        DesignChange = 2,
        BomRevision = 3,
        ProcedureUpdate = 4,
        Capa = 5,                 // engineering CAPA, often linked to a Quality NCR
    }

    // ASME Y14.35 change-type categorization. FFF = Form / Fit / Function.
    public enum ChangeTypeFFF
    {
        Form = 0,
        Fit = 1,
        Function = 2,
        Documentation = 3,
        Safety = 4,
    }

    public enum EngineeringRiskLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3,
    }
}
