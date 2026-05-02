# Route Canonicalization Map

This document maps old/current routes to their canonical form after the navigation overhaul.

## Principles

1. Routes should reflect the IA group, not the implementation folder
2. Detail pages use `/{Module}/Details/{id}` pattern
3. Query-string actions (`?action=transfer`) are acceptable for modal triggers on list pages
4. Admin-prefixed routes for master data remain unchanged (no URL-breaking changes)

## Route Mappings

### Work

| Old Route | Canonical Route | Label | Change |
|---|---|---|---|
| `/Maintenance` | `/Maintenance` | Work Orders | No change |
| `/Maintenance/Details/{id}` | `/Maintenance/Details/{id}` | Work Order Details | No change |
| `/Maintenance/WorkRequests` | `/Maintenance/WorkRequests` | Work Requests | No change |
| `/Maintenance/WorkRequests/Create` | `/Maintenance/WorkRequests/Create` | Create Work Request | No change |
| `/Maintenance/WorkRequests/Details/{id}` | `/Maintenance/WorkRequests/Details/{id}` | Work Request Details | No change |
| `/Maintenance/Schedules` | `/Maintenance/Schedules` | PM Schedules | No change |
| `/Maintenance/Assignments` | `/Maintenance/Assignments` | PM Assignments | No change |

### Assets

| Old Route | Canonical Route | Label | Change |
|---|---|---|---|
| `/Assets` | `/Assets` | Asset Registry | No change |
| `/Assets/Asset/{id}` | `/Assets/Asset/{id}` | Asset Details | No change |
| `/Assets?action=transfer` | `/Assets?action=transfer` | Transfer Asset | No change |
| `/Assets?action=dispose` | `/Assets?action=dispose` | Dispose Asset | No change |
| `/Assets?action=improve` | `/Assets?action=improve` | Improve Asset | No change |
| `/Inventory` | `/Inventory` | Physical Inventory | No change |
| `/BulkOperations` | `/BulkOperations` | Bulk Operations | No change |

### Materials & Procurement

| Old Route | Canonical Route | Label | Change |
|---|---|---|---|
| `/Materials/ItemEdit` | `/Materials/ItemEdit` | Item Master | No change |
| `/Materials/ItemEdit/{id}` | `/Materials/ItemEdit/{id}` | Item Details | No change |
| `/Admin/Storerooms` | `/Admin/Storerooms` | Warehouses | Label change only ("Storerooms" -> "Warehouses") |
| `/Admin/StockLevels` | `/Admin/StockLevels` | Stock Levels | No change |
| `/Admin/Inventory` | `/Admin/Inventory` | Stock Transactions | No change |
| `/Admin/ItemCategories` | `/Admin/ItemCategories` | Item Categories | No change |
| `/Admin/Kits` | `/Admin/Kits` | Kits & Assemblies | No change |
| `/Admin/Barcodes` | `/Admin/Barcodes` | Barcode Labels | No change |
| `/Admin/Vendors` | `/Admin/Vendors` | Vendors | No change |
| `/Admin/Requisitions` | `/Admin/Requisitions` | Purchase Requisitions | No change |
| `/Purchasing` | `/Purchasing` | Purchase Orders | No change |
| `/Purchasing/Details/{id}` | `/Purchasing/Details/{id}` | PO Details | No change |
| `/Receiving` | `/Receiving` | Receipts | Label change ("Receiving" -> "Receipts") |
| `/AccountsPayable` | `/AccountsPayable` | Invoices / AP | No change |
| `/AccountsPayable/Details/{id}` | `/AccountsPayable/Details/{id}` | Invoice Details | No change |

### Finance

| Old Route | Canonical Route | Label | Change |
|---|---|---|---|
| `/CIP` | `/CIP` | CIP Projects | No change |
| `/CIP/Details/{id}` | `/CIP/Details/{id}` | CIP Project Details | No change |
| `/CIP/Costs` | `/CIP/Costs` | CIP Cost Analysis | No change |
| `/Books` | `/Books` | Depreciation Books | No change |
| `/Books/Details/{id}` | `/Books/Details/{id}` | Book Details | No change |
| `/Journals` | `/Journals` | Journal Entries | No change |
| `/Journals/Details/{id}` | `/Journals/Details/{id}` | Journal Details | No change |
| `/UsTax` | `/UsTax` | US Tax (MACRS/179) | No change |
| `/CCA` | `/CCA` | Canadian CCA | No change |

### Reports

| Old Route | Canonical Route | Label | Change |
|---|---|---|---|
| `/Reports/ReportHub` | `/Reports/ReportHub` | Report Center | No change |
| `/Reports/Builder` | `/Reports/Builder` | Report Builder | No change |
| `/Reports/Compliance` | `/Reports/Compliance` | Compliance Reports | No change |
| `/Reports/Export` | `/Reports/Export` | Report Export | No change |
| `/Reports/Form4562` | `/Reports/Form4562` | Form 4562 (US) | No change |
| `/Reports/T2Schedule8` | `/Reports/T2Schedule8` | T2 Schedule 8 (CA) | No change |

### Admin

| Old Route | Canonical Route | Label | Change |
|---|---|---|---|
| `/Admin` | `/Admin` | Admin Hub | No change |
| `/Admin/Sites` | `/Admin/Sites` | Organization & Sites | Label change |
| `/Admin/Company` | `/Admin/Company` | Company Settings | No change |
| `/Admin/Users` | `/Admin/Users` | Users & Roles | No change |
| `/Admin/SystemSettings` | `/Admin/SystemSettings` | System Settings | No change |
| `/Admin/Lookups` | `/Admin/Lookups` | Lookup Tables | No change |
| `/Admin/PMTemplates` | `/Admin/PMTemplates` | PM Templates | No change |
| `/Admin/AuditLog` | `/Admin/AuditLog` | Audit Log | No change |
| `/API` | `/API` | Integrations | Moved from Admin Tools to Admin group |
| `/AI` | `/AI` | AI Assistant | Moved to sidebar footer |
| `/Help` | `/Help` | Help Center | Remains in sidebar footer |

### Removed from Sidebar

| Old Route | Reason |
|---|---|
| `/Admin/Departments` | Moved to Lookups (accessible via Admin > Lookup Tables) |
| `/Admin/CostCenters` | Moved to Lookups |
| `/Admin/AssetCategories` | Moved to Lookups |
| `/Admin/GlAccounts` | Moved to Lookups |
| `/Admin/Manufacturers` | Moved to Lookups |
| `/Admin/Technicians` | Moved to Lookups |
| `/Admin/ProjectManagers` | Moved to Lookups |
| `/Admin/ExchangeRates` | Moved to Lookups |
| `/Admin/Approvals` | Moved to Admin Hub |
| `/Admin/EnvironmentStatus` | Moved to Admin Hub |
| `/Admin/SmokeTests` | Moved to Admin Hub |
| `/Admin/DemoData` | Moved to Admin Hub |
| `/Admin/DataImport` | Moved to Admin Hub |
| `/Admin/Export` | Moved to Admin Hub |

## Summary of Changes

- **0 URL-breaking changes**: All existing routes continue to work
- **Label-only renames**: Storerooms -> Warehouses, Receiving -> Receipts, Sites -> Organization & Sites
- **IA restructuring**: Quick-action duplicates removed; Setup (Masters) items consolidated into Lookups or Admin Hub
- **New attributes**: `data-nav-route` added to every sidebar link
