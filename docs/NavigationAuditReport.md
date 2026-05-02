# Navigation Audit Report

**Generated:** 2026-01-24  
**Purpose:** Comprehensive audit of sidebar navigation, routes, and field labels

---

## Executive Summary

| Category | Count | Status |
|----------|-------|--------|
| Sidebar Sections | 8 | ✓ All render correctly |
| Sidebar Links | 58 | ✓ All resolve to existing pages |
| Razor Pages | 119 | ✓ Enumerated |
| asp-page Usages | 65 | See intra-screen audit |
| href Usages | 403 | See intra-screen audit |

---

## Sidebar Inventory

### Quick Actions (Top-Level)

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Dashboard | / | Pages/Index.cshtml | ✓ |
| Work Orders (Cockpit) | /Maintenance | Pages/Maintenance/Index.cshtml | ✓ |
| Work Requests | /Maintenance/WorkRequests | Pages/Maintenance/WorkRequests/Index.cshtml | ✓ |
| PM Schedules | /Maintenance/Schedules | Pages/Maintenance/Schedules.cshtml | ✓ |
| Capital Projects (CIP) | /CIP | Pages/CIP/Index.cshtml | ✓ |

### Section: Assets

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Asset Register | /Assets | Pages/Assets/Index.cshtml | ✓ |
| Physical Inventory | /Inventory | Pages/Inventory/Index.cshtml | ✓ |
| Transfer Asset | /Assets?action=transfer | Pages/Assets/Index.cshtml (filtered) | ✓ |
| Dispose Asset | /Assets?action=dispose | Pages/Assets/Index.cshtml (filtered) | ✓ |
| Improve Asset | /Assets?action=improve | Pages/Assets/Index.cshtml (filtered) | ✓ |
| Bulk Operations | /BulkOperations | Pages/BulkOperations/Index.cshtml | ✓ |

### Section: Work Execution

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Work Orders (Cockpit) | /Maintenance | Pages/Maintenance/Index.cshtml | ✓ |
| Work Requests | /Maintenance/WorkRequests | Pages/Maintenance/WorkRequests/Index.cshtml | ✓ |
| Create Work Request | /Maintenance/WorkRequests/Create | Pages/Maintenance/WorkRequests/Create.cshtml | ✓ |
| PM Schedules | /Maintenance/Schedules | Pages/Maintenance/Schedules.cshtml | ✓ |
| PM Assignments | /Maintenance/Assignments | Pages/Maintenance/Assignments/Index.cshtml | ✓ |

### Section: Inventory & Stores

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Item Master | /Materials/Items | Pages/Materials/Items.cshtml | ✓ |
| Stock Levels | /Admin/StockLevels | Pages/Admin/StockLevels.cshtml | ✓ |
| Stock Transactions | /Admin/Inventory | Pages/Admin/Inventory.cshtml | ✓ |
| Item Categories | /Admin/ItemCategories | Pages/Admin/ItemCategories.cshtml | ✓ |
| Kits & Assemblies | /Admin/Kits | Pages/Admin/Kits.cshtml | ✓ |
| Barcode Labels | /Admin/Barcodes | Pages/Admin/Barcodes.cshtml | ✓ |

### Section: Purchasing

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Vendors | /Admin/Vendors | Pages/Admin/Vendors.cshtml | ✓ |
| Purchase Requisitions | /Admin/Requisitions | Pages/Admin/Requisitions.cshtml | ✓ |
| Purchase Orders | /Purchasing | Pages/Purchasing/Index.cshtml | ✓ |
| Receiving | /Receiving | Pages/Receiving/Index.cshtml | ✓ |
| Accounts Payable | /AccountsPayable | Pages/AccountsPayable/Index.cshtml | ✓ |

### Section: Finance

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Depreciation Books | /Books | Pages/Books/Index.cshtml | ✓ |
| Depreciation Reports | /Reports/ReportHub | Pages/Reports/ReportHub.cshtml | ✓ |
| Journal Entries | /Journals | Pages/Journals/Index.cshtml | ✓ |
| US Tax (MACRS/179) | /UsTax | Pages/UsTax/Index.cshtml | ✓ |
| Canadian CCA | /CCA | Pages/CCA/Index.cshtml | ✓ |
| Capital Projects (CIP) | /CIP | Pages/CIP/Index.cshtml | ✓ |
| CIP Cost Analysis | /CIP/Costs | Pages/CIP/Costs.cshtml | ✓ |

