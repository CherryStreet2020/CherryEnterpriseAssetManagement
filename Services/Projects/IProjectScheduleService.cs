// Theme B9 Wave 3 PR-8 (2026-05-30) — IProjectScheduleService.
//
// The schedule + task layer over the WBS backbone. Hosts the §20 gates:
//   - AchieveMilestone: cannot achieve a milestone while a blocking task is open.
//   - CompleteTask: cannot complete a task while a finish-to-start predecessor
//     is still open.
//   - AddDependency: rejects self-loops, duplicates, cross-project edges, and
//     any edge that would create a CYCLE.
//
// ADR-025: PageModels / voice read through THIS service. Tenant scope flows
// THROUGH the parent CustomerProject (these entities have no CompanyId).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectScheduleService
{
    /// <summary>Milestones, tasks, and dependency edges for a project (read).</summary>
    Task<Result<ProjectScheduleView>> GetScheduleAsync(int projectId, CancellationToken ct = default);

    Task<Result<int>> CreateMilestoneAsync(CreateMilestoneRequest req, CancellationToken ct = default);
    Task<Result<int>> CreateTaskAsync(CreateTaskRequest req, CancellationToken ct = default);

    /// <summary>Add a precedence edge. Rejects self / duplicate / cross-project / cycle.</summary>
    Task<Result<int>> AddDependencyAsync(AddDependencyRequest req, CancellationToken ct = default);

    Task<Result<ProjectTask>> StartTaskAsync(int taskId, string? modifiedBy = null, CancellationToken ct = default);

    /// <summary>Complete a task. Blocked while any finish-to-start predecessor is open. Set-once.</summary>
    Task<Result<ProjectTask>> CompleteTaskAsync(CompleteTaskRequest req, CancellationToken ct = default);

    /// <summary>Achieve a milestone. Blocked while any blocking task is open. Set-once.</summary>
    Task<Result<ProjectMilestone>> AchieveMilestoneAsync(AchieveMilestoneRequest req, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectScheduleView(
    int ProjectId,
    IReadOnlyList<ScheduleMilestoneRow> Milestones,
    IReadOnlyList<ScheduleTaskRow> Tasks,
    IReadOnlyList<ScheduleDependencyRow> Dependencies);

public sealed record ScheduleMilestoneRow(
    int Id,
    string Code,
    string Name,
    MilestoneType MilestoneType,
    ProjectMilestoneStatus Status,
    System.DateTime? TargetDate,
    System.DateTime? ForecastDate,
    System.DateTime? ActualDate,
    bool CustomerVisible,
    bool IsBillingMilestone,
    decimal? BillingAmount,
    int BlockingOpenTaskCount);

public sealed record ScheduleTaskRow(
    int Id,
    string Code,
    string Name,
    ProjectTaskType TaskType,
    ProjectTaskStatus Status,
    ProjectTaskPriority Priority,
    decimal? PercentComplete,
    System.DateTime? PlannedStart,
    System.DateTime? PlannedFinish,
    System.DateTime? ActualStart,
    System.DateTime? ActualFinish,
    bool IsCriticalPath,
    int? ProjectPhaseId,
    int? ProjectMilestoneId,
    bool IsMilestoneBlocking,
    IReadOnlyList<int> PredecessorTaskIds);

public sealed record ScheduleDependencyRow(
    int Id,
    int PredecessorTaskId,
    int SuccessorTaskId,
    DependencyType DependencyType,
    int LagDays);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateMilestoneRequest(
    int CustomerProjectId,
    string Code,
    string Name,
    string? Description = null,
    MilestoneType MilestoneType = MilestoneType.Internal,
    System.DateTime? TargetDate = null,
    int? ProjectPhaseId = null,
    bool CustomerVisible = false,
    bool IsBillingMilestone = false,
    decimal? BillingAmount = null,
    decimal? WeightPercent = null,
    int SortOrder = 0,
    string? CreatedBy = null);

public sealed record CreateTaskRequest(
    int CustomerProjectId,
    string Code,
    string Name,
    string? Description = null,
    ProjectTaskType TaskType = ProjectTaskType.Task,
    int? ProjectPhaseId = null,
    int? ParentTaskId = null,
    int? ProjectMilestoneId = null,
    bool IsMilestoneBlocking = true,
    string? ResponsibleOwner = null,
    ProjectTaskPriority Priority = ProjectTaskPriority.Normal,
    System.DateTime? PlannedStart = null,
    System.DateTime? PlannedFinish = null,
    decimal? WorkHoursPlanned = null,
    decimal? WeightPercent = null,
    int SortOrder = 0,
    string? CreatedBy = null);

public sealed record AddDependencyRequest(
    int PredecessorTaskId,
    int SuccessorTaskId,
    DependencyType DependencyType = DependencyType.FinishToStart,
    int LagDays = 0,
    string? CreatedBy = null);

public sealed record CompleteTaskRequest(int TaskId, string? CompletedBy = null);

public sealed record AchieveMilestoneRequest(int MilestoneId, string? AchievedBy = null);
