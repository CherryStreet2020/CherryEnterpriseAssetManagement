using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Reports
{
    public class Form4562Model : PageModel
    {
        private readonly AppDbContext _db;
        private readonly UsTaxService _usTaxService;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public Form4562Model(AppDbContext db, UsTaxService usTaxService,
            IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _usTaxService = usTaxService;
            _tenantContext = tenantContext;
        }

        public int TaxYear { get; set; } = DateTime.Now.Year;
        public int? SelectedCompanyId { get; set; }
        public List<Company> Companies { get; set; } = new();
        
        public decimal Section179Limit { get; set; }
        public decimal Section179Threshold { get; set; }
        public decimal Section179LimitAdjusted { get; set; }
        public decimal TotalSection179Cost { get; set; }
        public decimal TotalSection179 { get; set; }
        public decimal TotalBonusDepreciation { get; set; }
        public decimal TotalMacrsDepreciation { get; set; }
        public decimal BonusRate { get; set; }
        
        public List<UsTaxSettings> Section179Assets { get; set; } = new();
        public List<UsTaxSettings> BonusDepreciationAssets { get; set; } = new();
        public List<MacrsClassSummary> MacrsClassSummary { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? taxYear, int? companyId)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("reports"))
                return RedirectToPage("/ModuleDisabled", new { module = "Reports" });


            TaxYear = taxYear ?? DateTime.Now.Year;
            var visibleIds = _tenantContext.VisibleCompanyIds;
            SelectedCompanyId = companyId.HasValue && visibleIds.Contains(companyId.Value) ? companyId : null;
            
            Companies = await _db.Companies.Where(c => c.IsActive && visibleIds.Contains(c.Id)).ToListAsync();

            var limits = await _usTaxService.GetSection179LimitsAsync(TaxYear);
            if (limits != null)
            {
                Section179Limit = limits.MaxDeduction;
                Section179Threshold = limits.PhaseoutThreshold;
            }

            BonusRate = await _usTaxService.GetBonusDepreciationRateAsync(TaxYear);

            var query = _db.UsTaxSettings
                .Include(t => t.Asset)
                .Where(t => t.TaxYear == TaxYear)
                .Where(t => t.Asset != null && visibleIds.Contains(t.Asset.CompanyId ?? 0));

            if (SelectedCompanyId.HasValue)
            {
                query = query.Where(t => t.Asset != null && t.Asset.CompanyId == SelectedCompanyId);
            }

            var allSettings = await query.ToListAsync();

            Section179Assets = allSettings.Where(t => t.Section179Elected && t.Section179Amount > 0).ToList();
            TotalSection179Cost = Section179Assets.Sum(t => t.DepreciableBasis);
            TotalSection179 = Section179Assets.Sum(t => t.Section179Amount);

            var phaseoutReduction = Math.Max(0, TotalSection179Cost - Section179Threshold);
            Section179LimitAdjusted = Math.Max(0, Section179Limit - phaseoutReduction);

            BonusDepreciationAssets = allSettings.Where(t => t.BonusDepreciationAmount > 0).ToList();
            TotalBonusDepreciation = BonusDepreciationAssets.Sum(t => t.BonusDepreciationAmount);

            var macrsGroups = allSettings
                .GroupBy(t => new { t.PropertyClass, t.Convention })
                .Select(g => new MacrsClassSummary
                {
                    RecoveryPeriod = (int)g.Key.PropertyClass,
                    Convention = g.Key.Convention.ToString(),
                    AssetCount = g.Count(),
                    TotalBasis = g.Sum(t => t.DepreciableBasis - t.Section179Amount - t.BonusDepreciationAmount),
                    CurrentYearDepreciation = CalculateMacrsForGroup(g.ToList(), TaxYear)
                })
                .Where(g => g.TotalBasis > 0)
                .OrderBy(g => g.RecoveryPeriod)
                .ToList();

            MacrsClassSummary = macrsGroups;
            TotalMacrsDepreciation = macrsGroups.Sum(g => g.CurrentYearDepreciation);
        
            return Page();
        }

        private decimal CalculateMacrsForGroup(List<UsTaxSettings> settings, int taxYear)
        {
            decimal total = 0;
            foreach (var s in settings)
            {
                if (s.PlacedInServiceDate.HasValue)
                {
                    var yearInService = taxYear - s.PlacedInServiceDate.Value.Year + 1;
                    var basis = s.DepreciableBasis - s.Section179Amount - s.BonusDepreciationAmount;
                    if (basis > 0 && yearInService > 0)
                    {
                        total += _usTaxService.CalculateMacrsDepreciation(basis, s.PropertyClass, s.Convention, yearInService, s.UseADS);
                    }
                }
            }
            return total;
        }
    }

    public class MacrsClassSummary
    {
        public int RecoveryPeriod { get; set; }
        public string Convention { get; set; } = string.Empty;
        public int AssetCount { get; set; }
        public decimal TotalBasis { get; set; }
        public decimal CurrentYearDepreciation { get; set; }
    }
}
