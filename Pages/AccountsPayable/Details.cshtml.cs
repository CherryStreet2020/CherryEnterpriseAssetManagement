using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.AccountsPayable;
using Abs.FixedAssets.Services.Cip;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.AccountsPayable
{
    // PR #105 / B-18: AP details page hosts Approve / Record Payment / Void —
    // every handler commits money. Tighten the role gate.
    [Authorize(Roles = "Admin,Accountant")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;
        private readonly InvoiceMatchingService _matchingService;
        private readonly CipAutoCostPostingService _cipAutoCostPosting;
        private readonly IApPostingService _apPosting;
        private readonly ILogger<DetailsModel> _logger;

        public DetailsModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext,
            ILookupService lookupService, InvoiceMatchingService matchingService,
            CipAutoCostPostingService cipAutoCostPosting, IApPostingService apPosting, ILogger<DetailsModel> logger)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
            _matchingService = matchingService;
            _cipAutoCostPosting = cipAutoCostPosting;
            _apPosting = apPosting;
            _logger = logger;
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

            // Tenant scoping is mandatory regardless of whether the user has
            // an explicit CompanyId set on their tenant context. VisibleCompanyIds
            // is the source of truth for which companies the caller can see;
            // an empty list correctly returns no results. The previous
            // `if (companyId.HasValue)` guard skipped scoping for users with
            // no explicit company assignment, leaking invoices across tenants.
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
                .Where(i => i.Id == id)
                .Where(i => _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));

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

        public async Task<IActionResult> OnPostApproveAsync(int id, string? returnUrl, bool overrideMatch = false, string? overrideReason = null)
        {
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            // PR #105 / B-19: overrideMatch=true bypasses the 3-way match gate
            // and is a sensitive privilege. Two guards layered on top of the
            // delegate call:
            //   1) Role gate — only Admin can force-approve with override.
            //      Accountant gets a 403 here even though they can approve
            //      cleanly-matched invoices via the normal path.
            //   2) Reason required — the override leaves an audit trail with
            //      a free-text explanation, which lands in AuditLogs alongside
            //      the user identity for SOX-style internal control review.
            // The non-override happy path is unchanged.
            var fallbackReturn = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
            if (overrideMatch)
            {
                if (!User.IsInRole("Admin"))
                {
                    _logger.LogWarning("AP override-approval denied for invoice {Id} — user {User} lacks Admin role", id, User.Identity?.Name);
                    TempData["Error"] = "Override approval requires the Admin role.";
                    return RedirectToPage(new { id, returnUrl = fallbackReturn });
                }
                if (string.IsNullOrWhiteSpace(overrideReason))
                {
                    TempData["Error"] = "A reason is required when approving with match override.";
                    return RedirectToPage(new { id, returnUrl = fallbackReturn });
                }
            }

            // S1-5: delegate to ApPostingService — runs the 3-way match gate,
            // posts the approval JE (Dr GR-Accrued/Expense + PPV / Cr AP),
            // period-guards the date, flips status. Errors surface to TempData.
            try
            {
                await _apPosting.PostApprovalAsync(id, overrideMatch, User.Identity?.Name ?? "");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AP posting failed for invoice {Id}", id);
                TempData["Error"] = ex.Message;
                var fallbackUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
                return RedirectToPage(new { id, returnUrl = fallbackUrl });
            }

            // Reload to pick up the status flip + JE link.
            invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            // PR #105 / B-19: if override took the place of a clean match, leave
            // the audit trail. AuditLog rows are read by the SOX testing skill +
            // by any downstream forensic review of approval decisions. The Match
            // status before override is captured in BeforeJson; the reason in
            // Description for fast scanning.
            if (overrideMatch)
            {
                _context.AuditLogs.Add(new AuditLog
                {
                    EntityType = "VendorInvoice",
                    EntityId = invoice.Id,
                    Action = "ApproveWithMatchOverride",
                    BeforeJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        invoiceNumber = invoice.InvoiceNumber,
                        matchStatusAtOverride = invoice.MatchStatus.ToString(),
                        totalAmount = invoice.Total,
                        vendorId = invoice.VendorId
                    }),
                    Username = User.Identity?.Name,
                    Description = $"Override reason: {overrideReason}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Timestamp = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                _logger.LogWarning("AP override approval committed for invoice {InvoiceNumber} by {User}: {Reason}",
                    invoice.InvoiceNumber, User.Identity?.Name, overrideReason);
            }

            // Mirror status to the LookupValue FK (legacy paired-enum convention).
            var approvedLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", ((int)InvoiceStatus.Approved).ToString());
            if (approvedLv != null && invoice.StatusLookupValueId != approvedLv.Id)
            {
                invoice.StatusLookupValueId = approvedLv.Id;
                await _context.SaveChangesAsync();
            }

            // S1-3: route any CIP-tagged invoice lines into CipAutoCostPostingService.
            // The service is idempotent — re-runs against an already-posted line return
            // the existing CipCost. Only invoice lines with an explicit CipProjectId on
            // the line itself are routed; PO-derived CIP linkage rides through the
            // receipt-line path instead (avoids double-posting).
            // Note: full AP→GL posting (S1-5) is a separate PR per ADR-002. This
            // wiring restores the CIP cost-accumulation chain only.
            int cipRouted = 0;
            foreach (var invoiceLine in invoice.Lines.Where(l => l.CipProjectId != null))
            {
                try
                {
                    var cipCost = await _cipAutoCostPosting.PostFromVendorInvoiceLineAsync(invoiceLine.Id);
                    if (cipCost != null) cipRouted++;
                }
                catch (Exception cipEx)
                {
                    _logger.LogError(cipEx,
                        "CIP auto-cost posting failed for invoice line {LineId} on invoice {InvoiceNumber}",
                        invoiceLine.Id, invoice.InvoiceNumber);
                }
            }
            if (cipRouted > 0)
                TempData["Success"] = $"Invoice {invoice.InvoiceNumber} approved. {cipRouted} line(s) routed to CIP.";

            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
            return RedirectToPage(new { id, returnUrl = safeReturnUrl });
        }

        public async Task<IActionResult> OnPostRecordPaymentAsync(int id, string? returnUrl, decimal amount, string? paymentMethod, string? referenceNumber, string? notes)
        {
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice == null) return NotFound();

            // S1-5: payment posting via ApPostingService — period-guards,
            // posts the Dr AP / Cr Cash JE, flips status to Paid when fully
            // paid (or PartiallyPaid otherwise via the resync below).
            try
            {
                await _apPosting.PostPaymentAsync(id, amount, DateTime.Today, referenceNumber);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AP payment failed for invoice {Id}", id);
                TempData["Error"] = ex.Message;
                var fbUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
                return RedirectToPage(new { id, returnUrl = fbUrl });
            }

            // Persist the InvoicePayment audit record alongside the JE
            // (the service updates AmountPaid + Status; this row tracks
            // operational metadata like method/reference/notes).
            _context.InvoicePayments.Add(new InvoicePayment
            {
                VendorInvoiceId = id,
                PaymentDate = DateTime.Today,
                Amount = amount,
                PaymentMethod = paymentMethod,
                ReferenceNumber = referenceNumber,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            });

            invoice = await LoadInvoiceScopedAsync(id);
            if (invoice != null)
            {
                invoice.BalanceDue = invoice.Total - invoice.AmountPaid;
                if (invoice.BalanceDue > 0 && invoice.AmountPaid > 0 && invoice.Status != InvoiceStatus.PartiallyPaid)
                {
                    invoice.Status = InvoiceStatus.PartiallyPaid;
                    var partialLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", ((int)InvoiceStatus.PartiallyPaid).ToString());
                    if (partialLv != null) invoice.StatusLookupValueId = partialLv.Id;
                }
                else if (invoice.Status == InvoiceStatus.Paid)
                {
                    var paidLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", ((int)InvoiceStatus.Paid).ToString());
                    if (paidLv != null) invoice.StatusLookupValueId = paidLv.Id;
                }
            }

            await _context.SaveChangesAsync();
            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
            return RedirectToPage(new { id, returnUrl = safeReturnUrl });
        }

        public async Task<IActionResult> OnPostVoidAsync(int id, string? returnUrl, string? reason)
        {
            // S1-5: void via ApPostingService — period-guards, posts a contra
            // JE that reverses the approval JE (when one exists), flips status.
            try
            {
                await _apPosting.PostVoidAsync(id, reason ?? "voided");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "AP void failed for invoice {Id}", id);
                TempData["Error"] = ex.Message;
                var fb = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
                return RedirectToPage(new { id, returnUrl = fb });
            }

            // Mirror the status to the LookupValue FK.
            var invoice = await LoadInvoiceScopedAsync(id);
            if (invoice != null)
            {
                var voidedLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InvoiceStatus", ((int)InvoiceStatus.Voided).ToString());
                if (voidedLv != null && invoice.StatusLookupValueId != voidedLv.Id)
                {
                    invoice.StatusLookupValueId = voidedLv.Id;
                    await _context.SaveChangesAsync();
                }
            }

            var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/AccountsPayable";
            return RedirectToPage(new { id, returnUrl = safeReturnUrl });
        }

        private async Task<VendorInvoice?> LoadInvoiceScopedAsync(int id)
        {
            // Mandatory tenant scope. See OnGetAsync for the same fix's rationale.
            var query = _context.VendorInvoices
                .Where(i => i.Id == id)
                .Where(i => _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));
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
