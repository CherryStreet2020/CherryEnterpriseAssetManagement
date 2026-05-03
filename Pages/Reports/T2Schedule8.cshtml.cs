using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Reports
{
    public class T2Schedule8Model : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public T2Schedule8Model(AppDbContext db,
            IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _tenantContext = tenantContext;
        }

        public int FiscalYear { get; set; } = DateTime.Now.Year;
        public int? SelectedCompanyId { get; set; }
        public List<Company> Companies { get; set; } = new();
        
        public List<CcaClassBalance> ClassBalances { get; set; } = new();
        public List<AssetTaxSettings> AdditionsThisYear { get; set; } = new();
        public List<AssetTaxSettings> DispositionsThisYear { get; set; } = new();
        
        public decimal TotalOpeningUcc { get; set; }
        public decimal TotalAdditions { get; set; }
        public decimal TotalDispositions { get; set; }
        public decimal TotalHalfYearAdj { get; set; }
        public decimal TotalBaseForCca { get; set; }
        public decimal TotalCcaClaimed { get; set; }
        public decimal TotalRecapture { get; set; }
        public decimal TotalTerminalLoss { get; set; }
        public decimal TotalClosingUcc { get; set; }

        public async Task<IActionResult> OnGetAsync(int? fiscalYear, int? companyId)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });


            FiscalYear = fiscalYear ?? DateTime.Now.Year;
            var visibleIds = _tenantContext.VisibleCompanyIds;
            SelectedCompanyId = companyId.HasValue && visibleIds.Contains(companyId.Value) ? companyId : null;
            
            Companies = await _db.Companies
                .Where(c => c.IsActive && visibleIds.Contains(c.Id))
                .ToListAsync();

            // Scope to the chosen subsidiary if one is selected; otherwise
            // limit to companies the user is allowed to see.
            var balanceQuery = _db.CcaClassBalances
                .Include(b => b.CcaClass)
                .Where(b => b.FiscalYear == FiscalYear);
            if (SelectedCompanyId.HasValue)
                balanceQuery = balanceQuery.Where(b => b.CompanyId == SelectedCompanyId.Value);
            else
                balanceQuery = balanceQuery.Where(b => visibleIds.Contains(b.CompanyId));
            ClassBalances = await balanceQuery
                .OrderBy(b => b.CcaClass.ClassNumber)
                .ToListAsync();

            TotalOpeningUcc = ClassBalances.Sum(b => b.OpeningUcc);
            TotalAdditions = ClassBalances.Sum(b => b.Additions);
            TotalDispositions = ClassBalances.Sum(b => b.Dispositions);
            TotalHalfYearAdj = ClassBalances.Sum(b => b.HalfYearAdjustment);
            TotalBaseForCca = ClassBalances.Sum(b => b.BaseForCca);
            TotalCcaClaimed = ClassBalances.Sum(b => b.CcaClaimed);
            TotalRecapture = ClassBalances.Sum(b => b.Recapture ?? 0);
            TotalTerminalLoss = ClassBalances.Sum(b => b.TerminalLoss ?? 0);
            TotalClosingUcc = ClassBalances.Sum(b => b.ClosingUcc);

            var yearStart = new DateTime(FiscalYear, 1, 1);
            var yearEnd = new DateTime(FiscalYear, 12, 31);

            AdditionsThisYear = await _db.AssetTaxSettings
                .Include(t => t.Asset)
                .Include(t => t.CcaClass)
                .Where(t => t.AvailableForUseDate >= yearStart && t.AvailableForUseDate <= yearEnd)
                .Where(t => t.Asset != null && visibleIds.Contains(t.Asset.CompanyId ?? 0))
                .ToListAsync();

            DispositionsThisYear = await _db.AssetTaxSettings
                .Include(t => t.Asset)
                .Include(t => t.CcaClass)
                .Where(t => t.DisposalDate >= yearStart && t.DisposalDate <= yearEnd)
                .Where(t => t.Asset != null && visibleIds.Contains(t.Asset.CompanyId ?? 0))
                .ToListAsync();
        
            return Page();
        }
    }
}
