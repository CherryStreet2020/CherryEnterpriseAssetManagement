using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Inventory
{
    public class ListModel : PageModel
    {
        private readonly InventoryService _inventoryService;
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public ListModel(InventoryService inventoryService, AppDbContext context, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _inventoryService = inventoryService;
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public InventoryList InventoryList { get; set; } = null!;
        public List<InventoryScan> Scans { get; set; } = new();
        public List<Asset> AvailableAssets { get; set; } = new();
        public List<SelectListItem> DiscrepancyOptions { get; set; } = new();
        public List<SelectListItem> ConditionOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("inventory"))
                return RedirectToPage("/ModuleDisabled", new { module = "Inventory" });

            var list = await _context.InventoryLists
                .Include(l => l.Scans!)
                    .ThenInclude(s => s.Asset)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list == null)
                return NotFound();

            InventoryList = list;
            Scans = list.Scans?.OrderByDescending(s => s.ScanDate).ToList() ?? new();
            AvailableAssets = await _context.Assets
                .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.Status == AssetStatus.Active)
                .OrderBy(a => a.AssetNumber)
                .Take(100)
                .ToListAsync();

            DiscrepancyOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InventoryDiscrepancy", null, "-- Select --");
            ConditionOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "InventoryCondition", null, "-- Select --");

            return Page();
        }

        public async Task<IActionResult> OnPostStartAsync(int id)
        {
            var list = await _context.InventoryLists
                .Where(l => l.Id == id)
                .FirstOrDefaultAsync();
            if (list == null)
                return NotFound();

            list.Status = InventoryStatus.InProgress;
            list.StartedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostCompleteAsync(int id)
        {
            var list = await _context.InventoryLists
                .Where(l => l.Id == id)
                .FirstOrDefaultAsync();
            if (list == null)
                return NotFound();

            list.Status = InventoryStatus.Completed;
            list.CompletedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }

        public async Task<IActionResult> OnPostAddScanAsync(
            int id,
            int assetId,
            int result,
            int condition,
            string? notes)
        {
            var list = await _context.InventoryLists
                .Where(l => l.Id == id)
                .FirstOrDefaultAsync();
            var asset = await _context.Assets
                .Where(a => a.Id == assetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            if (list == null || asset == null)
                return NotFound();

            var scan = new InventoryScan
            {
                InventoryListId = id,
                AssetId = assetId,
                ScannedBarcode = $"CFA{assetId:D8}",
                ScanDate = DateTime.UtcNow,
                ScannedBy = User.Identity?.Name ?? "system",
                Location = asset.LocationRef?.Name,
                Result = (ScanResult)result,
                Condition = (AssetCondition)condition,
                Notes = notes
            };

            _context.InventoryScans.Add(scan);
            list.ScannedAssets++;
            if ((ScanResult)result == ScanResult.Missing)
                list.MissingAssets++;
            else
                list.FoundAssets++;

            await _context.SaveChangesAsync();
            return RedirectToPage(new { id });
        }
    }
}
