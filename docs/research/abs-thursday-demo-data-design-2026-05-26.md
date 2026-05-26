# ABS Machining Thursday Demo — Data Design

**Written:** 2026-05-26 (after PR #348 shipped)
**Demo target:** Thursday 2026-05-28 (ABS Machining onsite, ~2 days out)
**Authoring constraint:** best-in-class process, best-in-class product — no shortcuts, no demo-only hacks where a real schema artifact is the right answer.

## 1. Audience & motions

**Two separate scenarios** (per Dean's decision 2026-05-26), each with its own scripted walkthrough:

| Motion | Audience | Headline question | Surface |
|---|---|---|---|
| **CFO motion** | Paul Marcotte (CFO/Controller, ex-Kraft Heinz/KPMG) | "Why is NBV $X on Asset Y? Walk me back to the invoice." | `/Controller` (PR #345-#348 already live) + new `/Controller/Walkthrough` |
| **COO motion** | Shadi Mohaisen (COO, most technically literate, knows ERP21 internals) | "Show me a 10-level part going through 2 plants with a subcontract step. Where are the costs?" | `/Production/ControlCenter` (live) + Project view + new `/Production/Walkthrough` |

Both motions sit on the **same prod database** post-Republish-with-Copy. They don't share a hero scenario — clean separation, two distinct storylines.

## 2. What's built vs. what's faked — the honest cut

Per the 2026-05-26 audit (research/abs-thursday-demo-data-design-2026-05-26-audit.md companion):

### Built and working
- ProductionOrder header + service + status machine (Planned → Released → InProgress → Completed)
- MaterialStructure (BOM) + MaterialStructureLine polymorphic schema, revision-chain
- Routing + RoutingOperation with 5-time decomposition (Setup/Run/Queue/Move/Wait) + Subcontract OperationType
- ProductionOperation execution-time snapshot table + 8-state status machine
- WorkOrderOperation with `IsExternal`, `VendorId`, `AutoGeneratePR`, `VendorExpectedReturnDate` (subcontract schema)
- LaborEntry + clock-in/clock-out with one-open-clock-in constraint
- Company → Site → Location hierarchy
- Production Control Center page fully wired (KPI band, Queue, Exception Lane, Activity)
- Operator Workbench fully wired (Operations, MyTime, Activity tabs)
- ProductionOrderService Create/Update/Status transitions/Project link

### Schema-only (built but not wired through services)
- ReleaseFromRoutingAsync exists but no UI calls it (we'll call it from the seeder)
- MaterialStructure.Lines exists but no BOM explosion service walks the tree
- LaborEntry exists but no aggregation to operation cost
- AutoGeneratePR flag exists but no requisition generator

### Missing entirely (must hand-craft via data)
- Backward scheduling engine
- Multi-level BOM explosion (parent → child WO auto-cut)
- Cost roll-up child WO → parent WO
- Material issuance from BOM with inventory decrement + GL post
- Subcontract GR auto-completion
- Facility-level GL absorption posting
- `ProjectPostingMode` consumption (Consumed vs FinishedItem)

### What this means
We are NOT shipping a working MRP+cost engine for Thursday. We are shipping **a hand-crafted scenario that LOOKS like an MRP+cost engine ran**, atop the screens that exist. Standard demo pattern (Oracle/SAP/Plex all work this way). The honest answers to "what if I change X right now" are scripted in advance, not faked live.

## 3. The two scenarios

### 3.1 CFO motion — data needs

**Storyline:** Paul opens `/Controller`, sees the KPI band with believable numbers, clicks Drilldown, picks a hero asset, Cherry narrates the full chain from asset back to the invoice that paid for it. Every step clickable.

**Hero asset:** One of the 25 existing DEMO- assets (Asset IDs #1116-#1140, $32.27M CAD total already on prod). Pick the highest-value DEMO- asset that's plausibly a capital purchase — likely a 5-axis CNC (Mazak or Parpas in the photo set). Set its **per-asset GL overrides** so the chain trace narration walks cleanly through `Accumulated Depreciation` and `Depreciation Expense` accounts instead of saying "not disambiguated" (that's PR #346's honest-answer fallback firing today).

**KPI band data targets** (vs. dev preview values today):

| Tile | Today on dev | Demo target | How |
|---|---|---|---|
| Cash Position | $32.2K | $5-8M | Add 10-15 cash JEs into CashAndReceivables accounts on CompanyId=2 |
| AP Due This Week | $3.5K (1 inv) | $400-700K (15-25 inv) | Add VendorInvoices with realistic vendor names, due dates within +7d, in Approved/PartiallyPaid status |
| Open POs | 102 / $664K | 110-130 / $2.0-2.8M | Bump existing PO totals + add 10-20 more high-dollar ones |
| WIP Balance | $0 (5 projects) | $1.5-3M (5 projects) | Seed CipCosts on existing CipProjects so TotalCosts is non-zero |

Tone heuristic verification: AP tile should land in `warning` range (>$50K, <$200K = warning; >$200K = danger). Target $400-700K = `danger` tone, blue → red banner above the tile. Good demo color.

**Chain trace verification:** for the hero asset, walking `?q=ASSET-<id>` on the Drilldown tab should render the full narration with NO "not disambiguated" fallback. Requires populating `Asset.GlAccountAccumDepOverride` and `Asset.GlAccountDepExpOverride` columns to per-asset values (PR #346 ChainTraceService disambiguates on these).

### 3.2 COO motion — data needs

**Storyline:** Shadi opens the `/CustomerProjects/Details/<id>` view for **"Rolls-Royce Aerospace Engine Bracket Q3 2026"**, sees a 10-level production tree, clicks into the parent ProductionOrder, sees BOM + Operations + Cost rollup. He clicks down through children, finds the one going to subcontract paint, sees the vendor + expected return date, sees the next operation at Burlington plant, sees the cost stack rolled up to the parent.

**Hero scenario: aerospace engine bracket for Rolls-Royce** (Dean's decision — Rolls-Royce is a known ABS customer; tier-1 aerospace story Shadi will instantly recognize).

**Data structure:**

1. **CustomerProject** (1 row)
   - Name: "Rolls-Royce Trent XWB Engine Bracket Assembly — Q3 2026"
   - Customer: Rolls-Royce (existing Customer row or add)
   - Status: Active
   - 4 ProjectPhases: Engineering / Procurement / Production / Delivery

2. **ProductionOrders** (10 rows, all linked to the CustomerProject + linked parent-child via new `ParentProductionOrderId` FK from PR #5a)
   - PRO-2026-1000 → Top-level Bracket Assembly (parent, no ParentProductionOrderId)
     - PRO-2026-1001 → Forged housing sub-assembly
       - PRO-2026-1002 → Raw forging stock prep
     - PRO-2026-1003 → Machined bracket plate
       - PRO-2026-1004 → Bar stock prep
     - PRO-2026-1005 → Threaded insert kit
     - PRO-2026-1006 → Fastener kit
     - PRO-2026-1007 → Painted finish (subcontract operation lives here)
     - PRO-2026-1008 → Hardware inspection package
     - PRO-2026-1009 → Final QA + packaging

3. **MaterialStructure** (10 BOMs — one per ProductionOrder)
   - Parent BOM has 9 lines, each line pointing to a sub-assembly's OutputItemId
   - Each child BOM has its own lines for raw stock / hardware / consumables

4. **Routings + RoutingOperations**
   - Each ProductionOrder has its own Routing with 3-5 RoutingOperations
   - **Operation distribution across facilities:**
     - Mississauga (Site MISS, Locations MISS-A1 Machine Shop, MISS-A2 Assembly): operations 1-3 on each child
     - Burlington (Site BURL, Locations BURL-P1 Paint, BURL-F1 Finishing): operations 4-5 on PRO-2026-1007 + final operations on parent PRO-2026-1000
   - **Subcontract operation:** PRO-2026-1007 operation 2 has `OperationType=Subcontract`, `IsExternal=true`, `VendorId=AeroCoat Industries`, `VendorExpectedReturnDate=2026-06-12`

5. **ProductionOperations** (snapshotted from Routings via `ReleaseFromRoutingAsync`)
   - Each carries `PlannedStart`/`PlannedEnd` (backward-scheduled by PR #5b stub from ship date 2026-08-15)
   - Mix of statuses: 30% Completed, 40% Running/InSetup, 30% Scheduled (gives realistic "in-flight" feel)

6. **LaborEntry** (~25-40 rows)
   - For Completed operations: clock-in/clock-out pairs with realistic durations
   - For Running operations: 1-2 open clock-ins
   - Operators: a mix of named seed users

7. **WorkOrderPart** (~30 rows)
   - Pre-issued kits — one per operation
   - Mix of "from inventory" + "job-specific PO" sources

8. **JournalEntry** (~20 rows tagging facility costs)
   - WIP-MISS / WIP-BURL accruals
   - OH-MISS / OH-BURL applied overhead
   - Inter-facility transfer entry when work moves Mississauga → Burlington

9. **Pre-computed cost values on ProductionOrder cost columns (added in PR #5a):**
   - Parent PRO-2026-1000 ActualCost ≈ $185-220K
   - Children sum to parent (verifiable by clicking through)
   - Cost split by facility: roughly 70% MISS, 30% BURL

### 3.3 What we deliberately don't try to demo

- Real-time re-scheduling when an operation slips ("watch me drag this to next week" — not built)
- Real inventory decrement on material issue (audit S1 — not built)
- Real GR triggering operation completion after subcontract (not built)
- Real overhead absorption posting at month-end (not built)
- ProjectPostingMode auto-flowing costs to project GL (schema only)

**The honest deflection script** for these (rehearse in advance):
> *"That's a great question. What you're seeing today is the result of those flows — pre-computed and stamped, like a playback. The live engines that produce these values are Sprint 14 work. Want to schedule a follow-up where I show those engines being built, on your real data?"*

This is not bullshit. It's how demos work. The data tells the truth.

## 4. PR sequence

Sized for parallel execution where coordination cost is low.

| PR | Scope | Hours | Mode |
|---|---|---|---|
| **#5a** | Schema migration: cost columns on ProductionOrder + `ParentProductionOrderId` self-FK + ADR-028 (parent-child for multi-level BOM execution) | 1.5 | me, sequential |
| **#5b** | `IBackwardSchedulingService.BackwardScheduleAsync` stub — linear walk, no resource leveling | 2-3 | agent, parallel to 5c kickoff |
| **#5c** | ABS scenario seeder — all 9 entity classes above, idempotent, runs once in dev, then Republish-with-Copy carries it to prod | 6-10 | me, sequential (judgment-heavy) |
| **#5d** | `/Controller/Walkthrough` page — split-screen, scripted CFO motion | 2 | agent, parallel after 5c lands |
| **#5e** | `/Production/Walkthrough` page — split-screen, scripted COO motion | 2-3 | agent, parallel after 5c lands |
| **#5f** | TimescaleDB removal migration (typed, drops the dead `CREATE EXTENSION` line per Lock 12) | 0.5 | folded into 5a OR ship separately |
| **Final** | Republish-with-Copy + Lock 16 E2E on prod (industryos.app) | 1 | me |

**Total: ~15-22 hours** across 2 days. Parallel execution shaves 4-6 hours. Realistic for Thursday demo if Dean is responsive on review and I stay focused.

## 5. Schema additions (PR #5a precisely)

```csharp
// Migration: AddProductionOrderCostsAndParentFk
// References ADR-028.

migrationBuilder.AddColumn<decimal>(
    name: "MaterialCost", table: "ProductionOrders",
    type: "decimal(18,2)", nullable: true);

migrationBuilder.AddColumn<decimal>(
    name: "LaborCost", table: "ProductionOrders",
    type: "decimal(18,2)", nullable: true);

migrationBuilder.AddColumn<decimal>(
    name: "OverheadCost", table: "ProductionOrders",
    type: "decimal(18,2)", nullable: true);

migrationBuilder.AddColumn<decimal>(
    name: "SubcontractCost", table: "ProductionOrders",
    type: "decimal(18,2)", nullable: true);

migrationBuilder.AddColumn<decimal>(
    name: "ActualCost", table: "ProductionOrders",
    type: "decimal(18,2)", nullable: true);

migrationBuilder.AddColumn<int>(
    name: "ParentProductionOrderId", table: "ProductionOrders",
    type: "integer", nullable: true);

migrationBuilder.CreateIndex(
    name: "IX_ProductionOrders_ParentProductionOrderId",
    table: "ProductionOrders",
    column: "ParentProductionOrderId");

migrationBuilder.AddForeignKey(
    name: "FK_ProductionOrders_ProductionOrders_ParentProductionOrderId",
    table: "ProductionOrders",
    column: "ParentProductionOrderId",
    principalTable: "ProductionOrders",
    principalColumn: "Id",
    onDelete: ReferentialAction.SetNull);

// CHECK constraint: no self-parent
migrationBuilder.Sql(@"
    ALTER TABLE ""ProductionOrders""
    ADD CONSTRAINT ck_productionorders_no_self_parent
    CHECK (""ParentProductionOrderId"" IS NULL OR ""ParentProductionOrderId"" <> ""Id"");
");
```

Plus model updates on `Models/Production/ProductionOrder.cs`:
- 5 nullable `decimal?` cost properties
- 1 nullable `int? ParentProductionOrderId` + nav `ProductionOrder? Parent`
- `ICollection<ProductionOrder>? Children` for the inverse

Plus `docs/ADR-028-production-order-parent-child-multi-level-bom.md` — 1 page covering:
- Decision: add `ParentProductionOrderId` separate from `MasterProductionOrderId` (revision chain)
- Rationale: parent-child semantics are NOT revision semantics; conflating them causes the SAP PP02 trap
- Alternatives considered: imply via dates (rejected, fragile); use MasterProductionOrderId (rejected, semantic confusion); model via CustomerProject only (rejected, doesn't survive without a project)
- Consequences: requires a future BOM-explosion service to populate; for Sprint 12.7 PR #5 the seeder populates manually
- Status: Accepted, 2026-05-26

## 6. Republish-with-Copy plan

Per Lock 14, data-changing PR → Republish must check the "Copy your development database to production database" box. PR #5c lands the seeder; we run it once on dev; verify via psql + Lock 16 E2E on dev preview; then Republish copies dev → prod. Industryos.app gets the demo state.

Pre-flight checklist for the Republish:
- [ ] PR #5a/b/c/d/e/f all merged to main
- [ ] Replit dev workspace pulled to latest main
- [ ] Seeder ran ONCE in dev (idempotent guard prevents re-run)
- [ ] psql verify: row counts match expected per section 3.2 data structure
- [ ] Lock 16 E2E on dev preview /Controller + /Production/ControlCenter + both Walkthrough URLs
- [ ] Codex window clean on all 6 PRs
- [ ] Republish click → check "Copy dev DB → prod DB" → Publish
- [ ] Lock 16 E2E on prod industryos.app after publish completes
- [ ] Memory updated

## 7. Open questions for Dean

None right now — the four decisions from 2026-05-26 cover the design choices. Will surface new questions if the seeder hits a model constraint I didn't anticipate.

## 8. References

- `docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md` — S1 flaws (cost rollup, inventory decrement, GR completion)
- `docs/ADR-013-production-domain.md` — the polymorphic MaterialStructure + ProductionOrder design that PR #5a extends
- `docs/research/master-files-baseline-2026-05-24.md` — the master-files cascade that PR #5c data sits on
- `docs/research/abs-machining-demo-prep-2026-05-25.md` — audience profiles + demo flow
- `Models/Production/ProductionOrder.cs` — what's there today
- `Services/Production/ProductionOrderService.cs` — what mutations exist
- `Services/Production/IProductionOperationService.cs` — `ReleaseFromRoutingAsync` (the routing snapshot service the seeder will call)
- `Pages/Production/ControlCenter.cshtml.cs` — the live page the COO motion sits on
- MEMORY entry: `project_pr348_shipped.md` — what just shipped
