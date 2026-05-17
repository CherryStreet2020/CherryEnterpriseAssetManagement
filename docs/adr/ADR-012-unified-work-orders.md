# ADR-012: Unified Work Orders (MES Expansion)

**Status:** Accepted (v0.2 — supersedes v0.1)
**Date:** 2026-05-17
**Deciders:** Dean Dunagan, Claude
**Categories:** Architecture | Data | Integration | UX

---

## Status history

| Version | Date | Result |
|---|---|---|
| v0.1 | 2026-05-17 (morning) | Accepted — "Category enum + satellites" pattern with all six work types unified under one MaintenanceEvent table. Five open questions resolved in favor of recommended option. |
| **v0.2** | **2026-05-17 (afternoon)** | **Revised after Dean pushed back: "A maintenance WO and a CIP WO are drastically different from a production WO… How can we keep a unified system but have a different WO format for each category?" Four-stream platform research (Maximo / SAP / Infor / Oracle / Dynamics / ISA-95 / B2MML / OPC UA 10031-4) showed the dominant industry pattern is unified-data + per-type-UX, with Production as a federated sibling rather than a subtype. v0.2 adopts that pattern, adds four configuration backbone tables, and renames `MaintenanceEvent` → `WorkOrder` now.** |

**v0.2 sign-off (2026-05-17):** Dean accepted after the research presentation; "stay disciplined, remember Best In Class — this is going to be incredible."

---

## Context

### What we're solving

Today CherryAI EAM ships a single `MaintenanceEvent` table tuned for eight maintenance flavors (PM, Corrective, Predictive, Emergency, Inspection, Calibration, Upgrade, Other). It is competitive against Maximo / SAP PM / Infor EAM at maintenance-only work orders.

But every plant operator's day is full of work that touches an asset and isn't maintenance:

- **Production WO** moving down a CNC line — operators need to know if the machine is on a PM hold before queuing the next part.
- **Quality non-conformance** (NCR) flagged at an inspection station — engineering needs to link it back to the CNC + the corrective WO that fixed the bearing.
- **Engineering change order** (ECO) modifying the spindle assembly — maintenance needs to know the calibration baseline has shifted.
- **Capital project** (CIP) replacing the spindle motor as a depreciable asset — accounting needs an AFE-tracked cost rollup that reclassifies to the fixed-asset ledger on substantial completion.
- **Safety inspection** finding a guard interlock failing — HSE needs to open a corrective on the same asset and record it to the OSHA 300 log.

In every competing platform (Maximo, SAP PM, Infor EAM, AVEVA APM) these live in different modules with different work queues. The plant manager has to flip between four screens to see what's happening on a single machine.

**This is the disruption opportunity.**

### v0.1 was right about the goal, wrong about the depth

The v0.1 ADR proposed a `WorkOrderCategory` discriminator + per-category satellites on a single `MaintenanceEvent` table. The instinct was correct: unify the data so the Plant Floor view sees ALL work touching an asset in one timeline.

The execution was thin. Specifically v0.1:
- Hand-waved "satellite tables" without listing the actual fields each non-maintenance category needs. Dean caught it: "I don't see a bunch of fields we'll need in the work order like revision, etc."
- Glossed over the fact that each work type has a fundamentally different **state machine** (CIP has a *substantial-completion* event that triggers accounting reclassification; MOC has a *PSSR gate* that blocks startup; Production has a *QA-release* gate; Quality has an *effectiveness-verification* gate).
- Treated approval as a single `ApprovedBy` field when CIP needs threshold-tiered multi-level approval, ECO/MOC needs a Change Control Board + PSSR sign-off, and Maintenance is single-stage.
- Lumped Production into the same table even though Production's event cadence (every operation confirmation, every lot move, every scrap reason) is 100-1000× higher than Maintenance and its KPI surface (OEE: Availability × Performance × Quality) is alien to Maintenance's KPIs (MTBF, MTTR).
- Deferred the `MaintenanceEvent` → `WorkOrder` rename, which guaranteed the table name would lie forever.

### What the four-stream research found

I researched seven platforms + four standards bodies. The pattern is unanimous:

