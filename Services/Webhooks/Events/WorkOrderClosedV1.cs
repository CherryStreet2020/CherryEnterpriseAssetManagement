using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// WorkOrder closed payload, V1. Mirrors the anonymous-object payload
/// emitted by <c>Services/Maintenance/CloseoutService.cs</c> as of
/// 2026-05-08. Field set is frozen — any breaking change must ship as
/// V2 alongside.
/// </summary>
[DomainEvent("workorder.closed", version: 1)]
public sealed record WorkOrderClosedV1(
    int WorkOrderId,
    string WorkOrderNumber,
    string Status,
    int? AssetId,
    DateTime? ClosedAt,
    string? ClosedBy
) : IDomainEvent
{
    public string EventType => "workorder.closed";
    public int Version => 1;
    public string EntityType => "WorkOrder";
    public string EntityId => WorkOrderId.ToString();
}
