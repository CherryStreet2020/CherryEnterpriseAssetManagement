# UI Cleanup Fix List — Sprint 13.6 Synthesis

**Date:** 2026-05-25 (end of PR #338 ship session)
**Source memos:** `nav-audit-2026-05-25.md` + `page-render-audit-2026-05-25.md` + `form-density-audit-2026-05-25.md` + `subform-redundancy-audit-2026-05-25.md`
**Trigger:** Dean directive — *"a lot of these forms and subforms are taking up MASSIVE amounts of real estate. The side menu is a disaster and many of the links are old or point to old or non-existing pages."*

---

## Executive scorecard

| Audit | Surfaces examined | Defects found | Worst single page |
|---|---|---|---|
| Side-Nav Sweep | 51 nav links + 159 routes (orphan check) | 11 broken / 38 orphan / 14 label mismatch | `/Admin/Customers` (vapor page with "new" badge) |
| Page Render | 159 pages / 78 audited | 3 hard 500s / 15 sparse / 3 SVG-overflow / 9 dev-jargon leaks | `/Reports/TrialBalance` (HTTP 500 — CFO critical) |
| Form Density | 441 .cshtml / 151 with overrides | **2,342** inline data-csp-style overrides | `Pages/Assets/Asset.cshtml` (229 overrides) |
| Subform Redundancy | 159 pages + 282 partials | ~30 copies of identical child-table chrome / 12 parallel-stack header pages | `Pages/CustomerProjects/Details.cshtml` (5 copies of same pattern incl. today's FAI section) |

**Total cleanup surface area:** ~2,342 inline overrides removable + ~1,150 LOC of duplicated chrome removable + 11 nav fixes + ~6 critical bug-fix PRs.

---

## P0 — STOP-THE-BLEED (ship immediately, ABS demo can't risk these)

| # | Defect | Page / file | Fix | PR |
|---|---|---|---|---|
| P0-1 | HTTP 500 on Trial Balance | `/Reports/TrialBalance` | Debug the request — likely service-injection failure or null deref. CFO close motion BROKEN. | PR #5 (urgent) |
| P0-2 | HTTP 500 on Period Close | `/Periods/Close` | Same — CFO close button doesn't work in prod. | PR #5 (urgent) |
| P0-3 | HTTP 500 on Inventory List by id | `/Inventory/List/{id}` | Missing NotFound handling on the page model. Add `if (model == null) return NotFound();` | PR #5 |
| P0-4 | `/Plant/Floor` advertised in nav but `@page "{siteId:int}"` → bare URL 404 | NavRegistry entry | Repoint nav to `/Plant` (Index, titled "Plant Floor") OR add a parameterless route to Plant/Floor. | PR #2 |
| P0-5 | Quick Actions tray 60% broken — `/WorkOrders/Create`, `/Purchasing/Requisitions/Create`, `/Assets/Create` all 404 | `_NavQuickActions.cshtml` | Remove from tray OR build the 3 Create pages (the legit answer depends on whether Create paths exist via other routes). | PR #2 |
| P0-6 | `/Admin/Customers` "new" badge advertises a page that doesn't exist | NavRegistry | Either delete the entry OR scaffold the page now (high embarrassment risk for demo). | PR #2 |
| P0-7 | `/Reports` has no Index — Insights group's primary destination is a 404 | NavRegistry | Repoint `/Reports` → `/Reports/ReportHub`. | PR #2 |
| P0-8 | `/Notifications` bell icon goes nowhere (folder doesn't exist) | `_NavSidebar.cshtml` | Remove the bell OR scaffold a minimal Notifications/Index page. | PR #2 |

---

## P1 — DENSE-FORM CLEANUP (Top-10 worst offenders)

Authority: `wwwroot/css/design-tokens-v2.json` (asymmetric 4/6/12/20/40 spacing) + `docs/research/luxury-cockpit-ux.md` + Receiving CC + Operator Workbench as exemplars.

| Rank | File | Overrides | Primary offense | Fix |
|---|---|---|---|---|
| 1 | `Pages/Assets/Asset.cshtml` | 229 | Hidden Razor lambda factory emitting 5-style banners; hardcoded hex; vertical-stacked hero | Deep rebuild — deferred to Wave 2.5+ (too big for PR #3). |
| 2 | `Pages/WorkOrders/Details.cshtml` | 150 | Collapsible Edit form with `auto-fit minmax(220px,1fr)` + inline label sizes | Deferred to Wave 2.5+. |
| 3 | `Pages/Materials/ItemEdit.cshtml` | 111 | 8 vertically-stacked section-cards — needs tab-shell | Deferred to Wave 2.5+. |
| 4 | `Pages/Admin/Items.cshtml` | 90 | 30 inline `<th>` widths — pure debt | PR #3 (drop to data-table class) |
| 5 | `Pages/Assets/_AssetMesIotSafetyTabs.cshtml` | 83 | 80 are repeated `flex: 1` — one CSS rule wipes them | PR #3 (1-line CSS) |
| 6-10 | Pages from today's ship (AssetImport/Upload+Preview+Detail+Index, Fai/Create+Detail) | varies | Inline form-field padding overrides | PR #3 — extract `.form-stack-tight`, `.form-grid--2/3/4`, semantic callout utilities |

**New CSS utilities required in `wwwroot/css/cockpit.css` (per density-audit memo):**
- `.form-stack-tight` — replaces inline `margin-bottom: 1rem` per form row
- `.form-grid--2`, `.form-grid--3`, `.form-grid--4` — replaces inline vertical stacks that should be column grids
- `.callout--success / --warning / --danger / --info` — replaces ~80 inline overrides across 6 files, removes 6+ hardcoded rgba() colors (violates "no info-blue, no purple-AI" anti-pattern)
- Empty-state SVG constraint: `width: 96px; max-height: 96px` — fixes AccessDenied + Admin/Webhooks overflow

**Expected impact of PR #3:** -49% of total inline-style debt in one PR. Top-listed pages fit in 1440×900 viewport on first paint.

---

## P2 — SUBFORM REDUNDANCY (consolidate, extract partials)

| # | Pattern | Pages with violation | Fix |
|---|---|---|---|
| P2-1 | Repeated child-table chrome `<section detail-card--full><h3>Title (count)</h3>[table\|empty]</section>` | 30+ Details pages incl. CustomerProjects.Details ×5 (incl. today's FAI section), Asset.cshtml, WorkOrders.Details, etc. | Extract `Pages/Shared/_ChildSectionHeader.cshtml` + `_ChildTableEmpty.cshtml`. PR #4. |
| P2-2 | Pages using `_ScreenHeader` AND ALSO hand-rolling their own `<h1>` / breadcrumb / back-link | 12 pages: CustomerProjects/*, Production/*, Admin/WorkCenters/Routings/WorkCalendars/Carriers/Countries Index | Delete the parallel-stack chrome, rely on `_ScreenHeader`. PR #4. |
| P2-3 | KPI tile grid that duplicates `_ScreenHeader` Subtitle/Status | AssetImport/Preview.cshtml (triple-renders rows/valid/errors/status), AssetImport/Detail, Fai/Detail, AssetImport/Index | Drop duplicate KPI, OR move the KPI into `_ScreenHeader`'s `KpisPartial` slot. PR #4. |
| P2-4 | Section-card chrome wrapping a single CTA button | Fai/Index "Start a new FAI", AssetImport/Index "Start a new import", AssetImport/Upload | Promote the CTA to a `_ScreenHeader` ActionsPartial slot. PR #4. |
| P2-5 | First detail-card restates H1 identity | Receipt Information first row = ReceiptNumber, Order Header first 3 rows = H1 + status + subtitle | Drop redundant rows. PR #4. |

**New shared partials for PR #4:**
- `Pages/Shared/_ChildSectionHeader.cshtml` (title + count + optional CTA)
- `Pages/Shared/_ChildTableEmpty.cshtml` (empty-state copy + optional first-action CTA)
- `Pages/CustomerProjects/_ChildTable.cshtml` (extracts the Jobs/Phases/Members/Amendments/FAI shape)

**Estimated impact of PR #4:** ~1,150 LOC removed.

---

## P3 — DEV-JARGON CLEANUP + DEAD PAGES + POLISH

| # | Defect | Fix | PR |
|---|---|---|---|
| P3-1 | Dev jargon leaks into user UI — "Sprint 13.5 PRA-2 will populate..." / "PR #5c migration seeds 8 demo centers..." on /Admin/WorkCenters | Grep `.cshtml` for "PRA-", "PR #", "Sprint XX" + strip user-facing references | PR #5 |
| P3-2 | `/Admin/Outbox` is bare text "only in LAB" — no chrome at all | Wrap in standard layout + add link back to dashboard | PR #5 |
| P3-3 | `/Admin/Lookups` has 85 lookups but zero Cockpit chrome | Add `_ScreenHeader` + filter chips | PR #5 / PR #6 |
| P3-4 | `/Receiving/By-Po` and `/Receiving/Blind` legacy bare forms | Wrap in Cockpit primitives (these are Control Center children → Lock 3 applies) | PR #6 |
| P3-5 | `/Privacy` one sentence on a black page | Write real privacy content | Backlog |
| P3-6 | Duplicate routes — Admin/Items 301-redirects to Materials/Items; Admin/Locations 301 to Assets/Locations | Delete the stub Admin/* pages + update any links | PR #5 |
| P3-7 | 38 orphan pages (no nav entry) — many are legitimate (Detail pages) but some are abandoned | Triage list: promote ~12 to nav (FAI is the highest-leverage addition), 301 ~8 legacy, delete the rest | PR #2 |
| P3-8 | 14 label mismatches between NavRegistry and ViewData["Title"] | Sync labels — single source of truth = NavRegistry | PR #2 |

---

## Execution sequence (5 PRs, 3-5 days total)

### PR #1 (THIS DOC) — Synthesis docs-only
Lands `docs/research/ui-cleanup-fix-list-2026-05-25.md` + index entry in MEMORY.md + Master Plan reference. No code change.

### PR #2 — NavRegistry cleanup (Sprint 13.6 PR #2)
- Delete 5 vapor-page entries (Customers, etc.)
- Repoint 6 mis-pointers (Plant/Floor → Plant, Reports → Reports/ReportHub, etc.)
- Fix 3 Quick Actions
- Add FAI entry (#1 highest-leverage add per Side-Nav memo — for ABS demo)
- Sync 14 label mismatches
- Promote ~12 orphans to nav, retire ~8 legacy routes
- Build: 0 errors. Tests: nav-renders.spec ensures every link returns 200.

### PR #3 — Form density top 10 (Sprint 13.6 PR #3)
- Add ~145 LOC of utility classes to `wwwroot/css/cockpit.css`
- Refactor top 10 worst offenders (excluding the 3 deep-rebuild deferrals)
- Strip ~50% of `data-csp-style` overrides across the codebase
- All pages fit 1440×900 viewport without scroll on first paint

### PR #4 — Subform consolidation (Sprint 13.6 PR #4)
- Create `_ChildSectionHeader`, `_ChildTableEmpty`, `_ChildTable` partials
- Refactor 12 parallel-stack-header pages to use `_ScreenHeader` only
- Drop redundant KPI duplications
- Promote single-CTA section cards into `_ScreenHeader` ActionsPartial
- Estimated ~1,150 LOC removed

### PR #5 — Bug + polish (Sprint 13.6 PR #5)
- Fix 3 HTTP 500s (TrialBalance / Periods/Close / Inventory/List/{id})
- Fix SVG overflow on AccessDenied + Admin/Webhooks (1 CSS rule, fixes all empty-state icons)
- Strip dev jargon from `.cshtml` files (grep + scrub)
- Wrap bare-text pages (Outbox, Privacy)
- Delete duplicate-route stubs

### PR #6 — Cockpit primitive backport (Sprint 13.6 PR #6)
- `/Receiving/By-Po` + `/Receiving/Blind` get upgraded to `_CockpitPageHeader` + `_CockpitTabShell` (these are Control Center children, Lock 3 applies)
- `/Admin/Lookups` gets `_ScreenHeader` + filter chips
- Other identified Control Center descendants get the primitive backport

### PR #7 — Sidebar IA pass (Sprint 13.6 PR #7)
- Apply Dean's call on group ordering — current is "Today / Operations / Finance / Insights / Master Data / AI & Integrations / Settings"
- May regroup based on what the audit revealed about how items actually map to user mental models
- Add CI lint gate: any nav entry that 404s fails the build

---

## Verification gates per PR

Every PR in this sprint MUST:
1. **Render gate** — Playwright spec hits every NavRegistry route + every modified page, asserts 200 + non-blank.
2. **Density gate** — for PR #3+: count of `data-csp-style` overrides decreases by at least N% per the memo.
3. **Lock 3 gate** — any page in /Receiving, /Production, /CustomerProjects (Control Center surfaces) MUST compose Cockpit primitives.
4. **Build clean** — 0 errors, no new warnings.
5. **Lock 9** — for any prod-touching PR, real click-through on industryos.app.

---

## Out-of-scope (deferred)

- **Pages/Assets/Asset.cshtml deep rebuild** (229 overrides) — too big for this sprint; needs its own ~2-day dedicated PR.
- **Pages/WorkOrders/Details.cshtml deep rebuild** (150 overrides) — same.
- **Pages/Materials/ItemEdit.cshtml tab-shell extraction** (111 overrides) — already on the roadmap as Sprint 9 (Item Master Restructure).
- **Mobile breakpoints** — current authority is 1440×900 desktop baseline; mobile/tablet defers to Sprint 20 (PWA + DataWedge).
- **i18n of any stripped strings** — defers to Sprint 22 i18n + Launch Polish.

---

## Memory + cross-refs

- Memory: `reference_ui_audit_2026_05_25.md` (this initiative).
- Master Plan: Priority 1.62a / Wave 2.5 / TD-14.
- Authority docs: `wwwroot/css/design-tokens-v2.json` + `docs/research/luxury-cockpit-ux.md`.
- Source memos (this doc synthesizes): `nav-audit-2026-05-25.md`, `page-render-audit-2026-05-25.md`, `form-density-audit-2026-05-25.md`, `subform-redundancy-audit-2026-05-25.md`.
