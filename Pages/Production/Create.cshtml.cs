using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Production;

[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Hybrid page — AppDbContext for lookup-dropdown hydration only; the POST handler routes through IProductionOrderService.CreateAsync.")]
// Sprint 13.5 PR #4 — /Production/Create
// New production order. Posts to IProductionOrderService.CreateAsync.
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IProductionOrderService _productionService;
    private readonly ITenantContext _tenantContext;

    public CreateModel(AppDbContext db, IProductionOrderService productionService, ITenantContext tenantContext)
    {
        _db = db;
        _productionService = productionService;
        _tenantContext = tenantContext;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IEnumerable<SelectListItem> LocationOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> CustomerOptions { get; private set; } = Array.Empty<SelectListItem>();
    public IEnumerable<SelectListItem> ItemOptions { get; private set; } = Array.Empty<SelectListItem>();

    public async Task OnGetAsync()
    {
        await HydrateLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await HydrateLookupsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new CreateProductionOrderRequest(
            OrderNumber:             Input.OrderNumber,
            Type:                    Input.Type,
            Title:                   Input.Title,
            Description:             Input.Description,
            ItemId:                  Input.ItemId,
            LocationId:              Input.LocationId,
            CustomerId:              Input.CustomerId,
            QuantityOrdered:         Input.QuantityOrdered,
            Uom:                     Input.Uom,
            ScheduledStart:          Input.ScheduledStart,
            ScheduledEnd:            Input.ScheduledEnd,
            Priority:                Input.Priority,
            MasterProductionOrderId: null,
            MaterialStructureId:     null,
            CreatedBy:               User?.Identity?.Name);

        var result = await _productionService.CreateAsync(request, HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return Page();
        }

        TempData["Success"] = $"Production order {result.Value!.OrderNumber} created.";
        return RedirectToPage("/Production/Details", new { id = result.Value!.Id });
    }

    private async Task HydrateLookupsAsync()
    {
        var visible = _tenantContext.VisibleCompanyIds;

        LocationOptions = (await _db.Locations
            .AsNoTracking()
            .Where(l => l.CompanyId != null && visible.Contains(l.CompanyId.Value) && l.IsActive)
            .OrderBy(l => l.Code)
            .Select(l => new { l.Id, l.Code, l.Name })
            .ToListAsync())
            .Select(l => new SelectListItem(value: l.Id.ToString(), text: $"{l.Code} — {l.Name}"));

        CustomerOptions = (await _db.Customers
            .AsNoTracking()
            .Where(c => visible.Contains(c.CompanyId) && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync())
            .Select(c => new SelectListItem(value: c.Id.ToString(), text: c.Name));

        ItemOptions = (await _db.Items
            .AsNoTracking()
            .Where(i => i.CompanyId != null && visible.Contains(i.CompanyId.Value))
            .OrderBy(i => i.PartNumber)
            .Take(500)
            .Select(i => new { i.Id, i.PartNumber, i.Description })
            .ToListAsync())
            .Select(i => new SelectListItem(value: i.Id.ToString(), text: $"{i.PartNumber} — {i.Description}"));
    }

    public sealed class InputModel
    {
        [Required, StringLength(32)]
        [Display(Name = "Order Number")]
        public string OrderNumber { get; set; } = string.Empty;

        public ProductionType Type { get; set; } = ProductionType.JobShop;

        [Required, StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [Display(Name = "Item")]
        public int? ItemId { get; set; }

        [Display(Name = "Location")]
        public int? LocationId { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Quantity Ordered")]
        [Range(0, double.MaxValue)]
        public decimal QuantityOrdered { get; set; } = 0;

        [StringLength(16)]
        public string? Uom { get; set; }

        [Display(Name = "Scheduled Start")]
        public DateTime? ScheduledStart { get; set; }

        [Display(Name = "Scheduled End")]
        public DateTime? ScheduledEnd { get; set; }

        [Range(0, 100)]
        public int Priority { get; set; } = 50;
    }
}
