# ADR-008: Unified Screen Header System

**Status:** Accepted  
**Date:** January 2026  
**Author:** CherryAI Team

## Context

The CherryAI EAM application has 97+ pages using `page-hero` class patterns with significant drift:

| Issue | Frequency | Example |
|-------|-----------|---------|
| Inline style on status pills | Multiple pages | `style="background: #f59e0b;"` |
| Inconsistent status classes | High | `page-hero-status warning` vs `page-hero-status active` |
| Duplicated header markup | 97 pages | Full `<div class="page-hero">` structure repeated |
| Custom title/subtitle variants | Medium | Different class names and structures |

### Existing Partial Gaps

| Partial | Issue |
|---------|-------|
| `_AssetMaintenanceHeader.cshtml` | Module-specific (hardcoded pills/links), not generalizable |
| Legacy header partial | **REMOVED** — Was too simple, superseded by `_ScreenHeader.cshtml` |
| `_BackLink.cshtml` | Good, reusable for back navigation |

## Decision

Implement a unified screen header system:

1. **New Partial:** `Pages/Shared/_ScreenHeader.cshtml`
2. **CSS Module:** `wwwroot/css/modules/headers.css`
3. **Slot Strategy:** Use partial references (not Html.Raw) for extensibility

### ViewData Contract

| Key | Type | Required | Description |
|-----|------|----------|-------------|
| `HeaderTitle` | string | Yes | Page title (renders as `<h1>`). Falls back to `Title` if empty. |
| `Subtitle` | string | No | Description below title |
| `TypeLabel` | string | No | Module/category label (e.g., "Procurement") |
| `StatusText` | string | No | Status badge text |
| `StatusTone` | string | No | `info`\|`warning`\|`success`\|`danger`\|`muted` (default: info) |
| `ContextText` | string | No | Simple context line |
| `ContextPartial` | string | No | Partial name for complex context |
| `Breadcrumbs` | (Label, Href)[] | No | Breadcrumb trail (empty href = current) |
| `ShowBackLink` | bool | No | Show `_BackLink` partial |
| `BackLinkFallback` | string | No | Fallback URL for back link |
| `BackLinkLabel` | string | No | Label for back link |
| `KpisPartial` | string | No | Partial name for KPI strip |
| `KpisViewData` | ViewDataDictionary | No | Data for KPI partial |
| `ActionsPartial` | string | No | Partial name for actions row |
| `ActionsViewData` | ViewDataDictionary | No | Data for actions partial |

> **Important:** Do NOT use `ViewData["Title"]` for `_ScreenHeader` inputs. `ViewData["Title"]` is reserved for page/layout title and will often already be set by the calling page. Use `HeaderTitle` instead to avoid duplicate key exceptions. Use indexer syntax `["Key"] = value` instead of collection initializer `{ "Key", value }` to safely overwrite.

> **Required:** Pages using `_ScreenHeader` must also set `ViewData["HasScreenHeader"] = true;` in the page's `@{ }` block. This suppresses the layout's default title strip (`<h1 class="header-title">`) and ensures a single `<h1>` per page (accessibility requirement).

### Stable Class Contract

```
.screen-header                 - Container
.screen-header__breadcrumbs    - Breadcrumb nav
.screen-header__main           - Main content row
.screen-header__info           - Left side (title area)
.screen-header__meta           - Type label + status row
.screen-header__type           - Module type label
.screen-header__title          - Page title (<h1>)
.screen-header__subtitle       - Description
.screen-header__context        - Location/context line
.screen-header__kpis           - KPI strip container
.screen-header__actions        - Actions row
```

### Status Tone System

Reuses existing `.status-pill.tone-*` classes from `premium-components.css`:

```css
.status-pill.tone-info { ... }
.status-pill.tone-warning { ... }
.status-pill.tone-success { ... }
.status-pill.tone-danger { ... }
.status-pill.tone-muted { ... }
```

No new status styling is introduced. Inline `style=""` on status pills is prohibited.

### Slot Strategy

Slots use partial references (preferred over `Html.Raw`):

