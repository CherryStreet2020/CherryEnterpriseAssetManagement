using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Reports
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

        public int TotalAssets { get; set; }
        public decimal TotalCost { get; set; }
        public int TotalJournals { get; set; }
        public int TotalCcaClasses { get; set; }
        public int CurrentYear { get; set; } = DateTime.Now.Year;

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            TotalAssets = await _db.Assets.Where(a => visibleIds.Contains(a.CompanyId ?? 0)).CountAsync();
            TotalCost = await _db.Assets.Where(a => visibleIds.Contains(a.CompanyId ?? 0)).SumAsync(a => a.AcquisitionCost);
            TotalJournals = await _db.JournalEntries.Where(j => j.Book != null && visibleIds.Contains(j.Book.CompanyId ?? 0)).CountAsync();
            TotalCcaClasses = await _db.CcaClasses.CountAsync();
        
            return Page();
        }
    }
}
