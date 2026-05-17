# Sprint 3 Execution Plan — ADR-012 v0.2 Unified Work Orders

**Date:** 2026-05-17
**Owner:** Dean (vision) + Claude (execution)
**Status:** Locked — execute top to bottom
**Companion doc:** [ADR-012 v0.2](./adr/ADR-012-unified-work-orders.md) (to be rewritten in Phase A.1)

---

## North Star

Deliver the disruption pitch — **"every piece of work touching an asset in one screen, with the right form per kind of work"** — by adopting the **unified-data + per-type-UX** pattern that Maximo, SAP, Oracle, and the ISA-95 standard all converged on after 20 years.

**One unified `WorkOrder` table** for Maintenance / CIP / Quality / HSE / Engineering ECO+MOC.
**One sibling `ProductionOrder` table** for shop-floor production runs (separate state machine, separate event cadence, federated via shared Asset FK).
**Per-classification UI** driven by config tables — no code branching per type in the renderer.

---

## Phase map

```
A. Architecture lock          (1-2 days)   →  ADR v0.2 + enum cleanup
B. Configuration backbone     (2-3 days)   →  Field visibility / status profile / approval / number sequence
C. Header rename + Revision   (1 day)      →  MaintenanceEvent → WorkOrder + Revision column
D. Per-classification satellites (3-4 days)→  CIP / Quality / Engineering / HSE detail tables
E. Sibling Production model   (2-3 days)   →  ProductionOrder + run-card satellite + lot genealogy
F. Visible UI                 (5-7 days)   →  Unified queue page + per-type create/edit screens
─────────────────────────────────────────
TOTAL                          ~14-20 days  (calendar; assumes 2-3 PRs/day cadence we've been hitting)
```

**Hard milestone checkpoints** (Dean verifies live):

- **End of Phase A** — ADR v0.2 signed; enum cleanup live. No visible change.
- **End of Phase C** — `WorkOrder` table renamed; Plant Floor + Asset Detail still render. Internal-only milestone.
- **End of Phase D** — All 4 satellites exist with seed data. No visible change yet.
- **End of Phase E** — Production model + sample seed. No visible change yet.
- **End of Phase F.1** — Unified `/WorkOrders` page renders WorkOrder + ProductionOrder in one queue. **FIRST VISIBLE WIN.**
- **End of Phase F** — All 6 per-type create/edit screens live. **DISRUPTION PITCH READY TO DEMO.**

---

## Phase A — Architecture lock

### A.1 — Rewrite ADR-012 to v0.2
**Deliverable:** `docs/adr/ADR-012-unified-work-orders.md` rewritten with:
- v0.2 Status header
- Synthesis of the four-stream research (Maximo, SAP, Infor, Oracle, Dynamics, ISA-95, B2MML)
- Decision update: **drop `Production` from `WorkOrderClassification` enum, make it a sibling table.**
- New supporting infrastructure documented: `WorkOrderFieldVisibility`, `WorkOrderStatusProfile`, `WorkOrderApproval` (polymorphic), `NumberSequence`, `Revision` on header.
- Sibling Production model: `ProductionOrder` + `ProductionRunCard` + `MaterialConsumption` (lot genealogy).
- Updated migration plan (12 PRs in sequence).
- Anti-goals (don't replace SAP PP; don't model timekeeping yet).
- Dean's sign-off on v0.2.

**Why first:** Every subsequent PR ships against the v0.2 plan. If anything in v0.2 changes, do it here, not mid-migration.

### A.2 — PR #119.1.2: drop `Production` from `WorkOrderClassification`
**Files:**
- `Models/AssetMaintenance.cs` — remove `Production = 1` from the enum, renumber down (or leave a gap — preferred to keep gap to avoid data churn).
- Migration `20260518_DropProductionFromClassification.cs` — re-stamp any existing rows that ended up with Classification=1 back to Classification=0 (Maintenance). At this moment there are 0 rows with that value, so the migration is a guard.
- Comment in the enum noting Production now lives in `ProductionOrder` (sibling, not subtype).

