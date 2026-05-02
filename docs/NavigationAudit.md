# CherryAI EAM - Navigation Audit Phase 2

**Generated:** 2026-01-21  
**Source:** `Pages/Shared/_ModernLayout.cshtml`  
**Status:** HISTORICAL - This document represents the original audit plan. For current navigation state, see `docs/NavigationMap.md`.

> **IMPORTANT:** Some routes in this document are now outdated. Key changes:
> - Item Master moved from `/Admin/Items` to `/Materials/Items` (Sprint 15)
> - Item Edit at `/Materials/ItemEdit/{id?}` (full-page, no modals)
> - NavigationMap.md is now the canonical source of truth

---

## 1. Final Sidebar Information Architecture

Reorganized into 9 primary sections following EAM industry standards:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  DASHBOARD (Quick Action)                                                    │
│  └── Dashboard                              /                                │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  ASSETS                                                                      │
│  ├── Asset Register                         /Assets                          │
│  ├── Physical Inventory                     /Inventory                       │
│  ├── Transfer Asset                         /Assets?action=transfer [FIXED]  │
│  ├── Dispose Asset                          /Assets?action=dispose  [FIXED]  │
│  ├── Improve Asset                          /Assets?action=improve  [FIXED]  │
│  └── Bulk Operations                        /BulkOperations                  │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  MAINTENANCE (conditional: enableWorkOrders)                                 │
│  ├── Work Orders                            /Maintenance                     │
│  ├── PM Templates                           /Admin/PMTemplates               │
│  └── Maintenance Schedules                  /Maintenance/Schedules           │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  INVENTORY & STORES (conditional: enableInventory)                           │
│  ├── Item Master                            /Admin/Items                     │
│  ├── Stock Levels                           /Admin/StockLevels               │
│  ├── Stock Transactions                     /Admin/Inventory                 │
│  ├── Item Categories                        /Admin/ItemCategories            │
│  ├── Kits & Assemblies                      /Admin/Kits                      │
│  └── Barcode Labels                         /Admin/Barcodes                  │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  PURCHASING (conditional: enablePurchasing || enableAP || enableVendors)     │
│  ├── Vendors                                /Admin/Vendors                   │
│  ├── Purchase Requisitions                  /Admin/Requisitions              │
│  ├── Purchase Orders                        /Purchasing                      │
│  ├── Receiving                              /Receiving                       │
│  └── Accounts Payable                       /AccountsPayable                 │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  FINANCE                                                                     │
│  ├── Depreciation Books                     /Books                           │
│  ├── Depreciation Reports                   /Reports/ReportHub      [FIXED]  │
│  ├── Journal Entries                        /Journals                        │
│  ├── US Tax (MACRS/179)                     /UsTax                           │
│  ├── Canadian CCA                           /CCA                             │
│  ├── Capital Projects                       /CIP                    [MOVED]  │
│  └── Cost Analysis                          /CIP/Costs              [FIXED]  │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  REPORTS                                                                     │
│  ├── Report Center                          /Reports/ReportHub               │
│  ├── Report Builder                         /Reports/Builder                 │
│  ├── Compliance Reports                     /Reports/Compliance              │
│  ├── Export Reports                         /Reports/Export                  │
│  ├── Form 4562 (US)                         /Reports/Form4562       [MOVED]  │
│  └── T2 Schedule 8 (CA)                     /Reports/T2Schedule8    [MOVED]  │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  SETUP (Masters)                                                             │
│  ├── Sites                                  /Admin/Sites                     │
│  ├── Locations                              /Admin/Locations                 │
│  ├── Departments                            /Admin/Departments               │
│  ├── Cost Centers                           /Admin/CostCenters               │
│  ├── Asset Categories                       /Admin/AssetCategories           │
│  ├── Chart of Accounts                      /Admin/GlAccounts                │
│  ├── Manufacturers                          /Admin/Manufacturers             │
│  ├── Technicians                            /Admin/Technicians               │
│  ├── Project Managers                       /Admin/ProjectManagers           │
│  └── Exchange Rates                         /Admin/ExchangeRates             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  ADMINISTRATION & TOOLS (conditional: isAdmin)                               │
│  ├── Admin Hub                              /Admin                           │
│  ├── Company Settings                       /Admin/Company                   │
│  ├── Users & Roles                          /Admin/Users                     │
│  ├── System Settings                        /Admin/SystemSettings            │
│  ├── Approvals                              /Admin/Approvals                 │
│  ├── Audit Log                              /Admin/AuditLog                  │
│  ├── Master Data Import                     /Admin/DataImport                │
│  ├── File Export                            /Admin/Export                    │
│  ├── AI Assistant                           /AI                     [MOVED]  │
│  └── API Integration                        /API                    [MOVED]  │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  HELP CENTER (Quick Action)                                                  │
│  └── Help Center                            /Help                            │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│  ACCOUNT (Quick Actions)                                                     │
│  ├── Sign In                                /Account/Login                   │
│  └── Sign Out                               /Account/Logout                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Section Count:** 9 primary sections + 2 quick action areas  
**Visible Entry Count:** 50 nav items (same as current)

