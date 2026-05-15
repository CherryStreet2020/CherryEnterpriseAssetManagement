using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Cip;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Receiving;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
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
        private readonly CipAutoCostPostingService _cipAutoCostPosting;
        private readonly IReceivingPostingService _receivingPosting;
        private readonly IOutboxWriter _outbox;

        public ReceiveModel(AppDbContext context, IModuleGuardService moduleGuard,
            ITenantContext tenantContext, ILookupService lookupService, IPeriodGuard periodGuard,
            ILogger<ReceiveModel> logger, CipAutoCostPostingService cipAutoCostPosting,
            IReceivingPostingService receivingPosting, IOutboxWriter outbox)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
            _periodGuard = periodGuard;
            _logger = logger;
            _cipAutoCostPosting = cipAutoCostPosting;
            _receivingPosting = receivingPosting;
            _outbox = outbox;
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

            // DEF-008: per-line receiving location FK. Required for stock items
            // (validated server-side). Pre-filled by OnGetAsync using the cascade
            // ItemCompanyStocking.DefaultLocationId → Item.DefaultLocationId → null.
            public int? ReceivingLocationId { get; set; }

            // DEF-008: rendering-only flags so the view can hide the location
            // picker on service lines and badge the suggested-default vs. picked.
            public bool IsStockItem { get; set; }
            public int? SuggestedDefaultLocationId { get; set; }
            public string? SuggestedDefaultLocationName { get; set; }
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

            // DEF-008: resolve per-line default location via cascade
            //   ItemCompanyStocking.DefaultLocationId (per-company)
            //     → Item.DefaultLocationId (global default)
            //     → null (operator must pick before submit for stock items)
            var poCompanyId = po.CompanyId ?? _tenantContext.CompanyId ?? 0;
            var itemIds = po.Lines.Where(l => l.ItemId.HasValue).Select(l => l.ItemId!.Value).Distinct().ToList();
            var stockingDefaults = itemIds.Count > 0 && poCompanyId > 0
                ? await _context.ItemCompanyStockings
                    .Where(s => s.CompanyId == poCompanyId && itemIds.Contains(s.ItemId) && s.DefaultLocationId.HasValue)
                    .Select(s => new { s.ItemId, s.DefaultLocationId })
                    .ToDictionaryAsync(s => s.ItemId, s => s.DefaultLocationId)
                : new Dictionary<int, int?>();

            Locations = await _context.Locations
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .ToListAsync();
            var locationsById = Locations.ToDictionary(l => l.Id, l => l);

            Lines = po.Lines.OrderBy(l => l.LineNumber).Select(l =>
            {
                int? suggested = null;
                if (l.ItemId.HasValue)
                {
                    if (stockingDefaults.TryGetValue(l.ItemId.Value, out var perCo) && perCo.HasValue)
                        suggested = perCo;
                    else
                        suggested = l.Item?.DefaultLocationId;
                }
                var suggestedName = suggested.HasValue && locationsById.TryGetValue(suggested.Value, out var loc)
                    ? loc.Name
                    : null;
                var isStock = l.Item != null && l.Item.Type != ItemType.Service;
                return new ReceiveLineViewModel
                {
                    POLineId = l.Id,
                    ItemDescription = l.Description,
                    PartNumber = l.PartNumber ?? l.Item?.PartNumber,
                    UOM = l.UOM,
                    UnitPrice = l.UnitPrice,
                    QuantityOrdered = l.QuantityOrdered,
                    QuantityPreviouslyReceived = l.QuantityReceived,
                    QuantityRemaining = l.QuantityOrdered - l.QuantityReceived,
                    ReceivingLocationId = suggested,
                    IsStockItem = isStock,
                    SuggestedDefaultLocationId = suggested,
                    SuggestedDefaultLocationName = suggestedName
                };
            }).ToList();

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

                // DEF-008: stock items MUST have a put-away location. Without one
                // the receive posting service can't update ItemInventory, leaving
                // stock counts wrong silently. Form-level default falls back if
                // the per-line was left blank.
                var isStock = poLine.Item != null && poLine.Item.Type != ItemType.Service;
                if (isStock)
                {
                    var perLine = line.ReceivingLocationId;
                    var effective = perLine ?? receivingLocationId;
                    if (!effective.HasValue)
                    {
                        errors.Add($"Line '{poLine.Description}': stock items require a put-away location. Pick a location on the line, or set a Default Location on the Item master.");
                    }
                    else
                    {
                        // Stamp the effective location back on the line so the
                        // create-loop below uses it without re-resolving.
                        line.ReceivingLocationId = effective;
                    }
                }
                else
                {
                    // Service / non-stock: location is optional. Fall back if provided.
                    line.ReceivingLocationId = line.ReceivingLocationId ?? receivingLocationId;
                }
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

                // DEF-008: per-line ReceivingLocationId, validated above for stock.
                // Resolve the StorageLocation display string from the per-line FK
                // if set; fall back to the form-level dropdown for non-stock lines.
                Location? perLineLoc = null;
                if (line.ReceivingLocationId.HasValue)
                {
                    perLineLoc = await _context.Locations.FindAsync(line.ReceivingLocationId.Value);
                }
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
                    StorageLocation = perLineLoc?.Name ?? line.StorageLocation ?? receivingLoc?.Name,
                    ReceivingLocationId = line.ReceivingLocationId,
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

                // S1-1: GR/IR accrual + inventory movement. Posts the JE
                // (Dr Inventory/Expense / Cr GR-Accrued), increments
                // ItemInventory at the receiving location, writes
                // ItemTransaction(Receipt) audit rows. Idempotent on retry
                // (existing JE.Reference == "GR-{receiptNumber}" returns
                // without rewrite). Failure logs but does not roll back the
                // GR — operational truth wins; financial posting is fixable.
                //
                // 2026-05-15 diagnostic addition: surface the exception
                // message in TempData["Warning"] so the operator (and the
                // smoke-test runner) sees the real cause. Also reset the
                // ChangeTracker state for any failed-Add entities, so the
                // downstream Enqueue's SaveChanges doesn't retry them.
                ReceivingPostingResult? postingResult = null;
                string? postingError = null;
                try
                {
                    postingResult = await _receivingPosting.PostReceiptAsync(receipt.Id);
                }
                catch (Exception postEx)
                {
                    _logger.LogError(postEx,
                        "ReceivingPostingService failed for receipt {ReceiptNumber} (Id={ReceiptId}, Co={Co})",
                        receiptNumber, receipt.Id, receipt.CompanyId);
                    postingError = $"{postEx.GetType().Name}: {postEx.Message}";
                    if (postEx.InnerException != null)
                        postingError += $" — inner: {postEx.InnerException.GetType().Name}: {postEx.InnerException.Message}";

                    // Detach any entities the posting service attempted to add
                    // before throwing, so the downstream Enqueue.SaveChanges
                    // doesn't re-attempt them and fail again.
                    var staleAdds = _context.ChangeTracker.Entries()
                        .Where(e => e.State == EntityState.Added &&
                                    e.Entity is not OutboxEvent &&
                                    e.Entity is not GoodsReceipt &&
                                    e.Entity is not GoodsReceiptLine)
                        .ToList();
                    foreach (var entry in staleAdds)
                    {
                        entry.State = EntityState.Detached;
                    }
                }

                // S1-3: route any CIP-tagged receipt lines into CipAutoCostPostingService.
                // The service is idempotent — re-runs against an already-posted line
                // return the existing CipCost. CIP-tagged lines come from the PO line's
                // CipProjectId or the GR line's own override; the service decides.
                // CIP failure must NOT roll back the GR — receipt is the operational truth,
                // CIP routing is a downstream financial concern. Log + continue.
                int cipRouted = 0;
                foreach (var receiptLine in receipt.Lines)
                {
                    try
                    {
                        var cipCost = await _cipAutoCostPosting.PostFromReceiptLineAsync(receiptLine.Id);
                        if (cipCost != null) cipRouted++;
                    }
                    catch (Exception cipEx)
                    {
                        _logger.LogError(cipEx,
                            "CIP auto-cost posting failed for receipt line {LineId} on receipt {ReceiptNumber}",
                            receiptLine.Id, receiptNumber);
                    }
                }

                var summary = $"Receipt {receiptNumber}: received {totalItemsReceived} line(s) against PO {po.PONumber}.";
                if (postingResult?.JournalEntryId.HasValue == true && postingResult.TotalAccrued > 0)
                    summary += $" Accrued ${postingResult.TotalAccrued:N2}.";
                if (cipRouted > 0) summary += $" {cipRouted} routed to CIP.";
                TempData["Success"] = summary;

                // 2026-05-15: surface the inner posting failure (if any) so it
                // doesn't stay invisible. Operator sees Success (receipt
                // persisted) + Warning (financial chain didn't run).
                if (!string.IsNullOrEmpty(postingError))
                {
                    TempData["Warning"] = $"Receipt saved, but financial posting failed: {postingError}";
                }

                await _outbox.EnqueueAsync(
                    receipt.CompanyId ?? 0,
                    siteId: po.ShipToSiteId,
                    new PoReceivedV1(
                        PurchaseOrderId: po.Id,
                        PoNumber: po.PONumber,
                        GoodsReceiptId: receipt.Id,
                        ReceiptNumber: receipt.ReceiptNumber,
                        CompanyId: receipt.CompanyId,
                        VendorId: po.VendorId,
                        ReceiptDate: receipt.ReceiptDate,
                        LinesReceivedCount: totalItemsReceived,
                        IsFullyReceived: allFullyReceived,
                        PoStatusAfter: newStatus.ToString(),
                        AccrualJournalEntryId: postingResult?.JournalEntryId,
                        AccrualTotal: postingResult?.TotalAccrued ?? 0m,
                        InventoryRowsTouched: postingResult?.InventoryRowsTouched ?? 0,
                        CipLinesRouted: cipRouted,
                        ReceivedBy: receipt.ReceivedBy),
                    correlationId: $"po-receive-{receipt.Id}"
                );
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
