using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Reports
{
    public class ComplianceModel : PageModel
    {
        private readonly ReportBuilderService _reportService;
        private readonly IModuleGuardService _moduleGuard;

        public ComplianceModel(ReportBuilderService reportService,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _reportService = reportService;
        }

        public int Year { get; set; }
        public DepreciationScheduleReport DepreciationSchedule { get; set; } = new();
        public TaxSummaryReport TaxSummary { get; set; } = new();
        public AuditTrailReport AuditTrail { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? year)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });


            Year = year ?? DateTime.Now.Year;
            
            DepreciationSchedule = await _reportService.GetDepreciationScheduleAsync(Year);
            TaxSummary = await _reportService.GetTaxSummaryAsync(Year);
            AuditTrail = await _reportService.GetAuditTrailAsync();
        
            return Page();
        }
    }
}