```razor
@await Html.PartialAsync("_ScreenHeader", new ViewDataDictionary(ViewData) {
    { "KpisPartial", "/Pages/Module/_MyKpis.cshtml" },
    { "ActionsPartial", "/Pages/Module/_MyActions.cshtml" }
})
```

Benefits:
- Type safety for Model binding in partials
- Easier testing and maintenance
- Clear ownership of slot content

## Migration Checklist

When migrating a page to use `_ScreenHeader`, complete ALL of the following:

1. **Set HasScreenHeader flag** — Add `ViewData["HasScreenHeader"] = true;` in the page's `@{ }` block to suppress the layout's default title strip.

2. **Use HeaderTitle, not Title** — Pass `HeaderTitle` to `_ScreenHeader`. Never use `Title` as input; it's reserved for page title and causes duplicate key exceptions.

3. **Use indexer syntax for ViewDataDictionary** — Use `["Key"] = value` (not collection initializer `{ "Key", value }`) to safely overwrite existing keys.

4. **Verify in-app before merging** — Navigate to the page and confirm:
   - Page loads without exception
   - Only ONE header appears (no duplicate `<h1>` from layout)
   - Screen header renders with expected content

## Migration Strategy

### Phase 1: Pilot (This PR)
- Create `_ScreenHeader.cshtml` and `headers.css`
- Migrate `Pages/Purchasing/Index.cshtml` as pilot
- Validate visual parity and smoke tests

### Phase 2: Core Modules (Future)
- Migrate remaining list pages: Maintenance, Materials, AccountsPayable
- Prioritize pages with inline style violations

### Phase 3: Detail Pages (Future)
- Migrate detail/edit pages
- Consider if `_AssetMaintenanceHeader` can be deprecated

### Phase 4: Full Rollout (Future)
- Migrate all remaining pages
- Delete or archive deprecated partials

## Prohibited Patterns

After full migration:

1. **Direct page-hero markup** - Use `_ScreenHeader` partial
2. **Inline styles on headers** - Violates ADR-004
3. **Custom status pill classes** - Use `status-pill tone-*`
4. **Multiple `<h1>` elements** - One per page via `_ScreenHeader`
5. **Html.Raw for slots** - Use partial references
6. **Custom header class names** - Use `.screen-header__*` classes

## Accessibility Requirements

| Element | Requirement |
|---------|-------------|
| `<header>` | Semantic container (no role needed) |
| `<h1>` | Exactly one per page, contains title |
| Breadcrumb `<nav>` | `aria-label="Breadcrumb"` |
| Current breadcrumb | `aria-current="page"` |
| Status pill | Visible text (color not sole indicator) |

## Consequences

### Positive
- Single source of truth for header structure
- Eliminates inline style violations
- Consistent accessibility
- Easier theme changes (all headers update together)
- Reduced page size (no duplicated markup)

### Negative
- Migration effort for 97+ pages
- Need to create slot partials for complex pages
- Temporary duplication during migration

## Migration Log

Completed migrations are logged here for traceability and pattern reference.

### 2026-01-25: Pages/Help/Index.cshtml

**Migrated by:** CherryAI Agent  
**Branch:** screenheader/help-index  
**Net diff:** -49 lines (removed 62-line page-hero block, added 13-line _ScreenHeader call-site)

**Slot Partials Created:**
- `Pages/Help/_HelpIndexContext.cshtml` - Documentation/Topics context pills
- `Pages/Help/_HelpIndexKpis.cshtml` - 4 KPI cards (Guides, Topics, Tasks, Phases)
- `Pages/Help/_HelpIndexActions.cshtml` - Dashboard + Implementation Guide buttons

**Header Keys Used:**
```razor
ViewData["HasScreenHeader"] = true;

var headerVd = new ViewDataDictionary(ViewData) {
    ["HeaderTitle"] = "Help Center",
    ["Subtitle"] = "Find step-by-step guides, learn key concepts, and get the most out of your asset management system",
    ["TypeLabel"] = "Support",
    ["StatusText"] = "Online",
    ["StatusTone"] = "success",
    ["ContextPartial"] = "/Pages/Help/_HelpIndexContext.cshtml",
    ["KpisPartial"] = "/Pages/Help/_HelpIndexKpis.cshtml",
    ["ActionsPartial"] = "/Pages/Help/_HelpIndexActions.cshtml"
};
```

