# Navigation Map — Before (Current State)

This document captures the sidebar navigation structure as it exists prior to the Modern Navigation Overhaul.

## Sidebar Structure

The layout is defined in `Pages/Shared/_ModernLayout.cshtml` with CSS in `wwwroot/css/sidebar-nav.css`.

### Top-Level Quick Actions (Always Visible)

| Label | Route | Notes |
|---|---|---|
| Dashboard | `/` (Index) | Home page |
| Work Orders (Cockpit) | `/Maintenance` | Duplicated inside Work Execution group |
| Work Requests | `/Maintenance/WorkRequests` | Duplicated inside Work Execution group |
| PM Schedules | `/Maintenance/Schedules` | Duplicated inside Work Execution group |
| Capital Projects (CIP) | `/CIP` | Duplicated inside Finance group |

### Accordion Groups

#### 1. Assets (data-section="assets")
Expands when route starts with `/Assets` or `/Inventory`.

| Label | Route |
|---|---|
| Asset Register | `/Assets` |
| Physical Inventory | `/Inventory` |
| Transfer Asset | `/Assets?action=transfer` |
| Dispose Asset | `/Assets?action=dispose` |
| Improve Asset | `/Assets?action=improve` |
| Bulk Operations | `/BulkOperations` |

#### 2. Work Execution (data-section="operations")
Conditional: shown only when `enableWorkOrders == true`.

| Label | Route |
|---|---|
| Work Orders (Cockpit) | `/Maintenance` |
| Work Requests | `/Maintenance/WorkRequests` |
| Create Work Request | `/Maintenance/WorkRequests/Create` |
| PM Schedules | `/Maintenance/Schedules` |
| PM Assignments | `/Maintenance/Assignments` |

#### 3. Inventory & Stores (data-section="inventory")
Conditional: shown only when `enableInventory == true`.

| Label | Route |
|---|---|
| Item Master (Parts Catalog) | `/Materials/ItemEdit` |
| Storerooms | `/Admin/Storerooms` |
| Stock Levels | `/Admin/StockLevels` |
| Stock Transactions | `/Admin/Inventory` |
| Item Categories | `/Admin/ItemCategories` |
| Kits & Assemblies | `/Admin/Kits` |
| Barcode Labels | `/Admin/Barcodes` |

#### 4. Purchasing (data-section="procurement")
Conditional: shown when any of `enablePurchasing`, `enableAP`, or `enableVendors` is true.

| Label | Route |
|---|---|
| Vendors | `/Admin/Vendors` |
| Purchase Requisitions | `/Admin/Requisitions` |
| Purchase Orders | `/Purchasing` |
| Receiving | `/Receiving` |
| Accounts Payable | `/AccountsPayable` |

#### 5. Finance (data-section="financial")
Always visible. Expands when route starts with `/Books`, `/Journals`, `/UsTax`, `/CCA`, or `/CIP`.

| Label | Route |
|---|---|
| Depreciation Books | `/Books` |
| Depreciation Reports | `/Reports/ReportHub` |
| Journal Entries | `/Journals` |
| US Tax (MACRS/179) | `/UsTax` |
| Canadian CCA | `/CCA` |
| Capital Projects (CIP) | `/CIP` |
| CIP Cost Analysis | `/CIP/Costs` |

#### 6. Reports (data-section="reports")
Always visible.

| Label | Route |
|---|---|
| Report Center | `/Reports/ReportHub` |
| Report Builder | `/Reports/Builder` |
| Compliance Reports | `/Reports/Compliance` |
| Report Export | `/Reports/Export` |
| Form 4562 (US) | `/Reports/Form4562` |
| T2 Schedule 8 (CA) | `/Reports/T2Schedule8` |

#### 7. Setup (Masters) (data-section="setup")
Always visible.

| Label | Route |
|---|---|
| Sites | `/Admin/Sites` |
| Locations | `/Admin/Locations` |
| Departments | `/Admin/Departments` |
| Cost Centers | `/Admin/CostCenters` |
| Asset Categories | `/Admin/AssetCategories` |
| Chart of Accounts | `/Admin/GlAccounts` |
| Manufacturers | `/Admin/Manufacturers` |
| Technicians | `/Admin/Technicians` |
| Project Managers | `/Admin/ProjectManagers` |
| Exchange Rates | `/Admin/ExchangeRates` |

#### 8. Administration & Tools (data-section="admin")
Conditional: shown only when user has Admin role.

| Label | Route |
|---|---|
| Admin Hub | `/Admin` |
| Company Settings | `/Admin/Company` |
| Users & Roles | `/Admin/Users` |
| System Settings | `/Admin/SystemSettings` |
| Lookup Tables | `/Admin/Lookups` |
| PM Templates | `/Admin/PMTemplates` |
| Approvals | `/Admin/Approvals` |
| Audit Log | `/Admin/AuditLog` |
| Environment Status | `/Admin/EnvironmentStatus` |
| Smoke Tests | `/Admin/SmokeTests` |
| Demo Data | `/Admin/DemoData` |
| Master Data Import | `/Admin/DataImport` |
| File Export | `/Admin/Export` |
| AI Assistant | `/AI` |
| API Integration | `/API` |

### Sidebar Footer

| Label | Route | Notes |
|---|---|---|
| Help Center | `/Help` | Always visible |
| User Name | (no link) | Shown when authenticated |
| Sign Out | `/Account/Logout` | Shown when authenticated |
| Sign In | `/Account/Login` | Shown when not authenticated |

## Known Issues

1. **Duplicate links**: Dashboard quick-actions duplicate links that also appear in accordion groups (Work Orders, Work Requests, PM Schedules, CIP).
2. **"Storerooms" label**: Inventory group uses "Storerooms" instead of the preferred "Warehouses" terminology.
3. **Mixed route prefixes**: Inventory and Purchasing items route to `/Admin/*` paths, breaking the conceptual grouping.
4. **No collapse toggle**: The sidebar has no collapse/expand button or icon-rail mode.
5. **No `data-nav-route` attributes**: Links lack machine-readable route markers for automated gate crawling.
6. **No command palette or global search**: No Ctrl+K palette or "/" search shortcut exists.
7. **No breadcrumbs on detail pages**: Detail pages lack consistent breadcrumb + back-to-results navigation.
8. **Depreciation Reports links to ReportHub**: The Finance group "Depreciation Reports" link points to `/Reports/ReportHub`, same as "Report Center" in Reports group.
