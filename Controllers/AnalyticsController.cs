using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Controllers
{
    [Route("api/v1/analytics")]
    [ApiController]
    public class AnalyticsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public AnalyticsController(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        [HttpGet("drilldown")]
        public async Task<IActionResult> GetDrilldown(
            [FromQuery] string type = "invoice",
            [FromQuery] int limit = 50,
            [FromQuery] int offset = 0)
        {
            var companyId = _tenantContext.CompanyId;
            var siteId = _tenantContext.SiteId;

            object rows;
            int total;

            switch (type.ToLower())
            {
                case "invoice":
                case "customer_invoice":
                    var invoiceQuery = _db.CustomerInvoices
                        .Include(i => i.Customer)
                        .AsNoTracking()
                        .AsQueryable();
                    if (companyId.HasValue) invoiceQuery = invoiceQuery.Where(i => i.CompanyId == companyId.Value);
                    if (siteId.HasValue) invoiceQuery = invoiceQuery.Where(i => i.SiteId == siteId.Value);

                    total = await invoiceQuery.CountAsync();
                    var invoices = await invoiceQuery
                        .OrderByDescending(i => i.InvoiceDate)
                        .Skip(offset).Take(limit)
                        .Select(i => new
                        {
                            id = i.Id,
                            invoice_number = i.InvoiceNumber,
                            customer_name = i.Customer != null ? i.Customer.Name : "",
                            customer_id = i.CustomerId,
                            invoice_date = i.InvoiceDate,
                            total = i.Total,
                            balance_due = i.BalanceDue,
                            status = i.Status,
                            company_id = i.CompanyId,
                            detail_refs = new[] { new { type = "customer_invoice", id = i.Id } }
                        })
                        .ToListAsync();
                    rows = invoices;
                    break;

                case "purchase_order":
                    var poQuery = _db.PurchaseOrders
                        .Include(p => p.Vendor)
                        .AsNoTracking()
                        .AsQueryable();
                    if (companyId.HasValue) poQuery = poQuery.Where(p => p.CompanyId == companyId.Value);
                    if (siteId.HasValue) poQuery = poQuery.Where(p => p.ShipToSiteId == siteId.Value || p.BillToSiteId == siteId.Value);

                    total = await poQuery.CountAsync();
                    var pos = await poQuery
                        .OrderByDescending(p => p.OrderDate)
                        .Skip(offset).Take(limit)
                        .Select(p => new
                        {
                            id = p.Id,
                            po_number = p.PONumber,
                            vendor_name = p.Vendor != null ? p.Vendor.Name : "",
                            vendor_id = p.VendorId,
                            order_date = p.OrderDate,
                            total = p.Total,
                            status = p.Status,
                            company_id = p.CompanyId,
                            detail_refs = new[] { new { type = "purchase_order", id = p.Id } }
                        })
                        .ToListAsync();
                    rows = pos;
                    break;

                case "vendor_invoice":
                    var viQuery = _db.VendorInvoices
                        .Include(v => v.Vendor)
                        .AsNoTracking()
                        .AsQueryable();
                    if (companyId.HasValue) viQuery = viQuery.Where(v => v.CompanyId == companyId.Value);

                    total = await viQuery.CountAsync();
                    var vis = await viQuery
                        .OrderByDescending(v => v.InvoiceDate)
                        .Skip(offset).Take(limit)
                        .Select(v => new
                        {
                            id = v.Id,
                            invoice_number = v.InvoiceNumber,
                            vendor_name = v.Vendor != null ? v.Vendor.Name : "",
                            vendor_id = v.VendorId,
                            invoice_date = v.InvoiceDate,
                            total = v.Total,
                            status = v.Status,
                            company_id = v.CompanyId,
                            detail_refs = new[] { new { type = "vendor_invoice", id = v.Id } }
                        })
                        .ToListAsync();
                    rows = vis;
                    break;

                case "maintenance_event":
                    var meQuery = _db.WorkOrders
                        .AsNoTracking()
                        .AsQueryable();

                    total = await meQuery.CountAsync();
                    var mes = await meQuery
                        .OrderByDescending(m => m.CreatedAt)
                        .Skip(offset).Take(limit)
                        .Select(m => new
                        {
                            id = m.Id,
                            wo_number = m.WorkOrderNumber ?? ("WO-" + m.Id.ToString("D6")),
                            title = m.Description,
                            status = m.Status,
                            priority = m.Priority,
                            created_at = m.CreatedAt,
                            detail_refs = new[] { new { type = "maintenance_event", id = m.Id } }
                        })
                        .ToListAsync();
                    rows = mes;
                    break;

                case "asset":
                    var assetQuery = _db.Assets
                        .AsNoTracking()
                        .AsQueryable();
                    if (companyId.HasValue) assetQuery = assetQuery.Where(a => a.CompanyId == companyId.Value);
                    if (siteId.HasValue) assetQuery = assetQuery.Where(a => a.SiteId == siteId.Value);

                    total = await assetQuery.CountAsync();
                    var assets = await assetQuery
                        .OrderBy(a => a.AssetNumber)
                        .Skip(offset).Take(limit)
                        .Select(a => new
                        {
                            id = a.Id,
                            asset_number = a.AssetNumber,
                            description = a.Description,
                            status = a.Status,
                            company_id = a.CompanyId,
                            acquisition_date = a.InServiceDate,
                            acquisition_cost = a.AcquisitionCost,
                            detail_refs = new[] { new { type = "asset", id = a.Id } }
                        })
                        .ToListAsync();
                    rows = assets;
                    break;

                default:
                    return BadRequest(new { error = $"Unknown drilldown type: {type}" });
            }

            return Ok(new
            {
                type = type,
                total = total,
                offset = offset,
                limit = limit,
                rows = rows
            });
        }

        [HttpGet("kpis")]
        public async Task<IActionResult> GetKpis()
        {
            var companyId = _tenantContext.CompanyId;
            var siteId = _tenantContext.SiteId;

            var assetQuery = _db.Assets.AsNoTracking().AsQueryable();
            var poQuery = _db.PurchaseOrders.AsNoTracking().AsQueryable();
            var custInvQuery = _db.CustomerInvoices.AsNoTracking().AsQueryable();
            var meQuery = _db.WorkOrders.AsNoTracking().AsQueryable();

            if (companyId.HasValue)
            {
                assetQuery = assetQuery.Where(a => a.CompanyId == companyId.Value);
                poQuery = poQuery.Where(p => p.CompanyId == companyId.Value);
                custInvQuery = custInvQuery.Where(i => i.CompanyId == companyId.Value);
            }

            if (siteId.HasValue)
            {
                assetQuery = assetQuery.Where(a => a.SiteId == siteId.Value);
                poQuery = poQuery.Where(p => p.ShipToSiteId == siteId.Value || p.BillToSiteId == siteId.Value);
                custInvQuery = custInvQuery.Where(i => i.SiteId == siteId.Value);
            }

            var totalAssets = await assetQuery.CountAsync();
            var totalAssetValue = await assetQuery.SumAsync(a => (decimal?)a.AcquisitionCost) ?? 0;
            var openPOs = await poQuery.CountAsync();
            var poValue = await poQuery.SumAsync(p => (decimal?)p.Total) ?? 0;
            var totalInvoices = await custInvQuery.CountAsync();
            var invoiceRevenue = await custInvQuery.SumAsync(i => (decimal?)i.Total) ?? 0;
            var openWOs = await meQuery.CountAsync();

            return Ok(new
            {
                tiles = new[]
                {
                    new { key = "total_assets", label = "Total Assets", value = totalAssets.ToString(), format = "number" },
                    new { key = "asset_value", label = "Asset Value", value = totalAssetValue.ToString("C0"), format = "currency" },
                    new { key = "open_pos", label = "Purchase Orders", value = openPOs.ToString(), format = "number" },
                    new { key = "po_value", label = "PO Value", value = poValue.ToString("C0"), format = "currency" },
                    new { key = "total_invoices", label = "Customer Invoices", value = totalInvoices.ToString(), format = "number" },
                    new { key = "invoice_revenue", label = "Invoice Revenue", value = invoiceRevenue.ToString("C0"), format = "currency" },
                    new { key = "work_orders", label = "Work Orders", value = openWOs.ToString(), format = "number" }
                }
            });
        }
    }
}
