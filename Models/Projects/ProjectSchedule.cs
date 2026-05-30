// Theme B9 Wave 3 PR-8 (2026-05-30) — Project schedule spine.
//
// ProjectMilestone / ProjectTask / ProjectTaskDependency — the schedule + task
// layer that sits on the WBS backbone (ProjectPhase, hardened in PR-7). These
// are the inputs PR-9 turns into a Gantt + critical-path read.
//
// Conventions (per ProjectPhase precedent): these are CHILDREN of a
// CustomerProject — they carry CustomerProjectId and are tenant-scoped THROUGH
// the parent project; NO CompanyId of their own. xmin concurrency token.
// Field set follows the spec §6 (task fields), §17 (billing-milestone fields),
// and the schedule-summary / critical-path inputs.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectMilestone — a schedule/contractual/billing checkpoint.
    // The §20 gate (PR-8): a milestone cannot be ACHIEVED while any blocking
    // task is still open.
    // =====================================================================
    public class ProjectMilestone
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Optional WBS anchor (spec: "Link milestone to WBS").
        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public MilestoneType MilestoneType { get; set; } = MilestoneType.Internal;

        // Schedule (spec §schedule-summary): baseline frozen, forecast live,
        // target is the customer-facing commitment, actual stamped on achieve.
        public DateTime? BaselineDate { get; set; }
        public DateTime? ForecastDate { get; set; }
        public DateTime? TargetDate { get; set; }
        public DateTime? ActualDate { get; set; }

        public ProjectMilestoneStatus Status { get; set; } = ProjectMilestoneStatus.Open;

        // Progress contribution to a parent phase / project (0..100, CHECK).
        public decimal? WeightPercent { get; set; }

        public bool CustomerVisible { get; set; } = false;

        // Billing hooks (full billing engine lands in Wave 5 — these let a
        // milestone be flagged as a billing event now). spec §17.
        public bool IsBillingMilestone { get; set; } = false;
        public decimal? BillingAmount { get; set; }

        public int SortOrder { get; set; } = 0;

        // Set-once achieve stamp. Once Achieved, ActualDate + AchievedAt/By are
        // frozen; re-opening requires an explicit reopen path (not in v1).
        public DateTime? AchievedAt { get; set; }
        [StringLength(100)]
        public string? AchievedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectTask — a unit of work on the WBS. The §20-style gate (PR-8):
    // a task cannot be COMPLETED while a finish-to-start predecessor is open.
    // =====================================================================
    public class ProjectTask
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Optional WBS anchor + sub-task self-nesting + milestone this task feeds.
        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int? ParentTaskId { get; set; }
        public ProjectTask? ParentTask { get; set; }

        public int? ProjectMilestoneId { get; set; }
        public ProjectMilestone? ProjectMilestone { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public ProjectTaskType TaskType { get; set; } = ProjectTaskType.Task;

        // When this task feeds a milestone, does it gate that milestone's
        // achievement? (spec: blocking semantics). Default true.
        public bool IsMilestoneBlocking { get; set; } = true;

        // Critical-path flag — PR-9 computes + stamps this; settable here.
        public bool IsCriticalPath { get; set; } = false;

        public bool CustomerVisible { get; set; } = false;

        [StringLength(200)]
        public string? ResponsibleOwner { get; set; }
        [StringLength(64)]
        public string? ResponsibleDepartment { get; set; }

        public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.NotStarted;
        [StringLength(500)]
        public string? BlockReason { get; set; }

        public ProjectTaskPriority Priority { get; set; } = ProjectTaskPriority.Normal;

        // Progress 0..100 (CHECK).
        public decimal? PercentComplete { get; set; }

        // Schedule (spec §6): planned / forecast / actual start+finish.
        public DateTime? PlannedStart { get; set; }
        public DateTime? PlannedFinish { get; set; }
        public DateTime? ForecastStart { get; set; }
        public DateTime? ForecastFinish { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualFinish { get; set; }

        // Constraint (spec §6: constraint type + date) — drives scheduling in PR-9.
        public TaskConstraintType ConstraintType { get; set; } = TaskConstraintType.None;
        public DateTime? ConstraintDate { get; set; }

        // Work + cost (resource assignment + full cost land in later waves).
        public decimal? WorkHoursPlanned { get; set; }
        public decimal? WorkHoursActual { get; set; }
        public decimal? CostPlanned { get; set; }
        public decimal? CostActual { get; set; }

        public decimal? WeightPercent { get; set; }
        public int SortOrder { get; set; } = 0;

        // Set-once complete stamp.
        public DateTime? CompletedAt { get; set; }
        [StringLength(100)]
        public string? CompletedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectTaskDependency — a precedence edge between two tasks in the same
    // project. Both FKs are NoAction (a task pair has two paths to the table —
    // cascade would create multiple cascade paths). The service enforces
    // same-project, no self-loop, no duplicate, and NO CYCLE.
    // =====================================================================
    public class ProjectTaskDependency
    {
        public int Id { get; set; }

        public int PredecessorTaskId { get; set; }
        public ProjectTask? PredecessorTask { get; set; }

        public int SuccessorTaskId { get; set; }
        public ProjectTask? SuccessorTask { get; set; }

        public DependencyType DependencyType { get; set; } = DependencyType.FinishToStart;

        // Lead (negative) / lag (positive) in days.
        public int LagDays { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums
    // ---------------------------------------------------------------------

    public enum MilestoneType
    {
        Internal = 0,
        Customer = 1,
        Contractual = 2,
        Billing = 3,
        Gate = 4,        // phase/stage gate
        Delivery = 5
    }

    public enum ProjectMilestoneStatus
    {
        Open = 0,
        Achieved = 1,
        Missed = 2,
        Cancelled = 3
    }

    public enum ProjectTaskType
    {
        Task = 0,
        Summary = 1,          // rolls up children
        LevelOfEffort = 2,    // ongoing support work
        Administrative = 3
    }

    public enum ProjectTaskStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Blocked = 2,
        Complete = 3,
        Cancelled = 4
    }

    // Normal is the 0 value so the CLR default == the model/DB default
    // (avoids the EF enum-sentinel trap where an explicit non-default 0 would
    // be overwritten by the DB default).
    public enum ProjectTaskPriority
    {
        Normal = 0,
        Low = 1,
        High = 2,
        Critical = 3
    }

    public enum TaskConstraintType
    {
        None = 0,
        StartNoEarlierThan = 1,
        FinishNoLaterThan = 2,
        MustStartOn = 3,
        MustFinishOn = 4
    }

    public enum DependencyType
    {
        FinishToStart = 0,   // default — successor starts after predecessor finishes
        StartToStart = 1,
        FinishToFinish = 2,
        StartToFinish = 3
    }
}
