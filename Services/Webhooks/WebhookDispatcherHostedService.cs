using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Webhooks;

public class WebhookDispatcherHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WebhookDispatcherHostedService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);
    private readonly HttpClient _httpClient;

    private static readonly int[] BackoffMinutes = { 1, 5, 15, 60, 120, 240, 480, 960 };
    private const int MaxAttempts = 8;

    public WebhookDispatcherHostedService(
        IServiceProvider services,
        ILogger<WebhookDispatcherHostedService> logger)
    {
        _services = services;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook dispatcher started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook events");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingEventsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingEvents = await db.OutboxEvents
            .Where(e => e.Status == OutboxEventStatus.Pending &&
                       (e.NextAttemptAt == null || e.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(e => e.OccurredAt)
            .Take(50)
            .ToListAsync(ct);

        if (pendingEvents.Count == 0) return;

        _logger.LogDebug("Processing {Count} pending outbox events", pendingEvents.Count);

        foreach (var evt in pendingEvents)
        {
            if (ct.IsCancellationRequested) break;
            await DeliverEventAsync(db, evt, ct);
        }
    }

    private async Task DeliverEventAsync(AppDbContext db, OutboxEvent evt, CancellationToken ct)
    {
        var subscriptions = await db.WebhookSubscriptions
            .Where(s => s.CompanyId == evt.CompanyId && s.IsActive)
            .ToListAsync(ct);

        var matchingSubscriptions = subscriptions
            .Where(s => s.SubscribesToEvent(evt.EventType))
            .ToList();

        if (matchingSubscriptions.Count == 0)
        {
            evt.Status = OutboxEventStatus.Sent;
            evt.SentAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            _logger.LogDebug("No subscriptions for event {EventId}, marking as sent", evt.Id);
            return;
        }

        var allSucceeded = true;

        foreach (var sub in matchingSubscriptions)
        {
            var result = await DeliverToSubscriptionAsync(db, evt, sub, ct);
            if (!result) allSucceeded = false;
        }

        if (allSucceeded)
        {
            evt.Status = OutboxEventStatus.Sent;
            evt.SentAt = DateTime.UtcNow;
        }
        else
        {
            evt.AttemptCount++;
            if (evt.AttemptCount >= MaxAttempts)
            {
                evt.Status = OutboxEventStatus.DeadLetter;
                _logger.LogWarning("Event {EventId} moved to dead letter after {Attempts} attempts", evt.Id, evt.AttemptCount);
            }
            else
            {
                var backoffIndex = Math.Min(evt.AttemptCount - 1, BackoffMinutes.Length - 1);
                evt.NextAttemptAt = DateTime.UtcNow.AddMinutes(BackoffMinutes[backoffIndex]);
                evt.Status = OutboxEventStatus.Failed;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> DeliverToSubscriptionAsync(AppDbContext db, OutboxEvent evt, WebhookSubscription sub, CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var log = new WebhookDeliveryLog
        {
            WebhookSubscriptionId = sub.Id,
            OutboxEventId = evt.Id,
            AttemptNumber = evt.AttemptCount + 1,
            CreatedAt = startTime
        };

        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = WebhookEnvelopeBuilder.BuildEnvelope(evt);
            var signature = ComputeSignature(timestamp, payload, sub.Secret);

            log.PayloadSent = payload;

            using var request = new HttpRequestMessage(HttpMethod.Post, sub.Url);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            request.Headers.Add("X-CherryAI-Event-Id", evt.Id.ToString());
            request.Headers.Add("X-CherryAI-Event-Type", evt.EventType);
            request.Headers.Add("X-CherryAI-Timestamp", timestamp.ToString());
            request.Headers.Add("X-CherryAI-Signature", $"v1={signature}");
            request.Headers.Add("X-CherryAI-Tenant-Id", evt.CompanyId.ToString());
            request.Headers.Add("X-CherryAI-Correlation-Id", evt.CorrelationId ?? "");
            request.Headers.Add("Idempotency-Key", evt.Id.ToString());

            using var response = await _httpClient.SendAsync(request, ct);
            log.ResponseStatusCode = (int)response.StatusCode;
            log.DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            if (response.IsSuccessStatusCode)
            {
                sub.LastDeliveryAt = DateTime.UtcNow;
                sub.ConsecutiveFailures = 0;
                sub.SuccessCountLifetime++;
                _logger.LogInformation("Webhook delivered: Event {EventId} to {Url} - {StatusCode}",
                    evt.Id, sub.Url, response.StatusCode);
                db.WebhookDeliveryLogs.Add(log);
                return true;
            }
            else
            {
                log.Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                evt.LastError = log.Error;
                sub.ConsecutiveFailures++;
                sub.FailureCountLifetime++;
                await CheckAutoDisableAsync(db, sub, log.Error);
                _logger.LogWarning("Webhook delivery failed: Event {EventId} to {Url} - {StatusCode}",
                    evt.Id, sub.Url, response.StatusCode);
                db.WebhookDeliveryLogs.Add(log);
                return false;
            }
        }
        catch (Exception ex)
        {
            log.DurationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            log.Error = ex.Message.Length > 1000 ? ex.Message.Substring(0, 1000) : ex.Message;
            evt.LastError = log.Error;
            sub.ConsecutiveFailures++;
            sub.FailureCountLifetime++;
            await CheckAutoDisableAsync(db, sub, log.Error);
            _logger.LogError(ex, "Webhook delivery exception: Event {EventId} to {Url}", evt.Id, sub.Url);
            db.WebhookDeliveryLogs.Add(log);
            return false;
        }
    }

    private Task CheckAutoDisableAsync(AppDbContext db, WebhookSubscription sub, string reason)
    {
        if (sub.ConsecutiveFailures >= sub.MaxConsecutiveFailures && sub.IsActive)
        {
            sub.IsActive = false;
            sub.DisabledAt = DateTime.UtcNow;
            sub.DisabledReason = $"Auto-disabled after {sub.ConsecutiveFailures} consecutive failures: {reason}";

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "WebhookSubscription",
                EntityId = sub.Id,
                Action = "WEBHOOK.DISABLED",
                Username = "SYSTEM",
                Timestamp = DateTime.UtcNow,
                AfterJson = $"{{\"subscriptionId\":{sub.Id},\"consecutiveFailures\":{sub.ConsecutiveFailures}}}"
            });

            _logger.LogWarning("Webhook subscription {SubId} auto-disabled after {Failures} consecutive failures",
                sub.Id, sub.ConsecutiveFailures);
        }
        return Task.CompletedTask;
    }

    private static string ComputeSignature(long timestamp, string payload, string secret)
    {
        var signatureBase = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureBase));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public override void Dispose()
    {
        _httpClient.Dispose();
        base.Dispose();
    }
}
