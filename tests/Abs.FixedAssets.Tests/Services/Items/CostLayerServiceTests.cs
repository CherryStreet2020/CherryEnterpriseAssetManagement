// B6 Foundation Sprint PR-FS-4 (2026-05-26) — CostLayerService tests.
//
// All fixtures use REALISTIC precision-machining items per the HARD LOCK
// (memory: feedback_no_fake_data.md):
//   - 9270 BAR-1018-1.5X12 Cold-rolled Steel Bar 1018, 1.5" x 12 ft
//     Commodity price-tier scenario across Q1/Q2/Q3 2026 = $60.00 / $62.40 / $64.80
//     per foot. Heat numbers H-2401-A / H-2402-B / H-2403-C.
//   - 9245 BRG-6207-2RS Ball Bearing 35x72x17mm Sealed
//     Vendor-quote variation: Grainger $18.90 / MSC $19.25 / Travers $18.05.
//   - 9302 EM-4FL-8MM 8mm 4-Flute Square End Mill Carbide
//     Receipt-then-reverse scenario at $48.50.
//
// Coverage:
//   1. Empty state → GetOpenLayers returns empty, total qty 0, avg cost 0.
//   2. Single receipt → layer recorded with correct fields.
//   3. Three FIFO receipts (BAR-1018 commodity uplift) → consume 100 ft pulls Q1 first.
//   4. Same scenario, LIFO → consume 100 ft pulls Q3 first.
//   5. Same scenario, Average → consume 100 ft uses weighted-avg unit cost.
//   6. Mixed consumption: partial draw exhausts oldest layer + partial from next.
//   7. Idempotent receipt: same (ReceiptType, RefId, Lot) + same qty + cost = no-op.
//   8. Idempotent receipt with different qty → throws (caller must reverse first).
//   9. Reverse receipt with no consumption → layer flips to Reversed.
//  10. Reverse receipt with partial consumption → throws.
//  11. Insufficient quantity for consume → throws.
//  12. Per-Site scoping: receipts at Plant A don't bleed into Plant B's consume.

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

