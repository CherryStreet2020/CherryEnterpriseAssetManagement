# Navigation Release Notes

**Latest Update:** 2026-01-24 (Premium DataGrid v3.0 Rollout)  
**Document Version:** 2.4

*This document tracks all navigation and routing changes across sprints.*

---

## 2026-01-24: Premium DataGrid v3.0 Rollout

### Summary

Premium DataGrid v3.0 rolled out to all core modules. Row-click navigation standardized using server-generated URLs via `Url.Page()`. Actions columns removed from grid layouts where row-click navigation is enabled.

### Pages Updated

| Page | Grid ID | Changes |
|------|---------|---------|
| Assets/Index | assetGrid | Already conformant |
| Maintenance/Index | maintenanceGrid | Removed rowClickUrl, added columnVisibility |
| Maintenance/Schedules | pmSchedulesGrid | Removed Actions column, enabled clickableRows |
| Materials/Items | itemGrid | Added data-col/data-filter, data-row-href |
| Inventory/Index | inventoryGrid | Added grid attributes, data-row-href |
| Journals/Index | journalsGrid | Removed Actions column, added data-row-href |
| CIP/Index | cipGrid | Updated to data-row-href pattern |
| Purchasing/Index | poGrid | Added grid attributes, data-row-href |
| AccountsPayable/Index | invoiceGrid | Added grid attributes, data-row-href |
| Admin/PMTemplates | pmTemplatesGrid | Added data-row-href pattern |
| Admin/WorkOrders | workOrdersTable | Added data-row-href pattern |

### Key Changes

1. **Routing-Safe Navigation:** All list pages now use `data-row-href="@Url.Page(...)"` instead of client-side URL building
2. **Actions Columns Removed:** View/Edit buttons replaced by row-click navigation
3. **returnUrl Pattern:** Standard pattern for back navigation: `@{ var returnUrl = $"{HttpContext.Request.Path}{HttpContext.Request.QueryString}"; }`
4. **Column Visibility:** Added `columnVisibility: true` to enhanced grid configurations
5. **New Smoke Test:** "DataGrid → Row Href Targets Accept Id" validates target pages accept id parameter

### Guardrail Tests Added

- **"DataGrid → Row Href Targets Accept Id"** - Validates target pages referenced in data-row-href can accept the id parameter via route segment or query binding

---

## 2026-01-24: Comprehensive Navigation Audit

### Summary

Completed a full end-to-end navigation audit covering sidebar structure, route correctness, intra-screen links, and field labels. Added automated guardrail tests to prevent future navigation drift.

### Audit Scope

| Category | Count | Status |
|----------|-------|--------|
| Sidebar Sections | 8 | All verified |
| Sidebar Links | 58 | All resolve to existing pages |
| Razor Pages | 119 | Enumerated |
| asp-page Usages | 65 | All valid |
| href Usages | 403 | Audited |

### Guardrail Tests Added

1. **"Navigation → All Sidebar Links Resolve"**
   - Scans `_ModernLayout.cshtml` for `href="/..."` patterns
   - Verifies each route resolves to an existing Razor Page file
   - Excludes static assets (css, js, images)

2. **"Navigation → Intra-Screen asp-page Targets Valid"**
   - Scans all Razor Pages for `asp-page="/..."` attributes
   - Verifies each target page exists in the Pages directory
   - Reports broken links with source file context

### Documentation Updated

- `docs/NavigationAuditReport.md` - Full audit findings with sidebar inventory
- `docs/RouteRegistry.md` - Marked as CANONICAL SOURCE OF TRUTH
- `docs/DECISION_LOG.md` - Added navigation audit decision entry

### No Fixes Required

All 58 sidebar links, 65 asp-page usages, and field labels verified correct. The existing navigation was already compliant with the audit requirements.

---

## Sprint 16 Changes (2026-01-23)

### World-Class IA Restructure
- **Workflow-first navigation:** Work Execution OS is the primary mental model
- **2-click rule:** Every Phase 1 operational screen reachable in ≤2 clicks
- **Cognitive load reduction:** Operational up top, back office/reports lower
- **Premium clean design:** No chatty hints, consistent visual density

