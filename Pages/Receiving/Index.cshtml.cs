using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Receiving
{
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

        public List<PurchaseOrder> PendingPOs { get; set; } = new();
        public int PendingReceiptCount { get; set; }
        public int ReceivedTodayCount { get; set; }
        public int ReceivedThisWeekCount { get; set; }
        public int OverdueCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Receiving" });

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var receivableStatuses = new[] { POStatus.Approved, POStatus.Sent, POStatus.PartiallyReceived };
            
            var poQuery = _context.PurchaseOrders
                .Include(p => p.Vendor)
                .Include(p => p.Lines)
                    .ThenInclude(l => l.Item)
                .Include(p => p.ShipToSite)
                .Where(p => visibleIds.Contains(p.CompanyId ?? 0) && receivableStatuses.Contains(p.Status));

            if (_tenantContext.SiteId.HasValue)
                poQuery = poQuery.Where(p => p.ShipToSiteId == _tenantContext.SiteId.Value);

            PendingPOs = await poQuery
                .OrderBy(p => p.RequiredDate ?? p.OrderDate)
                .ToListAsync();

            PendingReceiptCount = PendingPOs.Count;

            var today = DateTime.Today;
            var receiptBaseQuery = _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Where(g => visibleIds.Contains(g.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                receiptBaseQuery = receiptBaseQuery.Where(g => g.PurchaseOrder != null && g.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value);

            ReceivedTodayCount = await receiptBaseQuery.CountAsync(g => g.ReceiptDate == today);

            var weekAgo = DateTime.Today.AddDays(-7);
            ReceivedThisWeekCount = await receiptBaseQuery.CountAsync(g => g.ReceiptDate >= weekAgo);

            OverdueCount = PendingPOs.Count(p => p.RequiredDate.HasValue && p.RequiredDate.Value < DateTime.Today);

            return Page();
        }
    }
}
