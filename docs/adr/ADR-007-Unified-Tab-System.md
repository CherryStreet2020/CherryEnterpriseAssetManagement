# ADR-007: Unified Tab Navigation System

**Status:** Accepted  
**Date:** January 2026  
**Updated:** January 2026 (PR #2.1 hardening)  
**Author:** CherryAI Team

## Context

The CherryAI EAM application has accumulated multiple tab implementations across different modules:

| Pattern | Usage Count | Location |
|---------|-------------|----------|
| `.premium-tabs` | 16 pages | Various detail pages |
| `.wo-tab-link` | 6 pages | Work order/maintenance |
| `.report-tabs` | 1 page | Reports module |
| `.tab-btn` | 2 pages | Miscellaneous |

This fragmentation violates ADR-004 (UI Hygiene) by requiring per-page style blocks and creates maintenance burden when updating tab behavior or styling.

## Decision

Implement a unified tab navigation system consisting of:

1. **Shared Partial:** `Pages/Shared/_TabNav.cshtml`
2. **CSS Module:** `wwwroot/css/modules/tabs.css`
3. **JS Module:** `wwwroot/js/tabs.js`

### Implementation: CSS + JS Location

| Asset | Path | Loaded By |
|-------|------|-----------|
| CSS | `wwwroot/css/modules/tabs.css` | `_ModernLayout.cshtml` |
| JS | `wwwroot/js/tabs.js` | `_ModernLayout.cshtml` |
| Partial | `Pages/Shared/_TabNav.cshtml` | Per-page via `Html.PartialAsync` |

The JS module automatically initializes all tab navs that have `role="tablist"` (button mode). It handles:
- Click activation with proper ARIA state management
- Keyboard navigation (ArrowLeft/Right, Home/End)
- Panel visibility via `hidden` attribute or `.active` class

### Markup Contract

**Button Mode (tab panels):**
```html
<nav class="tab-nav" role="tablist" aria-label="...">
    <button class="tab-nav__item [tab-nav__item--active]" role="tab" aria-selected="true|false" ...>
        Label
        <span class="tab-nav__count">N</span>
    </button>
</nav>
```

**Link Mode (navigation):**
```html
<nav class="tab-nav" aria-label="...">
    <a class="tab-nav__item [tab-nav__item--active]" aria-current="page" ...>
        Label
        <span class="tab-nav__count">N</span>
    </a>
</nav>
```

### Class Names

| Class | Purpose |
|-------|---------|
| `.tab-nav` | Container element |
| `.tab-nav__item` | Individual tab (link or button) |
| `.tab-nav__item--active` | Active/selected state |
| `.tab-nav__count` | Optional badge for item counts |

## Mode Semantics

### Link Mode (`Mode="link"`)

**Purpose:** Normal page navigation with bookmarkable URLs.

**Rendering:**
- Renders `<a>` tags with computed href
- Container: `<nav>` with `aria-label` (NO `role="tablist"`)
- Items: NO `role="tab"`, NO `aria-selected`
- Active item: `aria-current="page"`

**Href Dual Behavior:**
- If `keyOrHref` starts with `?`, `/`, or `http`: use verbatim as href
- Otherwise: treat as key and build `href="?tab={key}"`

**Use for:** Server-side tab switching, bookmarkable/shareable tab states.

### Button Mode (`Mode="button"`)

**Purpose:** Client-side tab panel control.

**Rendering:**
- Renders `<button type="button">` tags with `data-tab` attribute
- Container: `<nav role="tablist">` with `aria-label`
- Items: `role="tab"`, `aria-selected="true|false"`, `aria-controls="panel-{key}"`

**Use for:** JavaScript-driven tab panels where state stays client-side.

### Usage Example

```razor
@await Html.PartialAsync("_TabNav", new ViewDataDictionary(ViewData) {
    { "TabId", "my-tabs" },
    { "Mode", "link" },
    { "AriaLabel", "Section navigation" },
    { "Tabs", new (string keyOrHref, string label, bool active, int? count, string icon)[] {
        ("overview", "Overview", activeTab == "overview", null, null),
        ("details", "Details", activeTab == "details", 5, null),
        ("history", "History", activeTab == "history", null, null)
    }}
})
```

## Accessibility Requirements

### Focus States
- All tab items must support `:focus-visible` styling
- Focus ring must be visible and meet WCAG contrast requirements

### Link Mode Accessibility
- Use `aria-current="page"` on the active link
- Do NOT use `role="tab"` or `aria-selected` (these are for tab panels)
- Container should NOT have `role="tablist"`

### Button Mode Accessibility
- Container must have `role="tablist"` and `aria-label`
- Each button must have `role="tab"` and `aria-selected`
- Use `aria-controls` to link to panel IDs when panels exist

### Icon Parameter
- The `icon` parameter must contain trusted static SVG markup only
- Never pass user-provided content to the icon field
- Icons are rendered via `Html.Raw()` for efficiency

## Compatibility Shim Policy

### Current Shims

| Shim | Status | Reason |
|------|--------|--------|
| `.wo-tab-link` | TEMPORARY | Existing JS uses this selector |
| `.wo-tab-count` | REMOVED | Replaced by `.tab-nav__count` |
| `.active` | REMOVED | Replaced by `.tab-nav__item--active` |

### Shim Rules
1. `.wo-tab-link` is included in BOTH modes for JS compatibility
2. This shim will be removed after full migration is complete (Phase 4)
3. New pages must NOT depend on shim classes
4. JS should be updated to use `.tab-nav__item` selector when possible

## Migration Strategy

### Phase 1: Pilot (Complete)
- `Pages/Maintenance/Details.cshtml` migrated as pilot
- Validates partial design and JS compatibility
- PR #2.1: Hardened ARIA semantics and href behavior

### Phase 2: Work Order Module
- Migrate remaining `.wo-tab-link` pages (5 pages)
- Remove redundant styles from `work-order-details.css`

### Phase 3: Premium Tabs (In Progress)
- `Pages/Assets/Asset.cshtml` migrated (PR #3)
- Remaining: 15 `.premium-tabs` pages
- Update related CSS files

### Phase 4: Cleanup
- Remove `.wo-tab-link` shim from partial
- Remove legacy tab CSS from module files
- Update JS to use `.tab-nav__item` selector
- Update allowlist documentation

## Prohibited Patterns

After full migration, these patterns are prohibited:

1. **Per-page tab CSS** - All tabs must use `tabs.css`
2. **Inline styles on tabs** - Violates ADR-004
3. **Custom tab class names** - Use `.tab-nav*` classes only
4. **Direct tab markup** - Always use `_TabNav` partial
5. **Using `role="tab"` in link mode** - Incorrect ARIA semantics

## Consequences

### Positive
- Single source of truth for tab styling
- Correct ARIA semantics per mode
- Easier maintenance and updates
- Reduced CSS bundle size (after cleanup)
- Bookmarkable URLs in link mode

### Negative
- Migration effort required for existing pages
- Temporary style duplication during migration
- Need to maintain JS compatibility shims during transition

## Premium Tabs Standard (_TabNav)

This section provides a consolidated quick-reference for the canonical tab implementation.

### 1. Canonical Component

- **Partial:** `Pages/Shared/_TabNav.cshtml`
- **CSS:** `wwwroot/css/modules/tabs.css`
- **JS:** `wwwroot/js/tabs.js`
- **Usage:**
  ```razor
  @await Html.PartialAsync("_TabNav", new ViewDataDictionary(ViewData) {
      { "TabId", "my-tabs" },
      { "Mode", "button" },
      { "UrlParam", "tab" },
      { "AriaLabel", "Section tabs" },
      { "Tabs", new (string keyOrHref, string label, bool active, int? count, string icon)[] {
          ("overview", "Overview", activeTab == "overview", null, null),
          ("details", "Details", activeTab == "details", 5, null)
      }}
  })
  ```

### 2. Modes

| Mode | Purpose | Required Params |
|------|---------|-----------------|
| `Mode="button"` | Client-side panels | `TabId`, `UrlParam`, `AriaLabel` |
| `Mode="link"` | Filter/navigation | `Variant="primary"`, `AriaLabel` |

**Button Mode Panel Requirements:**
```html
<div class="tab-panel" 
     id="panel-{key}" 
     role="tabpanel" 
     aria-labelledby="{TabId}-{key}"
     hidden>
```
- Active panel: omit `hidden` attribute
- Inactive panels: include `hidden` attribute

**Link Mode:** Uses `aria-current="page"` semantics (handled automatically by `_TabNav`).

### 3. Variants

| Variant | Use Case |
|---------|----------|
| `Variant="primary"` | Page-level tabs (default for link mode) |
| `Variant="compact"` | Sub-tabs, form sections, nested tabs |

### 4. URL Sync

The `UrlParam` parameter enables `history.replaceState` updates via `tabs.js`:

| Param Value | Use Case |
|-------------|----------|
| `"tab"` | Main page tabs |
| `"subtab"` | Nested partial tabs (avoids collision with parent) |

### 5. Accessibility Requirements

| Element | Attribute | Notes |
|---------|-----------|-------|
| Container | `role="tablist"` | Button mode only |
| Tab button | `role="tab"`, `aria-selected` | Button mode only |
| Tab button | `aria-controls="panel-{key}"` | Links tab to panel |
| Tab link | `aria-current="page"` | Link mode, active item |
| Panel | `role="tabpanel"` | Required |
| Panel | `aria-labelledby="{TabId}-{key}"` | Links panel to tab |

**Keyboard Support (via `tabs.js`):**
- `ArrowLeft` / `ArrowRight`: Navigate between tabs
- `Home` / `End`: Jump to first/last tab
- `Enter` / `Space`: Activate focused tab

### 6. Do / Don't List

| DO | DON'T |
|----|-------|
| Use `_TabNav` partial for all tabs | Write custom tab JavaScript |
| Use `tab-panel` class on panels | Use legacy patterns: `premium-tab-*`, `report-tabs`, `tab-btn` |
| Set `hidden` attribute for inactive panels | Use CSS display/visibility for panel switching |
| Use unique `TabId` per tab group | Use `onclick="showTab()"` inline handlers |
| Use `subtab` param for nested tabs | Reuse parent's URL param in nested tabs |

## References

- [ADR-004: UI Hygiene - No Inline Styles](ADR-004-UI-Hygiene-No-Inline-Styles.md)
- [UX Standards: Tabs Section](../UXStandards.md#tabs)
- [Premium UX Alignment Execution Plan](../Premium-UX-Alignment-Execution-Plan.md)
- [WAI-ARIA: Tab Pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/)
