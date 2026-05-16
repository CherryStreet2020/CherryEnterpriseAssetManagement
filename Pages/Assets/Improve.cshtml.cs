using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Pages.Assets;

public class ImproveModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;
    private readonly IPeriodGuard _periodGuard;
    private readonly DepreciationBackfillService _depBackfill;
    private readonly IOutboxWriter _outbox;
    // PR #102 (B-10): improvements now post a JE alongside the AcquisitionCost
    // bump. The previous code path silently broke trial balance on every
    // capitalized improvement.
    private readonly ICapitalImprovementPostingService _improvementPosting;

    public ImproveModel(AppDbContext db, ITenantContext tenantContext,
            IModuleGuardService moduleGuard, IPeriodGuard periodGuard,
            DepreciationBackfillService depBackfill, IOutboxWriter outbox,
            ICapitalImprovementPostingService improvementPosting)
    {
        _moduleGuard = moduleGuard;
        _db = db;
        _tenantContext = tenantContext;
        _periodGuard = periodGuard;
        _depBackfill = depBackfill;
        _outbox = outbox;
        _improvementPosting = improvementPosting;
    }

    public string? ErrorMessage { get; set; }

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
    [Range(1, 600, ErrorMessage = "Useful life extension must be at least 1 month")]
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

        var assetCompanyId = Asset.CompanyId ?? _tenantContext.CompanyId ?? 0;
        if (assetCompanyId > 0)
        {
            var periodCheck = await _periodGuard.CanPostAsync(assetCompanyId, ImprovementDate);
            if (!periodCheck.IsAllowed)
            {
                ModelState.AddModelError(nameof(ImprovementDate), periodCheck.Reason ?? "Posting period is not open.");
                ErrorMessage = periodCheck.Reason;
                await LoadPreviousImprovementsAsync(AssetId);
                return Page();
            }
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

        // PR #102 (B-10): post the GL entry for the improvement. Pre-fix,
        // Asset.AcquisitionCost was bumped above but the GL never saw it —
        // trial balance drifted silently. Service posts DR AssetCost / CR
        // CipPending and refuses on a closed period. Only post when the
        // improvement was actually capitalized (Capitalize == true); a
        // non-capitalized improvement is a metadata-only event with no
        // basis change and so no GL impact.
        if (Capitalize)
        {
            try
            {
                await _improvementPosting.PostImprovementJeAsync(
                    improvementId: improvement.Id,
                    assetId: AssetId,
                    companyId: assetCompanyId,
                    amount: Cost,
                    improvementDate: ImprovementDate,
                    description: Description);
            }
            catch (InvalidOperationException ex)
            {
                // Period-guard refusal from the posting service. The form-level
                // guard upstream should catch this, but defending again here
                // surfaces the message instead of crashing the page.
                ModelState.AddModelError(nameof(ImprovementDate), ex.Message);
                ErrorMessage = ex.Message;
                await LoadPreviousImprovementsAsync(AssetId);
                return Page();
            }
        }

        // Refresh the cached depreciation snapshot on Asset and each
        // AssetBookSettings row so subsequent reads (asset detail page,
        // dashboard KPIs, schedule reports) reflect the new cost basis and
        // useful life. Posted JournalEntries are append-only and untouched
        // — only the running totals get restamped, plus the future-month
        // schedule changes.
        await _depBackfill.RecomputeAssetAsync(AssetId, ImprovementDate);

        await _outbox.EnqueueAsync(
            Asset.CompanyId ?? 0,
            siteId: Asset.SiteId,
            new AssetImprovedV1(
                AssetId: Asset.Id,
                AssetNumber: Asset.AssetNumber,
                CapitalImprovementId: improvement.Id,
                ImprovementDate: ImprovementDate,
                Description: Description,
                Cost: Cost,
                Capitalized: Capitalize,
                UsefulLifeExtensionMonths: UsefulLifeExtension,
                NewAcquisitionCost: Asset.AcquisitionCost,
                NewUsefulLifeMonths: Asset.UsefulLifeMonths,
                CompanyId: Asset.CompanyId,
                Vendor: Vendor,
                InvoiceNumber: InvoiceNumber),
            correlationId: $"asset-improve-{improvement.Id}"
        );

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
