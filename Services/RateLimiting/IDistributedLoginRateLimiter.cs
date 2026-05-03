namespace Abs.FixedAssets.Services.RateLimiting;

/// <summary>
/// Cluster-wide rate limiter for /Account/Login. Backed by a Postgres
/// counter table so the budget is enforced across every Replit Autoscale
/// instance (the in-process limiter from Phase 3 only counted within one
/// container, which an attacker could trivially bypass by fanning out).
///
/// Implementations MUST fail open on a database outage — a Postgres blip
/// must never lock everyone out of /Account/Login.
/// </summary>
public interface IDistributedLoginRateLimiter
{
    /// <summary>
    /// Atomically increments the counter for the current 1-minute window
    /// and returns true if the request is within the budget, false if it
    /// exceeds the budget and should be rejected with HTTP 429.
    /// </summary>
    Task<bool> TryAcquireAsync(string partitionKey, CancellationToken cancellationToken);
}
