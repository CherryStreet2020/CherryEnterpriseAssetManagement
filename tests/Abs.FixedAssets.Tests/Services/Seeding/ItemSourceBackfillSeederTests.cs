// B6 Foundation Sprint PR-FS-1.5.1 (2026-05-26) — ItemSourceBackfillSeeder tests.
//
// 5 tests:
//   1. Empty DB → zero counts, no changes.
//   2. Items not matching the fingerprint (e.g. Internal + RAW, or ExternalERP + FG)
//      → left alone.
//   3. Items matching the fingerprint (Internal + FG) → flipped to ExternalERP.
//   4. Idempotent: re-running on already-flipped DB matches zero rows.
//   5. Missing system 'FG' group → graceful zero-op with warning.

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

public class ItemSourceBackfillSeederTests
{
    private const int RawId       = 1;
    private const int FgId        = 3;
    private const int ConsumeId   = 4;
    private const int ToolingId   = 9;
    private const int SubAssyId   = 11;

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
            .UseInMemoryDatabase($"item-source-backfill-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static async Task SeedSystemItemGroupsAsync(AppDbContext db, bool includeFg = true)
    {
        if (includeFg)
        {
            db.Set<ItemGroup>().Add(
                new ItemGroup { Id = FgId, Code = "FG", Name = "Finished Goods", GroupType = ItemGroupType.FinishedGoods, IsSystem = true, IsActive = true });
        }
        db.Set<ItemGroup>().AddRange(
            new ItemGroup { Id = RawId,     Code = "RAW",        Name = "Raw Material", GroupType = ItemGroupType.RawMaterial, IsSystem = true, IsActive = true },
            new ItemGroup { Id = ConsumeId, Code = "CONSUMABLE", Name = "Consumable",   GroupType = ItemGroupType.Consumable,  IsSystem = true, IsActive = true },
            new ItemGroup { Id = ToolingId, Code = "TOOLING",    Name = "Tooling",      GroupType = ItemGroupType.Tooling,     IsSystem = true, IsActive = true },
            new ItemGroup { Id = SubAssyId, Code = "SUBASSY",    Name = "Sub-Assembly", GroupType = ItemGroupType.Subassembly, IsSystem = true, IsActive = true });
        await db.SaveChangesAsync();
    }

    private static ItemSourceBackfillSeeder NewSeeder(AppDbContext db) =>
        new(db,
            new ItemGroupResolver(db, NullLogger<ItemGroupResolver>.Instance),
            NullLogger<ItemSourceBackfillSeeder>.Instance);

    [Fact]
    public async Task Backfill_Empty_Db_Returns_Zeros()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);
        var seeder = NewSeeder(db);

        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(0, result.TotalItemsScanned);
        Assert.Equal(0, result.ItemsFlipped);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public async Task Backfill_Leaves_Non_Matching_Rows_Alone()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        // Three non-matches: Internal+RAW (not FG), Internal+SUBASSY (not FG),
        // ExternalERP+FG (Source already external, no flip needed).
        db.Items.AddRange(
            new Item { Id = 1, PartNumber = "INTERNAL-RAW", Description = "Internal but RAW", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.Internal, ItemGroupId = RawId },
            new Item { Id = 2, PartNumber = "INTERNAL-SUB", Description = "Internal but SUBASSY", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.Internal, ItemGroupId = SubAssyId },
            new Item { Id = 3, PartNumber = "EXTERNAL-FG",  Description = "FG but already external", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.ExternalERP, ItemGroupId = FgId });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(3, result.TotalItemsScanned);
        Assert.Equal(0, result.ItemsFlipped);
        Assert.Empty(result.Changes);

        // Sources unchanged.
        var fresh = await db.Items.OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(ItemMasterSource.Internal,    fresh[0].Source);
        Assert.Equal(ItemMasterSource.Internal,    fresh[1].Source);
        Assert.Equal(ItemMasterSource.ExternalERP, fresh[2].Source);
    }

    [Fact]
    public async Task Backfill_Flips_Matching_Rows_To_ExternalERP()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        // 3 matches + 1 non-match (so the non-match is verifiable as untouched).
        db.Items.AddRange(
            new Item { Id = 1, PartNumber = "BUG-1",   Description = "Bug fingerprint", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.Internal, ItemGroupId = FgId },
            new Item { Id = 2, PartNumber = "BUG-2",   Description = "Bug fingerprint", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.Internal, ItemGroupId = FgId },
            new Item { Id = 3, PartNumber = "BUG-3",   Description = "Bug fingerprint", StockUOM = "EA", Type = ItemType.Kit,  Source = ItemMasterSource.Internal, ItemGroupId = FgId },
            new Item { Id = 4, PartNumber = "CORRECT", Description = "Not a bug",       StockUOM = "EA", Type = ItemType.Tool, Source = ItemMasterSource.Internal, ItemGroupId = ToolingId });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(4, result.TotalItemsScanned);
        Assert.Equal(3, result.ItemsFlipped);
        Assert.Equal(3, result.Changes.Count);

        // The 3 bug rows are now ExternalERP; the 4th (Tool) is still Internal.
        var fresh = await db.Items.OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(ItemMasterSource.ExternalERP, fresh[0].Source);
        Assert.Equal(ItemMasterSource.ExternalERP, fresh[1].Source);
        Assert.Equal(ItemMasterSource.ExternalERP, fresh[2].Source);
        Assert.Equal(ItemMasterSource.Internal,    fresh[3].Source);

        // Change-log fields populated.
        var first = result.Changes[0];
        Assert.Equal("Internal", first.FromSource);
        Assert.Equal("ExternalERP", first.ToSource);
    }

    [Fact]
    public async Task Backfill_Is_Idempotent()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.Add(new Item
        {
            Id = 1,
            PartNumber = "BUG-1",
            Description = "Bug fingerprint",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.Internal,
            ItemGroupId = FgId,
        });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);

        var first = await seeder.BackfillAsync(CancellationToken.None);
        Assert.Equal(1, first.ItemsFlipped);

        // Second run — already-flipped, fingerprint no longer matches.
        var second = await seeder.BackfillAsync(CancellationToken.None);
        Assert.Equal(0, second.ItemsFlipped);
        Assert.Empty(second.Changes);
    }

    [Fact]
    public async Task Backfill_Without_System_FG_Returns_Zero_Op_With_Warning()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db, includeFg: false);

        // Even though there's a candidate row, the resolver can't locate FG so we bail.
        db.Items.Add(new Item
        {
            Id = 1,
            PartNumber = "ORPHAN",
            Description = "FG not seeded so resolver bails",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.Internal,
            ItemGroupId = 9999,  // bogus
        });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(0, result.ItemsFlipped);
        Assert.Empty(result.Changes);
        Assert.Single(result.Warnings);
        Assert.Contains("FG", result.Warnings[0], StringComparison.OrdinalIgnoreCase);
    }
}