---

## 2. Keep / Rename / Move / Hide Table

### Dashboard Section

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Dashboard | `/` | KEEP | Dashboard | YES | Quick action |

### Assets Section

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Asset Register | `/Assets` | RENAME | Asset Register | NO→YES | Change H1 from "Asset Management" to "Asset Register" |
| Asset Physical Inventory | `/Inventory` | RENAME | Physical Inventory | NO→YES | Shorter label; change H1 from "Asset Tracking" |
| Transfers | `/Assets/Transfer` | RENAME | Transfers | NO→YES | Change H1 from "Transfer Asset" to "Transfers" |
| Disposals | `/Assets/Dispose` | RENAME | Disposals | NO→YES | Change H1 from "Dispose Asset" to "Disposals" |
| Capital Improvements | `/Assets/Improve` | KEEP | Capital Improvements | NO→YES | Change H1 from "Capital Improvement" to "Capital Improvements" |
| Bulk Operations | `/BulkOperations` | KEEP | Bulk Operations | YES | — |

### Maintenance Section (was "Asset Maintenance")

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Work Orders | `/Maintenance` | KEEP | Work Orders | NO→YES | Change H1 from "Asset Maintenance" to "Work Orders" |
| PM Templates | `/Admin/PMTemplates` | KEEP | PM Templates | NO→YES | Change H1 from "Asset Maintenance" to "PM Templates" |
| Maintenance Schedules | `/Maintenance/Schedules` | KEEP | Maintenance Schedules | YES | — |

### Inventory & Stores Section

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Item Master | `/Admin/Items` | KEEP | Item Master | YES | — |
| Stock Levels | `/Admin/StockLevels` | KEEP | Stock Levels | YES | — |
| Inventory Transactions | `/Admin/Inventory` | RENAME | Stock Transactions | NO→YES | Change H1 to match; current H1 is "Stock Levels" |
| Item Categories | `/Admin/ItemCategories` | KEEP | Item Categories | YES | — |
| Kits & Assemblies | `/Admin/Kits` | KEEP | Kits & Assemblies | YES | — |
| Barcode Labels | `/Admin/Barcodes` | KEEP | Barcode Labels | YES | — |

### Purchasing Section (was "Procurement")

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Vendors | `/Admin/Vendors` | KEEP | Vendors | YES | — |
| Purchase Requisitions | `/Admin/Requisitions` | KEEP | Purchase Requisitions | YES | — |
| Purchase Orders | `/Purchasing` | KEEP | Purchase Orders | NO→YES | Change H1 from "Procurement" to "Purchase Orders" |
| Receiving | `/Receiving` | KEEP | Receiving | NO→YES | Change H1 from "Goods Receiving" to "Receiving" |
| Accounts Payable | `/AccountsPayable` | KEEP | Accounts Payable | YES | — |

### Finance Section (was "Finance & Tax" + "Projects (CIP)")

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Depreciation Books | `/Books` | KEEP | Depreciation Books | YES | — |
| Run Depreciation | `/Depreciation` | KEEP | Run Depreciation | N/A | Dynamic title |
| Journal Entries | `/Journals` | KEEP | Journal Entries | YES | — |
| US Tax (MACRS/179) | `/UsTax` | KEEP | US Tax (MACRS/179) | NO→YES | Change H1 from "US Tax" |
| Canadian CCA | `/CCA` | KEEP | Canadian CCA | NO→YES | Change H1 from "CCA Classes" |
| Capital Projects | `/CIP` | MOVE | Capital Projects | YES | Move from Projects section → Finance |
| Cost Analysis | `/CIP/CostDetails` | MOVE | Cost Analysis | NO→YES | Move from Projects section → Finance |
| Form 4562 (US) | `/Reports/Form4562` | MOVE | Form 4562 (US) | YES | Move to Reports section |
| T2 Schedule 8 (CA) | `/Reports/T2Schedule8` | MOVE | T2 Schedule 8 (CA) | YES | Move to Reports section |

