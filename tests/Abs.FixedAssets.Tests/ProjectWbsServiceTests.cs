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

// Theme B9 Wave 3 PR-7 (OPENS Wave 3) — ProjectWbsService: weighted roll-up,
// the 100%-rule validator, and the set-once baseline gate (owner + cost
// bucket on every leaf + 100%-rule).
public sealed class ProjectWbsServiceTests
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
            .UseInMemoryDatabase($"projwbs-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectWbsService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectWbsService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-W-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "WBS test", Status = CustomerProjectStatus.Active,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static ProjectPhase Phase(int projectId, string code, int? parentId = null,
        string? owner = null, decimal? planned = null, decimal? weight = null,
        decimal? pct = null, int level = 1, DateTime? fcStart = null, DateTime? fcFinish = null)
        => new()
        {
            CustomerProjectId = projectId, Code = code, Name = code, ParentPhaseId = parentId,
            ResponsibleOwner = owner, PlannedCost = planned, WeightPercent = weight,
            PercentComplete = pct, WbsLevel = level,
            ForecastStart = fcStart, ForecastFinish = fcFinish,
        };

    // -- 100%-rule -----------------------------------------------------

    [Fact]
    public async Task HundredPercentRule_satisfied_when_roots_sum_100()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        db.ProjectPhases.AddRange(
            Phase(pid, "A", owner: "Jane", planned: 1000m, weight: 60m),
            Phase(pid, "B", owner: "Bob", planned: 2000m, weight: 40m));
        await db.SaveChangesAsync();

        var res = await NewSvc(db).ValidateHundredPercentRuleAsync(pid);

        Assert.True(res.IsSuccess);
        Assert.True(res.Value!.Satisfied);
        Assert.Empty(res.Value.Violations);
    }

    [Fact]
    public async Task HundredPercentRule_flags_group_not_summing_100()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        db.ProjectPhases.AddRange(
            Phase(pid, "A", owner: "Jane", planned: 1000m, weight: 60m),
            Phase(pid, "B", owner: "Bob", planned: 2000m, weight: 30m)); // sums to 90
        await db.SaveChangesAsync();

        var res = await NewSvc(db).ValidateHundredPercentRuleAsync(pid);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value!.Satisfied);
        var v = Assert.Single(res.Value.Violations);
        Assert.Null(v.ParentPhaseId);
        Assert.Equal(90m, v.WeightSum);
    }

    // -- roll-up math --------------------------------------------------

    [Fact]
    public async Task GetWbsRollup_sums_cost_and_weights_percent_onto_parent()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var parent = Phase(pid, "ROOT", weight: 100m, level: 1);
        db.ProjectPhases.Add(parent);
        await db.SaveChangesAsync();
        db.ProjectPhases.AddRange(
            Phase(pid, "C1", parentId: parent.Id, owner: "Jane", planned: 1000m, weight: 25m, pct: 100m, level: 2),
            Phase(pid, "C2", parentId: parent.Id, owner: "Bob", planned: 3000m, weight: 75m, pct: 0m, level: 2));
        await db.SaveChangesAsync();

        var res = await NewSvc(db).GetWbsRollupAsync(pid);

        Assert.True(res.IsSuccess);
        var rollup = res.Value!;
        var root = Assert.Single(rollup.Roots);
        Assert.Equal(4000m, root.PlannedCost);              // 1000 + 3000
        Assert.Equal(25m, root.PercentComplete);            // (100*25 + 0*75)/100
        Assert.Equal(4000m, rollup.TotalPlannedCost);
        Assert.Equal(25m, rollup.RolledPercentComplete);
        Assert.False(root.IsLeaf);
        Assert.Equal(2, root.Children.Count);
    }

    // -- baseline gate -------------------------------------------------

    [Fact]
    public async Task Baseline_fails_when_a_leaf_has_no_owner()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        db.ProjectPhases.Add(Phase(pid, "A", owner: null, planned: 500m, weight: 100m)); // no owner
        await db.SaveChangesAsync();

        var res = await NewSvc(db).BaselineProjectWbsAsync(new BaselineProjectWbsRequest(pid));

        Assert.True(res.IsFailure);
        Assert.Contains("owner", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Baseline_fails_when_a_leaf_has_no_cost_bucket()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        db.ProjectPhases.Add(Phase(pid, "A", owner: "Jane", planned: null, weight: 100m)); // no cost
        await db.SaveChangesAsync();

        var res = await NewSvc(db).BaselineProjectWbsAsync(new BaselineProjectWbsRequest(pid));

        Assert.True(res.IsFailure);
        Assert.Contains("cost", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Baseline_fails_when_weights_violate_the_100_rule()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        db.ProjectPhases.AddRange(
            Phase(pid, "A", owner: "Jane", planned: 1000m, weight: 60m),
            Phase(pid, "B", owner: "Bob", planned: 2000m, weight: 30m)); // sums to 90
        await db.SaveChangesAsync();

        var res = await NewSvc(db).BaselineProjectWbsAsync(new BaselineProjectWbsRequest(pid));

        Assert.True(res.IsFailure);
        Assert.Contains("100", res.Error);
    }

    [Fact]
    public async Task Baseline_fails_on_empty_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);

        var res = await NewSvc(db).BaselineProjectWbsAsync(new BaselineProjectWbsRequest(pid));

        Assert.True(res.IsFailure);
        Assert.Contains("no WBS", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Baseline_succeeds_and_freezes_baseline_from_forecast()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var fcStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var fcFinish = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        db.ProjectPhases.AddRange(
            Phase(pid, "A", owner: "Jane", planned: 1000m, weight: 60m, fcStart: fcStart, fcFinish: fcFinish),
            Phase(pid, "B", owner: "Bob", planned: 2000m, weight: 40m, fcStart: fcStart, fcFinish: fcFinish));
        await db.SaveChangesAsync();

        var res = await NewSvc(db).BaselineProjectWbsAsync(
            new BaselineProjectWbsRequest(pid, BaselinedBy: "dean"));

        Assert.True(res.IsSuccess);
        Assert.Equal(2, res.Value!.PhasesBaselined);
        Assert.Equal(2, res.Value.LeafCount);
        Assert.Equal(3000m, res.Value.TotalPlannedCost);
        Assert.False(res.Value.WasRebaseline);

        var phases = await db.ProjectPhases.Where(p => p.CustomerProjectId == pid).ToListAsync();
        Assert.All(phases, p =>
        {
            Assert.True(p.IsBaselined);
            Assert.NotNull(p.BaselinedAt);
            Assert.Equal("dean", p.BaselinedBy);
            Assert.Equal(fcStart, p.BaselineStart);
            Assert.Equal(fcFinish, p.BaselineFinish);
        });
    }

    [Fact]
    public async Task Baseline_is_set_once_unless_rebaseline_allowed()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        db.ProjectPhases.AddRange(
            Phase(pid, "A", owner: "Jane", planned: 1000m, weight: 60m),
            Phase(pid, "B", owner: "Bob", planned: 2000m, weight: 40m));
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var first = await svc.BaselineProjectWbsAsync(new BaselineProjectWbsRequest(pid));
        Assert.True(first.IsSuccess);

        var second = await svc.BaselineProjectWbsAsync(new BaselineProjectWbsRequest(pid));
        Assert.True(second.IsFailure);
        Assert.Contains("already baselined", second.Error, StringComparison.OrdinalIgnoreCase);

        var third = await svc.BaselineProjectWbsAsync(new BaselineProjectWbsRequest(pid, AllowRebaseline: true));
        Assert.True(third.IsSuccess);
        Assert.True(third.Value!.WasRebaseline);
    }

    [Fact]
    public async Task Baseline_ignores_non_leaf_parent_missing_owner_and_cost()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        // Parent root has NO owner/cost — but it is not a leaf, so it must not block.
        var parent = Phase(pid, "ROOT", weight: 100m, level: 1);
        db.ProjectPhases.Add(parent);
        await db.SaveChangesAsync();
        db.ProjectPhases.AddRange(
            Phase(pid, "C1", parentId: parent.Id, owner: "Jane", planned: 1000m, weight: 50m, level: 2),
            Phase(pid, "C2", parentId: parent.Id, owner: "Bob", planned: 3000m, weight: 50m, level: 2));
        await db.SaveChangesAsync();

        var res = await NewSvc(db).BaselineProjectWbsAsync(new BaselineProjectWbsRequest(pid));

        Assert.True(res.IsSuccess);
        Assert.Equal(3, res.Value!.PhasesBaselined);
        Assert.Equal(2, res.Value.LeafCount);       // only C1 + C2 are leaves
        Assert.Equal(4000m, res.Value.TotalPlannedCost);
    }

    // -- tenant isolation ----------------------------------------------

    [Fact]
    public async Task GetWbsRollup_hidden_for_other_tenant()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 2);
        db.ProjectPhases.Add(Phase(pid, "A", owner: "Jane", planned: 1000m, weight: 100m));
        await db.SaveChangesAsync();

        var res = await NewSvc(db, 1).GetWbsRollupAsync(pid); // visible = company 1 only

        Assert.True(res.IsFailure);
        Assert.Contains("not found", res.Error, StringComparison.OrdinalIgnoreCase);
    }
}