**Risk:** None. Production has never been written.

---

## Phase B — Configuration backbone

These tables turn "per-type behavior" from a code-branching nightmare into config data. They're the single biggest architectural payoff in this sprint.

### B.1 — PR #119.2: `WorkOrderFieldVisibility` config table
**Pattern:** SAP `OIAN` field-selection table. One row per `(Classification × FieldName)`.

**Schema:**
```
WorkOrderFieldVisibility
  Id              int PK
  Classification  smallint     (WorkOrderClassification enum)
  FieldName       varchar(64)  ("FailureCode", "AfeNumber", "NcrNumber", etc.)
  Visibility      smallint     enum: Hidden=0, Optional=1, Required=2, ReadOnly=3
  DisplayLabel    varchar(80)  (override default field label per category)
  DisplayOrder    int
  SectionName     varchar(40)  ("Cost", "Closeout", "Approval", "Production", "Capital Asset")
  TenantId        int? FK      (NULL = global default; tenant-specific row overrides)
```

**Seed:** ~120 rows covering the standard MaintenanceEvent fields × 5 classifications. Service `IWorkOrderFieldVisibilityService.GetLayout(classification, tenantId)` returns the layout ordered by SectionName + DisplayOrder for the renderer.

**Risk:** Low. Read-only at runtime. Seeder runs once at startup (idempotent).

### B.2 — PR #119.3: `WorkOrderStatusProfile` + `WorkOrderStatusTransition`
**Pattern:** Maximo `WOSTATUS` synonym domain + classification-aware transition rules.

**Schema:**
```
WorkOrderStatusProfile
  Classification     smallint PK (one row per classification)
  Name               varchar(64)
  StartStatus        smallint
  FinalStatuses      smallint[]   (Postgres int array — Completed, Cancelled, etc.)

WorkOrderStatusTransition
  Id                 int PK
  Classification     smallint
  FromStatus         smallint
  ToStatus           smallint
  RequiresApproval   bool
  RequiredApprovalStage  varchar(40)?   (e.g. "PSSR", "QA-Release", "Substantial-Complete")
  GuardServiceName   varchar(80)?       (e.g. "CipCapitalizationGuard" — DI key for runtime guard)
```

**Seed:** Maintenance gets the existing 6-state machine. CIP adds `SubstantialComplete` → triggers accounting reclassification. Engineering MOC adds `PssrRequired` blocking close. Quality adds `EffectivenessVerification`. Service `IWorkOrderStatusEngine.CanTransition(wo, newStatus)` is the centralized gate.

**Risk:** Medium. Touches the WorkOrder save path. Mitigation: ship with a feature flag (`UseStatusEngine`) defaulted off; flip on after PR #119.7 (rename) settles.

### B.3 — PR #119.4: polymorphic `WorkOrderApproval` table
**Pattern:** Replace the single `ApprovedBy/ApprovedAt` columns on the header with a 1:N polymorphic table.

**Schema:**
```
WorkOrderApproval
  Id              int PK
  WorkOrderId     int FK → WorkOrder
  Stage           varchar(40)  ("PM-Approval", "AFE-Tier1", "AFE-Tier2", "CCB", "PSSR", "QA-Release", "EffectivenessVerification")
  StageOrder      int          (within the workflow)
  RoleRequired    varchar(40)  ("Planner", "Engineering Director", "CFO", "Board", "Safety Officer", "QA Director")
  ApproverUserId  int? FK → Users
  Decision        smallint     enum: Pending=0, Approved=1, Rejected=2, Skipped=3
  DecisionAt      timestamptz?
  Comments        varchar(1000)?
  RowVersion      bytea        (xmin)
```

**Migration:** Backfill existing rows where `ApprovedBy != NULL` into a single-row `WorkOrderApproval` record (Stage="Legacy"). Keep the legacy columns for one PR cycle then drop in PR #119.4.1.

