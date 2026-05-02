# Premium DataGrid Controls v3.0
Last updated: 2026-01-24


## Overview
The Premium DataGrid Controls system provides advanced filtering, sorting, export, and navigation capabilities for all list pages in CherryAI EAM. This document describes the strict DataGrid Contract that all list pages must follow.

## DataGrid Contract v3.0

### Table Attributes
Tables must have the following attributes:

| Attribute | Required | Description |
|-----------|----------|-------------|
| `id` | Yes | Stable, unique ID for the table |
| `data-enhanced-grid="true"` | Yes | Enables premium grid features |
| `data-row-click="true"` | Optional | Enables row click navigation |

**DEPRECATED (Legacy Fallback Only):**
| Attribute | Description |
|-----------|-------------|
| `data-row-click-page` | Legacy: Target page path (prefer `data-row-href` on rows) |
| `data-row-click-param` | Legacy: Query param name (prefer `data-row-href` on rows) |

Example (v3.0 Recommended):
```html
<table id="assetGrid" 
       class="data-table" 
       data-enhanced-grid="true" 
       data-row-click="true">
```

### Column (Header) Attributes
Each `<th>` element should have:

| Attribute | Required | Description |
|-----------|----------|-------------|
| `data-col` | Yes | Column display name for filter/column toggles |
| `data-filter` | Yes | Filter type: `text`, `select`, `number`, `date`, `none` |
| `data-filter-options` | For select | Pipe-separated options (e.g., `Active|Inactive`) |
| `data-export="false"` | Optional | Exclude column from exports |

Example:
```html
<th data-col="Status" data-filter="select" data-filter-options="Active|Inactive">Status</th>
<th data-col="Actions" data-filter="none" data-export="false">Actions</th>
```

### Row Attributes (v3.0 Routing-Safe Navigation)
Each `<tr>` in the body should have:

| Attribute | Required | Description |
|-----------|----------|-------------|
| `data-row-id` | Yes | The entity ID for the row |
| `data-row-href` | **Recommended** | Server-generated URL via `Url.Page()` for routing-safe navigation |

**Why data-row-href?**
- Razor Pages can use `@page "{id:int}"` (route segment) or `@page` (query string)
- Building URLs client-side can break for route-segment pages (e.g., `/Details/199` vs `/Details?id=199`)
- `Url.Page()` always generates the correct URL for the page's routing configuration

Example (v3.0 Recommended):
```razor
@{ var returnUrl = $"{HttpContext.Request.Path}{HttpContext.Request.QueryString}"; }
...
<tr data-row-id="@asset.Id" data-row-href="@Url.Page("/Assets/Asset", new { id = asset.Id, returnUrl = returnUrl })">
```

**Navigation Priority:**
1. If `data-row-href` exists → navigate directly to that URL
2. Else if `data-row-id` + legacy table attributes exist → build URL client-side (deprecated)

### Preventing Navigation on Interactive Elements
Add `data-no-row-nav` to elements that should not trigger row navigation:

```html
<button data-no-row-nav onclick="deleteItem(id)">Delete</button>
```

The system automatically prevents navigation on: `a`, `button`, `input`, `select`, `textarea`.

### No View/Edit Buttons in Grids
**Rule:** Click row opens record; no View/Edit action buttons in grids.

Premium DataGrid v3.0 standardizes row-click navigation:
- Clicking anywhere on a row navigates to the detail/edit page
- Action columns with View/Edit buttons are **removed** from grid layouts
- Delete buttons may remain but require `data-no-row-nav` attribute
- Modal-based pages (Vendors, Manufacturers) are exceptions that use onclick handlers

### Interactive Element Suppression
Use `data-no-row-nav` attribute on any element that should NOT trigger row navigation:

```html
<button data-no-row-nav class="btn-delete" onclick="confirmDelete(@id)">Delete</button>
<a data-no-row-nav href="/special/action">Special Action</a>
```

The system automatically suppresses navigation on: `a`, `button`, `input`, `select`, `textarea`.

## Features

### 1. Premium Toolbar
The enhanced grid automatically injects a premium toolbar with:
- **Search**: Full-text search across all visible columns
- **Filters**: Column-specific filters with operations (contains, equals, starts-with)
- **Columns**: Toggle column visibility
- **Export**: CSV and Excel export
- **Reset**: Clear all filters and restore defaults

### 2. Multi-Column Sort
- **Single click**: Sort by column (toggles asc/desc)
- **Shift+Click**: Add column to multi-sort (max 3 columns)
- Sort order badges show sequence (1, 2, 3)

### 3. Filter Operations
Text filters support multiple operations via dropdown:
- **contains** (default): Substring match
- **equals**: Exact match
- **starts-with**: Prefix match

### 4. State Persistence
Grid state is automatically saved to localStorage per page+table:
- Sort columns and directions
- Active filters
- Column visibility
- Search term

State persists across page refreshes and browser sessions.

### 5. Row Click Navigation
When `data-row-click="true"` is set:
- Clicking a row navigates to the detail page
- Automatically appends `returnUrl` query param for back navigation
- Interactive elements (buttons, links) do not trigger navigation

## Core List Pages

| Page | Table ID | Row Click Target |
|------|----------|------------------|
| Assets (Index) | `assetGrid` | `/Assets/Asset` |
| Items | `itemGrid` | `/Materials/ItemEdit` |
| Work Orders | `maintenanceGrid` | `/Maintenance/Details` |
| PM Schedules | `pmSchedulesGrid` | `/Admin/PMScheduleEdit` |
| Vendors | `vendorGrid` | Modal (no row click) |

## CSS Classes

Premium grid styles are defined in `wwwroot/css/premium-components.css`:

- `.grid-toolbar-premium`: Main toolbar container
- `.grid-search-input`: Search input field
- `.grid-filter-dropdown`: Filter panel dropdown
- `.grid-column-dropdown`: Column visibility dropdown
- `.grid-export-dropdown`: Export options dropdown
- `.sort-badge`: Multi-sort sequence indicator

## Smoke Tests

Test 61 (`DataGrid Premium Controls v3.0`) verifies:
1. `enhanced-grid.js` contains all required features
2. Key pages have `data-enhanced-grid="true"` attribute
3. Row click pages have proper `data-row-click` attributes
4. Row elements have `data-row-id` attributes
5. CSS contains required premium grid styles

## Migration Guide

To upgrade an existing list page to Premium DataGrid v3.0:

1. Add `data-enhanced-grid="true"` to the table
2. Add `data-col` and `data-filter` to all `<th>` elements
3. For row navigation, add:
   - `data-row-click="true"` to table
   - `data-row-id="@entity.Id"` to each `<tr>`
   - **REQUIRED:** `data-row-href="@Url.Page(...)"` to each `<tr>` (routing-safe)
4. Remove inline `onclick` handlers from rows
5. **Remove Actions columns** with View/Edit buttons
6. Add `data-no-row-nav` to any remaining action buttons/links
7. Add returnUrl for back navigation:
   ```razor
   @{ var returnUrl = $"{HttpContext.Request.Path}{HttpContext.Request.QueryString}"; }
   <tr data-row-id="@item.Id" 
       data-row-href="@Url.Page("/Path/Edit", new { id = item.Id, returnUrl = returnUrl })">
   ```

**DEPRECATED:** `data-row-click-page` and `data-row-click-param` are legacy fallbacks. All new pages must use `data-row-href` with `Url.Page()`.

## Related Documents

- [README.md](README.md) - Documentation index
- [UIConformance.md](UIConformance.md) - UI conformance and standards
- [NavigationAndRouting.md](NavigationAndRouting.md) - Navigation patterns and routing
