using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public IndexModel(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        // Asset Summary
        public int ActiveAssets { get; private set; }
        public int TotalAssets { get; private set; }
        public decimal TotalAcquisitionCost { get; private set; }
        public decimal TotalNetBookValue { get; private set; }
        public decimal TotalAccumulatedDepreciation { get; private set; }
        public decimal TotalFairMarketValue { get; private set; }

        // Maintenance Summary
        public int ScheduledMaintenance { get; private set; }
        public int OverdueMaintenance { get; private set; }
        public int InProgressMaintenance { get; private set; }
        public int OpenMaintenance { get; private set; }
        public decimal MaintenanceCostMTD { get; private set; }
        public decimal MaintenanceCostYTD { get; private set; }
        
        // KPI Metrics
        public int WorkOrderBacklog { get; private set; }
        public decimal PMCompliancePercent { get; private set; }
        public decimal MTTRHours { get; private set; }
        
        // PM Schedule KPIs
        public int TotalSchedules { get; private set; }
        public int SchedulesDueThisWeek { get; private set; }
        public int SchedulesOverdue { get; private set; }
        public int ScheduledEventsCompleted { get; private set; }
        
        // Work Request KPIs
        public int NewWorkRequests { get; private set; }
        public int DraftWorkOrders { get; private set; }
        public int OpenWorkOrders { get; private set; }

        // CIP Summary
        public int ActiveCipProjects { get; private set; }
        public decimal CipTotalBudget { get; private set; }
        public decimal CipTotalSpent { get; private set; }

        // Lists
        public List<Asset> RecentAssets { get; private set; } = new();
        public List<LocationSummary> LocationBreakdown { get; private set; } = new();
        public List<AlertItem> Alerts { get; private set; } = new();
        public List<AuditLog> RecentActivity { get; private set; } = new();
        
        // Database state
        public bool IsDatabaseEmpty { get; private set; }
        public bool IsDevelopment { get; private set; }

        // Scope labels for the dashboard context strip (DEF-003 from
        // 2026-05-08 E2E run). Inner pages render company + site
        // breadcrumbs via _ScreenHeader's ContextPartial; the dashboard
        // now matches them so the layout stops drifting.
        public string ScopeCompanyLabel { get; private set; } = "All Companies";
        public string ScopeSiteLabel { get; private set; } = "All Sites";

        public async Task OnGetAsync()
        {
            // Check if database is empty (no companies = not initialized)
            IsDatabaseEmpty = !await _db.Companies.AnyAsync();
            IsDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
            
            if (IsDatabaseEmpty)
            {
                // Return early - don't query empty tables
                return;
            }
            
            var visibleIds = _tenantContext.VisibleCompanyIds;
            var assetQuery = _db.Assets.Include(a => a.LocationRef).Where(a => visibleIds.Contains(a.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                assetQuery = assetQuery.Where(a => a.SiteId == _tenantContext.SiteId.Value);
            var assets = await assetQuery.ToListAsync();

            if (_tenantContext.CompanyId.HasValue)
            {
                var co = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == _tenantContext.CompanyId.Value);
                ScopeCompanyLabel = co?.Name ?? $"Company #{_tenantContext.CompanyId}";
            }
            if (_tenantContext.SiteId.HasValue)
            {
                var site = await _db.Sites.AsNoTracking().FirstOrDefaultAsync(s => s.Id == _tenantContext.SiteId.Value);
                ScopeSiteLabel = site?.Name ?? $"Site #{_tenantContext.SiteId}";
            }
            var activeAssets = assets.Where(a => a.Active).ToList();
            
            // Asset metrics
            TotalAssets = assets.Count;
            ActiveAssets = activeAssets.Count;
            TotalAcquisitionCost = activeAssets.Sum(a => a.AcquisitionCost);
            TotalNetBookValue = activeAssets.Sum(a => a.BookValue ?? a.AcquisitionCost - a.AccumulatedDepreciation);
            TotalAccumulatedDepreciation = activeAssets.Sum(a => a.AccumulatedDepreciation);
            TotalFairMarketValue = activeAssets.Where(a => a.FairMarketValue.HasValue).Sum(a => a.FairMarketValue ?? 0);

            // Maintenance metrics
            var meQuery = _db.MaintenanceEvents.Include(m => m.Asset).Where(m => m.Asset != null && visibleIds.Contains(m.Asset.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                meQuery = meQuery.Where(m => m.Asset != null && m.Asset.SiteId == _tenantContext.SiteId.Value);
            var maintenanceEvents = await meQuery.ToListAsync();
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfYear = new DateTime(now.Year, 1, 1);

            ScheduledMaintenance = maintenanceEvents.Count(m => m.Status == MaintenanceStatus.Scheduled);
            // Overdue = any event not completed/cancelled that is past scheduled date (matches MaintenanceService logic)
            OverdueMaintenance = maintenanceEvents.Count(m => 
                m.Status != MaintenanceStatus.Completed && 
                m.Status != MaintenanceStatus.Cancelled && 
                m.ScheduledDate < now);
            InProgressMaintenance = maintenanceEvents.Count(m => m.Status == MaintenanceStatus.InProgress);
            // Open = scheduled + in progress (all non-completed/cancelled events)
            OpenMaintenance = ScheduledMaintenance + InProgressMaintenance;
            MaintenanceCostMTD = maintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.Completed && m.CompletedDate >= startOfMonth)
                .Sum(m => m.ActualCost ?? 0);
            MaintenanceCostYTD = maintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.Completed && m.CompletedDate >= startOfYear)
                .Sum(m => m.ActualCost ?? 0);
            
            // KPI Metrics
            WorkOrderBacklog = maintenanceEvents.Count(m => 
                m.Status == MaintenanceStatus.Scheduled || 
                m.Status == MaintenanceStatus.InProgress ||
                m.Status == MaintenanceStatus.OnHold);
            
            // PM Compliance: % of PM events completed on time (within 7 days of scheduled)
            var pmEvents = maintenanceEvents.Where(m => m.Type == MaintenanceType.Preventative).ToList();
            var completedPM = pmEvents.Where(m => m.Status == MaintenanceStatus.Completed && m.CompletedDate.HasValue).ToList();
            var onTimePM = completedPM.Count(m => m.CompletedDate!.Value <= m.ScheduledDate.AddDays(7));
            PMCompliancePercent = completedPM.Count > 0 ? (decimal)onTimePM / completedPM.Count * 100 : 100;
            
            // MTTR: Average hours from scheduled to completed for completed corrective work orders
            var completedCorrective = maintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.Completed && 
                            m.CompletedDate.HasValue && 
                            m.Type == MaintenanceType.Corrective)
                .ToList();
            if (completedCorrective.Count > 0)
            {
                var totalHours = completedCorrective
                    .Sum(m => (m.CompletedDate!.Value - m.ScheduledDate).TotalHours);
                MTTRHours = (decimal)(totalHours / completedCorrective.Count);
            }
            else
            {
                MTTRHours = 0;
            }
            
            // PM Schedule KPIs - Use canonical PMSchedule model with tenant scoping
            // ALIGNED with Maintenance/Schedules.cshtml.cs for consistency
            var pmScheduleQuery = _db.PMSchedules.Where(s => s.Active).AsQueryable();
            if (_tenantContext.TenantId.HasValue)
                pmScheduleQuery = pmScheduleQuery.Where(s => s.TenantId == _tenantContext.TenantId);
            if (_tenantContext.CompanyId.HasValue)
                pmScheduleQuery = pmScheduleQuery.Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId ?? 0));
            if (_tenantContext.SiteId.HasValue)
                pmScheduleQuery = pmScheduleQuery.Where(s => s.SiteId == _tenantContext.SiteId);

            var pmSchedules = await pmScheduleQuery.ToListAsync();
            TotalSchedules = pmSchedules.Count;
            var todayUtc = DateTime.UtcNow.Date;
            var weekEndUtc = todayUtc.AddDays(7);
            SchedulesDueThisWeek = pmSchedules.Count(s => s.NextDueDateUtc.HasValue && s.NextDueDateUtc.Value.Date >= todayUtc && s.NextDueDateUtc.Value.Date <= weekEndUtc);
            SchedulesOverdue = pmSchedules.Count(s => s.NextDueDateUtc.HasValue && s.NextDueDateUtc.Value.Date < todayUtc);
            // Count PM-originated completed work orders via PMOccurrence linkage
            var completedPmOccurrences = await _db.PMOccurrences
                .Where(o => o.WorkOrderId.HasValue)
                .Select(o => o.WorkOrderId)
                .ToListAsync();
            ScheduledEventsCompleted = maintenanceEvents.Count(m => 
                m.Status == MaintenanceStatus.Completed && 
                completedPmOccurrences.Contains(m.Id));
            
            // Work Request KPIs
            NewWorkRequests = await _db.WorkRequests.CountAsync(r => r.Status == WorkRequestStatus.New && visibleIds.Contains(r.CompanyId ?? 0));
            DraftWorkOrders = maintenanceEvents.Count(m => 
                m.ApprovalStatus == WorkOrderApprovalStatus.PendingApproval &&
                m.Description != null && 
                m.Description.StartsWith("[Smart Assist Draft]"));
            OpenWorkOrders = maintenanceEvents.Count(m => 
                m.Status == MaintenanceStatus.Scheduled || 
                m.Status == MaintenanceStatus.InProgress);

            // CIP metrics
            var cipProjects = await _db.CipProjects.Where(c => visibleIds.Contains(c.CompanyId ?? 0)).ToListAsync();
            ActiveCipProjects = cipProjects.Count(c => c.Status == CipProjectStatus.Active || c.Status == CipProjectStatus.Planned);
            CipTotalBudget = cipProjects.Where(c => c.Status != CipProjectStatus.Cancelled).Sum(c => c.BudgetAmount);
            CipTotalSpent = cipProjects.Sum(c => c.TotalCosts);

            // Recent assets
            RecentAssets = assets.OrderByDescending(a => a.Id).Take(5).ToList();

            // Location breakdown
            LocationBreakdown = activeAssets
                .Where(a => a.LocationRef != null)
                .GroupBy(a => a.LocationRef!.Name)
                .Select(g => new LocationSummary 
                { 
                    Location = g.Key ?? "Unknown", 
                    Count = g.Count(), 
                    Value = g.Sum(a => a.AcquisitionCost) 
                })
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToList();

            // Alerts
            Alerts = new List<AlertItem>();
            
            if (OverdueMaintenance > 0)
            {
                Alerts.Add(new AlertItem 
                { 
                    Type = "danger", 
                    Title = $"{OverdueMaintenance} Overdue Maintenance", 
                    Description = "Maintenance events past their scheduled date",
                    Link = "/Maintenance"
                });
            }

            var fullyDepreciated = activeAssets.Count(a => a.AccumulatedDepreciation >= a.AcquisitionCost);
            if (fullyDepreciated > 0)
            {
                Alerts.Add(new AlertItem 
                { 
                    Type = "warning", 
                    Title = $"{fullyDepreciated} Fully Depreciated Assets", 
                    Description = "Assets that may need review for disposal",
                    Link = "/Assets?filter=fully-depreciated"
                });
            }

            var activeCipProjects = cipProjects.Where(c => c.Status == CipProjectStatus.Active || c.Status == CipProjectStatus.Planned).ToList();
            var projectsOverBudget = activeCipProjects.Count(c => c.BudgetAmount > 0 && c.TotalCosts > c.BudgetAmount);
            var projectsNearBudget = activeCipProjects.Count(c => c.BudgetAmount > 0 && c.TotalCosts > c.BudgetAmount * 0.9m && c.TotalCosts <= c.BudgetAmount);
            
            if (projectsOverBudget > 0)
            {
                Alerts.Add(new AlertItem 
                { 
                    Type = "danger", 
                    Title = $"{projectsOverBudget} CIP Project{(projectsOverBudget == 1 ? "" : "s")} Over Budget", 
                    Description = "Projects that have exceeded their budget",
                    Link = "/CIP"
                });
            }
            
            if (projectsNearBudget > 0)
            {
                Alerts.Add(new AlertItem 
                { 
                    Type = "warning", 
                    Title = $"{projectsNearBudget} CIP Project{(projectsNearBudget == 1 ? "" : "s")} Near Budget", 
                    Description = "Projects at 90-100% of budget",
                    Link = "/CIP"
                });
            }

            // Recent activity
            RecentActivity = await _db.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(8)
                .ToListAsync();
        }
    }
    
    public class LocationSummary
    {
        public string Location { get; set; } = "";
        public int Count { get; set; }
        public decimal Value { get; set; }
    }

    public class AlertItem
    {
        public string Type { get; set; } = "info";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Link { get; set; } = "#";
    }
}
