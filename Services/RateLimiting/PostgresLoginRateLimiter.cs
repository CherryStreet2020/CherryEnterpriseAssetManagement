using System.Security.Cryptography;
using System.Text;
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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
    // App-level salt for the fail-open log key hash. Reading from config
    // (RateLimit:LogKeyHashSalt or env RATELIMIT_LOG_SALT) lets operators
    // pin the same salt across an Autoscale cluster so they can correlate
    // a hashed key tag across instances. If unset, we fall back to a
    // per-process random salt so the log line is *still* salted (no
    // rainbow-table reverse lookup possible) — only cluster-level
    // correlation is sacrificed.
    private readonly byte[] _logSalt;

    public PostgresLoginRateLimiter(
        IServiceScopeFactory scopeFactory,
        ILogger<PostgresLoginRateLimiter> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var configured = config["RateLimit:LogKeyHashSalt"]
            ?? Environment.GetEnvironmentVariable("RATELIMIT_LOG_SALT");
        _logSalt = !string.IsNullOrWhiteSpace(configured)
            ? Encoding.UTF8.GetBytes(configured)
            : RandomNumberGenerator.GetBytes(32);
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
            // SHA-256(salt || partitionKey) so operators can correlate
            // repeated outages on the same bucket without the raw values
            // ever hitting stdout / log shippers, and an attacker who
            // captures the log cannot reverse the hash via rainbow tables
            // because the salt is unknown to them.
            var keyTag = ComputeLogKeyTag(partitionKey);
            _logger.LogWarning(ex, "Distributed login rate limiter failed open (db error). keyHash={KeyHash}", keyTag);
            return true;
        }
    }

    // Internal so unit tests can verify the redaction is deterministic
    // for a given (salt, key) pair without scraping logs.
    internal string ComputeLogKeyTag(string partitionKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(partitionKey);
        var combined = new byte[_logSalt.Length + keyBytes.Length];
        Buffer.BlockCopy(_logSalt, 0, combined, 0, _logSalt.Length);
        Buffer.BlockCopy(keyBytes, 0, combined, _logSalt.Length, keyBytes.Length);
        var hash = SHA256.HashData(combined);
        return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
    }
}
