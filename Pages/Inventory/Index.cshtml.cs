using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Inventory
{
    public class IndexModel : PageModel
    {
        private readonly InventoryService _inventoryService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public IndexModel(InventoryService inventoryService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _inventoryService = inventoryService;
            _tenantContext = tenantContext;
        }

        public InventoryStats Stats { get; set; } = new();
        public List<InventoryList> InventoryLists { get; set; } = new();
        public string? Message { get; set; }

        public async Task<IActionResult> OnGetAsync(string? message = null)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("inventory"))
                return RedirectToPage("/ModuleDisabled", new { module = "Inventory" });


            Stats = await _inventoryService.GetInventoryStatsAsync();
            InventoryLists = await _inventoryService.GetAllInventoryListsAsync();
            Message = message;
        
            return Page();
        }

        public async Task<IActionResult> OnPostGenerateBarcodesAsync()
        {
            var count = await _inventoryService.GenerateBarcodesForAllAssetsAsync();
            return RedirectToPage(new { message = $"Generated barcodes for {count} assets." });
        }

        public async Task<IActionResult> OnPostCreateListAsync(
            string name,
            string? description,
            string? location,
            string? assignedTo)
        {
            var list = new InventoryList
            {
                Name = name,
                Description = description,
                Location = location,
                AssignedTo = assignedTo
            };

            await _inventoryService.CreateInventoryListAsync(list);
            return RedirectToPage(new { message = $"Inventory list '{name}' created successfully." });
        }
    }
}
