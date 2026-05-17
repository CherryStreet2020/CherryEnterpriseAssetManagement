using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.4 — Polymorphic approval chain.
    //
    // Replaces the single ApprovedBy/ApprovedAt/ApprovalStatus columns
    // on WorkOrder with a 1:N child table. Lets one WorkOrder
    // carry an arbitrarily long approval chain whose shape depends on
    // classification + threshold:
    //
    //   Maintenance Corrective: 1 stage  (Planner)
    //   Quality NCR:            1 stage  (QA-Disposition)
    //   Engineering ECO:        1 stage  (CCB)
    //   Engineering MOC:        2 stages (CCB → PSSR)
    //   HSE Recordable:         1 stage  (EHS-Director)
    //   CIP < $50K:             1 stage  (AFE-Tier1)
    //   CIP $50K-$500K:         2 stages (AFE-Tier1 → AFE-Tier2)
    //   CIP > $500K:            4 stages (AFE-Tier1 → Tier2 → CFO → Board)
    //   CIP w/ JV partners:     +1 stage per partner (Partner-{name})
    //
    // The status engine reads the chain at transition time:
    // WorkOrderStatusTransition.RequiredApprovalStage must match a row
    // here with Decision=Approved before the engine allows the transition.
    //
    // Stage strings are stable identifiers used by the engine + guards.
    // DisplayLabel renders the human-friendly version in the UI.
    //
    // Legacy back-compat: existing WorkOrder.ApprovedById columns
    // stay in place. The PR #119.4 migration backfills a Stage='Legacy'
    // row for every existing approved WO so historical state survives.
    // PR #119.4.1 (deferred) drops the legacy columns once the UI cuts
    // over to read from this table.
    [Table("WorkOrderApproval")]
    public class WorkOrderApproval
    {
        public int Id { get; set; }

        // FK to WorkOrder (renamed to WorkOrder in PR #119.7).
        // No navigation property because we're carrying the FK through
        // the upcoming rename without churn.
        public int WorkOrderId { get; set; }

        // Machine-readable stage identifier. Matches
        // WorkOrderStatusTransition.RequiredApprovalStage exactly.
        // Examples:
        //   "PM-Approval"     — Maintenance Planner approval
        //   "QA-Disposition"  — Quality NCR disposition signoff
        //   "CCB"             — Engineering Change Control Board
        //   "PSSR"            — OSHA 1910.119(i) Pre-Startup Safety Review
        //   "EHS-Director"    — HSE recordable incident close
        //   "AFE-Tier1"       — CIP capital appropriation, first tier
        //   "AFE-Tier2"       — CIP, second tier (over threshold)
        //   "CFO"             — CIP, CFO threshold
        //   "Board"           — CIP, Board threshold
        //   "Partner-Foo"     — CIP JV partner approval
        //   "Legacy"          — backfill row from pre-#119.4 single-field state
        [Required, StringLength(40)]
        public string Stage { get; set; } = string.Empty;

        // Order within the workflow. 0-indexed; the UI shows the chain
        // in this order. Two stages can share an order if they happen
        // in parallel (e.g. simultaneous CFO + Board sign-off).
        public int StageOrder { get; set; } = 0;

        // The role required to approve this stage. Used by the chain-
        // builder service (Phase F) to find eligible approvers + by
        // the audit UI to show "who can sign this." Free text now; a
        // proper Role lookup migration is queued.
        [Required, StringLength(40)]
        public string RoleRequired { get; set; } = string.Empty;

        // Human-friendly label for the UI. Falls back to Stage when null.
        [StringLength(80)]
        public string? DisplayLabel { get; set; }

        // The user who decided this stage. NULL until decided.
        public int? ApproverUserId { get; set; }
        public User? ApproverUser { get; set; }

        public WorkOrderApprovalDecision Decision { get; set; } = WorkOrderApprovalDecision.Pending;

        // When the decision was recorded. NULL while Pending.
        public DateTime? DecisionAt { get; set; }

        // Optional rationale. Required when Decision=Rejected; the UI
        // refuses submit-with-rejection if Comments is empty.
        [StringLength(1000)]
        public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Optimistic concurrency via Postgres xmin.
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    // ADR-012 v0.2 / PR #119.4 — Decision states for a single approval row.
    //
    // Values are stable. Decision=Skipped lets a WO bypass an optional
    // stage when a guard determines the stage doesn't apply (e.g. an
    // Engineering Replacement-In-Kind WO can skip PSSR per 29 CFR
    // 1910.119(l)(1)).
    public enum WorkOrderApprovalDecision
    {
        Pending  = 0,
        Approved = 1,
        Rejected = 2,
        Skipped  = 3,
    }
}
