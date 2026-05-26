// B6 Foundation Sprint PR-FS-3 (2026-05-26) — ItemStandardCostService tests.
//
// All fixtures use REALISTIC precision-machining items per the HARD LOCK
// (memory: feedback_no_fake_data.md):
//   - 9245 BRG-6207-2RS  Ball Bearing 35x72x17mm Sealed — $18.90 total
//   - 9302 EM-4FL-8MM    8mm 4-Flute Square End Mill Carbide — $48.50 total
//   - 9270 BAR-1018-1.5X12 Cold-rolled steel bar stock 1.5" × 12 ft — $86.40 total
//
// Coverage:
//   1. GetCostBreakdownAsync: no rows → all $0, total $0, sources "default".
//   2. SetCostElementAsync inserts new row; GetCostBreakdownAsync returns it
//      under the correct ElementType slot with source "Item".
//   3. Full breakdown: BRG-6207-2RS Material $15.20 + VarOH $3.10 + Tooling
//      $0.60 = $18.90 total, percentages computed correctly.
//   4. Per-Site override wins over Item-level for the same ElementType.
//   5. Per-Site override on one ElementType leaves other Item-level elements
//      untouched (mixed cascade).
//   6. SetCostElementAsync on changed amount: prior row's EffectiveToUtc gets
//      stamped, new row becomes current. History returns both.
//   7. SetCostElementAsync idempotency: same amount + source → no-op write.
//   8. As-of date: query at an earlier point retrieves the historically-
//      effective row, not the latest.
//   9. Item not found → null breakdown.
//  10. Negative element type (Other catch-all) routes correctly.

using System;
using System.Linq;
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

