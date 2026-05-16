using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Purchasing
{
    // PR #105 / B-18: Purchasing write surfaces are restricted to roles that
    // can spend company money. Viewer was previously able to GET /Purchasing
    // and POST to OnPostCreatePOAsync because no role gate existed at all.
    [Authorize(Roles = "Admin,Accountant")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public IndexModel(AppDbContext context, IModuleGuardService moduleGuard, ILookupService lookupService, ITenantContext tenantContext)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<PurchaseOrder> PurchaseOrders { get; set; } = new();
        public List<Vendor> Vendors { get; set; } = new();
        public List<SelectListItem> POTypeOptions { get; set; } = new();
        public List<SelectListItem> POStatusOptions { get; set; } = new();
        public int OpenPOCount { get; set; }
        public int PendingApprovalCount { get; set; }
        public int ReceivedThisMonth { get; set; }
        public decimal OpenPOValue { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("purchasing"))
                return RedirectToPage("/ModuleDisabled", new { module = "Purchasing" });

            var visibleIds = _tenantContext.VisibleCompanyIds;

            var poQuery = _context.PurchaseOrders
                .Include(p => p.Vendor)
                .Include(p => p.Lines)
                .Where(p => visibleIds.Contains(p.CompanyId ?? 0));

            if (_tenantContext.SiteId.HasValue)
                poQuery = poQuery.Where(p => p.ShipToSiteId == _tenantContext.SiteId.Value);

            PurchaseOrders = await poQuery
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .ToListAsync();

            Vendors = await _context.Vendors
                .Where(v => v.IsActive && visibleIds.Contains(v.CompanyId ?? 0))
                .OrderBy(v => v.Name)
                .ToListAsync();

            POTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "PurchaseOrderType", null, "");
            POStatusOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", null, "All Statuses");

            var openStatuses = new[] { POStatus.Draft, POStatus.Approved, POStatus.Sent, POStatus.PartiallyReceived };
            var openPOs = PurchaseOrders.Where(p => openStatuses.Contains(p.Status)).ToList();
            
            OpenPOCount = openPOs.Count;
            OpenPOValue = openPOs.Sum(p => p.Total);
            PendingApprovalCount = PurchaseOrders.Count(p => p.Status == POStatus.PendingApproval);
            
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            ReceivedThisMonth = PurchaseOrders.Count(p => p.Status == POStatus.Received && p.UpdatedAt >= monthStart);
            
            return Page();
        }

        public async Task<IActionResult> OnPostCreatePOAsync(int vendorId, int poTypeLookupValueId, DateTime orderDate, DateTime? requiredDate, string? notes)
        {
            if (vendorId <= 0)
            {
                TempData["Error"] = "Please select a vendor";
                return RedirectToPage();
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
                CreatedAt = DateTime.UtcNow,
                CompanyId = _tenantContext.CompanyId,
                ShipToSiteId = _tenantContext.SiteId
            };

            var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, poTypeLookupValueId);
            if (lv != null && int.TryParse(lv.Code, out var enumVal))
                po.POType = (POType)enumVal;
            po.POTypeLookupValueId = poTypeLookupValueId > 0 ? poTypeLookupValueId : null;

            var draftLv = await _lookupService.GetValueByCodeAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "POStatus", ((int)POStatus.Draft).ToString());
            if (draftLv != null)
                po.StatusLookupValueId = draftLv.Id;

            _context.PurchaseOrders.Add(po);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Purchasing/Details", new { id = po.Id });
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