### Quick Actions (5 items)
- **Dashboard** - Direct access to main dashboard
- **Work Orders (Cockpit)** - Opens work order list with execution cockpit
- **Work Requests** - Self-service work request submission  
- **PM Schedules** - Preventive maintenance scheduling
- **Capital Projects (CIP)** - Direct access to capital improvement projects

### Work Execution Section (renamed from Maintenance)
- **Title:** "Work Execution" (was "Maintenance")
- **Subtitle:** "Requests, planning, execution"
- **Menu items:** Work Orders (Cockpit), Work Requests, Create Work Request, PM Schedules, PM Assignments
- **Data-step attributes:** Execute, Request, Plan, Dispatch for workflow guidance
- **Auto-expansion:** Section expands when on `/Maintenance/*` routes

### Section Auto-Expansion
- **Assets:** Expands on `/Assets/*` or `/Inventory/*` pages
- **Inventory & Stores:** Expands on `/Materials/*` pages
- **Finance:** Expands on `/Books`, `/Journals`, `/UsTax`, `/CCA`, `/CIP` routes
- **Work Execution:** Expands on `/Maintenance/*` pages

### Finance Section
- **CIP first-class visibility:** Capital Projects (CIP) prominent in Finance group
- **Labels clarified:** "Capital Projects (CIP)" and "CIP Cost Analysis"

### Purchasing Section
- **Data-section:** Uses "procurement" for CSS alignment

### Active State Logic
- **Introduced helper functions:** `IsPage()`, `IsExactPage()`, `IsAny()` for cleaner active state logic
- **Fixed Work Orders active state:** Matches `/Maintenance/Index` and `/Maintenance/Details`
- **Fixed Work Requests active state:** Excludes `/Maintenance/WorkRequests/Create` from list active
- **Dashboard:** Uses `IsExactPage("/Index")` for exact matching

### Technical Implementation
- Helper functions centralized at top of `_ModernLayout.cshtml` code block
- Server-side `expanded` class applied based on route matching
- No inline styles - all styling via CSS classes
- PM Templates remains at `/Admin/PMTemplates` in Admin section

---

## Sprint 15 Changes (2026-01-22)

### What Changed

### Information Architecture
- Consolidated from 12+ sections → **9 primary sections**
- Sections: Assets, Maintenance, Inventory & Stores, Purchasing, Finance, Reports, Setup, Administration & Tools, Help Center
- Removed duplicate/redundant menu entries
- All sidebar labels now match page H1 titles

### Label Standardization
- Asset workflow links renamed: "Transfer Asset", "Dispose Asset", "Improve Asset"
- Finance: "Depreciation Reports" (was "Run Depreciation") → points to Report Center
- Admin: "Master Data Import", "File Export" (consistent naming)
- Inventory: Standardized on "Item Master" terminology

### Route Pattern Changes
- Asset actions now use query params: `/Assets?action=transfer|dispose|improve`
- CIP cost analysis: `/CIP/Costs` (was `/CIP/CostDetails`)
- Tax forms moved to Reports section (routes unchanged)

---

## What Stayed Compatible

### Preserved Legacy Routes
| Legacy Route | Status | Notes |
|--------------|--------|-------|
| `/Assets/Transfer` | Works | Query param pattern preferred |
| `/Assets/Dispose` | Works | Query param pattern preferred |
| `/Assets/Improve` | Works | Query param pattern preferred |
| `/Reports/Index` | 301 → `/Reports/ReportHub` | Permanent redirect |
| `/WorkOrders/Details` | 302 → `/Maintenance` | Temporary redirect |
| `/Admin/WorkOrders` | Works | Yellow warning banner added |
| `/Admin/Diagnostics` | Works | Yellow warning banner added |

### Backward Compatibility
- All existing bookmarks continue to work
- No database schema changes
- No route deletions

---

## Security Changes

### Admin Role Enforcement
All `/Admin/*` pages now require authentication:
- **Admin role required:** Users, SystemSettings, Company, Sites, Locations, etc.
- **Admin or Accountant:** Items, StockLevels, Vendors, Requisitions, PMTemplates, etc.
- Unauthenticated requests → 302 redirect to `/Account/Login`