public class CostLayerServiceTests
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
            .UseInMemoryDatabase($"cost-layer-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static CostLayerService NewService(AppDbContext db) =>
        new(db, NullLogger<CostLayerService>.Instance);

    // Realistic precision-machining items.
    private static Item Bar1018() => new()
    {
        Id = 9270,
        PartNumber = "BAR-1018-1.5X12",
        Description = "Cold-rolled Steel Bar 1018, 1.5\" diameter x 12 ft",
        StockUOM = "FT",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
        StandardCost = 60.00m,
    };

    private static Item BearingBrg6207() => new()
    {
        Id = 9245,
        PartNumber = "BRG-6207-2RS",
        Description = "Ball Bearing 35x72x17mm Sealed",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
        StandardCost = 18.90m,
    };

    private static Item EndMill() => new()
    {
        Id = 9302,
        PartNumber = "EM-4FL-8MM",
        Description = "8mm 4-Flute Square End Mill Carbide",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
        StandardCost = 48.50m,
    };

    [Fact]
    public async Task Empty_State_Returns_Zero()
    {
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);

        Assert.Empty(await svc.GetOpenLayersAsync(9270, null, CancellationToken.None));
        Assert.Equal(0m, await svc.GetTotalOpenQuantityAsync(9270, null, CancellationToken.None));
        Assert.Equal(0m, await svc.GetWeightedAverageCostAsync(9270, null, CancellationToken.None));
    }

    [Fact]
    public async Task RecordReceipt_Persists_Layer_With_Correct_Fields()
    {
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var layer = await svc.RecordReceiptAsync(
            itemId: 9270, siteId: 1,
            receiptType: CostLayerReceiptType.PurchaseOrder,
            receiptReferenceId: 47281,
            receiptDocumentNumber: "PO-2026-Q1-47281",
            quantity: 48m,
            unitCost: 60.00m,
            currencyCode: "USD",
            lotNumber: "L-2401-A",
            serialNumber: null,
            heatNumber: "H-2401-A",
            vendorLot: "RYERSON-Q1-A",
            vendorReference: "Ryerson Steel 2026-Q1 contract",
            createdBy: "buyer",
            ct: CancellationToken.None);

        Assert.True(layer.Id > 0);
        Assert.Equal(9270, layer.ItemId);
        Assert.Equal(1, layer.SiteId);
        Assert.Equal(1L, layer.LayerNumber);
        Assert.Equal(48m, layer.ReceivedQuantity);
        Assert.Equal(48m, layer.RemainingQuantity);
        Assert.Equal(60.00m, layer.UnitCost);
        Assert.Equal("H-2401-A", layer.HeatNumber);
        Assert.Equal(CostLayerStatus.Open, layer.Status);
    }

    [Fact]
    public async Task FIFO_Consumes_Oldest_First()
    {
        // BAR-1018 commodity uplift across Q1/Q2/Q3 2026: $60.00 / $62.40 / $64.80
        // per foot. Heat numbers H-2401-A / H-2402-B / H-2403-C. Receive 48 ft each;
        // consume 100 ft FIFO; should pull all 48 of Q1, all 48 of Q2, 4 of Q3.
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 47281, "PO-2026-Q1-47281", 48m, 60.00m, "USD", "L-2401-A", null, "H-2401-A", "RYERSON-Q1-A", "Ryerson Q1 contract", "buyer", CancellationToken.None);
        await Task.Delay(10);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 48902, "PO-2026-Q2-48902", 48m, 62.40m, "USD", "L-2402-B", null, "H-2402-B", "RYERSON-Q2-B", "Ryerson Q2 contract uplift", "buyer", CancellationToken.None);
        await Task.Delay(10);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 50115, "PO-2026-Q3-50115", 48m, 64.80m, "USD", "L-2403-C", null, "H-2403-C", "RYERSON-Q3-C", "Ryerson Q3 contract uplift", "buyer", CancellationToken.None);

        var consumptions = await svc.ConsumeQuantityAsync(
            itemId: 9270, siteId: 1, quantity: 100m,
            costMethod: CostMethod.FIFO,
            consumedBy: "operator",
            consumptionReason: "Issue to PRO-2026-04421",
            ct: CancellationToken.None);

        Assert.Equal(3, consumptions.Count);
        Assert.Equal(48m, consumptions[0].QuantityConsumed);
        Assert.Equal(60.00m, consumptions[0].UnitCost);
        Assert.Equal("H-2401-A", consumptions[0].HeatNumber);
        Assert.Equal(48m, consumptions[1].QuantityConsumed);
        Assert.Equal(62.40m, consumptions[1].UnitCost);
        Assert.Equal("H-2402-B", consumptions[1].HeatNumber);
        Assert.Equal(4m, consumptions[2].QuantityConsumed);
        Assert.Equal(64.80m, consumptions[2].UnitCost);
        Assert.Equal("H-2403-C", consumptions[2].HeatNumber);

        // Total cost charged = (48*60.00) + (48*62.40) + (4*64.80) = 2880 + 2995.20 + 259.20 = 6134.40
        var totalCost = consumptions.Sum(c => c.CostConsumed);
        Assert.Equal(6134.40m, totalCost);

        // Remaining open: 0 + 0 + 44 = 44 ft, all at $64.80.
        Assert.Equal(44m, await svc.GetTotalOpenQuantityAsync(9270, 1, CancellationToken.None));
        Assert.Equal(64.80m, await svc.GetWeightedAverageCostAsync(9270, 1, CancellationToken.None));
    }

    [Fact]
    public async Task LIFO_Consumes_Newest_First()
    {
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 47281, "PO-2026-Q1-47281", 48m, 60.00m, "USD", "L-2401-A", null, "H-2401-A", "RYERSON-Q1-A", "Ryerson Q1", "buyer", CancellationToken.None);
        await Task.Delay(10);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 48902, "PO-2026-Q2-48902", 48m, 62.40m, "USD", "L-2402-B", null, "H-2402-B", "RYERSON-Q2-B", "Ryerson Q2", "buyer", CancellationToken.None);
        await Task.Delay(10);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 50115, "PO-2026-Q3-50115", 48m, 64.80m, "USD", "L-2403-C", null, "H-2403-C", "RYERSON-Q3-C", "Ryerson Q3", "buyer", CancellationToken.None);

        var consumptions = await svc.ConsumeQuantityAsync(9270, 1, 100m, CostMethod.LIFO, "operator", "Issue to PRO-04421", CancellationToken.None);

        Assert.Equal(3, consumptions.Count);
        Assert.Equal(48m, consumptions[0].QuantityConsumed);
        Assert.Equal(64.80m, consumptions[0].UnitCost);   // newest first
        Assert.Equal(48m, consumptions[1].QuantityConsumed);
        Assert.Equal(62.40m, consumptions[1].UnitCost);
        Assert.Equal(4m, consumptions[2].QuantityConsumed);
        Assert.Equal(60.00m, consumptions[2].UnitCost);   // oldest last

        // Total cost LIFO = (48*64.80) + (48*62.40) + (4*60.00) = 3110.40 + 2995.20 + 240.00 = 6345.60
        Assert.Equal(6345.60m, consumptions.Sum(c => c.CostConsumed));
    }

    [Fact]
    public async Task Average_Uses_WeightedAverage_Across_Open_Layers()
    {
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 47281, "PO-Q1", 48m, 60.00m, "USD", "L-A", null, "H-A", null, null, "buyer", CancellationToken.None);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 48902, "PO-Q2", 48m, 62.40m, "USD", "L-B", null, "H-B", null, null, "buyer", CancellationToken.None);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 50115, "PO-Q3", 48m, 64.80m, "USD", "L-C", null, "H-C", null, null, "buyer", CancellationToken.None);

        // Weighted-avg over 144 ft = (48*60 + 48*62.40 + 48*64.80) / 144 = (2880 + 2995.20 + 3110.40) / 144 = 8985.60 / 144 = 62.40
        var avg = await svc.GetWeightedAverageCostAsync(9270, 1, CancellationToken.None);
        Assert.Equal(62.40m, avg);

        var consumptions = await svc.ConsumeQuantityAsync(9270, 1, 100m, CostMethod.Average, "operator", "Issue avg", CancellationToken.None);

        // Every consumption row uses the same weighted-avg unit cost.
        Assert.All(consumptions, c => Assert.Equal(62.40m, c.UnitCost));
        // Total cost = 100 * 62.40 = 6240.00 (within rounding tolerance)
        Assert.Equal(6240.00m, Math.Round(consumptions.Sum(c => c.CostConsumed), 2));
    }

    [Fact]
    public async Task FIFO_Partial_Draw_Exhausts_Oldest_And_Draws_From_Next()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        // 50 EA from Grainger @ $18.90, then 30 EA from MSC @ $19.25 (Grainger out of stock that week).
        await svc.RecordReceiptAsync(9245, 1, CostLayerReceiptType.PurchaseOrder, 12001, "PO-2026-12001", 50m, 18.90m, "USD", "BRG-LOT-A", null, null, "GRAINGER-2026-W23", "Grainger Industrial", "buyer", CancellationToken.None);
        await Task.Delay(10);
        await svc.RecordReceiptAsync(9245, 1, CostLayerReceiptType.PurchaseOrder, 12047, "PO-2026-12047", 30m, 19.25m, "USD", "BRG-LOT-B", null, null, "MSC-2026-W24", "MSC Industrial Supply", "buyer", CancellationToken.None);

        var consumptions = await svc.ConsumeQuantityAsync(9245, 1, 60m, CostMethod.FIFO, "operator", null, CancellationToken.None);

        Assert.Equal(2, consumptions.Count);
        Assert.Equal(50m, consumptions[0].QuantityConsumed);
        Assert.Equal(18.90m, consumptions[0].UnitCost);
        Assert.Equal(10m, consumptions[1].QuantityConsumed);
        Assert.Equal(19.25m, consumptions[1].UnitCost);

        // 50*18.90 + 10*19.25 = 945.00 + 192.50 = 1137.50
        Assert.Equal(1137.50m, consumptions.Sum(c => c.CostConsumed));

        // Open: Grainger exhausted, MSC has 20 left.
        var open = await svc.GetOpenLayersAsync(9245, 1, CancellationToken.None);
        Assert.Single(open);
        Assert.Equal("MSC-2026-W24", open[0].VendorLot);
        Assert.Equal(20m, open[0].RemainingQuantity);
    }

    [Fact]
    public async Task Idempotent_Receipt_With_Same_Values_Is_NoOp()
    {
        await using var db = NewDb();
        db.Items.Add(EndMill());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var r1 = await svc.RecordReceiptAsync(9302, 1, CostLayerReceiptType.PurchaseOrder, 13500, "PO-2026-13500", 12m, 48.50m, "USD", "EM-LOT-2026-W22", null, null, "TRAVERS-2026-W22", "Travers Tool", "buyer", CancellationToken.None);
        var r2 = await svc.RecordReceiptAsync(9302, 1, CostLayerReceiptType.PurchaseOrder, 13500, "PO-2026-13500", 12m, 48.50m, "USD", "EM-LOT-2026-W22", null, null, "TRAVERS-2026-W22", "Travers Tool", "buyer", CancellationToken.None);

        Assert.Equal(r1.Id, r2.Id);
        var layers = await svc.GetOpenLayersAsync(9302, 1, CancellationToken.None);
        Assert.Single(layers);
    }

    [Fact]
    public async Task Idempotent_Receipt_With_Different_Qty_Throws()
    {
        await using var db = NewDb();
        db.Items.Add(EndMill());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.RecordReceiptAsync(9302, 1, CostLayerReceiptType.PurchaseOrder, 13500, "PO-2026-13500", 12m, 48.50m, "USD", "EM-LOT-W22", null, null, "TRAVERS-W22", "Travers Tool", "buyer", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await svc.RecordReceiptAsync(9302, 1, CostLayerReceiptType.PurchaseOrder, 13500, "PO-2026-13500", 15m, 48.50m, "USD", "EM-LOT-W22", null, null, "TRAVERS-W22", "Travers Tool", "buyer", CancellationToken.None));
    }

    [Fact]
    public async Task Reverse_Receipt_With_No_Consumption_Flips_Status()
    {
        await using var db = NewDb();
        db.Items.Add(EndMill());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var layer = await svc.RecordReceiptAsync(9302, 1, CostLayerReceiptType.PurchaseOrder, 13500, "PO-2026-13500", 12m, 48.50m, "USD", "EM-LOT-W22", null, null, "TRAVERS-W22", "Travers Tool", "buyer", CancellationToken.None);
        var reversed = await svc.ReverseReceiptAsync(layer.Id, "Vendor shipped wrong part — RMA RM-2026-0331 issued", "buyer", CancellationToken.None);

        Assert.Equal(CostLayerStatus.Reversed, reversed.Status);
        Assert.Equal(0m, reversed.RemainingQuantity);
        Assert.NotNull(reversed.ReversedAtUtc);
        Assert.Empty(await svc.GetOpenLayersAsync(9302, 1, CancellationToken.None));
    }

    [Fact]
    public async Task Reverse_Receipt_With_Partial_Consumption_Throws()
    {
        await using var db = NewDb();
        db.Items.Add(EndMill());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var layer = await svc.RecordReceiptAsync(9302, 1, CostLayerReceiptType.PurchaseOrder, 13500, "PO-2026-13500", 12m, 48.50m, "USD", "EM-LOT-W22", null, null, "TRAVERS-W22", "Travers Tool", "buyer", CancellationToken.None);
        await svc.ConsumeQuantityAsync(9302, 1, 3m, CostMethod.FIFO, "operator", null, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await svc.ReverseReceiptAsync(layer.Id, "Trying to reverse after partial use", "buyer", CancellationToken.None));
    }

    [Fact]
    public async Task Insufficient_Quantity_For_Consume_Throws()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.RecordReceiptAsync(9245, 1, CostLayerReceiptType.PurchaseOrder, 12001, "PO-12001", 10m, 18.90m, "USD", "BRG-LOT-A", null, null, null, null, "buyer", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await svc.ConsumeQuantityAsync(9245, 1, 25m, CostMethod.FIFO, "operator", null, CancellationToken.None));
    }

    [Fact]
    public async Task PerSite_Scoping_Isolates_Layers()
    {
        // Plant 1 has its own steel inventory; Plant 2 has its own. Consuming
        // at Plant 1 cannot draw from Plant 2's layers.
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.RecordReceiptAsync(9270, 1, CostLayerReceiptType.PurchaseOrder, 47281, "PO-Plant1", 48m, 60.00m, "USD", "L-P1-A", null, "H-P1-A", null, null, "buyer", CancellationToken.None);
        await svc.RecordReceiptAsync(9270, 2, CostLayerReceiptType.PurchaseOrder, 47282, "PO-Plant2", 48m, 62.40m, "USD", "L-P2-A", null, "H-P2-A", null, null, "buyer", CancellationToken.None);

        var plant1Qty = await svc.GetTotalOpenQuantityAsync(9270, 1, CancellationToken.None);
        var plant2Qty = await svc.GetTotalOpenQuantityAsync(9270, 2, CancellationToken.None);
        Assert.Equal(48m, plant1Qty);
        Assert.Equal(48m, plant2Qty);

        // Consume 20 from Plant 1 — Plant 2 untouched.
        await svc.ConsumeQuantityAsync(9270, 1, 20m, CostMethod.FIFO, "operator", null, CancellationToken.None);
        Assert.Equal(28m, await svc.GetTotalOpenQuantityAsync(9270, 1, CancellationToken.None));
        Assert.Equal(48m, await svc.GetTotalOpenQuantityAsync(9270, 2, CancellationToken.None));
    }
}
