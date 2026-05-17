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
        // PR #104 (B-15): Mean Time Between Failures, org-wide. Computed as
        // the average gap (in hours) between consecutive corrective WO
        // close-times across all assets that have at least two completed
        // corrective WOs in the window. Zero when fewer than two data
        // points per asset across the whole org — surfaced as "N/A" in the
        // dashboard tile.
        public decimal MTBFHours { get; private set; }
        public bool MTBFHasData { get; private set; }

        // PR #111: Reliability tiles. Surfaces the worst-5 assets, top-5 overdue
        // WOs, backlog by priority, and on-schedule WO completion %. Drill-throughs
        // from each tile to the appropriate detail page.
        public List<WorstMtbfRow> WorstMtbfAssets { get; private set; } = new();
        public List<OverdueWoRow> TopOverdueWos { get; private set; } = new();
        public Dictionary<string, int> BacklogByPriority { get; private set; } = new();
        public decimal OnScheduleCompletionPercent { get; private set; }
        public int OnScheduleWoCount { get; private set; }
        public int TotalCompletedWoCount { get; private set; }

        public sealed record WorstMtbfRow(int AssetId, string AssetNumber, string Description, decimal MtbfHours, int CorrectiveCount);
        public sealed record OverdueWoRow(int WoId, string WoNumber, string AssetNumber, DateTime ScheduledDate, int DaysOverdue, string Priority);
        
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
            var meQuery = _db.WorkOrders.Include(m => m.Asset).Where(m => m.Asset != null && visibleIds.Contains(m.Asset.CompanyId ?? 0));
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
            // PR #108 / B-25: dashboard MaintenanceCostMTD/YTD now sources from the
            // same JE table the PR #93 Maintenance Spend report and PR #103 closeout
            // rollup read. Pre-PR these tiles summed `WorkOrder.ActualCost` —
            // a legacy header field that drifted because (a) it was a manual entry
            // for years before PR #89 introduced JE-driven cost flows, and (b) PR
            // #103 only writes to it on close. After PRs #89, #92, #103, and #106
            // the JEs (Source IN WO-ISS, WO-RTN, WO-LBR, WO-ISS-OP, WO-RTN-OP) are
            // the single source of truth for "maintenance spend in period."
            //
            // Filter on PostingDate (the JE's accounting date), not CompletedDate,
            // because spend can be posted before WO close (issue-then-return cycles
            // happen mid-WO) and the PR #93 report keys off PostingDate too.
            var maintenanceJeSources = new[] { "WO-ISS", "WO-RTN", "WO-LBR", "WO-ISS-OP", "WO-RTN-OP" };
            // Net spend = debits on WO-ISS/WO-LBR/WO-ISS-OP minus debits on WO-RTN/WO-RTN-OP.
            // The Closeout/Spend semantics treat returns as offsets to the same line items.
            var spendQuery = _db.JournalLines
                .Where(l => l.JournalEntry != null
                    && maintenanceJeSources.Contains(l.JournalEntry.Source))
                .Select(l => new { l.JournalEntry.Source, l.JournalEntry.PostingDate, l.Debit });
            MaintenanceCostMTD = await spendQuery
                .Where(x => x.PostingDate >= startOfMonth)
                .Where(x => x.Source != "WO-RTN" && x.Source != "WO-RTN-OP")
                .SumAsync(x => (decimal?)x.Debit) ?? 0m;
            MaintenanceCostMTD -= await spendQuery
                .Where(x => x.PostingDate >= startOfMonth)
                .Where(x => x.Source == "WO-RTN" || x.Source == "WO-RTN-OP")
                .SumAsync(x => (decimal?)x.Debit) ?? 0m;
            MaintenanceCostYTD = await spendQuery
                .Where(x => x.PostingDate >= startOfYear)
                .Where(x => x.Source != "WO-RTN" && x.Source != "WO-RTN-OP")
                .SumAsync(x => (decimal?)x.Debit) ?? 0m;
            MaintenanceCostYTD -= await spendQuery
                .Where(x => x.PostingDate >= startOfYear)
                .Where(x => x.Source == "WO-RTN" || x.Source == "WO-RTN-OP")
                .SumAsync(x => (decimal?)x.Debit) ?? 0m;
            
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
            
            // PR #104 (B-14): MTTR = Mean Time To Repair = avg hours between
            // StartedAt (when the technician actually began the work, i.e.
            // status moved to InProgress) and CompletedDate. The pre-fix
            // formula used `CompletedDate - ScheduledDate` which measures
            // *lateness vs. the calendar*, not repair time — a WO scheduled
            // for Tuesday and completed Friday counted as 72 MTTR-hours
            // regardless of whether the actual fix took 30 minutes. With
            // StartedAt as the start anchor, MTTR finally answers the
            // question the KPI label promises: "how long does it take us
            // to fix a corrective WO once we start working it?"
            //
            // Only include WOs where both StartedAt and CompletedDate are
            // non-null — WOs that closed without ever going InProgress are
            // operational anomalies (likely zero-effort cancellations
            // mistakenly closed) and excluding them prevents skew.
            var correctiveWithBothTimestamps = maintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.Completed
                            && m.CompletedDate.HasValue
                            && m.StartedAt.HasValue
                            && m.Type == MaintenanceType.Corrective)
                .ToList();
            if (correctiveWithBothTimestamps.Count > 0)
            {
                var totalRepairHours = correctiveWithBothTimestamps
                    .Sum(m => (m.CompletedDate!.Value - m.StartedAt!.Value).TotalHours);
                MTTRHours = (decimal)(totalRepairHours / correctiveWithBothTimestamps.Count);
            }
            else
            {
                MTTRHours = 0;
            }

            // PR #104 (B-15): MTBF = Mean Time Between Failures. For each
            // asset that has 2+ completed corrective WOs in the window,
            // compute the gaps between consecutive close-times and average
            // them. Then average those per-asset MTBFs across the org so a
            // 50-asset fleet doesn't get dominated by one asset that fails
            // every shift.
            //
            // Edge cases:
            // - Asset with 0 or 1 corrective WO → no MTBF data point (skip)
            // - All assets with 0 or 1 → org MTBF is 0 + MTBFHasData=false
            // - Sub-hour MTBFs are theoretically valid but visually scary;
            //   we report the raw number and let the UI format/contextualize
            var correctiveCloses = maintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.Completed
                            && m.CompletedDate.HasValue
                            && m.Type == MaintenanceType.Corrective
                            && m.AssetId > 0)
                .GroupBy(m => m.AssetId)
                .Select(g => g.Select(m => m.CompletedDate!.Value).OrderBy(d => d).ToList())
                .Where(closes => closes.Count >= 2)
                .ToList();
            if (correctiveCloses.Count > 0)
            {
                decimal sumOfPerAssetMtbfHours = 0m;
                int assetsCounted = 0;
                foreach (var asset in correctiveCloses)
                {
                    decimal gapSum = 0m;
                    for (int i = 1; i < asset.Count; i++)
                    {
                        gapSum += (decimal)(asset[i] - asset[i - 1]).TotalHours;
                    }
                    var perAssetMtbf = gapSum / (asset.Count - 1);
                    sumOfPerAssetMtbfHours += perAssetMtbf;
                    assetsCounted++;
                }
                MTBFHours = sumOfPerAssetMtbfHours / assetsCounted;
                MTBFHasData = true;
            }
            else
            {
                MTBFHours = 0;
                MTBFHasData = false;
            }

            // PR #111: Top 5 worst-MTBF assets — same gap algorithm as the org
            // MTBF above, but per-asset and ranked. Worst = LOWEST MTBF (failing
            // most frequently). Need the AssetNumber + Description for the tile,
            // so we re-join against assets after the grouping.
            var perAssetMtbfList = maintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.Completed
                            && m.CompletedDate.HasValue
                            && m.Type == MaintenanceType.Corrective
                            && m.AssetId > 0)
                .GroupBy(m => m.AssetId)
                .Select(g =>
                {
                    var closes = g.Select(m => m.CompletedDate!.Value).OrderBy(d => d).ToList();
                    if (closes.Count < 2) return new { AssetId = g.Key, Mtbf = (decimal?)null, Count = closes.Count };
                    decimal gapSum = 0m;
                    for (int i = 1; i < closes.Count; i++)
                        gapSum += (decimal)(closes[i] - closes[i - 1]).TotalHours;
                    return new { AssetId = g.Key, Mtbf = (decimal?)Math.Round(gapSum / (closes.Count - 1), 1), Count = closes.Count };
                })
                .Where(x => x.Mtbf.HasValue)
                .OrderBy(x => x.Mtbf!.Value) // ascending — worst (lowest) first
                .Take(5)
                .ToList();
            var worstAssetIds = perAssetMtbfList.Select(x => x.AssetId).ToList();
            var worstAssetsLookup = await _db.Assets
                .Where(a => worstAssetIds.Contains(a.Id))
                .Select(a => new { a.Id, a.AssetNumber, Description = a.Description ?? "" })
                .ToListAsync();
            WorstMtbfAssets = perAssetMtbfList
                .Join(worstAssetsLookup, p => p.AssetId, a => a.Id, (p, a) => new WorstMtbfRow(
                    a.Id, a.AssetNumber, a.Description, p.Mtbf!.Value, p.Count))
                .ToList();

            // PR #111: Top 5 most-overdue WOs — open WOs whose ScheduledDate
            // is the furthest in the past. Drill-through to /WorkOrders/Details.
            TopOverdueWos = maintenanceEvents
                .Where(m => m.Status != MaintenanceStatus.Completed
                         && m.Status != MaintenanceStatus.Cancelled
                         && m.ScheduledDate < now)
                .OrderBy(m => m.ScheduledDate)
                .Take(5)
                .Select(m => new OverdueWoRow(
                    WoId: m.Id,
                    WoNumber: m.WorkOrderNumber ?? $"WO#{m.Id}",
                    AssetNumber: assets.FirstOrDefault(a => a.Id == m.AssetId)?.AssetNumber ?? "?",
                    ScheduledDate: m.ScheduledDate,
                    DaysOverdue: (int)Math.Floor((now - m.ScheduledDate).TotalDays),
                    Priority: m.Priority.ToString()))
                .ToList();

            // PR #111: Backlog by priority — count of open (Scheduled/InProgress/OnHold)
            // WOs grouped by Priority enum. Renders as a horizontal stack for an
            // at-a-glance view of where the team's attention should be.
            BacklogByPriority = maintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.Scheduled
                         || m.Status == MaintenanceStatus.InProgress
                         || m.Status == MaintenanceStatus.OnHold)
                .GroupBy(m => m.Priority.ToString())
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // PR #111: On-schedule completion % — of WOs completed in the last
            // 90 days, what % finished on or before their ScheduledDate. The
            // classic "are we hitting our commitments" KPI. Closely related to
            // PMCompliancePercent but covers all WO types not just PM.
            var ninetyDaysAgo = now.AddDays(-90);
            var recentCompleted = maintenanceEvents
                .Where(m => m.Status == MaintenanceStatus.Completed
                         && m.CompletedDate.HasValue
                         && m.CompletedDate >= ninetyDaysAgo)
                .ToList();
            TotalCompletedWoCount = recentCompleted.Count;
            OnScheduleWoCount = recentCompleted.Count(m => m.CompletedDate <= m.ScheduledDate.AddDays(1));
            OnScheduleCompletionPercent = TotalCompletedWoCount == 0
                ? 0m
                : Math.Round(100m * OnScheduleWoCount / TotalCompletedWoCount, 1);

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