**Smoke Test Added:** `Route Health → Help Index Renders`  
**Verification:** `curl http://localhost:5000/Help` returns HTTP 200, screenshot confirms visual parity

---

### 2026-01-25: Pages/UsTax/Index.cshtml

**Migrated by:** CherryAI Agent  
**Branch:** screenheader/ustax-index  
**Net diff:** -55 lines (removed 68-line page-hero block, added 13-line _ScreenHeader call-site)

**Slot Partials Created:**
- `Pages/UsTax/_UsTaxIndexContext.cshtml` - Tax Year / Assets count context pills
- `Pages/UsTax/_UsTaxIndexKpis.cshtml` - 4 KPI cards (Section 179, Bonus Rate, Tax Year, Assets)
- `Pages/UsTax/_UsTaxIndexActions.cshtml` - Dashboard, Form 4562, Canadian CCA buttons

**Header Keys Used:**
```razor
ViewData["HasScreenHeader"] = true;

var headerVd = new ViewDataDictionary(ViewData) {
    ["HeaderTitle"] = "US Tax (MACRS/179)",
    ["Subtitle"] = "Manage Section 179, Bonus Depreciation, and MACRS settings for US tax compliance",
    ["TypeLabel"] = "Tax Compliance",
    ["StatusText"] = "IRS",
    ["StatusTone"] = "info",
    ["ContextPartial"] = "/Pages/UsTax/_UsTaxIndexContext.cshtml",
    ["KpisPartial"] = "/Pages/UsTax/_UsTaxIndexKpis.cshtml",
    ["ActionsPartial"] = "/Pages/UsTax/_UsTaxIndexActions.cshtml"
};
```

**Smoke Test Added:** `Route Health → UsTax Index Renders`  
**Verification:** Navigate to `/Admin/SmokeTests` and run suite; `curl http://localhost:5000/UsTax` returns HTTP 200

---

### 2026-01-25: Pages/Assets/Asset.cshtml (Layout Chrome Suppression)

**Fixed by:** CherryAI Agent  
**Branch:** ui-fix/asset-detail-breadcrumb-chrome

**Issue:** Asset detail page (`/Assets/Asset/{id}`) uses a custom `asset-hero` component with its own `<h1>`. In View mode, the layout rendered both:
1. `<h1 class="header-title">Asset Management</h1>` - duplicate H1
2. `<span class="header-breadcrumb">Asset Register</span>` - redundant breadcrumb row

**Root Cause:** Missing `HideDefaultPageHeader` flag and unconditionally setting `ViewData["Breadcrumb"]`.

**Fix Applied (conditional suppression for View mode only):**
```razor
// View mode: suppress layout header and breadcrumb (custom asset-hero provides these)
// Create mode: show breadcrumb for context
if (!Model.IsCreateMode)
{
    ViewData["HideDefaultPageHeader"] = true;
    // Do NOT set Breadcrumb - suppresses the header-breadcrumb row
}
else
{
    ViewData["Breadcrumb"] = UiTerms.AssetManagement;
}
```

**Layout Mechanism (Pages/Shared/_ModernLayout.cshtml):**
- `ViewData["HideDefaultPageHeader"] = true` → suppresses `<h1 class="header-title">` (lines 832-834)
- `ViewData["Breadcrumb"] = null` (not set) → suppresses `<span class="header-breadcrumb">` (lines 835-838)

**Mode Behavior:**
- View/Edit modes: Both layout H1 and breadcrumb row suppressed (custom asset-hero provides title/context)
- Create mode: Breadcrumb row shown for navigation context

**Note:** This is NOT a full migration to `_ScreenHeader`. The custom `asset-hero` block is preserved due to its specialized features (image upload, QR code, health ring, mode-dependent actions). Full migration deferred pending design review.

**Smoke Test Updated:** `Route Health → Asset Detail Renders`  
- Asserts: Contains `asset-hero` ✅
- Asserts: No `class="header-title"` ✅  
- Asserts: No `class="header-breadcrumb"` ✅

