# CherryAI EAM Navigation Map

**Version:** 2.0  
**Last Updated:** 2026-01-22  
**Status:** CANONICAL SOURCE OF TRUTH

---

## Overview

This document is the **single source of truth** for all navigation routes in CherryAI EAM. Other navigation documents (NavigationAudit.md, RouteRegistry.md) should be considered historical reference only.

## Route Status Legend

| Status | Description |
|--------|-------------|
| **Canonical** | Primary route - linked in sidebar navigation |
| **Legacy** | Old route preserved for backwards compatibility (redirects to canonical) |
| **Config** | Configuration/tools route (under /Admin but not operational) |
| **Hidden** | Route exists but not in navigation (accessible by direct URL) |

---

## Feature Area Routes

### Assets Section

| Menu Label | Canonical Route | Page File | Status |
|------------|-----------------|-----------|--------|
| Asset Register | `/Assets` | Pages/Assets/Index.cshtml | Canonical |
| Physical Inventory | `/Inventory` | Pages/Inventory/Index.cshtml | Canonical |
| Transfer Asset | `/Assets?action=transfer` | Pages/Assets/Index.cshtml | Canonical |
| Dispose Asset | `/Assets?action=dispose` | Pages/Assets/Index.cshtml | Canonical |
| Improve Asset | `/Assets?action=improve` | Pages/Assets/Index.cshtml | Canonical |
| Bulk Operations | `/BulkOperations` | Pages/BulkOperations/Index.cshtml | Canonical |
| Asset Details | `/Assets/Asset/{id}` | Pages/Assets/Asset.cshtml | Hidden |

---

### Maintenance Section

| Menu Label | Canonical Route | Page File | Status | Notes |
|------------|-----------------|-----------|--------|-------|
| Work Orders | `/Maintenance` | Pages/Maintenance/Index.cshtml | **Canonical** | Primary list view |
| Work Order Details | `/Maintenance/Details/{id}` | Pages/Maintenance/Details.cshtml | Hidden | Execution Cockpit |
| Work Requests | `/Maintenance/WorkRequests` | Pages/Maintenance/WorkRequests/Index.cshtml | **Canonical** | Work request queue |
| Create Work Request | `/Maintenance/WorkRequests/Create` | Pages/Maintenance/WorkRequests/Create.cshtml | **Canonical** | Self-service submission |
| Work Request Details | `/Maintenance/WorkRequests/Details/{id}` | Pages/Maintenance/WorkRequests/Details.cshtml | Hidden | |
| PM Schedules | `/Maintenance/Schedules` | Pages/Maintenance/Schedules.cshtml | **Canonical** | Schedule management |
| PM Assignments | `/Maintenance/Assignments` | Pages/Maintenance/Assignments/Index.cshtml | **Canonical** | Technician assignments |

**Configuration (Admin Section):**
| Menu Label | Canonical Route | Page File | Status | Notes |
|------------|-----------------|-----------|--------|-------|
| PM Templates | `/Admin/PMTemplates` | Pages/Admin/PMTemplates.cshtml | **Canonical** | Template configuration |
| PM Template Edit | `/Admin/PMTemplateEdit/{id?}` | Pages/Admin/PMTemplateEdit.cshtml | Hidden | |

**Note:** Work Request conversion redirects to `/Maintenance/Details/{id}` (canonical WO route).

---

### Inventory & Stores Section (Materials)

| Menu Label | Canonical Route | Page File | Status |
|------------|-----------------|-----------|--------|
| Item Master | `/Materials/Items` | Pages/Materials/Items.cshtml | **Canonical** |
| Item Edit/Create | `/Materials/ItemEdit/{id?}` | Pages/Materials/ItemEdit.cshtml | Hidden |
| Stock Levels | `/Admin/StockLevels` | Pages/Admin/StockLevels.cshtml | Canonical |
| Stock Transactions | `/Admin/Inventory` | Pages/Admin/Inventory.cshtml | Canonical |
| Item Categories | `/Admin/ItemCategories` | Pages/Admin/ItemCategories.cshtml | Canonical |
| Kits & Assemblies | `/Admin/Kits` | Pages/Admin/Kits.cshtml | Canonical |
| Barcode Labels | `/Admin/Barcodes` | Pages/Admin/Barcodes.cshtml | Canonical |

