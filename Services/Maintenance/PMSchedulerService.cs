using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Abs.FixedAssets.Services.Integrations;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Maintenance;

public class PMGenerationPreview
{
    public int PMScheduleId { get; set; }
    public string ScheduleName { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public int PMTemplateId { get; set; }
    public DateTime DueDateUtc { get; set; }
    public int? TenantId { get; set; }
    public int? CompanyId { get; set; }
    public int? SiteId { get; set; }
    public bool AlreadyExists { get; set; }
}

public class PMGenerationResult
{
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<int> CreatedWorkOrderIds { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public interface IPMSchedulerService
{
    Task<List<PMGenerationPreview>> PreviewDueAsync(int horizonDays, DateTime nowUtc, int? tenantId = null, int? companyId = null, int? siteId = null);
    Task<PMGenerationResult> GenerateDueAsync(int horizonDays, DateTime nowUtc, string? initiatedByUserId = null, int? tenantId = null, int? companyId = null, int? siteId = null);
    Task<List<DateTime>> ComputeDueDatesAsync(PMSchedule schedule, DateTime fromUtc, DateTime toUtc);
}

public class PMSchedulerService : IPMSchedulerService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PMSchedulerService> _logger;

    public PMSchedulerService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<PMSchedulerService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<PMGenerationPreview>> PreviewDueAsync(
        int horizonDays, 
        DateTime nowUtc, 
        int? tenantId = null, 
        int? companyId = null, 
        int? siteId = null)
    {
        tenantId ??= _tenantContext.TenantId;
        companyId ??= _tenantContext.CompanyId;
        siteId ??= _tenantContext.SiteId;

        var horizon = nowUtc.AddDays(horizonDays);
        var previews = new List<PMGenerationPreview>();

        var schedulesQuery = _db.PMSchedules
            .Include(s => s.PMTemplate)
            .Where(s => s.Active);

        if (tenantId.HasValue)
            schedulesQuery = schedulesQuery.Where(s => s.TenantId == tenantId);
        if (companyId.HasValue)
            schedulesQuery = schedulesQuery.Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId ?? 0));
        if (siteId.HasValue)
            schedulesQuery = schedulesQuery.Where(s => s.SiteId == siteId);

        var schedules = await schedulesQuery.ToListAsync();

        foreach (var schedule in schedules)
        {
            var dueDates = await ComputeDueDatesAsync(schedule, nowUtc, horizon);

            foreach (var dueDate in dueDates)
            {
                var exists = await _db.PMOccurrences.AnyAsync(o =>
                    o.TenantId == schedule.TenantId &&
                    o.CompanyId == schedule.CompanyId &&
                    o.SiteId == schedule.SiteId &&
                    o.PMTemplateId == schedule.PMTemplateId &&
                    o.DueDateUtc.Date == dueDate.Date);

                previews.Add(new PMGenerationPreview
                {
                    PMScheduleId = schedule.Id,
                    ScheduleName = schedule.Name,
                    TemplateName = schedule.PMTemplate?.Name ?? "Unknown",
                    PMTemplateId = schedule.PMTemplateId,
                    DueDateUtc = dueDate,
                    TenantId = schedule.TenantId,
                    CompanyId = schedule.CompanyId,
                    SiteId = schedule.SiteId,
                    AlreadyExists = exists
                });
            }
        }

        return previews.OrderBy(p => p.DueDateUtc).ToList();
    }

