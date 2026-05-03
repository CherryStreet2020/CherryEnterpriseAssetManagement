using System.Security.Cryptography;
using System.Text;
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.RateLimiting;

// Atomic upsert against RateLimitCounters (unique on PartitionKey+WindowStartUtc).
// Budget: PermitLimit per WindowSize per partition key. Fails open on DB error
// so a Postgres outage never locks everyone out of /Account/Login.
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
            // Fail open. PartitionKey embeds the client IP and attempted
            // username, both PII. We log only the first 12 hex chars of
            // SHA-256(partitionKey) so operators can correlate repeated
            // outages on the same bucket without the raw values ever
            // hitting stdout / log shippers.
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(partitionKey));
            var keyTag = Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
            _logger.LogWarning(ex, "Distributed login rate limiter failed open (db error). keyHash={KeyHash}", keyTag);
            return true;
        }
    }
}