### Work Order Multi-Tenant Isolation (2026-01-23)
Work Orders (List/Details/Schedules/Assignments) now require auth and enforce tenant/company scoping:
- **Pages hardened:** `/Maintenance/Index`, `/Maintenance/Details`, `/Maintenance/Schedules`, `/Maintenance/Assignments`
- **Services hardened:** `MaintenanceService`, `CloseoutService`
- **Tenant isolation:** All queries filter by `Asset.CompanyId` via `ITenantContext`
- **Authorization:** All Work Order page models require `[Authorize]`
- **Mutation protection:** `CreateEventAsync`, `UpdateEventAsync`, `CompleteEventAsync` validate asset ownership
- **POST validation:** Work Order creation validates asset belongs to tenant company
- **SQL-side filtering:** `GetEventsForDashboardAsync` applies filters in SQL, not memory
- **Smoke test:** Test #42 validates Work Order company scope isolation via MaintenanceService

---

## Known Legacy URLs

| Legacy URL | Modern Replacement | Action |
|------------|-------------------|--------|
| `/Reports/Index` | `/Reports/ReportHub` | Auto-redirect |
| `/Depreciation` | `/Books` + `/Reports/ReportHub` | Split functionality |
| `/Admin/WorkOrders` | `/Maintenance` | Deprecated (banner shown) |
| `/Admin/Diagnostics` | `/Admin` | Deprecated (banner shown) |
| `/CIP/CostDetails?id=X` | `/CIP/Costs` | Hub page replaces parameterized route |

---

## How to Test Navigation (Zero-404 Sweep)

### Quick Verification Checklist
```bash
# 1. Extract all sidebar hrefs
grep -o 'href="[^"]*"' Pages/Shared/_ModernLayout.cshtml | grep -v '@' | sort -u

# 2. Test each route
curl -sI "http://localhost:5000/ROUTE" | head -1

# 3. Expected results:
#    - 200 OK = Page loads
#    - 302 Found = Auth redirect (expected for /Admin/*)
#    - 404 Not Found = BROKEN (fix required)
```

### Full Test Script
```bash
routes=("/Assets" "/Maintenance" "/Purchasing" "/Books" "/Reports/ReportHub" "/Help" "/Admin")
for r in "${routes[@]}"; do
  echo "$r: $(curl -sI http://localhost:5000$r | head -1 | awk '{print $2}')"
done
```

### Verification Criteria
- [ ] All 55 sidebar links return 200 or 302
- [ ] Zero 404 responses
- [ ] Sidebar label matches page H1
- [ ] Breadcrumb leaf matches sidebar label
- [ ] Admin pages redirect when not logged in

---

## Files Modified

| File | Purpose |
|------|---------|
| `Pages/Shared/_ModernLayout.cshtml` | Sidebar navigation |
| `docs/RouteRegistry.md` | Route documentation |
| `docs/NavigationAudit.md` | IA documentation |
| `Helpers/UiTerms.cs` | UI terminology constants |

---

## Sprint 7 Closeout: PM Templates UX Hardening (2026-01-22)

### PM Templates: New Full-Page Experience

**What Changed:**
- PM Templates now uses full-page create/edit instead of modals
- New unified page at `/Admin/PMTemplateEdit/{id?}`:
  - Create mode: `/Admin/PMTemplateEdit` (no ID)
  - Edit mode: `/Admin/PMTemplateEdit/123` (with ID)
- Form organized into sections: Basic Information, Scheduling, Cost Estimates, Safety Requirements
- Toggle switches for boolean fields (Requires Shutdown, Requires LOTO, Active)
- Sidebar with Status card and Danger Zone (delete) in edit mode
- Dynamic trigger field visibility (Calendar/Meter/Both/Manual)

**Issues Resolved:**
- Fixed double-modal bug when opening templates
- Fixed translucent/ghost card styling
- Fixed "Requires" text clipping (appeared as "PRQUIRES")
- Improved section layout and spacing

**Routes:**
| Route | Purpose |
|-------|---------|
| `/Admin/PMTemplates` | List page with grid |
| `/Admin/PMTemplateEdit` | Create new template |
| `/Admin/PMTemplateEdit/{id}` | Edit existing template |

