// Sprint 14.1 PR-1 (2026-05-26) — PoSnapshotServiceTests.
//
// All fixtures use REALISTIC aerospace + mfg scenarios per HARD LOCK
// feedback_no_fake_data.md:
//
//   ASM-TRENT-BRACKET-A   — Trent XWB engine bracket assembly (parent).
//   BRG-6207-2RS          — SKF ball bearing 35x72x17mm Sealed.
//   BAR-1018-1.5X12       — Cold-rolled steel bar 1018, 1.5" dia x 12 ft.
//   FAST-M8-25-A2         — DIN 933 M8x25 A2 stainless hex bolt.
//   ASM-SUBASSY-MOUNT     — Mount subassembly (phantom, exploded through).
//
// Coverage (16 tests):
//   1. Capture happy path freezes every line.
//   2. Capture is idempotent on re-call (no duplicate rows).
//   3. Capture stamps header timestamps (CapturedAtUtc + CapturedBy).
//   4. Capture stamps SourceMaterialStructureRevision.
//   5. Capture stamps SourceItemRevisionId from Item.CurrentReleasedRevisionId.
//   6. Capture on PRO without MaterialStructure returns Failure with friendly error.
//   7. Capture with empty BOM stamps header timestamps + zero rows (warn-not-fail).
//   8. Capture sets FrozenStandardCost from child Item.StandardCost.
//   9. Capture computes FrozenExtendedCost = qty * (1 + scrap%/100) * stdCost.
//  10. Capture fingerprint hash is 64-char hex + deterministic.
//  11. Capture frozen ChildPartNumber survives Item rename post-capture.
//  12. Capture does not reflect post-snapshot BOM changes.
//  13. ClearSnapshot deletes lines + nulls header.
//  14. ClearSnapshot on unsnapshotted PRO is a no-op success.
//  15. GetSnapshot on unsnapshotted PRO returns empty lines + null header.
//  16. Capture preserves source line Sequence ordering.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Production;

