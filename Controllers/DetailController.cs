using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Controllers
{
    [Route("api/v1/details")]
    [ApiController]
    public class DetailController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public DetailController(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        [HttpGet("{type}/{id}")]
        public async Task<IActionResult> GetDetail(string type, string id)
        {
            return type.ToLower() switch
            {
                "asset" => await GetAssetDetail(int.Parse(id)),
                "purchase_order" => await GetPurchaseOrderDetail(int.Parse(id)),
                "vendor_invoice" => await GetVendorInvoiceDetail(int.Parse(id)),
                "maintenance_event" or "work_order" => await GetMaintenanceEventDetail(int.Parse(id)),
                "vendor" => await GetVendorDetail(int.Parse(id)),
                "site" => await GetSiteDetail(int.Parse(id)),
                "location" => await GetLocationDetail(int.Parse(id)),
                "company" => await GetCompanyDetail(int.Parse(id)),
                "book" => await GetBookDetail(int.Parse(id)),
                "item" => await GetItemDetail(int.Parse(id)),
                "cip_project" => await GetCipProjectDetail(int.Parse(id)),
                "fiscal_year" => await GetFiscalYearDetail(int.Parse(id)),
                "fiscal_period" => await GetFiscalPeriodDetail(int.Parse(id)),
                "lookup_type" => await GetLookupTypeDetail(int.Parse(id)),
                "pm_schedule" => await GetPmScheduleDetail(int.Parse(id)),
                "pm_template" => await GetPmTemplateDetail(int.Parse(id)),
                "org_node" => await GetOrgNodeDetail(id),
                "user" => await GetUserDetail(int.Parse(id)),
                "journal_entry" => await GetJournalEntryDetail(int.Parse(id)),
                "work_request" => await GetWorkRequestDetail(int.Parse(id)),
                "gl_account" => await GetGlAccountDetail(int.Parse(id)),
                _ => NotFound(new { error = $"Unknown detail type: {type}" })
            };
        }

        private async Task<IActionResult> GetAssetDetail(int id)
        {
            var a = await _db.Assets.Include(x => x.Company).Include(x => x.Site)
                .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound(new { error = "Asset not found" });

            var books = await _db.AssetBookSettings.Where(abs => abs.AssetId == id).Include(abs => abs.Book).AsNoTracking()
                .Select(abs => new { abs.BookId, bookCode = abs.Book != null ? abs.Book.Code : "", bookName = abs.Book != null ? abs.Book.Name : "", method = abs.Book != null ? abs.Book.Method.ToString() : "", bookType = abs.Book != null ? abs.Book.BookType.ToString() : "" }).ToListAsync();
            var wos = await _db.MaintenanceEvents.Where(m => m.AssetId == id).AsNoTracking()
                .OrderByDescending(m => m.ScheduledDate).Take(10)
                .Select(m => new { m.Id, m.WorkOrderNumber, m.Description, m.Status, m.ScheduledDate }).ToListAsync();

            return Ok(new
            {
                type = "asset", id = a.Id,
                header = new
                {
                    a.AssetNumber, a.Description, a.LongDescription, a.Model, a.SerialNumber,
                    a.TagNumber, a.AssetType, a.Status, a.Condition, a.Priority,
                    a.IsCritical, a.AcquisitionCost, a.InServiceDate, a.PurchaseDate,
                    company_name = a.Company?.Name ?? "", site_name = a.Site?.Name ?? "",
                    a.CompanyId, a.SiteId, a.LocationId, a.UsefulLifeMonths,
                    a.SalvageValue, a.DepreciationMethod, a.ImageUrl, a.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { a.AssetNumber, a.SerialNumber, a.TagNumber, a.Model, a.ImageUrl } },
                    new { name = "financial", fields = new { a.AcquisitionCost, a.SalvageValue, a.UsefulLifeMonths, a.InServiceDate, a.PurchaseDate, a.DepreciationMethod } },
                    new { name = "classification", fields = new { a.AssetType, a.Status, a.Condition, a.Priority, a.IsCritical } },
                    new { name = "location", fields = new { a.CompanyId, company_name = a.Company?.Name, a.SiteId, site_name = a.Site?.Name, a.LocationId } }
                },
                related = new { books, work_orders = wos }
            });
        }

        private async Task<IActionResult> GetPurchaseOrderDetail(int id)
        {
            var p = await _db.PurchaseOrders.Include(x => x.Vendor).Include(x => x.ShipToSite)
                .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound(new { error = "PurchaseOrder not found" });

            var lines = await _db.PurchaseOrderLines.Where(l => l.PurchaseOrderId == id).AsNoTracking()
                .Select(l => new { l.Id, l.LineNumber, l.Description, l.QuantityOrdered, l.UnitPrice, l.LineTotal }).ToListAsync();

            return Ok(new
            {
                type = "purchase_order", id = p.Id,
                header = new
                {
                    p.PONumber, p.POType, p.Status, vendor_name = p.Vendor?.Name ?? "",
                    p.VendorId, p.OrderDate, p.RequiredDate, p.PromiseDate,
                    p.Currency, p.Subtotal, p.TaxAmount, p.ShippingAmount, p.Total,
                    ship_to_site = p.ShipToSite?.Name ?? "", p.CompanyId, p.Notes, p.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "order_info", fields = new { p.PONumber, p.POType, p.Status, p.OrderDate, p.RequiredDate, p.PromiseDate } },
                    new { name = "vendor", fields = new { p.VendorId, vendor_name = p.Vendor?.Name, vendor_code = p.Vendor?.Code } },
                    new { name = "financials", fields = new { p.Subtotal, p.TaxAmount, p.ShippingAmount, p.Total, p.Currency } }
                },
                related = new { lines }
            });
        }

        private async Task<IActionResult> GetCustomerInvoiceDetail(int id)
        {
            var inv = await _db.CustomerInvoices.Include(x => x.Customer)
                .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (inv == null) return NotFound(new { error = "CustomerInvoice not found" });

            var lines = await _db.CustomerInvoiceLines.Where(l => l.CustomerInvoiceId == id).AsNoTracking()
                .Select(l => new { l.Id, l.LineNumber, l.Description, l.Quantity, l.UnitPrice, l.LineTotal }).ToListAsync();

            return Ok(new
            {
                type = "customer_invoice", id = inv.Id,
                header = new
                {
                    inv.InvoiceNumber, customer_name = inv.Customer?.Name ?? "",
                    inv.CustomerId, inv.InvoiceDate, inv.DueDate,
                    inv.Subtotal, inv.TaxAmount, inv.Total, inv.AmountPaid,
                    inv.BalanceDue, inv.Status, inv.CompanyId, inv.SiteId,
                    inv.PurchaseOrderRef, inv.Notes, inv.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "invoice_info", fields = new { inv.InvoiceNumber, inv.InvoiceDate, inv.DueDate, inv.Status, inv.PurchaseOrderRef } },
                    new { name = "customer", fields = new { inv.CustomerId, customer_name = inv.Customer?.Name, customer_code = inv.Customer?.CustomerCode } },
                    new { name = "financials", fields = new { inv.Subtotal, inv.TaxAmount, inv.Total, inv.AmountPaid, inv.BalanceDue } }
                },
                related = new { lines }
            });
        }

        private async Task<IActionResult> GetVendorInvoiceDetail(int id)
        {
            var vi = await _db.VendorInvoices.Include(x => x.Vendor)
                .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (vi == null) return NotFound(new { error = "VendorInvoice not found" });

            return Ok(new
            {
                type = "vendor_invoice", id = vi.Id,
                header = new
                {
                    vi.InvoiceNumber, vendor_name = vi.Vendor?.Name ?? "",
                    vi.VendorId, vi.InvoiceDate, vi.DueDate,
                    vi.Subtotal, vi.TaxAmount, vi.Total, vi.AmountPaid,
                    vi.BalanceDue, vi.Status, vi.CompanyId,
                    vi.Notes, vi.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "invoice_info", fields = new { vi.InvoiceNumber, vi.InvoiceDate, vi.DueDate, vi.Status } },
                    new { name = "vendor", fields = new { vi.VendorId, vendor_name = vi.Vendor?.Name } },
                    new { name = "financials", fields = new { vi.Subtotal, vi.TaxAmount, vi.Total, vi.AmountPaid, vi.BalanceDue } }
                },
                related = new { }
            });
        }

        private async Task<IActionResult> GetMaintenanceEventDetail(int id)
        {
            var m = await _db.MaintenanceEvents.Include(x => x.Asset).Include(x => x.Technician)
                .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (m == null) return NotFound(new { error = "MaintenanceEvent not found" });

            var ops = await _db.WorkOrderOperations.Where(o => o.MaintenanceEventId == id).AsNoTracking()
                .Select(o => new { o.Id, o.OperationNumber, o.Description, o.Status }).ToListAsync();

            return Ok(new
            {
                type = "maintenance_event", id = m.Id,
                header = new
                {
                    m.WorkOrderNumber, m.Description, m.Type, m.Status, m.Priority,
                    asset_number = m.Asset?.AssetNumber ?? "", asset_description = m.Asset?.Description ?? "",
                    m.AssetId, m.ScheduledDate, m.CompletedDate,
                    m.EstimatedCost, m.ActualCost, m.LaborCost, m.PartsCost,
                    m.TechnicianName, m.DowntimeHours, m.LaborHours,
                    m.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "work_order_info", fields = new { m.WorkOrderNumber, m.Description, m.Type, m.Status, m.Priority } },
                    new { name = "scheduling", fields = new { m.ScheduledDate, m.CompletedDate, m.DowntimeHours, m.LaborHours } },
                    new { name = "costs", fields = new { m.EstimatedCost, m.ActualCost, m.LaborCost, m.PartsCost } },
                    new { name = "asset", fields = new { m.AssetId, asset_number = m.Asset?.AssetNumber, asset_description = m.Asset?.Description } }
                },
                related = new { operations = ops }
            });
        }

        private async Task<IActionResult> GetVendorDetail(int id)
        {
            var v = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (v == null) return NotFound(new { error = "Vendor not found" });

            var pos = await _db.PurchaseOrders.Where(p => p.VendorId == id).AsNoTracking()
                .OrderByDescending(p => p.OrderDate).Take(10)
                .Select(p => new { p.Id, p.PONumber, p.Total, p.Status, p.OrderDate }).ToListAsync();

            return Ok(new
            {
                type = "vendor", id = v.Id,
                header = new
                {
                    v.Code, v.Name, v.LegalName, v.VendorType, v.Status,
                    v.ContactName, v.Phone, v.Email, v.Website,
                    v.Address, v.City, v.State, v.PostalCode, v.Country,
                    v.TaxId, v.PaymentTerms, v.Currency, v.CreditLimit
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { v.Code, v.Name, v.LegalName, v.VendorType, v.Status } },
                    new { name = "contact", fields = new { v.ContactName, v.Phone, v.Email, v.Website } },
                    new { name = "address", fields = new { v.Address, v.City, v.State, v.PostalCode, v.Country } }
                },
                related = new { purchase_orders = pos }
            });
        }

        private async Task<IActionResult> GetCustomerDetail(int id)
        {
            var c = await _db.Customers.Include(x => x.Company).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound(new { error = "Customer not found" });

            var invoices = await _db.CustomerInvoices.Where(i => i.CustomerId == id).AsNoTracking()
                .OrderByDescending(i => i.InvoiceDate).Take(10)
                .Select(i => new { i.Id, i.InvoiceNumber, i.Total, i.Status, i.InvoiceDate }).ToListAsync();

            return Ok(new
            {
                type = "customer", id = c.Id,
                header = new
                {
                    c.CustomerCode, c.Name, c.ContactName, c.ContactEmail, c.ContactPhone,
                    c.Address, c.City, c.StateProvince, c.PostalCode, c.Country,
                    c.TaxId, c.Currency, c.IsActive, c.CompanyId,
                    company_name = c.Company?.Name ?? "", c.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { c.CustomerCode, c.Name, c.IsActive } },
                    new { name = "contact", fields = new { c.ContactName, c.ContactEmail, c.ContactPhone } },
                    new { name = "address", fields = new { c.Address, c.City, c.StateProvince, c.PostalCode, c.Country } }
                },
                related = new { invoices }
            });
        }

        private async Task<IActionResult> GetSiteDetail(int id)
        {
            var s = await _db.Sites.Include(x => x.Company).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound(new { error = "Site not found" });

            var locations = await _db.Locations.Where(l => l.SiteId == id).AsNoTracking()
                .Select(l => new { l.Id, l.Code, l.Name, l.Type }).ToListAsync();
            var assets = await _db.Assets.Where(a => a.SiteId == id).AsNoTracking()
                .Select(a => new { a.Id, a.AssetNumber, a.Description }).Take(20).ToListAsync();

            return Ok(new
            {
                type = "site", id = s.Id,
                header = new
                {
                    s.SiteCode, s.Name, s.Description, s.Type, s.Status,
                    company_name = s.Company?.Name ?? "", s.CompanyId,
                    s.Address1, s.City, s.StateProvince, s.PostalCode, s.Country,
                    s.TimeZone, s.SiteManager, s.MainPhone, s.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { s.SiteCode, s.Name, s.Description, s.Type, s.Status } },
                    new { name = "address", fields = new { s.Address1, s.Address2, s.City, s.StateProvince, s.PostalCode, s.Country } },
                    new { name = "operations", fields = new { s.TimeZone, s.SiteManager, location_count = locations.Count, asset_count = assets.Count } }
                },
                related = new { locations, assets }
            });
        }

        private async Task<IActionResult> GetLocationDetail(int id)
        {
            var l = await _db.Locations.Include(x => x.Site).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (l == null) return NotFound(new { error = "Location not found" });

            var assets = await _db.Assets.Where(a => a.LocationId == id).AsNoTracking()
                .Select(a => new { a.Id, a.AssetNumber, a.Description }).Take(20).ToListAsync();

            return Ok(new
            {
                type = "location", id = l.Id,
                header = new
                {
                    l.Code, l.Name, l.Description, l.Type,
                    l.Building, l.Floor, l.Bay, l.Station,
                    l.SiteId, site_name = l.Site?.Name ?? "",
                    l.IsActive, l.SortOrder, asset_count = assets.Count
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { l.Code, l.Name, l.Description, l.Type } },
                    new { name = "physical", fields = new { l.Building, l.Floor, l.Bay, l.Station } },
                    new { name = "hierarchy", fields = new { l.SiteId, site_name = l.Site?.Name, asset_count = assets.Count } }
                },
                related = new { assets }
            });
        }

        private async Task<IActionResult> GetCompanyDetail(int id)
        {
            var c = await _db.Companies.Include(x => x.Tenant).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound(new { error = "Company not found" });

            var sites = await _db.Sites.Where(s => s.CompanyId == id).AsNoTracking()
                .Select(s => new { s.Id, s.SiteCode, s.Name, s.Type, s.Status }).ToListAsync();
            var assetCount = await _db.Assets.CountAsync(a => a.CompanyId == id);

            return Ok(new
            {
                type = "company", id = c.Id,
                header = new
                {
                    c.Name, c.LegalName, c.CompanyCode, c.CompanyType, c.CompanyStructure,
                    c.Currency, c.TaxId, c.PeriodType, c.FiscalYearStartMonth,
                    c.Address, c.City, c.StateProvince, c.PostalCode, c.Country,
                    c.ContactPhone, c.IsActive, c.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { c.Name, c.LegalName, c.CompanyCode, c.CompanyType } },
                    new { name = "financial", fields = new { c.Currency, c.TaxId, c.PeriodType, c.FiscalYearStartMonth } },
                    new { name = "contact", fields = new { c.Address, c.City, c.StateProvince, c.PostalCode, c.Country, c.ContactPhone } }
                },
                related = new { sites, asset_count = assetCount }
            });
        }

        private async Task<IActionResult> GetBookDetail(int id)
        {
            var b = await _db.Books.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (b == null) return NotFound(new { error = "Book not found" });

            var assetCount = await _db.AssetBookSettings.CountAsync(abs => abs.BookId == id);

            return Ok(new
            {
                type = "book", id = b.Id,
                header = new
                {
                    b.Code, b.Name, b.Description, b.Method, b.Convention,
                    b.BookType, b.UsefulLifeOverrideMonths, b.IsPrimaryBook,
                    b.TaxJurisdiction, b.AllowManualDepreciation,
                    b.GlAccountDepExp, b.GlAccountAccumDep, b.GlAccountGainOnDisposal,
                    b.GlAccountLossOnDisposal, b.GlAccountAssetClearing, b.GlAccountCIP,
                    b.CreatedAt, asset_count = assetCount
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { b.Code, b.Name, b.Description, b.BookType } },
                    new { name = "depreciation", fields = new { b.Method, b.Convention, b.UsefulLifeOverrideMonths, b.TaxJurisdiction } },
                    new { name = "gl_accounts", fields = new { b.GlAccountDepExp, b.GlAccountAccumDep, b.GlAccountGainOnDisposal, b.GlAccountLossOnDisposal } }
                },
                related = new { asset_count = assetCount }
            });
        }

        private async Task<IActionResult> GetItemDetail(int id)
        {
            var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound(new { error = "Item not found" });

            var vendorParts = await _db.ItemVendors.Where(iv => iv.ItemId == id).AsNoTracking()
                .Select(iv => new { iv.VendorId, iv.VendorPartNumber }).Take(10).ToListAsync();

            return Ok(new
            {
                type = "item", id = item.Id,
                header = new
                {
                    item.PartNumber, item.Description, item.ExtendedDescription,
                    item.Revision, item.Type, item.Status,
                    item.UOM, item.CostMethod, item.StandardCost,
                    item.AverageCost, item.ListPrice, item.ReorderPoint,
                    item.ReorderQuantity, item.SafetyStock, item.IsActive
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { item.PartNumber, item.Description, item.Revision, item.Type } },
                    new { name = "inventory", fields = new { item.UOM, item.ReorderPoint, item.ReorderQuantity, item.SafetyStock } },
                    new { name = "costing", fields = new { item.CostMethod, item.StandardCost, item.AverageCost, item.ListPrice } }
                },
                related = new { vendor_parts = vendorParts }
            });
        }

        private async Task<IActionResult> GetCipProjectDetail(int id)
        {
            var p = await _db.CipProjects.Include(x => x.Company).Include(x => x.Site)
                .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound(new { error = "CipProject not found" });

            var costs = await _db.CipCosts.Where(c => c.CipProjectId == id).AsNoTracking()
                .Select(c => new { c.Id, c.CostType, c.Description, c.Amount, c.TransactionDate, c.SourceType, c.SourceDisplayRef, c.IsCapitalizable, c.Vendor, c.CostTypeLookupValueId }).ToListAsync();

            var workOrders = await _db.MaintenanceEvents.Where(w => w.CipProjectId == id).AsNoTracking()
                .OrderByDescending(w => w.ScheduledDate).Take(25)
                .Select(w => new { w.Id, w.WorkOrderNumber, w.Description, w.Status, w.ActualCost, w.ScheduledDate }).ToListAsync();

            var purchaseOrders = await _db.PurchaseOrders.Where(po => po.CipProjectId == id).AsNoTracking()
                .OrderByDescending(po => po.OrderDate).Take(25)
                .Select(po => new { po.Id, po.PONumber, po.Status, po.OrderDate, po.Total }).ToListAsync();

            var invoiceIds = await _db.CipCosts.Where(c => c.CipProjectId == id && c.VendorInvoiceId != null).AsNoTracking()
                .Select(c => c.VendorInvoiceId!.Value).Distinct().ToListAsync();
            var vendorInvoices = await _db.VendorInvoices.Where(i => invoiceIds.Contains(i.Id)).AsNoTracking()
                .Select(i => new { i.Id, i.InvoiceNumber, i.Status, i.InvoiceDate, i.Total }).ToListAsync();

            var journalIds = await _db.CipCapitalizations.Where(cap => cap.CipProjectId == id && cap.JournalEntryId != null).AsNoTracking()
                .Select(cap => cap.JournalEntryId!.Value).Distinct().ToListAsync();
            var costJournalIds = await _db.CipCosts.Where(c => c.CipProjectId == id && c.JournalEntryId != null).AsNoTracking()
                .Select(c => c.JournalEntryId!.Value).Distinct().ToListAsync();
            var allJournalIds = journalIds.Union(costJournalIds).Distinct().ToList();
            var journals = await _db.JournalEntries.Where(j => allJournalIds.Contains(j.Id)).AsNoTracking()
                .Select(j => new { j.Id, j.Batch, j.Source, j.PostingDate, j.Reference }).ToListAsync();

            var assetIds = await _db.CipCapitalizations.Where(cap => cap.CipProjectId == id).AsNoTracking()
                .Select(cap => cap.AssetId).Distinct().ToListAsync();
            if (p.ConvertedAssetId.HasValue && !assetIds.Contains(p.ConvertedAssetId.Value))
                assetIds.Add(p.ConvertedAssetId.Value);
            var assets = await _db.Assets.Where(a => assetIds.Contains(a.Id)).AsNoTracking()
                .Select(a => new { a.Id, a.AssetNumber, a.Description, a.AcquisitionCost, a.Status }).ToListAsync();

            return Ok(new
            {
                type = "cip_project", id = p.Id,
                header = new
                {
                    p.ProjectNumber, p.Name, p.Description, p.Status,
                    p.StartDate, p.EstimatedCompletionDate, p.ActualCompletionDate,
                    p.BudgetAmount, p.TotalCosts, p.CommittedCosts,
                    p.ProjectManagerName, p.Location, p.Department,
                    p.IsCapitalized, p.CapitalizedAt,
                    p.CompanyId, company_name = p.Company?.Name ?? "",
                    p.SiteId, site_name = p.Site?.Name ?? "",
                    p.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "project_info", fields = new { p.ProjectNumber, p.Name, p.Description, p.Status, p.IsCapitalized } },
                    new { name = "schedule", fields = new { p.StartDate, p.EstimatedCompletionDate, p.ActualCompletionDate, p.CapitalizedAt } },
                    new { name = "budget", fields = new { p.BudgetAmount, p.TotalCosts, p.CommittedCosts, spent = costs.Sum(c => c.Amount), cost_count = costs.Count } },
                    new { name = "organization", fields = new { p.CompanyId, company_name = p.Company?.Name, p.SiteId, site_name = p.Site?.Name, p.Location, p.Department } }
                },
                related = new
                {
                    costs,
                    work_orders = workOrders,
                    purchase_orders = purchaseOrders,
                    vendor_invoices = vendorInvoices,
                    journals,
                    assets
                }
            });
        }

        private async Task<IActionResult> GetFiscalYearDetail(int id)
        {
            var fy = await _db.FiscalYears.Include(x => x.Company).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (fy == null) return NotFound(new { error = "FiscalYear not found" });

            var periods = await _db.FiscalPeriods.Where(p => p.FiscalYearId == id).AsNoTracking()
                .OrderBy(p => p.PeriodNumber)
                .Select(p => new { p.Id, p.PeriodNumber, p.Name, p.StartDate, p.EndDate, p.Status }).ToListAsync();

            return Ok(new
            {
                type = "fiscal_year", id = fy.Id,
                header = new
                {
                    fy.Year, fy.Name, fy.StartDate, fy.EndDate, fy.Status,
                    fy.IsShortYear, fy.NumberOfPeriods, fy.PeriodType,
                    fy.HasAdjustmentPeriod, fy.CompanyId,
                    company_name = fy.Company?.Name ?? "", fy.CreatedAt, fy.ClosedAt, fy.ClosedBy
                },
                sections = new object[]
                {
                    new { name = "calendar", fields = new { fy.Year, fy.Name, fy.StartDate, fy.EndDate } },
                    new { name = "configuration", fields = new { fy.PeriodType, fy.NumberOfPeriods, fy.HasAdjustmentPeriod, fy.IsShortYear } },
                    new { name = "status", fields = new { fy.Status, fy.ClosedAt, fy.ClosedBy } }
                },
                related = new { periods }
            });
        }

        private async Task<IActionResult> GetFiscalPeriodDetail(int id)
        {
            var fp = await _db.FiscalPeriods.Include(x => x.FiscalYear).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (fp == null) return NotFound(new { error = "FiscalPeriod not found" });

            return Ok(new
            {
                type = "fiscal_period", id = fp.Id,
                header = new
                {
                    fp.PeriodNumber, fp.Name, fp.StartDate, fp.EndDate, fp.Status,
                    fp.IsAdjustmentPeriod, fp.DaysInPeriod, fp.FiscalYearId,
                    fiscal_year_name = fp.FiscalYear?.Name ?? "",
                    fp.DepreciationCalculated, fp.DepreciationPosted,
                    fp.ClosedAt, fp.ClosedBy,
                    fp.CreatedAt
                },
                sections = new object[]
                {
                    new { name = "period_info", fields = new { fp.PeriodNumber, fp.Name, fp.StartDate, fp.EndDate } },
                    new { name = "status", fields = new { fp.Status, fp.IsAdjustmentPeriod } },
                    new { name = "fiscal_year", fields = new { fp.FiscalYearId, fiscal_year_name = fp.FiscalYear?.Name } }
                },
                related = new { }
            });
        }

        private async Task<IActionResult> GetLookupTypeDetail(int id)
        {
            var lt = await _db.LookupTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (lt == null) return NotFound(new { error = "LookupType not found" });

            var values = await _db.LookupValues.Where(v => v.LookupTypeId == id).AsNoTracking()
                .OrderBy(v => v.SortOrder)
                .Select(v => new { v.Id, v.Code, v.Name, v.IsActive, v.SortOrder }).ToListAsync();

            return Ok(new
            {
                type = "lookup_type", id = lt.Id,
                header = new
                {
                    lt.Key, lt.Name, lt.IsSystem, lt.IsActive,
                    lt.TenantId, lt.CompanyId,
                    value_count = values.Count, lt.CreatedAt, lt.UpdatedAt,
                    active_values = values.Count(v => v.IsActive),
                    inactive_values = values.Count(v => !v.IsActive),
                    scope = lt.TenantId.HasValue ? "tenant" : lt.CompanyId.HasValue ? "company" : "global"
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { lt.Key, lt.Name, lt.IsSystem, lt.IsActive } },
                    new { name = "statistics", fields = new { value_count = values.Count, active_values = values.Count(v => v.IsActive), inactive_values = values.Count(v => !v.IsActive) } },
                    new { name = "scope", fields = new { lt.TenantId, lt.CompanyId, lt.CreatedAt, lt.UpdatedAt } }
                },
                related = new { values }
            });
        }

        private async Task<IActionResult> GetPmScheduleDetail(int id)
        {
            var s = await _db.MaintenanceSchedules.Include(x => x.Asset).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (s == null) return NotFound(new { error = "PmSchedule not found" });

            return Ok(new
            {
                type = "pm_schedule", id = s.Id,
                header = new
                {
                    s.Name, s.Description, s.Type, s.Recurrence, s.IntervalValue,
                    s.StartDate, s.EndDate, s.LastGeneratedDate, s.NextDueDate,
                    s.EstimatedCost, s.AssignedVendor, s.IsActive,
                    s.AssetId, asset_number = s.Asset?.AssetNumber ?? ""
                },
                sections = new object[]
                {
                    new { name = "schedule_info", fields = new { s.Name, s.Description, s.Type, s.IsActive } },
                    new { name = "recurrence", fields = new { s.Recurrence, s.IntervalValue, s.StartDate, s.EndDate } },
                    new { name = "execution", fields = new { s.LastGeneratedDate, s.NextDueDate, s.EstimatedCost, s.AssignedVendor } }
                },
                related = new { }
            });
        }

        private async Task<IActionResult> GetPmTemplateDetail(int id)
        {
            var t = await _db.PMTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (t == null) return NotFound(new { error = "PmTemplate not found" });

            var revisions = await _db.PMTemplateRevisions.Where(r => r.PMTemplateId == id).AsNoTracking()
                .OrderByDescending(r => r.RevisionCode)
                .Select(r => new { r.Id, r.RevisionCode, r.Status, r.EffectiveFromUtc, r.CreatedAtUtc }).ToListAsync();

            return Ok(new
            {
                type = "pm_template", id = t.Id,
                header = new
                {
                    t.Code, t.Name, t.Description, t.Type, t.Priority,
                    t.TriggerType, t.CalendarInterval, t.CalendarIntervalValue,
                    t.EstimatedHours, t.EstimatedTotalCost,
                    t.RequiresShutdown, t.RequiresLOTO, t.IsActive,
                    t.CompanyId, t.CreatedAt, t.UpdatedAt
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { t.Code, t.Name, t.Description, t.Type } },
                    new { name = "scheduling", fields = new { t.TriggerType, t.CalendarInterval, t.CalendarIntervalValue, t.MeterType, t.MeterInterval } },
                    new { name = "resources", fields = new { t.EstimatedHours, t.EstimatedLaborCost, t.EstimatedPartsCost, t.EstimatedTotalCost, t.SkillLevel, t.Craft } }
                },
                related = new { revisions }
            });
        }

        private async Task<IActionResult> GetOrgNodeDetail(string id)
        {
            if (!Guid.TryParse(id, out var guid))
                return BadRequest(new { error = "Invalid GUID format" });

            var n = await _db.OrgNodes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == guid);
            if (n == null) return NotFound(new { error = "OrgNode not found" });

            var children = await _db.OrgNodes.Where(c => c.ParentId == guid).AsNoTracking()
                .OrderBy(c => c.SortOrder)
                .Select(c => new { c.Id, c.NodeType, c.Name, c.Code }).ToListAsync();

            return Ok(new
            {
                type = "org_node", id = n.Id,
                header = new
                {
                    n.Id, n.TenantCode, n.NodeType, n.Name, n.Code,
                    n.ParentId, n.CompanyId, n.SiteId, n.LocationId,
                    n.IsActive, n.SortOrder, n.CreatedAt,
                    child_count = children.Count()
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { n.Name, n.Code, n.NodeType, n.TenantCode } },
                    new { name = "hierarchy", fields = new { n.ParentId, n.CompanyId, n.SiteId, n.LocationId } },
                    new { name = "status", fields = new { n.IsActive, n.SortOrder, n.CreatedAt } }
                },
                related = new { children }
            });
        }

        private async Task<IActionResult> GetUserDetail(int id)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound(new { error = "User not found" });

            return Ok(new
            {
                type = "user", id = u.Id,
                header = new
                {
                    u.Username, u.FullName, u.Email, u.Role,
                    u.IsActive, u.Language, u.TimeZone,
                    u.CompanyId, u.CreatedAt, u.LastLoginAt,
                    u.MustChangePassword, u.PasswordChangedAt
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { u.Username, u.FullName, u.Email } },
                    new { name = "access", fields = new { u.Role, u.IsActive, u.CompanyId } },
                    new { name = "preferences", fields = new { u.Language, u.TimeZone, u.MustChangePassword } }
                },
                related = new { }
            });
        }

        private async Task<IActionResult> GetJournalEntryDetail(int id)
        {
            var j = await _db.JournalEntries.AsNoTracking()
                .Include(x => x.Book)
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (j == null) return NotFound(new { error = "Journal entry not found" });

            var lines = j.Lines.Select(l => new
            {
                l.Id, l.Account,
                l.Debit, l.Credit, l.Description
            }).ToList();

            return Ok(new
            {
                type = "journal_entry", id = j.Id,
                header = new
                {
                    j.Batch, j.Reference, j.Source, j.Description,
                    j.Period, j.PostingDate, j.CreatedUtc,
                    bookName = j.Book?.Name,
                    lineCount = j.Lines.Count,
                    totalDebits = j.Lines.Sum(l => l.Debit),
                    totalCredits = j.Lines.Sum(l => l.Credit)
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { j.Batch, j.Reference, j.Source, j.Description } },
                    new { name = "financial", fields = new { j.Period, j.PostingDate, totalDebits = j.Lines.Sum(l => l.Debit), totalCredits = j.Lines.Sum(l => l.Credit) } },
                    new { name = "configuration", fields = new { bookName = j.Book?.Name, j.BookId, j.CreatedUtc } }
                },
                related = new { lines, book = j.Book != null ? new { j.Book.Id, j.Book.Name } : null }
            });
        }

        private async Task<IActionResult> GetWorkRequestDetail(int id)
        {
            var wr = await _db.WorkRequests.AsNoTracking()
                .Include(x => x.Site)
                .Include(x => x.Location)
                .Include(x => x.Asset)
                .Include(x => x.Company)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (wr == null) return NotFound(new { error = "Work request not found" });

            return Ok(new
            {
                type = "work_request", id = wr.Id,
                header = new
                {
                    wr.RequestNumber, wr.RequestText, status = wr.Status.ToString(),
                    priority = wr.Priority.ToString(), wr.RequestedBy, wr.RequestedAt,
                    wr.ContactPhone, wr.ContactEmail,
                    siteName = wr.Site?.Name, locationName = wr.Location?.Name,
                    assetNumber = wr.Asset?.AssetNumber, companyName = wr.Company?.Name,
                    wr.IsAIAssisted, wr.AIConfidence,
                    wr.GeneratedWorkOrderId, wr.CompanyId
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { wr.RequestNumber, wr.RequestText, status = wr.Status.ToString(), priority = wr.Priority.ToString() } },
                    new { name = "contact", fields = new { wr.RequestedBy, wr.ContactPhone, wr.ContactEmail, wr.RequestedAt } },
                    new { name = "location", fields = new { siteName = wr.Site?.Name, locationName = wr.Location?.Name, assetNumber = wr.Asset?.AssetNumber, companyName = wr.Company?.Name } },
                    new { name = "ai_analysis", fields = new { wr.IsAIAssisted, wr.AIConfidence, wr.AIExplanation } }
                },
                related = new
                {
                    asset = wr.Asset != null ? new { wr.Asset.Id, wr.Asset.AssetNumber, wr.Asset.Description } : null,
                    work_order = wr.GeneratedWorkOrderId.HasValue ? new { id = wr.GeneratedWorkOrderId.Value } : null
                }
            });
        }

        private async Task<IActionResult> GetGlAccountDetail(int id)
        {
            var a = await _db.GlAccounts.AsNoTracking()
                .Include(x => x.Company)
                .Include(x => x.ParentAccount)
                .Include(x => x.ChildAccounts)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (a == null) return NotFound(new { error = "GL account not found" });

            var children = a.ChildAccounts.Select(c => new { c.Id, c.AccountNumber, c.Name, accountType = c.AccountType.ToString() }).ToList();

            return Ok(new
            {
                type = "gl_account", id = a.Id,
                header = new
                {
                    a.AccountNumber, a.Name, a.Description,
                    accountType = a.AccountType.ToString(),
                    category = a.Category.ToString(),
                    subCategory = a.SubCategory.ToString(),
                    normalBalance = a.NormalBalance.ToString(),
                    a.IsActive, a.IsSystemAccount, a.AllowManualEntry,
                    a.RequiresCostCenter, a.RequiresDepartment,
                    companyName = a.Company?.Name, a.CompanyId,
                    parentAccountNumber = a.ParentAccount?.AccountNumber
                },
                sections = new object[]
                {
                    new { name = "identification", fields = new { a.AccountNumber, a.Name, a.Description } },
                    new { name = "classification", fields = new { accountType = a.AccountType.ToString(), category = a.Category.ToString(), subCategory = a.SubCategory.ToString(), normalBalance = a.NormalBalance.ToString() } },
                    new { name = "configuration", fields = new { a.IsActive, a.IsSystemAccount, a.AllowManualEntry, a.RequiresCostCenter, a.RequiresDepartment } },
                    new { name = "hierarchy", fields = new { parentAccountNumber = a.ParentAccount?.AccountNumber, a.ParentAccountId, childCount = a.ChildAccounts.Count } }
                },
                related = new
                {
                    children,
                    parent = a.ParentAccount != null ? new { a.ParentAccount.Id, a.ParentAccount.AccountNumber, a.ParentAccount.Name } : null,
                    company = a.Company != null ? new { a.Company.Id, a.Company.Name } : null
                }
            });
        }
    }
}
