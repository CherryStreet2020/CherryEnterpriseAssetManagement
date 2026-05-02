using Microsoft.AspNetCore.Mvc.Rendering;

namespace Abs.FixedAssets.Services.Lookups;

public interface ILookupService
{
    Task<List<LookupValueDto>> GetValuesAsync(int? tenantId, int? companyId, string lookupKey, bool includeInactive = false);
    Task<LookupValueDto?> GetValueByIdAsync(int? tenantId, int? companyId, int lookupValueId);
    Task<LookupValueDto?> GetValueByCodeAsync(int? tenantId, int? companyId, string lookupKey, string code);
    Task<List<SelectListItem>> GetSelectListAsync(int? tenantId, int? companyId, string lookupKey, string? selectedValue = null, string placeholder = "-- Select --");
    Task<List<SelectListItem>> GetSelectListByIdAsync(int? tenantId, int? companyId, string lookupKey, int? selectedId = null, string placeholder = "-- Select --");
    void InvalidateCache();
}
