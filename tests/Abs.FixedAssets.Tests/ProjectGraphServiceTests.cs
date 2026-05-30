using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

// Theme B9 Wave 1 PR-3 (CLOSES B9 Wave 1) — ProjectGraphService lifecycle-graph assembly.
public sealed class ProjectGraphServiceTests
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
            .UseInMemoryDatabase($"projgraph-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectGraphService NewService(AppDbContext db) =>
        new(db, new StubTenantContext(), NullLogger<ProjectGraphService>.Instance);

    private static async Task<int> SeedProjectAsync(
        AppDbContext db, int companyId = 1, string code = "PRJ-GRAPH-1",
        CustomerProjectStatus status = CustomerProjectStatus.Active,
        decimal? estTotalCost = null, decimal? contractValue = null, decimal? pctComplete = null)
    {
        var proj = new CustomerProject
        {
            CompanyId = companyId, Code = code, Name = "Graph test", Status = status,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
            EstimatedTotalCost = estTotalCost, ContractValue = contractValue, PercentComplete = pctComplete,
        };
        db.CustomerProjects.Add(proj);
        await db.SaveChangesAsync();
        return proj.Id;
    }

    [Fact]
    public async Task Graph_has_project_root_and_all_future_wave_stages()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);

        var r = await NewService(db).GetGraphAsync(pid, CancellationToken.None);
        Assert.True(r.IsSuccess);
        var g = r.Value!;

        // Project root is Present and points at the Command Center.
        var project = Assert.Single(g.Nodes, n => n.Stage == ProjectGraphStage.Project);
        Assert.Equal(ProjectGraphNodeState.Present, project.State);
        Assert.Equal($"/CustomerProjects/CommandCenter/{pid}", project.DeepLinkHref);

        // Every future-wave stage is mapped and dimmed with a wave label.
        foreach (var stage in new[] { ProjectGraphStage.Quote, ProjectGraphStage.Purchasing,
            ProjectGraphStage.Receipt, ProjectGraphStage.Billing, ProjectGraphStage.Acceptance })
        {
            var node = Assert.Single(g.Nodes, n => n.Stage == stage);
            Assert.Equal(ProjectGraphNodeState.Future, node.State);
            Assert.False(string.IsNullOrWhiteSpace(node.WaveLabel));
        }
    }

    [Fact]
    public async Task Phases_and_jobs_render_with_job_nested_under_its_phase()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var phase = new ProjectPhase { CustomerProjectId = pid, Code = "P10", Name = "Fabrication", SortOrder = 1 };
        db.ProjectPhases.Add(phase);
        await db.SaveChangesAsync();
        db.ProductionOrders.Add(new ProductionOrder
        {
            CompanyId = 1, OrderNumber = "PRO-FAB-1", Status = ProductionOrderStatus.Released,
            CustomerProjectId = pid, ProjectPhaseId = phase.Id, QuantityOrdered = 5,
        });
        await db.SaveChangesAsync();

        var g = (await NewService(db).GetGraphAsync(pid, CancellationToken.None)).Value!;

        var phaseNode = Assert.Single(g.Nodes, n => n.Stage == ProjectGraphStage.Wbs);
        Assert.Equal("project", phaseNode.ParentId);
        Assert.Equal(1, g.PhaseCount);

        var jobNode = Assert.Single(g.Nodes, n => n.Stage == ProjectGraphStage.Job);
        Assert.Equal($"phase-{phase.Id}", jobNode.ParentId);          // nested under its phase
        Assert.Equal(ProjectGraphTone.Good, jobNode.Tone);            // Released → on-track tone
        Assert.StartsWith("/Production/Orders/", jobNode.DeepLinkHref);
        Assert.EndsWith("/Cockpit", jobNode.DeepLinkHref);
        Assert.Equal(1, g.JobCount);
    }

    [Fact]
    public async Task Job_without_phase_attaches_to_project_root()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        db.ProductionOrders.Add(new ProductionOrder
        {
            CompanyId = 1, OrderNumber = "PRO-LOOSE", Status = ProductionOrderStatus.OnHold,
            CustomerProjectId = pid, QuantityOrdered = 2,
        });
        await db.SaveChangesAsync();

        var g = (await NewService(db).GetGraphAsync(pid, CancellationToken.None)).Value!;
        var jobNode = Assert.Single(g.Nodes, n => n.Stage == ProjectGraphStage.Job);
        Assert.Equal("project", jobNode.ParentId);
        Assert.Equal(ProjectGraphTone.Bad, jobNode.Tone); // OnHold → blocked tone
    }

    [Fact]
    public async Task Cost_node_is_present_when_evm_rollup_exists()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, estTotalCost: 50000m, contractValue: 80000m, pctComplete: 40m);

        var g = (await NewService(db).GetGraphAsync(pid, CancellationToken.None)).Value!;
        var cost = Assert.Single(g.Nodes, n => n.Stage == ProjectGraphStage.Cost);
        Assert.Equal(ProjectGraphNodeState.Present, cost.State);
        Assert.Equal(ProjectGraphTone.Good, cost.Tone); // est cost under contract → healthy
    }

    [Fact]
    public async Task Cost_node_is_future_when_no_evm_data()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);

        var g = (await NewService(db).GetGraphAsync(pid, CancellationToken.None)).Value!;
        var cost = Assert.Single(g.Nodes, n => n.Stage == ProjectGraphStage.Cost);
        Assert.Equal(ProjectGraphNodeState.Future, cost.State);
    }

    [Fact]
    public async Task GetGraphByRef_resolves_by_code()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, code: "DEMO-COO-PROJ-001");

        var r = await NewService(db).GetGraphByRefAsync("DEMO-COO-PROJ-001", CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(pid, r.Value!.ProjectId);
    }

    [Fact]
    public async Task GetGraphByRef_resolves_lowercase_code_case_insensitively()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, code: "DEMO-COO-PROJ-001");

        // Voice transcripts arrive lower-case — must still resolve the upper-case code.
        var r = await NewService(db).GetGraphByRefAsync("demo-coo-proj-001", CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(pid, r.Value!.ProjectId);
    }

    [Fact]
    public async Task Project_outside_tenant_scope_is_not_found()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99); // not in VisibleCompanyIds {1}

        var r = await NewService(db).GetGraphAsync(pid, CancellationToken.None);
        Assert.True(r.IsFailure);
    }
}
