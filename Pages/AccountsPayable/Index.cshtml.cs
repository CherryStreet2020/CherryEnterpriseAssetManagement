using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.AccountsPayable
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public IndexModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public List<VendorInvoice> Invoices { get; set; } = new();
        public int OpenInvoiceCount { get; set; }
        public int PastDueCount { get; set; }
        public decimal TotalPayable { get; set; }
        public decimal PaidThisMonth { get; set; }
        public bool CanCreateInvoice { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("accountspayable"))
                return RedirectToPage("/ModuleDisabled", new { module = "Accounts Payable" });

            var companyId = _tenantContext.CompanyId;
            CanCreateInvoice = companyId.HasValue;

            var baseQuery = _context.VendorInvoices.AsQueryable();
            if (companyId.HasValue)
                baseQuery = baseQuery.Where(i => _tenantContext.VisibleCompanyIds.Contains(i.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                baseQuery = baseQuery.Where(i => !i.Lines.Any(l => l.PurchaseOrderLineId != null)
                    || i.Lines.Any(l => l.PurchaseOrderLine != null
                        && l.PurchaseOrderLine.PurchaseOrder != null
                        && l.PurchaseOrderLine.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value));

            Invoices = await baseQuery
                .Include(i => i.Vendor)
                .OrderByDescending(i => i.InvoiceDate)
                .Take(50)
                .ToListAsync();

            var openStatuses = new[] { InvoiceStatus.Draft, InvoiceStatus.PendingApproval, InvoiceStatus.Approved };
            
            OpenInvoiceCount = await baseQuery
                .Where(i => openStatuses.Contains(i.Status) && i.BalanceDue > 0)
                .CountAsync();
            
            TotalPayable = await baseQuery
                .Where(i => openStatuses.Contains(i.Status) && i.BalanceDue > 0)
                .SumAsync(i => (decimal?)i.BalanceDue) ?? 0m;
            
            PastDueCount = await baseQuery
                .Where(i => i.DueDate < DateTime.UtcNow && i.BalanceDue > 0)
                .CountAsync();
            
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            PaidThisMonth = await baseQuery
                .Where(i => i.Status == InvoiceStatus.Paid && i.UpdatedAt >= monthStart)
                .SumAsync(i => (decimal?)i.Total) ?? 0m;
            
            return Page();
        }
    }
}
