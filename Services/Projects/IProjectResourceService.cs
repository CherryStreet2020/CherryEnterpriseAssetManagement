// Theme B9 Wave 4 PR-11 (2026-05-30) — IProjectResourceService.
//
// The labor-side execution layer: planned resource demand → concrete
// assignments → actual time → non-labor expenses. Feeds the Wave 5 margin
// engine (planned vs actual hours/cost). ADR-025: PageModels / voice read
// THROUGH this service. Tenant scope flows THROUGH the parent CustomerProject;
// every incoming FK on a write is scoped to the project's company.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectResourceService
{
    /// <summary>Plans, assignments, time entries, expenses, and planned-vs-actual totals (read).</summary>
    Task<Result<ProjectResourcingView>> GetResourcingAsync(int projectId, CancellationToken ct = default);

    Task<Result<int>> CreatePlanAsync(CreateResourcePlanRequest req, CancellationToken ct = default);

    /// <summary>Assign a concrete resource. Tenant-scopes every incoming FK (plan/phase/task/employee/workcenter/asset).</summary>
    Task<Result<int>> CreateAssignmentAsync(CreateAssignmentRequest req, CancellationToken ct = default);

    /// <summary>Log actual time. Blocked when the project is Closed/Cancelled. Computes cost from rate×hours.</summary>
    Task<Result<int>> RecordTimeEntryAsync(RecordTimeEntryRequest req, CancellationToken ct = default);

    /// <summary>Record a non-labor expense. Blocked when the project is Closed/Cancelled.</summary>
    Task<Result<int>> RecordExpenseAsync(RecordExpenseRequest req, CancellationToken ct = default);

    /// <summary>Approve a time entry (set-once).</summary>
    Task<Result<ProjectTimeEntry>> ApproveTimeEntryAsync(int timeEntryId, string? approvedBy = null, CancellationToken ct = default);

    /// <summary>Approve an expense (set-once).</summary>
    Task<Result<ProjectExpense>> ApproveExpenseAsync(int expenseId, string? approvedBy = null, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectResourcingView(
    int ProjectId,
    decimal PlannedHours,
    decimal PlannedLaborCost,
    decimal ActualHours,
    decimal ActualLaborCost,
    decimal BillableHours,
    decimal ExpenseTotal,
    decimal BillableExpenseTotal,
    IReadOnlyList<ResourcePlanRow> Plans,
    IReadOnlyList<AssignmentRow> Assignments,
    IReadOnlyList<TimeEntryRow> TimeEntries,
    IReadOnlyList<ExpenseRow> Expenses);

public sealed record ResourcePlanRow(
    int Id, string Code, string Name, ProjectResourceType ResourceType,
    ProjectResourcePlanStatus Status, string? RoleOrSkill,
    decimal? PlannedHours, decimal? PlannedCost, int? ProjectPhaseId, int? WorkCenterId,
    decimal AssignedHoursAgainstPlan, decimal ActualHoursAgainstPlan);

public sealed record AssignmentRow(
    int Id, string Code, ProjectResourceType ResourceType, ProjectAssignmentStatus Status,
    int? EmployeeId, int? WorkCenterId, int? AssetId, int? ProjectResourcePlanId,
    decimal? AllocationPercent, decimal? PlannedHours, decimal? CostRate,
    decimal ActualHours, decimal ActualCost);

public sealed record TimeEntryRow(
    int Id, System.DateTime WorkDate, decimal Hours, TimeEntryCategory Category,
    bool IsBillable, decimal? ComputedCost, TimeEntryStatus Status,
    int? EmployeeId, int? ProjectResourceAssignmentId, int? ProjectTaskId);

public sealed record ExpenseRow(
    int Id, string Code, ProjectExpenseCategory Category, decimal Amount,
    System.DateTime ExpenseDate, bool IsBillable, ProjectExpenseStatus Status, int? EmployeeId);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateResourcePlanRequest(
    int CustomerProjectId,
    string Code,
    string Name,
    string? Description = null,
    ProjectResourceType ResourceType = ProjectResourceType.Labor,
    string? RoleOrSkill = null,
    decimal? PlannedHours = null,
    decimal? PlannedRate = null,
    decimal? PlannedCost = null,
    string? Currency = null,
    System.DateTime? StartDate = null,
    System.DateTime? EndDate = null,
    int? ProjectPhaseId = null,
    int? ProjectTaskId = null,
    int? WorkCenterId = null,
    int SortOrder = 0,
    string? CreatedBy = null);

public sealed record CreateAssignmentRequest(
    int CustomerProjectId,
    string Code,
    ProjectResourceType ResourceType = ProjectResourceType.Labor,
    int? ProjectResourcePlanId = null,
    int? ProjectPhaseId = null,
    int? ProjectTaskId = null,
    int? EmployeeId = null,
    int? WorkCenterId = null,
    int? AssetId = null,
    decimal? AllocationPercent = null,
    decimal? PlannedHours = null,
    decimal? CostRate = null,
    decimal? BillRate = null,
    string? Currency = null,
    System.DateTime? StartDate = null,
    System.DateTime? EndDate = null,
    string? CreatedBy = null);

public sealed record RecordTimeEntryRequest(
    int CustomerProjectId,
    System.DateTime WorkDate,
    decimal Hours,
    int? ProjectResourceAssignmentId = null,
    int? ProjectTaskId = null,
    int? ProjectPhaseId = null,
    int? EmployeeId = null,
    TimeEntryCategory Category = TimeEntryCategory.Regular,
    bool IsBillable = true,
    decimal? CostRate = null,
    decimal? BillRate = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record RecordExpenseRequest(
    int CustomerProjectId,
    string Code,
    decimal Amount,
    System.DateTime ExpenseDate,
    ProjectExpenseCategory Category = ProjectExpenseCategory.Travel,
    string? Description = null,
    string? Currency = null,
    int? ProjectPhaseId = null,
    int? ProjectTaskId = null,
    int? EmployeeId = null,
    bool IsBillable = true,
    bool IsReimbursable = false,
    string? ReceiptReference = null,
    string? CreatedBy = null);
