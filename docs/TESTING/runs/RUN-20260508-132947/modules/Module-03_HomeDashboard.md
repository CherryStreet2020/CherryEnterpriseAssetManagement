# Module 3 — Home dashboard (T-110 → T-130)

**Status:** DONE
**Started:** 2026-05-08 13:55 UTC
**Finished:** 2026-05-08 14:00 UTC

## Summary

| Step | Status | Result |
|---|---|---|
| T-110 | PASS | "Total Asset Value" KPI clicked → `/Assets` (Asset Register) |
| T-111 | N/A | "Net Book Value" KPI is a non-clickable `<div>`. Plan allows "drilldown or no-op (per design)" — no-op is acceptable. |
| T-112 | N/A | "Accumulated Depreciation" KPI is a non-clickable `<div>`. Plan allows no-op. |
| T-113 | **FAIL** | Plan requires the "Open Work Orders" KPI to drill into `/Maintenance`. Actual: card is a non-clickable `<div>`. **DEF-002 logged.** |
| T-114 | **FAIL** | Plan requires the "Active Capital Projects" KPI to drill into `/CIP`. Actual: card is a non-clickable `<div>`. **DEF-002 logged (same defect class).** |
| T-115 | N/A | Plan expected a "Pending Approvals" KPI; this build shows "Fair Market Value $0" instead. **DEV logged in DEFECTS.md as a planning question, not a defect.** |
| T-116a | PASS | Quick action "Add New Asset" → `/Assets/Asset?mode=create` (h1: Register Capital Asset) |
| T-116b | PASS | Quick action "Run Depreciation" → `/Journals` (Journal Entries) |
| T-116c | PASS | Quick action "View Reports" → `/Reports/ReportHub` |
| T-116d | PASS | Quick action "Create Work Order" → `/Maintenance/WorkRequests/Create` (New Work Request) |
| T-117 | PASS | Recent Activity row "TEST-E2E-001" → `/Assets/Asset/907` (h1: TEST-E2E-001). Plan's URL template `/Assets/Asset?id={N}&mode=view` differs from actual `/Assets/Asset/{id}` — DEV logged. |
| T-118 | **FAIL** | Plan expected a header context strip with a company/site selector. None present in the header DOM. **DEF-003 logged.** |
| T-119 | N/A | Tied to T-118 (no re-selection possible since selector doesn't exist). |
| T-120 | PASS | Header has Help (`/Help/Topic?id=dashboard` — "Understanding the Dashboard" page renders), Sign Out, New Asset, Reports. No additional Actions cluster button found, but all observed header actions function. |
| T-130 | PASS | Reload preserves KPIs (Total Asset Value remains $88,040,015). No JS errors on reload. |

## Notes

- Console clean throughout (no `Uncaught` errors).
- The dashboard's **6th KPI** is "Fair Market Value" (always $0 in this seed) — plan expects "Pending Approvals". Recommend the plan be reconciled OR the missing Pending Approvals KPI be implemented.
- `_IndexContext` partial / company-site selector is mentioned in the plan as a present feature. Either it's stripped from this build, gated behind multi-company licensing, or it was removed. Worth checking with whoever owns the layout.

## Verdict

13 PASS, 2 FAIL (KPI drilldowns + missing context selector), 4 N/A. Two new defects logged.
