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
    // PR #100 (B-02): Viewer was listed alongside Admin/Accountant, granting
    // read-only users full edit access to the Vendor master — a copy-paste
    // typo of the Index page's gate. Viewer removed; Vendor edits now require
    // Admin or Accountant. Same posture as the rest of the Materials write
    // surface (e.g. Items/Edit.cshtml.cs).
    [Authorize(Roles = "Admin,Accountant")]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public EditModel(AppDbContext db, IModuleGuardService moduleGuard, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public Vendor Vendor { get; set; } = null!;
        public string Mode { get; set; } = "view";
        public bool IsEditMode => Mode == "edit";
        public bool IsViewMode => Mode == "view";
        public List<SelectListItem> VendorTypeOptions { get; set; } = new();
        public List<SelectListItem> PaymentTermsOptions { get; set; } = new();
        public List<PurchaseOrder> RecentPOs { get; set; } = new();
        public int POCount { get; set; }
        public decimal TotalPOValue { get; set; }
        public string ActiveTab { get; set; } = "info";

        public async Task<IActionResult> OnGetAsync(int id, string? mode, string? tab)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("vendors"))
                return RedirectToPage("/ModuleDisabled", new { module = "Vendor Management" });

            var vendor = await LoadVendorAsync(id);
            if (vendor == null) return RedirectToPage("/Admin/Vendors");

            Vendor = vendor;
            Mode = mode ?? "view";
            ActiveTab = tab ?? "info";

            if (IsEditMode && !User.IsInRole("Admin") && !User.IsInRole("Accountant"))
                Mode = "view";

            await LoadFormDataAsync();
            await LoadPurchaseHistoryAsync(id);
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string code, string name, int vendorType, int paymentTerms, string? taxId, decimal? creditLimit, bool is1099Vendor, bool isPreferred, string? contactName, string? phone, string? fax, string? email, string? website, string? address, string? city, string? state, string? postalCode, string? country, string? notes, string? accountNumber, string? currency, string? legalName, bool isActive, string? activeTab)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Accountant"))
                return Forbid();

            var vendor = await LoadVendorAsync(id);
            if (vendor == null)
            {
                TempData["Error"] = "Vendor not found.";
                return RedirectToPage("/Admin/Vendors");
            }

            if (activeTab == "contact")
            {
                vendor.ContactName = contactName;
                vendor.Phone = phone;
                vendor.Fax = fax;
                vendor.Email = email;
                vendor.Website = website;
                vendor.Address = address;
                vendor.City = city;
                vendor.State = state;
                vendor.PostalCode = postalCode;
                vendor.Country = country;
            }
            else
            {
                vendor.Code = code;
                vendor.Name = name;
                vendor.LegalName = legalName;
                vendor.VendorType = (VendorType)vendorType;
                vendor.PaymentTerms = (PaymentTerms)paymentTerms;
                vendor.TaxId = taxId;
                vendor.CreditLimit = creditLimit;
                vendor.Is1099Vendor = is1099Vendor;
                vendor.IsPreferred = isPreferred;
                vendor.AccountNumber = accountNumber;
                vendor.Currency = currency ?? "USD";
                vendor.Notes = notes;
                vendor.IsActive = isActive;
            }
            vendor.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Vendor {vendor.Code} updated successfully.";
            return RedirectToPage(new { id, mode = "view", tab = activeTab ?? "info" });
        }

        public async Task<IActionResult> OnPostDuplicateAsync(int id)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Accountant"))
                return Forbid();

            var source = await LoadVendorAsync(id);
            if (source == null)
            {
                TempData["Error"] = "Vendor not found.";
                return RedirectToPage("/Admin/Vendors");
            }

            var newCode = $"{source.Code}-COPY";
            var counter = 1;
            while (await _db.Vendors.AnyAsync(v => v.Code == newCode))
            {
                newCode = $"{source.Code}-COPY{counter++}";
            }

            var maxSortOrder = await _db.Vendors.MaxAsync(v => (int?)v.SortOrder) ?? 0;

            var newVendor = new Vendor
            {
                Code = newCode,
                Name = $"{source.Name} (Copy)",
                LegalName = source.LegalName,
                VendorType = source.VendorType,
                PaymentTerms = source.PaymentTerms,
                TaxId = source.TaxId,
                CreditLimit = source.CreditLimit,
                Is1099Vendor = source.Is1099Vendor,
                IsPreferred = source.IsPreferred,
                ContactName = source.ContactName,
                Phone = source.Phone,
                Fax = source.Fax,
                Email = source.Email,
                Website = source.Website,
                Address = source.Address,
                City = source.City,
                State = source.State,
                PostalCode = source.PostalCode,
                Country = source.Country,
                Notes = $"Duplicated from {source.Code}",
                AccountNumber = source.AccountNumber,
                Currency = source.Currency,
                IsActive = true,
                SortOrder = maxSortOrder + 10,
                CompanyId = _tenantContext.CompanyId
            };

            _db.Vendors.Add(newVendor);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Vendor duplicated as {newCode}.";
            return RedirectToPage(new { id = newVendor.Id, mode = "view" });
        }

        public async Task<IActionResult> OnPostToggleStatusAsync(int id)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Accountant"))
                return Forbid();

            var vendor = await LoadVendorAsync(id);
            if (vendor == null)
            {
                TempData["Error"] = "Vendor not found.";
                return RedirectToPage("/Admin/Vendors");
            }

            vendor.IsActive = !vendor.IsActive;
            vendor.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Vendor {vendor.Code} {(vendor.IsActive ? "activated" : "deactivated")}.";
            return RedirectToPage(new { id, mode = "view" });
        }

        private async Task<Vendor?> LoadVendorAsync(int id)
        {
            return await _db.Vendors
                .Where(v => (v.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0)) && v.Id == id)
                .FirstOrDefaultAsync();
        }

        private async Task LoadFormDataAsync()
        {
            VendorTypeOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "VendorType", null, "-- Select --");
            PaymentTermsOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "PaymentTerms", null, "-- Select --");
        }

        private async Task LoadPurchaseHistoryAsync(int vendorId)
        {
            var poQuery = _db.PurchaseOrders.Where(po => po.VendorId == vendorId && (po.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(po.CompanyId ?? 0)));
            POCount = await poQuery.CountAsync();
            TotalPOValue = await poQuery.SumAsync(po => (decimal?)po.Total) ?? 0;
            RecentPOs = await poQuery
                .OrderByDescending(po => po.OrderDate)
                .Take(10)
                .ToListAsync();
        }
    }
}
