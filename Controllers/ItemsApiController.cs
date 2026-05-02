using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Controllers
{
    [ApiController]
    [Route("api/items")]
    public class ItemsApiController : ControllerBase
    {
        private readonly IItemStockingService _stockingService;

        public ItemsApiController(IItemStockingService stockingService)
        {
            _stockingService = stockingService;
        }

        [HttpGet("{itemId}/stocking")]
        public async Task<IActionResult> GetStocking(int itemId, [FromQuery] int? companyId)
        {
            var stocking = await _stockingService.GetStockingAsync(itemId, companyId);
            return Ok(new
            {
                minQuantity = stocking.MinQuantity,
                maxQuantity = stocking.MaxQuantity,
                reorderPoint = stocking.ReorderPoint,
                reorderQuantity = stocking.ReorderQuantity,
                safetyStock = stocking.SafetyStock,
                leadTimeDays = stocking.LeadTimeDays,
                defaultWarehouse = stocking.DefaultWarehouse,
                defaultAisle = stocking.DefaultAisle,
                defaultRack = stocking.DefaultRack,
                defaultShelf = stocking.DefaultShelf,
                defaultBin = stocking.DefaultBin,
                isStocked = stocking.IsStocked,
                isCriticalSpare = stocking.IsCriticalSpare,
                preferredVendorId = stocking.PreferredVendorId
            });
        }
    }
}
