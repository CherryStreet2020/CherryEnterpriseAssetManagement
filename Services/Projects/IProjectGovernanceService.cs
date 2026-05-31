// Theme B9 Wave 6 PR-16 (2026-05-31) — IProjectGovernanceService.
//
// The RAID log (Risks / Issues / Actions / Decisions) + meetings/minutes for a
// CustomerProject (research §13). Read THROUGH this service (ADR-025). Tenant
// scope flows through the parent project; every incoming FK on a write is scoped
// to the project.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectGovernanceService
{
    /// <summary>RAID + meetings rollup + the five lists (read).</summary>
    Task<Result<ProjectGovernanceView>> GetGovernanceAsync(int projectId, CancellationToken ct = default);

    // -- Risks --
    Task<Result<int>> CreateRiskAsync(CreateRiskRequest req, CancellationToken ct = default);
    /// <summary>Re-assess probability/impact/plans (recomputes Exposure). Blocked once Closed/Accepted.</summary>
    Task<Result<ProjectRisk>> UpdateRiskAssessmentAsync(UpdateRiskAssessmentRequest req, CancellationToken ct = default);
    Task<Result<ProjectRisk>> TransitionRiskAsync(int riskId, ProjectRiskStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default);

    // -- Issues --
    Task<Result<int>> CreateIssueAsync(CreateIssueRequest req, CancellationToken ct = default);
    Task<Result<ProjectIssue>> TransitionIssueAsync(int issueId, ProjectIssueStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default);

    // -- Meetings --
    Task<Result<int>> CreateMeetingAsync(CreateMeetingRequest req, CancellationToken ct = default);
    Task<Result<ProjectMeeting>> TransitionMeetingAsync(int meetingId, ProjectMeetingStatus newStatus, string? modifiedBy = null, CancellationToken ct = default);

    // -- Action items --
    Task<Result<int>> CreateActionItemAsync(CreateActionItemRequest req, CancellationToken ct = default);
    Task<Result<ProjectActionItem>> TransitionActionItemAsync(int actionId, ProjectActionStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default);

    // -- Decisions --
    Task<Result<int>> RecordDecisionAsync(RecordDecisionRequest req, CancellationToken ct = default);
    Task<Result<ProjectDecision>> TransitionDecisionAsync(int decisionId, ProjectDecisionStatus newStatus, string? modifiedBy = null, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectGovernanceView(
    int ProjectId,
    string Currency,
    int OpenRiskCount,
    int TopRiskExposure,
    decimal OpenRiskCostExposure,
    int OpenIssueCount,
    int CriticalIssueCount,
    int OpenActionCount,
    int OverdueActionCount,
    int DecisionCount,
    int MeetingCount,
    IReadOnlyList<RiskRow> Risks,
    IReadOnlyList<IssueRow> Issues,
    IReadOnlyList<ActionRow> Actions,
    IReadOnlyList<DecisionRow> Decisions,
    IReadOnlyList<MeetingRow> Meetings);

public sealed record RiskRow(
    int Id, int Number, string? Title, ProjectRiskCategory Category, ProjectRiskRating Probability,
    ProjectRiskRating Impact, int Exposure, ProjectRiskStatus Status, string? Owner, DateTime? DueDate,
    decimal CostExposure, int? ScheduleExposureDays, bool CustomerImpact, bool SupplierImpact,
    int? AffectedPhaseId, int? LinkedChangeRequestId, bool IsOpen);

public sealed record IssueRow(
    int Id, int Number, string? Title, ProjectIssueSeverity Severity, ProjectPriority Priority,
    ProjectIssueStatus Status, string? Owner, DateTime OpenDate, DateTime? DueDate, DateTime? ClosedDate,
    decimal CostImpact, int? ScheduleImpactDays, bool CustomerImpact, int? AffectedPhaseId,
    int? LinkedChangeRequestId, bool IsOpen);

public sealed record ActionRow(
    int Id, int Number, string? Description, string? Owner, DateTime? DueDate, ProjectPriority Priority,
    ProjectActionStatus Status, DateTime? CompletionDate, int? ProjectMeetingId, ProjectActionSource Source,
    int? SourceId, bool IsOpen, bool IsOverdue);

public sealed record DecisionRow(
    int Id, int Number, string? Title, DateTime DecisionDate, string? DecisionMaker,
    ProjectDecisionStatus Status, int? AffectedPhaseId, int? LinkedChangeRequestId);

public sealed record MeetingRow(
    int Id, int Number, string? Title, ProjectMeetingType MeetingType, DateTime MeetingDate,
    ProjectMeetingStatus Status, int ActionItemCount);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateRiskRequest(
    int CustomerProjectId,
    string? Title = null,
    string? Description = null,
    ProjectRiskCategory Category = ProjectRiskCategory.Technical,
    ProjectRiskRating Probability = ProjectRiskRating.NotSet,
    ProjectRiskRating Impact = ProjectRiskRating.NotSet,
    string? Owner = null,
    string? MitigationPlan = null,
    string? ContingencyPlan = null,
    string? Trigger = null,
    DateTime? DueDate = null,
    decimal CostExposure = 0m,
    int? ScheduleExposureDays = null,
    bool CustomerImpact = false,
    bool SupplierImpact = false,
    int? AffectedPhaseId = null,
    int? LinkedChangeRequestId = null,
    string? Currency = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record UpdateRiskAssessmentRequest(
    int ProjectRiskId,
    ProjectRiskRating Probability,
    ProjectRiskRating Impact,
    string? MitigationPlan = null,
    string? ContingencyPlan = null,
    string? Trigger = null,
    decimal? CostExposure = null,
    int? ScheduleExposureDays = null,
    string? ModifiedBy = null);

public sealed record CreateIssueRequest(
    int CustomerProjectId,
    string? Title = null,
    string? Description = null,
    ProjectIssueSeverity Severity = ProjectIssueSeverity.Medium,
    ProjectPriority Priority = ProjectPriority.Medium,
    string? Owner = null,
    DateTime? OpenDate = null,
    DateTime? DueDate = null,
    string? RootCause = null,
    string? CorrectiveAction = null,
    bool CustomerImpact = false,
    decimal CostImpact = 0m,
    int? ScheduleImpactDays = null,
    int? AffectedPhaseId = null,
    int? LinkedChangeRequestId = null,
    string? Currency = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record CreateMeetingRequest(
    int CustomerProjectId,
    string? Title = null,
    ProjectMeetingType MeetingType = ProjectMeetingType.Status,
    DateTime? MeetingDate = null,
    string? Location = null,
    string? Attendees = null,
    string? Agenda = null,
    string? Minutes = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record CreateActionItemRequest(
    int CustomerProjectId,
    string? Description = null,
    string? Owner = null,
    DateTime? DueDate = null,
    ProjectPriority Priority = ProjectPriority.Medium,
    int? ProjectMeetingId = null,
    ProjectActionSource Source = ProjectActionSource.Manual,
    int? SourceId = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record RecordDecisionRequest(
    int CustomerProjectId,
    string? Title = null,
    string? Description = null,
    DateTime? DecisionDate = null,
    string? DecisionMaker = null,
    string? AlternativesConsidered = null,
    string? Impact = null,
    ProjectDecisionStatus Status = ProjectDecisionStatus.Proposed,
    int? AffectedPhaseId = null,
    int? LinkedChangeRequestId = null,
    string? Notes = null,
    string? CreatedBy = null);
