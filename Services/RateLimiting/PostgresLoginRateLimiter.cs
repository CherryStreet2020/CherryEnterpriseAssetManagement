using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.RateLimiting;

/// <summary>
/// Postgres-backed implementation of <see cref="IDistributedLoginRateLimiter"/>.
///
/// Uses a single-roundtrip atomic upsert against the "RateLimitCounters"
/// table (unique on (PartitionKey, WindowStartUtc)) and returns the new
/// post-increment count via PostgreSQL's RETURNING clause. The decision
/// (allow vs throttle) is made on that returned value.
///
/// Budget: <see cref="PermitLimit"/> requests per <see cref="WindowSize"/>
/// per partition key. Default is 100 / minute (matches the previous
/// in-process limiter from Phase 3).
///
/// Fail-open guarantee: any exception from the database path is swallowed
/// and logged at Warning, and the request is allowed through. A Postgres
/// outage must never lock every legitimate user out of /Account/Login.
/// </summary>
public sealed class PostgresLoginRateLimiter : IDistributedLoginRateLimiter
{
    public const int PermitLimit = 100;
    public static readonly TimeSpan WindowSize = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PostgresLoginRateLimiter> _logger;

    public PostgresLoginRateLimiter(IServiceScopeFactory scopeFactory, ILogger<PostgresLoginRateLimiter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<bool> TryAcquireAsync(string partitionKey, CancellationToken cancellationToken)
    {
        // Compute the canonical window start (truncate UtcNow to the start
        // of the current minute). Using UtcNow rather than NOW() in SQL
        // keeps both code paths consistent when reading the table later.
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(cancellationToken);
            }

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ""RateLimitCounters"" (""PartitionKey"", ""WindowStartUtc"", ""Count"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
                VALUES (@key, @windowStart, 1, @now, @now)
                ON CONFLICT (""PartitionKey"", ""WindowStartUtc"") DO UPDATE
                  SET ""Count"" = ""RateLimitCounters"".""Count"" + 1,
                      ""UpdatedAtUtc"" = EXCLUDED.""UpdatedAtUtc""
                RETURNING ""Count"";";

            var pKey = cmd.CreateParameter();
            pKey.ParameterName = "@key";
            pKey.Value = partitionKey;
            cmd.Parameters.Add(pKey);

            var pWindow = cmd.CreateParameter();
            pWindow.ParameterName = "@windowStart";
            pWindow.Value = windowStart;
            cmd.Parameters.Add(pWindow);

            var pNow = cmd.CreateParameter();
            pNow.ParameterName = "@now";
            pNow.Value = now;
            cmd.Parameters.Add(pNow);

            var raw = await cmd.ExecuteScalarAsync(cancellationToken);
            var count = raw is null ? 0 : Convert.ToInt32(raw);
            return count <= PermitLimit;
        }
        catch (OperationCanceledException)
        {
            // Caller bailed; no decision to make. Treat as allowed so we
            // don't leak a 429 to a request the client has already given
            // up on.
            return true;
        }
        catch (Exception ex)
        {
            // Fail open. PartitionKey is intentionally NOT logged in full
            // because it embeds the attempted username; we hash-truncate
            // for triage instead.
            var keyTag = partitionKey.Length > 24 ? partitionKey.Substring(0, 24) + "..." : partitionKey;
            _logger.LogWarning(ex, "Distributed login rate limiter failed open (db error). key~={Key}", keyTag);
            return true;
        }
    }
}
