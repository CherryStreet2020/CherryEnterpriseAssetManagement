using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;

namespace Abs.FixedAssets.Services.Items;

public interface IEffectiveProcurementService
{
    Task<EffectiveProcurementValues> GetEffectiveValuesAsync(int itemId);
    Task<Dictionary<int, EffectiveProcurementValues>> GetEffectiveValuesForItemsAsync(IEnumerable<int> itemIds);
    EffectiveProcurementValues GetEffectiveValues(Item item, VendorItemPart? preferredVpn, ItemApprovedVendor? preferredAvl);
}

public class EffectiveProcurementValues
{
    public int ItemId { get; set; }
    
    public int? LeadTimeDays { get; set; }
    public string LeadTimeSource { get; set; } = "None";
    
    public decimal? LastPrice { get; set; }
    public string LastPriceSource { get; set; } = "None";
    
    public string? PurchaseUom { get; set; }
    public string PurchaseUomSource { get; set; } = "None";
    
    public decimal? PackQty { get; set; }
    public string PackQtySource { get; set; } = "None";
    
    public decimal? MinOrderQty { get; set; }
    public string MinOrderQtySource { get; set; } = "None";
    
    public decimal? OrderMultiple { get; set; }
    public string OrderMultipleSource { get; set; } = "None";
    
    public string? CurrencyCode { get; set; }
    public DateTime? PriceEffectiveDate { get; set; }
    
    public int? PreferredVendorId { get; set; }
    public string? PreferredVendorName { get; set; }
    public string? PreferredVendorPartNumber { get; set; }
    public string? PreferredVendorCatalogUrl { get; set; }
    
    public bool HasPreferredVendor => PreferredVendorId.HasValue;
    
    public string GetSourceBadge(string source) => source switch
    {
        "Preferred Vendor" => "badge-primary",
        "Item Default" => "badge-secondary",
        _ => "badge-muted"
    };
}

