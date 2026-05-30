using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

// Theme B9 Wave 2 PR-5 — ProjectEstimateService: cost-model rollup + immutable snapshot freeze.
public sealed class ProjectEstimateServiceTests
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
            .UseInMemoryDatabase($"projest-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectEstimateService NewEstimateSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectEstimateService>.Instance);

    private static ProjectQuoteService NewQuoteSvc(AppDbContext db) =>
        new(db, new StubTenantContext(), NullLogger<ProjectQuoteService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1, string code = "PRJ-E-1")
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = code, Name = "Estimate test", Status = CustomerProjectStatus.Active,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // Builds an estimate with Material 1000 + Labor (40h × 50 = 2000) + Subcontract 500
    // + Overhead 300 = 3800 direct.
    private static async Task<int> SeedEstimateWithLinesAsync(AppDbContext db, ProjectEstimateService svc, int pid,
        decimal? contingencyPct = null)
    {
        var e = await svc.CreateEstimateAsync(new CreateEstimateRequest(pid, "EST-1", ContingencyPct: contingencyPct));
        Assert.True(e.IsSuccess);
        int eid = e.Value;
        await svc.AddLineAsync(new AddEstimateLineRequest(eid, CostElementType.Material, "Ti-6Al-4V plate", Quantity: 1m, UnitCost: 1000m));
        await svc.AddLineAsync(new AddEstimateLineRequest(eid, CostElementType.Labor, "5-axis machining", Hours: 40m, Rate: 50m));
        await svc.AddLineAsync(new AddEstimateLineRequest(eid, CostElementType.Subcontract, "Anodize (NADCAP)", Quantity: 1m, UnitCost: 500m));
        await svc.AddLineAsync(new AddEstimateLineRequest(eid, CostElementType.FixedOverhead, "Burden", Quantity: 1m, UnitCost: 300m));
        return eid;
    }

    [Fact]
    public async Task Rollup_buckets_costs_by_element()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewEstimateSvc(db);
        var eid = await SeedEstimateWithLinesAsync(db, svc, pid);

        var r = await svc.GetEstimateAsync(eid);
        Assert.True(r.IsSuccess);
        var v = r.Value!;
        Assert.Equal(1000m, v.MaterialCost);
        Assert.Equal(2000m, v.LaborCost);       // 40h × 50
        Assert.Equal(500m, v.SubcontractCost);
        Assert.Equal(300m, v.OverheadCost);
        Assert.Equal(3800m, v.DirectTotalCost);
        Assert.Equal(3800m, v.TotalCost);         // no contingency
        Assert.Equal(4, v.LineCount);
    }

    [Fact]
    public async Task Contingency_applies_on_top_of_direct_total()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewEstimateSvc(db);
        var eid = await SeedEstimateWithLinesAsync(db, svc, pid, contingencyPct: 10m);

        var v = (await svc.GetEstimateAsync(eid)).Value!;
        Assert.Equal(3800m, v.DirectTotalCost);
        Assert.Equal(4180m, v.TotalCost);         // 3800 × 1.10
    }

    [Fact]
    public async Task Snapshot_freezes_rollup_and_locks_estimate()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewEstimateSvc(db);
        var eid = await SeedEstimateWithLinesAsync(db, svc, pid);

        var s = await svc.SnapshotEstimateAsync(new SnapshotEstimateRequest(eid, QuotedPrice: 5000m));
        Assert.True(s.IsSuccess);
        Assert.Equal(3800m, s.Value!.TotalCost);
        Assert.Equal(5000m, s.Value.QuotedPrice);
        Assert.Equal(24m, s.Value.EstimatedMarginPct); // (5000-3800)/5000 = 24%

        // Estimate is now locked — further edits rejected.
        var add = await svc.AddLineAsync(new AddEstimateLineRequest(eid, CostElementType.Material, "extra", Quantity: 1m, UnitCost: 1m));
        Assert.True(add.IsFailure);
        Assert.Contains("locked", add.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Snapshot_is_immutable_after_estimate_edits_are_blocked()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewEstimateSvc(db);
        var eid = await SeedEstimateWithLinesAsync(db, svc, pid);
        var snapId = (await svc.SnapshotEstimateAsync(new SnapshotEstimateRequest(eid, QuotedPrice: 5000m))).Value!.SnapshotId;

        // The snapshot retains the frozen total even though the working estimate can't change.
        var read = await svc.GetSnapshotAsync(snapId);
        Assert.True(read.IsSuccess);
        Assert.Equal(3800m, read.Value!.TotalCost);
        Assert.Equal(4, read.Value.LineCount);
    }

    [Fact]
    public async Task Snapshot_attaches_to_quote_revision_and_stamps_margin()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var estSvc = NewEstimateSvc(db);
        var quoteSvc = NewQuoteSvc(db);

        // A submitted quote revision with TotalPrice 5000.
        var q = await quoteSvc.CreateQuoteAsync(new CreateQuoteRequest(pid, "Q-EST"));
        var revId = await db.ProjectQuoteRevisions.Where(r => r.ProjectQuoteId == q.Value!.QuoteId).Select(r => r.Id).FirstAsync();
        await quoteSvc.AddLineAsync(new AddQuoteLineRequest(revId, PartNumber: "P1", Quantity: 1m, UnitPrice: 5000m));
        await quoteSvc.SubmitRevisionAsync(revId);

        var eid = await SeedEstimateWithLinesAsync(db, estSvc, pid);
        var s = await estSvc.SnapshotEstimateAsync(new SnapshotEstimateRequest(eid, RevisionId: revId));
        Assert.True(s.IsSuccess);

        var rev = await db.ProjectQuoteRevisions.SingleAsync(r => r.Id == revId);
        Assert.Equal(s.Value!.SnapshotId, rev.SourceEstimateSnapshotId);
        Assert.Equal(24m, rev.EstimatedMarginPct);  // (5000-3800)/5000, price from the revision total
    }

    [Fact]
    public async Task Snapshot_with_no_lines_is_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewEstimateSvc(db);
        var e = await svc.CreateEstimateAsync(new CreateEstimateRequest(pid, "EST-EMPTY"));
        var s = await svc.SnapshotEstimateAsync(new SnapshotEstimateRequest(e.Value));
        Assert.True(s.IsFailure);
    }

    [Fact]
    public async Task AddLine_rejects_item_from_another_tenant()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var foreign = new Item { PartNumber = "OTHER-CO", Description = "Foreign", CompanyId = 99 };
        db.Items.Add(foreign);
        await db.SaveChangesAsync();
        var svc = NewEstimateSvc(db, 1);
        var e = await svc.CreateEstimateAsync(new CreateEstimateRequest(pid, "EST-T"));

        var l = await svc.AddLineAsync(new AddEstimateLineRequest(e.Value, CostElementType.Material, ItemId: foreign.Id, Quantity: 1m, UnitCost: 5m));
        Assert.True(l.IsFailure);
        Assert.Contains("tenant scope", l.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateEstimate_for_project_outside_tenant_scope_fails()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);
        var svc = NewEstimateSvc(db, 1);
        var r = await svc.CreateEstimateAsync(new CreateEstimateRequest(pid, "EST-X"));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task CreateEstimate_duplicate_number_per_company_is_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewEstimateSvc(db);
        await svc.CreateEstimateAsync(new CreateEstimateRequest(pid, "EST-DUP"));
        var dup = await svc.CreateEstimateAsync(new CreateEstimateRequest(pid, "EST-DUP"));
        Assert.True(dup.IsFailure);
    }
}