| Platform / Standard | Pattern | Per-type UX mechanism |
|---|---|---|
| **IBM Maximo** | Single `WORKORDER` table + `WOCLASS` discriminator + `WORKTYPE` for CIP (`CAP`) | 4 Applications + Application Designer + conditional UI rules |
| **SAP PM/PP/PS** | Single `AUFK` header + module satellites (`AFIH` maintenance, `AFKO` production) | Dedicated transactions (IW31/CO01/CJ20N) + **`OIAN` field-selection table** per order type |
| **Infor EAM** | Single WO header + UDF columns + custom tabs | Screen Designer renders different layouts per screen context |
| **Oracle eAM** | Shared `WIP_DISCRETE_JOBS` + `EAM_WORK_ORDER_DETAILS` satellite + `entity_type` discriminator | Per-type Forms / OAF pages, DFFs context-switched |
| **Dynamics 365** | One `msdyn_workorder` entity for service, separate `ProdTable` for Production, separate Project tasks | Multiple main forms + per-role form assignment + business rules |
| **ISA-95 Part 4** | One Operations Definition / Schedule / Performance triad reused across Production, Maintenance, Quality, Inventory | `OperationsType` enum + type-specific payload extensions |
| **OPC UA 10031-4** | Single generic `JobOrder` / `JobResponse` ObjectType | All types share one model |
| **B2MML V0700** | Generic `Operations*.xsd` with `OperationsType` enum (modernized FROM separate-per-domain) | One schema, one enum, one set of consumers |

**Seven of eight converged on unified-data + per-type-UX.** The one legitimate split: **Production**. Even the unifiers (Maximo, SAP) keep production operations in their own satellite (`AFKO` for SAP); Microsoft and Infor went further and made Production a separate entity entirely.

### Per-type field deltas the research surfaced

`MaintenanceEvent` is missing at least these polymorphic fields, with regulatory citation:

| Gap | Affects | Source |
|---|---|---|
| `Revision` / `BomRevision` / `RoutingRevision` | Production, ECO | ISA-95 Pt 2; ASME Y14.35 |
| `AfeNumber`, `CipSubAccount`, `CapitalizedInterest`, `InServiceDate`, `UsefulLife`, `DepreciationMethod`, `TargetFixedAssetId` | CIP | ASC 360-10, ASC 835-20 |
| `LotNumber`, `BatchNumber`, `SerialNumber`, `MaterialGenealogy[]`, `UDI` | Production | ISA-95 Pt 2; 21 CFR 820.184 |
| `FailureMode`, `FailureMechanism`, `FailureCause`, `DetectionMethod` | Maintenance | ISO 14224 |
| `DispositionCode` (Use-As-Is / Rework / Scrap / Return / Repair), `8DFields[D0..D8]`, `RootCauseMethod`, `EffectivenessVerification` | Quality | ISO 9001; Ford G8D |
| `OshaCaseNumber`, `RecordabilityClass`, `BodyPart`, `EventDescription`, `OshaReportableFlag`, `DaysAway`, `DaysRestricted` | HSE | 29 CFR 1904.29 |
| `JsaSteps[]` (Step / Hazard / Control), `HierarchyOfControls` | HSE | OSHA 3071 |
| `MocPshUpdated`, `MocOperatingProceduresUpdated`, `MocTrainingRequired`, `PssrCompleted`, `PssrSignoffs[]`, `ReplacementInKindFlag` | ECO/MOC | 29 CFR 1910.119(l) + (i) |
| `AffectedItems[]` (old-rev → new-rev pairs), `ChangeTypeFFF` (Form / Fit / Function) | ECO | ASME Y14.35; PLM convention |
| Polymorphic approval chain | All | All standards |

The full field-by-field research is in [docs/SPRINT3_PLAN_v0.2.md](../SPRINT3_PLAN_v0.2.md) Phase D scope.

---

## Decision (v0.2)

**Adopt the unified-data + per-type-UX pattern, with Production as a federated sibling table.**

### Six structural commitments

**1. One unified `WorkOrder` table** for the five classifications whose data shapes meaningfully overlap:

```csharp
public enum WorkOrderClassification
{
    Maintenance  = 0,   // PM, Corrective, Predictive, Emergency, Inspection, Calibration, Upgrade
    // (gap, formerly Production — Production is now a sibling table, not a subtype)
    Quality      = 2,   // NCR, deviation, CAPA, 8D, audit finding, customer complaint
    Engineering  = 3,   // ECO, MOC, design change, BOM revision, procedure update
    HSE          = 4,   // Safety inspection, hazard report, near-miss, incident, JSA
    CIP          = 5,   // Capital project work — AFE-tracked, capitalization-bound
}
```

