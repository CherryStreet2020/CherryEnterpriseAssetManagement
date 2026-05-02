using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class ItemStockingDto
    {
        public int ItemId { get; set; }
        public int? CompanyId { get; set; }
        
        public bool IsStocked { get; set; } = true;
        public bool IsPurchasable { get; set; } = true;
        public bool IsCriticalSpare { get; set; } = false;
        
        public decimal MinQuantity { get; set; } = 0;
        public decimal MaxQuantity { get; set; } = 0;
        public decimal ReorderPoint { get; set; } = 0;
        public decimal ReorderQuantity { get; set; } = 0;
        public decimal SafetyStock { get; set; } = 0;
        
        public int LeadTimeDays { get; set; } = 0;
        public int? PreferredVendorId { get; set; }
        
        public ReorderMethod ReorderMethod { get; set; } = ReorderMethod.ReorderPoint;
        public bool AutoReorderEnabled { get; set; } = false;
        public ABCClassification ABCClass { get; set; } = ABCClassification.Unclassified;
        
        public string? DefaultWarehouse { get; set; }
        public string? DefaultAisle { get; set; }
        public string? DefaultRack { get; set; }
        public string? DefaultShelf { get; set; }
        public string? DefaultBin { get; set; }
    }

    public interface IItemStockingService
    {
        Task<bool> IsMultiCompanyModeAsync();
        Task<ItemStockingDto> GetStockingAsync(int itemId, int? companyId = null);
        Task<Dictionary<int, ItemStockingDto>> GetStockingForItemsAsync(IEnumerable<int> itemIds, int? companyId = null);
        Task UpsertStockingAsync(int itemId, int? companyId, ItemStockingDto dto, string? userName = null);
        Task<int> GetDefaultCompanyIdAsync();
    }

    public class ItemStockingService : IItemStockingService
    {
        private readonly AppDbContext _context;
        private bool? _isMultiCompanyMode;
        private int? _defaultCompanyId;

        public ItemStockingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsMultiCompanyModeAsync()
        {
            if (_isMultiCompanyMode.HasValue)
                return _isMultiCompanyMode.Value;

            var companies = await _context.Companies.Where(c => c.IsActive).ToListAsync();
            _isMultiCompanyMode = companies.Count > 1 || 
                companies.Any(c => c.CompanyStructure == CompanyStructure.MultiCompany);
            
            return _isMultiCompanyMode.Value;
        }

        public async Task<int> GetDefaultCompanyIdAsync()
        {
            if (_defaultCompanyId.HasValue)
                return _defaultCompanyId.Value;

            var company = await _context.Companies
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .FirstOrDefaultAsync();

            _defaultCompanyId = company?.Id ?? 0;
            return _defaultCompanyId.Value;
        }

        public async Task<ItemStockingDto> GetStockingAsync(int itemId, int? companyId = null)
        {
            var isMultiCompany = await IsMultiCompanyModeAsync();
            
            if (!isMultiCompany)
            {
                return await GetStockingFromItemAsync(itemId);
            }

            var targetCompanyId = companyId ?? await GetDefaultCompanyIdAsync();
            
            var stocking = await _context.ItemCompanyStockings
                .FirstOrDefaultAsync(s => s.ItemId == itemId && s.CompanyId == targetCompanyId);

            if (stocking != null)
            {
                return MapFromCompanyStocking(stocking);
            }

            return await GetStockingFromItemAsync(itemId);
        }

        public async Task<Dictionary<int, ItemStockingDto>> GetStockingForItemsAsync(IEnumerable<int> itemIds, int? companyId = null)
        {
            var itemIdList = itemIds.ToList();
            if (!itemIdList.Any())
                return new Dictionary<int, ItemStockingDto>();

            var isMultiCompany = await IsMultiCompanyModeAsync();
            
            if (!isMultiCompany)
            {
                return await GetStockingFromItemsAsync(itemIdList);
            }

            var targetCompanyId = companyId ?? await GetDefaultCompanyIdAsync();
            
            var stockings = await _context.ItemCompanyStockings
                .Where(s => itemIdList.Contains(s.ItemId) && s.CompanyId == targetCompanyId)
                .ToDictionaryAsync(s => s.ItemId, s => MapFromCompanyStocking(s));

            var missingItemIds = itemIdList.Where(id => !stockings.ContainsKey(id)).ToList();
            if (missingItemIds.Any())
            {
                var itemDefaults = await GetStockingFromItemsAsync(missingItemIds);
                foreach (var kvp in itemDefaults)
                {
                    stockings[kvp.Key] = kvp.Value;
                }
            }

            return stockings;
        }

        public async Task UpsertStockingAsync(int itemId, int? companyId, ItemStockingDto dto, string? userName = null)
        {
            var isMultiCompany = await IsMultiCompanyModeAsync();
            
            var item = await _context.Items.Where(i => i.Id == itemId).FirstOrDefaultAsync();
            if (item == null)
                throw new InvalidOperationException($"Item {itemId} not found");

            item.IsStocked = dto.IsStocked;
            item.IsPurchasable = dto.IsPurchasable;
            item.IsCriticalSpare = dto.IsCriticalSpare;
            item.MinQuantity = dto.MinQuantity;
            item.MaxQuantity = dto.MaxQuantity;
            item.ReorderPoint = dto.ReorderPoint;
            item.ReorderQuantity = dto.ReorderQuantity;
            item.SafetyStock = dto.SafetyStock;
            item.LeadTimeDays = dto.LeadTimeDays;
            item.PrimaryVendorId = dto.PreferredVendorId;
            item.ReorderMethod = dto.ReorderMethod;
            item.AutoReorderEnabled = dto.AutoReorderEnabled;
            item.ABCClass = dto.ABCClass;
            item.Warehouse = dto.DefaultWarehouse;
            item.Aisle = dto.DefaultAisle;
            item.Rack = dto.DefaultRack;
            item.Shelf = dto.DefaultShelf;
            item.Bin = dto.DefaultBin;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = userName;

            if (isMultiCompany && companyId.HasValue)
            {
                var stocking = await _context.ItemCompanyStockings
                    .FirstOrDefaultAsync(s => s.ItemId == itemId && s.CompanyId == companyId);

                if (stocking == null)
                {
                    stocking = new ItemCompanyStocking
                    {
                        ItemId = itemId,
                        CompanyId = companyId.Value,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = userName
                    };
                    _context.ItemCompanyStockings.Add(stocking);
                }

                stocking.IsStocked = dto.IsStocked;
                stocking.IsPurchasable = dto.IsPurchasable;
                stocking.IsCriticalSpare = dto.IsCriticalSpare;
                stocking.MinQuantity = dto.MinQuantity;
                stocking.MaxQuantity = dto.MaxQuantity;
                stocking.ReorderPoint = dto.ReorderPoint;
                stocking.ReorderQuantity = dto.ReorderQuantity;
                stocking.SafetyStock = dto.SafetyStock;
                stocking.LeadTimeDays = dto.LeadTimeDays;
                stocking.PreferredVendorId = dto.PreferredVendorId;
                stocking.ReorderMethod = dto.ReorderMethod;
                stocking.AutoReorderEnabled = dto.AutoReorderEnabled;
                stocking.ABCClass = dto.ABCClass;
                stocking.DefaultWarehouse = dto.DefaultWarehouse;
                stocking.DefaultAisle = dto.DefaultAisle;
                stocking.DefaultRack = dto.DefaultRack;
                stocking.DefaultShelf = dto.DefaultShelf;
                stocking.DefaultBin = dto.DefaultBin;
                stocking.UpdatedAt = DateTime.UtcNow;
                stocking.UpdatedBy = userName;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<ItemStockingDto> GetStockingFromItemAsync(int itemId)
        {
            var item = await _context.Items.Where(i => i.Id == itemId).FirstOrDefaultAsync();
            if (item == null)
                return new ItemStockingDto { ItemId = itemId };

            return MapFromItem(item);
        }

        private async Task<Dictionary<int, ItemStockingDto>> GetStockingFromItemsAsync(IEnumerable<int> itemIds)
        {
            var items = await _context.Items
                .Where(i => itemIds.Contains(i.Id))
                .ToListAsync();

            return items.ToDictionary(i => i.Id, i => MapFromItem(i));
        }

        private ItemStockingDto MapFromItem(Item item)
        {
            return new ItemStockingDto
            {
                ItemId = item.Id,
                CompanyId = item.CompanyId,
                IsStocked = item.IsStocked,
                IsPurchasable = item.IsPurchasable,
                IsCriticalSpare = item.IsCriticalSpare,
                MinQuantity = item.MinQuantity,
                MaxQuantity = item.MaxQuantity,
                ReorderPoint = item.ReorderPoint,
                ReorderQuantity = item.ReorderQuantity,
                SafetyStock = item.SafetyStock,
                LeadTimeDays = item.LeadTimeDays,
                PreferredVendorId = item.PrimaryVendorId,
                ReorderMethod = item.ReorderMethod,
                AutoReorderEnabled = item.AutoReorderEnabled,
                ABCClass = item.ABCClass,
                DefaultWarehouse = item.Warehouse,
                DefaultAisle = item.Aisle,
                DefaultRack = item.Rack,
                DefaultShelf = item.Shelf,
                DefaultBin = item.Bin
            };
        }

        private ItemStockingDto MapFromCompanyStocking(ItemCompanyStocking stocking)
        {
            return new ItemStockingDto
            {
                ItemId = stocking.ItemId,
                CompanyId = stocking.CompanyId,
                IsStocked = stocking.IsStocked,
                IsPurchasable = stocking.IsPurchasable,
                IsCriticalSpare = stocking.IsCriticalSpare,
                MinQuantity = stocking.MinQuantity,
                MaxQuantity = stocking.MaxQuantity,
                ReorderPoint = stocking.ReorderPoint,
                ReorderQuantity = stocking.ReorderQuantity,
                SafetyStock = stocking.SafetyStock,
                LeadTimeDays = stocking.LeadTimeDays,
                PreferredVendorId = stocking.PreferredVendorId,
                ReorderMethod = stocking.ReorderMethod,
                AutoReorderEnabled = stocking.AutoReorderEnabled,
                ABCClass = stocking.ABCClass,
                DefaultWarehouse = stocking.DefaultWarehouse,
                DefaultAisle = stocking.DefaultAisle,
                DefaultRack = stocking.DefaultRack,
                DefaultShelf = stocking.DefaultShelf,
                DefaultBin = stocking.DefaultBin
            };
        }
    }
}
