using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Cip
{
    public class CipTraceQueryService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public CipTraceQueryService(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<List<MaintenanceEvent>> GetRelatedWorkOrdersAsync(int cipProjectId)
        {
            return await _db.MaintenanceEvents
                .Where(w => w.CipProjectId == cipProjectId)
                .OrderByDescending(w => w.ScheduledDate)
                .ToListAsync();
        }

        public async Task<List<PurchaseOrder>> GetRelatedPurchaseOrdersAsync(int cipProjectId)
        {
            var directPOs = await _db.PurchaseOrders
                .Where(po => po.CipProjectId == cipProjectId)
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();

            var linePOIds = await _db.Set<PurchaseOrderLine>()
                .Where(l => l.CipProjectId == cipProjectId)
                .Select(l => l.PurchaseOrderId)
                .Distinct()
                .ToListAsync();

            var linePOs = await _db.PurchaseOrders
                .Where(po => linePOIds.Contains(po.Id) && !directPOs.Select(d => d.Id).Contains(po.Id))
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();

            return directPOs.Concat(linePOs).ToList();
        }

        public async Task<List<VendorInvoice>> GetRelatedVendorInvoicesAsync(int cipProjectId)
        {
            var invoiceIds = await _db.Set<VendorInvoiceLine>()
                .Where(l => l.CipProjectId == cipProjectId)
                .Select(l => l.VendorInvoiceId)
                .Distinct()
                .ToListAsync();

            var costInvoiceIds = await _db.CipCosts
                .Where(c => c.CipProjectId == cipProjectId && c.VendorInvoiceId != null)
                .Select(c => c.VendorInvoiceId!.Value)
                .Distinct()
                .ToListAsync();

            var allIds = invoiceIds.Union(costInvoiceIds).Distinct().ToList();

            return await _db.VendorInvoices
                .Where(i => allIds.Contains(i.Id))
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();
        }

        public async Task<List<JournalEntry>> GetRelatedJournalsAsync(int cipProjectId)
        {
            var journalIds = await _db.CipCosts
                .Where(c => c.CipProjectId == cipProjectId && c.JournalEntryId != null)
                .Select(c => c.JournalEntryId!.Value)
                .Distinct()
                .ToListAsync();

            var capJournalIds = await _db.CipCapitalizations
                .Where(cap => cap.CipProjectId == cipProjectId && cap.JournalEntryId != null)
                .Select(cap => cap.JournalEntryId!.Value)
                .Distinct()
                .ToListAsync();

            var allIds = journalIds.Union(capJournalIds).Distinct().ToList();

            return await _db.JournalEntries
                .Where(j => allIds.Contains(j.Id))
                .OrderByDescending(j => j.PostingDate)
                .ToListAsync();
        }

        public async Task<List<Asset>> GetAssetLinksAsync(int cipProjectId)
        {
            var assetIds = await _db.CipCapitalizations
                .Where(cap => cap.CipProjectId == cipProjectId)
                .Select(cap => cap.AssetId)
                .Distinct()
                .ToListAsync();

            var companyId = GetCompanyId();
            var project = await _db.CipProjects.Where(p => p.Id == cipProjectId && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (project?.ConvertedAssetId != null && !assetIds.Contains(project.ConvertedAssetId.Value))
                assetIds.Add(project.ConvertedAssetId.Value);

            return await _db.Assets
                .Where(a => assetIds.Contains(a.Id))
                .ToListAsync();
        }

        public async Task<List<GoodsReceipt>> GetRelatedReceiptsAsync(int cipProjectId)
        {
            var receiptIds = await _db.CipCosts
                .Where(c => c.CipProjectId == cipProjectId && c.GoodsReceiptId != null)
                .Select(c => c.GoodsReceiptId!.Value)
                .Distinct()
                .ToListAsync();

            return await _db.GoodsReceipts
                .Where(r => receiptIds.Contains(r.Id))
                .OrderByDescending(r => r.ReceiptDate)
                .ToListAsync();
        }
    }
}
