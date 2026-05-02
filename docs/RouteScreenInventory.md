# CherryAI EAM - Route & Screen Inventory

**Generated:** 2026-01-21  
**Commit:** `c8143e697ceddc65e07cf333aa9fb79be1c4ddcd`

## Status Legend
- ✅ Loads successfully
- ⚠️ Loads but empty/broken data
- ❌ Exception
- 🔐 Auth required (expected)

---

## CORE ASSET MANAGEMENT

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/` | Pages/Index.cshtml | Dashboard - system overview | ✅ | Shows 321 assets, 239 WOs |
| `/Assets` | Pages/Assets/Index.cshtml | Asset Register - list all assets | ✅ | Shows 321 assets, $87.9M total cost |
| `/Assets/Asset` | Pages/Assets/Asset.cshtml | Asset detail/create/edit | ✅ | Mode-aware (view/edit/create) |
| `/Assets/Transfer` | Pages/Assets/Transfer.cshtml | Asset transfers | ✅ | |
| `/Assets/Dispose` | Pages/Assets/Dispose.cshtml | Asset disposals | ✅ | |
| `/Assets/Improve` | Pages/Assets/Improve.cshtml | Capital improvements | ✅ | |
| `/Assets/Schedule` | Pages/Assets/Schedule.cshtml | Asset depreciation schedule | ✅ | |
| `/Assets/Delete` | Pages/Assets/Delete.cshtml | Delete asset | ✅ | |
| `/Inventory` | Pages/Inventory/Index.cshtml | Physical inventory hub | ✅ | |
| `/BulkOperations` | Pages/BulkOperations/Index.cshtml | Bulk asset operations | ✅ | |

## ASSET MAINTENANCE (Work Orders)

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Maintenance` | Pages/Maintenance/Index.cshtml | Work Orders dashboard | ✅ | Shows 239 WOs, 110 overdue |
| `/Maintenance/Details` | Pages/Maintenance/Details.cshtml | Work Order detail | ✅ | |
| `/Maintenance/Schedules` | Pages/Maintenance/Schedules.cshtml | Maintenance Schedules hub | ✅ | Empty - 0 PMTemplateAssets |
| `/Admin/PMTemplates` | Pages/Admin/PMTemplates.cshtml | PM Template management | 🔐 | Admin auth required |
| `/Admin/WorkOrders` | Pages/Admin/WorkOrders.cshtml | Work Order admin | 🔐 | Admin auth required |
| `/WorkOrders/Details` | Pages/WorkOrders/Details.cshtml | Work Order detail (alt) | ✅ | |

## FINANCE & DEPRECIATION

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Books` | Pages/Books/Index.cshtml | Depreciation Books | ✅ | Shows 2 books (GAAP, TAX) |
| `/Books/Create` | Pages/Books/Create.cshtml | Create depreciation book | ✅ | |
| `/Books/Details` | Pages/Books/Details.cshtml | Book details | ✅ | |
| `/Books/Edit` | Pages/Books/Edit.cshtml | Edit book | ✅ | |
| `/Books/GlAccounts` | Pages/Books/GlAccounts.cshtml | Book GL account mappings | ✅ | |
| `/Depreciation` | Pages/Reports/DepreciationPreview.cshtml | Depreciation preview | ✅ | |
| `/Journals` | Pages/Journals/Index.cshtml | Journal entries | ✅ | Empty |
| `/Journals/Generate` | Pages/Journals/Generate.cshtml | Generate journals | ✅ | |
| `/UsTax` | Pages/UsTax/Index.cshtml | US Tax settings | ✅ | |
| `/CCA` | Pages/CCA/Index.cshtml | Canadian CCA | ✅ | Shows 25 CCA classes |

## CAPITAL PROJECTS (CIP)

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/CIP` | Pages/CIP/Index.cshtml | CIP dashboard | ✅ | Empty - 0 projects |
| `/CIP/Details` | Pages/CIP/Details.cshtml | Project details | ✅ | |
| `/CIP/CostDetails` | Pages/CIP/CostDetails.cshtml | Cost breakdown | ✅ | |
| `/CIP/CostTypeDetails` | Pages/CIP/CostTypeDetails.cshtml | Cost type breakdown | ✅ | |