    public async Task<PMGenerationResult> GenerateDueAsync(
        int horizonDays,
        DateTime nowUtc,
        string? initiatedByUserId = null,
        int? tenantId = null,
        int? companyId = null,
        int? siteId = null)
    {
        tenantId ??= _tenantContext.TenantId;
        companyId ??= _tenantContext.CompanyId;
        siteId ??= _tenantContext.SiteId;

        var result = new PMGenerationResult();
        var horizon = nowUtc.AddDays(horizonDays);

        var schedulesQuery = _db.PMSchedules
            .Include(s => s.PMTemplate)
                .ThenInclude(t => t!.Items)
            .Include(s => s.PMTemplate)
                .ThenInclude(t => t!.CurrentReleasedRevision)
                    .ThenInclude(r => r!.Operations)
            .Where(s => s.Active);

        if (tenantId.HasValue)
            schedulesQuery = schedulesQuery.Where(s => s.TenantId == tenantId);
        if (companyId.HasValue)
            schedulesQuery = schedulesQuery.Where(s => _tenantContext.VisibleCompanyIds.Contains(s.CompanyId ?? 0));
        if (siteId.HasValue)
            schedulesQuery = schedulesQuery.Where(s => s.SiteId == siteId);

        var schedules = await schedulesQuery.ToListAsync();

        foreach (var schedule in schedules)
        {
            var dueDates = await ComputeDueDatesAsync(schedule, nowUtc, horizon);

            foreach (var dueDate in dueDates)
            {
                try
                {
                    var created = await CreateOccurrenceAndWorkOrderAsync(
                        schedule, dueDate, initiatedByUserId, tenantId, companyId, siteId);

                    if (created.HasValue)
                    {
                        result.CreatedCount++;
                        result.CreatedWorkOrderIds.Add(created.Value);
                    }
                    else
                    {
                        result.SkippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Schedule {schedule.Id} Date {dueDate:yyyy-MM-dd}: {ex.Message}");
                    _logger.LogError(ex, "Failed to generate PM occurrence for schedule {ScheduleId} date {DueDate}", schedule.Id, dueDate);
                }
            }
        }

        if (result.CreatedCount > 0)
        {
            try
            {
                await _db.AuditLogs.AddAsync(new AuditLog
                {
                    Action = "PMGENERATE",
                    EntityType = "PMSchedule",
                    EntityId = 0,
                    Username = initiatedByUserId,
                    AfterJson = System.Text.Json.JsonSerializer.Serialize(new { 
                        CreatedCount = result.CreatedCount, 
                        HorizonDays = horizonDays,
                        WorkOrderIds = result.CreatedWorkOrderIds
                    }),
                    Timestamp = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write PMGENERATE audit log");
            }
        }

        return result;
    }

    private async Task<int?> CreateOccurrenceAndWorkOrderAsync(
        PMSchedule schedule,
        DateTime dueDate,
        string? userId,
        int? tenantId,
        int? companyId,
        int? siteId)
    {
        var normalizedDate = DateTime.SpecifyKind(dueDate.Date, DateTimeKind.Utc);

        var existingOccurrence = await _db.PMOccurrences
            .FirstOrDefaultAsync(o =>
                o.TenantId == (tenantId ?? schedule.TenantId) &&
                o.CompanyId == (companyId ?? schedule.CompanyId) &&
                o.SiteId == (siteId ?? schedule.SiteId) &&
                o.PMTemplateId == schedule.PMTemplateId &&
                o.DueDateUtc.Date == normalizedDate.Date);

        if (existingOccurrence != null)
        {
            return null;
        }

        var occurrence = new PMOccurrence
        {
            TenantId = tenantId ?? schedule.TenantId,
            CompanyId = companyId ?? schedule.CompanyId,
            SiteId = siteId ?? schedule.SiteId,
            PMScheduleId = schedule.Id,
            PMTemplateId = schedule.PMTemplateId,
            DueDateUtc = normalizedDate,
            Status = PMOccurrenceStatus.Created,
            GeneratedBy = userId,
            GeneratedAt = DateTime.UtcNow
        };

        _db.PMOccurrences.Add(occurrence);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate") == true ||
                                            ex.InnerException?.Message.Contains("unique") == true)
        {
            _db.Entry(occurrence).State = EntityState.Detached;
            return null;
        }

        var template = schedule.PMTemplate;
        if (template == null)
        {
            template = await _db.PMTemplates
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == schedule.PMTemplateId);
        }

        var assetIds = await _db.PMTemplateAssets
            .Where(ta => ta.PMTemplateId == schedule.PMTemplateId && ta.IsActive)
            .Select(ta => ta.AssetId)
            .ToListAsync();

        if (!assetIds.Any())
        {
            assetIds = new List<int> { 0 };
        }

        int? createdWorkOrderId = null;

        foreach (var assetId in assetIds)
        {
            var workOrder = await CreateWorkOrderFromTemplateAsync(
                schedule, template, occurrence, assetId, normalizedDate, userId, tenantId, companyId, siteId);

            if (workOrder != null)
            {
                createdWorkOrderId ??= workOrder.Id;
            }
        }

        if (createdWorkOrderId.HasValue)
        {
            occurrence.WorkOrderId = createdWorkOrderId;
            await _db.SaveChangesAsync();

            await EnqueueOutboxEventsAsync(createdWorkOrderId.Value, occurrence.Id, tenantId, companyId, siteId);
        }

        schedule.NextDueDateUtc = await ComputeNextDueDateAsync(schedule, normalizedDate);
        await _db.SaveChangesAsync();

        return createdWorkOrderId;
    }

    private async Task<MaintenanceEvent?> CreateWorkOrderFromTemplateAsync(
        PMSchedule schedule,
        PMTemplate? template,
        PMOccurrence occurrence,
        int assetId,
        DateTime dueDate,
        string? userId,
        int? tenantId,
        int? companyId,
        int? siteId)
    {
        if (template == null) return null;

        var revision = template.CurrentReleasedRevision;
        var useName = revision?.Name ?? template.Name;
        var useProcedure = revision?.Procedure ?? template.Procedure;
        var useSafetyInstructions = revision?.SafetyInstructions ?? template.SafetyInstructions;
        var useType = revision?.Type ?? template.Type;
        var usePriority = revision?.Priority ?? template.Priority;
        var useTotalCost = revision?.EstimatedTotalCost ?? template.EstimatedTotalCost ?? 0;
        var revisionRef = revision != null ? $" (Rev {revision.RevisionCode})" : "";

        var woNumber = await GenerateWorkOrderNumberAsync();

        var workOrder = new MaintenanceEvent
        {
            AssetId = assetId > 0 ? assetId : 1,
            Type = useType,
            Description = $"[PM] {useName}{revisionRef}",
            Status = MaintenanceStatus.Scheduled,
            Priority = ConvertPriority(usePriority),
            ScheduledDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Unspecified),
            EstimatedCost = useTotalCost,
            WorkOrderNumber = woNumber,
            CustomField1 = $"PMTA:{occurrence.Id}"
        };

        _db.MaintenanceEvents.Add(workOrder);
        await _db.SaveChangesAsync();

        if (revision?.Operations?.Any() == true)
        {
            var seq = 10;
            foreach (var revOp in revision.Operations.OrderBy(o => o.Sequence))
            {
                var operation = new WorkOrderOperation
                {
                    MaintenanceEventId = workOrder.Id,
                    OperationNumber = seq.ToString("D3"),
                    Sequence = seq,
                    Type = OperationType.Inspection,
                    Title = revOp.Description,
                    Description = revOp.Notes,
                    Instructions = null,
                    Status = OperationStatus.Pending
                };
                _db.Set<WorkOrderOperation>().Add(operation);
                seq += 10;
            }
        }
        else if (!string.IsNullOrEmpty(useProcedure))
        {
            var operation = new WorkOrderOperation
            {
                MaintenanceEventId = workOrder.Id,
                OperationNumber = "010",
                Sequence = 10,
                Type = OperationType.Inspection,
                Title = useName,
                Description = useProcedure,
                Instructions = useSafetyInstructions,
                Status = OperationStatus.Pending
            };
            _db.Set<WorkOrderOperation>().Add(operation);
        }

        if (template.Items?.Any() == true)
        {
            foreach (var item in template.Items)
            {
                var part = new WorkOrderPart
                {
                    MaintenanceEventId = workOrder.Id,
                    ItemId = item.ItemId,
                    QuantityPlanned = item.Quantity,
                    CreatedAt = DateTime.UtcNow
                };
                _db.WorkOrderParts.Add(part);
            }
        }

        await _db.SaveChangesAsync();
        return workOrder;
    }

