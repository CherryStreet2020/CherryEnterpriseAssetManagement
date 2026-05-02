using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Revisions;

namespace Abs.FixedAssets.Services.Items;

public interface IPreferredVendorCatalogResolver
{
    Task<string?> GetPreferredVendorCatalogUrlAsync(int itemId);
    Task<VendorItemPart?> GetPreferredVendorPartAsync(int itemId);
    Task<CatalogResolution> ResolveAsync(int itemId);
}

public class CatalogResolution
{
    public string? CatalogUrl { get; set; }
    public string? DatasheetUrl { get; set; }
    public string? ExternalImageUrl { get; set; }
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public bool IsFromPreferredVendor { get; set; }
    public string? EnrichmentStatus { get; set; }
    public DateTime? LastEnrichedUtc { get; set; }
}

public class PreferredVendorCatalogResolver : IPreferredVendorCatalogResolver
{
    private readonly AppDbContext _db;
    private readonly IItemSourcingService _sourcingService;

    public PreferredVendorCatalogResolver(AppDbContext db, IItemSourcingService sourcingService)
    {
        _db = db;
        _sourcingService = sourcingService;
    }

    public async Task<string?> GetPreferredVendorCatalogUrlAsync(int itemId)
    {
        var vpn = await GetPreferredVendorPartAsync(itemId);
        return !string.IsNullOrWhiteSpace(vpn?.CatalogUrl) ? vpn.CatalogUrl : null;
    }

    public async Task<VendorItemPart?> GetPreferredVendorPartAsync(int itemId)
    {
        var preferredAvl = await _sourcingService.GetPreferredVendorAsync(itemId);
        if (preferredAvl == null) return null;

        return await _db.VendorItemParts
            .Include(v => v.Vendor)
            .FirstOrDefaultAsync(v => 
                v.ItemId == itemId && 
                v.VendorId == preferredAvl.VendorId && 
                v.IsActive);
    }

    public async Task<CatalogResolution> ResolveAsync(int itemId)
    {
        var result = new CatalogResolution();
        
        var preferredAvl = await _sourcingService.GetPreferredVendorAsync(itemId);
        if (preferredAvl == null) return result;

        var vpn = await _db.VendorItemParts
            .Include(v => v.Vendor)
            .FirstOrDefaultAsync(v => 
                v.ItemId == itemId && 
                v.VendorId == preferredAvl.VendorId && 
                v.IsActive);

        if (vpn == null) return result;

        result.VendorId = vpn.VendorId;
        result.VendorName = vpn.Vendor?.Name;
        result.IsFromPreferredVendor = true;
        result.CatalogUrl = !string.IsNullOrWhiteSpace(vpn.CatalogUrl) ? vpn.CatalogUrl : null;
        result.DatasheetUrl = !string.IsNullOrWhiteSpace(vpn.DatasheetUrl) ? vpn.DatasheetUrl : null;
        result.ExternalImageUrl = !string.IsNullOrWhiteSpace(vpn.ExternalImageUrl) ? vpn.ExternalImageUrl : null;
        result.EnrichmentStatus = vpn.LastEnrichStatus;
        result.LastEnrichedUtc = vpn.LastEnrichedUtc;

        return result;
    }
}
