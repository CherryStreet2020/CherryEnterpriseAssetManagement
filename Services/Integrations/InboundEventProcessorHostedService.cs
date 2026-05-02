// TENANT SCOPING EXCEPTION: BackgroundService runs outside HTTP request pipeline.
// No ITenantContext is available. Events carry their own tenant context via
// IntegrationEndpoint.TenantId and are processed accordingly.
using System.Text.Json;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Integrations;

public class InboundEventProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<InboundEventProcessorHostedService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);
    private static readonly int[] BackoffMinutes = { 1, 5, 15, 60, 120, 240, 480, 960 };
    private const int MaxAttempts = 8;

    public InboundEventProcessorHostedService(
        IServiceProvider services,
        ILogger<InboundEventProcessorHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inbound event processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEventsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing inbound events");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessPendingEventsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mappingService = scope.ServiceProvider.GetRequiredService<IIntegrationMappingService>();

        var pendingEvents = await db.InboundEvents
            .Include(e => e.IntegrationEndpoint)
            .Where(e => e.Status == InboundEventStatus.Pending &&
                       (e.NextAttemptAt == null || e.NextAttemptAt <= DateTime.UtcNow))
            .OrderBy(e => e.ReceivedAt)
            .Take(50)
            .ToListAsync(ct);

        if (pendingEvents.Count == 0) return;

        _logger.LogDebug("Processing {Count} pending inbound events", pendingEvents.Count);

        foreach (var evt in pendingEvents)
        {
            if (ct.IsCancellationRequested) break;
            await ProcessEventAsync(db, mappingService, evt, ct);
        }
    }

    private async Task ProcessEventAsync(AppDbContext db, IIntegrationMappingService mappingService, InboundEvent evt, CancellationToken ct)
    {
        evt.AttemptCount++;

        try
        {
            var success = await ApplyDomainCommandAsync(db, mappingService, evt, ct);

            if (success)
            {
                evt.Status = InboundEventStatus.Processed;
                evt.ProcessedAt = DateTime.UtcNow;

                if (evt.IntegrationEndpoint != null)
                    evt.IntegrationEndpoint.EventsProcessedCount++;

                db.AuditLogs.Add(new AuditLog
                {
                    EntityType = "InboundEvent",
                    EntityId = evt.Id,
                    Action = "INBOUND.PROCESSED",
                    Username = "SYSTEM",
                    Timestamp = DateTime.UtcNow,
                    AfterJson = $"{{\"eventType\":\"{evt.EventType}\",\"attempts\":{evt.AttemptCount}}}"
                });

                _logger.LogInformation("Inbound event {Id} processed successfully: {Type}", evt.Id, evt.EventType);
            }
            else
            {
                HandleFailure(db, evt, "Processing returned failure");
            }
        }
        catch (Exception ex)
        {
            HandleFailure(db, evt, ex.Message);
            _logger.LogError(ex, "Error processing inbound event {Id}", evt.Id);
        }

        await db.SaveChangesAsync(ct);
    }

    private void HandleFailure(AppDbContext db, InboundEvent evt, string error)
    {
        evt.LastError = error.Length > 1000 ? error.Substring(0, 1000) : error;

        if (evt.AttemptCount >= MaxAttempts)
        {
            evt.Status = InboundEventStatus.DeadLetter;

            if (evt.IntegrationEndpoint != null)
                evt.IntegrationEndpoint.EventsFailedCount++;

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "InboundEvent",
                EntityId = evt.Id,
                Action = "INBOUND.DEADLETTER",
                Username = "SYSTEM",
                Timestamp = DateTime.UtcNow,
                AfterJson = $"{{\"eventType\":\"{evt.EventType}\",\"attempts\":{evt.AttemptCount},\"error\":\"{error.Replace("\"", "'")}\"}}"
            });

            _logger.LogWarning("Inbound event {Id} moved to dead letter after {Attempts} attempts", evt.Id, evt.AttemptCount);
        }
        else
        {
            evt.Status = InboundEventStatus.Failed;
            var backoffIndex = Math.Min(evt.AttemptCount - 1, BackoffMinutes.Length - 1);
            evt.NextAttemptAt = DateTime.UtcNow.AddMinutes(BackoffMinutes[backoffIndex]);

            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "InboundEvent",
                EntityId = evt.Id,
                Action = "INBOUND.FAILED",
                Username = "SYSTEM",
                Timestamp = DateTime.UtcNow,
                AfterJson = $"{{\"eventType\":\"{evt.EventType}\",\"attempt\":{evt.AttemptCount},\"nextAttempt\":\"{evt.NextAttemptAt:O}\"}}"
            });
        }
    }

    private async Task<bool> ApplyDomainCommandAsync(AppDbContext db, IIntegrationMappingService mappingService, InboundEvent evt, CancellationToken ct)
    {
        switch (evt.EventType.ToLowerInvariant())
        {
            case "asset.updated":
                return await HandleAssetUpdatedAsync(db, mappingService, evt, ct);

            case "workorder.status.updated":
                return await HandleWorkOrderStatusUpdatedAsync(db, mappingService, evt, ct);

            default:
                _logger.LogWarning("Unknown inbound event type: {Type}", evt.EventType);
                return true;
        }
    }

    private async Task<bool> HandleAssetUpdatedAsync(AppDbContext db, IIntegrationMappingService mappingService, InboundEvent evt, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(evt.RawBodyJson);
            var root = doc.RootElement;

            string? externalAssetId = evt.ExternalEntityId;
            if (string.IsNullOrEmpty(externalAssetId) && root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("assetId", out var aid))
                    externalAssetId = aid.GetString();
            }

            if (string.IsNullOrEmpty(externalAssetId))
            {
                _logger.LogWarning("asset.updated: No external asset ID found in event {Id}", evt.Id);
                return false;
            }

            var internalAssetId = await mappingService.GetInternalIdAsync(
                evt.IntegrationEndpointId, IntegrationMappingType.Asset, externalAssetId);

            if (internalAssetId == null)
            {
                _logger.LogWarning("asset.updated: No mapping found for external asset {External}", externalAssetId);
                return false;
            }

            var asset = await db.Assets.FindAsync(new object[] { internalAssetId.Value }, ct);
            if (asset == null)
            {
                _logger.LogWarning("asset.updated: Asset {Id} not found", internalAssetId);
                return false;
            }

            if (root.TryGetProperty("data", out var dataElement))
            {
                if (dataElement.TryGetProperty("description", out var desc))
                    asset.Description = desc.GetString() ?? asset.Description;

                if (dataElement.TryGetProperty("serialNumber", out var serial))
                    asset.SerialNumber = serial.GetString();

                if (dataElement.TryGetProperty("notes", out var notes))
                    asset.Notes = notes.GetString();
            }

            _logger.LogInformation("asset.updated: Updated asset {Id} from external {External}", internalAssetId, externalAssetId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling asset.updated event {Id}", evt.Id);
            return false;
        }
    }

    private async Task<bool> HandleWorkOrderStatusUpdatedAsync(AppDbContext db, IIntegrationMappingService mappingService, InboundEvent evt, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(evt.RawBodyJson);
            var root = doc.RootElement;

            string? externalWorkOrderId = evt.ExternalEntityId;
            string? newStatus = null;

            if (root.TryGetProperty("data", out var data))
            {
                if (string.IsNullOrEmpty(externalWorkOrderId) && data.TryGetProperty("workOrderId", out var woid))
                    externalWorkOrderId = woid.GetString();

                if (data.TryGetProperty("status", out var status))
                    newStatus = status.GetString();
            }

            if (string.IsNullOrEmpty(externalWorkOrderId) || string.IsNullOrEmpty(newStatus))
            {
                _logger.LogWarning("workorder.status.updated: Missing workOrderId or status in event {Id}", evt.Id);
                return false;
            }

            if (!int.TryParse(externalWorkOrderId, out var workOrderId))
            {
                _logger.LogWarning("workorder.status.updated: Invalid workOrderId format in event {Id}", evt.Id);
                return false;
            }

            var workOrder = await db.MaintenanceEvents.FindAsync(new object[] { workOrderId }, ct);
            if (workOrder == null)
            {
                _logger.LogWarning("workorder.status.updated: Work order {Id} not found", workOrderId);
                return false;
            }

            if (Enum.TryParse<MaintenanceStatus>(newStatus, ignoreCase: true, out var parsedStatus))
            {
                workOrder.Status = parsedStatus;
                _logger.LogInformation("workorder.status.updated: Updated work order {Id} status to {Status}", workOrderId, parsedStatus);
                return true;
            }
            else
            {
                _logger.LogWarning("workorder.status.updated: Invalid status value '{Status}' in event {Id}", newStatus, evt.Id);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling workorder.status.updated event {Id}", evt.Id);
            return false;
        }
    }
}
