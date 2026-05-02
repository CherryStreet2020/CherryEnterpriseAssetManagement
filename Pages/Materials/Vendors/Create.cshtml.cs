using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;

namespace Abs.FixedAssets.Pages.Materials.Vendors
{
    [Authorize(Roles = "Admin,Accountant")]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public CreateModel(AppDbContext db, IModuleGuardService moduleGuard, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<SelectListItem> VendorTypeOptions { get; set; } = new();
        public List<SelectListItem> PaymentTermsOptions { get; set; } = new();
        public string? NextCode { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("vendors"))
                return RedirectToPage("/ModuleDisabled", new { module = "Vendor Management" });

            await LoadFormDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string code, string name, int vendorType, int paymentTerms, string? taxId, decimal? creditLimit, bool is1099Vendor, string? contactName, string? phone, string? email, string? website, string? address, string? city, string? state, string? postalCode, string? country, string? notes)
        {
            if (await _db.Vendors.AnyAsync(v => v.Code == code))
            {
                TempData["Error"] = "A vendor with this code already exists.";
                await LoadFormDataAsync();
                return Page();
            }

            var maxSortOrder = await _db.Vendors.MaxAsync(v => (int?)v.SortOrder) ?? 0;

            var vendor = new Vendor
            {
                Code = code,
                Name = name,
                VendorType = (VendorType)vendorType,
                PaymentTerms = (PaymentTerms)paymentTerms,
                TaxId = taxId,
                CreditLimit = creditLimit,
                Is1099Vendor = is1099Vendor,
                ContactName = contactName,
                Phone = phone,
                Email = email,
                Website = website,
                Address = address,
                City = city,
                State = state,
                PostalCode = postalCode,
                Country = country,
                Notes = notes,
                IsActive = true,
                SortOrder = maxSortOrder + 10,
                CompanyId = _tenantContext.CompanyId
            };

            _db.Vendors.Add(vendor);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Vendor {code} created successfully.";
            return RedirectToPage("/Materials/Vendors/Edit", new { id = vendor.Id });
        }

        private async Task LoadFormDataAsync()
        {
            VendorTypeOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "VendorType", null, "-- Select --");
            PaymentTermsOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "PaymentTerms", null, "-- Select --");

            var maxCode = await _db.Vendors
                .Where(v => v.Code.StartsWith("VND-"))
                .Select(v => v.Code)
                .OrderByDescending(c => c)
                .FirstOrDefaultAsync();

            if (maxCode != null && int.TryParse(maxCode.Replace("VND-", ""), out var num))
                NextCode = $"VND-{(num + 1):D4}";
            else
                NextCode = $"VND-{(await _db.Vendors.CountAsync() + 1):D4}";
        }
    }
}
