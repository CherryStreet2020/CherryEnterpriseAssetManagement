using System.Threading.Tasks;
using Abs.FixedAssets.ControlPlane;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Production;

[ControlPlaneExempt("Hybrid page — AppDbContext for GET projection only; every POST handler routes through IProductionOrderService.")]
// Sprint 13.5 PR #4 — /Production/Details/{id}
// Header view + status-transition action handlers. Every mutation goes
// through IProductionOrderService (PR #3).
public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IProductionOrderService _productionService;

    public DetailsModel(AppDbContext db, IProductionOrderService productionService)
    {
        _db = db;
        _productionService = productionService;
    }

    public ProductionOrder? Order { get; private set; }
    public string? StatusMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Order = await _db.ProductionOrders
            .AsNoTracking()
            .Include(p => p.Item)
            .Include(p => p.Location)
            .Include(p => p.Customer)
            .Include(p => p.CustomerProject)
            .Include(p => p.ProjectPhase)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (Order == null)
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostTransitionAsync(int id, ProductionOrderStatus newStatus)
    {
        var result = await _productionService.UpdateStatusAsync(
            new UpdateProductionOrderStatusRequest(id, newStatus, User?.Identity?.Name),
            HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
        }
        else
        {
            TempData["Success"] = $"Status updated to {newStatus}.";
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUnassignFromProjectAsync(int id)
    {
        var result = await _productionService.UnassignFromProjectAsync(
            new UnassignFromProjectRequest(id, User?.Identity?.Name),
            HttpContext.RequestAborted);

        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
        }
        else
        {
            TempData["Success"] = "Order unassigned from project.";
        }

        return RedirectToPage(new { id });
    }
}
