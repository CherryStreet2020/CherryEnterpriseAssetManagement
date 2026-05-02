# ADR-005: DataGrid Contract with Premium Controls and Server-Generated Row Navigation

**Status:** Accepted  
**Date:** 2026-01-24  
**Deciders:** Development Team  
**Categories:** UX, Architecture

---

## Context

CherryAI EAM uses data tables extensively for listing assets, work orders, items, and schedules. Issues arose with:

1. **Inconsistent grid behavior** across pages (some had filtering, others didn't)
2. **Broken row navigation** when pages used route segments (`@page "{id:int}"`)
3. **No standardized controls** for search, filter, export, column visibility
4. **State not persisted** between page visits

The critical bug: Client-side URL construction assumed query string parameters (`?id=123`), but some pages used route segments (`/123`), causing 404 errors.

## Decision

**Implement DataGrid Premium Controls v3.0 with server-generated row navigation via `data-row-href`.**

Specifically:
1. All data grids use `data-enhanced-grid="true"` for premium controls
2. Row navigation uses `data-row-href` with URLs from `Url.Page()`
3. Premium toolbar includes: search, column filters, column visibility, export, reset
4. Grid state persists to localStorage
5. Legacy `data-row-click-page` attributes are deprecated but supported

## Alternatives Considered

### Alternative 1: Client-Side URL Building
- **Description:** JavaScript builds URLs from `data-row-id` + page patterns
- **Pros:** Simple markup
- **Cons:** Can't handle route segments vs query strings
- **Why rejected:** Caused 404 errors on route-segment pages

### Alternative 2: Third-Party Grid Library
- **Description:** Use AG Grid, DataTables, or similar
- **Pros:** Feature-rich out of box
- **Cons:** Bundle size, learning curve, less control
- **Why rejected:** Overkill for current needs

### Alternative 3: Server-Side Rendering Only
- **Description:** All filtering/sorting via postback
- **Pros:** Works without JavaScript
- **Cons:** Poor UX, slow, requires full page reloads
- **Why rejected:** Bad user experience

## Consequences

### Positive
- Consistent grid experience across all list pages
- Row navigation works regardless of routing pattern
- Users can search, filter, sort, export without page reloads
- State persists between visits
- Automated smoke test enforcement

### Negative
- Slightly more markup per row (`data-row-href` attribute)
- JavaScript required for enhanced features
- Migration needed for existing grids

### Neutral
- Legacy attributes still work (graceful fallback)
- CSS for sort badges required

## Implementation Notes

### Table Markup

```html
<table class="data-table" 
       id="gridId" 
       data-enhanced-grid="true" 
       data-row-click="true">
    <thead>
        <tr>
            <th data-col="Name" data-filter="text">Name</th>
            <th data-col="Status" data-filter="select">Status</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var item in Model.Items)
        {
            <tr data-row-id="@item.Id" 
                data-row-href="@Url.Page("/Details", new { id = item.Id, returnUrl = returnUrl })">
                <td>@item.Name</td>
                <td>@item.Status</td>
            </tr>
        }
    </tbody>
</table>
```

### Navigation Priority

```javascript
// enhanced-grid.js
function handleRowClick(row) {
    // Priority 1: Server-generated URL
    const href = row.dataset.rowHref;
    if (href) {
        window.location.href = href;
        return;
    }
    
    // Priority 2: Legacy client-side URL building (deprecated)
    const id = row.dataset.rowId;
    const page = table.dataset.rowClickPage;
    if (id && page) {
        window.location.href = `${page}?id=${id}`;
    }
}
```

### Premium Controls

| Control | Purpose |
|---------|---------|
| Global Search | Filter all columns |
| Column Filters | Per-column filtering |
| Column Visibility | Show/hide columns |
| Export | CSV/Excel download |
| Reset | Clear all filters |

## Related Documents

- [DataGridPremium.md](../DataGridPremium.md) - Full documentation
- [UXStandards.md](../UXStandards.md) - UX standards
- [NavigationAndRouting.md](../NavigationAndRouting.md) - Navigation rules

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-01-24 | Development Team | Initial version |
