# Theme B7 — PO-as-Standard (Optional Item Master) + Make-or-Buy Duality
## Canonical Research Spec — RESEARCH-BEFORE-BUILD

**Written:** 2026-05-29 · Cowork session 23 · Claude (engineering lead) for Dean Dunagan
**Status:** RESEARCH COMPLETE. Cascade design NOT YET started (awaits Dean sign-off on this spec).
**Source brainstorm:** `reference_dean_brainstorm_b7_2026_05_26.md` (memory).
**Research dossiers (full citations):** `b7_research_area1_crystallization.md`, `b7_research_area2_makeorbuy.md`, `b7_research_area3_traceability_cost_mrp.md` (session outputs; key sources reproduced in §13).
**Companion specs this builds on:** `purchasing-subcontracting-supply-demand-dean-research.txt` (§1–§25), `production-costing-cost-rollup-dean-research.txt`, `b6-foundation-sprint-design-2026-05-26.md`.

---

## §0 — Executive Summary & Verdict

**Dean's thesis (verbatim, 2026-05-26):** *"You don't have to have an Item Master with a Standard BOM and ROUTING. You can do it all from the Production Order and when the Order ships, at that time, you can make it an Item Master Record from the Actual BOM/ROUTING and Costs incurred. All supply … should be driven solely by the Production Order BOM and ROUTING unless it is a make-to-stock or buy-to-stock part or has special planning parameters. Also we need the ability to have a part that can be both a make and a buy."*

**Verdict after 3-area tier-1 ERP + compliance research: the thesis is sound, legally viable, and sits in a quadrant no incumbent ERP fills.** Two sub-themes:

- **B7a — PO-as-Standard (optional Item Master).** In pure ETO/MTO, the Production Order's BOM + Routing *is* the operative standard during build. An Item Master is **optional** and is **crystallized at ship from actuals** (as-built BOM, as-run routing, actual cost) — becoming a reusable, costed, traceable template for repeat orders. **Verified compliance-viable** (AS9100D §8.5.2/§8.5.6, AS9102, DFARS 7008/7009/7007/7008 attach traceability to the drawing + job + lot/heat/serial, never to a master-data row).

- **B7b — Make-or-Buy Duality.** A part can be BOTH a make item and a buy item. A **decision service** chooses the path per-order from live work-center load + a live vendor quote + lead-time fit, and — the differentiator no incumbent ships — **explains the choice in plain language** and persists it as an AS9100 audit record.

**Why this is achievable now and not a moonshot:** the codebase already carries ~80% of the substrate (§9). `ProductionOrder.ItemId` is already nullable; `Item.MakeBuyCode.MakeOrBuy` already exists (its XML comment literally reads *"decision service routes per capacity/cost/lead-time at run time"*); the cost engine, supply-demand graph, DMS, and lot/heat-cert traceability are shipped. **B7 is an enrichment layer, not a redesign.** The only two genuinely-missing capabilities are (1) **crystallization-at-ship** (§3) and (2) the **explainable make-or-buy decision service** (§4).

---

## §1 — The Unoccupied Quadrant (cross-ERP synthesis)

Every tier-1 system implements *part* of B7; none unifies the whole. (Full citations in dossiers.)

| System | Build w/o item master? | Order-driven supply? | Crystallize from **actuals**? | Dynamic make/buy on live capacity+quote? | Explains the WHY? |
|---|---|---|---|---|---|
| **SAP S/4HANA** | Yes via **CO07** (rework) / non-valuated PS stock | Yes (non-stock comp → PReq; MTO/PS; strategy 82) | **No** | No — procurement type `X` resolves via costing-view config, not live load | No |
| **Oracle Fusion** | No (item-centric) | Yes (back-to-back firm link) | Crystallizes **pre-build, from a Model/Option-Class** + dedupe | No — rule-rank/split %, not finite-capacity-vs-quote | No |
| **Epicor Kinetic** | Header references a part; components Make-Direct | Yes (Make-Direct, ship-from-WIP) | Configurator generates part # **pre-build** + dedupe | **Not native — users bolt on custom code** | No |
| **Infor LN** | Item-centric, project-pegged | Yes (project/demand pegging) | No | No | No |
| **Plex** | Traceability-heavy; not publicly documented | MES-driven | No | Strong finite scheduler, **not wired** to make/buy | No |
| **Aras / Teamcenter (PLM)** | **Yes — as-built lives w/o any ERP item** | N/A (design domain) | **Captures as-built from actuals** | N/A | N/A |

