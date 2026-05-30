using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

// Theme B9 Wave 3 PR-8 — ProjectScheduleService: milestone achieve gate,
// task complete (FS-predecessor) gate, and dependency self/dup/cross-project/
// cycle rejection.
public sealed class ProjectScheduleServiceTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"projsched-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public int? TenantId { get; init; } = 1;
        public int? CompanyId { get; init; }
        public int? SiteId { get; init; }
        public int? AssignedCompanyId { get; init; }
        public int? AssignedSiteId { get; init; }
        public List<int> VisibleCompanyIds { get; init; } = new() { 1 };
        public List<int> VisibleSiteIds { get; init; } = new();
        public bool IsResolved => true;
        public string? ResolutionError => null;
        public void SetContext(int? tenantId, int? companyId, int? siteId) { }
        public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds) { }
        public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds) { }
        public void SetError(string error) { }
    }

    private static ProjectScheduleService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectScheduleService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-S-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Schedule test", Status = CustomerProjectStatus.Active,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // -- create -------------------------------------------------------

    [Fact]
    public async Task Create_milestone_and_task_succeed()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);

        var ms = await svc.CreateMilestoneAsync(new CreateMilestoneRequest(pid, "M1", "Design complete"));
        var tk = await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T1", "CAD model"));

        Assert.True(ms.IsSuccess);
        Assert.True(tk.IsSuccess);
        Assert.True(ms.Value > 0 && tk.Value > 0);
    }

    [Fact]
    public async Task Create_task_rejects_milestone_from_another_project()
    {
        using var db = NewDb();
        var p1 = await SeedProjectAsync(db);
        var p2 = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var ms2 = (await svc.CreateMilestoneAsync(new CreateMilestoneRequest(p2, "M", "Other project"))).Value;

        var res = await svc.CreateTaskAsync(new CreateTaskRequest(p1, "T1", "X", ProjectMilestoneId: ms2));

        Assert.True(res.IsFailure);
        Assert.Contains("not in this project", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- milestone achieve gate --------------------------------------

    [Fact]
    public async Task Achieve_milestone_blocked_by_open_blocking_task()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var msId = (await svc.CreateMilestoneAsync(new CreateMilestoneRequest(pid, "M1", "Ship"))).Value;
        await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T1", "Build", ProjectMilestoneId: msId, IsMilestoneBlocking: true));

        var res = await svc.AchieveMilestoneAsync(new AchieveMilestoneRequest(msId));

        Assert.True(res.IsFailure);
        Assert.Contains("blocking task", res.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("T1", res.Error);
    }

    [Fact]
    public async Task Achieve_milestone_succeeds_once_blocking_tasks_done_and_ignores_nonblocking_or_cancelled()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var msId = (await svc.CreateMilestoneAsync(new CreateMilestoneRequest(pid, "M1", "Ship"))).Value;

        var blocking = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T1", "Build", ProjectMilestoneId: msId, IsMilestoneBlocking: true))).Value;
        // a non-blocking open task on the same milestone must NOT block
        await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T2", "Nice to have", ProjectMilestoneId: msId, IsMilestoneBlocking: false));
        // a cancelled blocking task must NOT block
        var cancelled = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T3", "Dropped", ProjectMilestoneId: msId, IsMilestoneBlocking: true))).Value;
        var ct3 = await db.ProjectTasks.FirstAsync(t => t.Id == cancelled);
        ct3.Status = ProjectTaskStatus.Cancelled;
        await db.SaveChangesAsync();

        // still blocked while T1 open
        Assert.True((await svc.AchieveMilestoneAsync(new AchieveMilestoneRequest(msId))).IsFailure);

        await svc.CompleteTaskAsync(new CompleteTaskRequest(blocking));
        var res = await svc.AchieveMilestoneAsync(new AchieveMilestoneRequest(msId, AchievedBy: "dean"));

        Assert.True(res.IsSuccess);
        Assert.Equal(ProjectMilestoneStatus.Achieved, res.Value!.Status);
        Assert.NotNull(res.Value.AchievedAt);
        Assert.NotNull(res.Value.ActualDate);
    }

    [Fact]
    public async Task Achieve_milestone_is_set_once()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var msId = (await svc.CreateMilestoneAsync(new CreateMilestoneRequest(pid, "M1", "Ship"))).Value;

        Assert.True((await svc.AchieveMilestoneAsync(new AchieveMilestoneRequest(msId))).IsSuccess);
        var second = await svc.AchieveMilestoneAsync(new AchieveMilestoneRequest(msId));
        Assert.True(second.IsFailure);
        Assert.Contains("already achieved", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- task complete (FS predecessor) gate -------------------------

    [Fact]
    public async Task Complete_task_blocked_by_open_finish_to_start_predecessor()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var pre = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T1", "Pour foundation"))).Value;
        var post = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T2", "Frame walls"))).Value;
        await svc.AddDependencyAsync(new AddDependencyRequest(pre, post)); // FS by default

        var res = await svc.CompleteTaskAsync(new CompleteTaskRequest(post));

        Assert.True(res.IsFailure);
        Assert.Contains("predecessor", res.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("T1", res.Error);
    }

    [Fact]
    public async Task Complete_task_succeeds_after_predecessor_complete()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var pre = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T1", "Pour"))).Value;
        var post = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T2", "Frame"))).Value;
        await svc.AddDependencyAsync(new AddDependencyRequest(pre, post));

        Assert.True((await svc.CompleteTaskAsync(new CompleteTaskRequest(pre))).IsSuccess);
        var res = await svc.CompleteTaskAsync(new CompleteTaskRequest(post, CompletedBy: "dean"));

        Assert.True(res.IsSuccess);
        Assert.Equal(ProjectTaskStatus.Complete, res.Value!.Status);
        Assert.Equal(100m, res.Value.PercentComplete);
        Assert.NotNull(res.Value.CompletedAt);
    }

    [Fact]
    public async Task Complete_task_not_blocked_by_start_to_start_predecessor()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var pre = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T1", "A"))).Value;
        var post = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T2", "B"))).Value;
        await svc.AddDependencyAsync(new AddDependencyRequest(pre, post, DependencyType.StartToStart));

        // pre is still NotStarted, but the gate only applies to finish-to-start
        var res = await svc.CompleteTaskAsync(new CompleteTaskRequest(post));

        Assert.True(res.IsSuccess);
    }

    [Fact]
    public async Task Complete_task_is_set_once()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var t = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "T1", "A"))).Value;

        Assert.True((await svc.CompleteTaskAsync(new CompleteTaskRequest(t))).IsSuccess);
        var second = await svc.CompleteTaskAsync(new CompleteTaskRequest(t));
        Assert.True(second.IsFailure);
        Assert.Contains("already complete", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- dependency validation ---------------------------------------

    [Fact]
    public async Task AddDependency_rejects_self_duplicate_crossproject_and_cycle()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var other = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var a = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "A", "A"))).Value;
        var b = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "B", "B"))).Value;
        var c = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "C", "C"))).Value;
        var x = (await svc.CreateTaskAsync(new CreateTaskRequest(other, "X", "X"))).Value;

        // self
        Assert.Contains("itself", (await svc.AddDependencyAsync(new AddDependencyRequest(a, a))).Error, StringComparison.OrdinalIgnoreCase);
        // valid chain A->B->C
        Assert.True((await svc.AddDependencyAsync(new AddDependencyRequest(a, b))).IsSuccess);
        Assert.True((await svc.AddDependencyAsync(new AddDependencyRequest(b, c))).IsSuccess);
        // duplicate
        Assert.Contains("already exists", (await svc.AddDependencyAsync(new AddDependencyRequest(a, b))).Error, StringComparison.OrdinalIgnoreCase);
        // cross-project
        Assert.Contains("same project", (await svc.AddDependencyAsync(new AddDependencyRequest(a, x))).Error, StringComparison.OrdinalIgnoreCase);
        // cycle: C already reaches A (A->B->C), so C->A would close a loop
        Assert.Contains("cycle", (await svc.AddDependencyAsync(new AddDependencyRequest(c, a))).Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- read + tenant isolation -------------------------------------

    [Fact]
    public async Task GetSchedule_returns_rows_with_predecessors_and_blocking_count()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var msId = (await svc.CreateMilestoneAsync(new CreateMilestoneRequest(pid, "M1", "Ship"))).Value;
        var a = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "A", "A", ProjectMilestoneId: msId, IsMilestoneBlocking: true))).Value;
        var b = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, "B", "B"))).Value;
        await svc.AddDependencyAsync(new AddDependencyRequest(a, b));

        var res = await svc.GetScheduleAsync(pid);

        Assert.True(res.IsSuccess);
        var view = res.Value!;
        Assert.Single(view.Milestones);
        Assert.Equal(1, view.Milestones[0].BlockingOpenTaskCount); // A open + blocking
        Assert.Equal(2, view.Tasks.Count);
        Assert.Single(view.Dependencies);
        var bRow = view.Tasks.First(t => t.Code == "B");
        Assert.Contains(a, bRow.PredecessorTaskIds);
    }

    [Fact]
    public async Task GetSchedule_hidden_for_other_tenant()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 2);

        var res = await NewSvc(db, 1).GetScheduleAsync(pid);

        Assert.True(res.IsFailure);
        Assert.Contains("not found", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- PR-9: Gantt + critical path (CPM) ---------------------------

    private static async Task<int> TaskWithDatesAsync(ProjectScheduleService svc, AppDbContext db,
        int pid, string code, DateTime start, DateTime finish)
    {
        var id = (await svc.CreateTaskAsync(new CreateTaskRequest(pid, code, code,
            PlannedStart: start, PlannedFinish: finish))).Value;
        return id;
    }

    [Fact]
    public async Task GetGantt_identifies_critical_path_and_float()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var d = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = await TaskWithDatesAsync(svc, db, pid, "A", d, d.AddDays(2));            // 2d
        var b = await TaskWithDatesAsync(svc, db, pid, "B", d.AddDays(2), d.AddDays(5)); // 3d
        var c = await TaskWithDatesAsync(svc, db, pid, "C", d.AddDays(5), d.AddDays(6)); // 1d
        var par = await TaskWithDatesAsync(svc, db, pid, "D", d.AddDays(2), d.AddDays(3)); // 1d parallel
        await svc.AddDependencyAsync(new AddDependencyRequest(a, b));
        await svc.AddDependencyAsync(new AddDependencyRequest(b, c));
        await svc.AddDependencyAsync(new AddDependencyRequest(a, par));

        var res = await svc.GetGanttAsync(pid);

        Assert.True(res.IsSuccess);
        var view = res.Value!;
        Assert.Equal(3, view.CriticalTaskCount);
        Assert.Equal(new[] { "A", "B", "C" }, view.CriticalPathCodes.OrderBy(x => x).ToArray());
        var dBar = view.Bars.First(x => x.Code == "D");
        Assert.False(dBar.IsCritical);
        Assert.True(dBar.TotalFloatDays > 0.5, $"expected D to carry float, got {dBar.TotalFloatDays}");
        Assert.Equal(6d, view.ProjectDurationDays, 1);
    }

    [Fact]
    public async Task RecalculateCriticalPath_stamps_the_flag()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var d = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var a = await TaskWithDatesAsync(svc, db, pid, "A", d, d.AddDays(2));
        var b = await TaskWithDatesAsync(svc, db, pid, "B", d.AddDays(2), d.AddDays(5));
        await svc.AddDependencyAsync(new AddDependencyRequest(a, b));

        var res = await svc.RecalculateCriticalPathAsync(pid);

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value);
        var tasks = await db.ProjectTasks.Where(t => t.CustomerProjectId == pid).ToListAsync();
        Assert.All(tasks, t => Assert.True(t.IsCriticalPath));
    }

    [Fact]
    public async Task GetGantt_plots_milestones_and_empty_state()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);

        var empty = await svc.GetGanttAsync(pid);
        Assert.True(empty.IsSuccess);
        Assert.Empty(empty.Value!.Bars);
        Assert.Contains("No scheduled tasks", empty.Value.Headline, StringComparison.OrdinalIgnoreCase);

        await svc.CreateMilestoneAsync(new CreateMilestoneRequest(pid, "M1", "Ship",
            TargetDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));
        var res = await svc.GetGanttAsync(pid);
        Assert.Single(res.Value!.Milestones);
    }

    [Fact]
    public async Task GetGanttByRef_resolves_by_code_case_insensitive()
    {
        using var db = NewDb();
        var p = new CustomerProject { CompanyId = 1, Code = "PRJ-GANTT-1", Name = "G", Status = CustomerProjectStatus.Active, Mode = CustomerProjectMode.Standard, Currency = "USD" };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var res = await svc.GetGanttByRefAsync("prj-gantt-1");

        Assert.True(res.IsSuccess);
        Assert.Equal("PRJ-GANTT-1", res.Value!.ProjectCode);
    }
}
