# ADR-012: Unified Work Orders (MES Expansion)

**Status:** Accepted (v0.1)
**Date:** 2026-05-17
**Deciders:** Dean Dunagan, Claude
**Categories:** Architecture | Data | Integration

**Sign-off (2026-05-17):** Dean accepted the v0.1 recommendation with all 5 open questions resolved in favor of the recommended option.

**Naming note (PR #119.1.1):** The top-level discriminator enum is called `WorkOrderClassification` (not `WorkOrderCategory` as originally proposed) because a legacy `WorkOrderCategory` enum already exists on the `WorkOrderType` lookup table — a different concept entirely (it categorizes maintenance-flavor master-data rows). The semantic meaning of the ADR is unchanged; only the C# identifier moved. References below use "Classification" throughout.

**Sign-off detail:**
1. 6 categories (Maintenance / Production / Quality / Engineering / HSE / Project) — **accepted**.
2. Severity (Quality / HSE) is a separate axis from WO priority — **accepted**.
3. CAPA closed loop uses two named FKs (`CapaWorkOrderId` ↔ `LinkedNcrId`) — **accepted**.
4. `ExternalSource` is free text in PR #119.1; lookup migration deferred to PR #119.4 — **accepted**.
5. Tenant `RegulatedIndustry` flag deferred to Sprint 5 — **accepted**.

---

## Context

### The "single pane of glass for all work" thesis

Today CherryAI EAM ships a single `MaintenanceEvent` table that models eight kinds of maintenance work (PM, Corrective, Predictive, Emergency, Inspection, Calibration, Upgrade, Other). It is competitive with Maximo, IBM Maximo Asset Health Insights, and SAP PM at maintenance-only work orders.

But every plant operator's day is full of work that touches an asset and ISN'T maintenance:

- A **production work order** moving down a CNC line — operators need to know if the machine is on a PM hold before they queue the next part
- A **quality non-conformance** (NCR) flagged at an inspection station — engineering needs to link it back to the CNC + the corrective WO that fixed the bearing
- An **engineering change order** (ECO) modifying the spindle assembly — maintenance needs to know the calibration baseline has shifted
- A **safety inspection** finding a guard interlock failing — HSE needs to open a corrective on the same asset

In every competing platform (Maximo, SAP PM, Infor EAM, AVEVA APM) these live in **different modules with different work queues**. The plant manager has to flip between four screens to see what's happening on a single machine. **This is the disruption opportunity.**

### What Dean asked for (2026-05-16 brainstorm)

> "We'll need to expand on the work order functionality if we are going full blown MES as well. We are currently wired for maintenance and CIP work orders only. Whether we work as a standalone or integrate with work orders from an erp system, we'll need to beef it up right?"

> "We already have Maintenance Scheduling, we'll need to expand the scheduling as well too. To handle production work orders? ERP scheduling systems suck."

The goal is **one unified `WorkOrder` shape** that the Plant Floor, Work Queue, Asset Detail, and Schedule views all consume — so a CNC operator, a quality engineer, a maintenance planner, and a plant manager all see the same work touching the same asset, filtered by their lens but living in one table.

### Standards we're aligning to

| Standard | What it gives us |
|---|---|
| **ISA-95 / IEC 62264 Part 4** | "Work Record" object vocabulary — the supertype of Production Performance, Maintenance Performance, Quality Performance, Inventory Performance. Defines shared fields (ID, scope, start/end, personnel, equipment, material, status). |
| **ISO 22400-2** | KPI catalog (OEE, MTTR, MTBF, scrap %, yield, throughput) — these are computed FROM the unified work record stream, so our schema needs to surface the inputs. |
| **B2MML / OAGIS BOD** | XML/JSON serialization conventions for cross-system work-record exchange. Useful when we integrate with SAP PP, Oracle MES, Dynamics F&O. |
| **FDA 21 CFR Part 11** | Electronic records audit trail. Already covered by AuditService + xmin RowVersion — extends cleanly to new categories. |
| **GAMP 5** | Computerised system validation; relevant only when a customer is GxP (regulated pharma/food). |

### Current state recap

`Models/AssetMaintenance.cs` has a robust 100-field MaintenanceEvent with:

- 8-value `MaintenanceType` enum (all maintenance flavors)
- 6-value `MaintenanceStatus` (Scheduled / InProgress / Completed / Cancelled / Overdue / OnHold)
- 4-value `MaintenancePriority` (Low / Medium / High / Critical)
- Cost breakdown (Labor, Parts, Materials, OutsideVendor)
- Approval flow (WorkOrderApprovalStatus + ApprovedBy / ApprovedAt / RequestedBy / RequestedAt)
- PM occurrence FK + CIP project FK
- Failure code (FK + free text)
- Root cause / corrective action / lessons learned
- `Operations` collection (multi-step work breakdown)
- xmin RowVersion (optimistic concurrency)

Lookup-value FKs exist for Type/Status/Priority for the tenant-customizable values dropdown (PR series 90s-100s).

---

## Decision

**Extend `MaintenanceEvent` with a `WorkOrderClassification` enum + per-category satellite tables. Defer the table rename to a future sprint.**

### What we add

**1. WorkOrderClassification enum**

```csharp
public enum WorkOrderClassification
{
    Maintenance  = 0, // PM, Corrective, Predictive, Emergency, Inspection, Calibration, Upgrade
    Production   = 1, // Work order from MES/MRP — make N units of part XYZ on this asset
    Quality      = 2, // NCR, deviation, CAPA, audit finding
    Engineering  = 3, // ECO, MOC, design change, BOM revision
    HSE          = 4, // Safety inspection, JSA, hazard report, near-miss investigation
    Project      = 5, // Project task that touches an asset (currently overloaded into CIP)
}
```

The existing `MaintenanceType` enum stays — it becomes the **sub-type** within `Category=Maintenance`. For `Category=Production`, `MaintenanceType` is ignored (default to `Other`).

**2. Two new always-on fields on MaintenanceEvent**

```csharp
public WorkOrderClassification Category { get; set; } = WorkOrderClassification.Maintenance;

// External-system linkage — set when this WO was created from / synced to
// an ERP work order, MES production order, QMS NCR, etc. NULL for native
// Cherry-created WOs.
[StringLength(64)]
public string? ExternalWorkOrderId { get; set; }

[StringLength(32)]
public string? ExternalSource { get; set; }  // "SAP-PP" | "Oracle-EBS" | "Dynamics-365" | "Plex" | "ManualImport" | etc.
```

Every existing row migrates to `Category=Maintenance` (lossless backfill).

**3. Four satellite detail tables (one per non-maintenance category)**

Each is a 1:0..1 FK to MaintenanceEvent. NULL satellite = category-specific data not yet captured. Empty satellite row = explicit "no detail" (rare). The Plant Floor query joins by `LEFT JOIN ... ON wo.Category = X` so the read cost stays bounded.

**ProductionWorkOrderDetails**

| Column | Type | Purpose |
|---|---|---|
| `Id` | int PK | |
| `MaintenanceEventId` | int FK (unique) | 1:1 with the unified WO |
| `PartNumber` | string(64) | What's being produced (links to Item.Sku in a future PR) |
| `ItemId` | int? FK | Optional FK to Item catalog (when we have a parts master) |
| `QuantityOrdered` | decimal(18,4) | How many units in this run |
| `QuantityCompleted` | decimal(18,4) | Good units produced so far |
| `QuantityScrap` | decimal(18,4) | Scrap units (drives ISO 22400-2 scrap-ratio KPI) |
| `QuantityRework` | decimal(18,4) | Rework units |
| `UnitOfMeasure` | string(16) | "EA", "KG", "L", "M" |
| `ShiftCode` | string(8)? | "1ST", "2ND", "3RD" — links to ShiftPattern (future PR) |
| `RoutingStep` | int? | Operation sequence within a multi-step routing |
| `MaterialMaster` | string(64)? | SAP MATNR or equivalent for ERP linkage |

**QualityWorkOrderDetails**

| Column | Type | Purpose |
|---|---|---|
| `Id` | int PK | |
| `MaintenanceEventId` | int FK (unique) | |
| `NcrNumber` | string(32)? | NCR / deviation / audit finding ID |
| `QualityIssueType` | enum | `Defect`, `Deviation`, `AuditFinding`, `CustomerComplaint`, `SupplierIssue`, `InternalNcr` |
| `Severity` | enum | `Minor`, `Major`, `Critical` (separate axis from MaintenancePriority — Quality severity ≠ WO priority) |
| `DispositionCode` | enum | `UseAsIs`, `Rework`, `Scrap`, `Return`, `Pending` |
| `CapaRequired` | bool | If true → triggers a linked Engineering-category WO for CAPA |
| `CapaWorkOrderId` | int? FK | Self-FK back to the CAPA WO |
| `AffectedQuantity` | decimal(18,4)? | Units affected |
| `RootCauseCategory` | enum | `Machine`, `Material`, `Method`, `Manpower`, `Measurement`, `Environment` (5M+1E fishbone) |
| `RegulatoryReportable` | bool | FDA / OSHA / EPA notification required |

**EngineeringWorkOrderDetails**

| Column | Type | Purpose |
|---|---|---|
| `Id` | int PK | |
| `MaintenanceEventId` | int FK (unique) | |
| `EcoNumber` | string(32)? | Engineering Change Order ID |
| `EngineeringIssueType` | enum | `EngineeringChangeOrder`, `ManagementOfChange`, `DesignChange`, `BomRevision`, `ProcedureUpdate`, `Capa` |
| `ImpactAssessment` | string(2000)? | Free-text impact narrative |
| `AffectedAssets` | string(500)? | Comma-separated asset numbers — for cross-asset MOC scope (future: junction table) |
| `RegulatoryReview` | bool | Triggers QA/regulatory sign-off step |
| `RiskLevel` | enum | `Low`, `Medium`, `High`, `Critical` |
| `LinkedNcrId` | int? FK | Back-link to the Quality WO that triggered this engineering change |

**HseWorkOrderDetails**

| Column | Type | Purpose |
|---|---|---|
| `Id` | int PK | |
| `MaintenanceEventId` | int FK (unique) | |
| `HseIssueType` | enum | `SafetyInspection`, `HazardReport`, `NearMiss`, `Incident`, `Jsa`, `BehaviorBasedSafety`, `Audit` |
| `HazardSeverity` | enum | `Negligible`, `Minor`, `Moderate`, `Serious`, `Catastrophic` (ANSI Z10) |
| `Likelihood` | enum | `Rare`, `Unlikely`, `Possible`, `Likely`, `AlmostCertain` (ANSI Z10) |
| `RiskScore` | int | Computed: severity × likelihood, 1-25 |
| `RecordableIncident` | bool | Counts toward OSHA TRIR/DART |
| `RegulatoryReportable` | bool | OSHA 300 log, EPA, etc. |
| `JsaReferenceUrl` | string(500)? | Link to Job Safety Analysis doc |
| `EmployeesAffected` | int? | Count for incident reports |
| `LostTimeIncident` | bool | LTI flag — separate from RecordableIncident |

### What we explicitly do NOT do in this ADR

- **Don't rename `MaintenanceEvent` to `WorkOrder` yet.** The .NET class + DB table stay named MaintenanceEvent for backward compatibility. A future sprint can do the clean rename migration when we have a full freeze window. The conceptual model is "WorkOrder, currently named MaintenanceEvent."
- **Don't model shop-floor data collection** (timekeeping, clock-in/out, andon). That's a future ADR (production execution).
- **Don't model electronic batch records** (eBR) — separate ADR when a GxP customer signs.
- **Don't build the Inventory/Material module yet.** ProductionWorkOrderDetails.ItemId is a forward reference; we'll wire it when the parts master ships.
- **Don't replace ERP production scheduling.** We integrate via ExternalWorkOrderId, we don't try to be SAP PP.

---

## Alternatives Considered

### Alternative A: WorkOrderClassification + satellites *(CHOSEN)*

- **Description:** Add a category discriminator + per-category 1:1 satellite tables. MaintenanceEvent stays as the work-order header for all categories.
- **Pros:** Zero-risk migration (existing data backfills to Category=Maintenance). Single-table read in the 80% case (no joins for maintenance WOs). EF Core 9 handles the optional 1:1 navs natively.
- **Cons:** MaintenanceEvent name becomes a misnomer. Satellite tables proliferate (4 new tables in this ADR, potentially more later).
- **Why chosen:** Least disruptive path; ships a usable disruptive feature in 2-3 sprints without a heavyweight refactor.

### Alternative B: TPH inheritance — rename to WorkOrder with subclass per type

- **Description:** Use EF Core 9 Table-Per-Hierarchy: `WorkOrder` base, `MaintenanceWorkOrder` / `ProductionWorkOrder` / `QualityWorkOrder` subclasses with a discriminator column.
- **Pros:** Cleanest type system; LINQ filters by subtype (`db.WorkOrders.OfType<ProductionWorkOrder>()`).
- **Cons:** Massive refactor — every existing query, every Razor page, every report breaks. The TPH table grows wide (sum of all subtype columns) — eventually hits Postgres's effective row-size budget.
- **Why rejected:** The refactor cost is 5-10 PRs of churn before any new functionality lands.

### Alternative C: New `WorkOrder` table; MaintenanceEvent FKs into it 1:1

- **Description:** Create a new top-level WorkOrder table with the shared header fields. MaintenanceEvent gets a FK to WorkOrder. ProductionOrder, QualityOrder, etc. each get their own table with FK to WorkOrder.
- **Pros:** Cleanest separation; each category owns its own table.
- **Cons:** Every read of a maintenance WO becomes a two-table join. Five tables to maintain. Cross-category queries (the "single pane of glass" promise) require UNION ALL across five tables.
- **Why rejected:** The two-table read penalty hits the Plant Floor view (hottest read in the app). Defeats the unification thesis — we'd actually have five fragmented tables, just like Maximo.

### Alternative D: Stay maintenance-only; create separate models for the other types

- **Description:** Build `ProductionOrder`, `QualityNcr`, `EngineeringChangeOrder`, `SafetyInspection` as their own top-level models, unrelated to MaintenanceEvent.
- **Pros:** Each module evolves independently.
- **Cons:** Five separate work queues. Five separate views per asset. Same problem Maximo has — no unified work picture per asset.
- **Why rejected:** Defeats the disruptive thesis. This is what every competing platform already does.

---

## Consequences

### Positive

- **Single unified work queue** — Plant Floor and Asset Detail show ALL work touching an asset (PM + production run + open NCR + ECO + safety inspection) in one timeline, color-coded by category. This is the marketed differentiator.
- **ERP/MES integration shape is settled** — ExternalWorkOrderId + ExternalSource gives us a clean two-field linkage that works for SAP PP, Oracle MES, Dynamics, Plex, Epicor, manual CSV imports. Already supports webhook-based bidirectional sync (PR series 80s-90s infrastructure is reusable).
- **ISA-95 Part 4 alignment** — work-record vocabulary lets us serialize/deserialize against B2MML and OAGIS BODs in a future PR without schema churn.
- **ISO 22400-2 KPIs become computable** — scrap %, yield, OEE all derive directly from ProductionWorkOrderDetails. Current maintenance schema can't compute these.
- **CAPA / NCR closed loop** — Quality WO with `CapaRequired=true` links to an Engineering WO that links back via `LinkedNcrId`. Auditors love this for FDA 21 CFR Part 11 / ISO 9001 sign-off.
- **Zero-risk migration** — every existing row backfills to Category=Maintenance, every existing query keeps working unchanged, every existing Razor page keeps rendering.
- **Lookup-value FKs work for new categories** — tenant-customizable dropdowns for QualityIssueType, HseIssueType, etc. fall out of the existing LookupValue infrastructure.

### Negative

- **MaintenanceEvent is now a misnomer.** Mitigation: heavy comments in the model file; rename in a future sprint when we have a freeze window.
- **Satellite tables add join overhead** when a UI needs the per-category fields. Mitigation: only join when actually displaying those fields; the 80% Plant Floor read path doesn't join.
- **Sprint 3 + 4 will ship 4-5 PRs of UI work** — one per category. The schema is the easy part; the UX is where the disruption shows up.
- **MaintenanceType.Other becomes overloaded** — for non-maintenance categories, Type defaults to Other. This is acceptable; new code reads Category first, Type second.

### Neutral

- The `WorkOrderApprovalStatus` flow applies uniformly across categories. Production WOs typically don't need approval (auto-approved when ERP-sourced); engineering WOs always do. This is data, not schema.
- The `Operations` collection (WorkOrderOperation) becomes useful across all categories — a production WO has routing steps, a quality WO has investigation steps, an HSE WO has corrective actions. Same shape.

---

## Implementation Plan

This ADR ships across **3 PRs**:

| PR | Scope | Risk |
|---|---|---|
| **PR #119.1** | Add `WorkOrderClassification` enum + `Category`/`ExternalWorkOrderId`/`ExternalSource` columns on MaintenanceEvent. Migration backfills all existing rows to Category=Maintenance. No UI changes. | Low — additive only. |
| **PR #119.2** | Add the 4 satellite tables (Production/Quality/Engineering/HSE Details) + EF nav properties + migration. No UI yet. | Low — purely additive. |
| **PR #119.3** | Plant Floor + Asset Detail filter chips for category. New `/WorkOrders` unified queue page replaces `/Maintenance/Index`. Old route 301-redirects for ~30 days then dies. | Medium — touches hot UI paths. |

**Sprint 4 then ships category-specific creation flows** (one PR per category):

- PR #120 — Production WO creation + run-card UI
- PR #121 — Quality NCR intake + disposition workflow
- PR #122 — Engineering ECO + MOC routing
- PR #123 — HSE incident + JSA workflow

**ADR-013 (Unified Scheduling)** will handle the production-scheduling expansion separately — that's about scheduling math, not work-order shape.

---

## Open Questions for Dean's Sign-off

1. **Are the 6 categories (Maintenance / Production / Quality / Engineering / HSE / Project) the right top-level cut?** I separated Project from Maintenance because today CIP is bolted onto MaintenanceEvent.CipProjectId — making Project a first-class category lets us model capital project workflows cleanly. **Recommend:** Yes — keep all 6.
2. **Severity vs. Priority — do you want them as separate axes?** Quality and HSE have an industry-standard severity scale (Minor/Major/Critical for quality; ANSI Z10 severity × likelihood for HSE) that is conceptually different from the WO priority (Low/Medium/High/Critical, which is "when do I work on this"). **Recommend:** Yes — keep them separate. Most regulated industries require both.
3. **CAPA closed-loop FK** — should the link between Quality NCR and Engineering CAPA be a single `LinkedWorkOrderId` field that points both ways, or two named FKs (`CapaWorkOrderId` from NCR side, `LinkedNcrId` from engineering side)? **Recommend:** Two named FKs — the bidirectional semantics is clearer in code, and EF Core handles the inverse navigation cleanly.
4. **External-source list** — should `ExternalSource` be a free-text string or an enum/lookup? Free text gives us flexibility for one-off integrations; lookup gives us cleaner reports and a dropdown in the UI. **Recommend:** Free text in PR #119.1, migrate to a lookup table in PR #119.4 when we have 3+ live integrations to seed it with.
5. **GxP / regulated-industry flag at tenant level** — should we add a Tenant.RegulatedIndustry flag that turns on the "regulatory reportable" UI hints in Quality + HSE? **Recommend:** Yes, but punt to Sprint 5. Most demo customers don't need it; the schema fields are ready for when a GxP customer signs.

---

## Related Documents

- [ADR-001: PMSchedule Canonical Model](./ADR-001-PMSchedule-Canonical-Model.md) — the original maintenance-only model
- [ADR-002: DemoPackV2 Canonical Seed](./ADR-002-DemoPackV2-Canonical-Seed.md) — seeded demo data we'll extend for production WOs
- [ADR-007: Unified Tab System](./ADR-007-Unified-Tab-System.md) — the UI pattern the unified work queue will use
- [ADR-011: Industrial Sensor Data Architecture](./ADR-011-industrial-sensor-data-architecture.md) — the telemetry substrate that powers cross-category alarms feeding into all work-order types
- ISA-95 Part 4: Object Models and Attributes for Manufacturing Operations Management Integration (IEC 62264-4:2015)
- ISO 22400-2:2014 — KPIs for manufacturing operations management

## Revision History

| Date | Author | Description |
|------|--------|-------------|
| 2026-05-17 | Claude (proposed) | Initial v0.1 draft — category + satellites path |
