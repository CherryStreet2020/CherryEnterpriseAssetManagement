using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Receiving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Receiving
{
    [Authorize(Roles = "Admin,Accountant")]
    public class InspectModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;
        private readonly IReceivingPostingService _receivingPosting;
        private readonly ILogger<InspectModel> _logger;

        public InspectModel(AppDbContext context, IModuleGuardService moduleGuard,
            ITenantContext tenantContext, ILookupService lookupService,
            IReceivingPostingService receivingPosting, ILogger<InspectModel> logger)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
            _receivingPosting = receivingPosting;
            _logger = logger;
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

            // PR #105 / B-17: when rejections were recorded, post the reversing
            // inventory + JE moves. Idempotent via the GR-REV reference so a
            // refresh-and-re-submit doesn't double-book. Surfaces a follow-up
            // message but does not block completion if reversal fails — the
            // operational status flip is what gates the rest of the P2P path.
            if (hasRejections)
            {
                try
                {
                    var revResult = await _receivingPosting.PostRejectionReversalAsync(id);
                    if (revResult.JournalEntryId.HasValue && revResult.TotalAccrued > 0m)
                    {
                        TempData["Success"] += $" Reversal JE #{revResult.JournalEntryId} posted for ${revResult.TotalAccrued:N2} ({revResult.InventoryRowsTouched} inventory row(s) adjusted).";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Rejection reversal failed for receipt {Id}", id);
                    TempData["Error"] = $"Inspection saved, but rejection reversal failed: {ex.Message}. Please contact Accounting.";
                }
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
