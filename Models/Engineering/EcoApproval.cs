// Sprint 14.3 PR-1 (2026-05-27) — EcoApproval.
//
// One row per approval stage. The full chain is N rows ordered by StageOrder.
// An ECO flips from InApproval → Approved when ALL non-Skipped stages have
// Status=Approved AND no stage has Status=Rejected.
//
// Stages must be approved IN ORDER — a stage's Status can only flip from
// Pending to Approved when all earlier-StageOrder stages are Approved or
// Skipped. The service enforces this; the data layer just stores the chain.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Engineering
{
    /// <summary>
    /// Per-stage approval status.
    /// </summary>
    public enum EcoApprovalStatus
    {
        /// <summary>Pending — awaiting decision.</summary>
        Pending = 0,

        /// <summary>Approved — stage signed off.</summary>
        Approved = 1,

        /// <summary>Rejected — stage refused. ECO must be cancelled or revised.</summary>
        Rejected = 2,

        /// <summary>Skipped — stage bypassed (e.g., Emergency urgency short-circuit
        /// with leadership sign-off, or stage not required for this change scope).</summary>
        Skipped = 3,

        /// <summary>Not required — administratively marked as not applicable for
        /// this specific ECO without short-circuit (template stage that doesn't apply).</summary>
        NotRequired = 4,
    }

    /// <summary>
    /// One stage in an ECO's multi-stage approval chain.
    /// </summary>
    [Table("EcoApprovals")]
    public class EcoApproval
    {
        public int Id { get; set; }

        // ===== Parent + tenant denorm =====================================

        [Required]
        public int EcoId { get; set; }
        public EngineeringChangeOrder? Eco { get; set; }

        [Required]
        public int CompanyId { get; set; }

        // ===== Stage identity =============================================

        /// <summary>
        /// 1-based ordering within the ECO. UNIQUE per (EcoId, StageOrder).
        /// Service enforces "earlier stages first" decision order.
        /// </summary>
        [Required]
        public int StageOrder { get; set; }

        /// <summary>
        /// Role label (e.g., "Engineering Lead", "Quality Manager",
        /// "Customer Liaison", "Manufacturing Engineering").
        /// </summary>
        [Required, StringLength(100)]
        [Display(Name = "Approval Role")]
        public string ApprovalRole { get; set; } = string.Empty;

        /// <summary>
        /// Optional named user expected to approve. NULL = anyone in the
        /// ApprovalRole can approve.
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Required Approver")]
        public string? RequiredApprover { get; set; }

        [Required]
        public EcoApprovalStatus Status { get; set; } = EcoApprovalStatus.Pending;

        // ===== Decision stamps ============================================

        [Display(Name = "Decided At")]
        public DateTime? DecidedAtUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Decided By")]
        public string? DecidedBy { get; set; }

        [StringLength(1000)]
        [Display(Name = "Decision Notes")]
        public string? DecisionNotes { get; set; }

        // ===== Audit + concurrency ========================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
