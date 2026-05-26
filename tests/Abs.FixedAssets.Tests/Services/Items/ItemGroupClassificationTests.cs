// B6 Foundation Sprint PR-FS-1 (2026-05-26) — Tests for Item.ItemGroupId wire-up.
// HOTFIX PR-FS-1.5.1 (2026-05-26) — Updated to Source-aware resolver assertions.
//
// Verifies:
//   1. Create with Source=Internal AND no ItemGroupId → REJECTS with validation error.
//   2. Create with Source=Internal AND explicit ItemGroupId → persists + classifies correctly.
//   3. Create with Source=ExternalERP AND no ItemGroupId → SUCCEEDS (legacy import path).
//   4. Update with no ItemGroupId provided → preserves existing classification.
//   5. Update with explicit ItemGroupId → re-classifies the Item.
//   6. IItemGroupResolver.ResolveByCodeAsync returns the right system Id; unknown code returns null.
//   7. PR-FS-1.5.1: Source-aware dispatch — Part+ExternalERP→RAW, Part+Internal→SUBASSY,
//      Part+Synced→RAW, Kit+ExternalERP→RAW, Kit+Internal→SUBASSY.
//   8. PR-FS-1.5.1: Type-only dispatch — Tool→TOOLING, Fastener→RAW, Consumable→CONSUMABLE
//      regardless of Source.

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
/// Sprint B6 Foundation PR-FS-1 + PR-FS-1.5.1 — ItemGroupId classification tests.
/// </summary>
public class ItemGroupClassificationTests
{
    private const int TenantCompanyId = 100;

    // PR-FS-1.5.1 — SYSTEM ItemGroup Ids used across the assertions.
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

        // SYSTEM ItemGroups mirror the PRA-7 seeds. PR-FS-1.5.1 adds SUBASSY (Id=11)
        // to support the new Source-aware Internal → SUBASSY dispatch.
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

        var req = BuildCreateRequest(itemGroupId: FgId, source: ItemMasterSource.Internal);
        var result = await svc.CreateItemAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(FgId, result.Value!.ItemGroupId);
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

