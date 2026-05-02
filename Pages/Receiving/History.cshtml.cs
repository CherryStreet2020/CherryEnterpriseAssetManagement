using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Receiving
{
    public class HistoryModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public HistoryModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public List<GoodsReceipt> Receipts { get; set; } = new();
        public int TotalCount { get; set; }
        public int ThisMonthCount { get; set; }
        public int ThisWeekCount { get; set; }
        public decimal TotalReceivedValue { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DateFrom { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? DateTo { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? VendorSearch { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? POSearch { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReceiptSearch { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Receiving" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var query = _context.GoodsReceipts
                .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Vendor)
                .Include(g => g.Lines).ThenInclude(l => l.PurchaseOrderLine)
                .Where(g => visibleIds.Contains(g.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                query = query.Where(g => g.PurchaseOrder != null && g.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value);

            if (DateFrom.HasValue)
                query = query.Where(g => g.ReceiptDate >= DateFrom.Value);

            if (DateTo.HasValue)
                query = query.Where(g => g.ReceiptDate <= DateTo.Value);

            if (!string.IsNullOrWhiteSpace(VendorSearch))
            {
                var vs = VendorSearch.Trim().ToLower();
                query = query.Where(g => g.PurchaseOrder != null && g.PurchaseOrder.Vendor != null &&
                    g.PurchaseOrder.Vendor.Name.ToLower().Contains(vs));
            }

            if (StatusFilter.HasValue)
                query = query.Where(g => (int)g.Status == StatusFilter.Value);

            if (!string.IsNullOrWhiteSpace(POSearch))
            {
                var ps = POSearch.Trim().ToLower();
                query = query.Where(g => g.PurchaseOrder != null && g.PurchaseOrder.PONumber.ToLower().Contains(ps));
            }

            if (!string.IsNullOrWhiteSpace(ReceiptSearch))
            {
                var rs = ReceiptSearch.Trim().ToLower();
                query = query.Where(g => g.ReceiptNumber.ToLower().Contains(rs));
            }

            Receipts = await query
                .OrderByDescending(g => g.ReceiptDate)
                .ThenByDescending(g => g.CreatedAt)
                .ToListAsync();

            TotalCount = Receipts.Count;

            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            ThisMonthCount = Receipts.Count(r => r.ReceiptDate >= monthStart);

            var weekAgo = DateTime.Today.AddDays(-7);
            ThisWeekCount = Receipts.Count(r => r.ReceiptDate >= weekAgo);

            TotalReceivedValue = Receipts.Sum(r =>
                r.Lines.Sum(l => l.QuantityReceived * (l.PurchaseOrderLine?.UnitPrice ?? 0)));

            return Page();
        }
    }
}
