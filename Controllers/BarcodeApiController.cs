using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Controllers
{
    [ApiController]
    [Route("api/barcode")]
    public class BarcodeApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IBarcodeService _barcodeService;
        private readonly IItemStockingService _stockingService;

        public BarcodeApiController(AppDbContext db, IBarcodeService barcodeService, IItemStockingService stockingService)
        {
            _db = db;
            _barcodeService = barcodeService;
            _stockingService = stockingService;
        }

        [HttpGet("generate/{itemId}")]
        public async Task<IActionResult> GenerateBarcode(int itemId, [FromQuery] int width = 300, [FromQuery] int height = 100)
        {
            var item = await _db.Items.FindAsync(itemId);
            if (item == null)
                return NotFound(new { error = "Item not found" });

            var barcodeValue = !string.IsNullOrEmpty(item.Barcode) 
                ? item.Barcode 
                : item.PartNumber;

            var barcodeType = item.BarcodeType;
            var imageBytes = _barcodeService.GenerateBarcode(barcodeValue, barcodeType, width, height);
            
            return File(imageBytes, "image/png");
        }

        [HttpGet("label/{itemId}")]
        public async Task<IActionResult> GenerateLabel(int itemId, [FromQuery] int width = 400, [FromQuery] int height = 200)
        {
            var item = await _db.Items.FindAsync(itemId);
            if (item == null)
                return NotFound(new { error = "Item not found" });

            var barcodeValue = !string.IsNullOrEmpty(item.Barcode) 
                ? item.Barcode 
                : item.PartNumber;

            var barcodeType = item.BarcodeType;
            var imageBytes = _barcodeService.GenerateLabel(
                barcodeValue, 
                barcodeType, 
                item.PartNumber, 
                item.Description ?? "",
                width, 
                height);
            
            return File(imageBytes, "image/png");
        }

        [HttpPost("scan")]
        public async Task<IActionResult> ScanBarcode([FromBody] ScanRequest request)
        {
            if (string.IsNullOrEmpty(request.ImageBase64))
                return BadRequest(new { error = "No image provided" });

            var decodedValue = _barcodeService.DecodeBarcodeFromBase64(request.ImageBase64);
            
            if (string.IsNullOrEmpty(decodedValue))
                return Ok(new ScanResponse { Success = false, Message = "No barcode detected in image" });

            var item = await _db.Items
                .FirstOrDefaultAsync(i => i.Barcode == decodedValue || i.PartNumber == decodedValue);

            if (item != null)
            {
                var qtyOnHand = await _db.ItemInventories2
                    .Where(ii => ii.ItemId == item.Id)
                    .SumAsync(ii => ii.QuantityOnHand);

                var stocking = await _stockingService.GetStockingAsync(item.Id, request.CompanyId);
                var location = FormatLocationFromStocking(stocking, item);

                return Ok(new ScanResponse
                {
                    Success = true,
                    BarcodeValue = decodedValue,
                    ItemId = item.Id,
                    PartNumber = item.PartNumber,
                    Description = item.Description,
                    QuantityOnHand = qtyOnHand,
                    Location = location,
                    Message = "Item found"
                });
            }

            var asset = await _db.Assets
                .FirstOrDefaultAsync(a => a.AssetNumber == decodedValue || a.SerialNumber == decodedValue);

            if (asset != null)
            {
                return Ok(new ScanResponse
                {
                    Success = true,
                    BarcodeValue = decodedValue,
                    AssetId = asset.Id,
                    AssetNumber = asset.AssetNumber,
                    AssetDescription = asset.Description,
                    Message = "Asset found"
                });
            }

            return Ok(new ScanResponse
            {
                Success = true,
                BarcodeValue = decodedValue,
                Message = "Barcode decoded but no matching item or asset found"
            });
        }

        [HttpGet("lookup/{barcodeValue}")]
        public async Task<IActionResult> LookupBarcode(string barcodeValue, [FromQuery] int? companyId = null)
        {
            var item = await _db.Items
                .FirstOrDefaultAsync(i => i.Barcode == barcodeValue || i.PartNumber == barcodeValue);

            if (item != null)
            {
                var qtyOnHand = await _db.ItemInventories2
                    .Where(ii => ii.ItemId == item.Id)
                    .SumAsync(ii => ii.QuantityOnHand);

                var stocking = await _stockingService.GetStockingAsync(item.Id, companyId);
                var reorderPoint = stocking.ReorderPoint;
                var location = FormatLocationFromStocking(stocking, item);

                return Ok(new LookupResponse
                {
                    Found = true,
                    Type = "Item",
                    ItemId = item.Id,
                    PartNumber = item.PartNumber,
                    Description = item.Description,
                    QuantityOnHand = qtyOnHand,
                    ReorderPoint = reorderPoint,
                    IsLowStock = qtyOnHand <= reorderPoint,
                    Location = location,
                    UnitCost = item.StandardCost
                });
            }

            var asset = await _db.Assets
                .Include(a => a.LocationRef)
                .FirstOrDefaultAsync(a => a.AssetNumber == barcodeValue || a.SerialNumber == barcodeValue);

            if (asset != null)
            {
                return Ok(new LookupResponse
                {
                    Found = true,
                    Type = "Asset",
                    AssetId = asset.Id,
                    AssetNumber = asset.AssetNumber,
                    Description = asset.Description,
                    LocationName = asset.LocationRef?.Name,
                    Status = asset.Status.ToString()
                });
            }

            return Ok(new LookupResponse { Found = false });
        }

        [HttpPost("batch-print")]
        public async Task<IActionResult> BatchPrintLabels([FromBody] BatchPrintRequest request)
        {
            if (request.ItemIds == null || request.ItemIds.Length == 0)
                return BadRequest(new { error = "No items specified" });

            var items = await _db.Items
                .Where(i => request.ItemIds.Contains(i.Id))
                .ToListAsync();

            var labels = new List<LabelData>();
            foreach (var item in items)
            {
                var barcodeValue = !string.IsNullOrEmpty(item.Barcode) 
                    ? item.Barcode 
                    : item.PartNumber;
                var barcodeType = item.BarcodeType;
                
                var imageBytes = _barcodeService.GenerateLabel(
                    barcodeValue, 
                    barcodeType, 
                    item.PartNumber, 
                    item.Description ?? "",
                    request.Width ?? 400, 
                    request.Height ?? 200);

                labels.Add(new LabelData
                {
                    ItemId = item.Id,
                    PartNumber = item.PartNumber,
                    ImageBase64 = Convert.ToBase64String(imageBytes)
                });
            }

            return Ok(new { labels });
        }

        private static string FormatLocation(Item item)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(item.Warehouse)) parts.Add(item.Warehouse);
            if (!string.IsNullOrEmpty(item.Aisle)) parts.Add($"Aisle {item.Aisle}");
            if (!string.IsNullOrEmpty(item.Rack)) parts.Add($"Rack {item.Rack}");
            if (!string.IsNullOrEmpty(item.Shelf)) parts.Add($"Shelf {item.Shelf}");
            if (!string.IsNullOrEmpty(item.Bin)) parts.Add($"Bin {item.Bin}");
            return string.Join(", ", parts);
        }

        private static string FormatLocationFromStocking(ItemStockingDto stocking, Item item)
        {
            var warehouse = stocking.DefaultWarehouse ?? item.Warehouse;
            var aisle = stocking.DefaultAisle ?? item.Aisle;
            var rack = stocking.DefaultRack ?? item.Rack;
            var shelf = stocking.DefaultShelf ?? item.Shelf;
            var bin = stocking.DefaultBin ?? item.Bin;

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(warehouse)) parts.Add(warehouse);
            if (!string.IsNullOrEmpty(aisle)) parts.Add($"Aisle {aisle}");
            if (!string.IsNullOrEmpty(rack)) parts.Add($"Rack {rack}");
            if (!string.IsNullOrEmpty(shelf)) parts.Add($"Shelf {shelf}");
            if (!string.IsNullOrEmpty(bin)) parts.Add($"Bin {bin}");
            return string.Join(", ", parts);
        }
    }

    public class ScanRequest
    {
        public string ImageBase64 { get; set; } = string.Empty;
        public int? CompanyId { get; set; }
    }

    public class ScanResponse
    {
        public bool Success { get; set; }
        public string? BarcodeValue { get; set; }
        public string? Message { get; set; }
        public int? ItemId { get; set; }
        public string? PartNumber { get; set; }
        public string? Description { get; set; }
        public decimal? QuantityOnHand { get; set; }
        public string? Location { get; set; }
        public int? AssetId { get; set; }
        public string? AssetNumber { get; set; }
        public string? AssetDescription { get; set; }
    }

    public class LookupResponse
    {
        public bool Found { get; set; }
        public string? Type { get; set; }
        public int? ItemId { get; set; }
        public string? PartNumber { get; set; }
        public string? Description { get; set; }
        public decimal? QuantityOnHand { get; set; }
        public decimal? ReorderPoint { get; set; }
        public bool IsLowStock { get; set; }
        public string? Location { get; set; }
        public decimal? UnitCost { get; set; }
        public int? AssetId { get; set; }
        public string? AssetNumber { get; set; }
        public string? LocationName { get; set; }
        public string? Status { get; set; }
    }

    public class BatchPrintRequest
    {
        public int[] ItemIds { get; set; } = Array.Empty<int>();
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    public class LabelData
    {
        public int ItemId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ImageBase64 { get; set; } = string.Empty;
    }
}
