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
using Abs.FixedAssets.Services.Production;
using Abs.FixedAssets.Services.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

// Theme B9 Wave 1 PR-2 — ProjectPromiseService verdict logic (schedule / job / amendment signals).
public sealed class ProjectPromiseServiceTests
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
            .UseInMemoryDatabase($"promise-{dbName}-{Guid.NewGuid()}")
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

    // Minimal readiness stub — never exercised by these tests (no Released/InProgress jobs).
    private sealed class StubReadiness : IOperationReadinessService
    {
        public Task<Result<OperationReadiness>> CheckOperationReadinessAsync(int operationId, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<OperationReadiness>("stub"));
        public Task<Result<ProductionOrderReadiness>> CheckOrderReadinessAsync(int productionOrderId, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<ProductionOrderReadiness>("stub"));
        public Task<Result<IReadOnlyList<MaterialReadinessDetail>>> CheckMaterialReadinessAsync(int operationId, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<IReadOnlyList<MaterialReadinessDetail>>("stub"));
        public Task<Result<int>> RefreshSupplyLinksAsync(int productionOrderId, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<int>("stub"));
        public Task<Result<ProductionMaterialStructure>> LinkSupplyAsync(LinkSupplyRequest request, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<ProductionMaterialStructure>("stub"));
        public Task<Result<ProductionMaterialStructure>> UnlinkSupplyAsync(int bomLineId, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<ProductionMaterialStructure>("stub"));
        public Task<Result<ProductionMaterialStructure>> UpdateSupplyStatusAsync(UpdateSupplyStatusRequest request, CancellationToken ct = default)
            => Task.FromResult(Result.Failure<ProductionMaterialStructure>("stub"));
        public Task<IReadOnlyList<ProductionMaterialStructure>> GetSupplyLinksForOperationAsync(int operationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProductionMaterialStructure>>(Array.Empty<ProductionMaterialStructure>());
        public Task<IReadOnlyList<ProductionMaterialStructure>> GetSupplyLinksForOrderAsync(int productionOrderId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ProductionMaterialStructure>>(Array.Empty<ProductionMaterialStructure>());
    }

    private static ProjectPromiseService NewService(AppDbContext db) =>
        new(db, new StubTenantContext(), new StubReadiness(), NullLogger<ProjectPromiseService>.Instance);

    private static async Task<int> SeedAsync(AppDbContext db, DateTime? targetEnd, CustomerProjectStatus status = CustomerProjectStatus.Active)
    {
        var proj = new CustomerProject
        {
            CompanyId = 1, Code = "PRJ-PROMISE-1", Name = "Promise test", Status = status,
            Mode = CustomerProjectMode.Standard, Currency = "USD", TargetEndDate = targetEnd,
        };
        db.CustomerProjects.Add(proj);
        await db.SaveChangesAsync();
        return proj.Id;
    }

    [Fact]
    public async Task Clean_project_with_future_target_is_green()
    {
        using var db = NewDb();
        var pid = await SeedAsync(db, DateTime.UtcNow.Date.AddDays(60));
        var r = await NewService(db).EvaluateAsync(pid, CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(PromiseStatus.Green, r.Value!.Status);
        Assert.Empty(r.Value.Reasons);
    }

    [Fact]
    public async Task Past_due_and_incomplete_is_black()
    {
        using var db = NewDb();
        var pid = await SeedAsync(db, DateTime.UtcNow.Date.AddDays(-5));
        var r = await NewService(db).EvaluateAsync(pid, CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(PromiseStatus.Black, r.Value!.Status);
        Assert.Contains(r.Value.Reasons, x => x.Code == PromiseReasonCode.AlreadyPastDue);
    }

    [Fact]
    public async Task Onhold_linked_job_is_red()
    {
        using var db = NewDb();
        var pid = await SeedAsync(db, DateTime.UtcNow.Date.AddDays(30));
        db.ProductionOrders.Add(new ProductionOrder { CompanyId = 1, OrderNumber = "PRO-H", Status = ProductionOrderStatus.OnHold, CustomerProjectId = pid });
        await db.SaveChangesAsync();
        var r = await NewService(db).EvaluateAsync(pid, CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(PromiseStatus.Red, r.Value!.Status);
        Assert.Contains(r.Value.Reasons, x => x.Code == PromiseReasonCode.JobOnHold);
    }

    [Fact]
    public async Task Open_amendment_is_yellow()
    {
        using var db = NewDb();
        var pid = await SeedAsync(db, DateTime.UtcNow.Date.AddDays(30));
        db.ProjectAmendments.Add(new ProjectAmendment { CustomerProjectId = pid, AmendmentNumber = 1, EffectiveDate = DateTime.UtcNow, Status = ProjectAmendmentStatus.Submitted, ValueDelta = 1000m });
        await db.SaveChangesAsync();
        var r = await NewService(db).EvaluateAsync(pid, CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(PromiseStatus.Yellow, r.Value!.Status);
        Assert.Contains(r.Value.Reasons, x => x.Code == PromiseReasonCode.ChangeOrderNotApproved);
    }

    [Fact]
    public async Task No_target_date_flags_yellow_no_baseline()
    {
        using var db = NewDb();
        var pid = await SeedAsync(db, targetEnd: null);
        var r = await NewService(db).EvaluateAsync(pid, CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(PromiseStatus.Yellow, r.Value!.Status);
        Assert.Contains(r.Value.Reasons, x => x.Code == PromiseReasonCode.NoScheduleBaselined);
    }

    [Fact]
    public async Task EvaluateByRef_resolves_by_code()
    {
        using var db = NewDb();
        var pid = await SeedAsync(db, DateTime.UtcNow.Date.AddDays(60));
        var r = await NewService(db).EvaluateByRefAsync("PRJ-PROMISE-1", CancellationToken.None);
        Assert.True(r.IsSuccess);
        Assert.Equal(pid, r.Value!.ProjectId);
    }
}
