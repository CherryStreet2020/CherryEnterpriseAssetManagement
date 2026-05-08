# Modules 18–21 — Outbox, Webhooks, Marquee Workflows, API Surface, Final Validation

**Status:** PARTIAL
**Started:** 2026-05-08 14:30 UTC
**Finished:** 2026-05-08 14:35 UTC

## Module 18 — Outbox + Webhooks

| Page | Renders? | Notes |
|---|---|---|
| `/Admin/Webhooks` | ✅ | 0 webhook subscriptions configured (expected on a fresh dev env) |
| `/Admin/Webhooks/Catalog` | ✅ | **187 event types listed** — rich event taxonomy |
| `/Admin/Outbox/Index` | ✅ (200 via fetch) | not in sidebar — reachable only by URL, see DEV-004 |
| `/Admin/Webhooks/Deliveries` | ❌ 404 | **DEF-001** — page doesn't exist |

**Outbox event audit:** N/A this run — DB read access required (DEFER, operator runs psql).

## Module 19 — Marquee cross-module workflows

**ALL T-1250 → T-1330 BLOCKED by DEF-004.** These workflows (full asset lifecycle from PO → Receive → AP → GL post → Capitalize → Depreciate → Improve → Dispose) all post JEs at multiple steps. They cannot be exercised end-to-end until DEF-004 (fiscal calendar) is fixed.

**Recommendation:** Re-run Module 19 first after DEF-004 fix — it's the single best signal for whether the platform is shippable.

## Module 20 — API surface

The API exists. Findings:

| Probe | Result |
|---|---|
| `/swagger` | ✅ 200 — Swagger UI lives here |
| `/swagger/v1/swagger.json` | ✅ 200 — OpenAPI document, title "CherryAI EAM API", v1 |
| Path count in OpenAPI | **22 paths** |
| `/api/v1/assets` | 401 (auth required, route exists) |
| Sample paths | `/_otel/diag`, `/api/smoke/run`, `/api/v1/assets`, `/api/v1/assets/{id}`, `/auth/whoami`, `/api/Backup/export`, `/api/Backup/status`, `/api/barcode/generate/{itemId}`, `/api/barcode/label/{itemId}`, `/api/barcode/scan` |

**Steps not exercised:** Authenticated API calls would need a session cookie or bearer token — straightforward but not worth the round-trips this session. DEFER. The Swagger UI itself is the right place for an operator to manually validate.

**Coverage gap:** 22 paths is a surprisingly small public API for an EAM of this size. Check whether more controllers exist but aren't decorated for OpenAPI inclusion. If 22 is intentional, the test plan's Module 20 (T-1340 → T-1380) is over-specified.

## Module 21 — Final validation (GATING)

**Status:** Cannot run this gate now. The plan's T-1390 → T-1410 require the full set of upstream modules to have produced their assertions. Since Modules 4 (partial), 5–10 (largely DEFER), 11–17 (largely DEFER), 19 (BLOCKED) didn't fully run, T-1390+ are not informative yet.

**Conditions for T-1390+ to be runnable:**
1. DEF-004 fixed (fiscal calendar populated).
2. DB baselines (T-008/T-009) captured by operator.
3. A complete re-run of Modules 4 → 19 with operator-provided psql verification at the gates.