**Files Modified:**
| File | Change |
|------|--------|
| `Pages/Admin/PMTemplateEdit.cshtml` | New full-page create/edit view |
| `Pages/Admin/PMTemplateEdit.cshtml.cs` | Page model with CRUD handlers |
| `Pages/Admin/PMTemplates.cshtml` | Updated list page, removed modals |
| `Pages/Admin/PMTemplates.cshtml.cs` | Removed obsolete modal handlers |
| `replit.md` | Updated UI/UX documentation |

---

## Sprint 8: Work Orders UX + Data Clarity Hardening

**Date:** 2026-01-22  
**Scope:** Work order list and details UX improvements, origin classification, empty states

### Work Orders List Improvements

| Before | After |
|--------|-------|
| No source/origin indication | **Source column** with badges: Smart Assist / PM Schedule / Manual |
| Generic empty state | Context-aware empty state with filter reset action |
| Inconsistent column order | Improved scan-ability: WO# → Status → Priority → Source → Asset → Description |
| No unassigned asset styling | Unassigned assets shown with italic gray "Unassigned" text |

### Work Order Details Improvements

| Before | After |
|--------|-------|
| No origin indication | **Origin badge** in hero tags (Smart Assist / PM Schedule / Manual) |
| Generic operations empty state | Context-aware message based on WO origin |
| No Parts section | **Parts / Materials section** with table or empty state |
| No Labor section | **Labor / Time section** with data or empty state |

### Origin Classification Logic

Work order origin is determined heuristically (no schema changes):

| Origin | Detection Logic |
|--------|----------------|
| **Smart Assist** | WorkRequest exists with `GeneratedWorkOrderId` pointing to this WO and `IsAIAssisted=true` |
| **PM Schedule** | `CustomField1` starts with "PMTA:" OR (`Type=Preventative` AND `RecurrenceIntervalDays > 0`) |
| **Manual** | Everything else |

### New Smoke Tests

| Test # | Name | Description |
|--------|------|-------------|
| #11 | Work Order Origin Classification | Verifies Smart Assist, PM, and Manual WOs are classified correctly |
| #12 | Work Order Details Empty States | Verifies WO with zero operations/parts/labor renders without errors |

### Files Modified

| File | Change |
|------|--------|
| `Services/Maintenance/WorkOrderOriginService.cs` | NEW: Origin classification service |
| `Pages/Maintenance/Index.cshtml` | Origin badges, improved columns, filter-aware empty state |
| `Pages/Maintenance/Index.cshtml.cs` | Origin service integration, origin filters |
| `Pages/Maintenance/Details.cshtml` | Origin badge, Parts/Labor sections, improved empty states |
| `Pages/Maintenance/Details.cshtml.cs` | Origin service integration, Parts loading |
| `Services/Testing/SmokeTestRunner.cs` | Added tests #11 and #12 |
| `Program.cs` | Registered WorkOrderOriginService |
| `docs/DECISION_LOG.md` | Sprint 8 decisions documented |

---

## Sprint 11: Item Master Cross-Reference v1 (2026-01-22)

### New Routes
| Route | Purpose |
|-------|---------|
| `/Materials/Items` | Item Master list with cross-reference search |
| `/Materials/ItemEdit/{id?}` | Item create/edit with revision and cross-ref management |

### New Features
- Three-way part number resolution (Internal PN → MPN → VPN)
- Item revision control (Draft/Released/Obsolete lifecycle)
- Manufacturer Part Number (MPN) management
- Vendor Part Number (VPN) management with optional MPN linking
- Search across all part number types with optional vendor filter

