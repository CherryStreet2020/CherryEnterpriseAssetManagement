using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.CCA
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;

        private readonly ITenantContext _tenantContext;

        public IndexModel(AppDbContext db,
            IModuleGuardService moduleGuard,
            ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _tenantContext = tenantContext;
        }

        public List<CcaClass> CcaClasses { get; set; } = new();
        public int TotalAssetsByCca { get; set; }
        public Dictionary<int, int> AssetCountByClass { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "CCA" });


            CcaClasses = await _db.CcaClasses
                .Where(c => c.Active)
                .OrderBy(c => c.ClassNumber)
                .ToListAsync();

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var assetCounts = await _db.AssetTaxSettings
                .Where(t => visibleIds.Contains(t.Asset.CompanyId ?? 0))
                .GroupBy(t => t.CcaClassId)
                .Select(g => new { ClassId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var item in assetCounts)
            {
                AssetCountByClass[item.ClassId] = item.Count;
            }

            TotalAssetsByCca = assetCounts.Sum(a => a.Count);
        
            return Page();
        }
    }
}
