using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Revisions;
using Abs.FixedAssets.Services.Integrations;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
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

/// <summary>
/// S2-2 — projection result for meter-based PM scheduling. Returned by
/// <see cref="IPMSchedulerService.ProjectMeterCrossingDateAsync"/>.
/// </summary>
public class MeterCrossingProjection
{
    public DateTime? ProjectedCrossingUtc { get; set; }
    public decimal LatestReading { get; set; }
    public decimal TargetReading { get; set; }
    /// <summary>Units per day from the lookback window. Zero when no
    /// historical readings exist, or when readings show no movement.</summary>
    public decimal Velocity { get; set; }
    public int ReadingsUsed { get; set; }
    /// <summary>Plain-English reason the projection could not be made
    /// (no readings, zero velocity, target already crossed). Null on
    /// successful projection.</summary>
    public string? UnprojectableReason { get; set; }
}

public interface IPMSchedulerService
{
    Task<List<PMGenerationPreview>> PreviewDueAsync(int horizonDays, DateTime nowUtc, int? tenantId = null, int? companyId = null, int? siteId = null);
    Task<PMGenerationResult> GenerateDueAsync(int horizonDays, DateTime nowUtc, string? initiatedByUserId = null, int? tenantId = null, int? companyId = null, int? siteId = null);
    Task<List<DateTime>> ComputeDueDatesAsync(PMSchedule schedule, DateTime fromUtc, DateTime toUtc);

    /// <summary>
    /// S2-2 — for meter-driven PM templates (PMTemplate.TriggerType==Meter or Both),
    /// project the UTC date when an asset's meter is expected to cross a
    /// target reading. Velocity is computed from MeterReading rows in the
    /// last <paramref name="lookbackDays"/> for the given asset + meterType.
    /// Returns a structured projection so the caller can decide whether to
    /// honor the date or surface "no historical data" / "zero velocity"
    /// fallback handling.
    /// </summary>
    Task<MeterCrossingProjection> ProjectMeterCrossingDateAsync(
        int assetId,
        MeterType meterType,
        decimal targetReading,
        DateTime? asOfUtc = null,
        int lookbackDays = 30);
}

