using System.Text.Json;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Webhooks;

public static class WebhookEventTypes
{
    public const string WorkRequestCreated = "workrequest.created";
    public const string WorkOrderCreated = "workorder.created";
    public const string WorkOrderClosed = "workorder.closed";
    public const string CloseoutSummaryGenerated = "closeout.summary.generated";
    public const string LessonSaved = "lesson.saved";

    public static readonly string[] AllEventTypes = new[]
    {
        WorkRequestCreated,
        WorkOrderCreated,
        WorkOrderClosed,
        CloseoutSummaryGenerated,
        LessonSaved
    };
}

/// <summary>
/// Canonical webhook payload envelope v1. <see cref="SchemaVersion"/> is
/// the WIRE-format version (the envelope shape itself); independent of
/// <see cref="PayloadVersion"/>, which versions the contents of
/// <see cref="Data"/>. Subscribers MUST tolerate unknown envelope
/// fields per the schemaVersion-1.0 contract.
/// </summary>
public class WebhookEnvelope
{
    public string SchemaVersion { get; set; } = "1.0";
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    /// <summary>Strongly-typed payload version (the IDomainEvent.Version
    /// of the producing record). Defaults to 1 for legacy untyped events
    /// and for events enqueued before the typed-payloads migration.</summary>
    public int PayloadVersion { get; set; } = 1;

    public string OccurredAt { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string? SiteId { get; set; }
    public WebhookEntity Entity { get; set; } = new();
    public string? CorrelationId { get; set; }
    public object Data { get; set; } = new { };
}

public class WebhookEntity
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public static class WebhookEnvelopeBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string BuildEnvelope(OutboxEvent evt)
    {
        var envelope = new WebhookEnvelope
        {
            SchemaVersion = "1.0",
            EventId = evt.Id.ToString(),
            EventType = evt.EventType,
            PayloadVersion = evt.PayloadVersion ?? 1,
            OccurredAt = evt.OccurredAt.ToString("O"),
            TenantId = evt.TenantId?.ToString() ?? evt.CompanyId.ToString(),
            CompanyId = evt.CompanyId.ToString(),
            SiteId = evt.SiteId?.ToString(),
            Entity = new WebhookEntity
            {
                Type = evt.EntityType,
                Id = evt.EntityId
            },
            CorrelationId = evt.CorrelationId,
            Data = JsonSerializer.Deserialize<object>(evt.PayloadJson, JsonOptions) ?? new { }
        };

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    public static string BuildEnvelopeWithTimestamp(OutboxEvent evt, out long timestamp)
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return BuildEnvelope(evt);
    }
}

public interface IOutboxWriter
{
    /// <summary>Strongly-typed enqueue. The recommended path for all
    /// new producer call sites. The event's <see cref="IDomainEvent.EventType"/>,
    /// <see cref="IDomainEvent.Version"/>, <see cref="IDomainEvent.EntityType"/>,
    /// and <see cref="IDomainEvent.EntityId"/> all come from the event
    /// record itself — no string drift between producer and registry.</summary>
    Task EnqueueAsync<T>(int companyId, int? siteId, T evt, string? correlationId = null) where T : IDomainEvent;

    [System.Obsolete("Use EnqueueAsync<T>(int, int?, IDomainEvent, string?) instead. " +
                     "This overload is preserved for migration only — see " +
                     "docs/design/OUTBOX_TYPED_PAYLOADS.md Phase 2.")]
    Task EnqueueAsync(OutboxEventData eventData);

    [System.Obsolete("Use EnqueueAsync<T>(int, int?, IDomainEvent, string?) instead.")]
    Task EnqueueAsync(int companyId, int? siteId, string eventType, string entityType, string entityId, object payload, string? correlationId = null);

    [System.Obsolete("Use EnqueueAsync<T>(int, int?, IDomainEvent, string?) instead.")]
    Task EnqueueAsync(int? tenantId, int companyId, int? siteId, string eventType, string entityType, string entityId, object payload, string? correlationId = null);
}

public class OutboxEventData
{
    public int? TenantId { get; set; }
    public int CompanyId { get; set; }
    public int? SiteId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public object Payload { get; set; } = new { };
    public string? CorrelationId { get; set; }
}

public class OutboxWriter : IOutboxWriter
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<OutboxWriter> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public OutboxWriter(AppDbContext db, ITenantContext tenantContext, ILogger<OutboxWriter> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task EnqueueAsync<T>(int companyId, int? siteId, T evt, string? correlationId = null)
        where T : IDomainEvent
    {
        if (evt is null) throw new ArgumentNullException(nameof(evt));

        // Serialize against the runtime type, not the static T. T may be
        // bound to IDomainEvent at the call site, in which case static-T
        // serialization would emit only interface members. Concrete-type
        // serialization gives us the full record shape.
        var payloadJson = JsonSerializer.Serialize(evt, evt.GetType(), JsonOptions);

        var outboxEvent = new OutboxEvent
        {
            TenantId = _tenantContext.TenantId,
            CompanyId = companyId,
            SiteId = siteId,
            EventType = evt.EventType,
            EntityType = evt.EntityType,
            EntityId = evt.EntityId,
            PayloadJson = payloadJson,
            PayloadVersion = evt.Version,
            OccurredAt = DateTime.UtcNow,
            Status = OutboxEventStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = DateTime.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
        };

        _db.OutboxEvents.Add(outboxEvent);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Outbox event enqueued (typed): {EventType} v{Version} for {EntityType}:{EntityId} (tenant: {TenantId})",
            evt.EventType, evt.Version, evt.EntityType, evt.EntityId, outboxEvent.TenantId);
    }

#pragma warning disable CS0618 // we are the legacy implementations
    public async Task EnqueueAsync(OutboxEventData eventData)
    {
        await EnqueueAsync(
            eventData.TenantId ?? _tenantContext.TenantId,
            eventData.CompanyId,
            eventData.SiteId,
            eventData.EventType,
            eventData.EntityType,
            eventData.EntityId,
            eventData.Payload,
            eventData.CorrelationId
        );
    }

    public async Task EnqueueAsync(int companyId, int? siteId, string eventType, string entityType, string entityId, object payload, string? correlationId = null)
    {
        await EnqueueAsync(_tenantContext.TenantId, companyId, siteId, eventType, entityType, entityId, payload, correlationId);
    }

    public async Task EnqueueAsync(int? tenantId, int companyId, int? siteId, string eventType, string entityType, string entityId, object payload, string? correlationId = null)
    {
        // Legacy untyped path. Wraps the call in an UntypedLegacyEvent so it
        // flows through the same persistence shape — the only externally
        // observable difference is PayloadVersion=1 stamped explicitly,
        // matching what NULL-rows already get interpreted as at dispatch.
        var outboxEvent = new OutboxEvent
        {
            TenantId = tenantId ?? _tenantContext.TenantId,
            CompanyId = companyId,
            SiteId = siteId,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            PayloadVersion = 1,
            OccurredAt = DateTime.UtcNow,
            Status = OutboxEventStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = DateTime.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
        };

        _db.OutboxEvents.Add(outboxEvent);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Outbox event enqueued (legacy): {EventType} for {EntityType}:{EntityId} (tenant: {TenantId})",
            eventType, entityType, entityId, outboxEvent.TenantId);
    }
#pragma warning restore CS0618
}
