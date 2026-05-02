using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Maintenance.Assignments
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public IndexModel(AppDbContext db, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _tenantContext = tenantContext;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 1;

        public List<AssignmentRow> Assignments { get; set; } = new();
        public List<Asset> AvailableAssets { get; set; } = new();
        public List<PMTemplate> AvailableTemplates { get; set; } = new();
        public int TotalAssignments { get; set; }
        public int ActiveAssignments { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        public class AssignmentRow
        {
            public int Id { get; set; }
            public string AssetNumber { get; set; } = "";
            public string AssetDescription { get; set; } = "";
            public string TemplateCode { get; set; } = "";
            public string TemplateName { get; set; } = "";
            public DateTime? NextDueDate { get; set; }
            public DateTime? LastCompletedDate { get; set; }
            public bool IsActive { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Maintenance" });


            var companyId = GetCompanyId();
            
            var assignments = await _db.Set<PMTemplateAsset>()
                .Include(pa => pa.PMTemplate)
                .Include(pa => pa.Asset)
                .Where(pa => pa.Asset != null && _tenantContext.VisibleCompanyIds.Contains(pa.Asset.CompanyId ?? 0))
                .OrderByDescending(pa => pa.CreatedAt)
                .ToListAsync();

            Assignments = assignments.Select(pa => new AssignmentRow
            {
                Id = pa.Id,
                AssetNumber = pa.Asset?.AssetNumber ?? "",
                AssetDescription = pa.Asset?.Description ?? "",
                TemplateCode = pa.PMTemplate?.Code ?? "",
                TemplateName = pa.PMTemplate?.Name ?? "",
                NextDueDate = pa.NextDueDate,
                LastCompletedDate = pa.LastCompletedDate,
                IsActive = pa.IsActive
            }).ToList();

            TotalAssignments = Assignments.Count;
            ActiveAssignments = Assignments.Count(a => a.IsActive);

            AvailableAssets = await _db.Assets
                .Where(a => _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && a.Status == AssetStatus.Active)
                .OrderBy(a => a.AssetNumber)
                .ToListAsync();

            AvailableTemplates = await _db.Set<PMTemplate>()
                .Where(t => t.IsActive)
                .OrderBy(t => t.Code)
                .ToListAsync();
        
            return Page();
        }

        public async Task<IActionResult> OnPostCreateAsync(int assetId, int pmTemplateId, DateTime? nextDueDate)
        {
            var companyId = GetCompanyId();
            
            var asset = await _db.Assets
                .Where(a => a.Id == assetId && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            
            if (asset == null)
            {
                TempData["ErrorMessage"] = "Asset not found or access denied.";
                return RedirectToPage();
            }
            
            var exists = await _db.Set<PMTemplateAsset>()
                .AnyAsync(pa => pa.AssetId == assetId && pa.PMTemplateId == pmTemplateId);

            if (exists)
            {
                TempData["ErrorMessage"] = "This assignment already exists.";
                return RedirectToPage();
            }

            var template = await _db.Set<PMTemplate>()
                .Where(t => t.Id == pmTemplateId && _tenantContext.VisibleCompanyIds.Contains(t.CompanyId ?? 0))
                .FirstOrDefaultAsync();
            DateTime? calculatedNextDue = nextDueDate;
            
            if (!calculatedNextDue.HasValue && template != null)
            {
                int daysToAdd = template.CalendarInterval switch
                {
                    RecurrenceType.Daily => template.CalendarIntervalValue,
                    RecurrenceType.Weekly => template.CalendarIntervalValue * 7,
                    RecurrenceType.Monthly => template.CalendarIntervalValue * 30,
                    RecurrenceType.Quarterly => template.CalendarIntervalValue * 90,
                    RecurrenceType.Annually => template.CalendarIntervalValue * 365,
                    _ => 30
                };
                calculatedNextDue = DateTime.UtcNow.Date.AddDays(daysToAdd);
            }

            var assignment = new PMTemplateAsset
            {
                AssetId = assetId,
                PMTemplateId = pmTemplateId,
                NextDueDate = calculatedNextDue,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Set<PMTemplateAsset>().Add(assignment);
            await _db.SaveChangesAsync();

            SuccessMessage = "Assignment created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var companyId = GetCompanyId();
            
            var assignment = await _db.Set<PMTemplateAsset>()
                .Include(pa => pa.Asset)
                .Where(pa => pa.Id == id && pa.Asset != null && _tenantContext.VisibleCompanyIds.Contains(pa.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
                
            if (assignment != null)
            {
                _db.Set<PMTemplateAsset>().Remove(assignment);
                await _db.SaveChangesAsync();
                SuccessMessage = "Assignment deleted.";
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int id)
        {
            var companyId = GetCompanyId();
            
            var assignment = await _db.Set<PMTemplateAsset>()
                .Include(pa => pa.Asset)
                .Where(pa => pa.Id == id && pa.Asset != null && _tenantContext.VisibleCompanyIds.Contains(pa.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync();
                
            if (assignment != null)
            {
                assignment.IsActive = !assignment.IsActive;
                assignment.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                SuccessMessage = assignment.IsActive ? "Assignment activated." : "Assignment deactivated.";
            }
            return RedirectToPage();
        }
    }
}
