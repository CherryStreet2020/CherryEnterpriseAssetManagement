# Premium UX Alignment + Seed Data Execution Plan

**Date:** January 2026  
**Status:** Verified & Ready for Implementation  
**Prerequisites:** docs/UX-Seed-Inventory-Report-Jan2026.md (verified)

---

## A. AUDIT VERIFICATION (Concrete Citations)

### A1. Tab Fragmentation — 4 Distinct Implementations Confirmed

| Pattern | CSS Classes | Files | Markup Style |
|---------|-------------|-------|--------------|
| **Premium Tabs** | `.premium-tabs`, `.premium-tab-nav`, `.premium-tab-btn` | `Pages/Assets/Asset.cshtml`, `Pages/Materials/ItemEdit.cshtml`, `Pages/Maintenance/Assignments/Index.cshtml`, `Pages/Maintenance/Schedules.cshtml` | Button-based, JS data-tab switching |
| **WO Tabs** | `.wo-tabs-nav`, `.wo-tab-link`, `.wo-tab-panel` | `Pages/Maintenance/Details.cshtml` | Anchor-based, query param switching (`?tab=`) |
| **Report Tabs** | `.report-tabs` | `Pages/Reports/Compliance.cshtml` | Custom styling |
| **Generic Tabs** | `.tabs`, `.tab-btn`, `.tab-content` | `Pages/Assets/Asset.cshtml`, `Pages/Reports/Compliance.cshtml`, `Pages/Materials/ItemEdit.cshtml` | Mixed usage |

**Verified Counts:**
```
premium-tab-btn:  16 occurrences
wo-tab-link:       6 occurrences
wo-tab-panel:      6 occurrences
tab-btn:           2 occurrences
report-tabs:       1 occurrence
```

**Impact:** 7+ pages need migration to unified system.

---

### A2. Inline Styles Scale — Verified Counts

| Page | Reported | **Verified** | Severity |
|------|----------|--------------|----------|
| `Pages/Materials/ItemEdit.cshtml` | 177 | **177** ✅ | 🔴 Critical |
| `Pages/Assets/Asset.cshtml` | 154 | **154** ✅ | 🔴 Critical |
| `Pages/WorkOrders/Details.cshtml` | 104 | **104** ✅ | 🔴 Critical |
| `Pages/Admin/Items.cshtml` | 90 | **90** ✅ | 🔴 Critical |
| `Pages/Index.cshtml` | 83 | **84** ≈ | 🔴 Critical |
| `Pages/Maintenance/Index.cshtml` | 52 | **54** ≈ | 🟠 High |

**Total pages with `style=` attributes:** 98+

---

### A3. `<style>` Blocks in Partials — 3 Found

| File | Line | Allowlisted? | Action |
|------|------|--------------|--------|
| `Pages/Shared/_BackLink.cshtml` | Line 21 | ❌ **Not allowlisted** | Migrate to `wwwroot/css/components.css` |
| `Pages/Shared/_AssetMaintenanceHeader.cshtml` | Line 28 | ❌ **Not allowlisted** | Migrate to `wwwroot/css/modules/assets.css` |
| `Pages/Shared/_ModernLayout.cshtml` | Line 939 | ✅ Allowlisted | Keep (layout file) |

**_BackLink.cshtml styles (46 lines):**
- `.back-link` flex styling, hover states, SVG sizing
- `.page-hero .back-link` margin adjustment

**_AssetMaintenanceHeader.cshtml styles (60+ lines):**
- `.module-header`, `.module-breadcrumbs` layout
- `.breadcrumb-sep`, `.breadcrumb-current` styling

---

### A4. DataGrid Coverage — 15 Compliant, 7 Need Migration

**✅ Compliant (15 pages with `data-enhanced-grid="true"`):**
```
Pages/AccountsPayable/Index.cshtml
Pages/Admin/Items.cshtml
Pages/Admin/PMTemplates.cshtml
Pages/Admin/Vendors.cshtml
Pages/Admin/WorkOrders.cshtml
Pages/Assets/Index.cshtml
Pages/CIP/Index.cshtml
Pages/Inventory/Index.cshtml
Pages/Journals/Index.cshtml
Pages/Maintenance/Assignments/Index.cshtml
Pages/Maintenance/Index.cshtml
Pages/Maintenance/Schedules.cshtml
Pages/Maintenance/WorkRequests/Index.cshtml
Pages/Materials/Items.cshtml
Pages/Purchasing/Index.cshtml
```

