# ADR-006: ReturnUrl Helper Hardened Against Open Redirect and Bypass Attacks

**Status:** Accepted  
**Date:** 2026-01-24  
**Deciders:** Development Team  
**Categories:** Security, Architecture

---

## Context

CherryAI EAM uses `returnUrl` parameters for context-preserving navigation (e.g., returning to a filtered list after viewing a detail page). Security concerns:

1. **Open Redirect**: Attacker could inject external URLs (`?returnUrl=https://evil.com`)
2. **Protocol-Relative Bypass**: URLs like `//evil.com` bypass scheme checks
3. **Path Traversal**: URLs with `..` could escape intended scope
4. **XSS Injection**: URLs with `<script>` could execute JavaScript
5. **Newline Injection**: URLs with `%0d%0a` could inject headers

Without validation, these could be exploited for phishing or XSS attacks.

## Decision

**Implement comprehensive URL validation in `ReturnUrlHelper` with multiple security layers.**

Validation rules:
1. **No external schemes** - Reject `http://`, `https://`, `javascript:`, etc.
2. **No protocol-relative URLs** - Reject URLs starting with `//`
3. **No path traversal** - Reject URLs containing `..`
4. **No XSS vectors** - Reject URLs containing `<`, `>`, `"`, `'`
5. **No newlines** - Reject URLs with `\r`, `\n`, `%0d`, `%0a`
6. **Allowlist validation** - URL path must match known routes
7. **Fallback on failure** - Invalid URLs redirect to canonical module page

## Alternatives Considered

### Alternative 1: No Return URL Parameter
- **Description:** Don't allow return navigation
- **Pros:** Eliminates vulnerability entirely
- **Cons:** Poor UX, users lose context
- **Why rejected:** Context preservation is important feature

### Alternative 2: Simple Relative URL Check
- **Description:** Only check that URL doesn't start with `http`
- **Pros:** Simple implementation
- **Cons:** Easily bypassed with `//`, `javascript:`, etc.
- **Why rejected:** Insufficient security

### Alternative 3: Encrypted Return URLs
- **Description:** Encrypt/sign return URLs with server key
- **Pros:** Tamper-proof
- **Cons:** Complex, URL bloat, key management
- **Why rejected:** Overkill for current threat model

## Consequences

### Positive
- Prevents open redirect attacks
- Blocks XSS injection via URLs
- Blocks newline injection
- Safe fallback behavior
- Automated smoke test validation

### Negative
- Some valid URLs might be rejected if not in route allowlist
- Additional processing on every return URL

### Neutral
- Users see canonical fallback if URL invalid
- No error shown to user on validation failure

## Implementation Notes

### ReturnUrlHelper.cs

```csharp
public static class ReturnUrlHelper
{
    public static bool IsValidReturnUrl(string? url, IEnumerable<string>? allowedPaths = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        
        // Decode URL for inspection
        var decoded = Uri.UnescapeDataString(url);
        
        // Check for external schemes
        if (decoded.Contains("://") || decoded.StartsWith("//"))
            return false;
        
        // Check for javascript scheme
        if (decoded.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            return false;
        
        // Check for path traversal
        if (decoded.Contains(".."))
            return false;
        
        // Check for XSS vectors
        if (decoded.ContainsAny('<', '>', '"', '\''))
            return false;
        
        // Check for newline injection
        if (decoded.ContainsAny('\r', '\n'))
            return false;
        
        // Check encoded newlines
        if (url.Contains("%0d", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("%0a", StringComparison.OrdinalIgnoreCase))
            return false;
        
        // Must start with /
        if (!url.StartsWith("/"))
            return false;
        
        // Optional: Check against allowlist
        if (allowedPaths != null)
        {
            var path = url.Split('?')[0];
            if (!allowedPaths.Any(p => path.StartsWith(p)))
                return false;
        }
        
        return true;
    }
    
    public static string GetSafeReturnUrl(string? url, string fallback)
    {
        return IsValidReturnUrl(url) ? url! : fallback;
    }
}
```

### Usage in Pages

```csharp
public class DetailsModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }
    
    public string SafeReturnUrl => 
        ReturnUrlHelper.GetSafeReturnUrl(ReturnUrl, "/Assets");
}
```

### Smoke Test

```csharp
// Test 53: Return Path → Open Redirect Protection
var testUrls = new[]
{
    ("https://evil.com", false),
    ("//evil.com", false),
    ("javascript:alert(1)", false),
    ("/../../../etc/passwd", false),
    ("/Assets<script>", false),
    ("/Assets%0d%0aSet-Cookie:evil", false),
    ("/Assets?filter=active", true),
    ("/Assets/Asset/123?returnUrl=%2FAssets", true),
};
```

## Related Documents

- [NavigationAndRouting.md](../NavigationAndRouting.md) - Navigation rules
- [TenancyAndSecurity.md](../TenancyAndSecurity.md) - Security model
- [ReturnPathAuditReport.md](../ReturnPathAuditReport.md) - Implementation audit

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-01-24 | Development Team | Initial version |
