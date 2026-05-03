using Abs.FixedAssets.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Abs.FixedAssets.Services.Health;

public sealed class DbHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public DbHealthCheck(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL reachable")
                : HealthCheckResult.Unhealthy("PostgreSQL CanConnectAsync returned false");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL connection failed", ex);
        }
    }
}
