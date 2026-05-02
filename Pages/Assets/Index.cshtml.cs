using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Assets
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ICompanyService _companyService;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public List<Asset> Assets { get; private set; } = new();
        public List<Asset> AllAssets { get; private set; } = new();
        public List<Asset> AssetList => Assets;
        
        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }
        
        public string FilterLabel { get; set; } = "All Assets";

        public IndexModel(AppDbContext db, ICompanyService companyService,
            IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _companyService = companyService;
            _tenantContext = tenantContext;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Assets" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var query = _db.Assets
                .AsNoTracking()
                .Include(a => a.LocationRef)
                .Include(a => a.Company)
                .Where(a => visibleIds.Contains(a.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                query = query.Where(a => a.SiteId == _tenantContext.SiteId.Value);

            AllAssets = await query
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();
            
            Assets = Filter?.ToLower() switch
            {
                "active" => AllAssets.Where(a => a.Active).ToList(),
                "inactive" => AllAssets.Where(a => !a.Active).ToList(),
                "disposed" => AllAssets.Where(a => a.Status == AssetStatus.Disposed).ToList(),
                _ => AllAssets
            };
            
            FilterLabel = Filter?.ToLower() switch
            {
                "active" => "Active Assets",
                "inactive" => "Inactive Assets",
                "disposed" => "Disposed Assets",
                _ => "All Assets"
            };
        
            return Page();
        }
    }
}