**Verification:** Navigate to `/Admin/SmokeTests` and run suite; `curl http://localhost:5000/Assets/Asset/1` returns HTTP 200

---

### 2026-01-24: Pages/Materials/Items.cshtml

**Migrated by:** CherryAI Agent  
**Branch:** screenheader/items-index

**Slot Partials Created:**
- `Pages/Materials/_ItemsIndexContext.cshtml`
- `Pages/Materials/_ItemsIndexKpis.cshtml`
- `Pages/Materials/_ItemsIndexActions.cshtml`

**Smoke Test Added:** `Route Health → Items Index Renders`

---

### 2026-01-23: Pages/Assets/Index.cshtml

**Migrated by:** CherryAI Agent  
**Branch:** screenheader/assets-index

**Slot Partials Created:**
- `Pages/Assets/_AssetsIndexContext.cshtml`
- `Pages/Assets/_AssetsIndexKpis.cshtml`
- `Pages/Assets/_AssetsIndexActions.cshtml`

**Smoke Test Added:** `Route Health → Assets Index Renders`

---

### 2026-01-22: Pages/Purchasing/Index.cshtml (Pilot)

**Migrated by:** CherryAI Agent  
**Branch:** screenheader/purchasing-pilot

**Slot Partials Created:**
- `Pages/Purchasing/_PurchasingIndexContext.cshtml`
- `Pages/Purchasing/_PurchasingIndexKpis.cshtml`
- `Pages/Purchasing/_PurchasingIndexActions.cshtml`

**Smoke Test Added:** `Route Health → Purchasing Index Renders`

---

## How to Migrate the Next Page

Follow this checklist for each new page migration:

1. **Identify legacy header block** - Find the `<div class="page-hero">` block (typically 50-70 lines)

2. **Extract slot partials** - Create three partials in the same folder:
   - `_<PageName>Context.cshtml` - Location/context pills
   - `_<PageName>Kpis.cshtml` - KPI cards
   - `_<PageName>Actions.cshtml` - Tags and action buttons

3. **Set HasScreenHeader flag** - Add `ViewData["HasScreenHeader"] = true;` in the page's `@{ }` block

4. **Insert _ScreenHeader call-site** - Use indexer syntax with allowed keys:
   ```razor
   var headerVd = new ViewDataDictionary(ViewData) {
       ["HeaderTitle"] = "Page Title",  // NOT "Title"
       ["Subtitle"] = "Description",
       ["TypeLabel"] = "Module",
       ["StatusText"] = "Active",
       ["StatusTone"] = "success",
       ["ContextPartial"] = "/Pages/Module/_ContextPartial.cshtml",
       ["KpisPartial"] = "/Pages/Module/_KpisPartial.cshtml",
       ["ActionsPartial"] = "/Pages/Module/_ActionsPartial.cshtml"
   };
   @await Html.PartialAsync("_ScreenHeader", headerVd)
   ```

5. **Add Route Health smoke test** - Add test to `Services/Testing/SmokeTestRunner.cs`:
   - Add entry to test list: `("PageName Index Renders", () => RunPageNameIndexRendersTestAsync(), null)`
   - Add test method following the Items test pattern (same harness)
   - Use canonical principal helper, ITenantContextOverride.BeginScope, IActionInvokerFactory

6. **Run verification tests:**
   Navigate to `/Admin/SmokeTests` and run the full smoke test suite (in-process tests).

7. **Visual verification** - Take screenshot to confirm visual parity

## Guardrail Rules

| Rule | Allowed | Blocked |
|------|---------|---------|
| `ViewData["Title"]` for layout | YES | - |
| `["HeaderTitle"] = "..."` for _ScreenHeader | YES | - |
| `["Title"] = "..."` as _ScreenHeader input | - | YES (causes duplicate key) |
| Inline styles in slot partials | - | YES (violates ADR-004) |

---

## Fallback Header Rule (_ModernLayout)

The layout file `Pages/Shared/_ModernLayout.cshtml` contains a fallback header mechanism (lines 832-838):

