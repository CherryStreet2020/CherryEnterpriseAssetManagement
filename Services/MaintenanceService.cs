using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class MaintenanceService
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly ILookupService? _lookupService;

        public MaintenanceService(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        }

        public MaintenanceService(AppDbContext context, ITenantContext tenantContext, ILookupService lookupService)
        {
            _context = context;
            _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
            _lookupService = lookupService;
        }

        private async Task<int?> ResolveMaintenanceStatusFkAsync(MaintenanceStatus status)
        {
            if (_lookupService == null) return null;
            var lv = await _lookupService.GetValueByCodeAsync(null, null, "MaintenanceStatus", ((int)status).ToString());
            return lv?.Id;
        }

        private int GetCompanyId() => _tenantContext.CompanyId ?? 0;

        private IQueryable<MaintenanceEvent> GetScopedEventsQuery()
        {
            var companyId = GetCompanyId();
            return _context.MaintenanceEvents
                .Include(x => x.Asset)
                .Where(x => x.Asset != null && _tenantContext.VisibleCompanyIds.Contains(x.Asset.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || x.Asset.SiteId == _tenantContext.SiteId.Value));
        }

        public async Task<List<MaintenanceEvent>> GetAllEventsAsync()
        {
            return await GetScopedEventsQuery()
                .AsNoTracking()
                .OrderByDescending(x => x.ScheduledDate)
                .Take(500)
                .ToListAsync();
        }

        public async Task<List<MaintenanceEvent>> GetEventsForDashboardAsync(string? filter, int limit = 250)
        {
            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var thirtyDaysAgo = now.AddDays(-30);

            IQueryable<MaintenanceEvent> query = GetScopedEventsQuery().AsNoTracking();

            query = filter?.ToLower() switch
            {
                "overdue" => query
                    .Where(e => e.Status != MaintenanceStatus.Completed && 
                                e.Status != MaintenanceStatus.Cancelled && 
                                e.ScheduledDate < now)
                    .OrderBy(e => e.ScheduledDate),
                "scheduled" => query
                    .Where(e => e.Status == MaintenanceStatus.Scheduled)
                    .OrderBy(e => e.ScheduledDate),
                "inprogress" => query
                    .Where(e => e.Status == MaintenanceStatus.InProgress)
                    .OrderByDescending(e => e.ScheduledDate),
                "completed" => query
                    .Where(e => e.Status == MaintenanceStatus.Completed && 
                                e.CompletedDate >= thirtyDaysAgo)
                    .OrderByDescending(e => e.CompletedDate),
                _ => query.OrderByDescending(e => e.ScheduledDate)
            };

            return await query.Take(limit).ToListAsync();
        }

        public async Task<List<MaintenanceEvent>> GetUpcomingEventsAsync(int days = 30)
        {
            var cutoffDate = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(days), DateTimeKind.Unspecified);
            return await GetScopedEventsQuery()
                .AsNoTracking()
                .Where(x => x.Status != MaintenanceStatus.Completed && 
                            x.Status != MaintenanceStatus.Cancelled &&
                            x.ScheduledDate <= cutoffDate)
                .OrderBy(x => x.ScheduledDate)
                .ToListAsync();
        }

        public async Task<List<MaintenanceEvent>> GetOverdueEventsAsync()
        {
            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            return await GetScopedEventsQuery()
                .AsNoTracking()
                .Where(x => x.Status != MaintenanceStatus.Completed && 
                            x.Status != MaintenanceStatus.Cancelled &&
                            x.ScheduledDate < now)
                .OrderBy(x => x.ScheduledDate)
                .ToListAsync();
        }

        public async Task<MaintenanceEvent?> GetEventAsync(int id)
        {
            var companyId = GetCompanyId();
            return await _context.MaintenanceEvents
                .Include(x => x.Asset)
                .Where(x => x.Asset != null && _tenantContext.VisibleCompanyIds.Contains(x.Asset.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || x.Asset.SiteId == _tenantContext.SiteId.Value))
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<MaintenanceEvent?> CreateEventAsync(MaintenanceEvent evt)
        {
            // Tenant scoping is mandatory: the asset must belong to a company
            // visible to the current tenant. No conditional null-guard — the
            // service contract requires a resolved ITenantContext (see ctor).
            var assetBelongsToTenant = await _context.Assets
                .AnyAsync(a => a.Id == evt.AssetId
                    && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value));
            if (!assetBelongsToTenant)
            {
                return null;
            }

            evt.CreatedAt = DateTime.UtcNow;
            
            if (string.IsNullOrEmpty(evt.WorkOrderNumber))
            {
                evt.WorkOrderNumber = await GenerateWorkOrderNumberAsync();
            }
            
            _context.MaintenanceEvents.Add(evt);
            await _context.SaveChangesAsync();
            return evt;
        }

        public async Task<string> GenerateWorkOrderNumberAsync()
        {
            var year = DateTime.UtcNow.Year.ToString().Substring(2);
            var prefix = $"WO-{year}-";
            
            var lastWO = await _context.MaintenanceEvents
                .Where(e => e.WorkOrderNumber != null && e.WorkOrderNumber.StartsWith(prefix))
                .OrderByDescending(e => e.WorkOrderNumber)
                .FirstOrDefaultAsync();
            
            var nextNum = 1;
            if (lastWO?.WorkOrderNumber != null)
            {
                var lastNumStr = lastWO.WorkOrderNumber.Replace(prefix, "");
                if (int.TryParse(lastNumStr, out var lastNum))
                {
                    nextNum = lastNum + 1;
                }
            }
            
            return $"{prefix}{nextNum:D5}";
        }

        public async Task<MaintenanceEvent?> UpdateEventAsync(MaintenanceEvent evt)
        {
            // Tenant scoping is mandatory — see CreateEventAsync.
            var exists = await _context.MaintenanceEvents
                .Include(e => e.Asset)
                .AnyAsync(e => e.Id == evt.Id
                    && e.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(e.Asset.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || e.Asset.SiteId == _tenantContext.SiteId.Value));
            if (!exists)
            {
                return null;
            }

            _context.MaintenanceEvents.Update(evt);
            await _context.SaveChangesAsync();
            return evt;
        }

        public async Task<MaintenanceEvent?> CompleteEventAsync(int id, string resolution, decimal actualCost)
        {
            // Tenant scoping is mandatory — no unscoped fallback. The previous
            // version skipped scoping when CompanyId was null; that mirrored
            // the AccountsPayable leak fixed in PR #22 and is closed here.
            var evt = await _context.MaintenanceEvents
                .Include(e => e.Asset)
                .Where(e => e.Id == id
                    && e.Asset != null
                    && _tenantContext.VisibleCompanyIds.Contains(e.Asset.CompanyId ?? 0)
                    && (!_tenantContext.SiteId.HasValue || e.Asset.SiteId == _tenantContext.SiteId.Value))
                .FirstOrDefaultAsync();

            if (evt != null)
            {
                evt.Status = MaintenanceStatus.Completed;
                evt.StatusLookupValueId = await ResolveMaintenanceStatusFkAsync(MaintenanceStatus.Completed);
                evt.CompletedDate = DateTime.UtcNow;
                evt.Resolution = resolution;
                evt.ActualCost = actualCost;
                await _context.SaveChangesAsync();
                
                await UpdatePMAssignmentOnCompletionAsync(evt);
            }
            return evt;
        }
        
        private async Task UpdatePMAssignmentOnCompletionAsync(MaintenanceEvent evt)
        {
            if (evt.Type != MaintenanceType.Preventative) return;

            // S1-2: advance the PM cycle from the explicit FKs the scheduler
            // stamped (PMOccurrenceId, PMTemplateAssetId). Replaces the
            // previous CustomField1 = "PMTA:N" hack that confused
            // PMOccurrence.Id with PMTemplateAsset.Id and silently miss-targeted
            // rows or no-oped — see docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md.

            // 1. Mark the occurrence as Closed (the scheduler reads this to
            // know which occurrences have been fulfilled).
            if (evt.PMOccurrenceId.HasValue)
            {
                var occurrence = await _context.Set<PMOccurrence>()
                    .FirstOrDefaultAsync(o => o.Id == evt.PMOccurrenceId.Value);
                if (occurrence != null && occurrence.Status != PMOccurrenceStatus.Closed)
                {
                    occurrence.Status = PMOccurrenceStatus.Closed;
                    occurrence.UpdatedAtUtc = DateTime.UtcNow;
                }
            }

            // 2. Stamp the assignment's last-completed + advance NextDueDate
            // for legacy clients that still read PMTemplateAsset.NextDueDate
            // (the modern path uses PMSchedule.NextDueDateUtc, computed by
            // the scheduler — but template-asset-level dates remain as a
            // per-asset informational field).
            if (evt.PMTemplateAssetId.HasValue)
            {
                var assignment = await _context.Set<PMTemplateAsset>()
                    .Include(pa => pa.PMTemplate)
                    .FirstOrDefaultAsync(pa => pa.Id == evt.PMTemplateAssetId.Value && pa.IsActive);

                if (assignment != null)
                {
                    assignment.LastCompletedDate = DateTime.UtcNow;
                    assignment.UpdatedAt = DateTime.UtcNow;

                    if (assignment.PMTemplate != null)
                    {
                        int daysToAdd = assignment.PMTemplate.CalendarInterval switch
                        {
                            RecurrenceType.Daily => assignment.PMTemplate.CalendarIntervalValue,
                            RecurrenceType.Weekly => assignment.PMTemplate.CalendarIntervalValue * 7,
                            RecurrenceType.Monthly => assignment.PMTemplate.CalendarIntervalValue * 30,
                            RecurrenceType.Quarterly => assignment.PMTemplate.CalendarIntervalValue * 90,
                            RecurrenceType.Annually => assignment.PMTemplate.CalendarIntervalValue * 365,
                            _ => 30
                        };
                        assignment.NextDueDate = DateTime.UtcNow.Date.AddDays(daysToAdd);
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<MaintenanceEvent?> UpdateDispatchAsync(int id, MaintenancePriority priority, DateTime scheduledDate, int? technicianId)
        {
            var evt = await GetEventAsync(id);
            if (evt == null) return null;
            
            if (evt.Status == MaintenanceStatus.Completed || evt.Status == MaintenanceStatus.Cancelled)
                return null;
            
            if (technicianId.HasValue)
            {
                var techExists = await _context.Technicians
                    .AnyAsync(t => t.Id == technicianId.Value && t.Active);
                if (!techExists) return null;
            }
            
            evt.Priority = priority;
            evt.ScheduledDate = scheduledDate;
            evt.TechnicianId = technicianId;
            
            await _context.SaveChangesAsync();
            return evt;
        }

        public async Task<List<MaintenanceSchedule>> GetAllSchedulesAsync()
        {
            return await _context.MaintenanceSchedules
                .Include(x => x.Asset)
                .OrderBy(x => x.NextDueDate)
                .ToListAsync();
        }

        public async Task<MaintenanceSchedule> CreateScheduleAsync(MaintenanceSchedule schedule)
        {
            schedule.NextDueDate = schedule.StartDate;
            _context.MaintenanceSchedules.Add(schedule);
            await _context.SaveChangesAsync();
            return schedule;
        }

        public async Task<int> GenerateEventsFromSchedulesAsync()
        {
            var cutoff = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(7), DateTimeKind.Unspecified);
            var allSchedules = await _context.MaintenanceSchedules.ToListAsync();
            var schedules = allSchedules.Where(s => s.IsActive && s.NextDueDate <= cutoff).ToList();

            var count = 0;
            foreach (var schedule in schedules)
            {
                var woNumber = await GenerateWorkOrderNumberAsync();
                var evt = new MaintenanceEvent
                {
                    AssetId = schedule.AssetId,
                    Type = schedule.Type,
                    Description = $"[Scheduled] {schedule.Name}",
                    ScheduledDate = schedule.NextDueDate ?? DateTime.UtcNow,
                    Status = MaintenanceStatus.Scheduled,
                    Priority = MaintenancePriority.Medium,
                    EstimatedCost = schedule.EstimatedCost,
                    Vendor = schedule.AssignedVendor,
                    WorkOrderNumber = woNumber,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MaintenanceEvents.Add(evt);

                schedule.LastGeneratedDate = DateTime.UtcNow;
                schedule.NextDueDate = CalculateNextDueDate(schedule);
                count++;
            }

            await _context.SaveChangesAsync();
            return count;
        }

        private DateTime? CalculateNextDueDate(MaintenanceSchedule schedule)
        {
            if (schedule.NextDueDate == null) return null;
            
            DateTime? next = schedule.Recurrence switch
            {
                RecurrenceType.Daily => schedule.NextDueDate.Value.AddDays(schedule.IntervalValue),
                RecurrenceType.Weekly => schedule.NextDueDate.Value.AddDays(7 * schedule.IntervalValue),
                RecurrenceType.BiWeekly => schedule.NextDueDate.Value.AddDays(14 * schedule.IntervalValue),
                RecurrenceType.Monthly => schedule.NextDueDate.Value.AddMonths(schedule.IntervalValue),
                RecurrenceType.Quarterly => schedule.NextDueDate.Value.AddMonths(3 * schedule.IntervalValue),
                RecurrenceType.SemiAnnually => schedule.NextDueDate.Value.AddMonths(6 * schedule.IntervalValue),
                RecurrenceType.Annually => schedule.NextDueDate.Value.AddYears(schedule.IntervalValue),
                RecurrenceType.Custom => schedule.NextDueDate.Value.AddDays(schedule.IntervalValue),
                _ => null
            };

            if (schedule.EndDate.HasValue && next > schedule.EndDate)
                return null;

            return next;
        }

        public async Task<int> BackfillMissingWorkOrderNumbersAsync()
        {
            var eventsWithoutWO = await _context.MaintenanceEvents
                .Where(e => e.WorkOrderNumber == null || e.WorkOrderNumber == "")
                .OrderBy(e => e.Id)
                .ToListAsync();

            foreach (var evt in eventsWithoutWO)
            {
                evt.WorkOrderNumber = await GenerateWorkOrderNumberAsync();
            }

            if (eventsWithoutWO.Any())
            {
                await _context.SaveChangesAsync();
            }

            return eventsWithoutWO.Count;
        }

        public async Task<MaintenanceStats> GetMaintenanceStatsAsync()
        {
            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var monthStart = DateTime.SpecifyKind(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), DateTimeKind.Unspecified);
            var companyId = GetCompanyId();
            
            var baseQuery = _context.MaintenanceEvents
                .Include(x => x.Asset)
                .Where(x => x.Asset != null && _tenantContext.VisibleCompanyIds.Contains(x.Asset.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || x.Asset.SiteId == _tenantContext.SiteId.Value));
            
            var totalEvents = await baseQuery.CountAsync();
            
            var overdueCount = await baseQuery
                .Where(x => x.Status != MaintenanceStatus.Completed && 
                            x.Status != MaintenanceStatus.Cancelled &&
                            x.ScheduledDate < now)
                .CountAsync();
            
            var scheduledCount = await baseQuery
                .Where(x => x.Status == MaintenanceStatus.Scheduled)
                .CountAsync();
            
            var completedThisMonth = await baseQuery
                .Where(x => x.Status == MaintenanceStatus.Completed &&
                            x.CompletedDate >= monthStart)
                .CountAsync();
            
            var totalCostThisMonth = await baseQuery
                .Where(x => x.Status == MaintenanceStatus.Completed &&
                            x.CompletedDate >= monthStart)
                .SumAsync(x => x.ActualCost ?? 0);

            return new MaintenanceStats
            {
                TotalEvents = totalEvents,
                OverdueCount = overdueCount,
                ScheduledCount = scheduledCount,
                CompletedThisMonth = completedThisMonth,
                TotalCostThisMonth = totalCostThisMonth
            };
        }

        private async Task<MaintenanceEvent?> LoadWorkOrderForCompanyAsync(int id, int companyId, bool tracking = true)
        {
            var query = _context.MaintenanceEvents
                .Include(x => x.Asset)
                .Include(x => x.Operations)
                .Where(x => x.Asset != null && _tenantContext.VisibleCompanyIds.Contains(x.Asset.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || x.Asset.SiteId == _tenantContext.SiteId.Value) && x.Id == id);

            if (!tracking)
                query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync();
        }

        private Task WriteAuditLogAsync(string action, int entityId, string username, string? description = null)
        {
            var audit = new AuditLog
            {
                EntityType = "MaintenanceEvent",
                EntityId = entityId,
                Action = action,
                Username = username,
                Timestamp = DateTime.UtcNow,
                Description = description ?? $"{action} by {username}"
            };
            _context.AuditLogs.Add(audit);
            return Task.CompletedTask;
        }

        public async Task<ExecutionResult> StartAsync(int workOrderId, string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return ExecutionResult.Fail("Username is required");

            var companyId = GetCompanyId();
            var wo = await LoadWorkOrderForCompanyAsync(workOrderId, companyId);

            if (wo == null)
                return ExecutionResult.Fail("Work order not found");

            if (wo.Status == MaintenanceStatus.Completed || wo.Status == MaintenanceStatus.Cancelled)
                return ExecutionResult.Fail($"Cannot start work order with status {wo.Status}");

            if (wo.Status == MaintenanceStatus.InProgress)
                return ExecutionResult.NoOp("Work order is already in progress");

            if (wo.Status != MaintenanceStatus.Scheduled && wo.Status != MaintenanceStatus.OnHold)
                return ExecutionResult.Fail($"Cannot start work order from status {wo.Status}");

            wo.Status = MaintenanceStatus.InProgress;
            wo.StatusLookupValueId = await ResolveMaintenanceStatusFkAsync(MaintenanceStatus.InProgress);
            wo.StartedAt = DateTime.UtcNow;
            wo.StartedBy = userName;

            await WriteAuditLogAsync("WORKORDER_STARTED", workOrderId, userName, $"Work order started by {userName}");
            await _context.SaveChangesAsync();

            return ExecutionResult.Success(wo);
        }

        public async Task<ExecutionResult> PauseAsync(int workOrderId, string reason, string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return ExecutionResult.Fail("Username is required");

            var companyId = GetCompanyId();
            var wo = await LoadWorkOrderForCompanyAsync(workOrderId, companyId);

            if (wo == null)
                return ExecutionResult.Fail("Work order not found");

            if (wo.Status != MaintenanceStatus.InProgress)
                return ExecutionResult.Fail($"Cannot pause work order with status {wo.Status}. Must be InProgress.");

            wo.Status = MaintenanceStatus.OnHold;
            wo.StatusLookupValueId = await ResolveMaintenanceStatusFkAsync(MaintenanceStatus.OnHold);
            wo.HoldReason = reason;

            await WriteAuditLogAsync("WORKORDER_PAUSED", workOrderId, userName, $"Work order paused: {reason}");
            await _context.SaveChangesAsync();

            return ExecutionResult.Success(wo);
        }

        public async Task<ExecutionResult> ResumeAsync(int workOrderId, string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return ExecutionResult.Fail("Username is required");

            var companyId = GetCompanyId();
            var wo = await LoadWorkOrderForCompanyAsync(workOrderId, companyId);

            if (wo == null)
                return ExecutionResult.Fail("Work order not found");

            if (wo.Status != MaintenanceStatus.OnHold)
                return ExecutionResult.Fail($"Cannot resume work order with status {wo.Status}. Must be OnHold.");

            wo.Status = MaintenanceStatus.InProgress;
            wo.StatusLookupValueId = await ResolveMaintenanceStatusFkAsync(MaintenanceStatus.InProgress);
            wo.HoldReason = null;

            await WriteAuditLogAsync("WORKORDER_RESUMED", workOrderId, userName, $"Work order resumed by {userName}");
            await _context.SaveChangesAsync();

            return ExecutionResult.Success(wo);
        }

        public async Task<OperationResult> AddOperationAsync(int workOrderId, OperationCreateDto dto, string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return OperationResult.Fail("Username is required");

            if (string.IsNullOrEmpty(dto.Title))
                return OperationResult.Fail("Operation title is required");

            var companyId = GetCompanyId();
            var wo = await LoadWorkOrderForCompanyAsync(workOrderId, companyId);

            if (wo == null)
                return OperationResult.Fail("Work order not found");

            var existingOps = wo.Operations?.ToList() ?? new List<WorkOrderOperation>();
            var maxSequence = existingOps.Any() ? existingOps.Max(o => o.Sequence) : 0;
            var newSequence = dto.Sequence > 0 ? dto.Sequence : maxSequence + 10;

            var opNumber = $"OP-{(existingOps.Count + 1):D3}";

            var operation = new WorkOrderOperation
            {
                MaintenanceEventId = workOrderId,
                OperationNumber = opNumber,
                Title = dto.Title,
                Description = dto.Description,
                Type = dto.Type ?? OperationType.Mechanical,
                Sequence = newSequence,
                Status = OperationStatus.Pending,
                PlannedHours = dto.EstimatedHours ?? 0
            };

            _context.WorkOrderOperations.Add(operation);
            await WriteAuditLogAsync("WORKORDER_OPERATION_ADDED", workOrderId, userName, $"Operation '{dto.Title}' added");
            await _context.SaveChangesAsync();

            return OperationResult.Success(operation);
        }

        public async Task<ExecutionResult> ReorderOperationsAsync(int workOrderId, int[] orderedOperationIds, string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return ExecutionResult.Fail("Username is required");

            var companyId = GetCompanyId();
            var wo = await LoadWorkOrderForCompanyAsync(workOrderId, companyId);

            if (wo == null)
                return ExecutionResult.Fail("Work order not found");

            var operations = wo.Operations?.ToList() ?? new List<WorkOrderOperation>();
            var opIds = operations.Select(o => o.Id).ToHashSet();

            if (!orderedOperationIds.All(id => opIds.Contains(id)))
                return ExecutionResult.Fail("Some operation IDs do not belong to this work order");

            if (orderedOperationIds.Length != orderedOperationIds.Distinct().Count())
                return ExecutionResult.Fail("Duplicate operation IDs provided");

            var sequence = 10;
            foreach (var opId in orderedOperationIds)
            {
                var op = operations.First(o => o.Id == opId);
                op.Sequence = sequence;
                sequence += 10;
            }

            await WriteAuditLogAsync("WORKORDER_OPERATIONS_REORDERED", workOrderId, userName, $"Operations reordered: {string.Join(",", orderedOperationIds)}");
            await _context.SaveChangesAsync();

            return ExecutionResult.Success(wo);
        }

        public async Task<OperationResult> CompleteOperationAsync(int operationId, OperationCompleteDto dto, string userName)
        {
            if (string.IsNullOrEmpty(userName))
                return OperationResult.Fail("Username is required");

            var companyId = GetCompanyId();

            var operation = await _context.WorkOrderOperations
                .Include(o => o.MaintenanceEvent)
                    .ThenInclude(m => m!.Asset)
                .FirstOrDefaultAsync(o => o.Id == operationId);

            if (operation == null)
                return OperationResult.Fail("Operation not found");

            if (operation.MaintenanceEvent?.Asset?.CompanyId != companyId)
                return OperationResult.Fail("Operation not found");

            if (operation.Status == OperationStatus.Completed)
                return OperationResult.NoOp("Operation is already completed");

            operation.Status = OperationStatus.Completed;
            operation.CompletedAt = DateTime.UtcNow;
            operation.CompletedBy = userName;
            if (dto.ActualHours.HasValue)
                operation.ActualHours = dto.ActualHours.Value;
            if (!string.IsNullOrEmpty(dto.Notes))
            {
                operation.Description = string.IsNullOrEmpty(operation.Description) 
                    ? dto.Notes 
                    : $"{operation.Description}\n\n[Completion Notes]: {dto.Notes}";
            }

            await WriteAuditLogAsync("WORKORDER_OPERATION_COMPLETED", operation.MaintenanceEventId, userName, $"Operation '{operation.Title}' completed");
            await _context.SaveChangesAsync();

            return OperationResult.Success(operation);
        }

        public async Task<bool> AreAllOperationsCompleteAsync(int workOrderId)
        {
            var companyId = GetCompanyId();
            var wo = await LoadWorkOrderForCompanyAsync(workOrderId, companyId, tracking: false);
            if (wo == null) return false;

            var operations = wo.Operations?.ToList() ?? new List<WorkOrderOperation>();
            if (!operations.Any()) return true;

            return operations.All(o => o.Status == OperationStatus.Completed || o.Status == OperationStatus.Cancelled);
        }
    }

    public class ExecutionResult
    {
        public bool IsSuccess { get; set; }
        public bool IsNoOp { get; set; }
        public string? Error { get; set; }
        public MaintenanceEvent? WorkOrder { get; set; }

        public static ExecutionResult Success(MaintenanceEvent wo) => new() { IsSuccess = true, WorkOrder = wo };
        public static ExecutionResult NoOp(string message) => new() { IsSuccess = true, IsNoOp = true, Error = message };
        public static ExecutionResult Fail(string error) => new() { IsSuccess = false, Error = error };
    }

    public class OperationResult
    {
        public bool IsSuccess { get; set; }
        public bool IsNoOp { get; set; }
        public string? Error { get; set; }
        public WorkOrderOperation? Operation { get; set; }

        public static OperationResult Success(WorkOrderOperation op) => new() { IsSuccess = true, Operation = op };
        public static OperationResult NoOp(string message) => new() { IsSuccess = true, IsNoOp = true, Error = message };
        public static OperationResult Fail(string error) => new() { IsSuccess = false, Error = error };
    }

    public class OperationCreateDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public OperationType? Type { get; set; }
        public int Sequence { get; set; }
        public decimal? EstimatedHours { get; set; }
    }

    public class OperationCompleteDto
    {
        public decimal? ActualHours { get; set; }
        public string? Notes { get; set; }
    }

    public class MaintenanceStats
    {
        public int TotalEvents { get; set; }
        public int OverdueCount { get; set; }
        public int ScheduledCount { get; set; }
        public int CompletedThisMonth { get; set; }
        public decimal TotalCostThisMonth { get; set; }
    }
}
