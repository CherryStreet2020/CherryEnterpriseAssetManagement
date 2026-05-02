# Navigation Map — After (Target State)

This document describes the final sidebar IA after the Modern Navigation Overhaul.

## Design Principles

- Only menu items with existing backing pages are shown
- Every link carries a `data-nav-route` attribute for automated gate crawling
- Sidebar supports collapse toggle with icon-rail mode
- "Locations" = functional locations; "Warehouses" = stocking points (never "Storerooms")
- No Reliability group (no pages exist yet)
- No Job Plans, Meters, Condition & Health, or Hierarchy items (pages do not exist)
- Breadcrumbs + back-to-results on all detail pages

## Sidebar Structure (Actual — Only Existing Pages)

### Overview (Top-Level)

| Label | Route | Page File |
|---|---|---|
| Overview | `/` | `Pages/Index.cshtml` |

### Work (conditional: enableWorkOrders)

| Label | Route | Page File |
|---|---|---|
| Requests | `/Maintenance/WorkRequests` | `Pages/Maintenance/WorkRequests/Index.cshtml` |
| Work Orders | `/Maintenance` | `Pages/Maintenance/Index.cshtml` |
| Planning & Scheduling | `/Maintenance/Schedules` | `Pages/Maintenance/Schedules.cshtml` |
| PM Program | `/Admin/PMTemplates` | `Pages/Admin/PMTemplates.cshtml` |

### Assets

| Label | Route | Page File |
|---|---|---|
| Asset Registry | `/Assets` | `Pages/Assets/Index.cshtml` |
| Locations | `/Admin/Locations` | `Pages/Admin/Locations.cshtml` |

### Materials (conditional: enableInventory/enablePurchasing/enableAP/enableVendors)

| Label | Route | Page File | Condition |
|---|---|---|---|
| Inventory | `/Materials/Items` | `Pages/Materials/Items.cshtml` | enableInventory |
| Warehouses | `/Inventory` | `Pages/Inventory/Index.cshtml` | enableInventory |
| Vendors | `/Admin/Vendors` | `Pages/Admin/Vendors.cshtml` | enableVendors |
| Purchase Orders | `/Purchasing` | `Pages/Purchasing/Index.cshtml` | enablePurchasing |
| Receipts | `/Receiving` | `Pages/Receiving/Index.cshtml` | enablePurchasing |
| Invoices | `/AccountsPayable` | `Pages/AccountsPayable/Index.cshtml` | enableAP |

### Finance

| Label | Route | Page File |
|---|---|---|
| CIP Projects | `/CIP` | `Pages/CIP/Index.cshtml` |
| Capitalizations | `/CIP/Costs` | `Pages/CIP/Costs.cshtml` |
| Journals | `/Journals` | `Pages/Journals/Index.cshtml` |
| Cost Analytics | `/CIP/PartyDrilldown` | `Pages/CIP/PartyDrilldown.cshtml` |
| Depreciation Books | `/Books` | `Pages/Books/Index.cshtml` |
| Reports | `/Reports/ReportHub` | `Pages/Reports/ReportHub.cshtml` |

### Admin (admin role only)

| Label | Route | Page File |
|---|---|---|
| Organization & Sites | `/Admin/Sites` | `Pages/Admin/Sites.cshtml` |
| Users & Roles | `/Admin/Users` | `Pages/Admin/Users.cshtml` |
| Lookups | `/Admin/Lookups` | `Pages/Admin/Lookups/Index.cshtml` |
| Integrations | `/Admin/Integrations` | `Pages/Admin/Integrations/Index.cshtml` |
| Audit Log | `/Admin/AuditLog` | `Pages/Admin/AuditLog.cshtml` |
| System Settings | `/Admin/SystemSettings` | `Pages/Admin/SystemSettings.cshtml` |

### Sidebar Footer

| Label | Route | Page File |
|---|---|---|
| Help Center | `/Help` | `Pages/Help/Index.cshtml` |

## Items NOT Shown (No Backing Page Exists)

| Spec Item | Why Hidden |
|---|---|
| Job Plans | No `Pages/**/JobPlan*` file exists |
| Meters & Readings | No `Pages/**/Meter*` file exists |
| Condition & Health | No `Pages/**/Condition*` or `Health*` file exists |
| Asset Hierarchy / Genealogy | No `Pages/**/Hierarchy*` file exists |
| Reliability (entire section) | No Failures/RCA, FMEA, RCM, or PM Optimization pages exist |

## Detail Pages with Breadcrumbs

Every detail page includes breadcrumb trail and back-to-results button:

| Detail Page | Breadcrumb Path | Back Target |
|---|---|---|
| `/Maintenance/Details/{id}` | Work > Work Orders > WO-{number} | `/Maintenance` |
| `/Maintenance/WorkRequests/Details/{id}` | Work > Work Requests > WR-{number} | `/Maintenance/WorkRequests` |
| `/Assets/Asset/{id}` | Assets > Asset Registry > {tag} | `/Assets` |
| `/Materials/ItemEdit/{id}` | Materials > Item Master > {partNo} | `/Materials/Items` |
| `/CIP/Details/{id}` | Finance > CIP Projects > {code} | `/CIP` |
| `/Purchasing/Details/{id}` | Materials > Purchase Orders > PO-{number} | `/Purchasing` |
| `/AccountsPayable/Details/{id}` | Materials > Invoices > INV-{number} | `/AccountsPayable` |
| `/Books/Details/{id}` | Finance > Depreciation Books > {name} | `/Books` |
| `/Journals/Details/{id}` | Finance > Journal Entries > JE-{number} | `/Journals` |

## Shell Features

- **Sidebar collapse**: Toggle button in sidebar brand; collapsed = 64px icon-rail
- **Command palette**: Ctrl+K opens modal with fuzzy search over all canonical routes
- **Global search**: "/" focuses search input in top bar; Esc blurs
- **Recent nav**: localStorage-backed recent page list (feature-flagged via `FEATURE_RECENT_NAV`)
- **Org selector**: Company/site scope dropdown at top of sidebar
