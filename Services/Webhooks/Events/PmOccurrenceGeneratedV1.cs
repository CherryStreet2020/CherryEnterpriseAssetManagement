using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// PM occurrence generated payload, V1. Emitted by
/// <c>Services/Maintenance/PMSchedulerService</c> after a new
/// <c>PMOccurrence</c> + (one or more) <c>MaintenanceEvent</c>
/// work orders are committed for a PM schedule's due date. One
/// event per occurrence, regardless of how many work orders were
/// fanned out across template-assets.
///
/// The companion <c>workorder.created</c> event fires once with the
/// first WO id (today's contract); a future change may fan-out
/// per WO. Subscribers should treat this event as the canonical
/// "PM cycle ticked" signal.
/// </summary>
[DomainEvent("pm.occurrence.generated", version: 1)]
public sealed record PmOccurrenceGeneratedV1(
    int PmOccurrenceId,
    int PmScheduleId,
    int PmTemplateId,
    int? CompanyId,
    int? SiteId,
    DateTime DueDateUtc,
    int? FirstWorkOrderId,
    int WorkOrderCount,
    string? GeneratedBy,
    DateTime GeneratedAt
) : IDomainEvent
{
    public string EventType => "pm.occurrence.generated";
    public int Version => 1;
    public string EntityType => "PMOccurrence";
    public string EntityId => PmOccurrenceId.ToString();
}