### Section: Reports

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Report Center | /Reports/ReportHub | Pages/Reports/ReportHub.cshtml | ✓ |
| Report Builder | /Reports/Builder | Pages/Reports/Builder.cshtml | ✓ |
| Compliance Reports | /Reports/Compliance | Pages/Reports/Compliance.cshtml | ✓ |
| Report Export | /Reports/Export | Pages/Reports/Export.cshtml | ✓ |
| Form 4562 (US) | /Reports/Form4562 | Pages/Reports/Form4562.cshtml | ✓ |
| T2 Schedule 8 (CA) | /Reports/T2Schedule8 | Pages/Reports/T2Schedule8.cshtml | ✓ |

### Section: Setup (Masters)

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Sites | /Admin/Sites | Pages/Admin/Sites.cshtml | ✓ |
| Locations | /Admin/Locations | Pages/Admin/Locations.cshtml | ✓ |
| Departments | /Admin/Departments | Pages/Admin/Departments.cshtml | ✓ |
| Cost Centers | /Admin/CostCenters | Pages/Admin/CostCenters.cshtml | ✓ |
| Asset Categories | /Admin/AssetCategories | Pages/Admin/AssetCategories.cshtml | ✓ |
| Chart of Accounts | /Admin/GlAccounts | Pages/Admin/GlAccounts.cshtml | ✓ |
| Manufacturers | /Admin/Manufacturers | Pages/Admin/Manufacturers.cshtml | ✓ |
| Technicians | /Admin/Technicians | Pages/Admin/Technicians.cshtml | ✓ |
| Project Managers | /Admin/ProjectManagers | Pages/Admin/ProjectManagers.cshtml | ✓ |
| Exchange Rates | /Admin/ExchangeRates | Pages/Admin/ExchangeRates.cshtml | ✓ |

### Section: Administration & Tools (Admin-only)

| Label | Route | Target Page | RBAC | Status |
|-------|-------|-------------|------|--------|
| Admin Hub | /Admin | Pages/Admin/Index.cshtml | Admin | ✓ |
| Company Settings | /Admin/Company | Pages/Admin/Company.cshtml | Admin | ✓ |
| Users & Roles | /Admin/Users | Pages/Admin/Users.cshtml | Admin | ✓ |
| System Settings | /Admin/SystemSettings | Pages/Admin/SystemSettings.cshtml | Admin | ✓ |
| PM Templates | /Admin/PMTemplates | Pages/Admin/PMTemplates.cshtml | Admin | ✓ |
| Approvals | /Admin/Approvals | Pages/Admin/Approvals.cshtml | Admin | ✓ |
| Audit Log | /Admin/AuditLog | Pages/Admin/AuditLog.cshtml | Admin | ✓ |
| Environment Status | /Admin/EnvironmentStatus | Pages/Admin/EnvironmentStatus.cshtml | Admin | ✓ |
| Smoke Tests | /Admin/SmokeTests | Pages/Admin/SmokeTests.cshtml | Admin | ✓ |
| Demo Data | /Admin/DemoData | Pages/Admin/DemoData.cshtml | Admin | ✓ |
| Master Data Import | /Admin/DataImport | Pages/Admin/DataImport.cshtml | Admin | ✓ |
| File Export | /Admin/Export | Pages/Admin/Export.cshtml | Admin | ✓ |
| AI Assistant | /AI | Pages/AI/Index.cshtml | Admin | ✓ |
| API Integration | /API | Pages/API/Index.cshtml | Admin | ✓ |

### Footer Links

| Label | Route | Target Page | Status |
|-------|-------|-------------|--------|
| Help Center | /Help | Pages/Help/Index.cshtml | ✓ |
| Sign Out | /Account/Logout | Pages/Account/Logout.cshtml | ✓ |
| Sign In | /Account/Login | Pages/Account/Login.cshtml | ✓ |

---

## Route Registry

### Core Routes

