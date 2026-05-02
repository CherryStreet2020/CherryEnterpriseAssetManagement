using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant")]
    public class RequisitionsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public RequisitionsModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<PurchaseRequisition> Requisitions { get; set; } = new();
        public List<ReorderAlert> ReorderAlerts { get; set; } = new();
        public List<Vendor> Vendors { get; set; } = new();
        public List<Item> Items { get; set; } = new();
        public List<ItemCategory> ExpenseCategories { get; set; } = new();
        public List<GlAccount> GlAccounts { get; set; } = new();
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> PriorityOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            StatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RequisitionStatus", null, "All Statuses");
            PriorityOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RequisitionPriority", null, "");
            Requisitions = await _db.PurchaseRequisitions
                .Include(r => r.SuggestedVendor)
                .Include(r => r.Lines)
                .OrderByDescending(r => r.RequisitionDate)
                .ToListAsync();

            ReorderAlerts = await _db.ReorderAlerts
                .Include(a => a.Item)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            Vendors = await _db.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.Name)
                .ToListAsync();

            Items = await _db.Items
                .Where(i => i.IsActive && i.IsPurchasable)
                .OrderBy(i => i.PartNumber)
                .ToListAsync();

            ExpenseCategories = await _db.ItemCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            GlAccounts = await _db.GlAccounts
                .Where(g => g.IsActive)
                .OrderBy(g => g.AccountNumber)
                .ToListAsync();

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            int priorityLookupValueId, DateTime? requiredDate, int? suggestedVendorId,
            string? requestor, string? department, string? justification,
            int? itemId, decimal quantity, decimal unitPrice, string uom, string? notes,
            bool isNonItemMaster, int? expenseCategoryId, string? nonItemDescription,
            string? nonItemMfrPartNumber, string? nonItemVendorPartNumber,
            decimal? nonItemQuantity, decimal? nonItemUnitPrice, string? nonItemUOM, int? nonItemGlAccountId)
        {
            if (isNonItemMaster)
            {
                if (string.IsNullOrWhiteSpace(nonItemDescription))
                {
                    TempData["Error"] = "Description is required for non-Item Master items.";
                    return RedirectToPage();
                }
                if (!expenseCategoryId.HasValue)
                {
                    TempData["Error"] = "Expense category is required for non-Item Master items.";
                    return RedirectToPage();
                }
                if ((nonItemQuantity ?? 0) <= 0)
                {
                    TempData["Error"] = "Quantity must be greater than zero.";
                    return RedirectToPage();
                }
            }
            else
            {
                if (!itemId.HasValue)
                {
                    TempData["Error"] = "Please select an item from the Item Master.";
                    return RedirectToPage();
                }
                var item = await _db.Items.Where(i => i.Id == itemId).FirstOrDefaultAsync();
                if (item != null && item.RequireRevisionControl && string.IsNullOrWhiteSpace(item.Revision))
                {
                    TempData["Error"] = $"Item {item.PartNumber} requires revision control but has no revision set.";
                    return RedirectToPage();
                }
                if (quantity <= 0)
                {
                    TempData["Error"] = "Quantity must be greater than zero.";
                    return RedirectToPage();
                }
            }

            var reqNumber = await GenerateRequisitionNumber();

            var effectiveQuantity = isNonItemMaster ? (nonItemQuantity ?? 1) : quantity;
            var effectiveUnitPrice = isNonItemMaster ? (nonItemUnitPrice ?? 0) : unitPrice;

            var priorityEnum = RequisitionPriority.Normal;
            int? resolvedPriorityLvId = null;
            if (priorityLookupValueId > 0)
            {
                var priorityLv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, priorityLookupValueId);
                if (priorityLv != null && int.TryParse(priorityLv.Code, out var priEnumVal))
                    priorityEnum = (RequisitionPriority)priEnumVal;
                resolvedPriorityLvId = priorityLookupValueId;
            }

            var pendingLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RequisitionStatus", ((int)RequisitionStatus.Pending).ToString());

            var requisition = new PurchaseRequisition
            {
                RequisitionNumber = reqNumber,
                Status = RequisitionStatus.Pending,
                StatusLookupValueId = pendingLv?.Id,
                Priority = priorityEnum,
                PriorityLookupValueId = resolvedPriorityLvId,
                Source = RequisitionSource.Manual,
                RequisitionDate = DateTime.UtcNow,
                RequiredDate = requiredDate,
                SuggestedVendorId = suggestedVendorId,
                Requestor = requestor,
                Department = department,
                Justification = justification,
                Notes = notes,
                TotalAmount = effectiveQuantity * effectiveUnitPrice,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "system"
            };

            _db.PurchaseRequisitions.Add(requisition);
            await _db.SaveChangesAsync();

            PurchaseRequisitionLine line;

            if (isNonItemMaster)
            {
                line = new PurchaseRequisitionLine
                {
                    RequisitionId = requisition.Id,
                    LineNumber = 1,
                    IsNonItemMaster = true,
                    ItemId = null,
                    PartNumber = null,
                    Description = nonItemDescription,
                    ManufacturerPartNumber = nonItemMfrPartNumber,
                    VendorPartNumber = nonItemVendorPartNumber,
                    ExpenseCategoryId = expenseCategoryId,
                    GlAccountId = nonItemGlAccountId,
                    Quantity = nonItemQuantity ?? 1,
                    UOM = nonItemUOM ?? "EA",
                    UnitPrice = nonItemUnitPrice ?? 0,
                    SuggestedVendorId = suggestedVendorId,
                    CreatedAt = DateTime.UtcNow
                };
            }
            else
            {
                var item = await _db.Items.Where(i => i.Id == itemId).FirstOrDefaultAsync();
                line = new PurchaseRequisitionLine
                {
                    RequisitionId = requisition.Id,
                    LineNumber = 1,
                    IsNonItemMaster = false,
                    ItemId = itemId,
                    PartNumber = item?.PartNumber,
                    Description = item?.Description,
                    ManufacturerPartNumber = item?.ManufacturerPartNumber,
                    Revision = item?.Revision,
                    Quantity = quantity,
                    UOM = uom,
                    UnitPrice = unitPrice,
                    SuggestedVendorId = suggestedVendorId,
                    CreatedAt = DateTime.UtcNow
                };
            }

            _db.PurchaseRequisitionLines.Add(line);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Requisition {reqNumber} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostApproveAsync(int id)
        {
            var req = await _db.PurchaseRequisitions
                .Where(r => (r.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0)) && r.Id == id)
                .FirstOrDefaultAsync();
            if (req == null)
            {
                TempData["Error"] = "Requisition not found.";
                return RedirectToPage();
            }

            req.Status = RequisitionStatus.Approved;
            var approvedLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RequisitionStatus", ((int)RequisitionStatus.Approved).ToString());
            if (approvedLv != null)
                req.StatusLookupValueId = approvedLv.Id;
            req.ApprovedBy = User.Identity?.Name ?? "system";
            req.ApprovedDate = DateTime.UtcNow;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Requisition {req.RequisitionNumber} approved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostConvertToPOAsync(int id)
        {
            var req = await _db.PurchaseRequisitions
                .Include(r => r.Lines!)
                .ThenInclude(l => l.Item)
                .Where(r => (r.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0)) && r.Id == id)
                .FirstOrDefaultAsync();

            if (req == null)
            {
                TempData["Error"] = "Requisition not found.";
                return RedirectToPage();
            }

            if (!req.SuggestedVendorId.HasValue || req.SuggestedVendorId <= 0)
            {
                TempData["Error"] = "Cannot convert to PO: Requisition must have a vendor assigned first.";
                return RedirectToPage();
            }

            var poNumber = await GeneratePONumber();

            var poApprovedLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", ((int)POStatus.Approved).ToString());

            var po = new PurchaseOrder
            {
                PONumber = poNumber,
                VendorId = req.SuggestedVendorId.Value,
                OrderDate = DateTime.UtcNow,
                Status = POStatus.Approved,
                StatusLookupValueId = poApprovedLv?.Id,
                Subtotal = req.TotalAmount,
                Total = req.TotalAmount,
                Notes = $"Created from requisition {req.RequisitionNumber}",
                CreatedAt = DateTime.UtcNow
            };

            _db.PurchaseOrders.Add(po);
            await _db.SaveChangesAsync();

            int lineNum = 1;
            foreach (var line in req.Lines ?? new List<PurchaseRequisitionLine>())
            {
                var poLine = new PurchaseOrderLine
                {
                    PurchaseOrderId = po.Id,
                    LineNumber = lineNum++,
                    IsNonItemMaster = line.IsNonItemMaster,
                    ItemId = line.ItemId,
                    PartNumber = line.PartNumber,
                    Description = line.Description ?? "",
                    ManufacturerPartNumber = line.ManufacturerPartNumber,
                    VendorPartNumber = line.VendorPartNumber,
                    Revision = line.Revision,
                    ExpenseCategoryId = line.ExpenseCategoryId,
                    QuantityOrdered = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    LineTotal = line.Quantity * line.UnitPrice,
                    UOM = line.UOM
                };
                _db.PurchaseOrderLines.Add(poLine);
            }

            req.Status = RequisitionStatus.Converted;
            var convertedLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RequisitionStatus", ((int)RequisitionStatus.Converted).ToString());
            if (convertedLv != null)
                req.StatusLookupValueId = convertedLv.Id;
            req.ConvertedToPOId = po.Id;
            req.ConvertedDate = DateTime.UtcNow;
            req.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Requisition converted to PO {poNumber}.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCreateFromAlertAsync(int alertId)
        {
            var alert = await _db.ReorderAlerts
                .Include(a => a.Item)
                .ThenInclude(i => i!.PrimaryVendor)
                .FirstOrDefaultAsync(a => a.Id == alertId);

            if (alert?.Item == null)
            {
                TempData["Error"] = "Alert or item not found.";
                return RedirectToPage();
            }

            var reqNumber = await GenerateRequisitionNumber();

            var pendingLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RequisitionStatus", ((int)RequisitionStatus.Pending).ToString());
            var highLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RequisitionPriority", ((int)RequisitionPriority.High).ToString());

            var requisition = new PurchaseRequisition
            {
                RequisitionNumber = reqNumber,
                Status = RequisitionStatus.Pending,
                StatusLookupValueId = pendingLv?.Id,
                Priority = RequisitionPriority.High,
                PriorityLookupValueId = highLv?.Id,
                Source = alert.AlertType,
                RequisitionDate = DateTime.UtcNow,
                SuggestedVendorId = alert.Item.PrimaryVendorId,
                Requestor = "System - Auto Reorder",
                Justification = $"Auto-generated from reorder alert. Current stock: {alert.CurrentStock:N2}, Reorder point: {alert.ReorderPoint:N2}",
                TotalAmount = alert.SuggestedQuantity * alert.Item.StandardCost,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            _db.PurchaseRequisitions.Add(requisition);
            await _db.SaveChangesAsync();

            var line = new PurchaseRequisitionLine
            {
                RequisitionId = requisition.Id,
                LineNumber = 1,
                ItemId = alert.ItemId,
                PartNumber = alert.Item.PartNumber,
                Description = alert.Item.Description,
                Quantity = alert.SuggestedQuantity,
                UOM = alert.Item.StockUOM,
                UnitPrice = alert.Item.StandardCost,
                CurrentStock = alert.CurrentStock,
                ReorderPoint = alert.ReorderPoint,
                SuggestedVendorId = alert.Item.PrimaryVendorId,
                CreatedAt = DateTime.UtcNow
            };

            _db.PurchaseRequisitionLines.Add(line);

            alert.IsAcknowledged = true;
            alert.AcknowledgedBy = User.Identity?.Name ?? "system";
            alert.AcknowledgedDate = DateTime.UtcNow;
            alert.RequisitionId = requisition.Id;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Requisition {reqNumber} created from reorder alert.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDismissAlertAsync(int alertId)
        {
            var alert = await _db.ReorderAlerts
                .Where(a => a.Id == alertId)
                .FirstOrDefaultAsync();
            if (alert == null)
            {
                TempData["Error"] = "Alert not found.";
                return RedirectToPage();
            }

            alert.IsAcknowledged = true;
            alert.AcknowledgedBy = User.Identity?.Name ?? "system";
            alert.AcknowledgedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = "Alert dismissed.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDuplicateAsync(int id)
        {
            var source = await _db.PurchaseRequisitions
                .Include(r => r.Lines)
                .Where(r => (r.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0)) && r.Id == id)
                .FirstOrDefaultAsync();
            
            if (source == null)
            {
                TempData["Error"] = "Requisition not found.";
                return RedirectToPage();
            }

            var newReqNumber = await GenerateRequisitionNumber();

            var draftLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "RequisitionStatus", ((int)RequisitionStatus.Draft).ToString());

            var newReq = new PurchaseRequisition
            {
                RequisitionNumber = newReqNumber,
                Status = RequisitionStatus.Draft,
                StatusLookupValueId = draftLv?.Id,
                Priority = source.Priority,
                PriorityLookupValueId = source.PriorityLookupValueId,
                Source = source.Source,
                RequisitionDate = DateTime.UtcNow,
                RequiredDate = source.RequiredDate,
                SuggestedVendorId = source.SuggestedVendorId,
                Requestor = User.Identity?.Name ?? "system",
                Department = source.Department,
                Justification = $"Duplicated from {source.RequisitionNumber}",
                CompanyId = source.CompanyId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "system"
            };

            _db.PurchaseRequisitions.Add(newReq);
            await _db.SaveChangesAsync();

            decimal totalAmount = 0;
            foreach (var srcLine in source.Lines ?? new List<PurchaseRequisitionLine>())
            {
                var newLine = new PurchaseRequisitionLine
                {
                    RequisitionId = newReq.Id,
                    LineNumber = srcLine.LineNumber,
                    ItemId = srcLine.ItemId,
                    PartNumber = srcLine.PartNumber,
                    Description = srcLine.Description,
                    Quantity = srcLine.Quantity,
                    UOM = srcLine.UOM,
                    UnitPrice = srcLine.UnitPrice,
                    SuggestedVendorId = srcLine.SuggestedVendorId,
                    GlAccountId = srcLine.GlAccountId,
                    ExpenseCategoryId = srcLine.ExpenseCategoryId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.PurchaseRequisitionLines.Add(newLine);
                totalAmount += srcLine.Quantity * srcLine.UnitPrice;
            }

            newReq.TotalAmount = totalAmount;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Requisition duplicated as {newReqNumber}.";
            return RedirectToPage();
        }

        private async Task<string> GenerateRequisitionNumber()
        {
            var year = DateTime.UtcNow.Year % 100;
            var lastReq = await _db.PurchaseRequisitions
                .Where(r => r.RequisitionNumber.StartsWith($"REQ-{year:D2}-"))
                .OrderByDescending(r => r.RequisitionNumber)
                .FirstOrDefaultAsync();

            int nextNum = 1;
            if (lastReq != null)
            {
                var parts = lastReq.RequisitionNumber.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int lastNum))
                {
                    nextNum = lastNum + 1;
                }
            }

            return $"REQ-{year:D2}-{nextNum:D5}";
        }

        public async Task<IActionResult> OnGetRequisitionDetailsAsync(int id)
        {
            var req = await _db.PurchaseRequisitions
                .Include(r => r.SuggestedVendor)
                .Include(r => r.Lines!)
                    .ThenInclude(l => l.Item)
                .Include(r => r.Lines!)
                    .ThenInclude(l => l.ExpenseCategory)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null)
                return new JsonResult(new { error = "Not found" });

            var result = new
            {
                id = req.Id,
                requisitionNumber = req.RequisitionNumber,
                status = req.Status.ToString(),
                priority = req.Priority.ToString(),
                source = req.Source.ToString(),
                requisitionDate = req.RequisitionDate.ToString("MM/dd/yyyy"),
                requiredDate = req.RequiredDate?.ToString("MM/dd/yyyy"),
                requestor = req.Requestor,
                department = req.Department,
                justification = req.Justification,
                notes = req.Notes,
                vendor = req.SuggestedVendor?.Name,
                vendorId = req.SuggestedVendorId,
                totalAmount = req.TotalAmount,
                approvedBy = req.ApprovedBy,
                approvedDate = req.ApprovedDate?.ToString("MM/dd/yyyy"),
                createdBy = req.CreatedBy,
                createdAt = req.CreatedAt.ToString("MM/dd/yyyy HH:mm"),
                lines = req.Lines?.Select(l => new
                {
                    lineNumber = l.LineNumber,
                    partNumber = l.PartNumber ?? l.Item?.PartNumber,
                    description = l.Description ?? l.Item?.Description,
                    mfrPartNumber = l.ManufacturerPartNumber ?? l.Item?.ManufacturerPartNumber,
                    vendorPartNumber = l.VendorPartNumber,
                    revision = l.Revision ?? l.Item?.Revision,
                    quantity = l.Quantity,
                    uom = l.UOM,
                    unitPrice = l.UnitPrice,
                    lineTotal = l.Quantity * l.UnitPrice,
                    isNonItemMaster = l.IsNonItemMaster,
                    expenseCategory = l.ExpenseCategory?.Name
                }).ToList()
            };

            return new JsonResult(result);
        }

        private async Task<string> GeneratePONumber()
        {
            var year = DateTime.UtcNow.Year % 100;
            var lastPO = await _db.PurchaseOrders
                .Where(p => p.PONumber.StartsWith($"PO-{year:D2}-"))
                .OrderByDescending(p => p.PONumber)
                .FirstOrDefaultAsync();

            int nextNum = 1;
            if (lastPO != null)
            {
                var parts = lastPO.PONumber.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int lastNum))
                {
                    nextNum = lastNum + 1;
                }
            }

            return $"PO-{year:D2}-{nextNum:D5}";
        }
    }
}
