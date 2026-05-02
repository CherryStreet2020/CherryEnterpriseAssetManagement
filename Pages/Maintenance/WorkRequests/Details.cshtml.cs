using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Maintenance;
using Abs.FixedAssets.Services.Navigation;

namespace Abs.FixedAssets.Pages.Maintenance.WorkRequests;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWorkRequestConversionService _conversionService;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;

    public DetailsModel(AppDbContext db, IWorkRequestConversionService conversionService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
    {
            _moduleGuard = moduleGuard;
        _db = db;
        _conversionService = conversionService;
        _tenantContext = tenantContext;
    }

    public WorkRequest WorkRequest { get; set; } = null!;
    public SmartAssistResult? AssistResult { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string GetBackUrl() => ReturnUrlHelper.GetBackUrl(ReturnUrl, "/Maintenance/WorkRequests/Details");

    public async Task<IActionResult> OnGetAsync(int id)
    {
            if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Maintenance" });


        var request = await _db.WorkRequests
            .Include(r => r.Site)
            .Include(r => r.Asset)
            .Include(r => r.GeneratedWorkOrder)
            .FirstOrDefaultAsync(r => r.Id == id && _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || r.SiteId == _tenantContext.SiteId.Value));

        if (request == null)
        {
            return NotFound();
        }

        WorkRequest = request;
        return Page();
    }

    public async Task<IActionResult> OnPostSmartAssistAsync(int id)
    {

        var request = await _db.WorkRequests
            .FirstOrDefaultAsync(r => r.Id == id && _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || r.SiteId == _tenantContext.SiteId.Value));

        if (request == null)
        {
            return NotFound();
        }

        var username = User.Identity?.Name ?? "System";
        var result = await _conversionService.ConvertWithSmartAssistAsync(id, username);

        if (result.Success && result.WorkOrderId.HasValue)
        {
            TempData["Success"] = $"Generated Draft Work Order {result.WorkOrderNumber}";
            return RedirectToPage("/Maintenance/Details", new { id = result.WorkOrderId });
        }

        TempData["Warning"] = result.Error ?? "Could not determine asset. Please select one manually.";
        WorkRequest = request;
        return Page();
    }

    public async Task<IActionResult> OnPostConvertToWOAsync(int id)
    {

        var request = await _db.WorkRequests
            .Include(r => r.Site)
            .Include(r => r.Asset)
            .Include(r => r.GeneratedWorkOrder)
            .FirstOrDefaultAsync(r => r.Id == id && _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || r.SiteId == _tenantContext.SiteId.Value));

        if (request == null)
        {
            return NotFound();
        }

        if (request.Status == WorkRequestStatus.ConvertedToWO && request.GeneratedWorkOrderId.HasValue)
        {
            TempData["Warning"] = "This work request has already been converted to a work order.";
            WorkRequest = request;
            return Page();
        }

        if (request.Status == WorkRequestStatus.Rejected || request.Status == WorkRequestStatus.Cancelled)
        {
            TempData["Warning"] = $"Cannot convert a {request.Status} work request.";
            WorkRequest = request;
            return Page();
        }

        if (!request.AssetId.HasValue)
        {
            TempData["Warning"] = "Cannot convert: no asset assigned. Please assign an asset first or use Smart Assist.";
            WorkRequest = request;
            return Page();
        }

        var username = User.Identity?.Name ?? "System";
        var result = await _conversionService.ConvertWithSmartAssistAsync(id, username);

        if (result.Success && result.WorkOrderId.HasValue)
        {
            TempData["Success"] = $"Work Request converted to Work Order {result.WorkOrderNumber}";
            return RedirectToPage("/Maintenance/Details", new { id = result.WorkOrderId });
        }

        TempData["Warning"] = result.Error ?? "Conversion failed. Please try again.";
        WorkRequest = request;
        return Page();
    }
}
