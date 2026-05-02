using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Maintenance;

public enum WorkOrderOrigin
{
    Manual = 0,
    SmartAssist = 1,
    PMSchedule = 2
}

public class WorkOrderOriginInfo
{
    public WorkOrderOrigin Origin { get; set; } = WorkOrderOrigin.Manual;
    public string Label { get; set; } = "Manual";
    public string BadgeClass { get; set; } = "badge-neutral";
    public string? TemplateReference { get; set; }
    public string? ScheduleReference { get; set; }
    public double? AIConfidence { get; set; }
}

public interface IWorkOrderOriginService
{
    Task<WorkOrderOriginInfo> GetOriginAsync(int maintenanceEventId);
    Task<WorkOrderOriginInfo> GetOriginAsync(MaintenanceEvent evt);
    Task<Dictionary<int, WorkOrderOriginInfo>> GetOriginsForEventsAsync(IEnumerable<int> eventIds);
}

public class WorkOrderOriginService : IWorkOrderOriginService
{
    private readonly AppDbContext _context;

    public WorkOrderOriginService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WorkOrderOriginInfo> GetOriginAsync(int maintenanceEventId)
    {
        var evt = await _context.MaintenanceEvents.Where(e => e.Id == maintenanceEventId).FirstOrDefaultAsync();
        if (evt == null)
            return new WorkOrderOriginInfo();

        return await GetOriginAsync(evt);
    }

    public async Task<WorkOrderOriginInfo> GetOriginAsync(MaintenanceEvent evt)
    {
        var workRequest = await _context.WorkRequests
            .FirstOrDefaultAsync(wr => wr.GeneratedWorkOrderId == evt.Id);

        if (workRequest != null && workRequest.IsAIAssisted)
        {
            double? confidence = null;
            if (!string.IsNullOrEmpty(workRequest.AIConfidence) && 
                double.TryParse(workRequest.AIConfidence.Replace("%", ""), out var parsed))
            {
                confidence = parsed;
            }

            return new WorkOrderOriginInfo
            {
                Origin = WorkOrderOrigin.SmartAssist,
                Label = "Smart Assist",
                BadgeClass = "badge-info",
                AIConfidence = confidence
            };
        }

        if (!string.IsNullOrEmpty(evt.CustomField1) && evt.CustomField1.StartsWith("PMTA:", StringComparison.OrdinalIgnoreCase))
        {
            var pmtaIdStr = evt.CustomField1.Substring(5);
            string? templateRef = null;
            if (int.TryParse(pmtaIdStr, out var pmtaId))
            {
                var pmta = await _context.Set<PMTemplateAsset>()
                    .Include(p => p.PMTemplate)
                    .FirstOrDefaultAsync(p => p.Id == pmtaId);
                if (pmta?.PMTemplate != null)
                {
                    templateRef = pmta.PMTemplate.Name;
                }
            }

            return new WorkOrderOriginInfo
            {
                Origin = WorkOrderOrigin.PMSchedule,
                Label = "PM Schedule",
                BadgeClass = "badge-primary",
                TemplateReference = templateRef
            };
        }

        if (evt.Type == MaintenanceType.Preventative && evt.RecurrenceIntervalDays.HasValue && evt.RecurrenceIntervalDays > 0)
        {
            return new WorkOrderOriginInfo
            {
                Origin = WorkOrderOrigin.PMSchedule,
                Label = "PM Schedule",
                BadgeClass = "badge-primary",
                ScheduleReference = $"Every {evt.RecurrenceIntervalDays} days"
            };
        }

        return new WorkOrderOriginInfo
        {
            Origin = WorkOrderOrigin.Manual,
            Label = "Manual",
            BadgeClass = "badge-neutral"
        };
    }

    public async Task<Dictionary<int, WorkOrderOriginInfo>> GetOriginsForEventsAsync(IEnumerable<int> eventIds)
    {
        var eventIdList = eventIds.ToList();
        if (!eventIdList.Any())
            return new Dictionary<int, WorkOrderOriginInfo>();

        var events = await _context.MaintenanceEvents
            .Where(e => eventIdList.Contains(e.Id))
            .ToListAsync();

        var workRequests = await _context.WorkRequests
            .Where(wr => wr.GeneratedWorkOrderId.HasValue && eventIdList.Contains(wr.GeneratedWorkOrderId.Value))
            .ToListAsync();

        var pmtaIds = events
            .Where(e => !string.IsNullOrEmpty(e.CustomField1) && e.CustomField1.StartsWith("PMTA:", StringComparison.OrdinalIgnoreCase))
            .Select(e => int.TryParse(e.CustomField1!.Substring(5), out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var pmtas = pmtaIds.Any()
            ? await _context.Set<PMTemplateAsset>()
                .Include(p => p.PMTemplate)
                .Where(p => pmtaIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id)
            : new Dictionary<int, PMTemplateAsset>();

        var result = new Dictionary<int, WorkOrderOriginInfo>();

        foreach (var evt in events)
        {
            var workRequest = workRequests.FirstOrDefault(wr => wr.GeneratedWorkOrderId == evt.Id);

            if (workRequest != null && workRequest.IsAIAssisted)
            {
                double? confidence = null;
                if (!string.IsNullOrEmpty(workRequest.AIConfidence) && 
                    double.TryParse(workRequest.AIConfidence.Replace("%", ""), out var parsed))
                {
                    confidence = parsed;
                }

                result[evt.Id] = new WorkOrderOriginInfo
                {
                    Origin = WorkOrderOrigin.SmartAssist,
                    Label = "Smart Assist",
                    BadgeClass = "badge-info",
                    AIConfidence = confidence
                };
                continue;
            }

            if (!string.IsNullOrEmpty(evt.CustomField1) && evt.CustomField1.StartsWith("PMTA:", StringComparison.OrdinalIgnoreCase))
            {
                var pmtaIdStr = evt.CustomField1.Substring(5);
                string? templateRef = null;
                if (int.TryParse(pmtaIdStr, out var pmtaId) && pmtas.TryGetValue(pmtaId, out var pmta))
                {
                    templateRef = pmta.PMTemplate?.Name;
                }

                result[evt.Id] = new WorkOrderOriginInfo
                {
                    Origin = WorkOrderOrigin.PMSchedule,
                    Label = "PM Schedule",
                    BadgeClass = "badge-primary",
                    TemplateReference = templateRef
                };
                continue;
            }

            if (evt.Type == MaintenanceType.Preventative && evt.RecurrenceIntervalDays.HasValue && evt.RecurrenceIntervalDays > 0)
            {
                result[evt.Id] = new WorkOrderOriginInfo
                {
                    Origin = WorkOrderOrigin.PMSchedule,
                    Label = "PM Schedule",
                    BadgeClass = "badge-primary",
                    ScheduleReference = $"Every {evt.RecurrenceIntervalDays} days"
                };
                continue;
            }

            result[evt.Id] = new WorkOrderOriginInfo
            {
                Origin = WorkOrderOrigin.Manual,
                Label = "Manual",
                BadgeClass = "badge-neutral"
            };
        }

        return result;
    }
}
