using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.2 — Per-classification field-visibility configuration.
    //
    // This table is the single source of truth for "which fields show, in
    // what order, with what label, in which section" for the unified
    // WorkOrder form. Pattern is lifted directly from SAP's `OIAN` /
    // T185F field-selection tables: one row per (Classification × FieldName)
    // with explicit visibility, ordering, and section grouping.
    //
    // The Razor renderer (Phase F) reads this table once at request time
    // (cached aggressively per-classification per-tenant), then emits the
    // form fields in the right order, in the right sections, with the
    // right required/optional/read-only semantics. ZERO per-classification
    // code branches in the renderer — every diff is data.
    //
    // Tenant overrides: a row with TenantId NULL is the global default.
    // A row with the same (Classification, FieldName) and a non-NULL
    // TenantId overrides the global default for that tenant. The runtime
    // service merges global + tenant rows by precedence (tenant wins).
    //
    // Onboarding a new industry vertical = seed N tenant-scoped rows.
    // No code changes.
    [Table("WorkOrderFieldVisibility")]
    public class WorkOrderFieldVisibility
    {
        public int Id { get; set; }

        // Which classification this rule applies to. Matches the
        // WorkOrderClassification enum (Maintenance=0, Quality=2,
        // Engineering=3, HSE=4, CIP=5). Value 1 is intentionally
        // not allowed here (formerly Production — Production is a
        // sibling table, not a WorkOrder subtype).
        public WorkOrderClassification Classification { get; set; }

        // The .NET property name on WorkOrder (case-sensitive) or the
        // dot-path into a satellite (e.g. "CipDetails.AfeNumber"). The
        // renderer maps this to the actual property at runtime via
        // reflection-cached lookup.
        [Required, StringLength(80)]
        public string FieldName { get; set; } = string.Empty;

        // How the field should appear on the form. The status engine
        // (PR #119.3) can further override Required to ReadOnly when the
        // WO is in a terminal status (Closed, Cancelled).
        public FieldVisibility Visibility { get; set; } = FieldVisibility.Optional;

        // Per-classification label override. NULL = use the property's
        // [Display(Name = ...)] attribute or property name. Examples:
        //   FailureCode under Maintenance shows as "Failure Code" (default).
        //   FailureCode under HSE shows as "Hazard Code" (override).
        [StringLength(80)]
        public string? DisplayLabel { get; set; }

        // Order within SectionName for this classification. Section is
        // grouped, then fields are ordered by DisplayOrder ascending.
        public int DisplayOrder { get; set; } = 100;

        // Visual section grouping on the form. Examples:
        //   "Identification" — WorkOrderNumber, Description, Revision, Priority
        //   "Scheduling"     — ScheduledDate, NextScheduledDate, RecurrenceIntervalDays
        //   "People"         — TechnicianId, RequestedBy, ApprovedBy
        //   "Cost"           — EstimatedCost, ActualCost, LaborCost, ...
        //   "Resolution"     — FailureCode, RootCause, CorrectiveAction, Resolution
        //   "Approval"       — ApprovalStatus, ApprovedBy, HoldReason
        //   "External"       — ExternalWorkOrderId, ExternalSource
        //   "Capital Asset"  — (CIP only)
        //   "Quality NCR"    — (Quality only)
        //   "Engineering Change" — (Engineering only)
        //   "Incident"       — (HSE only)
        //   "JSA"            — (HSE only)
        [Required, StringLength(40)]
        public string SectionName { get; set; } = "Other";

        // Optional help text rendered as a tooltip below the field.
        [StringLength(500)]
        public string? HelpText { get; set; }

        // Optional regex / format hint for client-side validation.
        // (Server-side validation always wins; this is UX-only.)
        [StringLength(200)]
        public string? ValidationHint { get; set; }

        // NULL = global default. Non-NULL = per-tenant override that
        // beats the global row of the same (Classification, FieldName).
        public int? TenantId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // ADR-012 v0.2 — Field visibility states for the unified WorkOrder form.
    //
    // Values are stable. New values append.
    public enum FieldVisibility
    {
        // Hide the field entirely. The renderer doesn't emit anything;
        // the underlying property keeps whatever value it has but the
        // user can't see or edit it.
        Hidden = 0,

        // Show the field; allow blank submission. Default for any field
        // not explicitly listed for this classification (renderer falls
        // back to Optional). Most common visibility.
        Optional = 1,

        // Show the field with a "required" indicator; reject submission
        // if blank. The status engine (PR #119.3) can NOT downgrade a
        // Required field, but it CAN upgrade an Optional → Required
        // when entering specific statuses (e.g. ResolutionSummary
        // becomes Required when transitioning to Closed).
        Required = 2,

        // Show the field as read-only (display the current value but no
        // input). Used for system-managed fields (WorkOrderNumber after
        // creation, CreatedAt, ApprovedAt) and for fields that become
        // immutable after a status gate (e.g. PartNumber on Production
        // after Release).
        ReadOnly = 3,
    }
}
