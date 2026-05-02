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
    public class LocationsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public LocationsModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<Location> Locations { get; set; } = new();
        public List<CostCenter> CostCenters { get; set; } = new();
        public List<Site> Sites { get; set; } = new();
        public List<SelectListItem> TypeOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            TypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "LocationType", null, "");
            Locations = await _db.Locations
                .Include(l => l.ParentLocation)
                .Include(l => l.CostCenter)
                .Include(l => l.Site)
                .OrderBy(l => l.Site != null ? l.Site.Name : "")
                .ThenBy(l => l.SortOrder)
                .ThenBy(l => l.Code)
                .ToListAsync();

            CostCenters = await _db.CostCenters
                .Where(c => c.IsActive)
                .OrderBy(c => c.Code)
                .ToListAsync();

            Sites = await _db.Sites
                .Where(s => s.Status == SiteStatus.Active)
                .OrderBy(s => s.Name)
                .ToListAsync();

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(string code, string name, string? description, int typeLookupValueId, int? parentLocationId, int? costCenterId, int? siteId, string? bay, string? aisle, string? rack, string? shelf, string? bin)
        {
            if (await _db.Locations.AnyAsync(l => l.Code == code))
            {
                TempData["Error"] = "A location with this code already exists.";
                return RedirectToPage();
            }

            var resolvedType = LocationType.Building;
            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (LocationType)enumVal;
            }

            var location = new Location
            {
                Code = code,
                Name = name,
                Description = description,
                Type = resolvedType,
                TypeLookupValueId = resolvedTypeLvId,
                ParentLocationId = parentLocationId,
                CostCenterId = costCenterId,
                SiteId = siteId,
                Bay = bay,
                Aisle = aisle,
                Rack = rack,
                Shelf = shelf,
                Bin = bin,
                IsActive = true,
                CreatedBy = User.Identity?.Name ?? "System"
            };

            _db.Locations.Add(location);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Location {code} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string code, string name, string? description, int typeLookupValueId, int? parentLocationId, int? costCenterId, int? siteId, string? bay, string? aisle, string? rack, string? shelf, string? bin, bool isActive)
        {
            var location = await _db.Locations
                .Where(l => (l.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0)) && l.Id == id)
                .FirstOrDefaultAsync();
            if (location == null)
            {
                TempData["Error"] = "Location not found.";
                return RedirectToPage();
            }

            var resolvedType = LocationType.Building;
            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (LocationType)enumVal;
            }

            location.Code = code;
            location.Name = name;
            location.Description = description;
            location.Type = resolvedType;
            location.TypeLookupValueId = resolvedTypeLvId;
            location.ParentLocationId = parentLocationId == 0 ? null : parentLocationId;
            location.CostCenterId = costCenterId == 0 ? null : costCenterId;
            location.SiteId = siteId == 0 ? null : siteId;
            location.Bay = bay;
            location.Aisle = aisle;
            location.Rack = rack;
            location.Shelf = shelf;
            location.Bin = bin;
            location.IsActive = isActive;
            location.ModifiedAt = DateTime.UtcNow;
            location.ModifiedBy = User.Identity?.Name ?? "System";

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Location {code} updated successfully.";
            return RedirectToPage();
        }
    }
}