### Reports Section

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Report Center | `/Reports/ReportHub` | KEEP | Report Center | NO→YES | Change H1 from "Reports" to "Report Center" |
| Report Builder | `/Reports/Builder` | KEEP | Report Builder | YES | — |
| Compliance Reports | `/Reports/Compliance` | KEEP | Compliance Reports | YES | — |
| Export Reports | `/Reports/Export` | RENAME | Report Export | NO→YES | Label matches page; H1 says "Report Export" |
| Form 4562 (US) | `/Reports/Form4562` | MOVE→HERE | Form 4562 (US) | YES | From Finance section |
| T2 Schedule 8 (CA) | `/Reports/T2Schedule8` | MOVE→HERE | T2 Schedule 8 (CA) | YES | From Finance section |

### Setup (Masters) Section

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Sites | `/Admin/Sites` | KEEP | Sites | YES | — |
| Locations | `/Admin/Locations` | KEEP | Locations | YES | — |
| Departments | `/Admin/Departments` | KEEP | Departments | YES | — |
| Cost Centers | `/Admin/CostCenters` | KEEP | Cost Centers | YES | — |
| Asset Categories | `/Admin/AssetCategories` | KEEP | Asset Categories | YES | — |
| Chart of Accounts | `/Admin/GlAccounts` | KEEP | Chart of Accounts | NO→YES | Change H1 from "GL Accounts" |
| Manufacturers | `/Admin/Manufacturers` | KEEP | Manufacturers | YES | — |
| Technicians | `/Admin/Technicians` | KEEP | Technicians | NO→YES | Change H1 from "Technician Management" |
| Project Managers | `/Admin/ProjectManagers` | KEEP | Project Managers | YES | — |
| Exchange Rates | `/Admin/ExchangeRates` | KEEP | Exchange Rates | YES | — |

### Administration & Tools Section (merged)

| Current Label | Route | Action | New Label | H1 Match | Notes |
|---------------|-------|--------|-----------|----------|-------|
| Admin Hub | `/Admin` | KEEP | Admin Hub | NO→YES | Change H1 from "Administration" |
| Company Settings | `/Admin/Company` | KEEP | Company Settings | YES | — |
| Users & Roles | `/Admin/Users` | KEEP | Users & Roles | NO→YES | Change H1 from "User Management" |
| System Settings | `/Admin/SystemSettings` | KEEP | System Settings | YES | — |
| Approvals | `/Admin/Approvals` | KEEP | Approvals | NO→YES | Change H1 from "Approval Workflows" |
| Audit Log | `/Admin/AuditLog` | KEEP | Audit Log | YES | — |
| Master Data Import | `/Admin/DataImport` | KEEP | Master Data Import | YES | — |
| File Export | `/Admin/Export` | KEEP | File Export | NO→YES | Change H1 to match |
| AI Assistant | `/AI` | MOVE | AI Assistant | YES | Move from Tools section → Admin & Tools |
| API Integration | `/API` | MOVE | API Integration | YES | Move from Tools section → Admin & Tools |

---

## 3. Legacy Candidates (Hide from Nav)

Routes hidden from navigation but remain accessible by direct URL:

| Route | Current Location | Reason | Action |
|-------|------------------|--------|--------|
| `/Admin/Import` | Administration | Replaced by Master Data Import | HIDDEN (done) |
| `/Admin/SeedData` | Administration | Dev-only testing tool | HIDDEN (done) |
| `/Admin/WorkOrders` | *(not in nav)* | Duplicate of `/Maintenance` | HIDE (add legacy banner) |
| `/Admin/Diagnostics` | *(not in nav)* | Duplicate of `/Admin` hub | HIDE (add legacy banner) |
| `/Reports/Index` | *(not in nav)* | Duplicate of `/Reports/ReportHub` | HIDE (redirect to ReportHub) |
| `/WorkOrders/Details` | *(not in nav)* | Legacy route for work order details | REDIRECT to `/Maintenance/Details` |

**Implementation Notes:**
- Already hidden: `/Admin/Import`, `/Admin/SeedData`
- Add legacy banner to: `/Admin/WorkOrders`, `/Admin/Diagnostics`
- Add redirect from: `/Reports/Index` → `/Reports/ReportHub`, `/WorkOrders/Details` → `/Maintenance/Details`

---

## 4. Title Alignment Checklist

Pages where sidebar label ≠ H1 (requires H1 change):