The enum gap at 1 is intentional: no row was ever written with Classification=1 (`Production` was in the v0.1 enum for 4 hours but no migration ever applied it to data). Keeping the gap avoids analytics churn if anyone ever inspects historical enum values.

**2. One sibling `ProductionOrder` table** for shop-floor production runs. Same Asset FK; entirely separate state machine, event cadence, and KPI surface. The unified `/WorkOrders` queue page UNIONs `WorkOrder` + `ProductionOrder` so users still see one timeline per asset — but the underlying models don't compromise each other.

**Why Production gets sibling status:**
- Event cadence: every operation confirmation, every lot move, every scrap reason. 100-1000× higher than maintenance.
- KPI surface: ISO 22400-2 OEE = Availability × Performance × Quality. Alien to maintenance's MTBF/MTTR.
- State machine: Plan → Released → Material Staged → Setup → Run → In-Process Inspection → Move/Complete → QA Released → Closed. None of those states map to anything maintenance does.
- Lot/batch/serial traceability and material consumption are the audit trail (per ISA-95 Pt 2 + 21 CFR 820.184), not the work history.
- Pattern precedent: Maximo + SAP keep production in a dedicated satellite even on a unified header; Infor + Dynamics keep Production in a separate entity entirely.

**3. Four config tables drive per-classification behavior** — no code branching per type in the renderer. This is the architectural payoff.

- **`WorkOrderFieldVisibility`** (SAP `OIAN` pattern). One row per `(Classification × FieldName)` with `Visibility ∈ {Hidden, Optional, Required, ReadOnly}`, `DisplayLabel`, `DisplayOrder`, `SectionName`, optional `TenantId` for per-tenant overrides. The Razor renderer reads this config and emits the right form for the right type.
- **`WorkOrderStatusProfile`** + **`WorkOrderStatusTransition`** (Maximo `WOSTATUS` pattern). One status column on the header, but the allowed transitions are per-classification: CIP can transition to `SubstantialComplete` (triggers accounting reclassification); Engineering MOC has `PssrRequired` (blocks Close until cross-functional signoffs are captured); Quality has `EffectivenessVerification`. A central `IWorkOrderStatusEngine.CanTransition(wo, newStatus)` is the gate.
- **`WorkOrderApproval`** (polymorphic). 1:N from header. Replaces the single `ApprovedBy/ApprovedAt` columns with `(WorkOrderId, Stage, StageOrder, RoleRequired, ApproverUserId, Decision, DecisionAt, Comments)`. Maintenance gets one row. CIP gets four rows (PM → Engineering Director → CFO → Board, threshold-driven). ECO/MOC gets CCB + PSSR rows. Effectiveness verification for Quality is just another stage.
- **`NumberSequence`** (SAP `NRIV` pattern). One row per `(Classification, Year, TenantId)` with `Prefix` + `CurrentValue`. The `INumberSequenceService.Next(classification, tenantId)` returns the next number atomically. Drives `PM-2026-1234`, `AFE-2026-005`, `NCR-2026-0042`, `ECO-2026-12-001`, `INC-2026-003`.

**4. Per-classification satellite tables** for the non-maintenance work types:
- `CipWorkOrderDetails` — capital project + accounting fields
- `QualityWorkOrderDetails` — NCR / CAPA / 8D / disposition
- `EngineeringWorkOrderDetails` — revision tracking, ECO/MOC, PSSR
- `HseWorkOrderDetails` — OSHA 300/301, JSA, incident classification

Maintenance keeps using the header fields (no satellite needed; the header was originally built for maintenance).

**5. `Revision` is a first-class header field.** Re-issued WOs increment revision; original + each revision share a `MasterWorkOrderId` self-FK (NULL = "I am the master"). Critical for CIP, ECO, and any re-issued maintenance WO.

**6. Rename `MaintenanceEvent` → `WorkOrder` now.** PR #119.7. Risky but worth doing once instead of forever pretending the table is named "MaintenanceEvent." Done via `ALTER TABLE RENAME` so PostgreSQL keeps the underlying OID and statistics — no index rebuild needed.

### What this delivers

