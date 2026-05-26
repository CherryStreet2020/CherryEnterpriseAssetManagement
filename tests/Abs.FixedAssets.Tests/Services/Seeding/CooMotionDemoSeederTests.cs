using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services.Production;
using Abs.FixedAssets.Services.Production.BackwardScheduling;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Seeding;

/// <summary>
/// Sprint 12.8 PR #5c.1 — CooMotionDemoSeeder tests.
///
/// Invariants (mirror PR #353's CfoMotionDemoSeeder discipline):
///   1. No demo-tenant Company row → warning, zero rows created.
///   2. Company exists but Name doesn't start with "PWH" → safety guard
///      fires (refusing to write demo data to a non-placeholder tenant).
///   3. Happy path — placeholder tenant + fixtures → all buckets create
///      expected rows; parent-child FK populated; cost stamps present.
///   4. Idempotency — second SeedAsync reports AlreadySeeded=true and
///      creates zero net new rows.
/// </summary>
public class CooMotionDemoSeederTests
{
    private const string DemoCode = "PWH-CAN";
    private const string DemoName = "PWH MANUFACTURING CANADA";

    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
            mb.Entity<ProductionOrder>().Ignore(p => p.RowVersion);
            mb.Entity<MaterialStructure>().Ignore(m => m.RowVersion);
            mb.Entity<CustomerProject>().Ignore(p => p.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"coo-demo-seeder-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    /// <summary>
    /// Test impl of IProductionOperationService. Persists ONE ProductionOperation
    /// row per ReleaseFromRoutingAsync call so the seeder's granular idempotency
    /// check (which requires both orders AND operations to be present before
    /// returning AlreadySeeded) sees real rows on the second invocation.
    /// </summary>
    private sealed class StubProductionOperationService : IProductionOperationService
    {
        private readonly AppDbContext _db;
        public StubProductionOperationService(AppDbContext db) { _db = db; }

        public async Task<Result<IReadOnlyList<ProductionOperation>>> ReleaseFromRoutingAsync(
            ReleaseFromRoutingRequest request, CancellationToken ct)
        {
            // Persist one snapshot op so granular idempotency works in tests.
            var op = new ProductionOperation
            {
                ProductionOrderId = request.ProductionOrderId,
                SequenceNumber = 10,
                LocationIdSnapshot = 0,
                CompanyIdSnapshot = 0,
                WorkCenterId = 1,
                Description = "Stubbed snapshot op for test",
            };
            _db.Set<ProductionOperation>().Add(op);
            await _db.SaveChangesAsync(ct);
            return Result.Success<IReadOnlyList<ProductionOperation>>(new[] { op });
        }

        public Task<Result<ProductionOperation>> UpdateStatusAsync(
            UpdateProductionOperationStatusRequest request, CancellationToken ct) =>
            Task.FromResult(Result.Failure<ProductionOperation>("stub"));

        public Task<Result<ProductionOperation>> RecordActualsAsync(
            RecordOperationActualsRequest request, CancellationToken ct) =>
            Task.FromResult(Result.Failure<ProductionOperation>("stub"));
    }

    /// <summary>
    /// No-op implementation of IBackwardSchedulingService for tests.
    /// </summary>
    private sealed class StubBackwardSchedulingService : IBackwardSchedulingService
    {
        public Task<Result<BackwardScheduleOutcome>> BackwardScheduleAsync(
            int parentProductionOrderId, CancellationToken ct) =>
            Task.FromResult(Result.Success(new BackwardScheduleOutcome(
                parentProductionOrderId,
                Array.Empty<int>(),
                OperationsStamped: 0,
                TotalSpannedDays: 0,
                Warnings: Array.Empty<string>())));
    }

    private static CooMotionDemoSeeder NewSeeder(AppDbContext db) =>
        new(db, new StubProductionOperationService(db), new StubBackwardSchedulingService(),
            NullLogger<CooMotionDemoSeeder>.Instance);

    /// <summary>
    /// Seed the minimum FK fixtures the service needs against a placeholder
    /// tenant: Company, ~3 WorkCenters, ~15 Items.
    /// </summary>
    private static async Task SeedFixturesAsync(AppDbContext db,
        string companyCode = DemoCode, string companyName = DemoName)
    {
        db.Companies.Add(new Company
        {
            Id = 100,
            CompanyCode = companyCode,
            Name = companyName,
            IsActive = true,
        });
        for (int i = 1; i <= 5; i++)
        {
            db.WorkCenters.Add(new WorkCenter
            {
                Id = i,
                Code = $"WC-{i:D2}",
                Name = $"WorkCenter {i}",
                CompanyId = 100,
                IsActive = true,
            });
        }
        for (int i = 1; i <= 15; i++)
        {
            db.Items.Add(new Item
            {
                Id = i,
                PartNumber = $"ITEM-{i:D3}",
                Description = $"Item {i}",
                CompanyId = 100,
                IsActive = true,
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedAsync_Without_DemoCompany_Returns_Warning_And_Zero_Rows()
    {
        await using var db = NewDb();
        var seeder = NewSeeder(db);

        var result = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(0, result.TotalRowsCreated);
        Assert.False(result.AlreadySeeded);
        Assert.Contains(result.Warnings, w => w.Contains(DemoCode) && w.Contains("not found"));
    }

    [Fact]
    public async Task SeedAsync_Refuses_To_Write_When_CompanyName_Does_Not_Match_Placeholder_Prefix()
    {
        await using var db = NewDb();
        // CompanyCode matches but Name has been changed away from PWH-*.
        await SeedFixturesAsync(db, companyCode: DemoCode, companyName: "Real Customer Co.");
        var seeder = NewSeeder(db);

        var result = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(0, result.TotalRowsCreated);
        Assert.False(result.AlreadySeeded);
        Assert.Contains(result.Warnings, w => w.Contains("does NOT start with") && w.Contains("PWH"));
    }

    [Fact]
    public async Task SeedAsync_Happy_Path_Creates_The_Tree()
    {
        await using var db = NewDb();
        await SeedFixturesAsync(db);
        var seeder = NewSeeder(db);

        var result = await seeder.SeedAsync(CancellationToken.None);

        Assert.Equal(DemoCode, result.CompanyCode);
        Assert.False(result.AlreadySeeded);

        Assert.True(result.LocationsCreated >= 1, "Locations should be created");
        Assert.Equal(1, result.CustomerProjectsCreated);
        Assert.Equal(4, result.ProjectPhasesCreated);
        Assert.Equal(10, result.MaterialStructuresCreated);
        Assert.True(result.MaterialStructureLinesCreated > 0, "BOM lines should be created");
        Assert.Equal(10, result.RoutingsCreated);
        Assert.True(result.RoutingOperationsCreated > 0, "Routing operations should be created");
        Assert.Equal(10, result.ProductionOrdersCreated);

        // Sanity-check DB state.
        Assert.True(await db.Locations.AnyAsync(l => l.Code == "PWH-CAN-MAIN"));
        Assert.True(await db.Locations.AnyAsync(l => l.Code == "PWH-CAN-NORTH"));
        Assert.True(await db.Set<CustomerProject>().AnyAsync(p => p.Code == "DEMO-COO-PROJ-001"));
        Assert.Equal(10, await db.Set<ProductionOrder>().CountAsync(p => p.OrderNumber.StartsWith("DEMO-COO-PRO-")));

        // Cost stamps populated on every order.
        Assert.True(await db.Set<ProductionOrder>()
            .Where(p => p.OrderNumber.StartsWith("DEMO-COO-PRO-"))
            .AllAsync(p => p.MaterialCost.HasValue && p.LaborCost.HasValue
                           && p.OverheadCost.HasValue && p.ActualCost.HasValue));
    }

    [Fact]
    public async Task SeedAsync_Parent_Child_FK_Populated()
    {
        await using var db = NewDb();
        await SeedFixturesAsync(db);
        var seeder = NewSeeder(db);

        await seeder.SeedAsync(CancellationToken.None);

        var parent = await db.Set<ProductionOrder>()
            .FirstAsync(p => p.OrderNumber == "DEMO-COO-PRO-1000");
        var children = await db.Set<ProductionOrder>()
            .Where(p => p.OrderNumber.StartsWith("DEMO-COO-PRO-1")
                        && p.OrderNumber != "DEMO-COO-PRO-1000")
            .ToListAsync();

        Assert.Null(parent.ParentProductionOrderId);
        Assert.Equal(9, children.Count);
        Assert.All(children, c => Assert.Equal(parent.Id, c.ParentProductionOrderId));
    }

    [Fact]
    public async Task SeedAsync_Is_Idempotent_On_Second_Call()
    {
        await using var db = NewDb();
        await SeedFixturesAsync(db);
        var seeder = NewSeeder(db);

        var first = await seeder.SeedAsync(CancellationToken.None);
        var ordersAfterFirst = await db.Set<ProductionOrder>().CountAsync();
        var projectsAfterFirst = await db.Set<CustomerProject>().CountAsync();
        var bomsAfterFirst = await db.Set<MaterialStructure>().CountAsync();

        var second = await seeder.SeedAsync(CancellationToken.None);

        Assert.True(second.AlreadySeeded);
        Assert.Equal(0, second.TotalRowsCreated);
        Assert.Equal(ordersAfterFirst, await db.Set<ProductionOrder>().CountAsync());
        Assert.Equal(projectsAfterFirst, await db.Set<CustomerProject>().CountAsync());
        Assert.Equal(bomsAfterFirst, await db.Set<MaterialStructure>().CountAsync());
    }
}
