// B6 Foundation Sprint PR-FS-7 (2026-05-26) — ItemMasterReader + 18-column
// expansion semantics tests.
//
// Realistic precision-machining fixtures per HARD LOCK feedback_no_fake_data:
//   - 9245 BRG-6207-2RS — purchased bearing: PlanningPolicy.PurchaseToStock,
//     MakeBuyCode.Buy, LotSizingRule.EOQ, IsSellable=false, EAR99=true, ECCN=EAR99.
//   - 9500 ASM-TRENT-BRACKET-A — internally manufactured FG: PlanningPolicy.MakeToOrder,
//     MakeBuyCode.Make, IsSellable=true, AS9100Critical=true, KeyCharacteristic=true,
//     RequiresFai=true, ECCN=9A610.a (aerospace), LifecycleStage.Production.
//   - 9270 BAR-1018-1.5X12 — steel raw material: PlanningPolicy.PurchaseToStock,
//     ItemFamily="Bar Stock", FrozenStandardCost=$60.00 (Q1 fiscal close).

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Items;

public class ItemMasterReaderTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
            mb.Entity<CostLayer>().Ignore(c => c.RowVersion);
            mb.Entity<ItemSourcingRule>().Ignore(r => r.RowVersion);
            mb.Entity<CustomerItemXref>().Ignore(x => x.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"itm-master-reader-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static ItemMasterReader NewReader(AppDbContext db) =>
        new(db, NullLogger<ItemMasterReader>.Instance);

    [Fact]
    public async Task GetExpansionFields_Returns_PRFS7_Columns_For_Purchased_Bearing()
    {
        // BRG-6207-2RS: realistic purchased-bearing expansion field set.
        await using var db = NewDb();
        db.Items.Add(new Item
        {
            Id = 9245,
            PartNumber = "BRG-6207-2RS",
            Description = "Ball Bearing 35x72x17mm Sealed",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.ExternalERP,
            IsActive = true,
            PlanningPolicy = PlanningPolicy.PurchaseToStock,
            MakeBuyCode = MakeBuyCode.Buy,
            LotSizingRule = LotSizingRule.EOQ,
            MrpPlannerCode = "MRP-INDIRECT-01",
            IsSellable = false,
            ECCN = "EAR99",
            EAR99 = true,
            ItemFamily = "Bearings",
            LifecycleStage = LifecycleStage.Production,
        });
        await db.SaveChangesAsync();

        var reader = NewReader(db);
        var hit = await reader.GetExpansionFieldsAsync(9245, CancellationToken.None);

        Assert.NotNull(hit);
        Assert.Equal("BRG-6207-2RS", hit!.PartNumber);
        Assert.Equal(PlanningPolicy.PurchaseToStock, hit.PlanningPolicy);
        Assert.Equal(MakeBuyCode.Buy, hit.MakeBuyCode);
        Assert.Equal(LotSizingRule.EOQ, hit.LotSizingRule);
        Assert.Equal("MRP-INDIRECT-01", hit.MrpPlannerCode);
        Assert.False(hit.IsSellable);
        Assert.Equal("EAR99", hit.ECCN);
        Assert.True(hit.EAR99);
        Assert.Equal("BEARINGS", hit.ItemFamily); // codebase uppercases ItemFamily on save
        Assert.Equal(LifecycleStage.Production, hit.LifecycleStage);
    }

    [Fact]
    public async Task GetExpansionFields_Returns_Aerospace_FG_With_AS9100_Flags()
    {
        // ASM-TRENT-BRACKET-A: realistic AS9100 internally-made sellable FG.
        await using var db = NewDb();
        db.Items.Add(new Item
        {
            Id = 9500,
            PartNumber = "ASM-TRENT-BRACKET-A",
            Description = "Precision-machined engine bracket assembly — sellable FG",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.Internal,
            IsActive = true,
            PlanningPolicy = PlanningPolicy.MakeToOrder,
            MakeBuyCode = MakeBuyCode.Make,
            LotSizingRule = LotSizingRule.LotForLot,
            IsSellable = true,
            AS9100Critical = true,
            KeyCharacteristic = true,
            RequiresFai = true,
            InspectionPlanId = 1001,
            ECCN = "9A610.a",
            ScheduleB = "8803.30.0030",
            IntrastatCode = "88033000",
            EAR99 = false,
            ItemFamily = "AEROSPACE BRACKETS",
            LifecycleStage = LifecycleStage.Production,
        });
        await db.SaveChangesAsync();

        var reader = NewReader(db);
        var hit = await reader.GetExpansionFieldsAsync(9500, CancellationToken.None);

        Assert.NotNull(hit);
        Assert.True(hit!.IsSellable);
        Assert.True(hit.AS9100Critical);
        Assert.True(hit.KeyCharacteristic);
        Assert.True(hit.RequiresFai);
        Assert.Equal(1001, hit.InspectionPlanId);
        Assert.Equal(PlanningPolicy.MakeToOrder, hit.PlanningPolicy);
        Assert.Equal(MakeBuyCode.Make, hit.MakeBuyCode);
        Assert.Equal("9A610.A", hit.ECCN); // codebase uppercases ECCN on save
        Assert.Equal("8803.30.0030", hit.ScheduleB);
        Assert.False(hit.EAR99);
    }

    [Fact]
    public async Task GetExpansionFields_Returns_Frozen_Standard_Cost_For_Raw_Steel()
    {
        // BAR-1018: frozen $60.00 at end of Q1 2026 for fiscal-close audit window.
        await using var db = NewDb();
        var freezeDate = new DateTime(2026, 3, 31, 23, 59, 0, DateTimeKind.Utc);
        db.Items.Add(new Item
        {
            Id = 9270,
            PartNumber = "BAR-1018-1.5X12",
            Description = "Cold-rolled Steel Bar 1018, 1.5\" dia x 12 ft",
            StockUOM = "FT",
            Type = ItemType.Part,
            Source = ItemMasterSource.ExternalERP,
            IsActive = true,
            PlanningPolicy = PlanningPolicy.PurchaseToStock,
            MakeBuyCode = MakeBuyCode.Buy,
            LotSizingRule = LotSizingRule.MinOrderQty,
            FrozenStandardCost = 60.00m,
            FrozenStandardCostEffectiveAtUtc = freezeDate,
            ItemFamily = "BAR STOCK",
            LifecycleStage = LifecycleStage.Production,
        });
        await db.SaveChangesAsync();

        var reader = NewReader(db);
        var hit = await reader.GetExpansionFieldsAsync(9270, CancellationToken.None);

        Assert.NotNull(hit);
        Assert.Equal(60.00m, hit!.FrozenStandardCost);
        Assert.Equal(freezeDate, hit.FrozenStandardCostEffectiveAtUtc);
        Assert.Equal("BAR STOCK", hit.ItemFamily); // codebase uppercases ItemFamily on save
        Assert.Equal(LotSizingRule.MinOrderQty, hit.LotSizingRule);
    }

    [Fact]
    public async Task GetExpansionFields_Defaults_Are_Backward_Compatible()
    {
        // An Item created with NO PR-FS-7 fields set should still resolve, with
        // the documented defaults (MakeToStock / Buy / LotForLot / Production
        // lifecycle / all flags false / all strings null).
        await using var db = NewDb();
        db.Items.Add(new Item
        {
            Id = 9999,
            PartNumber = "LEGACY-PRE-FS7-ITEM",
            Description = "Item created before PR-FS-7 — should accept defaults",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.ExternalERP,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var reader = NewReader(db);
        var hit = await reader.GetExpansionFieldsAsync(9999, CancellationToken.None);

        Assert.NotNull(hit);
        Assert.Equal(PlanningPolicy.MakeToStock, hit!.PlanningPolicy);
        Assert.Equal(MakeBuyCode.Buy, hit.MakeBuyCode);
        Assert.Equal(LotSizingRule.LotForLot, hit.LotSizingRule);
        Assert.False(hit.IsSellable);
        Assert.False(hit.IsPhantom);
        Assert.False(hit.AS9100Critical);
        Assert.False(hit.EAR99);
        Assert.Null(hit.MrpPlannerCode);
        Assert.Null(hit.ECCN);
        Assert.Null(hit.FrozenStandardCost);
        Assert.Equal(LifecycleStage.Production, hit.LifecycleStage);
    }

    [Fact]
    public async Task GetExpansionFields_Returns_Null_For_Unknown_Item()
    {
        await using var db = NewDb();
        var reader = NewReader(db);

        var hit = await reader.GetExpansionFieldsAsync(99999, CancellationToken.None);
        Assert.Null(hit);
    }

    [Fact]
    public async Task LifecycleStage_Transitions_Persist()
    {
        // Item moves through Prototype → Sample → Released → Production over the
        // course of an engineering cycle. Reader returns whatever the current
        // stage is.
        await using var db = NewDb();
        var item = new Item
        {
            Id = 9500,
            PartNumber = "ASM-TRENT-BRACKET-A",
            Description = "Precision-machined bracket — engineering progression",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.Internal,
            IsActive = true,
            LifecycleStage = LifecycleStage.Prototype,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var reader = NewReader(db);
        var p = await reader.GetExpansionFieldsAsync(9500, CancellationToken.None);
        Assert.Equal(LifecycleStage.Prototype, p!.LifecycleStage);

        item.LifecycleStage = LifecycleStage.Sample;
        await db.SaveChangesAsync();
        var s = await reader.GetExpansionFieldsAsync(9500, CancellationToken.None);
        Assert.Equal(LifecycleStage.Sample, s!.LifecycleStage);

        item.LifecycleStage = LifecycleStage.Released;
        await db.SaveChangesAsync();
        var r = await reader.GetExpansionFieldsAsync(9500, CancellationToken.None);
        Assert.Equal(LifecycleStage.Released, r!.LifecycleStage);

        item.LifecycleStage = LifecycleStage.Production;
        await db.SaveChangesAsync();
        var prod = await reader.GetExpansionFieldsAsync(9500, CancellationToken.None);
        Assert.Equal(LifecycleStage.Production, prod!.LifecycleStage);
    }
}
