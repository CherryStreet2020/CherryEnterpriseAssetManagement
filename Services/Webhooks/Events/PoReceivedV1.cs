using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Goods-receipt posted against a PO, V1. Emitted by the
/// <c>/Receiving/Receive</c> page after a successful save AND the
/// downstream GR/IR posting service run. <see cref="IsFullyReceived"/>
/// distinguishes the closing receipt (PO transitions to <c>Received</c>)
/// from a partial receipt (PO stays at <c>PartiallyReceived</c>).
///
/// Per-line inventory movements emit a separate <c>item.received</c>
/// event from <see cref="Abs.FixedAssets.Services.Receiving.ReceivingPostingService"/>.
/// </summary>
[DomainEvent("po.received", version: 1)]
public sealed record PoReceivedV1(
    int PurchaseOrderId,
    string PoNumber,
    int GoodsReceiptId,
    string ReceiptNumber,
    int? CompanyId,
    int? VendorId,
    DateTime ReceiptDate,
    int LinesReceivedCount,
    bool IsFullyReceived,
    string PoStatusAfter,
    int? AccrualJournalEntryId,
    decimal AccrualTotal,
    int InventoryRowsTouched,
    int CipLinesRouted,
    string? ReceivedBy
) : IDomainEvent
{
    public string EventType => "po.received";
    public int Version => 1;
    public string EntityType => "PurchaseOrder";
    public string EntityId => PurchaseOrderId.ToString();
}
