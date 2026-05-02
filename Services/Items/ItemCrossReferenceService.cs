using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Items;

public enum MatchOrigin
{
    Internal = 0,
    MfrPartNumber = 1,
    VendorPartNumber = 2
}

public class ItemResolutionResult
{
    public int ItemId { get; set; }
    public int? CurrentRevisionId { get; set; }
    public string PartNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MatchOrigin OriginMatched { get; set; }
    public string MatchedValue { get; set; } = string.Empty;
    public int? VendorId { get; set; }
    public string? VendorName { get; set; }
    public int? ManufacturerId { get; set; }
    public string? ManufacturerName { get; set; }
    public string? MfrPartNumber { get; set; }
    public string? VendorPartNumber { get; set; }
}

public interface IItemCrossReferenceService
{
    Task<ItemManufacturerPart> AddMpnAsync(int itemId, int manufacturerId, string mfrPartNumber, string? description = null, string? userId = null);
    Task<ItemManufacturerPart> UpdateMpnAsync(int mpnId, string? mfrPartNumber, string? description, string? lifecycleStatus, string? datasheetUrl);
    Task<List<ItemManufacturerPart>> GetMpnsForItemAsync(int itemId);
    Task<VendorItemPart> AddVpnAsync(int itemId, int vendorId, string vendorPartNumber, int? mpnId = null, string? userId = null);
    Task<VendorItemPart> UpdateVpnAsync(int vpnId, string? vendorPartNumber, int? mpnId, string? vendorUom, decimal? packQty, int? leadTimeDays, decimal? minOrderQty, bool? preferred, decimal? unitPrice);
    Task<List<VendorItemPart>> GetVpnsForItemAsync(int itemId);
    Task<List<VendorItemPart>> GetVpnsForVendorAsync(int vendorId);
    Task<ItemResolutionResult?> ResolveItemAsync(string query, int? vendorId = null);
    Task<List<ItemResolutionResult>> SearchItemsAsync(string query, int? vendorId = null, int maxResults = 20);
}