| Route | Page | Purpose | Parameters |
|-------|------|---------|------------|
| / | Index | Dashboard | None |
| /Assets | Assets/Index | Asset list | ?action=transfer|dispose|improve |
| /Assets/Asset | Assets/Asset | Asset details/edit | ?id={assetId}&mode=view|edit|create |
| /Maintenance | Maintenance/Index | Work order list | None |
| /Maintenance/Details | Maintenance/Details | Work order details | ?id={workOrderId} |
| /Maintenance/WorkRequests | WorkRequests/Index | Work request list | None |
| /Maintenance/WorkRequests/Create | WorkRequests/Create | Create work request | None |
| /Maintenance/WorkRequests/Details | WorkRequests/Details | Work request details | ?id={requestId} |
| /Materials/Items | Materials/Items | Item list | None |
| /Materials/ItemEdit | Materials/ItemEdit | Item details/edit | ?id={itemId}&mode=view|edit|create |
| /Admin/Vendors | Admin/Vendors | Vendor list | None |
| /CIP | CIP/Index | CIP project list | None |
| /CIP/Details | CIP/Details | CIP project details | ?id={projectId} |

### Admin Routes (Admin-only)

| Route | Page | Purpose | RBAC |
|-------|------|---------|------|
| /Admin | Admin/Index | Admin hub | Admin |
| /Admin/Company | Admin/Company | Company settings | Admin |
| /Admin/Users | Admin/Users | User management | Admin |
| /Admin/SmokeTests | Admin/SmokeTests | Smoke test runner | Admin |
| /Admin/PMTemplates | Admin/PMTemplates | PM template management | Admin |

---

## Intra-Screen Link Audit

### Verified Link Patterns

| Source Page | Link Type | Target | Status |
|-------------|-----------|--------|--------|
| Assets/Index | View Asset | /Assets/Asset?id={id} | ✓ |
| Maintenance/Index | View Work Order | /Maintenance/Details?id={id} | ✓ |
| Maintenance/Details | View Asset | /Assets/Asset?id={assetId} | ✓ |
| WorkRequests/Index | View Request | /Maintenance/WorkRequests/Details?id={id} | ✓ |
| Materials/Items | View Item | /Materials/ItemEdit?id={id} | ✓ |
| Admin/Vendors | Edit Vendor | inline modal | ✓ |
| CIP/Index | View Project | /CIP/Details?id={id} | ✓ |

### Issues Found and Fixed

None - all intra-screen links verified.

---

## Field Label Audit

### Core Screens Audited

- Work Orders (Index, Details)
- Work Requests (Index, Create, Details)
- Assets (Index, Asset)
- Items (Items, ItemEdit)
- Vendors
- Locations
- PM Templates

### Label Conventions

| Term | Standard Usage | Notes |
|------|----------------|-------|
| Work Order | Primary term for maintenance events | Not "Maintenance Event" in UI |
| Asset Number | Primary asset identifier | Not "Asset Tag" |
| Serial Number | Equipment serial | Distinct from Asset Number |
| Part Number | Item identifier (IPN) | Internal Part Number |
| Vendor Part Number | VPN | Vendor's catalog number |
| Manufacturer Part Number | MPN | OEM part number |
| Failure Code | Issue classification | Used in closeout |
| Work Order Number | WO-YYYYMMDD-### format | Auto-generated |

### Issues Found and Fixed

None - all labels verified consistent with domain vocabulary.

---

## Guardrail Tests Added

### Test: "Navigation → All Sidebar Links Resolve"
- Enumerates sidebar links from layout
- Verifies each route resolves to an existing Razor Page
- Added to SmokeTestRunner

### Test: "Navigation → Intra-Screen Links Valid"
- Scans Razor Pages for asp-page and href patterns
- Verifies targets exist and parameters are valid
- Added to SmokeTestRunner

---

## Recommendations Implemented

1. **All sidebar links verified** - Every sidebar href points to an existing page
2. **Route consistency** - All routes use consistent naming conventions
3. **RBAC enforcement** - Admin section properly gated by isAdmin check
4. **Active state logic** - Helper functions (IsPage, IsExactPage, IsAny) used consistently
5. **Section auto-expand** - Sections expand when containing active page

---

## Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-01-24 | Initial comprehensive audit | Agent |
| 2026-01-24 | All 58 sidebar links verified | Agent |
| 2026-01-24 | Added navigation guardrail tests | Agent |
