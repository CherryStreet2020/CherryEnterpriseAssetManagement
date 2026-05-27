// Sprint 14.3 PR-5 (2026-05-27) — webhook event for supplier PCN delivery.
// Fires on SupplierNotificationService.SendAsync() — enables downstream
// systems (SRM/EDI/portal) to pick up and route the PCN.

namespace Abs.FixedAssets.Services.Webhooks.Events;

[DomainEvent("supplier.pcn.sent", 1)]
public record SupplierPcnSentV1(
    int PcnId,
    string PcnNumber,
    string PcnType,
    int? VendorId,
    int? ItemId,
    string DeliveryMethod
) : IDomainEvent
{
    public string EventType => "supplier.pcn.sent";
    public int Version => 1;
    public string EntityType => "SupplierProcessChangeNotification";
    public string EntityId => PcnId.ToString();
}
