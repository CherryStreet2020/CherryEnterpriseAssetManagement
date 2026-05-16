using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Integrations;

public interface IInboundWebhookService
{
    Task<(bool Success, string Message, int? EventId)> ReceiveWebhookAsync(
        string integrationKey,
        string rawBody,
        string? timestamp,
        string? signature,
        string? idempotencyKey,
        Dictionary<string, string> headers);

    bool VerifySignature(string secret, string timestamp, string rawBody, string signature);
}

public class InboundWebhookService : IInboundWebhookService
{
    private readonly AppDbContext _db;
    private readonly ILogger<InboundWebhookService> _logger;
    private const int TimestampToleranceSeconds = 300;

    public InboundWebhookService(AppDbContext db, ILogger<InboundWebhookService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool Success, string Message, int? EventId)> ReceiveWebhookAsync(
        string integrationKey,
        string rawBody,
        string? timestamp,
        string? signature,
        string? idempotencyKey,
        Dictionary<string, string> headers)
    {
        var endpoint = await _db.IntegrationEndpoints
            .FirstOrDefaultAsync(e => e.IntegrationKey == integrationKey && e.IsActive);

        if (endpoint == null)
        {
            _logger.LogWarning("Inbound webhook rejected: unknown integration key {Key}", integrationKey);
            return (false, "Unknown or inactive integration endpoint", null);
        }

        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Inbound webhook rejected: missing timestamp or signature for {Key}", integrationKey);
            return (false, "Missing required headers: X-CherryAI-Timestamp, X-CherryAI-Signature", null);
        }

        if (!long.TryParse(timestamp, out var ts))
        {
            _logger.LogWarning("Inbound webhook rejected: invalid timestamp format for {Key}", integrationKey);
            return (false, "Invalid timestamp format", null);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - ts) > TimestampToleranceSeconds)
        {
            _logger.LogWarning("Inbound webhook rejected: timestamp outside tolerance for {Key}", integrationKey);
            return (false, "Timestamp outside acceptable tolerance (+/- 5 minutes)", null);
        }

        if (!VerifySignature(endpoint.Secret, timestamp, rawBody, signature))
        {
            _logger.LogWarning("Inbound webhook rejected: invalid signature for {Key}", integrationKey);
            return (false, "Invalid signature", null);
        }

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existingEvent = await _db.InboundEvents
                .FirstOrDefaultAsync(e => e.IntegrationEndpointId == endpoint.Id && e.IdempotencyKey == idempotencyKey);
            if (existingEvent != null)
            {
                _logger.LogInformation("Inbound webhook deduplicated: idempotency key {Key} already exists", idempotencyKey);
                return (true, "Event already processed (idempotent)", existingEvent.Id);
            }
        }

        string eventType = "unknown";
        string? externalEntityId = null;
        string? correlationId = null;

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.TryGetProperty("eventType", out var et))
                eventType = et.GetString() ?? "unknown";
            else if (doc.RootElement.TryGetProperty("event_type", out var et2))
                eventType = et2.GetString() ?? "unknown";

            if (doc.RootElement.TryGetProperty("entity", out var entity))
            {
                if (entity.TryGetProperty("id", out var id))
                    externalEntityId = id.GetString();
            }
            else if (doc.RootElement.TryGetProperty("entityId", out var eid))
                externalEntityId = eid.GetString();

            if (doc.RootElement.TryGetProperty("correlationId", out var cid))
                correlationId = cid.GetString();
        }
        catch
        {
        }

        if (!endpoint.AllowsEventType(eventType))
        {
            _logger.LogWarning("Inbound webhook rejected: event type {Type} not allowed for {Key}", eventType, integrationKey);
            return (false, $"Event type '{eventType}' not allowed for this integration", null);
        }

        var inboundEvent = new InboundEvent
        {
            TenantId = endpoint.TenantId,
            IntegrationEndpointId = endpoint.Id,
            ReceivedAt = DateTime.UtcNow,
            EventType = eventType,
            ExternalEntityId = externalEntityId,
            CorrelationId = correlationId,
            IdempotencyKey = idempotencyKey,
            RawBodyJson = rawBody,
            HeadersJson = JsonSerializer.Serialize(headers),
            Status = InboundEventStatus.Pending,
            AttemptCount = 0
        };

        _db.InboundEvents.Add(inboundEvent);

        endpoint.LastEventAt = DateTime.UtcNow;
        endpoint.EventsReceivedCount++;

        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            EntityType = "InboundEvent",
            EntityId = inboundEvent.Id,
            Action = "INBOUND.RECEIVED",
            Username = "SYSTEM",
            Timestamp = DateTime.UtcNow,
            AfterJson = $"{{\"integrationKey\":\"{integrationKey}\",\"eventType\":\"{eventType}\",\"eventId\":{inboundEvent.Id}}}"
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("Inbound webhook received: {Type} for integration {Key}, event ID {Id}",
            eventType, integrationKey, inboundEvent.Id);

        return (true, "Event received and queued for processing", inboundEvent.Id);
    }

    public bool VerifySignature(string secret, string timestamp, string rawBody, string signature)
    {
        var sig = signature;
        if (sig.StartsWith("v1=", StringComparison.OrdinalIgnoreCase))
            sig = sig.Substring(3);

        var signatureBase = $"{timestamp}.{rawBody}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureBase));

        // PR #100 (B-01): constant-time compare on the raw HMAC bytes. The
        // previous string.Equals on hex strings short-circuited on the first
        // mismatched character, leaking timing information that lets a
        // remote attacker brute-force the signature one byte at a time. We
        // parse the incoming hex into bytes (rejecting bad hex with false)
        // and use CryptographicOperations.FixedTimeEquals — the canonical
        // .NET API for HMAC comparison, used by ASP.NET's own anti-forgery
        // and data-protection stacks.
        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(sig);
        }
        catch (FormatException)
        {
            return false; // bad hex == bad signature
        }

        if (providedBytes.Length != hash.Length)
            return false; // length mismatch == bad signature (still constant-time within each comparison)

        return CryptographicOperations.FixedTimeEquals(hash, providedBytes);
    }
}