## PROCUREMENT

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Purchasing` | Pages/Purchasing/Index.cshtml | Purchase Orders | ✅ | Empty - 0 POs |
| `/Purchasing/Details` | Pages/Purchasing/Details.cshtml | PO details | ✅ | |
| `/Receiving` | Pages/Receiving/Index.cshtml | Goods receiving | ✅ | |
| `/AccountsPayable` | Pages/AccountsPayable/Index.cshtml | Accounts Payable | ✅ | |
| `/Admin/Requisitions` | Pages/Admin/Requisitions.cshtml | Purchase requisitions | 🔐 | Admin auth |

## INVENTORY & STORES

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Admin/Items` | Pages/Admin/Items.cshtml | Item Master | 🔐 | Empty - 0 items |
| `/Admin/ItemCategories` | Pages/Admin/ItemCategories.cshtml | Item categories | 🔐 | Empty |
| `/Admin/StockLevels` | Pages/Admin/StockLevels.cshtml | Stock level monitoring | 🔐 | |
| `/Admin/Inventory` | Pages/Admin/Inventory.cshtml | Inventory management | 🔐 | |
| `/Admin/Kits` | Pages/Admin/Kits.cshtml | Kit management | 🔐 | |
| `/Admin/Barcodes` | Pages/Admin/Barcodes.cshtml | Barcode system | 🔐 | |

## MASTER DATA - ORGANIZATION

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Admin/Sites` | Pages/Admin/Sites.cshtml | Facility sites | ✅ | Empty - 0 sites |
| `/Admin/Locations` | Pages/Admin/Locations.cshtml | Locations | 🔐 | 21 locations exist |
| `/Admin/Departments` | Pages/Admin/Departments.cshtml | Departments | 🔐 | Empty - 0 depts |
| `/Admin/CostCenters` | Pages/Admin/CostCenters.cshtml | Cost centers | 🔐 | Empty |
| `/Admin/AssetCategories` | Pages/Admin/AssetCategories.cshtml | Asset categories | ✅ | Empty - 0 categories |
| `/Admin/GlAccounts` | Pages/Admin/GlAccounts.cshtml | Chart of Accounts | ✅ | Shows 15 GL accounts |

## MASTER DATA - VENDORS & PEOPLE

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Admin/Vendors` | Pages/Admin/Vendors.cshtml | Vendor directory | ✅ | Shows 10 vendors |
| `/Admin/Technicians` | Pages/Admin/Technicians.cshtml | Technicians | 🔐 | 5 technicians exist |
| `/Admin/ProjectManagers` | Pages/Admin/ProjectManagers.cshtml | Project managers | 🔐 | Empty |
| `/Admin/Manufacturers` | Pages/Admin/Manufacturers.cshtml | Manufacturers | 🔐 | Empty |

