# ADR-028 — ProductionOrder Parent-Child for Multi-Level BOM Execution

**Status:** Accepted
**Date:** 2026-05-26
**Sprint:** 12.8 PR #5a (Production Motion cascade ship #1 of 5)
**Authors:** Dean Dunagan (architectural directive), Claude (drafting)
**Supersedes:** N/A
**Related:**
- ADR-013 production-domain (the polymorphic `ProductionOrder` + `MaterialStructure` backbone this ADR extends)
- ADR-019 wms-posting-profile-pattern (facility-level GL accounts the cost columns route to)
- ADR-027 sales-order-lines-drive-po (separation-of-concerns precedent for distinct entities with distinct lineage semantics)
- `docs/research/abs-thursday-demo-data-design-2026-05-26.md` §3.2 + §5
- `docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md` (Severity-1: WO ActualCost hand-typed not rolled up — same shape on Production side)
- memory: `project_pr348_shipped.md` (the prior ship before this one)

---

## Context

The 2026-05-26 directive from Dean: the Thursday ABS Machining demo needs to walk a **10-level BOM** for an aerospace engine bracket assembly through **two plants** (Mississauga + Burlington), out for a **subcontract paint** operation, with **costs accumulating per Work Order at each BOM level and rolling up to the parent** when each child closes.

The schema today already has the bones:

- `ProductionOrder` (`Models/Production/ProductionOrder.cs`) with `Status`, `LocationId`, `CustomerProjectId`, `MaterialStructureId`, `MasterProductionOrderId` self-FK for revision chains.
- `MaterialStructure` (`Models/Production/MaterialStructure.cs`) with `Lines` collection for components.
- `Routing` + `RoutingOperation` + `ProductionOperation` (snapshot model) — 5-time decomposition (Setup/Run/Queue/Move/Wait), Subcontract `OperationType`.
- `WorkOrderOperation.IsExternal` + `VendorId` + `AutoGeneratePR` + `VendorExpectedReturnDate` for outside processing.
- Site → Location hierarchy already supports multi-plant.

What is **missing** is two things:

1. **No way to record parent-child between ProductionOrders for executing a multi-level BOM as nested orders.** `MasterProductionOrderId` is the *revision* chain — PRO v1 → PRO v2 → PRO v3 of the SAME order, with revision semantics (cascade lifecycle on supersede). That is structurally distinct from "this PRO is a sub-assembly that feeds that PRO" (assembly hierarchy, cost-rollup direction child→parent, lifecycle parallel rather than cascaded).
2. **No cost columns on ProductionOrder.** The May 8 structural audit flagged the equivalent gap on `WorkOrder` as Severity-1 ("ActualCost is hand-typed not rolled up"). For Sprint 12.8's demo + Sprint 14's eventual real cost-rollup engine, the columns need to exist.

The question for this ADR: **what's the cleanest shape to add multi-level-BOM parent-child semantics + cost columns to ProductionOrder, without conflating with the revision chain?**

Two strong-and-incompatible options.

## Decision

Adopt **Option B — separate self-FK column for assembly hierarchy** + **five header cost columns**.

### Schema additions on `ProductionOrders`

```
ParentProductionOrderId  int NULL           — assembly-hierarchy self-FK
  FK to ProductionOrders.Id, ON DELETE SET NULL
  CHECK: ParentProductionOrderId IS NULL OR ParentProductionOrderId <> Id

MaterialCost      numeric(18,2) NULL
LaborCost         numeric(18,2) NULL
OverheadCost      numeric(18,2) NULL
SubcontractCost   numeric(18,2) NULL
ActualCost        numeric(18,2) NULL    — sum of the four (and any child rollup)
```

### Semantic separation locked

| Column | Direction | Cascade | Cost-rollup direction | Lifecycle |
|---|---|---|---|---|
| `MasterProductionOrderId` (revision chain) | newer-revision → older-revision | SET NULL on master delete | N/A (revisions share cost ledger) | linear / supersede |
| `ParentProductionOrderId` (assembly hierarchy, **NEW**) | sub-assembly → parent-assembly | SET NULL on parent delete | child cost → parent cost | parallel (children run concurrently) |

### Cost column semantics

- **NULL** = "the cost engine has not yet populated this value" (different from `0`, which means "engine ran and found zero on this bucket").
- **`ActualCost`** is held explicitly, not computed. The future cost-rollup service (Sprint 0.5 finishing + Sprint 14 building) stamps it after summing this order's own four cost buckets PLUS all child orders' `ActualCost`. Holding it explicitly lets dashboards and the chain-trace narration read total cost in a single column scan without recursive queries.
- **CHECK constraints on cost columns deferred until the engine lands.** Negative or absurd values are invalid but the seeder won't try; tightening later is non-breaking.

### Sprint 12.8 vs Sprint 14 boundary

