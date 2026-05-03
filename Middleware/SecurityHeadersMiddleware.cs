using System.Security.Cryptography;

namespace Abs.FixedAssets.Middleware;

// Sets CSP, X-Content-Type-Options, Referrer-Policy, Permissions-Policy.
// CSP frame-ancestors allow-lists Replit edge so the preview iframe still
// works (X-Frame-Options is removed elsewhere in Program.cs).
//
// CSP hardening (Task #15):
//   * `script-src` and `style-src` no longer use 'unsafe-inline'. Instead a
//     per-request nonce is generated and emitted into the CSP header. The
//     `NonceTagHelper` (see Middleware/NonceTagHelper.cs) automatically
//     stamps the same nonce onto every <script> and <style> element rendered
//     by Razor, so legitimate inline blocks keep working while injected
//     <script> / <style> tags from an attacker are blocked by the browser.
//   * `script-src-attr` / `style-src-attr` retain 'unsafe-inline' (a CSP3
//     directive that ONLY governs inline event handlers and `style="..."`
//     attributes, NOT inline <script>/<style> elements). This preserves
//     hundreds of existing inline event handlers and ~2300 inline style
//     attributes across the Razor pages without re-introducing the broad
//     'unsafe-inline' fallback on `script-src` / `style-src`. Tightening
//     these further is a future ratchet (convert handlers to addEventListener
//     and inline styles to CSS classes).
public sealed class SecurityHeadersMiddleware
{
    public const string NonceHttpContextItemKey = "CspNonce";

    private readonly RequestDelegate _next;

    private const string PermissionsPolicy =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=(), magnetometer=(), gyroscope=(), accelerometer=()";

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext ctx)
    {
        // Per-request 128-bit nonce, hex-encoded (see GenerateNonce). Generated unconditionally
        // so the TagHelper can always read it from HttpContext.Items, even on
        // responses where another middleware has already supplied a CSP header.
        var nonce = GenerateNonce();
        ctx.Items[NonceHttpContextItemKey] = nonce;

        var headers = ctx.Response.Headers;
        if (!headers.ContainsKey("Content-Security-Policy"))
            headers["Content-Security-Policy"] = BuildCsp(nonce);
        if (!headers.ContainsKey("X-Content-Type-Options"))
            headers["X-Content-Type-Options"] = "nosniff";
        if (!headers.ContainsKey("Referrer-Policy"))
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        if (!headers.ContainsKey("Permissions-Policy"))
            headers["Permissions-Policy"] = PermissionsPolicy;

        return _next(ctx);
    }

    private static string GenerateNonce()
    {
        // Hex (not base64) so the value contains only [0-9a-f] — no '+' or
        // '/' that Razor's HTML-attribute encoder would rewrite to entities
        // (&#x2B; / &#x2F;) when the NonceTagHelper stamps it onto a tag.
        // CSP only requires the source-list value match the rendered nonce
        // attribute byte-for-byte, so any URL-/HTML-safe alphabet works.
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private static string BuildCsp(string nonce)
    {
        var nonceSrc = $"'nonce-{nonce}'";
        return string.Join("; ", new[]
        {
            "default-src 'self'",
            $"script-src 'self' {nonceSrc} https://cdnjs.cloudflare.com https://cdn.jsdelivr.net",
            // Inline event handlers (onclick=, onchange=, ...) – kept permissive
            // so existing Razor pages keep working. Does NOT relax script-src.
            "script-src-attr 'unsafe-inline'",
            $"style-src 'self' {nonceSrc} https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://fonts.googleapis.com",
            // Inline style="..." attributes – kept permissive (see above).
            "style-src-attr 'unsafe-inline'",
            "img-src 'self' data: blob: https:",
            "font-src 'self' data: https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://fonts.gstatic.com",
            "connect-src 'self'",
            "frame-ancestors 'self' https://*.replit.dev https://*.replit.app https://*.repl.co https://*.replit.com",
            "base-uri 'self'",
            "form-action 'self'",
            "object-src 'none'",
        });
    }
}

public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