**🔴 Need EnhancedGrid (List Pages with Tables):**

| Page | Table Purpose | Priority |
|------|---------------|----------|
| `Pages/Books/Index.cshtml` | Book master list | High |
| `Pages/CCA/Index.cshtml` | CCA class list | High |
| `Pages/Receiving/Index.cshtml` | Receipt list | High |
| `Pages/BulkOperations/Index.cshtml` | Bulk ops history | Medium |
| `Pages/Inventory/List.cshtml` | Inventory items | High |

**⚪ Allowlist Candidates (Internal/Reference):**

| Page | Reason |
|------|--------|
| `Pages/UsTax/Index.cshtml` | Reference data table |
| `Pages/API/Index.cshtml` | Developer tools |
| `Pages/Admin/Outbox/Index.cshtml` | Internal admin |
| `Pages/Admin/Integrations/Index.cshtml` | Internal admin |
| `Pages/Index.cshtml` (dashboard) | Activity feed, not data grid |

---

## B. LOGICAL EAM FLOW (Data Dependencies)

### The Canonical User Journey

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        EAM TRANSACTIONAL FLOW                           │
└─────────────────────────────────────────────────────────────────────────┘

FOUNDATION (must exist first)
├── Tenant/Company/Site hierarchy (OrgAndFinanceSeedPipeline)
├── Vendors (VendorsAndPartsFoundationSeedPipeline)
├── Items/Parts (DemoPackV2Pipeline)
├── Assets (DemoPackV1Pipeline)
└── Technicians (EamExecutionMastersSeedPipeline)
           │
           ▼
PROCUREMENT PHASE
├── Purchase Requisition (PR)
│   └── FK: Items, RequestedBy (User), Site
│   └── Creates demand signal
│
├── Purchase Order (PO)
│   └── FK: Vendor, Items via POLines
│   └── Optionally links to PR (ConvertedFromRequisitionId)
│   └── Status: Draft → Approved → Sent
           │
           ▼
RECEIVING PHASE
├── Goods Receipt (RCV)
│   └── FK: PurchaseOrderId (required)
│   └── Must have PO in Sent/PartiallyReceived status
│   └── Creates ReceiptLines linked to POLines
│
└── Inventory Transaction (Stock-In)
    └── FK: Item, Location
    └── Creates on-hand balances
           │
           ▼
MAINTENANCE EXECUTION
├── Work Order (MaintenanceEvent)
│   └── FK: Asset, Technician
│   └── Status: Scheduled → InProgress → Completed
│
├── WO Parts
│   └── FK: WorkOrder, Item
│   └── Decrements inventory (Stock-Out transaction)
│
├── WO Labor
│   └── FK: WorkOrder, Technician, LaborType
│   └── Tracks hours and costs
│
└── Closeout
    └── Resolution Summary (auto-generated)
    └── Status → Completed
           │
           ▼
ACCOUNTS PAYABLE
├── Vendor Invoice
│   └── FK: Vendor, PurchaseOrder (optional)
│   └── 3-way match: Invoice ↔ PO ↔ Receipt
│   └── Status: Draft → Approved → Paid
│
└── Payment
    └── FK: VendorInvoice
    └── Closes the cycle
```

### Why Each Phase Depends on the Previous

| Phase | Depends On | Reason |
|-------|------------|--------|
| **PR** | Items, Users | Can't request parts that don't exist |
| **PO** | Vendors, Items | PO lines reference vendor item pricing |
| **Receipt** | PO | `GoodsReceipt.PurchaseOrderId` is required FK |
| **Inventory** | Receipt | Stock-in transaction creates from receipt acceptance |
| **Work Order** | Assets, Technicians | Can't maintain non-existent assets |
| **WO Parts** | Work Order, Items | Parts must be in inventory |
| **WO Labor** | Work Order, Technicians | Labor records track technician time |
| **Invoice** | PO, Receipt | 3-way match validates pricing and quantities |

### Model Classes & Locations

| Entity | Model File | Key FKs |
|--------|------------|---------|
| PurchaseRequisition | `Models/PurchaseRequisition.cs` | ItemId, RequestedById, SiteId |
| PurchaseOrder | `Models/PurchaseOrder.cs` | VendorId, SiteId |
| POLineItem | `Models/PurchaseOrder.cs` (nested) | PurchaseOrderId, ItemId |
| GoodsReceipt | `Models/GoodsReceipt.cs` | PurchaseOrderId |
| ReceiptLine | `Models/GoodsReceipt.cs` (nested) | GoodsReceiptId, POLineId |
| MaintenanceEvent | `Models/AssetMaintenance.cs` | AssetId, TechnicianId |
| WorkOrderLabor | `Models/AssetMaintenance.cs` | MaintenanceEventId, TechnicianId |
| WorkOrderPart | `Models/AssetMaintenance.cs` | MaintenanceEventId, ItemId |
| VendorInvoice | `Models/VendorInvoice.cs` | VendorId, PurchaseOrderId |
| InvoiceLine | `Models/VendorInvoice.cs` (nested) | VendorInvoiceId, ItemId |

---

## C. PHASED REMEDIATION PLAN

### Phase 1: Shared UI Primitives (Prevent Drift First)

**Objective:** Create reusable components so future pages are consistent by default.

**Duration:** 2-3 days

#### 1.1 Create Unified Tab System

**New Files:**
- `Pages/Shared/_TabNav.cshtml` — Single tab partial
- `wwwroot/css/modules/tabs.css` — Tab styles

**_TabNav.cshtml Contract:**
```razor
@* Usage: <partial name="_TabNav" model="@Model.Tabs" /> *@
@model TabNavModel

