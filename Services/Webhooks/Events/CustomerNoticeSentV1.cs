// Sprint 14.3 PR-5 (2026-05-27) — webhook event for customer notice delivery.
// Fires on CustomerNotificationService.SendAsync() — enables downstream
// systems (EDI/CRM/portal) to pick up and route the notification.

namespace Abs.FixedAssets.Services.Webhooks.Events;

[DomainEvent("customer.notice.sent", 1)]
public record CustomerNoticeSentV1(
    int NoticeId,
    string NoticeNumber,
    string NoticeType,
    int? CustomerId,
    int? ItemId,
    string DeliveryMethod
) : IDomainEvent
{
    public string EventType => "customer.notice.sent";
    public int Version => 1;
    public string EntityType => "CustomerNotice";
    public string EntityId => NoticeId.ToString();
}
