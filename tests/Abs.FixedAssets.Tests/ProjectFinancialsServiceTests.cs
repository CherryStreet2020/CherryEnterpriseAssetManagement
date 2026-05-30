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

// Theme B9 Wave 5 PR-12 — ProjectFinancialsService (the margin engine):
// budget/line/lock, actual posting, forecast-driven ETC, committed-from-
// commitments cross-wire, EAC snapshot, and the closed-project posting guard.
public sealed class ProjectFinancialsServiceTests
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
            .UseInMemoryDatabase($"projfin-{dbName}-{Guid.NewGuid()}")
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

    private static ProjectFinancialsService NewSvc(AppDbContext db, params int[] visible) =>
        new(db, new StubTenantContext { VisibleCompanyIds = (visible.Length == 0 ? new[] { 1 } : visible).ToList() },
            NullLogger<ProjectFinancialsService>.Instance);

    private static async Task<int> SeedProjectAsync(AppDbContext db, int companyId = 1,
        decimal? contract = 185_000m, CustomerProjectStatus status = CustomerProjectStatus.Active)
    {
        var p = new CustomerProject
        {
            CompanyId = companyId, Code = $"PRJ-F-{Guid.NewGuid():N}".Substring(0, 12),
            Name = "Financials test", Status = status, ContractValue = contract,
            Mode = CustomerProjectMode.Standard, Currency = "USD",
        };
        db.CustomerProjects.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // Build a locked budget with two element lines totalling 60,000.
    private static async Task<int> SeedLockedBudgetAsync(ProjectFinancialsService svc, int pid)
    {
        var b = (await svc.CreateBudgetAsync(new CreateBudgetRequest(pid, "BUD1", "Baseline"))).Value;
        await svc.AddBudgetLineAsync(new AddBudgetLineRequest(b, 1, CostElementType.Material, 40_000m));
        await svc.AddBudgetLineAsync(new AddBudgetLineRequest(b, 2, CostElementType.Labor, 20_000m));
        await svc.LockBudgetAsync(b, "pm");
        return b;
    }

    // -- budget + lock -----------------------------------------------------

    [Fact]
    public async Task Budget_lines_roll_up_and_lock_freezes_lines()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        var b = await SeedLockedBudgetAsync(svc, pid);

        var addAfterLock = await svc.AddBudgetLineAsync(new AddBudgetLineRequest(b, 3, CostElementType.Subcontract, 5_000m));

        Assert.True(addAfterLock.IsFailure);
        Assert.Contains("locked", addAfterLock.Error, StringComparison.OrdinalIgnoreCase);

        var view = (await svc.GetFinancialsAsync(pid)).Value!;
        Assert.Equal(60_000m, view.BudgetTotal);
        Assert.Equal(b, view.BaselineBudgetId);
    }

    // -- margin math (ETC fallback = budget − actual) ----------------------

    [Fact]
    public async Task Margin_uses_budget_minus_actual_when_no_forecast()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, contract: 100_000m);
        var svc = NewSvc(db);
        await SeedLockedBudgetAsync(svc, pid);   // budget 60,000
        await svc.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.Material, 15_000m, new DateTime(2026, 6, 1)));

        var v = (await svc.GetFinancialsAsync(pid)).Value!;
        Assert.Equal(60_000m, v.BudgetTotal);
        Assert.Equal(15_000m, v.ActualCostToDate);
        Assert.Equal(45_000m, v.EstimateToComplete);   // 60k − 15k
        Assert.Equal(60_000m, v.EstimateAtCompletion); // 15k + 45k
        Assert.Equal(40_000m, v.ProjectedMargin);      // 100k − 60k
        Assert.Equal(40m, v.ProjectedMarginPercent);
    }

    [Fact]
    public async Task Forecast_drives_etc_over_the_fallback()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, contract: 100_000m);
        var svc = NewSvc(db);
        await SeedLockedBudgetAsync(svc, pid);   // 60,000
        await svc.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.Labor, 10_000m, new DateTime(2026, 6, 1)));
        await svc.CreateForecastAsync(new CreateForecastRequest(pid, CostElementType.Material, new DateTime(2026, 6, 2), EstimateToComplete: 70_000m));

        var v = (await svc.GetFinancialsAsync(pid)).Value!;
        Assert.Equal(70_000m, v.EstimateToComplete);     // forecast wins, not 60−10
        Assert.Equal(80_000m, v.EstimateAtCompletion);   // 10k + 70k
        Assert.Equal(20_000m, v.ProjectedMargin);        // 100k − 80k
    }

    // -- committed-cost cross-wire to PR-10 commitments --------------------

    [Fact]
    public async Task Committed_cost_pulls_open_commitment_balance()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        // Open commitment of 38,500 with a 4,180 receipt → open balance 34,320.
        var c = new ProjectCommitment
        {
            CustomerProjectId = pid, Code = "CMT-1", CommittedAmount = 38_500m,
            Status = ProjectCommitmentStatus.PartiallyReceived, Currency = "USD",
        };
        db.ProjectCommitments.Add(c);
        await db.SaveChangesAsync();
        db.ProjectReceipts.Add(new ProjectReceipt { ProjectCommitmentId = c.Id, CustomerProjectId = pid, ReceivedAmount = 4_180m, ReceiptDate = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var svc = NewSvc(db);

        var v = (await svc.GetFinancialsAsync(pid)).Value!;
        Assert.Equal(34_320m, v.CommittedCost);
    }

    // -- closed-project posting guard --------------------------------------

    [Fact]
    public async Task Post_actual_blocked_on_closed_project()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, status: CustomerProjectStatus.Closed);
        var svc = NewSvc(db);

        var res = await svc.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.Material, 100m, new DateTime(2026, 6, 1)));

        Assert.True(res.IsFailure);
        Assert.Contains("Closed", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    // -- EAC snapshot ------------------------------------------------------

    [Fact]
    public async Task Snapshot_freezes_position_and_surfaces_as_latest()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, contract: 100_000m);
        var svc = NewSvc(db);
        await SeedLockedBudgetAsync(svc, pid);
        await svc.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.Material, 15_000m, new DateTime(2026, 6, 1)));

        var snapId = await svc.SnapshotEacAsync(new SnapshotEacRequest(pid, "Mid-build"));
        Assert.True(snapId.IsSuccess);

        var v = (await svc.GetFinancialsAsync(pid)).Value!;
        Assert.NotNull(v.LatestSnapshot);
        Assert.Equal(60_000m, v.LatestSnapshot!.EstimateAtCompletion);
        Assert.Equal(40_000m, v.LatestSnapshot!.ProjectedMargin);
        Assert.Equal("Mid-build", v.LatestSnapshot!.SnapshotReason);
    }

    // -- tenant scope ------------------------------------------------------

    [Fact]
    public async Task CreateBudget_rejects_project_outside_tenant_scope()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db, companyId: 99);
        var svc = NewSvc(db, 1);

        var res = await svc.CreateBudgetAsync(new CreateBudgetRequest(pid, "BUD1", "X"));

        Assert.True(res.IsFailure);
        Assert.Contains("tenant scope", res.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Duplicate_budget_code_rejected()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await svc.CreateBudgetAsync(new CreateBudgetRequest(pid, "BUD1", "First"));

        var dup = await svc.CreateBudgetAsync(new CreateBudgetRequest(pid, "BUD1", "Second"));

        Assert.True(dup.IsFailure);
        Assert.Contains("already exists", dup.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task By_element_breakdown_pairs_budget_and_actual()
    {
        using var db = NewDb();
        var pid = await SeedProjectAsync(db);
        var svc = NewSvc(db);
        await SeedLockedBudgetAsync(svc, pid);   // Material 40k, Labor 20k
        await svc.PostActualCostAsync(new PostActualCostRequest(pid, CostElementType.Material, 12_000m, new DateTime(2026, 6, 1)));

        var v = (await svc.GetFinancialsAsync(pid)).Value!;
        var mat = v.ByElement.Single(e => e.CostElementType == CostElementType.Material);
        Assert.Equal(40_000m, mat.Budget);
        Assert.Equal(12_000m, mat.Actual);
        Assert.Equal(28_000m, mat.Variance);
    }
}
