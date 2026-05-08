using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// WorkOrder created payload, V1. Mirrors the anonymous-object payload
/// emitted by <c>Services/Maintenance/WorkRequestConversionService.cs</c>
/// when a work request is converted into a work order.
/// </summary>
[DomainEvent("workorder.created", version: 1)]
public sealed record WorkOrderCreatedV1(
    int WorkOrderId,
    string WorkOrderNumber,
    string Status,
    string Priority,
    int? AssetId,
    int SourceWorkRequestId,
    int OperationCount,
    DateTime CreatedAt
) : IDomainEvent
{
    public string EventType => "workorder.created";
    public int Version => 1;
    public string EntityType => "MaintenanceEvent";
    public string EntityId => WorkOrderId.ToString();
}
