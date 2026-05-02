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
    public class DepartmentsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public DepartmentsModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<Department> Departments { get; set; } = new();
        public List<CostCenter> CostCenters { get; set; } = new();
        public List<SelectListItem> ActiveInactiveOptions { get; set; } = new();
        public List<SelectListItem> TypeOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            ActiveInactiveOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ActiveInactive", null, "");
            TypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "DepartmentType", null, "");
            Departments = await _db.Departments
                .Include(d => d.CostCenter)
                .OrderBy(d => d.SortOrder)
                .ThenBy(d => d.Code)
                .ToListAsync();

            CostCenters = await _db.CostCenters
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync();

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(string code, string name, string? description, int typeLookupValueId, int? costCenterId)
        {
            if (await _db.Departments.AnyAsync(d => d.Code == code))
            {
                TempData["Error"] = "A department with this code already exists.";
                return RedirectToPage();
            }

            var resolvedType = DepartmentType.Operations;
            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (DepartmentType)enumVal;
            }

            var dept = new Department
            {
                Code = code,
                Name = name,
                Description = description,
                Type = resolvedType,
                TypeLookupValueId = resolvedTypeLvId,
                CostCenterId = costCenterId,
                IsActive = true,
                CompanyId = _tenantContext.CompanyId ?? 1
            };

            _db.Departments.Add(dept);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Department {code} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string code, string name, string? description, int typeLookupValueId, int? costCenterId, bool isActive)
        {
            var dept = await _db.Departments
                .Where(d => (d.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(d.CompanyId ?? 0)) && d.Id == id)
                .FirstOrDefaultAsync();
            if (dept == null)
            {
                TempData["Error"] = "Department not found.";
                return RedirectToPage();
            }

            var resolvedType = DepartmentType.Operations;
            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (DepartmentType)enumVal;
            }

            dept.Code = code;
            dept.Name = name;
            dept.Description = description;
            dept.Type = resolvedType;
            dept.TypeLookupValueId = resolvedTypeLvId;
            dept.CostCenterId = costCenterId;
            dept.IsActive = isActive;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Department {code} updated successfully.";
            return RedirectToPage();
        }
    }
}
