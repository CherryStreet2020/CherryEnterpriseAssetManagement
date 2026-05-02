using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant")]
    public class PMScheduleEditModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public PMScheduleEditModel(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        [BindProperty]
        public PMScheduleInput Input { get; set; } = new();

        public PMSchedule? ExistingSchedule { get; set; }
        public bool IsEditMode => ExistingSchedule != null;
        public string PageMode => IsEditMode ? "Edit" : "Create";

        public SelectList? TemplateOptions { get; set; }
        public SelectList? SiteOptions { get; set; }
        public SelectList? CadenceOptions { get; set; }

        public string? SuccessMessage => TempData["Success"]?.ToString();
        public string? ErrorMessage => TempData["Error"]?.ToString();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            await LoadDropdownsAsync();

            if (id.HasValue)
            {
                var query = _db.PMSchedules
                    .Include(s => s.PMTemplate)
                    .Include(s => s.Company)
                    .Where(s => s.Id == id);

                if (_tenantContext.TenantId.HasValue)
                    query = query.Where(s => s.TenantId == _tenantContext.TenantId);
                if (_tenantContext.CompanyId.HasValue)
                    query = query.Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId ?? 0));
                if (_tenantContext.SiteId.HasValue)
                    query = query.Where(s => s.SiteId == _tenantContext.SiteId);

                ExistingSchedule = await query.FirstOrDefaultAsync();

                if (ExistingSchedule == null)
                    return NotFound();

                Input = new PMScheduleInput
                {
                    Id = ExistingSchedule.Id,
                    Name = ExistingSchedule.Name,
                    Description = ExistingSchedule.Description,
                    PMTemplateId = ExistingSchedule.PMTemplateId,
                    CompanyId = ExistingSchedule.CompanyId,
                    SiteId = ExistingSchedule.SiteId,
                    Active = ExistingSchedule.Active,
                    CadenceType = ExistingSchedule.CadenceType,
                    IntervalDays = ExistingSchedule.IntervalDays,
                    DaysOfWeekMask = ExistingSchedule.DaysOfWeekMask,
                    DayOfMonth = ExistingSchedule.DayOfMonth,
                    StartDateUtc = ExistingSchedule.StartDateUtc,
                    Sunday = (ExistingSchedule.DaysOfWeekMask & 1) != 0,
                    Monday = (ExistingSchedule.DaysOfWeekMask & 2) != 0,
                    Tuesday = (ExistingSchedule.DaysOfWeekMask & 4) != 0,
                    Wednesday = (ExistingSchedule.DaysOfWeekMask & 8) != 0,
                    Thursday = (ExistingSchedule.DaysOfWeekMask & 16) != 0,
                    Friday = (ExistingSchedule.DaysOfWeekMask & 32) != 0,
                    Saturday = (ExistingSchedule.DaysOfWeekMask & 64) != 0
                };
            }
            else
            {
                Input.StartDateUtc = DateTime.UtcNow.Date;
                Input.IntervalDays = 30;
                Input.DayOfMonth = 1;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDropdownsAsync();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (Input.SiteId.HasValue && _tenantContext.SiteId.HasValue && 
                Input.SiteId != _tenantContext.SiteId)
            {
                ModelState.AddModelError("", "Invalid site selection for your scope.");
                return Page();
            }

            PMSchedule schedule;

            if (Input.Id > 0)
            {
                var query = _db.PMSchedules.Where(s => s.Id == Input.Id);
                if (_tenantContext.TenantId.HasValue)
                    query = query.Where(s => s.TenantId == _tenantContext.TenantId);
                if (_tenantContext.CompanyId.HasValue)
                    query = query.Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId ?? 0));
                if (_tenantContext.SiteId.HasValue)
                    query = query.Where(s => s.SiteId == _tenantContext.SiteId);

                schedule = (await query.FirstOrDefaultAsync())!;
                if (schedule == null)
                    return NotFound();
            }
            else
            {
                schedule = new PMSchedule();
                schedule.TenantId = _tenantContext.TenantId;
                schedule.CompanyId = _tenantContext.CompanyId;
                schedule.SiteId = _tenantContext.SiteId;
                schedule.CreatedBy = User.Identity?.Name;
                schedule.CreatedAt = DateTime.UtcNow;
                _db.PMSchedules.Add(schedule);
            }

            schedule.Name = Input.Name;
            schedule.Description = Input.Description;
            schedule.PMTemplateId = Input.PMTemplateId;
            schedule.SiteId = Input.SiteId;
            schedule.Active = Input.Active;
            schedule.CadenceType = Input.CadenceType;
            schedule.IntervalDays = Input.IntervalDays;
            schedule.DayOfMonth = Input.DayOfMonth;
            schedule.StartDateUtc = DateTime.SpecifyKind(Input.StartDateUtc.Date, DateTimeKind.Utc);
            schedule.UpdatedBy = User.Identity?.Name;
            schedule.UpdatedAt = DateTime.UtcNow;

            int mask = 0;
            if (Input.Sunday) mask |= 1;
            if (Input.Monday) mask |= 2;
            if (Input.Tuesday) mask |= 4;
            if (Input.Wednesday) mask |= 8;
            if (Input.Thursday) mask |= 16;
            if (Input.Friday) mask |= 32;
            if (Input.Saturday) mask |= 64;
            schedule.DaysOfWeekMask = mask;

            await _db.SaveChangesAsync();

            TempData["Success"] = Input.Id > 0 
                ? $"Schedule '{schedule.Name}' updated successfully." 
                : $"Schedule '{schedule.Name}' created successfully.";

            return RedirectToPage("PMSchedules");
        }

        private async Task LoadDropdownsAsync()
        {
            var templateQuery = _db.PMTemplates.Where(t => t.IsActive).AsQueryable();
            var siteQuery = _db.Sites.AsQueryable();

            if (_tenantContext.SiteId.HasValue)
                siteQuery = siteQuery.Where(s => s.Id == _tenantContext.SiteId);

            var templates = await templateQuery
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();
            TemplateOptions = new SelectList(templates, "Id", "Name");

            var sites = await siteQuery
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();
            SiteOptions = new SelectList(sites, "Id", "Name");

            CadenceOptions = new SelectList(new[]
            {
                new { Value = (int)PMCadenceType.IntervalDays, Text = "Fixed Interval (Days)" },
                new { Value = (int)PMCadenceType.Weekly, Text = "Weekly (Specific Days)" },
                new { Value = (int)PMCadenceType.Monthly, Text = "Monthly (Day of Month)" }
            }, "Value", "Text");
        }
    }

    public class PMScheduleInput
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Schedule Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "PM Template")]
        public int PMTemplateId { get; set; }

        [Display(Name = "Company")]
        public int? CompanyId { get; set; }

        [Display(Name = "Site")]
        public int? SiteId { get; set; }

        [Display(Name = "Active")]
        public bool Active { get; set; } = true;

        [Display(Name = "Cadence Type")]
        public PMCadenceType CadenceType { get; set; } = PMCadenceType.IntervalDays;

        [Display(Name = "Interval (Days)")]
        [Range(1, 365)]
        public int? IntervalDays { get; set; } = 30;

        [Display(Name = "Day of Month")]
        [Range(1, 28)]
        public int? DayOfMonth { get; set; } = 1;

        public int? DaysOfWeekMask { get; set; }

        [Required]
        [Display(Name = "Start Date")]
        [DataType(DataType.Date)]
        public DateTime StartDateUtc { get; set; } = DateTime.UtcNow.Date;

        public bool Sunday { get; set; }
        public bool Monday { get; set; }
        public bool Tuesday { get; set; }
        public bool Wednesday { get; set; }
        public bool Thursday { get; set; }
        public bool Friday { get; set; }
        public bool Saturday { get; set; }
    }
}