### Files Changed
| File | Change |
|------|--------|
| `Models/Manufacturer.cs` | Added Code, TenantId for cross-reference support |
| `Models/Revisions/ItemRevisionEnhanced.cs` | Full revision lifecycle pattern |
| `Models/ItemManufacturerPart.cs` | NEW: MPN cross-reference entity |
| `Models/VendorItemPart.cs` | NEW: VPN cross-reference entity |
| `Services/Items/ItemRevisionService.cs` | NEW: Revision lifecycle management |
| `Services/Items/ItemCrossReferenceService.cs` | NEW: Resolution and search |
| `Pages/Materials/Items.cshtml` | NEW: Item list with search |
| `Pages/Materials/ItemEdit.cshtml` | NEW: Item edit with revision tabs |
| `Services/Testing/SmokeTestRunner.cs` | Added tests #21, #22, #23 |
| `docs/Architecture/ItemCrossReference.md` | NEW: Architecture documentation |

---

## Sprint 12: Procurement-Grade Parts v1 (2026-01-22)

### New Features
- **Approved Vendor List (AVL)** - Track approved vendors per item with preferred vendor selection
- **Alternates/Substitutes** - Define substitute items with ranking and approval status
- **Supersession Chains** - Track item replacement chains with cycle prevention

### UI Updates
| Route | Changes |
|-------|---------|
| `/Materials/ItemEdit/{id?}` | Added 3 new tabs: Approved Vendors, Alternates, Supersession |

### New Entities
| Entity | Purpose |
|--------|---------|
| ItemApprovedVendor | AVL entries with approval status |
| ItemAlternate | Alternate items with ranking |
| ItemSupersession | Old->New item replacement chains |

### New Services
| Service | Purpose |
|---------|---------|
| IItemSourcingService | AVL management, preferred vendor enforcement |
| IItemAlternateService | Alternates management, best alternate selection |
| IItemSupersessionService | Supersession chains, cycle prevention |

### Files Changed
| File | Change |
|------|--------|
| `Models/ItemApprovedVendor.cs` | NEW: AVL entity |
| `Models/ItemAlternate.cs` | NEW: Alternates entity |
| `Models/ItemSupersession.cs` | NEW: Supersession entity |
| `Services/Items/ItemSourcingService.cs` | NEW: AVL service |
| `Services/Items/ItemAlternateService.cs` | NEW: Alternates service |
| `Services/Items/ItemSupersessionService.cs` | NEW: Supersession service |
| `Pages/Materials/ItemEdit.cshtml` | Added AVL, Alternates, Supersession tabs |
| `Pages/Materials/ItemEdit.cshtml.cs` | Added handlers for new tabs |
| `Services/Testing/SmokeTestRunner.cs` | Added tests #24, #25, #26 |
| `docs/Architecture/ItemSourcing.md` | NEW: Architecture documentation |

---

## Sprint 14: Item Master UI Hydration (2026-01-22)

### New Features
- **Sourcing Summary Card** - Read-only derived fields on Item Edit page
- Fields displayed: Rev, Manufacturer, Mfr Part #, Primary Vendor, Vendor Part #
- Navigation hints linking to source tabs (Revisions, Manufacturer Parts, Vendor Parts, Approved Vendors)

### Files Changed
| File | Change |
|------|--------|
| `Pages/Materials/ItemEdit.cshtml` | Added Sourcing Summary card |
| `Pages/Materials/ItemEdit.cshtml.cs` | Added summary DTO and service calls |
| `Services/Items/ItemSourcingService.cs` | Added GetSourcingSummaryAsync |
| `wwwroot/css/modern.css` | Added form-control-static styling |

---

## Sprint 15: Navigation Unification v1 (2026-01-22)

### Summary
Consolidated navigation paths for Item Master, eliminating "old modal vs new full page" confusion.

### Canonical Routes Established
| Feature | Canonical Route | Description |
|---------|-----------------|-------------|
| Item Master List | `/Materials/Items` | Full list view with Materials Management context |
| Item Edit/Create | `/Materials/ItemEdit/{id?}` | Full-page editor with tabs |

### Legacy Routes Preserved (Redirected)
| Legacy Route | Redirects To | Notes |
|--------------|--------------|-------|
| `/Admin/Items` | `/Materials/Items` | 302 redirect, query params preserved |

### Menu Updates
- **Inventory & Stores > Item Master** now points to `/Materials/Items`
- Sidebar "active" state updated to detect `/Materials/Item*` paths