| Route | Current Sidebar | Current H1 | Proposed H1 |
|-------|-----------------|------------|-------------|
| `/Assets` | Asset Register | Asset Management | **Asset Register** |
| `/Inventory` | Physical Inventory | Asset Tracking | **Physical Inventory** |
| `/Assets/Transfer` | Transfers | Transfer Asset | **Transfers** |
| `/Assets/Dispose` | Disposals | Dispose Asset | **Disposals** |
| `/Assets/Improve` | Capital Improvements | Capital Improvement | **Capital Improvements** |
| `/Maintenance` | Work Orders | Asset Maintenance | **Work Orders** |
| `/Admin/PMTemplates` | PM Templates | Asset Maintenance | **PM Templates** |
| `/Admin/Inventory` | Stock Transactions | Stock Levels | **Stock Transactions** |
| `/Purchasing` | Purchase Orders | Procurement | **Purchase Orders** |
| `/Receiving` | Receiving | Goods Receiving | **Receiving** |
| `/UsTax` | US Tax (MACRS/179) | US Tax | **US Tax (MACRS/179)** |
| `/CCA` | Canadian CCA | CCA Classes | **Canadian CCA** |
| `/CIP/CostDetails` | Cost Analysis | Cost Entry - {name} | **Cost Analysis** |
| `/Reports/ReportHub` | Report Center | Reports | **Report Center** |
| `/Admin/GlAccounts` | Chart of Accounts | GL Accounts | **Chart of Accounts** |
| `/Admin/Technicians` | Technicians | Technician Management | **Technicians** |
| `/Admin` | Admin Hub | Administration | **Admin Hub** |
| `/Admin/Users` | Users & Roles | User Management | **Users & Roles** |
| `/Admin/Approvals` | Approvals | Approval Workflows | **Approvals** |
| `/Admin/Export` | File Export | File Export | *(OK - matches)* |

**Total H1 Changes Required:** 19 pages

---

## 5. Section Changes Summary

| Section | Change | Details |
|---------|--------|---------|
| Asset Maintenance | RENAME | → "Maintenance" (shorter) |
| Procurement | RENAME | → "Purchasing" (industry standard) |
| Finance & Tax | MERGE | + Projects (CIP) → "Finance" |
| Projects (CIP) | MERGE | → Into Finance section |
| Tools | MERGE | → Into Administration & Tools |
| Reports | EXPAND | + Tax forms (Form 4562, T2 Schedule 8) |

---

## 6. Migration Plan (Non-Breaking)

**Phase 2A: Label/H1 Alignment (This Sprint)**
1. Update 19 page H1 titles to match sidebar labels
2. No route changes
3. No nav structure changes

**Phase 2B: Section Reorganization (Future)**
1. Rename sidebar section headers
2. Move items between sections per IA above
3. Remove/merge Projects (CIP) and Tools sections

**Phase 2C: Legacy Cleanup (Future)**
1. Add legacy banners to duplicate pages
2. Implement redirects for legacy routes
3. Remove deprecated pages (after monitoring)

---

## 7. Rules of the Road

### Navigation Standards

1. **Sidebar label must match page H1**
   - The text shown in the sidebar menu must exactly match the H1 heading on the page
   - Example: Sidebar shows "Asset Register" → Page H1 must be "Asset Register"

2. **Avoid duplicate concepts**
   - Each label should represent one unique concept
   - Never have two nav items pointing to functionally equivalent pages
   - Example: "Work Orders" appears only once (under Maintenance)

3. **Legacy pages stay accessible but hidden**
   - Deprecated pages remain accessible by direct URL
   - Never delete routes that may have external links or bookmarks
   - Mark hidden items with `@* LEGACY (hidden): ... *@` comments in layout
   - Add legacy warning banner to deprecated pages

4. **New routes must be registered**
   - Any new page/route must be added to `/docs/RouteRegistry.md`
   - Include: Section, Sidebar Label, Route, Page Title, Auth, Status

### Code Standards

```razor
@* LEGACY (hidden): /Admin/Import retained for compatibility; not used in current app flow *@
```

### Page Banner for Legacy Pages

```html
<div class="alert alert-warning mb-4" style="...">
    <svg>...</svg>
    <span><strong>Legacy page:</strong> This page is not used in the current workflow. 
    Use <a href="/NewRoute">New Page Name</a> instead.</span>
</div>
```

---

## 8. Summary Metrics

| Metric | Count |
|--------|-------|
| Visible nav entries | 50 |
| Hidden legacy routes | 6 |
| H1 changes required | 19 |
| Section renames | 2 |
| Section merges | 2 |
| Item moves | 6 |

---

## Related Documents

- `/docs/RouteRegistry.md` - Complete route inventory with auth policies
- `/docs/NamingMap.md` - Terminology standardization
- `/docs/SeedCoverageMatrix.md` - Seed package coverage
