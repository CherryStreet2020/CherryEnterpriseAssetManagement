using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Pages.Assets;

public class ImproveModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;

    public ImproveModel(AppDbContext db, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
    {
            _moduleGuard = moduleGuard;
        _db = db;
        _tenantContext = tenantContext;
    }

    public Asset? Asset { get; set; }
    public List<CapitalImprovement> PreviousImprovements { get; set; } = new();

    [BindProperty]
    public int AssetId { get; set; }

    [BindProperty]
    [Required]
    public DateTime ImprovementDate { get; set; } = DateTime.Today;

    [BindProperty]
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Cost must be greater than 0")]
    public decimal Cost { get; set; }

    [BindProperty]
    [StringLength(200)]
    public string? Vendor { get; set; }

    [BindProperty]
    [StringLength(100)]
    public string? InvoiceNumber { get; set; }

    [BindProperty]
    public int? UsefulLifeExtension { get; set; }

    [BindProperty]
    public bool Capitalize { get; set; } = true;

    [BindProperty]
    public string? Notes { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Assets" });

        Asset = await _db.Assets.Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).FirstOrDefaultAsync();
        if (Asset == null)
            return Page();

        AssetId = id;
        await LoadPreviousImprovementsAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Asset = await _db.Assets.Where(a => a.Id == AssetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).FirstOrDefaultAsync();
        if (Asset == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadPreviousImprovementsAsync(AssetId);
            return Page();
        }

        var improvement = new CapitalImprovement
        {
            AssetId = AssetId,
            ImprovementDate = ImprovementDate,
            Description = Description,
            Cost = Cost,
            Vendor = Vendor,
            InvoiceNumber = InvoiceNumber,
            UsefulLifeExtensionMonths = UsefulLifeExtension,
            Notes = Notes,
            Capitalized = Capitalize,
            CreatedAt = DateTime.UtcNow
        };

        _db.CapitalImprovements.Add(improvement);

        if (Capitalize)
        {
            Asset.AcquisitionCost += Cost;
        }

        if (UsefulLifeExtension.HasValue && UsefulLifeExtension.Value > 0)
        {
            Asset.UsefulLifeMonths = Asset.UsefulLifeMonths + UsefulLifeExtension.Value;
        }

        await _db.SaveChangesAsync();

        TempData["Message"] = $"Capital improvement of {Cost:C0} added to asset {Asset.AssetNumber}.";
        return RedirectToPage("./Asset", new { id = AssetId, mode = "view" });
    }

    private async Task LoadPreviousImprovementsAsync(int assetId)
    {
        PreviousImprovements = await _db.CapitalImprovements
            .Where(c => c.AssetId == assetId)
            .OrderByDescending(c => c.ImprovementDate)
            .ToListAsync();
    }
}
