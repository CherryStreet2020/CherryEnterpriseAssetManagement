using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Reports
{
    public class ReportHubModel : PageModel
    {
        private readonly IModuleGuardService _moduleGuard;

        public ReportHubModel(IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
        }

        public class ReportLink
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Url { get; set; } = string.Empty;
            public string Icon { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Color { get; set; } = "primary";
            public bool IsNew { get; set; }
        }

        public List<ReportLink> DepreciationReports { get; set; } = new();
        public List<ReportLink> AssetReports { get; set; } = new();
        public List<ReportLink> FinancialReports { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });
            DepreciationReports = new List<ReportLink>
            {
                new ReportLink
                {
                    Title = "Depreciation Preview",
                    Description = "Preview depreciation calculations before posting to journals. See projected values for any period.",
                    Url = "/Reports/DepreciationPreview",
                    Icon = "calculator",
                    Color = "primary"
                },
                new ReportLink
                {
                    Title = "Run Depreciation",
                    Description = "Generate monthly depreciation journal entries for all active assets.",
                    Url = "/Journals",
                    Icon = "calendar",
                    Color = "primary"
                },
                new ReportLink
                {
                    Title = "Depreciation Books",
                    Description = "View and manage GAAP and Tax depreciation books.",
                    Url = "/Books",
                    Icon = "compare",
                    Color = "primary"
                }
            };

            AssetReports = new List<ReportLink>
            {
                new ReportLink
                {
                    Title = "Custom Report Builder",
                    Description = "Build custom reports by selecting fields, filters, and criteria. Export in any format.",
                    Url = "/Reports/Builder",
                    Icon = "chart",
                    Color = "success",
                    IsNew = true
                },
                // PR #116a: removed Compliance Reports, Export Assets,
                // Maintenance Spend by Asset entries — underlying pages
                // were orphans. Per-asset cost rollup now lives on
                // /Reports/AssetReliability + on the Asset detail page.
                new ReportLink
                {
                    Title = "Asset Register",
                    Description = "Complete listing of all fixed assets with acquisition details, locations, and current values.",
                    Url = "/Assets",
                    Icon = "list",
                    Color = "success"
                }
            };

            FinancialReports = new List<ReportLink>
            {
                new ReportLink
                {
                    Title = "Chart of Accounts",
                    Description = "Complete listing of all GL accounts with account types, categories, and balances.",
                    Url = "/Reports/ChartOfAccounts",
                    Icon = "ledger",
                    Color = "warning",
                    IsNew = true
                },
                new ReportLink
                {
                    Title = "Journal Entries",
                    Description = "View and export all depreciation journal entries for GL posting.",
                    Url = "/Journals",
                    Icon = "document",
                    Color = "warning"
                },
                new ReportLink
                {
                    Title = "CCA Tax Classes",
                    Description = "Canadian Capital Cost Allowance classes and rates.",
                    Url = "/CCA",
                    Icon = "bank",
                    Color = "warning"
                },
                new ReportLink
                {
                    Title = "CCA Schedule",
                    Description = "UCC rollforward report for CRA tax filing.",
                    Url = "/CCA/ClassReport",
                    Icon = "tax",
                    Color = "warning"
                }
            };

            return Page();
        }
    }
}