**Service:** `IWorkOrderApprovalEngine.Submit(workOrderId)` builds the approval chain from `WorkOrderStatusProfile` + threshold rules. Tier rules read from a `WorkOrderApprovalThreshold` config table (e.g. CIP > $50K → Tier 2 required).

**Risk:** Medium-high. Touches every WO create + approve UI. Mitigation: existing `WorkOrderApprovalStatus` enum stays valid; new table augments without replacing until F-phase UI ships.

### B.4 — PR #119.5: `NumberSequence` table
**Pattern:** SAP `NRIV` number range buffer. One row per `(Classification, Year)`.

**Schema:**
```
NumberSequence
  Id              int PK
  Classification  smallint
  Year            int
  Prefix          varchar(8)   ("PM", "AFE", "NCR", "ECO", "INC", "MO")
  CurrentValue    int
  StepSize        int          (default 1; reserved for batch reservation later)
  TenantId        int? FK
  RowVersion      bytea

  UNIQUE (Classification, Year, TenantId)
```

**Service:** `INumberSequenceService.Next(classification, tenantId)` returns the next `WorkOrderNumber` atomically (SELECT FOR UPDATE inside a short transaction). Falls back to a random GUID-tail if the row is locked by another session for >100ms.

**Risk:** Low. Net-new service. Old `WorkOrderNumber` strings on existing rows stay valid.

---

## Phase C — Header rename + Revision

### C.1 — PR #119.6: add `Revision` to WorkOrder header
**Schema:** `MaintenanceEvent.Revision` (smallint NOT NULL DEFAULT 0). Re-issued WOs get incremented revision; the original WO + each revision share a `MasterWorkOrderId` self-FK (NULL on first revision = "I am the master"). Index on `(MasterWorkOrderId, Revision DESC)`.

**Risk:** Low. Pure addition.

### C.2 — PR #119.7: rename `MaintenanceEvent` → `WorkOrder` (the big one)
**Scope:**
- `Models/AssetMaintenance.cs` → split into `Models/WorkOrders/WorkOrder.cs` + `Models/WorkOrders/MaintenanceSchedule.cs` (the latter stays unchanged for now).
- DB rename: `MaintenanceEvents` → `WorkOrders` via `ALTER TABLE` (Postgres). All FKs renamed accordingly. The migration uses raw SQL renames so PostgreSQL keeps the underlying OID and statistics — index rebuilds NOT needed.
- AppDbContext: `DbSet<MaintenanceEvent> MaintenanceEvents` → `DbSet<WorkOrder> WorkOrders`. Add a compatibility extension: `db.MaintenanceEvents` returns the same DbSet so legacy code keeps compiling until we sweep it.
- Refs across ~150 files (search/replace `MaintenanceEvent` → `WorkOrder` + `MaintenanceEvents` → `WorkOrders` + careful in Razor views where the noun "Maintenance" might be user-facing text we want to KEEP).
- Razor URL routes: `/Maintenance/*` → keep as a 301-redirect to `/WorkOrders/*` for ~60 days, then delete.
- The legacy `WorkOrderNumber` field stays — that's the customer-facing number; we don't rename it.

**Risk:** HIGH. This is the most invasive PR in the plan.

