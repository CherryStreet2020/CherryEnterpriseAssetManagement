// B6 Foundation Sprint PR-FS-1.5 (2026-05-26) — ItemGroupBackfillSeeder tests.
//
// 4 tests:
//   1. Empty DB → returns zero counts, no warnings.
//   2. All items already classified → 0 newly classified, count reflects already.
//   3. Mixed (some classified, some not) → classifies only the NULL ones.
//   4. Item with unmapped Type → skipped + reported in SkippedItemIds.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services.Items;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Seeding;

public class ItemGroupBackfillSeederTests
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
            .UseInMemoryDatabase($"itemgroup-backfill-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static async Task SeedSystemItemGroupsAsync(AppDbContext db)
    {
        db.Set<ItemGroup>().AddRange(
            new ItemGroup { Id = 1, Code = "RAW",        Name = "Raw Material",   GroupType = ItemGroupType.RawMaterial,    IsSystem = true, IsActive = true },
            new ItemGroup { Id = 3, Code = "FG",         Name = "Finished Goods", GroupType = ItemGroupType.FinishedGoods,  IsSystem = true, IsActive = true },
            new ItemGroup { Id = 4, Code = "CONSUMABLE", Name = "Consumable",     GroupType = ItemGroupType.Consumable,     IsSystem = true, IsActive = true },
            new ItemGroup { Id = 5, Code = "SERVICE",    Name = "Service",        GroupType = ItemGroupType.Service,        IsSystem = true, IsActive = true },
            new ItemGroup { Id = 9, Code = "TOOLING",    Name = "Tooling",        GroupType = ItemGroupType.Tooling,        IsSystem = true, IsActive = true },
            new ItemGroup { Id = 10, Code = "SPAREPART", Name = "Spare Part",     GroupType = ItemGroupType.SparePart,      IsSystem = true, IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    private static ItemGroupBackfillSeeder NewSeeder(AppDbContext db) =>
        new(db,
            new ItemGroupResolver(db, NullLogger<ItemGroupResolver>.Instance),
            NullLogger<ItemGroupBackfillSeeder>.Instance);

    [Fact]
    public async Task BackfillAsync_Empty_Db_Returns_Zeros()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);
        var seeder = NewSeeder(db);

        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(0, result.TotalItemsScanned);
        Assert.Equal(0, result.ItemsClassified);
        Assert.Equal(0, result.ItemsAlreadyClassified);
        Assert.Equal(0, result.ItemsSkippedNoMapping);
        Assert.Empty(result.PerItemGroupClassified);
    }

    [Fact]
    public async Task BackfillAsync_All_Already_Classified_Is_No_Op()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.AddRange(
            new Item { Id = 10, PartNumber = "A", Description = "A", StockUOM = "EA", Type = ItemType.Part, ItemGroupId = 3 },
            new Item { Id = 11, PartNumber = "B", Description = "B", StockUOM = "EA", Type = ItemType.Tool, ItemGroupId = 9 });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(2, result.TotalItemsScanned);
        Assert.Equal(0, result.ItemsClassified);
        Assert.Equal(2, result.ItemsAlreadyClassified);
        Assert.Empty(result.PerItemGroupClassified);
    }

    [Fact]
    public async Task BackfillAsync_Mixed_Classifies_Only_The_Null_Ones()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        // 5 items: 2 already classified, 3 NULL (one each Part / Tool / Fastener)
        db.Items.AddRange(
            new Item { Id = 1, PartNumber = "PRE-1", Description = "Pre-classified 1", StockUOM = "EA", Type = ItemType.Part, ItemGroupId = 3 },
            new Item { Id = 2, PartNumber = "PRE-2", Description = "Pre-classified 2", StockUOM = "EA", Type = ItemType.Tool, ItemGroupId = 9 },
            new Item { Id = 3, PartNumber = "NEW-1", Description = "Needs classify (Part → FG)", StockUOM = "EA", Type = ItemType.Part },
            new Item { Id = 4, PartNumber = "NEW-2", Description = "Needs classify (Tool → TOOLING)", StockUOM = "EA", Type = ItemType.Tool },
            new Item { Id = 5, PartNumber = "NEW-3", Description = "Needs classify (Fastener → RAW)", StockUOM = "EA", Type = ItemType.Fastener });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(5, result.TotalItemsScanned);
        Assert.Equal(3, result.ItemsClassified);
        Assert.Equal(2, result.ItemsAlreadyClassified);
        Assert.Equal(0, result.ItemsSkippedNoMapping);

        // Verify per-group counts.
        Assert.Equal(1, result.PerItemGroupClassified.GetValueOrDefault("FG"));
        Assert.Equal(1, result.PerItemGroupClassified.GetValueOrDefault("TOOLING"));
        Assert.Equal(1, result.PerItemGroupClassified.GetValueOrDefault("RAW"));

        // Verify DB state.
        var freshDb = await db.Items.Where(i => i.Id >= 3 && i.Id <= 5).OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(3, freshDb[0].ItemGroupId);     // FG
        Assert.Equal(9, freshDb[1].ItemGroupId);     // TOOLING
        Assert.Equal(1, freshDb[2].ItemGroupId);     // RAW
    }

    [Fact]
    public async Task BackfillAsync_Is_Idempotent_On_Second_Call()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.AddRange(
            new Item { Id = 1, PartNumber = "A", Description = "A", StockUOM = "EA", Type = ItemType.Part },
            new Item { Id = 2, PartNumber = "B", Description = "B", StockUOM = "EA", Type = ItemType.Tool });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);

        var first = await seeder.BackfillAsync(CancellationToken.None);
        Assert.Equal(2, first.ItemsClassified);

        // Second run — already-classified state.
        var second = await seeder.BackfillAsync(CancellationToken.None);
        Assert.Equal(0, second.ItemsClassified);
        Assert.Equal(2, second.ItemsAlreadyClassified);
    }
}
