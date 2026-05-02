using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant")]
    public class StockLevelsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IItemStockingService _stockingService;

        public StockLevelsModel(AppDbContext context, IItemStockingService stockingService)
        {
            _context = context;
            _stockingService = stockingService;
        }

        public int InStockCount { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public decimal TotalValue { get; set; }
        public List<StockItem> LowStockItems { get; set; } = new();
        public List<StockItem> AllItems { get; set; } = new();
        public bool IsMultiCompanyMode { get; set; }

        public async Task OnGetAsync()
        {
            IsMultiCompanyMode = await _stockingService.IsMultiCompanyModeAsync();
            var currentCompanyId = 0;

            var items = await _context.Items
                .Where(i => i.Status == ItemStatus.Active)
                .Include(i => i.Category)
                .ToListAsync();

            var itemIds = items.Select(i => i.Id).ToList();
            var stockingData = await _stockingService.GetStockingForItemsAsync(itemIds, currentCompanyId);

            var inventories = await _context.ItemInventories2
                .GroupBy(ii => ii.ItemId)
                .Select(g => new { ItemId = g.Key, OnHand = g.Sum(x => x.QuantityOnHand) })
                .ToDictionaryAsync(x => x.ItemId, x => x.OnHand);

            AllItems = items.Select(i => {
                var stocking = stockingData.TryGetValue(i.Id, out var s) ? s : null;
                var reorderPoint = stocking?.ReorderPoint ?? i.ReorderPoint;
                return new StockItem
                {
                    ItemId = i.Id,
                    PartNumber = i.PartNumber,
                    MfrPartNumber = i.ManufacturerPartNumber,
                    Description = i.Description ?? "",
                    Category = i.Category?.Name ?? "-",
                    OnHand = inventories.ContainsKey(i.Id) ? inventories[i.Id] : 0,
                    ReorderPoint = reorderPoint,
                    UnitCost = i.StandardCost,
                    TotalValue = (inventories.ContainsKey(i.Id) ? inventories[i.Id] : 0) * i.StandardCost
                };
            }).OrderBy(i => i.PartNumber).ToList();

            InStockCount = AllItems.Count(i => i.OnHand > i.ReorderPoint);
            LowStockCount = AllItems.Count(i => i.OnHand > 0 && i.OnHand <= i.ReorderPoint);
            OutOfStockCount = AllItems.Count(i => i.OnHand <= 0);
            TotalValue = AllItems.Sum(i => i.TotalValue);

            LowStockItems = AllItems
                .Where(i => i.OnHand <= i.ReorderPoint && i.ReorderPoint > 0)
                .OrderBy(i => i.OnHand)
                .Take(20)
                .Select(i => new StockItem
                {
                    ItemId = i.ItemId,
                    PartNumber = i.PartNumber,
                    Description = i.Description,
                    OnHand = i.OnHand,
                    ReorderPoint = i.ReorderPoint,
                    SuggestedOrder = Math.Max(i.ReorderPoint * 2 - i.OnHand, i.ReorderPoint)
                })
                .ToList();
        }

        public class StockItem
        {
            public int ItemId { get; set; }
            public string PartNumber { get; set; } = "";
            public string? MfrPartNumber { get; set; }
            public string Description { get; set; } = "";
            public string Category { get; set; } = "";
            public decimal OnHand { get; set; }
            public decimal ReorderPoint { get; set; }
            public decimal UnitCost { get; set; }
            public decimal TotalValue { get; set; }
            public decimal SuggestedOrder { get; set; }
        }
    }
}
