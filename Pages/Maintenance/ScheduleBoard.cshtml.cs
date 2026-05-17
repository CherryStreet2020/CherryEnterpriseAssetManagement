using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using System.Text.Json;

namespace Abs.FixedAssets.Pages.Maintenance
{
    public class ScheduleBoardModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ILookupService _lookupService;

        public ScheduleBoardModel(AppDbContext context, ITenantContext tenantContext, IModuleGuardService moduleGuard, ILookupService lookupService)
        {
            _context = context;
            _tenantContext = tenantContext;
            _moduleGuard = moduleGuard;
            _lookupService = lookupService;
        }

        // Keeps WorkOrderOperation.Status (legacy enum) and StatusLookupValueId
        // (FK) in lockstep on every status write. Mirrors the canonical helper
        // in Pages/Purchasing/Details.cshtml.cs::SyncStatusFkAsync.
        private async Task SyncOperationStatusFkAsync(WorkOrderOperation op, OperationStatus status)
        {
            op.Status = status;
            var lv = await _lookupService.GetValueByCodeAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId,
                "OperationStatus", ((int)status).ToString());
            if (lv != null)
                op.StatusLookupValueId = lv.Id;
        }

        public string TechniciansJson { get; set; } = "[]";
        public string EventsJson { get; set; } = "[]";
        public string BacklogJson { get; set; } = "[]";
        public string CraftsJson { get; set; } = "[]";
        public string WeekDaysJson { get; set; } = "[]";

        public int TotalBacklog { get; set; }
        public int ScheduledThisWeek { get; set; }
        public int OverdueCount { get; set; }
        public decimal AvgUtilization { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Mode { get; set; }
        public bool IsPopout => Mode == "popout";

        // Week navigation
        [BindProperty(SupportsGet = true)]
        public string? WeekStart { get; set; }

        public DateTime CurrentWeekStart { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("maintenance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Scheduling" });

            // Calculate week start (Monday)
            if (!string.IsNullOrEmpty(WeekStart) && DateTime.TryParse(WeekStart, out var ws))
                CurrentWeekStart = ws.Date;
            else
            {
                var today = DateTime.UtcNow.Date;
                var diff = ((int)today.DayOfWeek + 6) % 7; // Monday=0
                CurrentWeekStart = today.AddDays(-diff);
            }

            // Build week days array
            var weekDays = Enumerable.Range(0, 5).Select(i =>
            {
                var d = CurrentWeekStart.AddDays(i);
                return new
                {
                    date = d.ToString("yyyy-MM-dd"),
                    label = d.ToString("ddd M/d"),
                    dayName = d.ToString("ddd").ToUpper(),
                    isToday = d.Date == DateTime.UtcNow.Date
                };
            }).ToList();
            WeekDaysJson = JsonSerializer.Serialize(weekDays, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var companyIds = _tenantContext.VisibleCompanyIds;
            var siteId = _tenantContext.SiteId;

            // Technicians
            var techQuery = _context.Technicians
                .Where(t => t.Active && (t.CompanyId == null || companyIds.Contains(t.CompanyId ?? 0)));
            if (siteId.HasValue && siteId.Value > 0)
                techQuery = techQuery.Where(t => t.SiteId == null || t.SiteId == siteId.Value);

            var technicians = await techQuery.OrderBy(t => t.PrimaryCraft).ThenBy(t => t.Name).ToListAsync();
            var techIds = technicians.Select(t => t.Id).ToList();
            var techSkills = await _context.TechnicianSkills.Where(ts => techIds.Contains(ts.TechnicianId)).ToListAsync();
            var techCerts = await _context.TechnicianCertifications.Where(tc => techIds.Contains(tc.TechnicianId)).ToListAsync();

            TechniciansJson = JsonSerializer.Serialize(technicians.Select(t => new
            {
                id = t.Id,
                name = t.Name,
                craft = t.PrimaryCraft ?? "UNASSIGNED",
                secondaryCraft = t.SecondaryCraft ?? "",
                shiftStart = t.ShiftStart?.ToString("HH:mm") ?? "07:00",
                shiftEnd = t.ShiftEnd?.ToString("HH:mm") ?? "15:30",
                hourlyRate = t.HourlyRate ?? 0m,
                skillCount = techSkills.Count(s => s.TechnicianId == t.Id),
                certCount = techCerts.Count(c => c.TechnicianId == t.Id)
            }), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // Week boundaries
            var weekEnd = CurrentWeekStart.AddDays(5);

            // Scheduled operations for this week
            var scheduledOps = await _context.WorkOrderOperations
                .Include(o => o.WorkOrder).ThenInclude(m => m!.Asset)
                .Include(o => o.Craft)
                .Where(o => o.AssignedTechnicianId != null && o.PlannedStartDate != null
                    && o.PlannedStartDate >= CurrentWeekStart && o.PlannedStartDate < weekEnd
                    && o.Status != OperationStatus.Cancelled
                    && o.WorkOrder != null && o.WorkOrder.Asset != null
                    && companyIds.Contains(o.WorkOrder.Asset.CompanyId ?? 0))
                .OrderBy(o => o.PlannedStartDate).ToListAsync();

            EventsJson = JsonSerializer.Serialize(scheduledOps.Select(o =>
            {
                var dayIdx = (int)(o.PlannedStartDate!.Value.Date - CurrentWeekStart).TotalDays;
                return new
                {
                    id = o.Id,
                    techId = o.AssignedTechnicianId,
                    dayIdx = dayIdx >= 0 && dayIdx < 5 ? dayIdx : 0,
                    wo = o.WorkOrder?.WorkOrderNumber ?? "",
                    woId = o.WorkOrderId,
                    title = o.Title,
                    asset = o.WorkOrder?.Asset?.Description ?? "",
                    craft = o.Craft?.Code ?? "",
                    hours = o.PlannedHours,
                    priority = o.WorkOrder?.Priority.ToString() ?? "Medium",
                    overdue = o.WorkOrder?.ScheduledDate < DateTime.UtcNow.Date,
                    status = o.Status.ToString()
                };
            }), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // Backlog (unscheduled)
            var backlogOps = await _context.WorkOrderOperations
                .Include(o => o.WorkOrder).ThenInclude(m => m!.Asset)
                .Include(o => o.Craft)
                .Where(o => (o.AssignedTechnicianId == null || o.PlannedStartDate == null)
                    && o.Status != OperationStatus.Cancelled && o.Status != OperationStatus.Completed
                    && o.WorkOrder != null && o.WorkOrder.Asset != null
                    && companyIds.Contains(o.WorkOrder.Asset.CompanyId ?? 0))
                .OrderByDescending(o => o.WorkOrder!.Priority)
                .ThenBy(o => o.WorkOrder!.ScheduledDate).ToListAsync();

            BacklogJson = JsonSerializer.Serialize(backlogOps.Select(o => new
            {
                id = o.Id,
                wo = o.WorkOrder?.WorkOrderNumber ?? "",
                woId = o.WorkOrderId,
                title = o.Title,
                asset = o.WorkOrder?.Asset?.Description ?? "",
                craft = o.Craft?.Code ?? "",
                craftName = o.Craft?.Name ?? "",
                hours = o.PlannedHours,
                priority = o.WorkOrder?.Priority.ToString() ?? "Medium",
                priorityLevel = (int)(o.WorkOrder?.Priority ?? MaintenancePriority.Medium),
                overdue = o.WorkOrder?.ScheduledDate < DateTime.UtcNow.Date
            }), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // Crafts
            var crafts = await _context.Crafts.Where(c => c.IsActive).OrderBy(c => c.SortOrder).ToListAsync();
            CraftsJson = JsonSerializer.Serialize(crafts.Select(c => new { code = c.Code, name = c.Name }),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // KPIs
            TotalBacklog = backlogOps.Count;
            ScheduledThisWeek = scheduledOps.Count;
            OverdueCount = backlogOps.Count(o => o.WorkOrder?.ScheduledDate < DateTime.UtcNow.Date);
            if (technicians.Any())
            {
                var cap = technicians.Count * 40m;
                var hrs = scheduledOps.Sum(o => o.PlannedHours);
                AvgUtilization = cap > 0 ? Math.Round(hrs / cap * 100, 1) : 0;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAssignAsync(int operationId, int technicianId, string startDate, string? endDate)
        {
            var op = await _context.WorkOrderOperations.FindAsync(operationId);
            if (op == null) return new JsonResult(new { success = false, error = "Operation not found" });
            if (!DateTime.TryParse(startDate, out var start))
                return new JsonResult(new { success = false, error = "Invalid date" });

            var end = !string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var pe) ? pe : start.AddHours((double)op.PlannedHours);
            op.AssignedTechnicianId = technicianId;
            op.PlannedStartDate = start;
            op.PlannedEndDate = end;
            if (op.Status == OperationStatus.Pending)
                await SyncOperationStatusFkAsync(op, OperationStatus.Ready);
            op.ModifiedAt = DateTime.UtcNow;
            op.ModifiedBy = User.Identity?.Name ?? "scheduler";
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true, operationId, technicianId });
        }

        public async Task<IActionResult> OnPostRescheduleAsync(int operationId, int? technicianId, string startDate, string endDate)
        {
            var op = await _context.WorkOrderOperations.FindAsync(operationId);
            if (op == null) return new JsonResult(new { success = false, error = "Operation not found" });
            if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
                return new JsonResult(new { success = false, error = "Invalid dates" });
            op.PlannedStartDate = start;
            op.PlannedEndDate = end;
            if (technicianId.HasValue) op.AssignedTechnicianId = technicianId.Value;
            var h = (decimal)(end - start).TotalHours;
            if (h > 0) op.PlannedHours = Math.Round(h, 2);
            op.ModifiedAt = DateTime.UtcNow;
            op.ModifiedBy = User.Identity?.Name ?? "scheduler";
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostUnassignAsync(int operationId)
        {
            var op = await _context.WorkOrderOperations.FindAsync(operationId);
            if (op == null) return new JsonResult(new { success = false, error = "Operation not found" });
            op.AssignedTechnicianId = null;
            op.PlannedStartDate = null;
            op.PlannedEndDate = null;
            await SyncOperationStatusFkAsync(op, OperationStatus.Pending);
            op.ModifiedAt = DateTime.UtcNow;
            op.ModifiedBy = User.Identity?.Name ?? "scheduler";
            await _context.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }
    }
}
