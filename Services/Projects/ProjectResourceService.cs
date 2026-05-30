// Theme B9 Wave 4 PR-11 (2026-05-30) — ProjectResourceService impl.
//
// Tenant-scoped through the parent CustomerProject. Plans → assignments →
// time → expenses. Every incoming FK on a write is scoped to the project's
// company (session-30 lesson, reinforced by the PR-10 Codex cross-company find).
// Time/expense capture is blocked once the project is Closed/Cancelled.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectResourceService : IProjectResourceService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectResourceService> _log;

    public ProjectResourceService(AppDbContext db, ITenantContext tenant, ILogger<ProjectResourceService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    // Resolve + tenant-check a project; returns its CompanyId + Status.
    private async Task<(bool ok, string? err, int? companyId, CustomerProjectStatus status)> ProjectInfoAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, "CustomerProjectId must be > 0.", null, default);
        var row = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.CompanyId, p.Status })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? (false, $"Customer project {projectId} not found in your tenant scope.", null, default)
            : (true, null, row.CompanyId, row.Status);
    }

    private Task<bool> PhaseInProjectAsync(int phaseId, int projectId, CancellationToken ct)
        => _db.ProjectPhases.AnyAsync(p => p.Id == phaseId && p.CustomerProjectId == projectId, ct);
    private Task<bool> TaskInProjectAsync(int taskId, int projectId, CancellationToken ct)
        => _db.ProjectTasks.AnyAsync(t => t.Id == taskId && t.CustomerProjectId == projectId, ct);

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<Result<ProjectResourcingView>> GetResourcingAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err, _, _) = await ProjectInfoAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectResourcingView>(err!);

        var plans = await _db.ProjectResourcePlans
            .Where(p => p.CustomerProjectId == projectId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);
        var assignments = await _db.ProjectResourceAssignments
            .Where(a => a.CustomerProjectId == projectId).OrderBy(a => a.Id).ToListAsync(ct);
        var times = await _db.ProjectTimeEntries
            .Where(t => t.CustomerProjectId == projectId).OrderBy(t => t.WorkDate).ThenBy(t => t.Id).ToListAsync(ct);
        var expenses = await _db.ProjectExpenses
            .Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.ExpenseDate).ThenBy(x => x.Id).ToListAsync(ct);

        bool TimeCounts(ProjectTimeEntry t) => t.Status != TimeEntryStatus.Rejected;
        bool ExpenseCounts(ProjectExpense x) => x.Status != ProjectExpenseStatus.Rejected;

        // Actual hours/cost per assignment + per plan.
        var actualHoursByAssignment = times.Where(t => t.ProjectResourceAssignmentId.HasValue && TimeCounts(t))
            .GroupBy(t => t.ProjectResourceAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Hours));
        var actualCostByAssignment = times.Where(t => t.ProjectResourceAssignmentId.HasValue && TimeCounts(t))
            .GroupBy(t => t.ProjectResourceAssignmentId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ComputedCost ?? 0m));
        var assignmentsByPlan = assignments.Where(a => a.ProjectResourcePlanId.HasValue)
            .GroupBy(a => a.ProjectResourcePlanId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var planRows = plans.Select(p =>
        {
            var planAssignments = assignmentsByPlan.TryGetValue(p.Id, out var la) ? la : new List<ProjectResourceAssignment>();
            var assignedHours = planAssignments.Sum(a => a.PlannedHours ?? 0m);
            var actualHours = planAssignments.Sum(a => actualHoursByAssignment.TryGetValue(a.Id, out var h) ? h : 0m);
            return new ResourcePlanRow(p.Id, p.Code, p.Name, p.ResourceType, p.Status, p.RoleOrSkill,
                p.PlannedHours, p.PlannedCost, p.ProjectPhaseId, p.WorkCenterId, assignedHours, actualHours);
        }).ToList();

        var assignmentRows = assignments.Select(a => new AssignmentRow(
            a.Id, a.Code, a.ResourceType, a.Status, a.EmployeeId, a.WorkCenterId, a.AssetId,
            a.ProjectResourcePlanId, a.AllocationPercent, a.PlannedHours, a.CostRate,
            actualHoursByAssignment.TryGetValue(a.Id, out var ah) ? ah : 0m,
            actualCostByAssignment.TryGetValue(a.Id, out var ac) ? ac : 0m)).ToList();

        var timeRows = times.Select(t => new TimeEntryRow(
            t.Id, t.WorkDate, t.Hours, t.Category, t.IsBillable, t.ComputedCost, t.Status,
            t.EmployeeId, t.ProjectResourceAssignmentId, t.ProjectTaskId)).ToList();

        var expenseRows = expenses.Select(x => new ExpenseRow(
            x.Id, x.Code, x.Category, x.Amount, x.ExpenseDate, x.IsBillable, x.Status, x.EmployeeId)).ToList();

        var countedTimes = times.Where(TimeCounts).ToList();
        var countedExpenses = expenses.Where(ExpenseCounts).ToList();

        return Result.Success(new ProjectResourcingView(
            projectId,
            plans.Where(p => p.Status != ProjectResourcePlanStatus.Cancelled).Sum(p => p.PlannedHours ?? 0m),
            plans.Where(p => p.Status != ProjectResourcePlanStatus.Cancelled).Sum(p => p.PlannedCost ?? 0m),
            countedTimes.Sum(t => t.Hours),
            countedTimes.Sum(t => t.ComputedCost ?? 0m),
            countedTimes.Where(t => t.IsBillable).Sum(t => t.Hours),
            countedExpenses.Sum(x => x.Amount),
            countedExpenses.Where(x => x.IsBillable).Sum(x => x.Amount),
            planRows, assignmentRows, timeRows, expenseRows));
    }

    // ------------------------------------------------------------------
    // Create plan
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreatePlanAsync(CreateResourcePlanRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code)) return Result.Failure<int>("A plan Code is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) return Result.Failure<int>("A plan Name is required.");
        var (ok, err, companyId, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (req.PlannedHours is < 0 || req.PlannedRate is < 0 || req.PlannedCost is < 0)
            return Result.Failure<int>("Planned hours/rate/cost cannot be negative.");

        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (req.ProjectTaskId.HasValue && !await TaskInProjectAsync(req.ProjectTaskId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Task {req.ProjectTaskId} is not in this project.");
        if (req.WorkCenterId.HasValue && !await _db.WorkCenters.AnyAsync(w => w.Id == req.WorkCenterId.Value && w.CompanyId == companyId, ct))
            return Result.Failure<int>($"Work center {req.WorkCenterId} does not belong to this project's company.");

        var code = req.Code.Trim();
        if (await _db.ProjectResourcePlans.AnyAsync(p => p.CustomerProjectId == req.CustomerProjectId && p.Code == code, ct))
            return Result.Failure<int>($"Plan Code '{code}' already exists in this project.");

        var plan = new ProjectResourcePlan
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectPhaseId = req.ProjectPhaseId,
            ProjectTaskId = req.ProjectTaskId,
            WorkCenterId = req.WorkCenterId,
            Code = code,
            Name = req.Name.Trim(),
            Description = req.Description,
            ResourceType = req.ResourceType,
            RoleOrSkill = string.IsNullOrWhiteSpace(req.RoleOrSkill) ? null : req.RoleOrSkill.Trim(),
            PlannedHours = req.PlannedHours,
            PlannedRate = req.PlannedRate,
            PlannedCost = req.PlannedCost ?? (req.PlannedHours.HasValue && req.PlannedRate.HasValue ? req.PlannedHours * req.PlannedRate : null),
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            SortOrder = req.SortOrder,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectResourcePlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return Result.Success(plan.Id);
    }

    // ------------------------------------------------------------------
    // Create assignment — tenant-scope EVERY incoming FK.
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateAssignmentAsync(CreateAssignmentRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code)) return Result.Failure<int>("An assignment Code is required.");
        var (ok, err, companyId, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (req.AllocationPercent is < 0 or > 100) return Result.Failure<int>("AllocationPercent must be 0..100.");
        if (req.PlannedHours is < 0 || req.CostRate is < 0 || req.BillRate is < 0)
            return Result.Failure<int>("Planned hours / rates cannot be negative.");

        if (req.ProjectResourcePlanId.HasValue && !await _db.ProjectResourcePlans.AnyAsync(
                p => p.Id == req.ProjectResourcePlanId.Value && p.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Plan {req.ProjectResourcePlanId} is not in this project.");
        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (req.ProjectTaskId.HasValue && !await TaskInProjectAsync(req.ProjectTaskId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Task {req.ProjectTaskId} is not in this project.");
        if (req.EmployeeId.HasValue && !await _db.Employees.AnyAsync(x => x.Id == req.EmployeeId.Value && x.CompanyId == companyId, ct))
            return Result.Failure<int>($"Employee {req.EmployeeId} does not belong to this project's company.");
        if (req.WorkCenterId.HasValue && !await _db.WorkCenters.AnyAsync(w => w.Id == req.WorkCenterId.Value && w.CompanyId == companyId, ct))
            return Result.Failure<int>($"Work center {req.WorkCenterId} does not belong to this project's company.");
        if (req.AssetId.HasValue && !await _db.Assets.AnyAsync(a => a.Id == req.AssetId.Value && a.CompanyId == companyId, ct))
            return Result.Failure<int>($"Asset {req.AssetId} does not belong to this project's company.");

        var code = req.Code.Trim();
        if (await _db.ProjectResourceAssignments.AnyAsync(a => a.CustomerProjectId == req.CustomerProjectId && a.Code == code, ct))
            return Result.Failure<int>($"Assignment Code '{code}' already exists in this project.");

        var asg = new ProjectResourceAssignment
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectResourcePlanId = req.ProjectResourcePlanId,
            ProjectPhaseId = req.ProjectPhaseId,
            ProjectTaskId = req.ProjectTaskId,
            EmployeeId = req.EmployeeId,
            WorkCenterId = req.WorkCenterId,
            AssetId = req.AssetId,
            Code = code,
            ResourceType = req.ResourceType,
            AllocationPercent = req.AllocationPercent,
            PlannedHours = req.PlannedHours,
            CostRate = req.CostRate,
            BillRate = req.BillRate,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectResourceAssignments.Add(asg);
        await _db.SaveChangesAsync(ct);
        return Result.Success(asg.Id);
    }

    // ------------------------------------------------------------------
    // Record time entry — blocked on Closed/Cancelled project. Computes cost.
    // ------------------------------------------------------------------
    public async Task<Result<int>> RecordTimeEntryAsync(RecordTimeEntryRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        if (req.Hours < 0) return Result.Failure<int>("Hours cannot be negative.");
        if (req.CostRate is < 0 || req.BillRate is < 0) return Result.Failure<int>("Rates cannot be negative.");
        var (ok, err, companyId, status) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (status == CustomerProjectStatus.Closed || status == CustomerProjectStatus.Cancelled)
            return Result.Failure<int>($"Cannot log time — project is {status}.");

        // Resolve cost rate: explicit, else inherit from the assignment.
        decimal? costRate = req.CostRate;
        decimal? billRate = req.BillRate;
        if (req.ProjectResourceAssignmentId.HasValue)
        {
            var asg = await _db.ProjectResourceAssignments
                .Where(a => a.Id == req.ProjectResourceAssignmentId.Value && a.CustomerProjectId == req.CustomerProjectId)
                .Select(a => new { a.CostRate, a.BillRate })
                .FirstOrDefaultAsync(ct);
            if (asg is null) return Result.Failure<int>($"Assignment {req.ProjectResourceAssignmentId} is not in this project.");
            costRate ??= asg.CostRate;
            billRate ??= asg.BillRate;
        }
        if (req.ProjectTaskId.HasValue && !await TaskInProjectAsync(req.ProjectTaskId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Task {req.ProjectTaskId} is not in this project.");
        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (req.EmployeeId.HasValue && !await _db.Employees.AnyAsync(x => x.Id == req.EmployeeId.Value && x.CompanyId == companyId, ct))
            return Result.Failure<int>($"Employee {req.EmployeeId} does not belong to this project's company.");

        var entry = new ProjectTimeEntry
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectResourceAssignmentId = req.ProjectResourceAssignmentId,
            ProjectTaskId = req.ProjectTaskId,
            ProjectPhaseId = req.ProjectPhaseId,
            EmployeeId = req.EmployeeId,
            WorkDate = req.WorkDate == default ? DateTime.UtcNow.Date : req.WorkDate,
            Hours = req.Hours,
            Category = req.Category,
            IsBillable = req.IsBillable,
            CostRate = costRate,
            BillRate = billRate,
            ComputedCost = costRate.HasValue ? req.Hours * costRate.Value : (decimal?)null,
            Notes = req.Notes,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectTimeEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return Result.Success(entry.Id);
    }

    // ------------------------------------------------------------------
    // Record expense — blocked on Closed/Cancelled project.
    // ------------------------------------------------------------------
    public async Task<Result<int>> RecordExpenseAsync(RecordExpenseRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code)) return Result.Failure<int>("An expense Code is required.");
        if (req.Amount < 0) return Result.Failure<int>("Amount cannot be negative.");
        var (ok, err, companyId, status) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (status == CustomerProjectStatus.Closed || status == CustomerProjectStatus.Cancelled)
            return Result.Failure<int>($"Cannot record expense — project is {status}.");

        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (req.ProjectTaskId.HasValue && !await TaskInProjectAsync(req.ProjectTaskId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Task {req.ProjectTaskId} is not in this project.");
        if (req.EmployeeId.HasValue && !await _db.Employees.AnyAsync(x => x.Id == req.EmployeeId.Value && x.CompanyId == companyId, ct))
            return Result.Failure<int>($"Employee {req.EmployeeId} does not belong to this project's company.");

        var code = req.Code.Trim();
        if (await _db.ProjectExpenses.AnyAsync(x => x.CustomerProjectId == req.CustomerProjectId && x.Code == code, ct))
            return Result.Failure<int>($"Expense Code '{code}' already exists in this project.");

        var exp = new ProjectExpense
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectPhaseId = req.ProjectPhaseId,
            ProjectTaskId = req.ProjectTaskId,
            EmployeeId = req.EmployeeId,
            Code = code,
            Description = req.Description,
            Category = req.Category,
            Amount = req.Amount,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            ExpenseDate = req.ExpenseDate == default ? DateTime.UtcNow.Date : req.ExpenseDate,
            IsBillable = req.IsBillable,
            IsReimbursable = req.IsReimbursable,
            ReceiptReference = string.IsNullOrWhiteSpace(req.ReceiptReference) ? null : req.ReceiptReference.Trim(),
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectExpenses.Add(exp);
        await _db.SaveChangesAsync(ct);
        return Result.Success(exp.Id);
    }

    // ------------------------------------------------------------------
    // Approve time entry — set-once.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectTimeEntry>> ApproveTimeEntryAsync(int timeEntryId, string? approvedBy = null, CancellationToken ct = default)
    {
        if (timeEntryId <= 0) return Result.Failure<ProjectTimeEntry>("A valid time-entry id is required.");
        var entry = await _db.ProjectTimeEntries.FirstOrDefaultAsync(t => t.Id == timeEntryId, ct);
        if (entry is null) return Result.Failure<ProjectTimeEntry>($"Time entry {timeEntryId} not found.");
        var (ok, err, _, _) = await ProjectInfoAsync(entry.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectTimeEntry>(err!);

        if (entry.Status == TimeEntryStatus.Approved)
            return Result.Failure<ProjectTimeEntry>("Time entry is already approved.");
        if (entry.Status == TimeEntryStatus.Rejected)
            return Result.Failure<ProjectTimeEntry>("Time entry is rejected and cannot be approved.");

        entry.Status = TimeEntryStatus.Approved;
        entry.ApprovedAt = DateTime.UtcNow;
        entry.ApprovedBy = approvedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(entry);
    }

    // ------------------------------------------------------------------
    // Approve expense — set-once.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectExpense>> ApproveExpenseAsync(int expenseId, string? approvedBy = null, CancellationToken ct = default)
    {
        if (expenseId <= 0) return Result.Failure<ProjectExpense>("A valid expense id is required.");
        var exp = await _db.ProjectExpenses.FirstOrDefaultAsync(x => x.Id == expenseId, ct);
        if (exp is null) return Result.Failure<ProjectExpense>($"Expense {expenseId} not found.");
        var (ok, err, _, _) = await ProjectInfoAsync(exp.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectExpense>(err!);

        if (exp.Status == ProjectExpenseStatus.Approved || exp.Status == ProjectExpenseStatus.Reimbursed)
            return Result.Failure<ProjectExpense>($"Expense is already {exp.Status}.");
        if (exp.Status == ProjectExpenseStatus.Rejected)
            return Result.Failure<ProjectExpense>("Expense is rejected and cannot be approved.");

        exp.Status = ProjectExpenseStatus.Approved;
        exp.ApprovedAt = DateTime.UtcNow;
        exp.ApprovedBy = approvedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(exp);
    }
}
