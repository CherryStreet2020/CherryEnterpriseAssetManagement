using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.AccountsPayable
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;

        public CreateModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext, ILookupService lookupService)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
        }

        public List<Vendor> Vendors { get; set; } = new();
        public List<SelectListItem> PaymentTermsOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("accountspayable"))
                return RedirectToPage("/ModuleDisabled", new { module = "Accounts Payable" });

            await LoadFormDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int vendorId, string invoiceNumber, DateTime invoiceDate, decimal amount, DateTime? dueDate, string? description)
        {
            var companyId = _tenantContext.CompanyId;

            if (!companyId.HasValue)
            {
                TempData["Error"] = "Cannot create invoice: No company context available.";
                await LoadFormDataAsync();
                return Page();
            }

            if (vendorId <= 0)
            {
                TempData["Error"] = "Please select a vendor.";
                await LoadFormDataAsync();
                return Page();
            }

            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                TempData["Error"] = "Invoice number is required.";
                await LoadFormDataAsync();
                return Page();
            }

            var vendor = await _context.Vendors
                .Where(v => v.Id == vendorId && _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0))
                .FirstOrDefaultAsync();

            if (vendor == null)
            {
                TempData["Error"] = "Cannot create invoice: Vendor not found or not accessible.";
                await LoadFormDataAsync();
                return Page();
            }

            var calculatedDueDate = dueDate ?? invoiceDate.AddDays(vendor.PaymentTerms switch
            {
                PaymentTerms.Net30 => 30,
                PaymentTerms.Net45 => 45,
                PaymentTerms.Net60 => 60,
                PaymentTerms.Net90 => 90,
                PaymentTerms.DueOnReceipt => 0,
                _ => 30
            });

            var invoice = new VendorInvoice
            {
                InvoiceNumber = invoiceNumber,
                VendorId = vendorId,
                CompanyId = companyId.Value,
                InvoiceDate = invoiceDate,
                ReceivedDate = DateTime.Today,
                DueDate = calculatedDueDate,
                PaymentTerms = vendor.PaymentTerms,
                Subtotal = amount,
                Total = amount,
                BalanceDue = amount,
                Status = InvoiceStatus.PendingApproval,
                MatchStatus = InvoiceMatchStatus.NotMatched,
                Notes = description,
                CreatedAt = DateTime.UtcNow
            };

            _context.VendorInvoices.Add(invoice);
            await _context.SaveChangesAsync();

            return RedirectToPage("/AccountsPayable/Details", new { id = invoice.Id });
        }

        private async Task LoadFormDataAsync()
        {
            var companyId = _tenantContext.CompanyId;
            var vendorQuery = _context.Vendors.Where(v => v.IsActive).AsQueryable();
            if (companyId.HasValue)
                vendorQuery = vendorQuery.Where(v => _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0));

            Vendors = await vendorQuery.OrderBy(v => v.Name).ToListAsync();
        }
    }
}
