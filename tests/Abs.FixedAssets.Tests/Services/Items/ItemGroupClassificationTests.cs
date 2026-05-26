// B6 Foundation Sprint PR-FS-1 (2026-05-26) — Tests for Item.ItemGroupId wire-up.
//
// Verifies:
//   1. Create with Source=Internal AND no ItemGroupId → REJECTS with validation error.
//   2. Create with Source=Internal AND explicit ItemGroupId → persists + classifies correctly.
//   3. Create with Source=ExternalERP AND no ItemGroupId → SUCCEEDS (legacy import path).
//   4. Update with no ItemGroupId provided → preserves existing classification.
//   5. Update with explicit ItemGroupId → re-classifies the Item.
//   6. IItemGroupResolver.ResolveByCodeAsync returns the right system Id; unknown code returns null.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Items;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests.Services.Items;

/// <summary>
/// Sprint B6 Foundation PR-FS-1 — ItemGroupId classification tests.
/// </summary>
public class ItemGroupClassificationTests
{
    private const int TenantCompanyId = 100;

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
            .UseInMemoryDatabase($"itemgroup-classification-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public int? TenantId { get; init; } = 1;
        public int? CompanyId { get; init; }
        public int? SiteId { get; init; }
        public int? AssignedCompanyId { get; init; }
        public int? AssignedSiteId { get; init; }
        public List<int> VisibleCompanyIds { get; init; } = new();
        public List<int> VisibleSiteIds { get; init; } = new();
        public bool IsResolved => true;
        public string? ResolutionError => null;
        public void SetContext(int? tenantId, int? companyId, int? siteId) { }
        public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds) { }
        public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds) { }
        public void SetError(string error) { }
    }

    private static ITenantContext TenantA() => new StubTenantContext
    {
        CompanyId = TenantCompanyId,
        VisibleCompanyIds = new() { TenantCompanyId }
    };

    private static ILookupService NewLookup(AppDbContext db) =>
        new LookupService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<LookupService>.Instance);

    private static async Task EnsureFixturesAsync(AppDbContext db)
    {
        db.Companies.Add(new Company
        {
            Id = TenantCompanyId,
            CompanyCode = "PWH-CAN",
            Name = "PWH MANUFACTURING CANADA",
            IsActive = true,
        });

        // Minimal SYSTEM ItemGroups (mirror the 12 PRA-7 seeds — we only seed the ones the tests reference).
        db.Set<ItemGroup>().AddRange(
            new ItemGroup { Id = 1, Code = "RAW",        Name = "Raw Material",   GroupType = ItemGroupType.RawMaterial,    IsSystem = true, IsActive = true },
            new ItemGroup { Id = 3, Code = "FG",         Name = "Finished Goods", GroupType = ItemGroupType.FinishedGoods,  IsSystem = true, IsActive = true },
            new ItemGroup { Id = 4, Code = "CONSUMABLE", Name = "Consumable",     GroupType = ItemGroupType.Consumable,     IsSystem = true, IsActive = true },
            new ItemGroup { Id = 5, Code = "SERVICE",    Name = "Service",        GroupType = ItemGroupType.Service,        IsSystem = true, IsActive = true },
            new ItemGroup { Id = 9, Code = "TOOLING",    Name = "Tooling",        GroupType = ItemGroupType.Tooling,        IsSystem = true, IsActive = true }
        );
        await db.SaveChangesAsync();
    }

    private static ItemMasterService NewItemMaster(AppDbContext db) =>
        new(db, TenantA(), NewLookup(db),
            new Abs.FixedAssets.Tests.TestHelpers.NullChainOfCustodyService(),
            NullLogger<ItemMasterService>.Instance);

    private static ItemGroupResolver NewResolver(AppDbContext db) =>
        new(db, NullLogger<ItemGroupResolver>.Instance);

    private static CreateItemRequest BuildCreateRequest(int? itemGroupId, ItemMasterSource source) =>
        new CreateItemRequest(
            PartNumber: $"TEST-{Guid.NewGuid():N}",
            TypeLookupValueId: null,
            Description: "Test item",
            ExtendedDescription: null,
            StockUom: "EA",
            IsActive: true,
            LeadTimeDays: null,
            MinOrderQty: null,
            OrderMultiple: null,
            PurchaseUom: null,
            PackQty: null,
            StockPolicy: StockPolicy.Stock,
            LastPrice: null,
            CurrencyCode: null,
            PriceEffectiveDate: null,
            ContractFlag: false,
            ContractRef: null,
            StatusLookupValueId: null,
            CostMethodLookupValueId: null,
            TrackingTypeLookupValueId: null,
            StandardCost: null,
            DefaultLocationId: null,
            ItemGroupId: itemGroupId,
            Source: source);

    [Fact]
    public async Task CreateItem_Source_Internal_Without_ItemGroupId_Is_Rejected()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var svc = NewItemMaster(db);

        var req = BuildCreateRequest(itemGroupId: null, source: ItemMasterSource.Internal);
        var result = await svc.CreateItemAsync(req, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("ItemGroupId is required", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateItem_Source_Internal_With_Explicit_ItemGroupId_Persists()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var svc = NewItemMaster(db);

        var req = BuildCreateRequest(itemGroupId: 3 /* FG */, source: ItemMasterSource.Internal);
        var result = await svc.CreateItemAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(3, result.Value!.ItemGroupId);
        Assert.Equal(ItemMasterSource.Internal, result.Value!.Source);
    }

    [Fact]
    public async Task CreateItem_Source_ExternalERP_Without_ItemGroupId_Allowed()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var svc = NewItemMaster(db);

        var req = BuildCreateRequest(itemGroupId: null, source: ItemMasterSource.ExternalERP);
        var result = await svc.CreateItemAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Null(result.Value!.ItemGroupId);
        Assert.Equal(ItemMasterSource.ExternalERP, result.Value!.Source);
    }

    [Fact]
    public async Task UpdateItem_Without_ItemGroupId_Preserves_Existing_Classification()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var svc = NewItemMaster(db);

        // Create with ItemGroupId=3.
        var createReq = BuildCreateRequest(itemGroupId: 3, source: ItemMasterSource.Internal);
        var created = await svc.CreateItemAsync(createReq, CancellationToken.None);
        Assert.True(created.IsSuccess);
        var itemId = created.Value!.Id;

        // Update with NO ItemGroupId — must preserve.
        var updateReq = new UpdateItemRequest(
            ItemId: itemId,
            PartNumber: createReq.PartNumber,
            TypeLookupValueId: null,
            Description: "Updated description",
            ExtendedDescription: null,
            StockUom: "EA",
            IsActive: true,
            LeadTimeDays: null,
            MinOrderQty: null,
            OrderMultiple: null,
            PurchaseUom: null,
            PackQty: null,
            StockPolicy: StockPolicy.Stock,
            LastPrice: null,
            CurrencyCode: null,
            PriceEffectiveDate: null,
            ContractFlag: false,
            ContractRef: null,
            StatusLookupValueId: null,
            CostMethodLookupValueId: null,
            TrackingTypeLookupValueId: null,
            StandardCost: null,
            DefaultLocationId: null,
            ItemGroupId: null);  // No change

        var updated = await svc.UpdateItemAsync(updateReq, CancellationToken.None);
        Assert.True(updated.IsSuccess, updated.Error);
        Assert.Equal(3, updated.Value!.ItemGroupId);  // Preserved
    }

    [Fact]
    public async Task UpdateItem_With_New_ItemGroupId_Reclassifies_The_Item()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var svc = NewItemMaster(db);

        // Create with ItemGroupId=3 (FG).
        var createReq = BuildCreateRequest(itemGroupId: 3, source: ItemMasterSource.Internal);
        var created = await svc.CreateItemAsync(createReq, CancellationToken.None);
        Assert.True(created.IsSuccess);
        var itemId = created.Value!.Id;

        // Update with ItemGroupId=1 (RAW) — must re-classify.
        var updateReq = new UpdateItemRequest(
            ItemId: itemId,
            PartNumber: createReq.PartNumber,
            TypeLookupValueId: null,
            Description: createReq.Description,
            ExtendedDescription: null,
            StockUom: "EA",
            IsActive: true,
            LeadTimeDays: null,
            MinOrderQty: null,
            OrderMultiple: null,
            PurchaseUom: null,
            PackQty: null,
            StockPolicy: StockPolicy.Stock,
            LastPrice: null,
            CurrencyCode: null,
            PriceEffectiveDate: null,
            ContractFlag: false,
            ContractRef: null,
            StatusLookupValueId: null,
            CostMethodLookupValueId: null,
            TrackingTypeLookupValueId: null,
            StandardCost: null,
            DefaultLocationId: null,
            ItemGroupId: 1);  // Reclassify to RAW

        var updated = await svc.UpdateItemAsync(updateReq, CancellationToken.None);
        Assert.True(updated.IsSuccess, updated.Error);
        Assert.Equal(1, updated.Value!.ItemGroupId);
    }

    [Fact]
    public async Task ItemGroupResolver_ResolveByCode_Returns_Matching_Id_And_Null_For_Unknown()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        // Known codes.
        Assert.Equal(1, await resolver.ResolveByCodeAsync("RAW", CancellationToken.None));
        Assert.Equal(3, await resolver.ResolveByCodeAsync("FG", CancellationToken.None));
        Assert.Equal(3, await resolver.ResolveByCodeAsync("fg", CancellationToken.None));     // case-insensitive
        Assert.Equal(4, await resolver.ResolveByCodeAsync("CONSUMABLE", CancellationToken.None));

        // Unknown code.
        Assert.Null(await resolver.ResolveByCodeAsync("DOES-NOT-EXIST", CancellationToken.None));
        Assert.Null(await resolver.ResolveByCodeAsync("", CancellationToken.None));
        Assert.Null(await resolver.ResolveByCodeAsync(null!, CancellationToken.None));

        // Type-based resolution (Part → FG default).
        Assert.Equal(3, await resolver.ResolveDefaultForItemTypeAsync(ItemType.Part, CancellationToken.None));
        Assert.Equal(4, await resolver.ResolveDefaultForItemTypeAsync(ItemType.Consumable, CancellationToken.None));
        Assert.Equal(9, await resolver.ResolveDefaultForItemTypeAsync(ItemType.Tool, CancellationToken.None));
    }
}
