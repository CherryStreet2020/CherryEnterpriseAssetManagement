using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Purchase order approved payload, V1. Emitted by the
/// <c>/Purchasing/Details</c> page when a PO transitions
/// PendingApproval → Approved. Fires once per state transition;
/// the idempotent guard at the page handler prevents re-emission
/// on duplicate clicks.
/// </summary>
[DomainEvent("po.approved", version: 1)]
public sealed record PoApprovedV1(
    int PurchaseOrderId,
    string PoNumber,
    int VendorId,
    int? CompanyId,
    int? CipProjectId,
    decimal Total,
    DateTime OrderDate,
    DateTime? RequiredDate,
    DateTime ApprovedAt,
    string? ApproverUsername
) : IDomainEvent
{
    public string EventType => "po.approved";
    public int Version => 1;
    public string EntityType => "PurchaseOrder";
    public string EntityId => PurchaseOrderId.ToString();
}
