# CherryAI EAM - Route Registry
Last updated: 2026-01-24

**CANONICAL SOURCE OF TRUTH** for all application routes.

**See Also:**
- [README.md](README.md) - Documentation index
- [NavigationAndRouting.md](NavigationAndRouting.md) - Navigation patterns and standards
- [Architecture.md](Architecture.md) - System architecture overview

---

## Navigation Source Files

| File | Description |
|------|-------------|
| `Pages/Shared/_ModernLayout.cshtml` | Main sidebar navigation (accordion menu groups) |
| `Pages/Shared/_Layout.cshtml` | Legacy top navbar (deprecated, kept for fallback) |
| `Pages/Shared/_AssetMaintenanceHeader.cshtml` | Module-specific pill navigation (Asset Maintenance) |
| `wwwroot/css/sidebar-nav.css` | Sidebar styling |
| `wwwroot/js/sidebar-nav.js` | Sidebar behavior/accordion |

---

## Route Registry

### Dashboard / Home

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Dashboard | `/` | Dashboard | None | Active | Quick action (top of nav) |

---

### Assets Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Asset Register | `/Assets` | Asset Register | None | Active | Main asset list |
| Physical Inventory | `/Inventory` | Physical Inventory | None | Active | Physical inventory counts |
| Transfer Asset | `/Assets?action=transfer` | Asset Register | None | Active | Opens asset list for transfer selection |
| Dispose Asset | `/Assets?action=dispose` | Asset Register | None | Active | Opens asset list for disposal selection |
| Improve Asset | `/Assets?action=improve` | Asset Register | None | Active | Opens asset list for improvement selection |
| Bulk Operations | `/BulkOperations` | Bulk Operations | None | Active | Bulk transfers/status changes |
| *(not in nav)* | `/Assets/Schedule` | Depreciation Schedule | None | Active | DO NOT TOUCH (depreciation) |
| *(not in nav)* | `/Assets/Delete` | Delete Asset | None | Active | Asset deletion |
| *(not in nav)* | `/Assets/Asset` | Asset Management | None | Active | Unified view/edit/create |
| *(not in nav)* | `/BulkOperations/Details` | Bulk Operation Details | None | Active | Bulk operation detail |

---

### Asset Maintenance Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Work Requests | `/Maintenance/WorkRequests` | Work Requests | None | Active | Work request list (company-scoped) |
| *(not in nav)* | `/Maintenance/WorkRequests/Create` | Create Work Request | None | Active | New work request form |
| *(not in nav)* | `/Maintenance/WorkRequests/Details/{id}` | Work Request Details | None | Active | WR detail; conversion redirects to `/Maintenance/Details/{id}` |
| Work Orders | `/Maintenance` | Asset Maintenance | None | Active | Work order list |
| PM Templates | `/Admin/PMTemplates` | Asset Maintenance | Admin,Accountant | Active | Preventive maintenance templates |
| Maintenance Schedules | `/Maintenance/Schedules` | Maintenance Schedules | None | Active | PM schedules |
| *(not in nav)* | `/Maintenance/Details/{id}` | Work Order #{id} | None | Active | Canonical WO detail view |
| *(not in nav)* | `/Maintenance/Assignments` | PM Assignments | None | Active | PM assignment list |
| *(not in nav)* | `/WorkOrders/Details` | Asset Maintenance | None | Legacy | Redirects to `/Maintenance/Details/{id}` |
| *(not in nav)* | `/Admin/WorkOrders` | Work Orders | None | Legacy | Duplicate of /Maintenance |

---

### Inventory & Stores Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Item Master | `/Materials/Items` | Item Master | None | Active | Parts/items catalog (canonical) |
| Stock Levels | `/Admin/StockLevels` | Stock Levels | Admin,Accountant | Active | Inventory levels |
| Stock Transactions | `/Admin/Inventory` | Stock Transactions | Admin,Accountant | Active | Inventory movements |
| Item Categories | `/Admin/ItemCategories` | Item Categories | Admin | Active | Item categorization |
| Kits & Assemblies | `/Admin/Kits` | Kits & Assemblies | Admin | Active | Kit management |
| Barcode Labels | `/Admin/Barcodes` | Barcode Labels | Admin,Accountant | Active | Barcode generation |
| *(not in nav)* | `/Inventory/List` | {List Name} | None | Active | Inventory list detail |

---

