using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class CostCentersModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public CostCentersModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<CostCenter> CostCenters { get; set; } = new();
        public List<SelectListItem> ActiveInactiveOptions { get; set; } = new();
        public List<SelectListItem> TypeOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            ActiveInactiveOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ActiveInactive", null, "");
            TypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CostCenterType", null, "");
            CostCenters = await _db.CostCenters
                .Include(c => c.ParentCostCenter)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Code)
                .ToListAsync();

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(string code, string name, string? description, int typeLookupValueId, string? city, string? stateProvince, string? country, int? parentId)
        {
            if (await _db.CostCenters.AnyAsync(c => c.Code == code))
            {
                TempData["Error"] = "A cost center with this code already exists.";
                return RedirectToPage();
            }

            var resolvedType = CostCenterType.Plant;
            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (CostCenterType)enumVal;
            }

            var costCenter = new CostCenter
            {
                Code = code,
                Name = name,
                Description = description,
                Type = resolvedType,
                TypeLookupValueId = resolvedTypeLvId,
                City = city,
                StateProvince = stateProvince,
                Country = country,
                ParentCostCenterId = parentId,
                IsActive = true,
                CompanyId = _tenantContext.CompanyId ?? 1
            };

            _db.CostCenters.Add(costCenter);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Cost Center {code} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string code, string name, string? description, int typeLookupValueId, string? city, string? stateProvince, string? country, int? parentId, bool isActive)
        {
            var costCenter = await _db.CostCenters
                .Where(c => (c.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(c.CompanyId ?? 0)) && c.Id == id)
                .FirstOrDefaultAsync();
            if (costCenter == null)
            {
                TempData["Error"] = "Cost center not found.";
                return RedirectToPage();
            }

            if (parentId == id)
            {
                TempData["Error"] = "A cost center cannot be its own parent.";
                return RedirectToPage();
            }

            var resolvedType = CostCenterType.Plant;
            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (CostCenterType)enumVal;
            }

            costCenter.Code = code;
            costCenter.Name = name;
            costCenter.Description = description;
            costCenter.Type = resolvedType;
            costCenter.TypeLookupValueId = resolvedTypeLvId;
            costCenter.City = city;
            costCenter.StateProvince = stateProvince;
            costCenter.Country = country;
            costCenter.ParentCostCenterId = parentId;
            costCenter.IsActive = isActive;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Cost Center {code} updated successfully.";
            return RedirectToPage();
        }
    }
}