<nav class="tab-nav" role="tablist">
    @foreach (var tab in Model.Tabs)
    {
        <button type="button" 
                class="tab-nav-btn @(tab.IsActive ? "active" : "")"
                data-tab="@tab.Id"
                role="tab"
                aria-selected="@(tab.IsActive ? "true" : "false")">
            @if (!string.IsNullOrEmpty(tab.Icon))
            {
                <span class="tab-icon">@Html.Raw(tab.Icon)</span>
            }
            @tab.Label
            @if (tab.Count.HasValue && tab.Count > 0)
            {
                <span class="tab-count">@tab.Count</span>
            }
        </button>
    }
</nav>
```

**Acceptance Criteria:**
- [ ] Single `.tab-nav`, `.tab-nav-btn`, `.tab-count` class hierarchy
- [ ] Supports both JS switching (data-tab) and URL switching (href)
- [ ] Works with icons, counts, and plain text tabs
- [ ] All existing tab patterns can be expressed with this partial

#### 1.2 Migrate `<style>` Blocks from Partials

**_BackLink.cshtml:**
- Move 46 lines to `wwwroot/css/components.css`
- Delete `<style>` block from partial
- Add `.back-link` classes to CSS

**_AssetMaintenanceHeader.cshtml:**
- Move 60+ lines to `wwwroot/css/modules/assets.css`
- Delete `<style>` block from partial
- Verify `.module-header`, `.module-breadcrumbs` render correctly

#### 1.3 Update CSS Loading in _ModernLayout.cshtml

Add to `<head>`:
```html
<link rel="stylesheet" href="~/css/modules/tabs.css" asp-append-version="true">
```

Verify `components.css` and `assets.css` are already loaded.

#### 1.4 Documentation Updates (Phase 1)

- Create `docs/adr/ADR-007-Unified-Tab-System.md`
- Update `docs/UI-Conformance-Allowlist.md`:
  - Remove `_BackLink.cshtml` from expected violations
  - Remove `_AssetMaintenanceHeader.cshtml` from expected violations
- Update `docs/UXStandards.md`:
  - Add "Tab System" section referencing `_TabNav.cshtml`

**Acceptance Criteria (Phase 1):**
- [ ] `_TabNav.cshtml` partial created with documented contract
- [ ] `tabs.css` created with unified tab styles
- [ ] No `<style>` blocks in `_BackLink.cshtml` or `_AssetMaintenanceHeader.cshtml`
- [ ] Smoke tests 57-60 still pass (69/69 green)
- [ ] ADR-007 created and linked from docs/README.md

---

### Phase 2: List Pages DataGrid Standardization

**Objective:** All list pages use EnhancedGrid for consistent UX.

**Duration:** 2 days

#### 2.1 Add EnhancedGrid to 5 Pages

| Page | Current Table | EnhancedGrid Features |
|------|---------------|----------------------|
| `Pages/Books/Index.cshtml` | Simple table | Search, sort, export |
| `Pages/CCA/Index.cshtml` | Simple table | Search, sort, export |
| `Pages/Receiving/Index.cshtml` | Simple table | Search, sort, export, row-click |
| `Pages/BulkOperations/Index.cshtml` | Simple table | Search, sort, export |
| `Pages/Inventory/List.cshtml` | Simple table | Search, filter, export |

**Per-Page Changes:**
1. Add `data-enhanced-grid="true"` to `<table>`
2. Add `data-row-click="true"` if navigable
3. Add `data-row-href="@Url.Page(...)"` to each `<tr>`
4. Add `data-col` and `data-filter` attributes to `<th>` headers
5. Ensure `enhanced-grid.js` is loaded (via layout)

#### 2.2 Update Allowlist for Internal Pages

Add to `docs/UI-Conformance-Allowlist.md` DataGrid Exceptions:
```markdown
| `Pages/UsTax/Index.cshtml` | Reference | Static tax reference data |
| `Pages/API/Index.cshtml` | Developer | API endpoint documentation |
| `Pages/Admin/Outbox/Index.cshtml` | Internal | Admin webhook queue |
| `Pages/Admin/Integrations/Index.cshtml` | Internal | Admin integration config |
```

#### 2.3 Documentation Updates (Phase 2)

- Update `docs/DataGridPremium.md`:
  - Add Books, CCA, Receiving, BulkOperations, Inventory/List to compliant list
- Update `docs/RouteRegistry.md` if new routes added
- Verify `docs/NavigationMap.md` accuracy

**Acceptance Criteria (Phase 2):**
- [ ] 5 pages migrated to EnhancedGrid
- [ ] returnUrl patterns use `Url.Page()` (ADR-006 compliant)
- [ ] Smoke tests pass
- [ ] DataGridPremium.md updated with new pages

---

### Phase 3: High-Offender Inline Style Remediation

**Objective:** Reduce ADR-004 violations from top 5 pages.

**Duration:** 3-4 days

#### 3.1 Target Pages & CSS Modules

| Page | Current | Target CSS Module | Strategy |
|------|---------|-------------------|----------|
| `Materials/ItemEdit.cshtml` | 177 | `wwwroot/css/modules/materials.css` | Extract procurement, VPN, revision styles |
| `Assets/Asset.cshtml` | 154 | `wwwroot/css/modules/assets.css` (extend) | Extend existing file |
| `WorkOrders/Details.cshtml` | 104 | `wwwroot/css/modules/workorders.css` (new) | Create new file |
| `Index.cshtml` (Dashboard) | 84 | `wwwroot/css/modules/dashboard.css` (extend) | Extend existing file |
| `Maintenance/Index.cshtml` | 54 | `wwwroot/css/modules/maintenance.css` (new) | Create new file |

#### 3.2 Style Extraction Process

For each page:
1. Identify unique style patterns (grep for `style="`)
2. Group by purpose (layout, colors, spacing)
3. Create semantic CSS classes
4. Replace inline styles with classes
5. Test visual regression