#### Legacy Routes (Inventory)

| Legacy Route | Redirects To | Type | Notes |
|--------------|--------------|------|-------|
| `/Admin/Items` | `/Materials/Items` | 302 | Query params preserved |
| `/Admin/Items?handler=Edit&id=X` | `/Materials/ItemEdit/X` | 302 | Legacy edit pattern |

---

### Purchasing Section

| Menu Label | Canonical Route | Page File | Status |
|------------|-----------------|-----------|--------|
| Vendors | `/Admin/Vendors` | Pages/Admin/Vendors.cshtml | Canonical |
| Purchase Requisitions | `/Admin/Requisitions` | Pages/Admin/Requisitions.cshtml | Canonical |
| Purchase Orders | `/Purchasing` | Pages/Purchasing/Index.cshtml | Canonical |
| Receiving | `/Receiving` | Pages/Receiving/Index.cshtml | Canonical |
| Accounts Payable | `/AccountsPayable` | Pages/AccountsPayable/Index.cshtml | Canonical |

---

### Finance Section

| Menu Label | Canonical Route | Page File | Status |
|------------|-----------------|-----------|--------|
| Depreciation Books | `/Books` | Pages/Books/Index.cshtml | Canonical |
| Depreciation Reports | `/Reports/ReportHub` | Pages/Reports/ReportHub.cshtml | Canonical |
| Journal Entries | `/Journals` | Pages/Journals/Index.cshtml | Canonical |
| US Tax (MACRS/179) | `/UsTax` | Pages/UsTax/Index.cshtml | Canonical |
| Canadian CCA | `/CCA` | Pages/CCA/Index.cshtml | Canonical |
| Capital Projects | `/CIP` | Pages/CIP/Index.cshtml | Canonical |
| Cost Analysis | `/CIP/Costs` | Pages/CIP/Costs.cshtml | Canonical |

---

### Reports Section

| Menu Label | Canonical Route | Page File | Status |
|------------|-----------------|-----------|--------|
| Report Center | `/Reports/ReportHub` | Pages/Reports/ReportHub.cshtml | Canonical |
| Report Builder | `/Reports/Builder` | Pages/Reports/Builder.cshtml | Canonical |
| Compliance Reports | `/Reports/Compliance` | Pages/Reports/Compliance.cshtml | Canonical |
| Report Export | `/Reports/Export` | Pages/Reports/Export.cshtml | Canonical |
| Form 4562 (US) | `/Reports/Form4562` | Pages/Reports/Form4562.cshtml | Canonical |
| T2 Schedule 8 (CA) | `/Reports/T2Schedule8` | Pages/Reports/T2Schedule8.cshtml | Canonical |

#### Legacy Routes (Reports)

| Legacy Route | Redirects To | Type |
|--------------|--------------|------|
| `/Reports/Index` | `/Reports/ReportHub` | 301 |

---

### Setup (Masters) Section

| Menu Label | Canonical Route | Status |
|------------|-----------------|--------|
| Sites | `/Admin/Sites` | Config |
| Locations | `/Admin/Locations` | Config |
| Departments | `/Admin/Departments` | Config |
| Cost Centers | `/Admin/CostCenters` | Config |
| Asset Categories | `/Admin/AssetCategories` | Config |
| Chart of Accounts | `/Admin/GlAccounts` | Config |
| Manufacturers | `/Admin/Manufacturers` | Config |
| Technicians | `/Admin/Technicians` | Config |
| Project Managers | `/Admin/ProjectManagers` | Config |
| Exchange Rates | `/Admin/ExchangeRates` | Config |

