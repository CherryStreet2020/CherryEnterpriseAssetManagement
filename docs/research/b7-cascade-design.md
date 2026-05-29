# Theme B7 — PO-as-Standard + Make-or-Buy Cascade Design

**Created:** 2026-05-29 session 23 · Claude (engineering lead) for Dean Dunagan
**Source spec:** `docs/research/po-as-standard-make-or-buy-dean-research.md` (the §-numbered B7 research spec)
**Codebase audit:** session 23 — verified Item, ProductionOrder (ItemId nullable), ProductionMaterialStructure, ProductionSupplyDemand, CostTransaction/Summary, SubcontractOperation, Document/DocumentVersion, scheduler substrate, SupplierQuote, ItemStandardCostElement against `main`.
**Current HEAD:** `1aada91` (PR #421 — Master Plan audit). Last code HEAD `c98015d` (PR #420, RFQ/Quote — CLOSES purchasing cascade).

---

## Dean's locked decisions (from spec §12, answered 2026-05-29)

1. **SourcePattern default = `StandardFirst` global.** ETO/PO-as-Standard is opt-in per item or per order. Lowest risk to existing flows.
2. **Crystallization trigger = manual one-click at ship.** Honors "don't force standardization."
3. **Dedupe = always human-confirm the fingerprint match.** No silent auto-linking of flight hardware (AS9100).
4. **Make-or-Buy F2 (live capacity) = ship v1 with a coarse capacity proxy, upgrade later.** Confirmed by codebase audit: no finite scheduler exists (backward-scheduler is a stub; no load/utilization store; no bottleneck concept). v1 proxy = scheduled-hours-in-window (from `ProductionOperation.PlannedStart/PlannedEnd`) ÷ static available-hours (WorkCalendar × `WorkCenter.UtilizationPct`), plus the live concurrent-ops signal from `OperationReadinessService`. "Bottleneck" in v1 = the highest-proxy-load WC on the routing, not a true plant-wide TOC drum.
5. **Variance baseline = locked PO estimate is the variance "standard" for PoFirst orders** (no item-master standard required).

---

## Design Principles (LOCKED)

> **The Item Master is OPTIONAL for ETO.** A `PoFirst` Production Order may carry `ItemId == null` at release. The PO's frozen BOM + Routing IS the standard during build. (`ProductionOrder.ItemId` is already nullable — no schema change needed to allow this.)

> **Crystallization is harvest-from-actuals, at ship, optional, reversible, dedupe-aware.** No incumbent does this. The crystallized record is simultaneously a PLM-style traceability artifact and an ERP-style plannable/costed standard.

> **Make-or-Buy is a decision SERVICE on top of the existing `ItemSourcingRule` TABLE.** It decides per-order and — the differentiator — explains the choice in plain language and persists it as an AS9100 audit record.

> **GO BIG. No fake data** (real mfg part numbers / vendors / costs — Ti-6Al-4V, Inconel 718, NADCAP finishers, 5-axis $175/hr). Every admin probe ships INSERT/UPDATE write-buttons (Lock 16 corollary). Pre-PR subagent review + Codex resolution + Lock-16 E2E on dev preview + ship-memory write, every PR.

---

## What Already Exists (the ~80% substrate)

| Domain | Entity/Service | B7 relevance |
|--------|---------------|--------------|
| Master-less build | `ProductionOrder.ItemId` **nullable**; `ParentProductionOrderId`; snapshot fields | PoFirst already structurally allowed |
| Make/buy flag | `Item.MakeBuyCode` (incl. `MakeOrBuy=2`, comment: "decision service routes…at run time") | The flag exists; nothing consumes it dynamically |
| Sourcing rules | `ItemSourcingRule` (MakeInternal/BuyFromVendor/Subcontract/Transfer + split + approval) | B7b decision service sits on top |
| Planning / lifecycle | `Item.PlanningPolicy` (EngineerToOrder=2), `LifecycleStage` (Concept→Production→Obsolete), `IsSellable`, `IsStocked` | Carve-out gating + crystallization target |
| As-built BOM | `ProductionMaterialStructure` (per-PO frozen, `ConsumedQuantity`, lot/serial/heat-cert flags, `ChildItemFingerprintHash`) | Crystallization source + dedupe fingerprint pattern |
| Order-scoped demand | `ProductionSupplyDemand` (12-value SupplyPolicy, BuyerActionState) + `PurchaseOrderLineDemandLink` | Supply driven off PO BOM, no item needed |
| Cost engine | `CostTransaction` (8-element, estimate/actual, anti-compounding), `ProductionOrderCostSummary`, Sprint 14.4 rollup + variance/close | First-actual crystallized cost + estimate-as-standard variance |
| DMS / change ctrl | `Document`/`DocumentVersion` (Drawing/MaterialCert/CoC, effectivity, ContentHash, supersede); ECR/ECO | Drawing/rev/effectivity for crystallization + AS9100 |
| Subcontract | `SubcontractOperation` + full ship/receipt/costing flow | Make-or-Buy → subcontract path |
| Quotes | `SupplierQuote`/`SupplierQuoteLine` (unit price, LeadTimeDays, ValidUntilDate, OTD%) | Make-or-Buy "most recent valid quote" input |
| Cost elements | `ItemStandardCostElement` (Material/Labor/VarOH/FixedOH/Subcontract/Setup/Tooling) | Make-or-Buy fully-loaded make cost |
| Scheduling | `BackwardSchedulingService` (stub, stamps PlannedStart/End); `WorkCenter` (static Efficiency/Utilization); `OperationReadinessService` (live concurrent-ops) | Make-or-Buy F2 **coarse proxy only** (decision #4) |

## What's Missing (the build surface)

| # | Gap | Spec § | Entities / Services | Wave |
|---|-----|--------|---------------------|------|
| 1 | SourcePattern + duality policy on Item | §2.4, §4.2 | enum fields on `Item` (no new entity) | A |
| 2 | Master-optional PO release path | §2, §6 | as-planned identity fields on `ProductionOrder` | A |
| 3 | Estimate-as-standard variance baseline | §5 | flag on `ProductionOrderCostSummary` + close-engine support | A |
| 4 | Crystallization record + fingerprint | §3.4 | `ItemCrystallization` | B |
| 5 | Crystallization service (mint/dedupe/reverse) | §3.5 | `IItemCrystallizationService` | B |
| 6 | First-actual cost seeding + lifecycle | §5.4 | (service path on minted Item) | B |
| 7 | Make-Buy decision record + policy | §4.4 | `MakeBuyDecision`, `MakeBuyDecisionPolicy` | C |
| 8 | Make-Buy decision service (factor chain) | §4.3 | `IMakeBuyDecisionService` | C |
| 9 | Make-Buy → supply integration | §4.5 | wire to Auto-PO / SupplyDemand / Subcontract | C |
| 10 | Cockpit crystallization panel | §3 | `_CockpitCrystallizationPanel` | D |
| 11 | Cockpit make-or-buy panel | §4.3 | `_CockpitMakeBuyPanel` | D |
| 12 | Cherry voice intents | §3, §4.3 | 2 voice intents | D |

---

## PR Cascade (~12 PRs across 4 waves)

### Wave A — Source Pattern + Master-Optional Foundation (PRs 1-3)

#### PR-1: `Item.SourcePattern` + Make-or-Buy duality policy fields (~6h)
- New enum `SourcePattern { StandardFirst=0, PoFirst=1, Hybrid=2 }` on `Item` (default `StandardFirst` per decision #1).
- New enums/fields on `Item`: `MakeBuyPolicy { MakeOnly, BuyOnly, MakeOrBuy, MakeWithBuyOverflow, BuyWithMakeBackup }`, `DefaultSourcePreference { Make, Buy, LetSystemDecide }`, `IsSourceControlled` (bool) + `SourceControlReason` (string), `MakeBreakEvenQty` (decimal?), `FixedMakeInvestment` (decimal?).
- **Enum DB default discipline (HARD LOCK):** `SourcePattern` default `StandardFirst=0` and `DefaultSourcePreference` default — both value-0 semantic defaults → `HasDefaultValue` safe. `MakeBuyPolicy` default is a real choice; confirm sentinel == 0 or set NO DB default (per the enum-defaults lock).
- Backfill: all existing items → `StandardFirst` (no behavior change).
- **Carve-out runtime guard:** an item that is `IsStocked`/MTS/BTS or carries special planning **cannot** be `PoFirst` — validate in the Item write path (the resolver-consults-IsSellable pattern).
- EF migration (`--output-dir Migrations`), entity config, admin probe `/Admin/SourcePatternProbe` (write-buttons: set pattern, set policy, attempt-invalid-carve-out → rejection demo).

#### PR-2: Master-optional PO release path (~8h)
- Confirm + harden `ProductionOrder` release when `ItemId == null` (PoFirst). Add as-planned identity fields the order carries in lieu of an item: `AsPlannedPartNumber` (string?), `AsPlannedDrawingNumber` (string?), `AsPlannedDrawingRev` (string?), `AsPlannedDescription` (string?), `IsPoFirst` (bool), `CrystallizedItemId` (int?, set later by Wave B).
- Release validation: PoFirst order requires drawing number + rev (AS9100 §8.5.2 anchor) but NOT an item.
- Supply generation already flows off the PO BOM (`ProductionSupplyDemand`) — verify it tolerates `ItemId == null` on the demand and on `ProductionMaterialStructure` child lines (component identity = drawing/spec ref).
- EF migration, entity config, admin probe (write-buttons: create PoFirst PRO with no item, attempt release without drawing → rejection, generate demands from master-less PRO).

#### PR-3: Estimate-as-standard variance baseline (~6h)
- `ProductionOrderCostSummary`: add `VarianceBaselineMode { ItemMasterStandard=0, LockedPoEstimate=1 }` (default `ItemMasterStandard`; PoFirst orders set `LockedPoEstimate` per decision #5) + `LockedEstimateCapturedUtc`.
- Extend `IProductionVarianceCloseService` to compute estimate-to-actual variance against the locked PO estimate when `VarianceBaselineMode == LockedPoEstimate` (order-scoped, not item-master PPV).
- Lock the estimate at PO release (capture into the summary) for PoFirst orders.
- EF migration, admin probe (write-buttons: lock estimate, post actuals, run close → estimate-to-actual variance shown).

### Wave B — Crystallization-at-Ship (PRs 4-6) — **the core differentiator**

#### PR-4: `ItemCrystallization` entity + structure fingerprint (~7h)
- New entity `ItemCrystallization` (§3.4): `SourceProductionOrderId`, `CrystallizationNumber` (two-phase `CRYST-YYYY-NNNNNN`), `Outcome { CreatedNewItem, LinkedToExisting, Rejected }`, `CreatedItemId?`, `MatchedItemId?`, `StructureFingerprintHash` (SHA-256 over as-built BOM+routing), `SeededStandardCost`, `CostSource { FirstActual, MovingAverage, ManualOverride }`, `AsBuiltDrawingNumber/Rev/EcoEffectivity`, `RationaleText`, `CrystallizedAtUtc/By`, `IsReversed`, `ReversalReason`, tenant trio, `RowVersion (xmin)`.
- Fingerprint compute helper (reuse `ChildItemFingerprintHash` SHA-256 pattern) over the PO's `ProductionMaterialStructure` lines + as-run routing.
- EF migration, entity config (enum defaults), admin probe (write-buttons: compute fingerprint for a PRO, create crystallization record stub).

#### PR-5: `IItemCrystallizationService` — preview / crystallize / dedupe / reverse (~12h) — **BIC differentiator**
- `PreviewCrystallizationAsync(proId)` — read-only: shows the would-be Item + standard BOM + standard Routing + standard cost, AND the dedupe match preview (fingerprint hit → "matches `TI-BRKT-0042 Rev C`").
- `CrystallizeAsync(proId, options)` — atomic (cross-service transaction enlistment per HARD LOCK): mint `Item` (`SourcePattern` transitions, `LifecycleStage=Production`) + standard BOM (from as-built `ProductionMaterialStructure`) + standard Routing (from as-run ops) + standard cost; OR link to dedupe match. Two-phase `CRYST-YYYY-NNNNNN`. Sets `ProductionOrder.CrystallizedItemId`.
- **Dedupe = always human-confirm (decision #3):** never auto-link; surface the match, require explicit confirm-link-vs-create.
- **Compliance guards (§7):** crystallized part #/rev MUST equal FAIR/traveler/shipper value; lot-serial genealogy preserved; reject if contradiction. Crystallization is master-data convenience, NOT a compliance event.
- `ReverseCrystallizationAsync(crystallizationId, reason)` — reversible; never rewrites as-built history.
- Admin probe (write-buttons: preview, crystallize-new, crystallize-with-dedupe-confirm, attempt-contradicting-rev → rejection, reverse).

#### PR-6: First-actual cost seeding + ETO-originated lifecycle flagging (~6h)
- Seed minted Item standard cost = first-unit actual from `ProductionOrderCostSummary` (§5.4); break into `ItemStandardCostElement` rows (Material/Labor/OH/Setup/Tooling) from the actual buckets.
- Flag minted master: `StandardCostBasis { Forecast, FirstActual, Validated }` = `FirstActual`, "unvalidated for repeat" until a 2nd run or deliberate standard-set.
- Wire `SourcePattern` of the minted Item (`PoFirst`→ becomes a real repeat candidate; remains `PoFirst` or transitions to `StandardFirst` on validation).
- Admin probe (write-buttons: seed cost from actuals, set validated, show cost-element breakdown).

### Wave C — Make-or-Buy Decision Service (PRs 7-9)

#### PR-7: `MakeBuyDecision` entity + `MakeBuyDecisionPolicy` config (~7h)
- New entity `MakeBuyDecision` (§4.4): `ItemId`, `SiteIdSnapshot`, `Qty`, `DueDate`, `DecidedAtUtc`, `Context { ProductionOrder, ParentPoBomLine, Mrp, ManualWhatIf }` + `SourceType`/`SourceId`, `Outcome { Make, Buy }`, `BuyScore`, `Confidence`, `WasHardGated` + `HardGateReason`, `RationaleText`, `FactorBreakdown` (jsonb F1–F6), frozen snapshots (`MakeCostFullyLoaded`, `BuyCostLanded`, `BottleneckWorkCenterCode`, `BottleneckLoadPct`, `RoutedThroughDrum`, `MakeCompletionDate`, `VendorDeliveryDate`, `ChosenSupplierId`, `ChosenQuoteId`), tenant trio, `RowVersion (xmin)`.
- New entity `MakeBuyDecisionPolicy` (per-tenant/site): `CapacityThresholdPct` (85), `DrumOffloadCostTolerancePct` (8), `BuyDecisionScoreThreshold` (0.50), factor weights F2..F6, `FinalTieBreak { PreferMake, PreferBuy }`.
- EF migration, entity config, admin probe (write-buttons: create policy, create decision record stub).

#### PR-8: `IMakeBuyDecisionService` — factor chain + explainable rationale (~12h) — **the money-shot**
- `DecideAsync(itemId, qty, dueDate, siteId?, context?)` — evaluates F1–F6 (§4.3):
  - **F1 eligibility gate (hard):** policy allows path; valid routing for make; ≥1 approved supplier + valid quote (`SupplierQuote.ValidUntilDate >= today`) for buy.
  - **F2 capacity (v1 COARSE PROXY per decision #4):** for the candidate WC, sum `PlannedRunMins+PlannedSetupMins` of operations overlapping [today, dueDate] ÷ available hours (WorkCalendar × `WorkCenter.UtilizationPct`); blend with live concurrent-ops signal (`OperationReadinessService`). "Bottleneck" = highest-proxy-load WC on the routing. Snapshot fields stay (so the F2 upgrade to a true finite scheduler later is drop-in).
  - **F3 cost delta:** fully-loaded make (`ItemStandardCostElement` Labor+OH+Setup+Tooling+Material) vs landed buy (`SupplierQuote` unit×qty + freight + receiving + risk).
  - **F4 break-even:** `BE = FixedMakeInvestment / (BuyUnit − VarMakeUnit)`.
  - **F5 lead-time fit:** make completion (planned) vs vendor delivery (`SupplierQuote.LeadTimeDays` + transit + inspection) vs dueDate.
  - **F6 quality/risk:** supplier OTD/quality, NADCAP/AS9100 cert match, single-source risk.
  - **Hard gates:** only-feasible-path; drum-offload (force BUY within `DrumOffloadCostTolerancePct`); `IsSourceControlled` → force MAKE.
  - Aggregate `BuyScore = Σ(wᵢ·scoreᵢ)`; BUY ≥ 0.50; tie-breaks (drum → slack → cost → prefer make).
  - **Rationale:** sort factors by `wᵢ·|scoreᵢ−0.5|`, emit top 2-3 plain-English lines + persist full `FactorBreakdown`.
- `ExplainAsync(decisionId)` — re-render persisted decision (audit / Cherry Bar).
- Admin probe (write-buttons: decide for a MakeOrBuy item with real Ti-bracket fixture → BUY rationale; flip capacity/quote → MAKE; source-controlled → forced MAKE; what-if).

#### PR-9: Make-or-Buy → supply integration (~6h)
- Invoke `DecideAsync` for `MakeBuyCode.MakeOrBuy` items at PO release / demand generation.
- On **BUY** → hand to Auto-PO engine (purchasing §16) / set `ProductionSupplyDemand.SupplyPolicy`; subcontract ops route via `SubcontractOperation`.
- On **MAKE** → create/confirm child job/PRO.
- Persist the `MakeBuyDecision` against the demand/PO line for traceability.
- Admin probe (write-buttons: end-to-end decide→create-PO; decide→create-job).

### Wave D — Surfaces + Voice (PRs 10-12)

#### PR-10: Cockpit Crystallization panel (~6h)
- `_CockpitCrystallizationPanel.cshtml` on a completed PO: preview card (would-be Item + standard BOM/routing/cost), dedupe-match banner with human-confirm, crystallize button, reverse. Wire into the existing Cockpit transaction drawer.

#### PR-11: Cockpit Make-or-Buy panel (~6h)
- `_CockpitMakeBuyPanel.cshtml`: per-demand/operation decision card — Outcome badge (MAKE/BUY), confidence, the ranked rationale lines, the F1–F6 factor breakdown table, supervisor override. Wire into Cockpit routing/BOM row click.

#### PR-12: Cherry Bar voice intents — **CLOSES B7** (~6h)
- Intent `ExplainMakeBuyDecision`: *"why are we buying this bracket this run?"* → reads `MakeBuyDecision.RationaleText` + `FactorBreakdown`.
- Intent `CrystallizeJobToStandard`: *"crystallize this job into a standard"* → invokes `PreviewCrystallizationAsync` and surfaces the confirm flow.
- E2E both intents on dev preview.

---

## Estimated Total Scope

| Wave | PRs | New entities | New services | Hours |
|------|-----|-------------|-------------|-------|
| A — Source Pattern + master-optional | 3 | 0 (field adds) | 0 (extends close svc) | ~20h |
| B — Crystallization | 3 | 1 (`ItemCrystallization`) | 1 (`IItemCrystallizationService`) | ~25h |
| C — Make-or-Buy decision | 3 | 2 (`MakeBuyDecision`, `…Policy`) | 1 (`IMakeBuyDecisionService`) | ~25h |
| D — Surfaces + voice | 3 | 0 | 0 | ~18h |
| **Total** | **12** | **3** | **2** | **~88h** |

~3-4 sessions at current velocity (3-4 PRs/session). Smaller than the 20-PR purchasing cascade because ~80% of the substrate already shipped in B6/B8/Sprint 14.x/15.x.

---

## Key Architectural Decisions

1. **No new entity in Wave A** — SourcePattern + duality are field additions on `Item`/`ProductionOrder`/`ProductionOrderCostSummary`. The master-less build path already exists (`ItemId` nullable); Wave A hardens and gates it.
2. **Crystallization is the flagship** (Wave B) — harvest-from-actuals at ship, dedupe (human-confirm), first-actual cost, reversible, compliance-guarded. This is the claim no incumbent can make.
3. **Make-or-Buy F2 ships as a coarse proxy** (decision #4) with the snapshot fields in place so the upgrade to a true finite scheduler (future Sprint 14 backward-scheduler completion) is drop-in — no entity change.
4. **Explainability is a first-class persisted artifact** — `MakeBuyDecision.RationaleText` + `FactorBreakdown` is the demo money-shot AND the AS9100 audit record AND the Cherry Bar voice answer, in one record.
5. **Cross-service transaction enlistment** (HARD LOCK) governs `CrystallizeAsync` (composes cost + BOM + routing services inside one tx) and the Make-or-Buy → supply handoff.
6. **Carve-outs are enforced, not optional** — MTS/BTS/special-planning items are runtime-guarded to `StandardFirst`; crystallization never rewrites as-built history; specialty-metals copy uses DFARS 7008/7009 (not 7012).

---

*Cascade design complete. On Dean's go, Wave A PR-1 (`Item.SourcePattern` + duality policy) starts the build — full ship cycle per `/cowork-github-replit-process`, pre-PR subagent, Lock-16 E2E, ship-memory write per PR.*