**The two seams we own:**

1. **"No master up front + harvest the standard from as-run/as-built/actual-cost at ship + dedupe future identical orders."** SAP gives material-less build but *kills standard costing/variance* (CO07 settles to a cost center, no material receiver → no variance). Oracle/Epicor crystallize but *pre-build from a template you authored before cutting metal*. PLM captures as-built but *never makes it a costed, plannable ERP standard*. We unify PLM-as-built + ERP-standard **on the ship event**.

2. **"Decide make-vs-buy per order on live capacity + a live quote, and narrate the why."** The duality flag (`X`/`MakeOrBuy`) is everywhere; the dynamic, capacity-aware, *explainable* decision is open white space. Incumbent MRP emits terse exception codes (defer/expedite/cancel) — never a costed, capacity-aware sourcing narrative.

---

## §2 — Sub-theme B7a: PO-as-Standard (Optional Item Master)

### §2.1 Core principle
In pure ETO/MTO, the **Production Order carries the only standard that matters** — the BOM + Routing the customer ordered against. The Item Master is not a prerequisite to build. This is defensible and proven: SAP CO07 (order without material), Epicor Make-Direct (non-stock components), and PLM as-planned all validate that the order/structure — not an item code — can be the operative standard.

### §2.2 What "all supply driven by the PO" means in practice
- **Child PO (sub-assembly):** references the parent PO + parent BOM line, not an item code. Inherits qty/spec from the parent's BOM line. (Substrate: `ProductionOrder.ParentProductionOrderId` + `ProductionMaterialStructure` already exist.)
- **Purchase Order (raw material):** references the parent PO's BOM line for the requirement; purchase qty + delivery date come from the PO BOM, not MRP. (Substrate: `ProductionSupplyDemand` + `PurchaseOrderLineDemandLink` already exist.)
- **Subcontract:** references the parent PO operation; the vendor sees "make this from your routing-op spec," not "make item code XYZ." (Substrate: `SubcontractOperation` already exists.)

### §2.3 Carve-outs that STILL require an Item Master up front
The thesis is explicitly **not** "abolish the Item Master." It is "make it optional for one-off ETO." These cases keep the master at release (normal path):
- **Make-to-stock** (replenished to ROP/EOQ from independent-demand planning — needs master + reorder policy + lead times).
- **Buy-to-stock** (purchased into inventory for general consumption — needs master).
- **Special planning policies** (ABC class, EOQ, kanban, MSL/shelf-life rules).
- **Rev-controlled repeat parts** (ECR/ECO change control needs a rev chain → needs a master to hang revs off).

**Why the carve-outs are real (research-confirmed):** ROP/EOQ/safety-stock are *item-master-bound by design* — Oracle sets the inventory planning method "when you define the item." The industry explicitly excludes ETO from ROP. So the boundary is not arbitrary: **independent-demand planning needs an item master; dependent-/order-demand planning does not.**

### §2.4 The gating attribute — `Item.SourcePattern` (NEW)
The clean shape the brainstorm proposed, now confirmed by research:

```
Item.SourcePattern : enum {
    StandardFirst = 0,   // Classic: Item Master + standard BOM/Routing required at PO release. (MTS/BTS/repeat.)
    PoFirst       = 1,   // ETO: build from the PO; Item Master optional, crystallized at ship from actuals.
    Hybrid        = 2     // Master exists but PO may diverge; crystallize an as-built variant/rev at ship.
}
```

This is **orthogonal** to the existing `Item.PlanningPolicy` (which already has `EngineerToOrder=2`, `MakeToOrder=1`, etc.). `PlanningPolicy` says *how demand is planned*; `SourcePattern` says *when the master must exist*. A `PoFirst` order may have **no `Item` row at all** at release — `ProductionOrder.ItemId` is already nullable to allow exactly this.

