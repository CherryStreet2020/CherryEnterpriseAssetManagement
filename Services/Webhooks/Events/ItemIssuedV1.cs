using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Stock item issued to a work order, V1. Emitted by the
/// <c>/WorkOrders/Details::OnPostIssueMaterialAsync</c> handler after
/// the WorkOrderPart counters are updated, ItemInventory is decremented,
/// and the ItemTransaction(Issue) audit row is written. One event per
/// issuance action (NOT per cumulative usage).
///
/// Returns are the symmetric reverse and do not emit this event;
/// future <c>item.returned</c> can be added if a partner asks. The
/// <see cref="LocationId"/> is null when the WorkOrderPart had no
/// IssuedFromLocationId — issuance still records the transaction
/// for audit, but there's no specific stock row to point at.
/// </summary>
[DomainEvent("item.issued", version: 1)]
public sealed record ItemIssuedV1(
    int ItemId,
    int? LocationId,
    int? CompanyId,
    int WorkOrderId,
    int WorkOrderPartId,
    string WorkOrderNumber,
    int? AssetId,
    decimal Quantity,
    decimal UnitCost,
    decimal? NewQuantityOnHand,
    string? LotNumber,
    string? SerialNumber,
    string? IssuedBy,
    DateTime IssuedAt
) : IDomainEvent
{
    public string EventType => "item.issued";
    public int Version => 1;
    public string EntityType => "ItemTransaction";

    // The WorkOrderPart id is the unique audit anchor for this issuance.
    public string EntityId => WorkOrderPartId.ToString();
}
