# E2E Test Plan — Sprint 3 Phase A/B + CIP Rename

**Date:** 2026-05-17
**Scope:** PR #119.1 through #119.5 (config-backbone) + PR #159 (CIP rename)
**Outcome target:** All critical tests PASS before starting Phase C (rename of `MaintenanceEvent` → `WorkOrder`).

---

## Process commitment

**From this point forward we E2E-test after every PR that touches a screen or user-visible functionality.** Config-backbone PRs (no UI consumer yet) get the lighter psql + build-hygiene check. Screen/UI PRs get the full browser-driven E2E.

This file is the template. Copy + adapt per sprint phase.

---

## What's been shipped that we're testing

| PR | Behavior shipped |
|---|---|
| #119.1 | `WorkOrderClassification` enum + `Category` / `ExternalWorkOrderId` / `ExternalSource` columns + 2 indexes on `MaintenanceEvents` |
| #119.1.1 | Rename `WorkOrderCategory` → `WorkOrderClassification` (CS0104 fix) |
| #119.1.2 | Drop `Production` from enum, rename `Project` → `CIP` |
| #119.2 | `WorkOrderFieldVisibility` table (77 rows seeded) + cache + service |
| #119.3 | `WorkOrderStatusProfile` (5) + `WorkOrderStatusLabel` (44) + `WorkOrderStatusTransition` (48) + engine |
| #119.4 | `WorkOrderApproval` polymorphic table + service + status-engine wiring |
| #119.5 | `NumberSequence` (5 rows seeded) + atomic generator service |
| #159 | CIP page header → "Capital Improvement Project"; GL accounts kept on GAAP "Construction in Progress" |

---

## Test lineage

Each test has a unique ID and a Pass/Fail criterion. Order matters — earlier tests gate later ones (no point testing Sprint 3 if Sprint 2 broke).

### T1 — Sprint 2 regression (must hold)

| ID | Test | How | Pass criterion |
|---|---|---|---|
| T1.1 | Plant index renders | Navigate `/Plant` | 12 plant cards visible; Main Manufacturing Plant shows 320 assets |
| T1.2 | Plant Floor renders | Navigate `/Plant/Floor/1` | 320 asset cards visible; storyline assets at top with Critical (Health 0); per-classification sensor tiles populated |
| T1.3 | Asset detail renders | Navigate to one storyline asset (AST-00001 HAAS VF-2SS) | Asset header + financials + recent activity all populated |

### T2 — CIP rename + GAAP preservation (PR #159)

| ID | Test | How | Pass criterion |
|---|---|---|---|
| T2.1 | New CIP header | Navigate `/CIP` | Header reads **"Capital Improvement Project"**; subtitle says "Track capital improvement projects from planning through capitalization"; 5 ACTIVE chip + 11/5/$5,185,000/$7,010 KPIs intact |
| T2.2 | CIP detail still works | Click into CIP-CAN-001 (Robotic Welding Cell) | Project detail page renders without error |
| T2.3 | GL accounts GAAP preserved | Navigate `/Books/GlAccounts` | Picker label "CIP Account (Construction in Progress)" still says **Construction in Progress** (intentional, GAAP standard) |
| T2.4 | GL account 1580 name preserved | psql `SELECT "Name" FROM "BookGlAccounts" WHERE "AccountNumber"='1580'` | Returns **"Construction in Progress"** (GAAP chart-of-account name unchanged) |

### T3 — Phase A enum integrity (PRs #119.1, #119.1.2)

| ID | Test | How | Pass criterion |
|---|---|---|---|
| T3.1 | Classification column exists | psql `\d "MaintenanceEvents"` | Column `Classification` smallint NOT NULL DEFAULT 0 present |
| T3.2 | All rows backfilled to Maintenance | psql `SELECT "Classification", COUNT(*) FROM "MaintenanceEvents" GROUP BY "Classification"` | Exactly 279 rows at Classification=0; ZERO rows at Classification=1 (Production gap honored) |
| T3.3 | External-source columns exist | psql `\d "MaintenanceEvents"` | Columns `ExternalWorkOrderId` varchar(64) NULL, `ExternalSource` varchar(32) NULL present |
| T3.4 | Indexes created | psql `\di "IX_MaintenanceEvents_*"` | `IX_MaintenanceEvents_Classification` + `IX_MaintenanceEvents_ExternalSource_ExternalWorkOrderId` present |

### T4 — Phase B config-table integrity (PRs #119.2–#119.5)

