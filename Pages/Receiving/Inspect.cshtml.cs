using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Receiving
{
    [Authorize(Roles = "Admin,Accountant")]
    public class InspectModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;

        public InspectModel(AppDbContext context, IModuleGuardService moduleGuard,
            ITenantContext tenantContext, ILookupService lookupService)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
        }

        public GoodsReceipt Receipt { get; set; } = null!;
        public List<InspectLineViewModel> Lines { get; set; } = new();
        public List<SelectListItem> RejectionReasonOptions { get; set; } = new();
        public List<SelectListItem> DispositionOptions { get; set; } = new();

        public class InspectLineViewModel
        {
            public int LineId { get; set; }
            public int LineNumber { get; set; }
            public string? PartNumber { get; set; }
            public string? Description { get; set; }
            public string? UOM { get; set; }
            public decimal QuantityReceived { get; set; }
            public decimal QuantityAccepted { get; set; }
            public decimal QuantityRejected { get; set; }
            public string? RejectionReason { get; set; }
            public string? LotNumber { get; set; }
            public string? SerialNumber { get; set; }
            public string? Disposition { get; set; }
            public string? Notes { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Receiving" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var receipt = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Vendor)
                .Include(g => g.Lines).ThenInclude(l => l.PurchaseOrderLine).ThenInclude(pl => pl!.Item)
                .Where(g => g.Id == id && visibleIds.Contains(g.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || (g.PurchaseOrder != null && g.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value)))
                .FirstOrDefaultAsync();

            if (receipt == null)
                return NotFound();

            Receipt = receipt;

            Lines = receipt.Lines.OrderBy(l => l.LineNumber).Select(l => new InspectLineViewModel
            {
                LineId = l.Id,
                LineNumber = l.LineNumber,
                PartNumber = l.PurchaseOrderLine?.PartNumber ?? l.PurchaseOrderLine?.Item?.PartNumber,
                Description = l.PurchaseOrderLine?.Description,
                UOM = l.PurchaseOrderLine?.UOM,
                QuantityReceived = l.QuantityReceived,
                QuantityAccepted = l.QuantityAccepted,
                QuantityRejected = l.QuantityRejected,
                RejectionReason = l.RejectionReason,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                Notes = l.Notes
            }).ToList();

            await LoadDropdownsAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostInspectAsync(int id, List<InspectLineViewModel> lines)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Receiving" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var receipt = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Lines)
                .Where(g => g.Id == id && visibleIds.Contains(g.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || (g.PurchaseOrder != null && g.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value)))
                .FirstOrDefaultAsync();

            if (receipt == null)
                return NotFound();

            foreach (var lineVm in lines)
            {
                var grLine = receipt.Lines.FirstOrDefault(l => l.Id == lineVm.LineId);
                if (grLine == null) continue;

                grLine.QuantityAccepted = lineVm.QuantityAccepted;
                grLine.QuantityRejected = lineVm.QuantityRejected;
                grLine.RejectionReason = lineVm.RejectionReason;
                grLine.Notes = lineVm.Notes;

                _context.Entry(grLine).Property(l => l.QuantityAccepted).IsModified = true;
                _context.Entry(grLine).Property(l => l.QuantityRejected).IsModified = true;
                _context.Entry(grLine).Property(l => l.RejectionReason).IsModified = true;
                _context.Entry(grLine).Property(l => l.Notes).IsModified = true;
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Inspection saved for receipt {receipt.ReceiptNumber}.";
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while saving the inspection. Please try again.";
                return RedirectToPage("Inspect", new { id });
            }

            return RedirectToPage("Inspect", new { id });
        }

        public async Task<IActionResult> OnPostCompleteAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Receiving" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var receipt = await _context.GoodsReceipts
                .Include(g => g.PurchaseOrder)
                .Include(g => g.Lines)
                .Where(g => g.Id == id && visibleIds.Contains(g.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || (g.PurchaseOrder != null && g.PurchaseOrder.ShipToSiteId == _tenantContext.SiteId.Value)))
                .FirstOrDefaultAsync();

            if (receipt == null)
                return NotFound();

            bool hasRejections = receipt.Lines.Any(l => l.QuantityRejected > 0);

            if (hasRejections)
            {
                receipt.Status = ReceiptStatus.Rejected;
                var statusLv = await _lookupService.GetValueByCodeAsync(
                    _tenantContext.TenantId, _tenantContext.CompanyId, "ReceiptStatus", "5");
                if (statusLv != null) receipt.StatusLookupValueId = statusLv.Id;
            }
            else
            {
                receipt.Status = ReceiptStatus.Accepted;
                var statusLv = await _lookupService.GetValueByCodeAsync(
                    _tenantContext.TenantId, _tenantContext.CompanyId, "ReceiptStatus", "4");
                if (statusLv != null) receipt.StatusLookupValueId = statusLv.Id;
            }

            receipt.UpdatedAt = DateTime.UtcNow;
            _context.Entry(receipt).Property(r => r.Status).IsModified = true;
            _context.Entry(receipt).Property(r => r.StatusLookupValueId).IsModified = true;
            _context.Entry(receipt).Property(r => r.UpdatedAt).IsModified = true;

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Inspection complete for {receipt.ReceiptNumber}. Status: {receipt.Status}.";
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while completing the inspection.";
                return RedirectToPage("Inspect", new { id });
            }

            return RedirectToPage("Details", new { id });
        }

        private async Task LoadDropdownsAsync()
        {
            var rejectionReasons = await _lookupService.GetValuesAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId, "RejectionReason");
            RejectionReasonOptions = rejectionReasons
                .Select(v => new SelectListItem(v.Name, v.Code))
                .ToList();

            var dispositions = await _lookupService.GetValuesAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId, "Disposition");
            DispositionOptions = dispositions
                .Select(v => new SelectListItem(v.Name, v.Code))
                .ToList();
        }
    }
}
