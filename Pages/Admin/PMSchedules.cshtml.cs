using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Maintenance;
using Microsoft.AspNetCore.Authorization;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant")]
    public class PMSchedulesModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IPMSchedulerService _scheduler;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService _lookupService;

        public PMSchedulesModel(AppDbContext db, IPMSchedulerService scheduler, ITenantContext tenantContext, ILookupService lookupService)
        {
            _db = db;
            _scheduler = scheduler;
            _tenantContext = tenantContext;
            _lookupService = lookupService;
        }

        public List<PMSchedule> Schedules { get; set; } = new();
        public List<PMGenerationPreview> Previews { get; set; } = new();
        public PMGenerationResult? GenerationResult { get; set; }
        public List<SelectListItem> HorizonOptions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int HorizonDays { get; set; } = 7;

        public string? SuccessMessage => TempData["Success"]?.ToString();
        public string? ErrorMessage => TempData["Error"]?.ToString();

        public async Task OnGetAsync()
        {
            HorizonOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "PMFrequency", null, "");

            var query = _db.PMSchedules
                .Include(s => s.PMTemplate)
                .Include(s => s.Company)
                .Include(s => s.Site)
                .AsQueryable();

            if (_tenantContext.TenantId.HasValue)
                query = query.Where(s => s.TenantId == _tenantContext.TenantId);
            if (_tenantContext.CompanyId.HasValue)
                query = query.Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                query = query.Where(s => s.SiteId == _tenantContext.SiteId);

            Schedules = await query.OrderBy(s => s.Name).ToListAsync();

            Previews = await _scheduler.PreviewDueAsync(HorizonDays, DateTime.UtcNow, 
                _tenantContext.TenantId, _tenantContext.CompanyId, _tenantContext.SiteId);
        }

        public async Task<IActionResult> OnPostGenerateAsync()
        {
            var userId = User.Identity?.Name ?? "System";
            GenerationResult = await _scheduler.GenerateDueAsync(HorizonDays, DateTime.UtcNow, userId,
                _tenantContext.TenantId, _tenantContext.CompanyId, _tenantContext.SiteId);

            if (GenerationResult.CreatedCount > 0)
            {
                TempData["Success"] = $"Generated {GenerationResult.CreatedCount} work order(s). Skipped {GenerationResult.SkippedCount} (already exist).";
            }
            else if (GenerationResult.SkippedCount > 0)
            {
                TempData["Success"] = $"All {GenerationResult.SkippedCount} work orders already exist. No new work orders generated.";
            }
            else
            {
                TempData["Success"] = "No schedules due within the horizon. No work orders generated.";
            }

            if (GenerationResult.ErrorCount > 0)
            {
                TempData["Error"] = $"{GenerationResult.ErrorCount} error(s) occurred: {string.Join("; ", GenerationResult.Errors.Take(3))}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var query = _db.PMSchedules.Where(s => s.Id == id);
            if (_tenantContext.TenantId.HasValue)
                query = query.Where(s => s.TenantId == _tenantContext.TenantId);
            if (_tenantContext.CompanyId.HasValue)
                query = query.Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                query = query.Where(s => s.SiteId == _tenantContext.SiteId);

            var schedule = await query.FirstOrDefaultAsync();
            if (schedule != null)
            {
                _db.PMSchedules.Remove(schedule);
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Schedule '{schedule.Name}' deleted.";
            }
            return RedirectToPage();
        }
    }
}
