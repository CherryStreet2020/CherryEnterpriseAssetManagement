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

// Theme B9 Wave 4 PR-11 — ProjectResourceService: plan/assignment/time/expense
// CRUD + tenant-scoping, planned-vs-actual rollups, cost computation, the
// closed-project capture guard, and set-once approvals.
public sealed class ProjectResourceServiceTests
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
            .UseInMemoryDatabase($"projres-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectResourceService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectResourceService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1,
        CustomerProjectStatus status = CustomerProjectStatus.Active)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-R-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Resource test", Status = status,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // -- create + rollups --------------------------------------------------

    [Fact]
    public async Task Plan_assignment_time_expense_succeed_and_roll_up()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);

        var plan = await svc.CreatePlanAsync(new CreateResourcePlanRequest(pid, "RP1", "CNC labor", PlannedHours: 120m, PlannedCost: 11_400m));
        var asg = await svc.CreateAssignmentAsync(new CreateAssignmentRequest(pid, "RA1", ProjectResourcePlanId: plan.Value, PlannedHours: 120m, CostRate: 95m));
        var t1 = await svc.RecordTimeEntryAsync(new RecordTimeEntryRequest(pid, new DateTime(2026, 6, 12), 8m, ProjectResourceAssignmentId: asg.Value));
        await svc.RecordExpenseAsync(new RecordExpenseRequest(pid, "EXP1", 1_240m, new DateTime(2026, 6, 22)));

        Assert.True(plan.IsSuccess && asg.IsSuccess && t1.IsSuccess);

        var view = (await svc.GetResourcingAsync(pid)).Value!;
        Assert.Equal(120m, view.PlannedHours);
        Assert.Equal(11_400m, view.PlannedLaborCost);
        Assert.Equal(8m, view.ActualHours);
        Assert.Equal(760m, view.ActualLaborCost);   // 8 × 95 (rate inherited from assignment)
        Assert.Equal(1_240m, view.ExpenseTotal);
        var asgRow = view.Assignments.Single();
        Assert.Equal(8m, asgRow.ActualHours);
        Assert.Equal(760m, asgRow.ActualCost);
    }

    [Fact]
    public async Task Time_entry_inherits_cost_rate_from_assignment()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var asg = (await svc.CreateAssignmentAsync(new CreateAssignmentRequest(pid, "RA1", CostRate: 110m))).Value;

        var t = await svc.RecordTimeEntryAsync(new RecordTimeEntryRequest(pid, new DateTime(2026, 6, 12), 4m, ProjectResourceAssignmentId: asg));

        Assert.True(t.IsSuccess);
        var view = (await svc.GetResourcingAsync(pid)).Value!;
        Assert.Equal(440m, view.ActualLaborCost);   // 4 × 110 inherited
    }

    // -- tenant + scoping --------------------------------------------------

    [Fact]
    public async Task Assignment_rejects_plan_from_another_project()
    {
        using var db = NewDb();
        var p1 = await SeedProjectAsync(db);
        var p2 = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var planP2 = (await svc.CreatePlanAsync(new CreateResourcePlanRequest(p2, "RP", "Other"))).Value;

        var res = await svc.CreateAssignmentAsync(new CreateAssignmentRequest(p1, "RA1", ProjectResourcePlanId: planP2));

        Assert.True(res.IsFailure);
        Assert.Contains("not in this project", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePlan_rejects_project_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);
        var svc = NewSvc(db, 1);

        var res = await svc.CreatePlanAsync(new CreateResourcePlanRequest(pid, "RP1", "X"));

        Assert.True(res.IsFailure);
        Assert.Contains("tenant scope", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_expense_code_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.RecordExpenseAsync(new RecordExpenseRequest(pid, "EXP1", 100m, new DateTime(2026, 6, 1)));

        var dup = await svc.RecordExpenseAsync(new RecordExpenseRequest(pid, "EXP1", 50m, new DateTime(2026, 6, 2)));

        Assert.True(dup.IsFailure);
        Assert.Contains("already exists", dup.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- closed-project capture guard --------------------------------------

    [Fact]
    public async Task Time_entry_blocked_on_closed_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, status: CustomerProjectStatus.Closed);
        var svc = NewSvc(db);

        var res = await svc.RecordTimeEntryAsync(new RecordTimeEntryRequest(pid, new DateTime(2026, 6, 12), 8m));

        Assert.True(res.IsFailure);
        Assert.Contains("Closed", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expense_blocked_on_cancelled_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, status: CustomerProjectStatus.Cancelled);
        var svc = NewSvc(db);

        var res = await svc.RecordExpenseAsync(new RecordExpenseRequest(pid, "EXP1", 100m, new DateTime(2026, 6, 1)));

        Assert.True(res.IsFailure);
        Assert.Contains("Cancelled", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- rejected rows don't count -----------------------------------------

    [Fact]
    public async Task Rejected_time_excluded_from_actuals()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var asg = (await svc.CreateAssignmentAsync(new CreateAssignmentRequest(pid, "RA1", CostRate: 100m))).Value;
        var tId = (await svc.RecordTimeEntryAsync(new RecordTimeEntryRequest(pid, new DateTime(2026, 6, 12), 5m, ProjectResourceAssignmentId: asg))).Value;

        // Manually reject the entry, then confirm it drops out of the rollup.
        var entry = await db.ProjectTimeEntries.FindAsync(tId);
        entry!.Status = TimeEntryStatus.Rejected;
        await db.SaveChangesAsync();

        var view = (await svc.GetResourcingAsync(pid)).Value!;
        Assert.Equal(0m, view.ActualHours);
        Assert.Equal(0m, view.ActualLaborCost);
    }

    // -- set-once approvals ------------------------------------------------

    [Fact]
    public async Task Approve_time_entry_is_set_once()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var tId = (await svc.RecordTimeEntryAsync(new RecordTimeEntryRequest(pid, new DateTime(2026, 6, 12), 8m))).Value;

        var first = await svc.ApproveTimeEntryAsync(tId, "lead");
        var second = await svc.ApproveTimeEntryAsync(tId, "lead");

        Assert.True(first.IsSuccess);
        Assert.Equal(TimeEntryStatus.Approved, first.Value!.Status);
        Assert.NotNull(first.Value!.ApprovedAt);
        Assert.True(second.IsFailure);
        Assert.Contains("already approved", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approve_expense_is_set_once()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var xId = (await svc.RecordExpenseAsync(new RecordExpenseRequest(pid, "EXP1", 500m, new DateTime(2026, 6, 1)))).Value;

        var first = await svc.ApproveExpenseAsync(xId, "mgr");
        var second = await svc.ApproveExpenseAsync(xId, "mgr");

        Assert.True(first.IsSuccess);
        Assert.Equal(ProjectExpenseStatus.Approved, first.Value!.Status);
        Assert.True(second.IsFailure);
    }

    [Fact]
    public async Task Assignment_rejects_allocation_over_100()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);

        var res = await svc.CreateAssignmentAsync(new CreateAssignmentRequest(pid, "RA1", AllocationPercent: 150m));

        Assert.True(res.IsFailure);
        Assert.Contains("0..100", res.Error);
    }
}
