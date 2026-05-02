# Return Path / Back Navigation Audit Report

**Date:** 2026-01-24  
**Author:** CherryAI Agent  
**Status:** Implemented

## Overview

This document records the comprehensive return path / back navigation implementation across the CherryAI EAM application. The goal was to ensure users can reliably return to their originating context after drilling down into detail pages.

## Standard Adopted

### Return URL Parameter
- **Parameter name:** `returnUrl` (query string)
- **Helper location:** `Services/Navigation/ReturnUrlHelper.cs`

### Helper Methods
1. `BuildReturnUrl(HttpRequest req)` - Builds return URL from current path + query string
2. `IsSafeLocalReturnUrl(string? returnUrl)` - Validates URL is local and safe
3. `GetSafeReturnUrlOrDefault(returnUrl, defaultUrl)` - Returns validated URL or fallback
4. `GetCanonicalFallback(currentPagePath)` - Gets module's default return path
5. `GetBackUrl(returnUrl, currentPagePath)` - Combined validation + fallback

### Shared UI Component
- **Location:** `Pages/Shared/_BackLink.cshtml`
- Renders consistent back button with arrow icon
- Uses ViewData["ReturnUrl"], ViewData["FallbackPage"], ViewData["FallbackLabel"]

## Security Rules

1. **No external URLs:** Rejects URLs with schemes (http://, https://)
2. **No protocol-relative URLs:** Rejects URLs starting with //
3. **No path traversal:** Rejects URLs containing ..
4. **No XSS vectors:** Rejects URLs containing <, >, ", '
5. **No newlines:** Rejects URLs with \n, \r, \0
6. **Allowlist validation:** Only accepts paths matching known app routes
7. **Fallback behavior:** Invalid URLs fall back to canonical module list

### Allowed Base Paths
- `/`, `/Index`, `/Assets`, `/Maintenance`, `/Materials`
- `/WorkOrders`, `/Admin`, `/Reports`, `/Depreciation`
- `/CIP`, `/CCA`, `/Journals`, `/Purchasing`, `/Help`
- Plus all subpaths under these

### Canonical Fallbacks
| Detail Page | Fallback |
|-------------|----------|
| /WorkOrders/Details | /Maintenance |
| /WorkOrders/Execute | /Maintenance |
| /Maintenance/WorkRequests/Details | /Maintenance/WorkRequests |
| /Assets/Asset | /Assets |
| /Materials/ItemEdit | /Materials/Items |
| /Purchasing/Details | /Purchasing |
| /CIP/Details | /CIP |
| /Journals/Details | /Journals |
| /CCA/ClassReport | /CCA |

## Pages Updated

### Source Pages (Pass returnUrl)
| Page | Target | Method |
|------|--------|--------|
| Pages/Maintenance/Index.cshtml | /WorkOrders/Details | JavaScript rowClickUrl |
| Pages/Maintenance/WorkRequests/Index.cshtml | ./Details | asp-route-returnUrl |
| Pages/Assets/Index.cshtml | /Assets/Asset | JavaScript rowClickUrl |
| Pages/Materials/Items.cshtml | /Materials/ItemEdit | JavaScript rowClickUrl |

### Detail Pages (Accept returnUrl + Render Back)
| Page | ReturnUrl Binding | Back Link |
|------|-------------------|-----------|
| Pages/WorkOrders/Details.cshtml.cs | Yes | _BackLink partial |
| Pages/Maintenance/WorkRequests/Details.cshtml.cs | Yes | _BackLink partial |
| Pages/Assets/Asset.cshtml.cs | Yes | _BackLink partial |
| Pages/Materials/ItemEdit.cshtml.cs | Yes | _BackLink partial |

## Smoke Tests Added

### Test 53: Return Path → Open Redirect Protection
Validates ReturnUrlHelper correctly blocks malicious URLs:
- External URLs (https://evil.com)
- Protocol-relative URLs (//evil.com)
- Path traversal (//../../../etc/passwd)
- XSS vectors (/Assets<script>)
- Validates fallback behavior for rejected URLs

### Test 54: Return Path → Detail Pages Accept returnUrl
Verifies core detail pages:
- Have ReturnUrl BindProperty in page model
- Render _BackLink partial or set ViewData["ReturnUrl"]

### Test 55: Return Path → Source Pages Pass returnUrl
Verifies core list pages:
- Pass returnUrl to detail page links
- Via asp-route-returnUrl or JavaScript URL construction

## Implementation Notes

1. JavaScript rowClickUrl appends returnUrl using `encodeURIComponent(window.location.pathname + window.location.search)`
2. Razor asp-page links use `asp-route-returnUrl="@(ViewContext.HttpContext.Request.Path + ViewContext.HttpContext.Request.QueryString)"`
3. Back link styling uses CSS variables for theme consistency
4. Back button appears at top of detail page, before hero section

## Future Considerations

1. Consider adding returnUrl to more drill-down links (vendor details, manufacturer details)
2. Consider preserving returnUrl through POST redirects where appropriate
3. Consider adding browser history integration as enhancement