**Mitigation:**
1. Branch + grep + replace + dotnet build locally before push.
2. Razor views: do a manual diff review for user-facing text (don't replace "Maintenance Schedule" → "Work Order Schedule" wholesale — Maintenance Schedule is a real domain term that stays).
3. Migration runs `ALTER TABLE` not `CREATE NEW + COPY` — sub-second on existing data.
4. Rollback: if anything blows up after deploy, the `down` migration reverses the rename. 5 min recovery.

**Pre-flight tests before push:**
- `grep -rn "MaintenanceEvent" .` should match only intentional Razor view strings + comments.
- `dotnet build` succeeds.
- Plant Floor + Asset Detail render locally (via Replit preview after deploy).

### C.3 — Memory update
After C.2 succeeds, update `MEMORY.md` to add a note that the table/class is now `WorkOrder` so future sessions don't grep for the old name.

---

## Phase D — Per-classification satellites

Each PR follows the same shape:

1. New `Models/WorkOrders/<Classification>WorkOrderDetails.cs` entity (Id PK + WorkOrderId unique FK + the classification-specific fields).
2. AppDbContext registration with `HasOne(wo => wo.<Classification>Details).WithOne(d => d.WorkOrder)` 1:0..1 optional nav.
3. EF migration with raw SQL.
4. Seed: backfill 1-2 example rows so the renderer in Phase F has something to display. No mass backfill.
5. Field-visibility config rows: extend the `WorkOrderFieldVisibility` seeder with rows for the new satellite fields.

### D.1 — PR #119.8: `CipWorkOrderDetails` (capital projects)
**Fields:**
- `AfeNumber` varchar(32) NOT NULL
- `GlCipSubAccount` varchar(32) — links to ChartOfAccounts (future FK)
- `ApprovedBudget` decimal(18,2)
- `CapitalizedInterest` decimal(18,2)? (ASC 835-20)
- `SubstantialCompletionDate` date?
- `InServiceDate` date?
- `UsefulLifeMonths` int?
- `DepreciationMethod` smallint (enum: StraightLine, DDB, UnitsOfProduction, MACRS)
- `TargetFixedAssetId` int? FK → Asset
- `Stage` smallint enum (Feasibility / FEED / Approved / Design / Procurement / Construction / Commissioning / SubstantialComplete / Closeout)
- `ChangeOrderCount` int (denormalized; updated by trigger or service)
- `RetainagePercent` decimal(5,2)?
- `JvPartnerSplits` jsonb? (array of `{partnerId, sharePercent}`)
- `RegulatoryAuthority` varchar(40)? (e.g. "FERC", "PUC-CA")

**Source:** ASC 360-10, ASC 835-20, AFE practice (Finario / Plant Services).

### D.2 — PR #119.9: `QualityWorkOrderDetails`
**Fields:**
- `NcrNumber` varchar(32) NOT NULL
- `QualityIssueType` smallint (Defect / Deviation / AuditFinding / CustomerComplaint / SupplierIssue / InternalNcr)
- `Severity` smallint (Minor / Major / Critical)
- `Source` smallint (Internal / Customer / Supplier / Audit)
- `AffectedQuantity` decimal(18,4)?
- `AffectedLotNumber` varchar(64)?
- `DispositionCode` smallint (Pending / UseAsIs / Rework / Scrap / Return / SortAndUse)
- `RootCauseMethod` smallint (FiveWhy / Fishbone / FaultTree / EightD / IsIsNot)
- `RootCauseCategory` smallint (Machine / Material / Method / Manpower / Measurement / Environment)
- `CapaRequired` bool
- `CapaWorkOrderId` int? FK → WorkOrder (self-FK to the linked CAPA)
- `LinkedNcrId` int? FK → WorkOrder (inverse of CapaWorkOrderId for the engineering-side view)
- `EffectivenessVerificationDate` date?
- `EffectivenessVerificationStatus` smallint (NotStarted / InProgress / Verified / Failed)
- `RegulatoryReportable` bool
- `D0_PrepNotes` through `D8_Recognition` — 8 nullable text columns for the 8D record (per ASQ / Ford G8D)

**Source:** ISO 9001 Cl. 8.7 + 10.2, FDA 21 CFR 820.90 + 820.100, Ford G8D, IATF 16949.

### D.3 — PR #119.10: `EngineeringWorkOrderDetails`
**Fields:**
- `EcoNumber` varchar(32)
- `EngineeringIssueType` smallint (EngineeringChangeOrder / ManagementOfChange / DesignChange / BomRevision / ProcedureUpdate / Capa)
- `ChangeTypeFFF` smallint (Form / Fit / Function / Documentation / Safety)
- `RiskLevel` smallint (Low / Medium / High / Critical)
- `IsReplacementInKind` bool (RIK skips full MOC review per 1910.119(l)(1))
- `MocPshUpdated` bool (Process Safety Information updated)
- `MocOperatingProceduresUpdated` bool
- `MocTrainingRequired` bool
- `PssrCompleted` bool
- `PssrCompletedAt` timestamptz?
- `LinkedNcrWorkOrderId` int? FK → WorkOrder (Quality NCR that triggered this CAPA)
- `EffectiveDate` date?
- `CutInSerial` varchar(64)?
- `RegulatoryReview` bool
- `AffectedItems` jsonb (array of `{itemType, oldRevision, newRevision, dispositionOfInStock}`)

**Source:** ASME Y14.35, OSHA 29 CFR 1910.119(l) + (i), ISO 9001 Cl. 8.5.6, AS9100 Cl. 8.5.6.

### D.4 — PR #119.11: `HseWorkOrderDetails`
**Fields:**
- `HseIssueType` smallint (SafetyInspection / HazardReport / NearMiss / Incident / Jsa / BehaviorBasedSafety / Audit)
- `OshaCaseNumber` varchar(32)?
- `RecordabilityClass` smallint (NotRecordable / OtherRecordable / RestrictedDuty / DaysAway / Hospitalization / Fatality)
- `HazardSeverity` smallint (Negligible / Minor / Moderate / Serious / Catastrophic) — ANSI Z10
- `Likelihood` smallint (Rare / Unlikely / Possible / Likely / AlmostCertain) — ANSI Z10
- `RiskScore` int (computed: Severity × Likelihood, 1-25)
- `EmployeesAffected` int?
- `BodyPartAffected` varchar(64)?
- `InjuryType` varchar(80)? (per OSHA classification taxonomy)
- `DaysAway` int?
- `DaysRestricted` int?
- `LostTimeIncident` bool
- `OshaItaSubmissionRequired` bool
- `RegulatoryNotifications` jsonb (array of `{agency, formNumber, submittedAt}`)
- `JsaSteps` jsonb (array of `{stepOrder, step, hazard, control, hierarchyOfControls}`)
- `WitnessStatementsUrl` varchar(500)?
- `PhotosUrl` varchar(500)?

**Source:** ISO 45001, OSHA 29 CFR 1904, OSHA 3071 (JSA), OSHA ITA.

---

## Phase E — Sibling Production model

Production gets its own top-level entity because the state machine, event cadence, and KPI surface are too far from maintenance/CIP/quality/HSE/engineering.

### E.1 — PR #119.12: `ProductionOrder` + `ProductionRunCard` + state engine
**Schema:**

```
ProductionOrder
  Id                       int PK
  OrderNumber              varchar(32) UNIQUE (PO-2026-001234)
  AssetId                  int FK → Asset
  CompanyId                int? FK → Company (tenant scope)
  ItemId                   int? FK → Item (future parts master)
  PartNumber               varchar(64)            (denormalized for SAP MATNR-style IDs)
  PartRevision             varchar(16)
  BomRevision              varchar(16)
  RoutingRevision          varchar(16)
  PlannedQuantity          decimal(18,4)
  CompletedQuantity        decimal(18,4) default 0
  ScrapQuantity            decimal(18,4) default 0
  ReworkQuantity           decimal(18,4) default 0
  UnitOfMeasure            varchar(16)
  PlannedStart             timestamptz
  PlannedEnd               timestamptz
  ActualStart              timestamptz?
  ActualEnd                timestamptz?
  Status                   smallint enum (Planned / Released / MaterialStaged / Setup / Run / InProcessInspection / Complete / QaReleased / Closed)
  Priority                 smallint
  ShiftCode                varchar(8)?
  WorkCenterId             int? FK
  SalesOrderRef            varchar(32)?
  ExternalOrderId          varchar(64)?
  ExternalSource           varchar(32)?
  CreatedAt                timestamptz default now()
  RowVersion               bytea (xmin)

ProductionRunCard
  Id                       int PK
  ProductionOrderId        int FK → ProductionOrder
  OperationSequence        int
  OperationName            varchar(80)
  PlannedSetupMinutes      decimal(10,2)
  ActualSetupMinutes       decimal(10,2)?
  PlannedRunMinutes        decimal(10,2)
  ActualRunMinutes         decimal(10,2)?
  OperatorUserId           int? FK
  ConfirmedAt              timestamptz?
  GoodQuantity             decimal(18,4)?
  ScrapQuantity            decimal(18,4)?
  ScrapReasonCode          varchar(32)?
  DowntimeMinutes          decimal(10,2)?
  DowntimeReasonCode       varchar(32)?
```

**State engine:** Same pattern as B.2 but a separate profile + transition table (`ProductionOrderStatusProfile`, `ProductionOrderStatusTransition`).

**FK to Asset:** That's the federation. Asset Detail queries both `WorkOrders WHERE AssetId = X` and `ProductionOrders WHERE AssetId = X` and merges into one timeline.

### E.2 — PR #119.13: `MaterialConsumption` (lot genealogy)
**Schema:**
```
MaterialConsumption
  Id                      int PK
  ProductionOrderId       int FK → ProductionOrder
  ConsumedItemId          int FK → Item
  ConsumedLotNumber       varchar(64)
  Quantity                decimal(18,4)
  UnitOfMeasure           varchar(16)
  ConsumedAt              timestamptz
  ConfirmedBy             int? FK → Users

  INDEX (ProductionOrderId)
  INDEX (ConsumedLotNumber)
```

**Why:** ISA-95 Pt 2 + 21 CFR 820.184 require lot/batch genealogy ("which input lots went into which output lot?"). This table IS the audit trail when regulators ask.

---

## Phase F — Visible UI (the disruption pitch)

### F.1 — PR #119.14: Unified `/WorkOrders` queue page
**Scope:**
- New `Pages/WorkOrders/Index.cshtml` + code-behind that UNIONs `WorkOrders` (all classifications) with `ProductionOrders` mapped to a shared `WorkOrderListItem` record.
- Classification filter chips: All / Maintenance / CIP / Production / Quality / HSE / Engineering — drives a `Classification IN (...)` filter.
- Each row links to the per-type detail page (which doesn't exist yet — links go to placeholder "Coming soon — PR #119.15-19" pages).
- Plant Floor + Asset Detail timelines: same merge query, narrower asset filter.
- Status badge colors per classification (Maintenance=blue, CIP=purple, Production=orange, Quality=red, HSE=amber, Engineering=green).

**This is the FIRST visible win.** Show it to one customer prospect after F.1 and the architecture sells itself.

### F.2-F.6 — Per-classification create/edit screens
Each PR ships ONE create + edit screen for ONE classification, using the `WorkOrderFieldVisibility` config + `WorkOrderStatusProfile` to drive rendering. The renderer is a single Razor component (`<WorkOrderForm Classification="..." />`) that reads the config and renders sections + fields dynamically.

- **F.2 — PR #119.15** — Maintenance (the existing screen rewritten via the new renderer; reference implementation).
- **F.3 — PR #119.16** — CIP / Capital Projects (AFE form, multi-tier approval visualization, capitalization gate).
- **F.4 — PR #119.17** — Production (run card, lot confirmation, scrap reason quick-buttons).
- **F.5 — PR #119.18** — Quality (NCR intake, 8D wizard, disposition workflow).
- **F.6 — PR #119.19** — Engineering ECO/MOC (revision table, PSSR gate, affected items grid). HSE bundled at the tail.

### F.7 — PR #119.20: Plant Floor + Asset Detail timeline merge
Update Plant Floor cards + Asset Detail "Activity" tab to show the unified timeline with per-classification color coding. This is the polish PR.

---

## Cross-cutting verification protocol

After every PR ships through `ship-workflow`, run the E2E checklist:

1. **Build clean** — `dotnet build Abs.FixedAssets.csproj` returns `0 Errors`.
2. **Migration applied** — `psql $DATABASE_URL -c "\d <new_table>"` shows the new schema.
3. **Plant Floor renders** — `/Plant/Floor/1` loads under 200ms median (PR #117.8 budget held).
4. **Asset Detail renders** — open one storyline asset, verify timeline still shows historical data.
5. **No new warnings** — build warning count stays at 23 (the pre-existing CS8602 baseline) — anything new gets fixed before merge.

If ANY of those fail, stop, fix in a follow-up `.1` PR before moving on. Don't chain broken state.

---

## Memory updates after each phase

| Phase | Memory file to write/update |
|---|---|
| A | Update `reference_master_plan.md` with Sprint 3 progress + revised plan link |
| C | New `feedback_table_rename_pattern.md` — the playbook for safe table renames (we'll learn this during PR #119.7) |
| D | New `project_workorder_satellites_shipped.md` — captures classification → satellite mapping for future reference |
| E | New `project_production_model_shipped.md` — the sibling table approach + lot genealogy schema |
| F.1 | New `project_unified_workorder_queue_live.md` — first customer-visible disruption shipped |

---

## Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `MaintenanceEvent` → `WorkOrder` rename breaks 5+ Razor views silently | Medium | High | Diff-review every renamed Razor view BEFORE push. Pre-flight grep + build check. |
| Per-classification status engine breaks the existing approval flow on first deploy | Medium | High | Feature flag (`UseStatusEngine`) defaulted off; flip on after rename settles. |
| Polymorphic `WorkOrderApproval` migration loses legacy `ApprovedBy` data | Low | Medium | Backfill migration writes Stage="Legacy" row for every existing approval; keep legacy columns until F-phase UI ships. |
| Production model gets too big for one sprint | Medium | Medium | E phase is scoped to schema + state engine ONLY. Run-card UI ships in F.4. |
| New namespace-enum collision (per memory feedback) | Medium | Low | Grep every new enum name against `Abs.FixedAssets.Models.*` before defining. |
| Replit Agent's git-reset block stalls Replit pulls | Medium | Low | Use Shell directly via Chrome MCP (already authorized this session). |

---

## Anti-goals (explicit non-scope)

- No SAP PP replacement — we integrate via `ExternalWorkOrderId`/`ExternalSource`, not replace.
- No shop-floor timekeeping (clock-in/out, andon) — separate Sprint 4 ADR.
- No electronic batch records (eBR) — separate ADR when a GxP customer signs.
- No formal Replacement-In-Kind (RIK) approval workflow — flag in schema, full workflow when a real customer needs it.
- No FERC Uniform System of Accounts integration — flagged in CIP satellite, deferred.
- No customer/vendor portal for ECO acknowledgments — deferred.

---

## Rollback strategy

Each PR ships with a `Down()` migration that reverses the change. If a PR breaks Replit:

1. Shell into Replit, `git reset --hard HEAD~1`.
2. Run `dotnet ef database update <PreviousMigrationName>` (or run the `Down()` SQL by hand).
3. Web Server restarts on prior commit.
4. Open a `.1` follow-up PR with the fix.
5. Re-ship.

Total rollback budget per PR: 15 minutes.

---

## Ship cadence target

Following the ship-workflow plugin (edit on Mac → osascript push → gh CLI PR + merge → Replit pull → Agent restart Web Server → live verify) we've been hitting **2-3 PRs per day** when nothing breaks. At 12 substantive PRs in the plan plus the inevitable 3-4 `.1` follow-ups, that's **5-8 working days of focused execution.**

Calendar target: **end of week of 2026-05-25** for Phase F.1 (the first visible win). Phase F.7 (full demo readiness): **mid-June 2026**.

---

## Next actions (immediate, on this commit)

1. Sign off on this plan (Dean: read + confirm or request changes).
2. Phase A.1 — rewrite ADR-012 to v0.2 with the research synthesis + this plan as the migration plan. (~30 min.)
3. Phase A.2 — ship PR #119.1.2 (drop Production from the enum). (~15 min through ship-workflow.)
4. Phase B.1 — start `WorkOrderFieldVisibility` (the highest-leverage piece — everything per-type config flows through it). (~2 hours.)

That sequence puts us at end-of-Phase-A by the end of today and Phase B in motion by tomorrow.
