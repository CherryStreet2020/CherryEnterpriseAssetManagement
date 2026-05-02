using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;

namespace Abs.FixedAssets.Pages.Maintenance.WorkRequests;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILookupService _lookupService;
    private readonly IModuleGuardService _moduleGuard;

    public IndexModel(AppDbContext db, ITenantContext tenantContext, ILookupService lookupService, IModuleGuardService moduleGuard)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lookupService = lookupService;
        _moduleGuard = moduleGuard;
    }

    public List<WorkRequest> WorkRequests { get; set; } = new();
    public List<SelectListItem> StatusOptions { get; set; } = new();
    public List<WorkRequest> NewRequests { get; set; } = new();
    public List<WorkRequest> InReviewRequests { get; set; } = new();
    public List<WorkRequest> ApprovedRequests { get; set; } = new();
    public List<WorkRequest> ConvertedRequests { get; set; } = new();
    
    public int TotalCount { get; set; }
    public int NewCount { get; set; }
    public int InReviewCount { get; set; }
    public int ConvertedCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? View { get; set; }

    public string ViewMode => string.Equals(View, "list", StringComparison.OrdinalIgnoreCase) ? "List" : "Board";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
            return RedirectToPage("/ModuleDisabled", new { module = "Work Requests" });

        StatusOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "WorkRequestStatus", null, "All Statuses");


        var query = _db.WorkRequests
            .Include(r => r.Site)
            .Include(r => r.Asset)
            .Include(r => r.GeneratedWorkOrder)
            .Where(r => _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0));

        if (_tenantContext.SiteId.HasValue)
            query = query.Where(r => r.SiteId == _tenantContext.SiteId.Value);

        if (!string.IsNullOrEmpty(StatusFilter))
        {
            if (Enum.TryParse<WorkRequestStatus>(StatusFilter, out var status))
            {
                query = query.Where(r => r.Status == status);
            }
        }

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var term = SearchTerm.ToLower();
            query = query.Where(r =>
                r.RequestNumber.ToLower().Contains(term) ||
                r.RequestText.ToLower().Contains(term) ||
                (r.RequestedBy != null && r.RequestedBy.ToLower().Contains(term)));
        }

        WorkRequests = await query
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .ToListAsync();

        NewRequests = WorkRequests.Where(r => r.Status == WorkRequestStatus.New).ToList();
        InReviewRequests = WorkRequests.Where(r => r.Status == WorkRequestStatus.InReview).ToList();
        ApprovedRequests = WorkRequests.Where(r => r.Status == WorkRequestStatus.Approved).ToList();
        ConvertedRequests = WorkRequests.Where(r => r.Status == WorkRequestStatus.ConvertedToWO).ToList();

        var wrCountBase = _db.WorkRequests.Where(r => _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0));
        if (_tenantContext.SiteId.HasValue)
            wrCountBase = wrCountBase.Where(r => r.SiteId == _tenantContext.SiteId.Value);
        TotalCount = await wrCountBase.CountAsync();
        NewCount = await wrCountBase.CountAsync(r => r.Status == WorkRequestStatus.New);
        InReviewCount = await wrCountBase.CountAsync(r => r.Status == WorkRequestStatus.InReview);
        ConvertedCount = await wrCountBase.CountAsync(r => r.Status == WorkRequestStatus.ConvertedToWO);
        ApprovedCount = await wrCountBase.CountAsync(r => r.Status == WorkRequestStatus.Approved);
        RejectedCount = await wrCountBase.CountAsync(r => r.Status == WorkRequestStatus.Rejected);

        return Page();
    }
}
