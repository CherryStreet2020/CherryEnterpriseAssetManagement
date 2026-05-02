using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Pages.Reports
{
    public class DepreciationScheduleModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly DepreciationService _depr;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public DepreciationScheduleModel(AppDbContext db, DepreciationService depr,
            IModuleGuardService moduleGuard,
            ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _depr = depr;
            _tenantContext = tenantContext;
        }

        public string? AssetNumber { get; set; }
        public string? Description { get; set; }
        public DateTime AsOf { get; set; }
        public DateTime AsOfMonthEnd { get; set; }

        public List<DepreciationRow> ScheduleRows { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id, DateTime? asOf = null)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });


            if (id is null) return Page();

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var asset = await _db.Assets.AsNoTracking().Where(a => a.Id == id.Value && visibleIds.Contains(a.CompanyId ?? 0)).OrderBy(a => a.Id).FirstOrDefaultAsync();
            if (asset is null) return NotFound();

            AssetNumber = asset.AssetNumber;
            Description  = asset.Description;

            AsOf = (asOf ?? DateTime.Today).Date;
            AsOfMonthEnd = new DateTime(AsOf.Year, AsOf.Month, DateTime.DaysInMonth(AsOf.Year, AsOf.Month));

            ScheduleRows = _depr.BuildSchedule(asset, AsOfMonthEnd);
        
            return Page();
        }

        public static string Money(decimal amount) => amount.ToString("C2");
        public static string ShortDate(DateTime date) => date.ToString("MM/dd/yyyy");
    }
}