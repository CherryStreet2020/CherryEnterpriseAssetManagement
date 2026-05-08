using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Stock item received into inventory, V1. Emitted by
/// <c>Services/Receiving/ReceivingPostingService</c> after each stock
/// line's <c>ItemInventory</c> row is incremented and the
/// <c>ItemTransaction(Receipt)</c> audit row is written. One event
/// per stock receipt line.
///
/// Service / non-stock and CIP-tagged lines do NOT emit this — they
/// don't move inventory. CIP-tagged lines emit a separate
/// <c>cip.cost.added</c> (future) when the CIP cost row is written.
/// </summary>
[DomainEvent("item.received", version: 1)]
public sealed record ItemReceivedV1(
    int ItemId,
    int LocationId,
    int CompanyId,
    int GoodsReceiptId,
    int GoodsReceiptLineId,
    int? PurchaseOrderId,
    int? PurchaseOrderLineId,
    decimal Quantity,
    decimal UnitCost,
    decimal NewQuantityOnHand,
    string? LotNumber,
    string? SerialNumber,
    DateTime ReceiptDate
) : IDomainEvent
{
    public string EventType => "item.received";
    public int Version => 1;
    public string EntityType => "ItemInventory";

    // Composite identity: an ItemInventory row is keyed (Item, Location, Company).
    // Use the GR line id as the unique audit anchor instead of synthesizing
    // a composite — partners trace via GoodsReceiptLineId, not the stock row.
    public string EntityId => GoodsReceiptLineId.ToString();
}