### Procurement Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Vendors | `/Admin/Vendors` | Vendors | Admin | Active | Vendor master |
| Purchase Requisitions | `/Admin/Requisitions` | Purchase Requisitions | Admin,Accountant | Active | PR management |
| Purchase Orders | `/Purchasing` | Procurement | None | Active | PO list |
| Receiving | `/Receiving` | Goods Receiving | None | Active | Goods receipt |
| Accounts Payable | `/AccountsPayable` | Accounts Payable | None | Active | AP invoices/payments |
| *(not in nav)* | `/Purchasing/Details` | Procurement | None | Active | PO detail view |
| *(not in nav)* | `/AccountsPayable/Details/{id}` | Accounts Payable | None | Active | Invoice detail view |

---

### Finance & Tax Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Depreciation Books | `/Books` | Depreciation Books | None | Active | Book list/configuration |
| Depreciation Reports | `/Reports/ReportHub` | Report Center | None | Active | Depreciation reports and calculations |
| Journal Entries | `/Journals` | Journal Entries | None | Active | GL journal list |
| US Tax (MACRS/179) | `/UsTax` | US Tax (MACRS/179) | None | Active | US tax engine |
| Canadian CCA | `/CCA` | Canadian CCA | None | Active | Canadian CCA classes |
| Form 4562 (US) | `/Reports/Form4562` | Form 4562 - Depreciation and Amortization | None | Active | IRS form |
| T2 Schedule 8 (CA) | `/Reports/T2Schedule8` | T2 Schedule 8 - Capital Cost Allowance | None | Active | CRA form |
| *(not in nav)* | `/Books/Create` | Create Book | None | Active | Book creation |
| *(not in nav)* | `/Books/Details` | Book Details | None | Active | Book detail |
| *(not in nav)* | `/Books/Edit` | Edit Book | None | Active | Book editing |
| *(not in nav)* | `/Books/Delete` | Delete Book | None | Active | Book deletion |
| *(not in nav)* | `/Books/GlAccounts` | GL Accounts | None | Active | Book GL mapping |
| *(not in nav)* | `/Journals/Details` | Journal Details | None | Active | Journal detail |
| *(not in nav)* | `/Journals/Generate` | Generate Depreciation Journal | None | Active | Journal generation |
| *(not in nav)* | `/CCA/ClassReport` | CCA Schedule | None | Active | CCA class detail |
| *(not in nav)* | `/Reports/DepreciationPreview` | Depreciation Preview | None | Active | Depreciation preview |

---

### Projects (CIP) Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Capital Projects | `/CIP` | Capital Projects | None | Active | CIP list |
| Cost Analysis | `/CIP/CostDetails` | Cost Entry - {name} | None | Active | Cost entry detail |
| *(not in nav)* | `/CIP/Details` | Capital Projects | None | Active | Project detail |
| *(not in nav)* | `/CIP/CostTypeDetails` | {CostType} Costs | None | Active | Cost type breakdown |

---

### Reports Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Report Center | `/Reports/ReportHub` | Reports | None | Active | Report dashboard |
| Report Builder | `/Reports/Builder` | Report Builder | None | Active | Custom reports |
| Compliance Reports | `/Reports/Compliance` | Compliance Reports | None | Active | Regulatory reports |
| Export Reports | `/Reports/Export` | Report Export | None | Active | Report export |
| *(not in nav)* | `/Reports/Index` | Reports & Exports | None | Legacy | Duplicate of ReportHub? |
| *(not in nav)* | `/Reports/ChartOfAccounts` | Chart of Accounts | None | Active | COA report |

---

### Setup (Master Data) Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Sites | `/Admin/Sites` | Sites | Admin | Active | Site master |
| Locations | `/Admin/Locations` | Locations | Admin | Active | Location master |
| Departments | `/Admin/Departments` | Departments | Admin | Active | Department master |
| Cost Centers | `/Admin/CostCenters` | Cost Centers | Admin | Active | Cost center master |
| Asset Categories | `/Admin/AssetCategories` | Asset Categories | Admin | Active | Asset categorization |
| GL Accounts | `/Admin/GlAccounts` | GL Accounts | Admin | Active | Chart of accounts |
| Manufacturers | `/Admin/Manufacturers` | Manufacturers | Admin | Active | Manufacturer master |
| Technicians | `/Admin/Technicians` | Technician Management | Admin | Active | Technician master |
| Project Managers | `/Admin/ProjectManagers` | Project Managers | Admin | Active | PM master |
| Exchange Rates | `/Admin/ExchangeRates` | Exchange Rates | Admin | Active | Currency exchange |

---

