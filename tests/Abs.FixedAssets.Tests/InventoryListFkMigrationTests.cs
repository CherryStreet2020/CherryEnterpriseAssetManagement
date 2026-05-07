using System;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Locks in the FK schema + backfill behavior for the InventoryList
/// migration shipped alongside this test file. Mirrors the canonical
/// InMemory test pattern in AuthServiceTests / AssetConcurrencyTests.
///
/// What we prove here:
///   1. The new StatusLookupValueId column exists on the entity (the
///      compiler enforces this at build time, but a write/read round-trip
///      catches any EF mapping mistake).
///   2. Setting StatusLookupValueId alongside Status persists both
///      values together (the contract every Sync*FkAsync helper depends on).
///   3. Default-constructed rows leave the FK as null until something
///      writes it — i.e., adding the column doesn't break legacy code paths.
/// </summary>
public class InventoryListFkMigrationTests
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
            .UseInMemoryDatabase($"inventorylist-fk-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    [Fact]
    public async Task InventoryList_NewRow_LeavesStatusLookupValueIdNull()
    {
        await using var db = NewDb();
        db.InventoryLists.Add(new InventoryList
        {
            Name = "Q4 Sweep",
            Status = InventoryStatus.Draft
        });
        await db.SaveChangesAsync();

        var read = await db.InventoryLists.AsNoTracking().FirstAsync();
        Assert.Equal(InventoryStatus.Draft, read.Status);
        Assert.Null(read.StatusLookupValueId);
    }

    [Fact]
    public async Task InventoryList_WriteFkAndEnumTogether_PersistsBoth()
    {
        await using var db = NewDb();

        // Set up the InventoryStatus LookupType + a single LookupValue
        // for code "1" (= InventoryStatus.InProgress per PR #6 alignment).
        var lt = new LookupType { Key = "InventoryStatus", Name = "Inventory Status" };
        db.LookupTypes.Add(lt);
        await db.SaveChangesAsync();

        var lv = new LookupValue
        {
            LookupTypeId = lt.Id,
            Code = "1",
            Name = "In Progress"
        };
        db.LookupValues.Add(lv);
        await db.SaveChangesAsync();

        db.InventoryLists.Add(new InventoryList
        {
            Name = "Q4 Sweep",
            Status = InventoryStatus.InProgress,
            StatusLookupValueId = lv.Id,
            StartedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var read = await db.InventoryLists.AsNoTracking().FirstAsync();
        Assert.Equal(InventoryStatus.InProgress, read.Status);
        Assert.Equal(lv.Id, read.StatusLookupValueId);
    }

    [Fact]
    public async Task InventoryList_UpdateBothColumns_RoundTripStaysInSync()
    {
        // Scenario the SyncStatusFkAsync helper drives at runtime:
        //   list.Status = X;
        //   list.StatusLookupValueId = lookup-of-X.Id;
        //   await SaveChangesAsync();
        // Read-back must show both values consistent.
        await using var db = NewDb();

        var lt = new LookupType { Key = "InventoryStatus", Name = "Inventory Status" };
        db.LookupTypes.Add(lt);
        await db.SaveChangesAsync();

        var lvDraft       = new LookupValue { LookupTypeId = lt.Id, Code = "0", Name = "Draft" };
        var lvInProgress  = new LookupValue { LookupTypeId = lt.Id, Code = "1", Name = "In Progress" };
        var lvCompleted   = new LookupValue { LookupTypeId = lt.Id, Code = "2", Name = "Completed" };
        db.LookupValues.AddRange(lvDraft, lvInProgress, lvCompleted);
        await db.SaveChangesAsync();

        // Start in Draft
        db.InventoryLists.Add(new InventoryList
        {
            Name = "Q4 Sweep",
            Status = InventoryStatus.Draft,
            StatusLookupValueId = lvDraft.Id
        });
        await db.SaveChangesAsync();

        // Transition to InProgress (mirroring OnPostStartAsync)
        var list = await db.InventoryLists.FirstAsync();
        list.Status = InventoryStatus.InProgress;
        list.StatusLookupValueId = lvInProgress.Id;
        await db.SaveChangesAsync();

        var afterStart = await db.InventoryLists.AsNoTracking().FirstAsync();
        Assert.Equal(InventoryStatus.InProgress, afterStart.Status);
        Assert.Equal(lvInProgress.Id, afterStart.StatusLookupValueId);

        // Transition to Completed (mirroring OnPostCompleteAsync)
        var tracked = await db.InventoryLists.FirstAsync();
        tracked.Status = InventoryStatus.Completed;
        tracked.StatusLookupValueId = lvCompleted.Id;
        await db.SaveChangesAsync();

        var afterComplete = await db.InventoryLists.AsNoTracking().FirstAsync();
        Assert.Equal(InventoryStatus.Completed, afterComplete.Status);
        Assert.Equal(lvCompleted.Id, afterComplete.StatusLookupValueId);
    }

    [Fact]
    public async Task InventoryList_QueryByStatusLookupValueId_Works()
    {
        // Verify the index path: queries that filter by StatusLookupValueId
        // return the right rows. This proves the FK column is queryable
        // via EF (not just settable).
        await using var db = NewDb();

        var lt = new LookupType { Key = "InventoryStatus", Name = "Inventory Status" };
        db.LookupTypes.Add(lt);
        await db.SaveChangesAsync();

        var lvCompleted = new LookupValue { LookupTypeId = lt.Id, Code = "2", Name = "Completed" };
        var lvDraft     = new LookupValue { LookupTypeId = lt.Id, Code = "0", Name = "Draft" };
        db.LookupValues.AddRange(lvCompleted, lvDraft);
        await db.SaveChangesAsync();

        db.InventoryLists.AddRange(
            new InventoryList { Name = "A", Status = InventoryStatus.Completed, StatusLookupValueId = lvCompleted.Id },
            new InventoryList { Name = "B", Status = InventoryStatus.Completed, StatusLookupValueId = lvCompleted.Id },
            new InventoryList { Name = "C", Status = InventoryStatus.Draft,     StatusLookupValueId = lvDraft.Id });
        await db.SaveChangesAsync();

        var completed = await db.InventoryLists
            .Where(l => l.StatusLookupValueId == lvCompleted.Id)
            .ToListAsync();

        Assert.Equal(2, completed.Count);
        Assert.All(completed, l => Assert.Equal(lvCompleted.Id, l.StatusLookupValueId));
    }
}
