using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.ControlPlane;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Production;

[ControlPlaneExempt("Read-only list page — no mutations. AppDbContext used only for projection/filtering of ProductionOrder rows.")]
// Sprint 13.5 PR #4 — /Production cockpit landing page.
//
// First customer-visible surface on ProductionOrder. List view with
// status filter chips + table of orders + "New Order" CTA.
//
// Reads only — every mutation goes through IProductionOrderService
// (PR #3) so the page model stays CHERRY025-clean (no AppDbContext
// mutations in the page).
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public IndexModel(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public IReadOnlyList<ProductionOrderRow> Orders { get; private set; } = new List<ProductionOrderRow>();
    public ProductionOrderStatus? FilterStatus { get; private set; }
    public Dictionary<ProductionOrderStatus, int> CountByStatus { get; private set; } = new();
    public int TotalCount { get; private set; }

    public async Task OnGetAsync(string? status)
    {
        if (!string.IsNullOrWhiteSpace(status)
            && System.Enum.TryParse<ProductionOrderStatus>(status, ignoreCase: true, out var parsed))
        {
            FilterStatus = parsed;
        }

        // ProductionOrder has no direct CompanyId yet — scope through
        // Location.CompanyId, fall back to Customer.CompanyId. Matches
        // the IProductionOrderService tenant-scoping rule.
        var visible = _tenantContext.VisibleCompanyIds;

        var baseQuery = _db.ProductionOrders
            .AsNoTracking()
            .Include(p => p.Location)
            .Include(p => p.Customer)
            .Include(p => p.Item)
            .Include(p => p.CustomerProject)
            .Where(p =>
                (p.Location != null && p.Location.CompanyId != null && visible.Contains(p.Location.CompanyId.Value)) ||
                (p.Customer != null && visible.Contains(p.Customer.CompanyId)));

        // KPI counts (per status) computed BEFORE the status filter so the
        // chips show the true universe, not the filtered subset.
        var counts = await baseQuery
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        TotalCount = counts.Sum(c => c.Count);
        CountByStatus = counts.ToDictionary(c => c.Status, c => c.Count);

        var filtered = FilterStatus.HasValue
            ? baseQuery.Where(p => p.Status == FilterStatus.Value)
            : baseQuery;

        Orders = await filtered
            .OrderByDescending(p => p.CreatedAt)
            .Take(200)
            .Select(p => new ProductionOrderRow
            {
                Id              = p.Id,
                OrderNumber     = p.OrderNumber,
                Title           = p.Title,
                Status          = p.Status,
                Type            = p.Type,
                QuantityOrdered = p.QuantityOrdered,
                QuantityCompleted = p.QuantityCompleted,
                ScheduledStart  = p.ScheduledStart,
                ScheduledEnd    = p.ScheduledEnd,
                ItemCode        = p.Item != null ? p.Item.PartNumber : null,
                LocationCode    = p.Location != null ? p.Location.Code : null,
                CustomerName    = p.Customer != null ? p.Customer.Name : null,
                ProjectId       = p.CustomerProjectId,
                ProjectCode     = p.CustomerProject != null ? p.CustomerProject.Code : null,
            })
            .ToListAsync();
    }

    public sealed class ProductionOrderRow
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public ProductionOrderStatus Status { get; set; }
        public ProductionType Type { get; set; }
        public decimal QuantityOrdered { get; set; }
        public decimal QuantityCompleted { get; set; }
        public System.DateTime? ScheduledStart { get; set; }
        public System.DateTime? ScheduledEnd { get; set; }
        public string? ItemCode { get; set; } // Item.Code (renamed in projection for UI clarity)
        public string? LocationCode { get; set; }
        public string? CustomerName { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectCode { get; set; }
    }
}
