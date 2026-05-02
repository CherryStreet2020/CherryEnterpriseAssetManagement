using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Pages.Assets;

public class TransferModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILookupService _lookupService;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;

    public TransferModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
    {
            _moduleGuard = moduleGuard;
        _db = db;
        _lookupService = lookupService;
        _tenantContext = tenantContext;
    }

    public Asset? Asset { get; set; }
    public List<Location> LocationsList { get; set; } = new();
    public List<Department> DepartmentsList { get; set; } = new();
    public List<SelectListItem> TransferReasonOptions { get; set; } = new();

    [BindProperty]
    public int AssetId { get; set; }

    [BindProperty]
    [Required]
    public int NewLocationId { get; set; }

    [BindProperty]
    public string? NewBay { get; set; }

    [BindProperty]
    public int? NewDepartmentId { get; set; }

    [BindProperty]
    [Required]
    public DateTime TransferDate { get; set; } = DateTime.Today;

    [BindProperty]
    public string? TransferReason { get; set; }

    [BindProperty]
    public int? TransferReasonLookupValueId { get; set; }

    [BindProperty]
    public string? Notes { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Assets" });

        Asset = await _db.Assets.Include(a => a.LocationRef).Include(a => a.DepartmentRef).Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (Asset == null)
            return Page();

        AssetId = id;
        await LoadDropdownsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Asset = await _db.Assets.Include(a => a.LocationRef).Include(a => a.DepartmentRef).Where(a => a.Id == AssetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (Asset == null)
            return NotFound();

        var newLocation = await _db.Locations.Where(l => l.Id == NewLocationId && _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0)).FirstOrDefaultAsync();
        if (newLocation == null)
        {
            ModelState.AddModelError("NewLocationId", "Please select a valid location.");
            await LoadDropdownsAsync();
            return Page();
        }

        if (TransferReasonLookupValueId.HasValue)
        {
            var lv = await _lookupService.GetValueByIdAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId, TransferReasonLookupValueId.Value);
            if (lv != null) TransferReason = lv.Code;
        }

        var oldLocationName = Asset.LocationRef?.Name;
        var oldBay = Asset.Bay;
        var oldDepartment = Asset.DepartmentRef?.Name;

        var newDepartment = NewDepartmentId.HasValue ? await _db.Departments.Where(d => d.Id == NewDepartmentId.Value && _tenantContext.VisibleCompanyIds.Contains(d.CompanyId ?? 0)).FirstOrDefaultAsync() : null;

        Asset.LocationId = NewLocationId;
        Asset.Bay = NewBay;
        Asset.DepartmentId = NewDepartmentId;

        var transfer = new AssetTransfer
        {
            AssetId = AssetId,
            TransferDate = TransferDate,
            FromLocation = oldLocationName,
            FromBay = oldBay,
            FromDepartment = oldDepartment,
            ToLocation = newLocation.Name,
            ToBay = NewBay,
            ToDepartment = newDepartment?.Name,
            Reason = TransferReason,
            ReasonLookupValueId = TransferReasonLookupValueId,
            Notes = Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.AssetTransfers.Add(transfer);
        await _db.SaveChangesAsync();

        TempData["Message"] = $"Asset {Asset.AssetNumber} successfully transferred to {newLocation.Name}.";
        return RedirectToPage("./Asset", new { id = AssetId, mode = "view" });
    }

    private async Task LoadDropdownsAsync()
    {
        LocationsList = await _db.Locations
            .Where(l => l.IsActive && _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0))
            .OrderBy(l => l.SortOrder)
            .ToListAsync();

        DepartmentsList = await _db.Departments
            .Where(d => d.IsActive && _tenantContext.VisibleCompanyIds.Contains(d.CompanyId ?? 0))
            .OrderBy(d => d.SortOrder)
            .ToListAsync();

        TransferReasonOptions = await _lookupService.GetSelectListByIdAsync(
            _tenantContext.TenantId, _tenantContext.CompanyId,
            "TransferReason", TransferReasonLookupValueId);
    }
}
