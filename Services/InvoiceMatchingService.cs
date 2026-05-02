using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class InvoiceMatchingService
    {
        private readonly AppDbContext _db;
        private const decimal Tolerance = 0.01m;

        public InvoiceMatchingService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<InvoiceMatchStatus> EvaluateMatchAsync(int invoiceId)
        {
            var invoice = await _db.VendorInvoices
                .Include(i => i.Lines)
                    .ThenInclude(l => l.PurchaseOrderLine)
                .Include(i => i.Lines)
                    .ThenInclude(l => l.GoodsReceiptLine)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                return InvoiceMatchStatus.NotMatched;

            if (!invoice.Lines.Any())
                return InvoiceMatchStatus.NotMatched;

            int fullyMatched = 0;
            int partiallyLinked = 0;
            int exceptions = 0;
            int unlinked = 0;

            foreach (var line in invoice.Lines)
            {
                if (line.PurchaseOrderLineId == null)
                {
                    unlinked++;
                    continue;
                }

                var poLine = line.PurchaseOrderLine;
                if (poLine == null)
                {
                    unlinked++;
                    continue;
                }

                if (line.GoodsReceiptLineId == null || line.GoodsReceiptLine == null)
                {
                    partiallyLinked++;
                    continue;
                }

                var grLine = line.GoodsReceiptLine;

                bool qtyMatch = line.Quantity == poLine.QuantityOrdered && grLine.QuantityReceived >= poLine.QuantityOrdered;
                decimal poLineTotal = poLine.QuantityOrdered * poLine.UnitPrice;
                bool amountMatch = Math.Abs(line.LineTotal - poLineTotal) <= Tolerance;

                if (qtyMatch && amountMatch)
                    fullyMatched++;
                else
                    exceptions++;
            }

            if (exceptions > 0)
                return InvoiceMatchStatus.Exception;
            if (fullyMatched == invoice.Lines.Count)
                return InvoiceMatchStatus.FullyMatched;
            if (fullyMatched > 0 || partiallyLinked > 0)
                return InvoiceMatchStatus.PartialMatch;

            return InvoiceMatchStatus.NotMatched;
        }

        public async Task<int> AutoLinkToPOAsync(int invoiceId)
        {
            var invoice = await _db.VendorInvoices
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);

            if (invoice == null)
                return 0;

            var vendorPOs = await _db.PurchaseOrders
                .Include(po => po.Lines)
                .Where(po => po.VendorId == invoice.VendorId
                    && (po.CompanyId == invoice.CompanyId || po.CompanyId == null)
                    && (po.Status == POStatus.Approved || po.Status == POStatus.Sent
                        || po.Status == POStatus.PartiallyReceived || po.Status == POStatus.Received))
                .ToListAsync();

            var allPOLines = vendorPOs.SelectMany(po => po.Lines).ToList();

            var poIds = vendorPOs.Select(po => po.Id).ToList();
            var grLines = await _db.GoodsReceiptLines
                .Include(grl => grl.GoodsReceipt)
                .Where(grl => poIds.Contains(grl.GoodsReceipt!.PurchaseOrderId))
                .ToListAsync();

            int linked = 0;

            foreach (var invLine in invoice.Lines)
            {
                if (invLine.PurchaseOrderLineId != null)
                    continue;

                var candidates = allPOLines
                    .Where(pol => !string.IsNullOrEmpty(pol.Description)
                        && pol.Description.Equals(invLine.Description, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (candidates.Count != 1)
                    continue;

                var matchedPOLine = candidates[0];
                invLine.PurchaseOrderLineId = matchedPOLine.Id;
                _db.Entry(invLine).Property(l => l.PurchaseOrderLineId).IsModified = true;

                var grCandidates = grLines
                    .Where(grl => grl.PurchaseOrderLineId == matchedPOLine.Id)
                    .ToList();

                if (grCandidates.Count == 1)
                {
                    invLine.GoodsReceiptLineId = grCandidates[0].Id;
                    _db.Entry(invLine).Property(l => l.GoodsReceiptLineId).IsModified = true;
                }

                linked++;
            }

            if (linked > 0)
                await _db.SaveChangesAsync();

            return linked;
        }

        public async Task<(bool Success, string? Error)> LinkLineAsync(int invoiceLineId, int? poLineId, int? grLineId, int invoiceVendorId, int? invoiceCompanyId)
        {
            var line = await _db.VendorInvoiceLines.Where(l => l.Id == invoiceLineId).FirstOrDefaultAsync();
            if (line == null)
                return (false, "Invoice line not found.");

            if (poLineId.HasValue)
            {
                var poLine = await _db.PurchaseOrderLines
                    .Include(pl => pl.PurchaseOrder)
                    .FirstOrDefaultAsync(pl => pl.Id == poLineId.Value);

                if (poLine == null)
                    return (false, "PO line not found.");

                if (poLine.PurchaseOrder?.VendorId != invoiceVendorId)
                    return (false, "PO line does not belong to this invoice's vendor.");

                if (invoiceCompanyId.HasValue && poLine.PurchaseOrder?.CompanyId != null
                    && poLine.PurchaseOrder.CompanyId != invoiceCompanyId)
                    return (false, "PO line does not belong to the same company.");
            }

            if (grLineId.HasValue)
            {
                var grLine = await _db.GoodsReceiptLines
                    .Include(grl => grl.GoodsReceipt)
                    .FirstOrDefaultAsync(grl => grl.Id == grLineId.Value);

                if (grLine == null)
                    return (false, "Goods receipt line not found.");

                if (poLineId.HasValue && grLine.PurchaseOrderLineId != poLineId.Value)
                    return (false, "Goods receipt line does not belong to the selected PO line.");
            }

            line.PurchaseOrderLineId = poLineId;
            line.GoodsReceiptLineId = grLineId;
            _db.Entry(line).Property(l => l.PurchaseOrderLineId).IsModified = true;
            _db.Entry(line).Property(l => l.GoodsReceiptLineId).IsModified = true;

            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task UnlinkLineAsync(int invoiceLineId)
        {
            var line = await _db.VendorInvoiceLines.Where(l => l.Id == invoiceLineId).FirstOrDefaultAsync();
            if (line == null)
                return;

            line.PurchaseOrderLineId = null;
            line.GoodsReceiptLineId = null;
            _db.Entry(line).Property(l => l.PurchaseOrderLineId).IsModified = true;
            _db.Entry(line).Property(l => l.GoodsReceiptLineId).IsModified = true;

            await _db.SaveChangesAsync();
        }

        public async Task UpdateInvoiceMatchStatusAsync(int invoiceId)
        {
            var invoice = await _db.VendorInvoices.Where(i => i.Id == invoiceId).FirstOrDefaultAsync();
            if (invoice == null)
                return;

            var status = await EvaluateMatchAsync(invoiceId);
            invoice.MatchStatus = status;
            _db.Entry(invoice).Property(i => i.MatchStatus).IsModified = true;
            await _db.SaveChangesAsync();
        }
    }
}