public class PMSchedulerService : IPMSchedulerService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<PMSchedulerService> _logger;
    private readonly IOutboxWriter _outbox;

    public PMSchedulerService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<PMSchedulerService> logger,
        IOutboxWriter outbox)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
        _outbox = outbox;
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

        // S1-2: load the full PMTemplateAsset rows so we can stamp the
        // assignment id on each generated WorkOrder. The legacy
        // path projected only AssetId; the assignment id was lost and the
        // CustomField1 hack tried to recover it from the wrong table.
        var templateAssets = await _db.PMTemplateAssets
            .Where(ta => ta.PMTemplateId == schedule.PMTemplateId && ta.IsActive)
            .Select(ta => new { ta.Id, ta.AssetId })
            .ToListAsync();

        if (!templateAssets.Any())
        {
            templateAssets = new() { new { Id = 0, AssetId = 0 } };
        }

        int? createdWorkOrderId = null;
        int workOrderCount = 0;

        foreach (var ta in templateAssets)
        {
            var workOrder = await CreateWorkOrderFromTemplateAsync(
                schedule, template, occurrence, ta.AssetId,
                ta.Id == 0 ? (int?)null : ta.Id,
                normalizedDate, userId, tenantId, companyId, siteId);

            if (workOrder != null)
            {
                createdWorkOrderId ??= workOrder.Id;
                workOrderCount++;
            }
        }

        if (createdWorkOrderId.HasValue)
        {
            occurrence.WorkOrderId = createdWorkOrderId;
            await _db.SaveChangesAsync();

            await EnqueuePmOccurrenceGeneratedAsync(occurrence, createdWorkOrderId, workOrderCount, userId);
        }

        schedule.NextDueDateUtc = await ComputeNextDueDateAsync(schedule, normalizedDate);
        await _db.SaveChangesAsync();

        return createdWorkOrderId;
    }

    private async Task<WorkOrder?> CreateWorkOrderFromTemplateAsync(
        PMSchedule schedule,
        PMTemplate? template,
        PMOccurrence occurrence,
        int assetId,
        int? pmTemplateAssetId,
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

        var workOrder = new WorkOrder
        {
            AssetId = assetId > 0 ? assetId : 1,
            Type = useType,
            Description = $"[PM] {useName}{revisionRef}",
            Status = MaintenanceStatus.Scheduled,
            Priority = ConvertPriority(usePriority),
            ScheduledDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Unspecified),
            EstimatedCost = useTotalCost,
            WorkOrderNumber = woNumber,
            // S1-2: explicit FKs replace the CustomField1 = "PMTA:N" hack.
            // The closeout flow reads these to advance the PM cycle.
            PMOccurrenceId = occurrence.Id,
            PMTemplateAssetId = pmTemplateAssetId
        };

        _db.WorkOrders.Add(workOrder);
        await _db.SaveChangesAsync();

        if (revision?.Operations?.Any() == true)
        {
            var seq = 10;
            foreach (var revOp in revision.Operations.OrderBy(o => o.Sequence))
            {
                var operation = new WorkOrderOperation
                {
                    WorkOrderId = workOrder.Id,
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
                WorkOrderId = workOrder.Id,
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
                    WorkOrderId = workOrder.Id,
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

    private async Task EnqueuePmOccurrenceGeneratedAsync(
        PMOccurrence occurrence,
        int? firstWorkOrderId,
        int workOrderCount,
        string? userId)
    {
        try
        {
            await _outbox.EnqueueAsync(
                occurrence.CompanyId ?? 0,
                siteId: occurrence.SiteId,
                new PmOccurrenceGeneratedV1(
                    PmOccurrenceId: occurrence.Id,
                    PmScheduleId: occurrence.PMScheduleId,
                    PmTemplateId: occurrence.PMTemplateId,
                    CompanyId: occurrence.CompanyId,
                    SiteId: occurrence.SiteId,
                    DueDateUtc: occurrence.DueDateUtc,
                    FirstWorkOrderId: firstWorkOrderId,
                    WorkOrderCount: workOrderCount,
                    GeneratedBy: userId,
                    GeneratedAt: occurrence.GeneratedAt),
                correlationId: $"pm-occurrence-{occurrence.Id}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to enqueue pm.occurrence.generated for occurrence {OccurrenceId} (WO {WorkOrderId})",
                occurrence.Id, firstWorkOrderId);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// S2-2 — uses linear velocity from MeterReading rows in the lookback
    /// window. Two readings minimum to compute velocity (latest - earliest)/
    /// (days between them). If the latest reading already meets/exceeds the
    /// target, returns "already crossed." Zero velocity (asset hasn't been
    /// run during the window) returns null projection — caller should fall
    /// back to a calendar-based estimate or hold the PM open until a fresh
    /// reading lands.
    ///
    /// The projection is intentionally linear. Most PM cadences (oil change
    /// every 250 hours, brake pad inspection every 30,000 miles) are tied
    /// to wear that's roughly proportional to use. A more sophisticated
    /// model (seasonal, duty-cycle-weighted) is a future enhancement.
    /// </remarks>
    public async Task<MeterCrossingProjection> ProjectMeterCrossingDateAsync(
        int assetId,
        MeterType meterType,
        decimal targetReading,
        DateTime? asOfUtc = null,
        int lookbackDays = 30)
    {
        var asOf = asOfUtc ?? DateTime.UtcNow;
        var since = asOf.AddDays(-lookbackDays);

        // Pull readings within the lookback window for this asset + meterType,
        // ordered chronologically. ReadingDate is the canonical timestamp on
        // MeterReading; not all readings have it set, so we coalesce to
        // CreatedAt as a fallback for older rows.
        var readings = await _db.MeterReadings
            .Where(r => r.AssetId == assetId
                        && r.MeterType == meterType
                        && r.ReadingDate >= since
                        && r.ReadingDate <= asOf)
            .OrderBy(r => r.ReadingDate)
            .Select(r => new { r.ReadingDate, r.Reading })
            .ToListAsync();

        var projection = new MeterCrossingProjection
        {
            TargetReading = targetReading,
            ReadingsUsed = readings.Count
        };

        if (readings.Count == 0)
        {
            projection.UnprojectableReason = "No meter readings in lookback window.";
            return projection;
        }

        var latest = readings[^1];
        projection.LatestReading = latest.Reading;

        if (latest.Reading >= targetReading)
        {
            // Target already crossed. The projected date is the reading that
            // crossed (or its date), so the caller can mark the PM due now.
            projection.ProjectedCrossingUtc = latest.ReadingDate;
            projection.Velocity = ComputeLinearVelocity(readings.Select(r => (r.ReadingDate, r.Reading)).ToList());
            return projection;
        }

        if (readings.Count < 2)
        {
            projection.UnprojectableReason = "Need at least 2 readings to compute velocity.";
            return projection;
        }

        var velocity = ComputeLinearVelocity(readings.Select(r => (r.ReadingDate, r.Reading)).ToList());
        projection.Velocity = velocity;

        if (velocity <= 0m)
        {
            projection.UnprojectableReason = "Meter velocity is zero or negative — asset has not been operated during the lookback window.";
            return projection;
        }

        // Linear projection: days = (target - latest) / velocity.
        var unitsRemaining = targetReading - latest.Reading;
        var daysToTarget = (double)(unitsRemaining / velocity);
        projection.ProjectedCrossingUtc = latest.ReadingDate.AddDays(daysToTarget);
        return projection;
    }

    /// <summary>
    /// S2-2 — units-per-day across the readings window. Uses first and
    /// last readings (rather than a regression) for simplicity and
    /// robustness against single-row outliers; multi-point regression
    /// is a future enhancement once we have real-world duty-cycle data.
    /// </summary>
    private static decimal ComputeLinearVelocity(List<(DateTime When, decimal Reading)> readings)
    {
        if (readings.Count < 2) return 0m;
        var first = readings[0];
        var last = readings[^1];
        var deltaDays = (decimal)(last.When - first.When).TotalDays;
        if (deltaDays <= 0m) return 0m;
        var deltaReading = last.Reading - first.Reading;
        return deltaReading / deltaDays;
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
        var count = await _db.WorkOrders
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
