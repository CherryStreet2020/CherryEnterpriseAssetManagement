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

        // DEF-N08/DEF-N03 (PR #91): Open POs available to attach this invoice
        // to. Carrying VendorId + Lines so the picker can filter client-side by
        // vendor and the OnPostAsync handler can build invoice lines that
        // explicitly reference PurchaseOrderLineId — the foundation of the
        // three-way match (PO/GR/Invoice) reconciliation downstream.
        public List<PurchaseOrder> AvailablePos { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("accountspayable"))
                return RedirectToPage("/ModuleDisabled", new { module = "Accounts Payable" });

            await LoadFormDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int vendorId, string invoiceNumber, DateTime invoiceDate, decimal amount, DateTime? dueDate, string? description, int? purchaseOrderId)
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

            var lineDescription = string.IsNullOrWhiteSpace(description)
                ? $"Invoice {invoiceNumber}"
                : description!;

            // DEF-N08/DEF-N03 (PR #91): If the user picked a PO, attach the
            // invoice to it explicitly — load the PO and its lines, validate the
            // vendor matches (preventing cross-vendor PO linkage), and seed the
            // invoice with one VendorInvoiceLine per PO line (carrying its
            // PurchaseOrderLineId). That gives ApPostingService a real PO-line
            // resolution so it can post each line against the correct GL
            // account (the receiving-side accrual hits Inventory/DirectExpense/
            // CIP depending on the line, instead of falling back to the catch-
            // all DirectExpense bucket the header-only path used). It also
            // unlocks the three-way match (PO/GR/Invoice) workflow downstream.
            PurchaseOrder? matchedPo = null;
            List<VendorInvoiceLine> invoiceLines;
            decimal headerTotal;

            if (purchaseOrderId.HasValue && purchaseOrderId.Value > 0)
            {
                matchedPo = await _context.PurchaseOrders
                    .Include(p => p.Lines)
                    .Where(p => p.Id == purchaseOrderId.Value
                        && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                    .FirstOrDefaultAsync();

                if (matchedPo == null)
                {
                    TempData["Error"] = "Selected PO is not accessible.";
                    await LoadFormDataAsync();
                    return Page();
                }

                if (matchedPo.VendorId != vendorId)
                {
                    TempData["Error"] = $"PO {matchedPo.PONumber} belongs to a different vendor than the selected one.";
                    await LoadFormDataAsync();
                    return Page();
                }

                // Seed one VendorInvoiceLine per PO line. We preserve the PO's
                // Quantity/UnitPrice/LineTotal so the JE posts at the PO-line
                // basis (matching the GR side); per-line variances vs. the
                // actual amount billed are surfaced through the existing
                // MatchStatus / variance workflow.
                int lineNo = 1;
                invoiceLines = new List<VendorInvoiceLine>();
                foreach (var pol in matchedPo.Lines)
                {
                    invoiceLines.Add(new VendorInvoiceLine
                    {
                        LineNumber = lineNo++,
                        Description = pol.Description,
                        Quantity = pol.QuantityOrdered,
                        UnitPrice = pol.UnitPrice,
                        LineTotal = pol.LineTotal,
                        PurchaseOrderLineId = pol.Id
                    });
                }
                // If the PO had no lines (edge case — a draft PO that shouldn't
                // be billable but somehow made it through approval), fall back
                // to the single header-only line at the user-entered amount to
                // keep the balance guard (PR #84) happy.
                if (invoiceLines.Count == 0)
                {
                    invoiceLines.Add(new VendorInvoiceLine
                    {
                        LineNumber = 1,
                        Description = lineDescription,
                        Quantity = 1,
                        UnitPrice = amount,
                        LineTotal = amount
                    });
                    headerTotal = amount;
                }
                else
                {
                    // Header total reflects the actual sum of seeded line
                    // totals — that's the PO total. Variance vs. user-entered
                    // `amount` is logged in Notes for the AP team to see.
                    headerTotal = invoiceLines.Sum(l => l.LineTotal);
                }
            }
            else
            {
                // DEF-N01: header-only invoices used to land with no Lines, so
                // the approval JE built from invoice.Lines came out as a single
                // $0/$0 line crediting AP — bypassing the accrual entirely.
                // Seed one generic direct-expense line for the full amount;
                // ApPostingService resolves the missing PurchaseOrderLineId to
                // GlAccountKind.DirectExpense (GL 6000) and posts a balanced
                // 2-line JE (DR 6000 / CR AP). Auto-Match-to-PO continues to
                // work after-the-fact by overwriting PurchaseOrderLineId on
                // this same line.
                invoiceLines = new List<VendorInvoiceLine>
                {
                    new VendorInvoiceLine
                    {
                        LineNumber = 1,
                        Description = lineDescription,
                        Quantity = 1,
                        UnitPrice = amount,
                        LineTotal = amount
                    }
                };
                headerTotal = amount;
            }

            // If user-entered amount diverges from the PO-derived total,
            // capture the variance in Notes so the AP reviewer can see it.
            // We don't refuse the post — the variance might be intentional
            // (partial-quantity invoice, freight, tax) and the existing
            // MatchStatus workflow already surfaces it.
            string? annotatedNotes = description;
            if (matchedPo != null && amount > 0 && amount != headerTotal)
            {
                var varianceNote = $"[PO {matchedPo.PONumber}] entered amount {amount:C} differs from PO total {headerTotal:C} (variance {(amount - headerTotal):C}).";
                annotatedNotes = string.IsNullOrWhiteSpace(description)
                    ? varianceNote
                    : $"{description}\n\n{varianceNote}";
            }

            var invoice = new VendorInvoice
            {
                InvoiceNumber = invoiceNumber,
                VendorId = vendorId,
                CompanyId = companyId.Value,
                InvoiceDate = invoiceDate,
                ReceivedDate = DateTime.Today,
                DueDate = calculatedDueDate,
                PaymentTerms = vendor.PaymentTerms,
                Subtotal = headerTotal,
                Total = headerTotal,
                BalanceDue = headerTotal,
                Status = InvoiceStatus.PendingApproval,
                MatchStatus = matchedPo != null ? InvoiceMatchStatus.PartialMatch : InvoiceMatchStatus.NotMatched,
                Notes = annotatedNotes,
                CreatedAt = DateTime.UtcNow,
                Lines = invoiceLines
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

            // DEF-N08/DEF-N03 (PR #91): Load POs that are billable — anything
            // in Approved/Sent/PartiallyReceived/Received. Excludes Draft and
            // PendingApproval (not yet a real commitment) plus Invoiced/
            // Closed/Cancelled (no further AP work expected). We include the
            // vendor so the picker label can show vendor + PO + total at
            // once; the client-side JS filters the picker by the currently
            // selected vendor so the AP team isn't scrolling through every
            // vendor's POs.
            var billableStatuses = new[] {
                POStatus.Approved, POStatus.Sent,
                POStatus.PartiallyReceived, POStatus.Received
            };
            AvailablePos = await _context.PurchaseOrders
                .Include(p => p.Vendor)
                .Where(p => billableStatuses.Contains(p.Status)
                    && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .OrderByDescending(p => p.OrderDate)
                .ThenBy(p => p.PONumber)
                .Take(200)
                .ToListAsync();
        }
    }
}