- **One Plant Floor + one Asset Detail timeline** showing every kind of work touching an asset, color-coded by classification. The disruption pitch versus Maximo/SAP/Infor.
- **Per-type forms that match the methodology** — a CIP form doesn't pretend to be a maintenance form, a Quality NCR form doesn't pretend to be a CIP form. Each looks like the regulator-blessed form the operator already knows.
- **Per-type state machines** — accounting events fire on CIP `SubstantialComplete`; OSHA-300 entries fire on HSE `Recordable`; effectiveness-verification gates fire on Quality close; PSSR gates block Engineering close.
- **Single integration surface for ERP** — `ExternalWorkOrderId` + `ExternalSource` work the same way regardless of classification. SAP PP feeds Production; SAP PM feeds Maintenance; both flow through the same shape.
- **Config-driven customization** — `WorkOrderFieldVisibility` is the entry point for any customer-specific field hiding/requiring. No code change to onboard a new industry vertical.

---

## What's intentionally NOT in v0.2 scope

Each of these is a separate ADR or a Sprint 4+ topic:

- **Shop-floor data collection** (operator clock-in/out, andon, takt-time tracking). Separate ADR.
- **Electronic batch records (eBR)** for GxP customers. Separate ADR when a regulated customer signs.
- **FERC Uniform System of Accounts** mapping. CIP satellite flags `RegulatoryAuthority`; full mapping deferred.
- **Customer portal for ECO acknowledgments.** Deferred.
- **Production scheduling math** (sequencing, finite scheduling, capacity planning). That's ADR-013 (Unified Scheduling).
- **Replacing SAP PP / Oracle MES.** We integrate via `ExternalWorkOrderId`; we don't replace.
- **Replacement-In-Kind (RIK) workflow.** Flag in schema (`IsReplacementInKind`), full workflow when a real PSM customer needs it.
- **Tenant `RegulatedIndustry` flag** turning on regulatory-reportable UI hints. Deferred to Sprint 5.

---

## Alternatives Considered

### Alternative A: WorkOrderClassification + satellites (v0.1 — REJECTED in v0.2 for Production)

v0.1's recommendation. Put all six work types on `MaintenanceEvent`. Defer the rename. Reject because:
- Production's state machine + event cadence don't fit a maintenance-shaped table.
- Maximo/SAP both keep production in a dedicated satellite even when unified; v0.1 missed that nuance.
- The field-by-field research showed `MaintenanceEvent` would balloon to ~150 columns if it absorbed all six types' mandatory fields directly.
- Deferring the rename means the table name lies forever.

v0.2 keeps the unified backbone for five of the six types and lifts Production to a sibling.

### Alternative B: TPH (Table-Per-Hierarchy) inheritance — REJECTED

Use EF Core 9 TPH: `WorkOrder` base, `MaintenanceWorkOrder` / `ProductionWorkOrder` / etc. subclasses with a discriminator column.
- **Pro:** Cleanest type system; LINQ filters by subtype (`db.WorkOrders.OfType<ProductionWorkOrder>()`).
- **Con:** Massive refactor; every existing query, every Razor page, every report breaks. The TPH table grows wide (sum of all subtype columns) — eventually hits Postgres's effective row-size budget. Doesn't solve the per-type-state-machine problem.
- **Why rejected:** 10+ PRs of churn before any new functionality lands.

### Alternative C: New `WorkOrder` top-level + 1:1 FK from MaintenanceEvent — REJECTED

Create a top-level `WorkOrder` table with shared header fields. `MaintenanceEvent`, `ProductionOrder`, `QualityNcr`, etc. each get a FK to `WorkOrder`.
- **Pro:** Cleanest separation; each category owns its own table.
- **Con:** Every read of any WO becomes a two-table join. Five tables to maintain. Cross-category queries (the "single pane of glass" promise) require UNION ALL across five tables WITH joins to the shared header. Plant Floor hot read path doubles.
- **Why rejected:** Defeats the unification thesis — we'd actually have six fragmented tables, the same problem Maximo solved 15 years ago.

### Alternative D: Stay maintenance-only; six separate top-level entities — REJECTED

Build `ProductionOrder`, `QualityNcr`, `EngineeringChangeOrder`, `SafetyInspection`, `CapitalProject` as their own top-level models, unrelated.
- **Pro:** Each module evolves independently.
- **Con:** Five separate work queues per asset. Same problem Maximo, SAP, Oracle, Infor all spent 15+ years escaping.
- **Why rejected:** Defeats the disruptive thesis.

### Alternative E: Unified backbone for 5 + sibling Production *(CHOSEN — v0.2)*

