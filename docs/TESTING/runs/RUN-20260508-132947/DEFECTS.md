# Defects — RUN-20260508-132947

Structured for hand-off to Claude Code. Each entry has: ID, severity, repro steps, expected/actual, suggested fix area.

**Severity scale:** P0 = ship-blocker / P1 = release-blocker / P2 = significant gap / P3 = UX or polish issue / P4 = doc drift.

---

## DEF-004 — No fiscal calendar period seeded for 2026-05-08 on company 2 (BIGGEST BLOCKER)

- **Severity:** **P1** — blocks every journal-entry-posting flow (Improve, Dispose, Run Depreciation, AP voucher, PO post, Receive-to-GL, CIP capitalization, etc.). ~30–40% of the test plan can't run until this is fixed.
- **Module / step:** First seen Module 4, T-181. Will recur in Modules 8/9/10/11/13/15/19.
- **First seen:** 2026-05-08 14:08 UTC
- **Repro:**
  1. Create an asset (or use existing asset 954 / `E2E-A-135249`).
  2. From asset detail, click "Improve" → opens `/Assets/Improve/{id}`.
  3. Fill: Description=anything, Cost=2500, ImprovementDate=today (2026-05-08).
  4. Click "Add Improvement".
  5. Server returns: **"No fiscal period defined for 2026-05-08 on company 2. Set up the calendar in Admin → Fiscal Calendar before posting."**
- **Expected:** Improvement posts; AcquisitionCost increases; outbox `asset.improved` event fires.
- **Actual:** Validation error; nothing persists.
- **Root cause hypothesis:** Demo seed creates fiscal periods only for the seed reference year (likely 2025). Today is 2026-05-08, so periods are missing.
- **Suggested fix area (any of):**
  - **Option A (preferred):** Extend the seed to insert FiscalCalendar rows covering 2026 + 2027 for both companies. Look for the existing seeder.
  - **Option B:** App-startup auto-rollforward so periods always cover [today − 1y, today + 2y].
  - **Option C:** Have the operator manually create periods via Admin UI — **but see DEF-006 below; the UI route the error points to doesn't exist.**
- **Files Claude Code should look at:**
  - `Data/Seeders/*` or `Data/Migrations/*`
  - `Services/FiscalCalendarService.cs` or similar
  - `Pages/Assets/Improve.cshtml.cs` (the validator/poster)
  - **And solve DEF-006 in the same PR** so admins have a UI path to recover.

---

## DEF-006 — `/Admin/FiscalCalendar` route doesn't exist (UI named in DEF-004 error message)

