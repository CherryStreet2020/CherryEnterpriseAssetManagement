# CherryAI EAM — Test Scripts

## Layout

This `tests/` folder holds four distinct test surfaces. Each catches
different bugs; together they form the full quality posture:

| Surface | Where | Catches |
|---|---|---|
| **xUnit unit/integration tests** | `Abs.FixedAssets.Tests/*.cs` | Code-level logic, service-layer behavior, DI wiring |
| **Playwright browser E2E** | `*.spec.js` (~30 files, ~3,400 lines) | Real user flows: login, navigate, click, assert UI |
| **DB schema validation** | `db-validation/` *(new — Phase E)* | Table/column/FK/index presence + correct delete behaviors |
| **DB integration scenarios** | `integration-scenarios/` *(new — Phase E)* | Real INSERT/DELETE proving CASCADE/SET NULL/RESTRICT/UNIQUE actually fire |
| **HTTP route smoke** | `route-smoke/` *(new — Phase E)* | Every nav URL returns expected status (200/302/404 not 500) |

The Phase E scripts (bash + psql) added with the Phase E test pass are
the **bottom-of-the-stack invariants**: they assume the EF model + Razor
pages + Playwright already exist, and verify the database itself is
shaped correctly. Catches schema drift that compiles + renders but is
subtly wrong (wrong delete behavior, missing index, dropped column).

## What's here

| Folder | Purpose | When to run |
|---|---|---|
| `db-validation/` | Verifies every table, column, FK, UNIQUE index from the Phase E PRs is present with correct shape | After any schema-touching deploy |
| `integration-scenarios/` | Real INSERT/DELETE rolled back in a transaction — proves CASCADE / SET NULL / RESTRICT / UNIQUE behave correctly | After any FK-touching deploy |
| `route-smoke/` | Curls every nav route to confirm 200/302 (no 500s) | After any controller / Razor page deploy |

All scripts are idempotent and safe to re-run. The integration
scenarios run inside `BEGIN ... ROLLBACK` so production data is never
mutated.

## How to run (from inside Replit Shell)

```bash
cd ~/workspace
bash tests/db-validation/01-schema-validation.sh
bash tests/integration-scenarios/02-integration-scenarios.sh
bash tests/route-smoke/03-route-smoke.sh
```

Each script exits 0 on success and 1 if any check fails. Output is
human-readable, one line per assertion: `  PASS  description` or
`  FAIL  description` followed by expected/actual on a failure.

To run all three in sequence:

```bash
bash tests/run-all.sh
```

## Coverage

### Layer 1 — Schema (`01-schema-validation.sh`)

Verifies presence + shape of:

- **PR #119.12** — `ProductionOrders`, `ProductionJobShopDetails`,
  `WorkOrderOperations` outside-op extension columns
- **PR #119.13a** — `ProductionBatches` polymorphic parent, `Nests`,
  `ProcessBatches`, `ProductionBatchAllocations`,
  `ProductionBatchEquipmentLinks`, `ProductionBatchStateEvents`,
  `RecipeRevisions`, `MrbDispositions`, `WorkOrderOperations` batch
  extension columns
- **PR #119.13b** — `MaterialMasters`, `StockReceipts`, `Remnants`,
  `CutListLines`, `Nests.StockReceiptId`

For every FK: checks `confdeltype` in `pg_constraint` to confirm the
ON DELETE behavior (CASCADE / SET NULL / RESTRICT) matches the source-
of-truth schema in the AppDbContext.

### Layer 2 — Integration scenarios (`02-integration-scenarios.sh`)

Real data scenarios in a rolled-back transaction:

1. **Heat number genealogy** — insert StockReceipt -> Nest ->
   CutListLine, verify the heat number is retrievable end-to-end
2. **Polymorphic batch creation** — insert ProductionBatch + Nest
   subtype, then + ProcessBatch subtype, verify both come back with
   their type-specific fields
3. **CASCADE** — delete ProductionBatch removes Nest / Allocations /
   StateEvents
4. **RESTRICT** — cannot delete StockReceipt that has a Remnant
5. **UNIQUE** — duplicate BatchNumber raises `unique_violation`
6. **State event audit log** — manual UPDATE on `ProductionBatch.Status`
   alongside an INSERT into `ProductionBatchStateEvents` lands correctly
7. **Allocation row** — link ProductionBatch -> WorkOrderOperation with
   AllocatedCost, retrieve via the parent join

### Layer 3 — Route smoke (`03-route-smoke.sh`)

Curls every nav route and confirms:
- Auth pages return 200
- Authenticated pages return 302 (login redirect) — confirms the route
  is wired, not 404
- Phase E new-schema URLs (e.g., `/ProductionBatches`) currently 404
  because no UI is wired yet — a 500 here would mean the EF model is
  misconfigured

### Layer 4 — Browser E2E (Playwright `.spec.js`)

Existing suite. `smoke_all_pages.spec.js` already covers every Razor
page (login, navigate, assert 200 or explicitly-tolerated 3xx/404).
When Phase F lands UI for ProductionOrders / ProductionBatches /
StockReceipts / etc., add their routes to the `STATIC_PAGES` array
in `smoke_all_pages.spec.js` (and `pickers` for any new param-route
detail pages).

Until then, the Phase E entities have no UI to test — only the schema
(covered by Layer 1+2) and the route shape (covered by Layer 3
returning 404 for the not-yet-wired controllers).

## Adding new tests

When you add a new table or column, append to the relevant `.sh`:

- Schema validation: add `table_exists`, `column_exists`, `column_type`,
  `fk_with_action`, or `unique_index` lines for each new field
- Integration: add a new `scenario "..."` block that exercises the
  expected data flow
- Route smoke: add a `probe "..."` line for any new controller

## Why bash + psql, not xUnit

The existing test project (`tests/Abs.FixedAssets.Tests/`) has 5
constructor-drift errors that predate ADR-012. Until they're fixed, we
can't run `dotnet test` cleanly. The bash + psql approach:

1. Runs entirely outside the test project, so the broken xUnit tests
   don't block it
2. Hits the live DB on Replit — actually verifies the deployed schema,
   not just the in-memory `AppDbContext`
3. Re-runnable by anyone with shell access; no .NET toolchain required

Once the test project is fixed, code-level `AppDbContext` integration
tests will live alongside these scripts.
