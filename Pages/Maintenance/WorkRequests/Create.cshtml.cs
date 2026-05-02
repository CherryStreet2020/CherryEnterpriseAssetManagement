using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Maintenance;
using System.Text.Json;

namespace Abs.FixedAssets.Pages.Maintenance.WorkRequests;

public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWorkRequestConversionService _conversionService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<CreateModel> _logger;
    private readonly IModuleGuardService _moduleGuard;

    public CreateModel(AppDbContext db, IWorkRequestConversionService conversionService, ITenantContext tenantContext, ILogger<CreateModel> logger,
            IModuleGuardService moduleGuard)
    {
            _moduleGuard = moduleGuard;
        _db = db;
        _conversionService = conversionService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [BindProperty]
    public WorkRequest WorkRequest { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SelectedSiteId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SelectedLocationId { get; set; }

    [BindProperty]
    public string? IssueSummary { get; set; }

    [BindProperty]
    public string? Symptoms { get; set; }

    [BindProperty]
    public string? ErrorCode { get; set; }

    [BindProperty]
    public bool IsDowntime { get; set; }

    [BindProperty]
    public bool IsSafetyRisk { get; set; }

    [BindProperty]
    public DateTime? StartedAt { get; set; }

    [BindProperty]
    public string? AssetHint { get; set; }

    [BindProperty]
    public string? AttachmentNotes { get; set; }

    public SelectList Sites { get; set; } = null!;
    public SelectList Locations { get; set; } = null!;
    public SelectList Assets { get; set; } = null!;

    public bool HasSites { get; set; }
    public bool HasLocationsForSite { get; set; }
    public bool HasAssetsForSite { get; set; }
    public bool IsSiteSelected => SelectedSiteId.HasValue;

    public SmartAssistResult? AssistResult { get; set; }
    public MaintenanceEvent? GeneratedWorkOrder { get; set; }
    public bool ShowAssistPanel { get; set; } = false;

    public int ReadinessScore { get; set; }
    public List<string> ReadinessItems { get; set; } = new();

    private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

    public async Task<IActionResult> OnGetAsync()
    {
            if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Maintenance" });

        NormalizeFilterState();
        await LoadDropdownsAsync();
        SyncWorkRequestFromFilters();
        CalculateReadiness();
        return Page();
    }

    public async Task<IActionResult> OnGetLocationsJsonAsync(int siteId)
    {
        var companyId = GetCompanyId();
        
        var locations = await _db.Locations
            .Where(l => _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0) && l.SiteId == siteId && l.IsActive)
            .OrderBy(l => l.Name)
            .Select(l => new { id = l.Id, label = l.Code + " - " + l.Name })
            .Take(500)
            .ToListAsync();

        if (!locations.Any())
        {
            locations = await _db.Locations
                .Where(l => _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0) && l.SiteId == siteId)
                .OrderBy(l => l.Name)
                .Select(l => new { id = l.Id, label = l.Code + " - " + l.Name })
                .Take(500)
                .ToListAsync();
        }

        return new JsonResult(locations);
    }

    public async Task<IActionResult> OnGetAssetsJsonAsync(int siteId, int? locationId)
    {
        var companyId = GetCompanyId();

        var query = _db.Assets
            .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.SiteId == siteId && a.Status == AssetStatus.Active);

        if (locationId.HasValue)
        {
            query = query.Where(a => a.LocationId == locationId.Value);
        }

        var assets = await query
            .OrderBy(a => a.Description)
            .Select(a => new { id = a.Id, label = a.AssetNumber + " — " + a.Description })
            .Take(500)
            .ToListAsync();

        if (!assets.Any())
        {
            query = _db.Assets.Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.SiteId == siteId);
            if (locationId.HasValue)
            {
                query = query.Where(a => a.LocationId == locationId.Value);
            }
            assets = await query
                .OrderBy(a => a.Description)
                .Select(a => new { id = a.Id, label = a.AssetNumber + " — " + a.Description })
                .Take(500)
                .ToListAsync();
        }

        return new JsonResult(assets);
    }

    public async Task<IActionResult> OnPostSaveOnlyAsync()
    {
        return await SaveWorkRequestAsync(useSmartAssist: false);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        return await SaveWorkRequestAsync(useSmartAssist: true);
    }

    private void NormalizeFilterState()
    {
        if (!SelectedSiteId.HasValue)
        {
            SelectedLocationId = null;
        }
    }

    private void SyncWorkRequestFromFilters()
    {
        WorkRequest.SiteId = SelectedSiteId;
        WorkRequest.LocationId = SelectedLocationId;
    }

    private async Task<IActionResult> SaveWorkRequestAsync(bool useSmartAssist)
    {
        SyncWorkRequestFromFilters();

        if (string.IsNullOrWhiteSpace(WorkRequest.RequestText) && string.IsNullOrWhiteSpace(IssueSummary))
        {
            ModelState.AddModelError("WorkRequest.RequestText", "Please describe the issue or request.");
            await LoadDropdownsAsync();
            CalculateReadiness();
            return Page();
        }

        var companyId = GetCompanyId();

        if (WorkRequest.SiteId.HasValue)
        {
            var siteValid = await _db.Sites.AnyAsync(s => s.Id == WorkRequest.SiteId && _tenantContext.VisibleCompanyIds.Contains(s.CompanyId));
            if (!siteValid)
            {
                ModelState.AddModelError("", "Invalid site selection for your scope.");
                await LoadDropdownsAsync();
                CalculateReadiness();
                return Page();
            }
        }

        if (WorkRequest.LocationId.HasValue && WorkRequest.SiteId.HasValue)
        {
            var locationValid = await _db.Locations.AnyAsync(l => l.Id == WorkRequest.LocationId && l.SiteId == WorkRequest.SiteId && _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0));
            if (!locationValid)
            {
                ModelState.AddModelError("", "Selected location does not belong to the selected site.");
                WorkRequest.LocationId = null;
                SelectedLocationId = null;
                await LoadDropdownsAsync();
                CalculateReadiness();
                return Page();
            }
        }

        if (WorkRequest.AssetId.HasValue && WorkRequest.SiteId.HasValue)
        {
            var assetQuery = _db.Assets.Where(a => a.Id == WorkRequest.AssetId && a.SiteId == WorkRequest.SiteId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0));
            if (WorkRequest.LocationId.HasValue)
            {
                assetQuery = assetQuery.Where(a => a.LocationId == WorkRequest.LocationId);
            }
            if (!await assetQuery.AnyAsync())
            {
                ModelState.AddModelError("", "Selected asset does not belong to the selected site/location.");
                WorkRequest.AssetId = null;
                await LoadDropdownsAsync();
                CalculateReadiness();
                return Page();
            }
        }

        BuildStructuredRequestText();

        WorkRequest.CompanyId = companyId;
        WorkRequest.RequestNumber = await GenerateRequestNumberAsync(companyId);
        WorkRequest.Status = WorkRequestStatus.New;
        WorkRequest.RequestedAt = DateTime.UtcNow;
        WorkRequest.RequestedBy ??= User.Identity?.Name ?? "System";
        WorkRequest.CreatedBy = User.Identity?.Name ?? "System";
        WorkRequest.CreatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(AttachmentNotes))
        {
            WorkRequest.AttachmentPaths = AttachmentNotes;
        }

        _db.WorkRequests.Add(WorkRequest);
        await _db.SaveChangesAsync();

        if (useSmartAssist)
        {
            return await RunSmartAssistAsync();
        }

        TempData["Success"] = $"Work Request {WorkRequest.RequestNumber} created successfully.";
        return RedirectToPage("./Index");
    }

    private async Task<IActionResult> RunSmartAssistAsync()
    {
        var username = User.Identity?.Name ?? "System";

        try
        {
            var result = await _conversionService.ConvertWithSmartAssistAsync(WorkRequest, username);
            AssistResult = result.AssistResult;
            ShowAssistPanel = true;

            if (result.Success && result.WorkOrderId.HasValue)
            {
                TempData["Success"] = $"Smart Assist generated Draft Work Order {result.WorkOrderNumber}";
                return RedirectToPage("/Maintenance/Details", new { id = result.WorkOrderId });
            }
            else
            {
                TempData["Warning"] = result.Error ?? "Smart Assist could not determine an asset. Please select an asset manually.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Smart Assist failed for Work Request {Id}", WorkRequest.Id);
            TempData["Error"] = "Smart Assist encountered an error. Please try again or submit manually.";
        }

        await LoadDropdownsAsync();
        CalculateReadiness();
        return Page();
    }

    private void BuildStructuredRequestText()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(IssueSummary))
        {
            parts.Add($"**Summary:** {IssueSummary.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(Symptoms))
        {
            parts.Add($"**Symptoms:** {Symptoms.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(ErrorCode))
        {
            parts.Add($"**Error Code:** {ErrorCode.Trim()}");
        }

        if (IsDowntime)
        {
            parts.Add("**Impact:** Equipment is DOWN / not operational");
        }

        if (IsSafetyRisk)
        {
            parts.Add("**Safety:** Potential safety hazard identified");
        }

        if (StartedAt.HasValue)
        {
            parts.Add($"**Started:** {StartedAt.Value:g}");
        }

        if (!string.IsNullOrWhiteSpace(AssetHint))
        {
            parts.Add($"**Asset Hint:** {AssetHint.Trim()}");
        }

        if (parts.Count > 0)
        {
            var structuredHeader = string.Join("\n", parts);
            if (!string.IsNullOrWhiteSpace(WorkRequest.RequestText))
            {
                WorkRequest.RequestText = structuredHeader + "\n\n---\n\n" + WorkRequest.RequestText.Trim();
            }
            else
            {
                WorkRequest.RequestText = structuredHeader;
            }
        }

        var contextData = new
        {
            IssueSummary,
            Symptoms,
            ErrorCode,
            IsDowntime,
            IsSafetyRisk,
            StartedAt,
            AssetHint,
            SiteId = WorkRequest.SiteId,
            LocationId = WorkRequest.LocationId,
            AssetId = WorkRequest.AssetId,
            Priority = WorkRequest.Priority.ToString()
        };
        WorkRequest.AIExplanation = JsonSerializer.Serialize(contextData);
    }

    private void CalculateReadiness()
    {
        ReadinessItems.Clear();
        var score = 0;

        if (!string.IsNullOrWhiteSpace(IssueSummary) || !string.IsNullOrWhiteSpace(WorkRequest.RequestText))
        {
            score += 20;
            ReadinessItems.Add("Description provided");
        }

        if (SelectedSiteId.HasValue)
        {
            score += 15;
            ReadinessItems.Add("Site selected");
        }

        if (WorkRequest.AssetId.HasValue || !string.IsNullOrWhiteSpace(AssetHint))
        {
            score += 25;
            ReadinessItems.Add(WorkRequest.AssetId.HasValue ? "Asset selected" : "Asset hint provided");
        }

        if (!string.IsNullOrWhiteSpace(Symptoms))
        {
            score += 15;
            ReadinessItems.Add("Symptoms described");
        }

        if (IsDowntime || IsSafetyRisk)
        {
            score += 10;
            ReadinessItems.Add("Impact/urgency indicated");
        }

        if (!string.IsNullOrWhiteSpace(ErrorCode))
        {
            score += 10;
            ReadinessItems.Add("Error code captured");
        }

        if (StartedAt.HasValue)
        {
            score += 5;
            ReadinessItems.Add("Start time provided");
        }

        ReadinessScore = Math.Min(score, 100);
    }

    private async Task<string> GenerateRequestNumberAsync(int companyId)
    {
        var today = DateTime.UtcNow;
        var prefix = $"WR-{today:yyyyMM}-";
        var count = await _db.WorkRequests
            .CountAsync(r => _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0) && r.RequestNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task LoadDropdownsAsync()
    {
        var companyId = GetCompanyId();

        var sites = await _db.Sites
            .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId) && s.Status == SiteStatus.Active)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();

        if (!sites.Any())
        {
            sites = await _db.Sites
                .Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId))
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();
        }

        HasSites = sites.Any();
        Sites = new SelectList(sites, "Id", "Name", SelectedSiteId);

        if (SelectedSiteId.HasValue)
        {
            var locations = await _db.Locations
                .Where(l => _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0) && l.SiteId == SelectedSiteId.Value && l.IsActive)
                .OrderBy(l => l.Name)
                .Select(l => new { l.Id, Display = l.Code + " - " + l.Name })
                .Take(500)
                .ToListAsync();

            if (!locations.Any())
            {
                locations = await _db.Locations
                    .Where(l => _tenantContext.VisibleCompanyIds.Contains(l.CompanyId ?? 0) && l.SiteId == SelectedSiteId.Value)
                    .OrderBy(l => l.Name)
                    .Select(l => new { l.Id, Display = l.Code + " - " + l.Name })
                    .Take(500)
                    .ToListAsync();
            }

            HasLocationsForSite = locations.Any();
            Locations = new SelectList(locations, "Id", "Display", SelectedLocationId);

            var assetQuery = _db.Assets
                .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.SiteId == SelectedSiteId.Value && a.Status == AssetStatus.Active);

            if (SelectedLocationId.HasValue)
            {
                assetQuery = assetQuery.Where(a => a.LocationId == SelectedLocationId.Value);
            }

            var assets = await assetQuery
                .OrderBy(a => a.Description)
                .Select(a => new { a.Id, Display = a.AssetNumber + " - " + a.Description })
                .Take(500)
                .ToListAsync();

            if (!assets.Any())
            {
                assetQuery = _db.Assets
                    .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.SiteId == SelectedSiteId.Value);

                if (SelectedLocationId.HasValue)
                {
                    assetQuery = assetQuery.Where(a => a.LocationId == SelectedLocationId.Value);
                }

                assets = await assetQuery
                    .OrderBy(a => a.Description)
                    .Select(a => new { a.Id, Display = a.AssetNumber + " - " + a.Description })
                    .Take(500)
                    .ToListAsync();
            }

            HasAssetsForSite = assets.Any();
            Assets = new SelectList(assets, "Id", "Display", WorkRequest.AssetId);
        }
        else
        {
            HasLocationsForSite = false;
            HasAssetsForSite = false;
            Locations = new SelectList(Enumerable.Empty<object>(), "Id", "Display");
            Assets = new SelectList(Enumerable.Empty<object>(), "Id", "Display");
        }
    }
}