        var createReq = BuildCreateRequest(itemGroupId: FgId, source: ItemMasterSource.Internal);
        var created = await svc.CreateItemAsync(createReq, CancellationToken.None);
        Assert.True(created.IsSuccess);
        var itemId = created.Value!.Id;

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
            ItemGroupId: null);

        var updated = await svc.UpdateItemAsync(updateReq, CancellationToken.None);
        Assert.True(updated.IsSuccess, updated.Error);
        Assert.Equal(FgId, updated.Value!.ItemGroupId);
    }

    [Fact]
    public async Task UpdateItem_With_New_ItemGroupId_Reclassifies_The_Item()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var svc = NewItemMaster(db);

        var createReq = BuildCreateRequest(itemGroupId: FgId, source: ItemMasterSource.Internal);
        var created = await svc.CreateItemAsync(createReq, CancellationToken.None);
        Assert.True(created.IsSuccess);
        var itemId = created.Value!.Id;

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
            ItemGroupId: RawId);

        var updated = await svc.UpdateItemAsync(updateReq, CancellationToken.None);
        Assert.True(updated.IsSuccess, updated.Error);
        Assert.Equal(RawId, updated.Value!.ItemGroupId);
    }

    [Fact]
    public async Task ItemGroupResolver_ResolveByCode_Returns_Matching_Id_And_Null_For_Unknown()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        // Known codes.
        Assert.Equal(RawId,     await resolver.ResolveByCodeAsync("RAW", CancellationToken.None));
        Assert.Equal(FgId,      await resolver.ResolveByCodeAsync("FG", CancellationToken.None));
        Assert.Equal(FgId,      await resolver.ResolveByCodeAsync("fg", CancellationToken.None));     // case-insensitive
        Assert.Equal(ConsumeId, await resolver.ResolveByCodeAsync("CONSUMABLE", CancellationToken.None));
        Assert.Equal(SubAssyId, await resolver.ResolveByCodeAsync("SUBASSY", CancellationToken.None));

        // Unknown code.
        Assert.Null(await resolver.ResolveByCodeAsync("DOES-NOT-EXIST", CancellationToken.None));
        Assert.Null(await resolver.ResolveByCodeAsync("", CancellationToken.None));
        Assert.Null(await resolver.ResolveByCodeAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task Resolver_Part_ExternalERP_Maps_To_RAW()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        var id = await resolver.ResolveDefaultForItemAsync(
            ItemType.Part, ItemMasterSource.ExternalERP, CancellationToken.None);

        Assert.Equal(RawId, id);
    }

    [Fact]
    public async Task Resolver_Part_Synced_Maps_To_RAW()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        var id = await resolver.ResolveDefaultForItemAsync(
            ItemType.Part, ItemMasterSource.Synced, CancellationToken.None);

        Assert.Equal(RawId, id);
    }

    [Fact]
    public async Task Resolver_Part_Internal_Maps_To_SUBASSY()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        var id = await resolver.ResolveDefaultForItemAsync(
            ItemType.Part, ItemMasterSource.Internal, CancellationToken.None);

        Assert.Equal(SubAssyId, id);
    }

    [Fact]
    public async Task Resolver_Kit_Branches_On_Source()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        Assert.Equal(RawId,     await resolver.ResolveDefaultForItemAsync(ItemType.Kit, ItemMasterSource.ExternalERP, CancellationToken.None));
        Assert.Equal(SubAssyId, await resolver.ResolveDefaultForItemAsync(ItemType.Kit, ItemMasterSource.Internal,    CancellationToken.None));
    }

    [Fact]
    public async Task Resolver_Non_Part_Types_Ignore_Source()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        // Tool routes to TOOLING regardless of Source.
        Assert.Equal(ToolingId, await resolver.ResolveDefaultForItemAsync(ItemType.Tool, ItemMasterSource.Internal, CancellationToken.None));
        Assert.Equal(ToolingId, await resolver.ResolveDefaultForItemAsync(ItemType.Tool, ItemMasterSource.ExternalERP, CancellationToken.None));

        // Fastener routes to RAW regardless of Source.
        Assert.Equal(RawId, await resolver.ResolveDefaultForItemAsync(ItemType.Fastener, ItemMasterSource.Internal, CancellationToken.None));
        Assert.Equal(RawId, await resolver.ResolveDefaultForItemAsync(ItemType.Fastener, ItemMasterSource.ExternalERP, CancellationToken.None));

        // Consumable routes to CONSUMABLE.
        Assert.Equal(ConsumeId, await resolver.ResolveDefaultForItemAsync(ItemType.Consumable, ItemMasterSource.Internal, CancellationToken.None));
        Assert.Equal(ConsumeId, await resolver.ResolveDefaultForItemAsync(ItemType.Consumable, ItemMasterSource.ExternalERP, CancellationToken.None));

        // Service routes to SERVICE.
        Assert.Equal(ServiceId, await resolver.ResolveDefaultForItemAsync(ItemType.Service, ItemMasterSource.Internal, CancellationToken.None));
    }

    [Fact]
    public async Task Resolver_PartInternal_IsSellableTrue_Routes_To_FG_PRFS7_Tightening()
    {
        // PR-FS-7 tightening: when Item.IsSellable=true AND Part+Internal,
        // resolver routes to FG (truly sellable internal item). When IsSellable
        // is false, the SUBASSY default from PR-FS-1.5.1 still applies.
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        var sellableInternalPart = new Item
        {
            Id = 9500,
            PartNumber = "ASM-TRENT-BRACKET-A",
            Description = "Precision-machined engine bracket assembly — internally manufactured sellable FG",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.Internal,
            IsSellable = true,
            IsActive = true,
        };

        var subassemblyInternalPart = new Item
        {
            Id = 9501,
            PartNumber = "SUBASM-TRENT-BRACKET-RIB",
            Description = "Internally machined rib subassembly consumed by the bracket FG",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.Internal,
            IsSellable = false,
            IsActive = true,
        };

        var sellableGroup = await resolver.ResolveDefaultForItemAsync(sellableInternalPart, CancellationToken.None);
        Assert.Equal(FgId, sellableGroup);

        var subassyGroup = await resolver.ResolveDefaultForItemAsync(subassemblyInternalPart, CancellationToken.None);
        Assert.Equal(SubAssyId, subassyGroup);
    }

    [Fact]
    public async Task Resolver_NewOverload_Falls_Through_To_SourceAware_When_Not_PartInternalSellable()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        // BRG-6207-2RS — purchased bearing, Source=ExternalERP, IsSellable=false (default).
        // New overload should match the old Source-aware path: Part+ExternalERP → RAW.
        var purchasedPart = new Item
        {
            Id = 9245,
            PartNumber = "BRG-6207-2RS",
            Description = "Ball Bearing 35x72x17mm Sealed",
            StockUOM = "EA",
            Type = ItemType.Part,
            Source = ItemMasterSource.ExternalERP,
            IsSellable = false,
            IsActive = true,
        };
        var groupId = await resolver.ResolveDefaultForItemAsync(purchasedPart, CancellationToken.None);
        Assert.Equal(RawId, groupId);

        // Tool type — IsSellable irrelevant, routes via Type-only branch.
        var tool = new Item
        {
            Id = 9302,
            PartNumber = "EM-4FL-8MM",
            Description = "8mm 4-Flute Square End Mill Carbide",
            StockUOM = "EA",
            Type = ItemType.Tool,
            Source = ItemMasterSource.ExternalERP,
            IsSellable = true, // even with sellable flag, Tool routes to TOOLING
            IsActive = true,
        };
        var toolGroup = await resolver.ResolveDefaultForItemAsync(tool, CancellationToken.None);
        Assert.Equal(ToolingId, toolGroup);
    }

    [Fact]
    public async Task Resolver_GetByIdAsync_Projects_Code()
    {
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        var raw = await resolver.GetByIdAsync(RawId, CancellationToken.None);
        Assert.Equal(RawId, raw.Id);
        Assert.Equal("RAW", raw.Code);

        var fg = await resolver.GetByIdAsync(FgId, CancellationToken.None);
        Assert.Equal("FG", fg.Code);

        var none = await resolver.GetByIdAsync(99999, CancellationToken.None);
        Assert.Null(none.Id);
        Assert.Null(none.Code);

        var nullArg = await resolver.GetByIdAsync(null, CancellationToken.None);
        Assert.Null(nullArg.Id);
    }

    [Fact]
    public async Task Resolver_Never_Defaults_To_FG()
    {
        // FG must never be a DEFAULT — it requires explicit operator classification.
        // Any (Type, Source) combination resolved through the convention map must
        // land somewhere other than FG.
        await using var db = NewDb();
        await EnsureFixturesAsync(db);
        var resolver = NewResolver(db);

        foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
        {
            foreach (ItemMasterSource source in Enum.GetValues(typeof(ItemMasterSource)))
            {
                var id = await resolver.ResolveDefaultForItemAsync(type, source, CancellationToken.None);
                Assert.True(id != FgId,
                    $"Resolver default for (Type={type}, Source={source}) was FG ({FgId}) — FG must never be a default classification.");
            }
        }
    }
}
