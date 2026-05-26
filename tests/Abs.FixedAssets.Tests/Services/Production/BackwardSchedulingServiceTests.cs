using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production.BackwardScheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Production;

/// <summary>
/// Sprint 12.8 PR #2 — BackwardSchedulingService unit tests.
///
/// Covers the stub's contract surface:
///   1. Parent missing                         → Failure
///   2. Parent outside tenant scope            → Failure
///   3. Parent with no ScheduledEnd            → Failure
///   4. Parent with no children                → Success, empty outcome
///   5. Single child + 3 ops walks correctly   → schedule + ops stamped, arithmetic clean
///   6. Multi-child stack runs sequentially in CreatedAt-desc order
///   7. Child with no released ops             → Warning issued, schedule stamped as zero-duration placeholder
///
/// Uses the same EF Core InMemory pattern as ChainTraceServiceTests +
/// ReceivingPostingServiceTests — TestAppDbContext that Ignores RowVersion
/// timestamps + LookupValue.Metadata. ProductionOrder.RowVersion is the PG
/// xmin shadow column; InMemory cannot model it, so we Ignore.
/// </summary>
public class BackwardSchedulingServiceTests
{
    private const int CompanyId = 100;

    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
            mb.Entity<ProductionOrder>().Ignore(p => p.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backward-schedule-{dbName}-{Guid.NewGuid()}")
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

    private static BackwardSchedulingService NewService(AppDbContext db, ITenantContext? tenant = null)
    {
        tenant ??= new StubTenantContext
        {
            CompanyId = CompanyId,
            VisibleCompanyIds = new() { CompanyId },
        };
        return new BackwardSchedulingService(
            db,
            tenant,
            NullLogger<BackwardSchedulingService>.Instance);
    }

    /// <summary>
    /// Seeds a parent ProductionOrder with the given ScheduledEnd and CompanyId.
    /// Caller can pass null ScheduledEnd to test the missing-end path.
    /// </summary>
    private static async Task<ProductionOrder> SeedParentAsync(
        AppDbContext db, DateTime? scheduledEnd, int companyId = CompanyId)
    {
        var parent = new ProductionOrder
        {
            CompanyId = companyId,
            OrderNumber = $"PRO-PARENT-{Guid.NewGuid():N}".Substring(0, 30),
            Title = "Parent Assembly",
            Type = ProductionType.JobShop,
            Status = ProductionOrderStatus.Planned,
            QuantityOrdered = 1,
            ScheduledEnd = scheduledEnd,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
        };
        db.ProductionOrders.Add(parent);
        await db.SaveChangesAsync();
        return parent;
    }

    /// <summary>
    /// Seeds a child ProductionOrder under the given parent. Returns the child
    /// without operations — caller can add SeedOpAsync rows next.
    /// </summary>
    private static async Task<ProductionOrder> SeedChildAsync(
        AppDbContext db,
        ProductionOrder parent,
        DateTime createdAt,
        string? orderNumberOverride = null)
    {
        var child = new ProductionOrder
        {
            CompanyId = parent.CompanyId,
            OrderNumber = orderNumberOverride ?? $"PRO-C-{Guid.NewGuid():N}".Substring(0, 30),
            Title = "Child Sub-assembly",
            Type = ProductionType.JobShop,
            Status = ProductionOrderStatus.Planned,
            QuantityOrdered = 1,
            ParentProductionOrderId = parent.Id,
            CreatedAt = createdAt,
        };
        db.ProductionOrders.Add(child);
        await db.SaveChangesAsync();
        return child;
    }

    /// <summary>
    /// Seeds a ProductionOperation under the given child PRO with the five
    /// planned-time components summed to <paramref name="totalMins"/>. For
    /// arithmetic tests this lets us assert tight wall-clock math without
    /// caring how the total is decomposed.
    /// </summary>
    private static async Task<ProductionOperation> SeedOpAsync(
        AppDbContext db,
        ProductionOrder child,
        int sequenceNumber,
        decimal totalMins)
    {
        // Spread the total across the five buckets so the arithmetic path
        // exercises all five. 60% run, 10% setup/queue/move/wait each.
        var setup = Math.Round(totalMins * 0.10m, 4);
        var queue = Math.Round(totalMins * 0.10m, 4);
        var move = Math.Round(totalMins * 0.10m, 4);
        var wait = Math.Round(totalMins * 0.10m, 4);
        var run = totalMins - setup - queue - move - wait;

        var op = new ProductionOperation
        {
            ProductionOrderId = child.Id,
            SequenceNumber = sequenceNumber,
            LocationIdSnapshot = 1,
            CompanyIdSnapshot = child.CompanyId,
            WorkCenterId = 1,
            OperationType = ProductionOperationType.Run,
            Status = ProductionOperationStatus.Scheduled,
            Description = $"Op {sequenceNumber}",
            PlannedSetupMins = setup,
            PlannedRunMins = run,
            PlannedQueueMins = queue,
            PlannedMoveMins = move,
            PlannedWaitMins = wait,
            PlannedQty = 1,
        };
        db.ProductionOperations.Add(op);
        await db.SaveChangesAsync();
        return op;
    }

    [Fact]
    public async Task BackwardSchedule_Returns_Failure_When_Parent_Not_Found()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        var result = await svc.BackwardScheduleAsync(999_999, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error ?? "");
    }

