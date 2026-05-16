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

namespace Abs.FixedAssets.Pages.Periods
{
    // MP #112 — Period Close Orchestration: Index of all fiscal periods
    // grouped by year per company. Auditor-facing (read-only here);
    // close action is on the separate /Periods/Close screen.
    [Authorize(Roles = "Admin,Manager,Accountant,Finance")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;

        public IndexModel(AppDbContext db, ITenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        public List<SelectListItem> CompanyOptions { get; set; } = new();

        [BindProperty(SupportsGet = true)] public int? CompanyId { get; set; }

        public List<FiscalYear> Years { get; set; } = new();
        public Dictionary<int, List<FiscalPeriod>> PeriodsByYear { get; set; } = new();
        public FiscalPeriod? NextCloseable { get; set; }
        public string? CompanyName { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var companies = await _db.Companies
                .Where(c => c.IsActive && _tenant.VisibleCompanyIds.Contains(c.Id))
                .OrderBy(c => c.Name)
                .ToListAsync();

            CompanyOptions = companies
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = $"{c.CompanyCode} — {c.Name}"
                })
                .ToList();

            if (!CompanyId.HasValue && companies.Any())
                CompanyId = companies.First().Id;

            if (!CompanyId.HasValue) return Page();

            CompanyName = companies.FirstOrDefault(c => c.Id == CompanyId.Value)?.Name;

            Years = await _db.FiscalYears
                .Where(fy => fy.CompanyId == CompanyId.Value)
                .OrderByDescending(fy => fy.Year)
                .ToListAsync();

            var yearIds = Years.Select(fy => fy.Id).ToList();
            var periods = await _db.FiscalPeriods
                .Where(p => yearIds.Contains(p.FiscalYearId))
                .OrderBy(p => p.PeriodNumber)
                .ToListAsync();

            PeriodsByYear = periods.GroupBy(p => p.FiscalYearId)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.PeriodNumber).ToList());

            // Surface "what's the next period the user could close" so the
            // big call-to-action card on top can deep-link to /Periods/Close.
            var today = DateTime.UtcNow.Date;
            NextCloseable = periods
                .Where(p => p.Status == PeriodStatus.Open && p.EndDate <= today)
                .OrderBy(p => p.StartDate)
                .FirstOrDefault();

            return Page();
        }
    }
}
