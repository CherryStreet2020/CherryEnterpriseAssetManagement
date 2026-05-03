using Abs.FixedAssets.Services.RateLimiting;

namespace Abs.FixedAssets.Middleware;

/// <summary>
/// Replaces the in-process <c>System.Threading.RateLimiting</c> limiter
/// from Phase 3 with a Postgres-backed counter (<see cref="PostgresLoginRateLimiter"/>)
/// so the budget is enforced across all Replit Autoscale instances —
/// the prior implementation only counted within one container, which an
/// attacker could trivially bypass by spreading requests across replicas.
///
/// Scope: only POST /Account/Login. All other paths fall through with
/// zero work.
///
/// Partition key: <c>"login:{ip}:{username}"</c>. The username is read
/// from <c>HttpContext.Items["LoginUsername"]</c>, populated by the
/// upstream username-snoop middleware in Program.cs.
///
/// Fail-open behavior is delegated to <see cref="PostgresLoginRateLimiter"/>;
/// this middleware never converts a DB outage into a 429.
/// </summary>
public sealed class DistributedLoginRateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public DistributedLoginRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx, IDistributedLoginRateLimiter limiter)
    {
        if (HttpMethods.IsPost(ctx.Request.Method)
            && ctx.Request.Path.StartsWithSegments("/Account/Login"))
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var username = (ctx.Items["LoginUsername"] as string ?? "").ToLowerInvariant();
            var partitionKey = $"login:{ip}:{username}";

            var allowed = await limiter.TryAcquireAsync(partitionKey, ctx.RequestAborted);
            if (!allowed)
            {
                ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ctx.Response.Headers["Retry-After"] = "60";
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.WriteAsync("Too many login attempts. Please wait a minute and try again.");
                return;
            }
        }

        await _next(ctx);
    }
}

public static class DistributedLoginRateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseDistributedLoginRateLimit(this IApplicationBuilder app)
        => app.UseMiddleware<DistributedLoginRateLimitMiddleware>();
}
