using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    // Sprint 2 PR #115 — Approval Hierarchy + SoD.
    //
    // Immutable decision log. One row per approve/reject action against a
    // target document (PurchaseOrder, VendorInvoice, etc.). Keyed by
    // (TargetEntityType, TargetEntityId) so we can fetch the full decision
    // chain for any document, and so the SoD enforcement can refuse a user
    // who has already acted on this same target.
    //
    // CreatorUserId is denormalized at decision time so the SoD check at
    // read time doesn't have to re-derive who created the doc — important
    // for older docs whose creator metadata is on different fields per type.
    public enum ApprovalDecision
    {
        Approved = 0,
        Rejected = 1
    }

    public class ApprovalAction
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string TargetEntityType { get; set; } = string.Empty;

        [Required]
        public int TargetEntityId { get; set; }

        // Workflow that drove this decision (resolved at time of approve).
        public int? ApprovalWorkflowId { get; set; }
        public ApprovalWorkflow? ApprovalWorkflow { get; set; }

        // Step number — defaults to 1 for the current single-step
        // implementation. Reserved for the future sequential-chain
        // extension (Manager → Director → CFO).
        public int StepNumber { get; set; } = 1;

        public ApprovalDecision Decision { get; set; }

        [Required, StringLength(450)]
        // ASP.NET Identity user PK type is string; match it here.
        public string DecidedByUserId { get; set; } = string.Empty;

        [Required, StringLength(256)]
        public string DecidedByUsername { get; set; } = string.Empty;

        public DateTime DecidedAt { get; set; } = DateTime.UtcNow;

        [StringLength(1000)]
        public string? Comment { get; set; }

        // The role the approver used to take this action. Captured for
        // audit so a future role change doesn't invalidate the history.
        [StringLength(100)]
        public string? ApproverRole { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }
    }
}