#### 3.3 Tab Migration During Phase 3

While touching these pages, migrate to `_TabNav.cshtml`:
- `Assets/Asset.cshtml`: Replace `.premium-tabs` with `_TabNav`
- `Materials/ItemEdit.cshtml`: Replace `.premium-tabs` with `_TabNav`
- `Maintenance/Details.cshtml`: Replace `.wo-tabs-nav` with `_TabNav`

**Acceptance Criteria (Phase 3):**
- [ ] Top 5 pages reduced to <10 inline styles each
- [ ] New CSS modules created and loaded via layout
- [ ] Tab implementations unified to `_TabNav.cshtml`
- [ ] Smoke tests pass
- [ ] No visual regressions (manual verification)

---

### Phase 4: DemoPackV3 Seeding (Transactional Scenario)

**Objective:** Populate all empty transactional screens with realistic demo data.

**Duration:** 3-4 days

#### 4.1 Create DemoPackV3Pipeline.cs

**Location:** `Services/Seeding/Pipelines/DemoPackV3Pipeline.cs`

**Pipeline Structure:**
```csharp
public class DemoPackV3Pipeline : ISeedPipeline
{
    public string Name => "DemoPackV3";
    public string Version => "3.0.0";
    public string Description => "Transactional demo: PR → PO → Receipt → WO → Invoice";
    public bool IsDevOnly => true;

    public IReadOnlyList<ISeedStep> Steps => new List<ISeedStep>
    {
        new DemoPackV3RequisitionsSeedStep(...),
        new DemoPackV3PurchaseOrdersSeedStep(...),
        new DemoPackV3GoodsReceiptsSeedStep(...),
        new DemoPackV3InventoryTransactionsSeedStep(...),
        new DemoPackV3WorkOrdersSeedStep(...),
        new DemoPackV3WorkOrderLaborSeedStep(...),
        new DemoPackV3WorkOrderPartsSeedStep(...),
        new DemoPackV3VendorInvoicesSeedStep(...)
    };
}
```