- **Severity:** **P1** — blocks the documented recovery path for DEF-004
- **Module / step:** Module 17 (probe after DEF-004 surfaced)
- **First seen:** 2026-05-08 14:32 UTC
- **Repro:**
  1. Trigger DEF-004 (any post-JE flow on today's date).
  2. The error tells the user "Set up the calendar in Admin → Fiscal Calendar before posting."
  3. Try every plausible URL — all return 404:
     - `/Admin/FiscalCalendar` → 404
     - `/Admin/FiscalCalendars` → 404
     - `/Admin/FiscalPeriods` → 404
     - `/Admin/AccountingPeriods` → 404
     - `/Admin/Periods` → 404
     - `/Admin/Calendar` → 404
  4. There is no link for "Fiscal Calendar" in the Admin sidebar either.
- **Expected:** A page exists where admins can create/edit/close fiscal periods, reachable from the Admin nav, matching the URL the error message gives.
- **Actual:** No such page; the error sends users into a dead end.
- **Suggested fix area:**
  - Either ship `Pages/Admin/FiscalCalendar.cshtml` + nav link + `Pages/Admin/FiscalCalendar.cshtml.cs` (CRUD over `FiscalCalendar` / `FiscalPeriod` entities).
  - Or change the error message in `Services/FiscalCalendarService.cs` (or wherever it's thrown) to point to the actual recovery path.
- **Files Claude Code should look at:**
  - Wherever the literal string "Set up the calendar in Admin → Fiscal Calendar before posting" lives (`grep -r "Set up the calendar" Pages/ Services/ Models/`).
  - `Pages/Admin/` — confirm whether `FiscalCalendar.cshtml` was authored but not registered.
  - `Pages/Shared/_AdminLayout.cshtml` / `_ModernLayout.cshtml` — nav wiring.

---

## DEF-001 — `/Admin/Webhooks/Deliveries` returns HTTP 404 and is not linked from menu

- **Severity:** P2
- **Module / step:** Module 2, T-100; reconfirmed in Module 18
- **Repro:** `fetch('/Admin/Webhooks/Deliveries')` → 404. Search DOM for `a[href="/Admin/Webhooks/Deliveries"]` → none.
- **Expected:** Webhook delivery log page reachable from Admin → Webhooks.
- **Actual:** 404, no nav.
- **Files Claude Code should look at:** `Pages/Admin/Webhooks/`, sidebar layouts, plan reconciliation.

---

## DEF-002 — Dashboard KPI cards (Open WO, Active CIP) are non-clickable when plan requires drilldowns

- **Severity:** P3 (UX gap; alternate paths exist)
- **Module / step:** Module 3, T-113 + T-114
- **Repro:** On `/`, hover Open Work Orders / Active Capital Projects KPI cards. No pointer cursor; element is bare `<div>` not wrapped in `<a>`; no `onclick`. Only Total Asset Value KPI is wrapped in `<a href="/Assets">`.
- **Expected:** Each KPI drills into the corresponding module page.
- **Suggested fix:** Wrap KPI cards in `<a>` and add `cursor: pointer` styling. `Pages/Index.cshtml` is the entry point.

---

## DEF-003 — Header context strip / company-site selector missing on the dashboard (revised)

- **Severity:** P3 (layout inconsistency)
- **Module / step:** Module 3, T-118 (revised after Module 4 finding showed the strip exists on inner pages)
- **Repro:**
  1. `/` (dashboard) — no "All Companies" / "All Sites" buttons in header.
  2. `/Assets/Transfer/954` (or any inner page) — header DOES show "All Companies" + "All Sites" buttons.
- **Expected:** Selectors are consistent across all pages.
- **Suggested fix:** `Pages/Index.cshtml` likely uses a different layout/partial than inner pages. Include `_IndexContext` partial (or whichever renders the strip) in the dashboard layout.

---

## DEF-005 — Asset Register search input does not filter the grid

- **Severity:** P2 (search is a primary discovery mechanism on a 300+ asset grid)
- **Module / step:** Module 4, T-142
- **Repro:** On `/Assets`, set the search input to "CNC" via `input` + `change` events. tbody row count unchanged at 25.
- **Expected:** Live filter on Description / AssetNumber.
- **Files Claude Code should look at:** `Pages/Assets/Index.cshtml`, the JS module that owns the grid, and `Pages/Assets/Index.cshtml.cs`'s `OnGetAsync` for `?q=` support.

---

## DEF-007 — API surface is unusually small (22 paths) and `/api/health`, `/api/version` don't exist

- **Severity:** P3 (operational visibility)
- **Module / step:** Module 20
- **Findings:** Swagger reports 22 paths total. Notable absences:
  - `/api/health` → 404 (standard `/healthz` or `/api/health` is the convention for an EAM).
  - `/api/version` → 404.
  - No bulk asset/JE list endpoints in the public API (only `/api/v1/assets`, `/api/v1/assets/{id}`).
- **Expected:** A public API of this scope (a full EAM) typically exposes ~50–150 endpoints. Health and version probes are typically included.
- **Suggested fix area:** Decide whether the API is intentionally minimal (B2B integration only) or whether more controllers should be exposed via OpenAPI. Add `/api/health` and `/api/version` for any monitoring.
- **Files Claude Code should look at:** `Program.cs` / `Startup.cs` / wherever Swagger is registered (`AddSwaggerGen`); `Controllers/` to see what's been authored vs. exposed.

---

## DEV findings (low priority — plan or doc drift)

### DEV-001 — Test plan vs. live URL space (10+ routes)

| Live href | Plan-expected | Plan step |
|---|---|---|
| `/Maintenance/PMTemplates` | `/Admin/PMTemplates` | T-036 |
| `/Purchasing/Requisitions` | `/Admin/Requisitions` | T-041 |
| `/Materials/Categories` | `/Admin/ItemCategories` | T-052 |
| `/Materials/Kits` | `/Admin/Kits` | T-053 |
| `/Inventory/StockLevels` | `/Admin/StockLevels` | T-055 |
| `/Assets/Categories` | `/Admin/AssetCategories` | T-060 |
| `/Assets/Locations` | `/Admin/Locations` | T-061 |
| `/Assets/Barcodes` | `/Admin/Barcodes` | T-062 |
| `/Reports/Index` redirects to `/Reports/ReportHub` | distinct page | T-081 |
| `/Assets/Asset/{id}` | `/Assets/Asset?id={N}&mode=view` | T-117 |
| `/Assets/Schedule/{id}` (200) vs `/Assets/Schedule?id={id}` (404) | latter | T-200 |

**Action:** Update the test plan to match live route shapes.

### DEV-002 — Page titles say "ABS Machining EAM" but plan looks for "CherryAI EAM"

Footer correctly says "ABS Machining EAM, powered by CherryAI" — the relationship is intentional. Either change `<title>` template or update T-001 verification.

### DEV-003 — Asset detail tab list differs significantly from plan

Plan tabs: Overview, Specifications, Meter Readings, Maintenance, Transactions, Attachments, Image, Documentation, Audit Trail.
Actual tabs: General, Location, Financial, Technical, MES/OEE, IoT, Safety, Warranty, Hierarchy, Attachments, Transactions.

The actual model is more engineering/operations-flavoured (MES/OEE, IoT, Safety) than the plan's accounting-flavoured layout (Meter Readings, Audit Trail). Reconcile or document the divergence.

### DEV-004 — Admin pages exist as routes but are not in the sidebar

`/Admin/Approvals` (200), `/Admin/Diagnostics` (200), `/Admin/Outbox/Index` (200) — all reachable by URL, none linked from the sidebar.

**Action:** Either add nav links or document why these are hidden (feature-flagged, behind elevated permissions, etc.).
