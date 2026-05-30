// Theme B9 Wave 3 PR-8 (2026-05-30) — ProjectScheduleService impl.
//
// Tenant-scoped through the parent CustomerProject. Hosts the milestone /
// task / dependency gates (achieve-blocked-by-open-task, complete-blocked-by-
// open-FS-predecessor, dependency self/dup/cross-project/cycle rejection).

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

public sealed class ProjectScheduleService : IProjectScheduleService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectScheduleService> _log;

    public ProjectScheduleService(AppDbContext db, ITenantContext tenant, ILogger<ProjectScheduleService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    private async Task<(bool ok, string? err)> ProjectVisibleAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, "CustomerProjectId must be > 0.");
        var seen = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .AnyAsync(ct);
        return seen ? (true, null) : (false, $"Customer project {projectId} not found in your tenant scope.");
    }

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<Result<ProjectScheduleView>> GetScheduleAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err) = await ProjectVisibleAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectScheduleView>(err!);

        var milestones = await _db.ProjectMilestones
            .Where(m => m.CustomerProjectId == projectId)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .ToListAsync(ct);

        var tasks = await _db.ProjectTasks
            .Where(t => t.CustomerProjectId == projectId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
            .ToListAsync(ct);
        var taskIds = tasks.Select(t => t.Id).ToHashSet();

        var deps = await _db.ProjectTaskDependencies
            .Where(d => d.CustomerProjectId == projectId)
            .ToListAsync(ct);

        var predsByTask = deps.GroupBy(d => d.SuccessorTaskId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(x => x.PredecessorTaskId).ToList());

        // Blocking-open-task count per milestone.
        var openBlockingByMs = tasks
            .Where(t => t.ProjectMilestoneId.HasValue && t.IsMilestoneBlocking && !IsTaskClosed(t.Status))
            .GroupBy(t => t.ProjectMilestoneId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var msRows = milestones.Select(m => new ScheduleMilestoneRow(
            m.Id, m.Code, m.Name, m.MilestoneType, m.Status, m.TargetDate, m.ForecastDate, m.ActualDate,
            m.CustomerVisible, m.IsBillingMilestone, m.BillingAmount,
            openBlockingByMs.TryGetValue(m.Id, out var c) ? c : 0)).ToList();

        var taskRows = tasks.Select(t => new ScheduleTaskRow(
            t.Id, t.Code, t.Name, t.TaskType, t.Status, t.Priority, t.PercentComplete,
            t.PlannedStart, t.PlannedFinish, t.ActualStart, t.ActualFinish, t.IsCriticalPath,
            t.ProjectPhaseId, t.ProjectMilestoneId, t.IsMilestoneBlocking,
            predsByTask.TryGetValue(t.Id, out var pl) ? pl : new List<int>())).ToList();

        var depRows = deps
            .Where(d => taskIds.Contains(d.PredecessorTaskId) && taskIds.Contains(d.SuccessorTaskId))
            .Select(d => new ScheduleDependencyRow(d.Id, d.PredecessorTaskId, d.SuccessorTaskId, d.DependencyType, d.LagDays))
            .ToList();

        return Result.Success(new ProjectScheduleView(projectId, msRows, taskRows, depRows));
    }

    private static bool IsTaskClosed(ProjectTaskStatus s)
        => s == ProjectTaskStatus.Complete || s == ProjectTaskStatus.Cancelled;

    // ------------------------------------------------------------------
    // Create milestone
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateMilestoneAsync(CreateMilestoneRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code))
            return Result.Failure<int>("A milestone Code is required.");
        if (string.IsNullOrWhiteSpace(req.Name))
            return Result.Failure<int>("A milestone Name is required.");
        var (ok, err) = await ProjectVisibleAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (req.BillingAmount is < 0) return Result.Failure<int>("BillingAmount cannot be negative.");
        if (req.WeightPercent is < 0 or > 100) return Result.Failure<int>("WeightPercent must be 0..100.");

        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");

        var code = req.Code.Trim();
        if (await _db.ProjectMilestones.AnyAsync(m => m.CustomerProjectId == req.CustomerProjectId && m.Code == code, ct))
            return Result.Failure<int>($"Milestone Code '{code}' already exists in this project.");

        var ms = new ProjectMilestone
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectPhaseId = req.ProjectPhaseId,
            Code = code,
            Name = req.Name.Trim(),
            Description = req.Description,
            MilestoneType = req.MilestoneType,
            TargetDate = req.TargetDate,
            ForecastDate = req.TargetDate,
            CustomerVisible = req.CustomerVisible,
            IsBillingMilestone = req.IsBillingMilestone,
            BillingAmount = req.BillingAmount,
            WeightPercent = req.WeightPercent,
            SortOrder = req.SortOrder,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectMilestones.Add(ms);
        await _db.SaveChangesAsync(ct);
        return Result.Success(ms.Id);
    }

    // ------------------------------------------------------------------
    // Create task — tenant-scope EVERY incoming FK (phase/parent/milestone)
    // to the same project (session-30 lesson).
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateTaskAsync(CreateTaskRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code))
            return Result.Failure<int>("A task Code is required.");
        if (string.IsNullOrWhiteSpace(req.Name))
            return Result.Failure<int>("A task Name is required.");
        var (ok, err) = await ProjectVisibleAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (req.WeightPercent is < 0 or > 100) return Result.Failure<int>("WeightPercent must be 0..100.");
        if (req.WorkHoursPlanned is < 0) return Result.Failure<int>("WorkHoursPlanned cannot be negative.");

        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (req.ParentTaskId.HasValue && !await _db.ProjectTasks.AnyAsync(
                t => t.Id == req.ParentTaskId.Value && t.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Parent task {req.ParentTaskId} is not in this project.");
        if (req.ProjectMilestoneId.HasValue && !await _db.ProjectMilestones.AnyAsync(
                m => m.Id == req.ProjectMilestoneId.Value && m.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Milestone {req.ProjectMilestoneId} is not in this project.");

        var code = req.Code.Trim();
        if (await _db.ProjectTasks.AnyAsync(t => t.CustomerProjectId == req.CustomerProjectId && t.Code == code, ct))
            return Result.Failure<int>($"Task Code '{code}' already exists in this project.");

        var task = new ProjectTask
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectPhaseId = req.ProjectPhaseId,
            ParentTaskId = req.ParentTaskId,
            ProjectMilestoneId = req.ProjectMilestoneId,
            Code = code,
            Name = req.Name.Trim(),
            Description = req.Description,
            TaskType = req.TaskType,
            IsMilestoneBlocking = req.IsMilestoneBlocking,
            ResponsibleOwner = string.IsNullOrWhiteSpace(req.ResponsibleOwner) ? null : req.ResponsibleOwner.Trim(),
            Priority = req.Priority,
            PlannedStart = req.PlannedStart,
            PlannedFinish = req.PlannedFinish,
            ForecastStart = req.PlannedStart,
            ForecastFinish = req.PlannedFinish,
            WorkHoursPlanned = req.WorkHoursPlanned,
            WeightPercent = req.WeightPercent,
            SortOrder = req.SortOrder,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return Result.Success(task.Id);
    }

    private Task<bool> PhaseInProjectAsync(int phaseId, int projectId, CancellationToken ct)
        => _db.ProjectPhases.AnyAsync(p => p.Id == phaseId && p.CustomerProjectId == projectId, ct);

    // ------------------------------------------------------------------
    // Add dependency — same project, no self, no duplicate, NO CYCLE.
    // ------------------------------------------------------------------
    public async Task<Result<int>> AddDependencyAsync(AddDependencyRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        if (req.PredecessorTaskId == req.SuccessorTaskId)
            return Result.Failure<int>("A task cannot depend on itself.");

        var pred = await _db.ProjectTasks
            .Where(t => t.Id == req.PredecessorTaskId)
            .Select(t => new { t.Id, t.CustomerProjectId })
            .FirstOrDefaultAsync(ct);
        var succ = await _db.ProjectTasks
            .Where(t => t.Id == req.SuccessorTaskId)
            .Select(t => new { t.Id, t.CustomerProjectId })
            .FirstOrDefaultAsync(ct);
        if (pred is null) return Result.Failure<int>($"Predecessor task {req.PredecessorTaskId} not found.");
        if (succ is null) return Result.Failure<int>($"Successor task {req.SuccessorTaskId} not found.");

        // Tenant gate via the (shared) project + same-project requirement.
        var (ok, err) = await ProjectVisibleAsync(pred.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (pred.CustomerProjectId != succ.CustomerProjectId)
            return Result.Failure<int>("Both tasks must belong to the same project.");

        if (await _db.ProjectTaskDependencies.AnyAsync(
                d => d.PredecessorTaskId == req.PredecessorTaskId && d.SuccessorTaskId == req.SuccessorTaskId, ct))
            return Result.Failure<int>("That dependency already exists.");

        // Cycle check: would pred→succ create a cycle? It does iff succ can
        // already reach pred over the existing edge set within this project.
        var edges = await _db.ProjectTaskDependencies
            .Where(d => d.CustomerProjectId == pred.CustomerProjectId)
            .Select(d => new { d.PredecessorTaskId, d.SuccessorTaskId })
            .ToListAsync(ct);
        var adjacency = edges
            .GroupBy(e => e.PredecessorTaskId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SuccessorTaskId).ToList());
        if (CanReach(adjacency, req.SuccessorTaskId, req.PredecessorTaskId))
            return Result.Failure<int>("That dependency would create a cycle.");

        var dep = new ProjectTaskDependency
        {
            CustomerProjectId = pred.CustomerProjectId,
            PredecessorTaskId = req.PredecessorTaskId,
            SuccessorTaskId = req.SuccessorTaskId,
            DependencyType = req.DependencyType,
            LagDays = req.LagDays,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectTaskDependencies.Add(dep);
        await _db.SaveChangesAsync(ct);
        return Result.Success(dep.Id);
    }

    // DFS reachability over the dependency DAG.
    private static bool CanReach(Dictionary<int, List<int>> adjacency, int from, int target)
    {
        var stack = new Stack<int>();
        var seen = new HashSet<int>();
        stack.Push(from);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == target) return true;
            if (!seen.Add(node)) continue;
            if (adjacency.TryGetValue(node, out var nexts))
                foreach (var n in nexts) if (!seen.Contains(n)) stack.Push(n);
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Start task
    // ------------------------------------------------------------------
    public async Task<Result<ProjectTask>> StartTaskAsync(int taskId, string? modifiedBy = null, CancellationToken ct = default)
    {
        var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
        if (task is null) return Result.Failure<ProjectTask>($"Task {taskId} not found.");
        var (ok, err) = await ProjectVisibleAsync(task.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectTask>(err!);
        if (task.Status == ProjectTaskStatus.Complete || task.Status == ProjectTaskStatus.Cancelled)
            return Result.Failure<ProjectTask>($"Task '{task.Code}' is {task.Status} and cannot be started.");

        task.Status = ProjectTaskStatus.InProgress;
        task.ActualStart ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(task);
    }

    // ------------------------------------------------------------------
    // Complete task — blocked while a finish-to-start predecessor is open.
    // Set-once.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectTask>> CompleteTaskAsync(CompleteTaskRequest req, CancellationToken ct = default)
    {
        if (req is null || req.TaskId <= 0) return Result.Failure<ProjectTask>("A valid TaskId is required.");
        var task = await _db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == req.TaskId, ct);
        if (task is null) return Result.Failure<ProjectTask>($"Task {req.TaskId} not found.");
        var (ok, err) = await ProjectVisibleAsync(task.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectTask>(err!);

        if (task.Status == ProjectTaskStatus.Complete)
            return Result.Failure<ProjectTask>($"Task '{task.Code}' is already complete.");
        if (task.Status == ProjectTaskStatus.Cancelled)
            return Result.Failure<ProjectTask>($"Task '{task.Code}' is cancelled and cannot be completed.");

        // Gate: every finish-to-start predecessor must be Complete or Cancelled.
        var openPreds = await _db.ProjectTaskDependencies
            .Where(d => d.SuccessorTaskId == task.Id && d.DependencyType == DependencyType.FinishToStart)
            .Join(_db.ProjectTasks, d => d.PredecessorTaskId, t => t.Id, (d, t) => t)
            .Where(t => t.Status != ProjectTaskStatus.Complete && t.Status != ProjectTaskStatus.Cancelled)
            .Select(t => t.Code)
            .ToListAsync(ct);
        if (openPreds.Count > 0)
            return Result.Failure<ProjectTask>(
                $"Cannot complete '{task.Code}' — finish-to-start predecessor(s) still open: {string.Join(", ", openPreds)}.");

        task.Status = ProjectTaskStatus.Complete;
        task.PercentComplete = 100m;
        task.ActualFinish ??= DateTime.UtcNow;
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedBy = req.CompletedBy;
        task.BlockReason = null;
        await _db.SaveChangesAsync(ct);
        return Result.Success(task);
    }

    // ------------------------------------------------------------------
    // Achieve milestone — the §20 gate. Blocked while a blocking task is open.
    // Set-once.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectMilestone>> AchieveMilestoneAsync(AchieveMilestoneRequest req, CancellationToken ct = default)
    {
        if (req is null || req.MilestoneId <= 0) return Result.Failure<ProjectMilestone>("A valid MilestoneId is required.");
        var ms = await _db.ProjectMilestones.FirstOrDefaultAsync(m => m.Id == req.MilestoneId, ct);
        if (ms is null) return Result.Failure<ProjectMilestone>($"Milestone {req.MilestoneId} not found.");
        var (ok, err) = await ProjectVisibleAsync(ms.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectMilestone>(err!);

        if (ms.Status == ProjectMilestoneStatus.Achieved)
            return Result.Failure<ProjectMilestone>($"Milestone '{ms.Code}' is already achieved.");
        if (ms.Status == ProjectMilestoneStatus.Cancelled)
            return Result.Failure<ProjectMilestone>($"Milestone '{ms.Code}' is cancelled and cannot be achieved.");

        // Gate: no blocking task may still be open.
        var openBlockers = await _db.ProjectTasks
            .Where(t => t.ProjectMilestoneId == ms.Id && t.IsMilestoneBlocking
                     && t.Status != ProjectTaskStatus.Complete && t.Status != ProjectTaskStatus.Cancelled)
            .Select(t => t.Code)
            .ToListAsync(ct);
        if (openBlockers.Count > 0)
            return Result.Failure<ProjectMilestone>(
                $"Cannot achieve '{ms.Code}' — blocking task(s) still open: {string.Join(", ", openBlockers)}.");

        ms.Status = ProjectMilestoneStatus.Achieved;
        ms.ActualDate ??= DateTime.UtcNow;
        ms.AchievedAt = DateTime.UtcNow;
        ms.AchievedBy = req.AchievedBy;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Milestone {Code} achieved on project {ProjectId}.", ms.Code, ms.CustomerProjectId);
        return Result.Success(ms);
    }

    // ==================================================================
    // PR-9 — Gantt + critical path (CPM).
    // ==================================================================

    public async Task<Result<ProjectGanttView>> GetGanttAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err) = await ProjectVisibleAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectGanttView>(err!);

        var proj = await _db.CustomerProjects
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Code, p.Name })
            .FirstAsync(ct);

        var tasks = await _db.ProjectTasks
            .Where(t => t.CustomerProjectId == projectId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
            .ToListAsync(ct);
        var milestones = await _db.ProjectMilestones
            .Where(m => m.CustomerProjectId == projectId)
            .OrderBy(m => m.SortOrder).ThenBy(m => m.Id)
            .ToListAsync(ct);
        var deps = await _db.ProjectTaskDependencies
            .Where(d => d.CustomerProjectId == projectId)
            .ToListAsync(ct);
        var phaseCodes = await _db.ProjectPhases
            .Where(p => p.CustomerProjectId == projectId)
            .Select(p => new { p.Id, p.Code })
            .ToDictionaryAsync(p => p.Id, p => p.Code, ct);

        // CPM — schedule each task; flag the critical ones.
        var cpm = ComputeSchedule(tasks, deps);

        // Anchor date for tasks that have no explicit dates (chain them by CPM).
        var anchor = tasks
            .Select(t => t.PlannedStart ?? t.ForecastStart)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .DefaultIfEmpty(DateTime.UtcNow.Date)
            .Min();

        var bars = new List<GanttBar>();
        foreach (var t in tasks)
        {
            var c = cpm[t.Id];
            DateTime start, end;
            if (t.PlannedStart.HasValue && t.PlannedFinish.HasValue && t.PlannedFinish > t.PlannedStart)
            { start = t.PlannedStart.Value; end = t.PlannedFinish.Value; }
            else if (t.ForecastStart.HasValue && t.ForecastFinish.HasValue && t.ForecastFinish > t.ForecastStart)
            { start = t.ForecastStart.Value; end = t.ForecastFinish.Value; }
            else
            { start = anchor.AddDays(c.Es); end = anchor.AddDays(c.Ef); }

            bars.Add(new GanttBar(
                t.Id, t.Code, t.Name,
                t.ProjectPhaseId.HasValue && phaseCodes.TryGetValue(t.ProjectPhaseId.Value, out var pc) ? pc : null,
                start, end, Math.Round(c.Duration, 2), t.PercentComplete ?? 0m, t.Status,
                c.IsCritical, Math.Round(c.TotalFloat, 2)));
        }

        // Milestone markers (a milestone is "critical" if it sits on/after the
        // critical project finish, or any of its blocking tasks is critical).
        var criticalTaskIds = cpm.Where(kv => kv.Value.IsCritical).Select(kv => kv.Key).ToHashSet();
        var blockingByMs = tasks
            .Where(t => t.ProjectMilestoneId.HasValue)
            .GroupBy(t => t.ProjectMilestoneId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        var msMarkers = new List<GanttMilestoneMarker>();
        foreach (var m in milestones)
        {
            var date = m.TargetDate ?? m.ForecastDate ?? m.ActualDate;
            if (date is null) continue; // undated milestones don't plot
            bool crit = blockingByMs.TryGetValue(m.Id, out var ids) && ids.Any(criticalTaskIds.Contains);
            msMarkers.Add(new GanttMilestoneMarker(
                m.Id, m.Code, m.Name, date.Value, m.MilestoneType, m.Status, crit));
        }

        // Timeline bounds.
        var allStarts = bars.Select(b => b.Start).Concat(msMarkers.Select(m => m.Date)).ToList();
        var allEnds = bars.Select(b => b.End).Concat(msMarkers.Select(m => m.Date)).ToList();
        var tlStart = allStarts.DefaultIfEmpty(anchor).Min();
        var tlEnd = allEnds.DefaultIfEmpty(anchor.AddDays(1)).Max();
        if (tlEnd <= tlStart) tlEnd = tlStart.AddDays(1);

        // Order the critical chain by CPM earliest-start (then code) so the
        // voice handler narrates it in dependency/path order — NOT display order
        // (Codex P2: SortOrder can differ from the dependency sequence).
        var critCodes = bars.Where(b => b.IsCritical)
            .OrderBy(b => cpm[b.TaskId].Es).ThenBy(b => b.Code)
            .Select(b => b.Code).ToList();
        var projDuration = cpm.Count > 0 ? cpm.Values.Max(c => c.Ef) : 0d;

        var headline = bars.Count == 0
            ? "No scheduled tasks yet — add tasks and dependencies to see the critical path."
            : $"{bars.Count} task(s), {critCodes.Count} on the critical path · project span {Math.Round(projDuration, 1)} working-day(s) of dependency-driven work.";

        return Result.Success(new ProjectGanttView(
            projectId, proj.Code, proj.Name, tlStart, tlEnd, Math.Round(projDuration, 2),
            critCodes.Count, headline, critCodes, bars, msMarkers,
            deps.Select(d => new GanttDependencyEdge(d.PredecessorTaskId, d.SuccessorTaskId, d.DependencyType)).ToList()));
    }

    public async Task<Result<ProjectGanttView>> GetGanttByRefAsync(string projectRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectRef))
            return Result.Failure<ProjectGanttView>("Which project? Say a project code or id.");

        var raw = projectRef.Trim();
        int? pid = null;
        if (int.TryParse(raw, out var asId) && asId > 0)
            pid = await _db.CustomerProjects
                .Where(x => x.Id == asId && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);

        if (pid is null)
        {
            // Case-insensitive code resolution (ToUpper both sides — InMemory-testable,
            // not EF.Functions.ILike). Exact → prefix.
            var upper = raw.ToUpperInvariant();
            pid = await _db.CustomerProjects
                .Where(x => x.Code != null && x.Code.ToUpper() == upper && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            pid ??= await _db.CustomerProjects
                .Where(x => x.Code != null && x.Code.ToUpper().StartsWith(upper) && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        }

        if (pid is null or 0)
            return Result.Failure<ProjectGanttView>($"I couldn't find a project matching '{raw}' in your scope.");

        return await GetGanttAsync(pid.Value, ct);
    }

    public async Task<Result<int>> RecalculateCriticalPathAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err) = await ProjectVisibleAsync(projectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        var tasks = await _db.ProjectTasks.Where(t => t.CustomerProjectId == projectId).ToListAsync(ct);
        var deps = await _db.ProjectTaskDependencies.Where(d => d.CustomerProjectId == projectId).ToListAsync(ct);
        var cpm = ComputeSchedule(tasks, deps);

        int critical = 0;
        foreach (var t in tasks)
        {
            bool isCrit = cpm[t.Id].IsCritical;
            if (t.IsCriticalPath != isCrit) t.IsCriticalPath = isCrit;
            if (isCrit) critical++;
        }
        await _db.SaveChangesAsync(ct);
        return Result.Success(critical);
    }

    // ------------------------------------------------------------------
    // CPM — forward/backward pass over the dependency network.
    // Durations come from planned (else forecast) dates; default 1 day.
    // Cycles are already prevented at AddDependency, so a topological order
    // exists; if a stray cycle is hit we fall back to input order.
    // ------------------------------------------------------------------
    private sealed class CpmNode
    {
        public double Duration;
        public double Es, Ef, Ls, Lf, TotalFloat;
        public bool IsCritical;
    }

    private static Dictionary<int, CpmNode> ComputeSchedule(
        List<ProjectTask> tasks, List<ProjectTaskDependency> deps)
    {
        const double Eps = 0.0001;
        var nodes = new Dictionary<int, CpmNode>();
        foreach (var t in tasks)
            nodes[t.Id] = new CpmNode { Duration = DurationDays(t) };
        if (nodes.Count == 0) return nodes;

        // Keep only edges whose endpoints are both real tasks.
        var edges = deps.Where(d => nodes.ContainsKey(d.PredecessorTaskId) && nodes.ContainsKey(d.SuccessorTaskId)).ToList();

        // Topological order via Kahn's algorithm.
        var outAdj = edges.GroupBy(e => e.PredecessorTaskId).ToDictionary(g => g.Key, g => g.ToList());
        var indeg = nodes.Keys.ToDictionary(id => id, _ => 0);
        foreach (var e in edges) indeg[e.SuccessorTaskId]++;
        var queue = new Queue<int>(nodes.Keys.Where(id => indeg[id] == 0));
        var topo = new List<int>();
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            topo.Add(n);
            if (outAdj.TryGetValue(n, out var outs))
                foreach (var e in outs)
                    if (--indeg[e.SuccessorTaskId] == 0) queue.Enqueue(e.SuccessorTaskId);
        }
        if (topo.Count < nodes.Count) // cycle fallback — process remaining in input order
            topo.AddRange(nodes.Keys.Where(id => !topo.Contains(id)));

        // Forward pass — earliest start/finish.
        var inAdj = edges.GroupBy(e => e.SuccessorTaskId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var id in topo)
        {
            var node = nodes[id];
            double es = 0;
            if (inAdj.TryGetValue(id, out var ins))
            {
                foreach (var e in ins)
                {
                    var p = nodes[e.PredecessorTaskId];
                    double cand = e.DependencyType switch
                    {
                        DependencyType.FinishToStart => p.Ef + e.LagDays,
                        DependencyType.StartToStart => p.Es + e.LagDays,
                        DependencyType.FinishToFinish => p.Ef + e.LagDays - node.Duration,
                        DependencyType.StartToFinish => p.Es + e.LagDays - node.Duration,
                        _ => p.Ef + e.LagDays,
                    };
                    if (cand > es) es = cand;
                }
            }
            node.Es = Math.Max(0, es);
            node.Ef = node.Es + node.Duration;
        }

        double projectEnd = nodes.Values.Max(n => n.Ef);

        // Backward pass — latest start/finish.
        foreach (var id in Enumerable.Reverse(topo))
        {
            var node = nodes[id];
            double lf = projectEnd;
            if (outAdj.TryGetValue(id, out var outs))
            {
                foreach (var e in outs)
                {
                    var s = nodes[e.SuccessorTaskId];
                    double cand = e.DependencyType switch
                    {
                        DependencyType.FinishToStart => s.Ls - e.LagDays,
                        DependencyType.StartToStart => s.Ls - e.LagDays + node.Duration,
                        DependencyType.FinishToFinish => s.Lf - e.LagDays,
                        DependencyType.StartToFinish => s.Lf - e.LagDays + node.Duration,
                        _ => s.Ls - e.LagDays,
                    };
                    if (cand < lf) lf = cand;
                }
            }
            node.Lf = lf;
            node.Ls = node.Lf - node.Duration;
            node.TotalFloat = node.Ls - node.Es;
            node.IsCritical = Math.Abs(node.TotalFloat) <= Eps;
        }

        return nodes;
    }

    private static double DurationDays(ProjectTask t)
    {
        if (t.PlannedStart.HasValue && t.PlannedFinish.HasValue && t.PlannedFinish.Value > t.PlannedStart.Value)
            return (t.PlannedFinish.Value - t.PlannedStart.Value).TotalDays;
        if (t.ForecastStart.HasValue && t.ForecastFinish.HasValue && t.ForecastFinish.Value > t.ForecastStart.Value)
            return (t.ForecastFinish.Value - t.ForecastStart.Value).TotalDays;
        return 1d;
    }
}
