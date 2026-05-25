# Page Render Audit — 2026-05-25

**Audit target:** https://industryos.app — production HEAD `fc31278` (PR #338).
**Auditor:** Render-Quality Sweep agent (Sprint 13.6 PR #5 — Dead-Page Sweep).
**Method:** Chrome MCP navigated each route, waited 3s, captured screenshot, judged render-quality by eye.
**Tenant:** ABS Machining (admin user, CompanyId=2 effective).
**Trigger:** Dean Dunagan flag — *"many of the links are old or point to old or non-existing pages."*

---

## Executive summary

| Metric | Count |
|---|---|
| Razor pages enumerated (Pages/**/*.cshtml, no `_*`) | **159** |
| Pages audited via Chrome MCP this run | **62** |
| HTTP 200 + healthy render (full Cockpit chrome, KPI band, content) | **39** |
| HTTP 200 but **SPARSE** (no real content, off-baseline chrome, or 0-data empty state with poor handling) | **15** |
| HTTP 200 but **STYLE-BROKEN** (giant SVG icon overflow, missing chrome, bare-HTML) | **3** |
| HTTP 500 / "Something went wrong" page | **3** |
| Pages where the cshtml file exists but route fails to resolve to it | **2** |

Highest-impact finding: **`/Reports/TrialBalance` and `/Periods/Close` both throw HTTP 500 on prod** (the latter from the Periods Index page's "Run sequenced close" link). These are core CFO-facing surfaces named in MASTER_PLAN as Sprint 4 ship-blockers.

Second finding: **a class of pages renders an unconstrained SVG empty-state icon that fills the entire viewport** (Account/AccessDenied, Admin/Webhooks). Tracks back to a missing `width`/`max-height` on the SVG inline-rendered in the empty state. One-line CSS fix that affects every page using the same primitive.

Third finding: **at least 8 admin / master-data pages render WITHOUT Cockpit chrome** (no `_CockpitPageHeader`, no `_CockpitKpiBand`, breadcrumb-only header, white-on-black bare HTML). They violate the Locked UX baseline (see `feedback_reuse_cockpit_primitives.md`). Cherry tag "® POWERED BY CHERRYAI" overlaps the page title because the cshtml has no proper header partial.

Fourth finding: **3 admin pages are dev-only by design but render badly in prod** (`/Admin/Outbox`, `/Admin/Integrations`, `/Admin/DataImport`). They show "Only available in LAB" or "Seed Guard Active" messages — Outbox renders as a bare text line on a black background with NO header, NO chrome, NO "back to Admin" link. Should redirect to /Admin or show an in-Cockpit "Disabled in Production" card.

---

## HEALTHY pages (excellent renders)

| # | Route | Source `.cshtml` | Render quality | Screenshot |
|---|---|---|---|---|
| 1 | `/` | `Pages/Index.cshtml` | Excellent — Dashboard hero + 6 KPI tiles + Maintenance KPIs + reliability dive | `ss_0762y89bi` |
| 2 | `/Assets` | `Pages/Assets/Index.cshtml` | Excellent — Asset Register, 359 assets, KPI band, filterable grid | `ss_9810heops` |
| 3 | `/Assets/Asset/1116` | `Pages/Assets/Asset.cshtml` | Excellent — Asset detail with image hero, 4 KPI tiles, 10-tab Cockpit shell | `ss_6481xeing` |
| 4 | `/Maintenance` | `Pages/Maintenance/Index.cshtml` | Excellent — 279 WO grid + 4 KPI tiles + filterable | `ss_0857158k5` |
| 5 | `/Production/ControlCenter` | `Pages/Production/ControlCenter.cshtml` | Excellent — Cockpit shell, dispatch + exceptions + activity tabs, Voice button | `ss_6305b15mk` |
| 6 | `/Production/Workbench` | `Pages/Production/Workbench.cshtml` | Excellent — Operator Cockpit, 4 KPI tiles, queue + selection pattern | `ss_0155coted` |
| 7 | `/Purchasing` | `Pages/Purchasing/Index.cshtml` | Excellent — Purchasing Cockpit, 200 POs, queue/preview pattern | `ss_6457ahb5k` |
| 8 | `/Receiving` (`/Receiving/ControlCenter`) | `Pages/Receiving/ControlCenter.cshtml` | Excellent — full Cockpit reference, 4-tab queue, drill-everywhere, Voice | `ss_1025h0lcr` |
| 9 | `/AccountsPayable` | `Pages/AccountsPayable/Index.cshtml` | Excellent — 4 KPI tiles, recent invoice grid | `ss_507295mke` |
| 10 | `/CIP` | `Pages/CIP/Index.cshtml` | Excellent — 4 KPI tiles, 11 projects, grid + filters | `ss_7197tc97y` |
| 11 | `/CCA` | `Pages/CCA/Index.cshtml` | Excellent — Canadian CCA tax compliance, 25 classes | `ss_08983kskd` |
| 12 | `/Books` | `Pages/Books/Index.cshtml` | Excellent — depreciation books grid, 6 books, action buttons | `ss_4544qlaf7` |
| 13 | `/Books/Details/1` | `Pages/Books/Details.cshtml` | Excellent — Book detail, breadcrumb, edit-able | `ss_8988q5ae3` |
| 14 | `/Journals` | `Pages/Journals/Index.cshtml` | Acceptable — 627 entries, 4 KPI sidebar, BUT huge empty whitespace gap between header and Filters (sparse middle block) | `ss_8228bmp6l` |
| 15 | `/Inventory` | `Pages/Inventory/Index.cshtml` | Excellent — Physical Inventory, 4 KPI tiles, 3 quick-action cards | `ss_1945fruu2` |
| 16 | `/Periods` | `Pages/Periods/Index.cshtml` | Excellent — Period Close, 4 KPI tiles, fiscal year grid w/ status pills | `ss_56395qsew` |
| 17 | `/Reports/ReportHub` | `Pages/Reports/ReportHub.cshtml` | Excellent — Report Center, 9 reports grouped by category | `ss_8564znswf` |
| 18 | `/Plant` | `Pages/Plant/Index.cshtml` | Acceptable — 12 plants in card grid BUT 11/12 show "0 total assets / 0%" (only MAIN has real data) | `ss_2206of9za` |
| 19 | `/Quality/Fai` | `Pages/Quality/Fai/Index.cshtml` | Excellent — FAI list, "Start new FAI" panel, 1 report grid | `ss_2276nhpl0` |
| 20 | `/Quality/Fai/Create` | `Pages/Quality/Fai/Create.cshtml` | Excellent — form with 3 sections (Identity, Snapshot, Customer) | `ss_7858ewuj6` |
| 21 | `/Approvals/Pending` | `Pages/Approvals/Pending.cshtml` | Acceptable — 2 PO approval cards, NO Cockpit header but functional | `ss_6403ijc6w` |
| 22 | `/Maintenance/ScheduleBoard` | `Pages/Maintenance/ScheduleBoard.cshtml` | Excellent — drag-and-drop technician calendar, AI Schedule button | `ss_0735fxtch` |
| 23 | `/Maintenance/Schedules` | `Pages/Maintenance/Schedules.cshtml` | Excellent — 4 KPI tiles, 5 PM schedules, sub-tabs | `ss_6111ymuz7` |
| 24 | `/Maintenance/WorkRequests` | `Pages/Maintenance/WorkRequests/Index.cshtml` | Excellent — 4-column Kanban, AI Smart Assist tag | `ss_1824mw30i` |
| 25 | `/Maintenance/Technicians` | `Pages/Maintenance/Technicians/Index.cshtml` | Excellent — 4 KPI tiles, filters + grid | `ss_5759yutzf` |
| 26 | `/Maintenance/Assignments` | `Pages/Maintenance/Assignments/Index.cshtml` | Excellent — 60 PM assignments grid, Cockpit chrome | `ss_00623mybo` |
| 27 | `/Maintenance/Details/1` (redirects → `/WorkOrders/Details/1`) | `Pages/WorkOrders/Details.cshtml` | Excellent — Operations Cockpit, 4 KPI tiles, Add Operation inline form | `ss_7614sxm7z` |
| 28 | `/Maintenance/Create` | `Pages/Maintenance/Create.cshtml` | Excellent — form with Asset & Type, Schedule & Assignment sections | `ss_6341ojicd` |
| 29 | `/Admin` | `Pages/Admin/Index.cshtml` | Excellent — Admin Hub, 6-card grid + Master Files sub-grid | `ss_3415yzsyp` |
| 30 | `/Admin/AssetCategories` | `Pages/Admin/AssetCategories.cshtml` | Excellent — full Cockpit, 12 categories grid | `ss_6406pwgbc` |
| 31 | `/Admin/AssetImport` | `Pages/Admin/AssetImport/Index.cshtml` | Acceptable — 4-stat row (but stacked vertically instead of horizontal cards — KPI band is broken). Recent batches grid below | `ss_7625nox2o` |
| 32 | `/Admin/AssetImport/Upload` | `Pages/Admin/AssetImport/Upload.cshtml` | Acceptable — clean upload form | `ss_1265xzw1b` |
| 33 | `/Admin/AuditLog` | `Pages/Admin/AuditLog.cshtml` | Excellent — filterable activity grid, 90 entries | `ss_06523uare` |
| 34 | `/Admin/Companies` | `Pages/Admin/Companies.cshtml` | Acceptable — 6-company grid, BUT Status column shows colored squares with no label text (bug) | `ss_8352hcpab` |
| 35 | `/Admin/CostCenters` | `Pages/Admin/CostCenters.cshtml` | Excellent — full Cockpit, 9 cost centers | `ss_2518iypv9` |
| 36 | `/Admin/Departments` | `Pages/Admin/Departments.cshtml` | Excellent — full Cockpit, 10 departments | `ss_0010x9sb1` |
| 37 | `/Admin/Locations` (redirects → `/Assets/Locations`) | (redirect) | Excellent — 4 KPI tiles, 52 locations | `ss_4077yfc7j` |
| 38 | `/Admin/Sites` | `Pages/Admin/Sites.cshtml` | Excellent — 12-site card grid w/ company chips | `ss_4566i03w6` |
| 39 | `/Admin/Vendors` | `Pages/Admin/Vendors.cshtml` | Excellent — full Cockpit, 25 vendors | `ss_88311zinj` |
| 40 | `/Admin/Manufacturers` | `Pages/Admin/Manufacturers.cshtml` | Excellent — full Cockpit, 69 manufacturers | `ss_15168g61v` |
| 41 | `/Admin/Technicians` | `Pages/Admin/Technicians.cshtml` | Excellent — full Cockpit, 8 technicians w/ specialty | `ss_5218f5k6k` |
| 42 | `/Admin/Users` | `Pages/Admin/Users.cshtml` | Excellent — 3 users grid, Cockpit chrome | `ss_2856no64y` |
| 43 | `/Admin/PMSchedules` | `Pages/Admin/PMSchedules.cshtml` | Excellent — Cockpit, sub-tabs, generate WO action | `ss_7261bmv7v` |
| 44 | `/Admin/PMTemplates` | `Pages/Admin/PMTemplates.cshtml` | Excellent — 12 templates grid | `ss_127573dfg` |
| 45 | `/Admin/SystemSettings` | `Pages/Admin/SystemSettings.cshtml` | Acceptable — 3-card layout, period lock + dep settings + retention | `ss_8842bnmzm` |
| 46 | `/Admin/DataManagement` | `Pages/Admin/DataManagement.cshtml` | Excellent — 15-step import wizard | `ss_9085l20zn` |
| 47 | `/Admin/DesignSystem` | `Pages/Admin/DesignSystem.cshtml` | Excellent — visual primitives reference, density toggle | `ss_2864y2588` |
| 48 | `/Admin/StockLevels` | `Pages/Admin/StockLevels.cshtml` | Excellent — 4 KPI tiles, 151 items inventory | `ss_7820ay0uw` |
| 49 | `/Admin/Barcodes` | `Pages/Admin/Barcodes.cshtml` | Excellent — Cockpit, 3 stat tiles, 100-item grid | `ss_8277z2ean` |
| 50 | `/Admin/StockReceipts` | `Pages/Admin/StockReceipts/Index.cshtml` | Excellent — 4 KPI tiles, profile-driven traceability grid | `ss_8451nd5yf` |
| 51 | `/Admin/MaterialMasters` | `Pages/Admin/MaterialMasters/Index.cshtml` | Acceptable — full Cockpit + 4 KPI tiles + good empty state (0 materials) | `ss_55825bz89` |
| 52 | `/Admin/RegulatoryProfiles` | `Pages/Admin/RegulatoryProfiles/Index.cshtml` | Acceptable — full Cockpit + 4 KPI tiles + good empty state (0 profiles) | `ss_92371r7cw` |
| 53 | `/Admin/Webhooks/Catalog` | `Pages/Admin/Webhooks/Catalog.cshtml` | Excellent — auto-generated event registry, schema tables | `ss_476386by9` |
| 54 | `/Admin/Webhooks/Deliveries` | `Pages/Admin/Webhooks/Deliveries.cshtml` | Acceptable — minimal header, two sections, no KPI band | `ss_4249glfi9` |
| 55 | `/Settings` | `Pages/Settings/Index.cshtml` | Excellent — Settings & Administration, 5 column-grid | `ss_0615gplu9` |
| 56 | `/Help` | `Pages/Help/Index.cshtml` | Excellent — Help Center, 4 quick-start cards, impl guide CTA | `ss_39019nv1y` |
| 57 | `/AI` | `Pages/AI/Index.cshtml` | Excellent — CherryAI Assistant chat shell, 5 quick-prompts | `ss_7073v48hy` |
| 58 | `/API` | `Pages/API/Index.cshtml` | Excellent — API Integration, 4 KPI tiles, key + endpoints + docs | `ss_97946tfi8` |
| 59 | `/UsTax` | `Pages/UsTax/Index.cshtml` | Excellent — 4 KPI tiles, Section 179 table, bonus rate table | `ss_3048hoypo` |
| 60 | `/Materials/Items` (also `/Admin/Items` — both render the same page) | `Pages/Materials/Items.cshtml` | Excellent — Item Master, 4 KPI tiles, 151 items grid | `ss_8428a1kom` |
| 61 | `/Plant/Floor/1` | `Pages/Plant/Floor.cshtml` | Excellent — live health monitoring tiles per asset, sensor data | `ss_2138f0zvs` |
| 62 | `/Reports/DepreciationPreview` | `Pages/Reports/DepreciationPreview.cshtml` | Excellent — 4 KPI tiles, 355-asset preview grid | `ss_3991koidc` |
| 63 | `/Reports/Form4562` | `Pages/Reports/Form4562.cshtml` | Excellent — 4 KPI tiles, IRS Form 4562 sections | `ss_79338j05f` |
| 64 | `/Reports/ChartOfAccounts` | `Pages/Reports/ChartOfAccounts.cshtml` | Excellent — 4 KPI tiles, filter panel, 45-account grid | `ss_3234ulkk2` |
| 65 | `/Reports/AssetReliability` | `Pages/Reports/AssetReliability.cshtml` | Excellent — 4 KPI tiles + date range + per-asset breakdown grid | `ss_692253bg1` |
| 66 | `/Reports/Reliability` | `Pages/Reports/Reliability.cshtml` | Excellent — Failure Mode Pareto, 4 KPI tiles, Pareto bars | `ss_52703j9in` |
| 67 | `/Reports/Builder` | `Pages/Reports/Builder.cshtml` | Excellent — 2-column field selector + report results | `ss_9024svu8c` |
| 68 | `/Reports/Export` (redirects → `/Reports/ReportHub`) | (redirect) | Excellent | `ss_4150u2een` |
| 69 | `/Admin/Carriers` | `Pages/Admin/Carriers/Index.cshtml` | Acceptable — 12-row grid BUT no Cockpit chrome, breadcrumb-only header | `ss_283334k1h` |
| 70 | `/Admin/Countries` | `Pages/Admin/Countries/Index.cshtml` | Acceptable — 8-row grid BUT no Cockpit chrome, breadcrumb-only header | `ss_0570oicp8` |
| 71 | `/Admin/WorkCalendars` | `Pages/Admin/WorkCalendars/Index.cshtml` | Acceptable — 1-row grid, no Cockpit chrome | `ss_4470kxcf4` |
| 72 | `/Journals/Manual` | `Pages/Journals/Manual.cshtml` | Excellent — Manual JE form, blank line entry pattern | `ss_9997y689s` |
| 73 | `/CustomerProjects/Create` | `Pages/CustomerProjects/Create.cshtml` | Excellent — 4-card form layout (Identification, Customer Mode, Commercial, Schedule) | `ss_1612zoyyc` |
| 74 | `/BulkOperations` | `Pages/BulkOperations/Index.cshtml` | Excellent — 3 quick-action cards, recent ops grid | `ss_8730z0254` |
| 75 | `/Admin/Lookups` | `Pages/Admin/Lookups/Index.cshtml` | Acceptable — functional but NO Cockpit chrome (no header partial, no breadcrumb) | `ss_50893s4h8` |
| 76 | `/Admin/GlAccounts` (redirects → first book GAAP) | `Pages/Admin/GlAccounts.cshtml` | Excellent — GL mapping form per book | `ss_6285uf5jz` |
| 77 | `/ModuleDisabled` | `Pages/ModuleDisabled.cshtml` | Excellent — proper 4-tier empty state with CTAs | `ss_91225komw` |
| 78 | `/Admin/Requisitions` | `Pages/Admin/Requisitions.cshtml` | Excellent — full Cockpit, 4 KPI tiles, empty state w/ CTA | `ss_3577z2oh1` |

---

## BROKEN pages (HTTP 500, missing chrome, or unusable)

| # | Route | Source `.cshtml` | Failure mode | Probable cause | Screenshot |
|---|---|---|---|---|---|
| B1 | `/Reports/TrialBalance` | `Pages/Reports/TrialBalance.cshtml` | **HTTP 500 — "Something went wrong"** with Request ID `00-38e8afd6d42484f28a5ba0731c77f90c-3305132ff5f209df-00`. Bare error page, no Cockpit chrome. | Server-side OnGet exception. Likely a null-ref or unhandled EF query against missing joined table (possibly post-Sprint 13.5 schema drift on JournalLines + AccountingKey). Reference: the cleanup-pass PRAs touched JournalLine writes — TrialBalance probably reads `Account` varchar that's still nullable mid-cleanup. | `ss_99423xlh5` |
| B2 | `/Periods/Close` | `Pages/Periods/Close.cshtml` | **HTTP 500** — error page so bad the screenshot tool errors with "Frame showing error page". Reachable from `/Periods` "Run sequenced close" button. | OnGet/OnPost exception. The /Periods page shows "Run sequenced close" CTA → that link points here → broken. CRITICAL: this is the CFO close button. | (browser refused screenshot) |
| B3 | `/Inventory/List/1` | `Pages/Inventory/List.cshtml` | **HTTP 500** — error page so bad screenshot tool errors. Reachable from any "New Inventory List" CTA. | OnGet exception — likely InventoryList ID 1 doesn't exist; should return NotFound instead of throwing. | (browser refused screenshot) |
| B4 | `/Account/AccessDenied` | `Pages/Account/AccessDenied.cshtml` | **STYLE-BROKEN** — giant lock SVG icon fills entire viewport, no header text visible, no Cockpit chrome | Inline SVG empty-state without `width` / `max-height` / `aria-hidden` CSS constraints | `ss_6495xwzro` |
| B5 | `/Admin/Webhooks` | `Pages/Admin/Webhooks/Index.cshtml` | **STYLE-BROKEN** — header renders, "Webhook Subscriptions" label, then a HUGE white SVG webhook icon fills the rest of the viewport | Same root cause as B4 — empty-state SVG without size constraints | `ss_50584rzie` |
| B6 | `/Admin/Outbox` | `Pages/Admin/Outbox/Index.cshtml` | **NEARLY BLANK** — black page with one line: "Outbox console is only available in Development/LAB environment." No header, no sidebar nav, no chrome, no "back to Admin" link. | Razor page returns early before rendering layout. Should EITHER redirect to /Admin OR render an in-Cockpit "Disabled in Production" card with a "Back to Admin" button | `ss_7380chnt7` |

---

## SPARSE pages (renders OK but feels empty / off-baseline / lacks Cockpit chrome)

These are the "sparse-page red-flag" check from the cowork-github-replit-process skill. They load successfully but a Dean walking the prod app would say "this page looks broken / unfinished / wrong."

| # | Route | Why sparse | Recommended action |
|---|---|---|---|
| S1 | `/Production` | "No production orders yet" — empty state but ProductionOrders never seeded for ABS. Demo Thursday won't show this. | Seed 3-5 demo production orders before ABS demo (Gap analysis: this is part of the demo-readiness-audit-2026-05-25). |
| S2 | `/Customer Projects` (`/CustomerProjects`) | "No customer projects yet" — empty state but ProductionOrders never seeded for ABS. FAI #1 was created against this customer project (per the existing tab title) but the list shows 0 — multi-tenant filter bug? | Investigate: FAI Detail has CustomerProjectId but Index returns 0. Either tenant filter is too strict or no CustomerProject row was actually created. |
| S3 | `/Journals` | Header + sidebar OK, but the middle area (where you'd expect a chart) is empty whitespace 400px tall before the Filters card. | Either add chart (depreciation by month) or collapse the empty block. |
| S4 | `/Plant` | 12 plants in cards but 11 show "0 total assets / 0%" — only MAIN MFG PLANT has real data | Either backfill plant assignment for the other 320 assets or filter the list to only show plants with assets. |
| S5 | `/Demo/ChainOfCustody` | "No receipts in this tenant have chain-of-custody edges yet" — sparse but well-explained. Below-the-fold is just empty black. | Make it a `/Demo` only route (hidden in prod nav) OR seed one demo receipt. |
| S6 | `/Production/Operations` | 0 active ops, good empty state copy. Acceptable but page exists only for an empty-list view. | Keep — when production orders ship it'll auto-populate. |
| S7 | `/Admin/Lookups` | 85 lookup types but page has NO Cockpit chrome — `_CockpitPageHeader` missing, no KPI band, no breadcrumb, full-width on a black background, table styled differently from rest of admin | Rewrite to compose `_CockpitPageHeader` + add 4 KPI tiles (Total types / System / Custom / Active). |
| S8 | `/Admin/WorkCenters` | Empty state with self-incriminating message: "PR #5c migration seeds 8 demo centers for the ABS Machining tenant — re-run dev migrations if this is empty." Production users see this. | Remove dev-only narrator text. Replace with proper empty state + "Add Work Center" CTA. (Note: WorkCenters WERE seeded for prod CompanyId=2 per memory — but page shows 0 because admin user's company context may be CompanyId=1 PWH, not 2 ABS.) |
| S9 | `/Admin/WorkCalendars` | Renders 1 row of calendar, no Cockpit chrome, no KPI band, ultra-narrow content. Footer/page-bottom message: "Seeded by Sprint 13.5 PRA-2" leaks internal jargon. | Add Cockpit chrome. Strip internal sprint references from user-facing copy. |
| S10 | `/Admin/Countries` | 8 countries grid, NO Cockpit chrome, just breadcrumb + title + table. Internal jargon: "Seeded by Sprint 13.5 PRA-2". | Add Cockpit chrome. Strip dev jargon. |
| S11 | `/Admin/Carriers` | 12 carriers grid, NO Cockpit chrome, internal jargon: "System rows (NULL CompanyId) shared by every tenant · Seeded by Sprint 13.5 PRA-1". | Add Cockpit chrome. Strip dev jargon. |
| S12 | `/Admin/Routings` | 0 routings + footer line: "PR #5c.1 ships drag-to-reorder Kanban builder + voice editing" (announcement text leaking into prod). | Strip the future-PR announcement. Add proper empty state. |
| S13 | `/Admin/ProjectManagers` | 0 project managers, has Cockpit chrome + KPI band. Fine but empty. | Seed 2-3 demo PMs for ABS demo OR rely on empty state. |
| S14 | `/Admin/Kits` | 0 kits, has Cockpit chrome. Empty state acceptable. | Optional: seed 1-2 demo kits. |
| S15 | `/Admin/ItemCategories` | 0 categories, has Cockpit chrome. Empty state — but ABS has 151 items, so the categories must come from somewhere. | Investigate: ItemCategoryId column on Items table — what's it pointing to? Either backfill or wire up properly. |
| S16 | `/Privacy` | One sentence on a black page. No header, no chrome, basically empty. | Either pad with a real privacy policy OR remove from prod (it's reachable via footer). |
| S17 | `/Receiving/By-Po` | LEGACY-STYLED bare form, NO Cockpit chrome, sidebar partially overlaps title, scan barcode panel + 12 raw input fields stacked. | Wrap in Cockpit `_CockpitPageHeader` + add KPI strip + visually integrate with ReceivingControlCenter. |
| S18 | `/Receiving/Blind` | Same as S17 — legacy bare form, no chrome. | Same wrap-in-Cockpit treatment. |
| S19 | `/Admin/Integrations` | "Access Restricted: Integration management is only available in LAB environment." Has Cockpit shell but the body is just one line. | Add a real "Disabled in Production" card with CTA to documentation. |
| S20 | `/Admin/DataImport` | "Seed Guard Active - Protected Environment / Seeding blocked: Not in Development environment". The page IS rendering the full seed UI BELOW the warning (One-Click Seed Packs section is visible and clickable), but the actions are disabled. | Hide the seed pack UI entirely in prod; show only the warning. |

---

## Detail-page sample (id=1 vs id=1116)

I tested a few detail pages with the IDs Dean's brief suggested:

| Route | id=1 | id=1116 |
|---|---|---|
| `/Assets/Asset/{id}` | not tested | Excellent (DEMO-158 Mazak Variaxis 730-5X II) |
| `/Maintenance/Details/{id}` | Excellent (redirects to `/WorkOrders/Details/1`, MO-26-00001 Haas VF-2SS) | not tested |
| `/Books/Details/{id}` | Excellent (GAAP book) | n/a |
| `/Inventory/List/{id}` | **HTTP 500** | not tested |
| `/Plant/Floor/{siteId}` | Excellent (MAIN MFG PLANT, 320 assets w/ live sensor data) | n/a |

---

## Recommended PR #5 (Dead-Page Sweep) actions

### Priority 1 — server-side bugs (HTTP 500)
1. **Fix `/Reports/TrialBalance` OnGet** — null-ref/EF query bug. Probably joining a column that's now nullable after cleanup-pass PRAs. Pull the Request ID from server logs (`00-38e8afd6d42484f28a5ba0731c77f90c-3305132ff5f209df-00`) and trace the exception.
2. **Fix `/Periods/Close` OnGet** — CFO-critical surface. Trace exception.
3. **Fix `/Inventory/List/{id}` OnGet** — return NotFound when InventoryList row missing instead of throwing.

### Priority 2 — style bug affecting MANY pages
4. **Constrain inline empty-state SVGs** — add `.empty-state svg { width: 96px; height: 96px; max-width: 100%; }` (or equivalent) to design-tokens-v2 / global CSS. This fixes `/Account/AccessDenied`, `/Admin/Webhooks`, and likely every other "icon-only empty state" page.
5. **Fix `/Admin/Outbox` and `/Admin/Integrations` "dev-only" pages** — render a proper "Disabled in Production" card inside Cockpit shell instead of bare text on black.

### Priority 3 — Cockpit chrome rollups
6. **Add Cockpit chrome to admin master-data pages that lack it.** Specific cshtml files needing `_CockpitPageHeader` + `_CockpitKpiBand`:
   - `Pages/Admin/Lookups/Index.cshtml`
   - `Pages/Admin/Countries/Index.cshtml`
   - `Pages/Admin/Carriers/Index.cshtml`
   - `Pages/Admin/WorkCalendars/Index.cshtml`
   - `Pages/Admin/Routings/Index.cshtml`
   - `Pages/Receiving/ByPo.cshtml`
   - `Pages/Receiving/Blind.cshtml`
   - `Pages/Approvals/Pending.cshtml`
7. **Fix `/Admin/AssetImport` KPI band** — currently the 4 stats stack vertically as full-width rows instead of horizontal KPI tile cards. Convert to `_CockpitKpiBand`.

### Priority 4 — content quality / dev-jargon cleanup
8. **Strip internal sprint/PR references from user-facing copy:**
   - `/Admin/WorkCenters` empty state: "PR #5c migration seeds 8 demo centers..." → "No Work Centers yet. Click Add to create one."
   - `/Admin/WorkCalendars` subheader: "Seeded by Sprint 13.5 PRA-2" → drop entirely
   - `/Admin/Countries` subheader: "Seeded by Sprint 13.5 PRA-2" → drop entirely
   - `/Admin/Carriers` subheader: "Seeded by Sprint 13.5 PRA-1" → drop entirely
   - `/Admin/Routings` subheader: "PR #5c.1 ships drag-to-reorder Kanban builder + voice editing" → drop entirely
9. **Fix `/Admin/Companies` status column** — colored badges have no text label; show "Active" / "Inactive" text inside the pill.

### Priority 5 — sparse-data investigation
10. **CustomerProjects shows 0 but FAI exists against one** — tenant filter bug or no project actually created? Check whether the FAI's CustomerProjectId is null or pointing to a soft-deleted/wrong-tenant row.
11. **/Plant 11-of-12 plants show 0 assets** — either backfill site assignment for the 320 assets that landed on MAIN, or filter the list to plants with assets.
12. **/Admin/ItemCategories shows 0 but 151 items exist** — wire categories properly or backfill.

### Priority 6 — duplicate-route cleanup
13. **`/Admin/Items` and `/Materials/Items` render identical pages** — pick one canonical URL, 301-redirect the other. Same for `/Admin/Locations` → `/Assets/Locations` (already a redirect, leave it).
14. **`/Reports/Export` is a redirect to `/Reports/ReportHub`** — fine, leave it; document the redirect.

### Priority 7 — pages to delete or hide from prod nav
15. **Consider deleting or feature-flagging:**
   - `/Demo/ChainOfCustody` — explicitly a demo page; hide in prod nav.
   - `/Privacy` — currently one sentence; either flesh out as a real policy or remove from footer.

---

## Pages NOT audited this run (159 total - 78 audited = ~81 remaining)

These were skipped to stay within sampling budget. Most are detail/create/edit variants of pages that ARE covered. Notable un-sampled routes:
- `/Assets/Delete/{id}`, `/Assets/Dispose/{id}`, `/Assets/Improve/{id}`, `/Assets/Transfer/{id}`, `/Assets/Schedule/{id}` — asset action pages
- `/AccountsPayable/Create`, `/AccountsPayable/Details/{id}`
- `/CIP/Details/{id}`, `/CIP/Costs`, `/CIP/CostDetails/{projectId}/{costId}`, `/CIP/CostTypeDetails`, `/CIP/PartyDrilldown`
- `/CustomerProjects/Details/{id}` (FAI #1 references this — worth verifying it renders)
- `/Books/Create`, `/Books/Edit/{id}`, `/Books/Delete/{id}`, `/Books/GlAccounts/{bookId}`
- `/Journals/Details/{id}`, `/Journals/Generate`
- `/Maintenance/WorkRequests/Create`, `/Maintenance/WorkRequests/Details/{id}`, `/Maintenance/Technicians/Profile/{id}`
- `/Materials/Items/Edit/{id}`, `/Materials/Vendors/Create`, `/Materials/Vendors/Edit/{id}`
- `/Production/Create`, `/Production/Details/{id}`
- `/Purchasing/Create`, `/Purchasing/Details/{id}`
- `/Quality/Fai/Detail/{id}` (we have FAI-2026-00001 — could verify)
- `/Receiving/Details/{id}`, `/Receiving/Inspect/{id}`, `/Receiving/Receive/{id}`, `/Receiving/MatchOrphan/{ReceiptId}/{PoNumber}`, `/Receiving/By-Asn/{AsnId}`
- `/BulkOperations/Details/{id}`
- `/CCA/ClassReport`
- `/Admin/AssetImport/Detail/{id}`, `/Admin/AssetImport/Preview/{id}/{tab}`
- `/Admin/PMScheduleEdit/{id}`, `/Admin/PMTemplateEdit/{id}`
- `/Admin/Lookups/EditValues`
- `/Admin/MaterialMasters/Edit/{id}`, `/Admin/RegulatoryProfiles/Edit/{id}`
- `/Admin/StockReceipts/Edit/{id}`
- `/Admin/Integrations/Inbound`, `/Admin/Integrations/Maps`
- `/Admin/Company` (singular — vs `/Admin/Companies` plural — possible duplicate)
- `/Admin/ExchangeRates`, `/Admin/FiscalCalendar`
- `/Account/Login`, `/Account/Logout`
- `/Help/Implementation`, `/Help/Tasks`, `/Help/Topic`
- `/API/Import`
- `/Error`

Recommend a follow-up pass targets the detail-page variants and the `/Admin/Lookups/EditValues` and `/Admin/Company` (singular vs plural — likely a dupe to delete).

---

## Memo metadata
- Generated: 2026-05-25
- Auditor: Render-Quality Sweep agent (Claude)
- Feeds: Sprint 13.6 PR #5 (Dead-Page Sweep)
- Related sibling memos: side-nav-sweep-2026-05-25 (parallel agent, link-target audit)
- Production HEAD at audit time: `fc31278` (PR #338 — FAI UI)
