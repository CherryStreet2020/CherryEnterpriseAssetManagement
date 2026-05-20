using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        /// <summary>
        /// Capitalize the WO's directly-attached costs onto its CIP project.
        ///
        /// Posts up to two CipCost rows per work order, idempotent per CostType:
        ///   • Labor          — sourced from WorkOrder.LaborCost   (CloseoutService computes from JE history)
        ///   • OutsideServices — sourced from WorkOrder.OutsideVendorCost (subcontractor field labor captured on the WO header)
        ///
        /// Materials are DELIBERATELY NOT auto-posted from the WO header. Capital
        /// materials must flow through the receipt-line path
        /// (<see cref="PostFromReceiptLineAsync"/>) — when a CIP-tagged PO line
        /// is received, the receipt posts the material cost to CIP directly. This
        /// avoids double-counting when stock from a CIP-tagged receipt is later
        /// issued to the WO. Stock-issued materials on a non-CIP-tagged receipt
        /// (i.e. existing inventory consumed by a CIP project) currently require
        /// a manual journal entry from the accountant; ticket #TODO-CIP-MATERIALS
        /// tracks the dedupe-aware auto-post for Sprint 14+ (Controller Cockpit /
        /// CIP Capitalization Wizard).
        ///
        /// Idempotency is per-CostType per-source-WO: re-running this method
        /// for the same WO returns whatever was already posted; only the missing
        /// CostType rows get added. Safe to call repeatedly from CloseoutService.
        ///
        /// Returns the list of CipCost rows touched (existing + newly created)
        /// — empty list if the WO is not CIP-tagged, the project is locked, or
        /// no LaborCost/OutsideVendorCost is recorded.
        /// </summary>
        public async Task<IReadOnlyList<CipCost>> PostFromWorkOrderAsync(int workOrderId)
        {
            var companyId = GetCompanyId();
            var wo = await _db.WorkOrders
                .Include(e => e.Asset)
                .Where(e => e.Id == workOrderId && e.Asset != null && _tenantContext.VisibleCompanyIds.Contains(e.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (wo == null || wo.CipProjectId == null) return Array.Empty<CipCost>();

            var project = await _db.CipProjects
                .Where(p => p.Id == wo.CipProjectId.Value && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (project == null || project.IsLocked) return Array.Empty<CipCost>();

            // Per-CostType idempotency. Previous behavior used a single row
            // guard which made it impossible to add a second-bucket (Labor +
            // OutsideServices) posting on the same WO. Now each CostType
            // gets its own existing-row check.
            var existingByType = await _db.CipCosts
                .Where(c => c.SourceType == "WORKORDER"
                         && c.SourceHeaderId == workOrderId
                         && c.CipProjectId == wo.CipProjectId.Value)
                .ToDictionaryAsync(c => c.CostType, c => c);

            var result = new List<CipCost>();
            // EF Core InMemory provider auto-assigns Id on Add() (eager identity
            // generation); Postgres assigns on SaveChanges. So we can't use
            // c.Id == 0 to detect new rows — InMemory tests would skip SaveChanges
            // and return rows that never actually persisted. Track explicitly.
            var addedAny = false;

            // ---- Labor row ----
            var laborAmount = wo.LaborCost ?? 0m;
            if (laborAmount > 0m)
            {
                if (existingByType.TryGetValue(CipCostType.Labor, out var existingLabor))
                {
                    result.Add(existingLabor);
                }
                else
                {
                    var laborCostTypeId = await ResolveCostTypeLookupValueIdAsync("LABOR");
                    var laborCost = new CipCost
                    {
                        CipProjectId = wo.CipProjectId.Value,
                        CostTypeLookupValueId = laborCostTypeId,
                        CostType = CipCostType.Labor,
                        Amount = laborAmount,
                        TransactionDate = wo.CompletedDate ?? DateTime.UtcNow,
                        Description = $"Labor from WO {wo.WorkOrderNumber}: {wo.Description}",
                        SourceType = "WORKORDER", // pre-uppercased to match AppDbContext.CapitalizeStringProperties so the existing-cost guard above matches its own writes
                        SourceHeaderId = workOrderId,
                        SourceDisplayRef = $"WO-{wo.WorkOrderNumber}",
                        WorkOrderId = workOrderId,
                        IsCapitalizable = true,
                        EnteredBy = "system",
                        CreatedByUserId = "system",
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.CipCosts.Add(laborCost);
                    result.Add(laborCost);
                    addedAny = true;
                }
            }

            // ---- OutsideServices row (PR #268 — fix for CIP under-capitalization
            // gap flagged by GPT audit + verified 2026-05-20). Subcontractor field
            // labor captured on the WO header field never landed in CIP before
            // because PostFromWorkOrderAsync only read LaborCost. Now posts a
            // separate CipCost row tagged CostType.OutsideServices so analytics
            // can distinguish in-house labor from outside-vendor labor. ----
            var outsideAmount = wo.OutsideVendorCost ?? 0m;
            if (outsideAmount > 0m)
            {
                if (existingByType.TryGetValue(CipCostType.OutsideServices, out var existingOutside))
                {
                    result.Add(existingOutside);
                }
                else
                {
                    var outsideCostTypeId = await ResolveCostTypeLookupValueIdAsync("OUTSIDESERVICES");
                    var outsideCost = new CipCost
                    {
                        CipProjectId = wo.CipProjectId.Value,
                        CostTypeLookupValueId = outsideCostTypeId,
                        CostType = CipCostType.OutsideServices,
                        Amount = outsideAmount,
                        TransactionDate = wo.CompletedDate ?? DateTime.UtcNow,
                        Description = $"Outside services from WO {wo.WorkOrderNumber}: {wo.Description}",
                        SourceType = "WORKORDER",
                        SourceHeaderId = workOrderId,
                        SourceDisplayRef = $"WO-{wo.WorkOrderNumber}",
                        WorkOrderId = workOrderId,
                        IsCapitalizable = true,
                        EnteredBy = "system",
                        CreatedByUserId = "system",
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.CipCosts.Add(outsideCost);
                    result.Add(outsideCost);
                    addedAny = true;
                }
            }

            if (result.Count == 0) return Array.Empty<CipCost>();

            // Only persist if we actually added new rows in this call. The
            // all-existing case (re-run after prior post) skips the write but
            // still returns the existing rows for status reporting.
            if (addedAny)
            {
                await _db.SaveChangesAsync();
                await _cipCostService.ReconcileProjectTotalAsync(wo.CipProjectId.Value);
            }
            return result;
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
                .FirstOrDefaultAsync(c => c.SourceType == "RECEIPT" && c.SourceLineId == receiptLineId && c.CipProjectId == cipProjectId.Value);
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
                SourceType = "RECEIPT",
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
                .FirstOrDefaultAsync(c => c.SourceType == "INVOICE" && c.SourceLineId == invoiceLineId && c.CipProjectId == cipProjectId.Value);
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
                SourceType = "INVOICE",
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
