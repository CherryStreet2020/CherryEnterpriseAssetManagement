using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Abs.FixedAssets.Services.Lookups;

public class LookupService : ILookupService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LookupService> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private const string CachePrefix = "Lookup_";

    public LookupService(AppDbContext db, IMemoryCache cache, ILogger<LookupService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<LookupValueDto>> GetValuesAsync(int? tenantId, int? companyId, string lookupKey, bool includeInactive = false)
    {
        var cacheKey = $"{CachePrefix}{tenantId}_{companyId}_{lookupKey}_{includeInactive}";

        if (_cache.TryGetValue(cacheKey, out List<LookupValueDto>? cached) && cached != null)
            return cached;

        var lookupType = await _db.LookupTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(lt => lt.Key == lookupKey && lt.TenantId == tenantId && lt.CompanyId == companyId && lt.IsActive);

        if (lookupType == null && companyId != null)
        {
            lookupType = await _db.LookupTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(lt => lt.Key == lookupKey && lt.TenantId == tenantId && lt.CompanyId == null && lt.IsActive);
        }

        if (lookupType == null)
        {
            _logger.LogWarning("LookupType '{Key}' not found for tenant={TenantId}, company={CompanyId}", lookupKey, tenantId, companyId);
            return new List<LookupValueDto>();
        }

        var query = _db.LookupValues
            .AsNoTracking()
            .Where(lv => lv.LookupTypeId == lookupType.Id);

        if (!includeInactive)
            query = query.Where(lv => lv.IsActive);

        var values = await query
            .OrderBy(lv => lv.SortOrder)
            .ThenBy(lv => lv.Name)
            .Select(lv => new LookupValueDto
            {
                Id = lv.Id,
                Code = lv.Code,
                Name = lv.Name,
                SortOrder = lv.SortOrder,
                IsActive = lv.IsActive,
                Metadata = lv.Metadata
            })
            .ToListAsync();

        _cache.Set(cacheKey, values, CacheTtl);
        return values;
    }

    public async Task<LookupValueDto?> GetValueByIdAsync(int? tenantId, int? companyId, int lookupValueId)
    {
        var value = await _db.LookupValues
            .AsNoTracking()
            .Include(lv => lv.LookupType)
            .FirstOrDefaultAsync(lv => lv.Id == lookupValueId);

        if (value == null) return null;

        if (value.LookupType.TenantId != tenantId)
        {
            _logger.LogWarning("Cross-tenant lookup access denied: requested={TenantId}, actual={ActualTenantId}", tenantId, value.LookupType.TenantId);
            return null;
        }

        if (value.LookupType.CompanyId != null && value.LookupType.CompanyId != companyId)
        {
            _logger.LogWarning("Cross-company lookup access denied: requested={CompanyId}, actual={ActualCompanyId}", companyId, value.LookupType.CompanyId);
            return null;
        }

        return new LookupValueDto
        {
            Id = value.Id,
            Code = value.Code,
            Name = value.Name,
            SortOrder = value.SortOrder,
            IsActive = value.IsActive,
            Metadata = value.Metadata
        };
    }

    public async Task<LookupValueDto?> GetValueByCodeAsync(int? tenantId, int? companyId, string lookupKey, string code)
    {
        var values = await GetValuesAsync(tenantId, companyId, lookupKey, includeInactive: true);
        return values.FirstOrDefault(v => v.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<SelectListItem>> GetSelectListAsync(int? tenantId, int? companyId, string lookupKey, string? selectedValue = null, string placeholder = "-- Select --")
    {
        var values = await GetValuesAsync(tenantId, companyId, lookupKey);
        var items = new List<SelectListItem>();

        if (!string.IsNullOrEmpty(placeholder))
        {
            items.Add(new SelectListItem(placeholder, "") { Selected = string.IsNullOrEmpty(selectedValue) });
        }

        foreach (var v in values)
        {
            items.Add(new SelectListItem(v.Name, v.Code) { Selected = v.Code == selectedValue });
        }

        return items;
    }

    public async Task<List<SelectListItem>> GetSelectListByIdAsync(int? tenantId, int? companyId, string lookupKey, int? selectedId = null, string placeholder = "-- Select --")
    {
        var values = await GetValuesAsync(tenantId, companyId, lookupKey);
        var items = new List<SelectListItem>();

        if (!string.IsNullOrEmpty(placeholder))
        {
            items.Add(new SelectListItem(placeholder, "") { Selected = selectedId == null });
        }

        foreach (var v in values)
        {
            items.Add(new SelectListItem(v.Name, v.Id.ToString()) { Selected = v.Id == selectedId });
        }

        return items;
    }

    public void InvalidateCache()
    {
        if (_cache is MemoryCache mc)
        {
            mc.Compact(1.0);
        }
        _logger.LogInformation("Lookup cache invalidated");
    }
}