    [Fact]
    public async Task BackwardSchedule_Returns_Failure_When_Parent_Outside_Tenant_Scope()
    {
        await using var db = NewDb();
        var parent = await SeedParentAsync(db, scheduledEnd: DateTime.UtcNow.AddDays(60), companyId: 777);
        var svc = NewService(db); // default tenant only sees CompanyId=100

        var result = await svc.BackwardScheduleAsync(parent.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("tenant scope", result.Error ?? "");
    }

    [Fact]
    public async Task BackwardSchedule_Returns_Failure_When_Parent_Has_No_ScheduledEnd()
    {
        await using var db = NewDb();
        var parent = await SeedParentAsync(db, scheduledEnd: null);
        var svc = NewService(db);

        var result = await svc.BackwardScheduleAsync(parent.Id, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("ScheduledEnd", result.Error ?? "");
    }

    [Fact]
    public async Task BackwardSchedule_Returns_Empty_Outcome_When_Parent_Has_No_Children()
    {
        await using var db = NewDb();
        var parent = await SeedParentAsync(db, scheduledEnd: DateTime.UtcNow.AddDays(60));
        var svc = NewService(db);

        var result = await svc.BackwardScheduleAsync(parent.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(parent.Id, result.Value!.ParentProductionOrderId);
        Assert.Empty(result.Value.ChildProductionOrderIds);
        Assert.Equal(0, result.Value.OperationsStamped);
        Assert.Equal(0, result.Value.TotalSpannedDays);
        Assert.Empty(result.Value.Warnings);
    }

    [Fact]
    public async Task BackwardSchedule_Single_Child_With_Three_Ops_Stamps_Schedule_And_Operations()
    {
        await using var db = NewDb();
        // Parent ends 2026-08-14 17:00 UTC. Child has 3 ops totalling
        // 60 + 120 + 30 = 210 minutes (3h 30m). Child should land at
        // ScheduledEnd = 2026-08-14 17:00, ScheduledStart = 2026-08-14 13:30.
        var parentEnd = new DateTime(2026, 8, 14, 17, 0, 0, DateTimeKind.Utc);
        var parent = await SeedParentAsync(db, scheduledEnd: parentEnd);
        var child = await SeedChildAsync(db, parent, createdAt: DateTime.UtcNow);

        var op1 = await SeedOpAsync(db, child, sequenceNumber: 10, totalMins: 60);
        var op2 = await SeedOpAsync(db, child, sequenceNumber: 20, totalMins: 120);
        var op3 = await SeedOpAsync(db, child, sequenceNumber: 30, totalMins: 30);

        var svc = NewService(db);
        var result = await svc.BackwardScheduleAsync(parent.Id, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Value!.OperationsStamped);
        Assert.Single(result.Value.ChildProductionOrderIds);
        Assert.Equal(child.Id, result.Value.ChildProductionOrderIds[0]);
        Assert.Empty(result.Value.Warnings);

        var refreshedChild = await db.ProductionOrders.AsNoTracking().FirstAsync(p => p.Id == child.Id);
        Assert.Equal(parentEnd, refreshedChild.ScheduledEnd);
        Assert.Equal(parentEnd.AddMinutes(-210), refreshedChild.ScheduledStart);

        // Walk backward: op3 ends at parentEnd, op2 ends at op3.start,
        // op1 ends at op2.start. Op times: op3=30m, op2=120m, op1=60m.
        var refreshedOp3 = await db.ProductionOperations.AsNoTracking().FirstAsync(o => o.Id == op3.Id);
        var refreshedOp2 = await db.ProductionOperations.AsNoTracking().FirstAsync(o => o.Id == op2.Id);
        var refreshedOp1 = await db.ProductionOperations.AsNoTracking().FirstAsync(o => o.Id == op1.Id);

        Assert.Equal(parentEnd, refreshedOp3.PlannedEnd);
        Assert.Equal(parentEnd.AddMinutes(-30), refreshedOp3.PlannedStart);
        Assert.Equal(refreshedOp3.PlannedStart, refreshedOp2.PlannedEnd);
        Assert.Equal(parentEnd.AddMinutes(-150), refreshedOp2.PlannedStart);
        Assert.Equal(refreshedOp2.PlannedStart, refreshedOp1.PlannedEnd);
        Assert.Equal(parentEnd.AddMinutes(-210), refreshedOp1.PlannedStart);

        // Total span = 210 minutes = 3h 30m → floor(0.146 days) = 0.
        Assert.Equal(0, result.Value.TotalSpannedDays);
    }

    [Fact]
    public async Task BackwardSchedule_Multiple_Children_Stack_Sequentially_In_CreatedAt_Desc_Order()
    {
        await using var db = NewDb();
        var parentEnd = new DateTime(2026, 8, 14, 17, 0, 0, DateTimeKind.Utc);
        var parent = await SeedParentAsync(db, scheduledEnd: parentEnd);

        // Three children created at staggered times. The NEWEST (latest
        // CreatedAt) finishes LAST, i.e. at parent.ScheduledEnd. The
        // OLDEST finishes FIRST in wall-clock terms (earlier than the
        // newest). Per the stub's "OrderByDescending(CreatedAt)" pass.
        var newest = await SeedChildAsync(db, parent, createdAt: DateTime.UtcNow);
        var middle = await SeedChildAsync(db, parent, createdAt: DateTime.UtcNow.AddDays(-1));
        var oldest = await SeedChildAsync(db, parent, createdAt: DateTime.UtcNow.AddDays(-2));

        await SeedOpAsync(db, newest, 10, totalMins: 60);   // newest = 60m block
        await SeedOpAsync(db, middle, 10, totalMins: 90);   // middle = 90m block
        await SeedOpAsync(db, oldest, 10, totalMins: 30);   // oldest = 30m block

        var svc = NewService(db);
        var result = await svc.BackwardScheduleAsync(parent.Id, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Value!.ChildProductionOrderIds.Count);
        Assert.Equal(3, result.Value.OperationsStamped);

        // Refresh + assert wall-clock arithmetic:
        //   newest:  end=parentEnd            start=parentEnd-60m
        //   middle:  end=newest.start         start=newest.start-90m
        //   oldest:  end=middle.start         start=middle.start-30m
        var refreshedNewest = await db.ProductionOrders.AsNoTracking().FirstAsync(p => p.Id == newest.Id);
        var refreshedMiddle = await db.ProductionOrders.AsNoTracking().FirstAsync(p => p.Id == middle.Id);
        var refreshedOldest = await db.ProductionOrders.AsNoTracking().FirstAsync(p => p.Id == oldest.Id);

        Assert.Equal(parentEnd, refreshedNewest.ScheduledEnd);
        Assert.Equal(parentEnd.AddMinutes(-60), refreshedNewest.ScheduledStart);
        Assert.Equal(refreshedNewest.ScheduledStart, refreshedMiddle.ScheduledEnd);
        Assert.Equal(parentEnd.AddMinutes(-150), refreshedMiddle.ScheduledStart);
        Assert.Equal(refreshedMiddle.ScheduledStart, refreshedOldest.ScheduledEnd);
        Assert.Equal(parentEnd.AddMinutes(-180), refreshedOldest.ScheduledStart);

        // Processed order in the outcome: newest first, oldest last.
        Assert.Equal(newest.Id, result.Value.ChildProductionOrderIds[0]);
        Assert.Equal(middle.Id, result.Value.ChildProductionOrderIds[1]);
        Assert.Equal(oldest.Id, result.Value.ChildProductionOrderIds[2]);
    }

    [Fact]
    public async Task BackwardSchedule_Child_Without_Operations_Issues_Warning_And_Stamps_Zero_Duration()
    {
        await using var db = NewDb();
        var parentEnd = new DateTime(2026, 8, 14, 17, 0, 0, DateTimeKind.Utc);
        var parent = await SeedParentAsync(db, scheduledEnd: parentEnd);

        // One released child (with ops), one not-yet-released (no ops).
        // The not-yet-released child should be stamped as zero-duration
        // at the cursor's current position with a warning recorded.
        var released = await SeedChildAsync(db, parent, createdAt: DateTime.UtcNow);
        await SeedOpAsync(db, released, 10, totalMins: 45);

        var unreleased = await SeedChildAsync(db, parent, createdAt: DateTime.UtcNow.AddDays(-1));

        var svc = NewService(db);
        var result = await svc.BackwardScheduleAsync(parent.Id, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Single(result.Value!.Warnings);
        Assert.Contains("ProductionOperations", result.Value.Warnings[0]);
        Assert.Equal(1, result.Value.OperationsStamped);

        var refreshedReleased = await db.ProductionOrders.AsNoTracking().FirstAsync(p => p.Id == released.Id);
        var refreshedUnreleased = await db.ProductionOrders.AsNoTracking().FirstAsync(p => p.Id == unreleased.Id);

        Assert.Equal(parentEnd, refreshedReleased.ScheduledEnd);
        Assert.Equal(parentEnd.AddMinutes(-45), refreshedReleased.ScheduledStart);

        // Unreleased child stamped at the cursor (released.ScheduledStart)
        // as a zero-duration placeholder — ScheduledStart == ScheduledEnd.
        Assert.Equal(refreshedReleased.ScheduledStart, refreshedUnreleased.ScheduledEnd);
        Assert.Equal(refreshedUnreleased.ScheduledEnd, refreshedUnreleased.ScheduledStart);
    }
}