public class EffectiveProcurementService : IEffectiveProcurementService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public EffectiveProcurementService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<EffectiveProcurementValues> GetEffectiveValuesAsync(int itemId)
    {
        var results = await GetEffectiveValuesForItemsAsync(new[] { itemId });
        return results.TryGetValue(itemId, out var value) ? value : new EffectiveProcurementValues { ItemId = itemId };
    }

    public async Task<Dictionary<int, EffectiveProcurementValues>> GetEffectiveValuesForItemsAsync(IEnumerable<int> itemIds)
    {
        var itemIdList = itemIds.ToList();
        if (!itemIdList.Any()) return new Dictionary<int, EffectiveProcurementValues>();

        var tenantId = _tenantContext.TenantId;

        var items = await _db.Items
            .AsNoTracking()
            .Where(i => itemIdList.Contains(i.Id))
            .ToListAsync();

        var preferredAvls = await _db.ItemApprovedVendors
            .AsNoTracking()
            .Include(a => a.Vendor)
            .Where(a => a.TenantId == tenantId && itemIdList.Contains(a.ItemId) && a.IsPreferred)
            .ToListAsync();

        var preferredVendorIds = preferredAvls
            .Where(a => a.VendorId > 0)
            .ToDictionary(a => a.ItemId, a => a.VendorId);

        var vpnQuery = from vpn in _db.VendorItemParts.AsNoTracking()
                       where itemIdList.Contains(vpn.ItemId) && vpn.IsActive
                       select vpn;
        var allVpns = await vpnQuery.Include(v => v.Vendor).ToListAsync();

        var result = new Dictionary<int, EffectiveProcurementValues>();

        foreach (var item in items)
        {
            var values = new EffectiveProcurementValues { ItemId = item.Id };
            
            VendorItemPart? preferredVpn = null;
            if (preferredVendorIds.TryGetValue(item.Id, out var prefVendorId))
            {
                preferredVpn = allVpns.FirstOrDefault(v => v.ItemId == item.Id && v.VendorId == prefVendorId);
                var prefAvl = preferredAvls.FirstOrDefault(a => a.ItemId == item.Id);
                if (prefAvl?.Vendor != null)
                {
                    values.PreferredVendorId = prefVendorId;
                    values.PreferredVendorName = prefAvl.Vendor.Name;
                }
                if (preferredVpn != null)
                {
                    values.PreferredVendorPartNumber = preferredVpn.VendorPartNumber;
                    values.PreferredVendorCatalogUrl = preferredVpn.ProductPageUrl;
                }
            }

            values.LeadTimeDays = GetEffectiveValue(
                preferredVpn?.LeadTimeDays,
                item.LeadTimeDays > 0 ? item.LeadTimeDays : (int?)null,
                out var leadTimeSource);
            values.LeadTimeSource = leadTimeSource;

            values.LastPrice = GetEffectiveValue(
                preferredVpn?.UnitPrice,
                item.LastPrice,
                out var lastPriceSource);
            values.LastPriceSource = lastPriceSource;

            values.PurchaseUom = GetEffectiveValue(
                preferredVpn?.VendorUom,
                item.PurchaseUOM,
                out var purchaseUomSource);
            values.PurchaseUomSource = purchaseUomSource;

            values.PackQty = GetEffectiveValue(
                preferredVpn?.PackQty,
                item.PackQty,
                out var packQtySource);
            values.PackQtySource = packQtySource;

            values.MinOrderQty = GetEffectiveValue(
                preferredVpn?.MinOrderQty,
                item.MinOrderQty.HasValue ? (decimal?)item.MinOrderQty.Value : null,
                out var minOrderQtySource);
            values.MinOrderQtySource = minOrderQtySource;

            values.OrderMultiple = item.OrderMultiple;
            values.OrderMultipleSource = item.OrderMultiple.HasValue ? "Item Default" : "None";

            values.CurrencyCode = item.CurrencyCode ?? "USD";
            values.PriceEffectiveDate = preferredVpn?.PriceEffectiveDate ?? item.PriceEffectiveDate;

            result[item.Id] = values;
        }

        return result;
    }

    private static T? GetEffectiveValue<T>(T? vendorValue, T? itemValue, out string source)
    {
        if (HasValue(vendorValue))
        {
            source = "Preferred Vendor";
            return vendorValue;
        }
        if (HasValue(itemValue))
        {
            source = "Item Default";
            return itemValue;
        }
        source = "None";
        return default;
    }

    private static bool HasValue<T>(T? value)
    {
        if (value == null) return false;
        if (typeof(T) == typeof(int) && value is int intVal) return intVal != 0;
        if (typeof(T) == typeof(decimal) && value is decimal decVal) return decVal != 0m;
        if (typeof(T) == typeof(string) && value is string strVal) return !string.IsNullOrEmpty(strVal);
        return true;
    }

    public EffectiveProcurementValues GetEffectiveValues(Item item, VendorItemPart? preferredVpn, ItemApprovedVendor? preferredAvl)
    {
        var values = new EffectiveProcurementValues { ItemId = item.Id };

        if (preferredAvl?.Vendor != null)
        {
            values.PreferredVendorId = preferredAvl.VendorId;
            values.PreferredVendorName = preferredAvl.Vendor.Name;
        }
        else if (preferredVpn?.Vendor != null)
        {
            values.PreferredVendorId = preferredVpn.VendorId;
            values.PreferredVendorName = preferredVpn.Vendor.Name;
        }

        if (preferredVpn != null)
        {
            values.PreferredVendorPartNumber = preferredVpn.VendorPartNumber;
            values.PreferredVendorCatalogUrl = preferredVpn.ProductPageUrl;
        }

        values.LeadTimeDays = GetEffectiveValue(
            preferredVpn?.LeadTimeDays,
            item.LeadTimeDays,
            out var leadTimeSource);
        values.LeadTimeSource = leadTimeSource;

        values.LastPrice = GetEffectiveValue(
            preferredVpn?.UnitPrice,
            item.LastPrice,
            out var lastPriceSource);
        values.LastPriceSource = lastPriceSource;

        values.PurchaseUom = GetEffectiveValue(
            preferredVpn?.VendorUom,
            item.PurchaseUOM,
            out var purchaseUomSource);
        values.PurchaseUomSource = purchaseUomSource;

        values.PackQty = GetEffectiveValue(
            preferredVpn?.PackQty,
            item.PackQty,
            out var packQtySource);
        values.PackQtySource = packQtySource;

        values.MinOrderQty = GetEffectiveValue(
            preferredVpn?.MinOrderQty,
            item.MinOrderQty,
            out var minOrderQtySource);
        values.MinOrderQtySource = minOrderQtySource;

        values.OrderMultiple = item.OrderMultiple;
        values.OrderMultipleSource = item.OrderMultiple.HasValue ? "Item Default" : "None";

        values.CurrencyCode = item.CurrencyCode ?? "USD";
        values.PriceEffectiveDate = preferredVpn?.PriceEffectiveDate ?? item.PriceEffectiveDate;

        return values;
    }
}