Sprint 12.8 PR #5a (this ADR) ships **schema only**: columns, FK, index, CHECK constraint. PR #5c (ABS scenario seeder) stamps pre-computed values for the hero scenario. **No engine** is built in Sprint 12.8 — that's Sprint 0.5 + Sprint 14.

The schema this ADR locks is exactly the shape the engine will populate when built. Demo data and real engine output land in the same columns.

## Alternatives considered

### Option A — Overload `MasterProductionOrderId` for both revision AND assembly hierarchy

**Rejected.** This is the SAP PP02 trap. Revision and assembly hierarchy have different cascade semantics (supersede vs. parallel execution), different cost-rollup direction (revision doesn't roll up; assembly does), and different lifecycle (revisions are sequential; sub-assemblies are concurrent). Conflating them produces silent bugs every time the assumption changes. SAP's PP02 architecture explicitly split these for the same reason — referenced in `ProductionOrder.cs:13` comment ("Mixing them on one header is the SAP-PP02 trap").

### Option C — Imply parent-child via `CustomerProjectId` grouping + date ordering, NO new column

**Considered, partially rejected.** All 10 ProductionOrders in the hero scenario DO share a CustomerProject, and the project view IS the visible tree on the Walkthrough page. But that's not enough on its own:

- Sub-assembly relationships need to survive without a project. Some manufacturers run nested production with no project context (pure job-shop mode).
- The cost-rollup engine needs an unambiguous FK to follow when summing child ActualCost into parent ActualCost. Grouping doesn't provide that.
- The chain-trace narration (PR #346) needs a direct FK walk for "trace this asset's source production order back to the parent assembly."

**Decision: do BOTH.** Use CustomerProject for the visible tree on the demo Walkthrough (already wired in PR #345's chain trace), AND add the explicit FK for cost rollup + chain trace + future BOM-explosion service. Dean's directive 2026-05-26: *"Both — group AND add the FK. Best fidelity."*

### Option D — Add a separate `ProductionOrderTree` junction table

**Rejected as over-engineering for the present use case.** Multi-parent assembly hierarchies (one sub-assembly feeding multiple parents) are rare in discrete manufacturing and absent in the ABS scenario. A simple self-FK with a SET NULL behavior covers the discrete case cleanly. If the future surfaces multi-parent demand, the schema can evolve to a junction table with the data migration cost paid by the customer who needs it.

### Option E — Defer until Sprint 14 / structural audit cleanup

**Rejected against the Thursday demo timeline.** Dean's directive 2026-05-26: *"We need it all and we are going to do it even though you dont think we can. MRP and everything."* This ADR is the smallest possible foundation that supports the demo without taking a shortcut the future engine will have to un-do.

## Consequences

### Positive
- Cost rollup engine in Sprint 14 has the exact columns it needs. No future migration to add them.
- Chain trace (PR #346) can extend to walk `Asset → CipCapitalization → CipProject → CipCosts → ProductionOrder → Parent ProductionOrder` deterministically.
- Demo scenario seeder (PR #5c) populates real schema, not a mock side table.
- Revision chain and assembly hierarchy remain semantically separate. Future readers of the model file don't have to reason about which one a self-FK reference means.

### Negative
- Two self-FKs on the same table doubles the cognitive overhead for first-time readers of `ProductionOrder.cs`. Mitigated by the inline comments distinguishing them.
- Five new nullable cost columns until the engine lands. Mitigated by ADR-028 documenting the NULL-vs-zero semantics, and by the seeder populating sentinel values that make the demo coherent.

### Open questions deferred to Sprint 14
- Whether `ProductionOperation` also needs its own cost columns or whether per-operation cost lives on `LaborEntry` + `WorkOrderPart` aggregations. ADR-028 covers ProductionOrder header only; ProductionOperation cost shape gets its own ADR when Sprint 14 cost engine is designed.
- Whether `ActualCost` needs a generated stored column variant in the database for query performance. Defer until query-pattern data exists post-Republish.
- Multi-parent assembly hierarchies (Option D shape) if customer demand surfaces.

## Implementation

PR #5a (this PR) ships:
- Model changes in `Models/Production/ProductionOrder.cs`: 5 cost properties + `ParentProductionOrderId` + `Parent` nav + `Children` inverse collection.
- `AppDbContext.cs` configuration: index on ParentProductionOrderId, FK with `OnDelete(SetNull)`, CHECK constraint via `HasCheckConstraint` (Lock 12 — typed builder, not raw SQL).
- Migration `20260526135013_AddProductionOrderCostsAndParentFk_Sprint128Pr1.cs` (auto-scaffolded from EF Core 9 model change detection).

PR #5b (next, Sprint 12.8) ships the `IBackwardSchedulingService.BackwardScheduleAsync` stub that consumes these columns.

PR #5c (next, Sprint 12.8) ships the ABS scenario seeder that populates the hero ProductionOrder hierarchy.

Sprint 14 (post-demo) ships the real cost-rollup service that auto-populates these columns on ProductionOrder completion.

## Status: Accepted 2026-05-26 by Dean Dunagan.