#### 4.2 Seed Step Details

| Step | Entity | Count | Natural Key | Dependencies |
|------|--------|-------|-------------|--------------|
| 1 | PurchaseRequisition | 3 | `RequisitionNumber` | Items (V2), Site (OrgFinance) |
| 2 | PurchaseOrder + Lines | 5 POs, 15 lines | `PONumber` | Vendors (VendorParts), Items (V2) |
| 3 | GoodsReceipt + Lines | 5 receipts, 15 lines | `ReceiptNumber` | PurchaseOrders (step 2) |
| 4 | InventoryTransaction | 15 | `TransactionNumber` | Items, Locations |
| 5 | MaintenanceEvent | 10 | `WorkOrderNumber` | Assets (V1), Technicians |
| 6 | WorkOrderLabor | 20 | WO + Technician + Date | MaintenanceEvents (step 5) |
| 7 | WorkOrderPart | 25 | WO + Item | MaintenanceEvents, Items |
| 8 | VendorInvoice + Lines | 5 invoices, 15 lines | `InvoiceNumber` | POs, Receipts |

#### 4.3 Deterministic Scenario

**Story: Bearing Replacement for CNC Machine**

```
Day 1: Stock check reveals low bearing inventory
  → PR-2026-001 created (3 items: bearing, seal, lubricant)
  
Day 2: PR approved, converted to PO
  → PO-2026-001 to Grainger (linked to PR-2026-001)
  
Day 5: Parts arrive
  → RCV-2026-001 received (full qty)
  → Inventory: +3 bearings, +2 seals, +1 lubricant
  
Day 7: PM scheduled for CNC-001
  → WO-2026-001 created (Preventive Maintenance)
  → Parts issued: 1 bearing, 1 seal
  → Labor: 4 hours by John Smith (technician)
  
Day 8: Work completed
  → WO-2026-001 closed with resolution summary
  
Day 10: Invoice received
  → VINV-2026-001 from Grainger
  → 3-way match to PO-2026-001 / RCV-2026-001
```

#### 4.4 Idempotency Rules

Each seed step follows `BaseSeedStep<T>` pattern:
- Natural key lookup before insert
- Upsert on match
- Audit receipt written to seed log

#### 4.5 Admin/SeedData Integration

Update `Pages/Admin/SeedData.cshtml.cs`:
```csharp
// Add to pipeline list
private readonly ISeedPipeline[] _pipelines = new ISeedPipeline[]
{
    // ... existing pipelines
    new DemoPackV3Pipeline(context, loggerFactory)
};
```

Add to `Pages/Admin/DemoData.cshtml` UI:
```html
<button type="submit" asp-page-handler="SeedDemoPackV3" class="btn btn-secondary">
    Run DemoPackV3 (Transactions)
</button>
```

