using Abs.FixedAssets.Services.RateLimiting;

namespace Abs.FixedAssets.Middleware;

// Throttles POST /Account/Login via PostgresLoginRateLimiter.
// Partition key: "login:{ip}:{username}" (username sniffed by upstream middleware).
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