    private async Task EnqueueOutboxEventsAsync(int workOrderId, int occurrenceId, int? tenantId, int? companyId, int? siteId)
    {
        try
        {
            var evt = new OutboxEvent
            {
                EventType = "workorder.created",
                EntityType = "MaintenanceEvent",
                EntityId = workOrderId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    workOrderId,
                    source = "pm_schedule",
                    occurrenceId
                }),
                TenantId = tenantId,
                CompanyId = companyId ?? 0,
                SiteId = siteId
            };
            _db.OutboxEvents.Add(evt);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enqueue outbox event for work order {WorkOrderId}", workOrderId);
        }
    }

    public Task<List<DateTime>> ComputeDueDatesAsync(PMSchedule schedule, DateTime fromUtc, DateTime toUtc)
    {
        var dates = new List<DateTime>();

        if (schedule.StartDateUtc > toUtc)
            return Task.FromResult(dates);

        var startDate = schedule.StartDateUtc > fromUtc ? schedule.StartDateUtc : fromUtc;
        var currentDate = schedule.StartDateUtc;

        while (currentDate < startDate)
        {
            currentDate = GetNextScheduledDate(schedule, currentDate);
        }

        while (currentDate <= toUtc)
        {
            if (currentDate >= fromUtc)
            {
                dates.Add(DateTime.SpecifyKind(currentDate.Date, DateTimeKind.Utc));
            }
            currentDate = GetNextScheduledDate(schedule, currentDate);
        }

        return Task.FromResult(dates);
    }

    private DateTime GetNextScheduledDate(PMSchedule schedule, DateTime current)
    {
        return schedule.CadenceType switch
        {
            PMCadenceType.IntervalDays => current.AddDays(schedule.IntervalDays ?? 30),
            PMCadenceType.Weekly => GetNextWeeklyDate(schedule, current),
            PMCadenceType.Monthly => GetNextMonthlyDate(schedule, current),
            _ => current.AddDays(30)
        };
    }

    private DateTime GetNextWeeklyDate(PMSchedule schedule, DateTime current)
    {
        if (!schedule.DaysOfWeekMask.HasValue || schedule.DaysOfWeekMask == 0)
            return current.AddDays(7);

        var mask = schedule.DaysOfWeekMask.Value;
        var checkDate = current.AddDays(1);

        for (int i = 0; i < 14; i++)
        {
            var dayBit = 1 << (int)checkDate.DayOfWeek;
            if ((mask & dayBit) != 0)
                return checkDate;
            checkDate = checkDate.AddDays(1);
        }

        return current.AddDays(7);
    }

    private DateTime GetNextMonthlyDate(PMSchedule schedule, DateTime current)
    {
        var dayOfMonth = schedule.DayOfMonth ?? 1;
        if (dayOfMonth < 1) dayOfMonth = 1;
        if (dayOfMonth > 28) dayOfMonth = 28;

        var nextMonth = current.AddMonths(1);
        return new DateTime(nextMonth.Year, nextMonth.Month, dayOfMonth, 0, 0, 0, DateTimeKind.Utc);
    }

    private async Task<DateTime?> ComputeNextDueDateAsync(PMSchedule schedule, DateTime afterDate)
    {
        var dates = await ComputeDueDatesAsync(schedule, afterDate.AddDays(1), afterDate.AddYears(1));
        return dates.FirstOrDefault();
    }

    private async Task<string> GenerateWorkOrderNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"WO-{today:yyyyMM}-";
        var count = await _db.MaintenanceEvents
            .CountAsync(e => e.WorkOrderNumber != null && e.WorkOrderNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D5}";
    }

    private static MaintenancePriority ConvertPriority(PMPriority priority) => priority switch
    {
        PMPriority.Low => MaintenancePriority.Low,
        PMPriority.Medium => MaintenancePriority.Medium,
        PMPriority.High => MaintenancePriority.High,
        PMPriority.Critical => MaintenancePriority.Critical,
        _ => MaintenancePriority.Medium
    };
}
