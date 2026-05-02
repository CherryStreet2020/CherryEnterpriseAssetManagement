# Return Path Standard

This document defines the breadcrumb and back-to-results navigation pattern used on all detail pages in CherryAI EAM.

## Pattern

Every detail page implements two navigation aids:

1. **Breadcrumb trail**: Shows the hierarchical path from the IA group to the current entity
2. **Back-to-results button**: Returns the user to the list page they came from, preserving filters and scroll position

## Breadcrumb Format

```
{Group} > {List Page Label} > {Entity Identifier}
```

Examples:
- `Work > Work Orders > WO-2025-0042`
- `Assets > Asset Registry > PUMP-001`
- `Finance > CIP Projects > CIP-HQ-2025`
- `Materials > Item Master > P/N 7842-A`

## Back-to-Results Behavior

### Storage Mechanism

When a user visits a list page, JavaScript stores the current URL (including query parameters for filters, sort, and page) in `sessionStorage`:

```
Key:   cherryai_returnUrl_{module}_{orgScope}
Value: /Maintenance?status=Open&sort=priority&page=2
```

- `{module}`: Canonical module name (e.g., `work-orders`, `assets`, `cip-projects`)
- `{orgScope}`: Current organization scope ID (supports multi-org switching)

### Resolution Order

When a detail page renders, the return URL is resolved in this order:

1. `?returnUrl=` query parameter (explicit, highest priority)
2. `sessionStorage` key for the module + org scope
3. Hardcoded fallback to the module's list page root (e.g., `/Maintenance`)

### Implementation

Detail pages include a shared partial or JS helper:

```html
<nav class="breadcrumb-nav" aria-label="Breadcrumb">
  <ol class="breadcrumb-list">
    <li><a href="/">Dashboard</a></li>
    <li><a href="/Maintenance">Work Orders</a></li>
    <li class="breadcrumb-current">WO-2025-0042</li>
  </ol>
</nav>
<a href="@returnUrl" class="btn-back-to-results">
  <i class="fas fa-arrow-left"></i> Back to Results
</a>
```

The `returnUrl` variable is set in the PageModel or via JavaScript:

```javascript
function resolveReturnUrl(module, fallback) {
  const params = new URLSearchParams(window.location.search);
  const explicit = params.get('returnUrl');
  if (explicit) return explicit;

  const orgScope = sessionStorage.getItem('cherryai_org_scope') || 'default';
  const stored = sessionStorage.getItem(`cherryai_returnUrl_${module}_${orgScope}`);
  if (stored) return stored;

  return fallback;
}
```

## Detail Pages Covered

| Detail Page | Module Key | Fallback URL |
|---|---|---|
| `/Maintenance/Details/{id}` | `work-orders` | `/Maintenance` |
| `/Maintenance/WorkRequests/Details/{id}` | `work-requests` | `/Maintenance/WorkRequests` |
| `/Assets/Asset/{id}` | `assets` | `/Assets` |
| `/Materials/ItemEdit/{id}` | `item-master` | `/Materials/ItemEdit` |
| `/CIP/Details/{id}` | `cip-projects` | `/CIP` |
| `/Purchasing/Details/{id}` | `purchase-orders` | `/Purchasing` |
| `/AccountsPayable/Details/{id}` | `accounts-payable` | `/AccountsPayable` |
| `/Books/Details/{id}` | `depreciation-books` | `/Books` |
| `/Journals/Details/{id}` | `journal-entries` | `/Journals` |
| `/BulkOperations/Details/{id}` | `bulk-operations` | `/BulkOperations` |

## List Page Storage

Each list page includes a small script block to store its URL on load:

```javascript
document.addEventListener('DOMContentLoaded', function() {
  const module = document.querySelector('[data-return-module]')?.dataset.returnModule;
  if (!module) return;
  const orgScope = sessionStorage.getItem('cherryai_org_scope') || 'default';
  sessionStorage.setItem(
    `cherryai_returnUrl_${module}_${orgScope}`,
    window.location.pathname + window.location.search
  );
});
```

List pages declare their module via a data attribute:

```html
<div data-return-module="work-orders">
  <!-- list content -->
</div>
```

## Edge Cases

| Scenario | Behavior |
|---|---|
| User navigates directly to detail page (no referrer) | Falls back to module list root |
| User switches org scope mid-session | New scope gets its own storage key; old scope URLs are not cleared |
| Detail page opened in new tab | Query param `returnUrl` works; sessionStorage does not cross tabs |
| Org selector changes while on detail page | Back button uses stored URL for previous scope (acceptable; user can re-navigate) |
