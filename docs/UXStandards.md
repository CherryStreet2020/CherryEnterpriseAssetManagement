# CherryAI EAM - UX Standards
Last updated: 2026-01-26


## Overview

This document defines the UX standards and UI component contracts for CherryAI EAM. All pages must conform to these standards, enforced by smoke tests.

## Design Tokens

### Policy

**All color values must reference design tokens.** Hard-coded hex values outside `tokens.css` are prohibited.

See [ADR-010](adr/ADR-010-Design-Tokens.md) for the full specification.

### Token Files

| File | Purpose | Load Order |
|------|---------|------------|
| `wwwroot/css/tokens.css` | Token definitions (colors, spacing, typography) | 1st |
| `wwwroot/css/base.css` | Base focus/selection styles | 2nd |
| `wwwroot/css/modern.css` | Legacy aliases + core components | 3rd |

### Token Families

| Family | Example | Usage |
|--------|---------|-------|
| `--color-brand-*` | `--color-brand-600` | Brand palette (900-50) |
| `--color-surface-*` | `--color-surface-1` | Background colors |
| `--color-border-*` | `--color-border-1` | Border colors |
| `--color-text-*` | `--color-text-1` | Text colors |
| `--color-success-*` | `--color-success-600` | Success semantic |
| `--color-warning-*` | `--color-warning-600` | Warning semantic |
| `--color-danger-*` | `--color-danger-600` | Danger semantic |
| `--gradient-*` | `--gradient-hero` | Pre-built gradients |
| `--font-size-*` | `--font-size-14` | Typography scale |
| `--space-*` | `--space-9` | Spacing scale (4px grid) |
| `--radius-*` | `--radius-lg` | Border radius |
| `--shadow-*` | `--shadow-card` | Box shadows |

### Prohibited

- Hard-coded hex colors in CSS (except `tokens.css`)
- Inline `style=` attributes with colors
- `<style>` blocks with color definitions
- New color hues without ADR amendment (brand lock)

## Header Rule: Exactly One Header System Per Page

### Policy

**Each page must render exactly ONE header system.** Combining multiple header patterns causes visual collisions and violates the unified header contract.

### Prohibited Combinations

The following combinations are detected by the "No Double Header" smoke test and will cause test failures:

| Header A | Header B | Why Prohibited |
|----------|----------|----------------|
| `_AssetMaintenanceHeader` | `page-hero` | Double title/subtitle, duplicate KPIs |
| `_ScreenHeader` | `page-hero` | Competing H1 elements |
| `screen-header` class | `page-hero` class | Same as above |

### Canonical Patterns

