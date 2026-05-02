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
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;
        private readonly InvoiceMatchingService _matchingService;

        public DetailsModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext, ILookupService lookupService, InvoiceMatchingService matchingService)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
            _matchingService = matchingService;
        }

        public VendorInvoice? Invoice { get; set; }
        public string SafeReturnUrl { get; private set; } = "/AccountsPayable";
        public List<SelectListItem> PaymentMethodOptions { get; set; } = new();
        public List<SelectListItem> InvoiceStatusOptions { get; set; } = new();
        public List<AvailablePOLineViewModel> AvailablePOLines { get; set; } = new();
        public List<AvailableGRLineViewModel> AvailableGRLines { get; set; } = new();

        public class AvailablePOLineViewModel
        {
            public int Id { get; set; }
            public string PONumber { get; set; } = "";
            public string Description { get; set; } = "";
            public string? PartNumber { get; set; }
            public decimal QuantityOrdered { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal LineTotal => QuantityOrdered * UnitPrice;
        }

        public class AvailableGRLineViewModel
        {
            public int Id { get; set; }
            public string ReceiptNumber { get; set; } = "";
            public int PurchaseOrderLineId { get; set; }
            public decimal QuantityReceived { get; set; }
        }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("accountspayable"))
                return RedirectToPage("/ModuleDisabled", new { module = "Accounts Payable" });

            SafeReturnUrl = !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) 
                ? ReturnUrl 
                : "/AccountsPayable";

            var companyId = _tenantContext.CompanyId;
            var query = _context.VendorInvoices
                .Include(i => i.Vendor)
                .Include(i => i.Lines)
                    .ThenInclude(l => l.GlAccount)
                .Include(i => i.Lines)
                    .ThenInclude(l => l.PurchaseOrderLine)
                        .ThenInclude(pol => pol!.PurchaseOrder)
                .Include(i => i.Lines)
                    .ThenInclude(l => l.GoodsReceiptLine)
                        .ThenInclude(grl => grl!.GoodsReceipt)
                .Include(i => i.Payments)
                .Include(i => i.Company)
                .Where(i => i.Id == id);

            if (companyId.HasValue)
                query = query.Where(i => _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                query = query.Where(i => !i.Lines.Any(l => l.PurchaseOrderLineId != null)
                    || i.Lines.Any(l => l.PurchaseOrderLine != null
                        && l.PurchaseOrderLine.PurchaseOrder != null
                        && l.PurchaseOrderLine.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value));

            Invoice = await query.FirstOrDefaultAsync();

            if (Invoice != null)
            {
                await LoadAvailablePOAndGRLinesAsync(Invoice);
            }

            PaymentMethodOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "PaymentMethod", null, "");
            InvoiceStatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", Invoice?.StatusLookupValueId, "");

            ViewData["ReturnUrl"] = ReturnUrl;
            ViewData["Breadcrumbs"] = new List<(string Label, string Href)>
            {
                ("Finance", "/AccountsPayable"),
                ("Accounts Payable", "/AccountsPayable"),
                ("Invoice", "")
            };
            ViewData["ShowBackLink"] = true;
            ViewData["BackLinkFallback"] = "/AccountsPayable";
            ViewData["BackLinkLabel"] = "Back to results";

            return Page();
        }

        private async Task LoadAvailablePOAndGRLinesAsync(VendorInvoice invoice)
        {
            var receivableStatuses = new[] { POStatus.Approved, POStatus.Sent, POStatus.PartiallyReceived, POStatus.Received };

            var vendorPOs = await _context.PurchaseOrders
                .Include(po => po.Lines)
                .Where(po => po.VendorId == invoice.VendorId
                    && (po.CompanyId == invoice.CompanyId || po.CompanyId == null)
                    && receivableStatuses.Contains(po.Status))
                .ToListAsync();

            AvailablePOLines = vendorPOs
                .SelectMany(po => po.Lines.Select(l => new AvailablePOLineViewModel
                {
                    Id = l.Id,
                    PONumber = po.PONumber,
                    Description = l.Description,
                    PartNumber = l.PartNumber,
                    QuantityOrdered = l.QuantityOrdered,
                    UnitPrice = l.UnitPrice
                }))
                .OrderBy(l => l.PONumber)
                .ThenBy(l => l.Description)
                .ToList();

            var poIds = vendorPOs.Select(po => po.Id).ToList();

            AvailableGRLines = await _context.GoodsReceiptLines
                .Include(grl => grl.GoodsReceipt)
                .Where(grl => poIds.Contains(grl.GoodsReceipt!.PurchaseOrderId))
                .Select(grl => new AvailableGRLineViewModel
                {
                    Id = grl.Id,
                    ReceiptNumber = grl.GoodsReceipt!.ReceiptNumber,
                    PurchaseOrderLineId = grl.PurchaseOrderLineId,
                    QuantityReceived = grl.QuantityReceived
                })
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id, string? returnUrl)
        {
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            invoice.Status = InvoiceStatus.Approved;
            var approvedLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", ((int)InvoiceStatus.Approved).ToString());
            if (approvedLv != null)
                invoice.StatusLookupValueId = approvedLv.Id;
            invoice.ApprovedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
            return RedirectToPage(new { id, returnUrl = safeReturnUrl });
        }

        public async Task<IActionResult> OnPostRecordPaymentAsync(int id, string? returnUrl, decimal amount, string? paymentMethod, string? referenceNumber, string? notes)
        {
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            var payment = new InvoicePayment
            {
                VendorInvoiceId = id,
                PaymentDate = DateTime.Today,
                Amount = amount,
                PaymentMethod = paymentMethod,
                ReferenceNumber = referenceNumber,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.InvoicePayments.Add(payment);

            invoice.AmountPaid += amount;
            invoice.BalanceDue = invoice.Total - invoice.AmountPaid;
            invoice.UpdatedAt = DateTime.UtcNow;

            if (invoice.BalanceDue <= 0)
            {
                invoice.Status = InvoiceStatus.Paid;
                var paidLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", ((int)InvoiceStatus.Paid).ToString());
                if (paidLv != null)
                    invoice.StatusLookupValueId = paidLv.Id;
            }
            else if (invoice.AmountPaid > 0)
            {
                invoice.Status = InvoiceStatus.PartiallyPaid;
                var partialLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", ((int)InvoiceStatus.PartiallyPaid).ToString());
                if (partialLv != null)
                    invoice.StatusLookupValueId = partialLv.Id;
            }

            await _context.SaveChangesAsync();
            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
            return RedirectToPage(new { id, returnUrl = safeReturnUrl });
        }

        public async Task<IActionResult> OnPostVoidAsync(int id, string? returnUrl)
        {
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            invoice.Status = InvoiceStatus.Voided;
            var voidedLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", ((int)InvoiceStatus.Voided).ToString());
            if (voidedLv != null)
                invoice.StatusLookupValueId = voidedLv.Id;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
            return RedirectToPage(new { id, returnUrl = safeReturnUrl });
        }

        private async Task<VendorInvoice?> LoadInvoiceScopedAsync(int id)
        {
            var query = _context.VendorInvoices.Where(i => i.Id == id);
            if (_tenantContext.CompanyId.HasValue)
                query = query.Where(i => _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                query = query.Where(i => !i.Lines.Any(l => l.PurchaseOrderLineId != null)
                    || i.Lines.Any(l => l.PurchaseOrderLine != null
                        && l.PurchaseOrderLine.PurchaseOrder != null
                        && l.PurchaseOrderLine.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value));
            return await query.FirstOrDefaultAsync();
        }

        public async Task<IActionResult> OnPostLinkLineAsync(int id, int invoiceLineId, int poLineId, int? grLineId)
        {
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            var line = await _context.VendorInvoiceLines
                .FirstOrDefaultAsync(l => l.Id == invoiceLineId && l.VendorInvoiceId == id);
            if (line == null) return NotFound();

            var (success, error) = await _matchingService.LinkLineAsync(invoiceLineId, poLineId, grLineId, invoice.VendorId, invoice.CompanyId);
            if (!success)
            {
                TempData["Error"] = error ?? "Failed to link line.";
                return RedirectToPage(new { id, returnUrl = ReturnUrl });
            }

            await _matchingService.UpdateInvoiceMatchStatusAsync(id);

            TempData["Success"] = "Line linked successfully.";
            return RedirectToPage(new { id, returnUrl = ReturnUrl });
        }

        public async Task<IActionResult> OnPostUnlinkLineAsync(int id, int invoiceLineId)
        {
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            var line = await _context.VendorInvoiceLines
                .FirstOrDefaultAsync(l => l.Id == invoiceLineId && l.VendorInvoiceId == id);
            if (line == null) return NotFound();

            await _matchingService.UnlinkLineAsync(invoiceLineId);
            await _matchingService.UpdateInvoiceMatchStatusAsync(id);

            TempData["Success"] = "Line unlinked.";
            return RedirectToPage(new { id, returnUrl = ReturnUrl });
        }

        public async Task<IActionResult> OnPostAutoMatchAsync(int id)
        {
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            int linked = await _matchingService.AutoLinkToPOAsync(id);
            await _matchingService.UpdateInvoiceMatchStatusAsync(id);

            if (linked > 0)
                TempData["Success"] = $"Auto-match linked {linked} line(s). Match status updated.";
            else
                TempData["Error"] = "No lines could be auto-matched. Try manual linking.";

            return RedirectToPage(new { id, returnUrl = ReturnUrl });
        }
    }
}
