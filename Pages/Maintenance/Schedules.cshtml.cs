using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Maintenance
{
    [Authorize]
    public class SchedulesModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public SchedulesModel(AppDbContext db, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _tenantContext = tenantContext;
        }

        public List<ScheduleRow> Schedules { get; set; } = new();
        public int TotalSchedules { get; set; }
        public int DueThisWeek { get; set; }
        public int Overdue { get; set; }
        public int ActiveTemplates { get; set; }

        public class ScheduleRow
        {
            public int Id { get; set; }
            public int TemplateId { get; set; }
            public string ScheduleName { get; set; } = "";
            public string TemplateCode { get; set; } = "";
            public string TemplateName { get; set; } = "";
            public string CompanyName { get; set; } = "";
            public string SiteName { get; set; } = "";
            public DateTime? NextDueDate { get; set; }
            public DateTime StartDate { get; set; }
            public PMCadenceType CadenceType { get; set; }
            public int? IntervalDays { get; set; }
            public bool IsActive { get; set; }
            public bool IsOverdue => NextDueDate.HasValue && NextDueDate.Value.Date < DateTime.Today;
            public bool IsDueSoon => NextDueDate.HasValue && !IsOverdue && NextDueDate.Value.Date <= DateTime.Today.AddDays(7);
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Maintenance" });


            var todayUtc = DateTime.UtcNow.Date;
            var weekFromNow = todayUtc.AddDays(7);

            // Build tenant-scoped query for PMSchedules (canonical model)
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

            var pmSchedules = await query
                .Where(s => s.Active)
                .OrderBy(s => s.NextDueDateUtc ?? DateTime.MaxValue)
                .ToListAsync();

            Schedules = pmSchedules.Select(s => new ScheduleRow
            {
                Id = s.Id,
                TemplateId = s.PMTemplateId,
                ScheduleName = s.Name,
                TemplateCode = s.PMTemplate?.Code ?? "",
                TemplateName = s.PMTemplate?.Name ?? "",
                CompanyName = s.Company?.Name ?? "",
                SiteName = s.Site?.Name ?? "",
                NextDueDate = s.NextDueDateUtc?.ToLocalTime(),
                StartDate = s.StartDateUtc.ToLocalTime(),
                CadenceType = s.CadenceType,
                IntervalDays = s.IntervalDays,
                IsActive = s.Active
            }).ToList();

            TotalSchedules = Schedules.Count;
            Overdue = Schedules.Count(s => s.IsOverdue);
            DueThisWeek = Schedules.Count(s => s.IsDueSoon);
            ActiveTemplates = pmSchedules.Select(s => s.PMTemplateId).Distinct().Count();
        
            return Page();
        }
    }
}
