# Modules 11–17 — CIP, Books, Journals, Bulk Ops, CCA/US Tax, Reports, Admin sweep

**Status:** PARTIAL (page-render coverage + a few deeper probes)
**Started:** 2026-05-08 14:25 UTC
**Finished:** 2026-05-08 14:30 UTC

## Page-render summary

| Module | Page | Renders? | Notes |
|---|---|---|---|
| 11 | `/CIP` | ✅ | 11 projects in grid |
| 11 | `/CIP/Costs` | ✅ | (Module 2 sweep) |
| 11 | `/CIP/PartyDrilldown` | ✅ | (Module 2 sweep) |
| 12 | `/Books` | ✅ | "Depreciation Books" |
| 12 | `/Books/GlAccounts` | ✅ | "Chart of Accounts" |
| 13 | `/Journals` | ✅ | 25 entries |
| 14 | `/BulkOperations` | ✅ | renders |
| 15 | `/CCA` | ✅ | "Canadian CCA" |
| 15 | `/UsTax` | ✅ | "US Tax (MACRS/179)" |
| 16 | `/Reports/ReportHub` | ✅ | 7 report links surfaced |
| 16 | `/Reports/Builder` | ✅ | "Report Builder" |
| 16 | `/Reports/Compliance` | ✅ (200 via fetch) | not directly clickable from `/Reports/Builder`; reach via ReportHub |
| 17 | `/Admin/Approvals` | ✅ (200 via fetch) | exists but not in nav — see DEV |
| 17 | `/Admin/Diagnostics` | ✅ (200 via fetch) | exists but not in nav — see DEV |
| 17 | `/Admin/FiscalCalendar` | ❌ **404** | **DEF-006** — error message at improvement step points users to this route, but it doesn't exist |
| 17 | `/Admin/FiscalPeriods` | ❌ 404 | (any plausible variant 404s — see DEF-006) |

## Deeper probes

### Module 13 — Journals
- 25 entries in grid.
- "New JE" / "Run Depreciation" buttons not located by simple regex; either they're text-less icons or live on a different page (e.g., the Books page). DEFER deep CRUD; the relevant flows post JEs and would be blocked by DEF-004 anyway.

### Module 14 — Bulk Operations
- Page renders.
- Specific bulk actions (Bulk Update, Bulk Reassign, Bulk Categorize, etc.) not exercised. DEFER — these may also touch the GL.

### Module 16 — Reports
- `/Reports/Compliance` page is reachable via direct URL but the link from `/Reports/Builder` was missing in this session — likely the Compliance card lives only on `/Reports/ReportHub`. DEV (plan implies all reports listed in Builder).
- Open report viewing (run a depreciation preview, export CSV, etc.) DEFER.

### Module 17 — Admin sweep
- All 11 admin pages from Module 2 still pass.
- New finding: `/Admin/Approvals` and `/Admin/Diagnostics` exist as routes (200 OK) but are NOT in the sidebar. **DEV-004** logged.

## Steps not exercised (DEFER for next run)

- **All deep CRUD on Books/Journals/CIP** because these post JEs (blocked by DEF-004).
- **Run Depreciation** (T-820+) — same blocker.
- **Report exports / CSV downloads** — not exercised; would benefit from a follow-up pass once DEF-004 is fixed.

## DEV findings

### DEV-004 — Admin pages not linked from sidebar

`/Admin/Approvals` and `/Admin/Diagnostics` are functional pages (200 OK) but no `<a>` to them exists in the sidebar/menu. Add nav links or document why they're hidden (e.g., feature-flagged).
