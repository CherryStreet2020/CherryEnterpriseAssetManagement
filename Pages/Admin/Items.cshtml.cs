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
    public class ItemsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IItemStockingService _stockingService;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public ItemsModel(AppDbContext db, IItemStockingService stockingService, IModuleGuardService moduleGuard, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _stockingService = stockingService;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<Item> Items { get; set; } = new();
        public List<ItemCategory> Categories { get; set; } = new();
        public List<Vendor> Vendors { get; set; } = new();
        public List<Manufacturer> Manufacturers { get; set; } = new();
        public List<Company> Companies { get; set; } = new();
        public int CurrentCompanyId { get; set; }
        public bool IsMultiCompanyMode { get; set; }
        public Dictionary<int, ItemStockingDto> StockingByItem { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public List<SelectListItem> ItemTypeOptions { get; set; } = new();
        public List<SelectListItem> ItemStatusOptions { get; set; } = new();
        public List<SelectListItem> UomOptions { get; set; } = new();
        public List<SelectListItem> CostMethodOptions { get; set; } = new();
        public List<SelectListItem> TrackingTypeOptions { get; set; } = new();
        public List<SelectListItem> BarcodeTypeOptions { get; set; } = new();
        public List<SelectListItem> AbcClassOptions { get; set; } = new();
        public List<SelectListItem> StockingMethodOptions { get; set; } = new();

        public IActionResult OnGet()
        {
            return Redirect("/Materials/Items");
        }

        public async Task<IActionResult> OnPostCreateAsync(
            int companyId,
            string partNumber, string description, string? extendedDescription,
            string? revision, bool requireRevisionControl,
            int typeLookupValueId, int statusLookupValueId, int? categoryId, int uom, string stockUOM,
            int costMethodLookupValueId, decimal standardCost, decimal? listPrice,
            int trackingTypeLookupValueId, decimal minQuantity, decimal maxQuantity,
            decimal reorderPoint, decimal reorderQuantity, decimal safetyStock,
            int leadTimeDays, string? warehouse, string? aisle, string? rack,
            string? shelf, string? bin, int? primaryVendorId, int? manufacturerId,
            string? vendorPartNumber, string? manufacturerPartNumber, bool isStocked, bool isPurchasable,
            bool isCriticalSpare, bool isTaxable, bool isHazmat, string? notes,
            int barcodeType, string? barcode, int abcClass, string? commodityCode,
            int reorderMethod, decimal? eoq, decimal annualUsage, decimal averageDailyUsage,
            bool autoReorderEnabled, int? warrantyMonths, string? supersedesPartNumber,
            string? alternatePartNumbers, string? imageUrl, string? specUrl)
        {
            if (await _db.Items.AnyAsync(i => i.PartNumber == partNumber))
            {
                TempData["Error"] = "An item with this part number already exists.";
                return RedirectToPage();
            }

            if (requireRevisionControl && string.IsNullOrWhiteSpace(revision))
            {
                TempData["Error"] = "Revision is required when Revision Control is enabled.";
                return RedirectToPage();
            }

            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var resolvedType = ItemType.Part;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (Enum.TryParse<ItemType>(typeLv.Code, true, out var parsed))
                    resolvedType = parsed;
            }

            int? resolvedStatusLvId = statusLookupValueId > 0 ? statusLookupValueId : (int?)null;
            var resolvedStatus = ItemStatus.Active;
            var statusLv = await _lookupService.GetValueByIdAsync(null, null, statusLookupValueId);
            if (statusLv != null)
            {
                resolvedStatusLvId = statusLv.Id;
                if (Enum.TryParse<ItemStatus>(statusLv.Code, true, out var parsed))
                    resolvedStatus = parsed;
            }

            int? resolvedCostMethodLvId = costMethodLookupValueId > 0 ? costMethodLookupValueId : (int?)null;
            var resolvedCostMethod = CostMethod.Standard;
            var costMethodLv = await _lookupService.GetValueByIdAsync(null, null, costMethodLookupValueId);
            if (costMethodLv != null)
            {
                resolvedCostMethodLvId = costMethodLv.Id;
                if (Enum.TryParse<CostMethod>(costMethodLv.Code, true, out var parsed))
                    resolvedCostMethod = parsed;
            }

            int? resolvedTrackingTypeLvId = trackingTypeLookupValueId > 0 ? trackingTypeLookupValueId : (int?)null;
            var resolvedTrackingType = TrackingType.None;
            var trackingTypeLv = await _lookupService.GetValueByIdAsync(null, null, trackingTypeLookupValueId);
            if (trackingTypeLv != null)
            {
                resolvedTrackingTypeLvId = trackingTypeLv.Id;
                if (Enum.TryParse<TrackingType>(trackingTypeLv.Code, true, out var parsed))
                    resolvedTrackingType = parsed;
            }

            var item = new Item
            {
                PartNumber = partNumber,
                Description = description,
                ExtendedDescription = extendedDescription,
                Revision = revision,
                RequireRevisionControl = requireRevisionControl,
                Type = resolvedType,
                TypeLookupValueId = resolvedTypeLvId,
                Status = resolvedStatus,
                StatusLookupValueId = resolvedStatusLvId,
                CategoryId = categoryId,
                UOM = (UnitOfMeasure)uom,
                StockUOM = stockUOM ?? "EA",
                CostMethod = resolvedCostMethod,
                CostMethodLookupValueId = resolvedCostMethodLvId,
                StandardCost = standardCost,
                ListPrice = listPrice,
                TrackingType = resolvedTrackingType,
                TrackingTypeLookupValueId = resolvedTrackingTypeLvId,
                PrimaryVendorId = primaryVendorId,
                ManufacturerId = manufacturerId,
                VendorPartNumber = vendorPartNumber,
                ManufacturerPartNumber = manufacturerPartNumber,
                IsTaxable = isTaxable,
                IsHazmat = isHazmat,
                Notes = notes,
                BarcodeType = (BarcodeType)barcodeType,
                Barcode = barcode ?? partNumber,
                CommodityCode = commodityCode,
                EOQ = eoq,
                AnnualUsage = annualUsage,
                AverageDailyUsage = averageDailyUsage,
                WarrantyMonths = warrantyMonths,
                SupersedesPartNumber = supersedesPartNumber,
                AlternatePartNumbers = alternatePartNumbers,
                ImageUrl = imageUrl,
                SpecUrl = specUrl,
                IsActive = true,
                Source = ItemMasterSource.Internal,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "system"
            };

            _db.Items.Add(item);
            await _db.SaveChangesAsync();

            var targetCompanyId = companyId > 0 ? companyId : await _stockingService.GetDefaultCompanyIdAsync();
            var stockingDto = new ItemStockingDto
            {
                ItemId = item.Id,
                CompanyId = targetCompanyId,
                IsStocked = isStocked,
                IsPurchasable = isPurchasable,
                IsCriticalSpare = isCriticalSpare,
                MinQuantity = minQuantity,
                MaxQuantity = maxQuantity,
                ReorderPoint = reorderPoint,
                ReorderQuantity = reorderQuantity,
                SafetyStock = safetyStock,
                LeadTimeDays = leadTimeDays,
                PreferredVendorId = primaryVendorId,
                ReorderMethod = (ReorderMethod)reorderMethod,
                AutoReorderEnabled = autoReorderEnabled,
                ABCClass = (ABCClassification)abcClass,
                DefaultWarehouse = warehouse,
                DefaultAisle = aisle,
                DefaultRack = rack,
                DefaultShelf = shelf,
                DefaultBin = bin
            };

            await _stockingService.UpsertStockingAsync(item.Id, targetCompanyId, stockingDto, User.Identity?.Name ?? "system");

            TempData["Success"] = $"Item {partNumber} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            int id, int companyId, string partNumber, string description, string? extendedDescription,
            string? revision, bool requireRevisionControl,
            int typeLookupValueId, int statusLookupValueId, int? categoryId, int uom, string stockUOM,
            int costMethodLookupValueId, decimal standardCost, decimal? listPrice,
            int trackingTypeLookupValueId, decimal minQuantity, decimal maxQuantity,
            decimal reorderPoint, decimal reorderQuantity, decimal safetyStock,
            int leadTimeDays, string? warehouse, string? aisle, string? rack,
            string? shelf, string? bin, int? primaryVendorId, int? manufacturerId,
            string? vendorPartNumber, string? manufacturerPartNumber, bool isStocked, bool isPurchasable,
            bool isCriticalSpare, bool isTaxable, bool isHazmat, string? notes, bool isActive,
            int barcodeType, string? barcode, int abcClass, string? commodityCode,
            int reorderMethod, decimal? eoq, decimal annualUsage, decimal averageDailyUsage,
            bool autoReorderEnabled, int? warrantyMonths, string? supersedesPartNumber,
            string? alternatePartNumbers, string? imageUrl, string? specUrl)
        {
            var item = await _db.Items
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();
            if (item == null)
            {
                TempData["Error"] = "Item not found.";
                return RedirectToPage();
            }

            if (item.Source == ItemMasterSource.ExternalERP)
            {
                TempData["Error"] = "Cannot modify externally managed items.";
                return RedirectToPage();
            }

            if (requireRevisionControl && string.IsNullOrWhiteSpace(revision))
            {
                TempData["Error"] = "Revision is required when Revision Control is enabled.";
                return RedirectToPage();
            }

            int? resolvedTypeLvId = typeLookupValueId > 0 ? typeLookupValueId : (int?)null;
            var resolvedType = ItemType.Part;
            var typeLv = await _lookupService.GetValueByIdAsync(null, null, typeLookupValueId);
            if (typeLv != null)
            {
                resolvedTypeLvId = typeLv.Id;
                if (Enum.TryParse<ItemType>(typeLv.Code, true, out var parsed))
                    resolvedType = parsed;
            }

            int? resolvedStatusLvId = statusLookupValueId > 0 ? statusLookupValueId : (int?)null;
            var resolvedStatus = ItemStatus.Active;
            var statusLv = await _lookupService.GetValueByIdAsync(null, null, statusLookupValueId);
            if (statusLv != null)
            {
                resolvedStatusLvId = statusLv.Id;
                if (Enum.TryParse<ItemStatus>(statusLv.Code, true, out var parsed))
                    resolvedStatus = parsed;
            }

            int? resolvedCostMethodLvId = costMethodLookupValueId > 0 ? costMethodLookupValueId : (int?)null;
            var resolvedCostMethod = CostMethod.Standard;
            var costMethodLv = await _lookupService.GetValueByIdAsync(null, null, costMethodLookupValueId);
            if (costMethodLv != null)
            {
                resolvedCostMethodLvId = costMethodLv.Id;
                if (Enum.TryParse<CostMethod>(costMethodLv.Code, true, out var parsed))
                    resolvedCostMethod = parsed;
            }

            int? resolvedTrackingTypeLvId = trackingTypeLookupValueId > 0 ? trackingTypeLookupValueId : (int?)null;
            var resolvedTrackingType = TrackingType.None;
            var trackingTypeLv = await _lookupService.GetValueByIdAsync(null, null, trackingTypeLookupValueId);
            if (trackingTypeLv != null)
            {
                resolvedTrackingTypeLvId = trackingTypeLv.Id;
                if (Enum.TryParse<TrackingType>(trackingTypeLv.Code, true, out var parsed))
                    resolvedTrackingType = parsed;
            }

            item.PartNumber = partNumber;
            item.Revision = revision;
            item.RequireRevisionControl = requireRevisionControl;
            item.Description = description;
            item.ExtendedDescription = extendedDescription;
            item.Type = resolvedType;
            item.TypeLookupValueId = resolvedTypeLvId;
            item.Status = resolvedStatus;
            item.StatusLookupValueId = resolvedStatusLvId;
            item.CategoryId = categoryId;
            item.UOM = (UnitOfMeasure)uom;
            item.StockUOM = stockUOM ?? "EA";
            item.CostMethod = resolvedCostMethod;
            item.CostMethodLookupValueId = resolvedCostMethodLvId;
            item.StandardCost = standardCost;
            item.ListPrice = listPrice;
            item.TrackingType = resolvedTrackingType;
            item.TrackingTypeLookupValueId = resolvedTrackingTypeLvId;
            item.PrimaryVendorId = primaryVendorId;
            item.ManufacturerId = manufacturerId;
            item.VendorPartNumber = vendorPartNumber;
            item.ManufacturerPartNumber = manufacturerPartNumber;
            item.IsTaxable = isTaxable;
            item.IsHazmat = isHazmat;
            item.Notes = notes;
            item.IsActive = isActive;
            item.BarcodeType = (BarcodeType)barcodeType;
            item.Barcode = barcode;
            item.CommodityCode = commodityCode;
            item.EOQ = eoq;
            item.AnnualUsage = annualUsage;
            item.AverageDailyUsage = averageDailyUsage;
            item.WarrantyMonths = warrantyMonths;
            item.SupersedesPartNumber = supersedesPartNumber;
            item.AlternatePartNumbers = alternatePartNumbers;
            item.ImageUrl = imageUrl;
            item.SpecUrl = specUrl;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = User.Identity?.Name ?? "system";

            await _db.SaveChangesAsync();

            var targetCompanyId = companyId > 0 ? companyId : await _stockingService.GetDefaultCompanyIdAsync();
            var stockingDto = new ItemStockingDto
            {
                ItemId = id,
                CompanyId = targetCompanyId,
                IsStocked = isStocked,
                IsPurchasable = isPurchasable,
                IsCriticalSpare = isCriticalSpare,
                MinQuantity = minQuantity,
                MaxQuantity = maxQuantity,
                ReorderPoint = reorderPoint,
                ReorderQuantity = reorderQuantity,
                SafetyStock = safetyStock,
                LeadTimeDays = leadTimeDays,
                PreferredVendorId = primaryVendorId,
                ReorderMethod = (ReorderMethod)reorderMethod,
                AutoReorderEnabled = autoReorderEnabled,
                ABCClass = (ABCClassification)abcClass,
                DefaultWarehouse = warehouse,
                DefaultAisle = aisle,
                DefaultRack = rack,
                DefaultShelf = shelf,
                DefaultBin = bin
            };

            await _stockingService.UpsertStockingAsync(id, targetCompanyId, stockingDto, User.Identity?.Name ?? "system");

            TempData["Success"] = $"Item {partNumber} updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var item = await _db.Items
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();
            if (item == null)
            {
                TempData["Error"] = "Item not found.";
                return RedirectToPage();
            }

            if (item.Source == ItemMasterSource.ExternalERP)
            {
                TempData["Error"] = "Cannot delete externally managed items.";
                return RedirectToPage();
            }

            var stockings = await _db.ItemCompanyStockings.Where(s => s.ItemId == id).ToListAsync();
            _db.ItemCompanyStockings.RemoveRange(stockings);

            var partNumber = item.PartNumber;
            _db.Items.Remove(item);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Item {partNumber} deleted successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDuplicateAsync(int id)
        {
            var source = await _db.Items
                .Where(i => i.Id == id)
                .FirstOrDefaultAsync();
            if (source == null)
            {
                TempData["Error"] = "Item not found.";
                return RedirectToPage();
            }

            var newPartNumber = $"{source.PartNumber}-COPY";
            var counter = 1;
            while (await _db.Items.AnyAsync(i => i.PartNumber == newPartNumber))
            {
                newPartNumber = $"{source.PartNumber}-COPY{counter++}";
            }

            var newItem = new Item
            {
                PartNumber = newPartNumber,
                Description = $"{source.Description} (Copy)",
                ExtendedDescription = source.ExtendedDescription,
                Revision = source.Revision,
                RequireRevisionControl = source.RequireRevisionControl,
                Type = source.Type,
                TypeLookupValueId = source.TypeLookupValueId,
                Status = ItemStatus.Active,
                StatusLookupValueId = null,
                CategoryId = source.CategoryId,
                UOM = source.UOM,
                StockUOM = source.StockUOM,
                CostMethod = source.CostMethod,
                CostMethodLookupValueId = source.CostMethodLookupValueId,
                StandardCost = source.StandardCost,
                ListPrice = source.ListPrice,
                TrackingType = source.TrackingType,
                TrackingTypeLookupValueId = source.TrackingTypeLookupValueId,
                PrimaryVendorId = source.PrimaryVendorId,
                ManufacturerId = source.ManufacturerId,
                VendorPartNumber = source.VendorPartNumber,
                ManufacturerPartNumber = source.ManufacturerPartNumber,
                IsTaxable = source.IsTaxable,
                IsHazmat = source.IsHazmat,
                Notes = $"Duplicated from {source.PartNumber}",
                BarcodeType = source.BarcodeType,
                ABCClass = source.ABCClass,
                CommodityCode = source.CommodityCode,
                ReorderMethod = source.ReorderMethod,
                EOQ = source.EOQ,
                AnnualUsage = source.AnnualUsage,
                AverageDailyUsage = source.AverageDailyUsage,
                AutoReorderEnabled = source.AutoReorderEnabled,
                WarrantyMonths = source.WarrantyMonths,
                MinQuantity = source.MinQuantity,
                MaxQuantity = source.MaxQuantity,
                ReorderPoint = source.ReorderPoint,
                ReorderQuantity = source.ReorderQuantity,
                SafetyStock = source.SafetyStock,
                LeadTimeDays = source.LeadTimeDays,
                IsActive = true
            };

            _db.Items.Add(newItem);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Item duplicated as {newPartNumber}.";
            return RedirectToPage();
        }
    }
}
