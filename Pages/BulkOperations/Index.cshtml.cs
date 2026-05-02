using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.BulkOperations
{
    [Authorize(Policy = "AccountantOrAdmin")]
    public class IndexModel : PageModel
    {
        private readonly BulkOperationsService _bulkOpsService;
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public IndexModel(BulkOperationsService bulkOpsService, AppDbContext context, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _bulkOpsService = bulkOpsService;
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public BulkOperationStats Stats { get; set; } = new();
        public List<BulkOperation> RecentOperations { get; set; } = new();
        public List<Asset> Assets { get; set; } = new();
        public List<SelectListItem> AssetStatusOptions { get; set; } = new();
        public List<SelectListItem> DisposalTypeOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Bulk Operations" });


            Stats = await _bulkOpsService.GetBulkOperationStatsAsync();
            RecentOperations = await _bulkOpsService.GetBulkOperationsAsync();
            var bulkQuery = _context.Assets.Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.Status == AssetStatus.Active);
            if (_tenantContext.SiteId.HasValue)
                bulkQuery = bulkQuery.Where(a => a.SiteId == _tenantContext.SiteId.Value);
            Assets = await bulkQuery.OrderBy(a => a.AssetNumber).ToListAsync();

            AssetStatusOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "AssetStatus", null, "");
            DisposalTypeOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "DisposalType", null, "");
        
            return Page();
        }

        public async Task<IActionResult> OnPostBulkTransferAsync(List<int> assetIds, string newLocation, string? newDepartment)
        {
            if (assetIds.Any() && !string.IsNullOrEmpty(newLocation))
            {
                await _bulkOpsService.BulkTransferAsync(assetIds, newLocation, newDepartment, User.Identity?.Name);
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostBulkStatusChangeAsync(List<int> assetIds, int newStatus)
        {
            if (assetIds.Any())
            {
                await _bulkOpsService.BulkStatusChangeAsync(assetIds, (AssetStatus)newStatus, User.Identity?.Name);
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostPartialDisposalAsync(int assetId, int percentage, decimal saleProceeds, int reason, string? buyer, string? notes)
        {
            if (assetId > 0 && percentage > 0 && percentage < 100)
            {
                var percentageDecimal = percentage / 100m;
                await _bulkOpsService.ProcessPartialDisposalAsync(
                    assetId, 
                    percentageDecimal, 
                    saleProceeds, 
                    (DisposalReason)reason, 
                    notes, 
                    buyer, 
                    User.Identity?.Name);
            }
            return RedirectToPage();
        }
    }
}
