// B6 Foundation Sprint PR-FS-2 (2026-05-26) — ItemSiteResolver tests.
//
// Verifies the ItemSite → Item → default cascade:
//   1. Item-only (no ItemSite row) → resolver returns Item values, all sources "Item".
//   2. SiteId=null → resolver skips the ItemSite lookup entirely, returns Item values.
//   3. ItemSite row present with one override field → that field resolves from ItemSite,
//      all others from Item.
//   4. ItemSite row present with ALL override fields → every field resolves from ItemSite.
//   5. Item not found → resolver returns null.
//   6. IsActive cascade: Item.IsActive=true + ItemSite.IsActive=false → Inactive (source: ItemSite).
//   7. IsActive cascade: Item.IsActive=false + ItemSite.IsActive=true → Inactive (source: Item).
//   8. GetOverrideAsync returns the raw ItemSite row.
//   9. Unique constraint on (TenantId, ItemId, SiteId) — sanity check.

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

public class ItemSiteResolverTests
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
            .UseInMemoryDatabase($"item-site-resolver-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static ItemSiteResolver NewResolver(AppDbContext db) =>
        new(db, NullLogger<ItemSiteResolver>.Instance);

    private static Item NewTestItem(int id = 100) => new()
    {
        Id = id,
        PartNumber = $"TEST-{id}",
        Description = "Test Item",
        StockUOM = "EA",
        Type = ItemType.Part,
        Source = ItemMasterSource.ExternalERP,
        IsActive = true,
        IsStocked = true,
        IsPurchasable = true,
        IsCriticalSpare = false,
        StockPolicy = StockPolicy.Stock,
        ABCClass = ABCClassification.B,
        MinQuantity = 10m,
        MaxQuantity = 100m,
        ReorderPoint = 25m,
        ReorderQuantity = 50m,
        SafetyStock = 5m,
        LeadTimeDays = 7,
        ReorderMethod = ReorderMethod.ReorderPoint,
        AutoReorderEnabled = false,
        CostMethod = CostMethod.Average,
        StandardCost = 100m,
        AverageCost = 105m,
        LastPurchaseCost = 110m,
        ListPrice = 200m,
        TrackingType = TrackingType.None,
        IsHazmat = false,
        Warehouse = "WH-001",
    };

    [Fact]
    public async Task ResolveEffective_NoSiteOverride_Returns_Item_Values()
    {
        await using var db = NewDb();
        db.Items.Add(NewTestItem(100));
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var eff = await resolver.ResolveEffectiveAsync(100, siteId: 1, CancellationToken.None);

        Assert.NotNull(eff);
        Assert.Equal(100, eff!.ItemId);
        Assert.Equal(1, eff.SiteId);
        Assert.Equal("TEST-100", eff.PartNumber);
        Assert.Equal(25m, eff.ReorderPoint);   // From Item
        Assert.Equal("Item", eff.OverrideSource["ReorderPoint"]);
        Assert.Equal("Item", eff.OverrideSource["LeadTimeDays"]);
        Assert.Equal("Item", eff.OverrideSource["StandardCost"]);
    }

    [Fact]
    public async Task ResolveEffective_NullSiteId_Skips_ItemSite_Lookup()
    {
        await using var db = NewDb();
        db.Items.Add(NewTestItem(100));
        // Even with an ItemSite row present, resolver with siteId=null must ignore it.
        db.ItemSites.Add(new ItemSite { ItemId = 100, SiteId = 1, ReorderPoint = 999m, IsActive = true });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var eff = await resolver.ResolveEffectiveAsync(100, siteId: null, CancellationToken.None);

        Assert.NotNull(eff);
        Assert.Null(eff!.SiteId);
        Assert.Equal(25m, eff.ReorderPoint);   // Item value, not the 999 override
        Assert.Equal("Item", eff.OverrideSource["ReorderPoint"]);
    }

    [Fact]
    public async Task ResolveEffective_SingleFieldOverride_Cascades_Correctly()
    {
        await using var db = NewDb();
        db.Items.Add(NewTestItem(100));
        db.ItemSites.Add(new ItemSite
        {
            ItemId = 100,
            SiteId = 5,
            IsActive = true,
            ReorderPoint = 999m,   // override
            // Other override fields left null → fall through to Item
        });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var eff = await resolver.ResolveEffectiveAsync(100, siteId: 5, CancellationToken.None);

        Assert.NotNull(eff);
        Assert.Equal(999m, eff!.ReorderPoint);                       // From ItemSite
        Assert.Equal("ItemSite", eff.OverrideSource["ReorderPoint"]);

        // All other fields still from Item.
        Assert.Equal(7, eff.LeadTimeDays);
        Assert.Equal("Item", eff.OverrideSource["LeadTimeDays"]);
        Assert.Equal(100m, eff.StandardCost);
        Assert.Equal("Item", eff.OverrideSource["StandardCost"]);
    }

    [Fact]
    public async Task ResolveEffective_AllOverrides_Cascades_All_To_ItemSite()
    {
        await using var db = NewDb();
        db.Items.Add(NewTestItem(100));
        db.ItemSites.Add(new ItemSite
        {
            ItemId = 100,
            SiteId = 5,
            IsActive = true,
            ReorderPoint = 999m,
            LeadTimeDays = 14,
            StandardCost = 200m,
            SafetyStock = 50m,
            ABCClass = ABCClassification.A,
            PreferredVendorId = 42,
            DefaultWarehouse = "WH-OVR",
        });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var eff = await resolver.ResolveEffectiveAsync(100, siteId: 5, CancellationToken.None);

        Assert.NotNull(eff);
        Assert.Equal(999m, eff!.ReorderPoint);
        Assert.Equal(14, eff.LeadTimeDays);
        Assert.Equal(200m, eff.StandardCost);
        Assert.Equal(50m, eff.SafetyStock);
        Assert.Equal(ABCClassification.A, eff.ABCClass);
        Assert.Equal(42, eff.PreferredVendorId);
        Assert.Equal("WH-OVR", eff.DefaultWarehouse);

        Assert.Equal("ItemSite", eff.OverrideSource["ReorderPoint"]);
        Assert.Equal("ItemSite", eff.OverrideSource["LeadTimeDays"]);
        Assert.Equal("ItemSite", eff.OverrideSource["StandardCost"]);
        Assert.Equal("ItemSite", eff.OverrideSource["SafetyStock"]);
        Assert.Equal("ItemSite", eff.OverrideSource["ABCClass"]);
        Assert.Equal("ItemSite", eff.OverrideSource["PreferredVendorId"]);
        Assert.Equal("ItemSite", eff.OverrideSource["DefaultWarehouse"]);
    }

    [Fact]
    public async Task ResolveEffective_ItemNotFound_Returns_Null()
    {
        await using var db = NewDb();
        var resolver = NewResolver(db);

        var eff = await resolver.ResolveEffectiveAsync(9999, siteId: 1, CancellationToken.None);

        Assert.Null(eff);
    }

    [Fact]
    public async Task ResolveEffective_IsActive_ItemSite_Suppresses_Item_Active()
    {
        // Item.IsActive=true + ItemSite.IsActive=false → resolved Inactive,
        // source attributed to ItemSite.
        await using var db = NewDb();
        var item = NewTestItem(100);
        item.IsActive = true;
        db.Items.Add(item);
        db.ItemSites.Add(new ItemSite { ItemId = 100, SiteId = 5, IsActive = false });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var eff = await resolver.ResolveEffectiveAsync(100, siteId: 5, CancellationToken.None);

        Assert.NotNull(eff);
        Assert.False(eff!.IsActive);
        Assert.Equal("ItemSite", eff.OverrideSource["IsActive"]);
    }

    [Fact]
    public async Task ResolveEffective_IsActive_Item_Inactive_Always_Wins()
    {
        // Item.IsActive=false + ItemSite.IsActive=true → resolved Inactive,
        // source attributed to Item (override can't reactivate).
        await using var db = NewDb();
        var item = NewTestItem(100);
        item.IsActive = false;
        db.Items.Add(item);
        db.ItemSites.Add(new ItemSite { ItemId = 100, SiteId = 5, IsActive = true });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var eff = await resolver.ResolveEffectiveAsync(100, siteId: 5, CancellationToken.None);

        Assert.NotNull(eff);
        Assert.False(eff!.IsActive);
        Assert.Equal("Item", eff.OverrideSource["IsActive"]);
    }

    [Fact]
    public async Task GetOverride_Returns_Raw_ItemSite_Row()
    {
        await using var db = NewDb();
        db.Items.Add(NewTestItem(100));
        db.ItemSites.Add(new ItemSite
        {
            Id = 50,
            ItemId = 100,
            SiteId = 5,
            IsActive = true,
            ReorderPoint = 999m,
            Notes = "Pilot plant override",
        });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var ov = await resolver.GetOverrideAsync(100, 5, CancellationToken.None);

        Assert.NotNull(ov);
        Assert.Equal(50, ov!.Id);
        Assert.Equal(999m, ov.ReorderPoint);
        Assert.Equal("Pilot plant override", ov.Notes);
    }

    [Fact]
    public async Task GetOverride_Returns_Null_When_No_Override_Exists()
    {
        await using var db = NewDb();
        db.Items.Add(NewTestItem(100));
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var ov = await resolver.GetOverrideAsync(100, 5, CancellationToken.None);

        Assert.Null(ov);
    }

    [Fact]
    public async Task ResolveEffective_DefaultWarehouse_Falls_Back_To_Item_Warehouse()
    {
        // No override on warehouse → cascade picks Item.Warehouse (legacy field).
        await using var db = NewDb();
        var item = NewTestItem(100);
        item.Warehouse = "WH-MAIN";
        db.Items.Add(item);
        db.ItemSites.Add(new ItemSite { ItemId = 100, SiteId = 5, IsActive = true });  // no warehouse override
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var eff = await resolver.ResolveEffectiveAsync(100, siteId: 5, CancellationToken.None);

        Assert.NotNull(eff);
        Assert.Equal("WH-MAIN", eff!.DefaultWarehouse);
        Assert.Equal("Item", eff.OverrideSource["DefaultWarehouse"]);
    }

    [Fact]
    public async Task ResolveEffective_EmptyString_Override_Clears_Inherited_Text()
    {
        // Codex P2 regression guard on PR #358 — empty string MUST be treated as a
        // deliberate override (clear the inherited value), NOT as "no override".
        // Use case: per-Site DefaultWarehouse cleared so receive-flow doesn't default
        // to the Item-level legacy warehouse code at this specific site.
        await using var db = NewDb();
        var item = NewTestItem(100);
        item.Warehouse = "WH-MAIN";
        db.Items.Add(item);
        db.ItemSites.Add(new ItemSite
        {
            ItemId = 100,
            SiteId = 5,
            IsActive = true,
            DefaultWarehouse = "",   // explicit empty — must override Item's WH-MAIN
        });
        await db.SaveChangesAsync();

        var resolver = NewResolver(db);
        var eff = await resolver.ResolveEffectiveAsync(100, siteId: 5, CancellationToken.None);

        Assert.NotNull(eff);
        Assert.Equal("", eff!.DefaultWarehouse);
        Assert.Equal("ItemSite", eff.OverrideSource["DefaultWarehouse"]);
    }
}
