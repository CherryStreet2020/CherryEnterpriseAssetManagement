using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.UsTax
{
    public class IndexModel : PageModel
    {
        private readonly UsTaxService _usTaxService;
        private readonly IModuleGuardService _moduleGuard;

        public IndexModel(UsTaxService usTaxService,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _usTaxService = usTaxService;
        }

        public int CurrentYear { get; set; }
        public decimal CurrentSection179Limit { get; set; }
        public decimal CurrentBonusRate { get; set; }
        public int AssetsWithUsTax { get; set; }
        public List<Section179Limits> Section179Limits { get; set; } = new();
        public List<BonusDepreciationRates> BonusRates { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "US Tax" });


            CurrentYear = DateTime.Now.Year;
            
            var currentLimits = await _usTaxService.GetSection179LimitsAsync(CurrentYear);
            CurrentSection179Limit = currentLimits?.MaxDeduction ?? 0;
            
            CurrentBonusRate = await _usTaxService.GetBonusDepreciationRateAsync(CurrentYear);
            
            var allSettings = await _usTaxService.GetAllUsTaxSettingsAsync();
            AssetsWithUsTax = allSettings.Count;
            
            Section179Limits = await _usTaxService.GetAllSection179LimitsAsync();
            BonusRates = await _usTaxService.GetAllBonusRatesAsync();
        
            return Page();
        }
    }
}