```csharp
var hasScreenHeader = ViewData["HasScreenHeader"] as bool? ?? false;
var hideDefaultPageHeader = ViewData["HideDefaultPageHeader"] as bool? ?? false;

@if (!hasScreenHeader && !hideDefaultPageHeader)
{
    <h1 class="header-title">@ViewData["Title"]</h1>
}
```

**Rule:** The fallback `<h1 class="header-title">` renders UNLESS one of these flags is true:
- `ViewData["HasScreenHeader"] = true` — page uses `_ScreenHeader` partial
- `ViewData["HideDefaultPageHeader"] = true` — page has custom header/exception

### DO / DON'T List (Preventing Double Headers)

| DO | DON'T |
|----|-------|
| Set `HasScreenHeader = true` when using `_ScreenHeader` | Forget to set a flag when adding custom `<h1>` |
| Set `HideDefaultPageHeader = true` for custom headers | Add `<h1>` without suppressing fallback |
| Set flags in page `@{ }` block before markup | Rely on partials to set flags (they can't modify parent ViewData) |
| Verify only ONE `<h1>` renders per page | Mix `_ScreenHeader` with inline `<h1>` |
| Run smoke tests after header changes | Skip `/api/smoke/run` verification |

---

## Approved Exceptions Inventory

### When to Use `HideDefaultPageHeader = true`

Use this flag for pages that:
1. Have **custom auth/system UI** (centered cards, specialized hero layouts)
2. Use **bespoke header partials** (e.g., `_AssetMaintenanceHeader`)
3. Need **no visible header** (redirect pages, minimal UI)

### When `Layout = null` is Acceptable

Only for pages that:
- Are pure redirects with no visible content
- Render completely custom HTML (e.g., embedded widgets)

### Current Exception Pages (January 2026)

| Page | Exception Type | Flag/Setting |
|------|----------------|--------------|
| `Pages/Account/AccessDenied.cshtml` | Custom error card | `HideDefaultPageHeader = true` |
| `Pages/Account/Login.cshtml` | Custom login hero | `HideDefaultPageHeader = true` |
| `Pages/Account/Logout.cshtml` | Redirect page | `Layout = null` |
| `Pages/Admin/PMTemplateEdit.cshtml` | Uses `_AssetMaintenanceHeader` | `HideDefaultPageHeader = true` |
| `Pages/Error.cshtml` | Custom error section | `HideDefaultPageHeader = true` |
| `Pages/ModuleDisabled.cshtml` | Custom centered warning | `HideDefaultPageHeader = true` |
| `Pages/Privacy.cshtml` | Simple content section | `HideDefaultPageHeader = true` |

---

## Checklist for New Pages

Before creating or modifying any page:

### 1. Decide: `_ScreenHeader` vs Exception

| If your page needs... | Use |
|-----------------------|-----|
| Standard list/detail/form layout | `_ScreenHeader` + `HasScreenHeader = true` |
| Custom hero/auth UI | Custom markup + `HideDefaultPageHeader = true` |
| No layout at all | `Layout = null` |

### 2. Ensure Exactly One Visible `<h1>`

- `_ScreenHeader` provides the `<h1>` — do NOT add another
- Custom headers must include their own `<h1>` but suppress layout fallback

### 3. Set Title + Correct Flag

```razor
@{
    ViewData["Title"] = "Page Title";        // Always set for <title> and fallback
    ViewData["HasScreenHeader"] = true;      // OR HideDefaultPageHeader = true
}
```

### 4. Validate

```bash
# Run smoke tests
curl -s http://127.0.0.1:5000/api/smoke/run | jq '.allPassed, .failed'

# Verify no double-header risk
grep -rl '<h1' Pages --include="*.cshtml" | grep -v Shared | while read f; do
  if ! grep -q 'HasScreenHeader.*true\|HideDefaultPageHeader.*true' "$f"; then
    if ! grep -q 'Layout.*null' "$f"; then echo "RISK: $f"; fi
  fi
done
```

---

## References

- [ADR-004: UI Hygiene - No Inline Styles](ADR-004-UI-Hygiene-No-Inline-Styles.md)
- [ADR-007: Unified Tab System](ADR-007-Unified-Tab-System.md)
- [UX Standards: Screen Headers](../UXStandards.md#screen-headers)