public class PoSnapshotServiceTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            // EF InMemory provider doesn't honor rowversion / concurrency
            // tokens — strip them so the in-memory fixtures don't throw.
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.CostLayer>().Ignore(c => c.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.ItemSourcingRule>().Ignore(r => r.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.CustomerItemXref>().Ignore(x => x.RowVersion);
            mb.Entity<ProductionMaterialStructure>().Ignore(p => p.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"po-snap-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static PoSnapshotService NewService(AppDbContext db) =>
        new(db, NullLogger<PoSnapshotService>.Instance);

    // ---------- realistic-mfg fixtures ----------

    private static Item TrentBracket() => new()
    {
        Id = 9501,
        PartNumber = "ASM-TRENT-BRACKET-A",
        Description = "Rolls-Royce Trent XWB Engine Bracket Assembly",
        Revision = "A",
        StockUOM = "EA",
        StandardCost = 487.50m,
        Type = ItemType.Part,
        Source = ItemMasterSource.Internal,
        IsActive = true,
        IsSellable = true,
        AS9100Critical = true,
        LifecycleStage = LifecycleStage.Production,
        MakeBuyCode = MakeBuyCode.Make,
    };

    private static Item BearingBrg6207() => new()
    {
        Id = 9245,
        PartNumber = "BRG-6207-2RS",
        Description = "SKF Ball Bearing 35x72x17mm Sealed",
        Revision = "R3",
        StockUOM = "EA",
        StandardCost = 12.4275m,
        Type = ItemType.Bearing,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
        MakeBuyCode = MakeBuyCode.Buy,
    };

    private static Item Bar1018() => new()
    {
        Id = 9270,
        PartNumber = "BAR-1018-1.5X12",
        Description = "Cold-rolled Steel Bar 1018, 1.5\" dia x 12 ft (Ryerson)",
        Revision = "A",
        StockUOM = "FT",
        StandardCost = 18.6000m,
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
        MakeBuyCode = MakeBuyCode.Buy,
    };

    private static Item FastenerM8() => new()
    {
        Id = 9410,
        PartNumber = "FAST-M8-25-A2",
        Description = "DIN 933 M8x25 A2 Stainless Hex Bolt (Grainger)",
        Revision = "A",
        StockUOM = "EA",
        StandardCost = 0.2750m,
        Type = ItemType.Fastener,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
        MakeBuyCode = MakeBuyCode.Buy,
    };

    private static Item PhantomMount() => new()
    {
        Id = 9550,
        PartNumber = "ASM-SUBASSY-MOUNT",
        Description = "Mount subassembly (phantom; exploded through MRP)",
        Revision = "A",
        StockUOM = "EA",
        StandardCost = 0m, // phantom — no stocked cost
        Type = ItemType.Part,
        Source = ItemMasterSource.Internal,
        IsActive = true,
        IsPhantom = true,
        MakeBuyCode = MakeBuyCode.Phantom,
    };

    private static MaterialStructure TrentBracketBom(int companyId = 1, int locationId = 1)
    {
        return new MaterialStructure
        {
            Id = 7100,
            CompanyId = companyId,
            LocationId = locationId,
            StructureNumber = "MS-TRENT-BRACKET-A-001",
            Name = "Trent XWB Bracket BOM, Rev A",
            StructureType = StructureType.Bom,
            Status = MaterialStructureStatus.Approved,
            Revision = "A",
            OutputItemId = 9501,
            ApprovedAt = DateTime.UtcNow.AddDays(-7),
            ApprovedBy = "engineer-1",
            Lines = new System.Collections.Generic.List<MaterialStructureLine>
            {
                new() { Id = 7101, MaterialStructureId = 7100, ItemId = 9270, LineKind = LineKind.Component, Sequence = 10, Quantity = 2.5m,  Uom = "FT", ScrapPercent = 3.0m },
                new() { Id = 7102, MaterialStructureId = 7100, ItemId = 9245, LineKind = LineKind.Component, Sequence = 20, Quantity = 4m,    Uom = "EA", ScrapPercent = 0m },
                new() { Id = 7103, MaterialStructureId = 7100, ItemId = 9410, LineKind = LineKind.Component, Sequence = 30, Quantity = 12m,   Uom = "EA", ScrapPercent = 2.0m },
                new() { Id = 7104, MaterialStructureId = 7100, ItemId = 9550, LineKind = LineKind.Component, Sequence = 40, Quantity = 1m,    Uom = "EA", ScrapPercent = 0m },
            },
        };
    }

    private static ProductionOrder TrentBracketPro(int id = 5000, int companyId = 1, int locationId = 1, int? materialStructureId = 7100)
    {
        return new ProductionOrder
        {
            Id = id,
            CompanyId = companyId,
            OrderNumber = $"PRO-2026-{id:D5}",
            Type = ProductionType.JobShop,
            Status = ProductionOrderStatus.Released,
            Title = "Trent XWB Bracket — Rolls-Royce shipset 27",
            ItemId = 9501,
            LocationId = locationId,
            QuantityOrdered = 8m,
            Uom = "EA",
            MaterialStructureId = materialStructureId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "operator-1",
        };
    }

    private static async Task SeedAsync(AppDbContext db, int companyId = 1, int locationId = 1, int? materialStructureId = 7100)
    {
        db.Items.AddRange(TrentBracket(), BearingBrg6207(), Bar1018(), FastenerM8(), PhantomMount());
        if (materialStructureId.HasValue)
        {
            db.MaterialStructures.Add(TrentBracketBom(companyId, locationId));
        }
        db.ProductionOrders.Add(TrentBracketPro(5000, companyId, locationId, materialStructureId));
        await db.SaveChangesAsync();
    }

    // ---------- tests --------------------------------------------------------

    [Fact]
    public async Task Capture_HappyPath_FreezesEveryLine()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(result.Value);
        Assert.Equal(4, result.Value!.Lines.Count);
        Assert.Equal("PRO-2026-05000", result.Value.OrderNumber);
        // Lines ordered by Sequence.
        Assert.Equal(new[] { 10, 20, 30, 40 }, result.Value.Lines.Select(l => l.Sequence).ToArray());
        Assert.Equal("BAR-1018-1.5X12", result.Value.Lines[0].ChildPartNumber);
        Assert.Equal("BRG-6207-2RS", result.Value.Lines[1].ChildPartNumber);
        Assert.Equal("FAST-M8-25-A2", result.Value.Lines[2].ChildPartNumber);
        Assert.Equal("ASM-SUBASSY-MOUNT", result.Value.Lines[3].ChildPartNumber);
        // Phantom flag mirrored.
        Assert.True(result.Value.Lines[3].IsPhantom);
        Assert.False(result.Value.Lines[0].IsPhantom);
    }

    [Fact]
    public async Task Capture_IsIdempotent_OnRecall()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var first = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);
        var second = await svc.CaptureAsync(5000, "operator-2", CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        // Same row count, same line identities — no duplicate rows written.
        var rowCount = await db.ProductionMaterialStructures.CountAsync(s => s.ProductionOrderId == 5000);
        Assert.Equal(4, rowCount);
        // Captured-by is preserved from the FIRST call (not overwritten).
        var po = await db.ProductionOrders.FirstAsync(p => p.Id == 5000);
        Assert.Equal("operator-1", po.SnapshotCapturedBy);
    }

    [Fact]
    public async Task Capture_StampsHeaderTimestamps()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var before = DateTime.UtcNow;
        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);
        var after = DateTime.UtcNow;

        Assert.True(result.IsSuccess);
        var po = await db.ProductionOrders.FirstAsync(p => p.Id == 5000);
        Assert.NotNull(po.SnapshotCapturedAtUtc);
        Assert.InRange(po.SnapshotCapturedAtUtc!.Value, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.Equal("operator-1", po.SnapshotCapturedBy);
    }

    [Fact]
    public async Task Capture_StampsSourceMaterialStructureRevision()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var po = await db.ProductionOrders.FirstAsync(p => p.Id == 5000);
        Assert.Equal("A", po.SourceMaterialStructureRevision);
        Assert.Equal("A", result.Value!.SourceMaterialStructureRevision);
    }

    [Fact]
    public async Task Capture_StampsSourceItemRevisionId_FromCurrentReleasedRevision()
    {
        await using var db = NewDb();
        // Seed Items + Bom, then layer a CurrentReleasedRevision onto the parent.
        await SeedAsync(db);
        // Use uppercase RevisionCode because AppDbContext's global string
        // normalizer uppercases "RevisionCode" (no allowlist entry for it,
        // by long-standing convention — see SaveChanges interceptor block).
        // "REV-12" mirrors what CustomerItemXref + every prior revision-coded
        // entity already stores.
        var rev = new Abs.FixedAssets.Models.ItemRevision
        {
            Id = 7777,
            ItemId = 9501,
            RevisionCode = "REV-12",
            Status = Abs.FixedAssets.Models.Revisions.RevisionStatus.Released,
            Name = "Trent Bracket Rev 12 release",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            ReleasedAtUtc = DateTime.UtcNow.AddDays(-1),
        };
        db.ItemRevisions.Add(rev);
        var parent = await db.Items.FirstAsync(i => i.Id == 9501);
        parent.CurrentReleasedRevisionId = 7777;
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7777, result.Value!.SourceItemRevisionId);
        Assert.Equal("REV-12", result.Value.SourceItemRevisionCode);
    }

    [Fact]
    public async Task Capture_OnPRO_WithoutMaterialStructure_ReturnsFailure()
    {
        await using var db = NewDb();
        await SeedAsync(db, materialStructureId: null);
        var svc = NewService(db);

        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("MaterialStructure", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Capture_EmptyBom_StillStampsHeader()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        // Strip all lines from the BOM to simulate empty.
        var lines = await db.MaterialStructureLines.Where(l => l.MaterialStructureId == 7100).ToListAsync();
        db.MaterialStructureLines.RemoveRange(lines);
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Lines);
        var po = await db.ProductionOrders.FirstAsync(p => p.Id == 5000);
        Assert.NotNull(po.SnapshotCapturedAtUtc);
        Assert.Equal("operator-1", po.SnapshotCapturedBy);
    }

    [Fact]
    public async Task Capture_SetsFrozenStandardCost_FromChildItem()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);
        Assert.True(result.IsSuccess);

        var bearingLine = result.Value!.Lines.Single(l => l.ChildPartNumber == "BRG-6207-2RS");
        Assert.Equal(12.4275m, bearingLine.FrozenStandardCost);

        var barLine = result.Value.Lines.Single(l => l.ChildPartNumber == "BAR-1018-1.5X12");
        Assert.Equal(18.6000m, barLine.FrozenStandardCost);

        var phantomLine = result.Value.Lines.Single(l => l.ChildPartNumber == "ASM-SUBASSY-MOUNT");
        // Phantom Item has StandardCost=0 → snapshot stores null (signals "no cost").
        Assert.Null(phantomLine.FrozenStandardCost);
    }

    [Fact]
    public async Task Capture_ComputesFrozenExtendedCost_WithScrap()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);
        Assert.True(result.IsSuccess);

        // BAR-1018: Qty 2.5 FT * (1 + 3%/100) * $18.60 = 2.5 * 1.03 * 18.60 = 47.8950
        var barLine = result.Value!.Lines.Single(l => l.ChildPartNumber == "BAR-1018-1.5X12");
        Assert.Equal(47.8950m, barLine.FrozenExtendedCost);

        // BRG-6207-2RS: Qty 4 EA * (1 + 0%/100) * $12.4275 = 49.7100
        var bearingLine = result.Value.Lines.Single(l => l.ChildPartNumber == "BRG-6207-2RS");
        Assert.Equal(49.7100m, bearingLine.FrozenExtendedCost);

        // FAST-M8-25-A2: Qty 12 EA * (1 + 2%/100) * $0.2750 = 12 * 1.02 * 0.275 = 3.366
        var fastLine = result.Value.Lines.Single(l => l.ChildPartNumber == "FAST-M8-25-A2");
        Assert.Equal(3.3660m, fastLine.FrozenExtendedCost);
    }

    [Fact]
    public async Task Capture_FingerprintHash_Is64HexChars_Deterministic()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);
        Assert.True(result.IsSuccess);

        foreach (var line in result.Value!.Lines)
        {
            Assert.NotNull(line.ChildItemFingerprintHash);
            Assert.Equal(64, line.ChildItemFingerprintHash!.Length);
            Assert.Matches("^[0-9a-f]{64}$", line.ChildItemFingerprintHash);
        }

        // Determinism — recompute against the same Items, compare.
        var brg = BearingBrg6207();
        var brgHash = PoSnapshotService.ComputeItemFingerprint(brg);
        var bearingLine = result.Value.Lines.Single(l => l.ChildPartNumber == "BRG-6207-2RS");
        Assert.Equal(brgHash, bearingLine.ChildItemFingerprintHash);
    }

    [Fact]
    public async Task Capture_FrozenChildPartNumber_SurvivesItemRename()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);
        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);
        Assert.True(result.IsSuccess);

        // Rename the bearing Item AFTER capture.
        var bearing = await db.Items.FirstAsync(i => i.Id == 9245);
        bearing.PartNumber = "BRG-6207-2RS-OBSOLETE-DO-NOT-USE";
        await db.SaveChangesAsync();

        // Snapshot row retains the frozen name.
        var snapRow = await db.ProductionMaterialStructures.FirstAsync(s => s.ProductionOrderId == 5000 && s.ChildItemId == 9245);
        Assert.Equal("BRG-6207-2RS", snapRow.ChildPartNumber);
    }

    [Fact]
    public async Task Capture_DoesNotReflect_PostSnapshot_BomChanges()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);
        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);
        Assert.True(result.IsSuccess);

        // Add a new line + delete an existing line in the source BOM AFTER capture.
        db.MaterialStructureLines.Add(new MaterialStructureLine
        {
            Id = 7199, MaterialStructureId = 7100, ItemId = 9410, LineKind = LineKind.Component,
            Sequence = 50, Quantity = 99m, Uom = "EA",
        });
        var lineToRemove = await db.MaterialStructureLines.FirstAsync(l => l.Id == 7101);
        db.MaterialStructureLines.Remove(lineToRemove);
        await db.SaveChangesAsync();

        // Snapshot is unchanged.
        var snapshot = await svc.GetSnapshotAsync(5000, CancellationToken.None);
        Assert.Equal(4, snapshot.Lines.Count);
        Assert.Contains(snapshot.Lines, l => l.ChildPartNumber == "BAR-1018-1.5X12"); // line 7101 still in snapshot
        Assert.DoesNotContain(snapshot.Lines, l => l.Sequence == 50); // line 7199 NOT in snapshot
    }

    [Fact]
    public async Task ClearSnapshot_DeletesLines_AndNullsHeader()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);
        await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);

        var clearResult = await svc.ClearSnapshotAsync(5000, "admin-recovery", "captured against stale BOM", CancellationToken.None);

        Assert.True(clearResult.IsSuccess);
        var rowCount = await db.ProductionMaterialStructures.CountAsync(s => s.ProductionOrderId == 5000);
        Assert.Equal(0, rowCount);
        var po = await db.ProductionOrders.FirstAsync(p => p.Id == 5000);
        Assert.Null(po.SnapshotCapturedAtUtc);
        Assert.Null(po.SnapshotCapturedBy);
        Assert.Null(po.SourceMaterialStructureRevision);
        Assert.Null(po.SourceItemRevisionId);
    }

    [Fact]
    public async Task ClearSnapshot_OnUnsnapshottedPRO_IsIdempotent()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var clearResult = await svc.ClearSnapshotAsync(5000, "admin", "preventive cleanup", CancellationToken.None);

        Assert.True(clearResult.IsSuccess);
        Assert.Empty(clearResult.Value!.Lines);
        Assert.Null(clearResult.Value.SnapshotCapturedAtUtc);
    }

    [Fact]
    public async Task GetSnapshot_OnUnsnapshottedPRO_ReturnsEmptyLines()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var snapshot = await svc.GetSnapshotAsync(5000, CancellationToken.None);

        Assert.Empty(snapshot.Lines);
        Assert.Null(snapshot.SnapshotCapturedAtUtc);
        Assert.Null(snapshot.SnapshotCapturedBy);
        Assert.Equal("PRO-2026-05000", snapshot.OrderNumber);
    }

    [Fact]
    public async Task Capture_PreservesLineSequence()
    {
        await using var db = NewDb();
        await SeedAsync(db);
        var svc = NewService(db);

        var result = await svc.CaptureAsync(5000, "operator-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 10, 20, 30, 40 }, result.Value!.Lines.Select(l => l.Sequence).ToArray());
    }
}
