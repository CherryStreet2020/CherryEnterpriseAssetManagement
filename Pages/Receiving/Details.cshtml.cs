using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Receiving
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public DetailsModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public GoodsReceipt Receipt { get; set; } = null!;
        public int TotalLinesCount { get; set; }
        public decimal TotalQtyReceived { get; set; }
        public decimal TotalQtyAccepted { get; set; }
        public decimal TotalQtyRejected { get; set; }
        public decimal TotalValue { get; set; }
        public List<VendorInvoice> LinkedInvoices { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Receiving" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var receipt = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Vendor)
                .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Lines)
                .Include(g => g.Lines).ThenInclude(l => l.PurchaseOrderLine).ThenInclude(pl => pl!.Item)
                .Include(g => g.Lines).ThenInclude(l => l.ReceivingLocation)
                .Where(g => g.Id == id && visibleIds.Contains(g.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || (g.PurchaseOrder != null && g.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value)))
                .FirstOrDefaultAsync();

            if (receipt == null)
                return NotFound();

            Receipt = receipt;
            TotalLinesCount = receipt.Lines.Count;
            TotalQtyReceived = receipt.Lines.Sum(l => l.QuantityReceived);
            TotalQtyAccepted = receipt.Lines.Sum(l => l.QuantityAccepted);
            TotalQtyRejected = receipt.Lines.Sum(l => l.QuantityRejected);
            TotalValue = receipt.Lines.Sum(l =>
                l.QuantityReceived * (l.PurchaseOrderLine?.UnitPrice ?? 0));

            var grLineIds = receipt.Lines.Select(l => l.Id).ToList();
            LinkedInvoices = await _context.VendorInvoices
                .Where(vi => vi.Lines.Any(vil => vil.GoodsReceiptLineId.HasValue && grLineIds.Contains(vil.GoodsReceiptLineId.Value)))
                .ToListAsync();

            return Page();
        }
    }
}