### UI Consistency
- All item edits now use full-page editor (no modal popups)
- Row clicks on item list navigate to `/Materials/ItemEdit/{id}`
- "Add Item" button navigates to `/Materials/ItemEdit` (no id = create mode)

### Files Changed
| File | Change |
|------|--------|
| `Pages/Shared/_ModernLayout.cshtml` | Sidebar link updated to /Materials/Items |
| `Pages/Admin/Items.cshtml.cs` | OnGet now redirects to canonical route |
| `Pages/Admin/Items.cshtml` | Row click and Add button use full-page navigation |
| `docs/NavigationMap.md` | NEW: Route status documentation |
| `docs/NavigationReleaseNotes.md` | Updated (this file)

---

## Sprint 16: Tenant Stamping Hardening (2026-01-22)

### Infrastructure Improvements

**SmokeTestDataFactory**
- New centralized factory for creating test data with automatic tenant stamping
- Tenant-scoped entities (ItemApprovedVendor, ItemAlternate, ItemSupersession) automatically stamped with TenantId
- Non-tenant-scoped entities (VendorItemPart, Vendor, Item, Manufacturer) created without TenantId as they don't require it
- Eliminates scattered `TenantId = _tenantContext.TenantId ?? 1` patterns

**Tenant Stamping Guard Test**
- New smoke test: "Tenant Stamping Guard → TenantId required on tenant-scoped entities"
- Verifies that entities with wrong TenantId are not visible via tenant-scoped queries
- Ensures factory-created entities with correct TenantId are visible

**Migration Bootstrap**
- Smoke test bootstrap now applies `MigrateAsync()` in Development/LAB environments
- Prevents schema drift from causing spurious test failures

### Files Changed
| File | Change |
|------|--------|
| `Services/Testing/SmokeTestDataFactory.cs` | NEW: Centralized test data factory |
| `Services/Testing/SmokeTestRunner.cs` | Refactored to use factory; added guard test; added MigrateAsync |
| `Program.cs` | Registered ISmokeTestDataFactory |
| `docs/Architecture/ItemSourcing.md` | Added "Tenant Scope" section |
| `docs/NavigationReleaseNotes.md` | Updated (this file)

---

## Sprint 1: Work Request/Work Order Improvements

**Date:** 2026-01-22

### Changes

- **Canonical WO Link Fix:** Work Request conversion now redirects to `/Maintenance/Details/{id}` (canonical route) instead of non-canonical `/Maintenance/WorkOrders/Details` variants
- **Company Scoping:** Work Requests pages (Index, Details, Create) now use `ITenantContext.CompanyId` for company-scoped queries and validation
- **Conversion Service Hardening:**
  - Idempotent: Re-calling conversion returns existing WorkOrder (no duplicates)
  - Atomic transaction: All-or-nothing conversion
  - Company guard: Validates CompanyId before any operations
  - Reload-before-check pattern: Prevents stale data issues
  - Blocks broken converted state (Status=ConvertedToWO without GeneratedWorkOrderId)
- **New Smoke Tests:**
  - Test #40: WorkRequest Conversion → Idempotent (No Duplicate WorkOrders)
  - Test #41: WorkRequests → Company Scoped Query (No Cross-Company Leakage)

### Files Changed (Code)

| File | Change |
|------|--------|
| `Pages/Maintenance/WorkRequests/Index.cshtml` | Canonical WO link fix |
| `Pages/Maintenance/WorkRequests/Details.cshtml` | Canonical WO link fix |
| `Pages/Maintenance/WorkRequests/Details.cshtml.cs` | ITenantContext injection, company scoping |
| `Pages/Maintenance/WorkRequests/Create.cshtml.cs` | ITenantContext injection, company scoping |
| `Services/Maintenance/WorkRequestConversionService.cs` | Idempotency, atomic transaction, company guard |
| `Services/Testing/SmokeTestRunner.cs` | Added Test #40 and #41 |

### Files Changed (Docs)

| File | Change |
|------|--------|
| `docs/RouteRegistry.md` | Added Work Requests routes |
| `docs/NavigationMap.md` | Fixed Page File paths for Work Requests |
| `docs/NavigationReleaseNotes.md` | Added Sprint 1 entry (this section)
