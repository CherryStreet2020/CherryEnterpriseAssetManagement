using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.RateLimiting;

// Periodically deletes rate-limit rows whose window is older than RetentionWindow.
// Idempotent and safe to run from every Replit Autoscale instance simultaneously.
public sealed class RateLimitCounterCleanupService : BackgroundService
{
    public static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RetentionWindow = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RateLimitCounterCleanupService> _logger;

    public RateLimitCounterCleanupService(IServiceScopeFactory scopeFactory, ILogger<RateLimitCounterCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Stagger first run by ~30s so a fleet restart doesn't all hit the
        // table at once.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var conn = db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync(stoppingToken);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"DELETE FROM ""RateLimitCounters"" WHERE ""WindowStartUtc"" < @cutoff;";
                var p = cmd.CreateParameter();
                p.ParameterName = "@cutoff";
                p.Value = DateTime.UtcNow - RetentionWindow;
                cmd.Parameters.Add(p);

                var rows = await cmd.ExecuteNonQueryAsync(stoppingToken);
                if (rows > 0)
                    _logger.LogInformation("RateLimitCounter cleanup removed {Rows} stale rows", rows);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RateLimitCounter cleanup pass failed");
            }

            try { await Task.Delay(RunInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
