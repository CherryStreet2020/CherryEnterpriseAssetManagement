using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant")]
    public class InventoryModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IItemStockingService _stockingService;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public InventoryModel(AppDbContext db, IItemStockingService stockingService, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _stockingService = stockingService;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<StockLevelView> StockLevels { get; set; } = new();
        public List<SelectListItem> TransactionTypeOptions { get; set; } = new();
        public List<LowStockItem> LowStockItems { get; set; } = new();
        public List<ItemTransaction> RecentTransactions { get; set; } = new();
        public List<Item> Items { get; set; } = new();
        public Dictionary<int, ItemStockingDto> StockingByItem { get; set; } = new();

        public int TotalSkus { get; set; }
        public int InStockCount { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public bool IsMultiCompanyMode { get; set; }
        public int CurrentCompanyId { get; set; }

        public string? SuccessMessage => TempData["Success"]?.ToString();
        public string? ErrorMessage => TempData["Error"]?.ToString();

        public async Task OnGetAsync()
        {
            IsMultiCompanyMode = await _stockingService.IsMultiCompanyModeAsync();
            CurrentCompanyId = 0;

            TransactionTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, null, "InventoryTransactionType", null, "");

            Items = await _db.Items.Where(i => i.IsActive).OrderBy(i => i.PartNumber).ToListAsync();

            var itemIds = Items.Select(i => i.Id).ToList();
            StockingByItem = await _stockingService.GetStockingForItemsAsync(itemIds, CurrentCompanyId);

            var inventories = await _db.Set<ItemInventory>()
                .Include(i => i.Item)
                .ToListAsync();

            StockLevels = inventories.Select(inv => {
                var stocking = StockingByItem.TryGetValue(inv.ItemId, out var s) ? s : null;
                var reorderPoint = stocking?.ReorderPoint ?? inv.Item?.ReorderPoint ?? 0;
                return new StockLevelView
                {
                    ItemId = inv.ItemId,
                    PartNumber = inv.Item?.PartNumber ?? "Unknown",
                    Description = inv.Item?.Description ?? "",
                    Warehouse = inv.Warehouse,
                    Bin = inv.Bin,
                    QuantityOnHand = inv.QuantityOnHand,
                    QuantityReserved = inv.QuantityReserved,
                    QuantityAvailable = inv.QuantityAvailable,
                    QuantityOnOrder = inv.QuantityOnOrder,
                    ReorderPoint = reorderPoint
                };
            }).ToList();

            TotalSkus = StockLevels.Count;
            InStockCount = StockLevels.Count(s => s.QuantityOnHand > s.ReorderPoint);
            LowStockCount = StockLevels.Count(s => s.QuantityOnHand > 0 && s.QuantityOnHand <= s.ReorderPoint);
            OutOfStockCount = StockLevels.Count(s => s.QuantityOnHand <= 0);

            LowStockItems = StockLevels
                .Where(s => s.QuantityOnHand <= s.ReorderPoint)
                .Select(s => {
                    var stocking = StockingByItem.TryGetValue(s.ItemId, out var st) ? st : null;
                    var reorderQty = stocking?.ReorderQuantity ?? Items.FirstOrDefault(i => i.Id == s.ItemId)?.ReorderQuantity ?? 10;
                    return new LowStockItem
                    {
                        ItemId = s.ItemId,
                        PartNumber = s.PartNumber,
                        Description = s.Description,
                        OnHand = s.QuantityOnHand,
                        ReorderPoint = s.ReorderPoint,
                        ReorderQty = reorderQty
                    };
                })
                .OrderBy(s => s.OnHand)
                .ToList();

            RecentTransactions = await _db.Set<ItemTransaction>()
                .Include(t => t.Item)
                .OrderByDescending(t => t.TransactionDate)
                .Take(50)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostTransactionAsync(
            int itemId, int typeLookupValueId, decimal quantity, decimal unitCost,
            string? warehouse, string? bin, string? referenceNumber, string? notes, int? companyId = null)
        {
            var item = await _db.Items
                .Where(i => i.Id == itemId)
                .FirstOrDefaultAsync();
            if (item == null)
            {
                TempData["Error"] = "Item not found.";
                return RedirectToPage();
            }

            var effectiveCompanyId = _tenantContext.CompanyId ?? await _stockingService.GetDefaultCompanyIdAsync();
            var stocking = await _stockingService.GetStockingAsync(itemId, effectiveCompanyId);
            var defaultWarehouse = stocking.DefaultWarehouse ?? item.Warehouse;
            var defaultBin = stocking.DefaultBin ?? item.Bin;

            var resolvedType = TransactionType.Receipt;
            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (int.TryParse(typeLv.Code, out var enumVal))
                    resolvedType = (TransactionType)enumVal;
            }

            var txnNumber = await GenerateTransactionNumberAsync();

            var transaction = new ItemTransaction
            {
                TransactionNumber = txnNumber,
                ItemId = itemId,
                Type = resolvedType,
                TypeLookupValueId = resolvedTypeLvId,
                Quantity = quantity,
                UnitCost = unitCost,
                ToBin = bin,
                ReferenceNumber = referenceNumber,
                Notes = notes,
                TransactedBy = User.Identity?.Name ?? "system",
                TransactionDate = DateTime.UtcNow
            };

            _db.Set<ItemTransaction>().Add(transaction);

            var inventory = await _db.Set<ItemInventory>()
                .FirstOrDefaultAsync(i => i.ItemId == itemId && i.Warehouse == (warehouse ?? defaultWarehouse) && i.Bin == (bin ?? defaultBin));

            if (inventory == null)
            {
                inventory = new ItemInventory
                {
                    ItemId = itemId,
                    Warehouse = warehouse ?? defaultWarehouse,
                    Bin = bin ?? defaultBin,
                    QuantityOnHand = 0
                };
                _db.Set<ItemInventory>().Add(inventory);
            }

            switch (resolvedType)
            {
                case TransactionType.Receipt:
                case TransactionType.Return:
                    inventory.QuantityOnHand += quantity;
                    inventory.LastReceiptDate = DateTime.UtcNow;
                    break;
                case TransactionType.Issue:
                case TransactionType.Scrap:
                    inventory.QuantityOnHand -= quantity;
                    inventory.LastIssueDate = DateTime.UtcNow;
                    break;
                case TransactionType.Adjust:
                    inventory.QuantityOnHand += quantity;
                    break;
            }

            inventory.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Transaction {txnNumber} recorded successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostCycleCountAsync(
            int itemId, decimal countedQuantity, string? warehouse, string? bin, string? notes, int? companyId = null)
        {
            var item = await _db.Items
                .Where(i => i.Id == itemId)
                .FirstOrDefaultAsync();
            if (item == null)
            {
                TempData["Error"] = "Item not found.";
                return RedirectToPage();
            }

            var effectiveCompanyId = _tenantContext.CompanyId ?? await _stockingService.GetDefaultCompanyIdAsync();
            var stocking = await _stockingService.GetStockingAsync(itemId, effectiveCompanyId);
            var defaultWarehouse = stocking.DefaultWarehouse ?? item.Warehouse;
            var defaultBin = stocking.DefaultBin ?? item.Bin;

            var inventory = await _db.Set<ItemInventory>()
                .FirstOrDefaultAsync(i => i.ItemId == itemId && i.Warehouse == (warehouse ?? defaultWarehouse) && i.Bin == (bin ?? defaultBin));

            decimal adjustment = 0;
            if (inventory != null)
            {
                adjustment = countedQuantity - inventory.QuantityOnHand;
                inventory.QuantityOnHand = countedQuantity;
                inventory.LastCountDate = DateTime.UtcNow;
                inventory.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                adjustment = countedQuantity;
                inventory = new ItemInventory
                {
                    ItemId = itemId,
                    Warehouse = warehouse ?? defaultWarehouse,
                    Bin = bin ?? defaultBin,
                    QuantityOnHand = countedQuantity,
                    LastCountDate = DateTime.UtcNow
                };
                _db.Set<ItemInventory>().Add(inventory);
            }

            var txnNumber = await GenerateTransactionNumberAsync();
            var transaction = new ItemTransaction
            {
                TransactionNumber = txnNumber,
                ItemId = itemId,
                Type = TransactionType.CycleCount,
                Quantity = adjustment,
                UnitCost = item.StandardCost,
                ToBin = bin,
                Notes = notes ?? $"Cycle count adjustment. Counted: {countedQuantity}",
                TransactedBy = User.Identity?.Name ?? "system",
                TransactionDate = DateTime.UtcNow
            };

            _db.Set<ItemTransaction>().Add(transaction);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Cycle count recorded. Adjustment: {adjustment:+0.##;-0.##;0}";
            return RedirectToPage();
        }

        private async Task<string> GenerateTransactionNumberAsync()
        {
            var year = DateTime.UtcNow.Year % 100;
            var lastTxn = await _db.Set<ItemTransaction>()
                .Where(t => t.TransactionNumber.StartsWith($"TXN-{year:D2}-"))
                .OrderByDescending(t => t.TransactionNumber)
                .FirstOrDefaultAsync();

            int nextSeq = 1;
            if (lastTxn != null && lastTxn.TransactionNumber.Length >= 12)
            {
                var seqStr = lastTxn.TransactionNumber.Substring(7);
                if (int.TryParse(seqStr, out int seq))
                {
                    nextSeq = seq + 1;
                }
            }

            return $"TXN-{year:D2}-{nextSeq:D5}";
        }

        public class StockLevelView
        {
            public int ItemId { get; set; }
            public string PartNumber { get; set; } = "";
            public string Description { get; set; } = "";
            public string? Warehouse { get; set; }
            public string? Bin { get; set; }
            public decimal QuantityOnHand { get; set; }
            public decimal QuantityReserved { get; set; }
            public decimal QuantityAvailable { get; set; }
            public decimal QuantityOnOrder { get; set; }
            public decimal ReorderPoint { get; set; }
        }

        public class LowStockItem
        {
            public int ItemId { get; set; }
            public string PartNumber { get; set; } = "";
            public string Description { get; set; } = "";
            public decimal OnHand { get; set; }
            public decimal ReorderPoint { get; set; }
            public decimal ReorderQty { get; set; }
        }
    }
}
