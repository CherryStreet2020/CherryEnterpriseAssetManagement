// Theme B9 Wave 6 PR-16 (2026-05-31) — Project governance (RAID + meetings).
//
// ProjectRisk / ProjectIssue / ProjectActionItem / ProjectDecision /
// ProjectMeeting — the "Risks, Issues, Actions, Decisions" (RAID) log plus the
// meeting/minutes record that Primavera P6 emphasizes (research §13). Every BIC
// PSA/EPM tool has this; manufacturing-project tools usually don't connect it to
// the WBS / jobs / change orders the way we do here.
//
// Conventions (ProjectBilling / ProjectChangeRequest precedent): each entity is
// tenant-scoped THROUGH the parent CustomerProject (no CompanyId); CASCADE from
// the project; every optional peg (WBS phase, change request, meeting) is SET
// NULL so there is exactly ONE cascade path to each row; xmin concurrency; each
// enum's 0 member is the CLR/model default == the DB default; per-project
// monotonic numbers (service MAX+1, unique-index-backstopped).

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectRisk — a tracked risk with probability×impact exposure, a
    // mitigation/contingency plan, and a status workflow.
    // =====================================================================
    [Table("ProjectRisks")]
    public class ProjectRisk
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int RiskNumber { get; set; }

        [StringLength(200)]
        public string? Title { get; set; }
        public string? Description { get; set; }

        public ProjectRiskCategory Category { get; set; } = ProjectRiskCategory.Technical;

        // 1..5 ratings (NotSet = 0). Exposure = Probability × Impact (0 if unset),
        // computed by the service.
        public ProjectRiskRating Probability { get; set; } = ProjectRiskRating.NotSet;
        public ProjectRiskRating Impact { get; set; } = ProjectRiskRating.NotSet;
        public int Exposure { get; set; } = 0;

        [StringLength(120)]
        public string? Owner { get; set; }
        public string? MitigationPlan { get; set; }
        public string? ContingencyPlan { get; set; }
        public string? Trigger { get; set; }

        public ProjectRiskStatus Status { get; set; } = ProjectRiskStatus.Open;

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CostExposure { get; set; } = 0m;
        public int? ScheduleExposureDays { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public bool CustomerImpact { get; set; } = false;
        public bool SupplierImpact { get; set; } = false;

        // Optional WBS phase + change-request pegs (SET NULL).
        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }
        public int? LinkedChangeRequestId { get; set; }
        public ProjectChangeRequest? LinkedChangeRequest { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime? ClosedAt { get; set; }
        [StringLength(120)]
        public string? ClosedBy { get; set; }   // case preserved ("closedby")

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectIssue — a realized problem (a risk that occurred, or a defect)
    // with severity/priority and a resolution workflow.
    // =====================================================================
    [Table("ProjectIssues")]
    public class ProjectIssue
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int IssueNumber { get; set; }

        [StringLength(200)]
        public string? Title { get; set; }
        public string? Description { get; set; }

        // Defaults are the 0 members (enum-sentinel rule == DB default); the
        // service writes the caller's chosen severity/priority (Medium by default).
        public ProjectIssueSeverity Severity { get; set; } = ProjectIssueSeverity.Low;
        public ProjectPriority Priority { get; set; } = ProjectPriority.Low;

        [StringLength(120)]
        public string? Owner { get; set; }

        [DataType(DataType.Date)]
        public DateTime OpenDate { get; set; } = DateTime.UtcNow;
        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? ClosedDate { get; set; }

        public ProjectIssueStatus Status { get; set; } = ProjectIssueStatus.Open;

        public string? RootCause { get; set; }
        public string? CorrectiveAction { get; set; }

        public bool CustomerImpact { get; set; } = false;
        [Column(TypeName = "decimal(18,4)")]
        public decimal CostImpact { get; set; } = 0m;
        public int? ScheduleImpactDays { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }
        public int? LinkedChangeRequestId { get; set; }
        public ProjectChangeRequest? LinkedChangeRequest { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime? ClosedAt { get; set; }
        [StringLength(120)]
        public string? ClosedBy { get; set; }    // case preserved ("closedby")

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectMeeting — a project meeting + its minutes. Action items hang off
    // it (ProjectActionItem.ProjectMeetingId).
    // =====================================================================
    [Table("ProjectMeetings")]
    public class ProjectMeeting
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int MeetingNumber { get; set; }

        [StringLength(200)]
        public string? Title { get; set; }
        public ProjectMeetingType MeetingType { get; set; } = ProjectMeetingType.Status;

        public DateTime MeetingDate { get; set; } = DateTime.UtcNow;

        [StringLength(200)]
        public string? Location { get; set; }
        public string? Attendees { get; set; }
        public string? Agenda { get; set; }
        // The meeting minutes prose (case preserved via "minutes").
        public string? Minutes { get; set; }

        public ProjectMeetingStatus Status { get; set; } = ProjectMeetingStatus.Scheduled;

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public ICollection<ProjectActionItem> ActionItems { get; set; } = new List<ProjectActionItem>();

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectActionItem — an assigned action with an owner, due date, and a
    // completion workflow. Optionally sourced from a meeting / risk / issue /
    // decision (the meeting peg is a real FK; other sources are a soft tag).
    // =====================================================================
    [Table("ProjectActionItems")]
    public class ProjectActionItem
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int ActionNumber { get; set; }

        // The meeting this action came out of (SET NULL).
        public int? ProjectMeetingId { get; set; }
        public ProjectMeeting? Meeting { get; set; }

        [StringLength(120)]
        public string? Owner { get; set; }
        public string? Description { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        public ProjectPriority Priority { get; set; } = ProjectPriority.Low;
        public ProjectActionStatus Status { get; set; } = ProjectActionStatus.Open;

        [DataType(DataType.Date)]
        public DateTime? CompletionDate { get; set; }

        // Soft polymorphic source tag (no FK) for actions raised off a
        // risk/issue/decision rather than a meeting.
        public ProjectActionSource Source { get; set; } = ProjectActionSource.Manual;
        public int? SourceId { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime? CompletedAt { get; set; }
        [StringLength(120)]
        public string? CompletedBy { get; set; }   // case preserved ("completedby")

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectDecision — a recorded decision with alternatives and impact (the
    // decision log / audit trail of "why we did it this way").
    // =====================================================================
    [Table("ProjectDecisions")]
    public class ProjectDecision
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int DecisionNumber { get; set; }

        public DateTime DecisionDate { get; set; } = DateTime.UtcNow;

        [StringLength(200)]
        public string? Title { get; set; }
        public string? Description { get; set; }

        [StringLength(120)]
        public string? DecisionMaker { get; set; }

        public string? AlternativesConsidered { get; set; }
        public string? Impact { get; set; }

        public ProjectDecisionStatus Status { get; set; } = ProjectDecisionStatus.Proposed;

        public int? AffectedPhaseId { get; set; }
        public ProjectPhase? AffectedPhase { get; set; }
        public int? LinkedChangeRequestId { get; set; }
        public ProjectChangeRequest? LinkedChangeRequest { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums — 0 member is the CLR/model default (== the DB default).
    // ---------------------------------------------------------------------

    public enum ProjectRiskCategory : short
    {
        Technical = 0,
        Schedule = 1,
        Cost = 2,
        Supplier = 3,
        Quality = 4,
        Resource = 5,
        Commercial = 6,
        External = 7
    }

    // 1..5 rating with an explicit NotSet=0 so the model/DB default is the
    // 0 member (enum-sentinel rule) and an un-rated risk reads as unset.
    public enum ProjectRiskRating : short
    {
        NotSet = 0,
        VeryLow = 1,
        Low = 2,
        Medium = 3,
        High = 4,
        VeryHigh = 5
    }

    public enum ProjectRiskStatus : short
    {
        Open = 0,
        Mitigating = 1,
        Closed = 2,       // TERMINAL
        Accepted = 3,     // TERMINAL (consciously accepted, no mitigation)
        Escalated = 4
    }

    public enum ProjectIssueSeverity : short
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public enum ProjectPriority : short
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Urgent = 3
    }

    public enum ProjectIssueStatus : short
    {
        Open = 0,
        InProgress = 1,
        Resolved = 2,
        Closed = 3,       // TERMINAL
        Escalated = 4
    }

    public enum ProjectActionStatus : short
    {
        Open = 0,
        InProgress = 1,
        Done = 2,         // TERMINAL
        Cancelled = 3     // TERMINAL
    }

    public enum ProjectActionSource : short
    {
        Manual = 0,
        Meeting = 1,
        Risk = 2,
        Issue = 3,
        Decision = 4
    }

    public enum ProjectDecisionStatus : short
    {
        Proposed = 0,
        Approved = 1,     // TERMINAL-ish (can be Superseded)
        Rejected = 2,     // TERMINAL
        Superseded = 3    // TERMINAL
    }

    // Status = 0 is the model/DB default (enum-sentinel rule): the 0 member must
    // be the default so a Status meeting isn't silently overwritten.
    public enum ProjectMeetingType : short
    {
        Status = 0,
        Kickoff = 1,
        DesignReview = 2,
        CustomerReview = 3,
        Closeout = 4,
        Other = 5
    }

    public enum ProjectMeetingStatus : short
    {
        Scheduled = 0,
        Held = 1,
        Cancelled = 2     // TERMINAL
    }
}
