# UX & Seed Data Inventory Report

**Date:** January 2026  
**Status:** Inventory Complete (No Code Changes)  
**Scope:** Full Pages/** scan + Seeding Pipeline analysis

---

## TASK A: UX ALIGNMENT INVENTORY

### 1. Layout Usage Analysis

| Layout | Page Count | Notes |
|--------|------------|-------|
| `_ModernLayout` (explicit) | 0 | All via `_ViewStart.cshtml` inheritance |
| `_ModernLayout` (effective) | ~120 | Correct - inherited from `_ViewStart` |
| Auth pages (minimal) | 3 | Login, Logout, AccessDenied |
| Error pages (simple) | 1 | Error.cshtml |

**Finding:** Layout usage is consistent. All operational pages correctly inherit `_ModernLayout` via `_ViewStart.cshtml`.

---

### 2. Hero Pattern Analysis

| Pattern | Usage Count | Pages Using |
|---------|-------------|-------------|
| `class="page-hero"` | **97 pages** | All list/index pages, most detail pages |
| `class="hero-section"` | **0 pages** | Not used anywhere |
| `class="premium-hero"` | **0 pages** | Not used anywhere |

**Finding:** ✅ Consistent - all pages use `page-hero` class. No `hero-section` or `premium-hero` variants exist despite docs mentioning them.

**ADR/Docs Contradiction:** `docs/UXStandards.md` may reference `.hero-section` but codebase uses `.page-hero` exclusively.

---

### 3. Duplicate Title Detection

| Page | Issue |
|------|-------|
| `Pages/Assets/Asset.cshtml` | 2 H1 tags (hero + content) |
| `Pages/AccountsPayable/Details.cshtml` | 2 H1 tags |
| `Pages/Materials/ItemEdit.cshtml` | 2 H1 tags |
| `Pages/Index.cshtml` | 2 H1 tags (dashboard) |

**Finding:** ⚠️ 4 pages have duplicate H1 tags. Dashboard is intentional (hero + welcome section), but detail pages should consolidate.

---

### 4. Tab Implementation Patterns

| Tab Pattern | Usage | Files |
|-------------|-------|-------|
| `.premium-tabs` / `.premium-tab-nav` | PM Templates, Items | Materials/ItemEdit.cshtml, Admin/Outbox |
| `.wo-tabs-nav` / `.wo-tab-link` | Work Order Details | WorkOrders/Details.cshtml |
| `.tabs` / `.tab-btn` | Generic tabs | Assets/Asset.cshtml, Maintenance/Details.cshtml |
| `.report-tabs` | Reports | Reports/Compliance.cshtml |

**Finding:** ⚠️ **4 different tab implementations** exist. This violates DRY and causes inconsistent appearance.

**Recommendation:** Create `_TabNav.cshtml` partial with single tab implementation.

---

### 5. Empty State Analysis

| Pattern | Usage | Files |
|---------|-------|-------|
| `_EmptyState.cshtml` partial | **2 pages only** | Schedules.cshtml, Assignments/Index.cshtml |
| Inline empty markup | **18+ pages** | Most list pages use inline |

**Finding:** ⚠️ Most pages use inline empty states instead of the shared partial.

**Examples of inline empty states:**
- `Pages/Assets/Index.cshtml` - "No assets found"
- `Pages/Maintenance/Index.cshtml` - "No maintenance events"
- `Pages/Inventory/Index.cshtml` - "No inventory records"

---

### 6. Inline Styles Audit (ADR-004 Violations)

**Total pages with `style=` attributes:** 98

| Severity | Page | Inline Styles Count |
|----------|------|---------------------|
| 🔴 Critical | `Materials/ItemEdit.cshtml` | 177 |
| 🔴 Critical | `Assets/Asset.cshtml` | 154 |
| 🔴 Critical | `WorkOrders/Details.cshtml` | 104 |
| 🔴 Critical | `Admin/Items.cshtml` | 90 |
| 🔴 Critical | `Index.cshtml` | 83 |
| 🟠 High | `Assets/_AssetMesIotSafetyTabs.cshtml` | 78 |
| 🟠 High | `Reports/T2Schedule8.cshtml` | 59 |
| 🟠 High | `Admin/Diagnostics.cshtml` | 55 |
| 🟠 High | `Admin/DemoData.cshtml` | 54 |
| 🟠 High | `Maintenance/Index.cshtml` | 52 |
| 🟠 High | `Reports/Form4562.cshtml` | 46 |
| 🟡 Medium | `CCA/ClassReport.cshtml` | 41 |
| 🟡 Medium | `Materials/Items.cshtml` | 40 |
| 🟡 Medium | `Admin/Inventory.cshtml` | 40 |

**`<style>` Block Violations:**
| File | Status |
|------|--------|
| `Pages/Shared/_AssetMaintenanceHeader.cshtml` | ⚠️ Contains `<style>` block (not allowlisted) |
| `Pages/Shared/_BackLink.cshtml` | ⚠️ Contains `<style>` block (not allowlisted) |
| `Pages/Shared/_ModernLayout.cshtml` | ✅ Allowlisted (layout) |

---

### 7. DataGrid Contract Compliance (ADR-005)

**Pages with `data-enhanced-grid="true"`:** 15 ✅

| Page | Status |
|------|--------|
| AccountsPayable/Index.cshtml | ✅ Compliant |
| Admin/Items.cshtml | ✅ Compliant |
| Admin/PMTemplates.cshtml | ✅ Compliant |
| Admin/Vendors.cshtml | ✅ Compliant |
| Admin/WorkOrders.cshtml | ✅ Compliant |
| Assets/Index.cshtml | ✅ Compliant |
| CIP/Index.cshtml | ✅ Compliant |
| Inventory/Index.cshtml | ✅ Compliant |
| Journals/Index.cshtml | ✅ Compliant |
| Maintenance/Assignments/Index.cshtml | ✅ Compliant |
| Maintenance/Index.cshtml | ✅ Compliant |
| Maintenance/Schedules.cshtml | ✅ Compliant |
| Maintenance/WorkRequests/Index.cshtml | ✅ Compliant |
| Materials/Items.cshtml | ✅ Compliant |
| Purchasing/Index.cshtml | ✅ Compliant |

**Tables Missing `data-enhanced-grid`:** 20+

| Page | Table Type | Action Needed |
|------|------------|---------------|
| `Books/Index.cshtml` | List page | 🔴 Needs EnhancedGrid |
| `BulkOperations/Index.cshtml` | List page | 🔴 Needs EnhancedGrid |
| `BulkOperations/Details.cshtml` | Detail | 🟡 Evaluate |
| `CCA/Index.cshtml` | List page | 🔴 Needs EnhancedGrid |
| `CCA/ClassReport.cshtml` | Report | ⚪ Allowlist |
| `CIP/Details.cshtml` | Detail | 🟡 Evaluate |
| `CIP/Costs.cshtml` | Sub-list | 🔴 Needs EnhancedGrid |
| `CIP/CostTypeDetails.cshtml` | Detail | 🟡 Evaluate |
| `Inventory/List.cshtml` | List page | 🔴 Needs EnhancedGrid |
| `Maintenance/Details.cshtml` | Detail | ⚪ Allowlisted |
| `Receiving/Index.cshtml` | List page | 🔴 **Missing** (Not Premium) |
| `Reports/*` | Reports | ⚪ Allowlist (reports) |
| `UsTax/Index.cshtml` | Reference | ⚪ Allowlist |

---

### 8. KPI Placement Analysis

**Pages with KPI patterns:** 92

All operational pages use `.kpi-card` within `.page-hero-kpis`. Pattern is consistent.

**Finding:** ✅ KPI placement is consistent across modules.

---

### 9. Module-by-Module Page Inventory

| Module | Pages | DataGrid Status | Hero | Style Issues |
|--------|-------|-----------------|------|--------------|
| **Account** | 3 | N/A (auth) | N/A | ✅ |
| **AccountsPayable** | 2 | ✅ Premium | ✅ | Low |
| **Admin** | 45 | Partial | ✅ | Medium |
| **AI** | 1 | N/A | ✅ | Low |
| **API** | 2 | N/A | ✅ | Low |
| **Assets** | 9 | ✅ Premium | ✅ | 🔴 High (154) |
| **Books** | 6 | ❌ Missing | ✅ | Medium |
| **BulkOperations** | 2 | ❌ Missing | ✅ | Low |
| **CCA** | 2 | ❌ Missing | ✅ | Medium |
| **CIP** | 5 | Partial | ✅ | Low |
| **Help** | 4 | N/A (docs) | ✅ | ⚪ Allowlisted |
| **Inventory** | 2 | Partial | ✅ | Low |
| **Journals** | 3 | ✅ Premium | ✅ | Low |
| **Maintenance** | 8 | ✅ Premium | ✅ | 🟠 High (52) |
| **Materials** | 2 | ✅ Premium | ✅ | 🔴 Critical (177+40) |
| **Purchasing** | 2 | ✅ Premium | ✅ | Low |
| **Receiving** | 1 | ❌ Missing | ✅ | Low |
| **Reports** | 9 | ⚪ Reports | ✅ | Medium |
| **UsTax** | 1 | ⚪ Reference | ✅ | Low |
| **WorkOrders** | 1 | ⚪ Detail | ✅ | 🔴 Critical (104) |

---

## TASK A SUMMARY: Prioritized Punch List

### Tier 1: Fix Once in Shared Partials/CSS (High Impact)

| Item | Fix Location | Pages Affected |
|------|--------------|----------------|
| 1. Create `_TabNav.cshtml` partial | `Pages/Shared/` | 7+ pages |
| 2. Migrate inline tab styles to `tabs.css` | `wwwroot/css/modules/` | 7+ pages |
| 3. Move `<style>` from `_BackLink.cshtml` to CSS | `wwwroot/css/` | All pages using partial |
| 4. Move `<style>` from `_AssetMaintenanceHeader.cshtml` to CSS | `wwwroot/css/modules/` | Asset/Maintenance pages |
| 5. Expand `_EmptyState.cshtml` usage | Propagate partial | 18+ pages |

### Tier 2: Must Touch Many Pages (Medium Impact)

| Item | Pages to Update | Effort |
|------|-----------------|--------|
| 1. Add EnhancedGrid to Books/Index.cshtml | 1 | Low |
| 2. Add EnhancedGrid to CCA/Index.cshtml | 1 | Low |
| 3. Add EnhancedGrid to Receiving/Index.cshtml | 1 | Low |
| 4. Add EnhancedGrid to BulkOperations/Index.cshtml | 1 | Low |
| 5. Add EnhancedGrid to CIP/Costs.cshtml | 1 | Low |
| 6. Add EnhancedGrid to Inventory/List.cshtml | 1 | Low |

### Tier 3: Inline Style Remediation (High Effort)

| Page | Inline Styles | Target Module CSS |
|------|---------------|-------------------|
| Materials/ItemEdit.cshtml | 177 | `materials.css` |
| Assets/Asset.cshtml | 154 | `assets.css` (extend) |
| WorkOrders/Details.cshtml | 104 | `workorders.css` |
| Index.cshtml | 83 | `dashboard.css` (extend) |
| Maintenance/Index.cshtml | 52 | `maintenance.css` |

---

## TASK B: SEED COVERAGE INVENTORY

### Current Seed Pipelines

| Pipeline | Version | Purpose | Modules Covered |
|----------|---------|---------|-----------------|
| `SystemReferenceSeedPipeline` | 1.0.0 | WO types, failure codes, crafts, priorities | WO Config only |
| `OrgAndFinanceSeedPipeline` | 1.0.0 | GL accounts, sites, departments | Org, Finance |
| `VendorsAndPartsFoundationSeedPipeline` | 1.0.0 | Vendors, item categories, labor rates | Vendors, Parts Config |
| `EamExecutionMastersSeedPipeline` | 1.0.0 | Technicians, PM templates | Technicians, PM Config |
| `DemoPackV1Pipeline` | 1.0.0 | Assets, PM Templates, PM Schedules | Assets, PM |
| `DemoPackV2Pipeline` | 2.0.0 | Items, Manufacturers, MPNs, VPNs, AVL | Materials |
| `DemoScenarioSeedPipeline` | 1.0.0 | Sample assets | Assets only |

### Seed Coverage Gaps by Module

| Module | Screen | Current Seeding | Gap |
|--------|--------|-----------------|-----|
| **Purchasing** | PR List | ❌ None | No PRs created |
| **Purchasing** | PO List | ❌ None | No POs created |
| **Receiving** | Receipt List | ❌ None | No receipts created |
| **Inventory** | On-Hand | ❌ None | No stock transactions |
| **Maintenance** | Work Orders | ❌ Types only | No actual WOs |
| **Maintenance** | Labor | ✅ Rates only | No labor entries |
| **Maintenance** | Parts Used | ❌ None | No WO parts |
| **AP** | Vendor Invoices | ❌ None | No invoices |
| **AP** | Payments | ❌ None | No payments |
| **Attachments** | All modules | ❌ None | No sample files |
| **Calibration** | Records | ❌ None | No calibrations |

### Why Key Screens Are Empty

1. **Purchase Orders:** `DemoPackV2` creates items/vendors but no transactional POs
2. **Receiving:** Depends on POs existing first (foreign key)
3. **Inventory On-Hand:** No stock transactions to create balances
4. **Work Orders:** Only WO *types* seeded, not actual work orders
5. **Vendor Invoices:** Depends on POs and receipts (3-way match)
6. **Labor Entries:** Technicians exist but no WO labor records

---

### Proposed DemoPackV3 Scenario

**Objective:** Create a complete, deterministic end-to-end procurement-to-maintenance scenario.

**Flow:**
```
PR-001 (Requisition)
    ↓
PO-001 (Purchase Order)
    ↓
RCV-001 (Goods Receipt)
    ↓
INV Transaction (Stock In)
    ↓
WO-001 (Work Order with Parts + Labor)
    ↓
Closeout (Resolution Summary)
    ↓
VINV-001 (Vendor Invoice 3-way matched)
```

**DemoPackV3 Seed Steps:**

| Step | Entity | Count | Dependencies |
|------|--------|-------|--------------|
| 1 | PurchaseRequisition | 3 | Items from V2 |
| 2 | PurchaseOrder | 5 | Vendors, Items |
| 3 | POLineItems | 15 | POs |
| 4 | GoodsReceipt | 5 | POs |
| 5 | ReceiptLines | 15 | Receipts, POLines |
| 6 | InventoryTransaction | 15 | Items, Locations |
| 7 | AssetMaintenance (WorkOrder) | 10 | Assets from V1 |
| 8 | WorkOrderLabor | 20 | WOs, Technicians |
| 9 | WorkOrderParts | 25 | WOs, Items |
| 10 | VendorInvoice | 5 | POs, Receipts |
| 11 | InvoiceLines | 15 | Invoices |
| 12 | Attachment | 10 | Various entities |
| 13 | CalibrationRecord | 5 | Assets |

**Scenario Details:**

- **Requisition PR-001:** Request for bearing replacement (3 items)
- **PO PO-001:** Approved PO to Grainger for PR-001
- **Receipt RCV-001:** Full receipt of PO-001
- **Work Order WO-001:** PM on CNC-001, uses parts from RCV-001
- **Labor:** 4 hours by John Smith (technician)
- **Closeout:** Auto-generated resolution summary
- **Invoice VINV-001:** 3-way matched to PO-001/RCV-001

---

## TASK C: PHASED EXECUTION PLAN

### Phase 1: Shell & Shared Components (2-3 days)

1. **Create missing partials:**
   - `_TabNav.cshtml` - Unified tab navigation component
   - Extend `_EmptyState.cshtml` with more variants

2. **CSS module creation:**
   - `wwwroot/css/modules/tabs.css`
   - `wwwroot/css/modules/workorders.css`
   - `wwwroot/css/modules/materials.css`

3. **Move `<style>` blocks from partials:**
   - `_BackLink.cshtml` → `components.css`
   - `_AssetMaintenanceHeader.cshtml` → `assets.css`

4. **Update allowlist:**
   - Add new CSS modules to `_ModernLayout.cshtml`
   - Update `UI-Conformance-Allowlist.md`

### Phase 2: List Pages DataGrid Standardization (2 days)

1. Add EnhancedGrid to:
   - Books/Index.cshtml
   - CCA/Index.cshtml
   - Receiving/Index.cshtml
   - BulkOperations/Index.cshtml
   - CIP/Costs.cshtml
   - Inventory/List.cshtml

2. Update smoke tests to enforce new pages

### Phase 3: Detail Pages Alignment (3-4 days)

1. Migrate inline styles from top offenders:
   - Materials/ItemEdit.cshtml (177 → 0)
   - Assets/Asset.cshtml (154 → 0)
   - WorkOrders/Details.cshtml (104 → 0)

2. Unify tab implementations:
   - Replace `.wo-tabs-nav` with `_TabNav.cshtml`
   - Replace `.report-tabs` with `_TabNav.cshtml`
   - Replace `.premium-tabs` with `_TabNav.cshtml`

3. Fix duplicate H1 issues on detail pages

### Phase 4: DemoPackV3 Seed Pipeline (3-4 days)

1. Create `DemoPackV3Pipeline.cs` with deterministic scenario
2. Add seed steps for:
   - Purchase Requisitions
   - Purchase Orders + Lines
   - Goods Receipts + Lines
   - Inventory Transactions
   - Work Orders + Labor + Parts
   - Vendor Invoices
   - Sample Attachments

3. Update Admin/SeedData page to include V3
4. Run smoke tests to verify data integrity

### Phase 5: Documentation Updates (1 day)

1. Update existing docs
2. Create new docs as needed
3. Final validation

---

## DOCUMENTATION UPDATES REQUIRED

### Docs to Update

| Document | Changes Needed |
|----------|----------------|
| `docs/UXStandards.md` | Clarify `.page-hero` as canonical (not `.hero-section`), add tab system reference |
| `docs/SeedingAndDemoData.md` | Add DemoPackV3 section, update pipeline inventory |
| `docs/UI-Conformance-Allowlist.md` | Add new module CSS files, update style block exceptions |
| `docs/DataGridPremium.md` | Add Books, CCA, Receiving to page list |

### New Docs to Create

| Document | Purpose |
|----------|---------|
| `docs/SeedPackages.md` | Comprehensive seed package reference with dependencies |
| `docs/adr/ADR-007-Unified-Tab-System.md` | Decision record for tab consolidation |
| `docs/adr/ADR-008-DemoPackV3-Procurement-Scenario.md` | Decision record for V3 seeding |

### ADR/Docs Contradictions Found

| Issue | Location | Resolution |
|-------|----------|------------|
| `.hero-section` vs `.page-hero` | UXStandards.md | Update docs to match code (`.page-hero`) |
| `_BackLink.cshtml` has inline `<style>` | Not in allowlist | Add to allowlist OR migrate to CSS |
| `_AssetMaintenanceHeader.cshtml` has inline `<style>` | Not in allowlist | Add to allowlist OR migrate to CSS |

---

## GUARDRAILS VERIFICATION

| Guardrail | Status |
|-----------|--------|
| No git modifications in inventory phase | ✅ Compliant |
| Folder-level coverage including Purchasing/Receiving | ✅ Complete |
| Consistent with ADR-004 (inline styles) | ✅ Violations documented |
| Consistent with ADR-005 (DataGrid contract) | ✅ Gaps identified |
| Consistent with ADR-006 (returnUrl security) | ✅ Not affected |
| ADR/docs contradictions called out | ✅ 3 issues documented |

---

## APPENDIX: File Counts by Module

```
Account:           3 pages
AccountsPayable:   2 pages
Admin:            45 pages
AI:                1 page
API:               2 pages
Assets:            9 pages
Books:             6 pages
BulkOperations:    2 pages
CCA:               2 pages
CIP:               5 pages
Help:              4 pages
Inventory:         2 pages
Journals:          3 pages
Maintenance:       8 pages
Materials:         2 pages
Purchasing:        2 pages
Receiving:         1 page
Reports:           9 pages
Shared:           11 partials
UsTax:             1 page
WorkOrders:        1 page
─────────────────────────
TOTAL:           121 pages + 11 partials
```
