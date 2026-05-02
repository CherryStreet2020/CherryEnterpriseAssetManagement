using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Services.Testing;

public interface ISmokeTestDataFactory
{
    int GetTenantId();
    Task<Vendor> CreateVendorAsync(string name, string? vendorCode = null);
    Task<Item> CreateItemAsync(string partNumber, string description, ItemType type = ItemType.Part, string stockUom = "EA");
    Task<ItemApprovedVendor> CreateAvlAsync(int itemId, int vendorId, bool isPreferred = false, AvlApprovalStatus status = AvlApprovalStatus.Approved, string? notes = null);
    Task<ItemAlternate> CreateItemAlternateAsync(int itemId, int alternateItemId, int rank = 1, string? reason = null);
    Task<ItemSupersession> CreateItemSupersessionAsync(int oldItemId, int newItemId, string? reason = null);
    Task<VendorItemPart> CreateVendorItemPartAsync(int itemId, int vendorId, string vendorPartNumber, string? catalogUrl = null, string? datasheetUrl = null, string? externalImageUrl = null, bool isPreferred = false);
    Task<Manufacturer> CreateManufacturerAsync(string name);
}

public class SmokeTestDataFactory : ISmokeTestDataFactory
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SmokeTestDataFactory(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public int GetTenantId()
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null || tenantId == 0)
        {
            tenantId = 1;
        }
        return tenantId.Value;
    }

    public async Task<Vendor> CreateVendorAsync(string name, string? vendorCode = null)
    {
        var vendor = new Vendor
        {
            Name = name,
            Code = vendorCode ?? $"VSMK{DateTime.UtcNow.Ticks % 1000000:000000}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();
        return vendor;
    }

    public async Task<Item> CreateItemAsync(string partNumber, string description, ItemType type = ItemType.Part, string stockUom = "EA")
    {
        var item = new Item
        {
            PartNumber = partNumber,
            Description = description,
            Type = type,
            StockUOM = stockUom,
            IsActive = true
        };
        _db.Items.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<ItemApprovedVendor> CreateAvlAsync(int itemId, int vendorId, bool isPreferred = false, AvlApprovalStatus status = AvlApprovalStatus.Approved, string? notes = null)
    {
        var tenantId = GetTenantId();
        var avl = new ItemApprovedVendor
        {
            ItemId = itemId,
            VendorId = vendorId,
            IsPreferred = isPreferred,
            ApprovalStatus = status,
            Notes = notes,
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ItemApprovedVendors.Add(avl);
        await _db.SaveChangesAsync();
        return avl;
    }

    public async Task<ItemAlternate> CreateItemAlternateAsync(int itemId, int alternateItemId, int rank = 1, string? reason = null)
    {
        var tenantId = GetTenantId();
        var alternate = new ItemAlternate
        {
            ItemId = itemId,
            AlternateItemId = alternateItemId,
            Rank = rank,
            Reason = reason,
            TenantId = tenantId,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.ItemAlternates.Add(alternate);
        await _db.SaveChangesAsync();
        return alternate;
    }

    public async Task<ItemSupersession> CreateItemSupersessionAsync(int oldItemId, int newItemId, string? reason = null)
    {
        var tenantId = GetTenantId();
        var supersession = new ItemSupersession
        {
            OldItemId = oldItemId,
            NewItemId = newItemId,
            Reason = reason,
            TenantId = tenantId,
            EffectiveFromUtc = DateTime.UtcNow
        };
        _db.ItemSupersessions.Add(supersession);
        await _db.SaveChangesAsync();
        return supersession;
    }

    public async Task<VendorItemPart> CreateVendorItemPartAsync(
        int itemId, 
        int vendorId, 
        string vendorPartNumber, 
        string? catalogUrl = null, 
        string? datasheetUrl = null, 
        string? externalImageUrl = null,
        bool isPreferred = false)
    {
        var vpn = new VendorItemPart
        {
            ItemId = itemId,
            VendorId = vendorId,
            VendorPartNumber = vendorPartNumber,
            CatalogUrl = catalogUrl,
            DatasheetUrl = datasheetUrl,
            ExternalImageUrl = externalImageUrl,
            Preferred = isPreferred,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.VendorItemParts.Add(vpn);
        await _db.SaveChangesAsync();
        return vpn;
    }

    public async Task<Manufacturer> CreateManufacturerAsync(string name)
    {
        var mfr = new Manufacturer
        {
            Code = $"MFR{DateTime.UtcNow.Ticks % 1000000:000000}",
            Name = name,
            Active = true
        };
        _db.Manufacturers.Add(mfr);
        await _db.SaveChangesAsync();
        return mfr;
    }
}
