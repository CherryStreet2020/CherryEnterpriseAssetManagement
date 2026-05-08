# Hand-off to Claude Code — RUN-20260508-132947

**Run wall-clock:** 65 min
**Coverage:** Modules 1 + 2 fully exercised. Module 3 fully exercised. Module 4 partially exercised (CRUD flows proven; JE-posting flows blocked). Modules 5–18, 20 page-render & form-open coverage only. Modules 19 + 21 blocked. ~40% of the test plan's `Verify` steps require psql DB access and were marked DEFER for the operator.

**App under test:** ABS Machining EAM (CherryAI EAM v1) on Replit at `https://e8802fe3-8508-4ead-883b-c5ea53b425aa-00-3qxqi4qcn5qjz.picard.replit.dev/`. Dotnet/Razor Pages, EF Core, Postgres backend.

**Repo:** `CherryStreet2020/CherryEnterpriseAssetManagement`

---

## How to use this document

Read top-to-bottom. Defects are ordered by what to fix first. Each defect has:
- **Repro** — exact steps that reproduced it
- **Expected vs. Actual**
- **Files Claude Code should look at** — first place to search
- **Suggested fix** — a starting point, not a prescription

Defects DEF-004 + DEF-006 are paired — fix them in the same PR.

---

## TL;DR — the three things to fix first

1. **Seed (or auto-rollforward) the fiscal calendar so today's date has a period.** Without this, ~30–40% of the system is wedged. (DEF-004)
2. **Ship the Admin → Fiscal Calendar UI** — the error from #1 directs admins there but the route 404s. (DEF-006)
3. **Wire the Asset Register search input** — typing in the search box does nothing. (DEF-005)

Everything else is P3 or doc drift.

---

## P1 — Ship blockers

### DEF-004 + DEF-006 (paired) — Fiscal calendar gap + missing recovery UI

**The system tells admins to fix a problem at a URL that doesn't exist.**

#### Repro
1. Sign in as ADMIN.
2. Open any existing asset (e.g., asset 954 / `E2E-A-135249` was created during this run; or use any seeded asset).
3. Click "Improve" → opens `/Assets/Improve/{id}`.
4. Fill: Description="any text", Cost=2500, ImprovementDate=today.
5. Click "Add Improvement".
6. Server validation banner: **"No fiscal period defined for 2026-05-08 on company 2. Set up the calendar in Admin → Fiscal Calendar before posting."**
7. Try to navigate to `/Admin/FiscalCalendar`. Result: HTTP 404.
8. Try `/Admin/FiscalCalendars`, `/Admin/FiscalPeriods`, `/Admin/AccountingPeriods`, `/Admin/Periods`, `/Admin/Calendar`. **All 404.**
9. Confirm there is no link in the Admin sidebar matching "Fiscal Calendar" / "Periods" / "Calendar".

#### Why this is P1
Every flow that posts a journal entry hits this validator: Improve, Dispose, Run Depreciation, PO post, AP voucher post, GR-to-GL, CIP capitalization. Until DEF-004 is unblocked, ~30–40% of the test plan is wedged. **Worse**, the recovery path the error message advertises is also missing (DEF-006), so admins cannot self-serve.

#### Suggested fix (one PR fixes both)

**Step 1 — Seed/rollforward.** Locate the existing fiscal-calendar seeder and extend it to cover today + 2 years. Look for:
- `Data/Seeders/*` (search for "FiscalCalendar" or "FiscalPeriod")
- `Data/Migrations/*` (look for any `Insert` into `FiscalCalendar` / `FiscalPeriods`)
- `Services/FiscalCalendarService.cs` (validator likely lives here)

Preferred long-term: add an app-startup hook that ensures periods always cover `[today − 1y, today + 2y]` for every company.

**Step 2 — Locate the error message in code.**
```bash
grep -r "Set up the calendar in Admin" Pages/ Services/ Models/
```
That tells you where the exception/validation is thrown.

**Step 3 — Ship the Admin → Fiscal Calendar UI.**
- `Pages/Admin/FiscalCalendar.cshtml` + `.cshtml.cs` — CRUD over `FiscalCalendar` / `FiscalPeriod` entities. Minimum viable: list of periods by company + "Generate next year" button.
- Wire it into `Pages/Shared/_AdminLayout.cshtml` and `_ModernLayout.cshtml`.
- The link should point to whatever URL the error message references (so the error becomes self-healing).

