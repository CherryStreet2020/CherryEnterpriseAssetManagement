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
using Microsoft.EntityFrameworkCore.InMemory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

// Theme B9 Wave 1 PR-1 — ProjectCommandCenterService aggregation + tenant scope.
public sealed class ProjectCommandCenterServiceTests
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
            .UseInMemoryDatabase($"pcc-{dbName}-{Guid.NewGuid()}")
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
        public List<int> VisibleCompanyIds { get; init; } = new();
        public List<int> VisibleSiteIds { get; init; } = new();
        public bool IsResolved => true;
        public string? ResolutionError => null;
        public void SetContext(int? tenantId, int? companyId, int? siteId) { }
        public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds) { }
        public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds) { }
        public void SetError(string error) { }
    }

    private static ProjectCommandCenterService NewService(AppDbContext db, params int[] visible)
    {
        var tenant = new StubTenantContext { VisibleCompanyIds = visible.ToList() };
        var quotes = new ProjectQuoteService(db, tenant, NullLogger<ProjectQuoteService>.Instance);
        return new(db, tenant, quotes, NullLogger<ProjectCommandCenterService>.Instance);
    }

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1,
        decimal? contractValue = 100_000m, decimal? estCost = 70_000m,
        DateTime? targetEnd = null, DateTime? projectedEnd = null)
    {
        var proj = new CustomerProject
        {
            CompanyId = companyId, Code = "PRJ-TI64-001", Name = "Ti-6Al-4V engine bracket program",
            Status = CustomerProjectStatus.Active, Mode = CustomerProjectMode.Standard,
            ContractValue = contractValue, Currency = "USD",
            EstimatedTotalCost = estCost, PercentComplete = 40m,
            TargetEndDate = targetEnd, ProjectedEndDate = projectedEnd,
            ProjectManagerName = "A. Rivera",
        };
        db.CustomerProjects.Add(proj);
        await db.SaveChangesAsync();
        return proj.Id;
    }

    [Fact]
    public async Task Aggregates_jobs_amendments_and_effective_contract_and_margin()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db,
            targetEnd: new DateTime(2026, 8, 1), projectedEnd: new DateTime(2026, 8, 11)); // 10 days late

        // 3 linked jobs: 1 open (Released), 1 completed, 1 on hold
        db.ProductionOrders.AddRange(
            new ProductionOrder { CompanyId = 1, OrderNumber = "PRO-A", Status = ProductionOrderStatus.Released, CustomerProjectId = pid },
            new ProductionOrder { CompanyId = 1, OrderNumber = "PRO-B", Status = ProductionOrderStatus.Completed, CustomerProjectId = pid },
            new ProductionOrder { CompanyId = 1, OrderNumber = "PRO-C", Status = ProductionOrderStatus.OnHold, CustomerProjectId = pid });
        // 1 approved amendment (+15,000) + 1 open/submitted amendment (+5,000)
        db.ProjectAmendments.AddRange(
            new ProjectAmendment { CustomerProjectId = pid, AmendmentNumber = 1, EffectiveDate = DateTime.UtcNow, Status = ProjectAmendmentStatus.Approved, ValueDelta = 15_000m },
            new ProjectAmendment { CustomerProjectId = pid, AmendmentNumber = 2, EffectiveDate = DateTime.UtcNow, Status = ProjectAmendmentStatus.Submitted, ValueDelta = 5_000m });
        await db.SaveChangesAsync();

        var r = await NewService(db, 1).GetCommandCenterAsync(pid, CancellationToken.None);
        Assert.True(r.IsSuccess);
        var d = r.Value!;

        Assert.Equal(3, d.Jobs.Total);
        Assert.Equal(1, d.Jobs.Open);        // Released
        Assert.Equal(1, d.Jobs.Completed);
        Assert.Equal(1, d.Jobs.OnHold);

        Assert.Equal(2, d.Amendments.Total);
        Assert.Equal(1, d.Amendments.Open);
        Assert.Equal(15_000m, d.Amendments.ApprovedValueDelta);
        Assert.Equal(5_000m, d.Amendments.PendingValueDelta);

        // effective contract = 100,000 + 15,000 approved = 115,000; margin = 115,000 - 70,000 = 45,000
        Assert.Equal(115_000m, d.EffectiveContractValue);
        Assert.Equal(45_000m, d.ProjectedMargin);
        Assert.Equal(10, d.DaysLateVsTarget);

        // "What's late?" must flag Attention (10 days past target)
        var late = d.Questions.First(q => q.Question == "What is late?");
        Assert.Equal(CommandCenterAnswerState.Attention, late.State);
        // "What changed?" must flag Attention (1 open amendment)
        var changed = d.Questions.First(q => q.Question == "What changed?");
        Assert.Equal(CommandCenterAnswerState.Attention, changed.State);
    }

    [Fact]
    public async Task Negative_margin_flags_attention()
    {
        using var db = NewDb();
        // contract 50k, est cost 80k → margin -30k
        var pid = await SeedProjectAsync(db, contractValue: 50_000m, estCost: 80_000m);

        var r = await NewService(db, 1).GetCommandCenterAsync(pid, CancellationToken.None);
        Assert.True(r.IsSuccess);
        var d = r.Value!;
        Assert.Equal(-30_000m, d.ProjectedMargin);
        var margin = d.Questions.First(q => q.Question == "What will margin be?");
        Assert.Equal(CommandCenterAnswerState.Attention, margin.State);
        var overBudget = d.Questions.First(q => q.Question == "What is over budget?");
        Assert.Equal(CommandCenterAnswerState.Attention, overBudget.State);
    }

    [Fact]
    public async Task Project_outside_tenant_scope_is_not_returned()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);   // company 99
        var r = await NewService(db, 1).GetCommandCenterAsync(pid, CancellationToken.None); // tenant sees only company 1
        Assert.True(r.IsFailure);
    }
}
