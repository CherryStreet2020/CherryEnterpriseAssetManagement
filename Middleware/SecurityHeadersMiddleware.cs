namespace Abs.FixedAssets.Middleware;

/// <summary>
/// Phase 4 security headers. Sets four production-grade headers on every
/// response:
///
///   Content-Security-Policy
///   X-Content-Type-Options: nosniff
///   Referrer-Policy: strict-origin-when-cross-origin
///   Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()
///
/// Iframe-embedding compatibility (CRITICAL):
///   The existing iframe-fix middleware in Program.cs deliberately removes
///   X-Frame-Options so the Replit canvas/preview iframe can host the app.
///   This middleware preserves that by using CSP <c>frame-ancestors</c>
///   with an explicit allow-list for the Replit edge domains plus 'self'.
///   DO NOT tighten frame-ancestors to 'self' or 'none' — it will break
///   the in-product preview pane.
///
/// Inline script/style policy:
///   The codebase contains many inline &lt;script&gt; blocks (Razor pages
///   like Assets, Maintenance, CIP, etc.) and ~150 occurrences of
///   <c>style="..."</c>. Migrating all of those to nonces or external
///   files is a larger refactor; for now CSP allows 'unsafe-inline' for
///   script-src and style-src so the policy is non-breaking. Future
///   hardening can ratchet to nonces page-by-page.
///
/// Already-fetched CDNs whitelisted: cdnjs.cloudflare.com (FontAwesome),
/// cdn.jsdelivr.net (Tom Select). 'data:' is allowed for fonts and
/// images (icon fonts, base64 thumbnails).
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    // Built once at construction; identical bytes on every response.
    private static readonly string CspValue = string.Join("; ", new[]
    {
        "default-src 'self'",
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net",
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
        // Set headers BEFORE any downstream middleware writes the body so
        // they're sent in the very first packet of the response.
        var headers = ctx.Response.Headers;

        // Use indexer assignment so this overwrites any default the host
        // might add later (e.g. Razor middleware setting X-XSS-Protection).
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
