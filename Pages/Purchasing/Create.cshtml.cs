using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Purchasing
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public CreateModel(AppDbContext context, IModuleGuardService moduleGuard, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<Vendor> Vendors { get; set; } = new();
        public List<SelectListItem> POTypeOptions { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Purchasing" });

            await LoadFormDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int vendorId, int poTypeLookupValueId, DateTime orderDate, DateTime? requiredDate, string? notes)
        {
            if (vendorId <= 0)
            {
                TempData["Error"] = "Please select a vendor.";
                await LoadFormDataAsync();
                return Page();
            }

            var poNumber = await GeneratePONumberAsync();

            var po = new PurchaseOrder
            {
                PONumber = poNumber,
                VendorId = vendorId,
                POType = POType.Standard,
                POTypeLookupValueId = poTypeLookupValueId > 0 ? poTypeLookupValueId : null,
                OrderDate = orderDate,
                RequiredDate = requiredDate,
                Notes = notes,
                Status = POStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, poTypeLookupValueId);
            if (lv != null && int.TryParse(lv.Code, out var enumVal))
                po.POType = (POType)enumVal;
            po.POTypeLookupValueId = poTypeLookupValueId > 0 ? poTypeLookupValueId : null;

            var draftLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", ((int)POStatus.Draft).ToString());
            if (draftLv != null)
                po.StatusLookupValueId = draftLv.Id;

            po.CompanyId = _tenantContext.CompanyId;
            po.ShipToSiteId = _tenantContext.SiteId;

            _context.PurchaseOrders.Add(po);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Purchasing/Details", new { id = po.Id });
        }

        private async Task LoadFormDataAsync()
        {
            var visibleIds = _tenantContext.VisibleCompanyIds;

            Vendors = await _context.Vendors
                .Where(v => v.IsActive && visibleIds.Contains(v.CompanyId ?? 0))
                .OrderBy(v => v.Name)
                .ToListAsync();

            POTypeOptions = await _lookupService.GetSelectListByIdAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId, "PurchaseOrderType", null, "");
        }

        private async Task<string> GeneratePONumberAsync()
        {
            var year = DateTime.UtcNow.Year.ToString().Substring(2);
            var prefix = $"PO-{year}-";

            var lastPO = await _context.PurchaseOrders
                .Where(p => p.PONumber.StartsWith(prefix))
                .OrderByDescending(p => p.PONumber)
                .FirstOrDefaultAsync();

            var nextNum = 1;
            if (lastPO != null)
            {
                var lastNumStr = lastPO.PONumber.Replace(prefix, "");
                if (int.TryParse(lastNumStr, out var lastNum))
                {
                    nextNum = lastNum + 1;
                }
            }

            return $"{prefix}{nextNum:D5}";
        }
    }
}
