using System;

namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Test ping payload, V1. Mirrors the anonymous-object payload sent by
/// the "Send test event" button in <c>Pages/Admin/Webhooks/Index.cshtml.cs</c>.
/// Used only for subscription-health verification — not a partner-facing
/// production event.
/// </summary>
[DomainEvent("test.ping", version: 1)]
public sealed record TestPingV1(
    string EntityId,
    string Message,
    DateTime Timestamp,
    int SubscriptionId,
    string? SubscriptionName
) : IDomainEvent
{
    public string EventType => "test.ping";
    public int Version => 1;
    public string EntityType => "TestEvent";
}
