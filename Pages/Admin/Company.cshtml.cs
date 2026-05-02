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
    public class CompanyModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public CompanyModel(AppDbContext context, IWebHostEnvironment env, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _env = env;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        [BindProperty]
        public Company Company { get; set; } = new();

        public List<Company> AllCompanies { get; set; } = new();
        public List<SelectListItem> CountryOptions { get; set; } = new();
        public List<SelectListItem> CurrencyOptions { get; set; } = new();
        public List<SelectListItem> LanguageOptions { get; set; } = new();
        public List<SelectListItem> TimezoneOptions { get; set; } = new();

        public string? SuccessMessage { get; set; }

        private async Task LoadLookupOptionsAsync()
        {
            CountryOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "Country", Company?.Country, "");
            CurrencyOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "Currency", Company?.Currency, "");
            LanguageOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "Language", Company?.DefaultLanguage, "");
            TimezoneOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "Timezone", Company?.TimeZone, "");
        }

        public async Task<IActionResult> OnGetAsync()
        {
            AllCompanies = await _context.Companies
                .Include(c => c.ChildCompanies)
                .OrderBy(c => c.ParentCompanyId == null ? 0 : 1)
                .ThenBy(c => c.Name)
                .ToListAsync();
            
            var company = AllCompanies.FirstOrDefault(c => c.ParentCompanyId == null) 
                ?? AllCompanies.FirstOrDefault();
            if (company != null)
                Company = company;
            await LoadLookupOptionsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var existing = await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (existing == null)
            {
                Company.CreatedAt = DateTime.UtcNow;
                _context.Companies.Add(Company);
            }
            else
            {
                existing.Name = Company.Name;
                existing.LegalName = Company.LegalName;
                existing.Currency = Company.Currency;
                existing.TaxId = Company.TaxId;
                existing.PeriodType = Company.PeriodType;
                existing.FiscalYearStartMonth = Company.FiscalYearStartMonth;
                existing.FiscalYearStartDay = Company.FiscalYearStartDay;
                existing.Address = Company.Address;
                existing.City = Company.City;
                existing.StateProvince = Company.StateProvince;
                existing.PostalCode = Company.PostalCode;
                existing.Country = Company.Country;
                existing.ContactName = Company.ContactName;
                existing.ContactEmail = Company.ContactEmail;
                existing.ContactPhone = Company.ContactPhone;
                existing.DefaultDepMethod = Company.DefaultDepMethod;
                existing.DefaultConvention = Company.DefaultConvention;
                existing.GstHstNumber = Company.GstHstNumber;
                existing.PstNumber = Company.PstNumber;
                existing.BusinessNumber = Company.BusinessNumber;
                existing.DefaultLanguage = Company.DefaultLanguage;
                existing.TimeZone = Company.TimeZone;
                existing.ApprovalThreshold = Company.ApprovalThreshold;
                existing.RequireApprovalForDisposals = Company.RequireApprovalForDisposals;
                existing.RequireApprovalForTransfers = Company.RequireApprovalForTransfers;
                existing.CompanyStructure = Company.CompanyStructure;
                existing.FinancialMode = Company.FinancialMode;
                existing.IntegrationType = Company.IntegrationType;
                existing.EnableWorkOrders = Company.EnableWorkOrders;
                existing.EnablePurchasing = Company.EnablePurchasing;
                existing.EnableAccountsPayable = Company.EnableAccountsPayable;
                existing.EnableVendors = Company.EnableVendors;
                existing.EnableInventory = Company.EnableInventory;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            SuccessMessage = "Company settings saved successfully.";
            return Page();
        }

        public async Task<IActionResult> OnPostUploadLogoAsync(IFormFile logo)
        {
            if (logo == null || logo.Length == 0)
                return RedirectToPage();

            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
            if (!allowedTypes.Contains(logo.ContentType))
                return RedirectToPage();

            var fileName = $"logo_{Guid.NewGuid()}{Path.GetExtension(logo.FileName)}";
            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await logo.CopyToAsync(stream);
            }

            var company = await _context.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
            if (company != null)
            {
                company.LogoPath = $"/uploads/{fileName}";
                company.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