## ADMINISTRATION

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Admin` | Pages/Admin/Index.cshtml | Admin hub | 🔐 | Admin auth required |
| `/Admin/Company` | Pages/Admin/Company.cshtml | Company settings | 🔐 | 3 companies |
| `/Admin/Users` | Pages/Admin/Users.cshtml | User management | 🔐 | 3 users |
| `/Admin/SystemSettings` | Pages/Admin/SystemSettings.cshtml | System settings | 🔐 | |
| `/Admin/Approvals` | Pages/Admin/Approvals.cshtml | Approval workflows | 🔐 | |
| `/Admin/AuditLog` | Pages/Admin/AuditLog.cshtml | Audit trail | 🔐 | Empty |
| `/Admin/ExchangeRates` | Pages/Admin/ExchangeRates.cshtml | Exchange rates | 🔐 | |
| `/Admin/SeedData` | Pages/Admin/SeedData.cshtml | Seed data utility | 🔐 | |
| `/Admin/Import` | Pages/Admin/Import.cshtml | Data import | 🔐 | |
| `/Admin/Export` | Pages/Admin/Export.cshtml | Data export | 🔐 | |
| `/Admin/Diagnostics` | Pages/Admin/Diagnostics.cshtml | System diagnostics | 🔐 | |

## REPORTS

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Reports/ReportHub` | Pages/Reports/ReportHub.cshtml | Report hub | ✅ | |
| `/Reports/Builder` | Pages/Reports/Builder.cshtml | Report builder | ✅ | |
| `/Reports/Compliance` | Pages/Reports/Compliance.cshtml | Compliance reports | ✅ | |
| `/Reports/Export` | Pages/Reports/Export.cshtml | Export reports | ✅ | |
| `/Reports/Form4562` | Pages/Reports/Form4562.cshtml | IRS Form 4562 | ✅ | |
| `/Reports/T2Schedule8` | Pages/Reports/T2Schedule8.cshtml | Canadian T2 Sch 8 | ✅ | |
| `/Reports/ChartOfAccounts` | Pages/Reports/ChartOfAccounts.cshtml | COA report | ✅ | |

## OTHER

| Route | Razor File | Description | Status | Notes |
|-------|-----------|-------------|--------|-------|
| `/Help` | Pages/Help/Index.cshtml | Help center | ✅ | |
| `/Help/Implementation` | Pages/Help/Implementation.cshtml | Implementation guide | ✅ | |
| `/Help/Tasks` | Pages/Help/Tasks.cshtml | Task guides | ✅ | |
| `/Help/Topic` | Pages/Help/Topic.cshtml | Topic viewer | ✅ | |
| `/AI` | Pages/AI/Index.cshtml | AI Assistant | ✅ | |
| `/API` | Pages/API/Index.cshtml | API documentation | ✅ | |
| `/API/Import` | Pages/API/Import.cshtml | API import | ✅ | |
| `/Account/Login` | Pages/Account/Login.cshtml | Login page | ✅ | |
| `/Account/Logout` | Pages/Account/Logout.cshtml | Logout | ✅ | |

---

## SUMMARY

| Category | Total Routes | ✅ Working | ⚠️ Broken Data | ❌ Error | 🔐 Auth |
|----------|-------------|-----------|----------------|---------|---------|
| Core Assets | 10 | 10 | 0 | 0 | 0 |
| Maintenance | 6 | 4 | 0 | 0 | 2 |
| Finance | 10 | 10 | 0 | 0 | 0 |
| CIP | 4 | 4 | 0 | 0 | 0 |
| Procurement | 5 | 3 | 0 | 0 | 2 |
| Inventory | 6 | 0 | 0 | 0 | 6 |
| Master Data Org | 6 | 3 | 0 | 0 | 3 |
| Master Data People | 4 | 1 | 0 | 0 | 3 |
| Administration | 12 | 0 | 0 | 0 | 12 |
| Reports | 7 | 7 | 0 | 0 | 0 |
| Other | 9 | 9 | 0 | 0 | 0 |
| **TOTAL** | **79** | **51** | **0** | **0** | **28** |

## KEY FINDINGS

1. **No route exceptions found** - All pages load without 500 errors
2. **No 404 errors found** - All sidebar links resolve to valid pages
3. **Auth protection working** - Admin pages correctly require authentication
4. **Empty state handling** - All pages show proper empty states when data is missing

## EMPTY DATA CONCERNS

The following screens load but show empty due to missing master data:
- Sites (0 records)
- Departments (0 records)
- Cost Centers (0 records)
- Asset Categories (0 records)
- PM Templates (0 records)
- Maintenance Schedules (0 PMTemplateAssets)
- CIP Projects (0 records)
- Items (0 records)
- Manufacturers (0 records)
- Project Managers (0 records)

**Action Required:** Seed system reference data and customer master data per Phase 3.
