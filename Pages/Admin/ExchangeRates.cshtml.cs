using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ExchangeRatesModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public ExchangeRatesModel(AppDbContext context, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<ExchangeRate> Rates { get; set; } = new();
        public List<SelectListItem> CurrencyOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }

        private async Task LoadLookupsAsync()
        {
            CurrencyOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "Currency", null, "");
        }

        public async Task OnGetAsync()
        {
            Rates = await _context.ExchangeRates
                .OrderByDescending(r => r.EffectiveDate)
                .ToListAsync();
            await LoadLookupsAsync();
        }

        public async Task<IActionResult> OnPostAddAsync(
            string fromCurrency,
            string toCurrency,
            decimal rate,
            DateTime effectiveDate,
            string? source)
        {
            var exchangeRate = new ExchangeRate
            {
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Rate = rate,
                EffectiveDate = effectiveDate,
                Source = source,
                CreatedBy = User.Identity?.Name
            };

            _context.ExchangeRates.Add(exchangeRate);
            await _context.SaveChangesAsync();

            SuccessMessage = "Exchange rate added successfully.";
            Rates = await _context.ExchangeRates.OrderByDescending(r => r.EffectiveDate).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostEditAsync(int id, string fromCurrency, string toCurrency, decimal rate, DateTime effectiveDate)
        {
            var existing = await _context.ExchangeRates
                .Where(r => r.Id == id)
                .FirstOrDefaultAsync();
            if (existing != null)
            {
                existing.FromCurrency = fromCurrency;
                existing.ToCurrency = toCurrency;
                existing.Rate = rate;
                existing.EffectiveDate = effectiveDate;
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var rate = await _context.ExchangeRates
                .Where(r => r.Id == id)
                .FirstOrDefaultAsync();
            if (rate != null)
            {
                _context.ExchangeRates.Remove(rate);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
