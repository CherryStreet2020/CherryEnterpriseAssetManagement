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
    public class SitesModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;

        public SitesModel(AppDbContext db, ITenantContext tenantContext, ILookupService lookupService)
        {
            _db = db;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
        }

        public List<Site> Sites { get; set; } = new();
        public List<Company> Companies { get; set; } = new();
        public List<SelectListItem> TimezoneOptions { get; set; } = new();
        public List<SelectListItem> SiteTypeOptions { get; set; } = new();
        public List<SelectListItem> SiteStatusOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public int CurrentSiteId => _tenantContext.SiteId ?? 1;

        public async Task OnGetAsync()
        {
            Sites = await _db.Sites
                .Include(s => s.Company)
                .OrderBy(s => s.Company!.Name)
                .ThenBy(s => s.Name)
                .ToListAsync();

            Companies = await _db.Companies
                .Where(c => c.IsActive && _tenantContext.VisibleCompanyIds.Contains(c.Id))
                .OrderBy(c => c.Name)
                .ToListAsync();

            TimezoneOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "Timezone", null, "");
            SiteTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "SiteType", null, "");
            SiteStatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "SiteStatus", null, "");

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string siteCode, string name, string? description, int typeLookupValueId, int statusLookupValueId,
            int companyId, string? address1, string? address2, string? city,
            string? stateProvince, string? postalCode, string? country,
            string? timeZone, string? siteManager, string? managerEmail,
            string? managerPhone, string? mainPhone, int? squareFootage,
            int? numberOfBuildings, int? employeeCount, bool isPrimarySite)
        {
            if (await _db.Sites.AnyAsync(s => s.SiteCode == siteCode))
            {
                TempData["Error"] = "A site with this code already exists.";
                return RedirectToPage();
            }

            var resolvedType = SiteType.Manufacturing;
            int? resolvedTypeLvId = null;
            var resolvedStatus = SiteStatus.Active;
            int? resolvedStatusLvId = null;

            var typeLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (SiteType)enumVal;
            }

            var statusLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, statusLookupValueId);
            if (statusLv != null)
            {
                resolvedStatusLvId = statusLv.Id;
                if (int.TryParse(statusLv.Code, out var enumVal))
                    resolvedStatus = (SiteStatus)enumVal;
            }

            var site = new Site
            {
                SiteCode = siteCode,
                Name = name,
                Description = description,
                Type = resolvedType,
                TypeLookupValueId = resolvedTypeLvId,
                Status = resolvedStatus,
                StatusLookupValueId = resolvedStatusLvId,
                CompanyId = companyId,
                Address1 = address1,
                Address2 = address2,
                City = city,
                StateProvince = stateProvince,
                PostalCode = postalCode,
                Country = country ?? "United States",
                TimeZone = timeZone ?? "America/New_York",
                SiteManager = siteManager,
                ManagerEmail = managerEmail,
                ManagerPhone = managerPhone,
                MainPhone = mainPhone,
                SquareFootage = squareFootage,
                NumberOfBuildings = numberOfBuildings,
                EmployeeCount = employeeCount,
                IsPrimarySite = isPrimarySite,
                CreatedBy = User.Identity?.Name ?? "System"
            };

            _db.Sites.Add(site);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Site {siteCode} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            int id, string siteCode, string name, string? description, int typeLookupValueId, int statusLookupValueId,
            int companyId, string? address1, string? address2, string? city,
            string? stateProvince, string? postalCode, string? country,
            string? timeZone, string? siteManager, string? managerEmail,
            string? managerPhone, string? mainPhone, int? squareFootage,
            int? numberOfBuildings, int? employeeCount, bool isPrimarySite)
        {
            var site = await _db.Sites
                .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId) && s.Id == id)
                .FirstOrDefaultAsync();
            if (site == null)
            {
                TempData["Error"] = "Site not found.";
                return RedirectToPage();
            }

            site.SiteCode = siteCode;
            site.Name = name;
            site.Description = description;

            var typeLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, typeLookupValueId);
            if (typeLv != null)
            {
                site.TypeLookupValueId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    site.Type = (SiteType)enumVal;
            }

            var statusLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, statusLookupValueId);
            if (statusLv != null)
            {
                site.StatusLookupValueId = statusLv.Id;
                if (int.TryParse(statusLv.Code, out var enumVal))
                    site.Status = (SiteStatus)enumVal;
            }
            site.CompanyId = companyId;
            site.Address1 = address1;
            site.Address2 = address2;
            site.City = city;
            site.StateProvince = stateProvince;
            site.PostalCode = postalCode;
            site.Country = country;
            site.TimeZone = timeZone;
            site.SiteManager = siteManager;
            site.ManagerEmail = managerEmail;
            site.ManagerPhone = managerPhone;
            site.MainPhone = mainPhone;
            site.SquareFootage = squareFootage;
            site.NumberOfBuildings = numberOfBuildings;
            site.EmployeeCount = employeeCount;
            site.IsPrimarySite = isPrimarySite;
            site.ModifiedAt = DateTime.UtcNow;
            site.ModifiedBy = User.Identity?.Name ?? "System";

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Site {siteCode} updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var site = await _db.Sites
                .Include(s => s.Locations)
                .Include(s => s.Assets)
                .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId) && s.Id == id)
                .FirstOrDefaultAsync();

            if (site == null)
            {
                TempData["Error"] = "Site not found.";
                return RedirectToPage();
            }

            if (site.Locations?.Any() == true)
            {
                TempData["Error"] = $"Cannot delete site {site.SiteCode}. It has {site.Locations.Count} locations assigned.";
                return RedirectToPage();
            }

            if (site.Assets?.Any() == true)
            {
                TempData["Error"] = $"Cannot delete site {site.SiteCode}. It has {site.Assets.Count} assets assigned.";
                return RedirectToPage();
            }

            _db.Sites.Remove(site);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Site {site.SiteCode} deleted successfully.";
            return RedirectToPage();
        }
    }
}