---

## §3 — Crystallization-at-Ship (THE core novel feature)

### §3.1 What it is
At PO completion/ship, **one explicit, optional action** promotes the finished order's actuals into a new (or deduped) Item Master + standard BOM + standard Routing + standard cost. No incumbent does this from *as-run actuals*; PLM captures as-built but never pushes it into ERP as a costed/plannable standard.

### §3.2 What gets harvested (from already-shipped substrate)
- **As-built BOM** ← `ProductionMaterialStructure` (per-PO frozen BOM snapshot, with `ConsumedQuantity`, lot/serial/heat-cert flags, `ChildItemFingerprintHash`).
- **As-run routing** ← the operation-transaction state machine + `SubcontractOperation` (actual operations/resources/times).
- **Actual cost** ← `ProductionOrderCostSummary` (8-element actual buckets) — set crystallized standard cost = **first-unit actual** (§5.4).
- **Configuration lineage** ← drawing number + rev + ECO effectivity from DMS (`Document`/`DocumentVersion`) + ECR/ECO substrate, preserved as the master's origin record (AS9100 §8.5.2/§8.5.6).

### §3.3 Dedupe (table-stakes, not novelty)
Oracle (configured-item match) and Epicor (typical-config → existing part) both dedupe; Epicor practitioners actively fight one-off part-master sprawl. On crystallize, **fingerprint the as-built structure** (reuse the existing `ChildItemFingerprintHash` SHA-256 pattern) and offer: *"This matches existing standard `TI-BRKT-0042 Rev C` — link instead of create."*

### §3.4 New entity (proposed) — `ItemCrystallization`
The audit/event record of a crystallization (frozen snapshot + reversible/auditable):
- `Id`, `CompanyId`, `SiteId`, `RowVersion (xmin)`
- `SourceProductionOrderId` (the as-built source), `CrystallizationNumber` (two-phase: `CRYST-YYYY-NNNNNN`)
- `Outcome` enum `{ CreatedNewItem, LinkedToExisting, Rejected }`
- `CreatedItemId` (int?, the minted master) / `MatchedItemId` (int?, dedupe hit)
- `StructureFingerprintHash` (SHA-256 over as-built BOM+routing)
- `SeededStandardCost` (= first-unit actual), `CostSource` enum `{ FirstActual, MovingAverage, ManualOverride }`
- `AsBuiltDrawingNumber`, `AsBuiltDrawingRev`, `AsBuiltEcoEffectivity` (frozen)
- `RationaleText` (plain-language: what was harvested, dedupe result, cost basis)
- `CrystallizedAtUtc`, `CrystallizedBy`, `IsReversed`, `ReversalReason`

### §3.5 New service (proposed) — `IItemCrystallizationService`
- `PreviewCrystallizationAsync(productionOrderId)` → what the new master/BOM/routing/cost would be + dedupe match preview (no writes).
- `CrystallizeAsync(productionOrderId, options)` → atomic: mint Item + standard BOM + standard Routing + standard cost, OR link to dedupe match. Cross-service transaction enlistment (shares the open tx per HARD LOCK) when it composes cost/BOM services.
- `ReverseCrystallizationAsync(crystallizationId, reason)` → reversible, never rewrites the as-built history.
- **Compliance guard:** crystallized part number/rev MUST equal the FAIR/traveler/shipper value; lot-serial genealogy preserved. Crystallization is a master-data convenience, **not** a compliance event.

---

## §4 — Sub-theme B7b: Make-or-Buy Duality + Explainable Decision Service

### §4.1 Substrate that already exists
- `Item.MakeBuyCode` enum: `Make=0 | Buy=1 | MakeOrBuy=2 | Phantom=3`. `MakeOrBuy` XML comment: *"decision service routes per capacity/cost/lead-time at run time."* **The flag is laid down; nothing consumes it dynamically yet.**
- `ItemSourcingRule` (B6 PR-FS-5): the rule TABLE (SourceMethod: MakeInternal/BuyFromVendor/Subcontract/DirectShip/TransferFromSite; Priority; AllocationPercent; approval state). B7b adds the **decision SERVICE on top** — an enrichment, not a redesign.

