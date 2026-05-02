using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Assets
{
    public class ScheduleModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly DepreciationService _dep;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public ScheduleModel(AppDbContext db, DepreciationService dep,
            IModuleGuardService moduleGuard,
            ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _dep = dep;
            _tenantContext = tenantContext;
        }

        // Header info
        public int AssetId { get; set; }
        public string AssetNumber { get; set; } = "";
        public string Description { get; set; } = "";

        // As-of dates
        public DateTime AsOf { get; set; }
        public DateTime AsOfMonthEnd => new DateTime(AsOf.Year, AsOf.Month, DateTime.DaysInMonth(AsOf.Year, AsOf.Month));

        // Rows rendered in the table
        public List<DepreciationRow> Rows { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id, DateTime? asOf)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Assets" });

            AssetId = id;
            AsOf = (asOf ?? DateTime.UtcNow.Date).Date;

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var asset = await _db.Assets.AsNoTracking().Where(a => a.Id == id && visibleIds.Contains(a.CompanyId ?? 0)).OrderBy(a => a.Id).FirstOrDefaultAsync();
            if (asset == null) return NotFound();

            AssetNumber = asset.AssetNumber;
            Description = asset.Description ?? "";

            // Build schedule rows (service already exists in your project)
            Rows = _dep.BuildSchedule(asset, AsOfMonthEnd);

            return Page();
        }

        // Helpers used by the .cshtml
        public string Money(decimal value) =>
            string.Format(CultureInfo.CurrentCulture, "{0:C0}", value);

        public string ShortDate(DateTime d) =>
            d.ToString("MM/dd/yyyy", CultureInfo.CurrentCulture);
    }
}