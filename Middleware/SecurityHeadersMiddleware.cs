namespace Abs.FixedAssets.Middleware;

// Sets CSP, X-Content-Type-Options, Referrer-Policy, Permissions-Policy.
// CSP frame-ancestors allow-lists Replit edge so the preview iframe still
// works (X-Frame-Options is removed elsewhere in Program.cs).
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string CspValue = string.Join("; ", new[]
    {
        "default-src 'self'",
        "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net",
        "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net",
        "img-src 'self' data: blob: https:",
        "font-src 'self' data: https://cdnjs.cloudflare.com https://cdn.jsdelivr.net",
        "connect-src 'self'",
        "frame-ancestors 'self' https://*.replit.dev https://*.replit.app https://*.repl.co https://*.replit.com",
        "base-uri 'self'",
        "form-action 'self'",
        "object-src 'none'",
    });

    private const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), magnetometer=(), gyroscope=(), accelerometer=()";

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext ctx)
    {
        var headers = ctx.Response.Headers;
        if (!headers.ContainsKey("Content-Security-Policy"))
            headers["Content-Security-Policy"] = CspValue;
        if (!headers.ContainsKey("X-Content-Type-Options"))
            headers["X-Content-Type-Options"] = "nosniff";
        if (!headers.ContainsKey("Referrer-Policy"))
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        if (!headers.ContainsKey("Permissions-Policy"))
            headers["Permissions-Policy"] = PermissionsPolicy;

        return _next(ctx);
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