### Administration Section (Admin Only)

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Admin Hub | `/Admin` | Administration | Admin | Active | Admin dashboard |
| Company Settings | `/Admin/Company` | Company Settings | Admin | Active | Company config |
| User Management | `/Admin/Users` | User Management | Admin | Active | User admin |
| System Settings | `/Admin/SystemSettings` | System Settings | Admin | Active | App settings |
| Approvals | `/Admin/Approvals` | Approval Workflows | Admin | Active | Workflow config |
| Audit Log | `/Admin/AuditLog` | Audit Log | Admin | Active | Audit trail |
| Master Data Import | `/Admin/DataImport` | Master Data Import | Admin | Active | Seed pipelines |
| File Import | `/Admin/Import` | File Import | Admin | Legacy | CSV/Excel import (hidden from nav) |
| File Export | `/Admin/Export` | File Export | Admin | Active | Data export |
| *(hidden)* | `/Admin/SeedData` | Seed Test Data | Admin | Legacy | Dev tool, removed from nav |
| *(not in nav)* | `/Admin/Diagnostics` | Administration | Admin | Legacy | Duplicate of Admin Hub? |
| *(not in nav)* | `/Admin/WorkOrders` | Work Orders | Admin | Legacy | Duplicate of /Maintenance |

---

### Tools Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| AI Assistant | `/AI` | AI Assistant | None | Active | Chat interface |
| API Integration | `/API` | API Integration | Admin | Active | API docs/testing |
| *(not in nav)* | `/API/Import` | Import Assets | Admin,Accountant | Active | API import endpoint |

---

### Help / Account Section

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| Help Center | `/Help` | Help Center | None | Active | Quick action (bottom nav) |
| Sign In | `/Account/Login` | Login | None | Active | Auth page |
| Sign Out | `/Account/Logout` | *(redirect)* | None | Active | Logout action |
| *(not in nav)* | `/Help/Topic` | {Topic Title} | None | Active | Help topic detail |
| *(not in nav)* | `/Help/Tasks` | {Task Title} | None | Active | Task guide |
| *(not in nav)* | `/Help/Implementation` | Implementation Guide | None | Active | Setup wizard |
| *(not in nav)* | `/Account/AccessDenied` | Access Denied | None | Active | 403 page |

---

### System / Utility Pages

| Sidebar Label | Route | Page Title | Auth | Status | Notes |
|---------------|-------|------------|------|--------|-------|
| *(not in nav)* | `/Error` | Error | None | Active | Error page |
| *(not in nav)* | `/Privacy` | Privacy | None | Active | Privacy policy |
| *(not in nav)* | `/ModuleDisabled` | Module Not Available | None | Active | Module gating |

---

## Potential Issues Identified

### Duplicates / Legacy Candidates

| Route | Issue | Recommendation |
|-------|-------|----------------|
| `/Admin/WorkOrders` | Duplicate of `/Maintenance` | Hide Candidate |
| `/Admin/Diagnostics` | Duplicate of `/Admin` (Admin Hub) | Hide Candidate |
| `/Reports/Index` | Possibly duplicate of `/Reports/ReportHub` | Investigate |
| `/WorkOrders/Details` | Legacy detail view, may redirect to `/Maintenance/Details` | Investigate |
| `/Admin/SeedData` | Dev tool, already hidden | Keep hidden |

### Title Mismatches

| Route | Nav Label | Page Title | Issue |
|-------|-----------|------------|-------|
| `/Maintenance` | Work Orders | Asset Maintenance | Label mismatch (ok per terminology) |
| `/Admin/Inventory` | Inventory Transactions | Stock Levels | Title mismatch |
| `/Reports/ReportHub` | Report Center | Reports | Minor mismatch |

### Auth Policy Review

| Route | Current Auth | Expected |
|-------|--------------|----------|
| `/Admin/Import` | None | Admin? |
| `/Admin/Export` | None | Admin? |
| Most `/Admin/*` pages | None | Admin? |

---

## Extraction Commands Used

```bash
# Find nav definitions
rg -n "nav" -S Pages/Shared

# Find sidebar references
rg -n "Sidebar" -S .

# Find href links in shared layouts
rg -n 'href="/' -S Pages/Shared

# Extract all page titles
rg -n 'ViewData\["Title"\]' Pages --type cshtml

# Find auth policies
rg -n '\[Authorize' Pages --type cs
```

---

## Next Steps

1. Review duplicates and decide: hide from nav or consolidate
2. Align page titles with nav labels where inconsistent
3. Add missing auth policies to admin-only pages
4. Consider consolidating `/Reports/Index` with `/Reports/ReportHub`
