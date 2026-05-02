using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using ClosedXML.Excel;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Reports
{
    public class ChartOfAccountsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public ChartOfAccountsModel(AppDbContext context,
            IModuleGuardService moduleGuard,
            ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _tenantContext = tenantContext;
        }

        public List<GlAccount> Accounts { get; set; } = new();
        public Dictionary<GlAccountType, int> AccountTypeCounts { get; set; } = new();
        public Dictionary<GlAccountCategory, int> CategoryCounts { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? FilterType { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FilterCategory { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Search { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });


            var visibleIds = _tenantContext.VisibleCompanyIds;
            var query = _context.GlAccounts.Where(a => visibleIds.Contains(a.CompanyId ?? 0));

            if (!string.IsNullOrEmpty(FilterType) && Enum.TryParse<GlAccountType>(FilterType, out var accountType))
            {
                query = query.Where(a => a.AccountType == accountType);
            }

            if (!string.IsNullOrEmpty(FilterCategory) && Enum.TryParse<GlAccountCategory>(FilterCategory, out var category))
            {
                query = query.Where(a => a.Category == category);
            }

            if (!string.IsNullOrEmpty(Search))
            {
                var searchLower = Search.ToLower();
                query = query.Where(a => 
                    a.AccountNumber.ToLower().Contains(searchLower) ||
                    a.Name.ToLower().Contains(searchLower) ||
                    (a.Description != null && a.Description.ToLower().Contains(searchLower)));
            }

            Accounts = await query.OrderBy(a => a.AccountNumber).ToListAsync();

            var allAccounts = await _context.GlAccounts.Where(a => visibleIds.Contains(a.CompanyId ?? 0)).ToListAsync();
            AccountTypeCounts = allAccounts.GroupBy(a => a.AccountType).ToDictionary(g => g.Key, g => g.Count());
            CategoryCounts = allAccounts.GroupBy(a => a.Category).ToDictionary(g => g.Key, g => g.Count());
        
            return Page();
        }

        public async Task<IActionResult> OnGetExportAsync()
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var accounts = await _context.GlAccounts.Where(a => visibleIds.Contains(a.CompanyId ?? 0)).OrderBy(a => a.AccountNumber).ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Chart of Accounts");

            worksheet.Cell(1, 1).Value = "Account Number";
            worksheet.Cell(1, 2).Value = "Account Name";
            worksheet.Cell(1, 3).Value = "Account Type";
            worksheet.Cell(1, 4).Value = "Category";
            worksheet.Cell(1, 5).Value = "Normal Balance";
            worksheet.Cell(1, 6).Value = "Description";
            worksheet.Cell(1, 7).Value = "Is Active";

            var headerRange = worksheet.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

            int row = 2;
            foreach (var account in accounts)
            {
                worksheet.Cell(row, 1).Value = account.AccountNumber;
                worksheet.Cell(row, 2).Value = account.Name;
                worksheet.Cell(row, 3).Value = account.AccountType.ToString();
                worksheet.Cell(row, 4).Value = account.Category.ToString();
                worksheet.Cell(row, 5).Value = account.NormalBalance.ToString();
                worksheet.Cell(row, 6).Value = account.Description ?? "";
                worksheet.Cell(row, 7).Value = account.IsActive ? "Yes" : "No";
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(), 
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"ChartOfAccounts_{DateTime.Now:yyyyMMdd}.xlsx");
        }
    }
}