| ID | Test | How | Pass criterion |
|---|---|---|---|
| T4.1 | FieldVisibility seeded | psql `SELECT "Classification", COUNT(*) FROM "WorkOrderFieldVisibility" GROUP BY "Classification"` | Counts: Maintenance=25, Quality=11, Engineering=10, HSE=13, CIP=18 (total 77) |
| T4.2 | FieldVisibility tenant-override structure works | psql verify the unique index `IX_WorkOrderFieldVisibility_Classification_FieldName_TenantId` exists and uses COALESCE(TenantId, 0) | Index present |
| T4.3 | StatusProfile seeded | psql `SELECT "Classification", "Name" FROM "WorkOrderStatusProfile" ORDER BY "Classification"` | 5 rows: Maintenance, Quality, Engineering, HSE, CIP |
| T4.4 | StatusLabel seeded | psql `SELECT COUNT(*) FROM "WorkOrderStatusLabel"` | 44 rows |
| T4.5 | StatusTransition seeded | psql `SELECT COUNT(*) FROM "WorkOrderStatusTransition"` | 48 rows |
| T4.6 | CIP SubstantialComplete state present | psql `SELECT "DisplayLabel", "DisplayColor", "IsTerminal" FROM "WorkOrderStatusLabel" WHERE "Classification"=5 AND "StatusKey"='SubstantialComplete'` | Returns "Substantial Completion" / green / false |
| T4.7 | Engineering PssrRequired guards visible | psql `SELECT "ActionLabel", "RequiredApprovalStage", "GuardServiceName" FROM "WorkOrderStatusTransition" WHERE "Classification"=3 AND "ToStatusCode"=6` | Returns row with ActionLabel="Mark Effective", RequiredApprovalStage="PSSR", GuardServiceName="PssrCompletionGuard" |
| T4.8 | WorkOrderApproval table exists | psql `\d "WorkOrderApproval"` | Table present with FK to MaintenanceEvents (CASCADE) + FK to Users (SET NULL) |
| T4.9 | Approval legacy backfill correct | psql `SELECT "Stage", COUNT(*) FROM "WorkOrderApproval" GROUP BY "Stage"` | 0 rows (no demo MaintenanceEvent has ApprovedById — backfill found nothing, correct) |
| T4.10 | NumberSequence seeded | psql `SELECT "Classification", "Year", "Prefix" FROM "NumberSequence" ORDER BY "Classification"` | 5 rows: (0,2026,PM), (2,2026,NCR), (3,2026,ECO), (4,2026,INC), (5,2026,AFE) |
| T4.11 | NumberSequence COALESCE unique index | psql `\di "IX_NumberSequence_*"` | `IX_NumberSequence_Classification_Year_TenantId` present |

### T5 — Sensor API regression (Sprint 2 / PRs #118.2-#118.6)

| ID | Test | How | Pass criterion |
|---|---|---|---|
| T5.1 | Sensor latest endpoint enforces tenant headers | `curl -s http://localhost:5000/api/v1/sensors/latest?assetIds=1,2,3` | Returns `{"error":"Missing required headers","missing":["X-Tenant-Id","X-User-Id","X-Org-Node-Id"]}` |
| T5.2 | SensorEvents hypertable intact | psql `SELECT COUNT(*) FROM "SensorEvents"` | ≥ 3,000,000 rows (backfill from PR #118.5 still present) |
| T5.3 | AssetSensorLatest cache intact | psql `SELECT COUNT(*) FROM "AssetSensorLatest"` | 905 rows |

### T6 — Build hygiene

| ID | Test | How | Pass criterion |
|---|---|---|---|
| T6.1 | Build clean on main | `dotnet build Abs.FixedAssets.csproj --no-restore` | 0 Errors, ≤ 23 Warnings (no new warnings from Phase A/B) |
| T6.2 | HEAD is at expected commit | `git log -1 --oneline` | `f2a6fb0` (after PR #159) |

---

## Test results template

After execution, log each test as PASS / FAIL / SKIP with notes.

| ID | Status | Notes |
|---|---|---|
| T1.1 | ⬜ | |
| T1.2 | ⬜ | |
| T1.3 | ⬜ | |
| T2.1 | ⬜ | |
| T2.2 | ⬜ | |
| T2.3 | ⬜ | |
| T2.4 | ⬜ | |
| T3.1 | ⬜ | |
| T3.2 | ⬜ | |
| T3.3 | ⬜ | |
| T3.4 | ⬜ | |
| T4.1 | ⬜ | |
| T4.2 | ⬜ | |
| T4.3 | ⬜ | |
| T4.4 | ⬜ | |
| T4.5 | ⬜ | |
| T4.6 | ⬜ | |
| T4.7 | ⬜ | |
| T4.8 | ⬜ | |
| T4.9 | ⬜ | |
| T4.10 | ⬜ | |
| T4.11 | ⬜ | |
| T5.1 | ⬜ | |
| T5.2 | ⬜ | |
| T5.3 | ⬜ | |
| T6.1 | ⬜ | |
| T6.2 | ⬜ | |

---

## Gate to Phase C

**Phase C (the rename PR) does NOT start until:**
- All T1, T2, T3, T4, T6 tests PASS
- T5 tests PASS or have a documented follow-up PR queued

If any FAIL: ship a `.1` follow-up PR before Phase C.
