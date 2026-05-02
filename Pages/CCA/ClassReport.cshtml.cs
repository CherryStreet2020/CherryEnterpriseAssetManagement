using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.CCA
{
    public class ClassReportModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly CcaService _ccaService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public ClassReportModel(AppDbContext db, CcaService ccaService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _ccaService = ccaService;
            _tenantContext = tenantContext;
        }

        [BindProperty(SupportsGet = true)]
        public int FiscalYear { get; set; } = DateTime.Now.Year;

        public List<CcaClassBalance> Balances { get; set; } = new();
        public List<int> AvailableYears { get; set; } = new();
        public decimal TotalOpeningUcc { get; set; }
        public decimal TotalAdditions { get; set; }
        public decimal TotalDispositions { get; set; }
        public decimal TotalCcaClaimed { get; set; }
        public decimal TotalClosingUcc { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "CCA" });


            AvailableYears = Enumerable.Range(DateTime.Now.Year - 5, 6).Reverse().ToList();

            Balances = await _db.CcaClassBalances
                .Include(b => b.CcaClass)
                .Where(b => b.FiscalYear == FiscalYear)
                .OrderBy(b => b.CcaClass.ClassNumber)
                .ToListAsync();

            TotalOpeningUcc = Balances.Sum(b => b.OpeningUcc);
            TotalAdditions = Balances.Sum(b => b.Additions);
            TotalDispositions = Balances.Sum(b => b.Dispositions);
            TotalCcaClaimed = Balances.Sum(b => b.CcaClaimed);
            TotalClosingUcc = Balances.Sum(b => b.ClosingUcc);
        
            return Page();
        }

        public async Task<IActionResult> OnPostCalculateAsync(int fiscalYear)
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var classes = await _db.CcaClasses.Where(c => c.Active).ToListAsync();
            
            foreach (var ccaClass in classes)
            {
                try
                {
                    await _ccaService.CalculateCcaForClassAsync(ccaClass.Id, fiscalYear);
                }
                catch (Exception)
                {
                    // Skip classes with issues
                }
            }

            return RedirectToPage(new { FiscalYear = fiscalYear });
        }
    }
}
