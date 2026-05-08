namespace Abs.FixedAssets.Services.Webhooks.Events;

/// <summary>
/// Marker interface for every strongly-typed webhook payload.
/// Implementations are immutable records decorated with
/// <see cref="DomainEventAttribute"/> to declare their event-type string
/// and version. The runtime <see cref="DomainEventRegistry"/> maps
/// (eventType, version) → CLR type, powering documentation, validation,
/// and partner-facing schema export.
///
/// Phase 1 design: docs/design/OUTBOX_TYPED_PAYLOADS.md
/// </summary>
public interface IDomainEvent
{
    /// <summary>Stable event-type string. Matches the producer constant
    /// (e.g., "workorder.closed") and the subscription filter.</summary>
    string EventType { get; }

    /// <summary>Monotonic integer payload version. Increment on any
    /// breaking change to the schema. V1 is the legacy/initial shape.</summary>
    int Version { get; }

    /// <summary>The "subject" entity — what the event is about. The
    /// envelope's <c>entity.type</c> and <c>entity.id</c> come from these.</summary>
    string EntityType { get; }
    string EntityId { get; }
}