What v0.2 adopts. Combines:
- The unification benefit (one timeline per asset, one Plant Floor, one /WorkOrders queue) for the five classifications whose data shapes overlap meaningfully.
- The honesty that Production is genuinely different and shouldn't be forced into a maintenance-shaped table.
- The config-driven per-type UX so we don't end up with thousands of lines of `switch(classification)` code in the renderer.

**Why chosen:** Matches the dominant pattern across Maximo, SAP, Oracle, Infor, Dynamics + ISA-95 standard. Delivers the disruption pitch without compromising data integrity for any single classification. Future-proof against the satellites we'll need to add (timekeeping, eBR, FERC integration).

---

## Consequences

### Positive

- **Single unified work queue** — Plant Floor + Asset Detail + /WorkOrders queue show ALL work touching an asset (PM + production run + open NCR + ECO + CIP + safety inspection) in one timeline, color-coded by classification. The marketed differentiator vs. Maximo / SAP PM / Infor EAM.
- **Per-type forms that match real-world methodology** — operators see the form they already know (8D for Quality, JSA for HSE, AFE for CIP, PSSR for Engineering MOC).
- **Per-type state machines fire the right side effects** — CIP `SubstantialComplete` triggers fixed-asset reclassification + depreciation start; HSE `Recordable` writes the OSHA 300 entry; Quality `EffectivenessVerified` closes the CAPA loop.
- **Config-driven customization** — onboarding a new industry vertical (pharma, oil & gas, food/bev) means seeding `WorkOrderFieldVisibility` rows, not writing code.
- **Polymorphic approval matches reality** — maintenance single-stage, CIP multi-tier with thresholds + partner approval, ECO/MOC CCB + PSSR. One audit trail.
- **ISA-95 Part 4 + OPC UA 10031-4 alignment** — work-record vocabulary lets us serialize/deserialize against B2MML and OAGIS BODs without schema churn.
- **ISO 22400-2 KPIs computable** — scrap %, yield, OEE all derive from `ProductionOrder` + `ProductionRunCard` + `MaterialConsumption`. Maintenance KPIs (MTBF, MTTR, MTTA) all derive from `WorkOrder`.
- **CAPA closed loop** — Quality WO with `CapaRequired=true` links via `CapaWorkOrderId`/`LinkedNcrWorkOrderId` to an Engineering WO. Auditors love this for ISO 9001 / FDA 21 CFR 820.100 / IATF 16949 sign-off.

### Negative

- **The rename is the riskiest single PR in the plan.** `MaintenanceEvent` → `WorkOrder` touches ~150 files. Mitigation: pre-flight grep + local build, raw-SQL `ALTER TABLE RENAME` (sub-second on existing data), keep `db.MaintenanceEvents` as a compatibility extension for one PR cycle. Rollback budget: 15 minutes.
- **Sprint 3 + 4 will ship 12-16 PRs of substantive work.** The schema + config tables ship in Phase B-E (no UI yet); the visible disruption ships in Phase F. Trade-off is deliberate — we build the right foundation once.
- **Per-classification status engine adds runtime complexity.** Mitigation: feature flag (`UseStatusEngine`) defaulted off until Phase C rename settles.
- **More tables to maintain.** Four config tables + four satellite tables + two production tables = 10 new tables. Each has its own EF entity, AppDbContext registration, seeder, migration, and (for the satellites) Razor partial. Worth it for the architecture payoff, but it's not free.

### Neutral

- The `WorkOrderApprovalStatus` enum stays valid (it was always a per-WO status, not the approval chain itself). The new `WorkOrderApproval` polymorphic table is additive.
- The `Operations` collection (WorkOrderOperation) becomes useful across all classifications — production has routing steps, quality has investigation steps, HSE has corrective actions. Same shape.
- `MaintenanceType` enum stays valid as the **sub-type within `Classification=Maintenance`**.

---

## Implementation Plan

**Full plan is in [docs/SPRINT3_PLAN_v0.2.md](../SPRINT3_PLAN_v0.2.md).** The plan is the executable source-of-truth document; this ADR is the architecture justification.

### Phase summary

