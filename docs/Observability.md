# CherryAI EAM - Observability

**Version:** 2.0  
**Last Updated:** 2026-01-24

---

## Overview

This document describes logging, monitoring, error handling, and operational visibility for CherryAI EAM.

## Logging

### Logging Framework

Uses `Microsoft.Extensions.Logging` with structured logging:

```csharp
_logger.LogInformation("Processing asset {AssetId} for company {CompanyId}", 
    assetId, companyId);
```

### Log Levels

| Level | Usage | Example |
|-------|-------|---------|
| Trace | Detailed debugging | Query parameters |
| Debug | Development info | Cache hit/miss |
| Information | Normal operation | Request completed |
| Warning | Unexpected but handled | Retry attempted |
| Error | Failures | Exception caught |
| Critical | System failures | Database unavailable |

### Log Format

Console output in Development:
```
[10:30:00 INF] Processing asset 123 for company 1
```

Production (JSON):
```json
{
  "timestamp": "2026-01-24T10:30:00.000Z",
  "level": "Information",
  "message": "Processing asset 123 for company 1",
  "properties": {
    "AssetId": 123,
    "CompanyId": 1
  }
}
```

### Logging Best Practices

**DO:**
```csharp
// Use structured logging with named parameters
_logger.LogError(ex, "Failed to save asset {AssetId}", assetId);

// Log at appropriate levels
_logger.LogDebug("Cache miss for key {Key}", cacheKey);
```

**DON'T:**
```csharp
// Don't use string interpolation
_logger.LogError($"Failed to save asset {assetId}");  // BAD

// Don't log sensitive data
_logger.LogInformation("User password: {Password}", password);  // BAD
```

## Error Handling

### Global Exception Handler

```csharp
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        
        _logger.LogError(exception, "Unhandled exception");
        
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "An unexpected error occurred",
            requestId = context.TraceIdentifier
        });
    });
});
```

### Error Response Format

```json
{
  "error": "Asset not found",
  "code": "ASSET_NOT_FOUND",
  "requestId": "abc123"
}
```

### User-Friendly Errors

In Razor Pages:

```csharp
try
{
    await _service.ProcessAsync();
}
catch (BusinessException ex)
{
    ModelState.AddModelError("", ex.Message);
    return Page();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error in {Page}", nameof(Index));
    return RedirectToPage("/Error");
}
```

## Request Tracing

### Correlation IDs

Each request gets a unique trace identifier:

```csharp
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    
    using (_logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId
    }))
    {
        await next();
    }
});
```

### Request Logging

```
[10:30:00 INF] HTTP GET /Assets started [CorrelationId: abc123]
[10:30:00 INF] Querying assets for company 1 [CorrelationId: abc123]
[10:30:01 INF] HTTP GET /Assets completed in 150ms [CorrelationId: abc123]
```

## Background Jobs

### Hosted Services

| Service | Purpose | Interval |
|---------|---------|----------|
| PMExecutionHostedService | Generate PM work orders | 15 min |
| WebhookDispatcherHostedService | Deliver webhooks | 30 sec |
| InboundEventProcessor | Process inbound events | 1 min |

### Job Logging

```csharp
public class PMExecutionHostedService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("PM execution loop started");
            
            try
            {
                var count = await ExecutePMSchedulesAsync();
                _logger.LogInformation("Generated {Count} PM work orders", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PM execution loop failed");
            }
            
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
```

## Webhook Monitoring

### Delivery Metrics

Track webhook delivery success:

| Metric | Description |
|--------|-------------|
| Pending | Awaiting delivery |
| Delivered | Successfully sent |
| Failed | Permanently failed |
| DLQ | In dead-letter queue |

### Failure Logging

```csharp
_logger.LogWarning(
    "Webhook delivery failed: {EndpointUrl}, attempt {Attempt}, status {Status}",
    delivery.EndpointUrl, delivery.RetryCount, response.StatusCode);
```

## Health Monitoring

### Health Check Endpoint

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => e.Value.Status.ToString()
            )
        });
    }
});
```

### Health Checks

| Check | What It Validates |
|-------|-------------------|
| Database | Can connect to PostgreSQL |
| Memory | Memory usage under threshold |

## Audit Trail

### Audited Operations

| Entity | Operations |
|--------|------------|
| Asset | Create, Update, Delete, Transfer |
| WorkOrder | Create, Update, Status Change |
| User | Login, Logout, Role Change |
| Settings | Any configuration change |

### Audit Log Entry

```csharp
public class AuditEntry
{
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; }
    public string Action { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public JsonDocument Changes { get; set; }
    public string IpAddress { get; set; }
}
```

## Performance Monitoring

### Slow Query Detection

```csharp
optionsBuilder.LogTo(
    message => _logger.LogWarning("Slow query: {Query}", message),
    new[] { DbLoggerCategory.Database.Command.Name },
    LogLevel.Warning,
    DbContextLoggerOptions.DefaultWithLocalTime);
```

### Request Duration

```csharp
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    sw.Stop();
    
    if (sw.ElapsedMilliseconds > 1000)
    {
        _logger.LogWarning(
            "Slow request: {Method} {Path} took {Duration}ms",
            context.Request.Method,
            context.Request.Path,
            sw.ElapsedMilliseconds);
    }
});
```

## Alerting Triggers

### Recommended Alerts

| Condition | Severity | Action |
|-----------|----------|--------|
| Error rate > 1% | High | Investigate immediately |
| Response time > 5s | Medium | Check performance |
| DLQ size > 10 | Medium | Review failed webhooks |
| Disk space < 10% | High | Expand storage |

## Related Documents

- [Deployment.md](Deployment.md) - Deployment configuration
- [DeveloperGettingStarted.md](DeveloperGettingStarted.md) - Development setup