public class ItemCrossReferenceService : IItemCrossReferenceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItemCrossReferenceService> _logger;
    private readonly ITenantContext _tenantContext;

    public ItemCrossReferenceService(AppDbContext db, ILogger<ItemCrossReferenceService> logger, ITenantContext tenantContext)
    {
        _db = db;
        _logger = logger;
        _tenantContext = tenantContext;
    }

    private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

    public async Task<ItemManufacturerPart> AddMpnAsync(int itemId, int manufacturerId, string mfrPartNumber, string? description = null, string? userId = null)
    {
        var companyId = GetCompanyId();
        var item = await _db.Items.Where(i => i.Id == itemId && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0)).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Item {itemId} not found");
        var manufacturer = await _db.Manufacturers.Where(m => m.Id == manufacturerId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Manufacturer {manufacturerId} not found");

        var existing = await _db.ItemManufacturerParts
            .FirstOrDefaultAsync(m => m.ItemId == itemId && m.ManufacturerId == manufacturerId 
                && m.MfrPartNumber.ToLower() == mfrPartNumber.ToLower());
        if (existing != null)
            throw new InvalidOperationException($"MPN {mfrPartNumber} already exists for this item and manufacturer");

        var mpn = new ItemManufacturerPart
        {
            ItemId = itemId,
            ManufacturerId = manufacturerId,
            MfrPartNumber = mfrPartNumber,
            Description = description,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = userId
        };

        _db.ItemManufacturerParts.Add(mpn);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Added MPN {MfrPartNumber} for Item {ItemId}, Manufacturer {ManufacturerId}", mfrPartNumber, itemId, manufacturerId);
        return mpn;
    }

    public async Task<ItemManufacturerPart> UpdateMpnAsync(int mpnId, string? mfrPartNumber, string? description, string? lifecycleStatus, string? datasheetUrl)
    {
        var companyId = GetCompanyId();
        var mpn = await _db.ItemManufacturerParts
            .Include(m => m.Item)
            .Where(m => m.Id == mpnId && m.Item != null && _tenantContext.VisibleCompanyIds.Contains(m.Item.CompanyId ?? 0))
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"MPN {mpnId} not found");

        if (mfrPartNumber != null) mpn.MfrPartNumber = mfrPartNumber;
        if (description != null) mpn.Description = description;
        if (lifecycleStatus != null) mpn.LifecycleStatus = lifecycleStatus;
        if (datasheetUrl != null) mpn.DatasheetUrl = datasheetUrl;
        mpn.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return mpn;
    }

    public async Task<List<ItemManufacturerPart>> GetMpnsForItemAsync(int itemId)
    {
        var companyId = GetCompanyId();
        var itemBelongsToTenant = await _db.Items.AnyAsync(i => i.Id == itemId && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));
        if (!itemBelongsToTenant)
            return new List<ItemManufacturerPart>();

        return await _db.ItemManufacturerParts
            .Include(m => m.Manufacturer)
            .Where(m => m.ItemId == itemId && m.IsActive)
            .OrderBy(m => m.ManufacturerId).ThenBy(m => m.MfrPartNumber)
            .ToListAsync();
    }

    public async Task<VendorItemPart> AddVpnAsync(int itemId, int vendorId, string vendorPartNumber, int? mpnId = null, string? userId = null)
    {
        var companyId = GetCompanyId();
        var item = await _db.Items.Where(i => i.Id == itemId && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0)).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Item {itemId} not found");
        var vendor = await _db.Vendors.Where(v => v.Id == vendorId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"Vendor {vendorId} not found");

        var existing = await _db.VendorItemParts
            .FirstOrDefaultAsync(v => v.VendorId == vendorId && v.VendorPartNumber.ToLower() == vendorPartNumber.ToLower());
        if (existing != null)
            throw new InvalidOperationException($"VPN {vendorPartNumber} already exists for vendor {vendor.Name}");

        var vpn = new VendorItemPart
        {
            ItemId = itemId,
            VendorId = vendorId,
            VendorPartNumber = vendorPartNumber,
            ItemManufacturerPartId = mpnId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = userId
        };

        _db.VendorItemParts.Add(vpn);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Added VPN {VendorPartNumber} for Item {ItemId}, Vendor {VendorId}", vendorPartNumber, itemId, vendorId);
        return vpn;
    }

    public async Task<VendorItemPart> UpdateVpnAsync(int vpnId, string? vendorPartNumber, int? mpnId, string? vendorUom, decimal? packQty, int? leadTimeDays, decimal? minOrderQty, bool? preferred, decimal? unitPrice)
    {
        var companyId = GetCompanyId();
        var vpn = await _db.VendorItemParts
            .Include(v => v.Item)
            .Where(v => v.Id == vpnId && v.Item != null && _tenantContext.VisibleCompanyIds.Contains(v.Item.CompanyId ?? 0))
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"VPN {vpnId} not found");

        if (vendorPartNumber != null) vpn.VendorPartNumber = vendorPartNumber;
        if (mpnId.HasValue) vpn.ItemManufacturerPartId = mpnId;
        if (vendorUom != null) vpn.VendorUom = vendorUom;
        if (packQty.HasValue) vpn.PackQty = packQty;
        if (leadTimeDays.HasValue) vpn.LeadTimeDays = leadTimeDays;
        if (minOrderQty.HasValue) vpn.MinOrderQty = minOrderQty;
        if (preferred.HasValue) vpn.Preferred = preferred.Value;
        if (unitPrice.HasValue) vpn.UnitPrice = unitPrice;
        vpn.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return vpn;
    }

    public async Task<List<VendorItemPart>> GetVpnsForItemAsync(int itemId)
    {
        var companyId = GetCompanyId();
        var itemBelongsToTenant = await _db.Items.AnyAsync(i => i.Id == itemId && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));
        if (!itemBelongsToTenant)
            return new List<VendorItemPart>();

        return await _db.VendorItemParts
            .Include(v => v.Vendor)
            .Include(v => v.ItemManufacturerPart)
            .ThenInclude(m => m!.Manufacturer)
            .Where(v => v.ItemId == itemId && v.IsActive)
            .OrderBy(v => v.VendorId).ThenBy(v => v.VendorPartNumber)
            .ToListAsync();
    }

    public async Task<List<VendorItemPart>> GetVpnsForVendorAsync(int vendorId)
    {
        var companyId = GetCompanyId();
        return await _db.VendorItemParts
            .Include(v => v.Item)
            .Include(v => v.ItemManufacturerPart)
            .Where(v => v.VendorId == vendorId && v.IsActive && v.Item != null && _tenantContext.VisibleCompanyIds.Contains(v.Item.CompanyId ?? 0))
            .OrderBy(v => v.VendorPartNumber)
            .ToListAsync();
    }

    public async Task<ItemResolutionResult?> ResolveItemAsync(string query, int? vendorId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var companyId = GetCompanyId();
        var normalizedQuery = query.Trim().ToLowerInvariant();

        var byInternalPn = await _db.Items
            .Include(i => i.CurrentReleasedRevision)
            .FirstOrDefaultAsync(i => i.PartNumber.ToLower() == normalizedQuery && i.IsActive && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));

        if (byInternalPn != null)
        {
            return new ItemResolutionResult
            {
                ItemId = byInternalPn.Id,
                CurrentRevisionId = byInternalPn.CurrentReleasedRevisionId,
                PartNumber = byInternalPn.PartNumber,
                Name = byInternalPn.CurrentReleasedRevision?.Name ?? byInternalPn.Description,
                OriginMatched = MatchOrigin.Internal,
                MatchedValue = byInternalPn.PartNumber
            };
        }

        var byMpn = await _db.ItemManufacturerParts
            .Include(m => m.Item)
            .ThenInclude(i => i!.CurrentReleasedRevision)
            .Include(m => m.Manufacturer)
            .FirstOrDefaultAsync(m => m.MfrPartNumber.ToLower() == normalizedQuery && m.IsActive && m.Item != null && _tenantContext.VisibleCompanyIds.Contains(m.Item.CompanyId ?? 0));

        if (byMpn?.Item != null)
        {
            return new ItemResolutionResult
            {
                ItemId = byMpn.ItemId,
                CurrentRevisionId = byMpn.Item.CurrentReleasedRevisionId,
                PartNumber = byMpn.Item.PartNumber,
                Name = byMpn.Item.CurrentReleasedRevision?.Name ?? byMpn.Item.Description,
                OriginMatched = MatchOrigin.MfrPartNumber,
                MatchedValue = byMpn.MfrPartNumber,
                ManufacturerId = byMpn.ManufacturerId,
                ManufacturerName = byMpn.Manufacturer?.Name,
                MfrPartNumber = byMpn.MfrPartNumber
            };
        }

        IQueryable<VendorItemPart> vpnQuery = _db.VendorItemParts
            .Include(v => v.Item)
            .ThenInclude(i => i!.CurrentReleasedRevision)
            .Include(v => v.Vendor)
            .Include(v => v.ItemManufacturerPart)
            .ThenInclude(m => m!.Manufacturer)
            .Where(v => v.VendorPartNumber.ToLower() == normalizedQuery && v.IsActive && v.Item != null && _tenantContext.VisibleCompanyIds.Contains(v.Item.CompanyId ?? 0));

        if (vendorId.HasValue)
            vpnQuery = vpnQuery.Where(v => v.VendorId == vendorId.Value);

        var byVpn = await vpnQuery.FirstOrDefaultAsync();

        if (byVpn?.Item != null)
        {
            return new ItemResolutionResult
            {
                ItemId = byVpn.ItemId,
                CurrentRevisionId = byVpn.Item.CurrentReleasedRevisionId,
                PartNumber = byVpn.Item.PartNumber,
                Name = byVpn.Item.CurrentReleasedRevision?.Name ?? byVpn.Item.Description,
                OriginMatched = MatchOrigin.VendorPartNumber,
                MatchedValue = byVpn.VendorPartNumber,
                VendorId = byVpn.VendorId,
                VendorName = byVpn.Vendor?.Name,
                VendorPartNumber = byVpn.VendorPartNumber,
                ManufacturerId = byVpn.ItemManufacturerPart?.ManufacturerId,
                ManufacturerName = byVpn.ItemManufacturerPart?.Manufacturer?.Name,
                MfrPartNumber = byVpn.ItemManufacturerPart?.MfrPartNumber
            };
        }

        return null;
    }

    public async Task<List<ItemResolutionResult>> SearchItemsAsync(string query, int? vendorId = null, int maxResults = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<ItemResolutionResult>();

        var companyId = GetCompanyId();
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var results = new List<ItemResolutionResult>();

        var internalMatches = await _db.Items
            .Include(i => i.CurrentReleasedRevision)
            .Where(i => i.IsActive && _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0) &&
                (i.PartNumber.ToLower().Contains(normalizedQuery) || 
                 i.Description.ToLower().Contains(normalizedQuery)))
            .Take(maxResults)
            .ToListAsync();

        results.AddRange(internalMatches.Select(i => new ItemResolutionResult
        {
            ItemId = i.Id,
            CurrentRevisionId = i.CurrentReleasedRevisionId,
            PartNumber = i.PartNumber,
            Name = i.CurrentReleasedRevision?.Name ?? i.Description,
            OriginMatched = MatchOrigin.Internal,
            MatchedValue = i.PartNumber
        }));

        if (results.Count < maxResults)
        {
            var mpnMatches = await _db.ItemManufacturerParts
                .Include(m => m.Item)
                .ThenInclude(i => i!.CurrentReleasedRevision)
                .Include(m => m.Manufacturer)
                .Where(m => m.IsActive && m.MfrPartNumber.ToLower().Contains(normalizedQuery) && m.Item != null && _tenantContext.VisibleCompanyIds.Contains(m.Item.CompanyId ?? 0))
                .Take(maxResults - results.Count)
                .ToListAsync();

            results.AddRange(mpnMatches.Where(m => m.Item != null && !results.Any(r => r.ItemId == m.ItemId)).Select(m => new ItemResolutionResult
            {
                ItemId = m.ItemId,
                CurrentRevisionId = m.Item!.CurrentReleasedRevisionId,
                PartNumber = m.Item.PartNumber,
                Name = m.Item.CurrentReleasedRevision?.Name ?? m.Item.Description,
                OriginMatched = MatchOrigin.MfrPartNumber,
                MatchedValue = m.MfrPartNumber,
                ManufacturerId = m.ManufacturerId,
                ManufacturerName = m.Manufacturer?.Name,
                MfrPartNumber = m.MfrPartNumber
            }));
        }

        if (results.Count < maxResults)
        {
            IQueryable<VendorItemPart> vpnQuery = _db.VendorItemParts
                .Include(v => v.Item)
                .ThenInclude(i => i!.CurrentReleasedRevision)
                .Include(v => v.Vendor)
                .Where(v => v.IsActive && v.VendorPartNumber.ToLower().Contains(normalizedQuery) && v.Item != null && _tenantContext.VisibleCompanyIds.Contains(v.Item.CompanyId ?? 0));

            if (vendorId.HasValue)
                vpnQuery = vpnQuery.Where(v => v.VendorId == vendorId.Value);

            var vpnMatches = await vpnQuery.Take(maxResults - results.Count).ToListAsync();

            results.AddRange(vpnMatches.Where(v => v.Item != null && !results.Any(r => r.ItemId == v.ItemId)).Select(v => new ItemResolutionResult
            {
                ItemId = v.ItemId,
                CurrentRevisionId = v.Item!.CurrentReleasedRevisionId,
                PartNumber = v.Item.PartNumber,
                Name = v.Item.CurrentReleasedRevision?.Name ?? v.Item.Description,
                OriginMatched = MatchOrigin.VendorPartNumber,
                MatchedValue = v.VendorPartNumber,
                VendorId = v.VendorId,
                VendorName = v.Vendor?.Name,
                VendorPartNumber = v.VendorPartNumber
            }));
        }

        return results.Take(maxResults).ToList();
    }
}