**Step 4 — Smoke test after fix.**
```
1. Open /Admin/FiscalCalendar → see periods for 2026
2. Open asset 954, click Improve, fill form, save → should succeed; outbox event `asset.improved` should fire
```

**Files to look at first:**
- `Data/Seeders/`, `Data/Migrations/`, `Services/FiscalCalendarService.cs`
- `Pages/Assets/Improve.cshtml.cs` (`OnPostAsync`)
- `Pages/Admin/` (does `FiscalCalendar.cshtml` exist but isn't routed?)
- `Pages/Shared/_AdminLayout.cshtml`, `Pages/Shared/_ModernLayout.cshtml`

---

## P2 — Significant gaps

### DEF-001 — `/Admin/Webhooks/Deliveries` returns HTTP 404 and is not linked

#### Repro
1. From any authenticated page: `fetch('/Admin/Webhooks/Deliveries')` → **404**.
2. From the Admin → Webhooks page, search the DOM for `a[href="/Admin/Webhooks/Deliveries"]` — none.

#### Expected
Webhook delivery log page reachable from Admin → Webhooks (the test plan calls for it explicitly at T-100).

#### Files to look at
- `Pages/Admin/Webhooks/` — does `Deliveries.cshtml` exist? Was it scaffolded but not finished?
- `Pages/Shared/_ModernLayout.cshtml` / `_AdminLayout.cshtml` — nav link wiring.
- `docs/TESTING/E2E_TEST_PLAN.md` (T-100) — if the route was intentionally deferred, update the plan.

---

### DEF-005 — Asset Register search input does not filter the grid

#### Repro
1. Navigate to `/Assets`. Note 25 rows in tbody.
2. In console:
   ```js
   const s = document.querySelector('input[placeholder*="Search"]');
   s.value = 'CNC';
   s.dispatchEvent(new Event('input', {bubbles:true}));
   s.dispatchEvent(new Event('change', {bubbles:true}));
   ```
3. Inspect tbody — still 25 rows.

#### Expected
Live filter on asset Description / AssetNumber.

#### Likely cause
Front-end search wiring is on a different event (`keyup`, debounced React/Stimulus controller), or the listing handler doesn't accept a `?q=` parameter. Check the actual event the input listens for.

#### Files to look at
- `Pages/Assets/Index.cshtml` — find the `<input>` and inspect data attributes.
- `wwwroot/js/asset-grid.js` (or whichever JS owns the grid).
- `Pages/Assets/Index.cshtml.cs` (`OnGetAsync`) — does it read `q` from query string?

---

### DEF-007 — API surface is unusually small (22 paths); `/api/health` and `/api/version` don't exist

#### Repro
1. Open `/swagger/v1/swagger.json`. Title: "CherryAI EAM API", v1, 22 paths.
2. `fetch('/api/health')` → 404. `fetch('/api/version')` → 404.

#### Expected
Either (a) intentionally minimal — document why, or (b) more controllers should be exposed via OpenAPI. Health + version are conventional for monitoring.

#### Files to look at
- `Program.cs` / `Startup.cs` — Swagger registration (`AddSwaggerGen` config).
- `Controllers/` — count vs. swagger output.
- Add `MapHealthChecks("/api/health")` and a version endpoint. Both are tiny.

---

## P3 — UX / polish

### DEF-002 — Dashboard KPI cards are non-clickable when plan requires drilldowns

Open Work Orders + Active Capital Projects KPI cards on `/` are bare `<div>`s with no pointer cursor and no `onclick`. Plan T-113/T-114 expects them to drill into `/Maintenance` and `/CIP`.

**Fix:** Wrap each clickable KPI card in `<a href="…">` like Total Asset Value already is. Add `cursor: pointer` styling.

**Files:** `Pages/Index.cshtml`, `Pages/Index.cshtml.cs`, `wwwroot/css/dashboard.css` (or equivalent).

---

### DEF-003 — Header context strip / company-site selector missing on the dashboard

The "All Companies" / "All Sites" buttons appear in the header on inner pages (e.g., `/Assets/Transfer/954`) but are absent from `/`. Layout drift.

**Fix:** `Pages/Index.cshtml` likely uses a different layout/partial than inner pages. Include `_IndexContext` partial (or whichever partial renders the strip) in the dashboard layout.

**Files:** `Pages/Index.cshtml`, `Pages/Shared/_ModernLayout.cshtml`, `Pages/Shared/_IndexContext.cshtml`, `Pages/Shared/_Layout.cshtml`.

---

## P4 — Doc / plan drift (low priority)

These are flagged for completeness; they're not bugs in the app. Update `docs/TESTING/E2E_TEST_PLAN.md` to match current reality.

### DEV-001 — URL space mismatches (10+ routes)

The plan documents routes like `/Admin/PMTemplates`, `/Admin/Requisitions`, `/Admin/ItemCategories`, etc. The live app reorganized many of these under domain prefixes (`/Maintenance/PMTemplates`, `/Purchasing/Requisitions`, `/Materials/Categories`). All routes work; only the plan is wrong.

Full list in `DEFECTS.md`. Update the plan once or update the test executor selectors to be tolerant of both URL spaces.

### DEV-002 — Page titles say "ABS Machining EAM" not "CherryAI EAM"

Plan T-001 expects `<title>` to contain "CherryAI EAM". Live titles say "ABS Machining EAM" (the customer/tenant). Footer correctly says "ABS Machining EAM, powered by CherryAI". Either change titles to include the product name, or change T-001 to look for "powered by CherryAI" in the footer.

### DEV-003 — Asset detail tabs differ from plan

| Plan | Live |
|---|---|
| Overview, Specifications, Meter Readings, Maintenance, Transactions, Attachments, Image, Documentation, Audit Trail | General, Location, Financial, Technical, MES/OEE, IoT, Safety, Warranty, Hierarchy, Attachments, Transactions |

Live model is engineering/operations-flavoured (MES/OEE, IoT, Safety) — plan is accounting-flavoured. Reconcile or document why.

### DEV-004 — Pages exist but aren't in nav

`/Admin/Approvals`, `/Admin/Diagnostics`, `/Admin/Outbox/Index` all return 200 but have no sidebar entry. Either add nav links or document why they're hidden.

---

## Operator action items (these are not for Claude Code)

These need a human at the Replit shell:

1. **Run `db-snapshots/README.md` snippets** to capture baseline counts — needed for outbox-event audit on next run.
2. **After Claude Code fixes DEF-004**, restart the Replit dyno and confirm asset 954's Improve flow works end-to-end.
3. **Decide:** is `/Admin/Diagnostics` supposed to be in the user-visible nav? It's a useful page; should probably be linked from System Settings.

---

## What this run did NOT cover (for the next test session)

This is the queue for the next E2E run. Order matters — these depend on DEF-004 being fixed.

| Priority | Module / area | Why deferred |
|---|---|---|
| 1 | Module 4.5 — Improve / Dispose / Run Depreciation | Need DEF-004 fixed |
| 2 | Module 19 — Marquee end-to-end workflows | Need DEF-004 fixed |
| 3 | Module 8/9/10 — PO → Receive → AP voucher chain | Need DEF-004 fixed |
| 4 | Module 13 — Run Depreciation, post adjustments | Need DEF-004 fixed |
| 5 | Module 16 — Report exports (CSV/XLSX downloads) | Coordination with computer-use for download capture |
| 6 | Module 5–7 — Master-data CRUD on Items, Vendors, PM Templates, etc. | Should be JE-free, can run independently |
| 7 | Module 4.1 — Asset grid sort/filter/pagination/export | Time budget |
| 8 | Module 20 — Authenticated API smoke | Needs auth-cookie bridge |
| 9 | Asset attachment upload (T-165, T-167) | Needs file-upload bridging |
| 10 | All `Verify` SQL assertions | Needs psql access |

---

## Files produced this run

```
docs/TESTING/runs/RUN-20260508-132947/
├── MASTER_TRACKER.md
├── DEFECTS.md
├── HANDOFF_FOR_CLAUDE_CODE.md   ← you are here
├── modules/
│   ├── Module-01-02_Preflight-and-NavMap.md
│   ├── Module-03_HomeDashboard.md
│   ├── Module-04_Assets.md
│   ├── Module-05-10_MasterDataAndProcurement.md
│   ├── Module-11-17_FinanceAndAdmin.md
│   └── Module-18-21_OutboxApiAndFinal.md
├── db-snapshots/
│   └── README.md   (psql snippets for operator)
└── fixtures/
    ├── test.txt
    ├── test.png
    └── test.pdf   (for future attachment-upload tests)
```

End of hand-off.
