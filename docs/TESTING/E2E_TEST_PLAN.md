# CherryAI EAM — Hyper-Detailed End-to-End Test Plan

**Version:** 1.0 (2026-05-08, after PR #57)
**Target executor:** Claude in Cowork (Chrome browser automation), Replit-hosted app
**Posture:** **Best-in-class proof.** Every page reached by clicking; no URL navigation. Every link clicked, every form exercised, every report opened, every export downloaded, every outbox event verified.

---

## How the Cowork agent should run this plan

1. **Sequential execution.** Steps are numbered globally `T-001`, `T-002`, … Run them in order. Do NOT skip a step because "it looks like it would pass." Click the link.
2. **No URL typing.** The whole point is to prove navigation. The only allowed URL entry is the very first one (the app's root). After that, every page transition must be a click on a link, button, or form submission visible on screen.
3. **One screenshot per step.** Capture before-state and after-state screenshots. Name them `T-NNN-before.png` and `T-NNN-after.png`.
4. **Per-step verification.** Each step has an `Expected` (what the screen should show) and a `Verify` (DB / outbox / GL assertion). If `Verify` requires SQL, run the canonical psql block at the end of each module.
5. **Failure protocol.** If a step fails:
   - Capture the failure screenshot (`T-NNN-FAIL.png`).
   - Capture the browser console + network tab.
   - Record the failure in the run log: `T-NNN | FAIL | <one-line reason>`.
   - Continue to the next step UNLESS the failure is gating (marked `[GATING]`). Gating failures stop the run for that module; restart the module after fixing.
6. **State-dependency awareness.** The plan is structured so that earlier modules set up data later modules consume. If you skip Module M, Module N (where N > M and depends on M) may need its own setup.
7. **Idempotency.** All "create" steps use timestamped names (e.g., `E2E-PO-{HHmmss}`). Re-running the plan does not collide with prior runs.
8. **Reporting.** After the run, produce a single `E2E_RUN_<YYYYMMDD-HHmmss>.md` with:
   - Total steps, passed, failed, skipped
   - Per-module breakdown
   - Failure list with screenshots
   - Outbox event audit (count of each `EventType` produced during the run)

---

## Prerequisites

Before starting:

1. **App must be running** on Replit at `https://<replit-id>.replit.app` (or `http://localhost:5000` if running locally). The agent should ask the user for the URL once.
2. **DB must be seeded.** If the home page shows "Database Not Initialized," the operator must set `RUN_SEED=true` in Replit and restart before this plan can proceed.
3. **Login credentials.** Default seeded admin user (operator confirms): username `ADMIN`, password TBD. Cowork agent: ask the user once at `T-002`.
4. **Cowork extension must be connected.** Verify with `mcp__Claude_in_Chrome__list_connected_browsers` before `T-001`.

---

## Module index (jump-to)

| # | Module | Steps | Gating? |
|---|---|---|---|
| 1 | [Pre-flight + Login](#module-1--pre-flight--login) | T-001 → T-010 | ⚠ GATING |
| 2 | [Navigation map (every nav link)](#module-2--navigation-map) | T-020 → T-100 | — |
| 3 | [Home dashboard](#module-3--home-dashboard) | T-110 → T-130 | — |
| 4 | [Assets module](#module-4--assets-module) | T-140 → T-220 | — |
| 5 | [Materials / Items / Vendors](#module-5--materials) | T-230 → T-310 | — |
| 6 | [Inventory](#module-6--inventory) | T-320 → T-360 | — |
| 7 | [Maintenance / Work Orders / PM](#module-7--maintenance) | T-370 → T-490 | — |
| 8 | [Purchasing](#module-8--purchasing) | T-500 → T-560 | — |
| 9 | [Receiving](#module-9--receiving) | T-570 → T-610 | — |
| 10 | [Accounts Payable](#module-10--accounts-payable) | T-620 → T-680 | — |
| 11 | [CIP](#module-11--cip-construction-in-progress) | T-690 → T-740 | — |
| 12 | [Books + GL Accounts](#module-12--books--gl-accounts) | T-750 → T-790 | — |
| 13 | [Journals + Depreciation](#module-13--journals--depreciation) | T-800 → T-840 | — |
| 14 | [Bulk Operations](#module-14--bulk-operations) | T-850 → T-880 | — |
| 15 | [CCA + US Tax](#module-15--cca--us-tax) | T-890 → T-920 | — |
| 16 | [Reports + Exports](#module-16--reports) | T-930 → T-1000 | — |
| 17 | [Admin sweep](#module-17--admin-sweep) | T-1010 → T-1180 | — |
| 18 | [Outbox + Webhooks](#module-18--outbox--webhooks) | T-1190 → T-1240 | — |
| 19 | [Cross-module marquee workflows](#module-19--cross-module-marquee-workflows) | T-1250 → T-1330 | — |
| 20 | [API surface (controllers)](#module-20--api-surface) | T-1340 → T-1380 | — |
| 21 | [Final validation + sign-off](#module-21--final-validation) | T-1390 → T-1410 | ⚠ GATING |

Total: ~600+ steps. Realistic execution time: 4–8 hours of agent work depending on screen-load times.

---

## Module 1 — Pre-flight + Login

**Goal:** Confirm app is up, log in once, land on Dashboard.

| Step | Action | Expected | Verify |
|---|---|---|---|
| **T-001** | Navigate to root URL (the ONLY allowed URL nav of this run). | Either Login page OR Dashboard renders. No 5xx. | HTTP 200; page title contains "ABS Machining EAM" (the customer-facing name) — footer reads "ABS Machining EAM, powered by CherryAI". (Per DEV-002 from RUN-20260508-132947.) |
| **T-002** | If Login page: type Username, type Password, click "Sign In". | Redirected to Dashboard. Top nav shows "Home / Assets / Books / Journals / Reports". | Cookie `.AspNetCore.Cookies` set; URL is `/Index` or `/`. |
| **T-003** | Open browser dev console; check for any JS errors on Dashboard. | No red errors. Yellow CSP warnings ok. | Console clean of `Uncaught` errors. |
| **T-004** | Note the Dashboard's headline KPIs (Total Asset Value, NBV, Accumulated Depreciation, Open WOs, Active CIP, Pending Approvals). Screenshot baseline. | All six KPI cards render with numeric values. | None show "$NaN" or "undefined". |
| **T-005** | Open `/Admin/Diagnostics` (the only non-nav-link page reached via the "System Diagnostics" quick action card on the dashboard, if visible). Capture the diagnostic readout. | Page shows app version, DB connectivity, lookup-cache stats, schema-validation summary. | No `[FATAL]` lines. |
| **T-006** | Back-button to Dashboard. | Dashboard renders again with same KPIs. | — |
| **T-007** | [GATING] If `T-001`–`T-006` failed, stop here and report. The whole plan depends on auth working. | — | — |
| **T-008** | Run the SQL baseline query in the Replit shell: `psql "$DATABASE_URL" -c 'SELECT "EventType", COUNT(*) FROM "OutboxEvents" GROUP BY "EventType" ORDER BY "EventType";'`. Save output as `outbox-baseline.txt`. | Either zero rows or a small set of pre-existing event types. | Captured for diff at the end. |
| **T-009** | Run `psql "$DATABASE_URL" -c 'SELECT COUNT(*) FROM "Assets", (SELECT COUNT(*) AS jes FROM "JournalEntries") j;'` (or equivalent counts of Assets, JournalEntries, OutboxEvents, MaintenanceEvents). Save as `db-baseline.txt`. | Counts captured. | — |
| **T-010** | Confirm time of run start. Record run-id `RUN-{YYYYMMDD-HHmmss}` for all timestamped objects below. | — | — |

---

## Module 2 — Navigation map

**Goal:** Click every link in the global navigation. Every page must render without 5xx. No URL typing.

The global nav comes from `Pages/Shared/_ModernLayout.cshtml`. The top navbar (`_Layout.cshtml`) has 5 entries; the modern sidebar/menu has the full ~50-link surface. Click each in order.

### Top navbar (always visible)

| Step | Click | Expected page |
|---|---|---|
| T-020 | "Home" | `/Index` Dashboard |
| T-021 | "Assets" | `/Assets/Index` |
| T-022 | "Books" | `/Books/Index` |
| T-023 | "Journals" | `/Journals/Index` |
| T-024 | "Reports" | `/Reports/ReportHub` |

### Sidebar / Modern menu (full surface)

For each click, the agent must:
- Verify the page renders (no 5xx, no exception page).
- Note the page title.
- Capture a screenshot.
- Return to the previous page (Back button) before clicking the next link, OR re-open the menu and click the next link from there.

**Operations / Maintenance submenu**

| Step | Click | Expected page |
|---|---|---|
| T-030 | "Maintenance" → root | `/Maintenance` |
| T-031 | "Work Orders" within Maintenance (if separate link) | `/Maintenance` (same) or `/WorkOrders` |
| T-032 | "Work Requests" | `/Maintenance/WorkRequests` |
| T-033 | "Schedules" | `/Maintenance/Schedules` |
| T-034 | "Schedule Board" | `/Maintenance/ScheduleBoard` |
| T-035 | "Assignments" | `/Maintenance/Assignments` |
| T-036 | "PM Templates" | `/Maintenance/PMTemplates` (renders the same page as `/Admin/PMTemplates`) |
| T-037 | "Technicians" | `/Maintenance/Technicians` |

**Purchasing / Procurement**

| Step | Click | Expected page |
|---|---|---|
| T-040 | "Purchasing" | `/Purchasing` |
| T-041 | "Requisitions" | `/Purchasing/Requisitions` (alias of `/Admin/Requisitions`) |
| T-042 | "Receiving" | `/Receiving` |
| T-043 | "Accounts Payable" | `/AccountsPayable` |

**Materials / Inventory**

| Step | Click | Expected page |
|---|---|---|
| T-050 | "Materials → Items" | `/Materials/Items` |
| T-051 | "Materials → Vendors" | `/Materials/Vendors` |
| T-052 | "Materials → Categories" | `/Materials/Categories` (alias of `/Admin/ItemCategories`) |
| T-053 | "Materials → Kits" | `/Materials/Kits` (alias of `/Admin/Kits`) |
| T-054 | "Inventory" | `/Inventory` |
| T-055 | "Stock Levels" | `/Inventory/StockLevels` (alias of `/Admin/StockLevels`) |

**Asset Management**

| Step | Click | Expected page |
|---|---|---|
| T-060 | "Assets → Categories" | `/Assets/Categories` (alias of `/Admin/AssetCategories`) |
| T-061 | "Assets → Locations" | `/Assets/Locations` (alias of `/Admin/Locations`) |
| T-062 | "Assets → Barcodes" | `/Assets/Barcodes` (alias of `/Admin/Barcodes`) |
| T-063 | "Bulk Operations" | `/BulkOperations` |

**Finance**

| Step | Click | Expected page |
|---|---|---|
| T-070 | "Books" | `/Books/Index` |
| T-071 | "GL Accounts" | `/Books/GlAccounts` (or `/Admin/GlAccounts`) |
| T-072 | "Journals" | `/Journals/Index` |
| T-073 | "CIP" | `/CIP` |
| T-074 | "CIP Costs" | `/CIP/Costs` |
| T-075 | "CIP Party Drilldown" | `/CIP/PartyDrilldown` |
| T-076 | "CCA" (Canadian capital cost allowance) | `/CCA` |
| T-077 | "US Tax" | `/UsTax` |

**Reports**

| Step | Click | Expected page |
|---|---|---|
| T-080 | "Reports → Hub" | `/Reports/ReportHub` |
| T-081 | "Reports → Index" | `/Reports/Index` (server may redirect to `/Reports/ReportHub`; either is acceptable) |

**Admin**

| Step | Click | Expected page |
|---|---|---|
| T-090 | "Admin → Companies" | `/Admin/Companies` |
| T-091 | "Admin → Sites" | `/Admin/Sites` |
| T-092 | "Admin → Departments" | `/Admin/Departments` |
| T-093 | "Admin → Cost Centers" | `/Admin/CostCenters` |
| T-094 | "Admin → Manufacturers" | `/Admin/Manufacturers` |
| T-095 | "Admin → Project Managers" | `/Admin/ProjectManagers` |
| T-096 | "Admin → Users" | `/Admin/Users` |
| T-097 | "Admin → Lookups" | `/Admin/Lookups` |
| T-098 | "Admin → System Settings" | `/Admin/SystemSettings` |
| T-099 | "Admin → Audit Log" | `/Admin/AuditLog` |
| T-100 | "Admin → Webhooks" / "Integrations" / "Outbox" | `/Admin/Webhooks` (and verify menu shows `/Admin/Integrations`, `/Admin/Outbox/Index`, `/Admin/Webhooks/Catalog`, `/Admin/Webhooks/Deliveries`) |

**Result of Module 2:** Every link in the global nav surface has been visited exactly once, with no 5xx and no JS errors. If any nav link is broken, that's a P0 finding — capture the URL the link pointed at vs. what rendered.

---

## Module 3 — Home dashboard

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-110 | Click the "Total Asset Value" KPI card. | Drilldown to `/Assets` (asset grid). | URL changed to `/Assets`. |
| T-111 | Back. Click the "Net Book Value" KPI card. | Either drills down or no-op (per design). | If no-op, document. |
| T-112 | Back. Click the "Accumulated Depreciation" KPI. | Same — drilldown or no-op. | — |
| T-113 | Back. Click the "Open Work Orders" KPI. | `/Maintenance` opens. | URL `/Maintenance`. |
| T-114 | Back. Click the "Active CIP" KPI. | `/CIP` opens. | URL `/CIP`. |
| T-115 | Back. Click "Pending Approvals" KPI. | `/Admin/Approvals` (or appropriate). | — |
| T-116 | Scroll down on the dashboard. Click each `quick-action-card` link in turn (Help Center, System Diagnostics, etc.). | Each renders without 5xx. | — |
| T-117 | If there's a "Recent Activity" or "Recent Work Orders" panel: click each row's link. Each must drill to a detail page. | — | — |
| T-118 | Verify the `_IndexContext` partial (header context strip) renders company/site selector. Click it; confirm dropdown opens; click any visible site/company. | Page reloads scoped to the selected company/site. | URL unchanged but data may differ. |
| T-119 | Re-select the original company/site if the change above re-scoped data. | Original KPIs return. | — |
| T-120 | Click the screen-header "Actions" button cluster (`_IndexActions`). Each action button (Generate, Quick Add, etc.) opens its target page or modal. | — | — |
| T-130 | Final dashboard sanity: refresh the page once. KPIs re-render with the same numbers. | — | DOM-ready JS errors none. |

---

## Module 4 — Assets module

**Pages exercised:** `/Assets/Index`, `/Assets/Asset` (view/edit/create), `/Assets/Improve`, `/Assets/Dispose`, `/Assets/Transfer`, `/Assets/Schedule`, `/Assets/Delete`, plus tabs/sub-actions on the asset detail page (machine spec, meter readings, attachments).

### 4.1 Asset list

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-140 | Navigate to `/Assets/Index` via top nav "Assets". | Premium DataGrid renders with seeded assets. | Pagination control present. |
| T-141 | Click each column header in turn to sort asc/desc. | Grid re-orders, no 5xx. | — |
| T-142 | Type into the search box at the top. | Grid filters live. | — |
| T-143 | Click the "Filter" button if present; set one filter (e.g., Status = Active). Apply. | Grid reduces. | — |
| T-144 | Click "Clear filters" / "Reset". | Grid restores. | — |
| T-145 | Click any pagination control (next page, page number). | Page 2+ loads. | — |
| T-146 | Click the "Export" button if present. | CSV/XLSX download starts. | File saved with rows > 0. |
| T-147 | Click "Add Asset" / "+ New". | Asset create form opens at `/Assets/Asset?mode=create`. | URL contains `mode=create`. |
| T-148 | Cancel back to grid (Back link). | Grid restored. | — |

### 4.2 Asset create

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-150 | From `/Assets/Index`, click "Add Asset". | Create form. | — |
| T-151 | Fill: AssetNumber=`E2E-A-{HHmmss}`, Description=`E2E test asset`, AcquisitionCost=`10000`, InServiceDate=today, UsefulLifeMonths=`60`, DepreciationMethod=Straight Line. Pick first Asset Category, Location, Manufacturer. | Form valid. | — |
| T-152 | Click "Save". | Redirect to `/Assets/Asset?id={N}&mode=view`. Success toast/banner. | DB: `Assets` count +1. Outbox: `asset.created` row with `Origin="ui.assets.create"`. |
| T-153 | Verify outbox via SQL: `SELECT "EventType", "EntityId", "PayloadJson"::jsonb -> 'origin' FROM "OutboxEvents" WHERE "EventType" = 'asset.created' ORDER BY "Id" DESC LIMIT 1;` | One row, Origin shows `ui.assets.create`. | — |
| T-154 | On the new asset detail page, capture the asset id (note as `ASSET_ID_E2E`). | — | — |

### 4.3 Asset detail tabs

The asset page uses `_TabNav` partial. Click every tab in turn, exercising any handler-driven sub-form:

| Step | Action | Expected | Verify |
|---|---|---|---|
**Live tab labels** (verified RUN-20260508-132947, DEV-003): General, Location, Financial, Technical, MES/OEE, IoT, Safety, Warranty, Hierarchy, Attachments, Transactions. Tabs are operations-engineering flavoured, not the accounting-flavoured set the plan originally listed.

| T-160 | Click "General" tab (default). | Asset summary, KPIs, headline fields. | — |
| T-161 | Click "Technical" / Machine Spec section. Fill any required fields, click Save. | OnPostSaveMachineSpecAsync fires. Saved confirmation. | DB: `MachineSpecifications` row. |
| T-162 | Find the Meter Readings panel (under General or its own tab). Click "Add Reading"; enter NewMeterReading=`100`, NewMeterReadingDate=today. Save. | OnPostAddMeterReadingAsync fires. | DB: `MeterReadings` +1. |
| T-163 | Open the asset's Maintenance section / linked WO list. Confirm any open WOs against this asset are listed. | — | — |
| T-164 | Click "Transactions" tab. Confirm transaction history (acquisition stamp). | — | — |
| T-165 | Click "Attachments" tab. Click "Upload"; pick a small txt/pdf file; click Upload. | OnPostUploadAsync fires. File appears in list. | DB: `Attachments` row. |
| T-166 | On the new attachment, click "Delete". | OnPostDeleteAttachmentAsync fires; row removed. | — |
| T-167 | Find the photo/image upload control (likely under General). Click "Upload Image"; pick a PNG/JPG; submit. | OnPostUploadImageAsync fires; image renders. | — |
| T-168 | Click each remaining tab (Location, Financial, IoT, Safety, Warranty, Hierarchy). Each renders without 5xx. | — | — |

### 4.4 Asset edit

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-170 | On asset detail, click "Edit" button. | Page switches to `mode=edit`. Fields editable. | — |
| T-171 | Change Description to `E2E test asset (edited)`. Click "Save". | Redirect to view. Description updated. | DB row's `ModifiedAt` updated. RowVersion advanced. |
| T-172 | (Optional concurrency test) Open the same asset in a second tab, edit Description differently, save. The first tab's later save should produce a yellow conflict banner. | Concurrency conflict UI. | — |

### 4.5 Asset Improve (capital improvement)

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-180 | On asset detail, click "Improve" / "Add Capital Improvement" button. | `/Assets/Improve?id={N}` opens. | — |
| T-181 | Fill: Description=`E2E improvement`, Cost=`2500`, ImprovementDate=today, Capitalize=checked, UsefulLifeExtension=`6`. Click Save. | Redirect to asset detail. AcquisitionCost = old + 2500. UsefulLifeMonths = old + 6. | DB: `CapitalImprovements` +1. Outbox: `asset.improved` row. AssetBookSettings BookValue restamped. |
| T-182 | SQL verify: `SELECT "PayloadJson"::jsonb FROM "OutboxEvents" WHERE "EventType" = 'asset.improved' ORDER BY "Id" DESC LIMIT 1;` — payload has correct `cost`, `newAcquisitionCost`, `capitalized`. | — | — |
| T-183 | Period-lock test: in another tab, lock current period via `/Admin/SystemSettings`. Re-attempt Improve with today's date. | ModelState error: "Posting period is not open." Asset cost unchanged. | — |
| T-184 | Unlock period before continuing. | — | — |

### 4.6 Asset Transfer

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-190 | On asset detail, click "Transfer". | `/Assets/Transfer` opens. | — |
| T-191 | Pick a different Location, optionally Department / Site. Click Save. | Redirect; new location stamped. | DB: `Asset.LocationId` updated. (Optional: `AssetTransfers` audit row.) |

### 4.7 Asset Schedule (depreciation schedule)

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-200 | From asset detail, click "Schedule" tab/link. | `/Assets/Schedule/{id}` (route shape; `?id=` returns 404 — path-bound). Shows month-by-month depreciation schedule. | Schedule has UsefulLifeMonths rows. |
| T-201 | Click "Export" if available. | CSV downloads. | — |

### 4.8 Asset Dispose

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-210 | Create a second test asset (T-150 pattern, AssetNumber=`E2E-DISP-{HHmmss}`, Cost=5000). Note its id `ASSET_DISP_E2E`. | — | — |
| T-211 | Open the new asset → click "Dispose". | `/Assets/Dispose?id={DISP}` opens. | — |
| T-212 | Pick DisposalReason (e.g., Sale), Proceeds=`3000`, DisposalExpense=`100`, BookId=first available, CreateJournalEntry=checked. Click Dispose. | Redirect to asset detail. Status=Disposed. JE posted. | DB: `Asset.Status` = Disposed; `JournalEntry` row with `Source='Disposal'`. Outbox: `asset.disposed` row. |
| T-213 | SQL verify: `SELECT "PayloadJson"::jsonb FROM "OutboxEvents" WHERE "EventType" = 'asset.disposed' ORDER BY "Id" DESC LIMIT 1;` — payload has `gainLoss`, `journalEntryId`, `disposalType="Sale"`. | — | — |
| T-214 | Try to dispose the same asset again. | Page shows "already disposed" message; no second JE. | — |

### 4.9 Asset Delete (soft)

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-220 | Create a third throwaway asset. Open it. Click "Delete". | `/Assets/Delete?id={N}` confirmation page. | — |
| T-221 | Click "Confirm Delete". | Redirect; asset gone from grid OR marked deleted. | — |

---

## Module 5 — Materials

**Pages:** `/Materials/Items`, `/Materials/ItemEdit`, `/Materials/Vendors`, `/Materials/Vendors/Create`, `/Materials/Vendors/Edit`, plus the admin-side `/Admin/Items`, `/Admin/ItemCategories`, `/Admin/Manufacturers`, `/Admin/Kits`, `/Admin/StockLevels`.

### 5.1 Items list + edit

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-230 | Navigate to `/Materials/Items`. | Items grid. | — |
| T-231 | Click any item row. | `/Materials/ItemEdit?id={N}`. | — |
| T-232 | Run through the tab cluster on ItemEdit (uses `_TabNav`): General, Revisions, Vendors, Alternates, Supersession, Image. | Each renders. | — |
| T-233 | Click "Save" with no changes. | Save succeeds, no error. OnPostSaveItemAsync. | — |
| T-234 | On Revisions tab: click "Create Draft". Fill RevisionCode=`E2E-R1`, name=`E2E rev`. Save. | OnPostCreateDraftAsync + OnPostSaveRevisionAsync fire. New revision visible. | DB: `ItemRevisions` +1. |
| T-235 | Click "Release Draft" with a change reason. | OnPostReleaseDraftAsync. Revision now Released. Item.CurrentReleasedRevisionId points at it. | — |
| T-236 | Click another revision → "Obsolete". | OnPostObsoleteRevisionAsync. Status → Obsolete. | — |
| T-237 | On the new revision (still in draft state if you create another), click "Delete Draft". | OnPostDeleteRevisionAsync. Row removed. | — |
| T-238 | Vendors tab: click "Add Approved Vendor". Pick a vendor, status=Approved, Preferred=unchecked. Save. | OnPostAddApprovedVendorAsync. Row added. | — |
| T-239 | Click "Set Preferred" on that vendor. | OnPostSetPreferredVendorAsync. IsPreferred=true. | — |
| T-240 | Click "Remove" on the same vendor. | OnPostRemoveApprovedVendorAsync. Row gone. | — |
| T-241 | Vendors tab → "Add VPN" (vendor part number). Submit. | OnPostAddVpnAsync. Row added. | — |
| T-242 | Click "Update VPN catalog" link/button on the row, change a URL, save. | OnPostUpdateVpnCatalogAsync. | — |
| T-243 | Click "Enrich VPN" if visible. | OnPostEnrichVpnAsync; possibly external API call (may no-op without keys). | — |
| T-244 | Click "Copy External Image" if a VPN has an externalImageUrl. | OnPostCopyExternalImageAsync. Item image populated. | — |
| T-245 | Manufacturers tab → "Add MPN". Submit. | OnPostAddMpnAsync. | — |
| T-246 | Alternates tab → "Add Alternate". Pick another item, type=Equivalent, rank=1. Save. | OnPostAddAlternateAsync. | — |
| T-247 | Click "Remove" alternate. | OnPostRemoveAlternateAsync. | — |
| T-248 | Supersession tab → "Set Supersession". Pick a successor item. Save. | OnPostSetSupersessionAsync. | — |
| T-249 | Click "Remove Supersession". | OnPostRemoveSupersessionAsync. | — |
| T-250 | Image tab → "Upload Image", pick a PNG. | OnPostUploadImageAsync. Image rendered. | — |
| T-251 | Click "Remove Image". | OnPostRemoveImageAsync. | — |

### 5.2 Vendors

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-260 | Navigate `/Materials/Vendors`. | Grid. | — |
| T-261 | Click "Add Vendor". | `/Materials/Vendors/Create`. | — |
| T-262 | Fill: code=`E2E-V-{HHmmss}`, name=`E2E Vendor`, type=first option, paymentTerms=first option, taxId, etc. Click Save. | OnPostAsync. Redirect to grid; new vendor visible. | DB: `Vendors` +1. |
| T-263 | Open the new vendor. | `/Materials/Vendors/Edit?id={N}`. | — |
| T-264 | Click each tab on the vendor edit page. | Each renders. | — |
| T-265 | Update Phone field, click Save (active tab "general"). | OnPostUpdateAsync. | — |
| T-266 | Click "Duplicate Vendor". | OnPostDuplicateAsync; new draft created. | — |
| T-267 | On the duplicated vendor, click "Toggle Status" (deactivate). | OnPostToggleStatusAsync. | — |

### 5.3 Materials → Categories (Admin)

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-280 | `/Admin/ItemCategories`. | Grid. | — |
| T-281 | Click "Create"; fill code=`E2E-IC`, name=`E2E Cat`, type=first lookup. Save. | OnPostCreateAsync. | DB: `ItemCategories` +1. |
| T-282 | Edit the new row; rename. Save. | OnPostUpdateAsync. | — |
| T-283 | Click "Delete" on the same row. | OnPostDeleteAsync. | — |

### 5.4 Materials → Kits

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-290 | `/Admin/Kits`. | — | — |
| T-291 | Click "Add Kit"; fill KitNumber=`E2E-K1`, Name=`E2E Kit`. Save. | OnPostAddAsync. | DB: `Kits` +1. |
| T-292 | Click "Delete". | OnPostDeleteAsync. | — |

### 5.5 Stock Levels (read-only review)

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-300 | `/Admin/StockLevels`. | Grid showing reorder points etc. | — |
| T-301 | Click any item row to drill into ItemEdit. | — | — |
| T-310 | (placeholder) | — | — |

---

## Module 6 — Inventory

**Pages:** `/Inventory/Index`, `/Inventory/List`, `/Admin/Inventory` (transactions / cycle count).

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-320 | `/Inventory/Index`. | Grid of inventory snapshots / cycle-count lists. | — |
| T-321 | Click "Generate Barcodes" if visible. | OnPostGenerateBarcodesAsync. PDF/HTML preview opens. | — |
| T-322 | Click "Create List"; fill name=`E2E-INV-{HHmmss}`, location=first available. Save. | OnPostCreateListAsync. New list in grid. | DB: `InventoryLists` +1. |
| T-323 | Open the new list (`/Inventory/List?id={N}`). | List detail. | — |
| T-324 | Click "Start Count". | OnPostStartAsync. Status transitions. | — |
| T-325 | Add a scan: pick an item, enter quantity. | OnPostAddScanAsync. Scan row added. | — |
| T-326 | Click "Complete Count". | OnPostCompleteAsync. Status → Completed. | — |
| T-330 | `/Admin/Inventory` (transaction screen). | Tabs: Transactions, Cycle Counts. | — |
| T-331 | "Add Transaction" → type=Receipt or Adjustment, item, qty, location. Save. | OnPostTransactionAsync. ItemInventory updated. | DB: `ItemTransactions` +1; `ItemInventory.QuantityOnHand` matches. |
| T-332 | "Cycle Count" tab → record count. Submit. | OnPostCycleCountAsync. | — |
| T-360 | (placeholder) | — | — |

---

## Module 7 — Maintenance

**Pages:** `/Maintenance/Index`, `/Maintenance/Create`, `/Maintenance/Details`, `/Maintenance/ScheduleBoard`, `/Maintenance/Schedules`, `/Maintenance/Assignments`, `/Maintenance/Technicians`, `/Maintenance/WorkRequests/*`, `/WorkOrders/Details`, plus PM Templates under Admin.

### 7.1 Work Requests

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-370 | `/Maintenance/WorkRequests`. | Grid. | — |
| T-371 | "+ New Request". `/Maintenance/WorkRequests/Create`. | Form. | — |
| T-372 | Fill: RequestText=`E2E pump grinding`, Priority=High, Asset=first. Click "Save Only". | OnPostSaveOnlyAsync. Saved; status=New. | DB: `WorkRequests` +1. |
| T-373 | Re-open the request. Click "Smart Assist". | OnPostSmartAssistAsync. AI suggestion appears (may stub if AI key absent). | — |
| T-374 | Click "Convert to WO". | OnPostConvertToWOAsync. Redirect to the new WO. | DB: `MaintenanceEvents` +1. WorkRequest.Status=ConvertedToWO. Outbox: `workorder.created` v1 row. |

### 7.2 Work Order detail (the flagship surface)

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-380 | On the new WO at `/WorkOrders/Details?id={WO}`: confirm header (WO number, asset, priority). | — | — |
| T-381 | Click "Add Operation". Title=`E2E Op 1`, Type=Inspection, plannedHours=2. Save. | OnPostAddOperationAsync. New op shows. | — |
| T-382 | Add 2 more operations. | — | — |
| T-383 | Click "Move Up" / "Move Down" arrows on an operation. | OnPostMoveOperationAsync. Sequence reorders. | — |
| T-384 | Click status dropdown on an operation; pick "In Progress". | OnPostUpdateStatusAsync. | — |
| T-385 | "Add Labor": pick technician, hours=1.5, rate=50. Save. | OnPostAddLaborAsync. | DB: `WorkOrderOperationLabor` +1. |
| T-386 | "Add Tool": name=`Wrench`, qty=1. Save. | OnPostAddToolAsync. | — |
| T-387 | "Add Part to Operation": pick item, planned=2, unitCost=10. Save. | OnPostAddPartAsync. | DB: `WorkOrderOperationPart` +1. |
| T-388 | "Add Planned Material" (legacy WorkOrderPart): pick item, planned=3. Save. | OnPostAddPlannedMaterialAsync. | DB: `WorkOrderParts` +1. |
| T-389 | Click "Issue Material" on the planned part: qty=2. | OnPostIssueMaterialAsync. ItemInventory decremented. ItemTransaction(Issue) row. | DB: `ItemTransactions` +1. Outbox: `item.issued` v1 row. |
| T-390 | SQL: `SELECT "PayloadJson"::jsonb FROM "OutboxEvents" WHERE "EventType" = 'item.issued' ORDER BY "Id" DESC LIMIT 1;` | Payload has correct `quantity`, `newQuantityOnHand`, `workOrderId`. | — |
| T-391 | Click "Return Material" on the same part: qty=1. | OnPostReturnMaterialAsync. ItemInventory incremented. NO `item.issued` event. | DB: `ItemTransactions` +1 (Return). |
| T-392 | "Remove Planned Material" (on a part with no issuance). | OnPostRemovePlannedMaterialAsync. | — |
| T-393 | "Load Template Materials" if visible. | OnPostLoadTemplateMaterialsAsync. (May no-op if no template.) | — |
| T-394 | "Start" the WO. | OnPostStartAsync (Maintenance/Details). Status → InProgress. | — |
| T-395 | "Edit" the WO header. Update Description. Save. | OnPostEditAsync. | — |
| T-396 | "Dispatch Update" if visible. | OnPostDispatchUpdateAsync. | — |
| T-397 | Capitalize: click "Capitalize"; amount=`500`, description=`E2E capitalize`. Save. | OnPostCapitalizeAsync. Asset.AcquisitionCost += 500. CapitalImprovement row. | DB: `CapitalImprovements` +1. Asset RowVersion advances. |
| T-398 | Upload an attachment. Delete it. | OnPostUploadAsync / OnPostDeleteAttachmentAsync. | — |
| T-399 | "Cancel" the WO (separate test WO recommended). | OnPostCancelAsync. Status → Cancelled. | — |
| T-400 | On a *different* WO that's been completed: click "Close Work Order". | OnPostCloseWorkOrderAsync (CloseoutService). Outbox: `workorder.closed` + `closeout.summary.generated`. | — |
| T-401 | After closeout, click "Save Lesson". | OnPostSaveLessonAsync. Outbox: `lesson.saved`. | — |

### 7.3 Maintenance index

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-410 | `/Maintenance` index. Filters/grid. | — | — |
| T-411 | Click "Create Event". | OnPostCreateEventAsync (modal or redirect). | — |
| T-412 | Open one open WO from the grid. Confirm full WO Details flow re-runs. | — | — |

### 7.4 Maintenance Create page

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-420 | `/Maintenance/Create`. | Form. | — |
| T-421 | Fill required fields, save. | OnPostAsync. New event. | — |

### 7.5 Schedule Board

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-430 | `/Maintenance/ScheduleBoard`. | Gantt-style board with operations / techs. | — |
| T-431 | Drag an operation to a technician (or click "Assign"). | OnPostAssignAsync. | — |
| T-432 | Drag to a different time slot. | OnPostRescheduleAsync. | — |
| T-433 | Click "Unassign". | OnPostUnassignAsync. | — |

### 7.6 Schedules / Assignments / Technicians

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-440 | `/Maintenance/Schedules`. | List of legacy + modern schedules. | — |
| T-441 | `/Maintenance/Assignments`. | PM-Asset assignments. | — |
| T-442 | "Create Assignment". Pick asset + PM template + next due. Save. | OnPostCreateAsync. | — |
| T-443 | "Toggle Active" on the new row. | OnPostToggleActiveAsync. | — |
| T-444 | "Delete". | OnPostDeleteAsync. | — |
| T-450 | `/Maintenance/Technicians`. | Grid. | — |
| T-451 | Open a technician → `/Maintenance/Technicians/Profile`. | Profile page. | — |
| T-452 | "Edit Profile". OnPostEditProfileAsync. | — | — |
| T-453 | "Add Certification". OnPostAddCertificationAsync. | — | — |
| T-454 | "Remove Certification". OnPostRemoveCertificationAsync. | — | — |
| T-455 | "Add Skill" / "Remove Skill". | OnPostAddSkillAsync / RemoveSkillAsync. | — |
| T-456 | "Toggle Active". OnPostToggleActiveAsync. | — | — |
| T-457 | "Upload Photo". OnPostUploadPhotoAsync. | — | — |

### 7.7 PM Templates (admin) → drives `pm.occurrence.generated`

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-470 | `/Admin/PMTemplates`. Grid. | — | — |
| T-471 | Open a template → `/Admin/PMTemplateEdit`. | Form. | — |
| T-472 | "Save" with no edits. OnPostAsync. | — | — |
| T-473 | "Create Draft". OnPostCreateDraftAsync. | — | — |
| T-474 | On the draft: "Save Revision" (edit fields), "Release Draft", "Delete Draft" — click each in turn (some on different draft revs). | Respective handlers fire. | — |
| T-475 | "Delete Template" (do this on a throwaway). | OnPostDeleteAsync. | — |
| T-480 | `/Admin/PMSchedules`. | List. | — |
| T-481 | Open a schedule → `/Admin/PMScheduleEdit`. Edit cadence, save. | OnPostAsync. | — |
| T-482 | (Optional) Trigger PM generation: navigate to wherever "Generate Due PMs" lives (likely `/Maintenance` or admin), click it. Expect new MaintenanceEvent + outbox `pm.occurrence.generated`. | — | DB: `PMOccurrences` +1. Outbox: `pm.occurrence.generated`. |
| T-490 | (placeholder) | — | — |

---

## Module 8 — Purchasing

**Pages:** `/Purchasing/Index`, `/Purchasing/Create`, `/Purchasing/Details`.

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-500 | `/Purchasing`. | PO grid. | — |
| T-501 | Click each column-sort, search, filter, pagination. | — | — |
| T-502 | "Export" CSV. | File downloads. | — |
| T-503 | "+ Create PO". `/Purchasing/Create`. | Form. | — |
| T-504 | Fill: vendor=first available, type=first lookup, orderDate=today, requiredDate=+30d, notes, optional cipProjectId. Save. | OnPostAsync (Create) OR OnPostCreatePOAsync (from Index). PO Draft created. | DB: `PurchaseOrders` +1, status=Draft. |
| T-505 | On detail: header "Update". Click "Update Header"; change requiredDate. Save. | OnPostUpdateHeaderAsync. | — |
| T-506 | "Add Line": pick item, qty=10, unitPrice=100, descr, GL account, optional CipProjectId. Save. | OnPostAddLineAsync. Line row appears. | — |
| T-507 | Add 2 more lines (one stock item, one service if available). | — | — |
| T-508 | "Update Line" on one: change qty=5. | OnPostUpdateLineAsync. | — |
| T-509 | "Delete Line" on one. | OnPostDeleteLineAsync. | — |
| T-510 | "Add Release" on a line: qty=5, shipTo=first location, dueDate=today+7. | OnPostAddReleaseAsync. | DB: `PurchaseOrderReleases` +1. |
| T-511 | "Delete Release". | OnPostDeleteReleaseAsync. | — |
| T-512 | "Submit for Approval". | OnPostSubmitForApprovalAsync. Status → PendingApproval. | — |
| T-513 | "Approve". | OnPostApproveAsync. Status → Approved. ApprovedAt stamped. | DB: status flip. Outbox: `po.approved` v1 row. |
| T-514 | SQL: `SELECT "PayloadJson"::jsonb FROM "OutboxEvents" WHERE "EventType" = 'po.approved' ORDER BY "Id" DESC LIMIT 1;` — payload has correct `purchaseOrderId`, `total`, `vendorId`. | — | — |
| T-515 | Try to Approve again. | No-op (status already Approved). No second outbox event. | — |
| T-520 | On a *different* draft PO: "Duplicate PO". | OnPostDuplicatePOAsync. New draft created with copies of lines. | — |
| T-521 | On a *throwaway* draft PO: "Delete PO". | OnPostDeletePOAsync. Removed from grid. | — |
| T-560 | (placeholder) | — | — |

---

## Module 9 — Receiving

**Pages:** `/Receiving/Index`, `/Receiving/Receive`, `/Receiving/Details`, `/Receiving/Inspect`, `/Receiving/History`.

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-570 | `/Receiving`. | List of approved POs awaiting receipt. | — |
| T-571 | Click the approved PO from T-513 → "Receive". | `/Receiving/Receive?id={PO}`. | — |
| T-572 | On the line, enter QuantityToReceive=`5` (partial), receivingLocation=first, headerNotes=`E2E partial receipt`. Click "Save Receipt". | OnPostReceiveAsync. Receipt created. PO status → PartiallyReceived. JE posted (Dr Inventory / Cr GR-Accrued). ItemInventory +5. ItemTransaction(Receipt). | DB: `GoodsReceipts` +1. `JournalEntries` +1 (Source='GR'). Outbox: `po.received` (IsFullyReceived=false), `item.received` (1 row per stock line). |
| T-573 | SQL: `SELECT "EventType", COUNT(*) FROM "OutboxEvents" WHERE "EntityId" = '{PO}' GROUP BY "EventType";` — confirms `po.approved` (1) + `po.received` (1). | — | — |
| T-574 | Receive the remaining qty. PO → Received. | Outbox: another `po.received` (IsFullyReceived=true). | — |
| T-580 | `/Receiving/Inspect?id={GR}`. | Inspect screen for the GR. | — |
| T-581 | Adjust inspection qty (mark some as Rejected). Click Inspect. | OnPostInspectAsync. | — |
| T-582 | "Complete Inspection". | OnPostCompleteAsync. | — |
| T-590 | `/Receiving/Details?id={GR}`. | Receipt detail. | — |
| T-600 | `/Receiving/History`. | Past receipts. | — |
| T-610 | (placeholder) | — | — |

---

## Module 10 — Accounts Payable

**Pages:** `/AccountsPayable/Index`, `/AccountsPayable/Create`, `/AccountsPayable/Details`.

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-620 | `/AccountsPayable`. Grid. | — | — |
| T-621 | "+ Create Invoice" → `/AccountsPayable/Create`. | Form. | — |
| T-622 | Fill: vendor matching the PO from T-572, invoiceNumber=`E2E-INV-{HHmmss}`, invoiceDate=today, amount matching PO total, dueDate=+30d, description. Save. | OnPostAsync. Invoice Draft. | DB: `VendorInvoices` +1, status=Draft. |
| T-623 | Open the invoice → `/AccountsPayable/Details`. | Detail page. | — |
| T-624 | Add a line manually (if Lines section visible) OR rely on Auto-Match. Click "Auto Match". | OnPostAutoMatchAsync. Lines linked to PO/GR lines. | — |
| T-625 | "Link Line" manually on one line: pick poLine, grLine. Save. | OnPostLinkLineAsync. | — |
| T-626 | "Unlink Line" on the same. | OnPostUnlinkLineAsync. | — |
| T-627 | Re-link via Auto Match. | — | — |
| T-630 | "Approve" the invoice. | OnPostApproveAsync (calls ApPostingService.PostApprovalAsync). 3-way match runs. JE posted (Dr GR-Accrued/Expense+PPV / Cr AP). | DB: `JournalEntries` +1 (Source='AP', Reference='AP-APR-{InvoiceNumber}'). VendorInvoice.Status=Approved. Outbox: `invoice.approved` v1 row. |
| T-631 | SQL: `SELECT "PayloadJson"::jsonb FROM "OutboxEvents" WHERE "EventType" = 'invoice.approved' ORDER BY "Id" DESC LIMIT 1;` — payload has correct `total`, `journalEntryId`, `matchStatus`. | — | — |
| T-632 | Click Approve again (idempotent replay). | Returns same JE id. No second outbox event. | DB: `OutboxEvents` count unchanged. |
| T-640 | "Record Payment": amount=full total, paymentMethod=Wire, referenceNumber=`E2E-WIRE-1`, notes. Save. | OnPostRecordPaymentAsync (calls PostPaymentAsync). JE posted (Dr AP / Cr Cash). Status → Paid (since fully paid). | DB: `JournalEntries` +1 (Source='AP', Reference contains 'PMT'). Outbox: `invoice.paid` v1 with `isFullyPaid=true`. |
| T-641 | SQL: `SELECT "PayloadJson"::jsonb FROM "OutboxEvents" WHERE "EventType" = 'invoice.paid' ORDER BY "Id" DESC LIMIT 1;` — payload has correct `amountPaid`, `runningTotalPaid=invoiceTotal`, `isFullyPaid=true`. | — | — |
| T-650 | Create a *second* invoice (T-622 pattern). Approve. Then Record Payment with amount=half. | Status stays Approved. Outbox: `invoice.paid` with `isFullyPaid=false`. | — |
| T-660 | On a *third* throwaway approved invoice: click "Void". Provide reason. | OnPostVoidAsync (calls PostVoidAsync). Contra-JE posted. | DB: `JournalEntries` contra row. Outbox: `invoice.voided` v1 with `previousStatus="Approved"`, `contraJournalEntryId` non-null. |
| T-661 | SQL: confirm payload. | — | — |
| T-680 | (placeholder) | — | — |

---

## Module 11 — CIP (Construction in Progress)

**Pages:** `/CIP/Index`, `/CIP/Details`, `/CIP/Costs`, `/CIP/CostDetails`, `/CIP/CostTypeDetails`, `/CIP/PartyDrilldown`.

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-690 | `/CIP`. | Grid of CIP projects. | — |
| T-691 | "Create Project". Fill name=`E2E-CIP-{HHmmss}`, code=`E2E-CIP`, plannedAmount=10000, status=Active. Save. | OnPostCreateProjectAsync. | DB: `CipProjects` +1. |
| T-692 | Open the new project → `/CIP/Details`. | Detail. | — |
| T-693 | "Add Cost": amount=2000, costType=Material, GL account, vendor, description. Save. | OnPostAddCostAsync. | DB: `CipCosts` +1. |
| T-694 | "Edit Project" (header). Change planned amount. Save. | OnPostEditProjectAsync. | — |
| T-695 | "Update Status" → next status (e.g., InReview). | OnPostUpdateStatusAsync. | — |
| T-696 | Upload an attachment. Delete it. | OnPostUploadAsync / OnPostDeleteAttachmentAsync. | — |
| T-700 | (Cross-module flow setup) Create a PO with `cipProjectId={NEW_CIP}` set in the header. Approve + receive. The CIP project should accrue cost via CipAutoCostPostingService. | DB: `CipCosts` increments from receipt. | — |
| T-710 | Back to CIP project: click "Capitalize". Fill assetNumber=`E2E-CIP-CAP-{HHmmss}`, description=`E2E capitalized asset`. Save. | OnPostCapitalizeAsync (CipCapitalizationService). New Asset row. JE posted (Dr Asset / Cr CIP). Depreciation snapshot recomputed. CipProject.Status=Capitalized. | DB: `Assets` +1, `JournalEntries` +1 (Source='CIP Capitalization'), `CipCapitalizations` +1. Outbox: `cip.capitalized` v1 + `asset.created` v1 (Origin="cip.capitalized"). |
| T-711 | SQL: `SELECT "EventType", "PayloadJson"::jsonb -> 'origin' FROM "OutboxEvents" WHERE "EventType" IN ('cip.capitalized', 'asset.created') ORDER BY "Id" DESC LIMIT 2;` — confirm origin tag distinguishes from UI-created assets. | — | — |
| T-720 | `/CIP/Costs`. Drill into one cost → `/CIP/CostDetails`. | — | — |
| T-721 | "Update Cost". Change amount. Save. | OnPostUpdateAsync. | — |
| T-722 | "Delete Cost" on a throwaway. | OnPostDeleteAsync. | — |
| T-730 | `/CIP/CostTypeDetails`. Read-only grouping. | — | — |
| T-731 | `/CIP/PartyDrilldown`. Vendor/party-level CIP cost view. | — | — |
| T-740 | (placeholder) | — | — |

---

## Module 12 — Books + GL Accounts

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-750 | `/Books/Index`. Grid of books. | — | — |
| T-751 | "Create Book". Fill code=`E2E-BK`, name=`E2E Book`, type=Financial, method=StraightLine, convention=FullMonth, GL accounts. Save. | OnPostAsync. | DB: `Books` +1. |
| T-752 | Open the new book → `/Books/Details`. Tabs render. | — | — |
| T-753 | "Edit Book". Change name. Save. | OnPostAsync (Edit). | — |
| T-754 | "GL Accounts" tab → `/Books/GlAccounts`. | Form to set DepExp / AccumDep / AssetClearing / Gain / Loss / Disposal. | — |
| T-755 | Fill all 6 accounts with valid GL codes. Save. | OnPostAsync (GlAccounts). BookGlAccount row written. | DB: `BookGlAccounts` +1. |
| T-756 | "Delete Book" on a throwaway book. | OnPostAsync (Delete). | — |
| T-770 | `/Admin/GlAccounts`. | Chart-of-accounts grid. | — |
| T-771 | "Create" → fill accountNumber=`9999`, name=`E2E Test`, type, category, normalBalance. Save. | OnPostCreateAsync. | DB: `GlAccounts` +1. |
| T-772 | Edit; change name; save. | OnPostUpdateAsync. | — |
| T-790 | (placeholder) | — | — |

---

## Module 13 — Journals + Depreciation

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-800 | `/Journals/Index`. JE list with filters (From/To, Source, Search). | — | — |
| T-801 | Apply each filter. Verify rows reduce. | — | — |
| T-802 | "Export" CSV. | File downloads with 8-column header. | — |
| T-803 | Click any JE → `/Journals/Details`. Lines render. | — | — |
| T-804 | Back. Click "Generate" → `/Journals/Generate`. | Form: book + month. | — |
| T-805 | Pick book = the new E2E book or first available, month = current month. Click "Generate". | OnPostAsync. JE created (Dr Dep Exp / Cr Accum Dep). | DB: `JournalEntries` +1 (Source='DEP'). Outbox: `depreciation.posted` v1 row. |
| T-806 | SQL: `SELECT "PayloadJson"::jsonb FROM "OutboxEvents" WHERE "EventType" = 'depreciation.posted' ORDER BY "Id" DESC LIMIT 1;` — payload has correct `bookId`, `period`, `totalDepreciation`. | — | — |
| T-807 | On `/Journals/Index`, also confirm there's an inline "Generate" form there (OnPostGenerateAsync). Use it for a different book/month combo. | Same outcome; second `depreciation.posted` row. | — |
| T-840 | (placeholder) | — | — |

---

## Module 14 — Bulk Operations

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-850 | `/BulkOperations`. | Grid; checkboxes. | — |
| T-851 | Select 2 assets via checkbox. Click "Bulk Transfer"; pick newLocation, newDepartment. Submit. | OnPostBulkTransferAsync. Both assets relocated. | DB: 2 rows updated. |
| T-852 | Select 2 assets. "Bulk Status Change" → newStatus. Submit. | OnPostBulkStatusChangeAsync. | — |
| T-853 | Pick one asset. "Partial Disposal" → percentage=20, saleProceeds=500, reason, buyer, notes. Submit. | OnPostPartialDisposalAsync. PartialDisposal row + JE if configured. | DB: `PartialDisposals` +1. |
| T-854 | `/BulkOperations/Details?id={N}`. View details. | — | — |
| T-880 | (placeholder) | — | — |

---

## Module 15 — CCA + US Tax

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-890 | `/CCA`. CCA index. | — | — |
| T-891 | `/CCA/ClassReport?fiscalYear=YYYY` (reach via clicking "Class Report" link, no URL typing). | Class report renders. | — |
| T-892 | Click "Calculate". | OnPostCalculateAsync. CCA computed for each class. | — |
| T-900 | `/UsTax`. | US Tax index. | — |
| T-920 | (placeholder) | — | — |

---

## Module 16 — Reports

The full reports surface lives at `/Reports/ReportHub` (top-nav "Reports") and `/Reports/Index`. Click each report card.

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-930 | `/Reports/ReportHub`. Card grid. | — | — |
| T-931 | Click "Depreciation Schedule" → `/Reports/DepreciationSchedule`. Pick asset + asOf. | Renders. | — |
| T-932 | Click "Depreciation Preview" → `/Reports/DepreciationPreview?asOf=YYYY-MM-DD`. | Period-end preview, all assets. | — |
| T-933 | "Export" → CSV downloads. | OnGetExportAsync. File saved. | — |
| T-940 | "Chart of Accounts" → `/Reports/ChartOfAccounts`. Renders. | — | — |
| T-941 | Export → CSV. | OnGetExportAsync. | — |
| T-950 | "Compliance" → `/Reports/Compliance?year=YYYY`. | Renders. | — |
| T-960 | "Form 4562" (US tax) → `/Reports/Form4562?taxYear=YYYY&companyId=X`. | Renders. | — |
| T-970 | "T2 Schedule 8" (Canadian) → `/Reports/T2Schedule8?fiscalYear=YYYY&companyId=X`. | Renders. | — |
| T-980 | "Report Builder" → `/Reports/Builder`. | Builder UI. | — |
| T-981 | Build a simple report (pick entity = Asset, fields, save). | OnPostAsync. | — |
| T-990 | "Export" generic → `/Reports/Export?type=Asset&format=csv`. | File downloads. | — |
| T-1000 | (placeholder) | — | — |

---

## Module 17 — Admin sweep

The biggest module. ~50 admin pages. For each, verify the page renders, then exercise its CRUD handlers.

### 17.1 Foundational data

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1010 | `/Admin/Tenants`. "Create": code, name. Save → OnPostCreateAsync. Edit → OnPostUpdateAsync. | DB: `Tenants` +1, then updated. | — |
| T-1015 | `/Admin/Companies`. Create / Edit. | — | — |
| T-1020 | `/Admin/Sites`. Create → OnPostCreateAsync. Edit → OnPostUpdateAsync. Delete throwaway → OnPostDeleteAsync. | — | — |
| T-1025 | `/Admin/Departments`. Create. Update. Delete. | — | — |
| T-1030 | `/Admin/CostCenters`. Create. Update. | — | — |
| T-1035 | `/Admin/Locations`. Create. Update. | — | — |
| T-1040 | `/Admin/Manufacturers`. Create / Update / Toggle Active. | — | — |
| T-1045 | `/Admin/ProjectManagers`. Create / Update / Toggle. | — | — |
| T-1050 | `/Admin/Users`. Create user → OnPostCreateAsync. Edit → OnPostUpdateAsync. Reset password → OnPostResetPasswordAsync. Toggle active → OnPostToggleActiveAsync. | DB: `Users` +1. PasswordHash uses Argon2id. | — |
| T-1055 | `/Admin/Vendors` (admin-side view). Read. | — | — |

### 17.2 Lookups + system settings

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1070 | `/Admin/Lookups/Index`. Grid of LookupTypes. | — | — |
| T-1071 | "Create Type" → key=`E2E_TEST_TYPE`, name. Save. | OnPostCreateTypeAsync. | DB: `LookupTypes` +1 with TenantId set (per feedback rule). |
| T-1072 | "Toggle Active" on the new row. | OnPostToggleActiveAsync. | — |
| T-1073 | Open the new type → `/Admin/Lookups/EditValues?typeId=X`. "Add Value" → code=`1`, name=`Test`, sortOrder=10. Save. | OnPostAddValueAsync. | DB: `LookupValues` +1. |
| T-1074 | "Update Value" on the same row. | OnPostUpdateValueAsync. | — |
| T-1075 | "Toggle Value Active". | OnPostToggleValueActiveAsync. | — |
| T-1080 | `/Admin/SystemSettings`. | — | — |
| T-1081 | "Lock Period" pick a period. | OnPostLockPeriodAsync. | DB: `FiscalPeriods.Status=Locked`. |
| T-1082 | "Unlock Period". | OnPostUnlockPeriodAsync. | — |
| T-1083 | `/Admin/ExchangeRates`. "Add" / "Edit" / "Delete". | OnPostAdd/Edit/DeleteAsync. | — |

### 17.3 Asset + Item categories

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1090 | `/Admin/AssetCategories`. Create → OnPostCreateAsync. Update → OnPostUpdateAsync. | — | — |
| T-1091 | `/Admin/ItemCategories`. (Already exercised at T-280s; revisit for Delete.) | — | — |
| T-1092 | `/Admin/Items` (admin item grid). Create / Update / Delete / Duplicate. | OnPostCreate/Update/Delete/DuplicateAsync. | — |
| T-1095 | `/Admin/Technicians` (admin grid). Create / Update / Toggle. | — | — |

### 17.4 Inventory admin

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1100 | `/Admin/Inventory`. "Add Transaction". | OnPostTransactionAsync. | — |
| T-1101 | "Cycle Count". | OnPostCycleCountAsync. | — |
| T-1102 | `/Admin/StockLevels`. Read-only review. | — | — |

### 17.5 Barcodes

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1110 | `/Admin/Barcodes`. "Generate" → BarcodeType, LabelSize, IncludeDescription. | OnPostGenerateAsync. PDF. | — |
| T-1111 | "Batch Print" → ItemIds, Copies. | OnPostBatchPrintAsync. | — |

### 17.6 Requisitions / Approvals

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1120 | `/Admin/Requisitions`. "Create" → OnPostCreateAsync. | — | DB: `Requisitions` +1. |
| T-1121 | "Approve" → OnPostApproveAsync. | — | — |
| T-1122 | "Convert to PO" → OnPostConvertToPOAsync. | New PO created from requisition. | — |
| T-1123 | "Duplicate" → OnPostDuplicateAsync. | — | — |
| T-1124 | "Create From Alert" → OnPostCreateFromAlertAsync. | — | — |
| T-1125 | "Dismiss Alert" → OnPostDismissAlertAsync. | — | — |
| T-1126 | `/Admin/Approvals`. Read-only review. | — | — |

### 17.7 Audit log + diagnostics

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1130 | `/Admin/AuditLog?page=1`. | Grid with audit entries. | — |
| T-1131 | Filter by user, then by entity type. | — | — |
| T-1132 | `/Admin/Diagnostics`. Read-only. | DB connectivity, schema-validation, lookup-cache stats, period-lock state. | No `[FATAL]`. |
| T-1133 | `/Admin/EnvironmentStatus`. Read-only. | Build info, version, env vars summary. | — |

### 17.8 Data ops

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1140 | `/Admin/DataManagement`. | Hub. | — |
| T-1141 | `/Admin/DataImport`. | Old import UI. | — |
| T-1142 | `/Admin/Import`. | Old import UI #2. | — |
| T-1143 | `/Admin/ImportWizard`. "Download Template" for entity=Vendor. | OnGetDownloadTemplateAsync. CSV downloads. | — |
| T-1144 | "Validate" upload a tiny test CSV. | OnPostValidateAsync. Validation report renders. | — |
| T-1145 | "Import" the same. | OnPostImportAsync. Rows inserted. | — |
| T-1146 | "Skip" on an optional step. | OnPostSkipAsync. | — |
| T-1147 | `/Admin/Export`. Export every available entity. | Each downloads. | — |

### 17.9 Demo + seeding (caution: data-affecting)

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1150 | `/Admin/DemoData`. | Tabs: Preview / Execute (V1 + V2). | — |
| T-1151 | "Preview" V1. OnPostPreviewAsync. | Read-only diff. | — |
| T-1152 | "Preview" V2. OnPostPreviewV2Async. | — | — |
| T-1153 | (Skip Execute on a real DB you care about; if ephemeral test DB, run.) | — | — |
| T-1154 | `/Admin/SeedData`. (Dev-only.) | Buttons for each pipeline. | — |
| T-1155 | Click each: "Seed Item Categories", "Seed Items", "Seed Inventory", "Seed Kits", "Seed Work Order Parts", "Generate Reorder Alerts", "Seed Requisitions", "Seed All", "Clear New Data". | Each handler fires. | — |

### 17.10 Backfills

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1160 | `/Admin/CcaBackfill`. Click "Run". | OnPostAsync. | — |
| T-1161 | `/Admin/DepreciationBackfill`. Run. | OnPostAsync. AssetBookSettings restamped. | — |
| T-1162 | `/Admin/JournalBackfill`. Run with action="preview" then "post". | OnPostAsync (per action). | — |

### 17.11 Smoke tests + integrations

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1170 | `/Admin/SmokeTests`. | Page renders. | — |
| T-1171 | "Start Run" → OnPostStartRun. | Run kicks off; progress shown. | — |
| T-1172 | Wait for completion. Read results. | All green expected (or known-flaky list documented). | — |
| T-1175 | `/Admin/Integrations/Index`. Existing endpoints. | — | — |
| T-1176 | "Create" endpoint. OnPostCreateAsync. | DB: `IntegrationEndpoints` +1. | — |
| T-1177 | "Toggle Active". OnPostToggleActiveAsync. | — | — |
| T-1178 | "Regenerate Secret". OnPostRegenerateSecretAsync. | — | — |
| T-1179 | "Delete" throwaway. OnPostDeleteAsync. | — | — |
| T-1180 | `/Admin/Integrations/Maps`. Create / Delete mapping. | — | — |
| T-1181 | `/Admin/Integrations/Inbound`. View inbound events. "Retry" / "Replay" any. | OnPostRetryAsync / OnPostReplayAsync. | — |

---

## Module 18 — Outbox + Webhooks

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1190 | `/Admin/Webhooks/Index`. Endpoints grid. | — | — |
| T-1191 | "Create" subscription: name=`E2E-WH`, allowedEventTypes (e.g., `invoice.approved,po.received`), description. Save. | OnPostCreateAsync. | DB: `WebhookSubscriptions` +1. |
| T-1192 | "Update" the row. OnPostUpdateAsync. | — | — |
| T-1193 | "Regenerate Secret". OnPostRegenerateSecretAsync. | — | — |
| T-1194 | "Send Test Event". OnPostSendTestEventAsync. | Test ping enqueued; dispatcher attempts delivery. | DB: `OutboxEvents` row with `EventType='test.ping'`. |
| T-1195 | "Delete" throwaway. OnPostDeleteAsync. | — | — |
| T-1200 | `/Admin/Webhooks/Catalog`. **CRITICAL.** Auto-generated from DomainEventRegistry. | **Must show 18 events**: workrequest.created, workorder.created, workorder.closed, closeout.summary.generated, lesson.saved, test.ping, invoice.approved, invoice.paid, invoice.voided, asset.created, asset.improved, asset.disposed, po.approved, po.received, item.received, cip.capitalized, depreciation.posted, pm.occurrence.generated, item.issued. | Count = 18. Each event has property table. |
| T-1201 | Click each event card to expand its property table. | Property names + types match the V1 record shape. | — |
| T-1210 | `/Admin/Webhooks/Deliveries`. Past delivery attempts. | Each row clickable. | — |
| T-1211 | Click any delivery → drilldown shows payload. | Payload JSON. | — |
| T-1212 | Click "Get Payload" link/button on any. | OnGetPayloadAsync. | — |
| T-1220 | `/Admin/Outbox/Index`. Tabs: Pending, Sent, Failed. | — | — |
| T-1221 | Click any pending event → drill. | — | — |
| T-1222 | "Retry Now" on a failed (if any). OnPostRetryNowAsync. | — | — |
| T-1223 | "Replay" on a sent. OnPostReplayAsync. | New row created. | — |
| T-1224 | "Get Payload" link on any row. OnGetPayloadAsync. | — | — |
| T-1240 | (placeholder) | — | — |

---

## Module 19 — Cross-module marquee workflows

These are the load-bearing end-to-end scenarios. Each is a single coherent test that traverses multiple modules. Everything done by clicking; verification by SQL after each major leg.

### Workflow A — Full procurement-to-asset lifecycle

This is the marquee. Walk through it once, top-to-bottom, then verify.

| Step | Action | Expected outcome | DB / outbox verify |
|---|---|---|---|
| T-1250 | Create a new Vendor `E2E-WF-V` (T-261 path). | Vendor row. | — |
| T-1251 | Create a new Item `E2E-WF-I` (admin path) typed=Part. | Item row. | — |
| T-1252 | Create a new CIP project `E2E-WF-CIP` (T-691). | CipProject row. | — |
| T-1253 | Create PO from `/Purchasing` linked to that CIP project, vendor=`E2E-WF-V`. Add line for `E2E-WF-I` qty=10 unitPrice=100. | PO Draft. | — |
| T-1254 | Submit for Approval → Approve. | PO Approved. | Outbox: `po.approved`. |
| T-1255 | Receive partial qty=5. | GR + JE (Dr Inventory or CIP-Pending / Cr GR-Accrued). ItemInventory +5. | Outbox: `po.received` (partial), `item.received` (1, unless CIP-tagged → goes to CIP route). |
| T-1256 | Receive remaining qty=5. | PO=Received. | Outbox: `po.received` (full). |
| T-1257 | Create vendor invoice for `vendor=E2E-WF-V`, total=1000. | Invoice Draft. | — |
| T-1258 | Auto-Match against the PO. | Lines linked. | — |
| T-1259 | Approve the invoice. | JE (Dr GR-Accrued / Cr AP). | Outbox: `invoice.approved`. |
| T-1260 | Record payment full amount. | JE (Dr AP / Cr Cash). Status=Paid. | Outbox: `invoice.paid` (`isFullyPaid=true`). |
| T-1261 | Open the CIP project. Confirm CipCosts include the receipt-routed cost. | DB: `CipCosts` row with SourceType='RECEIPT'. | — |
| T-1262 | Click "Capitalize". Asset number=`E2E-WF-AST`. | New Asset created. JE (Dr Asset / Cr CIP-Pending). Depreciation snapshot recomputed. | Outbox: `cip.capitalized` + `asset.created` (Origin=`cip.capitalized`). |
| T-1263 | Open the new asset. Add an "Improvement" of 500. | Asset.AcquisitionCost +500. | Outbox: `asset.improved`. |
| T-1264 | Generate monthly depreciation for the book (T-805). | JE (Dr Dep Exp / Cr Accum Dep). | Outbox: `depreciation.posted`. |
| T-1265 | Dispose the asset (T-211). Proceeds=200, expense=10. | Asset.Status=Disposed. JE. | Outbox: `asset.disposed`. |
| T-1266 | SQL: count outbox events by EventType for this entire workflow. Confirm: `po.approved` 1, `po.received` 2, `item.received` 0–2 (depending on CIP route), `invoice.approved` 1, `invoice.paid` 1, `cip.capitalized` 1, `asset.created` 1 (origin=`cip.capitalized`), `asset.improved` 1, `depreciation.posted` ≥1, `asset.disposed` 1. | All present. | — |

### Workflow B — PM cycle ticks correctly

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1280 | Create PM Template `E2E-PMT` with one operation. Release the draft. | DB: `PMTemplates` + revision. | — |
| T-1281 | Create PM Schedule for that template, asset=any, cadence=Calendar/Weekly, startDate=today. | DB: `PMSchedules` +1. | — |
| T-1282 | Trigger PM generation (Maintenance "Generate Due"). | DB: `PMOccurrences` +1, `MaintenanceEvents` +1. | Outbox: `pm.occurrence.generated`. (Confirm NOT `workorder.created` from PM path — that legacy event was retired in PR #55.) |
| T-1283 | Open the WO; add labor + parts; close it. | DB: WO Status=Completed. | Outbox: `workorder.closed` + `closeout.summary.generated`. |
| T-1284 | Re-trigger PM generation. NextDueDateUtc on the schedule should advance. | DB: `PMSchedule.NextDueDateUtc` updated. | — |

### Workflow C — Concurrency

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1300 | Open the same asset in two browser tabs. In Tab A, edit Description. Save (success). In Tab B, edit Description differently. Save. | Tab B shows yellow concurrency conflict banner. | — |
| T-1310 | Same for a PO (xmin RowVersion). Two parallel "Approve" clicks. | Second click no-ops or surfaces conflict. | — |

### Workflow D — Period-lock enforcement

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1320 | Lock current period via `/Admin/SystemSettings`. | — | — |
| T-1321 | Try to Approve an invoice with today's posting date. | Error: posting period is not open. No JE, no outbox event. | — |
| T-1322 | Try to Receive a PO. | Error similarly. | — |
| T-1323 | Try to Improve an asset. | Error similarly. | — |
| T-1324 | Try to Dispose an asset. | Error similarly. | — |
| T-1325 | Try to Capitalize a CIP project. | Error. | — |
| T-1326 | Try to Generate Depreciation. | Error. | — |
| T-1327 | Unlock the period. Re-attempt one of the above. Succeeds. | — | — |
| T-1330 | (placeholder) | — | — |

---

## Module 20 — API surface

The controllers in `Controllers/` are public REST endpoints. The browser can't easily click these, but the agent can use the Chrome devtools / fetch from the page console. For each endpoint, run a GET (or POST with a minimal body) from the running browser session, capturing status + body shape.

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1340 | `GET /auth/whoami`. | 200; user info. | — |
| T-1341 | `GET /api/v1/assets`. | 200; paged asset list. | — |
| T-1342 | `GET /api/v1/assets/{ASSET_ID_E2E}`. | 200; asset shape. | — |
| T-1343 | `POST /api/v1/assets` body={…}. | 201; new asset. (Optional — only run on disposable test data.) | — |
| T-1344 | `PUT /api/v1/assets/{N}`. | 200/204. | — |
| T-1345 | `DELETE /api/v1/assets/{N}` on a throwaway. | 204. | — |
| T-1350 | `GET /api/items/{itemId}/stocking`. | 200. | — |
| T-1351 | `GET /api/v1/org/sites`. | 200. | — |
| T-1352 | `POST /api/v1/org/site`. | 201. | — |
| T-1353 | `GET /api/v1/org/tree`. | 200. | — |
| T-1354 | `GET /api/v1/analytics/drilldown`. | 200. | — |
| T-1355 | `GET /api/v1/analytics/kpis`. | 200. | — |
| T-1356 | `GET /api/v1/details/{type}/{id}` for type=Asset. | 200. | — |
| T-1357 | `GET /api/v1/drilldown/party-summary`. | 200. | — |
| T-1358 | `GET /api/v1/drilldown/cip-kpis`. | 200. | — |
| T-1360 | `GET /api/Backup/export`. | 200; downloadable. | — |
| T-1361 | `GET /api/Backup/status`. | 200. | — |
| T-1362 | `GET /api/barcode/generate/{itemId}`. | 200; SVG/PNG. | — |
| T-1363 | `GET /api/barcode/label/{itemId}`. | 200. | — |
| T-1364 | `POST /api/barcode/scan` body. | 200. | — |
| T-1365 | `GET /api/barcode/lookup/{barcodeValue}`. | 200. | — |
| T-1366 | `POST /api/barcode/batch-print`. | 200. | — |
| T-1370 | `POST /api/integrations/inbound/{integrationKey}` body. | 200/202. (Confirm it produces an inbound event row in `/Admin/Integrations/Inbound`.) | — |
| T-1371 | `GET /swagger`. Swagger UI renders. | OpenAPI spec at `/swagger/v1/swagger.json`. | — |
| T-1380 | (placeholder) | — | — |

---

## Module 21 — Final validation

| Step | Action | Expected | Verify |
|---|---|---|---|
| T-1390 | Run the end-of-run SQL block (below). Capture as `outbox-final.txt`. | — | — |
| T-1391 | Diff `outbox-final.txt` against `outbox-baseline.txt`. Confirm increments per the workflow. | — | — |
| T-1392 | Re-visit the dashboard at `/Index`. KPIs should reflect the new data (more assets, higher cost, more open WOs, etc.). | KPIs differ from T-004 baseline. | — |
| T-1393 | Run `/Admin/SmokeTests` once more. | All green. | — |
| T-1394 | Capture `RUN_<id>.md` summary: total steps run, passed, failed, skipped, with screenshots inventory. | — | — |
| T-1395 | [GATING] If any failure was P0 (data corruption, missing JE, missing outbox event for a real domain transition), STOP and surface immediately. | — | — |
| T-1410 | Sign-off block: "Best-in-class proof: PASS / FAIL / PARTIAL." | — | — |

---

## Canonical SQL verification block

Run after any major leg of any workflow. Replace `LIMIT N` to taste.

```sql
-- Outbox audit
SELECT "EventType", COUNT(*) AS cnt, MAX("OccurredAt") AS latest
FROM "OutboxEvents"
GROUP BY "EventType"
ORDER BY "EventType";

-- JE audit (recent)
SELECT "Source", "Reference", "PostingDate", "Description"
FROM "JournalEntries"
ORDER BY "Id" DESC
LIMIT 20;

-- Asset cost ledger sanity
SELECT a."AssetNumber", a."AcquisitionCost", a."AccumulatedDepreciation",
       (a."AcquisitionCost" - a."AccumulatedDepreciation") AS book_value
FROM "Assets" a
WHERE a."AssetNumber" LIKE 'E2E-%'
ORDER BY a."AssetNumber";

-- Inventory snapshot
SELECT i."ItemId", i."LocationId", i."CompanyId", i."QuantityOnHand", i."LastReceiptDate", i."LastIssueDate"
FROM "ItemInventory" i
WHERE i."CompanyId" IS NOT NULL
ORDER BY i."ItemId";

-- WO completeness
SELECT "WorkOrderNumber", "Status", "ActualCost", "ClosedAt"
FROM "MaintenanceEvents"
WHERE "WorkOrderNumber" LIKE 'WO-%'
ORDER BY "Id" DESC
LIMIT 20;

-- PM cycle health
SELECT s."Id" AS schedule_id, s."NextDueDateUtc", s."LastGeneratedAtUtc",
       (SELECT COUNT(*) FROM "PMOccurrences" o WHERE o."PMScheduleId" = s."Id") AS occurrences
FROM "PMSchedules" s
ORDER BY s."Id";

-- Webhook delivery health
SELECT "Status", COUNT(*) FROM "OutboxEvents" GROUP BY "Status";
```

---

## Outbox event verification matrix

After Workflow A in Module 19, exactly these rows should exist (or have been added) in `OutboxEvents`:

| Event | Producer | Correlation | Expected count per workflow |
|---|---|---|---|
| `po.approved` | Pages/Purchasing/Details::OnPostApproveAsync | `po-approve-{Id}` | 1 |
| `po.received` | Pages/Receiving/Receive::OnPostReceiveAsync | `po-receive-{ReceiptId}` | 1 per receive action (partial + full = 2) |
| `item.received` | Services/Receiving/ReceivingPostingService | `item-receipt-{LineId}` | 1 per stock line; 0 for CIP-tagged or service lines |
| `invoice.approved` | Services/AccountsPayable/ApPostingService::PostApprovalAsync | `ap-approve-{Id}` | 1 |
| `invoice.paid` | …::PostPaymentAsync | `ap-payment-{Id}-{JeId}` | 1 per payment |
| `invoice.voided` | …::PostVoidAsync | `ap-void-{Id}` | 1 |
| `cip.capitalized` | Services/Cip/CipCapitalizationService::CapitalizeAsync | `cip-cap-{ProjectId}` | 1 |
| `asset.created` (CIP) | same | `asset-create-cip-{AssetId}` | 1 (Origin=`cip.capitalized`) |
| `asset.created` (UI) | Pages/Assets/Asset::OnPostAsync (create branch) | `asset-create-{AssetId}` | 1 (Origin=`ui.assets.create`) |
| `asset.improved` | Pages/Assets/Improve::OnPostAsync | `asset-improve-{ImprovementId}` | 1 |
| `asset.disposed` | Pages/Assets/Dispose::OnPostAsync | `asset-dispose-{AssetId}` | 1 |
| `depreciation.posted` | Pages/Journals/Generate or /Index | `dep-post-{JeId}` | 1 per generate |
| `pm.occurrence.generated` | Services/Maintenance/PMSchedulerService | `pm-occurrence-{OccurrenceId}` | 1 per occurrence |
| `item.issued` | Pages/WorkOrders/Details::OnPostIssueMaterialAsync | `item-issue-wo{WoId}-p{PartId}-{Ticks}` | 1 per issuance click |
| `workorder.created` (WR conv only) | Services/Maintenance/WorkRequestConversionService | `convert-{WrId}` | 1 |
| `workorder.closed` | Services/Maintenance/CloseoutService | `closeout-{WoId}` | 1 |
| `closeout.summary.generated` | same | `closeout-{WoId}` | 1 |
| `lesson.saved` | Services/Maintenance/CloseoutService::SaveLessonAsync | `lesson-{Id}` | 1 |

**Anti-events to confirm absent:**
- No `workorder.created` from PM-born WOs (retired in PR #55).
- No `item.issued` from a Return action.
- No `asset.created` from bulk imports / seeders.
- No `depreciation.posted` from `HistoricJournalBackfillService`.

---

## Failure triage

When a step fails, capture:
1. Screenshot of failure state (`T-NNN-FAIL.png`).
2. Browser console snapshot (Chrome devtools → Console → "Save as").
3. Network tab snapshot if HTTP call failed.
4. Replit app logs around the failure timestamp (last 50 lines).
5. SQL state of the affected entity.

Categorize:
- **P0 — data integrity:** Missing JE, missing outbox event for a verified state change, ghost transaction, cross-tenant leak.
- **P1 — broken flow:** A handler errors out, page 5xx's, save fails silently.
- **P2 — UX defect:** Visual regression, dropdown empty, button doesn't disable on click.
- **P3 — cosmetic:** Misalignment, typo, mismatched icon.

Each P0/P1 must be filed as a GitHub issue with the step ID in the title (e.g., `[T-572] Receiving GR posts no item.received event for stock line`).

---

## Maintenance of this plan

When the codebase changes:
- **New page added** → add a new Module-X section.
- **New OnPost handler** → add a row in the relevant module's table.
- **New outbox event** → add to Module 18 catalog count assertion + Module 19 marquee workflow + the verification matrix.
- **Removed feature** → strike the row, don't delete it (preserves step-id stability for run-log diffs).

Last updated: 2026-05-08, after PRs [#59](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/59)–[#61](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/61) closed the P1/P2 findings from RUN-20260508-132947. Catalog count baseline: **18 events**.

## Notes from RUN-20260508-132947 reconciliation

- **DEV-001 — URL aliases.** Several pages are reachable from both an `/Admin/*` URL and a domain-prefixed alias (e.g., `/Maintenance/PMTemplates`, `/Materials/Categories`, `/Inventory/StockLevels`, `/Assets/Categories`). The plan now lists the live nav target with the alias noted in parentheses.
- **DEV-002 — Title.** App title is "ABS Machining EAM" (the customer brand); footer reads "powered by CherryAI". T-001 verification updated.
- **DEV-003 — Asset detail tabs.** Live tabs are operations-engineering flavoured (General / Location / Financial / Technical / MES/OEE / IoT / Safety / Warranty / Hierarchy / Attachments / Transactions). Module 4 updated.
- **DEV-004 — Hidden admin pages.** `/Admin/Approvals`, `/Admin/Diagnostics` exist but are not in the sidebar. `/Admin/Outbox/Index` was unlinked; PR [#60](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/60) added it under the Webhooks group. The other two intentionally remain hidden.
- **Fiscal calendar.** Now auto-rolls forward at startup (PR [#59](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/59)). Module 19 marquee workflows can now run end-to-end. Operators can also explicitly materialize via `/Admin/FiscalCalendar`.
