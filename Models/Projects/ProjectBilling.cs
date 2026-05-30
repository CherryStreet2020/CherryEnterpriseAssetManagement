// Theme B9 Wave 5 PR-14 (2026-05-30) — Project billing / revenue. CLOSES Wave 5.
//
// ProjectBillingSchedule / ProjectInvoiceLink / ProjectRevenueRecognition — the
// bill-side of the margin engine. Billing events tie to the PR-8 billing
// milestones (ProjectMilestone.IsBillingMilestone / BillingAmount). Two §20
// gates live in the service:
//   - a Milestone-type billing line cannot be invoiced until its milestone is
//     Achieved;
//   - a line flagged RequiresAcceptance cannot be invoiced (final-billed) until
//     acceptance is confirmed (the formal Acceptance entity lands in W6; here a
//     set-once confirmation flag stands in).
//
// Conventions (per ProjectFinancials precedent): children tenant-scoped THROUGH
// the parent project; no CompanyId. Each CASCADEs from the project; the
// milestone + schedule pegs are SET NULL (single cascade path project→child).
// xmin; enum DB defaults == 0-member model default.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectBillingSchedule — a planned billing event (milestone / %-complete /
    // T&M / fixed / on-acceptance). The unit the invoice + recognition hang off.
    // =====================================================================
    public class ProjectBillingSchedule
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // The PR-8 billing milestone this event is tied to (when Milestone type).
        public int? ProjectMilestoneId { get; set; }
        public ProjectMilestone? ProjectMilestone { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public ProjectBillingType BillingType { get; set; } = ProjectBillingType.Milestone;

        public decimal ScheduledAmount { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public DateTime? ScheduledDate { get; set; }
        public decimal? PercentOfContract { get; set; }

        // §20 gate flag: a final billing event that requires customer acceptance.
        public bool RequiresAcceptance { get; set; } = false;
        // Set-once acceptance confirmation (placeholder for the W6 Acceptance entity).
        public bool AcceptanceConfirmed { get; set; } = false;
        public DateTime? AcceptanceConfirmedAt { get; set; }
        [StringLength(100)]
        public string? AcceptanceConfirmedBy { get; set; }

        public ProjectBillingStatus Status { get; set; } = ProjectBillingStatus.Planned;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public ICollection<ProjectInvoiceLink> Invoices { get; set; } = new List<ProjectInvoiceLink>();

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectInvoiceLink — records that a billing event was invoiced. The AR
    // invoice itself lives in the finance/AR module; this is the project-side
    // link (soft ExternalInvoiceId, no FK).
    // =====================================================================
    public class ProjectInvoiceLink
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectBillingScheduleId { get; set; }
        public ProjectBillingSchedule? BillingSchedule { get; set; }

        // Soft reference to an AR invoice row, if one exists (no FK).
        public int? ExternalInvoiceId { get; set; }

        [Required, StringLength(64)]
        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; }
        public decimal InvoicedAmount { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public ProjectInvoiceStatus Status { get; set; } = ProjectInvoiceStatus.Draft;

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectRevenueRecognition — a recognized-revenue entry (point-in-time /
    // over-time / milestone / %-complete).
    // =====================================================================
    public class ProjectRevenueRecognition
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectBillingScheduleId { get; set; }
        public ProjectBillingSchedule? BillingSchedule { get; set; }

        [StringLength(64)]
        public string? PeriodLabel { get; set; }

        public RevenueRecognitionMethod Method { get; set; } = RevenueRecognitionMethod.PointInTime;

        public decimal RecognizedAmount { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public DateTime RecognitionDate { get; set; }
        public decimal? PercentComplete { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums — the 0 member is the CLR/model default (== DB default).
    // ---------------------------------------------------------------------

    public enum ProjectBillingType
    {
        Milestone = 0,
        Percentage = 1,
        TimeAndMaterials = 2,
        Fixed = 3,
        OnAcceptance = 4
    }

    public enum ProjectBillingStatus
    {
        Planned = 0,
        Ready = 1,
        Invoiced = 2,
        Paid = 3,
        OnHold = 4,
        Cancelled = 5
    }

    public enum ProjectInvoiceStatus
    {
        Draft = 0,
        Issued = 1,
        Paid = 2,
        Void = 3
    }

    public enum RevenueRecognitionMethod
    {
        PointInTime = 0,
        OverTime = 1,
        Milestone = 2,
        PercentComplete = 3
    }
}
