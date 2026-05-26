// B6 Foundation Sprint PR-FS-1.5 (2026-05-26) — ItemGroupBackfillSeeder tests.
// HOTFIX PR-FS-1.5.1 (2026-05-26) — Source-aware + Reclassify mode tests.
//
// 8 tests:
//   1. Empty DB → returns zero counts, no warnings.
//   2. FillNullsOnly: all items already classified → 0 newly classified.
//   3. FillNullsOnly: mixed (some classified, some not) → classifies only the NULL ones,
//      with Source-aware dispatch (ExternalERP Part → RAW, Internal Part → SUBASSY).
//   4. FillNullsOnly is idempotent on second call.
//   5. Reclassify: items whose resolved group changed get updated; others left alone.
//   6. Reclassify: per-change audit row populated with from/to codes.
//   7. Reclassify is idempotent on second call (no changes detected).
//   8. Type with no convention mapping → routed via the catch-all (RAW).

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
    // SYSTEM ItemGroup Ids used across the assertions (mirrors PRA-7 + PR-FS-1.5.1).
    private const int RawId       = 1;
    private const int FgId        = 3;
    private const int ConsumeId   = 4;
    private const int ServiceId   = 5;
    private const int ToolingId   = 9;
    private const int SpareId     = 10;
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
            .UseInMemoryDatabase($"itemgroup-backfill-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static async Task SeedSystemItemGroupsAsync(AppDbContext db)
    {
        db.Set<ItemGroup>().AddRange(
            new ItemGroup { Id = RawId,     Code = "RAW",        Name = "Raw Material",   GroupType = ItemGroupType.RawMaterial,    IsSystem = true, IsActive = true },
            new ItemGroup { Id = FgId,      Code = "FG",         Name = "Finished Goods", GroupType = ItemGroupType.FinishedGoods,  IsSystem = true, IsActive = true },
            new ItemGroup { Id = ConsumeId, Code = "CONSUMABLE", Name = "Consumable",     GroupType = ItemGroupType.Consumable,     IsSystem = true, IsActive = true },
            new ItemGroup { Id = ServiceId, Code = "SERVICE",    Name = "Service",        GroupType = ItemGroupType.Service,        IsSystem = true, IsActive = true },
            new ItemGroup { Id = ToolingId, Code = "TOOLING",    Name = "Tooling",        GroupType = ItemGroupType.Tooling,        IsSystem = true, IsActive = true },
            new ItemGroup { Id = SpareId,   Code = "SPAREPART",  Name = "Spare Part",     GroupType = ItemGroupType.SparePart,      IsSystem = true, IsActive = true },
            new ItemGroup { Id = SubAssyId, Code = "SUBASSY",    Name = "Sub-Assembly",   GroupType = ItemGroupType.Subassembly,    IsSystem = true, IsActive = true }
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
        Assert.Equal(ItemGroupBackfillMode.FillNullsOnly, result.Mode);
    }

    [Fact]
    public async Task BackfillAsync_All_Already_Classified_Is_No_Op()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.AddRange(
            new Item { Id = 10, PartNumber = "A", Description = "A", StockUOM = "EA", Type = ItemType.Part, ItemGroupId = RawId, Source = ItemMasterSource.ExternalERP },
            new Item { Id = 11, PartNumber = "B", Description = "B", StockUOM = "EA", Type = ItemType.Tool, ItemGroupId = ToolingId });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(2, result.TotalItemsScanned);
        Assert.Equal(0, result.ItemsClassified);
        Assert.Equal(2, result.ItemsAlreadyClassified);
        Assert.Empty(result.PerItemGroupClassified);
    }

    [Fact]
    public async Task BackfillAsync_Mixed_Classifies_Only_The_Null_Ones_With_Source_Awareness()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        // 6 items: 2 already classified, 4 NULL with mixed Type / Source.
        db.Items.AddRange(
            new Item { Id = 1, PartNumber = "PRE-1", Description = "Pre-classified Internal Part as FG (operator-set)", StockUOM = "EA", Type = ItemType.Part, ItemGroupId = FgId, Source = ItemMasterSource.Internal },
            new Item { Id = 2, PartNumber = "PRE-2", Description = "Pre-classified Tool", StockUOM = "EA", Type = ItemType.Tool, ItemGroupId = ToolingId },
            new Item { Id = 3, PartNumber = "NEW-1", Description = "Internal Part → SUBASSY", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.Internal },
            new Item { Id = 4, PartNumber = "NEW-2", Description = "ExternalERP Part → RAW", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.ExternalERP },
            new Item { Id = 5, PartNumber = "NEW-3", Description = "Tool → TOOLING (Source irrelevant)", StockUOM = "EA", Type = ItemType.Tool, Source = ItemMasterSource.Internal },
            new Item { Id = 6, PartNumber = "NEW-4", Description = "Fastener → RAW (Source irrelevant)", StockUOM = "EA", Type = ItemType.Fastener, Source = ItemMasterSource.Internal });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(CancellationToken.None);

        Assert.Equal(6, result.TotalItemsScanned);
        Assert.Equal(4, result.ItemsClassified);
        Assert.Equal(2, result.ItemsAlreadyClassified);
        Assert.Equal(0, result.ItemsSkippedNoMapping);

        Assert.Equal(1, result.PerItemGroupClassified.GetValueOrDefault("SUBASSY"));
        Assert.Equal(2, result.PerItemGroupClassified.GetValueOrDefault("RAW"));     // ExternalERP Part + Fastener
        Assert.Equal(1, result.PerItemGroupClassified.GetValueOrDefault("TOOLING"));

        // Verify DB state.
        var fresh = await db.Items.Where(i => i.Id >= 3 && i.Id <= 6).OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(SubAssyId, fresh[0].ItemGroupId);
        Assert.Equal(RawId,     fresh[1].ItemGroupId);
        Assert.Equal(ToolingId, fresh[2].ItemGroupId);
        Assert.Equal(RawId,     fresh[3].ItemGroupId);
    }

    [Fact]
    public async Task BackfillAsync_FillNullsOnly_Is_Idempotent_On_Second_Call()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.AddRange(
            new Item { Id = 1, PartNumber = "A", Description = "A", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.ExternalERP },
            new Item { Id = 2, PartNumber = "B", Description = "B", StockUOM = "EA", Type = ItemType.Tool });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);

        var first = await seeder.BackfillAsync(CancellationToken.None);
        Assert.Equal(2, first.ItemsClassified);

        var second = await seeder.BackfillAsync(CancellationToken.None);
        Assert.Equal(0, second.ItemsClassified);
        Assert.Equal(2, second.ItemsAlreadyClassified);
    }

    [Fact]
    public async Task ReclassifyLegacyBugRows_Moves_Bug_Pattern_Items_Only()
    {
        // PR-FS-1.5.1 hotfix scenario — 3 Items currently classified to FG via the
        // PR-FS-1.5 bug. After flipping their Source to ExternalERP, the
        // ReclassifyLegacyBugRows mode should move them FG → RAW. A 4th Item that's
        // correctly classified must be left alone.
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.AddRange(
            new Item { Id = 1, PartNumber = "LEGACY-1", Description = "Legacy bug — Part+ExternalERP currently in FG", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.ExternalERP, ItemGroupId = FgId },
            new Item { Id = 2, PartNumber = "LEGACY-2", Description = "Legacy bug — Part+ExternalERP currently in FG", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.ExternalERP, ItemGroupId = FgId },
            new Item { Id = 3, PartNumber = "LEGACY-3", Description = "Legacy bug — Kit+ExternalERP currently in FG",  StockUOM = "EA", Type = ItemType.Kit,  Source = ItemMasterSource.ExternalERP, ItemGroupId = FgId },
            new Item { Id = 4, PartNumber = "CORRECT",  Description = "Correct — Tool in TOOLING",                     StockUOM = "EA", Type = ItemType.Tool, Source = ItemMasterSource.Internal,    ItemGroupId = ToolingId });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(ItemGroupBackfillMode.ReclassifyLegacyBugRows, CancellationToken.None);

        Assert.Equal(ItemGroupBackfillMode.ReclassifyLegacyBugRows, result.Mode);
        Assert.Equal(4, result.TotalItemsScanned);
        Assert.Equal(3, result.ItemsReclassified);
        Assert.Equal(1, result.ItemsUnchanged);
        Assert.Equal(0, result.ItemsSkippedNoMapping);

        // 3 RAW entries (Part+ExternalERP, Part+ExternalERP, Kit+ExternalERP).
        Assert.Equal(3, result.PerItemGroupClassified.GetValueOrDefault("RAW"));

        // Verify DB state — legacy 3 moved to RAW; correct one untouched.
        var fresh = await db.Items.OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(RawId,     fresh[0].ItemGroupId);
        Assert.Equal(RawId,     fresh[1].ItemGroupId);
        Assert.Equal(RawId,     fresh[2].ItemGroupId);
        Assert.Equal(ToolingId, fresh[3].ItemGroupId);
    }

    [Fact]
    public async Task ReclassifyLegacyBugRows_Preserves_Operator_Set_Classifications()
    {
        // Codex P1 #2 regression guard — the SCOPED Reclassify mode must NEVER
        // overwrite intentional operator-set classifications that fall outside
        // the bug fingerprint. The fingerprint is:
        //   Type IN (Part, Kit) AND Source IN (ExternalERP, Synced) AND ItemGroupId == FG
        // Any row that does NOT match (different Type, different Source, or
        // different ItemGroup) is left alone — including operator-set FG.
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.AddRange(
            // Operator intent #1 — Internal FG (truly sellable internal item).
            // Source=Internal → does NOT match the post-SourceBackfill fingerprint.
            new Item { Id = 1, PartNumber = "OP-INT-FG",    Description = "Operator-set: Internal sellable FG",       StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.Internal,    ItemGroupId = FgId },
            // Operator intent #2 — External Part explicitly set to SPAREPART (not FG).
            // Doesn't match because ItemGroupId != FG.
            new Item { Id = 2, PartNumber = "OP-EXT-SPARE", Description = "Operator-set: spare-part flag overridden", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.ExternalERP, ItemGroupId = SpareId },
            // Operator intent #3 — Tool explicitly set to FG (unusual but allowed).
            // Doesn't match because Type != Part|Kit.
            new Item { Id = 3, PartNumber = "OP-TOOL-FG",   Description = "Operator-set: Tool classified as FG",      StockUOM = "EA", Type = ItemType.Tool, Source = ItemMasterSource.ExternalERP, ItemGroupId = FgId },
            // Bug-fingerprint row — should be the only one touched.
            new Item { Id = 4, PartNumber = "BUG-MATCH",    Description = "Legacy bug fingerprint",                   StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.ExternalERP, ItemGroupId = FgId });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(ItemGroupBackfillMode.ReclassifyLegacyBugRows, CancellationToken.None);

        Assert.Equal(1, result.ItemsReclassified);
        Assert.Single(result.ReclassifyChanges);
        Assert.Equal("BUG-MATCH", result.ReclassifyChanges[0].PartNumber);

        // Verify operator intent preserved.
        var fresh = await db.Items.OrderBy(i => i.Id).ToListAsync();
        Assert.Equal(FgId,    fresh[0].ItemGroupId);   // Operator-set Internal FG preserved
        Assert.Equal(SpareId, fresh[1].ItemGroupId);   // Operator-set SPAREPART preserved
        Assert.Equal(FgId,    fresh[2].ItemGroupId);   // Operator-set Tool→FG preserved
        Assert.Equal(RawId,   fresh[3].ItemGroupId);   // Bug row moved FG → RAW
    }

    [Fact]
    public async Task Reclassify_Audit_Log_Captures_From_And_To_Codes()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.Add(new Item
        {
            Id = 1,
            PartNumber = "AUDIT-1",
            Description = "Bug-classified",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.ExternalERP,
            ItemGroupId = FgId,
        });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);
        var result = await seeder.BackfillAsync(ItemGroupBackfillMode.ReclassifyLegacyBugRows, CancellationToken.None);

        Assert.Single(result.ReclassifyChanges);
        var change = result.ReclassifyChanges[0];
        Assert.Equal(1, change.ItemId);
        Assert.Equal("AUDIT-1", change.PartNumber);
        Assert.Equal("FG", change.FromCode);
        Assert.Equal("RAW", change.ToCode);
    }

    [Fact]
    public async Task Reclassify_Is_Idempotent_On_Second_Call()
    {
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);

        db.Items.AddRange(
            new Item { Id = 1, PartNumber = "A", Description = "A", StockUOM = "EA", Type = ItemType.Part, Source = ItemMasterSource.ExternalERP, ItemGroupId = FgId },
            new Item { Id = 2, PartNumber = "B", Description = "B", StockUOM = "EA", Type = ItemType.Tool, ItemGroupId = ToolingId });
        await db.SaveChangesAsync();

        var seeder = NewSeeder(db);

        var first = await seeder.BackfillAsync(ItemGroupBackfillMode.ReclassifyLegacyBugRows, CancellationToken.None);
        Assert.Equal(1, first.ItemsReclassified);  // Just Item #1: FG → RAW

        var second = await seeder.BackfillAsync(ItemGroupBackfillMode.ReclassifyLegacyBugRows, CancellationToken.None);
        Assert.Equal(0, second.ItemsReclassified);
        Assert.Equal(2, second.ItemsUnchanged);
        Assert.Empty(second.ReclassifyChanges);
    }

    [Fact]
    public async Task Resolver_Default_Catchall_Routes_Unknown_Types_Through_RAW_Not_FG()
    {
        // PR-FS-1.5.1 sanity — the resolver's catch-all default (originally FG, the
        // bug) is now RAW. Use an enum value the convention table doesn't list to
        // exercise the catch-all path.
        //
        // ItemType currently has every enum value mapped, so this test is a
        // regression guard against re-introducing FG as a default if new
        // ItemTypes are added without updating the map.
        await using var db = NewDb();
        await SeedSystemItemGroupsAsync(db);
        var resolver = new ItemGroupResolver(db, NullLogger<ItemGroupResolver>.Instance);

        // Hit every defined ItemType / Source combo and assert no default is FG.
        foreach (ItemType t in Enum.GetValues(typeof(ItemType)))
        {
            foreach (ItemMasterSource s in Enum.GetValues(typeof(ItemMasterSource)))
            {
                var id = await resolver.ResolveDefaultForItemAsync(t, s, CancellationToken.None);
                Assert.True(id != FgId,
                    $"Resolver default for (Type={t}, Source={s}) = FG — FG must never be a default.");
            }
        }
    }
}
