using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Receiving
{
    [Authorize(Roles = "Admin")]
    public class ReceiveModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;
        private readonly IPeriodGuard _periodGuard;
        private readonly ILogger<ReceiveModel> _logger;

        public ReceiveModel(AppDbContext context, IModuleGuardService moduleGuard,
            ITenantContext tenantContext, ILookupService lookupService, IPeriodGuard periodGuard,
            ILogger<ReceiveModel> logger)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
            _periodGuard = periodGuard;
            _logger = logger;
        }

        public PurchaseOrder PO { get; set; } = null!;
        public List<ReceiveLineViewModel> Lines { get; set; } = new();
        public List<Location> Locations { get; set; } = new();

        public class ReceiveLineViewModel
        {
            public int POLineId { get; set; }
            public string? ItemDescription { get; set; }
            public string? PartNumber { get; set; }
            public string? UOM { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal QuantityOrdered { get; set; }
            public decimal QuantityPreviouslyReceived { get; set; }
            public decimal QuantityRemaining { get; set; }
            public decimal QuantityToReceive { get; set; }
            public string? Notes { get; set; }
            public decimal QuantityRejected { get; set; }
            public string? RejectionReason { get; set; }
            public string? LotNumber { get; set; }
            public string? SerialNumber { get; set; }
            public string? StorageLocation { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "purchasing" });

            // Tenant scoping is mandatory. The previous version skipped the
            // company filter when CompanyId was null on the tenant context —
            // same shape as the AccountsPayable leak fixed in PR #22. A user
            // with CompanyId=null but VisibleCompanyIds=[200] could load a
            // PO from company 100. Site scoping stays conditional because
            // it's a sub-scope: a user without a site sees all sites within
            // their visible companies.
            var poQuery = _context.PurchaseOrders
                .Include(p => p.Vendor)
                .Include(p => p.Lines).ThenInclude(l => l.Item)
                .Where(p => p.Id == id
                    && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                poQuery = poQuery.Where(p => p.ShipToSiteId == _tenantContext.SiteId.Value);

            var po = await poQuery.FirstOrDefaultAsync();

            if (po == null)
                return NotFound();

            var receivableStatuses = new[] { POStatus.Approved, POStatus.Sent, POStatus.PartiallyReceived };
            if (!receivableStatuses.Contains(po.Status))
            {
                TempData["Error"] = $"PO {po.PONumber} is not in a receivable status (current: {po.Status}).";
                return RedirectToPage("Index");
            }

            PO = po;
            Lines = po.Lines.OrderBy(l => l.LineNumber).Select(l => new ReceiveLineViewModel
            {
                POLineId = l.Id,
                ItemDescription = l.Description,
                PartNumber = l.PartNumber ?? l.Item?.PartNumber,
                UOM = l.UOM,
                UnitPrice = l.UnitPrice,
                QuantityOrdered = l.QuantityOrdered,
                QuantityPreviouslyReceived = l.QuantityReceived,
                QuantityRemaining = l.QuantityOrdered - l.QuantityReceived
            }).ToList();

            Locations = await _context.Locations
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostReceiveAsync(
            int id,
            List<ReceiveLineViewModel> lines,
            DateTime? receiptDate,
            string? shippingCarrier,
            string? trackingNumber,
            string? packingSlipNumber,
            int? receivingLocationId,
            string? headerNotes)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "purchasing" });

            // Tenant scoping is mandatory — see OnGetAsync for the rationale.
            var poQuery = _context.PurchaseOrders
                .Include(p => p.Vendor)
                .Include(p => p.Lines).ThenInclude(l => l.Item)
                .Where(p => p.Id == id
                    && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                poQuery = poQuery.Where(p => p.ShipToSiteId == _tenantContext.SiteId.Value);

            var po = await poQuery.FirstOrDefaultAsync();

            if (po == null)
                return NotFound();

            var receivableStatuses = new[] { POStatus.Approved, POStatus.Sent, POStatus.PartiallyReceived };
            if (!receivableStatuses.Contains(po.Status))
            {
                TempData["Error"] = $"PO {po.PONumber} is not in a receivable status.";
                return RedirectToPage("Index");
            }

            var errors = new List<string>();
            foreach (var line in lines)
            {
                if (line.QuantityToReceive < 0)
                    errors.Add($"Quantity cannot be negative for PO line {line.POLineId}.");
            }

            var receivingLines = lines.Where(l => l.QuantityToReceive > 0).ToList();
            if (!receivingLines.Any() && !errors.Any())
            {
                TempData["Error"] = "Please enter a quantity to receive on at least one line.";
                return RedirectToPage("Receive", new { id });
            }

            foreach (var line in receivingLines)
            {
                var poLine = po.Lines.FirstOrDefault(l => l.Id == line.POLineId);
                if (poLine == null)
                {
                    errors.Add($"PO line {line.POLineId} not found.");
                    continue;
                }
                var remaining = poLine.QuantityOrdered - poLine.QuantityReceived;
                if (line.QuantityToReceive > remaining)
                    errors.Add($"Line '{poLine.Description}': cannot receive {line.QuantityToReceive} — only {remaining} remaining.");
            }

            if (errors.Any())
            {
                TempData["Error"] = string.Join(" ", errors);
                return RedirectToPage("Receive", new { id });
            }

            // Period locking: a goods receipt creates AP / inventory exposure
            // and (downstream) GL postings. Block a receipt that would post
            // into a closed/locked accounting period — same posture as
            // Pages/Assets/Improve.cshtml.cs::OnPostAsync and Dispose.
            var postingDate = receiptDate ?? DateTime.Today;
            var receiptCompanyId = po.CompanyId ?? _tenantContext.CompanyId ?? 0;
            if (receiptCompanyId > 0)
            {
                var periodCheck = await _periodGuard.CanPostAsync(receiptCompanyId, postingDate);
                if (!periodCheck.IsAllowed)
                {
                    TempData["Error"] = periodCheck.Reason
                        ?? $"Cannot post receipt: posting period for {postingDate:yyyy-MM-dd} is closed.";
                    return RedirectToPage("Receive", new { id });
                }
            }

            var receiptNumber = $"GR-{DateTime.UtcNow:yyyyMMdd}-{DateTime.UtcNow:HHmmss}";

            var receiptStatusLv = await _lookupService.GetValueByCodeAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId, "ReceiptStatus", "1");

            var receivingLoc = receivingLocationId.HasValue
                ? await _context.Locations.FindAsync(receivingLocationId.Value)
                : null;

            var receipt = new GoodsReceipt
            {
                ReceiptNumber = receiptNumber,
                PurchaseOrderId = po.Id,
                Status = ReceiptStatus.Received,
                StatusLookupValueId = receiptStatusLv?.Id,
                ReceiptDate = receiptDate ?? DateTime.Today,
                ReceivedBy = User.Identity?.Name ?? "admin",
                ShippingCarrier = shippingCarrier,
                TrackingNumber = trackingNumber,
                PackingSlipNumber = packingSlipNumber,
                ReceivingLocation = receivingLoc?.Name,
                Notes = headerNotes,
                CompanyId = po.CompanyId ?? _tenantContext.CompanyId,
                CreatedAt = DateTime.UtcNow
            };

            int lineNum = 0;
            int totalItemsReceived = 0;
            foreach (var line in receivingLines)
            {
                var poLine = po.Lines.First(l => l.Id == line.POLineId);
                lineNum++;
                totalItemsReceived++;

                receipt.Lines.Add(new GoodsReceiptLine
                {
                    PurchaseOrderLineId = poLine.Id,
                    LineNumber = lineNum,
                    QuantityReceived = line.QuantityToReceive,
                    QuantityAccepted = line.QuantityToReceive - line.QuantityRejected,
                    QuantityRejected = line.QuantityRejected,
                    RejectionReason = line.RejectionReason,
                    LotNumber = line.LotNumber,
                    SerialNumber = line.SerialNumber,
                    StorageLocation = line.StorageLocation ?? receivingLoc?.Name,
                    ReceivingLocationId = receivingLocationId,
                    Notes = line.Notes
                });

                poLine.QuantityReceived += line.QuantityToReceive;
                _context.Entry(poLine).Property(p => p.QuantityReceived).IsModified = true;
            }

            bool allFullyReceived = po.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered);
            var newStatus = allFullyReceived ? POStatus.Received : POStatus.PartiallyReceived;
            po.Status = newStatus;
            var poStatusLv = await _lookupService.GetValueByCodeAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", ((int)newStatus).ToString());
            if (poStatusLv != null)
                po.StatusLookupValueId = poStatusLv.Id;

            _context.Entry(po).Property(p => p.Status).IsModified = true;
            _context.Entry(po).Property(p => p.StatusLookupValueId).IsModified = true;

            _context.GoodsReceipts.Add(receipt);

            try
            {
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Receipt {receiptNumber}: received {totalItemsReceived} line(s) against PO {po.PONumber}.";
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex,
                    "Concurrency conflict saving receipt {ReceiptNumber} for PO {PONumber} (Id={POId}, CompanyId={CompanyId})",
                    receiptNumber, po.PONumber, po.Id, po.CompanyId);
                TempData["Error"] = "Another user updated this purchase order while you were receiving. Reload and try again.";
                return RedirectToPage("Receive", new { id });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex,
                    "DbUpdateException saving receipt {ReceiptNumber} for PO {PONumber} (Id={POId}, CompanyId={CompanyId}). Inner: {InnerMessage}",
                    receiptNumber, po.PONumber, po.Id, po.CompanyId, ex.InnerException?.Message);
                TempData["Error"] = $"Could not save receipt {receiptNumber}: {ex.InnerException?.Message ?? ex.Message}";
                return RedirectToPage("Receive", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error saving receipt {ReceiptNumber} for PO {PONumber} (Id={POId}, CompanyId={CompanyId})",
                    receiptNumber, po.PONumber, po.Id, po.CompanyId);
                TempData["Error"] = $"Unexpected error saving receipt {receiptNumber}. Reference {po.Id}-{DateTime.UtcNow:HHmmss} for support.";
                return RedirectToPage("Receive", new { id });
            }

            if (allFullyReceived)
                return RedirectToPage("Index");

            return RedirectToPage("Receive", new { id });
        }
    }
}
