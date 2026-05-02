using System.Text.Json;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
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
/// Canonical webhook payload envelope v1
/// </summary>
public class WebhookEnvelope
{
    public string SchemaVersion { get; set; } = "1.0";
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
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
    Task EnqueueAsync(OutboxEventData eventData);
    Task EnqueueAsync(int companyId, int? siteId, string eventType, string entityType, string entityId, object payload, string? correlationId = null);
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
        var outboxEvent = new OutboxEvent
        {
            TenantId = tenantId ?? _tenantContext.TenantId,
            CompanyId = companyId,
            SiteId = siteId,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            OccurredAt = DateTime.UtcNow,
            Status = OutboxEventStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = DateTime.UtcNow,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
        };

        _db.OutboxEvents.Add(outboxEvent);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Outbox event enqueued: {EventType} for {EntityType}:{EntityId} (tenant: {TenantId})", 
            eventType, entityType, entityId, outboxEvent.TenantId);
    }
}
