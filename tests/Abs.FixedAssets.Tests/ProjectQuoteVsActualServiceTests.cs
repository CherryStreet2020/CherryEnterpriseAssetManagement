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
using CostElementType = Abs.FixedAssets.Models.Masters.CostElementType;

namespace Abs.FixedAssets.Tests;

// Theme B9 Wave 5 PR-13 — ProjectQuoteVsActualService: quoted snapshot (PR-5)
// vs live actuals/EAC (PR-12), bucket-for-bucket + headline.
public sealed class ProjectQuoteVsActualServiceTests
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
            .UseInMemoryDatabase($"projqva-{dbName}-{Guid.NewGuid()}")
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

    private static (ProjectQuoteVsActualService qva, ProjectFinancialsService fin) NewSvcs(AppDbContext db, params int[] visible)
    {
        var tenant = new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() };
        var fin = new ProjectFinancialsService(db, tenant, NullLogger<ProjectFinancialsService>.Instance);
        var qva = new ProjectQuoteVsActualService(db, tenant, fin, NullLogger<ProjectQuoteVsActualService>.Instance);
        return (qva, fin);
    }

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1, decimal? contract = 100_000m)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-Q-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "QvA test", Status = CustomerProjectStatus.Active, ContractValue = contract,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    private static async Task SeedSnapshotAsync(AppDbContext db, int pid,
        decimal material, decimal labor, decimal sub, decimal overhead, decimal total,
        decimal quotedPrice, decimal marginPct)
    {
        db.ProjectEstimateSnapshots.Add(new ProjectEstimateSnapshot
        {
            CompanyId = 1, CustomerProjectId = pid, Currency = "USD",
            MaterialCost = material, LaborCost = labor, SubcontractCost = sub, OverheadCost = overhead, OtherCost = 0m,
            DirectTotalCost = material + labor + sub + overhead, TotalCost = total,
            QuotedPrice = quotedPrice, EstimatedMarginPct = marginPct, LineCount = 4,
            CapturedAt = new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc), CapturedBy = "test",
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Compares_quoted_buckets_to_actuals_with_variance()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        await SeedSnapshotAsync(db, pid, material: 30_000m, labor: 10_000m, sub: 5_000m, overhead: 2_000m,
            total: 50_000m, quotedPrice: 100_000m, marginPct: 50m);
        var (qva, fin) = NewSvcs(db);
        await fin.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.Material, 35_000m, new DateTime(2026, 6, 1)));
        await fin.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.Labor, 8_000m, new DateTime(2026, 6, 1)));

        var v = (await qva.GetComparisonAsync(pid)).Value!;

        Assert.True(v.HasQuotedBaseline);
        Assert.Equal(50_000m, v.QuotedTotalCost);
        var mat = v.Buckets.Single(b => b.Bucket == "Material");
        Assert.Equal(30_000m, mat.Quoted);
        Assert.Equal(35_000m, mat.Actual);
        Assert.Equal(5_000m, mat.Variance);             // over the quote
        var lab = v.Buckets.Single(b => b.Bucket == "Labor");
        Assert.Equal(-2_000m, lab.Variance);            // under the quote (10k quoted, 8k actual)
    }

    [Fact]
    public async Task Overhead_bucket_sums_variable_and_fixed_overhead_actuals()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        await SeedSnapshotAsync(db, pid, material: 0m, labor: 0m, sub: 0m, overhead: 5_000m,
            total: 5_000m, quotedPrice: 10_000m, marginPct: 50m);
        var (qva, fin) = NewSvcs(db);
        await fin.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.VariableOverhead, 1_200m, new DateTime(2026, 6, 1)));
        await fin.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.FixedOverhead, 800m, new DateTime(2026, 6, 1)));

        var v = (await qva.GetComparisonAsync(pid)).Value!;
        var oh = v.Buckets.Single(b => b.Bucket == "Overhead");
        Assert.Equal(5_000m, oh.Quoted);
        Assert.Equal(2_000m, oh.Actual);   // 1,200 var + 800 fixed
    }

    [Fact]
    public async Task Headline_carries_quoted_margin_and_eac_variance()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, contract: 100_000m);
        await SeedSnapshotAsync(db, pid, material: 30_000m, labor: 10_000m, sub: 5_000m, overhead: 5_000m,
            total: 50_000m, quotedPrice: 100_000m, marginPct: 50m);
        var (qva, fin) = NewSvcs(db);
        // A material EAC-only forecast pushing EAC to 60k.
        await fin.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.Material, 20_000m, new DateTime(2026, 6, 1)));
        await fin.CreateForecastAsync(new CreateForecastRequest(pid, CostElementType.Material, new DateTime(2026, 6, 2), EstimateAtCompletion: 60_000m));

        var v = (await qva.GetComparisonAsync(pid)).Value!;
        Assert.Equal(50m, v.QuotedMarginPct);
        Assert.Equal(60_000m, v.EstimateAtCompletion);   // 20k actual + (60k EAC − 20k actual) ETC
        Assert.Equal(10_000m, v.QuotedCostVsEacVariance); // 60k EAC − 50k quoted
    }

    [Fact]
    public async Task No_quoted_baseline_returns_graceful_empty_surface()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var (qva, _) = NewSvcs(db);

        var res = await qva.GetComparisonAsync(pid);

        Assert.True(res.IsSuccess);
        Assert.False(res.Value!.HasQuotedBaseline);
        Assert.Empty(res.Value!.Buckets);
        Assert.Contains("No quoted baseline", res.Value!.Narrative, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_project_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);
        var (qva, _) = NewSvcs(db, 1);

        var res = await qva.GetComparisonAsync(pid);

        Assert.True(res.IsFailure);
        Assert.Contains("tenant scope", res.Error, StringComparison.OrdinalIgnoreCase);
    }
}