| Phase | What | PRs | Visible to user? |
|---|---|---|---|
| **A** | ADR lock + enum cleanup | A.1 (this rewrite) + #119.1.2 | No |
| **B** | Configuration backbone — `WorkOrderFieldVisibility`, `WorkOrderStatusProfile`, polymorphic `WorkOrderApproval`, `NumberSequence` | #119.2 → #119.5 | No |
| **C** | Header `Revision` + rename `MaintenanceEvent` → `WorkOrder` | #119.6 → #119.7 | No (internal) |
| **D** | Per-classification satellites — CIP / Quality / Engineering / HSE | #119.8 → #119.11 | No |
| **E** | Sibling `ProductionOrder` + `ProductionRunCard` + `MaterialConsumption` | #119.12 → #119.13 | No |
| **F** | Unified `/WorkOrders` queue + per-type create/edit screens + Plant Floor timeline merge | #119.14 → #119.20 | **YES — first visible disruption at F.1** |

### Calendar target

- **End of Phase A:** today (2026-05-17).
- **End of Phase F.1 (first visible win):** week of 2026-05-25.
- **End of Phase F.7 (full demo readiness):** mid-June 2026.

Cadence assumption: 2-3 PRs/day through ship-workflow plugin. ~14-20 working days total.

---

## Open questions for Dean's sign-off

**All v0.1 open questions remain resolved (6 categories, severity as separate axis, two named FKs for CAPA, free-text ExternalSource, defer regulated-industry flag).** v0.2 introduces zero new open questions — every decision was either grounded in the research or follows directly from the unified-data + sibling-Production decision.

If anything in v0.2 needs to change, the place to push back is this ADR before Phase B starts.

---

## Related Documents

- [SPRINT3_PLAN_v0.2.md](../SPRINT3_PLAN_v0.2.md) — the executable plan (12 PRs, phase-by-phase, with risk register)
- [ADR-001: PMSchedule Canonical Model](./ADR-001-PMSchedule-Canonical-Model.md)
- [ADR-002: DemoPackV2 Canonical Seed](./ADR-002-DemoPackV2-Canonical-Seed.md)
- [ADR-007: Unified Tab System](./ADR-007-Unified-Tab-System.md)
- [ADR-011: Industrial Sensor Data Architecture](./ADR-011-industrial-sensor-data-architecture.md)
- ADR-013: Unified Scheduling (forthcoming — production-vs-maintenance scheduling math)

## Standards Referenced

- **ISA-95 / IEC 62264 Part 4:2015** — Object Models and Attributes for Manufacturing Operations Management Integration. Work-record supertype vocabulary.
- **ISA-95 Part 2** — Material Lot, Material Definition, Equipment, Personnel object models (production lot genealogy).
- **ISO 22400-2:2014** — KPIs for manufacturing operations management. OEE definition.
- **ISO 14224** — Reliability + Maintenance data taxonomy. Failure-mode + failure-mechanism + failure-cause fields.
- **ISO 9001:2015** — Quality management. NCR + CAPA closed-loop.
- **ISO 13485 + 21 CFR 820** — Medical device QMS. Device History Record (820.184), CAPA (820.100), nonconforming product (820.90).
- **IATF 16949** — Automotive QMS. 8D problem-solving required.
- **Ford G8D** — Eight Disciplines specification (D0-D8).
- **ISO 45001** — OH&S management system. Incident + corrective action.
- **OSHA 29 CFR 1904** — Recordkeeping. 300 log + 301 incident report + 300A summary.
- **OSHA 3071** — Job Hazard Analysis worksheet.
- **OSHA 29 CFR 1910.119(l) + (i)** — PSM Management of Change + Pre-Startup Safety Review.
- **ASME Y14.35** — Engineering drawing revision conventions (A, B, C... skip I/O/Q/S/X/Z).
- **ASC 360-10** — PP&E recognition.
- **ASC 835-20** — Interest capitalization on capital projects.
- **IFRS IAS 16** — International equivalent for PP&E.
- **B2MML V0700** — OAGI XML serialization for ISA-95.
- **OPC UA 10031-4** — Job Control companion specification.
- **FDA QMSR (effective 2026-02-02)** — Quality Management System Regulation; supersedes 21 CFR 820 with ISO 13485 incorporation.

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-05-17 (am) | Claude | Initial v0.1 — category + satellites; all 5 open questions resolved |
| 2026-05-17 (am) | PR #119.1.1 | Renamed `WorkOrderCategory` → `WorkOrderClassification` (namespace collision) |
| 2026-05-17 (pm) | Claude | **v0.2 rewrite** — sibling Production model + 4 config tables + Revision field + rename now. Synthesizes 4-stream platform/standards research. Supersedes v0.1. |
