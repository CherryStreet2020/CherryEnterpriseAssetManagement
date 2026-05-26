// B6 Foundation Sprint PR-FS-6 (2026-05-26) — CustomerItemXrefService tests.
//
// All fixtures use REALISTIC OEM customer + part-number scenarios per HARD LOCK
// feedback_no_fake_data.md:
//
//   9245 BRG-6207-2RS Ball Bearing — GE Aviation (CustomerId=42) calls it
//        "GEAV-BRG-A12345" Rev R3 per drawing "DWG-A12345" R3.
//   9270 BAR-1018-1.5X12 Steel Bar — Boeing (CustomerId=51) calls it
//        "BA-MATL-1018-CR-RD-1.5" per BAMS-3320 spec.
//   9302 EM-4FL-8MM End Mill — Pratt & Whitney (CustomerId=63) calls it
//        "PW-TOOL-EM-4F-8-C2" Rev A.
//
// Coverage:
//   1. Add + Resolve (both directions).
//   2. Multi-OEM scoping: same literal customer-PN at two different Customers
//      resolves to different Items (or different Customer's xref).
//   3. Customer revision specificity: same customer-PN with different revisions
//      resolved by Rev parameter.
//   4. Supersession: R3 supersedes R2; R2 becomes Status=Superseded with
//      SupersededByXrefId pointing to the new row; lookup returns R3 only.
//   5. Obsolete xref excluded from default lookup.
//   6. Idempotent AddXref (same Customer/PN/Rev → same ItemId = no-op).
//   7. AddXref with different ItemId throws (caller must supersede first).
//   8. NULL-safe uniqueness — same Customer + PN with CustomerRevision=null
//      twice throws.
//   9. ResolveByCustomerPnAsync returns null for unknown PN.
//  10. ResolveByItem returns the current active xref.
//  11. GetAllForItem returns all (Active + Superseded by default).
//  12. GetAllForCustomer returns all (Active + Superseded by default).

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

public class CustomerItemXrefServiceTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.CostLayer>().Ignore(c => c.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.ItemSourcingRule>().Ignore(r => r.RowVersion);
            mb.Entity<Abs.FixedAssets.Models.Masters.CustomerItemXref>().Ignore(x => x.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cust-itm-xref-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static CustomerItemXrefService NewService(AppDbContext db) =>
        new(db, NullLogger<CustomerItemXrefService>.Instance);

    private static Item BearingBrg6207() => new()
    {
        Id = 9245,
        PartNumber = "BRG-6207-2RS",
        Description = "Ball Bearing 35x72x17mm Sealed",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
    };

    private static Item Bar1018() => new()
    {
        Id = 9270,
        PartNumber = "BAR-1018-1.5X12",
        Description = "Cold-rolled Steel Bar 1018, 1.5\" dia x 12 ft",
        StockUOM = "FT",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
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
    };

    [Fact]
    public async Task Add_And_Resolve_Both_Directions()
    {
        // GE Aviation (Customer=42) calls BRG-6207-2RS → "GEAV-BRG-A12345" Rev R3.
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345", "R3", "GE Aviation A-series bearing", "DWG-A12345", "R3", "AS9100-AVL-001", "GE-ECO-2026-118", "Customer-side xref for SO ingest + ship docs", "buyer", CancellationToken.None);

        // Customer PN → Item.
        var byPn = await svc.ResolveByCustomerPnAsync(42, "GEAV-BRG-A12345", "R3", null, CancellationToken.None);
        Assert.NotNull(byPn);
        Assert.Equal(9245, byPn!.ItemId);
        Assert.Equal("DWG-A12345", byPn.CustomerDrawingNumber);

        // Item → Customer PN.
        var byItem = await svc.ResolveByItemAsync(42, 9245, null, CancellationToken.None);
        Assert.NotNull(byItem);
        Assert.Equal("GEAV-BRG-A12345", byItem!.CustomerPartNumber);
        Assert.Equal("R3", byItem.CustomerRevision);
    }

    [Fact]
    public async Task MultiOem_Same_LiteralPn_At_Different_Customers_Resolves_Separately()
    {
        // Highly unlikely collision but valid: GE Aviation Customer=42 and Boeing
        // Customer=51 both happen to use literal "PN-001" but for different Items.
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddXrefAsync(9245, 42, "PN-001", null, "GE bearing", null, null, null, null, null, "buyer", CancellationToken.None);
        await svc.AddXrefAsync(9270, 51, "PN-001", null, "Boeing bar stock", null, null, null, null, null, "buyer", CancellationToken.None);

        var geResolution = await svc.ResolveByCustomerPnAsync(42, "PN-001", null, null, CancellationToken.None);
        var boeingResolution = await svc.ResolveByCustomerPnAsync(51, "PN-001", null, null, CancellationToken.None);

        Assert.NotNull(geResolution);
        Assert.NotNull(boeingResolution);
        Assert.Equal(9245, geResolution!.ItemId);
        Assert.Equal(9270, boeingResolution!.ItemId);
    }

    [Fact]
    public async Task Customer_Revision_Specificity()
    {
        // GE Aviation has BRG-6207-2RS at two revisions concurrently: R2 (legacy
        // platform still using) + R3 (new platform). Revision-specific lookup.
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345", "R2", "R2 legacy", "DWG-A12345", "R2", null, "GE-ECO-2024-088", null, "qm", CancellationToken.None);
        await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345", "R3", "R3 new platform", "DWG-A12345", "R3", null, "GE-ECO-2026-118", null, "qm", CancellationToken.None);

        var r2 = await svc.ResolveByCustomerPnAsync(42, "GEAV-BRG-A12345", "R2", null, CancellationToken.None);
        var r3 = await svc.ResolveByCustomerPnAsync(42, "GEAV-BRG-A12345", "R3", null, CancellationToken.None);

        Assert.NotNull(r2);
        Assert.NotNull(r3);
        Assert.NotEqual(r2!.Id, r3!.Id);
        Assert.Equal("GE-ECO-2024-088", r2.CustomerEcoNumber);
        Assert.Equal("GE-ECO-2026-118", r3.CustomerEcoNumber);
    }

    [Fact]
    public async Task Supersede_Closes_Old_And_Creates_New_Linked_Pair()
    {
        // BAR-1018: Boeing's BAMS-3320 spec was revised from Rev A to Rev B.
        await using var db = NewDb();
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var r2 = await svc.AddXrefAsync(9270, 51, "BA-MATL-1018-CR-RD-1.5", "A", "Boeing 1018 CR bar A-rev", "BAMS-3320", "A", "BAMS-3320", "BA-ECO-2025-0431", null, "qm", CancellationToken.None);
        var r3 = await svc.SupersedeAsync(r2.Id, "B", "BA-ECO-2026-0118", "B", "Spec uplift — heat-trace requirement tightened", "qm", CancellationToken.None);

        // Re-fetch r2 from DB to see the superseded flip.
        var r2Refresh = await db.CustomerItemXrefs.AsNoTracking().FirstAsync(x => x.Id == r2.Id);

        Assert.Equal(CustomerXrefStatus.Superseded, r2Refresh.Status);
        Assert.Equal(r3.Id, r2Refresh.SupersededByXrefId);
        Assert.NotNull(r2Refresh.EffectiveToUtc);

        Assert.Equal(CustomerXrefStatus.Active, r3.Status);
        Assert.Equal("B", r3.CustomerRevision);
        Assert.Equal("BA-ECO-2026-0118", r3.CustomerEcoNumber);

        // Lookup without revision parameter returns only the Active row.
        var current = await svc.ResolveByCustomerPnAsync(51, "BA-MATL-1018-CR-RD-1.5", null, null, CancellationToken.None);
        Assert.NotNull(current);
        Assert.Equal(r3.Id, current!.Id);
    }

    [Fact]
    public async Task Obsoleted_Xref_Excluded_From_Default_Resolution()
    {
        await using var db = NewDb();
        db.Items.Add(EndMill());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var x = await svc.AddXrefAsync(9302, 63, "PW-TOOL-EM-4F-8-C2", "A", "Pratt & Whitney 8mm end mill", null, null, null, null, null, "buyer", CancellationToken.None);
        await svc.ObsoleteAsync(x.Id, "PW EOL'd this tooling spec 2026-Q2", "qm", CancellationToken.None);

        var hit = await svc.ResolveByCustomerPnAsync(63, "PW-TOOL-EM-4F-8-C2", "A", null, CancellationToken.None);
        Assert.Null(hit);
    }

    [Fact]
    public async Task Idempotent_Add_Same_Item_Is_NoOp()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var r1 = await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345", "R3", null, null, null, null, null, null, "buyer", CancellationToken.None);
        var r2 = await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345", "R3", null, null, null, null, null, null, "buyer", CancellationToken.None);
        Assert.Equal(r1.Id, r2.Id);
    }

    [Fact]
    public async Task Add_Different_Item_For_Same_CustomerPn_Throws()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345", "R3", null, null, null, null, null, null, "buyer", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await svc.AddXrefAsync(9270, 42, "GEAV-BRG-A12345", "R3", null, null, null, null, null, null, "buyer", CancellationToken.None));
    }

    [Fact]
    public async Task NullSafe_Uniqueness_When_Revision_Null()
    {
        // PR-FS-5 lesson applied: service-side NULL-safe uniqueness catches
        // duplicates even when CustomerRevision IS NULL (Postgres unique index
        // alone wouldn't catch this).
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345", null /* no revision */, null, null, null, null, null, null, "buyer", CancellationToken.None);

        // Second add with same (Customer, PN, null-revision) pointing to a different Item → throws.
        db.Items.Add(Bar1018());
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await svc.AddXrefAsync(9270, 42, "GEAV-BRG-A12345", null, null, null, null, null, null, null, "buyer", CancellationToken.None));
    }

    [Fact]
    public async Task ResolveByCustomerPn_Returns_Null_For_Unknown()
    {
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        var hit = await svc.ResolveByCustomerPnAsync(99 /* unknown customer */, "DOES-NOT-EXIST", null, null, CancellationToken.None);
        Assert.Null(hit);
    }

    [Fact]
    public async Task GetAllForItem_Returns_AllCustomers()
    {
        // BRG is used by three customers — return all xrefs.
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345", "R3", "GE Aviation",      null, null, null, null, null, "buyer", CancellationToken.None);
        await svc.AddXrefAsync(9245, 51, "BA-BRG-A12345",   "B",  "Boeing",            null, null, null, null, null, "buyer", CancellationToken.None);
        await svc.AddXrefAsync(9245, 63, "PW-BRG-A12345",   "A",  "Pratt & Whitney",   null, null, null, null, null, "buyer", CancellationToken.None);

        var all = await svc.GetAllForItemAsync(9245, includeObsolete: false, CancellationToken.None);
        Assert.Equal(3, all.Count);
        Assert.Contains(all, x => x.CustomerId == 42 && x.CustomerPartNumber == "GEAV-BRG-A12345");
        Assert.Contains(all, x => x.CustomerId == 51 && x.CustomerPartNumber == "BA-BRG-A12345");
        Assert.Contains(all, x => x.CustomerId == 63 && x.CustomerPartNumber == "PW-BRG-A12345");
    }

    [Fact]
    public async Task GetAllForCustomer_Returns_AllItems()
    {
        // GE Aviation has multiple Items mapped: BRG + BAR + EM.
        await using var db = NewDb();
        db.Items.Add(BearingBrg6207());
        db.Items.Add(Bar1018());
        db.Items.Add(EndMill());
        await db.SaveChangesAsync();

        var svc = NewService(db);
        await svc.AddXrefAsync(9245, 42, "GEAV-BRG-A12345",      "R3", null, null, null, null, null, null, "buyer", CancellationToken.None);
        await svc.AddXrefAsync(9270, 42, "GEAV-MATL-1018-RD1.5", "C",  null, null, null, null, null, null, "buyer", CancellationToken.None);
        await svc.AddXrefAsync(9302, 42, "GEAV-TOOL-EM4F-8",     "A",  null, null, null, null, null, null, "buyer", CancellationToken.None);

        var all = await svc.GetAllForCustomerAsync(42, includeObsolete: false, CancellationToken.None);
        Assert.Equal(3, all.Count);
    }
}