### §4.2 New enums (proposed, richer than SAP E/F/X)
```
Item.MakeBuyPolicy : enum {
    MakeOnly | BuyOnly | MakeOrBuy | MakeWithBuyOverflow | BuyWithMakeBackup
}
Item.DefaultSourcePreference : enum { Make | Buy | LetSystemDecide }
```
Plus on `Item`: `IsSourceControlled` (bool) + `SourceControlReason` (forces a path; AS9100 flight-safety), `MakeBreakEvenQty` (decimal?), `FixedMakeInvestment` (decimal?, tooling capex for break-even).

### §4.3 The decision-factor chain (`IMakeBuyDecisionService.DecideAsync`)
Each factor returns a normalized score 0..1 favoring BUY, plus a one-line human reason. Final = weighted sum with hard-gate overrides.

| # | Factor | Signal | Favors BUY when | Default wt |
|---|---|---|---|---|
| F1 | **Eligibility gate** (hard) | policy allows path; valid routing for make; ≥1 approved supplier+quote for buy | one path infeasible → forced | gate |
| F2 | **Bottleneck capacity (TOC)** | projected finite Load% on routing's constraint WC over [today, dueDate]; routes through current drum? | Load% > threshold (85%) and/or on the drum | 0.30 |
| F3 | **Fully-loaded cost delta** | `MakeCostFullyLoaded` vs `BuyCostLanded` | buy is cheaper | 0.30 |
| F4 | **Make break-even qty** | `BE = FixedInvestment / (BuyUnit − VarMakeUnit)` | qty < break-even (low volume) | 0.10 |
| F5 | **Lead-time fit** | finite `MakeCompletionDate` vs `VendorDeliveryDate` vs dueDate | make misses date, buy meets it | 0.20 |
| F6 | **Quality / risk** | supplier quality score, NADCAP/AS9100 cert match, single-source risk | internal yield poor & qualified vendor exists | 0.10 |

**Hard-gate overrides (before weighting):**
- Only one feasible path (F1) → return it, rationale = "only feasible path."
- Op routes through the **current drum** AND a qualified vendor meets dueDate → **force BUY** (TOC throughput protection), tolerating up to `DrumOffloadCostTolerancePct` (default +8%) cost disadvantage to free the constraint.
- `IsSourceControlled` flight-safety → **force MAKE** (or approved-source buy), rationale cites the engineering flag.

**Aggregate:** `BuyScore = Σ(wᵢ·scoreᵢ)`; decision = **BUY if BuyScore ≥ 0.50** else MAKE. Tie-breaks (within ±0.05): protect the drum → earlier completion → lower landed/loaded cost → prefer make (keep capability/IP) as final, configurable.

**The money-shot — explainability:** sort factors by impact `wᵢ·|scoreᵢ−0.5|` and emit the top 2-3 as plain English. Grounded worked example (Ti-6Al-4V bracket, qty 12, 5-axis):
> **BUY** (confidence 0.78). (1) WC-MILL is 87% loaded through Aug 14 and this op runs on the constraint — buying frees throughput. (2) Vendor quote $1,840 beats fully-loaded internal $2,210 (−17%). (3) Vendor delivers Aug 9 vs internal Aug 19 — buy meets the Aug 12 due date; make would miss it.

This pairs with the existing Cherry Bar voice intent: *"Cherry, why are we buying this bracket this run?"*