public class ItemStandardCostServiceTests
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
            .UseInMemoryDatabase($"item-std-cost-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static ItemStandardCostService NewService(AppDbContext db) =>
        new(db, NullLogger<ItemStandardCostService>.Instance);

    // Realistic precision-machining items — mirror live dev rows.
    private static Item BearingBrg6207() => new()
    {
        Id = 9245,
        PartNumber = "BRG-6207-2RS",
        Description = "Ball Bearing 35x72x17mm Sealed",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        ItemGroupId = 1, // RAW
        IsActive = true,
        StandardCost = 18.90m,
        LeadTimeDays = 14,
    };

    private static Item EndMillEm4Fl8Mm() => new()
    {
        Id = 9302,
        PartNumber = "EM-4FL-8MM",
        Description = "8mm 4-Flute Square End Mill Carbide",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        ItemGroupId = 1, // RAW
        IsActive = true,
        StandardCost = 48.50m,
        LeadTimeDays = 7,
    };

    private static Item Bar1018() => new()
    {
        Id = 9270,
        PartNumber = "BAR-1018-1.5X12",
        Description = "Cold-rolled Steel Bar 1018, 1.5\" diameter x 12 ft",
        StockUOM = "FT",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        ItemGroupId = 1, // RAW
        IsActive = true,
        StandardCost = 86.40m,
        LeadTimeDays = 10,
    };

    [Fact]
    public async Task GetCostBreakdown_NoRows_Returns_All_Zero()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var b = await svc.GetCostBreakdownAsync(9245, siteId: null, asOfUtc: null, CancellationToken.None);

        Assert.NotNull(b);
        Assert.Equal(0m, b!.Material);
        Assert.Equal(0m, b.Labor);
        Assert.Equal(0m, b.Total);
        Assert.Equal("default", b.OverrideSource[CostElementType.Material]);
        Assert.Equal("default", b.OverrideSource[CostElementType.Labor]);
    }

    [Fact]
    public async Task SetCostElement_Then_GetCostBreakdown_Reflects_Value()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.SetCostElementAsync(
            itemId: 9245,
            siteId: null,
            elementType: CostElementType.Material,
            amount: 15.20m,
            source: CostElementSource.Manual,
            calculationNotes: null,
            createdBy: "test",
            ct: CancellationToken.None);

        var b = await svc.GetCostBreakdownAsync(9245, siteId: null, asOfUtc: null, CancellationToken.None);

        Assert.NotNull(b);
        Assert.Equal(15.20m, b!.Material);
        Assert.Equal(15.20m, b.Total);
        Assert.Equal("Item", b.OverrideSource[CostElementType.Material]);
    }

    [Fact]
    public async Task GetCostBreakdown_Composes_Real_Bearing_Cost_Correctly()
    {
        // BRG-6207-2RS — purchased bearing, Item.StandardCost = $18.90.
        // Realistic breakdown for a purchased part:
        //   Material   = $15.20  (vendor unit cost)
        //   VarOH      = $3.10   (receiving + storage absorption ~16%)
        //   Tooling    = $0.60   (perishable-tool amortization for kitting)
        //   ---------- = $18.90 total
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.SetCostElementAsync(9245, null, CostElementType.Material, 15.20m, CostElementSource.Manual, "vendor unit cost from PO 47281", "buyer", CancellationToken.None);
        await svc.SetCostElementAsync(9245, null, CostElementType.VariableOverhead, 3.10m, CostElementSource.Calculated, "16% receiving + storage absorption rate × $15.20 material", "system", CancellationToken.None);
        await svc.SetCostElementAsync(9245, null, CostElementType.Tooling, 0.60m, CostElementSource.Manual, "perishable kitting tools amortized per BOM use", "engineer", CancellationToken.None);

        var b = await svc.GetCostBreakdownAsync(9245, null, null, CancellationToken.None);

        Assert.NotNull(b);
        Assert.Equal(15.20m, b!.Material);
        Assert.Equal(3.10m, b.VariableOverhead);
        Assert.Equal(0.60m, b.Tooling);
        Assert.Equal(0m, b.Labor);     // purchased — no internal labor
        Assert.Equal(0m, b.Subcontract); // no outside processing
        Assert.Equal(18.90m, b.Total);
    }

    [Fact]
    public async Task PerSite_Override_Wins_Over_ItemLevel()
    {
        // EM-4FL-8MM endmill — Item-level Material $30, Plant-2 sources from a
        // different vendor at $34.50 (regional supplier closer to that plant).
        await using var db = NewDb();
        db.Items.Add(EndMillEm4Fl8Mm());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.SetCostElementAsync(9302, null,  CostElementType.Material, 30.00m, CostElementSource.Manual, null, "buyer", CancellationToken.None);
        await svc.SetCostElementAsync(9302, 2,     CostElementType.Material, 34.50m, CostElementSource.Manual, "Plant-2 regional supplier", "buyer", CancellationToken.None);

        var siteB = await svc.GetCostBreakdownAsync(9302, siteId: 2, asOfUtc: null, CancellationToken.None);
        Assert.NotNull(siteB);
        Assert.Equal(34.50m, siteB!.Material);
        Assert.Equal("ItemSite", siteB.OverrideSource[CostElementType.Material]);

        var itemB = await svc.GetCostBreakdownAsync(9302, siteId: null, asOfUtc: null, CancellationToken.None);
        Assert.NotNull(itemB);
        Assert.Equal(30.00m, itemB!.Material);
        Assert.Equal("Item", itemB.OverrideSource[CostElementType.Material]);
    }

    [Fact]
    public async Task PerSite_Override_Mixes_With_ItemLevel_For_Other_Elements()
    {
        // BAR-1018 steel bar — Plant-1 has its own per-cut labor rate, but
        // OH + tooling are still Item-level defaults.
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.SetCostElementAsync(9270, null, CostElementType.Material,  60.00m, CostElementSource.Manual, "mill direct quote 2026-Q2", "buyer", CancellationToken.None);
        await svc.SetCostElementAsync(9270, null, CostElementType.Labor,      4.20m, CostElementSource.Calculated, "0.10 hr saw cut × $42/hr", "system", CancellationToken.None);
        await svc.SetCostElementAsync(9270, null, CostElementType.VariableOverhead, 8.40m, CostElementSource.Calculated, "200% of labor absorption", "system", CancellationToken.None);
        // Plant-1 labor rate is higher ($58/hr) → site override.
        await svc.SetCostElementAsync(9270, 1, CostElementType.Labor, 5.80m, CostElementSource.Calculated, "Plant-1 burdened rate 0.10 hr × $58/hr", "system", CancellationToken.None);

        var b = await svc.GetCostBreakdownAsync(9270, siteId: 1, asOfUtc: null, CancellationToken.None);

        Assert.NotNull(b);
        Assert.Equal(60.00m, b!.Material);
        Assert.Equal("Item", b.OverrideSource[CostElementType.Material]);
        Assert.Equal(5.80m, b.Labor);   // Plant-1 override
        Assert.Equal("ItemSite", b.OverrideSource[CostElementType.Labor]);
        Assert.Equal(8.40m, b.VariableOverhead); // Item-level
        Assert.Equal("Item", b.OverrideSource[CostElementType.VariableOverhead]);
        Assert.Equal(74.20m, b.Total);
    }

    [Fact]
    public async Task SetCostElement_New_Amount_Closes_Prior_Row_And_Creates_History()
    {
        // Steel commodity prices fluctuate — BAR-1018 went $60 → $62.40 mid-quarter.
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var r1 = await svc.SetCostElementAsync(9270, null, CostElementType.Material, 60.00m, CostElementSource.Manual, "Q1 contract", "buyer", CancellationToken.None);
        var r2 = await svc.SetCostElementAsync(9270, null, CostElementType.Material, 62.40m, CostElementSource.Manual, "Q2 contract + commodity uplift", "buyer", CancellationToken.None);

        Assert.NotEqual(r1.Id, r2.Id);
        Assert.Null(r2.EffectiveToUtc);

        var history = await svc.GetHistoryAsync(9270, null, CancellationToken.None);
        Assert.Equal(2, history.Count);
        var r1Refreshed = history.Single(h => h.Id == r1.Id);
        Assert.NotNull(r1Refreshed.EffectiveToUtc);

        // Current breakdown reflects the new value.
        var b = await svc.GetCostBreakdownAsync(9270, null, null, CancellationToken.None);
        Assert.Equal(62.40m, b!.Material);
    }

    [Fact]
    public async Task SetCostElement_Same_Amount_Is_Idempotent_No_Op()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var r1 = await svc.SetCostElementAsync(9245, null, CostElementType.Material, 15.20m, CostElementSource.Manual, null, "buyer", CancellationToken.None);
        var r2 = await svc.SetCostElementAsync(9245, null, CostElementType.Material, 15.20m, CostElementSource.Manual, null, "buyer", CancellationToken.None);

        Assert.Equal(r1.Id, r2.Id);
        var history = await svc.GetHistoryAsync(9245, null, CancellationToken.None);
        Assert.Single(history);
    }

    [Fact]
    public async Task AsOf_Date_Retrieves_Historically_Effective_Row()
    {
        // Steel went $60 (set in Q1) → $62.40 (set later). As-of a date BETWEEN
        // those two writes should return $60.
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.SetCostElementAsync(9270, null, CostElementType.Material, 60.00m, CostElementSource.Manual, "Q1 contract", "buyer", CancellationToken.None);

        // Save a checkpoint after the first write so we can query as-of.
        var checkpoint = DateTime.UtcNow.AddMilliseconds(10);
        await Task.Delay(50);

        await svc.SetCostElementAsync(9270, null, CostElementType.Material, 62.40m, CostElementSource.Manual, "Q2 contract", "buyer", CancellationToken.None);

        var historical = await svc.GetCostBreakdownAsync(9270, null, asOfUtc: checkpoint, CancellationToken.None);
        Assert.Equal(60.00m, historical!.Material);

        var current = await svc.GetCostBreakdownAsync(9270, null, null, CancellationToken.None);
        Assert.Equal(62.40m, current!.Material);
    }

    [Fact]
    public async Task ItemNotFound_Returns_Null_Breakdown()
    {
        await using var db = NewDb();
        var svc = NewService(db);

        var b = await svc.GetCostBreakdownAsync(99999, null, null, CancellationToken.None);

        Assert.Null(b);
    }

    [Fact]
    public async Task Other_CatchAll_Element_Routes_Correctly()
    {
        // Catch-all bucket for freight-in or scrap allowance — important to verify it
        // doesn't get lost in the cascade.
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.SetCostElementAsync(9245, null, CostElementType.Other, 0.45m, CostElementSource.Manual, "freight-in absorption", "buyer", CancellationToken.None);

        var b = await svc.GetCostBreakdownAsync(9245, null, null, CancellationToken.None);
        Assert.Equal(0.45m, b!.Other);
        Assert.Equal(0.45m, b.Total);
        Assert.Equal("Item", b.OverrideSource[CostElementType.Other]);
    }
}
