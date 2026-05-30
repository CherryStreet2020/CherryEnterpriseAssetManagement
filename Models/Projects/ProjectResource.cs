// Theme B9 Wave 4 PR-11 (2026-05-30) — Project resource + labor + expense spine.
//
// ProjectResourcePlan / ProjectResourceAssignment / ProjectTimeEntry /
// ProjectExpense — the labor-side execution layer. Planned resource demand →
// concrete assignments of people/machines/work-centers → actual time logged →
// non-labor expenses. These feed the Wave 5 margin engine (planned vs actual).
//
// Conventions (per ProjectSchedule / ProjectProcurement precedent): CHILDREN of
// a CustomerProject — carry CustomerProjectId, tenant-scoped THROUGH the parent
// project; NO CompanyId. Each CASCADEs from the project; every optional peg
// (phase/task/plan/assignment/employee/workcenter/asset) is SET NULL so a
// single cascade path (project→child) keeps deletes clean. xmin concurrency;
// enum DB defaults == the 0-member model default.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models.Projects
{
    // =====================================================================
    // ProjectResourcePlan — the PLANNED resource demand for a project / phase /
    // task ("we need 120h of CNC machinist + 40h of 5-axis mill time").
    // =====================================================================
    public class ProjectResourcePlan
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int? ProjectTaskId { get; set; }
        public ProjectTask? ProjectTask { get; set; }

        // Optional work-center the demand targets (machine/labor capacity).
        public int? WorkCenterId { get; set; }
        public Abs.FixedAssets.Models.Production.WorkCenter? WorkCenter { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public ProjectResourceType ResourceType { get; set; } = ProjectResourceType.Labor;

        // The role / skill / trade the plan calls for ("CNC Machinist", "ME").
        [StringLength(128)]
        public string? RoleOrSkill { get; set; }

        public decimal? PlannedHours { get; set; }
        public decimal? PlannedRate { get; set; }   // cost per hour
        public decimal? PlannedCost { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public ProjectResourcePlanStatus Status { get; set; } = ProjectResourcePlanStatus.Draft;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectResourceAssignment — a concrete resource (employee / asset /
    // work-center) committed to the project, optionally against a plan + task.
    // =====================================================================
    public class ProjectResourceAssignment
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectResourcePlanId { get; set; }
        public ProjectResourcePlan? ProjectResourcePlan { get; set; }

        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int? ProjectTaskId { get; set; }
        public ProjectTask? ProjectTask { get; set; }

        // The assigned resource — one of these (all optional / SET NULL).
        public int? EmployeeId { get; set; }
        public Abs.FixedAssets.Models.Masters.Employee? Employee { get; set; }

        public int? WorkCenterId { get; set; }
        public Abs.FixedAssets.Models.Production.WorkCenter? WorkCenter { get; set; }

        public int? AssetId { get; set; }
        public Asset? Asset { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        public ProjectResourceType ResourceType { get; set; } = ProjectResourceType.Labor;

        // % of the resource's capacity committed to this project (0..100, CHECK).
        public decimal? AllocationPercent { get; set; }

        public decimal? PlannedHours { get; set; }
        public decimal? CostRate { get; set; }   // cost per hour
        public decimal? BillRate { get; set; }    // billable per hour
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public ProjectAssignmentStatus Status { get; set; } = ProjectAssignmentStatus.Planned;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public ICollection<ProjectTimeEntry> TimeEntries { get; set; } = new List<ProjectTimeEntry>();

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectTimeEntry — ACTUAL labor time logged against the project. Set-once
    // approve stamp. CASCADEs from the project; the assignment link is SET NULL
    // (single cascade path project→time-entry).
    // =====================================================================
    public class ProjectTimeEntry
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectResourceAssignmentId { get; set; }
        public ProjectResourceAssignment? Assignment { get; set; }

        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int? ProjectTaskId { get; set; }
        public ProjectTask? ProjectTask { get; set; }

        public int? EmployeeId { get; set; }
        public Abs.FixedAssets.Models.Masters.Employee? Employee { get; set; }

        public DateTime WorkDate { get; set; }

        public decimal Hours { get; set; }

        public TimeEntryCategory Category { get; set; } = TimeEntryCategory.Regular;

        public bool IsBillable { get; set; } = true;

        // Snapshot rates + computed cost (frozen at entry; the margin engine reads these).
        public decimal? CostRate { get; set; }
        public decimal? BillRate { get; set; }
        public decimal? ComputedCost { get; set; }

        public TimeEntryStatus Status { get; set; } = TimeEntryStatus.Draft;

        // Set-once approve stamp.
        public DateTime? ApprovedAt { get; set; }
        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // =====================================================================
    // ProjectExpense — a non-labor expense charged to the project (travel,
    // materials bought direct, lodging…). Set-once approve stamp.
    // =====================================================================
    public class ProjectExpense
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        public int? ProjectTaskId { get; set; }
        public ProjectTask? ProjectTask { get; set; }

        public int? EmployeeId { get; set; }
        public Abs.FixedAssets.Models.Masters.Employee? Employee { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public ProjectExpenseCategory Category { get; set; } = ProjectExpenseCategory.Travel;

        public decimal Amount { get; set; }
        [Required, StringLength(8)]
        public string Currency { get; set; } = "USD";

        public DateTime ExpenseDate { get; set; }

        public bool IsBillable { get; set; } = true;
        public bool IsReimbursable { get; set; } = false;

        public ProjectExpenseStatus Status { get; set; } = ProjectExpenseStatus.Draft;

        [StringLength(128)]
        public string? ReceiptReference { get; set; }

        // Set-once approve stamp.
        public DateTime? ApprovedAt { get; set; }
        [StringLength(100)]
        public string? ApprovedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    // ---------------------------------------------------------------------
    // Enums — the 0 member is the CLR/model default (== DB default).
    // ---------------------------------------------------------------------

    public enum ProjectResourceType
    {
        Labor = 0,
        Machine = 1,
        Subcontractor = 2,
        Equipment = 3,
        Other = 4
    }

    public enum ProjectResourcePlanStatus
    {
        Draft = 0,
        Approved = 1,
        Closed = 2,
        Cancelled = 3
    }

    public enum ProjectAssignmentStatus
    {
        Planned = 0,
        Active = 1,
        Completed = 2,
        Released = 3,
        Cancelled = 4
    }

    public enum TimeEntryCategory
    {
        Regular = 0,
        Overtime = 1,
        DoubleTime = 2,
        Travel = 3,
        Other = 4
    }

    public enum TimeEntryStatus
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        Rejected = 3
    }

    public enum ProjectExpenseCategory
    {
        Travel = 0,
        Materials = 1,
        Equipment = 2,
        Subcontract = 3,
        Lodging = 4,
        Meals = 5,
        Other = 6
    }

    public enum ProjectExpenseStatus
    {
        Draft = 0,
        Submitted = 1,
        Approved = 2,
        Reimbursed = 3,
        Rejected = 4
    }
}
