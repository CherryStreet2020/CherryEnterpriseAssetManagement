using System.Diagnostics;

namespace Abs.FixedAssets.Middleware;

/// <summary>
/// Emits a W3C Server-Timing response header on every request so browsers
/// (Chrome/Firefox/Edge dev-tools Network panel) and downstream APM tools
/// can visualize per-request duration without any extra instrumentation.
/// Format: "Server-Timing: total;dur=42.318"
/// Spec: https://www.w3.org/TR/server-timing/
/// </summary>
public sealed class ServerTimingMiddleware
{
    private readonly RequestDelegate _next;

    public ServerTimingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx)
    {
        var sw = ValueStopwatch.StartNew();

        ctx.Response.OnStarting(() =>
        {
            var ms = sw.GetElapsedMs();
            // Don't overwrite if a downstream component already set their own Server-Timing entries.
            if (ctx.Response.Headers.TryGetValue("Server-Timing", out var existing) && existing.Count > 0)
            {
                ctx.Response.Headers["Server-Timing"] = $"{existing}, total;dur={ms:F3}";
            }
            else
            {
                ctx.Response.Headers["Server-Timing"] = $"total;dur={ms:F3}";
            }
            // Required for Server-Timing values to be readable from JS via the
            // PerformanceObserver / Resource Timing API across origins. Without
            // this header, browsers and many edge proxies (incl. Google Front
            // End in front of Replit Autoscale) hide or strip the timing data
            // from cross-origin consumers.
            ctx.Response.Headers["Timing-Allow-Origin"] = "*";
            return Task.CompletedTask;
        });

        await _next(ctx);
    }

    private readonly struct ValueStopwatch
    {
        private static readonly double TimestampToMs = 1000.0 / Stopwatch.Frequency;
        private readonly long _start;
        private ValueStopwatch(long start) { _start = start; }
        public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());
        public double GetElapsedMs() => (Stopwatch.GetTimestamp() - _start) * TimestampToMs;
    }
}

public static class ServerTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseServerTiming(this IApplicationBuilder app)
        => app.UseMiddleware<ServerTimingMiddleware>();
}
