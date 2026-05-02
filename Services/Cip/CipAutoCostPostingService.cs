using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Cip
{
    public class CipAutoCostPostingService
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly CipCostService _cipCostService;

        public CipAutoCostPostingService(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext, CipCostService cipCostService)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
            _cipCostService = cipCostService;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

        public async Task<CipCost?> PostFromWorkOrderAsync(int workOrderId)
        {
            var companyId = GetCompanyId();
            var wo = await _db.MaintenanceEvents
                .Include(e => e.Asset)
                .Where(e => e.Id == workOrderId && e.Asset != null && _tenantContext.VisibleCompanyIds.Contains(e.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (wo == null || wo.CipProjectId == null) return null;

            var project = await _db.CipProjects.Where(p => p.Id == wo.CipProjectId.Value && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (project == null || project.IsLocked) return null;

            var existingCost = await _db.CipCosts
                .FirstOrDefaultAsync(c => c.SourceType == "WorkOrder" && c.SourceHeaderId == workOrderId && c.CipProjectId == wo.CipProjectId.Value);
            if (existingCost != null) return existingCost;

            var laborCostTypeId = await ResolveCostTypeLookupValueIdAsync("LABOR");
            var laborAmount = wo.LaborCost ?? wo.ActualCost ?? 0m;
            if (laborAmount <= 0) return null;

            var cost = new CipCost
            {
                CipProjectId = wo.CipProjectId.Value,
                CostTypeLookupValueId = laborCostTypeId,
                CostType = CipCostType.Labor,
                Amount = laborAmount,
                TransactionDate = wo.CompletedDate ?? DateTime.UtcNow,
                Description = $"Labor from WO {wo.WorkOrderNumber}: {wo.Description}",
                SourceType = "WorkOrder",
                SourceHeaderId = workOrderId,
                SourceDisplayRef = $"WO-{wo.WorkOrderNumber}",
                WorkOrderId = workOrderId,
                IsCapitalizable = true,
                EnteredBy = "system",
                CreatedByUserId = "system",
                CreatedAt = DateTime.UtcNow
            };

            _db.CipCosts.Add(cost);
            await _db.SaveChangesAsync();
            await _cipCostService.ReconcileProjectTotalAsync(wo.CipProjectId.Value);
            return cost;
        }

        public async Task<CipCost?> PostFromReceiptLineAsync(int receiptLineId)
        {
            var receiptLine = await _db.Set<GoodsReceiptLine>()
                .Include(l => l.GoodsReceipt)
                .Include(l => l.PurchaseOrderLine)
                .FirstOrDefaultAsync(l => l.Id == receiptLineId);
            if (receiptLine == null) return null;

            var cipProjectId = receiptLine.CipProjectId
                ?? receiptLine.PurchaseOrderLine?.CipProjectId;
            if (cipProjectId == null) return null;

            var companyId = GetCompanyId();
            var project = await _db.CipProjects.Where(p => p.Id == cipProjectId.Value && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (project == null || project.IsLocked) return null;

            var existingCost = await _db.CipCosts
                .FirstOrDefaultAsync(c => c.SourceType == "Receipt" && c.SourceLineId == receiptLineId && c.CipProjectId == cipProjectId.Value);
            if (existingCost != null) return existingCost;

            var materialsCostTypeId = await ResolveCostTypeLookupValueIdAsync("MATERIALS");
            var amount = receiptLine.QuantityAccepted * (receiptLine.PurchaseOrderLine?.UnitPrice ?? 0m);
            if (amount <= 0) return null;

            var poLine = receiptLine.PurchaseOrderLine;
            var receipt = receiptLine.GoodsReceipt;

            var cost = new CipCost
            {
                CipProjectId = cipProjectId.Value,
                CostTypeLookupValueId = materialsCostTypeId,
                CostType = CipCostType.Materials,
                Amount = amount,
                TransactionDate = receipt?.ReceiptDate ?? DateTime.UtcNow,
                Description = $"Receipt {receipt?.ReceiptNumber}: {poLine?.Description ?? poLine?.PartNumber ?? receipt?.ReceiptNumber ?? "N/A"}",
                SourceType = "Receipt",
                SourceHeaderId = receipt?.Id,
                SourceLineId = receiptLineId,
                SourceDisplayRef = $"RCV-{receipt?.ReceiptNumber}",
                GoodsReceiptId = receipt?.Id,
                GoodsReceiptLineId = receiptLineId,
                PurchaseOrderId = receipt?.PurchaseOrderId,
                PurchaseOrderLineId = poLine?.Id,
                IsCapitalizable = true,
                EnteredBy = "system",
                CreatedByUserId = "system",
                CreatedAt = DateTime.UtcNow
            };

            _db.CipCosts.Add(cost);
            await _db.SaveChangesAsync();
            await _cipCostService.ReconcileProjectTotalAsync(cipProjectId.Value);
            return cost;
        }

        public async Task<CipCost?> PostFromVendorInvoiceLineAsync(int invoiceLineId)
        {
            var invoiceLine = await _db.Set<VendorInvoiceLine>()
                .Include(l => l.VendorInvoice)
                .FirstOrDefaultAsync(l => l.Id == invoiceLineId);
            if (invoiceLine == null) return null;

            var cipProjectId = invoiceLine.CipProjectId;
            if (cipProjectId == null) return null;

            var project = await _db.CipProjects.Where(p => p.Id == cipProjectId.Value && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (project == null || project.IsLocked) return null;

            var existingCost = await _db.CipCosts
                .FirstOrDefaultAsync(c => c.SourceType == "Invoice" && c.SourceLineId == invoiceLineId && c.CipProjectId == cipProjectId.Value);
            if (existingCost != null) return existingCost;

            var equipmentCostTypeId = await ResolveCostTypeLookupValueIdAsync("EQUIPMENT");

            var cost = new CipCost
            {
                CipProjectId = cipProjectId.Value,
                CostTypeLookupValueId = equipmentCostTypeId,
                CostType = CipCostType.Equipment,
                Amount = invoiceLine.LineTotal,
                TransactionDate = invoiceLine.VendorInvoice?.InvoiceDate ?? DateTime.UtcNow,
                Description = $"Invoice {invoiceLine.VendorInvoice?.InvoiceNumber}: {invoiceLine.Description}",
                SourceType = "Invoice",
                SourceHeaderId = invoiceLine.VendorInvoiceId,
                SourceLineId = invoiceLineId,
                SourceDisplayRef = $"INV-{invoiceLine.VendorInvoice?.InvoiceNumber}",
                VendorInvoiceId = invoiceLine.VendorInvoiceId,
                VendorInvoiceLineId = invoiceLineId,
                VendorId = invoiceLine.VendorInvoice?.VendorId,
                IsCapitalizable = true,
                EnteredBy = "system",
                CreatedByUserId = "system",
                CreatedAt = DateTime.UtcNow
            };

            _db.CipCosts.Add(cost);
            await _db.SaveChangesAsync();
            await _cipCostService.ReconcileProjectTotalAsync(cipProjectId.Value);
            return cost;
        }

        private async Task<int?> ResolveCostTypeLookupValueIdAsync(string code)
        {
            var values = await _lookupService.GetValuesAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "CipCostType");
            return values.FirstOrDefault(v => v.Code == code)?.Id;
        }
    }
}