---

### Administration & Tools Section

| Menu Label | Canonical Route | Status | Notes |
|------------|-----------------|--------|-------|
| Admin Hub | `/Admin` | Config | Main admin dashboard |
| Company Settings | `/Admin/Company` | Config | |
| Users & Roles | `/Admin/Users` | Config | Admin only |
| System Settings | `/Admin/SystemSettings` | Config | Admin only |
| Approvals | `/Admin/Approvals` | Config | |
| Audit Log | `/Admin/AuditLog` | Config | |
| Master Data Import | `/Admin/DataImport` | Config | |
| File Export | `/Admin/Export` | Config | |
| Environment Status | `/Admin/EnvironmentStatus` | Config | |
| Smoke Tests | `/Admin/SmokeTests` | Config | LAB only |
| Demo Data | `/Admin/DemoData` | Config | LAB only |
| Integrations | `/Admin/IntegrationEndpoints` | Config | Webhooks hub |

#### Legacy Routes (Admin)

| Legacy Route | Status | Notes |
|--------------|--------|-------|
| `/Admin/WorkOrders` | Legacy | Shows banner, points to `/Maintenance` |
| `/Admin/Diagnostics` | Legacy | Shows banner, points to `/Admin` |
| `/Admin/Import` | Hidden | Replaced by DataImport |
| `/Admin/SeedData` | Hidden | Dev-only testing tool |

---

### Help Center

| Menu Label | Canonical Route | Status |
|------------|-----------------|--------|
| Help Center | `/Help` | Canonical |
| Task Guides | `/Help?tab=guides` | Canonical |
| Concepts | `/Help?tab=concepts` | Canonical |
| Glossary | `/Help?tab=glossary` | Canonical |
| Implementation Guide | `/Help/Implementation` | Canonical |

---

## Sidebar Active State Rules

### Pattern Matching

```csharp
// Assets section
currentPage.StartsWith("/Assets") && !currentPage.Contains("Transfer")...

// Materials section (Item Master)
currentPage.StartsWith("/Materials/Item")

// Maintenance section
currentPage == "/Maintenance" || currentPage.Contains("/Maintenance/")

// Admin sections
currentPage.StartsWith("/Admin/XXX")
```

### Active Highlighting

- Feature area sections: Match route prefix
- Detail pages: Highlight parent list item
- Edit pages: Highlight parent list item

---

## Route Governance Rules

### 1. Feature Areas Own Operational Routes

| Area | Route Prefix | Purpose |
|------|--------------|---------|
| Assets | `/Assets/*` | Asset lifecycle operations |
| Maintenance | `/Maintenance/*` | Work orders, PM |
| Materials | `/Materials/*` | Item master, parts management |
| Purchasing | `/Purchasing/*` | PO, receiving |
| Finance | `/Books/*`, `/Journals/*`, `/CIP/*` | Financial operations |
| Reports | `/Reports/*` | Analytics, exports |

### 2. Admin Owns Configuration

| Type | Route Prefix | Purpose |
|------|--------------|---------|
| Setup Masters | `/Admin/*` | Sites, Locations, Categories, etc. |
| System Config | `/Admin/*` | Company, Users, Settings |
| Tools | `/Admin/*` | Import, Export, Smoke Tests |

### 3. Migration Strategy

When moving a route from `/Admin/*` to a feature area:

1. Create new canonical route in feature area
2. Keep legacy `/Admin/*` route
3. Add 302 redirect from legacy to canonical
4. Update sidebar to point to canonical
5. Add legacy banner if page still renders
6. Document in this file

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.0 | 2026-01-22 | Made canonical source of truth, added all sections |
| 1.0 | 2026-01-22 | Initial creation during Sprint 15 |

---

*Maintained by CherryAI EAM Development Team*
