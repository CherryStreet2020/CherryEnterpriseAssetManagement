using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Controllers
{
    [Route("api/v1/drilldown")]
    [ApiController]
    public class DrilldownController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public DrilldownController(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        [HttpGet("party-summary")]
        public async Task<IActionResult> GetPartySummary()
        {
            var companyId = _tenantContext.CompanyId;

            var allRows = await _db.Vendors.AsNoTracking()
                .Where(v => companyId == null || v.CompanyId == companyId)
                .Select(v => new
                {
                    VendorName = v.Name ?? v.Code,
                    v.Code,
                    TotalAmount = _db.PurchaseOrders
                        .Where(po => po.VendorId == v.Id)
                        .SelectMany(po => po.Lines)
                        .Sum(l => (decimal?)(l.UnitPrice * l.QuantityOrdered)) ?? 0m,
                    TransactionCount = _db.PurchaseOrders
                        .Where(po => po.VendorId == v.Id)
                        .Count(),
                    LastTransactionDate = _db.PurchaseOrders
                        .Where(po => po.VendorId == v.Id)
                        .Max(po => (DateTime?)po.OrderDate)
                })
                .OrderByDescending(v => v.TotalAmount)
                .ToListAsync();

            return Ok(new
            {
                orgScope = companyId.HasValue ? $"Company {companyId}" : "All Companies",
                totalRows = allRows.Count,
                totalAmount = allRows.Sum(r => r.TotalAmount),
                rows = allRows.Select(r => new
                {
                    vendorName = r.VendorName,
                    vendorCode = r.Code,
                    r.TotalAmount,
                    r.TransactionCount,
                    LastTransactionDate = r.LastTransactionDate?.ToString("yyyy-MM-dd")
                })
            });
        }

        [HttpGet("cip-kpis")]
        public async Task<IActionResult> GetCipKpis()
        {
            var companyId = _tenantContext.CompanyId;
            var siteId = _tenantContext.SiteId;

            var query = _db.CipProjects.AsNoTracking();
            if (companyId.HasValue)
                query = query.Where(p => p.CompanyId == companyId);

            var projects = await query.ToListAsync();

            return Ok(new
            {
                orgScope = companyId.HasValue ? $"Company {companyId}" : "All Companies",
                totalProjects = projects.Count,
                activeProjects = projects.Count(p => p.Status == CipProjectStatus.Active),
                totalBudget = projects.Sum(p => p.BudgetAmount),
                totalSpent = projects.Sum(p => p.TotalCosts),
                completedProjects = projects.Count(p => p.IsCapitalized),
                cancelledProjects = projects.Count(p => p.Status == CipProjectStatus.Cancelled)
            });
        }
    }
}
