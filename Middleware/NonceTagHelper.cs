using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Abs.FixedAssets.Middleware;

// Stamps the per-request CSP nonce (set by SecurityHeadersMiddleware) onto
// every <script> and <style> element rendered through Razor. This lets the
// CSP header drop 'unsafe-inline' from script-src / style-src while keeping
// every legitimate inline <script>{...}</script> and <style>{...}</style>
// block in the codebase functional. External `<script src=…>` and
// `<link rel=stylesheet>` tags are unaffected by adding a nonce — browsers
// simply ignore it for resources allowed by an explicit allowlisted origin.
//
// If the developer has already supplied a `nonce="..."` attribute we leave
// it alone (don't double-stamp).
[HtmlTargetElement("script")]
[HtmlTargetElement("style")]
public sealed class NonceTagHelper : TagHelper
{
    [HtmlAttributeNotBound]
    [ViewContext]
    public ViewContext ViewContext { get; set; } = default!;

    // Run after most built-in tag helpers (e.g. asp-append-version) so we
    // don't fight them, but the order doesn't really matter for the nonce.
    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (output.Attributes.ContainsName("nonce"))
            return;

        var http = ViewContext?.HttpContext;
        if (http is null)
            return;

        if (http.Items.TryGetValue(SecurityHeadersMiddleware.NonceHttpContextItemKey, out var v)
            && v is string nonce
            && !string.IsNullOrEmpty(nonce))
        {
            output.Attributes.SetAttribute("nonce", nonce);
        }
    }
}