**Acceptance Criteria (Phase 4):**
- [ ] DemoPackV3Pipeline created with 8 seed steps
- [ ] All transactional screens show demo data after running
- [ ] Purchasing: 3 PRs, 5 POs visible
- [ ] Receiving: 5 receipts visible
- [ ] Inventory: On-hand balances visible
- [ ] Maintenance: 10 work orders visible with parts/labor
- [ ] AP: 5 vendor invoices visible
- [ ] Smoke tests pass (no data integrity violations)
- [ ] Seed is idempotent (running twice doesn't duplicate)

---

### Phase 5: Documentation Completion & Gates

**Objective:** Align all documentation with implemented changes.

**Duration:** 1 day

#### 5.1 Docs to Update

| Document | Changes |
|----------|---------|
| `docs/UXStandards.md` | Confirm `.page-hero` is canonical; add tab system section |
| `docs/SeedingAndDemoData.md` | Add DemoPackV3 section with scenario description |
| `docs/UI-Conformance-Allowlist.md` | Update for new CSS modules, remove resolved violations |
| `docs/DataGridPremium.md` | Add newly migrated pages to compliant list |
| `docs/UX-Seed-Inventory-Report-Jan2026.md` | Mark resolved items |

#### 5.2 New Docs to Create

| Document | Content |
|----------|---------|
| `docs/SeedPackages.md` | Comprehensive seed package reference with dependencies |
| `docs/adr/ADR-007-Unified-Tab-System.md` | Tab consolidation decision |
| `docs/adr/ADR-008-DemoPackV3-Procurement-Scenario.md` | V3 seeding decision |

#### 5.3 Final Verification

- [ ] Run all 69 smoke tests
- [ ] Verify no new inline style violations
- [ ] Verify all EnhancedGrid pages work
- [ ] Verify DemoPackV3 data appears correctly
- [ ] Cross-reference docs with code

**Acceptance Criteria (Phase 5):**
- [ ] All docs updated and consistent with code
- [ ] ADR-007 and ADR-008 created
- [ ] SeedPackages.md created
- [ ] 69/69 smoke tests green
- [ ] Manual spot-check of 5 key screens

---

## D. NEXT ACTION: First Implementation PR

### PR #1: Phase 1.2 — Migrate Partial Style Blocks

**Rationale:** This is the smallest, safest change that immediately reduces ADR-004 violations without touching operational pages.

#### File-by-File Changes

**1. `wwwroot/css/components.css`** (extend)
```css
/* Add at end of file */

/* Back Link Component - migrated from _BackLink.cshtml */
.back-link {
    display: inline-flex;
    align-items: center;
    gap: 0.5rem;
    padding: 0.5rem 1rem;
    font-size: 0.875rem;
    font-weight: 500;
    color: var(--text-secondary, #64748b);
    text-decoration: none;
    border-radius: 0.5rem;
    transition: all 0.15s ease;
    background: transparent;
}

.back-link:hover {
    color: var(--primary, #3b82f6);
    background: var(--bg-hover, rgba(59, 130, 246, 0.08));
}

.back-link svg {
    flex-shrink: 0;
}

.page-hero .back-link {
    margin-bottom: 0.75rem;
}
```

**2. `Pages/Shared/_BackLink.cshtml`** (edit)
- Delete lines 21-47 (the `<style>` block)
- Keep lines 1-20 (the markup)

**3. `wwwroot/css/modules/assets.css`** (extend)
```css
/* Add at end of file */

/* Module Header - migrated from _AssetMaintenanceHeader.cshtml */
.module-header {
    margin-bottom: 1.5rem;
}

.module-breadcrumbs {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.8125rem;
    color: #64748b;
    margin-bottom: 0.5rem;
}

.module-breadcrumbs a {
    color: #64748b;
    text-decoration: none;
    transition: color 0.15s;
}

.module-breadcrumbs a:hover {
    color: #3b82f6;
    text-decoration: underline;
}

.module-breadcrumbs .breadcrumb-sep {
    color: #cbd5e1;
    font-size: 0.75rem;
}

.module-breadcrumbs .breadcrumb-current {
    color: #1e293b;
    font-weight: 500;
}
```

**4. `Pages/Shared/_AssetMaintenanceHeader.cshtml`** (edit)
- Delete the `<style>` block (lines 28-60+)
- Keep the HTML markup

**5. `docs/UI-Conformance-Allowlist.md`** (update)
- Add note that `_BackLink.cshtml` and `_AssetMaintenanceHeader.cshtml` are now compliant

#### Acceptance Criteria

- [ ] No `<style>` blocks in `_BackLink.cshtml`
- [ ] No `<style>` blocks in `_AssetMaintenanceHeader.cshtml`
- [ ] Back links render correctly across all pages using partial
- [ ] Module headers render correctly in Asset/Maintenance pages
- [ ] Smoke test 59 (UI Hygiene) passes
- [ ] All 69 smoke tests pass

#### Smoke Test Expectations

```
Test 59: UI → No Inline Style Blocks
Expected: PASS (2 fewer violations)

All tests: 69/69 PASS
```

#### Docs Updates in Same PR

- Update `docs/UI-Conformance-Allowlist.md`:
  - Add migration note for `_BackLink.cshtml`
  - Add migration note for `_AssetMaintenanceHeader.cshtml`

---

## Summary

| Phase | Focus | Duration | Key Deliverables |
|-------|-------|----------|------------------|
| 1 | Shared Primitives | 2-3 days | `_TabNav.cshtml`, partial style migrations |
| 2 | List Pages | 2 days | 5 pages with EnhancedGrid |
| 3 | Inline Style Remediation | 3-4 days | Top 5 pages cleaned, tabs unified |
| 4 | DemoPackV3 Seeding | 3-4 days | Full transactional demo data |
| 5 | Documentation | 1 day | ADR-007, ADR-008, updated docs |

**Total Estimated Duration:** 11-14 days

**First PR:** Migrate `<style>` blocks from `_BackLink.cshtml` and `_AssetMaintenanceHeader.cshtml` to CSS modules.
