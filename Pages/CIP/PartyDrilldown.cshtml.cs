using Microsoft.AspNetCore.Mvc;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.CIP
{
    public class PartyDrilldownModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public PartyDrilldownModel(AppDbContext db, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _tenantContext = tenantContext;
        }

        public List<VendorRow> Vendors { get; set; } = new();
        public string OrgScope { get; set; } = "All Companies";
        public int TotalRows { get; set; }
        public decimal TotalAmount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("projects"))
                return RedirectToPage("/ModuleDisabled", new { module = "CIP" });


            var companyId = _tenantContext.CompanyId;
            OrgScope = companyId.HasValue ? $"Company {companyId}" : "All Companies";

            var siteId = _tenantContext.SiteId;
            Vendors = await _db.Vendors.AsNoTracking()
                .Where(v => companyId == null || _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0))
                .Select(v => new VendorRow
                {
                    VendorName = v.Name,
                    VendorCode = v.Code,
                    TotalAmount = _db.PurchaseOrders
                        .Where(po => po.VendorId == v.Id && (!siteId.HasValue || po.ShipToSiteId == siteId.Value))
                        .SelectMany(po => po.Lines)
                        .Sum(l => (decimal?)(l.UnitPrice * l.QuantityOrdered)) ?? 0m,
                    TransactionCount = _db.PurchaseOrders
                        .Where(po => po.VendorId == v.Id && (!siteId.HasValue || po.ShipToSiteId == siteId.Value))
                        .Count(),
                    LastTransactionDate = _db.PurchaseOrders
                        .Where(po => po.VendorId == v.Id && (!siteId.HasValue || po.ShipToSiteId == siteId.Value))
                        .Max(po => (DateTime?)po.OrderDate)
                })
                .OrderByDescending(v => v.TotalAmount)
                .ToListAsync();

            TotalRows = Vendors.Count;
            TotalAmount = Vendors.Sum(v => v.TotalAmount);
        
            return Page();
        }

        public class VendorRow
        {
            public string VendorName { get; set; } = "";
            public string VendorCode { get; set; } = "";
            public decimal TotalAmount { get; set; }
            public int TransactionCount { get; set; }
            public DateTime? LastTransactionDate { get; set; }
        }
    }
}
