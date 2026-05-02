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
    public class AssetCategoriesModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public AssetCategoriesModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<AssetCategory> AssetCategories { get; set; } = new();
        public List<GlAccount> GlAccounts { get; set; } = new();
        public List<SelectListItem> ActiveInactiveOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            ActiveInactiveOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ActiveInactive", null, "");
            AssetCategories = await _db.AssetCategories
                .Where(c => c.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(c.CompanyId ?? 0))
                .Include(c => c.AssetGlAccount)
                .Include(c => c.AccumDepGlAccount)
                .Include(c => c.DepExpGlAccount)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Code)
                .ToListAsync();

            GlAccounts = await _db.GlAccounts
                .Where(g => g.IsActive)
                .OrderBy(g => g.AccountNumber)
                .ToListAsync();

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(string code, string name, string? description, int macrsClass, int usefulLifeMonths, decimal salvagePercent, int? assetGlAccountId, int? accumDepGlAccountId, int? depExpGlAccountId)
        {
            if (await _db.AssetCategories.AnyAsync(c => c.Code == code))
            {
                TempData["Error"] = "An asset category with this code already exists.";
                return RedirectToPage();
            }

            var category = new AssetCategory
            {
                Code = code,
                Name = name,
                Description = description,
                DefaultMacrsClass = (MacrsPropertyClass)macrsClass,
                DefaultUsefulLifeMonths = usefulLifeMonths,
                DefaultSalvagePercent = salvagePercent,
                AssetGlAccountId = assetGlAccountId,
                AccumDepGlAccountId = accumDepGlAccountId,
                DepExpGlAccountId = depExpGlAccountId,
                IsActive = true,
                CompanyId = _tenantContext.CompanyId ?? 1
            };

            _db.AssetCategories.Add(category);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Asset Category {code} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string code, string name, string? description, int macrsClass, int usefulLifeMonths, decimal salvagePercent, int? assetGlAccountId, int? accumDepGlAccountId, int? depExpGlAccountId, bool isActive)
        {
            var category = await _db.AssetCategories
                .Where(c => (c.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(c.CompanyId ?? 0)) && c.Id == id)
                .FirstOrDefaultAsync();
            if (category == null)
            {
                TempData["Error"] = "Asset category not found.";
                return RedirectToPage();
            }

            category.Code = code;
            category.Name = name;
            category.Description = description;
            category.DefaultMacrsClass = (MacrsPropertyClass)macrsClass;
            category.DefaultUsefulLifeMonths = usefulLifeMonths;
            category.DefaultSalvagePercent = salvagePercent;
            category.AssetGlAccountId = assetGlAccountId;
            category.AccumDepGlAccountId = accumDepGlAccountId;
            category.DepExpGlAccountId = depExpGlAccountId;
            category.IsActive = isActive;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Asset Category {code} updated successfully.";
            return RedirectToPage();
        }
    }
}
