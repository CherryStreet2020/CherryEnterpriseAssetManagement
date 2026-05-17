using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.3 — Allowed (FromStatus → ToStatus) edges
    // per classification.
    //
    // One row per legal transition. If a row doesn't exist for a
    // (Classification, From, To) triple, the engine refuses the
    // transition. This is the heart of the state machine.
    //
    // Two extension points:
    //  - RequiredApprovalStage: if set, the engine refuses the transition
    //    unless WorkOrderApproval contains a Decision=Approved row with
    //    this exact Stage. (PSSR sign-off for Engineering → Effective,
    //    AFE-Tier1/2 approval for CIP → AfeApproved, QA-Release for
    //    Quality → EffectivenessVerified.)
    //  - GuardServiceName: a DI-resolved IWorkOrderTransitionGuard
    //    implementation that runs arbitrary checks + side effects at
    //    transition time. (CipCapitalizationGuard fires the fixed-asset
    //    reclassification when CIP → SubstantialComplete. PssrCompletion
    //    Guard checks that all PSSR signoffs are recorded when
    //    Engineering → Effective.)
    //
    // Both extension points reserve a contract; the actual guard
    // implementations ship with their respective satellites in Phase D.
    // The engine logs + allows when a named guard isn't yet registered
    // (developer-friendly during build-up).
    [Table("WorkOrderStatusTransition")]
    public class WorkOrderStatusTransition
    {
        public int Id { get; set; }

        public WorkOrderClassification Classification { get; set; }

        public short FromStatusCode { get; set; }

        public short ToStatusCode { get; set; }

        // If non-NULL: this transition requires an approved
        // WorkOrderApproval row with Stage == this value. The engine
        // checks the approval table before allowing the transition.
        // Examples: "AFE-Tier1", "AFE-Tier2", "CCB", "PSSR", "QA-Release".
        [StringLength(40)]
        public string? RequiredApprovalStage { get; set; }

        // If non-NULL: name of an IWorkOrderTransitionGuard registered
        // in DI. The engine resolves the guard by name and calls it
        // before allowing the transition. The guard can return Block
        // (refuse), Allow (proceed), or AllowWithWarning (proceed but
        // raise a warning to the UI). The guard can also perform side
        // effects (write OSHA 300 entry, fire accounting reclassification
        // event, etc.).
        [StringLength(80)]
        public string? GuardServiceName { get; set; }

        // True means this is a "reverse" transition (e.g. InProgress →
        // Scheduled to undo a wrong start). The UI shows these grouped
        // under a "Revert to..." menu rather than the main "Move to..."
        // list.
        public bool IsBackTransition { get; set; } = false;

        // Display label override for the action button (default: the
        // ToStatus's label, e.g. "Move to Scheduled"). Sometimes the
        // action verb makes more sense — "Start Work", "Submit for
        // Approval", "Close Out".
        [StringLength(80)]
        public string? ActionLabel { get; set; }

        // Order of the action button when multiple transitions are
        // available from the same FromStatus. Renders the primary
        // (most-common-next-step) button first.
        public int DisplayOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
