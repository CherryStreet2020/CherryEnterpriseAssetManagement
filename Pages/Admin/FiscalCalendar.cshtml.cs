using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class FiscalCalendarModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IFiscalCalendarService _calendar;
        private readonly ITenantContext _tenantContext;

        public FiscalCalendarModel(
            AppDbContext db,
            IFiscalCalendarService calendar,
            ITenantContext tenantContext)
        {
            _db = db;
            _calendar = calendar;
            _tenantContext = tenantContext;
        }

        public List<SelectListItem> CompanyOptions { get; set; } = new();
        public List<int> YearOptions { get; set; } = new();
        public List<FiscalYear> Years { get; set; } = new();
        public Dictionary<int, List<FiscalPeriod>> PeriodsByYear { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? CompanyId { get; set; }

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostGenerateNextYearAsync(int companyId)
        {
            try
            {
                var lastYear = await _db.FiscalYears
                    .Where(fy => fy.CompanyId == companyId)
                    .Select(fy => (int?)fy.Year)
                    .MaxAsync() ?? DateTime.UtcNow.Year - 1;

                await _calendar.GenerateYearAsync(companyId, lastYear + 1);
                SuccessMessage = $"Generated FY {lastYear + 1} (12 monthly periods).";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not generate next year: {ex.Message}";
            }

            CompanyId = companyId;
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEnsureCoverageAsync(int companyId)
        {
            try
            {
                var rows = await _calendar.EnsureCoverageAsync(companyId, DateTime.UtcNow);
                SuccessMessage = rows == 0
                    ? "Coverage is already complete for [today − 1y, today + 2y]."
                    : $"Materialized {rows} missing row(s) for [today − 1y, today + 2y].";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not ensure coverage: {ex.Message}";
            }

            CompanyId = companyId;
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostTogglePeriodAsync(int periodId, int companyId)
        {
            var period = await _db.FiscalPeriods.FirstOrDefaultAsync(p => p.Id == periodId);
            if (period == null)
            {
                ErrorMessage = "Period not found.";
            }
            else
            {
                period.Status = period.Status == PeriodStatus.Open
                    ? PeriodStatus.Closed
                    : PeriodStatus.Open;
                if (period.Status == PeriodStatus.Closed)
                {
                    period.ClosedAt = DateTime.UtcNow;
                    period.ClosedBy = User.Identity?.Name ?? "system";
                }
                else
                {
                    period.ClosedAt = null;
                    period.ClosedBy = null;
                }
                await _db.SaveChangesAsync();
                SuccessMessage = $"Period '{period.Name}' is now {period.Status}.";
            }

            CompanyId = companyId;
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostToggleYearAsync(int fiscalYearId, int companyId)
        {
            var fy = await _db.FiscalYears.FirstOrDefaultAsync(y => y.Id == fiscalYearId);
            if (fy == null)
            {
                ErrorMessage = "Fiscal year not found.";
            }
            else
            {
                fy.Status = fy.Status == FiscalYearStatus.Open
                    ? FiscalYearStatus.Closed
                    : FiscalYearStatus.Open;
                if (fy.Status == FiscalYearStatus.Closed)
                {
                    fy.ClosedAt = DateTime.UtcNow;
                    fy.ClosedBy = User.Identity?.Name ?? "system";
                }
                else
                {
                    fy.ClosedAt = null;
                    fy.ClosedBy = null;
                }
                await _db.SaveChangesAsync();
                SuccessMessage = $"Fiscal year {fy.Year} is now {fy.Status}.";
            }

            CompanyId = companyId;
            await LoadAsync();
            return Page();
        }

        private async Task LoadAsync()
        {
            var companies = await _db.Companies
                .Where(c => c.IsActive && _tenantContext.VisibleCompanyIds.Contains(c.Id))
                .OrderBy(c => c.Name)
                .ToListAsync();

            CompanyOptions = companies
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.CompanyCode} — {c.Name}"
                })
                .ToList();

            // Default to the first visible company on first load.
            if (!CompanyId.HasValue && companies.Any())
                CompanyId = companies.First().Id;

            if (!CompanyId.HasValue)
            {
                Years = new List<FiscalYear>();
                PeriodsByYear = new Dictionary<int, List<FiscalPeriod>>();
                return;
            }

            Years = await _db.FiscalYears
                .Where(fy => fy.CompanyId == CompanyId.Value)
                .OrderByDescending(fy => fy.Year)
                .ToListAsync();

            var yearIds = Years.Select(fy => fy.Id).ToList();
            var allPeriods = await _db.FiscalPeriods
                .Where(p => yearIds.Contains(p.FiscalYearId))
                .OrderBy(p => p.PeriodNumber)
                .ToListAsync();

            PeriodsByYear = allPeriods
                .GroupBy(p => p.FiscalYearId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.PeriodNumber).ToList());
        }
    }
}