| Page Cluster | Canonical Header | KPI Pattern |
|--------------|------------------|-------------|
| Work Execution (Maintenance/*) | `_AssetMaintenanceHeader` | `quick-stats-row-4` cards |
| Item Master (Items/*) | `_ScreenHeader` | `quick-stats-row-4` cards |
| Asset Detail pages | `page-hero` alone | `page-hero-kpis` |

### Smoke Test

Test name: **"No Double Header"**

Scans all `Pages/**/*.cshtml` (excluding `Pages/Shared`) and fails if any page contains both a header partial/class AND a `page-hero` block.

## Layout Contract

### Modern Layout Structure

All pages use `_ModernLayout.cshtml`:

```
┌─────────────────────────────────────────────────────────┐
│ Header Bar (LAB indicator, breadcrumbs)                 │
├──────────┬──────────────────────────────────────────────┤
│          │                                              │
│ Sidebar  │  Main Content                                │
│          │                                              │
│ - Quick  │  ┌─────────────────────────────────────────┐ │
│   Actions│  │ Hero Section                            │ │
│          │  │ - Title, subtitle                       │ │
│ - Nav    │  │ - KPI cards                             │ │
│   Groups │  │ - Action buttons                        │ │
│          │  └─────────────────────────────────────────┘ │
│          │                                              │
│          │  ┌─────────────────────────────────────────┐ │
│          │  │ Content Sections                        │ │
│          │  │ - Data grids                            │ │
│          │  │ - Forms                                 │ │
│          │  │ - Detail cards                          │ │
│          │  └─────────────────────────────────────────┘ │
│          │                                              │
└──────────┴──────────────────────────────────────────────┘
```

### Required Layout Elements

| Element | Requirement | Smoke Test |
|---------|-------------|------------|
| Layout | Use `_ModernLayout` | UI-01 |
| Title | Set `ViewData["Title"]` | UI-02 |
| Breadcrumb | Via layout ViewData | UI-03 |

## Hero Section Contract

### Hero Structure

```html
<div class="hero-section">
    <div class="hero-content">
        <span class="hero-category">CATEGORY NAME</span>
        <span class="status-badge">STATUS</span>
        
        <h1 class="hero-title">Page Title</h1>
        <p class="hero-subtitle">Brief description</p>
        
        <div class="hero-stats">
            <!-- KPI badges -->
        </div>
    </div>
    
    <div class="hero-actions">
        <!-- Primary action buttons -->
    </div>
</div>
```

### Hero Elements

| Element | CSS Class | Purpose |
|---------|-----------|---------|
| Container | `.hero-section` | Gradient background |
| Category | `.hero-category` | Module identifier |
| Title | `.hero-title` | Page heading |
| Subtitle | `.hero-subtitle` | Description |
| Stats | `.hero-stats` | KPI indicators |
| Actions | `.hero-actions` | Primary buttons |

### KPI Card Pattern

```html
<div class="kpi-card">
    <div class="kpi-icon">📊</div>
    <div class="kpi-content">
        <span class="kpi-value">$87.9M</span>
        <span class="kpi-label">TOTAL COST</span>
    </div>
</div>
```

## Tabs

### Policy

**All tab navigation must use the unified `_TabNav` partial.** Custom tab markup is prohibited.

See [ADR-007](adr/ADR-007-Unified-Tab-System.md) for the full specification.

### Implementation Files

| Asset | Path | Purpose |
|-------|------|---------|
| Partial | `Pages/Shared/_TabNav.cshtml` | Unified markup generation |
| CSS | `wwwroot/css/modules/tabs.css` | Tab styling |
| JS | `wwwroot/js/tabs.js` | Tab behavior (button mode) |

All assets are loaded by `_ModernLayout.cshtml`. No per-page script blocks required.

### Usage

```razor
@await Html.PartialAsync("_TabNav", new ViewDataDictionary(ViewData) {
    { "TabId", "section-tabs" },
    { "Mode", "link" },  // or "button"
    { "AriaLabel", "Section navigation" },
    { "Tabs", new (string keyOrHref, string label, bool active, int? count, string icon)[] {
        ("overview", "Overview", activeTab == "overview", null, "<svg>...</svg>"),
        ("details", "Details", activeTab == "details", 5, null)
    }}
})
```

### Modes

| Mode | Element | Use Case |
|------|---------|----------|
| `link` | `<a href="?tab=...">` | Server-side switching, bookmarkable |
| `button` | `<button data-tab="...">` | Client-side JS switching |

### Class Contract

| Class | Purpose |
|-------|---------|
| `.tab-nav` | Container |
| `.tab-nav__item` | Tab element |
| `.tab-nav__item--active` | Active state |
| `.tab-nav__count` | Count badge |

### Prohibited

- Custom tab CSS classes (use `.tab-nav*` only)
- Inline `<style>` blocks for tabs
- Direct tab markup without partial

## Forms: Input Group Primitive

### Policy

**All input group patterns must use the standard `.input-group` structure.** This component provides consistent styling for inputs with leading/trailing addons (icons, currency symbols, buttons).

### CSS Location

`wwwroot/css/modules/forms.css` - loaded globally by `_ModernLayout.cshtml`

### When to Use

- Currency/money inputs with $ prefix
- Search fields with search icon
- Password fields with lock icon
- Any input requiring a visual prefix or suffix

### Required Markup Contract

**Pattern 1: Leading Icon/Text Addon**
```html
<div class="input-group">
    <span class="input-group-text">
        <svg>...</svg>  <!-- or text like "$" -->
    </span>
    <input type="text" class="form-control" placeholder="...">
</div>
```

**Pattern 2: Currency Prefix**
```html
<div class="input-group">
    <span class="input-prefix">$</span>
    <input type="number" class="form-control" step="0.01">
</div>
```

**Pattern 3: Trailing Button**
```html
<div class="input-group">
    <input type="text" class="form-control" placeholder="Search...">
    <button class="btn btn-primary">Go</button>
</div>
```

### Styling Rules

| Element | Token/Variable |
|---------|----------------|
| Addon background | `var(--color-surface-2)` |
| Addon text color | `var(--color-text-3)` |
| Border color | `var(--color-border-1)` |
| Focus border | `var(--color-focus)` |
| Border radius | `var(--radius-md)` |

### Prohibited

- Inline `style=` attributes on input groups
- Hard-coded hex colors in input group styling
- Custom input-group CSS classes without updating forms.css

## Auth/Login Styling Contract

### Policy

**Login page styling is isolated in `auth.css` and must NOT leak from shared form primitives.** The login page uses dedicated CSS classes (`.login-*`) that are explicitly defined and scoped to prevent collision with shared form components.

### CSS Location

`wwwroot/css/modules/auth.css` - loaded **per-page** via `@section Styles` in `Pages/Account/Login.cshtml` (NOT globally)

### Login Page Class Contract

| Class | Purpose |
|-------|---------|
| `.login-page-wrapper` | Full-page flex container, centers content |
| `.login-hero` | Card container with logo, form, demo accounts |
| `.login-hero-header` | Branded header with logo and title |
| `.login-hero-form` | Form content area |
| `.login-form-group` | Label + input container |
| `.login-form-label` | Uppercase styled label |
| `.login-form-control` | Styled input (text AND password) |
| `.login-btn` | Primary submit button |
| `.login-error` | Error message display |
| `.login-divider` | Section separator |
| `.demo-accounts-grid` | Demo account buttons container |
| `.demo-account-btn` | Individual demo login button |

### Why Login is Isolated

1. **Shared form primitives** (`.form-control`, `.input-group`) in `modern.css` and `forms.css` target specific input types or wrapper classes
2. **Password inputs** were NOT in the broad selector list (`input[type="text"]`, etc.)
3. **Login uses dedicated classes** to avoid accidental styling from shared components
4. **ADR-004 compliance**: No inline styles on login page

### Prohibited

- Using `.form-control` or `.input-group` on login page without explicit testing
- Adding inline `style=` attributes to login markup
- Modifying login-specific classes in any file other than `auth.css`
- Adding new login styles outside `auth.css`

### Regression Prevention

- All login classes are defined ONLY in `auth.css`
- Smoke tests verify login page contract (file exists, classes defined)
- UI conformance tests include login page structure validation

## Screen Headers

### Policy

**All pages must use the unified `_ScreenHeader` partial for page headers.** Custom `page-hero` markup is prohibited for new pages.

See [ADR-008](adr/ADR-008-Unified-Screen-Header-System.md) for the full specification.

### Work Orders Module Header Pattern

The Work Orders module (Maintenance/Index, WorkOrders/Details) uses `_AssetMaintenanceHeader` partial for module-level navigation with status display. This pattern supports:

- **Module pills:** Work Orders, PM Schedules, Work Requests navigation
- **Status display:** StatusText and StatusTone parameters for entity status
- **Breadcrumbs:** Navigation trail with return URL support
- **Actions bar:** Uses `.wo-actions-bar` class for filter/action layout

CSS classes for Work Orders are in `wwwroot/css/modules/workorders.css`:
- `.wo-actions-bar`, `.wo-actions-left`, `.wo-actions-right` - Actions bar layout
- `.recurring-failures-grid`, `.recurring-failures-card`, `.recurring-failures-table` - Failure analysis cards
- `.failure-code-badge`, `.failure-count` - Badge styling with `.danger`/`.warning` modifiers
- `.backlink-container` - Back link positioning
- `.form-inline` - Inline form elements
- `.cell-truncate`, `.cell-nowrap`, `.cell-right`, `.cell-bold`, `.cell-muted` - Table cell utilities

### Usage

```razor
@{
    var headerVd = new ViewDataDictionary(ViewData) {
        ["HeaderTitle"] = "Page Title",
        ["Subtitle"] = "Description text",
        ["TypeLabel"] = "Module Name",
        ["StatusText"] = "Active",
        ["StatusTone"] = "success",
        ["KpisPartial"] = "/Pages/Module/_KpisPartial.cshtml",
        ["ActionsPartial"] = "/Pages/Module/_ActionsPartial.cshtml"
    };
}
@await Html.PartialAsync("_ScreenHeader", headerVd)
```

> **Important:** Do NOT use `ViewData["Title"]` for `_ScreenHeader` - it conflicts with the page/layout title. Use `HeaderTitle` instead. Always use indexer syntax `["Key"] = value` instead of collection initializer `{ "Key", value }` to safely overwrite existing keys.

> **Required:** Pages using `_ScreenHeader` must also set `ViewData["HasScreenHeader"] = true;` in the page's `@{ }` block. This suppresses the layout's default title strip and ensures a single `<h1>` per page (accessibility requirement).

### ViewData Keys

| Key | Type | Required | Description |
|-----|------|----------|-------------|
| `HeaderTitle` | string | Yes | Page title (single `<h1>`). Falls back to `Title` if empty. |
| `Subtitle` | string | No | Description below title |
| `TypeLabel` | string | No | Module category label |
| `StatusText` | string | No | Status badge text |
| `StatusTone` | string | No | `info`\|`warning`\|`success`\|`danger`\|`muted` |
| `Breadcrumbs` | (Label, Href)[] | No | Breadcrumb trail |
| `KpisPartial` | string | No | Partial for KPI strip |
| `ActionsPartial` | string | No | Partial for action buttons |

### Class Contract

| Class | Purpose |
|-------|---------|
| `.screen-header` | Container |
| `.screen-header__title` | Page title (`<h1>`) |
| `.screen-header__subtitle` | Description |
| `.screen-header__meta` | Type + status row |
| `.screen-header__kpis` | KPI area |
| `.screen-header__actions` | Actions row |

### Status Tones

Use existing `.status-pill.tone-*` classes:

| Tone | Class | Use Case |
|------|-------|----------|
| Info | `.tone-info` | Default/neutral |
| Warning | `.tone-warning` | Pending/attention needed |
| Success | `.tone-success` | Active/complete |
| Danger | `.tone-danger` | Error/critical |
| Muted | `.tone-muted` | Inactive/disabled |

### Prohibited

- Direct `page-hero` markup (use `_ScreenHeader` partial)
- Inline `style=""` on status pills
- Custom status pill classes (use `status-pill tone-*`)
- Multiple `<h1>` elements per page
- `Html.Raw` for slot content (use partial references)

## No Inline Styles Rule

### Policy

**Inline `<style>` blocks are prohibited on operational pages.**

See [ADR-004](adr/ADR-004-UI-Hygiene-No-Inline-Styles.md) for rationale.

### Allowed Locations

| Location | Inline Styles | Reason |
|----------|---------------|--------|
| `wwwroot/css/` | N/A (CSS files) | Standard location |
| `_ModernLayout.cshtml` | Yes | Layout foundation |
| `_Layout.cshtml` | Yes | Legacy layout |
| Allowlist pages | Yes | Documented exceptions |

### Prohibited

```html
<!-- BAD: Inline style block in operational page -->
<style>
    .my-custom-class { color: red; }
</style>
```

### Correct Approach

1. Add styles to `wwwroot/css/site.css` or appropriate CSS file
2. Use existing component classes
3. Apply inline `style=""` attributes sparingly for one-offs

### Allowlist

See [UI-Conformance-Allowlist.md](UI-Conformance-Allowlist.md) for approved exceptions.

## Component Library

### Buttons

```html
<!-- Primary action -->
<button class="btn-primary">Save Asset</button>

<!-- Secondary action -->
<button class="btn-secondary">Cancel</button>

<!-- Danger action -->
<button class="btn-danger">Delete</button>

<!-- Icon button -->
<button class="btn-icon" title="Edit">
    <span class="icon">✏️</span>
</button>
```

### Status Badges

```html
<span class="status-badge success">Active</span>
<span class="status-badge warning">Pending</span>
<span class="status-badge danger">Overdue</span>
<span class="status-badge info">Draft</span>
```

### Section Cards

```html
<div class="section-card">
    <div class="section-card-header">
        <h3>Section Title</h3>
        <span class="status-badge info">COUNT</span>
    </div>
    <div class="section-card-body">
        <!-- Content -->
    </div>
</div>
```

### Forms

```html
<div class="form-group">
    <label asp-for="Model.Field" class="form-label"></label>
    <input asp-for="Model.Field" class="form-control" />
    <span asp-validation-for="Model.Field" class="text-danger"></span>
</div>
```

## Modal System

### Global Modal Contract

All modals use the global modal system:

```html
<!-- Trigger -->
<button onclick="showModal('modal-id')">Open Modal</button>

<!-- Modal (at page bottom) -->
<div id="modal-id" class="modal-overlay" style="display: none;">
    <div class="modal-content">
        <div class="modal-header">
            <h2>Modal Title</h2>
            <button onclick="hideModal('modal-id')">×</button>
        </div>
        <div class="modal-body">
            <!-- Content -->
        </div>
        <div class="modal-footer">
            <button onclick="hideModal('modal-id')">Cancel</button>
            <button class="btn-primary">Confirm</button>
        </div>
    </div>
</div>
```

### Modal Functions

```javascript
function showModal(id) {
    document.getElementById(id).style.display = 'flex';
}

function hideModal(id) {
    document.getElementById(id).style.display = 'none';
}
```

## Data Tables

See [DataGridPremium.md](DataGridPremium.md) for complete DataGrid contract.

### Basic Table

```html
<table class="data-table">
    <thead>
        <tr>
            <th>Column 1</th>
            <th>Column 2</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>Value 1</td>
            <td>Value 2</td>
        </tr>
    </tbody>
</table>
```

### Enhanced Grid

```html
<table class="data-table" 
       id="gridId" 
       data-enhanced-grid="true" 
       data-row-click="true">
```

## Color Palette

### Primary Colors

| Name | Hex | Usage |
|------|-----|-------|
| Primary Blue | `#1e40af` | Buttons, links |
| Primary Light | `#3b82f6` | Hover states |
| Primary Dark | `#1e3a8a` | Active states |

### Status Colors

| Status | Hex | Usage |
|--------|-----|-------|
| Success | `#22c55e` | Active, completed |
| Warning | `#f59e0b` | Pending, attention |
| Danger | `#ef4444` | Errors, overdue |
| Info | `#3b82f6` | Informational |

### Neutral Colors

| Name | Hex | Usage |
|------|-----|-------|
| Gray 900 | `#111827` | Sidebar background |
| Gray 700 | `#374151` | Body text |
| Gray 100 | `#f3f4f6` | Page background |
| White | `#ffffff` | Cards, content |

## Typography

### Font Stack

```css
font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
```

### Font Sizes

| Element | Size | Weight |
|---------|------|--------|
| H1 (Page title) | 24px | 600 |
| H2 (Section) | 18px | 600 |
| H3 (Card header) | 16px | 600 |
| Body | 14px | 400 |
| Small | 12px | 400 |

## Responsive Design

### Breakpoints

| Name | Width | Usage |
|------|-------|-------|
| Mobile | < 640px | Stack columns |
| Tablet | 640-1024px | Collapse sidebar |
| Desktop | > 1024px | Full layout |

### Sidebar Behavior

- Desktop: Always visible
- Tablet: Collapsible toggle
- Mobile: Off-canvas drawer

## Accessibility

### Requirements

| Requirement | Implementation |
|-------------|----------------|
| Color contrast | WCAG AA minimum |
| Focus indicators | Visible focus rings |
| Form labels | Associated with inputs |
| Alt text | On informational images |

### Focus States

```css
:focus-visible {
    outline: 2px solid var(--color-primary);
    outline-offset: 2px;
}
```

## Related Documents

- [DataGridPremium.md](DataGridPremium.md) - Grid standards
- [NavigationAndRouting.md](NavigationAndRouting.md) - Navigation rules
- [BrandGuardrails.md](BrandGuardrails.md) - Brand guidelines
- [adr/ADR-004-UI-Hygiene-No-Inline-Styles.md](adr/ADR-004-UI-Hygiene-No-Inline-Styles.md) - Style ADR
