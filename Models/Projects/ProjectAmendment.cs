using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Projects
{
    // ====================================================================
    // Sprint 13.5 PR #1.5 — ProjectAmendments (append-only change-order log).
    //
    // The customer-issued change-order event log for a CustomerProject.
    //
    // CRITICAL DESIGN CALL (research §5.3): CustomerProjects.ContractValue
    // stays the IMMUTABLE BASELINE. The "effective" contract value is the
    // service-layer SUM:
    //
    //   EffectiveContractValue = CustomerProjects.ContractValue
    //                          + SUM(ProjectAmendments.ValueDelta
    //                                WHERE Status = Approved
    //                                  AND EffectiveDate <= asOfDate)
    //
    // This matches Acumatica (Original / Revised / Committed triple),
    // Oracle Project Accounting (baseline + change documents), SAP PS
    // (amendment vs baseline), and AIA G701 (industry-standard form).
    //
    // APPEND-ONLY. Never DELETE rows — mark Status=Voided instead.
    // A Postgres trigger (fn_block_amendment_status_regression) backstops
    // the service layer: once Approved or Rejected, cannot regress.
    //
    // See `docs/research/customerproject-field-set.md` §5 for the full
    // industry survey + anti-patterns + deferred items.
    // ====================================================================
    [Table("ProjectAmendments")]
    public class ProjectAmendment
    {
        public long Id { get; set; }

        // Parent project. CASCADE because amendments are intrinsic to the
        // project (in practice projects are soft-deleted via Status, never
        // hard-deleted).
        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Per-project monotonic sequence (1, 2, 3 ...). Human-readable —
        // "Weir Q2 Power Frame, CO #3". Per research anti-pattern §5.6:
        // NEVER a GUID. Service layer computes via MAX(AmendmentNumber)+1
        // inside a tenant lock on the project row.
        // CHECK ck_projectamendments_amendmentnumber_pos enforces >= 1.
        public int AmendmentNumber { get; set; }

        // When the change takes effect from the customer / accounting
        // perspective. Distinct from CreatedAt (when the row was written).
        [DataType(DataType.Date)]
        public DateTime EffectiveDate { get; set; }

        // What kind of change. CHECK ck_projectamendments_changetype_range
        // enforces 0..3.
        public ProjectAmendmentChangeType ChangeType { get; set; }

        // Short narrative — the customer's stated reason in their words
        // ("design rev B1 requires titanium").
        [StringLength(2000)]
        public string? Reason { get; set; }

        // Longer-form description of what was added / removed / changed.
        // Renders in the project amendment timeline view.
        public string? ScopeNarrative { get; set; }

        // Dollar (or Currency) delta. POSITIVE or NEGATIVE. Required for
        // the EffectiveContractValue SUM to be unambiguous. Default 0
        // because Scope-only and Schedule-only changes legitimately have
        // zero value impact.
        [Column(TypeName = "decimal(18,4)")]
        public decimal ValueDelta { get; set; } = 0m;

        // Date deltas in DAYS. Nullable = no change to that date.
        public int? TargetStartDateDelta { get; set; }
        public int? TargetEndDateDelta { get; set; }

        // FK to future Quotation entity (Sprint 14 Sales). No DB-level FK
        // constraint yet because Quotations table doesn't exist. Per
        // research anti-pattern §4.3: adding the FK constraint now would
        // point at a non-existent table. Constraint added in Sprint 14.
        public int? SourceQuotationId { get; set; }

        // B9 Wave 6 PR-15 — back-link to the ProjectChangeRequest this amendment
        // (change order) was converted from. SET NULL on the request side so a
        // request delete doesn't take the order with it. Null for amendments
        // created directly (the legacy CreateAmendment path predates change
        // requests). The forward link lives on
        // ProjectChangeRequest.ResultingProjectAmendmentId.
        public int? SourceChangeRequestId { get; set; }
        public ProjectChangeRequest? SourceChangeRequest { get; set; }

        // Customer's own change-order reference number ("Weir CO-2026-014").
        // Required for traceability per research anti-pattern §5.6.
        [StringLength(100)]
        public string? CustomerReference { get; set; }

        // Workflow state. Append-only — never DELETE rows, mark Voided.
        // Only Status=Approved contributes to EffectiveContractValue.
        // Postgres trigger fn_block_amendment_status_regression backstops
        // the service layer to prevent illegal status regressions.
        // CHECK ck_projectamendments_status_range enforces 0..5.
        public ProjectAmendmentStatus Status { get; set; } = ProjectAmendmentStatus.Draft;

        // Internal approver. ApprovedById is the FK; ApprovedByName is a
        // snapshot so the historical record survives user-renames/deletes.
        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        [StringLength(100)]
        public string? ApprovedByName { get; set; }

        public DateTime? ApprovedAt { get; set; }

        // When customer countersigned (paper or digital). Digital signature
        // capture itself defers to Sprint 21 launch hardening.
        public DateTime? CustomerSignatureAt { get; set; }

        // Internal notes for the audit trail / PM commentary.
        public string? Notes { get; set; }

        // Standard audit fields — same convention as CustomerProject.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }

    // What kind of contractual change this amendment represents.
    // Per research §5.2 + AIA G701 + Acumatica Change Orders pattern.
    public enum ProjectAmendmentChangeType : short
    {
        // Work added or removed. ValueDelta may be 0 (in-scope swap).
        Scope = 0,
        // Dates shifted. ValueDelta may be 0.
        Schedule = 1,
        // Pure price change, no scope/schedule shift.
        Value = 2,
        // Mix of scope/schedule/value.
        Combined = 3
    }

    // Workflow state for a ProjectAmendment. APPEND-ONLY discipline —
    // a Postgres BEFORE UPDATE trigger blocks illegal regressions
    // (Approved → Draft, Voided → anything, etc.). Per research §5.6.
    public enum ProjectAmendmentStatus : short
    {
        // Internal draft — service-layer creates here.
        Draft = 0,
        // Sent to customer for countersignature.
        Submitted = 1,
        // Customer countersigned. Contributes to EffectiveContractValue.
        // TERMINAL except for Voided transition.
        Approved = 2,
        // Customer rejected. TERMINAL except for Voided transition.
        Rejected = 3,
        // Withdrawn by us before customer review.
        Withdrawn = 4,
        // After-the-fact reversal. Preserves audit. TERMINAL.
        Voided = 5
    }
}