### §4.4 New entity (proposed) — `MakeBuyDecision` (the audit / "why" record)
`Id`, `ItemId`, `SiteIdSnapshot`, `Qty`, `DueDate`, `DecidedAtUtc`, `Context` enum `{ ProductionOrder | ParentPoBomLine | Mrp | ManualWhatIf }` + `SourceType`/`SourceId`, `Outcome` enum `{ Make | Buy }`, `BuyScore`, `Confidence`, `WasHardGated` + `HardGateReason`, `RationaleText`, `FactorBreakdown` (jsonb of F1–F6 `{code,label,score,weight,weightedImpact,reason}`), **frozen snapshots** (`MakeCostFullyLoaded`, `BuyCostLanded`, `BottleneckWorkCenterCode`, `BottleneckLoadPct`, `RoutedThroughDrum`, `MakeCompletionDate`, `VendorDeliveryDate`, `ChosenSupplierId`, `ChosenQuoteId`), `RowVersion (xmin)`. Per-tenant config `MakeBuyDecisionPolicy` (thresholds + factor weights + final tie-break).

### §4.5 Integration
- **Inputs (live):** finite WC load (scheduling/RCCP layer), current bottleneck (TOC drum), most-recent valid vendor quote (RFQ/quote layer shipped in PR #420), routing rates + WC burden (cost layer — `ItemStandardCostElement`).
- **Outputs:** on BUY → hand to the Auto-PO engine (purchasing §16) / `ProductionSupplyDemand`; on MAKE → create/confirm the job/PRO. Subcontract ops route via `SubcontractOperation`.
- **⚠ Hardest dependency to confirm before committing F2 @ 0.30 weight:** can the finite scheduler return a projected Load% for a single routing's bottleneck across an arbitrary window? If not, F2 degrades to a coarser capacity proxy in v1 (open question §12).

---

## §5 — Cost-Accounting Model (when "the PO is the standard")

Research verdict: this is textbook **job-order (actual) costing** — "the predominant method for to-order companies." Not harder than standard costing; simpler (no perpetual item-master standard to maintain/revalue).

- **§5.1 Three-tier "standard":** **Estimate** locked at PO release (the variance baseline / the "standard" in ETO) → **Actuals** accrued during build (`CostTransaction`) → **Estimate-to-actual variance** at completion. Gives the BIC "did we hit the quote per job?" view. Variances are **order-scoped**, not item-master PPV.
- **§5.2 WIP & settlement:** WIP accumulates posted actuals; at completion revalue WIP→FG/COGS and recognize variance. Mirrors SAP order settlement / actual-costing WIP revaluation. (Substrate: Sprint 14.4 `IProductionVarianceCloseService` — confirm it supports *estimate-as-standard* baselines, not only item-master standards. Open question §12.)
- **§5.3 Do NOT repeat SAP CO07's mistake:** SAP material-less orders forfeit variance. We must collect **full actual cost on the material-less PO** (job-as-cost-object, like SAP settle-to-WBS / Infor project peg) so crystallization can seed a real baseline and the *next* order gets variance.
- **§5.4 Crystallized standard cost = first-unit actual.** Flag the minted master as *"ETO-originated, standard = first actual, unvalidated for repeat"* until a deliberate standard-set or a second run promotes it.

---

## §6 — MRP / Demand-Propagation without an Item Master

Research verdict: classic MRP (ROP/EOQ/safety-stock) **requires** a material master *by design* — which is exactly why order-/project-based planning exists.

- **§6.1 Independent-demand layer (needs master, gated):** ROP, EOQ, safety stock, MTS strategies. Do **NOT** route ETO demand through these.
- **§6.2 Dependent-/order-demand layer (no master planning data):** the **order BOM is exploded into dependent requirements**, scoped to the order. Emulate SAP **strategy 82** (order creates supply, no MRP run) and PD/WBS project planning (requirements bound to the order, segregated stock). **Already implementable on shipped substrate:** `ProductionMaterialStructure` (order BOM) + `ProductionSupplyDemand` (order-scoped demand records) + `PurchaseOrderLineDemandLink`.
- **§6.3 Components need only enough identity to source** (a purchased-material spec / drawing reference) — not a full planned master with lead-time/lot-size/safety-stock. Those fields matter only if/when a component is promoted to a repeat-planned item.

---

## §7 — Compliance Constraints (AS9100 / AS9102 / DFARS)

Research verdict: **traceability is record- and configuration-centric, never master-row-centric.** A one-off can be legally built/shipped identified by job/PO + drawing number, master created after — with these order-/record-centric constraints:

1. **Unique identification** (serial/lot) tied to **drawing number + revision** (AS9100D §8.5.2; AS9102 Form 1). Form-1 anchors = part # + part rev + drawing # + drawing rev — all attributes of the drawing/job, and the FAIR itself is generated *from the run* (crystallization-at-completion precedent).
2. **As-built / sequential record (traveler)** — operations, inspections, tests, AAM acceptance, retrievable on demand (§8.5.2 NOTE; §8.5.6). Retain as an **immutable as-built snapshot at ship**. (Substrate: B8 operation-transaction state machine + readiness/quality checks.)
3. **Configuration control** — drawing rev / ECO effectivity that governed the build, frozen to the job (§8.5.6). (Substrate: Sprint 14.3 ECR/ECO + 14.2 DMS.)
4. **Lot/heat/serial genealogy through the BOM** — each consumed material links to heat/lot + MTR/CoC proving melt source (**DFARS 252.225-7008/7009** specialty metals — Ti, Zr, Ni/Co alloys) and OCM source for electronics (**DFARS 252.246-7007/7008**). Records retained ≥3 yr after final payment (FAR 4.7). (Substrate: `ProductionMaterialStructure.IsHeatCertRequired` + supply-link.)
5. **⚠ Spec-copy correction (carry into product UI/tooltips/marketing):** specialty metals = **252.225-7008/7009**, NOT 252.225-7012 (that is the Berry Amendment — food/clothing/textiles). The original brainstorm cited 7012; fix everywhere.
6. **Crystallized master must not contradict as-built** — crystallized part #/rev = FAIR/traveler/shipper; genealogy preserved; reversible/auditable.

---

## §9 — Substrate Inventory: What's Already Shipped vs. The Gap

**Already shipped (verified against current `main`, 2026-05-29):**
- `ProductionOrder.ItemId` **nullable** → master-less build already structurally allowed. Carries cost cols + `ParentProductionOrderId` + snapshot fields.
- `Item.MakeBuyCode` (incl. `MakeOrBuy`), `PlanningPolicy` (incl. `EngineerToOrder`), `LifecycleStage` (Concept→…→Production→Obsolete), `IsSellable`, `IsStocked`, `ItemGroupId`, revision control fields.
- `ItemSourcingRule` (the make/buy rule table + split-sourcing + approval).
- `ProductionMaterialStructure` (per-PO frozen as-built BOM + 19-col supply-link + lot/serial/heat-cert flags + `ChildItemFingerprintHash`).
- `ProductionSupplyDemand` (order-scoped demand graph, 12-value `SupplyPolicy`, `BuyerActionState`) + `PurchaseOrderLineDemandLink`.
- Cost engine: `CostTransaction` (8-element, estimate/actual, `RollupAdditiveFlag` anti-compounding), `ProductionOrderCostSummary`, `ICostTransactionService`, Sprint 14.4 rollup + variance/close.
- DMS: `Document`/`DocumentVersion` (Drawing/MaterialCert/CoC types, effectivity, `ContentHash`, supersede chain). ECR/ECO change control.
- `SubcontractOperation` + full subcontract shipment/receipt/costing flow. RFQ/Quote flow with ranked comparison (PR #420).

**The gap B7 fills (the build surface):**
1. `Item.SourcePattern` enum (+ `MakeBuyPolicy`, `DefaultSourcePreference`, `IsSourceControlled`, break-even fields). [§2.4, §4.2]
2. `ItemCrystallization` entity + `IItemCrystallizationService` (preview / crystallize / reverse + dedupe). [§3]
3. `MakeBuyDecision` entity + `IMakeBuyDecisionService` (DecideAsync / ExplainAsync) + `MakeBuyDecisionPolicy` config. [§4]
4. Variance/close engine: confirm/extend estimate-as-standard baseline support. [§5.2]
5. Cockpit surfaces + Cherry Bar voice intents for both features (the demo money-shots). [§3, §4.3]

---

## §10 — Provisional Build Shape (NOT the cascade — for sizing only)

Sequencing intuition for the cascade-design step (to be detailed only after sign-off), GO-BIG, research-before-build honored:
- **Wave A — Source Pattern + master-optional foundation:** `SourcePattern` + policy enums on `Item`; PO-release path that allows/handles `ItemId == null`; gating so MTS/BTS/special-planning stay `StandardFirst`.
- **Wave B — Crystallization:** `ItemCrystallization` entity + service (preview/crystallize/reverse) + dedupe fingerprint + first-actual cost seeding + compliance guards + admin probe (write-buttons per Lock 16).
- **Wave C — Make-or-Buy decision service:** `MakeBuyDecision` entity + `IMakeBuyDecisionService` factor chain + hard gates + `MakeBuyDecisionPolicy` + explainable rationale + admin probe.
- **Wave D — Surfaces:** Cockpit crystallization panel + make/buy decision panel; Cherry Bar voice intents ("why are we buying this…", "crystallize this job into a standard").
Each PR: pre-PR subagent review, Codex resolution, Lock-16 E2E on dev preview, ship-memory write.

---

## §12 — Open Questions for Dean (decide before cascade design)

1. **`SourcePattern` default for new items:** keep classic `StandardFirst` as the global default (safest; ETO is opt-in per item/order), or default aerospace-job-shop tenants to `PoFirst`?
2. **Make-or-Buy F2 (live capacity):** is "projected finite Load% on a single routing's bottleneck across an arbitrary window" available from the current scheduler? If not, ship v1 with a coarser capacity proxy and upgrade F2 later — acceptable?
3. **Crystallization trigger:** strictly manual one-click at ship (recommended — honors "don't force standardization"), or also offer an auto-prompt when a `PoFirst` order completes?
4. **Dedupe aggressiveness:** auto-link on exact fingerprint match, or always present the match and let a human confirm (recommended for AS9100 traceability)?
5. **Variance baseline:** confirm the close engine should treat the **locked PO estimate** as the variance "standard" for `PoFirst` orders (vs. requiring an item-master standard).

---

## §13 — Key Sources (full lists in the three dossiers)

**SAP:** CO07 production order without material + settlement-to-cost-center (no variance); procurement type E/F/X + special procurement key; MTO/MTS strategies 10/20/40/50/82; valuated vs non-valuated sales-order/project stock; WIP revaluation in actual costing. (help.sap.com, community.sap.com, learning.sap.com.)
**Oracle Fusion:** Supply Chain Orchestration back-to-back + configured-item auto-create/dedupe; item lifecycle phases; sourcing rules/assignment sets; reorder-point planning set "when you define the item"; RCCP load%. (docs.oracle.com.)
**Epicor/Infor/Plex:** Make-Direct non-stock components, ship-from-WIP MTO, configurator part-# generation + dedupe, "make vs buy dynamic = custom code" (community); Infor project/demand pegging; Plex finite scheduler not wired to make/buy. (estesgrp.com, epiusers.help, community.epicorusers.org, docs.infor.com, plex.rockwellautomation.com.)
**PLM:** Aras + Teamcenter as-designed→as-planned→as-built→as-maintained, effectivity (date/unit/serial), 150%/100% BOM. (aras.com, blogs.sw.siemens.com.)
**Compliance:** AS9100D §8.5.2/§8.5.6; AS9102 Forms 1-3; DFARS 252.225-7008/7009 (specialty metals), 252.246-7007/7008 (counterfeit parts); FAR Subpart 4.7. (acquisition.gov, law.cornell.edu, advisera.com, qmii.com.)
**OR / cost:** TOC Drum-Buffer-Rope; RCCP load% = required_hrs/available_hrs×100; fully-loaded make vs landed buy; break-even = FixedInvestment/(BuyUnit−VarMakeUnit); job-order/actual costing predominant for to-order. (en.wikipedia.org, supplychainmath.com, visualsouth.com, accountingcoach.com.)

---

*End of B7 research spec. Cascade design (`b7-cascade-design.md`) to follow only after Dean signs off on this spec and answers §12.*
