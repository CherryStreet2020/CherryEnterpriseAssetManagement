using Microsoft.Extensions.Primitives;

namespace Abs.FixedAssets.Middleware;

public sealed class RequestIdMiddleware
{
    public const string HeaderName = "X-Request-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestIdMiddleware> _logger;

    public RequestIdMiddleware(RequestDelegate next, ILogger<RequestIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        StringValues incoming = ctx.Request.Headers[HeaderName];
        var requestId = incoming.Count > 0 && !string.IsNullOrWhiteSpace(incoming[0])
            ? incoming[0]!
            : ctx.TraceIdentifier;

        ctx.Response.OnStarting(() =>
        {
            if (!ctx.Response.Headers.ContainsKey(HeaderName))
                ctx.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        ctx.Items[HeaderName] = requestId;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = requestId,
            ["RequestPath"] = ctx.Request.Path.Value,
            ["RequestMethod"] = ctx.Request.Method,
        }))
        {
            await _next(ctx);
        }
    }
}

public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app)
        => app.UseMiddleware<RequestIdMiddleware>();
}
