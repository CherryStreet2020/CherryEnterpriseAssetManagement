# Theme B11 — Resource Model & Finite Scheduler
## Code-Grounded Spec + PR Cascade Design

**Created:** 2026-05-29 session 23 · Claude (engineering lead) for Dean Dunagan
**Source research:** `docs/research/resource-model-dept-wc-machine-dean-research.md` (Dean's SAP/D365/Oracle/MIE-grounded org-structure research).
**Codebase audit:** session 23 — verified WorkCenter, WorkCenterAssetLink, Asset (+ telemetry/OEE/maintenance), MachineSpecification, RoutingOperation, ProductionOperation, OperationReadinessService, WorkCalendar, Employee/Skill/Craft/LaborRate/LaborEntry, BackwardSchedulingService against `main` (`1a8e9e8`).
**Sequencing (Dean, 2026-05-29):** interleave — build **after** B7's crystallization wave, **before** B7's make-or-buy wave, so the make-or-buy F2 capacity factor launches on a *real* finite scheduler instead of the coarse proxy.

---

## §0 — Design Principle (LOCKED)

> **Three objects, three jobs (Dean's rule).** **Department** organizes people, reporting, ownership, cost-center + location defaults. **Work Center** is the routing / scheduling / capacity / costing / material-flow object. **Machine/Resource** is the physical thing that executes — and it is *also* an EAM Asset and a telemetry source. Never make one table do all three.

> **Capability drives scheduling, not hard-coded machines.** A routing operation declares a *required capability*; the scheduler matches it to *eligible resources* (machines/labor/tools/vendors) by capability + availability + cost + health. This is the disruption vs. MIE/Epicor, which pin a machine on the routing.

> **Dual identity, never duplicated.** A machine's *production-resource* identity (scheduling/dispatch/cost) links to its *EAM-asset* identity (PM/downtime/telemetry/calibration) — which **already exists** as `Asset`. We add the resource/scheduling layer and bridge to `Asset`; we do not re-model the asset.

> **GO BIG on architecture, phase the field depth.** Ship the hierarchy + capability + scheduling-critical + cost-rate + dual-identity fields first (P1); layer the long-tail quality/security/telemetry sub-fields after (P2). Don't model master data for weeks before anything schedules.

---

## §1 — What Already Exists (the audit verdict: ~70% there)

| Concept | State | Detail |
|---|---|---|
| **WorkCenter** | ~90% | `Models/Production/WorkCenter.cs` — Type (Machine/Cell/ManualStation/Subcontract), CapacityModel (Single/Multi/Infinite), EfficiencyPct, UtilizationPct, SimultaneousOperationsMax, CalendarId, StandardCostRatePerHour, OverheadRatePerHour, LocationId, **OwningDepartmentId (orphan FK)**. Gaps: no xmin RowVersion, no SiteId, no Department nav, no capability link. |
| **WorkCenterAssetLink** | 100% | The dual-identity bridge — WorkCenterId ↔ AssetId, IsPrimary, EffectiveFrom/To. Already supports N machines per WC + moves over time. |
| **Asset (= the machine/EAM/telemetry half)** | ~95% | `Models/Asset.cs` — identity, status, condition, criticality, parent/child, Manufacturer/Model/Serial, **OEE (StandardRunRate, IdealCycleTime, Target/Current Availability/Performance/Quality/OEE), PredictiveHealthScore, PredictedFailureDate, Calibration (Required/LastDate/NextDue/Status), sensor cache + thresholds**, IoT (DeviceId/Protocol/Endpoint/ConnectionStatus), DepartmentId, SiteId, LocationId, CostCenterId, xmin RowVersion. Plus `AssetSensorReading`/`AssetSensorLatest` (14 telemetry types), `AssetMaintenance`, `WorkOrder`. **The research's "machine is also an EAM asset + telemetry source" is essentially DONE.** |
| **MachineSpecification** | satellite | `Models/MachineSpecification.cs` — CNC spec bag on Asset (control system, axes, travels, spindle, ATC pockets, table size). Spec attributes, not a schedulable record. Useful raw material for the **capability** model. |
| **RoutingOperation / ProductionOperation** | ~90% | WorkCenterId link present; **ProductionOperation already has `AssetId`** (dispatcher picks the specific machine). 5-time decomposition (Setup/Run/Queue/Move/Wait), Predecessor/IsParallel. **`RequiredSkillCodes` + `RequiredToolingIds` are CSV text — not FK-backed (the capability gap).** |
| **OperationReadinessService** | ~85% | `Services/Production/OperationReadinessService.cs` — **8 checks already shipped** (Materials, PriorOp, Resource, Labor[stub], Quality, Documents, Tooling, Maintenance). §5 of the research ("readiness + resource health") is largely live; CheckLaborQualified is a stub awaiting the cert link. |
| **Labor side** | ~90% | `Employee` (PRA-8), `Skill` (+Level/cert), `Craft`, `LaborRate`, `LaborType`, `LaborEntry` (clock-in/out). Not yet FK-linked to operation capability/cert checks. |
| **WorkCalendar** | ~90% | `Models/Masters/WorkCalendar.cs` — TimeZone, WorkDayMask, start/end, Holidays. Shifts implicit (no Shift entity). |
| **Department** | ~50% | Exists in `Models/GlAccount.cs` as a **cost-center entity** (Code/Name/Type[14 values]/ManagerId/CostCenterId, nav to Asset). **NOT linked to WorkCenter; no nesting.** |
| **Scheduler** | ~5% | `BackwardSchedulingService` is an explicit **stub** — 24h wall-clock, sequential, **no calendar, no capacity, no alternate-resource, no load profiling, no bottleneck**. |
| **Capability (machine)** | **0%** | **Absent entirely.** The crown-jewel gap. |
| **Tool/Fixture** | ~20% | CSV text on RoutingOperation + spec fields; no entity. |
| **Shift / Resource(distinct)** | 0–50% | Shift implicit in calendar; "Resource" today *is* the WorkCenter (+Asset link). |

**Bottom line:** the backbone (WC, Asset+telemetry, labor, routing-op, readiness) is strong. The missing coordination layer is **Capability + a real Resource/Machine scheduling record + the finite scheduler + formalized Department**.

---

## §2 — The Gap (the build surface)

1. **Department** → first-class production-org entity, FK from WorkCenter, optional Division tier, ownership/default fields (phased).
2. **WorkCenter hardening** → xmin RowVersion + SiteId; real Department FK/nav; scheduling/dispatch field group (bottleneck, alternates, dispatch rule, setup-family, finite flags); capability link.
3. **Machine/Resource record** → a schedulable *production-resource* identity that **bridges to `Asset`** (extending `WorkCenterAssetLink` or a new `ProductionResource`), plus **Tool/Fixture**, **Labor-pool**, **Vendor**, **Location** as resource types assignable to a WC with effective dates/calendars/finite-capacity/cost.
4. **Capability model (crown jewel)** → `Capability` master + `ResourceCapability` (capabilities on machines/labor/tools/vendors) + `OperationCapabilityRequirement` (FK-backed, replaces the CSV `RequiredSkillCodes`/`RequiredToolingIds`) + a **capability-match resolver** ("what resources can do this op").
5. **Finite scheduler** → replace the stub: calendar-aware, capacity-constrained, load-profiled per resource over a window, capability-based alternate selection, dispatch rules, bottleneck/drum detection. **This produces the real `Load%` that upgrades B7's make-or-buy F2.**
6. **Readiness/health + surfaces** → fill `CheckLaborQualified` via Employee/Skill/cert; add capability + resource-health signals; resource-health-blocks-scheduling rules; the Department / Work Center / Machine tabbed UIs (research §11).

---

## §3 — PR Cascade (~14–16 PRs across 5 waves)

### Wave R1 — Org backbone + WorkCenter hardening (PRs 1-3)

**R1-1: Department as a first-class production-org entity (~7h)**
- Promote/extend Department into the production org backbone (reuse the `GlAccount.cs` Department where sensible; add a production-org table if the cost-center one is too finance-coupled — decide in build). P1 fields: Id/Code/Name/Type, Site, ParentDepartmentId (nesting), Manager/Supervisor/Planner, Default CostCenter/Calendar/Shift, Active. Defer the long tail (KPI config, kiosk/mobile/dashboard visibility, the dozen default-location fields) to R5/P2.
- Wire `WorkCenter.OwningDepartmentId` → real FK + nav (close the orphan).
- Optional `Division` tier between Site and Department (P1 if Dean wants Org→Company→Site→Division→Dept; else skip).
- EF migration, entity config, admin probe (write buttons: create dept, nest, assign WC).

**R1-2: WorkCenter hardening — concurrency + tenant + scheduling group (~7h)**
- Add **xmin RowVersion** (MapXminRowVersion) + **SiteId** to WorkCenter (align with the tenant-trio + concurrency locks).
- Add the scheduling/dispatch P1 field group: BottleneckFlag, ConstraintPriority, FiniteCapacityFlag, AlternateWorkCenters (link table), PrimaryResourceSelectionRule, DispatchRule, SetupFamilyRule, ParallelMachineCount, CrewSizeRequired, SchedulingEnabled.
- EF migration, config, admin probe.

**R1-3: WorkCenter cost + operation-default groups (P1 subset) (~6h)**
- Cost: LaborRateSource, Setup/Run Labor & Machine rates, Burden/Fixed/Variable OH, Quoting vs Actual rate, CostCenter link (much already on WC — fill gaps).
- Operation defaults (P1 subset): default setup/run/queue/move/wait already exist; add default yield/scrap, count-point, backflush, completion behavior. Defer quality/security sub-fields to R5.
- Admin probe.

### Wave R2 — Resource layer + dual identity (PRs 4-6)

**R2-4: ProductionResource record + Asset bridge (~9h)**
- Introduce the schedulable **production-resource** identity. Decide (in build) between extending `WorkCenterAssetLink` vs. a new `ProductionResource` table with `ResourceKind { Machine, Labor, Tool, Vendor, Location, Cell }` + `AssetId?` (bridge to EAM for machines) + WC assignment + EffectiveFrom/To + Calendar + FiniteFlag + cost-rate + capacity.
- Deprecate `Asset.WorkCenterId` loose-text MES fields in favor of the structured link (keep readable, stop writing).
- Migration, config, admin probe (assign machine/labor/tool to WC; primary; effective-date move).

**R2-5: Tool/Fixture + Labor-pool + Vendor resource types (~8h)**
- `Tool`/`Fixture` entity (replaces CSV `RequiredToolingIds`): identity, crib location, calibration link, controlled flag, compatible machines.
- Labor-pool resource = bridge to existing `Employee`/`Craft`/`Skill` (no new HR model — reuse PRA-8).
- Vendor resource = bridge to existing `Vendor` + `SubcontractOperation` (reuse purchasing cascade).
- Migration, config, admin probe.

**R2-6: Resource calendars + finite-capacity attributes (~6h)**
- Per-resource calendar/shift override (reuse `WorkCalendar`); add a lightweight `Shift` only if needed (else WorkDayMask suffices — decide in build).
- Finite-capacity attributes per resource (capacity/hr, batch, min/max job, exclusive-use, efficiency, utilization).
- Admin probe.

### Wave R3 — Capability model (the crown jewel) (PRs 7-9)

**R3-7: Capability master + ResourceCapability (~8h)**
- `Capability` master (e.g. "Laser cut mild steel ≤0.250in", "5-axis mill", "170-ton press brake ≤10ft", "CMM ±0.001", "AWS-certified Al TIG"). Categorized; parameterized (envelope attrs).
- `ResourceCapability` — assign capabilities to machines/labor/tools/vendors, with qualification + expiration (AS9100 special-process qual). Seed from `MachineSpecification` + `Skill`.
- Migration, config, admin probe.

**R3-8: OperationCapabilityRequirement — FK-backed routing requirements (~7h)**
- Replace RoutingOperation `RequiredSkillCodes`/`RequiredToolingIds` CSV with FK-backed `OperationCapabilityRequirement` (op requires capability X at level/envelope Y). Keep CSV readable during migration; backfill.
- Wire ProductionOperation to carry the frozen requirement snapshot.
- Migration, config, admin probe.

**R3-9: Capability-match resolver (~8h)**
- `ICapabilityMatchService.GetEligibleResourcesAsync(operationId)` → ranked eligible resources (machines/labor/tools) that satisfy ALL required capabilities, filtered by availability + qualification currency + WC membership. Returns the "what can run this op" list the research §-"capability-based scheduling" calls for.
- Admin probe with the research's worked example (Laser1/Laser2 vs Brake2 vs WeldCell3+cert).

### Wave R4 — Finite scheduler (PRs 10-12) — **upgrades B7 F2**

**R4-10: Resource load profile + calendar engine (~10h)**
- Calendar-aware working-time engine (consume WorkCalendar + holidays + shifts; floor to working windows).
- `IResourceLoadService.GetProjectedLoadAsync(resourceOrWcId, [from,to])` → committed scheduled hours ÷ available hours over a window = **real Load%** (replaces B7's coarse proxy). Per WC and per resource.
- Bottleneck/drum detection (highest projected load across the routing / plant window).
- Admin probe (load profile for a WC over a window; identify the drum).

**R4-11: Finite backward/forward scheduler (~12h)**
- Replace `BackwardSchedulingService` stub: calendar + finite-capacity aware; respects SimultaneousOperationsMax + competing demand from other PROs; capability-based alternate-resource selection when primary is loaded; dispatch rules.
- Keep the existing `BackwardScheduleAsync` interface signature (the stub was designed for swap-in); add forward + what-if.
- Admin probe + regression vs the stub's stamped dates.

**R4-12: Dispatch board + schedule surface (~8h)**
- Per-WC/resource dispatch list (FIFO/due-date/priority/slack/setup-family), drag-aware reschedule within freeze window, supervisor override.
- Wire into the Cockpit.

### Wave R5 — Readiness/health extension + master-data surfaces (PRs 13-15)

**R5-13: Readiness/health upgrade (~7h)**
- Fill `CheckLaborQualified` via Employee/Skill/cert currency.
- Add capability-readiness (eligible qualified resource exists) + resource-health-blocks-scheduling (machine down / PM overdue / calibration expired / cert expired / lockout → warn or block by severity, per research §"resource health → scheduling").
- Extends the shipped 8-check `OperationReadinessService`.

**R5-14: Department / Work Center / Machine screens (~10h)**
- The tabbed UIs from research §11: Department (Overview/Employees/WorkCenters/KPIs/Calendar/Costing/Docs), Work Center (Overview/Resources/Scheduling/Costing/OpDefaults/Capabilities/MaterialFlow/Quality/Dispatch/EAM-Impact/Analytics), Machine (Overview/ProductionResource/EAM-Asset/Capacity/Costing/Capabilities/Telemetry/Downtime/Maintenance/Tooling/Quality/Analytics).
- Reuse shipped Asset OEE/telemetry/maintenance data for the Machine EAM/Telemetry/Analytics tabs.

**R5-15: P2 field depth + master-plan wording (~6h)**
- Layer the deferred long-tail fields (WC quality/security sub-groups, Dept KPI config + visibility flags, Machine capability-envelope long tail) now that the surfaces exist.
- Update `docs/research/MASTER_PLAN.md` with the research §13 "Departments, Work Centers, Machines & Resource Model Requirement" wording.

---

## §4 — Estimated Scope

| Wave | PRs | Theme | Hours |
|---|---|---|---|
| R1 — Org backbone + WC hardening | 3 | Department entity, WC concurrency/tenant/scheduling/cost groups | ~20h |
| R2 — Resource layer + dual identity | 3 | ProductionResource↔Asset, Tool/Labor/Vendor resources, calendars | ~23h |
| R3 — Capability model | 3 | Capability master, op requirements, match resolver | ~23h |
| R4 — Finite scheduler | 3 | Load profile, finite scheduler, dispatch board | ~30h |
| R5 — Readiness/health + surfaces | 3 | Readiness upgrade, Dept/WC/Machine UIs, P2 fields | ~23h |
| **Total** | **~15** | | **~120h** |

~4–5 sessions. Larger than B7 (~88h) because the finite scheduler (R4) is genuinely new engineering, but smaller than it would be greenfield because the WC/Asset/telemetry/labor/readiness backbone is already shipped.

---

## §5 — Interleave with B7 (Dean's locked sequencing)

```
NOW  → B7 Wave A finish (PR-2 master-optional PO release · PR-3 estimate-as-standard variance)
     → B7 Wave B crystallization (PR-4 entity · PR-5 service · PR-6 cost seeding)   [no scheduler needed]
THEN → B11 Resource Model (Waves R1→R4 at least; R5 surfaces can trail)
     → ⭐ R4 finite scheduler delivers real Load% + bottleneck/drum
THEN → B7 Wave C make-or-buy (PR-7..9) — F2 swaps the coarse proxy for the real finite Load%/drum signal
     → B7 Wave D surfaces + Cherry voice (PR-10..12) — CLOSES B7
     → B11 Wave R5 surfaces (if trailed)
```

The B7 make-or-buy `MakeBuyDecision` already reserves the F2 snapshot fields (`BottleneckWorkCenterCode`, `BottleneckLoadPct`, `RoutedThroughDrum`) precisely so this swap is drop-in — no entity change when R4 lands.

---

## §6 — Key Decisions — ANSWERED (Dean "go" 2026-05-29, Claude locked)

1. **Org tiers → NO Division tier. Site→Dept→WC.** A `ParentDepartmentId` self-FK on Department gives hierarchy/nesting, so a Division-like grouping can emerge as a parent department later WITHOUT a new table. ABS (single-site job shop) needs no Division. Lowest-risk, reversible.
2. **Department source → EXTEND the existing `Department` (GlAccount.cs).** Audit confirms it's NOT finance-coupled (Code/Name/Type[14 incl. Production/Maintenance/Quality/Engineering]/ManagerId/optional CostCenterId/CompanyId/Assets-nav). R1-1 adds production-org fields (ParentDepartmentId, SiteId, SupervisorId, PlannerId, default CalendarId, xmin) + wires `WorkCenter.OwningDepartmentId` → real FK. One Department, no parallel hierarchy.
3. **Resource record → dedicated `ProductionResource` with `ResourceKind`** (R2). Cleaner for Labor/Tool/Vendor that have no Asset; bridges to `Asset` for machines.
4. **Shift → DEFER the table.** `WorkCalendar.WorkDayMask` + hours covers v1; add `Shift` only if R2-6/R4 needs it.
5. **R4 v1 scope → finite backward + load profile + alternate-resource (the B7 F2 must-have).** Forward/what-if/drag-dispatch trail into R4-12/R5. Accepted.

> Original open questions preserved in git history. R1-1 (Department extension) is the first build.

### §6b — Org-structure facts + Site=Location ruling (Dean, 2026-05-29)

Real tenant structures (corrects the earlier wrong "ABS is single-site" assumption):
- **ABS** = 1 holding company · 1 operating company · **6 sites**.
- **EVS** = 1 holding company · **4 operating companies** · 4 sites.

Both map onto the EXISTING model with NO new tiers: `Company.CompanyType {Holding,Operating,Division}` + `Company.ParentCompanyId` (holding→operating nesting) under a `Tenant`, with `Site.CompanyId` → operating company. Chain: `Tenant → Company(Holding) → Company(Operating) → Site → Department → WorkCenter → Machine/Resource`. This is why decision #1 (no extra Division tier in the resource model) still holds — Holding/Operating/Division already live on `Company`.

**RULING: `Site` and `Location` are the SAME real-world thing (one physical plant).** `Site` is the **canonical** plant tier. `Location` (GlAccount.cs) is legacy and will be collapsed into Site in a dedicated cleanup PR. R1-2 wires `Department.SiteId → Site` + `WorkCenter.SiteId → Site` (canonical) but LEAVES `WorkCenter.LocationId` in place (load-bearing: UNIQUE (CompanyId,Code) + MES ProductionOperation.LocationIdSnapshot everywhere) — deprecate-in-place, do not rip out. The R4 scheduler scopes by Site.

---

## §7 — BIC Differentiators (what beats SAP/D365/Oracle/MIE/Epicor)

1. **Capability-based scheduling out of the box** — eligible-resource matching, not hard-coded machines on routings (Epicor/MIE force the latter).
2. **Resource health gates production** — the scheduler reads the *already-shipped* Asset predictive-health / calibration / PM / telemetry signals and warns/blocks. ERP+EAM+APS fused on one record is the move no incumbent ships cleanly.
3. **Dual identity done right** — production-resource ↔ EAM-asset linked, never duplicated; a rented/bench/virtual resource can schedule without being a fixed asset, and a fixed asset can be maintained without being schedulable.
4. **Operation readiness across all six dimensions** (material/machine/labor/tooling/quality/maintenance) — extends the shipped 8-check service; "don't say short, say WHY."
5. **Feeds the explainable make-or-buy** (B7) — the finite Load%/drum signal turns "Cherry, why are we buying this bracket?" into a real throughput argument.

---

*Spec + cascade complete. On Dean's go (and the §6 answers), R1-1 (Department entity) starts after B7's crystallization wave per the locked interleave.*
