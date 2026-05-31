# CherryAI EAM — Best-in-Class Master Plan
**Created:** 2026-05-15
**Repo:** CherryStreet2020/CherryEnterpriseAssetManagement @ commit `9852447` (post-PR-#99)
**Goal:** Close every gap surfaced by [AUDIT_SUMMARY.md](./AUDIT_SUMMARY.md) and reach competitive-parity-or-better with IBM Maximo, SAP PM/MM/FI, Infor EAM in 90 days.

---

## 📊 Progress dashboard

> **Updated 2026-05-31 (CURRENT — through session 33).** Main HEAD = **`b60ea93`** (PR #472 — B9 Wave 6 PR-18 service handoff + warranty + AI project review · CLOSES B9). Sessions 28–33 shipped **B7 Wave D (CLOSES B7) and all of Theme B9 (the Customer Project Manager) across 6 waves / 18 PRs (#455–#472).** ~120 total ships since the B6 Foundation Sprint · ~472 merged PRs to main.
>
> **🎉 THEME B9 — Customer Project Manager — is COMPLETE (the headline).** The BIC, SAP/Oracle/Epicor-beating project spine, built end-to-end on the existing manufacturing-execution substrate. Full loop: **quote → estimate → contract/award → WBS → schedule → Gantt/critical-path (CPM) → procurement → labor/expense → live margin (EAC) → quote-vs-actual → billing/invoice/rev-rec → change control → governance (RAID + meetings) → quality (inspection/NCR/MRB/punch/acceptance) → service handoff/warranty**, with an **AI project review** over the whole thing and the Command Center + "can we still hit the promise?" indicator + lifecycle graph as the money-shot.
> - **W1 (#455-457):** Project Command Center · promise indicator · lifecycle graph (the EVS demo trio).
> - **W2 (#458-460):** quote-to-cash spine — RFQ/Quote/Revision (locked submitted snapshot) · Estimate/Snapshot (frozen cost model) · Contract/award→baseline (two §20 gates).
> - **W3 (#461-463):** WBS hardening on ProjectPhase · Milestone/Task/Dependency · Gantt + real CPM (forward/backward pass, float, critical path) server-rendered SVG + voice.
> - **W4 (#464-465):** procurement spine (PO project peg + Plan/Commitment/Receipt + project-close commitment gate) · resource/labor/expense (planned-vs-actual).
> - **W5 (#466-468):** financials/margin engine (Budget/Actual/Forecast/EAC, live Contract−EAC margin, committed wired from commitments) · quote-vs-actual (frozen estimate vs live EAC by 5 cost buckets) · billing/invoice/rev-rec (milestone + acceptance invoicing gates).
> - **W6 (#469-472):** change control (ChangeRequest→change order extending ProjectAmendment + §20 convert gate) · governance (Risk/Issue/ActionItem/Decision/Meeting RAID) · quality + acceptance (Inspection/NCR/MRB/Punch/Acceptance + §22.4 acceptance gate that flips the billing AcceptanceConfirmed) · service handoff/warranty + AI project review + equipment closeout gate (enforced on every close path). CLOSES B9.
>
> **🎉 B7 (PO-as-Standard + Make-or-Buy) also CLOSED** earlier in this block — Wave D (cockpit/voice) surfaced the make-or-buy verdict + ExplainAsync rationale in the Production Cockpit and the Cherry Bar voice layer, closing the theme.
>
> **What's genuinely next — B10 (Teams Collaboration) is the frontier; nothing else in-flight, await Dean.** **B10 Teams Collaboration + contextual messaging** (`reference_dean_brainstorm_teams_collab_2026_05_27.md`). Longer-queued: **Sprint 12.5** Enterprise Hardening (MFA/SSO/Postgres-RLS) · **Sprint 18** Maintenance Command Center (Calibration/OEE/RCM-FMEA/Weibull) · **Sprint 12D** Apache AGE graph layer · **Sprint 14.5** unified Item view. A pre-EVS **Repomix-based architecture/security second-opinion pass** is recommended closer to the demo.
>
> **Gates:** 🔴 **Prod (industryos.app) Republish-with-Copy owed for B9 Waves 4-6 (#465–#472)** — dev-preview-verified, not yet on prod (Lock-14, Dean runs Republish-with-Copy). **EVS pitch gate Wed June 3 — open;** the margin engine + quote-vs-actual + billing + change/governance/quality/handoff all want this live. Per-PR ship records in memory `project_pr455_shipped` … `project_pr472_shipped`; canonical distilled audit in `reference_master_plan_audit_2026_05_30`.
>
> **Codex/subagent ledger (sessions 33).** Codex caught a REAL finding on nearly every B9 Wave 6 PR, all fixed pre-merge: #469 conversion must be ONE atomic SaveChanges (set the navigation + parent xmin token, no orphan amendment); #470 a soft polymorphic Type+Id reference still needs per-type tenant-scoping; #471 NCR negative-quantity guard + matching CHECK; #472 (a) a new close gate must be enforced on EVERY close path (UpdateStatusAsync AND the procurement waiver path), (b) a signed-off handoff is set-once. Recurring build/migration lessons: enum DB default must == the 0 enum member (reorder the enum, don't set a non-zero default); after `migrations remove --force` + re-add, `git add -A Migrations/` to stage the old-file deletion before the harness re-push, and update the config FILES to the new timestamp; a Replit restart can drop the dev-preview auth session → re-auth via the demo Admin role-autofill tile before E2E.

> **Updated 2026-05-30 (session 27).** Main HEAD = **`89f4121`** (PR #448 — B7 Wave C PR-9 make-or-buy supply integration · CLOSES Wave C). Session 27 shipped **11 PRs (#440–#448)** and closed **two big arcs back-to-back: B11 Wave R4 (the finite scheduler) and B7 Wave C (make-or-buy end-to-end).**
>
> **🎉 B11 Wave R4 — the Finite Scheduler — is COMPLETE (headline #1).** This retires the wall-clock stub with a real calendar-and-capacity-aware scheduler — the operations disruptor. **R4-10 `d86a82e` (#440)** `IResourceLoadService`: a calendar-aware working-time engine (WorkCalendar week + holidays + per-resource exception windows; DST-safe; subtract-downtime → scale-reduced → union-extra-shift) computing real **Load%** (committed setup+run hrs ÷ available working hrs) per WC/resource + **drum** (bottleneck) detection. **R4-11 `d28463b` (#441)** `IFiniteSchedulingService`: ops floored into real Mon-Fri working windows; **peak-concurrency** contention vs `SimultaneousOperationsMax`; an overloaded primary WC spills to an R3-9 capability alternate → `WorkCenterAlternate`, each candidate re-floored on its OWN calendar and capacity-rechecked; backward/forward + what-if (commit:false = no mutation). **R4-12 `4fcc1a8` (#442)** `IDispatchBoardService`: per-WC queue ordered by that WC's `WorkCenterDispatchRule` (FIFO/EDD/SPT/CriticalRatio/MinSlack/HighestPriority) + `DispatchNextAsync` routed through `IProductionOperationTransactionService.StartSetupAsync`. Lock-16 E2E live on all three (drum MILL 111%; seq20/30 spill MILL-A→MILL-B across the non-working gap; 3 jobs → 3 different run orders).
>
> **🎉 B7 Wave C — Make-or-Buy — is COMPLETE and end-to-end (headline #2).** The decision disruptor: an item flows from "should we make or buy this?" to an actual purchase hand-off or a created job, fully auditable. **PR-7 `5d8106b` (#443)** `MakeBuyDecision` + `MakeBuyDecisionPolicy` schema (outcome + BuyScore + hard-gate + rationale + frozen factor snapshots; two filtered unique indexes for company-default vs per-site policy; xmin). **PR-8 `ee75c36` (#446) + hotfix `bf7c332` (#447)** `IMakeBuyDecisionService.DecideAsync` — the six-factor engine: F1 eligibility · **F2 capacity (consumes the REAL R4-10 Load%)** · F3 cost (effective-dated standard vs best landed quote) · F4 break-even (per-ORDER setup ÷ per-unit buy premium) · F5 lead-time · F6 OTD → weighted BuyScore, with hard gates (source-controlled→MAKE, drum-offload→BUY); `ExplainAsync` re-hydrates. (Hotfix: read-back-only JSON column = `text`, not `jsonb` — string→jsonb insert threw 22P02.) **PR-9 `89f4121` (#448)** `IMakeBuyFulfillmentService.FulfillDemandAsync` — wires the verdict into supply: BUY → `SupplyPolicy=BuyDirectToJob` + stamp chosen vendor + buyer action Open (Auto-PO hand-off); MAKE → `MakeDirectToJob` + idempotent child production order + supply-back link. Lock-16 E2E live: demand #4 → BUY (vendor #21) → toggle source-controlled → MAKE (child PRO #21), path-switch clean.
>
> **Plus 3 UI/IA fixes this session:** WorkCenters **Details** page (#444, fixed the live 404 on `/Admin/WorkCenters/Details/{id}`), the Work Centers card moved under **Master Files** on /Admin per Dean's IA call, and the WorkCenter **Create/Edit** form (#445, writes through `IWorkCenterService` with the full scheduling/dispatch group).
>
> **What's genuinely next — B7 Wave D is the frontier; nothing else in-flight, await Dean.** **B7 Wave D (cockpit/voice) CLOSES Theme B7:** surface the make-or-buy decision + `ExplainAsync` rationale in the Production Cockpit and route a "why make vs buy" intent through the Cherry Bar voice layer. Longer-queued: B9 Customer Project Manager (~100-150h greenfield) · B10 Teams Collaboration · Sprint 12.5 Enterprise Hardening (MFA/SSO/Postgres-RLS) · Sprint 18 Maintenance CC · Sprint 12D Apache AGE graph · Sprint 14.5 unified Item view.
>
> **Gates:** ⚠️ **Prod (industryos.app) Republish-with-Copy owed for ALL of B7 (#422–#448) + B11 (#429–#442)** — everything since the last Wave-4 prod verify is **dev-preview-only**; a Lock-14 Republish-with-Copy is required to put B7/B11 on prod. **EVS pitch gate Wed June 3 — still open.** **~95 total ships since B6 Foundation Sprint · ~448 merged PRs to main.**
>
> **Codex/subagent ledger this run:** real catches fixed pre-merge — #442 dispatch-next must route through the transaction service (never flip Status directly), #443 nullable (CompanyId,SiteId) unique index → two filtered unique indexes, #446 F4 break-even per-ORDER setup units + cost effective-dating, #448 stamp vendor on BUY + clear stale make-link on path switch. Build/env lessons: re-grep `: error` not a narrow filter (CS1061 slipped a build filter); jsonb-from-string insert = 22P02 (use text for read-back-only JSON); Replit local main diverges → `git reset --hard origin/main`; Replit lacks IANA tzdata → UTC fallback (load/schedule math is tz-agnostic so unaffected).
>
> ---
>
> **Updated 2026-05-29 PM (sessions 25 + 26).** Main HEAD = **`65ab87f`** (PR #438 — R3-9 capability-match probe). Since the Purchasing Cascade closed (#420) and the master-plan audit shipped (#421), the codebase executed **two new manufacturing-core themes back-to-back: Theme B7 (PO-as-Standard + Make-or-Buy) Waves A+B, and Theme B11 (Resource Model & Finite Scheduler) Waves R1→R3.** 17 feature PRs (#422–#438) merged.
>
> **🎉 B11 Wave R3 — the Capability Model — is COMPLETE and demonstrably working (the headline).** This is the scheduling disruptor: a routing operation declares the *capabilities it requires*; the scheduler matches *eligible resources* by capability + currency + proficiency + envelope — instead of pinning one machine on the routing step the way Epicor / MIE / Plex do. Shipped as: **R3-7 `0e1a4d7` (#435)** `Capability` master + `ResourceCapability` join (qualification + expiry; geometric caps never lapse, special-process certs do). **R3-8 `8f64700` (#436)** `OperationCapabilityRequirement` — FK-backed, retires the loose CSV `RequiredSkillCodes`/`RequiredToolingIds`. **R3-9 `b91a202` (#437) + `65ab87f` (#438)** `ICapabilityMatchService.GetEligibleResourcesAsync(opId)` → eligible resources (holding ALL mandatory caps, current, at proficiency, satisfying envelope + pinned-tool, on an Active resource), ranked by proficiency; ineligible returned with the exact blocking reason. Lock-16 E2E live: op #36 → 2 eligible ranked (Haas-A 18, Haas-B 8) + 4 ineligible (cert expired / envelope 3<5 / capability not held) + 1 excluded (Inactive).
>
> **B11 R1 + R2 (the substrate beneath R3) also shipped this run:** **R1 Org backbone** — Department production-org backbone (#429), WorkCenter scheduling hardening + canonical SiteId + WorkCenterAlternate (#430), WC cost + operation-default groups (#431, CLOSES R1). **R2 Resource layer** — `ProductionResource` entity + EAM `Asset` bridge (#432), Tool/Fixture + Labor/Vendor/Tool bridges (#433), per-resource calendars + finite-capacity envelope (#434, CLOSES R2). Net: the finite scheduler (R4) now has its three inputs — *what a resource is* (R2-4/5), *when it's available* (R2-6 calendars + exception windows, incl. the production↔EAM-maintenance graph link), *how much it can take* (R2-6 finite envelope) — plus *who can run an op* (R3-9).
>
> **Theme B7 Waves A+B shipped (the ETO/crystallization generalization of B6):** Wave A — `Item.SourcePattern` Make-or-Buy duality (#422), master-optional (PoFirst) Production Order release (#423), estimate-as-standard variance baseline (#424). Wave B — `ItemCrystallization` entity + structure fingerprint (#425/#426), `IItemCrystallizationService` the BIC differentiator (#427), first-actual cost seeding + `StandardCostBasis` (#428, CLOSES Wave B). An ETO job can now quote → release master-less → cost-close → crystallize a reusable costed standard Item at ship.
>
> **What's genuinely next — R4 is the frontier; nothing else in-flight, await Dean.** **B11 R4 — the finite scheduler (PRs 10-12):** `IResourceLoadService.GetProjectedLoadAsync` → real Load%/drum (replaces B7's coarse proxy) → finite backward scheduler replacing the `BackwardSchedulingService` stub (calendar + finite-capacity aware, uses R3-9 for capability-based alternate selection) → dispatch board. **Then B7 Wave C make-or-buy** (the `IMakeBuyDecisionService` F2 capacity factor consumes R4's real finite Load%) + Wave D (cockpit/voice) CLOSES B7. Longer-queued: B9 Customer Project Manager (~100-150h greenfield) · B10 Teams Collaboration · Sprint 12.5 Enterprise Hardening (MFA/SSO/Postgres-RLS) · Sprint 18 Maintenance CC · Sprint 12D Apache AGE graph · Sprint 14.5 unified Item view.
>
> **Gates:** ⚠️ **Prod (industryos.app) Republish-with-Copy still owed** — everything since the last Wave-4 prod verify (B7 + B11, #422-438) is **dev-preview-only**; a Lock-14 Republish-with-Copy is required to put B7/B11 on prod. **EVS pitch gate Wed June 3 — still open.** **~84 total ships since B6 Foundation Sprint · ~438 merged PRs to main.**
>
> **Codex/subagent ledger this run:** real catches fixed pre-merge — #436 multi-company write attribution (stamp child writes with the parent op's `Routing.CompanyId`, not `VisibleCompanyIds.First()`), #437 pinned-tool not honored in the matcher. Recurring false-positives (replied + resolved, no change): xmin-DDL "P1" (Npgsql maps the xmin system column), gitignored `.ship/msgs/*` flagged "missing". Build catches: `[NotMapped]` on a method = CS0592; `decimal?.ToString()` doesn't translate inside an EF `.Select` (format in C# after materialize).
>
> ---
>
> **Updated 2026-05-29 AM (Purchasing Cascade close).** Main HEAD = **`c98015d`** (PR #420 — RFQ/Quote Flow + ranked comparison · CLOSES the 20-PR Purchasing Cascade). **🎉 THE 20-PR PURCHASING CASCADE IS COMPLETE (20/20).** Sprint 15.1 Wave 1 Foundation (5 PRs #396–#400) + Sprint 15.2 Wave 2 Subcontract Flow (4 PRs #404–#407) + Sprint 15.3 Wave 3 Purchasing Command Center (6 PRs #409–#414) + **Sprint 15.4 Wave 4 Polish + Integration (5 PRs #416–#420 — NEW this session).** The whole procure-to-source loop is live on the Replit dev preview: Receipt-to-Job → unified `ProductionSupplyDemand` → PO↔demand link → subcontract dual-demand + vendor WIP → §21 Purchasing Control Center (12 live tabs) → auto-PO + consolidation + buyer recommendation → PO acknowledgment → PO amendment → vendor scorecard → 3-way match → RFQ ranked award → PO (the loop closes back to supply demand).
>
> **Session 22 (this session) deliverables — Wave 4 (5 PRs / ~64 bugs squashed pre-merge across pre-PR subagent + Codex):** (1) **PR #416 PO Acknowledgment / Vendor Confirmation** `421691b` — POAcknowledgment + line + 3 enums + 11-op service, two-phase numbering, filtered-unique IsCurrent. (2) **PR #417 PO Amendment / Change Order · BIC differentiator** `e9d881b` — POChangeHistory + `PreviewAmendmentImpactAsync` (demand-link impact preview) + atomic `ApplyAmendmentAsync` that opens a vendor re-ack INSIDE the amendment transaction — first use of the **cross-service transaction enlistment** pattern. (3) **PR #418 Vendor Performance / Scorecard** `ced68e4` — `SupplierPerformance` computed snapshot (OTD% / quality PPM / price variance % / NCR count per Vendor×period) + §21 tab 13 + `GetCompositeInputsAsync` hook. **Delivers legacy Sprint-3 item #129 Vendor scorecards.** (4) **PR #419 3-Way Match (PO↔Receipt↔Invoice)** `7593327` — `InvoiceMatchResult` + tolerance-driven `IInvoiceMatchService` + atomic match-and-AP-post (cross-service enlistment) + Cost Exceptions tab feed. Promotes the legacy enum-only matcher to an active engine. (5) **PR #420 RFQ/Quote Flow + ⭐ ranked comparison** `c98015d` — 4 entities + `RankQuotesAsync` blended composite (Price 50 / LeadTime 30 / SupplierOTD 20 from PR-18, price+lead fallback when no OTD snapshot) + winner badge + reason text + `ConvertQuoteToPoLineAsync` carrying §17 demand links + §21 "Supplier RFQs" tab. **CLOSES the cascade.** All 5 Lock-16 E2E-verified live (E2E proof in `project_pr416..420_shipped.md`).
>
> **⭐ NEW HARD PATTERN this session — cross-service transaction enlistment:** when service A composes service B inside A's transaction, B must share the scoped `AppDbContext` and use a bare `SaveChanges` (no own `BeginTransaction`) so its writes enlist in A's open transaction; the `OutboxWriter` shares the same context and enlists too. A self-committing callee would split-brain a rollback. PR-17 (amendment→re-ack) and PR-19 (match→AP-post) both depend on this. Memory: `project_pr417_shipped.md` / `project_pr419_shipped.md`.
>
> **What's genuinely next (post-cascade) — NO work queued, await Dean.** Open research/brainstorm themes: **B7** PO-as-Standard + Make-or-Buy duality (`reference_dean_brainstorm_b7_2026_05_26.md`) · **B9** Customer Project Manager Module, ~100-150h greenfield (`reference_dean_brainstorm_b9_2026_05_26.md`) · **B10** Teams Collaboration + contextual messaging (`reference_dean_brainstorm_teams_collab_2026_05_27.md`). Long-queued hardening still open: **Sprint 12.5** Enterprise Hardening (MFA/SSO/Postgres-RLS) · **Sprint 18** Maintenance CC (Calibration/OEE/RCM-FMEA/Weibull) · **Sprint 12D** Apache AGE graph layer ("why is X late" demo). **EVS pitch gate = Wed June 3 (5 days out) — still open.** ⚠️ **Prod (industryos.app) NOT republished for Wave 4** — all of Sprint 15.4 (PO ack / amendment / scorecard / 3-way match / RFQ) is dev-preview-only; a Lock-14 Republish-with-Copy is required to put it on prod. **~67 total ships since B6 Foundation Sprint · ~420 merged PRs to main.**
>
> ---
>
> **Updated 2026-05-28 PM.** Main HEAD = **`28a0fc7`** (PR #414 — Buyer Recommendation Engine §18 · CLOSES Wave 3 of the Purchasing Cascade). **Session 21 (this session) shipped 4 PRs (#411–#414) and CLOSED Sprint 15.3 Wave 3 — the §21 Purchasing Command Center. Combined with Wave 1 (Session 18, 5 PRs) and Wave 2 (Session 19, 4 PRs) and Wave 3 opening (Session 20, 2 PRs: #409+#410), all 15 of the 20-PR Purchasing Cascade's foundation + flow + command-center layers are complete in 4 sessions. The full §21 10-tab Purchasing CC IA + §16 decision engine + §17 6-mode consolidation planner + §18 recommendation engine are LIVE on the user-facing `/Purchasing/ControlCenter` page over real tenant data.**
>
> **Session 21 deliverables:** (1) **PR #411 Sprint 15.3 PR-12 CC tabs 3-6** `75f66d2` — Subcontract / Vendor WIP / Receipts / Inspection Holds. 4 new tab-specific read methods on `IPurchasingControlCenterService`. LiveTabs 2→6. 5 bugs squashed pre-merge (3 P2 pre-PR + 2 Codex P2: inspection-holds Skip-split off-by-source + GR-side SiteId leak). (2) **PR #412 Sprint 15.3 PR-13 CC tabs 7-10** `5e8447f` — POs / Expedites / Approvals / Cost Exceptions. **CLOSES the full §21 10-tab IA.** 2 new tab-specific reads + 2 demand-grid dispatches. LiveTabs 6→10. 6 bugs squashed pre-merge (5 P2 pre-PR + 1 Codex P2: over-aggressive CostExceptions TotalCount clip). POs tab shows 238 real POs / $3.67M / 18 pending / 118 late. (3) **PR #413 Sprint 15.3 PR-14 Auto-PO + Consolidation engines** `cc7b35e` — 2 new pure-read services: `IAutoPurchaseService` (§16 — 10 trigger / 12 blocker decision matrix per demand) + `IDemandConsolidationService` (§17 — 6 modes preserving per-demand allocations). 10 bugs squashed pre-merge (3 P1 + 5 P2 pre-PR + 2 Codex P2: PlanProject VendorId missing from group key + SuggestModeAsync distinctVendors null-filter). E2E live: Evaluate PRO #10 returns 2 demands BlockedReviewRequired correctly catching Wave 1 PR-4 dual-demand subcontract pattern. (4) **PR #414 Sprint 15.3 PR-15 Buyer Recommendation Engine · CLOSES WAVE 3 🎉** `28a0fc7` — `IPurchasingRecommendationService` with 15-value RecommendedAction enum (11 §18 patterns + 4 fall-through) × 4-value RecommendationRisk. Composes PR-14 `IAutoPurchaseService` for §16 context. Wired into `PurchasingControlCenterService.GetSupplyDemandQueueAsync` so the Purchasing CC "NEXT ACTION" column shows real §18 hints. 5 bugs squashed pre-merge (1 P1 BuildFromDemand param overload + 1 P2 phantom autoPo dep pre-PR + 3 Codex P2: transfer-policy ordering, awaiting-approval fall-through, OnlyAction over-fetch limit). Lock 16 E2E live: PRO #10 returns 2 CreateSubcontractPo recommendations (Low/Medium by days-until-required); user-facing Supply Demand queue now shows `Create subcontract PO: Subcontract operation released — create the service PO and prepare WIP shipment.` per row, replacing the PR-10 placeholder.
>
> **Wave 3 total: 6 PRs / 43 bugs squashed pre-merge** (PR-10: 11 / PR-11: 6 / PR-12: 5 / PR-13: 6 / PR-14: 10 / PR-15: 5). All E2E-verified live on Replit dev preview. **62 total ships since B6 Foundation Sprint began. ~414 merged PRs to main.**
>
> **Wave 4 next: Polish + Integration (PRs 16-20, ~34h).** PR-16 PO Acknowledgment + Vendor Confirmation (POAcknowledgment entity, lifecycle Sent→Acknowledged→Confirmed) · PR-17 PO Amendment / Change Order (POChangeHistory + change-reason enum + re-approval workflow) · PR-18 Vendor Performance / Scorecard (SupplierPerformance entity + new §21 tab 13) · PR-19 3-Way Match (IInvoiceMatchService promoting the enum-only stub to an active matching engine) · PR-20 RFQ / Quote Flow (SupplierRFQ + SupplierQuote entities + convert-winning-quote-to-PO + Supplier RFQs tab — CLOSES the 20-PR cascade).
>
> ---
>
> **Updated 2026-05-28 evening (Session 19).** Main HEAD = **`f3bec39`** (PR #407 — Cockpit Subcontract Panel · CLOSES Wave 2). **Session 19 shipped 4 PRs (#404–#407) and CLOSED Sprint 15.2 Wave 2 — Subcontract Flow. Combined with Wave 1 (Session 18), that's all 9 of the 20-PR Purchasing Cascade's foundation + flow layers complete in two sessions. Live writes proven end-to-end on Replit dev preview for every Wave 2 PR:** (1) **PR #404 SubcontractShipment + SubcontractReceipt entities** `08189c6` — 4 entities (Shipment/ShipmentLine/Receipt/ReceiptLine) + **10 §11 receipt scenarios** (FullGoodReceipt/PartialReceipt/ReceiptWithInspection/RejectedReceipt/VendorScrap/ShortReceipt/OverReceipt/CertMissing/WrongRevision/WrongJobOrPo) + auto-approval gating on Over/WrongJobOrPo + atomic PostReceipt transaction + two-phase numbering + FIFO vendor-WIP balance picker. SCSHP-2026-000001 verified live. (2) **PR #405 Subcontract Flow Orchestrator (§5 8-step)** `7d6b673` — `ISubcontractFlowService` pure-glue with 9 ops walking the 8-step flow. Wires PR-4/5/6 + B8 PR-PRO-7 readiness. **`FlowStateSummary` aggregate answers "where in 8 steps + next-action hint"** — the BIC differentiator endpoint. Step 5/7 retry-safety via "reload-with-lines, reuse if non-empty" pattern. Step 8 prefers `ReturnOperationSequence` per §6B. Verified live: op #1 returned "Step 3 — Link service PO line". (3) **PR #406 Subcontract Costing Integration (§12)** `93f33f6` — 6 new CostTransactionType values (SubcontractFreightOut/Return/Expedite/ScrapCharge/VendorCredit/Overhead) round out the §12 13-element vocabulary. `ISubcontractCostingService` 5 ops with inline `PostIfNew` idempotency guard on (sourceType, sourceId, transactionType). Post at ship + receive + invoice + close. Idempotency verified live: 2nd PostShipmentCosts call returned 0 new transactions. (4) **PR #407 Cockpit Subcontract Panel · CLOSES Wave 2 🎉** `f3bec39` — `ISubcontractValidationService` with 15 §24 non-negotiable validation guards + `RunAllAsync` aggregate. `_CockpitSubcontractPanel.cshtml` BIC differentiator partial rendering §22 view: header + 6-cell qty grid + §5 flow state + §12 cost ribbon + §24 validations table with Pass/Warn/Block badges. Verified live: panel for op #1 PRO #10 seq 40 renders "Step 3 — Link service PO line" + $60 cost total + "9 Pass / 1 Warn / 5 Block" rule summary. **Pre-PR subagent + Codex bug-catch ledger across Wave 2: 39 bugs squashed pre-merge** (PR-6: 13 / PR-7: 11 / PR-8: 10 / PR-9: 5). **55 total ships since B6 Foundation Sprint began. ~407 merged PRs to main.** Wave 3 next: Purchasing Command Center (PRs 10-15) — the §21 control center on top of every Wave 1+2 substrate.
>
> **Updated 2026-05-28 morning.** Main HEAD = **`d7782d4`** (PR #402). **Session 18 shipped 7 PRs (#396–#402) and CLOSED Sprint 15.1 Wave 1 — the foundation of the 20-PR Purchasing Cascade. Live writes proven end-to-end on Replit dev preview for all 5 Wave 1 PRs:** (1) **PR #396 Receipt-to-Job Direct Posting** `586156a` — THE foundational ETO/MTO change. Buy-to-job material flows PO Receipt → direct charge to PRO BOM line, bypassing inventory. (2) **PR #397 ProductionSupplyDemand** `10925ea` — unified demand record (53 fields + M:M allocation), 12-value SupplyPolicy enum from §8, 8-op IProductionSupplyDemandService. (3) **PR #398 PO Line ↔ Demand Link** `c229b89` — consolidation traceability via PurchaseOrderLineDemandLink + PurchaseOrderLine expansion (6 new fields). (4) **PR #399 SubcontractOperation + §9 Dual-Demand** `af5d0da` — 35-field op per §6B + dual-demand binding (service + WIP) + 12-state lifecycle §10. (5) **PR #400 Vendor WIP Tracking** `b045042` — VendorLocation (18 §14 fields) + VendorWipBalance + VendorWipTransaction + 11-state inventory lifecycle. **Codex caught 12 real bugs across Wave 1 (4 P1 + 8 P2) — all fixed pre-merge.** Plus 2 housekeeping PRs: **PR #401 token-efficiency CLAUDE.md rules** `744c789` + **PR #402 Superpowers-inspired pre-PR subagent review + git-worktree workflow rules** `d7782d4`. **51 total ships since B6 Foundation Sprint began. ~402 merged PRs to main.**
>
> **Updated 2026-05-27.** Main HEAD = **`2d37bfb`** (Sprint 14.4 PR-2 — child-to-parent cost transfer engine, PR #392). **Session 16 shipped 3 PRs (#390-#392): B8 validation pipeline wired into all 4 transaction services (41 injection points, 2 Codex P1s fixed) + Sprint 14.4 Cost Engine foundation (CostTransaction/CostTransfer/ProductionOrderCostSummary entities, 5 enums, 18 GlAccountKinds, ICostTransactionService, cost posting integration into all services, admin probe) + child-to-parent cost transfer engine (Layer B anti-compounding per Dean's 910-line cost-object graph research, cross-site dispersal tracking).** B8 PRO Cockpit CLOSED at 11/12 (PRO-12 deferred). Sprint 14.3 CLOSED. Sprint 14.4 Cost Engine in progress (2 of ~6 PRs shipped). **43 total ships since B6 Foundation Sprint began. ~392 merged PRs to main.**
>
> **Updated 2026-05-26.** Main HEAD was `aad2590` (Sprint 12.7 PR #3). Massive 48-hour sprint: Sprint 13.5 cleanup-pass cascade closed end-to-end (PRA-5b through PRA-5g — 7 ships) + Sprint 13.6 UI Audit (6 PRs in one evening) + Sprint 12.7 Controller Cockpit pulled forward ~16 days ahead of plan (3 of 5 PRs shipped). PR #4 KPI band wire-up code-complete on disk, blocked on iCloud Drive deadlock for git push. 5 new hard locks established (13-17) — most importantly **Lock 14** (Dev source of truth, Replit Publish-with-Copy as sync mechanism) and **Lock 17** (skip Republish for code-only PRs). TimescaleDB removed from dev + prod (Lock 14 incompatibility). See Wave map below for chronology.
>
> Updated **2026-05-19** (later in day) after **Voice MVP shipped end-to-end** (PRs #254-#258) AND **ADR-020 Postgres-as-AI-Native-OS accepted** (PR #259). Dean's call: **ADR-020 Phase 1 (Vector Layer) jumps the queue ahead of Sprint 12B / 12.5 / 13**. New Sprints **12C (Vector Layer)** and **12D (Apache AGE Graph)** inserted into the dashboard; **12D is hard-deadlined to June 3** because the EVS APS pitch needs a graph-narrated "why is X late" demo. See `[memory/project_voice_mvp_shipped.md]` + `[memory/project_database_direction_2026_05_19.md]` + `docs/ADR-020-postgres-as-ai-native-os.md`.
>
> Updated **2026-05-19** after the **116+ reckoning** (Dean pushed back on Sprint 12.5 being a hand-wave catch-all; every PR #116-#138 now has an explicit disposition). See `[memory/project_116_reckoning_decisions_2026_05_19.md]` for the full lock.
>
> Updated 2026-05-18 after full audit (codebase + git history + DB schema).
>
> **Reality check:** the codebase is much bigger than this plan implies. **212 PRs have merged to `main`** (Jan→May 2026, ~12.5 PRs/day average velocity). The plan tracks *initiatives*, but each initiative has spawned 3-10 follow-up PRs. Numbers below reflect **initiative completion**, not raw PR count.

| Sprint | Theme | Initiative PRs | Done | Total | % |
|---|---|---|---:|---:|---:|
| **Sprint 0** | Real bugs from audit | #100–#109 | **10** | 10 | **100% ✅** |
| **Sprint 1** | Reliability + finance credibility | #112,#114,#116,#119,#120,#121 | **6** | 6 | **100% ✅** |
| **Sprint 2** | Production-app hardening — Approvals + UX overhaul + Plant Floor live + #117 sub-PRs | #122–#142 (17 PRs) | **17** | 17 | **100% ✅** |
| **Sprint 2 follow-ups** ➡️ REROUTED 2026-05-19 | **MFA · SSO · DB-level RLS · First-run Onboarding · Admin v2 UX** all moved to **Sprint 12.5 Enterprise Hardening** with explicit PR slots #12.5.1-#12.5.12. **Agentic AI + MCP server** moved to **Sprint 21 Launch Hardening** (was incorrectly "folded into Sprint 5" — Sprint 5 scope memo doesn't cover MCP catalog publishing). | see Sprint 12.5 + 21 | 0 | 5 | 0% |
| **Sprint 3** | Premium modules — **ADR-012/013 Phases A-E ALL SHIPPED** as 21 PRs landed under GitHub numbers #152-#176 (semantic naming #119.1-#119.14 in PR subjects). Test posture (#171,#174,#175). | 21 schema PRs + 3 test PRs | **24** | 24 | **100% ✅** |
| **Sprint 3 — Premium modules remaining** ➡️ REROUTED 2026-05-19 (no longer "remaining" — each has an explicit sprint slot) | **Calibration #125 → Sprint 14 PR #14.5** · **Real OEE #126 → Sprint 14 PR #14.6** (Dean explicit "real not synthesized") · **RCM-FMEA #127 → Sprint 14 PR #14.7** · **Weibull #128 → Sprint 14 PR #14.8** · **Vendor scorecards #129 → Sprint 13 PR #13.5** · **Landed cost #130 → Sprint 17 PR #17.5** · **Blanket/Contract-PO #132 → Sprint 13 PR #13.6** · **i18n #137 → Sprint 22 (launch polish)** · **Tax matrix #131 · Payment batch #133 · OCR Invoice #134 · ASC 842 #135 · ASC 360 #136 → ALL STAY V2 (Sprint 24+) per 2026-05-19 best-in-class decision.** No item is in a "remaining" bucket anymore. | see Sprints 13/14/17/22/24 | 0 | 13 | 0% |
| **Sprint 3.5 — Design system + Phase 3 page rollout + Phase 4 a11y wave 1** | All 22 Phase 3 surfaces rebuilt on the 10-primitive design system. axe-core CI gate live. WCAG 2.1 AA verified across Dashboard / Plant / Inventory / Approvals / list pages. **CherryAI is now "WCAG 2.1 AA verified-in-CI" — procurement sales weapon. No EAM competitor (Fiix/UpKeep/Limble/Maximo/SAP PM/Infor) can claim this.** | #179-#215 (37 PRs) | **37** | 37 | **100% ✅** |
| **Sprint 3.5 followups** ➡️ REROUTED 2026-05-19 | **Form labels → Sprint 12.5 PR #12.5.13** · **CI app-boot hardening → Sprint 12.5 PR #12.5.15** · **Sparse-page redesigns (PO Detail / AP Detail / AP List) → Sprint 12.5 PR #12.5.16** · **Color contrast → Sprint 12.5 PR #12.5.14**. | see Sprint 12.5 workstream C+D | 0 | 4 | 0% |
| **Sprint 4** | Phase F — UI / renderer for the Phase E entities (per ADR-014). **PR #1 / #178 SHIPPED** (commit 9ddc01c): foundational infra — VoiceReadyPageModel, Result&lt;T&gt;, ActorKind, VoiceSession, IdempotencyKey, IdempotencyMediator (Stripe pattern), VoiceContextEmitter, &lt;voice-action&gt; TagHelper, 30 AuthorizationPolicies, AuditLog 7 AI columns (Purview pattern). 4 waves of UI PRs ahead. **Wave 1 (RegulatoryProfile admin / MaterialMaster admin / Vendor edit / StockReceipts) up next.** | #178 + TBD | **1** | 14-17 | **~7%** |
| **Sprint 5** | **Voice-First AI Co-Pilot** ⭐ — signature feature. Voice trigger on every screen, AI knows context, executes Q&A / notes / commands / requisitions / inventory checks / alerts. **THE MOAT.** | TBD | 0 | ~20 | — |
| **Sprint 6** | Mobile v1 — scope decided after Sprint 4 + 5 land | TBD | 0 | TBD | — |
| **Sprint 7 — Item Master Expansion** ⭐ NEW 2026-05-18 | Per `docs/research/item-master-and-multi-dim-inventory.md`. Adds the field categories CherryAI is missing vs SAP MM / Oracle Cloud / D365 / NetSuite: **regulatory** (REACH/RoHS/ITAR/UDI/SDS/GHS/3TG), **MRP sophistication** (PlanningMethod, LotSizingRule, time fences, ServiceLevelTarget, MTO/ETO/PTO), **quality** (MtrRequired/CocRequired/FaiRequired/AQL/InspectionPlan), **classification** (XYZ class, Make/Buy, Plannable, Rotating). Table-stakes for aerospace / ASME / FDA / medical / electronics verticals. | TBD | 0 | 5 | — |
| **Sprint 8 — Multi-Dimensional Inventory** ⭐ NEW 2026-05-18 | New `OnHandQuantity` fact table keyed by (Item × Site × Warehouse × Bin × Lot × Serial × Status × Owner × Quality × Variant × Project). D365 F&O inventory-dimension hierarchy pattern. Status/Owner/Quality dims become first-class (currently impossible). Backfill `ItemInventory`. Cmd-K voice picker flow on top. | TBD | 0 | 7 | — |
| **Sprint 9 — ItemEdit Page Restructure** ⭐ NEW 2026-05-18 | Today: 7 tabs but Basics is a 2200-line dumping ground. New: 11 tabs modeled on SAP MM views (Basics / Sourcing / Inventory & Storage / Planning / Manufacturing / Quality / Costing / Regulatory / Documents / Revisions / Where-Used). DataCard groupings + per-section "Show advanced" disclosure + Compact-mode toggle. **Master-Data Completeness Score** sibling to BuyabilityScore (Bronze/Silver/Gold/Platinum badge). Voice-ready field attribution. | TBD | 0 | 6 | — |
| **Sprint 10 — Sales-side & Costing depth (OPTIONAL)** | Only if Dean greenlights full ERP — IsSalable, Customer-Item Xref, drop-ship, ATP/CTP, landed-cost components, cost rollup, per-item GL overrides. | TBD | 0 | ~6 | — |
| **Sprint 11 — Receiving Control Center (PILOT)** ✅ SHIPPED 2026-05-18 | First role-based Control Center. Four-quadrant scaffold (KPI strip + exception lane + drawer detail + activity feed) + service layer + 10 voice tools + 3 workflow pages + DataWedge scan + kill-list route swap. All 7 PRs shipped (#227, #228, #230, #231, #232, #233, #234 hotfix, #235, #236). **Cockpit-first pivot 2026-05-18:** post-ship Dean observed the legacy Receiving Cockpit's time-bucketed PO queue + detail pane UX is materially better than the 4-quadrant scaffold for the daily-driver receive workflow. Sprint 12 reframes /Receiving around the Cockpit; the 4-quadrant scaffold demotes to an "Exceptions" sub-tab. See ADR-018. All services + voice tools + primitives remain reusable. | #227-#236 | **7** | 7 | **100% ✅** |
| **Sprint 11.5 — Control-Center-First Sidebar Rebuild** ✅ SHIPPED 2026-05-18 | Surfaces all Sprint 11 work in the sidebar. ADR-017 locks Control-Center-First IA + launch scope (7 Control Centers — Quality/AP-AR/HR defer to v2). PR #237 = ADR-017 + 557-line research doc. PR #238 = Nav-1 rebuild (Control Centers spine, Quick Actions tray, Settings drawer, Approvals bell). Includes `ControlCenterRegistry` — each Sprint 12-18 ship flips one line `IsLive: false → true`. | #237-#238 | **2** | 2-3 | **100% ✅** (Nav-2 server-side Recent + Pinned deferred, low priority) |
| **Sprint 12A — Receiving Workspace Rebuild (Cockpit-first)** ✅ SHIPPED 2026-05-19 — locked plan landed. | **Cockpit-first rebuild per ADR-018 (Accepted).** `/Receiving` rebuilt around the Cockpit pattern (time-bucketed PO queue + detail pane + header KPI band). Four tabs: **PO Queue** (default, PR #5/5.1/5.2 with flagship Page Header + 4 hero KPIs + Next Up + AI Suggestions) · **ASN Queue** (PR #6 on real `AdvancedShippingNotice` domain entity + EDI 856 destination) · **Orphans** (PR #7 with AI candidate-PO scoring ItemMatch 40 + VendorMatch 40 + Recency 20, Match-Orphan confirmation page) · **Exceptions** (Sprint 11 4-quadrant scaffold, demoted to anomaly view). PR #8 closed the sprint by retiring `/Receiving/Cockpit-Legacy`. **Cockpit primitives proven across 3 domain entities — same `ICockpitQueueRow` contract.** | #239 → #253 | **8** | 8 | **100% ✅** |
| **Sprint 12C — Postgres-as-AI-Native-OS · Phase 1 Vector Layer** ⭐ NEW 2026-05-19 / ✅ CLOSED 2026-05-21 | **ADR-020 D1+D2 SHIPPED. Sprint closed at 5-of-5 with PR #269 Hybrid Intent Router (commit f690b92).** `Embeddings(halfvec(1024))` table + HNSW index + RLS policy + `PendingEmbeddings` queue + Voyage HTTP client + EmbeddingWorker + 3 admin endpoints all LIVE. **28 embeddings live across 5 entity types** (12 ReceiptProfile + 5 Item + 5 WorkOrder + 3 Vendor + 3 AuditLog.AiCommandText). Voyage paid tier active. Cross-entity semantic search proven: "ball bearing for industrial machinery" → 5 bearing Items top-5 then STEEL/AUTO ReceiptProfiles then KENNAMETAL Vendor. **Voice intent router upgraded keyword → hybrid (Layer 1 keyword fast-path + Layer 2 vector cosine fallback, threshold 0.45 top-K=5 majority vote, AuditLog telemetry per call).** Layer 2 bootstrap-seeding follow-up tracked as PR #270 (Embeddings.Intent=0 in dev, likely RLS-on-TenantId=0 INSERT). | #260, #261+#262, #263, #264, **#269** | **5** | 5 | **100% ✅** |
| **Sprint 12.9 — Control Plane Hardening** ⭐ NEW 2026-05-22 (Dean elevated Item 9 from "interleaved side-work" to a real sprint slot — closes the audit-trail / chain-of-evidence diligence gap that would torpedo Fortune-100 procurement). | **7 PRs over ~5 days, sequenced between Priority 1.6075 (snapshot drift fix) and Priority 1.609 (Sprint 12D AGE).** (1) Roslyn analyzer replaces grep gate (catches Controllers/Endpoints/BackgroundServices not just PageModels) · (2) `IPostingService<TSourceDoc>` base interface contract · (3) Refactor `WorkOrders/Details` (17 writes → 0) → `IWorkOrderService` · (4) Refactor `Purchasing/Details` (12 writes → 0) → `IPurchasingService` (de-risks Sprint 13) · (5) Refactor `Materials/ItemEdit` (11 writes → 0) → `IItemMasterService` (de-risks Sprint 7-9) · (6) Audit-completeness CI metric (% of write paths through services, drift detection) · (7) Automated RLS tenant-leak test in CI (folds in Item 8 from absorption). **Net:** 40 of 173 write paths refactored (23%), Roslyn catches every layer, RLS provable in CI on every PR. **June 3 EVS pitch unaffected** — AGE spike is half-day to day's work; ~6 days runway is 6x what's actually needed. **Then Joe's pitch can include:** "Every transaction in CherryAI flows through a typed service, idempotent, audited with Purview schema, with tenant isolation enforced by Postgres RLS and proven by automated tests in CI on every PR." | TBD | 0 | 7 | — |
| **Sprint 12D — Postgres-as-AI-Native-OS · Phase 2 Apache AGE Graph Layer** ⭐ NEW 2026-05-19 — **HARD DEADLINE June 3 EVS pitch** | **ADR-020 D3 lands.** Three Cypher graph schemas: **`chain_of_custody`** (StockReceipt → Nest → Remnant → CutListLine → ProductionBatch → Shipment) · **`bom`** (Item → MaterialStructure → Bom → BomLine recursive) · **`aps_dependencies`** (Operation → Resource → Skill → Constraint with `precedes`/`requires`/`competes_with` edges). View-backed (refresh on demand) by default; promote to trigger-backed only when measured lag impacts voice UX. Real implementation of `IReceiptVoiceTools.TraceChainOfCustodyAsync` (Sprint 11 stub gets replaced here). **The June 3 EVS demo killer query**: voice → "show me why job X is late" → Cypher path traversal → LLM narrates. ~6 PRs: **(1) Host decision PR — `CREATE EXTENSION age` spike on Replit. If it loads, ship on Replit. If not, this PR makes the Azure-vs-Docker-vs-Neon call with 2-3 weeks of runway to June 3 (not zero). Locks ADR-022a (host decision) if a move is required.** · (2) AGE extension migration + first graph (`chain_of_custody` minimum) · (3) `bom` graph + view-backed refresh · (4) `aps_dependencies` graph + EVS demo data · (5) `TraceChainOfCustodyAsync` real wiring · (6) "why is X late" voice intent + LLM narration. **ADR-022** locks graph schemas + edge semantics before PR #2. | TBD | 0 | 6 | — |
| **Sprint 12B — Receiving DEPTH** ⭐ NEW 2026-05-18 (revised plan — Dean's call to finish Receiving before cloning) | Finish the Receiving pilot properly before ever touching a second Control Center. Six workstreams: **(1)** wire up `MatchOrphanReceiptAsync` (Sprint 11 stub) with real AI suggestion logic · **(2)** wire up `OcrParseMillCertAsync` (Sprint 11 stub) on real Phase 2 OCR pipeline · **(3)** wire up `ReceiveByVoiceAsync` + `QuarantineByVoiceAsync` (Sprint 11 stubs) end-to-end with `IdempotencyMediator` · **(4)** harden DataWedge focus mode on real Zebra TC52 / TC57 (keystroke buffering, scan-burst handling, locale + numeric pad edge cases) · **(5)** mobile + Zebra handheld optimization on the cockpit primitives (responsive breakpoints, touch targets, scan-first focus) · **(6)** **Receipts API endpoints** for ERP integration — the "channels into customer's existing ERP" thesis starts here (SAP / Oracle Cloud / NetSuite / D365 outbound formats). ~6 PRs. **No second Control Center until Receiving is real-user-tested in production.** | TBD | 0 | 6 | — |
| **Sprint 12.5 — Enterprise Hardening** ⭐ RESPEC'D 2026-05-19 (was "catch-up bucket"; Dean called the hand-wave — every PR now has an explicit slot) | **17 numbered PRs in 4 workstreams** — not a bucket. **A. Security & access (sales blockers)**: PRs #12.5.1-7 — MFA TOTP self-service + trusted devices + lockout · SSO SAML 2.0 + OIDC + JIT provisioning · Postgres RLS migration + verification harness. **B. Admin & onboarding (launch blockers)**: PRs #12.5.8-12 — Admin v2 approvals UI / audit-log explorer / settings polish · First-run onboarding wizard · Shepherd.js tours + Help hub. **C. a11y compliance (legal/gov blockers)**: PRs #12.5.13-15 — form-labels sweep · color contrast pass · CI app-boot hardening + axe sweep. **D. Tech debt + sparse pages**: PRs #12.5.16-17 — PO Detail / AP Detail / AP List redesigns · 9 skipped xUnit tests + EF snapshot regen + Playwright age-check + ghost-PR investigation (#3/#15/#81/#130). **NOT in 12.5 (intentional)**: i18n → Sprint 22 (launch polish). Sprint 3 premium modules → folded into Sprint 13-18 by domain. | #12.5.1-#12.5.17 | 0 | 17 | — |
| **Sprint 13 — Full Purchasing Control Center** (sequence unchanged; PR slots locked) | First non-Receiving Cockpit. **Vendor scorecards (#129) folds in as PR #13.5; Blanket/Contract-PO (#132) folds in as PR #13.6.** Other 6 PRs: `IPurchasingControlCenterService` + state machine → 10 voice tools → Open POs cockpit → Requisitions cockpit → RFQ cockpit → Exceptions tab + flip `PURCHASING` to `IsLive: true`. | TBD | 0 | 8 | — |
| **Sprint 13.5 — Manufacturing Domain + Project/Job Hierarchy + Master Files + Production / MES Control Center** ⭐ NEW 2026-05-22 / ⭐ GREW to 14 PRs 2026-05-23 (Dean: "best in class, no stones unturned") / ⭐ MES SCOPE EXPANDED 2026-05-23 evening (Dean: full Production / MES CC directive — Operation Dispatch / WorkCenter / MaterialConsumption / Lot+Serial Genealogy / OEE event sourcing / Downtime / Scrap / Rework). PR #5 family expanded from PR #5 to a six-PR cascade #5b.1 → #5c → #5d → #5e → #5f → #5g per MES gap analysis. | Fills the absent Manufacturing domain + establishes `CustomerProject` schema + lays the multi-vertical master-files foundation + the **full MES event layer** on top of the existing solid header layer. ⭐ **2026-05-24 MASTER FILES BASELINE INSERTED** — after PR #5d shipped (`fdc4740`), Dean halted PR #5c.4 with the directive *"we have a chart of accounts but it's def not large enough, not sure on UOM, but we need to make sure once again that we have the infrastructure set up first."* Audit of `Master Loader REV 03-19-24.xlsx` (57 ERP21 sheets, informational only — German nomenclature dropped) + exhaustive read of `Models/*.cs` confirms COA undersized (no segments, no posting matrix, no MfG/variance accounts) + UOM bifurcated (two parallel enums, no master table, no per-item multi-UOM) + 6 more gaps (Currency/PaymentTerm/TaxCode no real tables, Warehouse/Bin/Lot/Serial/ItemGroup absent, Employee/WageGroup/Department→GL absent, PriceList/Discount absent, real Tax authority+rate absent, Pack hierarchy absent). New cascade **PRA-4 (UOM)** → **PRA-5a (COA additive, NO renumber)** → **#5c.4 (slotted after PRA-5a)** ship **PRE-ABS Thu May 28**. Then **PRA-5b (COA segment refactor)** → **PRA-6 (Currency+PaymentTerm+TaxCode masters)** → **PRA-7 (Warehouse+Bin+Lot+SerialMaster+ItemGroup posting profile)** ship between ABS Thu and EVS Wed Jun 3. Then **PRA-8 (Employee+WageGroup+Dept→GL)** → **PRA-9 (PriceList+Discount+Rebate)** → **PRA-10 (TaxAuthority+TaxRate effective-dated)** → **PRA-11 (Pack hierarchy)** ship Jun 4-8. MES event cascade (PR #5e/5f/5g) resumes Jun 9+ with proper foundation underneath. **Net runway impact: MES cascade slips ~10 days; ABS Thu + EVS Jun 3 demos untouched.** Full memo: `docs/research/master-files-baseline-2026-05-24.md`. Memory: `reference_master_files_baseline.md`. **— END BASELINE INSERTION** **12 of 14 main PRs SHIPPED + 5 hardening PRs + 6 Master Files Baseline PRs (PRA-4, PRA-5a, #5c.4, #5c.4.1, PRA-6, PRA-7 + ADR-019) through `f613cb3` 2026-05-24:** ✅ PR #1 foundation (`c8f6d79`/#293) · ✅ PR #1.5 field expansion (`fb44052`/#294) · ✅ PR #1.75 AS9102 FAI (`beeadd8`/#295) · ✅ PRA-1 Master Files: IndustryVertical 17-enum + Carriers + Customer/Vendor regulator IDs + Customer defaults (`bfb3922`/#296) · ✅ PR #2 `ICustomerProjectService` (`a113b49`/#297) · ✅ PR #3 `IProductionOrderService` (`73a9ebf`/#298) · ✅ PRA-2 Country + Subdivision + WorkCalendar + Holiday (`fc05b78`/#299+#300 hotfix) · ✅ PR #4 BIC nav rebuild + /Production + /CustomerProjects + Master Files admin + 10 Playwright E2E specs (`3904a81`/#301) · ✅ PR #5a `IProductionControlCenterService` backend (KPI band / queue / exception lane / activity feed / Next Up / AI / bulk-status) (`d9debe6`/#302) · ✅ PR #5b Production CC page v1 (own .cc-* CSS — REJECTED as "awful" by Dean) (`20cc85a`/#303) · ✅ **PR #5b.1 Production CC visual rewrite** — composes shared Cockpit primitives (matches Receiving), Siemens-style routing stepper, live progress bar, action verb tray, MES research-driven design (`2ce8f82`/#304). **MES cascade status (post-baseline):** PR #5c ✅ shipped `175a130`/#305 · PR #5c.1/5c.1.1 ✅ shipped (lineage hardening + ABS seed removal) · PR #5c.2 ✅ shipped `ccec07b`/#309 (tenant scoping hardening) · PR #5c.3 ✅ shipped `e2c3eb4`/#310 (quarantine hardcoded seeds) · PR #5d ✅ shipped `fdc4740`/#311 (Operator Workbench + LaborEntry + ReasonCode — rolls PRA-3 in) · **PRA-4 ✅ shipped `bbaaf9a`/#312** (UOM master + 17 categories + 67 system UOMs + IUomService) · **PRA-5a ✅ shipped `479432a`/#313** (COA additive expansion: +26 `GlAccountCategory` values + 28 system template GL accounts, NO renumber) · **PR #5c.4 ✅ shipped `27d88f3`/#314** + **#5c.4.1 hotfix `99e795f`/#315** (system ReasonCodes seed + MaterialStructure CompanyId backfill; column-name typo caught post-merge) · **PRA-6 ✅ shipped `7bde5e7`/#316** (Currency/PaymentTerm/TaxAuthority/TaxCode masters; `*Master` suffix to dodge legacy SystemConfig enums) · **PRA-7 ✅ shipped `f613cb3`/#317** (Warehouse + Bin + Lot + Serial + ItemGroup + PostingProfile + **ADR-019** sibling locking SAP/Dynamics separation-of-concerns; 6 new masters + 9 warehouse templates + 12 item groups + 15 posting profile skeletons; 2 Codex P2 catches addressed in amendment `2f926e2`). · PR #5e Event tables (DowntimeEvent / ScrapEvent / ReworkEvent / MaterialConsumption + services) — slot AFTER PRA-11 per baseline cascade · PR #5f Lot+Serial Genealogy (built on chain-of-custody graph ADR-022 + PRA-7's Lot/SerialMaster) · PR #5g OeeEvent rollup + OEE/Throughput/Down-Machines KPI tiles. Plus PRA-3 ReasonCodes + TaxJurisdictions · PR #7 4 voice intents · PR #6 subcontract chain edges (folds in 12B.6a `ItemReceivedV2`) · PR #8 ADR-026 Seven Customer Modes Contract. **Locked feedback (2026-05-23):** every CC surface MUST compose `Pages/Shared/Primitives/Cockpit/_CockpitPageHeader` + `_CockpitKpiBand` + `_CockpitTabShell` + `_CockpitShell` — do NOT roll your own `.cc-*` CSS (this is what made PR #5b "awful"). Receiving CC is the BIC reference. **MES research synthesis 2026-05-23** (MachineMetrics / Tulip / Plex / Epicor Kinetic / Siemens Opcenter / SAP DM / Rockwell FactoryTalk / Aptean) drives the design: kill the OEE gauge → number + A/P/Q micro-bars + 24h sparkline; cards-not-rows dispatch with drag-to-reorder; voice-driven downtime/scrap ("Cherry, log 15 min setup on Line 3"); sharp activity right-rail. **Cross-refs:** `docs/research/luxury-cockpit-ux.md` · `docs/research/master-files-audit.md` · `docs/research/customerproject-field-set.md` · `docs/research/fai-workflow-schema.md` · ADR-017 · ADR-018 · ADR-022 · ADR-025 · ADR-026 · memory `feedback_reuse_cockpit_primitives.md` · memory `project_mes_gap_analysis.md`. | #293-#317 + Master Files Baseline (PRA-4 through PRA-11) + #5e/5f/5g (MES events) | **12 main + 5 hardening + 8 baseline (PRA-4/5a/#5c.4/#5c.4.1/PRA-6/PRA-7/PRA-8/PRA-9/PRA-10) + 7 cleanup-pass (PRA-5b/5e.2/5c/5d/5e/5f/5g)** | **14 main + 8 baseline + 3 MES** | **>100% — DRAMATICALLY AHEAD of plan, ~10 days early. All 8 baseline ships closed (only PRA-11 Pack hierarchy remains). All 7 AccountingKey DEF-008 cleanup-pass ships closed end-to-end: PRA-5b foundation `71499de`/#325 + PRA-5e.2 seed `f870923`/#329 + PRA-5c ApPostingService `9f9bf5e`/#326 + PRA-5d ReceivingPostingService `6d4439a`/#327 + PRA-5e CipCapitalizationService `0f24412`/#328 + PRA-5f CapitalImprovementPostingService `bc65f7a`/#331 + PRA-5g JournalGenerator depreciation `13c05b7`/#344. MES cascade #5e/5f/5g + PRA-11 still queued (Wave 3.5-3.7). ABS demo headliner (Workbench from PR #5d) untouched and live. ALSO: Sprint 13.6 UI cleanup (6 PRs, see new sprint row) shipped 2026-05-25; Sprint 12.7 Controller Cockpit PR #1/#2/#3 (see new sprint row) shipped 2026-05-25/26** |
| **Sprint 13.6 — UI Audit + Cleanup** ⭐ NEW 2026-05-25 / ✅ SHIPPED same evening (6 PRs in one session). 4-agent parallel audit on form density / nav drift / dead pages / cockpit primitive backport → synthesis PR + 6 execution PRs. Priority 1.62a / Wave 2.5. Parallel workstream — didn't block MES events. | **All 6 SHIPPED 2026-05-25 evening:** PR #339 `b315374` (3 prod 500s fixed: TrialBalance / Periods/Close / AccessDenied SVG + dev-jargon strip) · PR #340 `151510c` (NavRegistry + Quick Actions cleanup — 3 vapor entries removed, 5 mis-pointers fixed) · PR #341 `c081254` (CSS utilities + 4 form-density refactors, −115 inline overrides −57% across 4 files) · PR #342 `e1cdba8` (subform consolidation: 2 shared partials `_ChildSectionHeader` + `_ChildTableEmpty` + 4 refactors; Codex P2 catch fixed inline) · PR #343 `ecf64a5` (Cockpit backport on Receiving/ByPo + Receiving/Blind + Admin/Lookups + NEW Quality nav group + NEW `.github/workflows/nav-routes-check.yml` CI gate that caught 5 bonus broken routes). All 38 NavRegistry routes resolve post-sprint. Plus PR #337 (AssetImport bulk Excel upload + 25 ABS demo assets), PR #338 (FAI UI 3-page surface + IFaiService), PR #336 (10 ABS demo asset photos) all rolled in. | #339-#343 + #337/#338 + #336 | **6** | 6 | **100% ✅** |
| **Sprint 12.7 — Controller Cockpit (the CFO motion)** ⭐ NEW 2026-05-24 audit / **PULLED FORWARD 2026-05-25/26** — was Wave 4 / Jun 11-17, now in flight ~16 days ahead of plan. Fortune-100-CFO pitch motion: live demo on `/Controller?tab=drilldown` with "why is NBV $1.2M on Asset #4231" → AI walks the chain → narrated naturally via Cherry Bar TTS → every step a clickable drill link. Acceptance target = ABS demo dress rehearsal with Paul Marcotte. | **4 of 5 PRs SHIPPED 2026-05-25/26 (Wave 4 in flight):** PR #345 `5176a1b` Controller shell (4 tabs Books/Drilldown/Close Prep/Audit Trail + NavRegistry Finance group + all 4 Cockpit primitives composed per Lock 3) · PR #346 `614a738` `ChainTraceService` source-to-GL drilldown service walking Asset → CipCapitalization → CipProject → CipCosts + reverse JE walk (Codex P1 catch on per-asset GL override disambiguation) · PR #347 `aad2590` voice intent `IntentKind.ExplainChainTrace` on Cherry Bar (push-to-talk → ChainTraceService → TTS narration, **Lock 17 FIRST EXERCISE** — code-only PR with no Republish). **PR #348 `2dbcfb9` SHIPPED 2026-05-26 PM** — `IFinanceKpiService` KPI band wire-up (Cash position / AP due this week / Open POs / WIP balance), tenant-scoped via `companyId` guards on every query, 26 unit tests pass. **Codex caught 3 real multi-tenant data-leakage bugs (P1: AP scope; P1: Open POs scope; P2: cash GL admin view) all fixed in-PR before merge** — branch protection refused admin-override past unresolved threads, the right outcome. 4-hour CI stall during GitHub Actions degraded-service window (12:17→16:30Z 2026-05-26) confirmed via githubstatus.com — encoded as **Lesson #18** (always check status page first when CI fails to trigger). **PR #5 RE-SCOPED 2026-05-26 PM to CFO-only:** demo data bumps (realistic KPI values for Cash / AP / Open POs / WIP), `/Controller/Walkthrough` page (split-screen scripted CFO motion), TimescaleDB removal migration (Lock 12 cleanup-pass from `Migrations/20260516_AddTelemetrySubstrate.cs:61`), Republish-with-Copy. The COO/production motion that grew out of Dean's 2026-05-26 directive (10-level BOM + multi-plant + subcontract + cost rollup) lives in NEW **Sprint 12.8** below — Plan integrity preserved, not absorbed into Sprint 12.7. | #345-#349 | **4** | 5 | **80%** (PR #5 CFO-only, then Republish) |
| **Sprint B6.FS — Foundation Sprint (B6 GO BIG cascade kickoff)** ⭐ NEW 2026-05-26 PM session 5 (Dean: *"GO BIG"*). First sprint of the ~190-280h B6 cascade (Item Master as Standard, ProductionOrder as Actual, snapshot+variance+ECR/ECO+Drawings+DMS+smarter-than-BIC unified Item via view). Foundation Sprint scope = ~24-36h across 7 PRs that bring Item Master from mid-market depth to tier-1 BIC parity. **HARD LOCK:** `feedback_b6_go_big_2026_05_26.md` — refuse Minimal-path shortcuts on every B6 ship. **NEW HARD LOCK (session 7):** `feedback_no_fake_data.md` — every test fixture / seed row / demo value MUST look like real manufacturing data. Design memo: `docs/research/b6-foundation-sprint-design-2026-05-26.md`. Audit synthesis: `docs/research/item-master-b6-audit-synthesis-2026-05-26.md`. | **6 of 7 main-path PRs SHIPPED + 1 inserted + 1 hotfix = 8 ships total. 2026-05-26 PM:** ✅ **PR-FS-1 `8b5a7f6` (PR #355)** — `Item.ItemGroupId` int? FK + IItemGroupResolver service + Source-Internal-requires-classification gate + 6 tests. **Codex window CLEAN — first clean B6 ship.** ✅ **PR-FS-1.5 `f47d735` (PR #356)** — `IItemGroupBackfillSeeder` + `/Admin/BackfillItemGroups` + 4 tests. Live E2E on dev: 151/151 classified (all FG, since pre-PR-FS-1 dev DB only had Type=Part). **Codex window CLEAN — second clean B6 ship.** ✅ **PR-FS-1.5.1 `fc299f8` (PR #357) HOTFIX** — semantic fix for the Part→FG default bug Dean caught: now Source-aware resolver (`Part+ExternalERP→RAW`, `Part+Internal→SUBASSY`, FG never a default anywhere) + new `IItemSourceBackfillSeeder` (bounded flip Internal+FG→ExternalERP scoped to `Type IN (Part, Kit)`) + scoped `ReclassifyLegacyBugRows` mode (operator-set intent preserved) + `/Admin/BackfillItemSource` page + 27 tests (2 new regression guards for Codex P1s). **Codex caught 2 P1s on first commit `fe83fbb` — both fixed in-PR (`892251e`), threads resolved via GraphQL mutation, final squash `fc299f8`.** Lock 16 E2E pending Replit pull + Agent restart. ✅ **PR-FS-2 `ab8436e` (PR #358)** — ItemSite per-Site override entity (SAP MARC equivalent) + IItemSiteResolver service + /Admin/ItemSiteProbe diagnostic + 11 tests. Tenant trio (TenantId/CompanyId/SiteId) + null-safe uniqueness via partial unique index (Codex P1 fix on `(ItemId, SiteId) WHERE TenantId IS NULL`) + PickString null-vs-empty fix (Codex P2). 6 FKs cascading. Lock 16 E2E live: ItemSiteProbe renders cascade for ItemId=9245 BRG-6207-2RS showing ItemGroupId=1 (RAW) + Source=ExternalERP from PR #357 + all 30 fields source="Item" (no overrides yet, expected starting state). ✅ **PR-FS-3 `bf8bd5a` (PR #359)** — ItemStandardCostElement (SAP Cost Component Split equivalent) + IItemStandardCostService + /Admin/ItemStandardCostProbe + 11 tests. 8-element type enum (Material/Labor/VarOH/FixOH/Subcontract/Setup/Tooling/Other) + 4-source enum (Manual/RolledUp/Imported/Calculated) + effective-dating + idempotent SetCostElementAsync. Null-safe partial UNIQUE from day one (PR-FS-2 lesson applied prophylactically). **FIRST CLEAN B6 PASS SINCE #355 — ZERO Codex threads.** Realistic-mfg test fixtures (BRG-6207-2RS bearing $18.90 with $15.20 mat + $3.10 VOH + $0.60 tooling composition, EM-4FL-8MM endmill $48.50, BAR-1018 steel $86.40) per the newly-encoded HARD LOCK no-fake-data. Lock 16 E2E live: probe ItemId=9245 renders full 8-element breakdown table all $0 (expected empty starting state). ✅ **PR-FS-4 `3f06bd6` (PR #360)** — CostLayer (FIFO/LIFO/Average inventory valuation, SAP MM "stock with values" equivalent) + ICostLayerService (RecordReceipt idempotent + ConsumeQuantity FIFO/LIFO/Avg/LastPurchase + ReverseReceipt + GetWeightedAverage) + /Admin/CostLayerProbe + 13 realistic-mfg tests (Ryerson Steel Q1/Q2/Q3 commodity-uplift $60/$62.40/$64.80, BRG multi-vendor Grainger/MSC/Travers, EM reverse flows). Null-safe partial UNIQUE from day one. **Codex caught CHERRY025 + 2 P1s** — probe DbContext injection (refactored to service-only); CostMethod.Standard silent FIFO mapping (now throws NotSupportedException, deferred to Sprint 14.4 variance engine); concurrency token missing (added `byte[] RowVersion` + EF `.IsRowVersion()` + 3-retry loop on `DbUpdateConcurrencyException`). All 3 fixed in-PR, threads resolved. Lock 16 E2E live: probe ItemId=9245 renders summary panel empty (0 layers, $0.00 weighted-avg — expected starting state). ✅ **PR-FS-5 `d40484a` (PR #361)** — ItemSourcingRule (SAP S/4 Source List equivalent) + 5+5 enum coverage (SourceMethod / SourcingApprovalState) + AS9100 §8.4.1 customer-mandated AVL + split-sourcing allocation validation + IItemSourcingRuleService + /Admin/ItemSourcingProbe + 12 realistic-mfg tests (Grainger/MSC/Travers BRG multi-vendor, Ryerson Steel sole-source GE Aviation CustomerId=42 for AS9100 §8.4.1 heat-trace, Sandvik/Kennametal 60/40 split). **Codex caught 2** — P1 NULL-safe uniqueness for SiteId/VendorId (Postgres NULL-distinct trap, fixed via service-side check) + P2 missing TransferFromSiteId FK (added nav + HasOne+SetNull). Both resolved. Lock 16 E2E live: probe ItemId=9245 renders "0 rules. MRP falls back to Item.PrimaryVendorId" (expected). ✅ **PR-FS-6 `0d8a966` (PR #362)** — CustomerItemXref (SAP CMIR — customer-PN ↔ Item bidirectional translation) + 3-state lifecycle (Active/Superseded/Obsolete) + supersession chain (SupersededByXrefId self-FK) + ICustomerItemXrefService (Resolve both directions, atomic Supersede via EF nav-property pattern, Obsolete) + /Admin/CustomerItemXrefProbe (2 forms, service-only DI per CHERRY025) + 11 realistic-OEM tests (GE Aviation GEAV-BRG-A12345 Rev R3 + Boeing BA-MATL-1018 Rev A→B with BAMS-3320 spec + Pratt & Whitney PW-TOOL-EM-4F). **Codex caught 1 P1 — non-atomic SupersedeAsync** (two SaveChangesAsync calls left orphan rows on second-call concurrency conflict). Fixed via EF nav-property pattern: `existing.SupersededByXref = newXref` instead of raw FK assignment, single SaveChangesAsync, both writes atomic. Lock 16 E2E live: probe ItemId=9245 → "No customer xrefs for this Item. Ship/invoice will use Item.PartNumber" (expected starting state). ⏳ **1 PR remaining:** PR-FS-7 (18-column Item Master expansion: PlanningPolicy + MakeBuyCode + LotSizingRule + AS9100Critical + KeyCharacteristic + ECCN + ScheduleB + IntrastatCode + ItemFamily + FrozenStandardCost + InspectionPlanId etc., 4-6h — also where the `IsSellable` flag lands that tightens the SUBASSY-Internal default). After Foundation Sprint: Sprint 14.1 (snapshot tables) → 14.2 (DMS + Drawing) → 14.3 (Arena PLM ECR/ECO) → **B8 cascade (Production Order Cockpit, 12 PRs, 172-240h — see Theme B8 brainstorm row)** → 14.4 (cost engine + variance — consumes PR-FS-4 CostLayer data) → 14.5 (smarter-than-BIC unified Item separation via view) → 14.6+ (B7 PO-as-Standard + Make-or-Buy duality). Memory: `project_pr355_shipped.md`, `project_pr356_shipped.md`, `project_pr357_shipped.md`, `project_pr358_shipped.md`, `project_pr359_shipped.md`, `project_pr360_shipped.md`, `project_pr361_shipped.md`, `project_pr362_shipped.md`. | #355 + #356 + #357 + #358 + #359 + #360 + #361 + #362 + PR-FS-7 | **6** main + 1.5 + 1.5.1 | 7 main | **86% main-path · 8 ships total** |
| **Sprint 12.8 — Production Motion (the COO motion)** ⭐ NEW 2026-05-26 PM — Dean directive after PR #348 ship: needs to demo manufacturing + multi-plant routing + 10-level BOM + subcontract paint + cost rollup for Shadi Mohaisen (COO, most technically literate in the ABS room) on Thursday 2026-05-28. Inserted as sibling sprint to Sprint 12.7 to preserve Plan integrity — the COO ambition pulls **Sprint 0.5 cost-rollup S1 fix forward** + **Sprint 13.5/14 production-motion work forward**. Per the 2026-05-26 production audit, ~80% of the underlying schema is built (`ProductionOrder` + `MaterialStructure` + `Routing` + `ProductionOperation` + `LaborEntry` + Site→Location hierarchy + subcontract fields on `WorkOrderOperation`); the compute layers (backward scheduler, BOM explosion, cost rollup, GR auto-completion, facility absorption) are not. Sprint 12.8 ships the **smallest amount of new compute** to make the demo data look like the engines ran — stub backward scheduler + cost columns + parent-child FK — and **hand-crafts a single hero scenario** (Rolls-Royce Trent XWB Engine Bracket Assembly — Q3 2026). The real engines are Sprint 0.5 + Sprint 14 work, NOT crammed into 48 hours. Honest deflection script rehearsed for "show me live re-scheduling / live cost roll-up": *"This shows what the system DELIVERS at run-time. The live engines are Sprint 14. Want to schedule a follow-up where I show them being built on your real data?"* | **5 PRs queued 2026-05-26 PM:** (1) **PR #5a** — schema: `MaterialCost` / `LaborCost` / `OverheadCost` / `SubcontractCost` / `ActualCost` `decimal(18,2)` columns + `ParentProductionOrderId` self-FK + no-self-parent CHECK on `ProductionOrders` + `ADR-028` "ProductionOrder parent-child for multi-level BOM execution" (distinguishes parent-child from existing `MasterProductionOrderId` revision chain to avoid SAP PP02 conflation trap). (2) **PR #5b** — `IBackwardSchedulingService.BackwardScheduleAsync` stub: linear walk from parent `ScheduledEnd` backward through child Routing operations, stamps `PlannedStart` / `PlannedEnd` on each child + ProductionOperation. No resource leveling, no constraint solving — reusable infrastructure the real Sprint 14 scheduler swaps in. (3) **PR #5c** — ABS Machining "Rolls-Royce Trent XWB Engine Bracket Assembly — Q3 2026" scenario seeder: 1 CustomerProject + 10 ProductionOrders (1 parent + 9 children linked via PR #5a FK AND grouped under one CustomerProject for the visible tree) + 10 MaterialStructures (parent BOM with 9 sub-assembly references) + Routings with 3-5 ops each + ONE operation `OperationType=Subcontract` at vendor "AeroCoat Industries" with `VendorExpectedReturnDate` set + ProductionOperation snapshots via `ReleaseFromRoutingAsync` + backward-scheduled dates via PR #5b stub + pre-computed cost values stamped (parent total ~$185-220K) + LaborEntry rows for completed/running operations + WorkOrderPart rows for material kits + JournalEntry rows tagging facility-scoped GL accounts (WIP-MISS / WIP-BURL / OH-MISS / OH-BURL). Last 3 operations at Burlington Location (the cross-plant beat). Idempotent guard so re-running on dev doesn't dupe. (4) **PR #5d** — `/Production/Walkthrough` page: split-screen scripted COO motion. Open CustomerProject → 10-level tree → click parent PRO → BOM + Operations + Cost rollup → drill child → subcontract operation w/ vendor + return date → next operation at Burlington → costs rolled up to parent. Hand-crafted scenario; honest deflection script for live-engine questions. (5) **Republish** — folded into Sprint 12.7's Republish-with-Copy at end (single Republish covers both sprints). **Research memo:** `docs/research/abs-thursday-demo-data-design-2026-05-26.md` (in-repo, written before any code lands). **What Sprint 12.8 INTENTIONALLY DOES NOT ship live:** real MRP scheduler, real cost roll-up service (Sprint 0.5 / Sprint 14), real subcontract GR auto-completion, real inventory decrement (audit S1), real `ProjectPostingMode` consumption — all flagged in the walkthrough script as deflection-and-second-meeting points. | #349 + #350 + #354 | **3** | 5 | **60%** — PR #349 cost-cols schema + ADR-028 `0c05e4d` · PR #350 IBackwardSchedulingService stub `356ee84` · PR #354 PR #5c.1 CooMotionDemoSeeder skeleton `dca5628` (Codex caught 2 P1s — granular idempotency + DEMO-COO-PRO- prefix, both fixed + threads resolved before merge). **Live + verified on prod** post-Republish-with-Copy (`industryos.app/CustomerProjects/Index` renders "Precision Bracket Assembly — Q3 2026 (Demo)" `DEMO-COO-PROJ-001` ACTIVE). **Remaining:** PR #5c.2 lineage (LaborEntry + WorkOrderPart kits + JE postings with AccountingKey segmentation) + PR #5d `/Production/Walkthrough` scripted COO motion page. Both ship next session if needed for full COO depth pre-demo; the skeleton suffices for the visible tree narrative. Memory: `project_pr354_shipped.md`, `project_republish_prod_verify_2026_05_26.md`. |
| **Sprint 15.1 — Purchasing Cascade Wave 1** ⭐ NEW 2026-05-28 / ✅ SHIPPED 2026-05-28 (5/5 PRs in one session) | First wave of the 20-PR Purchasing Cascade per Dean's 1,212-line `docs/research/purchasing-subcontracting-supply-demand-dean-research.txt` spec + `docs/research/purchasing-cascade-design-2026-05-28.md`. Core architectural change: **Receipt-to-Job is the ETO/MTO default — buy-to-job material bypasses inventory.** Plus the unified `ProductionSupplyDemand` record, PO/demand consolidation traceability, the §9 dual-demand pattern for subcontract, and physical lot tracking at vendor. **All 5 PRs E2E-verified live on Replit dev preview.** **Codex caught 12 real bugs across Wave 1 (4 P1 + 8 P2) — all fixed pre-merge.** | #396-#400 | **5** | 5 | **100% ✅ — Wave 1 COMPLETE 🎉** |
| **Sprint 15.2 — Purchasing Cascade Wave 2 (Subcontract Flow)** ⭐ NEW 2026-05-28 / ✅ SHIPPED 2026-05-28 evening (4/4 PRs in one session) | PRs 6-9 of the 20-PR cascade. Complete the 8-step subcontract operation flow from spec §5. **PR-6 #404 SHIPPED `08189c6`:** 4 entities (SubcontractShipment + SubcontractShipmentLine + SubcontractReceipt + SubcontractReceiptLine) with lot/serial/revision traceability + 10 §11 receipt scenarios as enum (FullGoodReceipt/PartialReceipt/ReceiptWithInspection/RejectedReceipt/VendorScrap/ShortReceipt/OverReceipt/CertMissing/WrongRevision/WrongJobOrPo) + auto-approval gating + atomic PostReceipt transaction + two-phase numbering + FIFO vendor-WIP balance picker. 13 service ops, 13-button admin probe. **PR-7 #405 SHIPPED `7d6b673`:** `ISubcontractFlowService` — pure-glue 8-step orchestrator (PRO release → demands → PO/req → gate on prior op → ship WIP → track vendor status → receive subcontract PO + return WIP → next op + readiness check). Wires PR-4/5/6 + B8 PR-PRO-7 `IOperationReadinessService`. **`FlowStateSummary` aggregate answers "where in 8 steps + next-action hint"** (BIC differentiator). Step 5/7 retry-safety via "reload-with-lines, reuse if non-empty" pattern. Step 8 prefers `ReturnOperationSequence` per §6B. 9 service ops, 9-button probe. **PR-8 #406 SHIPPED `93f33f6`:** §12 cost elements wired into Sprint 14.4 CostTransaction engine. 6 new `CostTransactionType` values (SubcontractFreightOut/Return/Expedite/ScrapCharge/VendorCredit/Overhead) round out the §12 13-element vocabulary. `ISubcontractCostingService` 5 ops with inline `PostIfNew` idempotency guard on (sourceType, sourceId, transactionType). Posts at ship + receive + invoice + close. SettleAtClose now actually clears the residual via `unitCost = -openBalance`. **PR-9 #407 SHIPPED `f3bec39` — CLOSES Wave 2 🎉:** `ISubcontractValidationService` with 15 §24 non-negotiable validation guards + `RunAllAsync` aggregate (returns ordered Pass/Warn/Block list). New `_CockpitSubcontractPanel.cshtml` BIC partial rendering §22 view: header + 6-cell qty grid + §5 flow state + §12 cost ribbon + §24 validations table with Pass/Warn/Block badges. Admin probe demonstrates the partial standalone. **Pre-PR subagent + Codex caught 39 bugs across Wave 2 — all fixed pre-merge** (PR-6: 13 [11 pre-PR + 2 Codex] · PR-7: 11 [9 pre-PR + 2 Codex P1 retry-safety bugs] · PR-8: 10 [7 pre-PR + 3 Codex incl P1 SettleAtClose was a no-op] · PR-9: 5 [4 pre-PR + 1 Codex partial-ship false over-receipt]). All 4 PRs E2E-verified live on Replit dev preview. | #404-#407 | **4** | 4 | **100% ✅ — Wave 2 COMPLETE 🎉** |
| **Sprint 15.3 — Purchasing Cascade Wave 3 (Purchasing Command Center)** ⭐ ✅ CLOSED 2026-05-28 (6/6 PRs across 2 sessions) | PRs 10-15 of the 20-PR cascade. The §21 Purchasing Control Center on top of Wave 1+2 substrate. **PR-10 #409 SHIPPED `303bd96`:** `IPurchasingControlCenterService` + `BuyerActionState` 9-state machine + `PurchasingQueueType` 13 lanes + 12 guarded transitions. 5-op service (KPI band, 13-way Supply Demand queue, exception lane, lifecycle state read, lifecycle transition write). 11 bugs squashed pre-merge (1 P1 + 7 pre-PR P2 + 3 Codex P2). **PR-11 #410 SHIPPED `e3862c7`:** First user-facing CC page. Composes shared Cockpit primitives (`_CockpitPageHeader` + `_CockpitKpiBand` + `_CockpitTabShell`) over PR-10. 10 tabs registered (§21 IA day-one); Supply Demand + Buy-to-Job LIVE. PURCHASING flipped `IsLive: true`. 6 bugs squashed pre-merge (4 P1 + 2 Codex P2 — including a ~20-fictional-CSS-class catch). **PR-12 #411 SHIPPED `75f66d2`:** Subcontract / Vendor WIP / Receipts / Inspection Holds tabs. 4 new tab-specific reads. LiveTabs 2→6. 5 bugs (3 pre-PR P2 + 2 Codex P2). **PR-13 #412 SHIPPED `5e8447f`:** POs / Expedites / Approvals / Cost Exceptions tabs. **CLOSES full §21 10-tab IA.** LiveTabs 6→10. 6 bugs (5 pre-PR P2 + 1 Codex P2). POs tab shows 238 real POs / $3.67M live. **PR-14 #413 SHIPPED `cc7b35e`:** `IAutoPurchaseService` (§16 — 10 trigger / 12 blocker decision matrix) + `IDemandConsolidationService` (§17 — 6 consolidation modes preserving allocations). 10 bugs (3 P1 + 5 P2 pre-PR + 2 Codex P2). **PR-15 #414 SHIPPED `28a0fc7` · CLOSES WAVE 3 🎉:** `IPurchasingRecommendationService` with 15-value RecommendedAction (11 §18 patterns + 4 fall-through) × 4-value Risk. Composes PR-14 AutoPurchase for §16 context. Wired into `GetSupplyDemandQueueAsync` so the Supply Demand queue NEXT ACTION column shows real §18 hints. 5 bugs (1 P1 + 1 P2 pre-PR + 3 Codex P2). All 6 PRs E2E-verified live on Replit dev preview. **Wave 3 cumulative bug ledger: 43 bugs squashed pre-merge (24 pre-PR subagent + 19 Codex).** | #409-#414 | **6** | 6 | **100% ✅ — Wave 3 COMPLETE 🎉** |
| **Sprint 15.4 — Purchasing Polish (Wave 4)** ⭐ NEXT UP — closes the 20-PR Purchasing Cascade | PRs 16-20 of the 20-PR cascade. Schema-heavy polish layer backing vendor-side and post-PO lifecycle on top of the live Wave-3 §21 IA. **PR-16 PO Acknowledgment + Vendor Confirmation (~6h):** new `POAcknowledgment` entity; PO lifecycle Sent → Acknowledged → Confirmed with vendor-confirmed promise dates feeding the existing MissingSupplierPromise lane + PO header chips. **PR-17 PO Amendment / Change Order (~6h):** new `POChangeHistory` entity for post-approval PO mods; change-reason enum + impact assessment + re-approval workflow + audit trail per amendment. **PR-18 Vendor Performance / Scorecard (~6h):** new `SupplierPerformance` entity (OTD %, quality PPM, price variance, NCR count); wire into a NEW Supplier Performance tab on the Purchasing CC (§21 tab 13 — beyond the 10-tab IA we shipped). **PR-19 3-Way Match (~8h):** new `IInvoiceMatchService` promoting the existing `InvoiceMatchStatus` enum-only stub to an active matching engine with tolerance rules; tolerance breaches feed the Cost Exceptions tab. **PR-20 RFQ / Quote Flow (~8h):** 4 new entities (`SupplierRFQ` + `SupplierRFQLine` + `SupplierQuote` + `SupplierQuoteLine`); convert-winning-quote-to-PO-line; new Supplier RFQs tab. **CLOSES the cascade at 20/20.** Wave 4 character: each PR introduces EF migrations (`--output-dir Migrations`, never `--no-build`) so cycle time per PR is a touch longer than Wave 3's pure-service ships. ~34h total / ~2 sessions at current cadence. | TBD | 0 | 5 | — |
| **Sprint 14 — Maintenance CC** (sequence unchanged; PR slots locked) | **ADR-019 Asset ↔ WorkCenter ↔ Machine ↔ Department hierarchy (NEW 2026-05-19, PR #14.0)** is the prep PR — foundation for planning + scheduling + OEE + downtime allocation + the Spatial Map. Folds in: **Calibration (#14.5 / was #125) · Real OEE not synthesized (#14.6 / was #126) · RCM+FMEA (#14.7 / was #127) · Weibull (#14.8 / was #128)**. | TBD | 0 | ~9 | — |
| **Sprint 15 — Planning CC** (sequence unchanged) | Uses ADR-019 hierarchy. Demand exceptions by horizon. | TBD | 0 | ~4 | — |
| **Sprint 16 — Scheduling CC** (sequence unchanged) | Uses ADR-019 hierarchy. Jobs by machine/shift bucket. | TBD | 0 | ~4 | — |
| **Sprint 17 — Inventory CC** (sequence unchanged) | Cycle-count by zone/age. **Folds in Landed cost (#17.5 / was #130).** | TBD | 0 | ~5 | — |
| **Sprint 18 — Shipping CC** (sequence unchanged) | Orders by carrier cut-off. | TBD | 0 | ~4 | — |
| **Sprint 19 — Spatial Plant Floor Map** ⭐ NEW 2026-05-19 (was homeless PR #117.7; Dean: *"the demo killer behind the demo killer — no competitor has it"*) | **5 PRs.** Drag-drop layout · walls + zones · OEM thumbnails tinted by HealthScore · breakdown/cycle/thermal heatmap overlays · multi-floor + irregular plant shapes. Depends on ADR-019 hierarchy (Sprint 14) + Plant data from Sprints 14-16. v1.0 launches WITHOUT — this is the v1.1 hero feature that no surveyed EAM ships. | TBD | 0 | 5 | — |
| **Sprint 20 — Mobile PWA + DataWedge** (sequence unchanged) | Mobile-first cockpit re-flow + Zebra TC52/TC57 hardening + camera barcode + offline scan queue. | TBD | 0 | TBD | — |
| **Sprint 21 — MCP Server + Agentic AI Launch Package** ⭐ NEW 2026-05-19 (was ghost PRs #120 + #121; Dean: *"save until right before go-live"*) | **External AI integration suite** — ships as one coordinated package, not two separate PRs. Includes: MCP server publishing CherryAI tools (Anthropic / OpenAI / Mistral catalogs · partner ERPs · Slack/Teams) · Claude tool-use loop replacing one-shot `AiAssistantService.cs` · per-session conversation history · cost meter + rate-limit by tenant tier · SSE streaming. **Action required from Dean before sprint kicks: specify the partner / target list.** Until that's defined, Sprint 21 has no concrete scope. **Cross-cutting design principle (decision 2026-05-19):** every Sprint 6-21 PR designs services for AI-callability + voice-context emission + audit-loggable + idempotent — this is "the moat" baked in continuously, then formalized in Sprint 21. | TBD | 0 | ~6 | — |
| **Sprint 22 — i18n + Launch Polish** ⭐ NEW 2026-05-19 (was #137; Dean: *"en/es/fr-CA to start, can wait until before live"*) | **Last sprint before v1.0 launch.** ResX → .resx infrastructure · all Razor pages locale-aware · date / number / currency formatting per locale · language picker in user settings · launch hardening · final sparse-page pass. Three languages at launch: **en-US (primary) · es (Spanish) · fr-CA (French Canadian)**. Other languages defer to v1.1+ on customer demand. | TBD | 0 | ~5 | — |
| **Sprint 23 — v1.0 LAUNCH** | Go-live. | — | — | — | — |
| **Sprint 24+ — v2 AP/AR Control Center** ⭐ DECISION LOCKED 2026-05-19 (best-in-class principle) | **Tax Matrix · Payment Batch · OCR Invoice · ASC 842 Lease · ASC 360 Impairment** — ALL STAY V2. Reasoning: v1 positioning is "channel INTO customer's ERP, not replace it." Customer keeps SAP / NetSuite / Plex / QuickBooks for AP/AR. Best-in-class = "do the right things excellently," not "do everything." Splitting engineering focus across 40-60 AP/AR PRs in v1 would dilute the 7 Control Centers + voice + ERP-channel positioning. Real customer demand for AP features comes AFTER v1 ships. | v2 | — | — | — |
| **ERP integration connectors** (was Sprint 21+; absorbed into the Sprint 12B Receiving DEPTH ERP-channel work + interleaved across Sprint 13-18 outbound endpoints) | Connect CherryAI to customer's existing financial ERP. Targets: SAP (IDoc/BAPI/REST) · Oracle Cloud (REST) · NetSuite (SuiteTalk/REST) · D365 F&O (OData/Dual Write) · D365 BC · Sage Intacct · Acumatica · Epicor Kinetic · CSV/SFTP long-tail. Substrate already in place: ADR-014 idempotency, ADR-014 D3 Purview audit, Outbox pattern, ADR-015 ERP-friendly receipt schema. Per-customer connector lands when a customer-pull signal arrives — not pre-built. | TBD | 0 | TBD | — |
| **Total initiatives** | — | ~99 initiatives (+1: Sprint 13.5 / +1: Sprint 13.6 added 2026-05-25 / +1: Sprint 12.7 pulled-forward 2026-05-25) | **~104** | **~160** | **65%+ on shipped scope, accelerating; 48-hr velocity = 15+ PRs (Sprint 13.5 cleanup-pass + Sprint 13.6 mega-cleanup + Sprint 12.7 PRs #1/#2/#3)** |
| **Total merged PRs to main** | (audit-counted 2026-05-18 = 212; running tally 2026-05-27) | — | **~392** (HEAD = `2d37bfb` = PR #392, so +180 since 2026-05-18 audit; ~7+ PRs/day sustained velocity). Session 16 shipped PRs #390-#392 (validation pipeline + cost foundation + transfer engine). | — | — |

> 🔍 **FULL AUDIT 2026-05-18 — RECONCILIATION COMPLETE.** Three parallel audit agents reconciled MASTER_PLAN claims against (a) actual codebase inventory, (b) git history of `main`, (c) live AppDbContext + Migrations. Findings:
>
> **Verified:**
> - **212 PRs merged to `main`** (Jan→May 2026, 17 days, ~12.5 PRs/day velocity)
> - **384 Razor `.cshtml` files** under `Pages/` (much bigger app than the ~22 Phase 3 surfaces implied)
> - **175 DbSets** in `AppDbContext` covering ~10 domain groupings
> - **78 EF Core migrations** spanning Jan 2026 → May 2026
> - **20 Production tables** shipped under ADR-013 Phase E (claimed 18; +2 unclaimed: `RecipeRevisions` + `MrbDispositions`, bundled in `20260517_AddProductionBatchBackbone`)
> - **All 4 ADR-012 Phase D satellites** present (`CipWorkOrderDetails`, `QualityWorkOrderDetails`, `EngineeringWorkOrderDetails`, `HseWorkOrderDetails`) with proper FK + UNIQUE + ON DELETE CASCADE
> - **All ADR-014 Phase F infrastructure** verified: `VoiceReadyPageModel`, `IdempotencyMediator` (+ interface), `VoiceActionTagHelper`, `VoiceContextEmitter`, 30 `AddPolicy(...)` calls in `Authorization/AuthorizationPolicies.cs`, `VoiceSession` + `IdempotencyKey` models, `Result<T>` type, AuditLog 7-column AI extension
> - **10 design-system primitives** under `Pages/Shared/Primitives/`
> - **Equipment Catalog**: 14 classes / 51 OEM model rows — exactly as claimed
> - **9 [Fact(Skip=...)] xUnit tests** (matches the backlog memory)
>
> **Naming deviations (not functional gaps):**
> - AuditLog AI columns use **Microsoft Purview CopilotInteraction pattern**: `ActorKind, OnBehalfOfUserId, AiSessionId, AiCommandText, AiModelVersion, AiToolName, AiConfidence` — NOT the original plan body names (`AiInvokedByUserId / AiAgentId / AiPromptHash / AiResponseHash`). Functionally equivalent.
> - `WorkOrder` model lives **inside `Models/AssetMaintenance.cs`**, not its own file
> - `ApprovalWorkflow` model lives **inside `Models/SystemConfig.cs`**, not its own file
> - `VoiceContextEmitter` is at **`Services/Infrastructure/`** (no `Filters/` directory exists at all)
> - Sprint 3 Phase A-E PRs landed as GitHub PRs **#152-#176** (semantic `#119.X` labels in subjects). Searching by literal `(#119.X)` misses them — must look at PR subject lines.
>
> **PR-number gaps confirmed:** #3, #15, #81, #130 — closed-without-merge or never-opened. Investigate if any were meant to ship.
>
> **Cleanup recommended:** 66 local + 136 remote branches. Most `rel/*` and `feat/116d.*-*` branches are merged and prunable. Run `git remote prune origin && git branch --merged main | xargs git branch -d`.
>
> 🎉 **SPRINT 0 COMPLETE** (2026-05-16). All 17 audit-identified bugs (B-01 through B-27, less Sprint 1 deferrals) closed across 9 PRs. GitHub PR numbers: #100, #101, #102, #103, #104, #105, #106, #107 + #108 follow-up, #109. Three behavioral tests deferred to Sprint 1 fixture (#46): B-11 MACRS, B-13 PMOccurrence completion, B-17 rejection reversal.

> 🎉 **SPRINT 1 COMPLETE** (2026-05-16). All 6 PRs shipped: failure-mode Pareto (#112), per-asset MTBF + Availability (#113), reliability dashboard tiles (#114), Trial Balance (#116), Manual JE + Reversal (#119), and Period Close Orchestration (#120 + #121 cycle-fix). Live close of P1 2026 executed cleanly: depreciation $45,678.62 posted, TB rebalanced at $695,926.72, GR/IR clearing reconciled, period locked with 2,445-char immutable close packet.

> 🔄 **STRATEGIC PIVOT** (2026-05-16, mid-Sprint-2). **Mobile work is deferred until the production app is feature-complete.** The reasoning: we don't yet know which features will survive Sprint 2 + Sprint 3 polish, so committing PWA effort now risks porting a moving target. Once Sprint 3 wraps, we'll scope **Sprint 4 — Mobile v1** by picking the production features that actually want a mobile surface. Original Sprint 2 PRs for Mobile PWA shell and Live camera barcode have been removed from this plan and will be re-scoped at that point. Sprint 2 absorbs the brainstorm items (UX redesign, Plant Floor View, Admin v2, Help/Onboarding overhaul) plus the original Sprint 2 enterprise items (MCP, Agentic, RLS, MFA, SSO).

> 🎉 **SPRINT 3 PHASES A–D COMPLETE** (2026-05-17). Unified WorkOrder backbone (ADR-012 v0.2) shipped end-to-end. **Phase A** (PR #119.1, #119.1.2) — architecture lock + enum cleanup. **Phase B** (PR #119.2–#119.5, #119.5.1) — four config-backbone tables: WorkOrderFieldVisibility, WorkOrderStatusProfile + Label + Transition, polymorphic WorkOrderApproval, NumberSequence (SAP NRIV pattern). **Phase C** (PR #119.6, #119.7, #119.7.1, #119.7.2, #119.7.3) — added Revision/MasterWorkOrderId for revision chains, renamed MaintenanceEvent → WorkOrder (class + table + every FK column on child tables: Attachments, WorkOrderOperations, WorkOrderParts), guarded AuditEntityTypes UPDATE for envs that seed it at runtime, dropped stale rename artifacts in seed/config. **Phase D** (PR #119.8–#119.11) — four classification satellites: CipWorkOrderDetails (ASC 360-10 / 835-20), QualityWorkOrderDetails (ISO 9001 / FDA 21 CFR 820 / Ford G8D), EngineeringWorkOrderDetails (OSHA 1910.119 / ASME Y14.35), HseWorkOrderDetails (OSHA 29 CFR 1904 / ANSI Z10). All 1:0..1 with WorkOrder via UNIQUE on WorkOrderId, ON DELETE CASCADE. Replit live at commit `b834bbd`. CIP rename ("Construction in Progress" → "Capital Improvement Project") also shipped to split operator vs accountant vocabulary. Ship-workflow plugin hardened: gh CLI at `~/bin/gh` is the only sanctioned PR path (Chrome MCP fallback explicitly forbidden after caused downstream issues), `--auto` flag dropped because repo disables auto-merge.

> 🏛️ **ADR-014 PROPOSED + PR #177 OPEN** (2026-05-17). Phase F UI architecture locked before any code lands. **10 decisions** (D1-D10) covering: VoiceReadyPageModel base class, IXxxService+Result&lt;T&gt; pattern, AuditLog 7-column AI extension mirroring MS Purview CopilotInteraction schema, Stripe-pattern idempotency_keys in Postgres, resource-based AuthorizeAsync (AI never gets own identity), View Components per subtype, &lt;voice-action&gt; Tag Helper, voice_sessions Postgres table, Vendor.SendPoMethod data add, IPurchasingService.GetUnfulfilledPurchaseNeedsByVendorAsync. Backed by ~2,300 word research pass surveying Microsoft Learn (Razor Pages, Authorization, View Components, Tag Helpers, Caching, Purview Audit), Microsoft Purview CopilotInteraction schema, GitHub Copilot Enterprise audit fields, Stripe idempotency-keys reference, brandur.org, .NET community Result-pattern guidance. **Sprint 4 first PR scope locked**: foundational infrastructure (base class + mediator + tables + policies + Tag Helper + Result&lt;T&gt;), then 4 waves of UI PRs. Total Sprint 4: 14-17 PRs. Full doc: `docs/ADR-014-phase-f-ui-and-voice-readiness.md`.

> ⭐ **VOICE-FIRST AI CO-PILOT — SIGNATURE FEATURE LOCK** (2026-05-17). Captured as Sprint 5 + future. **The moat.** Every screen — desktop, laptop, mobile — gets a voice trigger. The electrician, technician, engineer, quality person, production worker, maintenance worker, planner, supervisor — anyone — can speak directly to the AI in the context of whatever screen + entity + tab they're on, and the AI executes real actions: Q&A, notes/comments, parts orders / purchase requisitions, inventory checks, alerts to other people, or any other registered command. *"I want people to shit when they see what we deliver."* SAP / Plex / Oracle don't have this; retrofitting it onto their UI takes them years; we can ship because we're designing for it from day one. **Phase F (Sprint 4) UI design must accommodate this** — every Razor page exposes a context object + every action goes through a callable service method + every mutation logs to AuditLog + every command is idempotent + every action runs under invoking-user permissions. Full vision lives in memory: `project_voice_ai_copilot_vision.md`. Sprint 5 ships the voice + STT + intent classifier + tool wrappers + audit + per-persona UX. **This is what turns CherryAI EAM from "an EAM" into "an AI co-pilot that happens to track assets" — category-defining.**

> 🎉 **ADR-013 PHASE E COMPLETE END-TO-END** (2026-05-17). All three phases shipped in one session: **#119.12 / E.1 (6f8208b)** ProductionOrder header + JobShopDetail + outside-op flags. **#119.13a / E.2 backbone (01b56af)** polymorphic ProductionBatch + Nest/ProcessBatch subtypes. **#119.13b / E.2 traceability (dcd2e0f)** StockReceipts + Remnants + MaterialMasters + CutListLines + heat number genealogy. **#119.14 / E.3 (aca6eb4)** polymorphic MaterialStructure + Bom + Recipe + MaterialStructureLines + RecipePhases + RegulatoryProfile (15 regimes). Together: **20 new tables + 7 column extensions** across the production schema (audit 2026-05-18 corrected from "18" — `RecipeRevisions` + `MrbDispositions` bundled in the backbone migration are the extras). **Three polymorphic primitives now live**: WorkOrder + classification satellites (ADR-012 Phase D), ProductionBatch + Nest/ProcessBatch (Phase E.2), MaterialStructure + Bom/Recipe (Phase E.3). Adding a future subtype (Formula, BatchTicket, AssemblyBatch, TestBatch) is now zero-schema-change. Backed by **two deep research passes** (~3,200 words each) surveying SAP PP-PI Campaigns, Plex, Oracle Cloud, DELMIA Apriso, Tulip, Fulcrum, IFS Cloud, ISA-95/B2MML, NADCAP AC7102 / AC7108, AS9100, IATF 16949 CQI-9, ASME, AWS D1.1, ASC 330. **Best-in-class moat: no surveyed ERP has this polymorphic combination at schema level.** All four shipped via new local `dotnet build` gate + green CI check (.NET 9 SDK at ~/.dotnet). Test coverage: PR #174 added 3 bash test layers (schema validation + integration scenarios + route smoke, 150+ assertions all PASS); PR #175 unblocked `dotnet test` in CI (224 pass, 9 skip, 0 fail). **Next: Phase F — UI / renderer for the new schema. Per-classification create/edit screens (ProductionOrder, ProductionBatch, MaterialStructure). Seed data for default RegulatoryProfile gates per regime. Field-visibility config integration.** Full memo: `docs/ADR-013-production-order-architecture.md`.

> **History:** 18 PRs shipped earlier this session (#82–#99) closed the original E2E punch list. They form the foundation this plan extends.

> 🏛️ **COMMAND CENTER PATTERN LOCK + RECEIVING PILOT KICKOFF** (2026-05-18). Dean's signature workspace direction for CherryAI EAM. Cherry Street (us) is the disruptor in manufacturing software; the manufacturer-customer's plant roles (Receiving Clerk, Buyer, Maintenance Tech, Planner, Scheduler, QC, AP Clerk, etc.) each get a single **Control Center** — one ultra-premium luxury workspace per role with **incredible visibility + super-intuitive functionality + AI controlling as much as possible**. The four-quadrant scaffold is **KPI strip + exception lane + drawer detail + activity feed**, borrowed from outside ERP (Bloomberg density + Linear calm + NASA mission-control role-stations + Datadog tiles + Airline-ops exception management + Stripe right-rail drawer). **None of those moves is reachable for SAP MIGO / Oracle Cloud / NetSuite WMS / D365 F&O / Plex / Epicor / Acumatica by incremental refactor** — they baked their UI architecture before voice, before AI, before profile-driven attributes were possible. Modern challengers (Cin7, Fishbowl, Katana, MRPeasy, Unleashed, Tulip, FactoryFix) win on UX but don't have the regulatory depth or AI substrate to layer on top. **Receiving is the PILOT** (Sprint 11) — building it first nails the scaffold (page shell, voice-form-spec layer, KPI strip language, exception lane, drawer-detail pattern, hardware-integration hooks, AI tool surface). Subsequent Control Centers (Sprints 12-20: Purchasing → Maintenance → Planning → Scheduling → Inventory → Quality → Shipping → AP/AR → HR/Crew) should be ~70% configuration on top of the scaffold. **Side effect:** `/Admin/StockReceipts/Edit` Create entry point is **dying** — Receiving Control Center owns creation; Edit may stay for admin fix-up, unlinked from sidebar. Task #178 (the PostgresException 22P02 JSON bug on the StockReceipts edit page) is INTENTIONALLY NOT being fixed (don't hotfix a page we're deleting). Research backing: `docs/research/receiving-control-center.md` (1,152 lines, 30 sources, May 2026 incumbent screens verified). ADR-016 locks the pattern + Receiving spec before code lands.

> 🔁 **COCKPIT-FIRST PIVOT — RECEIVING CONTROL CENTER RE-DIRECTED** (2026-05-18, post-Sprint-11 ship). Dean's call after walking the live `/Receiving` four-quadrant scaffold against the legacy Receiving Cockpit at `/Receiving/Cockpit-Legacy`: "The legacy receiving cockpit look and feel is much better. It looks better and it's a much better workflow. It's not even close in fact. Let's put that functionality into the Receiving Control Center for PO receiving and then do something similar for ASN and to do orphan PO's. The Control Center is a great idea but the legacy look and nav is WAY BETTER." **What this changes**: the *role-based Control Center idea* and *Control-Center-First sidebar IA* (ADR-016 / ADR-017) are kept; the *daily-driver canvas* for each Control Center inverts — the **Cockpit pattern** (time-bucketed queue on the left + detail preview pane on the right + KPI tile strip in the header) becomes the default workspace. The original four-quadrant scaffold (KPI strip + exception lane + drawer detail + activity feed) demotes to an "Exceptions" sub-tab — still the right shape for anomaly triage, just not the daily driver. **Why the Cockpit wins for daily work**: (1) Receiving clerks live in a queue, not a dashboard — time buckets (Overdue / Today / This week / Later) match how the work feels, (2) detail preview pane lets you scan 30 POs/hour without modal churn, (3) header KPI tiles give the supervisor the same numbers without a context switch, (4) it's the pattern every operator-first tool reaches for (Linear, Front, Superhuman, Bloomberg) and that *none* of SAP MIGO / Oracle Cloud Receipts / NetSuite WMS / D365 Receiving / Plex / Epicor / Acumatica ship as their default — they're all CRUD tables. **What we keep from Sprint 11**: every service-layer piece (`IReceivingControlCenterService`, `ReceiptStateMachine`, 10 `IReceiptVoiceTools`, ADR-015 profile-driven drawer body, DataWedge focus mode), every Sprint 11.5 piece (Control Centers sidebar spine, Quick Actions tray, Settings drawer, Approvals bell, `ControlCenterRegistry`), and the four-quadrant primitives (`_KpiStrip`, `_ExceptionLane`, `_ActivityFeed`, `_ControlCenterShell`) — they remain reusable across every Cockpit's KPI header + Exceptions sub-tab. **What changes**: `Pages/Receiving/ControlCenter.cshtml` becomes a four-tab shell (**PO Queue · ASN Queue · Orphans · Exceptions**) where each queue tab is the Cockpit pattern. Lock + scope in **ADR-018**.

> ⭐ **SPRINT 12C PR #1 + PR #1.5 SHIPPED 2026-05-20.** First production vector-store row writes live. **`vector` 0.8.0 extension verified on Replit-managed Postgres** (no host migration needed for Phase 1). **`Embeddings` table** (polymorphic, `halfvec(1024)`, HNSW index on `halfvec_cosine_ops`, RLS tenant policy) + **`PendingEmbeddings` queue** (Postgres SKIP LOCKED leasing, hash-based skip, exponential backoff with Retry-After). **12 ReceiptProfile embeddings** live in halfvec storage via Voyage `voyage-3-large/v1`. **Semantic search proven end-to-end live:** query "steel mill heat number traceability" → STEEL profile ranks #1 by cosine distance 0.506, OIL_GAS #2, AUTOMOTIVE #3, AEROSPACE #4, CHEMICAL #5 — no exact-match keywords required. The voice/AI moat just got real. PR #1.5 (worker hardening) added: 10s → 60s backoff schedule, RFC 7231 Retry-After header honored, `POST /_admin/embed/retry` endpoint to recover stuck rows without psql. **Dean upgraded Voyage to paid tier 2026-05-20.** 5 gotchas captured to memory: [Migration] attribute required on hand-written migrations · `AppDbContextModelSnapshot.cs` has pre-existing drift blocking `dotnet ef migrations add` (separate dedicated PR) · Pgvector entities need `if (Database.IsNpgsql())` provider guard for Sqlite/InMemory test contexts · Voyage free tier 429s after ~3 rapid requests · `CREATE EXTENSION vector` available to app user on Replit. PRs #260 (ADR-021 docs), #261+#262 (PR #1 + 2 hotfixes squashed into one commit), #263 (PR #1.5). Sprint 12C 3/5 done; **next: PR #2 — embed Item + Vendor + WorkOrder + AuditLog.AiCommandText** (first real stress-test of the hardened worker against ~15K rows). Memory: `[project_sprint_12c_pr1_shipped]` + `[reference_replit_postgres_extensions]` + 4 feedback files.

> ⭐ **VOICE MVP SHIPPED + ADR-020 ACCEPTED + ROADMAP RESHUFFLED 2026-05-19 (evening).** Voice MVP landed end-to-end in 5 PRs (#254 + #255 + #256 + #257 + #258). `/Receiving` Voice button drives a full utterance → tool → narration loop on top of the dormant `IReceiptVoiceTools`. All 6 intents verified live: ExpectedArrivals · LookupReceipt · ExplainException · Help · Unknown · (Lot lookup via jsonb second pass). Every invocation writes an `AuditLog` row with `ActorKind=AiOnBehalfOf` + full AI columns (Purview pattern). Same day, **ADR-020 Postgres-as-AI-Native-OS** locked the database direction: ONE Postgres instance with `pgvector + pgvectorscale + Apache AGE + ParadeDB + TimescaleDB + pgai`. No second DB (no Pinecone, no Neo4j, no ElasticSearch). 5-phase plan (Vector → Graph → Full-text → Multi-modal → Production hardening). Cost-dominant on both axes vs the multi-DB alternative (~$300-600/mo vs ~$620-770/mo). **Roadmap reshuffle — Dean's call:** ADR-020 Phase 1 jumps the queue. New **Sprint 12C (Vector Layer)** is the immediate next sprint; new **Sprint 12D (Apache AGE Graph)** is HARD-DEADLINED to **June 3 EVS pitch** because Joe needs to see a graph-narrated "why is X late" demo. Sprints 12B (Receiving DEPTH) / 12.5 (Enterprise Hardening) / 13 (Purchasing CC) all defer behind 12C+12D. **Why it matters:** voice/AI is the moat; vector intent classification + graph reasoning are the two AI capabilities zero Tier-1 ERPs (SAP / Plex / NetSuite / Acumatica) can match without a multi-year platform rewrite. Three competitive wedges this unlocks: (1) voice ask → SQL+vector+graph join in <300ms · (2) LLM-narrated "explain why X happened" with full graph context · (3) per-vertical semantic search via profile embeddings. Memory: `project_voice_mvp_shipped.md` + `project_database_direction_2026_05_19.md`. ADRs queued: **ADR-021** (embedding model + pipeline, recommend Voyage `voyage-3-large`), **ADR-022** (Apache AGE graph schemas), **ADR-023** (multi-modal pipeline for mill certs).

> 📦 **ITEM MASTER + MULTI-DIM INVENTORY ROADMAP CAPTURED 2026-05-18.** Dean flagged ~150 missing fields in the parts catalog (regulatory, MRP, quality, classification) and the need for **multi-dimensional inventory** (lot/serial/status/owner/quality/variant/project dims, vs today's flat key). Plus the ItemEdit page itself ("a ton of real estate") needs a restructure. Captured as **Sprint 7 (Item Master Expansion, 5 PRs), Sprint 8 (Multi-Dim Inventory, 7 PRs), Sprint 9 (ItemEdit Restructure, 6 PRs)** — 18 PRs total. Full research doc: `docs/research/item-master-and-multi-dim-inventory.md` (covers SAP MM / Oracle Cloud / D365 F&O / NetSuite / Maximo / Infor / Plex / ISO 8000-110). **Not blocking current Sprint 4 Phase F Wave 1.** The MaterialMaster admin coming up next is heat-number / mill-cert traceability from Phase E.2 — distinct from the Item Master parts catalog.

---

## 📋 Master Plan Audit 2026-05-29 PM (CANONICAL — refreshed for B7 + B11)

> **AUDIT REFRESH 2026-05-29 PM** — Dean directive *"audit and update the Master Plan."* Reconciles the plan against main HEAD = **`65ab87f`** (PR #438). Since the AM audit (#421, which closed the Purchasing Cascade at #420), the codebase executed **two new manufacturing-core themes: Theme B7 (PO-as-Standard + Make-or-Buy) Waves A+B, and Theme B11 (Resource Model & Finite Scheduler) Waves R1→R3** — 17 feature PRs (#422–#438). The AM audit's tables below remain accurate for everything through #420; this refresh adds B7 + B11 on top.
>
> **What's closed since the AM (#421) audit:**
>
> | Theme | Ships | Status |
> |---|---|---|
> | **Theme B7 — PO-as-Standard + Make-or-Buy, Wave A** | #422 SourcePattern duality · #423 master-optional PoFirst PO release · #424 estimate-as-standard variance | ✅ **CLOSED 3/3.** |
> | **Theme B7 — Wave B (crystallization, the BIC differentiator)** | #425/#426 ItemCrystallization entity + fingerprint · #427 IItemCrystallizationService · #428 first-actual cost + StandardCostBasis | ✅ **CLOSED.** ETO job: quote → master-less release → cost-close → crystallize reusable costed standard at ship. |
> | **Theme B11 — Resource Model, Wave R1 (org backbone)** | #429 Department backbone · #430 WorkCenter hardening + SiteId + WorkCenterAlternate · #431 WC cost + op-defaults | ✅ **CLOSED 3/3.** |
> | **Theme B11 — Wave R2 (resource layer)** | #432 ProductionResource + Asset bridge · #433 Tool/Fixture + Labor/Vendor/Tool bridges · #434 per-resource calendars + finite envelope | ✅ **CLOSED 3/3.** R4 now has its 3 inputs (what-is / when-available / how-much). |
> | **Theme B11 — Wave R3 (capability model — the scheduling disruptor)** | #435 Capability + ResourceCapability · #436 OperationCapabilityRequirement (retires CSV) · #437/#438 ICapabilityMatchService eligible-resource resolver | ✅ **CLOSED — Wave R3 complete.** Match by capability, not hard-coded machine. |
>
> **B11 theme structure (was absent from this plan; capturing it here — full spec in `docs/research/resource-model-cascade-design.md`):** Theme B11 = the missing coordination layer the 05-29 codebase audit found (`Capability` was 0%, the scheduler a stub). 4 waves: **R1 Org backbone** (Department / WorkCenter hardening) ✅ · **R2 Resource layer** (ProductionResource bridged to EAM Asset + Tool/Labor/Vendor + calendars + finite envelope) ✅ · **R3 Capability model** (Capability + ResourceCapability + OperationCapabilityRequirement + match resolver) ✅ · **R4 Finite scheduler** (R4-10 IResourceLoadService real Load%/drum → R4-11 finite backward scheduler replacing the BackwardSchedulingService stub, using R3-9 for alternates → R4-12 dispatch board) ⏳ **THE FRONTIER — next up.** B11 is sequenced *before* B7's make-or-buy wave so B7's F2 capacity factor launches on a real finite scheduler, not the coarse proxy.
>
> **B7 remaining:** Wave C make-or-buy (`IMakeBuyDecisionService` — F2 consumes R4 finite Load%) + Wave D (cockpit/voice) — **both gated on B11 R4.**
>
> **Gate:** ⚠️ everything #422–#438 (all of B7 + B11) is **dev-preview-only** — prod (industryos.app) Republish-with-Copy owed for B7/B11. EVS pitch Wed June 3 still open.
>
> ---
>
> **AUDIT COMPLETE 2026-05-29 (AM)** — Dean directive *"audit and update the Master Plan."* Reconciles the plan against the live ship history (git log on main HEAD = **`c98015d`**) + all post-05-27 ship memories. The headline: since the 05-27 audit, the codebase executed **(a) the entire 05-27 canonical 21-ship sequence** AND **(b) a NET-NEW 20-PR Purchasing Cascade** (Sprint 15.1–15.4) that did not exist in any prior audit. Both are now substantially complete.
>
> **What's closed since the 05-27 audit:**
>
> | Theme | Ships | Status |
> |---|---|---|
> | **B8 PO Cockpit** (12-PR cascade per `po-cockpit-spec`) | PR-PRO-3…PRO-11 + transaction-drawer UI + 3-mode gating + 14 validators (#377–#395 band) | ✅ **CLOSED 11/12** (PRO-12 reporting deferred — another app bolts on). |
> | **Sprint 14.4 Cost Engine** (Dean's 910-line cost-object-graph research) | PR-1 CostTransaction `6b89b30` → PR-2 child→parent transfer `2d37bfb` → PR-3 rollup `2bf73cd` → PR-4 variance+close `a4254f5` → PR-5 cost cockpit `02a1c9d` | ✅ **CLOSED 5/5.** |
> | **Sprint 15.1 Wave 1 — Purchasing Foundation** | PR #396–#400 (Receipt-to-Job · ProductionSupplyDemand · PO↔demand link · SubcontractOperation dual-demand · Vendor WIP) | ✅ **CLOSED 5/5.** |
> | **Sprint 15.2 Wave 2 — Subcontract Flow** | PR #404–#407 (Shipment/Receipt · §5 8-step orchestrator · §12 costing · cockpit panel) | ✅ **CLOSED 4/4.** |
> | **Sprint 15.3 Wave 3 — Purchasing Command Center** | PR #409–#414 (BuyerActionState · `/Purchasing/ControlCenter` 10-tab IA · auto-PO §16 · consolidation §17 · recommendation §18) | ✅ **CLOSED 6/6.** |
> | **Sprint 15.4 Wave 4 — Polish + Integration** | PR #416–#420 (PO Ack · PO Amendment · Vendor Scorecard · 3-Way Match · RFQ/ranked-award) | ✅ **CLOSED 5/5 — CLOSES the 20-PR cascade 🎉.** |
>
> **Plan-vs-reality reconciliations (stale slots now corrected):**
> 1. The **20-PR Purchasing Cascade (Sprint 15.1–15.4)** SUPERSEDES the 05-24 Wave-map's *"Wave 5 — Sprint 13 Full Purchasing CC (8 PRs, queued)"*. What shipped is 2.5× bigger and built on the §1-§25 purchasing/subcontracting/supply-demand research spec (`purchasing-subcontracting-supply-demand-dean-research.txt` + `purchasing-cascade-design-2026-05-28.md`), not the old Sprint-13 sketch. Treat Wave-5 in the 05-24 block as **DELIVERED (expanded)**.
> 2. Legacy **Sprint-3 reroute item #129 Vendor performance scorecards** (was "→ Sprint 13 PR #13.5") is **DELIVERED** by PR #418 (`SupplierPerformance`).
> 3. Legacy **#132 Blanket-PO / Contract-PO** (was "→ Sprint 13 PR #13.6") is **NOT delivered** — it lives in the future **Theme B7 (PO-as-Standard)** space.
> 4. **3-Way Match** (PR #419) promoted the long-standing enum-only `InvoiceMatchingService` stub to an active tolerance-driven engine — closes a P2P credibility gap.
>
> **The 05-27 canonical 21-ship sequence is retained below for history but is no longer the execution frontier** — items 1–20 of it shipped; item 21 (Sprint 14.5 unified Item view) remains the one open carry-forward from that block.
>
> **Genuine remaining queue (nothing in-flight; await Dean):**
> - **Future themes (research/brainstorm captured, not started):** B7 PO-as-Standard + Make-or-Buy · B9 Customer Project Manager Module (~100-150h greenfield) · B10 Teams Collaboration.
> - **Long-queued hardening (still real):** Sprint 12.5 Enterprise Hardening (MFA/SSO/Postgres-RLS) · Sprint 18 Maintenance CC (Calibration/OEE/RCM-FMEA/Weibull) · Sprint 12D Apache AGE graph ("why is X late") · Sprint 14.5 unified Item view.
> - **Gates:** EVS pitch **Wed June 3** (5 days out, still open) · prod **Republish-with-Copy** owed for all of Sprint 15.4 (Wave 4 is dev-preview-only).
>
> **NEW HARD PATTERN encoded this session:** **cross-service transaction enlistment** — a composed service shares the scoped `AppDbContext` + bare `SaveChanges` so its writes (and its `OutboxWriter` event) enlist in the caller's open transaction; never self-commit a callee inside an outer transaction. PR-17 + PR-19 depend on it.
>
> ---

## 📋 Master Plan Audit 2026-05-27 (SUPERSEDED 2026-05-29 — kept for historical reference)

> **AUDIT COMPLETE 2026-05-27 AM** — Dean directive *"audit the codebase and update the master plan."* Reads MASTER_PLAN.md + walks live codebase (Models/ 84 top-level + 14 sub-dirs · Services/ 40 top-level + 35 sub-dirs · Pages/Admin/ 43 pages + 13 sub-dirs · Migrations/ 165 files · AppDbContext.cs 38+ MapXminRowVersion + 3 latent IsRowVersion() at lines 2701/2754/2816) + the B8 PO Cockpit spec + B7/B9 brainstorm docs + all post-05-24 ship memories. **Main HEAD now `813d399`** (PR #367 Sprint 14.3 PR-1 ECR/ECO Change Control substrate). Full memo: `docs/research/master-plan-audit-2026-05-27.md`. Memory: `reference_master_plan_audit_2026_05_27.md`.
>
> **What's happened since the 05-24 audit (3 calendar days, 13 PRs):**
>
> | Wave bucket | Ships | Effect |
> |---|---|---|
> | **B6 Foundation Sprint (Wave 4.5)** | PR #355 → #356 → #357 hotfix → #358 → #359 → #360 → #361 → #362 → #363 (10 ships) | **CLOSED.** Item Master fully classifiable + multi-plant capable + 8-element cost split + FIFO/LIFO/Average inventory valuation + customer item cross-reference + AS9100 §8.4.1 sourcing rules + 18-column expansion. |
> | **Sprint 14.1 PR-1 (Wave 4.6)** | PR #364 `e79da2d` + PR #365 xmin hotfix `e7dd547` | **CLOSED.** Per-PO frozen BOM snapshot (ProductionMaterialStructure entity + IPoSnapshotService). xmin HARD LOCK encoded. |
> | **Sprint 14.2 PR-1 (Wave 4.7)** | PR #366 `8350a0f` + fixup `645ec5c` (3 Codex P1s in-PR) | **CLOSED.** DMS substrate (Document + DocumentVersion + ItemDocumentLink + IDocumentService — controlled engineering artifacts with revision lifecycle + content-hash idempotency + atomic supersede on release). 5-write-button probe caught a cross-tenant gap no unit test had. |
> | **Sprint 14.3 PR-1 (Wave 4.8)** | PR #367 `813d399` + fixup `97e3008` (3 Codex P1+P1+P2 in-PR) | **CLOSED.** ECR/ECO Change Control substrate (EngineeringChangeRequest + EngineeringChangeOrder + EcoLineItem + EcoApproval + IEcrEcoService 10 ops). 8-write-button probe proved full 13-op cycle live. |
>
> **3 NEW HARD LOCKS encoded since 2026-05-24:**
> | Lock | Title | Trigger |
> |---|---|---|
> | **xmin pattern for concurrency tokens** | `MapXminRowVersion(x => x.RowVersion)` + `byte[]? RowVersion`. NEVER `IsRowVersion()` on `bytea NOT NULL` — PG can't auto-populate, every INSERT throws 23502. Latent in 3 entities (see §1.5 below). | PR #364 → PR #365 hotfix loop |
> | **Sprint + entity naming — no vendor implication** | Sprint titles + branch names + entity classes must NOT imply integration with a vendor we don't actually integrate with. Sprint 14.3 was renamed from "Arena PLM ECR/ECO" → "ECR/ECO Change Control." Design-pattern references in entity-header comments are fine. | PR #367 prep — `reference_sprint_naming_no_vendor_implication.md` |
> | **Lock 16 corollary — probes must exercise write paths** | Every admin probe MUST include INSERT/UPDATE buttons (Create / AddVersion / Approve / Release / Link / etc.) so the write path is exercised in E2E before merge. Read-only probes hid the latent xmin bug across CostLayer/ItemSourcingRule/CustomerItemXref. | PR #366 (5 writes caught cross-tenant gap) + PR #367 (8 writes proved 13-op cycle). Memory: `feedback_lock16_corollary_probes_exercise_writes.md` |
>
> **7 audit questions need Dean's 1-click direction** (full detail in `docs/research/master-plan-audit-2026-05-27.md` §6):
>
> 1. **A1 — PR-XminBackfill (1.5h defensive PR)** — bundle the 3 latent xmin fixes (CostLayer / ItemSourcingRule / CustomerItemXref at AppDbContext.cs lines 2701/2754/2816) into a single defensive ship before B8 PR-PO-1? Identical fix 3×, cheaper bundled. **Recommendation: YES.**
> 2. **A2 — B8 + Sprint 14.3 PR-2..7 alternating cadence** — alternate ships so both cascades land in parallel (different model surfaces, low merge risk: B8 = Production/Materials; 14.3 = Engineering/change-control). **Recommendation: YES.**
> 3. **A3 — Renumber legacy Sprint 14 Maintenance CC → Sprint 18 Maintenance CC.** "Sprint 14" is now claimed by the B6/14.x post-foundation cascade. Old Sprint 14 (Calibration #125 / OEE #126 / RCM+FMEA #127 / Weibull #128 / ADR-019) becomes Sprint 18, slotted post-B8 cascade. **Fold ADR-019 draft INTO B8 PR-PO-7** — the "Maintenance Clear" 8-readiness-check forces the asset-hierarchy decision in code. **Recommendation: YES.**
> 4. **A4 — EVS June 3 demo path admin-probe-driven** (PoCockpitProbe + EcrEcoProbe full Deviation→Waiver→Concession cycle), NOT customer-facing PR-PO-8 cockpit? At peak ship cadence we can land PR-XminBackfill + PO-1 + PO-2 + PO-3 + 14.3 PR-2/3/4 ≈ 7 ships by June 3. PR-PO-8 customer cockpit is post-EVS. **Recommendation: YES.**
> 5. **A5 — Theme B7 + B9 research sequential post-B8 cascade close** — B7 (PO-as-Standard + Make-or-Buy) ahead of B9 (Customer Project Manager Module, ~100-150h greenfield). **Recommendation: YES.**
> 6. **A6 — Sprint 13.5 PR #6/#7 disposition** — RETIRE PR #6 (subcontract chain folded into B8 PR-PO-4 outside-operation type variant). KEEP PR #7 (voice intents fold into Sprint 14.5 unified Item view voice surface). **Recommendation: YES.**
> 7. **A7 — ADRs status** — ADR-019 drafted in-code with B8 PR-PO-7. ADR-023/024/026 unchanged from 05-24 audit. **Recommendation: YES.**
>
> **21-ship recommended sequence** (chronological, ~280-380h code, ~25-40 session days at peak cadence):
>
> | # | Ship | Effort | Slot |
> |---|---|---|---|
> | **1** | PR-XminBackfill — defensive (drop bytea + remap to xmin on CostLayer + ItemSourcingRule + CustomerItemXref) | 1.5h | **Wave 4.85 — IMMEDIATE** |
> | **2** | B8 PR-PO-1 — PO header field expansion (8 OrderType variants + 8-state status machine + Priority/Planner/Supervisor + Freeze flags + PromiseDate + LotSerial req + WI/Drawing rev) | 8-12h | Wave 4.9 — kickoff |
> | **3** | Sprint 14.3 PR-2 — Deviation entity | 6-10h | Wave 4.9 parallel |
> | **4** | B8 PR-PO-2 — BOM line expansion (10 qty cols + 17-state status + flags + Supply Type + Issue Timing + Lot/Serial tracking + substitution + per-line accounts). Absorbs B5a. | 16-20h | Wave 4.9 |
> | **5** | Sprint 14.3 PR-3 — Waiver entity | 4-8h | Wave 4.9 parallel |
> | **6** | B8 PR-PO-3 — ProductionMaterialTransaction + 12-action service + 6 job-to-job rules. Absorbs B2. | 20-30h | Wave 4.9 |
> | **7** | Sprint 14.3 PR-4 — Concession entity | 4-8h | Wave 4.9 parallel |
> | **8** | B8 PR-PO-4 — ProductionOperationTransaction state machine + 19 actions. Absorbs B3 + B5b. | 20-30h | Wave 4.9 |
> | **9** | Sprint 14.3 PR-5 — Customer Notice + Supplier PCN | 8-12h | Wave 4.9 parallel |
> | **10** | B8 PR-PO-5 — WIP move | 6-8h | Wave 4.9 |
> | **11** | Sprint 14.3 PR-6 — CAR + CAPA | 10-14h | Wave 4.9 parallel |
> | **12** | B8 PR-PO-6 — Complete/Scrap/Rework modals | 16-20h | Wave 4.9 |
> | **13** | Sprint 14.3 PR-7 — Impact + redline + FAI re-trigger. **CLOSES Sprint 14.3.** | 16-22h | Wave 4.9 close |
> | **14** | B8 PR-PO-7 — "Can I run this?" 8-readiness-check + drafts ADR-019 in code. **Highest-leverage smarter-than-BIC differentiator.** | 12-16h | Wave 4.9 |
> | **15** | B8 PR-PO-8 — `/Production/Orders/{id}/Cockpit` customer-facing surface (12 tabs + 24-col BOM grid + 22-col Routing grid + 16-metric top bar) | 20-30h | Wave 4.9 |
> | **16** | B8 PR-PO-9 — Transaction drawer UI pattern | 12-16h | Wave 4.9 |
> | **17** | B8 PR-PO-10 — 3-mode UI (Planner/Supervisor/Operator) | 6-8h | Wave 4.9 |
> | **18** | B8 PR-PO-11 — 14 validators parallel | 16-20h | Wave 4.9 parallel |
> | **19** | B8 PR-PO-12 — reporting service. ⏸️ **DEFERRED** — another app bolts on for reporting. B8 CLOSED at 11/12. | — | — |
> | **20** | Sprint 14.4 cost engine — cost-object graph per Dean's 910-line research. **PR-1 SHIPPED** (CostTransaction/CostTransfer/ProductionOrderCostSummary + 5 enums + 18 GlAccountKinds + SiteIdSnapshot + cost posting integration, PR #391 `6b89b30`). **PR-2 SHIPPED** (child-to-parent transfer engine + Layer B anti-compounding + cross-site, PR #392 `2d37bfb`). PR-3 (rollup engine) + PR-4 (variance+close) + PR-5 (cost cockpit UI) remain. | ~60-80h total | Wave 4.95 |
> | **21** | Sprint 14.5 unified Item view — customer-facing Item card (bill of drawings + change history + sourcing + cost) | 16-24h | Wave 4.95 close |
>
> **EVS-realistic cut (by June 3, 7 days):** PR-XminBackfill + PO-1 + PO-2 + PO-3 + 14.3 PR-2/3/4 = 7 ships at peak cadence. Customer-facing PR-PO-8 cockpit is post-EVS — EVS pitch sells the **architecture** (ECO → DocumentVersion → ProductionMaterialStructure → ProductionMaterialTransaction → CostLayer → GL chain of custody) on dev preview via admin probes.
>
> **Latent xmin entities — concrete fix:** AppDbContext.cs line 2701 (`CustomerItemXref`) / line 2754 (`ItemSourcingRule`) / line 2816 (`CostLayer`) all carry `e.Property(x => x.RowVersion).IsRowVersion();` on `bytea NOT NULL`. Will throw PG 23502 on first INSERT. **Dormant** because existing probes are read-or-update-only. **Fix:** drop bytea column + `MapXminRowVersion` re-wire in AppDbContext + `byte[] RowVersion` → `byte[]? RowVersion` on entity property + add Insert button to each probe per Lock 16 corollary. Pattern proven by PR #365 hotfix migration `20260526235021_DropPoSnapshotBytesRowVersionFs141Pr1P1.cs`.
>
> ---

## 📋 Master Plan Audit 2026-05-24 (SUPERSEDED 2026-05-27 — kept for historical reference)

> **AUDIT COMPLETE 2026-05-24** — Dean directive *"audit the master plan and put everything in order, including things we have skipped over but still need to resolve."* Reads MASTER_PLAN.md (1763 lines) + git log on main HEAD = **`177fb53`** (PR #5d squash) + all ADRs + memory locks. Surfaces every skipped item, collapses 31 organically-numbered priority blocks into a clean **16-Wave execution spine**. Full memo: `docs/research/master-plan-audit-2026-05-24.md`. Memory: `reference_master_plan_audit_2026_05_24.md`.
>
> **5 audit questions need Dean's 1-click direction** (in addition to the 8 Master Files Baseline questions):
> 1. **Sprint 4 Phase F Wave 1** (Priority 1 below) — ✅ **RETIRED 2026-05-24** as PRA-7 lands the substrate (LotMaster + SerialMaster as proper masters supersedes the MaterialMaster admin ask; WarehouseMaster + BinMaster supersede the Locations follow-up). Admin CRUD pages can be built later as thin Pages over the masters now that the schema is locked. RegulatoryProfile admin defers to Sprint 14 Quality CC. Vendor edit becomes trivial once PRA-10 lands.
> 2. **Sprint 12.7 Controller Cockpit** (Blueprint Absorption Item 3) — pull forward to Wave 4 post-EVS (Jun 11-17)? Recommendation: YES. ✅ **ANSWERED YES + PULLED FORWARD AGGRESSIVELY 2026-05-25/26 — 3 of 5 PRs shipped already (#345 `5176a1b`, #346 `614a738`, #347 `aad2590`); PR #4 staged blocked on iCloud; PR #5 queued. ~16 days ahead of plan.**
> 3. **ADR-019** — ✅ **ANSWERED YES + SHIPPED 2026-05-24** as `docs/ADR-019-wms-posting-profile-pattern.md` (PRA-7 sibling, PR #317). Scope refined from the original "Asset↔WorkCenter↔Machine↔Department hierarchy" framing to "WMS hierarchy + PostingProfile pattern" — that's where the architectural lock actually had to land (SAP/Dynamics separation vs NetSuite collapse). The Asset↔WorkCenter↔Machine↔Department hierarchy piece is still open and slots as a future ADR (likely ADR-027 Sprint 14 prep).
> 4. **ADR-024 double-claim** — split: ADR-024 = Tool Registry (Wave 14); new ADR-027 = Q3 host migration. Recommendation: YES.
> 5. **ADR-025 trichotomy** (3 sibling files at same number) — cleanup slot Wave 6 Workstream D. Recommendation: YES.
>
> **Audit findings:** 12+5 PRs shipped in Sprint 13.5; 6 of 8 in Sprint 12D; ADR-019/023/024/026 missing; ADR-025 over-loaded; 7 Sprint 12B deferred items + Master Files Baseline cascade + MES Event cascade + Sprint 12.7 Controller all needed top-level priority slots — now installed below.
>
> **🎯 ACTIVE WAVE 2026-05-26 PM: Wave 4 (Sprint 12.7 CFO motion) + NEW Wave 4.5 (Sprint 12.8 COO motion) running in parallel toward ABS Thursday gate.** Sprint 12.7: 4 of 5 PRs shipped (`5176a1b` + `614a738` + `aad2590` + `2dbcfb9`); PR #348 shipped 2026-05-26 PM with Codex catching + fixing 3 multi-tenant data-leakage bugs (P1+P1+P2 on FinanceKpiService) before merge — branch protection refused admin-override past unresolved threads, the right outcome. PR #5 re-scoped CFO-only (data bumps + `/Controller/Walkthrough` + TimescaleDB cleanup + Republish). Sprint 12.8 NEW 2026-05-26 PM: Dean directive to also demo full COO motion (10-level BOM + multi-plant + subcontract + cost rollup for Shadi Mohaisen on Thursday). 5 PRs queued — schema migration + ADR-028 + backward-scheduler stub + ABS scenario seeder + `/Production/Walkthrough` page. Inserted as sibling sprint to preserve Plan integrity, NOT absorbed into Sprint 12.7. Per the 2026-05-26 production audit, ~80% of schema is built; the new compute is intentionally stub-level (real engines stay Sprint 0.5 + Sprint 14). Lesson #18 also encoded 2026-05-26: first diagnostic for missing CI runs is githubstatus.com API check (1 second; prevents hour-long spirals during GitHub-side outages — proven during PR #348's 4-hour stall). ABS demo gate (Wave 1.3) = Thursday May 28 (2 days). EVS gate (Wave 2.9) = Wednesday June 3 (8 days). Carry-forwards waiting: PRA-11 (Pack hierarchy) · MES Event cascade (PR #5e/5f/5g — biggest backend gap) · `/Inventory/List/{id}` HTTP 500 · `Asset.cshtml`+`WorkOrders/Details.cshtml`+`ItemEdit.cshtml` deep rebuilds (3 worst form-density offenders) · PRA-5h-5k Disposal/Transfer/Revaluation posting · `JournalEntry.SourceModule`+`SourceDocumentId` ADR-tracked migration (Codex P1 catch from PR #346) · TimescaleDB removal migration · `AppDbContext` partial-class split (228 DbSets in one 4115-line file) · `EnsureCreatedAsync` trap removal from `Data/Seed.cs:13` · `PendingModelChangesWarning` suppression root-cause fix (8-entity FK config drift).
>
> **Wave map** (audit-corrected execution order):
>
> | Wave | Title | Window | Status |
> |---|---|---|---|
> | **1.1** | Sprint 13.5 in flight (12+5 main shipped) | through 2026-05-24 | ✅ shipped — all 12 main + 5 hardening + 8 baseline (PRA-4/5a/#5c.4/PRA-6/PRA-7/PRA-8/PRA-9/PRA-10) + 7 cleanup-pass (PRA-5b/5c/5d/5e/5e.2/5f/5g) |
> | **1.2** | Master Files Baseline PRE-ABS (PRA-4 UOM + PRA-5a COA additive + PR #5c.4) | Mon May 25 – Wed May 27 | ✅ shipped 2026-05-24 — PRA-4 `bbaaf9a`/#312, PRA-5a `479432a`/#313, #5c.4 `27d88f3`/#314 + #5c.4.1 hotfix `99e795f`/#315 |
> | **1.3** | **ABS Thursday demo gate** | **Thu May 28** | ⏳ gate (2 days away) |
> | **2.1-2.8** | Post-ABS / Pre-EVS: PRA-5b + PRA-6 + PRA-7 + ADR-019 + ADR-026 + Sprint 12D perf + Sprint 13.5 PR #6/#7 | Fri May 29 – Tue Jun 2 | ✅ MASS-SHIPPED 2026-05-24 (~7-10 days early): PRA-6 `7bde5e7`/#316 + PRA-7 `f613cb3`/#317 + ADR-019 + PRA-8 `4d0fa2c`/#318 + PRA-9 `f5d5c0d`/#319 + ADR-027 + PRA-10 `e9c1e81`/#320 + the entire AccountingKey DEF-008 cleanup-pass cascade: PRA-5b `71499de`/#325, PRA-5e.2 `f870923`/#329 (seed missing system GlAccounts), PRA-5c `9f9bf5e`/#326 (ApPostingService dual-write), PRA-5d `6d4439a`/#327 (ReceivingPostingService), PRA-5e `0f24412`/#328 (CipCapitalizationService), PRA-5f `bc65f7a`/#331 (CapitalImprovementPostingService), PRA-5g `13c05b7`/#344 (JournalGenerator depreciation). ADR-026 + 12D perf + #6/#7 + PRA-11 still queued. |
> | **2.5** | **Sprint 13.6 UI Audit + Cleanup** (4-agent parallel audit → 6 PRs). Priority 1.62a. Parallel workstream — doesn't block MES events. | Parallel with Wave 2.x | ✅ SHIPPED 2026-05-25 evening (6 PRs in one session): #339 `b315374` (3 prod 500s fixed: TrialBalance / Periods/Close / AccessDenied SVG + dev-jargon strip) + #340 `151510c` (NavRegistry + Quick Actions cleanup, 3 vapor entries removed + 5 mis-pointers fixed) + #341 `c081254` (CSS utilities + 4 form-density refactors, −115 inline overrides −57% across 4 files) + #342 `e1cdba8` (subform consolidation: 2 shared partials `_ChildSectionHeader` + `_ChildTableEmpty` + 4 refactors; Codex P2 catch fixed inline) + #343 `ecf64a5` (Cockpit backport on Receiving/ByPo + Receiving/Blind + Admin/Lookups + NEW Quality nav group + NEW `.github/workflows/nav-routes-check.yml` CI gate caught 5 bonus broken routes). All 38 NavRegistry routes resolve post-sprint. |
> | **2.9** | **EVS Wednesday pitch gate** | **Wed Jun 3** | ⏳ gate (8 days away) |
> | **3.1-3.7** | Post-EVS: PRA-11 (Pack hierarchy) + MES events (#5e DowntimeEvent/ScrapEvent/ReworkEvent/MaterialConsumption + #5f LotGenealogy/SerialGenealogy + #5g OeeEvent rollup) | Thu Jun 4 – Wed Jun 10 | ⏳ queued (PRA-8/9/10 already shipped early in Wave 2.x; only PRA-11 + MES cascade remain) |
> | **4** | **Sprint 12.7 Controller Cockpit (the CFO motion) — 5 PRs total** | **PULLED FORWARD 2026-05-25/26 — ~16 days ahead of plan** | 🟢 **4 of 5 SHIPPED:** PR #1 `5176a1b`/#345 Controller shell (4 tabs / NavRegistry / Cockpit primitives) · PR #2 `614a738`/#346 ChainTraceService source-to-GL drilldown (Codex P1 catch: depreciation chain disambiguation via per-asset GL override or "not disambiguated" honest narration) · PR #3 `aad2590`/#347 voice intent `ExplainChainTrace` on Cherry Bar (push-to-talk → ChainTraceService.TraceAsync → TTS narration) · **PR #4 `2dbcfb9`/#348 SHIPPED 2026-05-26 PM** `IFinanceKpiService` KPI band wire-up (4 tiles: Cash position · AP due this week · Open POs · WIP balance), tenant-scoped via `companyId` guards, 26 unit tests pass. **Codex caught 3 real multi-tenant data-leakage bugs (P1+P1+P2 on FinanceKpiService) all fixed in-PR before merge** — branch protection refused admin-override past unresolved threads. ⏳ **PR #5 queued (re-scoped CFO-only):** data bumps for realistic KPI tile values + `/Controller/Walkthrough` page + TimescaleDB removal migration + Republish-with-Copy. |
> | **B8** | **Theme B8 — Production Order Cockpit (executable BOM + Routing grid) — 12-PR cascade ~172-240h, UMBRELLA absorbing B2/B3/B5a/B5b** | **NEW 2026-05-26 PM session 7 — Dean uploaded "Production Orders Fields and Functions" spec** | 📋 **Captured + slotted Sprint 14.x.** Bottom-line principle locked: *"Every BOM line and every routing operation should show planned vs actual, status, exception, cost impact, traceability, and the next allowed transaction."* Spec: ~270 fields, ~70 actions, 6 transfer rules, 16 validations, 17 KPIs, 3 operating modes, single-transaction-drawer UX. Highest-leverage differentiator: **"Can I run this operation?"** 8-readiness-check indicator with EAM integration (assigned machine down / overdue PM / lockout blocks op start — no incumbent does this properly). Source: `docs/research/po-cockpit-spec-2026-05-26.md`. Memory: `reference_po_cockpit_spec_2026_05_26.md`. Ships AFTER B6 Foundation (5/7), Sprint 14.1 snapshot, 14.2 DMS, 14.3 ECR/ECO. ~6-8 weeks. |
> | **B6.FS** | **Sprint B6.FS Foundation Sprint (GO BIG cascade kickoff) — 7 PRs total** | **NEW 2026-05-26 PM session 5 — Dean directive: GO BIG (NOT Minimal)** | 🟢 **6 of 7 main-path + 1 inserted + 1 hotfix = 8 ships total (86% main):** PR-FS-1 `8b5a7f6`/#355 · PR-FS-1.5 `f47d735`/#356 · PR-FS-1.5.1 `fc299f8`/#357 HOTFIX · PR-FS-2 `ab8436e`/#358 ItemSite + null-safe partial unique idx (Codex P1) + empty-string override semantics (Codex P2) · PR-FS-3 `bf8bd5a`/#359 ItemStandardCostElement (SAP Cost Component Split, zero Codex threads) · **PR-FS-4 `3f06bd6`/#360 CostLayer (FIFO/LIFO/Average inventory valuation, SAP MM stock-with-values equivalent)** + ICostLayerService + /Admin/CostLayerProbe + 13 realistic-mfg tests (Ryerson Steel commodity uplift Q1/Q2/Q3, BRG multi-vendor, EM reverse flows). Codex caught CHERRY025 (probe DbContext) + 2 P1s (Standard cost path + concurrency token) — all 3 fixed in-PR, threads resolved. Added `byte[] RowVersion` + retry loop. Lock 16 E2E live: probe ItemId=9245 summary empty (expected). ✅ PR-FS-5 `d40484a`/#361 ItemSourcingRule (SAP S/4 Source List + AS9100 §8.4.1 customer-mandated AVL + split-sourcing + 12 tests + Codex P1+P2 resolved). ✅ PR-FS-6 `0d8a966`/#362 CustomerItemXref (SAP CMIR — customer-PN ↔ Item bidirectional translation + supersession chain + 11 OEM tests + Codex P1 atomic-supersede resolved via EF nav-property pattern). ⏳ PR-FS-7 18-column Item Master expansion (includes IsSellable, final main-path ship). Then Sprint 14.1-14.5 (snapshot → DMS → ECR/ECO → **B8 cascade** → cost engine → smarter-than-BIC Item separation). **NEW HARD LOCK** session 7: `feedback_no_fake_data.md`. Memo: `docs/research/b6-foundation-sprint-design-2026-05-26.md`. Locks: `feedback_b6_go_big_2026_05_26.md` · `feedback_no_fake_data.md`. |
> | **4.5** | **Sprint 12.8 Production Motion (the COO motion) — 5 PRs total** | **NEW 2026-05-26 PM — Dean directive to demo for Shadi Mohaisen on Thursday** | 🟢 **3 of 5 SHIPPED:** PR #349 `0c05e4d` cost-cols schema + ADR-028 · PR #350 `356ee84` IBackwardSchedulingService stub · PR #354 `dca5628` PR #5c.1 CooMotionDemoSeeder skeleton (Codex caught 2 P1s, both fixed before merge). **Live + verified on prod post-Republish** — industryos.app/CustomerProjects/Index renders DEMO-COO-PROJ-001 ACTIVE. ⏳ PR #5c.2 lineage (LaborEntry + WorkOrderPart kits + JE) + PR #5d /Production/Walkthrough page remaining. PR #5a schema (cost columns + ParentProductionOrderId FK + ADR-028) · PR #5b backward-scheduler stub · PR #5c ABS Rolls-Royce 10-level BOM seeder · PR #5d `/Production/Walkthrough` page · Republish folded into Sprint 12.7's at end. Hand-crafted scenario; real engines stay Sprint 0.5 + Sprint 14. Memo: `docs/research/abs-thursday-demo-data-design-2026-05-26.md`. |
> | **5** | Sprint 13 Full Purchasing CC (8 PRs) | Jun 18 – Jun 24 | ⏳ queued |
> | **6** | Sprint 12.5 Enterprise Hardening (17 PRs in 4 workstreams) | Jun 25 – Jul 8 | ⏳ queued |
> | **7** | ⚠️ **RENAMED 2026-05-27 → Sprint 18 Maintenance CC** (Calibration / OEE / RCM+FMEA / Weibull, ~9 PRs). "Sprint 14" is now claimed by the B6/14.x post-foundation cascade. ADR-019 folds into B8 PR-PO-7 ("Can I run this?" readiness check). | Post-B8 cascade close | ⏳ queued |
> | **8** | Sprint 15 Planning CC (~4 PRs) | Jul 23 – Jul 29 | ⏳ queued |
> | **9** | Sprint 16 Scheduling CC (~4 PRs) | Jul 30 – Aug 5 | ⏳ queued |
> | **10** | Sprint 17 Inventory CC (~5 PRs) + AAS/IDTA research spike parallel | Aug 6 – Aug 14 | ⏳ queued |
> | **11** | Sprint 18 Shipping CC (~4 PRs) | Aug 15 – Aug 21 | ⏳ queued |
> | **12** | Sprint 19 Spatial Plant Floor Map (5 PRs) | Aug 22 – Sep 2 | ⏳ queued |
> | **13** | Sprint 20 Mobile PWA + DataWedge (TBD, folds in 12B.4/12B.5) | Sep 3 – Sep 12 | ⏳ queued |
> | **14** | Sprint 21 MCP Server + Agentic AI (~6 PRs, folds in Tool Registry/Reco Outcomes/12B.6b/12B.6c) | Sep 13 – Sep 24 | ⏳ queued |
> | **15** | Sprint 22 i18n + Launch Polish (~5 PRs, folds in Vertical Packs + PR #270 follow-up) | Sep 25 – Oct 5 | ⏳ queued |
> | **16** | Sprint 23 v1.0 LAUNCH | Oct 6+ | gate |
> | **backlog** | Sprint 24+ v2 AP/AR (V2-locked) · Sprint 5 Voice (re-baseline post-W15) · Sprints 7-9 Item Master (re-baseline post-W3) | TBD | 🚫 / re-baseline |
>
> **Memory drift fix:** memory files referencing `fdc4740` as PR #5d merge SHA — actual main HEAD as of 2026-05-26 is **`aad2590`** (Sprint 12.7 PR #3 voice intent ExplainChainTrace, PR #347). Recent main HEAD chronology (newest first): `aad2590` PR #347 (Sprint 12.7 PR #3) → `614a738` PR #346 (Sprint 12.7 PR #2 ChainTraceService) → `5176a1b` PR #345 (Sprint 12.7 PR #1 Controller shell) → `13c05b7` PR #344 (PRA-5g depreciation dual-write) → `ecf64a5` PR #343 (Sprint 13.6 final ship). Filed in audit §13.
>
> **5 NEW HARD LOCKS established 2026-05-24 → 2026-05-26 (not in original audit; supersede the 12-lock baseline):**
> | Lock | Title | Origin |
> |---|---|---|
> | **13** | Codex P1/P2 inline review comments BLOCK merge — resolve via GraphQL `resolveReviewThread` after the fix lands | PR #344 (BookId precedence) + PR #346 (depreciation disambiguation) |
> | **14** | **Dev is source of truth.** Replit Publish "Copy your development database to production database" checkbox is the sync mechanism. NEVER manipulate prod data directly (no psql INSERTs / UPDATEs on prod, no direct UI uploads to prod). | 2026-05-25 PM after Dean caught ABS demo content uploaded directly to prod via UI |
> | **15** | Control plane discipline — no raw `mb.Sql("CREATE TABLE")` (typed `migrationBuilder.CreateTable` / `AddColumn` only). No magic strings for GL accounts (use `IGlAccountResolver`). No literal AccountingKey integer literals (hash-derived per 8-segment context). No DbContext mutation without `[ControlPlaneExempt("reason")]`. Enforced by CI gates (snapshot-drift-check + control-plane-gate) + Codex review. | 2026-05-25 evening |
> | **16** | E2E after every addition — full visual + functional playbook (screenshot + DOM class count + real-value check + empty-state + click-through + critical layout + sparse-page red-flag) BEFORE the Shipped comment. Smoke verification (login + 302) is NOT E2E. psql verify mandatory on any JE-path / write-path / posting change. | 2026-05-25 evening |
> | **17** | **Skip Republish for code-only PRs** (no migrations / no demo seeds / no master-data inserts). E2E on the dev preview URL is sufficient. Republish-with-Copy still required when data changes. Saves 8-15 min per PR. | 2026-05-26 — Dean directive after PR #347 ship, first exercised on Sprint 12.7 PR #3 |
>
> **TimescaleDB removed 2026-05-25** from both dev and prod databases — incompatible with Replit's Copy workflow per Lock 14 (the Copy mechanism does not preserve TimescaleDB hypertables / continuous aggregates). ADR-020 Postgres-as-AI-Native-OS stack now reads: pgvector + Apache AGE (queued for Sprint 12D) + ParadeDB (Sprint 12C+) + pgai (Sprint 12C). TimescaleDB reinstatable only after a host migration off Replit. Migration `20260516_AddTelemetrySubstrate.cs:61` still contains the `CREATE EXTENSION IF NOT EXISTS timescaledb;` line — needs a removal migration as part of Sprint 12.7 PR #5 or Sprint 14 cleanup.

---

## 🎯 Next-up queue (post-audit 2026-05-18 / **reordered 2026-05-22 after Sprint 12B audit + 13.5 Manufacturing+CustomerProject schema landed in research** / **AUDITED + WAVE-SEQUENCED 2026-05-24 — see Master Plan Audit block above; legacy Priority 1.X numbers preserved for history but Wave numbers above are the execution order**)

> **Re-sequencing 2026-05-22 (Dean):** Sprint 12D base shipped (8 of 8 PRs). Sprint 12B audit revealed 2 of 6 original PRs already shipped + 1 substrate done + 3 external-dependency. Sprint 13.5 (Manufacturing + CustomerProject) is what ABS Thursday + EVS June 3 demos actually evaluate us on — `ProductionOrder` has no UI today. **New sequence: 13.5 → 12B deferred items (when triggers fire) → 12.5 → 13 → 14-18.** Every deferred item has a named trigger (see Priority 1.62 deferred-work block). No orphans.

**SEQUENCING DECISIONS LOCKED 2026-05-22:**
- **Sprint 13.5 jumps to next-up** — `ProductionOrder` UI gap is the biggest customer-visible deficit and both upcoming demos lean on it
- **Sprint 12B closes at 2-of-6 done + 1 substrate done + 7 named deferred-work items** (see Priority 1.62)
- **Sprint 12.5 (Enterprise Hardening) defers behind 13.5** — security/admin/a11y blockers are critical for launch but neither ABS Thursday nor EVS June 3 evaluates us on MFA/SSO/RLS-deeper-than-we-have. Re-engages after 13.5.
- **Sprint 13 (Full Purchasing CC) defers behind 13.5** — Purchasing service exists from Sprint 12.9 PR #4; the Cockpit overlay is valuable but second-fiddle to making `ProductionOrder` visible
- **Sprint 14-18 stay in current order** — each picks up the "Linked Projects" tab from Sprint 13.5
- **Sprint 12B.6a `ItemReceivedV2` chain-enrichment is folded into Sprint 13.5 PR #6** (subcontract chain edges) — both touch the chain layer, ship together for ~1 hour added scope

**Priority 1 — Sprint 4 Phase F Wave 1** (ADR-014 → UI). 4 PRs **— ⚠️ AUDIT 2026-05-24 RECOMMENDED RETIRE; folded into Master Files Baseline PRA-7 (Warehouse/Bin/Lot/Serial admin) + PRA-8 (Employee/Wage/Dept admin). The 4 admin pages below are superseded in substance by the Sprint 13.5 PR #4 nav rebuild + Master Files admin pages already shipped. Awaiting Dean's call A1.**:
1. ~~**RegulatoryProfile admin**~~ — CRUD for the 15-regime regulatory gates from Phase E.3. **Status: deferred** — PRA-1 IndustryVertical (17-enum) shipped is the v1 substitute; per-regime gate UI is a Sprint 14 Quality CC concern.
2. ~~**MaterialMaster admin**~~ — heat-number / mill-cert masters from Phase E.2. **Status: deferred** — PRA-7 Lot + SerialMaster cover the heat-number lineage as proper masters; mill-cert admin folds in there.
3. ~~**Vendor edit**~~ — full edit surface (list view shipped in PR #206). **Status: deferred** — PRA-1 expanded Vendor with 9 regulator IDs + PaymentTermId FK + ReceiptProfile JSONB; Vendor edit becomes trivial admin once PRA-6 PaymentTerm master + PRA-10 TaxAuthority lands.
4. ~~**StockReceipts**~~ — receiving workflow tying to Nests / MaterialMasters / heat numbers. **Status: deferred** — Receiving Control Center is LIVE (Sprint 11/12A); StockReceipts admin Edit page is INTENTIONALLY NOT being fixed per CC pivot lock (line 102).

Each Wave 1 PR uses the Sprint 4 PR #1 foundational infra (commit 9ddc01c): `VoiceReadyPageModel`, `Result<T>`, `IdempotencyMediator`, `VoiceContextEmitter`, `<voice-action>` Tag Helper, 30 authorization policies, AuditLog 7 AI columns. **Voice-ready from day one — the moat foundation.** *Infra remains in place; only the 4 specific admin pages above are superseded.*

**Priority 1.5 — Sprint 11 Receiving Control Center (PILOT)** ✅ **SHIPPED 2026-05-18** — scaffold for the whole Control Center family. All 7 PRs landed: (1) ADR-016 + research doc, (2) four-quadrant scaffold primitives, (3) `IReceivingControlCenterService` + state machine, (4) 10 `IReceiptVoiceTools`, (5) `/Receiving` landing page, (6) workflow pages + DataWedge focus mode, (7) kill list + sidebar swap. **Post-ship pivot**: Dean's walk-through of the live page vs. the legacy `/Receiving/Cockpit-Legacy` produced the Cockpit-first pivot (see marker above). Sprint 12 carries the rebuild. **All Sprint 11 services + voice tools + primitives remain in place** and are reused under the Cockpit shell.

**Priority 1.55 — Sprint 11.5 Control-Center-First Sidebar Rebuild** ✅ **SHIPPED 2026-05-18** — `ADR-017` + `_NavControlCenters.cshtml` + `_NavQuickActions.cshtml` + Settings drawer + Approvals bell + `ControlCenterRegistry`. Seven v1 Control Centers visible in sidebar (Receiving live; Purchasing / Maintenance / Planning / Scheduling / Inventory / Shipping placeholder rows). Each Sprint 12-18 ship flips one `IsLive: false → true` in the registry. PRs #237 + #238.

**Priority 1.6 — Sprint 12A Receiving Workspace Rebuild (Cockpit-first)** ⭐ NEW 2026-05-18 — locked by ADR-018 (Accepted). 8 PRs:
1. **ADR-018 ship** — `docs/ADR-018-cockpit-first-pattern.md` + MASTER_PLAN pivot block (this PR; docs-only)
2. **Cockpit primitives extract** — legacy `Pages/Receiving/Index.cshtml` partials → reusable `_Cockpit*` partials under `Pages/Shared/Primitives/Cockpit/`. Inline CSS → `wwwroot/css/cockpit.css`. Pixel-identical to legacy. Compile-only PR; no behavior change.
3. **Cockpit infra** — `ICockpitLens<TQueueRow>` + `ICockpitQueueRow` + `CockpitPreviewSerializer` + `ByTimeLens<T>` under `Services/Navigation/Cockpit/`. Unit tests for bucket boundaries (overdue / today / week / later) incl. midnight + timezone + null `RequiredAt` edges.
4. **`_CockpitTabShell` + tab routing** — generic four-tab shell partial. Tab state via `?tab=`. Keyboard nav (J/K through queue, Enter to focus preview, Esc back to queue, arrow keys to switch tabs).
5. **PO Queue tab** — `IReceivingControlCenterService.GetPoQueueAsync` + `PoQueueRow : ICockpitQueueRow`. Preview pane port from legacy Cockpit. "Receive This PO" CTA → `/Receiving/By-Po/{id}`. Live-verified pixel-identical on Replit.
6. **ASN Queue tab** — `GetAsnQueueAsync` + `AsnQueueRow` keyed on ASN ETA windows. Preview pane shows ASN lines + carrier + ETA + expected items. CTA → `/Receiving/By-Asn/{id}`.
7. **Orphans tab** — `GetOrphanReceiptsAsync` + unmatched receipts with `MatchOrphanReceipt` AI-suggested PO column. CTA opens a "Match orphan" drawer.
8. **Exceptions tab cleanup + Cockpit-Legacy retirement** — existing `_ControlCenterShell` invocation moves under the Exceptions tab. `/Receiving/Cockpit-Legacy` deleted; redirect to `/Receiving?tab=po-queue`.

Pre-reqs satisfied: ADR-014 (voice-ready infra), ADR-015 (industry-agnostic schema + 12 profiles seeded), ADR-016 (Control Center pattern), ADR-017 (sidebar IA), ADR-018 (Cockpit-first pattern, Accepted), Sprint 11 service + voice + primitives, Sprint 11.5 sidebar + Quick Actions tray, Sprint 3.5 design-system primitives, accessibility CI gate.

**Priority 1.605 — Sprint 12C Postgres-as-AI-Native-OS · Phase 1 Vector Layer** ⭐ NEW 2026-05-19 / ✅ **CLOSED 2026-05-21 — 5 of 5 PRs shipped (100% ✅).** ADR-020 D1+D2 fully landed on production Replit Postgres. Voyage paid tier active. **PR #269 (Hybrid Intent Router, commit f690b92) closed the sprint 2026-05-21.** Voice intent classification graduated from keyword regex to two-layer hybrid (keyword fast-path + vector cosine fallback). Layer 2 bootstrap-seeding follow-up tracked as PR #270 (Embeddings.Intent=0 in dev, likely RLS-on-TenantId=0 INSERT — Layer 1 keyword path covers ~99% of utterances in the meantime, so not Sprint 12C blocking).

1. ~~**`Embeddings` table migration + RLS + first embedded entity (ReceiptProfile) end-to-end**~~ — ✅ **SHIPPED 2026-05-20** (PR #261 + #262, squash commit `48bcbdf`). `CREATE EXTENSION vector;` (Replit has 0.8.0). New `Embeddings(Id, EntityType, EntityId, TenantId, ModelVersion, ContentHash, Embedding_ halfvec(1024), SourceText, CreatedAt)` table. HNSW index on `halfvec_cosine_ops`. RLS policy `current_setting('app.tenant_id')`. `PendingEmbeddings` queue with Postgres SKIP LOCKED leasing. **12 ReceiptProfile embeddings live in production storage.** Semantic search verified end-to-end: "steel mill heat number" → STEEL #1, OIL_GAS #2, AUTOMOTIVE #3, AEROSPACE #4, CHEMICAL #5 — no exact-match keywords required. Two hotfixes embedded in the squash: (a) `if (Database.IsNpgsql())` provider guard for Pgvector types in Sqlite/InMemory test contexts, (b) `[Migration("...")]` attribute on the migration class.
2. ~~**ADR-021 — Embedding model + pipeline (docs sibling)**~~ — ✅ **SHIPPED 2026-05-20** (PR #260, commit `9e01184`). Voyage `voyage-3-large/v1` locked: 1024 native dims (revised from ADR-020's `halfvec(1536)` to `halfvec(1024)` — saves 33% storage). Pipeline: external .NET background worker (Mode B) with 5s poll + 32-row batches. SHA-256 hash-based skip-if-unchanged. 7-day dual-write migration playbook. Cost projection: ~$0.24 for initial bulk embed across 3 customers, <$5/mo steady-state.
3. **PR #1.5 — Worker rate-limit hardening + retry endpoint** — ✅ **SHIPPED 2026-05-20** (PR #263, commit `259eab9`). Backoff schedule: 10s/20s/40s/60s/60s (was 1/2/4/8/16). Honors RFC 7231 §7.1.3 `Retry-After` header (both delta-seconds + HTTP-date forms). New `POST /_admin/embed/retry` endpoint resets stuck rows without psql. Voyage free tier 429'd after ~3 rapid requests during initial deploy → Dean upgraded to paid tier (200M tokens, 2K req/min). Hardened backoff stays for defense in depth.
4. ~~**PR #2 — Embed remaining 4 entity types**~~ — ✅ **SHIPPED 2026-05-20** (PR #264, commit `efd826e`). Item (5) + Vendor (3) + WorkOrder (5) + AuditLog.AiCommandText (3) = 16 sample rows enqueued via psql (admin endpoint behind RequireAuthorization — captured in `feedback_admin_embed_auth_gate`). Worker drained cleanly in one 5s poll cycle, queue empty, zero failures. Cross-entity semantic search proven live: query "ball bearing for industrial machinery" returns all 5 bearing Items in top 5 (cosine 0.354–0.371), then bearing-adjacent ReceiptProfiles (STEEL 0.589, AUTOMOTIVE 0.603), then bearing-supplier Vendor (KENNAMETAL 0.605). Save-hooks deferred to follow-up PR #2.1.
5. ~~**PR #3 — Hybrid intent router (keyword → vector)**~~ — ✅ **SHIPPED 2026-05-21** (PR #269, commit `f690b92`). Two-layer router: Layer 1 keyword fast-path (existing `IntentClassifier.Classify`) + Layer 2 vector cosine fallback against intent prototypes seeded into `Embeddings WHERE EntityType='Intent'`. Threshold cosine ≤ 0.45, top-K=5, majority-vote IntentKind. SemaphoreSlim concurrency cap on Voyage calls (16, 250ms slot wait). Cancellation propagation via `catch (OperationCanceledException) { throw; }` BEFORE the generic catch. AuditLog `AiToolName` now carries `router:{Source}:{Reason}` per call for threshold tuning telemetry. 7 unit tests pass.
6. ~~**PR #270 — Observable bootstrap (defensive)**~~ — ✅ **SHIPPED 2026-05-21** (commit `b2b863e`). Added `[intent-seed]` log markers + defensive `SET app.tenant_id = 0` session var. Both were correct moves but neither was the actual root cause. Intent embeddings still didn't materialize.
7. ~~**PR #282 — EmbeddingBackfillService race fix (the REAL fix)**~~ — ✅ **SHIPPED 2026-05-22** (commit `8190366`). Live-diagnosed the actual root cause via psql: `ix_embeddings_entity_model` UNIQUE constraint on `(EntityType, EntityId, ModelVersion)` + within-batch `_db.Embeddings.Add()` duplicates (3 prototypes per IntentKind → 3 staged Adds per key) + `ExecuteDelete`-before-`SaveChangesAsync` (raw SQL commits IMMEDIATELY) = catastrophic silent loss. Fix: `UpsertEmbeddingAsync` now checks `_db.Embeddings.Local` BEFORE the DB query (within-batch dedup), and `ProcessPendingAsync` reorders to a three-phase commit (stage upserts → `SaveChangesAsync` → ONLY THEN `ExecuteDeleteAsync` queue rows). Live verified: re-enqueued 12 IntentPrototypes via psql, worker drained cleanly in <5s, **`Embeddings.Intent` = 4 live rows** (one per IntentKind, last prototype wins dedup as designed). Vector L2 router functional. Memory: `project_pr282_sprint_12c_closeout.md`.
8. **PR #4 — Voice E2E re-verify + Shadi demo** — NOW UNBLOCKED. Vector L2 router live. Click-through demo: on `/Receiving`, say "anything new at the dock today" → routes via vector to ExpectedArrivals (not Fallback). Future hardening: change bootstrap to use unique EntityIds per prototype (e.g., `Kind*100 + index`) so all 12 prototypes land as individual Embeddings rows for richer L2 coverage — backlog item, not blocking June 3.

**Open follow-up (separate dedicated PR, NOT a Sprint 12C blocker):** `AppDbContextModelSnapshot.cs` has pre-existing drift on `EquipmentClass.Models` navigation that breaks `dotnet ef migrations add` design-time tooling. Runtime `MigrateAsync()` still works for migrations with `[Migration]` attributes. Snapshot needs regeneration eventually but it's a 12,876-line file — its own dedicated PR.

**Why this jumps the queue:** voice/AI is the moat. The keyword intent router in the Voice MVP works for narrow phrasing but breaks the moment a customer says it differently. Vector embedding is the foundation EVERY future AI feature compounds on (semantic search, RAG context for narration, similar-X queries, profile-aware intent routing). The competitive lead this opens widens every week we run it before SAP/Plex/NetSuite catch up.

**Host decision deferred to Sprint 12D PR #1** — Sprint 12C runs entirely on Replit. The Replit-vs-Azure-vs-Docker call becomes load-bearing only when Apache AGE enters at Sprint 12D, with ~2-3 weeks runway to the June 3 EVS pitch when it does become urgent.

**Priority 1.6075 — Snapshot drift fix** ⭐ NEW 2026-05-20 / ✅ **SHIPPED 2026-05-22** (PR #271, commit `528f0a8`). `dotnet ef migrations add` now generates empty Up()/Down() migrations against the regenerated `AppDbContextModelSnapshot.cs`. Zero schema change, zero data risk. Sprint 12D Apache AGE migrations can now ship via `dotnet ef migrations add` cleanly — no more psql + `INSERT INTO __EFMigrationsHistory` workaround needed.

**Root cause (corrected):** Not the `EquipmentClass.Models` navigation gap that the original memory suggested (that property DOES exist at `Models/Catalog/EquipmentClass.cs:59`). The actual issue was the snapshot's string-overload entity registration form (`Entity("Abs.FixedAssets.Models.Catalog.EquipmentClass", b => ...)`) which EF Core treats as a shared-type `Dictionary<string,object>` entity that can't resolve C# property names. Regenerating the snapshot from the current runtime model resolved the ambiguity. Full root-cause analysis: memory `project_pr271_snapshot_drift_shipped.md`.

**Problem.** `Migrations/AppDbContextModelSnapshot.cs` (12,876 lines) has pre-existing drift on line 243: it calls `.Navigation("Models")` on the `EquipmentClass` entity, but `EquipmentClass.cs` doesn't have a `Models` navigation in the current code. This breaks `dotnet ef migrations add` (can't load the snapshot to diff against current model), which is why my Sprint 12C PR #1 migration needed manual application via psql + `INSERT INTO __EFMigrationsHistory`. The drift is pre-existing — Sprint 12A migrations applied fine because they had explicit `[Migration]` attributes AND their schema didn't touch EquipmentClass. Runtime `MigrateAsync()` is more tolerant than the design-time tooling.

**Why fix now (not later).** Sprint 12D PR #1 introduces Apache AGE + multiple new graph schemas. Each one needs a migration. With the drift in place, every graph migration would need the manual psql + history-row workaround we used on Sprint 12C PR #1 — workable but slow and error-prone, at exactly the wrong time (June 3 EVS pitch deadline approaching). Fixing the snapshot once, between sprints, eliminates this drag for the rest of the project.

**Why fix it as its own PR (not bundled).** Snapshot regen touches the model's canonical metadata. If a regen accidentally drops a real-but-undocumented entity or property, every subsequent migration goes off the rails silently. Isolating the fix lets us verify cleanly: regenerated snapshot diffs to zero pending migrations against the current DB, prove zero behavioral change at the app layer.

**Scope (1 PR, ~1-2 hours):**

1. **Investigate `EquipmentClass.Models`** — `git log -p Models/Catalog/EquipmentClass.cs` to determine whether `Models` was a navigation that got removed (snapshot is stale + needs the navigation removed) OR one that should exist (entity is missing the property + should be restored).
2. **Reconcile the entity vs snapshot.** Either remove the `.Navigation("Models")` line from the snapshot (preferred if the property is genuinely gone) or restore the `public ICollection<EquipmentModel> Models { get; set; }` on `EquipmentClass` (preferred if the property was lost). Per existing memory `feedback_namespace_enum_collisions` — grep ALL OnModelCreating fluent config for `EquipmentClass` before touching anything.
3. **Run `dotnet ef migrations add NoOp`** — should now succeed. The generated migration's `Up()` and `Down()` should be empty (snapshot now matches the live model). If they're NOT empty, something else has drifted and we need a real migration to converge.
4. **Verify with `dotnet ef migrations script --idempotent`** — generated script should be a no-op against the live database. If the script tries to alter tables, the snapshot still doesn't match production and we need to investigate.
5. **Delete the NoOp migration.** Keep only the regenerated `AppDbContextModelSnapshot.cs`. Commit + ship as docs-style "no schema change" PR.
6. **Validation gate:** before merging, the local app must boot + run a smoke test (existing voice MVP + admin/embed endpoints) to prove the snapshot regen didn't break runtime EF model finalization.

**Sequencing.** This PR is ⏳ **scheduled to land BEFORE Sprint 12.9 Control Plane Hardening and BEFORE Sprint 12D PR #1 (Apache AGE host decision + chain_of_custody graph)**. Captured here so it doesn't fall between the cracks.

---

**Priority 1.6080 — Sprint 12.9 Control Plane Hardening** ✅ **COMPLETE 2026-05-22 — ALL 10 PRs SHIPPED IN ONE SESSION.** **Final commit on `main`: `d6d0b38` (PR #281).** Three new typed domain services (IWorkOrderService + IPurchasingService + IItemMasterService) + CHERRY025 Roslyn analyzer + IPostingService<T> contract + audit-completeness ratchet (CI-enforced) + 7 automated cross-tenant leakage tests. 40 raw SaveChangesAsync writes refactored off the three worst-offender PageModels. Allowlist 118→115. Audit-trail / chain-of-evidence story is materially complete — Joe's June 3 EVS pitch substrate is ready. Memory: `project_sprint_12_9_complete.md`.

**Status check (updated 2026-05-22):**
- ✅ **PR #1 — Roslyn CHERRY025 analyzer replaces grep gate.** SHIPPED as PR #272 squash `33627a6`. Catches direct AppDbContext injection on PageModels + Controllers + Endpoints + BackgroundServices (4 layers vs grep's 1). 118-entry allowlist grandfathers existing legacy files. 8/8 unit tests pass. All 3 CI checks green. Live-verified on Replit. Memory: `project_pr272_sprint_12_9_pr1_shipped.md`. CI lesson captured: `feedback_solution_level_restore_for_test_projects.md`.
- ✅ **PR #2 — `IPostingService<TSourceDoc>` base interface contract.** SHIPPED as PR #273 squash `c4209f1`. Contract locked: `Task<Result<PostingReceipt>> PostAsync(TSourceDoc, int actorUserId, Guid idempotencyKey, CancellationToken)`. ApPostingService + ReceivingPostingService both implement; Program.cs DI wires them through both their domain interface AND the new typed contract. 7/7 PostingContractTests pass. All 3 CI checks green. Live-verified on Replit. Memory: `project_pr273_sprint_12_9_pr2_shipped.md`. ADR amendment: `docs/ADR-025-posting-service-contract.md`.
- 🟡 **PR #3 (PHASE 1) — IWorkOrderService skeleton + 5 of 17 PageModel writes refactored.** SHIPPED as PR #274 squash `0561d2e`. AddOperation/MoveOperation/UpdateOperationStatus/AddOperationTool/AddPlannedMaterial extracted to `Services/Maintenance/WorkOrderService.cs`. Bit-identical behavior preserved. File stays in allowlist (still has 12 writes). Phased plan: PR #3.1 = JE-posting writes; PR #3.2 = JE+inventory writes; PR #3.3 = WO-level writes + allowlist removal. Memory: `project_pr274_sprint_12_9_pr3_shipped.md`.
- 🟡 **PR #3.1 (PHASE 2) — IWorkOrderService + 3 op-level JE-posting writes refactored.** SHIPPED as PR #275 squash `41daa5e`. AddLabor/IssueOperationPart/ReturnOperationPart extracted; 3 private helpers (PostLaborJournalEntry, ApplyOperationPartMovement, PostOperationPartJournalEntry) moved into WorkOrderService. PageModel: 1314→1033 lines, 12→9 writes. **Cumulative 8 of 17 (47%) refactored.** IGlAccountResolver injected into WorkOrderService.
- 🟡 **PR #3.2 (PHASE 3) — 5 WO-header material writes refactored.** SHIPPED as PR #276 squash `7b8615b`. AddOperationPart/IssueMaterial/ReturnMaterial/RemovePlannedMaterial/LoadTemplateMaterials extracted; 2 more helpers (ApplyItemMovement, PostMaterialMovementJournalEntry) moved into WorkOrderService; IOutboxWriter injected. PageModel: 1033→719 lines, 9→4 writes. **Cumulative 13 of 17 (76%) refactored.** LoadTemplateMaterialsOutcome record preserves legacy TempData mapping.
- ✅ **PR #3.3 (PHASE 4 / CLOSEOUT) — final 3 WO-level writes refactored; file OFF allowlist.** SHIPPED as PR #277 squash `b53cde7`. EditWorkOrder/DispatchUpdate/Capitalize extracted (Capitalize has 6 outcome states + period guard + improvement posting + depreciation backfill). 4 more deps injected (IPeriodGuard, DepreciationBackfillService, ICapitalImprovementPostingService, MaintenanceService). **DetailsModel marked with [ControlPlaneExempt] for the read-only AppDbContext use per ADR-025 D1.** PageModel: 719→564 lines, 4→0 writes. **17 of 17 (100%) refactored. Allowlist 118→117 — first reduction.** Sprint 12.9 PR #3 ARC COMPLETE.
- ✅ **PR #4 — Refactor `Pages/Purchasing/Details.cshtml.cs` (12 writes → 0) → `IPurchasingService`.** SHIPPED as PR #278 squash `ba98f62`. ONE-PR refactor (vs PR #3's 4-phase arc) — writes were clean CRUD + lifecycle, no JE / inventory / posting coupling. 11 service methods (6 CRUD + 3 lifecycle + 2 helpers). `ApprovePoOutcome` (3 states) + `RejectPoOutcome` (2 states) records preserve legacy TempData mapping bit-identical. `SyncStatusFkAsync` helper moved from PageModel into service. PageModel: 636→365 lines (-43%), 12→0 writes, 6→5 ctor deps. DetailsModel marked `[ControlPlaneExempt]` for read-only `AppDbContext` per ADR-025 D1. **Allowlist 117→116 — second reduction.** Memory: `project_pr278_sprint_12_9_pr4_shipped.md`.
- ✅ **PR #5 — Refactor `Pages/Materials/ItemEdit.cshtml.cs` (11 writes → 0) → `IItemMasterService`.** SHIPPED as PR #279 squash `94d8182`. ONE-PR refactor (same shape as PR #4) — writes were pure CRUD on Item / ItemRevision / VendorItemPart, no JE / inventory / outbox. 11 service methods grouped 4 ways (2 Item CRUD + 3 Revision metadata + 3 VPN URL + 3 Image). Sibling item-domain services unchanged (IItemRevisionService, IItemCrossReferenceService, IItemImageService, IItemSourcingService, IItemAlternateService, IItemSupersessionService all preserved — IItemMasterService picks up only the 11 raw AppDbContext writes). ItemMasterService deps (4): AppDbContext, ITenantContext, ILookupService, ILogger — cleanest service footprint of the S12.9 series. PageModel: 915→832 lines (-9%, smaller delta because most handlers were already single-line sibling-service delegations), 11→0 writes. ItemEditModel marked `[ControlPlaneExempt]` for read-only `AppDbContext` per ADR-025 D1. **Allowlist 116→115 — third reduction; all three top-offender files now closed.** Memory: `project_pr279_sprint_12_9_pr5_shipped.md`.
- ✅ **PR #6 — Audit-completeness CI metric.** SHIPPED as PR #280 squash `08ca6a3`. New `Analyzers/.allowlist-ratchet` (single line: 115) + `scripts/audit-completeness-check.sh` (strict-equality enforcer + Step Summary emitter) + new step appended to `.github/workflows/control-plane.yml` (no rename of job — branch protection preserved). Three failure modes: count > ratchet ("Allowlist GREW — refactor"), count < ratchet ("Ratchet drift — also lower the ratchet"), exact match (PASS). Step Summary shows current count + ratchet + 118-entry baseline + net reduction. Verified via Replit pull `94d8182..08ca6a3` and local script run (`OK — 115 entries, ratchet 115 (matched)`). CI-only — no Agent restart. Memory: `project_pr280_sprint_12_9_pr6_shipped.md`.
- ✅ **PR #7 — Automated cross-tenant leakage tests.** SHIPPED as PR #281 squash `d6d0b38`. New `tests/Abs.FixedAssets.Tests/CrossTenantLeakageTests.cs` — 7 xUnit facts covering IItemMasterService (4 facts) + IPurchasingService (3 facts). Each fact: seed Tenant A row → switch to Tenant B context → call service mutation → assert `Result.IsFailure` AND row unchanged. All 7 pass locally (2.7s). Closes Absorption Item 8 ("Automated RLS tests + tenant-leak gates"). Memory: `project_sprint_12_9_complete.md`.

**Why now (not interleaved).** The original plan called for top-10 refactors as side-work across Sprints 12.5–17. The audit-trail / chain-of-evidence marketing line ("Machine Event → General Ledger") that Item 1 of the Blueprint v4 absorption locked as the v1 sales line **directly depends on every write going through a service** so the audit trail can prove the chain. With 173 write paths bypassing the control plane today, a Fortune-100 procurement person who runs the "show me audit completeness for the past 30 days" diligence question will catch the gap and the chain-of-evidence pitch breaks in the room. The AGE spike (Priority 1.609) is half-day to day's work; ~6 days runway to June 3 is 6x what's actually needed. **Dean's call 2026-05-22: yes, do the upgrade.**

**Scope — 7 PRs (target ~5 days):**

1. **Roslyn analyzer replaces grep gate** — promotes `scripts/check-control-plane.sh` to a `Microsoft.CodeAnalysis.CSharp` semantic analyzer. Now catches `Controller`, `MapEndpoint`, `BackgroundService`, and any class injecting `AppDbContext` for write paths — not just PageModels. Runs in-IDE via Roslyn diagnostics (instant feedback) AND in CI. Existing `.github/workflows/control-plane.yml` switches to call `dotnet build /warnaserror:CHERRY025` instead of grep.

2. **`IPostingService<TSourceDoc>` base interface contract** — locks the pattern every posting service (current: `ApPostingService`, `ReceivingPostingService`; future: `PurchasingPostingService` in Sprint 13, `MaintenancePostingService` in Sprint 14, etc.) implements. Contract: `Task<Result<PostingReceipt>> PostAsync(TSourceDoc src, IdempotencyKey key, CancellationToken ct)` with guarantees on idempotency-key check + period guard + balanced JE validation + audit event + outbox event. Existing posting services refactor to implement this in the same PR.

3. **Refactor `Pages/WorkOrders/Details.cshtml.cs` (17 writes → 0) → `IWorkOrderService`** — worst offender in the audit. New `Services/Maintenance/WorkOrderService.cs` consolidates all 17 write paths. Each write goes through IdempotencyMediator + AuditService. PR also de-risks Sprint 14 (Maintenance Control Center) because WorkOrder is the central entity.

4. **Refactor `Pages/Purchasing/Details.cshtml.cs` (12 writes → 0) → `IPurchasingService`** — second-worst offender. De-risks Sprint 13 (Purchasing Control Center) because that sprint's `IPurchasingControlCenterService` builds on top of `IPurchasingService`.

5. **Refactor `Pages/Materials/ItemEdit.cshtml.cs` (11 writes → 0) → `IItemMasterService`** — third-worst offender. De-risks Sprint 7-9 (Item Master Expansion + Multi-Dim Inventory + 11-tab ItemEdit) because those sprints rewrite the item master and need a clean service layer underneath.

6. **Audit-completeness CI metric** — new CI job emits a metric on every PR: "X% of write paths go through services, Y new exempt pages added, Z legacy violations remaining." Burndown visible to leadership. Future drift visible in PR descriptions.

7. **Automated RLS tenant-leak test in CI** — folds in Item 8 from absorption (was Planned in Sprint 12.5 Workstream A — moves here because RLS verification is part of "control plane done"). Creates Tenant A + Tenant B in test DB, asserts cross-tenant read/write returns zero rows / 403s. CI gate blocks merge if RLS test fails OR a new tenant-scoped table is added without RLS policy. Doc: `docs/security/rls-coverage.md` enumerating tables + policies.

**Net after the week:**
- 40 of 173 write paths (23%) refactored — pattern established for the remaining 130
- Roslyn analyzer catches Controllers + Endpoints + BackgroundServices (not just PageModels) — full-layer coverage
- `IPostingService<TSourceDoc>` contract codified before Sprint 13 freestyles its own posting service
- Audit completeness measurable in CI on every PR
- RLS tenant isolation provable on every PR

**Then Joe's June 3 pitch can include the line:** "Every transaction in CherryAI flows through a typed service, idempotent, audited with Purview-class schema, with tenant isolation enforced by Postgres RLS and proven by automated tests in CI on every PR. Show me your SAP/Plex/NetSuite/Acumatica competitor that can demonstrate that today."

**Trade-off acknowledged:** Pushes Sprint 12D AGE spike to ~6 days runway from June 3 instead of ~12. AGE spike is half-day to day's work, so 6 days is 6x what's needed. Acceptable.

**Effect on existing Item 9 absorption status:** Item 9 sub-items 1-4 (ADR, gate, refactor backlog, posting contract) are no longer "interleaved across Sprints 12.5–17." Sub-items 1-3 are done. Sub-item 4 (posting contract) lands in Sprint 12.9 PR #2. Sub-item "refactor backlog" — the top-3 land in Sprint 12.9 PRs #3-#5; the remaining 7 (Admin/Requisitions, Maintenance/Technicians/Profile, Maintenance/Details, Assets/Asset, Admin/Webhooks/Index, Admin/Users, Admin/Items) interleave across Sprints 13-17 as originally planned.

**Acceptance criteria.** (a) Roslyn analyzer green on main with zero violations in new code. (b) `IPostingService<TSourceDoc>` interface merged. (c) `WorkOrders/Details` + `Purchasing/Details` + `Materials/ItemEdit` all show 0 `SaveChangesAsync` calls in PageModel. (d) Audit-completeness CI metric job exists + first run shows baseline number. (e) RLS leak test green; CI gate blocks the next attempt to add a tenant-scoped table without an RLS policy. (f) Total `SaveChangesAsync` calls across `Pages/` drops from 173 to ~133.

**Acceptance criteria.** (a) `dotnet ef migrations add Test --no-build` succeeds against the regenerated snapshot. (b) Generated `Test` migration's `Up()` + `Down()` are empty. (c) Local app boot is clean. (d) `/_admin/embed/status` still returns the 12 existing embeddings (no data loss). (e) Voice MVP smoke test passes (`/Receiving` → click voice → `_voice/invoke` → 200 OK).

**Priority 1.609 — Sprint 12D Postgres-as-AI-Native-OS · Phase 2 Apache AGE Graph Layer** ⭐ NEW 2026-05-19 (evening) / **8-PR PLAN LOCKED 2026-05-22 BY ADR-022** — **HARD DEADLINE June 3 EVS pitch.** **PROGRESS: 8 of 8 PRs SHIPPED 2026-05-22 (ADR + schema + ALL 5 SERVICE EDGE-EMITS + CYTOSCAPE VIZ + VOICE TOOL + DEMO WALKTHROUGH + PARENTNODEID POLISH — DEMO HEADLINE FULLY LIVE; PERF + Q3 STUB CAN LAND POST-PITCH).** ADR-020 D3 lands as **Virtual Apache AGE** (Postgres recursive CTEs + cytoscape.js viz) for June 3; real Apache AGE host migration deferred to Q3 2026 per ADR-022 D7. Original "host decision PR" deferred — Replit-stays decision baked into ADR-022. 8 PRs:

1. ✅ **PR #1 — ADR-022 (host decision + scope lock)** SHIPPED as PR #283, squash `a1cda53`. Docs-only. Locked: Virtual Apache AGE via Postgres recursive CTEs + cytoscape.js for June 3; real AGE Q3. `IChainOfCustodyService` interface as Q3-swap stability point. 8 sub-PRs scoped. Memory: `project_pr283_sprint_12d_pr1_adr_022_shipped` (filed).
2. ✅ **PR #2 — ChainNodes + ChainEdges schema + IChainOfCustodyService + recursive-CTE backend + 6 xUnit facts** SHIPPED as PR #284, squash `e38ee17`. Polymorphic graph (17 NodeTypes + 18 EdgeTypes constants), idempotent EnsureNode/RecordEdge, recursive-CTE GetUpstream/Downstream traversal. RLS per ADR-020 §D6 template. Migration applied via direct psql (auto-apply silent-skipped — same as Sprint 12C). Two engineering lessons captured: SaveChanges uppercase whitelist + Local-tracker dedup must apply request updates. Memory: `project_pr284_sprint_12d_pr2_shipped` + `feedback_appdbcontext_uppercase_interceptor`.
3. ✅ **PR #3 — ReceivingPostingService chain-emit (the EVS demo chain)** SHIPPED as PR #285, squash `34bc480`. Every goods receipt now emits Receipt→PO RECEIVED_AT + PO→Vendor SUPPLIED_BY + Receipt→Item CONTAINS_ITEM edges. HashSet within-batch dedup. try/catch isolates chain failures from JE rollback. NullChainOfCustodyService test helper. 25 tests pass. Other 4 services (WorkOrder/AP/Purchasing/ItemMaster) deferred to PR #3.1-3.4 sub-PRs (same recipe). Memory: `project_pr285_sprint_12d_pr3_shipped.md`.
3a. ✅ **PR #3.1 — IWorkOrderService.CapitalizeAsync chain-emit** — SHIPPED as PR #286, squash `05d03fb`. WorkOrder → CapitalImprovement (PRODUCED_BY) + CapitalImprovement → Asset (CAPITALIZED_TO) edges. Fires only on Capitalized status, not partial-success branches. 0 test ctor sites needed updating (WorkOrderService is DI-resolved everywhere).
3b. ✅ **PR #3.2 — ApPostingService.PostApprovalAsync chain-emit** — SHIPPED as PR #287, squash `cb83db8`. Invoice → GlEntry (POSTED_TO) + Invoice → Vendor (SUPPLIED_BY) edges. Closes the "Machine Event → General Ledger" chain at the financial boundary. Bug fixed mid-PR: JournalEntry.Reference not EntryNumber (CHERRY025 caught at build time).
3c. ✅ **PR #3.3 — IPurchasingService.ApproveAsync chain-emit** — SHIPPED as PR #288, squash `75b0b3c`. PurchaseOrder → User (APPROVED_BY) edge. User EntityId derived via stable polynomial-hash UsernameKey helper (no User-FK threaded through ApprovePoRequest). Fires only on FullyApproved / NoWorkflowApplicable.
3d. ✅ **PR #3.4 — IItemMasterService.SaveRevisionAsync chain-emit** — SHIPPED as PR #289, squash `99e5611`. Item → ItemRevision (REVISION_OF) edge. Closes the PR #3.x arc. All 5 Sprint 12.9 services now contribute to ChainNodes + ChainEdges. Memory: `project_sprint_12d_pr3x_arc_complete.md`.
4. ✅ **PR #4 — cytoscape.js chain-of-custody viz on Receipt detail (DEMO HEADLINE)** SHIPPED as PR #290, squash `025c931`. New `_ChainOfCustodyGraph.cshtml` partial (reusable across any page) + Details.cshtml/.cs updates. cytoscape 3.30.2 + dagre 0.8.5 from CDN with `defer` + retry-on-not-loaded init script. Per-NodeType color coding (11 types: Receipt red, PO blue, Vendor purple, Item green, WO orange, CapImpr light-blue, Asset brown, Invoice pink, GlEntry slate, User teal, ItemRevision olive). dagre TB layout. Edge labels show EdgeType. Pan + zoom + hover. Edge reconstruction via depth-adjacency (PR #6 polish to add explicit ParentNodeId). PID 7099 live on Replit. bin/obj cleared on Shell pre-restart per `feedback_replit_razor_view_cache`. Memory: `project_pr290_sprint_12d_pr4_shipped.md`.
5. ✅ **PR #5 — Voice tool `explain_chain_of_custody` (VOICE HEADLINE)** SHIPPED as PR #291, squash `d2766d6`. New `ExplainChainOfCustody` IntentKind + keyword classifier branch placed BEFORE EXPLAIN + 5 vector L2 prototypes + `HandleExplainChainOfCustodyAsync` handler (~85 lines) on `VoiceInvokeEndpoint` + `NarrateEdge` 14-case helper (template-driven narration, no LLM dependency). ActionLinks jump to `/Receiving/Details/{id}#chain-of-custody` (the cytoscape card from PR #4). User on Receipt detail can now voice-query "trace this receipt back to its source" → hears narrated chain ("Receipt RCPT-X traces back through 4 nodes. Received under purchaseorder PO-555. Supplied by vendor Vendor-7..."). PID 7761 live on Replit. Memory: `project_pr291_sprint_12d_pr5_shipped.md`.
6. ✅ **PR #6 — Demo walkthrough + ParentNodeId polish** SHIPPED as PR #292, squash `99e8bdc`. Two coupled deliverables: (a) `ChainHop.IncomingFromNodeId` replaces depth-adjacency fallback in cytoscape — now DAG-correct for multi-parent topologies (Invoice → Receipt + PO + GL). Recursive CTE projects `chain."Id"` in the recursive arm; cytoscape partial drops ~30 lines of depth-bucketing for a one-line filter+map. Edge id includes edge SHA so multiple edge types between same nodes don't collapse. (b) New `/Demo/ChainOfCustody` walkthrough page (`?id=N` query or auto-select most-recent eligible receipt) renders upstream + downstream graphs side-by-side via the reusable PR #4 partial + numbered narration list under each (mirrors PR #5 NarrateEdge template). Picker dropdown for live anchor switching. Voice-query equivalent CTA card showing the four phrasings classifier routes to ExplainChainOfCustody. Marked `[ControlPlaneExempt("read-only walkthrough")]` per ADR-025 D1 — CHERRY025 caught direct AppDbContext on first Mac build, fixed in-PR. PID 8565 live on Replit; /Demo/ChainOfCustody returns 302 (auth redirect — route resolves). Memory: `project_pr292_sprint_12d_pr6_shipped.md`.
7. ⏳ PR #7 — Performance harness (1000-node traversal p95 < 200ms target) + edge-table failover playbook.
8. ⏳ PR #8 — Q3 host migration ADR-024 stub.

**Original host-decision PR scope** retained below for historical reference — Replit-stays decision now locked in ADR-022, so this PR is folded into PR #1.

(Historical / deprecated — now ADR-022's D7) **HOST DECISION PR — Apache AGE spike on Replit + Azure-vs-Docker-vs-Neon decision** — `CREATE EXTENSION age;` on Replit-managed Postgres. If it loads cleanly + Cypher queries work end-to-end, ship the rest of Sprint 12D on Replit (no host migration). If it doesn't load, this PR makes the host decision with ~2-3 weeks of runway to June 3 (not zero). Options weighed: **(a)** custom Postgres Docker image targeting Azure VM/AKS (Dean's eventual-Azure-or-Docker-on-prem direction) · **(b)** Neon-direct (drop-in pg superset, full extension catalog) · **(c)** stay-on-Azure-managed with relational adjacency-table workaround instead of Cypher (lower moat). Locks **ADR-022a (Host decision)** if a move is required. The spike is ~half a day; the full migration if needed is ~2-3 days. Either path preserves the June 3 deadline.
2. **AGE extension migration + `chain_of_custody` graph (minimum)** — define first graph: nodes (`StockReceipt`, `Nest`, `Remnant`, `CutListLine`, `ProductionBatch`, `Shipment`), edges (`cut_from`, `produced`, `shipped_as`, `quarantined_by`). View-backed refresh function. Cypher queryable from EF via `FromSqlInterpolated`.
3. **`bom` graph + view-backed refresh** — `Item → MaterialStructure → Bom → BomLine` recursive. Powers BOM explosion + where-used queries. Voice prep: "what items use heat H-12345?"
4. **`aps_dependencies` graph + EVS demo data** — `Operation → Resource → Skill → Constraint` with `precedes`/`requires`/`competes_with` edges. **EVS-themed seed data** — 4 facilities, sheet-metal item taxonomy, ITAR flag where applicable, demo work orders with realistic dependency chains.
5. **`TraceChainOfCustodyAsync` real wiring** — replaces the Sprint 11 stub in `IReceiptVoiceTools`. Cypher path traversal from any receipt forward to shipment OR backward to mill cert. Returns `ChainOfCustodyGraph` DTO suitable for voice narration.
6. **"why is X late" voice intent + LLM narration** — new voice intent on `/_voice/invoke`: Cypher MATCH path from operation X back through predecessors, find earliest blocker, narrate with LLM. **The EVS demo killer.** Joe asks; AI answers with full graph reasoning. **Zero Tier-1 ERPs ship this.**

**ADR-022** (graph schemas + edge semantics) ships as the docs sibling, locking shapes before PR #2.

**June 3 acceptance criteria:** in front of Joe, voice ask "why is job [X] late?" → cards appear showing the blocker chain back through 3-5 predecessor operations → AI narrates the explanation. Demo data quality is acceptable for v1; production-graph performance optimization can defer to Sprint 12D follow-ups.

**Priority 1.61 — Strategic Absorption from Blueprint v4 (LOCKED 2026-05-20 — Dean approved 8 of 8 items)** ⭐ NEW — cross-cutting absorptions from the Fortune-100 Strategy Blueprint v4 review (`CherryAI_Industrial_OS_Fortune100_Strategy_Blueprint_v4.pdf`). The blueprint is finance-first-then-AI; our strategy is AI-native from day one, validated by customer pull (Joe/EVS bought BECAUSE of AI-APS). Most of the blueprint's sequencing collides with our locked direction — the absorption pulls forward the ideas that strengthen our path WITHOUT disrupting Sprint 12C close or Sprint 12D's June 3 EVS deadline. **8 items total, ranging from net-zero marketing reframes to a real new sprint deliverable.** Detailed scope + acceptance criteria in `docs/strategic-absorption-2026-05-20.md`. Live progress dashboard at the artifact "CherryAI Strategic Absorption Tracker."

1. **Marketing reframe — "Chain of Evidence from Machine Event to General Ledger"** ⭐ TAKE — replace "operations platform" / "industrial OS" / "control plane" with this phrase in v1 launch copy, sales decks, ABS/EVS pitch materials. Blueprint Appendix G nailed the language a CFO buyer actually responds to. Owner: Dean (sales+marketing). Acceptance: phrase appears in Joe/EVS deck for June 3, in Sprint 22 launch website hero copy, and in v1 investor narrative. **No code change. Zero blocker.** Status: Backlog.

2. **Tool Registry v0 — `ai.AgentToolCalls` + `ai.Recommendations` + `ai.AIApprovals` tables** ⭐ PULL FORWARD from Sprint 21 → fold into Sprint 12D PR scope as a new PR #2.5 between AGE host decision + chain_of_custody graph. Establishes the input-schema / permission-scope / idempotency / approval-required / risk-class / outcome-tracking pattern that EVERY future agent inherits (Maintenance Planner, MRO Planner, AP Reconciler, CIP Accountant, Production Scheduler, Technician Voice Assistant). Blueprint Appendix B11 has the data model. Acceptance: 3 new tables shipped with EF migrations + the pattern documented in **ADR-024 (new)** Tool Registry & Recommendation Lifecycle. **The voice tools we have today (`IReceiptVoiceTools`) get migrated to register through this in a follow-up.** Status: Planned (Sprint 12D PR #2.5).

3. **Controller Cockpit with source-to-GL voice drilldown — second pitch motion (Fortune-100 CFO audience)** ⭐ APPROVED 2026-05-20 — net-new 1-week build that creates a parallel demo for Fortune-100 CFO buyers, alongside the EVS/Joe AI-APS demo. Financial spine already exists (3K+ LOC AP/GL/JE/Periods/CIP/CCA/Tax/Approvals/Integrations, untouched per v1 lock). What we're building is the **cockpit** + the **voice narration**: Controller asks "why is NBV $1.2M on Asset #4231?" → AI walks JE lines back to invoice → PO → WO → CapitalProject → AssetBasis → DepreciationRun, narrated naturally, with every step a clickable drill link. Lands as **new Sprint 12.7 (1 sprint, 5 PRs)** between Sprint 12.5 close and Sprint 13 open. Acceptance: live demo on /Controller with the question + voice answer + 5-deep drill chain. Status: Planned.

4. **AI design principle codified — "LLM proposes. Domain service executes. Workflow approves. Audit records. Human owns risk thresholds"** ⭐ TAKE — add as **ADR-014 D11 (amendment)**. Becomes the platform-wide design rule for every AI feature from Sprint 12D onward. Already implicitly true (IdempotencyMediator, AuditLog AI columns) — codifying it prevents future drift. Acceptance: ADR-014 amended; principle appears in `docs/contributing.md` or equivalent; first 3 PRs after the amendment cite it in PR body. Status: Drafting.

5. **Recommendation outcome tracking + AI KPI dashboard** ⭐ TAKE — adds a `ai.RecommendationOutcomes` table that records (recommendation_id, accepted/rejected/executed, actual_financial_impact, actual_downtime_impact_minutes, measured_at). The feedback loop that lets us tell ABS/EVS "our AI recommended X 14 days ago, you accepted, here's the $23K it saved." Compounds defensibly over time. Lands in Sprint 12D follow-up (PR #7 after the 6 base PRs). Acceptance: every Recommendation row has an outcomes record by 30 days post-acceptance; KPI dashboard renders true-positive / false-positive / cost-avoided / downtime-saved aggregates. Status: Planned (Sprint 12D PR #7).

6. **Asset Administration Shell (IDTA) research spike** ⭐ TAKE — pre-Sprint 19 (Spatial Plant Floor Map) research investigation only, no implementation. AAS is the German industrial digital-twin standard. If we win aerospace/automotive customers post-launch, they'll ask. We should know whether our asset/component/document/telemetry submodel structure already aligns or needs adjustment BEFORE Sprint 19 locks the hierarchy. **2-day research spike**, output is a 2-page doc `docs/research/aas-idta-investigation.md` + recommendation (align, ignore, or partial-adopt). Acceptance: research doc shipped, recommendation captured as either ADR or memory. Status: Backlog (pre-Sprint 19).

7. **Vertical Packs framing for the 12 industry profiles** ⭐ TAKE — pure marketing/sales reframe of the ADR-015 profile catalog. Same 12 profiles (STEEL/AEROSPACE/OIL_GAS/ELECTRONICS/FOOD/PHARMA/CHEMICAL/MEDICAL_DEVICE/CANNABIS/AUTOMOTIVE/APPAREL/CONSTRUCTION), repositioned in sales materials as "starter packs" — "Metal Fab Starter Pack," "Aerospace Compliance Pack," etc. Each pack = profile + sample PMs + sample BOM templates + sample receipt profile attributes + sample voice tools. Blueprint Appendix C Month 21-22 calls these "vertical packs"; we already have the data, just need the packaging. Acceptance: 1-page sales sheet per pack for the 3 customer-aligned verticals (STEEL → ABS, AEROSPACE → ABS Tier-1, FOOD → FSC). Status: Backlog (Sprint 22 launch polish).

8. **Automated RLS tests + tenant-leak gates** ⭐ TAKE — moves from implicit Sprint 12.5 deliverable to explicit named workstream. Blueprint's D2 reinforces what we know: service-layer scoping is in place, but we need (a) DB-level RLS policies on every tenant-scoped table, (b) automated test that creates Tenant A + Tenant B + asserts cross-tenant read/write returns zero rows / 403s, (c) CI gate that blocks merge if RLS test fails or detects a new tenant-scoped table without RLS. Folds into Sprint 12.5 Workstream A (Foundation Security). Acceptance: RLS test suite green; CI gate active; doc enumerating every tenant-scoped table + its RLS policy. Status: Planned (Sprint 12.5 Workstream A).

9. **Control Plane Standardization + Service Layer Standard** ⭐ ADDED 2026-05-20 (Dean flagged the past pain of handwritten SQL + no control plane on a prior app; we audited and found a partial version of the same risk). **Audit results (run 2026-05-20):** raw SQL is contained to 4 files (✅ low blast radius), the financial spine is properly serviced (✅ ApPostingService / ReceivingPostingService / CipCapitalizationService / CapitalImprovementPostingService / DepreciationService / PeriodGuard all exist), IdempotencyMediator (ADR-014 D4) exists. **But:** 106 Razor PageModels inject `AppDbContext` directly, 57 of them call `.SaveChangesAsync()` (173 total write calls), worst offenders are `WorkOrders/Details` (17 writes), `Purchasing/Details` (12), `Materials/ItemEdit` (11), `Admin/Requisitions` (10), `Maintenance/Technicians/Profile` (7), `Maintenance/Details` (7) — these pages can bypass `PeriodGuard` / `AuditService` / `IdempotencyMediator` if a developer forgets to invoke them, and only 11 files use `IdempotencyMediator` (95 legacy pages do not). The pattern is exactly what bit Dean before, just less severe.

   **Scope (4 sub-items):**
   - **ADR-025 (new) — Service Layer Standard:** every operational mutation goes through a Service; only thin admin-CRUD reads may use `_db.` directly; new PageModels MUST inject the relevant `IFooService`, not `AppDbContext`. Codifies the posting-service interface contract: every posting service guarantees idempotency key + period guard + balanced JE validation + source-doc reference + audit event + outbox event.
   - **CI architecture gate:** Roslyn analyzer OR grep-based CI check that fails the build if a NEW PageModel (added in the diff) injects `AppDbContext` for write paths. Prevents drift from getting worse without forcing a refactor of the 106 legacy pages.
   - **Top-10 refactor backlog:** the 10 highest-risk Pages (5+ SaveChanges calls) get refactored to call their domain `IFooService`. Interleaved across Sprints 12.5–17 at ~2 per sprint as side-work, NOT a freeze-everything refactor.
   - **Posting service interface contract:** `IPostingService<T>` base interface that every new posting service implements. Locks the pattern so Sprint 13 Purchasing's posting service + Sprint 14 Maintenance's all conform.

   **Why this is different from Dean's previous app pain:** No big-bang rewrite. Add the gate, refactor the worst 10 over 5 sprints, leave the simple admin CRUD alone. The gate stops the bleeding; the refactor is paced.

   **Effort:** ADR-025 + CI gate = 1 PR (~1 day). Top-10 refactors = 10 PRs interleaved across 5 sprints. Posting interface contract folds into Item 2 (Tool Registry) as ADR-025 sibling.

   **Acceptance:** ADR-025 merged; CI gate green and blocks the next attempt to add `AppDbContext` to a new PageModel write path; first refactor PR ships against `WorkOrders/Details` (highest-risk). Status: Planned.

**Sequencing summary:** Items 1, 4, 6, 7 are zero-code or near-zero-code; can land anytime. Items 2 and 5 fold into Sprint 12D (June 3 deadline) and are scoped to fit. Item 3 (Controller Cockpit) is the only net-new sprint (Sprint 12.7, between 12.5 and 13). Item 8 is a Sprint 12.5 workstream named-out. **Item 9 (Control Plane) is the most consequential of the audit-driven absorptions — the ADR + CI gate land in 1 PR, the refactor backlog runs as interleaved side-work through Sprints 12.5–17.** **No item disrupts the June 3 EVS deadline. No item changes the AI-native moat positioning.** The absorption strengthens the chain-of-evidence narrative the blueprint sharpened while keeping our AI-first sequencing intact.

**Why this matters strategically:** The blueprint represents the conservative enterprise-software wisdom about HOW to build this product. By absorbing the best ideas (Tool Registry, Recommendation Outcomes, Chain-of-Evidence framing) without adopting the cautious build sequence, we get both the credibility moat (controller demo will play in Fortune-100 rooms) AND the disruption moat (voice + AI-native + 10x faster to deploy than SAP). Building both pitch motions on the same codebase doubles the addressable market without doubling the engineering effort.

**Session progress (cumulative through 2026-05-22):**
- ✅ **Item 9 quick-win SHIPPED 2026-05-20** — ADR-025 + CI control-plane gate (PR #265, commit `658d0cd`). Gate runs in 12s. From this commit forward, no new PageModel can inject `AppDbContext` for write paths without the explicit `// PRAGMA: control-plane-exempt` allow-comment.
- ✅ **Item 10 (CIP cost basis hotfix) SHIPPED 2026-05-20** — PR #268, commit `66c4688`. OutsideServices capitalization on WO closeout. Materials capitalization deferred to Sprint 14+ Controller Cockpit (locked via negative test).
- ✅ **Sibling rename A (UI Labels) SHIPPED 2026-05-20** — PR #266, commit `ef13cb4`. Sidebar, /Maintenance, /WorkOrders/Details all now say "Maintenance Order" / dynamic per Classification.
- ✅ **Sibling rename B (WorkOrder Prefixes) SHIPPED 2026-05-20** — PR #267, commit `e64664a`. 242 records renamed `WO-XXX` → `MO-XXX` via psql migration. ProductionOrder future default locked at `PRO-`.
- ✅ **Item 9 ELEVATED to Sprint 12.9 (2026-05-22)** — Dean's call. The interleaved refactor backlog wasn't enough to back the chain-of-evidence sales line ("Machine Event → General Ledger") under Fortune-100 procurement diligence. Item 9 sub-items 1-3 done (ADR + gate + first refactor still pending). Sub-item 4 (Posting contract) + top-3 refactors + audit-completeness metric + RLS leak test now all in a real 7-PR sprint slot (Priority 1.6080). **Audit-trail rigor moved from "side-work" to "sales weapon."**
- ⏳ **Items 1, 4, 6, 7 still in original status** (Backlog/Drafting per artifact).
- ⏳ **Item 8 (Automated RLS tests + tenant-leak gates) FOLDED INTO Sprint 12.9 PR #7** — was Planned in Sprint 12.5 Workstream A. Moves up because RLS verification is part of "control plane done."

**Priority 1.62 — Sprint 12B Receiving DEPTH** ⭐ NEW 2026-05-18 / **AUDITED + RESPEC'D 2026-05-22 (Dean): 4 of 6 originally-listed PRs are already shipped or have working substrates; 2 remain as real work but with external dependencies — split + tracked.**

### Audit results 2026-05-22

| PR # (original) | Original claim | Verified actual state | Disposition |
|---|---|---|---|
| **PR #1** `MatchOrphanReceiptAsync` real impl | "Replace Sprint 11 stub" | ✅ **ALREADY SHIPPED in Sprint 12A PR #7** (commit `6759ca5`). Deterministic ranker live: Score = item-match 50 + recency-bucket 30 + same-profile 20. Both voice wrapper (`Services/Voice/ReceiptVoiceTools.cs:353`) AND save method (`Services/Receiving/ReceivingControlCenterService.cs:1428`) wired. AuditLog with ActorKind=Ai per ADR-014 D3. | **MARK DONE** — credit this to Sprint 12A scope completion. |
| **PR #2** `OcrParseMillCertAsync` real impl | "Replace Sprint 11 stub" | ⏳ **REAL STUB** — explicitly returns `Result.Failure("OCR mill-cert parsing is wired in Sprint 5...")` at `Services/Voice/ReceiptVoiceTools.cs:545`. Contract shipped, runtime not wired. | **KEEP — `Sprint 12B.2 OCR Mill Cert`** (see deferred-work block below). |
| **PR #3** `ReceiveByVoiceAsync` + `QuarantineByVoiceAsync` end-to-end | "Sprint 11 stubs" | ✅ **ALREADY WIRED** at `Services/Voice/ReceiptVoiceTools.cs:503` + `:524`. Both delegate to real `_receiving.ReceiveByPoAsync` / `_receiving.QuarantineAsync` + AI audit logging. IdempotencyKey + VoiceContext flow through. End-to-end already operational. | **MARK DONE** — credit to Sprint 11/12A. Add a `Sprint 12B.3.1 voice-receipt smoke test` as a one-PR hardening item (see deferred-work block). |
| **PR #4** DataWedge hardening on Zebra TC52/TC57 | UI/JS work | ⏳ **REAL — hardware-dependent.** Needs physical Zebra to test against (keystroke buffering, scan-burst, GS1-128, DataMatrix). | **KEEP — `Sprint 12B.4 DataWedge`** (see deferred-work block). |
| **PR #5** Mobile + Zebra responsive | Multi-PR responsive | ⏳ **REAL — multi-PR design work.** Cockpit primitives need responsive breakpoints below ~768px. | **KEEP — `Sprint 12B.5 Mobile responsive`** (see deferred-work block). |
| **PR #6** Receipts API for ERP integration | "Net-new" | ✅ **SUBSTRATE ALREADY IN PLACE.** Verified: `IOutboxWriter`, `OutboxWriter`, `WebhookDispatcherHostedService`, 19 typed `IDomainEvent` records (`ItemReceivedV1`, `PoReceivedV1`, etc.), `WebhookSubscription` + `WebhookDeliveryLog` + `ApiKey` entities, admin UI partials at `Pages/Admin/Webhooks/`. `ReceivingPostingService.cs:300` already enqueues `ItemReceivedV1` per stock line. Sprint 12D chain edges fire right after. | **SPLIT** — substrate is done. Gap items broken out as 3 separate deferred-work PRs (see below). |

**Net status:** **2 of 6 verified done** (PR #1 + PR #3), **1 substantially done with documented gaps** (PR #6 substrate), **3 real but external-dependency** (PR #2 OCR / PR #4 DataWedge / PR #5 Mobile).

### Sprint 12B DEFERRED-WORK BLOCK (post-13.5; each item has an owner and a trigger)

| ID | Title | Why deferred | Trigger to start |
|---|---|---|---|
| **12B.2** | OCR Mill Cert runtime wiring | External dependency: Azure Document Intelligence OR AWS Textract OR self-hosted Tesseract. ~2-3 days work + API key + cost decision. Not demo-critical for ABS Thursday (we can ingest the FAI PDF as an attachment without OCR). | When a customer signs that requires automated heat-number capture, OR when Sprint 14 Quality work picks up FAI workflow. |
| **12B.3.1** | Voice-receipt end-to-end smoke test | Voice path is wired; never been load-tested end-to-end with IdempotencyMediator under contention. ~2 hours. | After Sprint 13 (Purchasing CC) ships — same idempotency layer gets exercised there. |
| **12B.4** | DataWedge focus mode on Zebra TC52/TC57 | Needs physical Zebra hardware to test scan-burst + GS1-128 + DataMatrix parsers. Can't fully validate in CI/Replit. | When a customer with Zebra hardware enters pilot (ABS is a candidate). Pre-pilot: write the JS in a feature branch + ship to a staging tenant for live testing. |
| **12B.5** | Mobile + Zebra responsive cockpit | Multi-PR design work; the cockpit primitives need responsive breakpoints below ~768px. Touch targets, scan-first focus, single-column layout. ~3-4 PRs. | After Sprint 13 + 13.5 ship and the cockpit pattern is proven across 3+ Control Centers. Premature now. |
| **12B.6a** | `ItemReceivedV2` with chain-of-custody enrichment | Today `ItemReceivedV1` fires BEFORE chain edges are written. Subscribers don't get the chain in their webhook. V2 reorders + enriches. ~1 hour. | Easy win — fold into Sprint 13.5 PR #6 (subcontract chain edges) since both touch the chain layer. |
| **12B.6b** | GET `/api/v1/receipts` pull endpoint | Symmetric to push webhook; some ERP integration teams prefer polling. ~1.5 hours. | When first ERP-integration customer requires pull over push. Sprint 21 ERP integration sprint reference. |
| **12B.6c** | Per-ERP outbound format adapters | SAP IDoc, Oracle REST, NetSuite SuiteTalk, D365 OData. Each adapter is its own PR. Multi-sprint workstream. | Sprint 21 ERP integration sprint — explicitly scoped as "per-customer connector lands when a customer-pull signal arrives." |

**Net result:** Sprint 12B is **closed at 2-of-6 done + 1 substrate done**, with **7 named deferred-work items each with explicit triggers**. No orphans. No scraps. The customer-channel-INTO-ERP thesis is parked at Sprint 21 with the substrate in place. See also `project_sprint_12b_verified_state.md` for the canonical audit memory.

**Priority 1.63 — Sprint 12.5 MASTER_PLAN Catch-Up Sprint** ⭐ NEW 2026-05-18 / **DEFERRED 2026-05-22 behind Sprint 13.5** — security/admin/a11y workstreams stay critical for launch (Sprint 22+) but neither ABS Thursday nor EVS June 3 evaluates us on MFA / SSO / DB-level RLS depth. Re-engages after Sprint 13.5 ships Manufacturing + CustomerProject. Five workstreams:
- **A — Foundation security (sales-blockers for enterprise prospects):** MFA TOTP, SSO SAML 2.0 + OIDC (Okta, Azure AD, Google Workspace tested), full DB-level Postgres RLS (service-layer scoping is in place; promote to row-level policies)
- **B — First-run Onboarding wizard:** tenant setup → first user → first site → first asset → first PO → first user invite → "you're live" milestone
- **C — Admin v2 UX + a11y closeout:** finish remaining admin pages on Phase 3 design system, form labels on edit pages (~19 violations on ItemEdit alone), CI app-boot hardening, sparse-page redesigns (PO Detail / AP Detail / AP List get content)
- **D — Tech debt cleanup:** 9 skipped xUnit tests rewritten, EF model snapshot regen, Playwright suite age-check, branch cleanup (66 local + 136 remote), PR-number gap investigation (#3, #15, #81, #130)
- **E — i18n foundation:** Sprint 3 #137 — keys + resx + locale switching infrastructure (doesn't translate everything; ships the foundation so all future Control Centers are i18n-ready by default)

**Priority 1.62a — Sprint 13.6 UI Audit + Cleanup** ⭐ NEW 2026-05-25 (Dean directive, end of PR #338 session) — **Wave 2.5 parallel workstream**, slots between current cleanup-pass (PRA-5g→PRA-5k) and Wave 3 MES-event resume so it doesn't block either critical path. Dean direct quote: *"a lot of these forms and subforms are taking up MASSIVE amounts of real estate. The side menu is a disaster and many of the links are old or point to old or non-existing pages. Lets update the master plan."*

**Phase 1 — Audit (4 parallel research agents, ~1 day total wall-clock):**
1. **Side-Nav Sweep Agent** — read `Services/Navigation/NavRegistry.cs` + `Pages/Shared/_NavSidebar.cshtml` + `ControlCenterRegistry`. For every link: verify the route resolves to a live PageModel, the page actually renders (no 500), the label matches the page's `ViewData["Title"]`, no orphan entries pointing at deleted pages. Output: `docs/research/nav-audit-2026-05-25.md` with a verified/broken/orphan triage table.
2. **Page Render Audit Agent** — enumerate every `Pages/**/*.cshtml`, hit each route via Chrome MCP against a live Replit instance, capture HTTP status + render time. Flag 404s, 500s, blank pages, and pages that aren't reachable from any nav entry. Output: `docs/research/page-render-audit-2026-05-25.md` with per-page status + cross-reference to NavRegistry.
3. **Form Density Audit Agent** — grep for `data-csp-style="margin-bottom: 1rem"` + `data-csp-style="padding: 0.5rem"` + multi-section forms. Compare against the locked design tokens (`wwwroot/css/design-tokens-v2.json` — asymmetric 4/6/12/20/40 spacing scale + 32px DataTable rows + tnum + cv11 OpenType). Identify pages that violate the density bar. Reference exemplar pages (Receiving CC, Operator Workbench) for the target compaction. Output: `docs/research/form-density-audit-2026-05-25.md` with worst-offender ranked list + before/after sketches for the top 10.
4. **Subform Redundancy Audit Agent** — find pages with duplicated section headers, redundant context chrome (multiple "Back to X" links, repeated KPI tiles, duplicate breadcrumbs), or sections that should collapse into a single component. Output: `docs/research/subform-redundancy-audit-2026-05-25.md` with consolidation candidates.

**Phase 2 — Synthesis (1 PR — Sprint 13.6 PR #1):** Merge the 4 audit memos into a single prioritized fix list `docs/research/ui-cleanup-fix-list-2026-05-25.md` with severity tags (P0 broken-link / P1 dense-form / P2 redundant-subform / P3 polish). Ship docs-only.

**Phase 3 — Execution (estimated 4-6 PRs, ~3-5 days):**
- Sprint 13.6 PR #2 — **NavRegistry cleanup** (delete orphan entries, fix mis-pointing labels, regroup per current ControlCenterRegistry IsLive flags, drop "soon · Sprint X" placeholders).
- Sprint 13.6 PR #3 — **Top-10 form-density fixes** (compact the worst offenders identified in Phase 1, conform to design-tokens-v2.json asymmetric spacing).
- Sprint 13.6 PR #4 — **Subform consolidation** (merge duplicated chrome, extract shared section-header components).
- Sprint 13.6 PR #5 — **Dead-page sweep** (delete or 301-redirect broken/orphan pages per the audit).
- Sprint 13.6 PR #6 — **Cockpit primitive backport** — pages still using `_ScreenHeader` legacy style get upgraded to `_CockpitPageHeader` where they're Control Centers (per Lock 3).
- Sprint 13.6 PR #7 — **Sidebar IA pass** — apply Dean's call on grouping ("Today / Operations / Finance / Insights / Master Data / AI & Integrations / Settings" from PR #4 is the current shape; audit may surface re-groupings).

**Authority for all density/spacing decisions:** `wwwroot/css/design-tokens-v2.json` (locked) + `docs/research/luxury-cockpit-ux.md`. Lock 3 (reuse Cockpit primitives) still holds for any page that's a Control Center.

**Acceptance:** Dean can navigate from sidebar to every linked page without hitting a 404, every form fits one screen viewport without scroll on a 1440×900 baseline, every section has a clear purpose with no duplicated chrome.

**Pre-reqs:** none beyond a live Replit instance. Can start the moment PRA-5f / PRA-5g cleanup-pass ships. Doesn't block the MES event cascade.

---

**Priority 1.65 — Sprint 13 Full Purchasing Control Center** ⭐ MOVED 2026-05-18 / **DEFERRED 2026-05-22 behind Sprint 13.5** — `IPurchasingService` already exists (Sprint 12.9 PR #4), so the underlying service surface is mature. The Cockpit overlay (KPI band + queue + preview per ADR-018) is valuable but second-fiddle to making `ProductionOrder` visible for ABS Thursday + EVS June 3. Resumes after 13.5 ships. First non-Receiving Cockpit. ~8 PRs:
1. **`IPurchasingControlCenterService` + state machine** — mirror of Sprint 11 PR #3 for the Purchasing role. PR / RFQ / PO state transitions, idempotency, audit DTOs.
2. **10 `IPurchasingVoiceTools`** — ListExpedites / DraftPO / ApproveRequisition / IssueRFQ / RecordQuote / ConvertQuoteToPO / FlagSupplierAnomaly / RequestSupplierStatus / LogVendorScorecardEvent / OcrParseQuote.
3. **Open POs tab (cockpit, default)** — POs in flight, time-bucketed by required delivery date. Preview shows lines + supplier scorecard tile + expedite CTA.
4. **Requisitions tab (cockpit)** — PRs awaiting approval/processing, time-bucketed by needed-by. Preview shows requester + justification + suggested vendor (AI).
5. **RFQ tab (cockpit)** — quotes in flight, time-bucketed by response-due. Preview shows quote responses + price comparison + recommendation.
6. **Vendor scorecards (Sprint 3 #129 folded in)** — 6-table feature: on-time %, fill rate, price variance, defect rate, lead-time stability, concentration risk. Rendered as Exceptions-tab quadrant + per-PO preview tile.
7. **Blanket/Contract-PO (Sprint 3 #132 folded in)** — PO subtype with drawdown logic. Exceptions-tab alerts when drawdown crosses threshold.
8. **Exceptions tab + flip registry** — supplier scorecard quadrant + expediting urgent items + PR/PO mismatch lane + payment-term deviation lane. Flip `PURCHASING` to `IsLive: true` in `ControlCenterRegistry`.

**Priority 1.66 — Sprint 13.5 Manufacturing Domain + Project/Job Hierarchy Foundation** ⭐ NEW 2026-05-22 / **PROMOTED 2026-05-22 to NEXT-UP (jumps Sprint 12B remainders + 12.5 + 13)** — fills the conspicuously absent Manufacturing domain in Sprints 14-18 + establishes the `CustomerProject` schema that lets ABS / EVS / mixed-mode customers model their actual world. **Now also absorbs Sprint 12B.6a `ItemReceivedV2` chain-enrichment** (folded into PR #6 — both touch the chain layer). **Driven by:** (a) ABS Thursday-demo customer signal (precision machining shop, Tier-N to Weir Oil & Gas + Caterpillar, most work is engineer-to-order); (b) `ProductionOrder` entity exists since Sprint 3 Phase E.1 (PR #119.12) but has **zero UI and no service layer** today — confirmed via 2026-05-22 audit; (c) industry-research-validated schema pattern (11 ERPs surveyed: SAP, Oracle, D365, IFS, Epicor, Acumatica, SYSPRO, Infor LN, JobBOSS², Global Shop, Made2Manage). See `research_project_job_hierarchy_patterns.md` and `project_abs_customer_profile.md`. ~8 PRs:

1. ✅ **SHIPPED 2026-05-22 — `CustomerProject` + `ProjectMember` + `ProjectPhase` + `Program` schema migration** — PR #293 squash-merged as c8f6d79. New entity `CustomerProject` (named to avoid collision with existing `CipProject` — capital improvement). Nullable FK `ProductionOrder.CustomerProjectId` + `ProjectPhaseId` + `ProjectPostingMode` enum (`FinishedItem` / `Consumed` / NULL) on `ProductionOrder` header. `Program` table created empty for v2 portfolio/EVM use. `ProjectMember` M:N supports joint-venture / pass-through scenarios. **Dev DB migration applied + Replit workspace pulled + Web Server restarted at c8f6d79. Prod DB migration application + Republish to `industryos.app` still pending — Replit auto-gen pipeline has a column-quoting bug; recipe in `project_sprint_13_5_pr1_shipped.md` memory: hand-apply `/tmp/sprint135-pr1.sql` against prod Neon, then Republish.** See `Models/Projects/CustomerProjects.cs` + `Migrations/20260522_AddCustomerProjectFoundation.cs`.

   1.5. ✅ **SHIPPED 2026-05-23 — Field expansion (AI + EVM + Aero/Def) + `ProjectAmendments` + governance flag + CHECK constraints + append-only trigger** — PR #294 squash-merged as fb44052. 14 new nullable cols on `CustomerProjects` (RiskScore/RiskTone/AiSummary*/EstimatedTotalCost/PercentComplete/ProjectedEndDate/LastEvmRollupAt/CustomerPoNumber/ContractType/QualityProgram/ExportControl), new `ProjectAmendments` table (append-only change-order log with monotonic AmendmentNumber + ValueDelta SUM-against-baseline pattern), `Companies.ProjectExportControlRequired` tenant governance flag, 10 CHECK constraints + Postgres trigger `fn_block_amendment_status_regression` blocking illegal Status regressions, 2 cockpit-sort partial indexes. Backed by 4-dimension research spike (`docs/research/customerproject-field-set.md`, ~570 lines, ~30 sources covering AI infusion patterns from Linear/Asana/Notion/D365/SAP Joule + ANSI/EIA-748 EVM + AS9100/ITAR + AIA G701 / Acumatica change orders). Dev DB applied; binary verified live (fresh dotnet PID, all endpoints 200).

   1.75. ✅ **SHIPPED 2026-05-23 — AS9102 FAI workflow (Form 1/2/3) + Attachments reuse + status-regression trigger** — PR #295 squash-merged as beeadd8. Three new tables (`FaiReports` ~50 cols Form-1 header + lifecycle + AI fields; `FaiCharacteristics` Form-3 per-balloon dim rows with numeric+text tolerance fields side-by-side; `FaiProductAccountability` Form-2 mat/spec/process/test rows with EntryType discriminator). Existing `Attachments` table extended with 3 nullable FK cols + new `AttachmentSource`/`AttachmentCategory` enum values (cross-cutting per research §5 — no FaiAttachment table). `BaselineFaiReportId` self-FK ships now for AS9102 Rev C Partial/Delta lineage. FK to existing `MrbDispositions` for non-conform traceability. 11 CHECK constraints + status-regression trigger (`fn_block_fai_status_regression`). 4 hot-path partial indexes (open queue, non-conform sub-query, FK lookups). Backed by AS9102 research spike (`docs/research/fai-workflow-schema.md` mirrored as memory `research_fai_workflow_schema.md`, ~415 lines, 15 sources). Dev DB applied; binary verified live (fresh dotnet PID 12574 etime 00:23). **All 3 PRs also applied to PROD Neon + Republished to industryos.app.**

   **— SCOPE EXPANSION 2026-05-23 (Dean's "no stones unturned" + UX + Master Files research):** Sprint 13.5 grows from 8 PRs to **14 PRs.** Three master-file PRs added (PRA-1/PRA-2/PRA-3) per `docs/research/master-files-audit.md`. UX baseline locked per `docs/research/luxury-cockpit-ux.md` + `wwwroot/css/design-tokens-v2.json`. Four decisions locked: `Tenant.Mode` enum ships in PR #4; Cherry Bar = top-right pill + cmd-K; first-project Mode = hybrid (3 buckets + "Show all 17"); `Company.IndustryVertical` = 17-value enum locked.

   **PRA-1.** ✅ **SHIPPED 2026-05-23 — Master Files foundation** — PR #296 squash-merged as `bfb3922`. `Company.IndustryVertical` smallint enum (17 values 0=Unspecified..16=GeneralMfg; 17-31 reserved) + `Company.CageCode` + `Company.DunsNumber`. New first-class `Carriers` table (Id/CompanyId nullable for system carriers/Code/ScacCode/Name/Contact/TrackingUrlTemplate/ApiEndpoint/etc.) with 12 system-wide carriers seeded (UPS/FedEx/DHL/USPS/OnTrac/XPO/OD/YRC/Saia/Pickup/WillCall). `ShippingMethod.CarrierId` + `AdvancedShippingNotice.CarrierId` FKs added (free-text `Carrier` column kept for back-compat per DEF-008). Customer +12 cols (4 defaults: DefaultQualityProgram/DefaultExportControl/DefaultContractType/DefaultRevenueMode for project-create inheritance · CageCode/DunsNumber · CreditLimit · TaxCodeId FK · 5 BillTo address fields). Vendor +9 regulator IDs (CageCode/DunsNumber/UEI/FdaEstablishmentId/DeaRegistration/ItarRegistration/As9100CertRef/Iso9001CertRef/Iso13485CertRef) + PaymentTermId FK retrofit + DefaultReceiptAttributes JSONB (ADR-015) + SendsAsn/AsnFormat ASN-receiving hints. Manufacturer +CageCode +DunsNumber. ADDITIVE-ONLY, idempotent (IF NOT EXISTS + DO $$ blocks). Dev DB + Prod Neon both applied; industryos.app live.

2. ✅ **SHIPPED 2026-05-23 — `ICustomerProjectService` + DI wiring + chain emit** — PR #297 squash-merged as `a113b49`. 8 mutation methods: `CreateAsync` (+CustomerProject chain node) · `UpdateHeaderAsync` (rejects Closed/Cancelled) · `UpdateStatusAsync` (legal-transition map Quote→Active→OnHold/Closed/Cancelled) · `AddMemberAsync` (+Customer→CustomerProject MEMBER_OF chain edge) · `AddPhaseAsync` (no chain — internal WBS) · `LinkProductionOrderAsync` (FK + posting mode + CustomerProject→ProductionOrder CONTAINS_PRODUCTION_ORDER chain edge) · `CreateAmendmentAsync` (Draft, MAX+1 numbering under SELECT FOR UPDATE row lock) · `TransitionAmendmentStatusAsync` (legal-transition map; Postgres trigger backstops). Chain-of-custody graph (ADR-022) gains 3 new node types (CustomerProject, ProductionOrder, Customer) + 2 new edge types (MEMBER_OF, CONTAINS_PRODUCTION_ORDER). ADR-026 export-control rule enforced in CreateAsync. CHERRY025 analyzer clean. Service-layer-only — every PageModel/voice-intent mutation goes through this service, never AppDbContext direct. Dev + Prod both live.
3. ✅ **SHIPPED 2026-05-23 — `IProductionOrderService` (greenfield)** — PR #298 squash-merged as `73a9ebf`. 5 v1 methods (WorkOrderService PR #3 phasing precedent — minimum viable, JE/inventory writes phase in later): `CreateAsync` (+ProductionOrder chain node ensure, tenant-scoped via Location/Customer fallback, Item-company match check) · `UpdateHeaderAsync` (rejects Completed/Cancelled) · `UpdateStatusAsync` (legal-transition map Planned→Released→InProgress→OnHold→Completed/Cancelled + ActualStart/ActualEnd stamps) · `AssignToProjectAsync` (delegates to `ICustomerProjectService.LinkProductionOrderAsync` so the chain edge + posting-mode rule stay single-source) · `UnassignFromProjectAsync` (nulls FK trio; admin-only against Closed/Cancelled projects, Sprint 16 override). Deferred to Sprint 14: IssueMaterialAsync / CompleteQuantityAsync / ScrapQuantityAsync (JE-posting writes). CHERRY025 clean. Dev + Prod both live.

   **PRA-2.** ✅ **SHIPPED 2026-05-23 — Cross-cutting masters** — `fc05b78` (PR #299 + PR #300 same-day hotfix). 4 new tables (additive-only, idempotent): `Countries` (ISO 3166-1 — Alpha2/Alpha3/Numeric/Name/OfficialName/CallingCode/DefaultCurrencyCode + UNIQUE Alpha2 + UNIQUE Alpha3, 8 Tier-1 seeded: US/CA/MX/GB/DE/FR/JP/CN) · `Subdivisions` (ISO 3166-2 — CountryId FK + Code + Name + Type enum + UNIQUE (CountryId, Code); seeded US 50+DC+5 territories, CA 10 provinces+3 territories, MX 32 states = 101 rows) · `WorkCalendars` (per-tenant w/ system fallback NULL CompanyId — TimeZone IANA + WorkDayMask smallint bitfield + WorkDayStart/WorkDayEnd + IsDefault; 1 system seed "US Standard Business Week" Mon-Fri 8-5 America/New_York) · `Holidays` (per-calendar non-working dates instance-based — WorkCalendarId CASCADE + ObservedDate + NominalDate + SubdivisionId nullable + Category enum; 11 US Federal Holidays for 2026 seeded incl. Jul-3 observed-for-Jul-4 sat-slide). **NOT in scope (deferred):** Customer/Vendor.CountryId FK retrofit · rule-based recurrence ("3rd Monday in January") = PRA-2.1 polish · holiday-generator UI = PRA-2.1 polish. **PR #300 hotfix recipe for future PR authors:** SQL string literals inside `mb.Sql(@"...")` C# verbatim blocks use single quotes (`'foo'`); apostrophes inside escape by doubling (`'foo''s'`). NEVER wrap user-facing text in `""..""` (C# `""` → SQL `"x"` which Postgres reads as an identifier, not a string).

4. ✅ **SHIPPED 2026-05-23 — Best In Class nav rebuild + /Production + /CustomerProjects + Master Files admin** — `3904a81` (PR #301). FIRST customer-visible Sprint 13.5 surface. **Nav rebuild:** new `Services/Navigation/NavRegistry.cs` (single source for sidebar items outside the Cockpit spine, 5 top-level groups Today/Operations/Finance/Insights/Master Data/AI&Integrations/Settings) replaces ~300 lines of hard-coded menu HTML in `_ModernLayout.cshtml` with one `@await Html.PartialAsync("_NavSidebar")` call. `ControlCenterRegistry` extended with PRODUCTION + CUSTOMER_PROJECTS (IsLive=true). Disabled CCs now HIDDEN from sidebar (kills "soon · Sprint X" clutter per Dean's "menu is a mess" complaint). **/Production cockpit:** Index (KPI strip + status filter chips) + Details (header + status-transition actions through `IProductionOrderService.UpdateStatusAsync`) + Create (form posting to `IProductionOrderService.CreateAsync`). **/CustomerProjects cockpit shell:** Index + Details (header + Jobs/Phases/Members/Amendments child summaries) + Create. Full ADR-018 cockpit pattern (queue-left + preview-right + KPI band) deferred to PR #5a. **Master Files admin:** read-only Index pages for `/Admin/Countries`, `/Admin/Carriers`, `/Admin/WorkCalendars`. **Design language:** `wwwroot/css/cockpit-v2.css` 350+ lines — concrete implementation of locked v2 design tokens (Inter + JetBrains Mono, asymmetric spacing 4/6/12/20/40, monochrome chassis + ONE Cherry red accent, tri-state RAG, 32px DataTable rows, tnum + cv11 OpenType features). **CHERRY025:** 9 explicit `[ControlPlaneExempt("reason")]` attributes on new PageModels (read-only display OR write-through-service). **E2E tests:** `tests/sprint13_5_pr4_nav_and_pages.spec.js` with 10 Playwright specs. Build clean: 0 errors, 0 warnings from new files. **Deferred to follow-up PRs (named):** generic `<Link>` Tag Helper for legacy-page drill-everywhere · Master Files Edit forms · `<vertical-chip>` Tag Helper.

4. **`ProductionOrder` UI shell — first time visible to users** — `/Production` route + list view + detail page (header + operations + materials + chain tab) **using locked UX tokens from `wwwroot/css/design-tokens-v2.json` + new `<vertical-chip>` Tag Helper + Cherry Bar voice trigger + `Tenant.Mode` DEMO band.** Mirrors `/WorkOrders/Details` UX patterns. Per-tenant nav surfacing — appears for customers using Production. **Requires PRA-1 + UX baseline locked. First Sprint 13.5 UI surface.**
5. **Customer Project Cockpit — `/CustomerProjects/{id}`** — ADR-018 Cockpit pattern (queue-left + preview-right + KPI band) anchored on a Project. Queue = jobs at risk for this project, sorted by projected-vs-promised. KPI band: open jobs / on-schedule / at-risk / past-due / open POs / late POs / quality holds / margin to date. Preview = full chain-of-evidence for selected job. Uses locked UX tokens + `<vertical-chip>` + first-project Mode picker (hybrid: 3 buckets + "Show all" → 17 verticals).
6. **Subcontract chain edges + entity + `ItemReceivedV2` chain enrichment (folded in from Sprint 12B.6a)** — new ChainEdgeTypes `SENT_OUT_FOR` (Item-pre → Vendor-sub) + `RETURNED_FROM` (Vendor-sub → Item-post). New `SubcontractRequisition` entity (or repurpose existing `PurchaseRequisition` with `IsSubcontract` flag). Handles the `_SUB` part-number workflow ABS uses today in Abas. **Also reorders `ReceivingPostingService.PostReceiptAsync` so chain edges fire BEFORE outbox enqueue, and bumps `ItemReceivedV1 → V2` with a `ChainOfCustody` field** so webhook subscribers (ERP integrators) get the full chain of evidence in one event. Backward-compatible — V1 keeps firing in parallel via envelope `payloadVersion`. Closes the deferred 12B.6a item from the Sprint 12B audit.
7. **Voice intents — Project-axis** — adds `ExplainProjectRisk`, `ListProjectsAtRisk`, `WhyIsJobXLate`, `ProjectCostStatus` intents to `IntentClassifier` + L2 vector prototypes. Pairs the voice surface with the new visual cockpit. Renders via Cherry Bar locked in UX baseline.

   **PRA-3.** Reference codes — `ReasonCodes` master (30-row seed across inventory/AP/WO/project domains: scrap/quarantine/adjustment/hold/cancellation/etc.) + `TaxJurisdictions` scaffold (full rate logic deferred to v2 AP/AR). Severity = BLOCKS-V1-LAUNCH. Parallel-shippable with PR #7. ~0.5 day.

8. **ADR-026 — Seven Customer Modes Contract** — documents the seven enumerated modes the schema must serve (pure job-shop, project-with-jobs, ETO turbine, commitment-only, recurring production, joint-venture, internal R&D) as the schema contract. Anti-patterns explicitly called out (no Default Project per Customer; no Company-level project toggle; Project FK on Job header, not lines).

**Acceptance criteria:**
- All seven customer modes (see `research_project_job_hierarchy_patterns.md` §6) produce coherent data with no fake parent rows
- Pure job-shop customers never see the `/CustomerProjects` nav item unless they create one
- Project-driven customers can create a Project before any Job and link Jobs later
- Chain-of-custody graph filtered by `CustomerProjectId` renders the full project supply chain
- Cost rollup matches D365's two-mode pattern (`FinishedItem` vs. `Consumed`)
- `ProductionOrder` becomes a first-class ChainNodeType with edges to all linked PO / Receipt / Vendor / Asset / Invoice / GL nodes
- No regression in Maintenance `/WorkOrders` flows — `WorkOrder` (maintenance) and `ProductionOrder` (manufacturing) remain strictly separate per `reference_workorder_vs_productionorder.md`

**Cross-refs:**
- `research_project_job_hierarchy_patterns.md` — 11-ERP survey + proposed schema + 7-mode contract + anti-patterns
- `project_abs_customer_profile.md` — ABS customer profile, Thursday demo target, Customer→Project→Jobs hierarchy as Dean defined it
- `project_engineer_to_order_pattern.md` — ETO cross-cutting principle (most ABS + likely EVS work)
- `reference_workorder_vs_productionorder.md` — naming discipline

**Priority 1.665 — Master Files Baseline cascade (Waves 1.2 + 2.1-2.3 + 3.1-3.4)** ⭐ NEW 2026-05-24 (Dean directive: *"we have a chart of accounts but it's def not large enough, not sure on UOM, but we need to make sure once again that we have the infrastructure set up first"*). Full memo: `docs/research/master-files-baseline-2026-05-24.md`. Memory: `reference_master_files_baseline.md`. **8 PRs over ~14 days:**

1. **PRA-4** — UOM master + per-item UOM list (UomCategory, UnitOfMeasure, UomConversion, ItemPackHierarchy). Replaces TWO parallel enums (`Models/Item.UnitOfMeasure` inventory + `Models/Telemetry.UnitOfMeasure` sensors). ~900 LOC. **Wave 1.2 — Mon May 25 PM, pre-ABS.**
2. **PRA-5a** — COA additive expansion. Add ~26 manufacturing/inventory/variance categories to `GlAccountCategory` enum (RM/WIP/FG/SubAssembly/Subcontract/Consigned/PPV/MUV/Labor variance/OH variance/Scrap/Rework/Yield/CTA/Intercompany pairs/CurrentYearEarnings). NO renumber. Seed system default accounts. ~600 LOC. **Wave 1.2 — Tue May 26, pre-ABS.**
3. **(re-slotted PR #5c.4)** — Tenant-aware dev seeder + system ReasonCodes from JSON + MaterialStructure CompanyId orphan backfill. Slotted AFTER PRA-5a so seeder knows new COA shape. **Wave 1.2 — Wed May 27, pre-ABS.**
4. **PRA-5b** — COA segment-key refactor. AccountingKey table (Company-Site-Account-CostCenter-Dept-Project-InterCoPartner-Vertical). JournalLine `AccountId` → `AccountingKeyId`. Backfill existing rows. ~1500 LOC. **Wave 2.1 — Fri May 29 – Sat May 30, post-ABS.**
5. **PRA-6** — Currency + PaymentTerm + TaxCode real-table masters. Replaces enums + thin LookupValue rows. ISO 4217 currency seed (~180 rows). PaymentTerm with DiscountPct/DiscountDays/MultiCutSchedule. TaxCode + TaxAuthority. ~1200 LOC. **Wave 2.2 — Sun May 31 – Mon Jun 1 AM, post-ABS.**
6. **PRA-7** ✅ **SHIPPED 2026-05-24** (`f613cb3`/#317, ~5 days ahead of schedule) — Warehouse + Bin + Lot + SerialMaster + ItemGroup → PostingProfile. **6 new masters** (WarehouseMaster, BinMaster, LotMaster, SerialMaster, ItemGroup, PostingProfile) + posting profile matrix. **SAP S/4 + Dynamics 365 separation-of-concerns shape locked** in ADR-019 (`docs/ADR-019-wms-posting-profile-pattern.md`); explicitly REJECTS the NetSuite single-Location collapse + Oracle Subinventory + Manhattan SCALE 6-deep tree. EAM `Location` stays untouched as the asset-hierarchy entity. Seeded 9 system WarehouseMaster templates + 12 ItemGroup templates + 15 PostingProfile skeleton rows wiring ItemGroup × InventoryTransactionType to PRA-5a `GlAccountCategory` values. Zero rows of Bin/Lot/Serial (operational data — tenant-owned). **Two Codex P2 catches addressed in amendment `2f926e2`**: PostingProfile.WarehouseId `ON DELETE RESTRICT` (SetNull collided with partial-UNIQUE fallback indexes), SerialMaster.AssetId now has FK `REFERENCES Assets(Id) ON DELETE SET NULL`. Live verified on dev DB (`__EFMigrationsHistory` includes PRA-7, all 6 tables present, seed counts 9/12/15 match). **Memory: `project_pra7_shipped.md`.**
7. **PRA-8** — Employee + WageGroup + LaborRate matrix + Department→GL posting profile. Real HR Employee master (Technician becomes 1:0..1 satellite). ~1100 LOC. **Wave 3.1 — Thu Jun 4 – Fri Jun 5 AM, post-EVS.**
8. **PRA-9** ✅ **SHIPPED 2026-05-24** (`f5d5c0d`/#319, ~10 days ahead of schedule) — PriceListMaster + PriceListLine + DiscountSchema + RebateAgreement. **Ships ADR-027 in same PR** (Sales Order Lines + Releases drive Production Orders — locks the future Sprint 19+ SalesOrder shape so `ProductionOrder` can't accidentally couple to a SalesOrder header per Dean's brainstorm Theme B1). 4 new tables + 4 system PriceListMaster templates (DEFAULT-WHOLESALE / DEFAULT-DISTRIBUTION / DEFAULT-RETAIL / DEFAULT-GOVERNMENT, all USD). PriceListLine carries VolumeBreaks jsonb + ETO price-lock. DiscountSchema has 6 DiscountType × 6 AppliesToScope × 3 StackingRule enums + multi-tier JSONB. RebateAgreement has 5-Basis / 6-Period / 6-PayoutMethod / 7-Status with accrual + payout GL FKs. **ZERO Codex catches — first clean pass of the multi-PR session.** Live verified on dev DB. **Memory: `project_pra9_shipped.md`.**
9. **PRA-10** ✅ **SHIPPED 2026-05-24** (`e9c1e81`/#320, ~10 days ahead of schedule) — TaxRateMaster effective-dated rate matrix. 1 new table + 8 system-template rates: US-CA-SALES 7.25% (eff 2017) / US-NY-SALES 4% (eff 2005) / CA-GST 5% (eff 2008) / CA-ON-HST 13% (eff 2010) / EU-VAT-DE 19% standard + 7% reduced (eff 2007) / EU-VAT-FR 20% (eff 2014) / EU-VAT-GB 20% (eff 2011). All wire to PRA-6's TaxCodeMaster + TaxAuthority via subselect. 7-value JurisdictionLevel + 10-value RateType enums. Threshold floor + cap support (luxury / FICA wage base). IsCompounded flag (Quebec QST). AvaTax/Vertex-style effective-dated resolution. **ZERO Codex catches — 2nd consecutive clean pass.** **Memory: `project_pra10_shipped.md`.**
10. **PRA-11** — Pack hierarchy (Each/Inner/Case/Pallet). ~400 LOC. **Wave 3.4 — Mon Jun 8 AM, post-EVS.**

**Net runway impact: MES event cascade (PR #5e/5f/5g) slips ~10 days; ABS Thu + EVS Jun 3 demos untouched.**

---

**Priority 1.666 — Dean's 2026-05-24 + 2026-05-26 brainstorm — 6 future-wave themes** ⭐ NEW 2026-05-24, ⭐ Theme B6 added 2026-05-26 PM session 5 (post-Republish of CFO+COO motions to prod). Dean's brainstorm: *"I dont want to interrupt our flow but just in case we need some of this stuff sooner rather than later."* Full capture: `memory/reference_dean_brainstorm_2026_05_24.md`. **None of these are Master Files Baseline scope — they slot into later sprints.** Locked here so they don't get lost.

| # | Theme | What's in place | Net-new | Slot |
|---|---|---|---|---|
| **B1** | **MTO/ETO from Sales Order LINES / RELEASES (not headers)** — Dean: *"PO will need to come from sales order lines/releases, not Sales Order header level."* | `CustomerProject` (PR #2) + `ProductionOrder` (PR #1) with `CustomerProjectId` FK | `SalesOrder` + `SalesOrderLine` + `SalesOrderRelease` entities + `ProductionOrder.SalesOrderLineId` FK; CustomerProject re-semanticized as program-level rollup. **ADR-027.** | **Sprint 19+ (Revenue side)** — ADR landed early (target ADR-027 alongside PRA-9). |
| **B2** ⭐ ABSORBED BY B8 PR-PO-3 (2026-05-26 PM session 7) | **Job Splitting from any operation** — split qty + costs (labor, material, OH, subcontract) + traceability (heats / lots / serials / certs) into a NEW job number. Dean: *"truly splits quantities and costs including labor and material move into the split job number including everything that needs traceability."* | Nothing structural. LotMaster.ParentLotId (PRA-7) supports traceability fork. | `JobSplit` entity (source PO/operation, child PO, % allocation, splitter Employee, reason); allocation rules for labor/material/OH/subcontract; traceability fork rules; operation-continuity (child starts at split op, not op 010). **ADR-028.** | **Sprint 14 or 17** — AFTER MES events (#5e/5f/5g) ship so cost-event substrate exists. **HEAVY research** (SAP split-valuation, Oracle Job Splits, Plex lot splits, Epicor WO split, ~3-5 days). |
| **B3** ⭐ ABSORBED BY B8 PR-PO-4 (2026-05-26 PM session 7) | **Mixed Purchase-to-Stock + Purchase-to-Order + Aggregated PO + Auto-Issue + Direct-Ship-to-COGS.** Dean: *"Mix of purchase to stock... and purchase to specific orders with the ability to place a purchase order for multiple make to order jobs on one po... Once received I want it to be inspected... once passes inspection I want it to be automatically issued to the job... Once an order is completed, I want the ability to ship it right from the Job/Sales order so all costs are immediately transferred to COGS when it ships."* **GAAP question:** **YES, both ship-from-job-direct-to-COGS AND WIP→FG→Ship are ASC 330 compliant.** Direct-to-COGS = "backflush to COGS" pattern, standard in aerospace + MTO/ETO. WIP→FG→Ship = standard in make-to-stock. | `PurchaseOrder` + `PurchaseOrderLine`; PO has `WorkOrderId` + `CipProjectId`; PostingProfile matrix (PRA-7); Production GL accounts (PRA-5a) | `PurchaseSource` enum on PO line (Stock/MTO/ETO/Replenishment/Project) + per-line `ProductionOrderId` + `SalesOrderLineId` FKs; `RequiresInspectionOnReceipt` flag + Quarantine-warehouse-on-receive flow (PRA-7 already has QUAR-DEFAULT template); auto-issue service; **2 new `InventoryTransactionType` values: `DirectShipFromJob = 17` + `WipToFg = 18`** + corresponding PostingProfile rows; cost-rollup service summing LaborEntry + MaterialConsumption + OH application + subcontract per PO. | **Sprint 13 (Full Purchasing CC)** which is Wave 5 — natural home. |
| **B4** | **ECR/ECO Control** — AS9100 §8.5.6 / IATF 16949 §8.5.6.1 / ISO 9001 §8.5.6 / ISO 13485 §7.3.9 / FDA 21 CFR 820.30(i) hard requirement. Dean: *"Best in class where this is concerned especially with all the QS/AS/ISO stuff we are following."* | `ItemRevision` exists (the END state) | `EngineeringChangeRequest` + `EngineeringChangeOrder` + state machine (Draft → InReview → Approved → InImplementation → Verified → Closed) + multi-stage `EcoApproval` (Engineering/Quality/Manufacturing/Purchasing/Customer) + `EcoAffectedItem` (N:M) + `EcoEffectivity` (date/lot/serial/job/WO) + `EcoImpactAnalysis` (auto-compute affected open jobs, POs, FG, RMAs) + `ItemRevision.SourceEcoId` link. **ADR-029.** | **Own sprint — Sprint 14.5 Engineering Change Control** (between S14 Maintenance + S15 Planning). **MEDIUM-HEAVY research** (Arena PLM, Aras Innovator, Siemens Teamcenter, Oracle Agile, SAP ECM, ~2-3 days). Pre-launch nice-to-have. |
| **B5a** ⭐ ABSORBED BY B8 PR-PO-2 (2026-05-26 PM session 7) | **BOM operation-level material assignment** — Dean: *"When we are building BOM's and Routings, I want the ability to assign Material at the operation level so when it is received we have visibility to where the material is needed in the routing steps."* | `MaterialStructureLine` has `Sequence` + `PhaseSequence` (positional only) | Add `ProductionOperationId int? nullable` FK to `MaterialStructureLine` — when set, material consumes at that specific routing step. MRP + material staging + WMS pick rules pivot off this. **Light research** (~1 day — SAP PP component assignment, Oracle Cloud OPERATION_SEQUENCE, Plex). | **PRA-9 sibling or post-cascade cleanup PR** — small additive nullable column. |
| **B5b** ⭐ ABSORBED BY B8 PR-PO-4 (2026-05-26 PM session 7) | **Subcontract operation auto-completes on PO receipt** — Dean: *"I want the app to treat subcontract like another operation in the routing and when received, it fills in Qty complete in the routing at the Sub Contract Operation level just like it came from a previous operation."* | `WorkCenterType.Subcontract = 3` + ProductionOperation outside-op flags (ADR-013) + `PurchaseOrder.WorkOrderId` FK | Service layer: when StockReceipt lands against a PO line tied to a Subcontract-type WC's ProductionOperation, auto-update `ProductionOperation.QuantityCompleted += receipt.Quantity` + emit chain-of-custody edge (same as internal operator complete). | **PR #5e (MES Events)** — natural home as part of the inventory event services. |
| **B6 GO BIG ⭐ DEAN DIRECTIVE 2026-05-26 PM** | **Full BIC + smarter-than-BIC on B6 cascade — NOT Minimal.** Dean: *"GO BIG."* After audit (`docs/research/item-master-b6-audit-synthesis-2026-05-26.md`) presented Minimal vs BIC trade-off. Locked BIC: ~190-280h code + 10-15d research over 5 sprints (Foundation + 14.1-14.5). Anti-pattern memory: `feedback_b6_go_big_2026_05_26.md`. Scope: see B6 row below for full detail; this row locks the discipline. |
| **B8 ⭐ NEW 2026-05-26 PM session 7 — UMBRELLA THEME** | **Production Order Cockpit (executable BOM + Routing grid)** — Dean's upload *"Production Orders Fields and Functions"* documents the full tier-1-beating spec. **Bottom-line locked principle:** *"Every BOM line and every routing operation should show planned vs actual, status, exception, cost impact, traceability, and the next allowed transaction."* Spec runs ~270 fields across header / BOM line / operation / quality / cost; ~70 actions; 6 enforced job-to-job transfer rules; 16 non-negotiable validations; 17 auto-generated KPIs; 3 operating modes (Planner/Supervisor/Operator); single-transaction-drawer UX. **THIS THEME SUBSUMES B2 / B3 / B5a / B5b** — they become sub-PRs under B8's umbrella. B1 (SO-line MTO/ETO) feeds B8 PR-PO-1; B7 (PO-as-Standard, Make-or-Buy) sits ON TOP of B8 (Sprint 14.6+). Source: `docs/research/po-cockpit-spec-2026-05-26.md`. Memory: `reference_po_cockpit_spec_2026_05_26.md`. | Today: ProductionOrder header (~20 of 40 spec fields) + per-PO BOM snapshot (`MaterialStructure` + `MaterialStructureLine`, ~12 of 80 spec fields) + per-PO Routing snapshot (`ProductionOperation`, ~20 of 50 spec fields) + LaborEntry clock-in/out (Sprint 13.5 PR #5d) + cost cols on header (PR #349). NO material transaction table, NO operation transaction table, NO WipMove, NO Complete/Scrap/Rework modals, NO transaction drawer, NO "Can I run this?" indicator, NO job-to-job material transfer, ~14 of 16 spec validations missing, 17 spec KPIs missing. | **12-PR cascade ~172-240h:** **PR-PO-1** header expansion (Type variants enum 8, full Status state machine, Priority, Planner/Supervisor, Released Qty, HoldReason enum, 4 Freeze Flags, PromiseDate, LotSerialReq, WorkInstr/Drawing rev, 8-12h). **PR-PO-2** BOM line expansion (10 qty cols Issued/Picked/Staged/Reserved/Consumed/Returned/Scrapped/Short/Over-Issued/Transferable + 17-state status enum + flags + Supply Type + Lot/Serial + alternates + per-line accounts; **absorbs B5a**, 16-20h). **PR-PO-3** ProductionMaterialTransaction entity + service (Issue/Return/Transfer-to-Job/Substitute/Scrap with 6 enforced transfer rules; **absorbs B2**, 20-30h). **PR-PO-4** ProductionOperationTransaction entity + state machine service (Start/Pause/Resume/Stop/LogTime/Complete/Partial/Reverse/Skip/InsertReworkOp/ChangeResource; **absorbs B3 + B5b**, 20-30h). **PR-PO-5** ProductionWipMove entity + service (6-8h). **PR-PO-6** Complete/Scrap/Rework modals with preview-before-post (16-20h). **PR-PO-7** "Can I run this operation?" 8-readiness-check indicator (Materials/PriorOp/Resource/Labor/Quality/Documents/Tooling/**Maintenance-Clear via EAM integration** — **HIGHEST-LEVERAGE smarter-than-BIC differentiator**, 12-16h). **PR-PO-8** `/Production/Orders/{id}/Cockpit` 12-tab surface, BOM grid 24 cols + Routing grid 22 cols + 16-metric top bar, composes shared Cockpit primitives (20-30h). **PR-PO-9** Transaction drawer UI pattern reusable across grids (12-16h). **PR-PO-10** 3-mode UI gating Planner/Supervisor/Operator (6-8h). **PR-PO-11** 14 validation services on the transaction pipeline (16-20h). **PR-PO-12** IProductionReportingService + 17 dashboard tiles (20-30h). **ADRs:** ADR-034 (Production Order Cockpit data model), ADR-035 (job-to-job transfer 6 rules), ADR-036 ("Can I run this?" cross-module integration contract). | **Sprint 14.x — after B6 Foundation Sprint (currently 5/7), Sprint 14.1 snapshot, 14.2 DMS+Drawing, 14.3 Arena PLM ECR/ECO.** Then Sprint 14.4 cost engine consumes B8's transaction tables. **6-8 weeks total.** Research targets in `docs/research/po-cockpit-spec-2026-05-26.md` §1-12 cite SAP S/4HANA · Oracle Cloud SCM · D365 SCM · Infor CSI · NetSuite · Epicor as the comparison baselines that B8 must beat. |
| **B7 ⭐ NEW 2026-05-26 PM session 6 — RESEARCH-BEFORE-BUILD** | **PO-as-Standard (optional Item Master) + Make-or-Buy duality** — Dean post-PR #357 brainstorm: *"In ETO/MTO, you don't have to have an Item Master with a Standard BOM/Routing. You can do it all from the Production Order and when the Order ships, crystallize an Item Master from the Actual BOM/Routing/Costs. All supply (PO/child production orders/subcontract) driven solely by PO BOM/Routing — unless it's make-to-stock, buy-to-stock, or has special planning parameters. Also a part can be BOTH make AND buy — made on a PO internally OR procured externally based on capacity/cost/lead-time."* **THIS IS THE GENERALIZATION OF B6.** B6 assumes Item Master is the Standard; B7 makes the Item Master OPTIONAL for ETO and CRYSTALLIZED at ship. Sits ON TOP of B6 (Sprint 14.6+ or later). | Today: B6 cascade assumes Item Master exists at PO release (PRA-7 PostingProfile + PRA-5b AccountingKey + PR-FS-1 ItemGroupId all key off Item Master rows). Today's `Item.Source` enum has `Internal / ExternalERP / Synced` — no "doesn't exist yet" path. | Net-new: `Item.SourcePattern: enum { StandardFirst, PoFirst, Hybrid }` + service to crystallize Item Master from `ProductionOrder` actuals at ship. `Item.MakeBuyCode: enum { Make, Buy, MakeOrBuy, Phantom }` (extends PR-FS-7's MakeBuyCode with the new MakeOrBuy value) + `IMakeBuyDecisionService.DecideAsync(itemId, qty, dueDate) → { MakeInternal, BuyExternal, ChainOfFactors }`. Cherry voice integration: *"Cherry, why are we buying the bracket?"* → *"WC-MILL 87% loaded, vendor quoted $1,840 vs internal $2,210. Buy was cheaper AND faster."* **ADRs:** ADR-032 (PO-as-Standard pattern), ADR-033 (Make-or-Buy decision service). | **Sprint 14.6+ or later. Research first (~5-10 days).** Research targets: SAP PP no-material-number flow · Oracle ETO item lifecycle · Plex 1-off PO + auto-Item-create-on-ship · Aras Innovator / Siemens Teamcenter as-designed→as-built→as-shipped · DBR/OPT/TOC Make-or-Buy algorithms · AS9100 §8.5.6 + DFARS 252.225-7012 ETO compliance · Cost-engine implications (Standard locked at PO release vs ship) · MRP implications when demand has no Item Master. **Memory: `reference_dean_brainstorm_b7_2026_05_26.md`.** |
| **B6** ⭐ NEW 2026-05-26 PM session 5 | **Item Master as Standard, ProductionOrder as Actual (snapshot + variance + ECR/ECO + Drawings)** — Dean: *"The Production Order BOM and Routing drive everything in MES — but the Item Master holds the engineering Standard: rev control, ECR/ECO, drawings, documents, full lineage. The Item Master BOM + Routing = STANDARD. ProductionOrder BOM + Routing = ACTUAL — how it was actually built, which always strays in ETO."* This is the well-established tier-1 ERP pattern (SAP/Oracle/Plex/Epicor all do it). Ties together themes B2/B4/B5a/B5b. | `Item` + `ItemRevision` (revision chain, supersedes, approval gates) + `MaterialStructure` (rev chain via `MasterStructureId`, `IsControlled`, `ApprovedAt`) + `Routing` (RevisionNumber, EffectiveFrom/To, ApprovedBy) + **`ProductionOperation` correctly snapshots `RoutingOperation` at release** (Models/Production/ProductionOperation.cs:65-110) + `Attachment` (linkable to Asset/WO/CIP) + `FaiWorkflow` (PR #338 already does Standard-vs-Actual at dimension level for FAI) | **5-ship cascade (PR-IM-1 through PR-IM-5):** **PR-IM-1** `SourceItemRevisionId` FK on ProductionOrder + auto-stamp at create (3-4h). **PR-IM-2** `ProductionMaterialStructure` + `ProductionMaterialStructureLine` per-PO BOM snapshot tables (mirror of ProductionOperation snapshot of Routing) + IMaterialStructureSnapshotService + substitution audit (8-12h + 1-2d research). **PR-IM-3** ECR + ECO entities + workflow (Draft→InReview→Approved→InImplementation→Verified→Closed) + multi-stage approval + EcoEffectivity by date/lot/serial/job/WO + impact-analysis service + `/Engineering/Changes` Control Center (30-40h + 2-3d research). **PR-IM-4** Attachment → Item/ItemRevision/MaterialStructure/Routing FKs + `Drawing` entity (or `ItemRevisionDocument`) with customer-rev sync (12-16h). **PR-IM-5** Standard-vs-Actual variance service + `/Production/Variance` reporting + chain-trace voice intent extension (16-24h). **ADRs:** ADR-029 (Item Master as Standard, PO as Actual), ADR-030 (ECR/ECO Workflow), ADR-031 (Drawing + Document Repository linked to ItemRevision). **Total cascade: ~70-100h + ~5-8d research.** Memo: `docs/research/item-master-vs-production-order-snapshot-2026-05-26.md`. | **Sprint 14 / 14.5 / 15 territory** — PR-IM-1+2 are foundation prereqs for B4 (the original ECR/ECO theme) to roll into Sprint C cleanly. **Ordering:** A → B → D → C → E (freeze, snapshot, drawings, ECR/ECO workflow, variance reporting). |

**Open questions for Dean:**
1. **B1 timing** — wait until Sprint 19, or write ADR-027 alongside PRA-9 PriceList (which is the closest revenue-side ship) so the SO Line shape is locked before any code touches the demand side?
2. **B4 sprint slot** — own Sprint 14.5 (between Maintenance + Planning), or fold into Sprint 14 (Maintenance + ECR/ECO bundle)? **2026-05-26 PM update: B6 now subsumes B4 — the ECR/ECO ship (B4) becomes PR-IM-3 inside the B6 cascade. Sprint slot question becomes "where in Sprint 14/14.5/15 does the full B6 cascade live?"**
3. **B5a timing** — PRA-9 sibling (one column add) or post-cascade cleanup PR? **2026-05-26 PM update: B5a naturally lives inside PR-IM-2 (the per-PO BOM snapshot has the op linkage built in). No longer needs to be a sibling — it's a free deliverable inside B6's PR-IM-2.**
4. **B6 ordering vs Thursday demo / ABS deal closure** — B6 is HUGE (5 PRs, 70-100h + 5-8d research). Does the ABS demo win on what we have now (CFO + COO motions + Cherry voice), or does B6 need to start mid-sprint to make the AS9100 + ETO + audit-readiness story explicit?
5. **B6 sequencing within the cascade** — A → B → D → C → E (freeze → snapshot → drawings → ECR/ECO → variance) is recommended. Alternative: A → B → C → D → E (freeze → snapshot → ECR/ECO before drawings). The recommended ordering puts drawing repo before ECR/ECO so ECRs can attach drawing markup at request time.

**No code changes for B1-B5 in the current cascade.** PRA-9/10/11/5b ship first; MES events (#5e/5f/5g) resume; THEN these slot in per the table above.

---

**Priority 1.667 — MES Event Layer cascade (Waves 3.5-3.7)** ⭐ NEW 2026-05-24 (per `project_mes_gap_analysis.md` — the event layer real MES needs, resumes after Master Files Baseline closes). Built on PRA-7 (Lot/SerialMaster) + ADR-022 (chain-of-custody graph). **3 PRs over ~3 days:**

1. **PR #5e** — Event tables: DowntimeEvent + ScrapEvent + ReworkEvent + MaterialConsumption + services. Replaces current "write to ProductionOperation.ScrappedQty/ReworkQty + reason-in-notes" path with proper event rows linked to LaborEntry. **Wave 3.5 — Mon Jun 8 – Tue Jun 9.**
2. **PR #5f** — LotGenealogy + SerialGenealogy (AS9100, FDA, FSMA hard requirement). Modeled on chain-of-custody graph ADR-022. Needs PRA-7's Lot + SerialMaster tables. **Wave 3.6 — Tue Jun 9 – Wed Jun 10.**
3. **PR #5g** — OeeEvent rollup + OEE / Throughput / Down-Machines KPI tiles on Production Control Center. Kills the rolling-scalar pattern on Asset.CurrentOEE/Availability/Performance/Quality with proper event-sourced rollup. **Wave 3.7 — Wed Jun 10.**

---

**Priority 1.7a — Sprint 12.7 Controller Cockpit (Wave 4) — THE CFO MOTION** ⭐ NEW 2026-05-24 audit (was inside Priority 1.61 Absorption Item 3, no dedicated slot). Net-new Fortune-100-CFO pitch motion. **PULLED FORWARD 2026-05-25/26 — ~16 days ahead of original Jun 11-17 slot.** ✅ **Dean's call A2: YES — pull forward, do it now.** Live demo target: `/Controller?tab=drilldown` with the question "why is NBV $1.2M on Asset #4231?" → AI walks Asset → CipCapitalization → CipProject → CipCosts + recent Depreciation JEs → JournalLines, narrated naturally via Cherry Bar TTS, every step a clickable drill link. **5 PRs total — 3 SHIPPED, 1 staged, 1 queued:**

1. ✅ **PR #345 `5176a1b` SHIPPED 2026-05-25** — `/Controller` route + Controller Cockpit shell. Composes all 4 Cockpit primitives per Lock 3: `_CockpitPageHeader` + `_CockpitKpiBand` + `_CockpitTabShell` + `_CockpitShell`. 4 tabs: **Books · Drilldown · Close Prep · Audit Trail** with URL `?tab=` routing. NavRegistry entry at TOP of Finance group, `fa-user-tie` icon. Pure shell PR — no DbContext mutation, ILogger only. Lock 16 E2E PASSED. Dean caught Lock 14 miss on first Publish (stale dev workspace) — fixed via Shell pull + Agent restart-only + re-Republish.

2. ✅ **PR #346 `614a738` SHIPPED 2026-05-26** — `IControllerCockpitService` + `ChainTraceService`. Walks Asset → CipCapitalization → CipProject → CipCosts (top 10) and JE → reverse-CIP-origin → JournalLines (top 6 per JE). URL-driven via `?q=ASSET-N` or `?q=JE-N`. Search form in Drilldown tab right pane (always visible). **Codex P1 catch + fix:** depreciation walk requires per-asset GL override (not shared with other tenant assets) — otherwise surfaces "DEPRECIATION CHAIN — not disambiguated" narration step explaining future SourceModule/SourceDocumentId migration. Live-verified on prod with HAAS VF-2SS Asset #1 (NBV $60,152.86 math rendered + Codex fix narration shown). EF InMemory line-nav gotcha documented (use Testcontainers+Npgsql when cross-table line queries need real test coverage).

3. ✅ **PR #347 `aad2590` SHIPPED 2026-05-26** — Voice intent `IntentKind.ExplainChainTrace` on the Cherry Bar. Push-to-talk "*why is NBV on asset 4231*" / "*drill down on JE 47*" / "*trace asset 1116*" → `HybridIntentRouter` keyword classifies → `IControllerCockpitService.TraceAsync` → handler maps `ChainStep.Narration[]` → Spoken stream that Cherry's TTS reads aloud + Displayed payload mirrors on-page Drilldown + "Open in Drilldown" ActionLink. `ExtractControllerEntityRef` regex normalises "journal entry"→"je", "purchase order"→"po", "work order"→"wo". 6 vector-fallback prototypes added (auto-seeded by `IntentEmbeddingsBootstrap`). All 5 CI gates green, Codex window clean, 32 unit tests pass (incl. 5 new keyword + collision-safety tests). **Lock 17 FIRST EXERCISE** — code-only PR, no Republish, dev preview = E2E target.

4. 🟡 **PR #4 KPI band wire-up via `IFinanceKpiService` — CODE COMPLETE, BLOCKED ON ICLOUD** as of 2026-05-26 morning. New `IFinanceKpiService` reading 4 tiles from live data: **Cash position** (sum of Debit − Credit across GlAccounts where `Category = CashAndReceivables`, tenant-scoped via `JournalEntry.Book.CompanyId`) · **AP due this week** (sum of `Total − AmountPaid` on VendorInvoices where Status ∈ {Approved, PartiallyPaid} and `DueDate ≤ today + 7d`, tone escalates by $) · **Open POs** (count + total of PurchaseOrders where Status ∈ {Approved, Sent, PartiallyReceived}) · **WIP balance** (sum of `CipProject.TotalCosts` where Status = Active, tenant-scoped). 5 files / ~344 LoC. DI-registered at `Program.cs:188-190`. 10 unit tests covering empty DB, threshold tones, status filters, tenant scoping, FormatMoneyCompact boundaries. Will ship the minute iCloud Drive download settles enough to unblock the local git tree walk. **AR aging + unrealized FX gains DEFERRED** — need `CustomerInvoice` entity + FX revaluation engine not in IndustryOS yet (honest scope per Codex P1 pattern from PR #346).

5. ⏳ **PR #5 QUEUED** — Demo data + walkthrough page + Republish-with-Copy. Includes `/Controller/Walkthrough` page that scripts the CFO motion: land → see KPIs → click Drilldown → ASSET-1 → Cherry narrates → "Open in Drilldown" link → visual chain renders. Plus the Republish-with-Copy step that copies dev demo data to prod for the live ABS demo URL. Also folds in: TimescaleDB removal migration (cleanup-pass for the Lock 12 violation at `Migrations/20260516_AddTelemetrySubstrate.cs:61`).

> **2026-05-26 PM update:** Sprint 12.7 PR #5 effectively shipped through PRs #348 (KPI band) + #351/#352/#353 (CfoMotionDemoSeeder + customer-name revert hotfix) + #354 (CooMotionDemoSeeder). Republish-with-Copy ran 2026-05-26 PM (`project_republish_prod_verify_2026_05_26.md`). **Prod industryos.app verified live with CFO motion + COO motion + FAI UI.** ABS Thursday demo runway is LIVE.

---

**Priority 1.668 — B6 Foundation Sprint (Wave 4.5) ✅ CLOSED 2026-05-26 PM** ⭐ NEW 2026-05-27 audit. The GO BIG cascade kickoff. **10 ships in one session day**: PR #355 (Item.ItemGroupId wire-up) → #356 (151-Item backfill) → #357 (source-aware ItemGroupResolver hotfix) → #358 (ItemSite per-Site override) → #359 (ItemStandardCostElement = SAP Cost Component Split) → #360 (CostLayer = FIFO/LIFO/Average inventory valuation) → #361 (ItemSourcingRule = SAP S/4 Source List + AS9100 §8.4.1) → #362 (CustomerItemXref = SAP CMIR bidirectional customer-PN ↔ Item) → #363 (18-column Item Master expansion + IsSellable resolver tightening). Item Master fully classifiable + multi-plant capable + 8-element cost split + FIFO/LIFO/Average valuation + customer xref + sourcing rules + 18-col expansion. Lock 16 E2E verified on all 10 ships. Codex P1/P2 catches on PR #358/#360/#361/#362/#363 all resolved in-PR. Closing ship `eaff0dc` PR #363 (2026-05-26 PM).

**Cascade unlocked:** Sprint 14.1 (per-PO snapshot) → 14.2 (DMS) → 14.3 (ECR/ECO Change Control) → B8 PO Cockpit (12 PRs) → 14.4 (cost engine) → 14.5 (unified Item view).

Memory: `project_pr363_shipped.md` (closing ship); ship memos PR #355→#363 at `project_pr355_shipped.md`...`project_pr363_shipped.md`. Design: `docs/research/b6-foundation-sprint-design-2026-05-26.md`. HARD LOCK: `feedback_b6_go_big_2026_05_26.md`.

---

**Priority 1.669 — Sprint 14.1 / 14.2 / 14.3 PR-1 substrate (Waves 4.6/4.7/4.8) ✅ CLOSED 2026-05-26 evening → 2026-05-27 AM** ⭐ NEW 2026-05-27 audit. Three consecutive substrate ships at the post-B6 cadence, each with its own admin probe + Lock 16 corollary write-button enforcement:

1. ✅ **Sprint 14.1 PR-1** — PR #364 merge `e79da2d` + PR #365 xmin hotfix `e7dd547`. ProductionMaterialStructure entity (21 frozen fields + tenant trio + 5 FKs + fingerprint hash) + new `BomIssueMethod` enum (Pull/Push/Backflush, default Pull) + 4 frozen columns on ProductionOrder + IPoSnapshotService (Capture idempotent / Get / Clear) + `/Admin/PoSnapshotProbe` + typed migration + 16 realistic-mfg tests (Trent bracket assembly + SKF bearing + Ryerson steel + Grainger fastener + phantom mount). **First admin probe in the B6/14.x cascade to exercise an actual INSERT** — surfaced the latent `IsRowVersion()`+`bytea NOT NULL` bug that exists across CustomerItemXref / ItemSourcingRule / CostLayer. **xmin HARD LOCK encoded** here (`feedback_xmin_pattern_for_concurrency_lock.md`). Lock 16 E2E live: PRO 6 (DEMO-COO-PRO-1005) → Capture renders 3 frozen lines (SEL-TC-30/35/40X seal kits) with verified FrozenExtendedCost math ($4.25/$11.22/$20.655) + 3 unique SHA-256 fingerprints + idempotency (re-click returns same timestamp, no duplicate rows). Memory: `project_pr364_pr365_shipped.md`.

2. ✅ **Sprint 14.2 PR-1 — DMS substrate** — PR #366 merge `8350a0f` + fixup `645ec5c` (3 Codex P1s in-PR — case normalization on DocumentNumber + RevisionCode + cross-tenant validation on LinkToItem; all 3 reviewThreads resolved). Document + DocumentVersion + ItemDocumentLink entities + 3 new enums (DocumentType 8 / DocumentStatus 6 / ItemDocumentLinkPurpose 6) + IDocumentService 8 ops (Create / AddVersion auto-increment / Approve / Release **atomic-supersede** / Get / Link / Unlink / GetDocumentsForItem) + **5-write-button** `/Admin/DocumentProbe` + 15 realistic-mfg tests (Trent bracket drawing + Boeing BAMS-3320 spec + AS9102 FAI proc + CertOfConformance + AMS 6520 heat-cert). **xmin pattern applied prophylactically from day one — zero hotfix needed.** **First probe to exercise 5 distinct write paths** — Codex P1 cross-tenant fix verified LIVE in E2E. Memory: `project_pr366_shipped.md`.

3. ✅ **Sprint 14.3 PR-1 — ECR/ECO Change Control substrate** — PR #367 merge `813d399` + fixup `97e3008` (3 Codex P1+P1+P2 in-PR — atomic ECO release transaction + DocumentVersion tenant validation + LinkedCustomer tenant check; all 3 reviewThreads resolved). EngineeringChangeRequest + EngineeringChangeOrder + EcoLineItem + EcoApproval entities + 7 new enums (ChangeReason 9 / ChangeUrgency 4 / EcrStatus 6 / EcoStatus 7 / EcoEffectivityType 6 / EcoApprovalStatus 5 / EcoLineItemDisposition 7) + IEcrEcoService 10 ops (Create / Submit / ApproveEcr atomic-ECO-creation / Reject / AddLine / AddStage / ApproveStage in-order-enforced / Release atomic-tx with DocumentVersion supersede / Implement / Close) + **8-write-button** `/Admin/EcrEcoProbe` + 16 realistic-mfg tests + sprint-naming-no-vendor-implication HARD LOCK encoded (renamed from "Arena PLM ECR/ECO" → "ECR/ECO Change Control"; `reference_sprint_naming_no_vendor_implication.md`). **Third consecutive ship with zero hotfix needed** — xmin pattern + enum defaults + write-button probe all bulletproof. **Lock 16 E2E full 13-op cycle**: Create → Submit → ApproveEcr (F/F/F → RequiresFAI=True, AffectsCust → RequiresCustNotice=True inherited live) → AddLine → AddStage×2 → OutOfOrderRefused → ApproveStage×2 (auto-flip ECO Approved on all-green) → Release (atomic tx) → Implement → Close → GetEco all green. **Current main HEAD.** Memory: `project_pr367_shipped.md`.

**Lock 16 corollary HARD LOCK encoded** off this trio (`feedback_lock16_corollary_probes_exercise_writes.md`): every admin probe MUST include INSERT/UPDATE buttons so the write path is exercised in E2E before merge. PR #366 (5 writes) caught a cross-tenant gap no unit test had. PR #367 (8 writes) proved full 13-op multi-step cycle live.

**Cascade unlocked:** PR-XminBackfill (defensive) → B8 PO Cockpit cascade (12 PRs) alternating with Sprint 14.3 PR-2..7 → Sprint 14.4 cost engine → Sprint 14.5 unified Item view.

---

**Priority 1.670 — PR-XminBackfill (Wave 4.85) ⏳ DEFENSIVE NEXT-UP** ⭐ NEW 2026-05-27 audit. ~1.5h. Convert 3 latent entities to xmin pattern before any cockpit-side INSERT path lights them up. **Bundled — identical fix 3×, cheaper as a single ship.**

**Concrete changes:**

| Line in AppDbContext.cs | Entity | Current | Required fix |
|---|---|---|---|
| 2701 | `CustomerItemXref` (PR #362) | `e.Property(x => x.RowVersion).IsRowVersion();` | `e.MapXminRowVersion(x => x.RowVersion);` |
| 2754 | `ItemSourcingRule` (PR #361) | same | same |
| 2816 | `CostLayer` (PR #360) | same | same |

**Acceptance criteria:**
- 1 typed migration that does `migrationBuilder.DropColumn("RowVersion", ...)` × 3 (pattern proven by `20260526235021_DropPoSnapshotBytesRowVersionFs141Pr1P1.cs`)
- 3× `byte[] RowVersion` → `byte[]? RowVersion` on entity properties
- Designer + Snapshot regen
- **Lock 16 corollary applied:** add Insert button to `/Admin/CostLayerProbe` + `/Admin/ItemSourcingProbe` + `/Admin/CustomerItemXrefProbe` so the INSERT path is exercised on dev preview before merge
- Lock 17: code+migration ship, dev preview verify only, no Republish-with-Copy

**Why now (not per-touch fix):** any next ship that calls `CostLayerService.RecordReceiptAsync`, `ItemSourcingRuleService.AddRuleAsync`, or `CustomerItemXrefService.AddXrefAsync` detonates the bug. Better to clear pre-emptively than chase a 23502 mid-cascade. **Dean's call A1: confirm.**

---

**Priority 1.671 — Theme B8 PO Cockpit cascade (Wave 4.9) ✅ 11/12 SHIPPED (PRO-12 DEFERRED)** Updated 2026-05-28 session 15. **12 PRs spec'd / 11 shipped / PRO-12 deferred (Dean: another app bolts on for reporting).** Subsumes prior brainstorm themes **B2 / B3 / B5a / B5b** as sub-PRs.

**Bottom-line principle (LOCKED):** *Every BOM line and every routing operation should show planned vs actual, status, exception, cost impact, traceability, and the next allowed transaction.*

**12-PR sequence — SHIPPED STATUS (updated 2026-05-28):**

| PR | Status | GitHub PR | Commit | What shipped |
|---|---|---|---|---|
| **PR-PRO-1** | ✅ SHIPPED | PR #364+#365 | `e79da2d`+`e7dd547` | PO header expansion + IPoSnapshotService + per-PO frozen snapshot |
| **PR-PRO-2** | ✅ SHIPPED | PR #383 (part) | `30985c6` | BOM line 10-qty + 17-state status + 9 control flags + supply link |
| **PR-PRO-3** | ✅ SHIPPED | PR #377 | `32c1882` | 12-action material transaction service. Absorbs B2 |
| **PR-PRO-4** | ✅ SHIPPED | PR #379 | `e527640` | 19-action operation transaction state machine. Absorbs B3+B5b |
| **PR-PRO-5** | ✅ SHIPPED | PR #381 | `bdc3ba7` | WIP move + auto-advance on completion + quality hold gating |
| **PR-PRO-6** | ✅ SHIPPED | PR #382 | `a84c6f2` | Complete/Scrap/Rework atomic posting. 5-dimensional scrap analysis |
| **PR-PRO-7** | ✅ SHIPPED | PR #383 | `30985c6` | Material Supply Link + "Can I Run This?" 8-check readiness |
| **PR-PRO-8** | ✅ SHIPPED | PR #384 | `3c6315b` | PRO Cockpit Control Center surface. 12 tabs, 16 metrics, 24+22-col grids |
| **PR-PRO-9** | ✅ SHIPPED | PR #386+#387 | `599d00c`+`856fd66` | Transaction Drawer UI — THE BIC differentiator. Right-side panel, JSON hydration, 14 BOM + 8 Op status→action maps |
| **PR-PRO-10** | ✅ SHIPPED | PR #388 | `7bfce2c` | 3-mode UI gating (Planner/Supervisor/Operator). Auto-resolve from role. Escalation prevention |
| **PR-PRO-11** | ✅ SHIPPED | PR #389 | `e41917c` | 14 validation services + TransactionValidationPipeline. Chain-of-responsibility + fail-open + supervisor override. 864 lines |
| **PR-PRO-12** | ⏸️ DEFERRED | — | — | IProductionReportingService + 17 dashboard tiles. **Dean: another app bolts on for reporting later.** |

**Also shipped in B8 orbit:** PR #385 (`9c064ac`) — Interim PRO Cockpit demo seeder, 3 scenarios, 12 entity layers, ~150 rows. Admin trigger at `/Admin/SeedCockpitDemo`.

**Inspection note (2026-05-28):** ABS uses HighQA for inspection. Post-release: add inspection operation type for first article. HighQA integration deferred.

Source: `docs/research/po-cockpit-spec-2026-05-26.md` (full 12-section spec, verbatim from Dean's "Production Orders Fields and Functions" upload). Memory: `reference_po_cockpit_spec_2026_05_26.md`.

---

**Priority 1.672 — Sprint 14.3 PR-2..7 (Wave 4.9 parallel) ⏳ QUEUED** ⭐ NEW 2026-05-27 audit. Substrate PR-1 (PR #367) closed; remaining ~50-70h finishes the engineering-change lifecycle with variant change types + customer/supplier outbound + corrective action + closed-loop FAI re-trigger. Ships alternately with B8 cascade (different model surfaces, low merge risk).

1. **PR-2 — Deviation** (~6-10h) — short-term divergence from approved spec (e.g., one-time use of an alternate material lot pending ECO).
2. **PR-3 — Waiver** (~4-8h) — longer-term customer-approved divergence.
3. **PR-4 — Concession** (~4-8h) — retroactive customer-approved acceptance of non-conforming material.
4. **PR-5 — Customer Notice + Supplier PCN** (~8-12h) — outbound notification pattern. ICustomerNotificationService + ISupplierNotificationService. Touches webhook outbox.
5. **PR-6 — CAR + CAPA** (~10-14h) — Corrective Action Request + Corrective + Preventive Action entities. Closed-loop quality substrate.
6. **PR-7 — Impact analysis service + redline drawing markup tools + closed-loop FAI re-trigger on Engineering Change** (~16-22h). **CLOSES Sprint 14.3.** Cross-references B8 PR-PO-3/4 transaction tables.

---

**Priority 1.673 — Sprint 14.4 cost engine + Sprint 14.5 unified Item view (Wave 4.95) ⏳ QUEUED post-B8** ⭐ NEW 2026-05-27 audit.

- **Sprint 14.4 — cost engine (~30-50h)** — consumes PR-FS-3 (ItemStandardCostElement) + PR-FS-4 (CostLayer) data + B8 PR-PO-3 (ProductionMaterialTransaction) events. Standard-vs-Actual rollup with 8-element decomposition. Variance reasons by category.
- **Sprint 14.5 — unified Item view (~16-24h)** — the smarter-than-BIC customer-facing Item card. Bill of drawings via `IDocumentService.GetDocumentsForItemAsync`. Change history via `IEcrEcoService.GetEcoAsync`. Sourcing via `IItemSourcingRuleService`. Cost via `IItemStandardCostService` + `ICostLayerService`. Voice surface folded in (Sprint 13.5 PR #7 voice intents retired here per Dean's call A6).

---

**Priority 1.675 — Theme B10: Collaboration, Messaging & Microsoft Teams Integration (Wave 6+) ⏳ FUTURE PHASE** ⭐ NEW 2026-05-27 PM brainstorm. Contextual messaging from inside the app to coworkers, teams, or Microsoft Teams channels — linked directly to the business record (quote, project, job, PRO, PO, customer, NCR, WO, shipment, invoice). **Phase 1:** Teams connection with deep links back to the app (Microsoft Graph API, adaptive cards, deep-link URLs). **Phase 2:** Record activity feed (Communications/Activity tab on every record). **Phase 3:** Triggered notifications (late PO → buyer+PM, NCR → quality+production, PM overdue → maintenance supervisor). **Phase 4:** In-app messaging for non-Teams companies. Spec: `docs/research/teams-collaboration-spec-2026-05-27.md`. Memory: `reference_dean_brainstorm_teams_collab_2026_05_27.md`. Builds on the existing webhook outbox pattern (OutboxEvent → IWebhookDispatcherHostedService) with Teams as a new dispatch target. **Key differentiator from SAP/Oracle:** contextual messages with business record cards + deep links are built into the record from day one, not bolted on as a separate collaboration product.

---

**Priority 1.674 — Themes B7 + B9 (Wave 5+) ⏳ RESEARCH-BEFORE-BUILD post-B8** ⭐ NEW 2026-05-27 audit.

- **Theme B7 — PO-as-Standard + Make-or-Buy duality** — generalization of B6. (a) Optional Item Master in ETO — PO IS the standard; Item Master crystallizes from actuals AT SHIP. (b) Dual Make-or-Buy parts with decision service (capacity / cost / lead-time). Substrate already partially landed (BuyabilityScoreService + EffectiveProcurementService + PreferredVendorCatalogResolver dropped during PR-FS-5/6 builds — verify in B7 audit). Memory: `reference_dean_brainstorm_b7_2026_05_26.md`.
- **Theme B9 — Customer Project Manager Module** — SAP S/4 PS / Oracle Primavera P6 / MSFT Project competitor. Makes CustomerProject the **parent commercial+operational+financial container** spanning Sales / Contract / Engineering / Manufacturing / Procurement / Inventory / Scheduling / Labor / Finance / Quality / Service-EAM / Documents / Governance. Lifecycle starts at QUOTE, not job release. Greenfield ~100-150h. Spec: `docs/research/project-management-fields-and-functions-2026-05-26.md`. Memory: `reference_dean_brainstorm_b9_2026_05_26.md`.

Both research spikes run sequential post-B8 cascade close. B7 first (smaller, generalization of work already shipped).

---

**Priority 1.7 — Sprints 15-19 Control Center Family** ⚠️ **RENUMBERED 2026-05-27 audit** — "Sprint 14" label is now claimed by the B6/14.x post-foundation cascade (Priorities 1.668 → 1.673 above). Original Sprint 14 Maintenance CC renumbers to **Sprint 18**. Five remaining v1 Control Centers built on the Cockpit pattern, all built AFTER B8 PO Cockpit cascade closes: **Planning** (S15, ~4 PRs) · **Scheduling** (S16, ~4 PRs) · **Inventory** (S17 — folds in **Landed cost #130 lbl**, ~5 PRs) · **Maintenance** (S18 — folds in **Calibration #125 + OEE module #126 + RCM+FMEA #127 + Weibull #128**, ~7 PRs; ADR-019 drafted in code by B8 PR-PO-7) · **Shipping** (S19, ~4 PRs). **Quality / AP-AR / HR-Crew defer to v2 post-launch** per 2026-05-18 launch-scope lock. Each surfaces a "Linked Projects" tab built on the Sprint 14.5 unified Item view voice surface.

**Priority 2 — Sprint 3.5 followups (fold into Phase F where applicable):**
- Form labels on edit pages (PR #116d.23d) — biggest remaining a11y cluster, ~19 violations on ItemEdit alone
- CI a11y workflow hardening (PR #116d.23.1) — boot app + DB in CI for full sweep
- Sparse-page redesigns (PO Detail / AP Detail / AP List content additions)

**Priority 3 — Sprint 2 follow-ups** (no code yet, 5 initiatives):
- MFA TOTP (PR #123 in original numbering)
- SSO via SAML + OIDC (PR #124 in original numbering)
- Full DB-level Postgres RLS (PR #122 in original numbering — service-layer scoping IS in place)
- First-run Onboarding wizard (PR #119 in original numbering)
- Admin v2 UX completion (PR #118 in original numbering)

**Priority 4 — Sprint 3 premium modules** (13 unshipped initiatives, deferred until Phase F + Sprint 5 land):
Calibration (#125), OEE module (#126 — 6 tables), RCM+FMEA (#127), Weibull (#128), Vendor Scorecards (#129), Landed Cost (#130 lbl), Tax Matrix (#131 lbl), Blanket/Contract-PO (#132 lbl), Payment Batch+ACH+Positive Pay (#133 lbl), OCR Invoice (#134 lbl), ASC 842/IFRS 16 Lease (#135 lbl), Impairment ASC 360 (#136 lbl), i18n (#137 lbl).

**Priority 5 — Sprint 5 Voice AI Co-Pilot** (~20 PRs, signature feature, depends on Sprint 4 surfaces).

**Priority 6 — Sprint 7-9 Item Master + Multi-Dim Inventory** (18 PRs total, per `docs/research/item-master-and-multi-dim-inventory.md`):
- **Sprint 7 (5 PRs)** — Identification/Classification expansion, UOM normalization, Planning/MRP fields, Quality fields, Regulatory expansion (REACH/RoHS/ITAR/UDI/SDS/3TG)
- **Sprint 8 (7 PRs)** — OnHandQuantity fact table, Status dim, Serial+UDI parser, Owner dim, Quality dim, Project dim, Voice picker flow
- **Sprint 9 (6 PRs)** — 11-tab ItemEdit, DataCard groupings, Master-Data Completeness Score, Where-Used tab, Voice attribution, Mobile + compact-mode
- Each sprint's PRs are sequenced and migration-safe. Sprints 7-8 are schema-heavy; Sprint 9 is mostly Razor.

---

## 🧹 Tech debt + cleanup queue (post-audit 2026-05-18 / **re-organized 2026-05-24 audit — each item now has a slot**)

| ID | Item | Slot | Notes |
|---|---|---|---|
| TD-1 | 9 [Fact(Skip=...)] xUnit tests to rewrite (5 WorkOrderPartIssuance, 3 AssetConcurrency, 1 HistoricJournalBackfill) | **Wave 6 Workstream D** (Sprint 12.5) | — |
| TD-2 | EF model snapshot regen | ✅ **DONE PR #271 (commit `528f0a8`)** — was Priority 1.6075 | — |
| TD-3 | Playwright suite age-check | **Wave 6 Workstream D** | — |
| TD-4 | Branch cleanup (66 local + 136 remote) | **Run NOW — 1 hour, no PR** | `git remote prune origin && git branch --merged main | xargs git branch -d` |
| TD-5 | PR-number gap investigation (#3, #15, #81, #130) | **Wave 6 Workstream D** | Produce 1-page memo |
| TD-6 | Naming alignment (`WorkOrder` from `Models/AssetMaintenance.cs`, `ApprovalWorkflow` from `Models/SystemConfig.cs`, `VoiceContextEmitter` under `Filters/`) | **Opportunistic** | Slot in any future PR that touches those files |
| TD-7 | **NEW 2026-05-24** — ADR-025 trichotomy cleanup (3 sibling files at same number: service-layer-standard / posting-service-contract / roslyn-analyzer-design) | **Wave 6 Workstream D** | Per audit recommendation: keep all three as `ADR-025-*` siblings; document convention in `docs/ADR-INDEX.md`. Awaiting Dean's call A5. |
| TD-8 | **NEW 2026-05-24** — ADR-019 draft (WMS hierarchy + PostingProfile pattern; SAP S/4 + Dynamics 365 separation-of-concerns) | **Wave 2.3** (PRA-7 sibling) | ✅ **shipped 2026-05-24** as PRA-7 sibling (`docs/ADR-019-wms-posting-profile-pattern.md`, PR #317) |
| TD-9 | **NEW 2026-05-24** — ADR-023 stub (multi-modal pipeline for mill certs — queued 2026-05-19 but never drafted) | **Backlog** | Trigger: customer signs that needs auto-heat-capture OR Sprint 14 FAI work picks up |
| TD-10 | **NEW 2026-05-24** — ADR-024 split (double-claimed: Tool Registry vs Q3 host migration) | **Wave 14** (Tool Registry); **Backlog** (Q3 host → new ADR-027) | Awaiting Dean's call A4 |
| TD-11 | **NEW 2026-05-24** — ADR-026 draft (Seven Customer Modes Contract — Sprint 13.5 PR #8 scope) | **Wave 2.6** (lands with PRA-5b — segment design needs the contract) | — |
| TD-12 | **NEW 2026-05-24** — Memory SHA drift fix (`fdc4740` → `177fb53` for PR #5d) | ✅ **filed in audit §13** | Cosmetic |
| TD-13 | **NEW 2026-05-24** — `docs/ADR-INDEX.md` doesn't exist; ADR catalog is implicit | **Wave 6 Workstream D** | Single-page index of all ADRs + statuses |
| TD-15 | **NEW 2026-05-27** — **PR-XminBackfill defensive bundle** — 3 latent `IsRowVersion()`+`bytea NOT NULL` entities at AppDbContext.cs lines 2701 (`CustomerItemXref`) / 2754 (`ItemSourcingRule`) / 2816 (`CostLayer`). Will throw PG 23502 on first INSERT. Dormant because existing probes are read-or-update-only. | **Wave 4.85 IMMEDIATE next ship** (per audit §4 — see Priority 1.670 above) | ~1.5h. Drop bytea column + remap to xmin + `byte[]? RowVersion`. Pattern proven by PR #365 hotfix. Memory: `feedback_xmin_pattern_for_concurrency_lock.md`. |
| TD-16 | **NEW 2026-05-27** — IndustryOS current state memory file stale (claims HEAD `8594f23`; actual main HEAD `813d399`) | **Opportunistic** | Cosmetic memory drift fix; bumped via `project_industryos_current_state.md` next time it's touched |
| TD-14 | **NEW 2026-05-25** — **UI Audit + Cleanup Sprint** (broken nav links, dense forms, redundant subforms, dead pages, "side menu is a disaster" per Dean) | **Wave 2.5** — Priority 1.62a | 4-agent parallel audit → 1 synthesis PR → ~6 execution PRs. Authority: `wwwroot/css/design-tokens-v2.json` + `docs/research/luxury-cockpit-ux.md`. Reference: Priority 1.62a above for full spec. |

---

## 🎯 Definition of Done (applies to every PR)

A PR is not "done" until:

1. ✅ **Code merged to `main`** via squash-merge.
2. ✅ **Pulled on Replit** + dotnet respawned + app responds HTTP 302/200 on `/`.
3. ✅ **Live verification** on the running app:
    - For bug fixes: reproduce the original bug failed; after PR, same probe passes.
    - For features: end-to-end happy-path exercised via the UI or JS-driven form POST.
    - For finance changes: JE balance proven (Σdebits == Σcredits) on the produced JE.
4. ✅ **PR thread comment** posted with verification evidence (response codes, JE refs, screenshots if relevant).
5. ✅ **No regression**: existing flows touched in the same code path still work.
6. ✅ **MASTER_PLAN.md checkbox** marked complete with the PR # link.

If any step fails: the PR is reopened, the failure noted in the verify comment, and the gap fixed before checking the box.

---

## 🚫 Anti-scope (what we are NOT doing in this plan)

- **Rewriting Razor Pages to MVC or Blazor.** Stays as-is.
- **Migrating Postgres to anything else.** Stays as-is.
- **Building a separate mobile app.** PWA approach only.
- **Custom auth replacing ASP.NET Identity.** We harden Identity, not replace.
- **Refactoring outside the gap surface.** No "while we're in there" rewrites.
- **Premium features that don't ship a customer-visible JE/UI delta.** No dead-letter code.

Anything not on this plan = future sprint or out of scope. **Surface scope creep loudly.**

---

## ⚠️ Risk register

| Risk | Mitigation |
|---|---|
| Cross-PR breakage (e.g. PR #103 changes CloseoutService that PR #105 also touches) | Sprint 0 PRs ordered so dependencies flow; verify regression on every push. |
| Migration safety (new columns on `JournalEntry` or `MaintenanceEvent`) | All schema changes use raw-SQL migration following PR #67 pattern; backfill in same migration. |
| Tenant data leakage in flight (RLS not in until PR #119) | Every new query joins through Asset → CompanyId check, same posture as PR #94+. |
| Replit auto-respawn kills in-flight dotnet | Kill-then-spawn pattern with `disown`. Already proven across PR #89–99. |
| `IMPR:0` legacy data | PR #99 noted a back-reference repair migration — added to PR #108. |

---

# SPRINT 0 — Real bugs from audit ⚡

**Goal:** Close every immediately-actionable bug surfaced by the audit. Mostly XS+S. Estimated 30 hours of work across 9 PRs. **Ship before any new features.**

**Order matters:** security XS → tenant scoping → finance integrity → operations rollup → reliability fixes → P2P hardening → operation-level parts → cleanups. Each PR is independent — no blockers.

---

## ✅ PR #100 — Security hardening sweep ([#100](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/100)) (XS, shipped)

Four XS bugs grouped because they're all <30 LOC each and live in security-adjacent files.

- [x] **B-01** Replace `string.Equals` with `CryptographicOperations.FixedTimeEquals` on webhook HMAC verification
  - **File:** `Services/Integrations/InboundWebhookService.cs` (or wherever HMAC verifies)
  - **Fix:** Use `CryptographicOperations.FixedTimeEquals(computedBytes, providedBytes)` (already imported elsewhere).
  - **Acceptance:** Code search confirms zero `string.Equals` calls on HMAC paths.
  - **Verification:** Post a webhook with a tampered signature → 401. Then with valid signature → 200.

- [x] **B-02** Fix `Viewer` role typo on Vendors Edit page
  - **File:** `Pages/Materials/Vendors/Edit.cshtml.cs:13`
  - **Diagnosis:** `[Authorize(Roles="Viewer")]` lets read-only users edit vendors.
  - **Fix:** `[Authorize(Roles="Admin,Manager")]` matching the standard write-side role gate elsewhere.
  - **Acceptance:** Logged in as Viewer → 403 on POST. As Admin → 200.
  - **Verification:** Browser test with both roles.

- [x] **B-03** Remove undefined `SystemAdmin` role reference from SmokeTests
  - **File:** `Pages/Admin/SmokeTests.cshtml.cs` (or equivalent)
  - **Diagnosis:** Page references `[Authorize(Roles="SystemAdmin")]` but that role doesn't exist in the identity store.
  - **Fix:** Replace with `Admin` (the canonical superuser role) or define `SystemAdmin` properly in the role seed.
  - **Acceptance:** Admin user can hit the SmokeTests page; non-Admin gets 403.

- [x] **B-04** Environment-gate the demo credential click-to-fill buttons on Login page
  - **File:** `Pages/Account/Login.cshtml`
  - **Diagnosis:** Renders three hardcoded `(role, password)` buttons regardless of environment.
  - **Fix:** Wrap the demo-creds `<div>` in `@if (Env.IsDevelopment() || tenantFlag.AllowDemoMode) { ... }`. Inject `IWebHostEnvironment` if needed.
  - **Acceptance:** Production-environment build renders no demo buttons. Development still does.
  - **Verification:** Hit `/Account/Login` with `ASPNETCORE_ENVIRONMENT=Production` → buttons hidden.

**PR Body template:** "Security hardening sweep — 4 XS bugs from audit (timing-safe HMAC + Viewer typo + SystemAdmin ref + demo-creds env gate)."

**Live verification checklist:**
- [x] B-01 verified by code inspection (FixedTimeEquals replaces string.Equals; hex parse + length check before constant-time compare)
- [x] Viewer GET on `/Materials/Vendors/Edit/1` → redirects to `/Account/AccessDenied` ✓
- [x] Viewer GET on `/Admin/SmokeTests` → redirects to `/Account/AccessDenied` ✓
- [x] Demo buttons present in Dev (Replit IS dev — 3 buttons rendered); gated off when `ShowDemoAccounts=false` per the `@if` wrapper

**Verify-comment:** [#100 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/100#issuecomment-4466762243)

---

## ✅ PR #101 — API controller tenant scoping ([#101](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/101)) (M, shipped)

The single most serious finding. Hostile API key currently reads across tenants. **Breach closed.**

- [x] **B-05** Add `TenantId` + `CompanyId` to `ApiKey` model
  - **File:** `Models/ApiKey.cs` (find via Grep)
  - **Migration:** Raw-SQL ALTER TABLE adding `TenantId int` and `CompanyId int?` (nullable for "global" admin keys).
  - **Backfill:** For each existing ApiKey, derive TenantId from `CreatedBy` user's tenant if possible; else mark `MIGRATION_NEEDED` and require admin re-issue.

- [x] **B-06** Enforce scoping in `AssetsApiController` and every other API controller
  - **Files:** `Controllers/*Api*.cs`
  - **Fix:** Resolve `apiKey.TenantId / CompanyId` from the bearer header → set `_tenantContext.TenantId / CompanyId / VisibleCompanyIds` → existing query layer already scopes.
  - **Critical:** every `_context.Assets.Where(...)` query in API must `.Where(a => visibleIds.Contains(a.CompanyId ?? 0))`.

- [x] **B-07** Add a tenant-isolation smoke test for the API layer
  - **File:** Add to existing `Services/Testing/SmokeTestRunner.cs`
  - **Test:** Tenant A's key issuing `GET /api/v1/assets?ids=...` for Tenant B's asset Ids returns 404 / empty.

- [x] **B-08** Document the breaking change for API consumers
  - **File:** New `docs/api/v1-breaking-tenant-scoping.md`
  - **Note:** Existing API keys without TenantId must be re-issued. Add deprecation header on responses for unmigrated keys.

**Acceptance:**
- New key created via Admin UI carries TenantId
- API call returns ONLY assets in that tenant's `VisibleCompanyIds`
- Pre-migration keys reject with a clear "re-issue required" 403

**Live verification:**
- [x] Schema migration applied (TenantId NOT NULL + CompanyId nullable) — confirmed via `\d "ApiKeys"`
- [x] Admin UI creates new keys with TenantId bound — DB row shows `TenantId=1, CompanyId=NULL`
- [x] API call with new key returns scoped data — `totalCount=334`, response shape unchanged
- [x] Smoke test `Test_ApiKeysAreTenantScoped` registered (#70 in the runner)
- [⏳] Cross-tenant fixture probe deferred — no second tenant in demo DB to test against
- [x] Sentinel-refusal path verified by inspection (`Controllers/AssetsApiController.cs:48-53`)

**Verify-comment:** [#101 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/101#issuecomment-4466797247)

---

## ✅ PR #102 — Trial balance integrity sweep ([#102](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/102)) (M, shipped)

Three finance bugs that together let the trial balance silently drift. Group because they all touch the JE plumbing.

- [x] **B-09** Tax + Freight in PO/GR/Invoice JEs
  - **Files:**
    - `Services/Receiving/ReceivingPostingService.cs` — add tax/freight DR lines
    - `Services/AccountsPayable/ApPostingService.cs` — add tax/freight to approval JE
  - **Diagnosis:** `PurchaseOrder.TaxAmount`/`.ShippingAmount` and `VendorInvoice.TaxAmount`/`.ShippingAmount` are stored in headers but never make it into JE lines. Header `Total = Subtotal + Tax + Shipping`, but the receiving + AP-approval JEs only debit `Subtotal`-equivalent lines.
  - **Fix:**
    - Add `GlAccountKind.SalesTaxPayable` (already in resolver? check) and `GlAccountKind.FreightExpense`
    - On GR posting: extra DR line(s) for the prorated tax/freight allocated by line value
    - On AP approval: same allocation, paired to a CR AP that includes tax/freight
  - **Acceptance:** Header `Total = $1000 + $80 tax + $50 freight = $1130` → JE total credits = $1130, balanced.
  - **Migration:** None (uses existing fields).

- [x] **B-10** Capital Improvement → JE
  - **File:** `Pages/Assets/Improve.cshtml.cs:113-148` AND new helper in `Services/`
  - **Diagnosis:** `OnPostImproveAsync` and the PR #96 `OnPostCapitalizeAsync` both increment `Asset.AcquisitionCost` directly without posting a JE.
  - **Fix:** New `Services/CapitalImprovementPostingService.cs` posting `DR AssetCost / CR (clearing or cash)`. Source = `CIP-IMPR` to distinguish from `WO-LBR`/`WO-ISS`/`AP-APR`.
  - **CR side:** Use `GlAccountKind.CipPending` (1400) if improvement came from CIP, else `GlAccountKind.AccountsPayable` (2000) if outside vendor, else `GlAccountKind.Cash` (1110) for direct purchases.
  - **Acceptance:** Improving an asset by $1,000 produces a 2-line JE with `Source="CIP-IMPR"`, `Reference="IMPR-{id}"`, balanced.
  - **Verification:** Repeat PR #96's $1234.56 capitalization on a fresh WO; confirm new JE row appears next to the AcquisitionCost bump.

- [x] **B-11** `JournalGenerator.GenerateMonthlyAsync` MACRS fall-through
  - **File:** `Services/JournalGenerator.cs`
  - **Diagnosis:** Reflection probe for `CalculateMonthly` method on the method object finds nothing, so MACRS / DDB acceleration is silently dropped and SL is used for the GL aggregate JE. Per-asset snapshots are correct; GL is wrong.
  - **Fix:** Either implement `CalculateMonthly` properly on each method, or change the call site to compute monthly = `(currentYearDepreciation / 12)` where `currentYearDepreciation` already exists. Validate that monthly sum across year = annual depreciation expense for each method.
  - **Acceptance:** Monthly JE for a MACRS-5 asset in Year 1 shows the **bonus first-year %**, not the SL equivalent.
  - **Test:** Year-1 monthly JE for $50K MACRS-5 asset should sum to ~$10K (20% × $50K) across 12 months, not ~$10K ($50K / 5).

**PR Body template:** "Trial balance integrity sweep — tax/freight in JE, Capital Improvement → JE, MACRS monthly fall-through."

**Live verification:**
- [x] Created invoice with $1000 + $80 tax + $50 freight → approval JE balances at $1130 (4 lines: DR 1290 $80 + DR 6000 $1000 + DR 6300 $50 / CR 2000 $1130). `/Journals/Details/1816` ✓
- [x] Improved asset $5000 → new `CIP-IMPR / IMPR-212` JE shows up in `/Journals` (DR 1500 $5000 / CR 1400 $5000, balanced). AcquisitionCost bumped $169,401.26 → $174,401.26 ✓
- [⏳] MACRS monthly-JE behavioral test deferred — demo DB has zero non-SL assets (all 334 are `DepreciationMethod=0`). Code-inspection verified; Sprint 1 fixture work tracked as task #46

**Verify-comment:** [#102 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/102#issuecomment-4466839710)

---

## ✅ PR #103 — WO closeout cost rollup + PM cycle ([#103](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/103)) (S, shipped)

Two operations bugs that both fire on WO close.

- [x] **B-12** CloseoutService writes back `LaborCost` / `PartsCost` / `MaterialsCost` to WO header
  - **File:** `Services/Maintenance/CloseoutService.cs::CloseWorkOrderAsync`
  - **Diagnosis:** PR #98 made this the default close path, but the service doesn't roll up the JE sums to `MaintenanceEvent.LaborCost / PartsCost / MaterialsCost`. WO header shows nulls.
  - **Fix:** Before final SaveChanges:
    ```csharp
    evt.LaborCost = await _context.JournalEntries
      .Where(j => j.Source == "WO-LBR" && j.Reference.Contains($"-{evt.Id}-"))
      .SelectMany(j => j.Lines)
      .Where(l => l.Debit > 0)
      .SumAsync(l => l.Debit);
    // similar for MaterialsCost (WO-ISS minus WO-RTN) and PartsCost
    ```
  - **Acceptance:** Close WO with $150 labor + $37.50 materials net → header reads LaborCost=$150, MaterialsCost=$37.50.

- [x] **B-13** PM cycle advances on close
  - **File:** `Services/Maintenance/CloseoutService.cs` (same file) and `PMSchedulerService.cs`
  - **Diagnosis:** `PMOccurrence.Status = Completed` is unreachable from the close flow.
  - **Fix:** In CloseoutService, after WO close: find the PMOccurrence (if PMOccurrenceId on the WO), flip Status=Completed, set `LastCompletedDate = DateTime.UtcNow`, and the scheduler's next-due calculation picks up from there.
  - **Acceptance:** Close a PM-driven WO → its PMOccurrence row shows `Completed` + `LastCompletedDate` populated.

**Live verification:**
- [x] Closed WO-26-00231 (had $150 labor + $0 net materials in JEs) → DB row: `LaborCost=$150, MaterialsCost=$0, ActualCost=$150, Status=Completed, CompletedDate=2026-05-16 12:33:48` ✓
- [⏳] PM-occurrence completion deferred — demo DB has zero WOs with `PMOccurrenceId IS NOT NULL`. Folded into Sprint 1 fixture task #46.

**Verify-comment:** [#103 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/103#issuecomment-4466864401)

---

## ✅ PR #104 — Reliability KPI fixes (S, ~4 hr) — **SHIPPED 2026-05-16 (#104)**

Three reliability fixes. Dashboard MTTR is currently lying — finance-readable bug.

- [x] **B-14** Fix MTTR calculation on dashboard
  - **File:** `Pages/Index.cshtml.cs:153-167`
  - **Diagnosis:** Current code computes `(CompletedDate - ScheduledDate)` — that's "average lateness vs schedule," not Mean Time To Repair.
  - **Fix:** `MTTR = AVG(CompletedDate - StartedAt)` for WOs where both are non-null. Add `StartedAt` to `MaintenanceEvent` if it doesn't exist (probably does as the InProgress status timestamp).
  - **Acceptance:** Dashboard MTTR matches `SELECT AVG(EXTRACT(epoch FROM (completed_date - started_at)) / 3600) FROM maintenance_events WHERE started_at IS NOT NULL AND completed_date IS NOT NULL` (hours).

- [x] **B-15** Compute and surface MTBF
  - **File:** `Pages/Index.cshtml.cs` + new tile in dashboard
  - **Fix:** `MTBF = Σ uptime / N failures` where uptime = (current time - last corrective WO close time) summed per asset. Initially compute org-wide MTBF; per-asset MTBF goes in PR #131.
  - **Acceptance:** Dashboard renders a "MTBF (org)" tile in hours.

- [x] **B-16** Convert `MaintenanceEvent.FailureCode` string → FK to `FailureCode` master
  - **Files:**
    - `Models/MaintenanceEvent.cs` (add `FailureCodeId int?` FK)
    - `Models/FailureCode.cs` (already exists with 24 seeded rows)
    - Migration: add column + index; backfill where `MaintenanceEvent.FailureCode` matches `FailureCode.Code`
    - Pages/WorkOrders/Details.cshtml — Edit form: replace text input with `<select>` from seeded FailureCode table
  - **Acceptance:** Edit a WO → FailureCode dropdown lists 24 options; saving stores `FailureCodeId`.
  - **Unblocks:** Pareto charts in PR #133, Weibull in PR #134.

**Live verification:**
- [x] Dashboard MTTR reads in hours (not "days late") — `0.0 avg hours to repair` (honest zero on demo data lacking StartedAt; pre-PR was lying with "days late")
- [x] Dashboard MTBF tile present — `3271.6 avg hours between failures` (new tile, border-left-purple)
- [x] WO edit form has FailureCode dropdown — `/WorkOrders/Details/241` → Edit panel → `HYD-LEAK — HYDRAULIC LEAK` dropdown with 24 seeded options
- [x] Verify FailureCodeId persists across reload — backfill produced `(1, 0)` (matched, ambiguous); FK constraint + index + ON DELETE SET NULL all confirmed via `\d "MaintenanceEvents"`

---

## ✅ PR #105 — Procure-to-Pay hardening (S, ~5 hr) — **SHIPPED 2026-05-16 (#105)**

Two P2P bugs + add the [Authorize] gates everyone's been asking for.

- [x] **B-17** Reject qty in Inspect reverses inventory move
  - **File:** `Services/Receiving/ReceivingPostingService.cs` (Inspect handler)
  - **Diagnosis:** When inspector marks qty as rejected, the inventory increment from PostReceiptAsync is NOT reversed. Physical count drifts.
  - **Fix:** On reject, decrement ItemInventory and post a corresponding negative inventory ItemTransaction (Type=ReceiptReverse) AND a reversing partial JE (DR GR-Accrued / CR Inventory) for the rejected portion.
  - **Acceptance:** Receive 10, reject 3 → inventory shows 7 on hand, JEs net to $0 for the rejected 3 (offsetting GR + reversal pair).

- [x] **B-18** Add `[Authorize(Roles="Admin,Accountant")]` on Purchasing + AP write actions
  - **Files:** Every `OnPost*` handler in `Pages/Purchasing/*` and `Pages/AccountsPayable/*` that creates or approves
  - **Fix:** Standard role decoration. `Approve` actions get `Manager+`; create/edit get `Admin,Manager`; receive gets `Admin,Manager,Receiver` (define new Receiver role if needed).
  - **Acceptance:** Viewer logged in → 403 on PO approve, AP approve, payment create.
  - **Note:** The 17-bug audit called out specifically: same user can create+approve+receive+invoice-approve. Add an SoD smoke test that the **same** user posting both an `Approve` and the `Create` on the same PO/Invoice is logged with a warning.

- [x] **B-19** AP approval `overrideMatch=true` requires Admin role + reason + AuditLog
  - **File:** `Pages/AccountsPayable/Details.cshtml.cs` (Approve handler)
  - **Diagnosis:** Currently `overrideMatch` toggle bypasses three-way match with zero role check despite ADR-002's "admin override with audit trail" comment.
  - **Fix:** If `overrideMatch=true`, require `[Authorize(Roles="Admin,Finance")]` and write an `AuditLog` row with the override reason (add a `overrideReason` text input on the form, required when override=true).

**Live verification:**
- [x] Viewer → 403 verified by source-equivalence with PR #100 pattern (same attribute shape proven there)
- [x] Admin approves PO → page renders + handlers commit (positive path live-tested)
- [x] AP approval with overrideMatch=true and no reason → HTML5 `required` validator rejects in browser; server-side `string.IsNullOrWhiteSpace` rejects on direct POST
- [x] AP approval with reason → AuditLog row #12966 captured with full reason text, EntityType=VendorInvoice, EntityId=5, Username=ADMIN
- [⏳] B-17 behavioral test deferred — no demo receipt has `QuantityRejected > 0`. Folded into Sprint 1 fixture task #46.

---

## ✅ PR #106 — Operation-level parts JE (S, ~4 hr)

One ops bug. Materials issued via `WorkOrderOperationPart` (the per-operation model) currently bypass GL — only `WorkOrderPart` (WO-level) posts JEs from PR #89. — **SHIPPED 2026-05-16 (#106)**

- [x] **B-20** Operation-level parts post JEs on issue/return
  - **File:** `Pages/WorkOrders/Details.cshtml.cs` — find handler for `OnPostUseOperationPartAsync` or `WorkOrderOperationPart` issuance
  - **Fix:** Wire `PostMaterialMovementJournalEntryAsync` (the helper PR #89 added) to the operation-level path too. Pass the operation's parent WO + asset for `GlResolveContext`.
  - **Acceptance:** Issue a part to an operation → JE shows up with `Source="WO-ISS"`, balanced.
  - **Reuse:** Don't duplicate — refactor the helper to accept either a `WorkOrderPart` or a `WorkOrderOperationPart`.

- [x] **B-21** Audit + reconcile: new SmokeTest #71 reports orphan op-part count (audit-mode; backfill deferred)

**Live verification:**
- [x] Added a part to op 32 → clicked "Issue" → JE 1818 posted with `Source=WO-ISS-OP, Reference=WO-ISS-OP-241-OP32-P1-..., DR 6210 $151 / CR 1300 $151` (balanced)
- [x] SmokeTest #71 registered (`Test_OperationPartsHaveJournalEntries`)
- [x] Maintenance Spend report code path picks both `WO-ISS` and `WO-ISS-OP` up via the expanded `CloseoutService` rollup query
- [⏳] Historical backfill of orphan op-parts deferred — admin workflow in a future PR (period-lock safety)

---

## ✅ PR #107 — Decorative scaffolding cleanup (S, ~3 hr) — **SHIPPED 2026-05-16 (GitHub #107 + #108 follow-up)**

Stop the asset detail page from showing fields no service writes — currently misleads salespeople and customers.

- [x] **B-22** Hide unpopulated IoT/OEE/Health fields in Asset detail
  - **File:** `Pages/Assets/Asset.cshtml` (the read view, not the edit)
  - **Fix:** Wrap each `~25 decorative field` in `@if (!string.IsNullOrEmpty(Model.Asset.IoTEndpointUrl)) { ... }` etc. Keep edit-form access so admins can populate for testing.
  - **List to gate:** All fields in the audit's "Decorative scaffolding" section: SCADATag, DataHistorianTag, IoT*, CurrentTemperature/Vibration/Pressure/Reading, SensorReadingsLastUpdated, HealthScore*, PredictiveHealth*, PredictedFailure*, CurrentOEE/Availability/Performance/Quality, TargetOEE/etc, EnergyConsumptionKW, EnergyClass.
  - **Acceptance:** Asset with none of these fields populated → that section of the detail page doesn't render at all.

- [x] **B-23** Empty-state banner + `/Help/Topic?id=iot-setup` page added — explains the 3 data sources (IoT gateway / MES / predictive) with setup steps

**Live verification:**
- [x] FA-CIP-USA-001 (no IoT data) → MES tab shows empty-state banner instead of Current OEE block; IoT tab shows banner instead of Connection Status + Sensor Readings panels
- [x] `/Help/Topic?id=iot-setup` renders with the new explainer content + Related Topics
- [⏳] Asset-with-HealthScore positive-path test deferred — no demo assets have PredictiveHealthScore set; gating logic verified by source inspection (`hasCurrentOee` flips true when any of CurrentOEE/CurrentAvailability/CurrentPerformance/CurrentQuality is non-null)

---

## ✅ PR #108 — Misc cleanups + IMPR back-reference repair (XS, ~2 hr) — **SHIPPED 2026-05-16 (GitHub #109)**

- [x] **B-24** Remove obsolete PR #82 comment in BulkOperationsService
  - **File:** `Services/BulkOperationsService.cs:211-217`
  - **Fix:** Delete or update the comment claiming 30-char Batch cap (widened to 60 in PR #83).

- [x] **B-25** Reconcile `MaintenanceCostMTD/YTD` data source on dashboard
  - **File:** `Pages/Index.cshtml.cs`
  - **Diagnosis:** Sums `MaintenanceEvent.ActualCost` (legacy manual entry) while PR #93 spend report sums the operation tables. Two sources of truth.
  - **Fix:** Either (a) make ActualCost computed from the same source as PR #93's spend report, or (b) deprecate ActualCost and have dashboard call the same query PR #93 uses. Prefer (b).

- [x] **B-26** IMPR back-reference repair (from PR #99 follow-up)
  - **File:** New migration `Migrations/20260516_RepairImpr0BackReferences.cs`
  - **Fix:** Walk every `MaintenanceEvent` with `CustomField2 = 'IMPR:0'`, find the matching `CapitalImprovement` by (AssetId, ImprovementDate, Cost), update `CustomField2 = 'IMPR:{matchedId}'`. Log mismatches (multiple candidates or no match).
  - **Acceptance:** Post-migration, no rows have `CustomField2='IMPR:0'` (except orphans logged).

- [x] **B-27** Smoke test for PR #93 reconciliation invariant
  - **File:** Add to `SmokeTestRunner`
  - **Test:** For each tenant, operational rollup total - JE rollup total = 0 (or = legacy un-backfilled amount documented elsewhere).

**Live verification:**
- [x] `git grep "30-char"` returns no stale references
- [x] Dashboard MaintenanceCostMTD now sums JE table — spend_mtd query returns $329.50 (includes WO-LBR + WO-ISS-OP entries)
- [x] Repair migration ran: 1 IMPR:0 row scanned, 0 unique matches, 1 left for admin review (correct conservative behavior — orphaned CapitalImprovement)
- [x] SmokeTest #72 `Test_MaintenanceSpendReconciliation` registered and runs in admin test suite

---

# SPRINT 1 — Reliability + Finance Credibility 📈

**Goal:** Close 50% of the Maximo reliability/APM gap (15% → ~40%) and finalize finance integrity. Reaches "ready for finance demo" status.

**Timeline:** 2 weeks after Sprint 0 wraps.

---

## ✅ PR #109 — Failure mode analytics (M)

- [ ] Pareto chart of FailureCode by frequency on `/Reports/Reliability`
- [ ] Pareto by cost (multiply count × avg actual cost from JEs)
- [ ] Drill-through: click bar → list of WOs in that failure code
- [ ] CSV export

**Unblocked by:** B-16 (FailureCode FK)

---

## ✅ PR #110 — Per-asset MTBF + Availability (M)

- [ ] Per-asset MTBF computation: walk Corrective WO close dates → compute mean time between them
- [ ] Availability % = (uptime / scheduled time) over a configurable window
- [ ] Surface on Asset detail page + as a new column in `/Reports/MaintenanceSpend`
- [ ] Asset register list sortable by MTBF, Availability, Spend YTD

---

## ✅ PR #111 — Reliability tiles on dashboard (S)

- [ ] Add tiles: Top 5 worst-MTBF assets, Top 5 most-overdue WOs, Backlog count by priority, % WOs completed on schedule
- [ ] Refresh-on-load with skeleton states

---

## ✅ PR #112 — Period close orchestration ([#120](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/120) + [#121](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/121) cycle-fix) (M, shipped)

- [x] One-click sequenced close: run depreciation → verify trial balance → reconcile GR/IR clearing → lock period
- [x] Pre-flight checklist (red/green): 7 checks — period state, TB balanced, depreciation status, GR/IR clearing, stale GRs, unapproved invoices in period, WO completion drift
- [x] Reversal journal for any post-close adjustment (existing `/Journals/Details` Reverse from #119 + PeriodGuard interaction)
- [x] Immutable close packet snapshot on `FiscalPeriod.PreflightSnapshotJson` (2,445 char structured JSON)
- [x] AuditLog entry on every close + reopen, override-reason capture

**Live verification (P1 2026 / fiscalPeriodId=25, 2026-05-16):**
- Pre-flight: 5 PASS + 2 WARN (depreciation pending, 2 unapproved invoices) → no blockers
- Sequenced close: 1 book posted DR $45,678.62 to Depreciation Expense; TB rebalanced $695,926.72 = $695,926.72; GR/IR Net $0.00 ≤ tolerance; Status flipped to Closed at 2026-05-16 15:23:06 UTC by ADMIN
- DB confirms: `Status=1`, `DepreciationPosted=t`, `ClosedAt` populated, `PreflightSnapshotJson` 2445 chars
- AuditLog row written with flat snapshot (no entity cycle), Action=PeriodClose

**Verify-comments:** [#120 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/120#issuecomment-4467260109), [#121 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/121#issuecomment-4467260138)

---

## ✅ PR #113 — Trial Balance + Sub-Ledger reconciliation reports (M)

- [ ] Trial Balance: balance per GL account from JournalLine sums; balanced check at footer
- [ ] Sub-Ledger Recon: AP balance vs Σ open VendorInvoice.BalanceDue; Inventory balance vs Σ ItemInventory.QuantityOnHand × UnitCost
- [ ] Drill-through to JE detail
- [ ] CSV + Excel export

---

## ✅ PR #114 — Manual JE entry UI + reversal (M)

- [ ] `/Journals/Manual` form: pick period, source="MANUAL", multiple lines, balance check before submit
- [ ] Reversal flow: button on existing JE detail → creates negated mirror with `Reference=REV-{originalRef}`
- [ ] Both gated by Finance role
- [ ] AuditLog every manual JE + reversal

---

# SPRINT 2 — Production-app hardening 🚀

**Goal:** Finish the production app. Close enterprise-readiness gates (approvals/SoD/SSO/RLS/MFA), ship the demo killers (Plant Floor live view, MCP, agentic AI), and overhaul the surfaces that gate "best in class" (nav, admin, help/onboarding, cosmetics). Mobile is deferred to Sprint 4 — once Sprint 3 wraps, we'll pick the production features that actually want a mobile surface.

**Theme rewrite (2026-05-16):** original Sprint 2 was "Approvals + Mobile + Agentic." Mobile pulled out (Sprint 4). Brainstorm items absorbed: UX/nav redesign, Plant Floor View (with Sensor + AssetHealth pulled forward from Sprint 3 PR #124), Admin v2, Help/Onboarding overhaul. Result: 10 PRs in Sprint 2 instead of 8.

**Timeline:** 6-8 weeks after Sprint 1 (was 4-6; the brainstorm items added meaningful scope).

---

## ✅ PR #115 — Approval hierarchy + SoD ([#122](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/122)) (L, shipped)

- [x] **New table**: `ApprovalActions` (immutable decision log keyed by TargetEntityType+TargetEntityId, 11 cols, 5 indexes, 2 FKs). The existing `ApprovalWorkflow` model + 6 seeded rows + DbSet were already in the repo.
- [x] **Configurable per-document-type thresholds** via `ApprovalWorkflow` rows. Seeded defaults: PO_STD ($10K, Manager), PO_HIGH ($50K, 2 approvers). Demo workflow `PO_DEMO` inserted live to prove threshold filtering.
- [x] **UI**: `/Approvals/Pending` user-facing queue with progress bars per row, target-type chips, deep-link to source doc, "Inbox zero" empty state.
- [⏳] Email + webhook on every step — deferred to Sprint 2.1.
- [x] **SoD enforcement**: `ApprovalService.RecordDecisionAsync` checks `creatorUserId == approverUserId` and returns `Outcome=SodViolation`.
- [x] **AuditLog**: every decision writes a flat snapshot to `AuditLogs` using the cycle-safe pattern from `feedback_audit_log_serialization`.
- [⏳] Sequential multi-step chain (Manager → Director → CFO) — deferred to Sprint 2.1; needs an `ApprovalWorkflowSteps` child table.
- [⏳] Invoice approve handler wiring — deferred to Sprint 2.1; PO Approve flow shipped first.

**Live verification (2026-05-16):**
- Schema applied at startup: `[Startup] Database migrations applied successfully` for migration `20260516_AddApprovalActions`.
- `/Approvals/Pending` rendered with 2 PendingApproval POs > $10K (PWH-0243 $12,853.35, PWH-0171 $11,436.91) after a `PO_DEMO` workflow row was inserted. The 3 POs below $10K correctly did NOT appear — proves threshold filtering.
- DI registration verified by zero startup errors after adding `IApprovalService` to both `Program.cs` and the PO `DetailsModel` constructor.

**Follow-up noted:** PO Details page render glitched on first click of `Review →` (Chrome frame error, no runtime exception in app log; pre-existing CS8602 warnings on lines untouched by this PR). Sprint 2.1 will repro in a clean session and patch if it's a real regression.

**Verify-comment:** [#122 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/122#issuecomment-4467325210)

---

## ✅ PR #116 — UX overhaul (M-L, split into 116a-d) **NEW**

> Absorbed from Dean's 2026-05-16 brainstorm + E2E behavioral audit. Inherits to every page after it including Plant Floor View, Admin v2, future Sprint 4 mobile work. Full plan in [PR116_UX_OVERHAUL_PLAN.md](./PR116_UX_OVERHAUL_PLAN.md).

### ✅ PR #116a — Behavioral fixes + orphan sweep ([#123](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/123) + [#124](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/124) partials-fix) (XS-S, shipped 2026-05-16)

- [x] Sidebar collapse reflows content pane (`:root:has(.sidebar.collapsed) { --sidebar-width: 64px }` + body-class fallback)
- [x] Tooltips on collapsed rail (`overflow: visible` + `[data-tooltip]::after` flyout + native `title=`)
- [x] Collapsed-icon clicks navigate to group's first child
- [x] Scroll preservation on list→detail→back (`wwwroot/js/scroll-restore.js`, sessionStorage-keyed by URL+query)
- [x] 20 orphan pages + 28 orphan partials deleted (48 file deletes total)
- [x] Inbound refs cleaned in 13 files (nav, command-palette, ReportHub, Export, dashboard, admin landing, partials, reports pages, help, ReturnUrlHelper, SmokeTestRunner, Program.cs)
- [x] Duplicate KPI strip removed from `/Assets`
- [x] Status humanization helper (`Humanize()`) applied to Asset Detail Recent Activity

**Live verification:** 0 CS errors, HTTP 302, sidebar reflow confirmed visually, 5 orphan URLs return 404. **Verify-comment:** [#123 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/123#issuecomment-4467497032)

**PR #116a follow-up ([#125](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/125)) — hover-peek + tone-active CSS alias:**
- Linear-style hover-peek on collapsed sidebar (140ms enter delay, 320ms leave grace). Expands sidebar to 280px overlay on hover; content pane stays put. Keyboard-accessible (focusin/focusout).
- `.status-pill.tone-active` aliased to `.tone-success` — 26 pages with `StatusTone="active"` (Locations, Admin/Sites, Admin/Departments, etc.) now render proper green pills instantly.
- Locations partial path normalized; duplicate inline KPI strip removed.
- **Verify-comment:** [#125 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/125#issuecomment-4467533396)

### ✅ PR #116b — Kill topbar + sidebar footer controls ([#126](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/126)) (M, shipped 2026-05-16)

- [x] Removed the persistent 64px topbar (`.main-header` deleted from layout + suppressed with `display: none !important`)
- [x] Migrated Search/Cmd-K trigger, Theme toggle, Help button to sidebar footer utility row
- [x] User dropdown migrated to sidebar footer with Sign Out + Help Center menu items
- [x] `/` hotkey rebound to open Cmd-K palette
- [x] Mobile menu becomes a floating button (mobile media query)
- [⏳] 40px contextual subbar inside content frame — deferred to PR #116c
- [⏳] Branded Cmd-K palette polish (frosted glass, recent items) — deferred to PR #116c

**Live verification:** ~65px reclaimed on every page; dashboard content now starts at y=55 (was y=120+). User dropdown popover opens upward with name/role/Help/Sign Out. Cmd-K + `/` + click all open the palette.

**Verify-comment:** [#126 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/126#issuecomment-4467554318)

### ✅ PR #116c — Design polish ([#127](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/127)) (M, shipped 2026-05-16)

- [x] Sidebar density refinement (8/12 padding, 28px icons, 11px tracked-caps section labels)
- [x] Active state uses 2px left accent bar + 6% tint (was full background pill)
- [x] Cmd-K palette monospace input (SF Mono / Menlo / Consolas)
- [x] Cmd-K palette RECENT group from localStorage (last 5 destinations)
- [x] Cmd-K palette branded empty state (echoes typed term in monospace)
- [x] Sticky table headers globally via CSS (.ledger-table, .data-table, .enhanced-grid-container .data-table, table.sortable-table, table.lux-table)
- [⏳] Design-system primitive extraction (`<DataCard>`, `<DrillThroughLink>`, etc.) — deferred until organic reuse case appears
- [⏳] Classic-theme escape hatch — deferred (no rollback need observed)

**Live verification:** Cmd-K hotkey + monospace input + branded empty state + grouped results all confirmed visually. Sidebar density visibly tightened with Linear/Stripe pattern.

**Follow-up noted:** Cmd-K route catalog missing `/Approvals/Pending` (PR #115) and `/Periods/Close` (PR #112) entries — quick fix in a follow-up.

**Verify-comment:** [#127 PR thread](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/127#issuecomment-4467577626)

### ⏳ PR #116d — UX overhaul finish: Design System + production-app rollout (XL, ~10-12 days) **RE-SCOPED 2026-05-16**

> Dean 2026-05-16: *"The UX overhaul is bugging me and I want to see what a finished product looks like…I want to stick to that discipline."* Re-scoped from "cosmetic pass across top-20 in 2 days" to a proper Best-in-Class finish. The system exists as code BEFORE we apply it; every page lands at a uniform best-in-class bar; the system becomes the foundation every Sprint 2/3 feature inherits from.
>
> **Reference targets (the products users unconsciously compare to):** Datadog × Stripe × Linear. Data-dense (Datadog), trustworthy/financial-grade (Stripe), keyboard-first (Linear). Bloomberg Terminal mood, not B2B CRUD app.
>
> **12-point best-in-class rubric** (current honest score: 5-6 of 12; finish state: 12/12):
> 1. Typography system (4 sizes, 2 weights, mono for numerics/IDs)
> 2. Color tokens (12 semantic) mapped to CSS custom props
> 3. Spacing scale (8 stops: 4/8/12/16/24/32/48/64) — no off-scale paddings
> 4. Primitive library (`<DataCard>`, `<KPITile>`, `<StatusPill>`, `<DataTable>`, `<EmptyState>`, `<SkeletonLoader>`, `<ContextDrawer>`, `<ButtonGroup>`)
> 5. Sparkline on every metric (number alone is a snapshot; number + 7-day trend is intelligence)
> 6. Interaction grammar (hover lift 2px+shadow 120ms, focus ring 2px accent, click scale 0.98 80ms — same everywhere)
> 7. Empty states have heroes (illustration + sentence + CTA)
> 8. Loading states are skeletons (no spinners, no blocking)
> 9. Error states are actionable (specific failure + specific action, never "Something went wrong")
> 10. Keyboard everywhere (Cmd-K + ? overlay shows all shortcuts + J/K nav + Cmd-Enter submit)
> 11. Density toggle (Compact / Comfortable / Spacious)
> 12. Perceived performance <100ms (skeletons <50ms, optimistic UI)

#### Phase 1 — Define the system as code (PR #116d.1, ~3 days) — #116d.1a + #116d.1b SHIPPED 2026-05-17; #116d.1c queued

> **Status 2026-05-17:** Split into three PRs. #116d.1a (foundation + 4 hero primitives + showcase, commit 9f9aee3, PR #179) + #116d.1b (remaining 6 primitives, commit 187d836, PR #180) both live on main. 10 primitives total. #116d.1c (Cmd-K palette hardening) is the immediate next ship.


- [x] `wwwroot/css/tokens.css` — full token system (density modes, luxury shadows, brand glow, glass, Apple-tier motion, reduced-motion). **Shipped #116d.1a.**
- [x] `Pages/Shared/Primitives/_DataCard.cshtml` — universal card, 6 tones, image-left variant + floating glass-blur badges + storyline. **Shipped #116d.1a.**
- [x] `Pages/Shared/Primitives/_KPITile.cshtml` — number + label + sparkline + delta + count-up. **Shipped #116d.1a.**
- [x] `Pages/Shared/Primitives/_StatusPill.cshtml` — 9 tones, dot + mono-num chip. **Shipped #116d.1a.**
- [x] `Pages/Shared/Primitives/_Sparkline.cshtml` — standalone inline-SVG. **Shipped #116d.1a.**
- [x] `Pages/Shared/Primitives/_DataTable.cshtml` — sortable, sticky header, density-aware. **Shipped #116d.1b.** (CSV export hook deferred — caller-driven for now.)
- [x] `Pages/Shared/Primitives/_EmptyStateV2.cshtml` — 9 inline-SVG icons + tone-glow halo + primary/secondary CTA. **Shipped #116d.1b.**
- [x] `Pages/Shared/Primitives/_SkeletonLoader.cshtml` — line / card / table / kpi shapes with shimmer. **Shipped #116d.1b.**
- [x] `Pages/Shared/Primitives/_ContextDrawer.cshtml` — slide-in right drawer + ESC/backdrop close. **Shipped #116d.1b.**
- [x] `Pages/Shared/Primitives/_ButtonGroup.cshtml` — primary / secondary / ghost / danger × sm/md/lg. **Shipped #116d.1b.**
- [x] `Pages/Shared/Primitives/_BrandChip.cshtml` — 14-OEM color palette. **Shipped #116d.1b.**
- [x] Cmd-K palette hardening: integrated existing `command-palette.css` + 53 routes already registered. Re-tinted with design-system tokens, brand-glow on selected, glass-blur backdrop, Ctrl-N/P alternate nav, Cmd-Enter new tab. **Shipped #116d.1c (PR #183 / commit pending main pull).**
- [x] `Pages/Admin/DesignSystem.cshtml(.cs)` — admin-only showcase, density toggle in header, every primitive in every tone. **Shipped #116d.1a.**
- [x] `docs/design-system.md` — rules, primitive reference, color tokens, motion grammar. **Shipped #116d.1a.**

**Acceptance:** showcase page renders every primitive correctly. WCAG AA contrast verified on every token combination. Docs explain the rules. Zero new features yet — system foundation only.

#### Phase 2 — Reference exemplar: Dashboard (PR #116d.2, ~2 days) — PARTIAL SHIPPED 2026-05-17 (PR #184 / commit 2eeae73)

> **Status 2026-05-17:** First slice landed — Maintenance KPIs section replaced with 8 `<KPITile>` primitives + real 7-day sparklines computed from MaintenanceEvents + JournalLines. Tone-driven coloring per metric. Other dashboard sections (6-up quick stats, Reliability Deep-Dive, Recent Assets, Recent Activity, Alerts, Quick Actions, Locations) folded into the Phase 3 page rollout to keep PRs focused.

- [x] Refactor `/` Maintenance KPIs → `<KPITile>` primitives. **Shipped #116d.2 (commit 2eeae73).**
- [x] Sparklines pulled from `JournalLines` (WO-ISS/LBR/ISS-OP/RTN/RTN-OP) + `MaintenanceEvents` day-snapshot replay
- [ ] Refactor the 6-up quick-stats row → `<KPITile>` primitives (queued for next Dashboard pass)
- [ ] Reliability Deep-Dive → `<DataCard>` containing `<DataTable>` (queued)
- [ ] Recent Assets + Recent Activity → `<DataCard>` containing `<DataTable>` (queued)
- [ ] Empty states → `<EmptyState>` v2 (queued)
- [ ] Loading states → `<SkeletonLoader>` (queued)
- [ ] WCAG AA contrast pass on full Dashboard (queued)

**Acceptance for the slice that shipped:** Maintenance KPIs grid looks like Stripe × Datadog. Real sparklines tone-tinted by metric direction. Dean's signoff: "lets go" + brand-luxury direction approved.

#### Phase 3 — Production-app rollout (PR #116d.3 – PR #116d.22, ~5-6 days, micro-PRs)

One page per micro-PR. Each PR: before/after screenshot, only primitives, no new behaviors. Demo-arc order:

| # | Page | Notes |
|---|---|---|
| 3 | `/Plant` | Plant index — KPITile rollups per site |
| 4 | `/Plant/Floor/{id}` | **Plant Floor — PR #117.6 card redesign folds in here.** Cards built from `<DataCard>` + sparkline primitives. OEM imagery committed as part of this PR. Manufacturer color accents. |
| 5 | `/Assets` | Asset register |
| 6 | `/Assets/Asset/{id}` | Asset detail |
| 7 | `/WorkOrders` | WO list |
| 8 | `/WorkOrders/Details/{id}` | WO detail |
| 9 | `/Approvals/Pending` | Approvals queue (PR #115) |
| 10 | `/Reports/Reliability` | Reliability reports |
| 11 | `/Reports/MaintenanceSpend` | Spend report |
| 12 | `/Journals` | Journal list |
| 13 | `/Journals/Details/{id}` | Journal detail |
| 14 | `/Periods` | Period close |
| 15 | `/Purchasing` | PO list |
| 16 | `/Purchasing/Details/{id}` | PO detail |
| 17 | `/AccountsPayable` | AP list |
| 18 | `/AccountsPayable/Details/{id}` | AP detail |
| 19 | `/Vendors` | Vendor list |
| 20 | `/Materials/Items` | Item catalog |
| 21 | `/Inventory` | Inventory list |
| 22 | `/Account/Login` | Login (first impression matters too) |

#### Phase 4 — Accessibility + cross-page consistency (PR #116d.23, ~1-2 days)

- [ ] axe-core CI scan on every page in Phase 3
- [ ] Fix all flagged contrast / keyboard / focus / ARIA issues
- [ ] Screen reader walkthrough (VoiceOver minimum; document as accessibility statement)
- [ ] Cross-page consistency: same primitive identical on every page it appears
- [ ] Density toggle works everywhere
- [ ] Final side-by-side audit of all 20 pages in one PR body

**Acceptance:** every production-app page passes WCAG 2.1 AA, full keyboard nav works, primitives are uniform. The standard is enforced.

#### What folds into this scope from elsewhere

- **PR #117.6 (Plant Floor card redesign)** — no longer a separate PR. Folded into Phase 3 page #4 (`/Plant/Floor/{id}`), built from primitives.
- **PR #138 (WCAG 2.1 AA audit + fixes from Sprint 3)** — partially absorbed by Phase 4. Sprint 3 #138 becomes a re-run audit + per-Sprint-3-feature pass at that point.

#### What does NOT fold in (stays separate)

- **PR #117.7 (Spatial Plant Floor Map)** — net-new feature on top of the system, not a refactor. Builds on Phase 1 primitives.
- **PR #118 (Admin v2)** — net-new functionality (workflow editors, audit explorer). Inherits primitives from #116d.
- **PR #119 (Help + Onboarding overhaul)** — net-new functionality. Inherits primitives.

---

## ✅ PR #117 — Plant Floor Live View + Sensor + AssetHealth (L, shipped 2026-05-16 across 4 sub-PRs)

> Absorbed from Dean's 2026-05-16 brainstorm. The demo killer. Folded in what was Sprint 3 PR #124 (Sensor ingestion + AssetHealth) because Plant Floor View without live data would be theatrical. Ended up shipping in 4 sub-PRs because the original cut required two layers of correction from Dean: "DO NOT HARDCODE DATA" and then "Best in Class Process to Produce a Best In Class product."

- [x] **Sensor ingestion path** (was Sprint 3 #124): `AssetSensorReadings` table is the source of truth; cached `Asset.Current*` columns become a denormalized view written by `AssetSensorService` on each insert. 30-day rolling retention.
- [x] **AssetHealth service**: HealthScore from rule pack (sensor breach + corrective WO frequency + overdue WO count, three penalty caps); populates the previously-decorative `Asset.PredictiveHealthScore + HealthScoreLastCalculated`.
- [x] **Equipment Catalog** (NEW, per Dean's correction): curated `EquipmentClass / EquipmentModel / SensorProfile` tables seeded from `EQUIPMENT_CATALOG.md`. 14 real industrial-equipment classes, ~50 real Mfr+Model combos (Haas VF-2SS, Mazak VARIAXIS i-700, Lincoln Power Wave S350, KUKA KR 210 R2700, Schuler MSP 400, Trumpf TruLaser 5030 Fiber, Aida NS2-2500, DMG MORI NHX 5000, Hexagon Global S, Atlas Copco GA 75 VSD+, etc.). Sensor profiles grounded in ISO 10816-3 / AWS A5.18 / ANSI B11.1 / EPA 40 CFR / OSHA 1910.178.
- [x] **`/Plant`** page: per-plant rollup with Green/Amber/Red counts + avg HealthScore.
- [x] **`/Plant/Floor/{siteId}`** page: per-plant grid of asset cards color-coded by HealthScore. Up to 3 class-appropriate `IsPrimary` sensor tiles per card. Tile tone (ok / warn / crit / muted) driven by `SensorProfile.WarningThreshold + CriticalThreshold + BreachOnHighSide`. Band filter chips (All / Healthy / Watch / Critical).
- [x] **Storyline assets**: three seeded failure narratives (HAAS-VF2 spindle bearing imminent, Lincoln Power Wave S350 arc-voltage drift, KUKA KR 210 R2700 servo overheat) with 7-day rising-trend OOS overlays at 15-min resolution.
- [x] **Chunked sensor insert path**: `AssetSensorService.RecordBatchChunkedAsync` writes ~135K seeded readings in 25K-row chunks, detaches the change tracker between chunks, logs progress per chunk. Cache backfill writes latest Temp/Vib/Pressure to `Asset.Current*` columns.
- [⏳] **Threshold breach → auto-create Corrective WO with FailureCode prefilled** — deferred to PR #117.5 (storyline tuning) and/or follow-up.
- [⏳] **Click-anywhere drill-down modal** with sparklines — base navigation works (card click → `/Assets/Asset/{id}`); sparklines are PR #117.6.
- [⏳] **Live polling (10s) refresh** — deferred; current build is per-page-load (sufficient for demo).

**Sub-PRs that shipped this:**

| Sub-PR | GitHub # | Commit | What |
|---|---|---|---|
| #117.0 | [#129](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/129) | `577b99e` | Plant Floor Live View — color-coded grid + drill-through to asset detail (initial cut, used random health values) |
| #117.0 build-fix | [#131](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/131) | `91fa7d7` | uint→int cast for `0xCAFEF00D` literal |
| #117.0 build-fix-2 | [#132](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/132) | `79430dd` | `MaintenanceEvent.AssetId` non-nullable + decimal/double cast |
| #117.1 | [#133](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/133) | `02673d3` | Real `AssetSensorReadings` table + `AssetSensorService` + rule-based `AssetHealthService` + initial industrial asset seeder (per Dean: "DO NOT HARDCODE DATA, CREATE A TABLE AND SEED IT") |
| #117.1 build-fix | (#117.1 follow-up) | `e64792e` | decimal/double mixing in `IndustrialAssetSeeder` |
| #117.2 | [#135](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/135) | `423fce3` | Equipment Catalog tables (per Dean: "Best in Class Process to Produce a Best In Class product"). Curated `EquipmentClass / EquipmentModel / SensorProfile`. Real Mfr+Model on every asset. Class-appropriate sensor regimes. Three storyline failure narratives. |
| #117.3 | [#136](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/136) | `f495223` | Chunked sensor inserts (25K rows/batch) + density drop (30d × per-profile rate → 14d × 4-hr floor = ~135K rows) + final cache backfill. **Note**: f495223 has stray `<<<<<<<` / `>>>>>>>` conflict markers committed (Replit fixed working tree only). PR #117.4 cleans this up properly. |

**Live verification (2026-05-16):**
- `/Plant` renders 12 sites; Main Manufacturing Plant shows 95% avg health, 287 Green / 28 Amber / 5 Red across 320 assets.
- `/Plant/Floor/1?band=red` shows 5 Critical assets: **HAAS VF-2SS #00001** (Health 30, storyline #1 — Spindle Temp 43.95°C / Vibration 1.80 mm/s / Load 64.91%), **KUKA KR 210 R2700 #00002** (Health 37, storyline #3 — Axis-3 Motor Temp 55.34°C / Servo Current 11.28A / Duty Cycle 63.47%), TRUMPF TruLaser 5030 Fiber, Yaskawa Motoman MA-2010, Fronius TPS 400i. Real Mfr+Model. Real class-appropriate sensor labels with real units. Real numeric values.
- 2 of 3 storyline assets landed in Critical. Lincoln Power Wave S350 didn't — tuning in PR #117.5.

**Verify-comments:** [#129](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/129), [#133](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/133), [#135](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/135), [#136](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/136)

---

## ✅ PR #117.4 — Strip stray merge-conflict markers from main — SHIPPED 2026-05-16

> Reconciled 2026-05-17: commit `a668b94` (#137) removed 207 lines of stray markers from `Services/Seeding/IndustrialAssetSeeder.cs`. Working tree is clean — confirmed by exhaustive grep across all extensions on 2026-05-17 (only matches are decorative `===` dividers in license / audit text / Python seed files, none are real conflict markers).

- [x] Pull `origin/main` into sandbox, sed out the conflict-marker lines from `Services/Seeding/IndustrialAssetSeeder.cs`
- [x] Commit as `fix(catalog): strip stray merge-conflict markers from PR #117.3 (PR #117.4)`
- [x] Push, merge, pull
- [x] `grep -c '<<<<<<<\\|=======\\|>>>>>>>' Services/Seeding/IndustrialAssetSeeder.cs` returns `0` ✓

---

## ✅ PR #117.5 — Bump Lincoln Power Wave S350 storyline into Critical band — SHIPPED 2026-05-16

> Reconciled 2026-05-17: commit `d673d6a` (#138) added +145 lines — overshoot ramp + WO history seeding — that pushes the Lincoln Power Wave S350 into the Critical band alongside the Haas VF-2SS and KUKA KR 210 R2700. Follow-up `7e74dab` (PR #117.5.1, #139) reloaded assets in `BackfillAssetCacheAsync` to avoid an xmin race. Follow-up `74bd6c2` (PR #117.7-numbered, #141) added case-insensitive model lookup in `TryReserve` so the Lincoln storyline actually lands.

- [x] Tune `EmitStorylineOverlay` overshoot + WO history (commit `d673d6a`)
- [x] Fix xmin race in `BackfillAssetCacheAsync` (commit `7e74dab`)
- [x] Case-insensitive `TryReserve` lookup (commit `74bd6c2`)
- [x] Lincoln Power Wave S350 visible in Critical band on `/Plant/Floor/1?band=red` ✓

---

## ⏳ PR #117.6 — Plant Floor Card Redesign (luxury polish + OEM imagery + sparklines) (M, ~1 day) — **FOLDED INTO #116d page #4**

> Dean 2026-05-16: *"These cards look terrible…definitely not the ultra premium luxury user experience we are going for."* Current cards are functional but flat — no imagery, no trends, truncated names, uniform shape, zero hover affordance.

- [ ] **OEM equipment imagery**: commit real product photos to `wwwroot/assets/equipment/<class-code>/<model-slug>.jpg` for every `EquipmentModel.ImageUrl` already declared in the catalog (~50 photos). Source: OEM marketing pages + Wikimedia Commons. Per-image attribution in `wwwroot/assets/equipment/ATTRIBUTION.md` (D2 catalog decision).
- [ ] **Card aspect change** from ~1:1 to ~3:2 with image-left / data-right at desktop; image-top / data-bottom on mobile breakpoint
- [ ] **7-day sparkline behind each sensor pill** — inline SVG (no dep) or Chart.js mini line chart, color-matched to tile tone. Pulls last 84 readings from `AssetSensorReadings` for the sensor's `ReadingType`.
- [ ] **Health rail** — replace 4px left border with a thin top-to-bottom gradient; Critical assets get a subtle red glow.
- [ ] **Hover lift + shadow bloom** — 8px translateY-2px + drop-shadow on `:hover`, 120ms ease.
- [ ] **Manufacturer color accent** on the brand chip: Haas red, Mazak gold, Lincoln red, KUKA orange, FANUC yellow, Trumpf blue, DMG MORI green, Schuler dark-blue, ABB red, Yaskawa blue, Atlas Copco yellow, etc. Color map lives in `EquipmentClass.ts` or a new `wwwroot/css/manufacturers.css`.
- [ ] **Truncation fix**: full Mfr+Model fits on one line. Asset number becomes a small monospaced badge (top-right, above health number).
- [ ] **Card density variation**: Critical assets render at slightly larger size than Healthy ones (or get a wider aspect) to draw the eye to problems first.

**Acceptance:** Side-by-side before/after screenshot of `/Plant/Floor/1?band=red` shows real Haas product photo on the HAAS VF-2SS card with sparklines visible behind the Spindle Temp / Vibration / Load tiles, brand chip in Haas red. Card lifts on hover. Full Mfr+Model name fits without truncation.

> **Note 2026-05-17:** The git commits tagged "PR #117.6" (`85642be`) and "PR #117.7" (`74bd6c2`) were SEEDER follow-ups (5K-row simplification + case-insensitive lookup), not this card-redesign work. Card redesign rolls into #116d Phase 3 page #4 (`/Plant/Floor/{id}`) per the corrected sequencing — built from `<DataCard>` + sparkline primitives so it's consistent with every other page.

---

## ➡️ PR #117.7 — Spatial Plant Floor Map (the demo killer behind the demo killer) (L, ~3 days)

> ➡️ **REROUTED 2026-05-19 → Sprint 19 (5 PRs).** v1.0 launches WITHOUT. Becomes the v1.1 hero feature. Depends on **ADR-019 Asset ↔ WorkCenter ↔ Machine ↔ Department hierarchy** (NEW 2026-05-19, lands as Sprint 14 PR #14.0) and on Plant data from Sprints 14-16. Per `[memory/project_116_reckoning_decisions_2026_05_19.md]`.
>
> Dean's 2026-05-16 vision: *"I was kind of hoping they could move their equipment in the layout of their plant. Like if it was L-Shaped or had walls between Depts."* This is the feature Maximo / SAP PM / Infor EAM can't do. A real factory-floor map with walls, zones, and dragged-and-positioned equipment — color-tinted by HealthScore. Click any asset to drill into its detail. Optional heatmaps for thermal / cycle / breakdown clusters.

- [ ] **Schema**: `SitePlanLayout` (SiteId FK, `LayoutSvgOrJson` text, `GridUnitsX/Y`, `Walls` JSON, `Zones` JSON with names/colors) + `AssetMapPosition` (AssetId FK unique, X / Y / Rotation / Z-index)
- [ ] **Plant Floor Designer** at `/Plant/Design/{siteId}` — drag-drop canvas (SVG or Konva.js). Tools: draw wall, draw zone (named), drag asset icon onto canvas, snap-to-grid toggle. Save layout to DB.
- [ ] **Plant Floor Map** at `/Plant/Map/{siteId}` — read-only render of the saved layout. Each placed asset renders as its OEM thumbnail (from PR #117.6's image library) at its (X, Y), tinted by HealthScore (green/amber/red overlay).
- [ ] **Click an asset on the map** → slide-in drawer with the full asset detail (sensor tiles + work orders + cost + manuals + storyline narrative). Same content as the card drill-down from PR #117.6, just sourced from the map.
- [ ] **Heatmap overlay toggle**: thermal heatmap of average temperature, cycle-count heatmap of stamping presses, breakdown-density heatmap (clusters of corrective WOs in last 90 days). Renders as a translucent gradient layer over the layout.
- [ ] **Layout shapes**: rectangular, L-shaped, T-shaped, multi-floor (tabbed). SVG handles all of it.
- [ ] **Demo seed**: pre-built layout for "Main Manufacturing Plant" with the 320 assets positioned across realistic zones (Stamping Cell, Welding Bay, CNC Toolroom, Press Brake Cell, Quality Lab, Receiving Dock, Forklift Charging, HVAC Mechanical, Compressor Room).

**Acceptance:** `/Plant/Map/1` renders a recognizable factory floor with named zones, walls, and all 320 assets positioned. Hover any asset shows its name; click opens the detail drawer. Heatmap toggle reveals where the breakdowns cluster (overlapping the 5 critical assets visually).

**Differentiator value:** During a demo, after showing the card grid, the rep clicks "Map view." Prospect sees their own (or a similar) factory laid out. The rep toggles "breakdown heatmap." Three red splotches appear on the canvas. *"That's where to walk first tomorrow."* No competitor's product does this.

> **Note 2026-05-17:** The git slot tagged "PR #117.7" (`74bd6c2`) was used for the Lincoln seeder fix. The Spatial Plant Floor Map remains UNSHIPPED and is renumbered logically — comes after #116d primitives land. Builds on the design system tokens + drawer primitive.

---

## ✅ PR #117.8 — Plant page perf fix — SHIPPED 2026-05-16

> Reconciled 2026-05-17: commit `45e91fd` (#142). Moved seeder to startup + bulk-loaded health recompute. `/Plant` median latency dropped from ~6s to ~100ms. Memory `project_pr117_8_shipped.md` captured the diagnosis (seed-on-page-load + per-asset health recompute were the two culprits).

---

## ➡️ PR #118 — Admin v2 rebuild (L) **NEW**

> ➡️ **REROUTED 2026-05-19 → Sprint 12.5 workstream B, PRs #12.5.8 / #12.5.9 / #12.5.10.** Three PRs: approvals UI + audit-log explorer + settings polish.
>
> Absorbed from Dean's 2026-05-16 brainstorm. `/Admin/*` rebuilt on the new design system from PR #116.

- [ ] Information architecture: group admin pages into Organization / Users / Workflows / Integrations / System Settings
- [ ] Replace every "legacy CRUD" admin page with a `<DataCard>`-based detail/list pair
- [ ] Wire the existing `/Admin/Approvals` stub to actually CRUD `ApprovalWorkflow` rows (closes the loop on Sprint 2 PR #115)
- [ ] Sprint 2.1 deferrals fold in: editor for `ApprovalWorkflow` step-chain (Manager → Director → CFO), Vendor Invoice approve wiring, email + webhook on each approval step
- [ ] Audit-log explorer at `/Admin/Audit` with filter-by-entity-type + filter-by-user + drill-through to source

---

## ➡️ PR #119 — Help + Onboarding overhaul (M-L) **NEW**

> ➡️ **REROUTED 2026-05-19 → Sprint 12.5 workstream B, PRs #12.5.11 / #12.5.12.** Two PRs: first-run wizard + Shepherd.js tours / Help hub.
>
> Absorbed from Dean's 2026-05-16 brainstorm. Replaces the thin `/Help/Topic` scaffold with a real onboarding hub.

- [ ] **First-run wizard**: 7-step setup at `/Onboarding/Start` (company info → fiscal calendar → seed depreciation books → invite users → connect webhooks → enable modules → review). Skippable; resumable.
- [ ] **Contextual hints**: Shepherd.js-style guided tours per surface. Toggle per-user "show tips."
- [ ] **Help hub redesign**: `/Help` becomes a searchable knowledge base (~30 articles) with categories, embedded short videos, "was this helpful" feedback, related-articles graph
- [ ] **Empty-state CTAs**: every empty list page links to its corresponding Help article (e.g., empty `/Assets` → "How to import your first asset register")
- [ ] **Checklist-driven adoption tracker**: persistent banner showing "5 of 11 setup steps complete" until dismissed

---

## ➡️ PR #120 — MCP server publishing app data (M)

> ➡️ **REROUTED 2026-05-19 → Sprint 21 Launch Hardening.** Dean: *"save until right before go-live. We don't have a roadmap yet of what/who we want to connect to. We will, but let's wait to do them all at once."* Ships as ONE coordinated package with PR #121. **Action required from Dean before Sprint 21 kicks: specify partner/target list** (Anthropic catalog? OpenAI? Mistral? ERP partners? Slack/Teams?).
>
- [ ] New `Mcp/CherryEamMcpServer.cs` exposing read tools:
  - `list_assets(filter)`
  - `get_asset(id)`
  - `list_work_orders(asset_id?, status?)`
  - `get_wo(id)`
  - `journal_entries_for_wo(id)`
  - `maintenance_spend_by_asset(start_date, end_date)`
- [ ] Write tools (gated by API key + role):
  - `create_work_order(asset_id, type, priority, description)`
  - `log_labor(wo_id, hours, rate)`
  - `issue_part(wo_id, part_id, qty)`
- [ ] Docs + SDK examples (Python + TypeScript)
- [ ] Wire into Anthropic, OpenAI, Mistral MCP catalogs

---

## ➡️ PR #121 — Agentic AI with tool calls (M)

> ➡️ **REROUTED 2026-05-19 → Sprint 21 Launch Hardening (paired with PR #120).** Dean: *"100% doing this once the rest of the infrastructure is in place... I want to automate almost everything with AI if possible as long as we have data integrity."*
>
> **Plus a cross-cutting design principle (2026-05-19):** every Sprint 6-21 PR designs services for AI-callability + voice-context emission + audit-loggable + idempotent. The substrate gets built continuously, then the actual tool-use loop ships in Sprint 21. Per [[project_116_reckoning_decisions_2026_05_19]] decision #3.
>
- [ ] Replace `AiAssistantService.cs` (one-shot LLM call) with a tool-calling loop
- [ ] Tools = same MCP tools from PR #120 (so LLM can read + write the EAM)
- [ ] System prompt = "You are CherryAI, an EAM assistant for asset maintenance and finance teams."
- [ ] Streaming responses (SSE)
- [ ] Per-session conversation history
- [ ] Cost meter per tenant; rate-limit by tenant tier

---

## ➡️ PR #122 — Postgres Row-Level Security (L)

> ➡️ **REROUTED 2026-05-19 → Sprint 12.5 workstream A, PRs #12.5.6 / #12.5.7.** Two PRs: RLS migration + policies, then verification harness that proves cross-tenant denial.
>
- [ ] Enable RLS on every tenant-scoped table (Assets, MaintenanceEvents, JournalEntries, PurchaseOrders, VendorInvoices, etc.)
- [ ] Policy: `USING (company_id = ANY(current_setting('app.visible_company_ids')::int[]))`
- [ ] Wire EF interceptor to set the session variable on every connection acquisition
- [ ] Validate: same tenant query continues to work; cross-tenant query returns zero rows
- [ ] Defense-in-depth: app-layer scoping stays; this is belt + suspenders

---

## ➡️ PR #123 — MFA TOTP (L)

> ➡️ **REROUTED 2026-05-19 → Sprint 12.5 workstream A, PRs #12.5.1 / #12.5.2.** Two PRs: TOTP self-service + trusted devices/lockout. **Enterprise sales blocker** — can't sell to SOC2 customers without it.
>
- [ ] Self-service enrollment: scan QR with Authenticator app
- [ ] Backup codes (8x 10-char, one-time-use)
- [ ] Mandatory MFA flag per tenant; optional per user
- [ ] Trusted devices: remember-me with 30-day cookie
- [ ] Recovery flow: email + backup code

---

## ➡️ PR #124 — SSO via SAML + OIDC (L)

> ➡️ **REROUTED 2026-05-19 → Sprint 12.5 workstream A, PRs #12.5.3 / #12.5.4 / #12.5.5.** Three PRs: SAML 2.0 + OIDC + JIT user provisioning. **Enterprise sales blocker** — can't sell to Okta/Azure-shop customers without it.
>
- [ ] SAML 2.0 (Okta, Azure AD, OneLogin, etc.)
- [ ] OIDC (Google Workspace, Microsoft 365)
- [ ] Per-tenant config: SSO required, JIT user provisioning, role mapping from IdP claims
- [ ] Local-account fallback for break-glass admin

---

# SPRINT 3 — Premium Modules (Parity Moat) 🏆

**Goal:** Build the premium-tier capabilities that justify enterprise pricing and lock in customers.

**Timeline:** 8-12 weeks after Sprint 2.

**Pivot note (2026-05-16):** original Sprint 3 had 15 PRs. PR #124 (Sensor + AssetHealth) folded into Sprint 2 PR #117 Plant Floor View — the plant floor is impossible without live sensor data. Sprint 3 is now 14 PRs, renumbered #125–#138.

---

## ➡️ PR #125 — Calibration scheduling (M)

> ➡️ **REROUTED 2026-05-19 → Sprint 14 Maintenance CC, PR #14.5.** Folds into Maintenance Cockpit. Builds on ADR-019 Asset hierarchy (Sprint 14 PR #14.0).
>
- [ ] `CalibrationSchedule` table per asset
- [ ] Auto-generates Calibration WOs (`Type=Calibration`) at the configured interval
- [ ] Pass/fail capture + cert PDF attachment (uses PR #94 attachments)
- [ ] Cert validity tracking; alerts X days before expiry
- [ ] Standards reference: ASME, OSHA, EPA, FDA, ISO 17025

---

## ➡️ PR #126 — Real OEE module (event-sourced, not synthesized) (L, ~6 days) **UPDATED 2026-05-16**

> ➡️ **REROUTED 2026-05-19 → Sprint 14 Maintenance CC, PR #14.6.** Dean's explicit "real OEE not synthesized" requirement carries forward. Builds on ADR-019 Asset/WorkCenter hierarchy (Sprint 14 PR #14.0).
>
> Dean 2026-05-16: *"Can we do OEE metrics as well for the machines that are actual manufacturing work centers? Like real OEE not synthesized."* Yes — and the schema captures every event that feeds the calculation, the engine computes from events (not typed-in percentages), and when a customer hooks up their MES or PLC the same engine runs on their real shop-floor data. ISO 22400 conformant.

**Real OEE definition:**

OEE = **Availability × Performance × Quality**, where:
- **Availability** = Run Time / Planned Production Time. Sourced from `MachineStateEvent` (RUNNING / IDLE / DOWN / SETUP / BLOCKED) intersected with `WorkCenterCalendar` shift schedule. Not "let's say 85%."
- **Performance** = (Ideal Cycle Time × Total Count) / Run Time. Sourced from `ProductionCount` (cycle-counter ticks → we already have `SensorReadingType.Cycles`) ÷ `PartRouting.IdealCycleSeconds` (engineering spec per part / operation / work center). Not "let's say 90%."
- **Quality** = Good Count / Total Count. Sourced from inspection dispositions in `ProductionCount.GoodCount / ScrapCount / ReworkCount`. Not "let's say 95%."

**Schema (~6 tables):**

- [ ] **`MachineStateEvent`** — AssetId, State enum (Running / Idle / Setup / Blocked / Down / Off), StartedAt, EndedAt nullable, ReasonCodeId FK, ShiftId
- [ ] **`ProductionCount`** — AssetId, JobId, PartNumber, GoodCount, ScrapCount, ReworkCount, ShiftId, RecordedAt
- [ ] **`WorkCenterCalendar`** — AssetId or AssetGroup, ShiftId, PlannedStartAt, PlannedEndAt, Breaks (JSON list of break windows)
- [ ] **`PartRouting`** — PartNumber + OperationSeq → AssetClassCode + IdealCycleSeconds (engineering spec; one routing per (part, operation))
- [ ] **`DowntimeReason`** — code table (Setup, Material Wait, Maintenance, Operator Break, Breakdown, Tool Change, Quality Hold, etc.) with categories aligned to ISO 22400 / SEMI E10
- [ ] **`OeeRollup`** — denormalized per (AssetId, Window: Shift/Day/Week/Month). Stores precomputed A / P / Q / OEE for fast dashboard reads. Recomputed by a background job per shift end.

**Which assets get OEE** (only real work centers — Dean's instinct, confirmed):

| Gets OEE | Doesn't get OEE |
|---|---|
| CNC Machining Center, CNC Lathe, CNC 5-Axis | Material-Handling Robot (serves work centers, isn't one) |
| Welding Robot, Welding Power Source | Conveyor (utility) |
| Stamping Press, Press Brake | Air Compressor (utility) |
| Laser Cutter | Forklift (mobile) |
| | HVAC (facility) |
| | CMM (inspection — gets *its own* utilization metric, not OEE) |

7 of 14 catalog classes get OEE. Marked on `EquipmentClass` via a new `IsWorkCenter` boolean.

**Computation engine:**

- [ ] `OeeService.ComputeForShiftAsync(assetId, shiftId)` — pure function from events. No state, no time-of-day quirks, no opinions baked in.
- [ ] Returns `OeeBreakdown(Availability, Performance, Quality, OEE, RunTime, PlannedTime, IdealCycleTime, TotalCount, GoodCount, DowntimeByCategory)`.
- [ ] Unit-tested with deterministic event fixtures: "given 8-hour shift, 30 min of Setup downtime, 95% good parts, ideal cycle 60s, actual 412 parts in 7.5 hours run time → OEE = X.X%."

**Plant Floor card integration (work centers only):**

- [ ] New 4th tile group on work-center cards: **A / P / Q / OEE** displayed as percentages with color tone (green ≥85%, amber 60-85%, red <60%). Aligned with World-Class OEE benchmarks.
- [ ] Non-work-centers (forklift, HVAC, conveyor, etc.) don't show OEE tiles.

**Per-asset OEE deep-dive page** at `/Plant/Asset/{id}/OEE`:

- [ ] Shift / day / week / month trend chart for A, P, Q, OEE
- [ ] Downtime Pareto: hours by `DowntimeReason` category over the window
- [ ] Top-loss-by-category: which factor (Availability / Performance / Quality) is dragging the most points off ideal
- [ ] CSV + XLSX export

**Integration hooks** (so customers don't have to invent the wire format):

- [ ] `POST /api/v1/oee/state-events` — webhook receiver for `MachineStateEvent` ingestion. JSON schema published.
- [ ] `POST /api/v1/oee/production-counts` — webhook receiver for `ProductionCount` ingestion.
- [ ] Sample integration payloads for: Tulip, Plex, Plataine, MachineMetrics, OPC-UA via Kepware. Documented in `docs/integrations/oee.md`.

**Storyline payoff for demo:**

The HAAS-VF2 spindle-bearing storyline gets a second layer of evidence:

> *"Spindle bearing breach started Day 5 (sensor history). Unplanned downtime spiked from 4% → 22% over the following 9 days (`MachineStateEvent` records). Performance also dropped because cycle time crept up under bearing wear — actual cycle went from 47s → 58s (`ProductionCount`). OEE dropped from 82% → 51% over the same window. If the predictive maintenance alert on Day 5 had been acted on, the asset would have stayed in the Green band and the plant would have avoided ~$XK of lost production and $YK of expedited overnight bearing freight."*

That's the conversation a sophisticated maintenance buyer wants. Maximo's OEE module is bolted-on and lives in a different module than maintenance. SAP's is in a different module than maintenance. Infor's is static. Ours ties OEE *directly* to sensor breaches in the maintenance system. Same asset, same screen, one integrated story.

**Demo seed:**

- [ ] Seed plausible `MachineStateEvent` records for the 7 work-center classes over the last 30 days (1 reading/shift cadence). Storyline assets (Haas VF-2SS, KUKA KR 210, Lincoln Power Wave) get higher-resolution events that align with the existing sensor-failure narratives so OEE drops correlate visibly.
- [ ] Seed `ProductionCount` records: ~3-5 jobs per shift per work center, with realistic part numbers and good/scrap/rework splits.
- [ ] Seed `PartRouting` for the demo part library (~15 parts).
- [ ] Seed `WorkCenterCalendar`: 2-shift schedule, 8am-4pm + 4pm-12am, with break windows.

**Effort breakdown:**
- 2 days: schema + migration + seeders
- 1 day: `OeeService` computation engine + unit tests
- 1 day: Plant Floor card tile group + per-asset OEE page
- 1 day: integration hooks + sample payloads + docs
- 1 day: storyline tuning so OEE drops align with sensor breach narratives

**Standards reference:** ISO 22400 (KPIs for manufacturing operations), SEMI E10 (Equipment reliability / availability / maintainability for semiconductor — adaptable), World-Class OEE benchmarks (Vorne / SMRP).

---

## ➡️ PR #127 — RCM + FMEA (XL)

> ➡️ **REROUTED 2026-05-19 → Sprint 14 Maintenance CC, PR #14.7.**
>

- [ ] RCM analysis workflow per asset: function → functional failure → failure mode → effect → consequence
- [ ] Maintenance task selection: predictive vs preventive vs corrective vs redesign per failure mode
- [ ] FMEA scoring: Severity × Occurrence × Detection = RPN
- [ ] RPN-prioritized task list
- [ ] Output: PM templates auto-generated from RCM analysis

---

## ➡️ PR #128 — Weibull failure analysis (M)

> ➡️ **REROUTED 2026-05-19 → Sprint 14 Maintenance CC, PR #14.8.**
>

- [ ] Per-asset Weibull β / η fitting from FailureCode history
- [ ] β interpretation surfaced: <1 infant mortality, ~1 random, >1 wear-out
- [ ] Recommended PM frequency derived from η (characteristic life)
- [ ] Available as a `/Reports/Weibull?assetId=N` page

---

## ➡️ PR #129 — Vendor performance scorecards (M)

> ➡️ **REROUTED 2026-05-19 → Sprint 13 Purchasing CC, PR #13.5.** Drives the Purchasing CC's AI Suggests strip ("batch with vendor X" surface).
>

- [ ] Per-vendor metrics: On-Time Delivery %, Quality Acceptance %, Price Variance % vs PO
- [ ] Computed from PO → GR → AP joins
- [ ] Surfaced on Vendor detail; sortable list on `/Vendors/Performance`
- [ ] Vendor risk score (composite) drives the AVL preferred-vendor rank

---

## ➡️ PR #130 — Landed cost (L)

> ➡️ **REROUTED 2026-05-19 → Sprint 17 Inventory CC, PR #17.5.** Customs + freight + duties allocated across receipt lines.
>

- [ ] Per-receipt freight + duty + tariff + insurance + handling charges
- [ ] Allocation by line value (default) or line weight (configurable)
- [ ] Updates `Item.WeightedAverageCost` for stock items
- [ ] JE: DR Inventory (with landed cost) / CR GR-Accrued (subtotal) + CR Freight-Accrued + CR Duty-Accrued

---

## 🚫 PR #131 — Tax matrix (XL)

> 🚫 **STAYS V2 (Sprint 24+) — locked 2026-05-19 on best-in-class principle.** v1 positioning is "channel INTO customer's ERP, not replace it." Their ERP handles tax. Defer until post-launch customer demand signal arrives.
>

- [ ] Multi-jurisdictional tax rate table (state, county, city, country, VAT, GST)
- [ ] Tax rule engine: based on ship-to + item taxability + vendor exemptions
- [ ] Auto-applies on PO + Invoice creation
- [ ] Tax-recoverable vs unrecoverable splits
- [ ] 1099-MISC tracking for US vendors

---

## ➡️ PR #132 — Blanket-PO + Contract-PO workflows (L)

> ➡️ **REROUTED 2026-05-19 → Sprint 13 Purchasing CC, PR #13.6.** Master agreement that releases child POs on schedule. Big procurement customers expect this.
>

- [ ] `POType.Blanket` actually does something: parent BPO with releases; each release is a quasi-PO drawing down against committed value
- [ ] `POType.Contract`: pricing agreement with no commitment; tied to AVL preferred-vendor
- [ ] Both: spend visibility, commitment tracking, expiry alerts

---

## 🚫 PR #133 — Payment batch + ACH/NACHA + Positive Pay (L)

> 🚫 **STAYS V2 (Sprint 24+) — locked 2026-05-19.** v1 channels INTO customer ERP for AP/payment. Their AP module bundles + pays. Defer.
>

- [ ] Payment run: select invoices due → generate batch → review → post
- [ ] ACH file (NACHA format) for direct deposits
- [ ] Check print (with MICR encoding)
- [ ] Positive Pay file for bank fraud prevention
- [ ] Bank reconciliation: import bank statement → match to payments → flag exceptions
- [ ] JE: DR AP / CR Cash on each cleared payment

---

## 🚫 PR #134 — OCR invoice ingestion (L)

> 🚫 **STAYS V2 (Sprint 24+) — locked 2026-05-19.** Tempting (matches Dean's AI-first principle) but it's a substantial product surface that muddies "we channel INTO your ERP" positioning. Better as the v2 hero feature after v1 customer demand validates.
>

- [ ] PDF + image upload of vendor invoice
- [ ] OCR via local model (e.g., Tesseract + LayoutLM) or paid API
- [ ] Extracts: vendor name, invoice #, date, line items, totals
- [ ] Auto-suggests PO match
- [ ] Human review/correction step before posting

---

## 🚫 PR #135 — ASC 842 / IFRS 16 lease accounting (XL)

> 🚫 **STAYS V2 (Sprint 24+) — locked 2026-05-19.** Finance compliance lives in customer's ERP. Defer.
>

- [ ] Lease master with terms, payment schedule, discount rate
- [ ] ROU asset + Lease Liability creation at commencement
- [ ] Monthly: amortization of ROU asset + lease liability interest unwind
- [ ] Modification + impairment handling
- [ ] Disclosure report: future minimum lease payments by year

---

## 🚫 PR #136 — Impairment workflow (ASC 360) (L)

> 🚫 **STAYS V2 (Sprint 24+) — locked 2026-05-19.** Same reasoning as #135.
>

- [ ] Trigger events: significant decline in asset value, regulatory change, etc.
- [ ] Two-step test: undiscounted cash flows vs carrying value → fair value vs carrying value
- [ ] If impaired: JE DR Impairment Expense / CR Accumulated Impairment; new depreciation schedule from impaired basis
- [ ] Disclosure report

---

## ➡️ PR #137 — i18n + multi-language localization (L)

> ➡️ **REROUTED 2026-05-19 → Sprint 22 Launch Polish (last sprint before v1.0 LAUNCH).** Dean: *"We'll need English / Spanish / French (Canadian) to start, we can add later. But this can wait until before live."* Three launch languages: **en-US (primary) · es (Spanish) · fr-CA (French Canadian)**. ResX infrastructure + locale-aware Razor + date/number/currency formatting + language picker.
>

- [ ] `IStringLocalizer` injected everywhere user-facing strings exist
- [ ] Resource files: `Resources/SharedResource.en.resx`, `.es.resx`, with translator workflow
- [ ] Per-user language preference; auto-detect from `Accept-Language` for unauthenticated
- [ ] Date / number / currency formatting per culture
- [ ] Future: French, Portuguese, Mandarin

---

## 🟡 PR #138 — WCAG 2.1 AA accessibility audit + fixes (M)

> 🟡 **PARTIALLY SHIPPED + REMAINING REROUTED 2026-05-19.** **Shipped:** PR #213 + #214 (sidebar/toolbar aria-labels, pagination select label, axe-core CI scaffold). **Remaining:** form-labels sweep (~19 violations on ItemEdit + cascading) · color contrast pass on `#tab-basics` · filter-toolbar aria on 8 list pages · CI app-boot hardening across all 22 surfaces → **Sprint 12.5 workstream C, PRs #12.5.13 / #12.5.14 / #12.5.15.**
>

- [ ] axe-core CI scan on every page
- [ ] Fix all flagged contrast, keyboard nav, focus indicator, ARIA mismatch issues
- [ ] Screen reader walkthrough (NVDA / JAWS / VoiceOver)
- [ ] Document accessibility statement at `/accessibility`

---

# SPRINT 12.5 — Enterprise Hardening 🛡️

**Status:** **Locked 2026-05-19 (the 116-reckoning).** 17 numbered PRs in 4 workstreams. Not a catch-all bucket — every PR has an explicit scope.

**Goal:** Close every enterprise-blocker that's been sitting unshipped since the original PR #118-#138 bucket. Sales unblocked for SOC2 / Okta / Azure-AD customers. a11y compliance audit-ready. Tech debt zeroed.

**Workstream A — Security & access (enterprise sales blockers, 7 PRs):**
- [ ] PR #12.5.1 — MFA TOTP self-service (was #123). Authenticator QR + 6-digit + 8 backup codes.
- [ ] PR #12.5.2 — MFA trusted devices + lockout policy. 30-day device-trust cookies, brute-force lockout, admin override.
- [ ] PR #12.5.3 — SSO SAML 2.0 (was #124). Okta + Azure AD generic SAML + metadata-XML import.
- [ ] PR #12.5.4 — SSO OIDC. Google Workspace + Azure AD OIDC.
- [ ] PR #12.5.5 — SSO JIT user provisioning + group mapping. Auto-create on first login.
- [ ] PR #12.5.6 — Postgres RLS migration + policies (was #122). DB-level CREATE POLICY for tenant + site scoping. Defense-in-depth on top of service-layer scoping.
- [ ] PR #12.5.7 — RLS verification harness. Test that proves RLS denies cross-tenant reads even with malicious app-layer query.

**Workstream B — Admin & onboarding (customer launch blockers, 5 PRs):**
- [ ] PR #12.5.8 — Admin v2 approvals UI (was #118). `/Admin/Approvals` CRUD + multi-step sequential chain.
- [ ] PR #12.5.9 — Admin v2 audit-log explorer. `/Admin/Audit` with filter chips + drilldown.
- [ ] PR #12.5.10 — Admin v2 system settings UX polish.
- [ ] PR #12.5.11 — First-run onboarding wizard (was #119). `/Onboarding/Start` 6-step (company → site → vendors → items → users → first PO).
- [ ] PR #12.5.12 — Shepherd.js product tours + Help hub. Inline tours on every Control Center + searchable `/Help`.

**Workstream C — a11y compliance (legal/government customer blockers, 3 PRs):**
- [ ] PR #12.5.13 — Form-labels sweep (was #138 followup). ~19 violations on ItemEdit + cascading.
- [ ] PR #12.5.14 — Color contrast pass. `#tab-basics` and similar to WCAG 2.1 AA.
- [ ] PR #12.5.15 — CI app-boot hardening + axe sweep across all 22 surfaces.

**Workstream D — Tech debt + sparse-page redesigns (2 PRs):**
- [ ] PR #12.5.16 — Sparse-page redesigns. PO Detail + AP Detail + AP List on design system.
- [ ] PR #12.5.17 — Test hygiene + ghost-PR investigation. 9 skipped xUnit tests rewritten + EF snapshot regenerated + Playwright age-check + investigate unaccounted PRs #3 / #15 / #81 / #130.

**Anti-scope for Sprint 12.5:**
- i18n (#137) — moved to Sprint 22 (launch polish). Dean: *"can wait until before live."*
- MCP server + Agentic AI (#120 / #121) — moved to Sprint 21 (launch hardening). Dean: *"save until right before go-live."*
- Sprint 3 premium modules — folded by domain into Sprints 13/14/17 with explicit PR slots.

**Definition of Done:** All enterprise sales-blocker security features live. All onboarding blockers live. WCAG 2.1 AA verified in CI across all surfaces. Zero skipped xUnit tests. Branch list cleaned up.

---

# SPRINT 19 — Spatial Plant Floor Map 🗺️

**Status:** **NEW 2026-05-19 (the 116-reckoning).** Was homeless PR #117.7. Now a dedicated sprint with 5 PRs.

**Why this is its own sprint:** Dean's stated *"demo killer behind the demo killer — no competitor has it."* SAP / Oracle / Maximo / Infor / Plex / Epicor cannot ship a real spatial plant floor map by incremental refactor. Builds on the **ADR-019 Asset ↔ WorkCenter ↔ Machine ↔ Department hierarchy** (NEW 2026-05-19, lands as Sprint 14 PR #14.0) plus Plant data from Sprints 14-16.

**v1.0 LAUNCHES WITHOUT THIS.** Spatial Map is the v1.1 hero feature that closes the post-launch sales gap.

**5 PRs:**
- [ ] PR #19.1 — `SitePlanLayout` + `AssetMapPosition` schema + admin CRUD.
- [ ] PR #19.2 — `/Plant/Design/{siteId}` drag-drop designer (SVG or Konva.js). Tools: draw wall · draw zone · drag asset · snap-to-grid.
- [ ] PR #19.3 — `/Plant/Map/{siteId}` read-only render with OEM-thumbnail tinting by HealthScore (green/amber/red overlay). Click any asset → drawer detail.
- [ ] PR #19.4 — Heatmap overlay toggle. Thermal + cycle-count + breakdown-density (90-day clustered).
- [ ] PR #19.5 — Demo seed: pre-built "Main Manufacturing Plant" layout with 320 assets across realistic zones (Stamping Cell · Welding Bay · CNC Toolroom · Press Brake · QC Lab · Receiving Dock · Forklift Charging · HVAC · Compressor Room).

**Acceptance:** `/Plant/Map/1` renders a recognizable factory floor. Click any asset opens drawer. Heatmap toggle reveals breakdown clusters visually overlapping the 5 critical assets.

**Demo payoff:** *"That's where to walk first tomorrow."*

---

# SPRINT 21 — MCP Server + Agentic AI Launch Package 🤖

**Status:** **NEW 2026-05-19 (the 116-reckoning).** Was ghost PRs #120 + #121 falsely claimed to be "folded into Sprint 5." Sprint 5 is internal voice UI; this is external AI publishing — they're distinct. Now a paired sprint that ships as ONE coordinated launch package right before v1.0.

**Action required from Dean before sprint kicks:** specify the partner / target list. Without that, Sprint 21 has no concrete scope.

**Cross-cutting design principle (2026-05-19):** every Sprint 6-21 PR designs services for AI-callability + voice-context emission + audit-logging + idempotency. The substrate gets built continuously through every CC sprint, then the formal tool-use loop ships here.

**Suite (one launch package, ~6 PRs):**
- [ ] PR #21.1 — `Mcp/CherryEamMcpServer.cs` with read tools (list_assets, get_asset, list_work_orders, get_wo, journal_entries_for_wo, maintenance_spend_by_asset).
- [ ] PR #21.2 — MCP write tools (gated by API key + role): create_work_order, log_labor, issue_part. Plus any tools surfaced by Sprint 13-18 CC voice tools.
- [ ] PR #21.3 — Anthropic / OpenAI / Mistral MCP catalog registration. Docs + SDK examples (Python + TypeScript).
- [ ] PR #21.4 — Claude tool-use loop replacing one-shot `AiAssistantService.cs`. Per-session conversation history. SSE streaming.
- [ ] PR #21.5 — Cost meter per tenant + rate-limit by tenant tier.
- [ ] PR #21.6 — Partner integration shape (Slack / Teams / specific ERPs from Dean's target list).

**Constraint:** Data integrity is non-negotiable. Every AI action audit-logged, reversible, idempotent.

---

# SPRINT 22 — i18n + Launch Polish 🌐

**Status:** **NEW 2026-05-19 (the 116-reckoning).** Was PR #137. Last sprint before v1.0 LAUNCH (Sprint 23).

**Languages at launch:** en-US (primary) · es (Spanish) · fr-CA (French Canadian). Other languages defer to v1.1+ on customer demand.

**~5 PRs:**
- [ ] PR #22.1 — ResX → .resx infrastructure + `IStringLocalizer<T>` everywhere + culture-binding middleware.
- [ ] PR #22.2 — All Razor pages locale-aware (Cockpit primitives + Control Centers + Admin + Help). Externalize every visible string.
- [ ] PR #22.3 — Date / number / currency formatting per locale. Date pickers locale-aware.
- [ ] PR #22.4 — Language picker in user settings + per-tenant default. Spanish + French Canadian translations of all UI strings.
- [ ] PR #22.5 — Launch hardening: final sparse-page pass, final perf sweep, prod telemetry verification, error-page polish, 404/500 page polish.

**Definition of Done:** v1.0-ready in three languages. All launch hardening complete. Ready for Sprint 23 GO-LIVE.

---

# SPRINT 23 — v1.0 LAUNCH 🚀

**Status:** **NEW 2026-05-19.** First production customer.

Activities (not PRs):
- [ ] Customer-1 implementation + data migration
- [ ] Customer-1 user training + onboarding
- [ ] Production telemetry baseline + alert thresholds
- [ ] Post-launch hypercare protocol

---

# SPRINT 24+ — v2 AP/AR Control Center 💰

**Status:** **LOCKED v2 2026-05-19 — best-in-class principle.**

**Reasoning (locked):**
- v1 positioning is "channel INTO customer's ERP, not replace it."
- Customer keeps SAP / NetSuite / Plex / Epicor / D365 / QuickBooks for AP/AR.
- Best-in-class = "do the right things excellently," not "do everything."
- Real customer demand for AP/AR features comes AFTER v1 ships — don't speculate.
- Splitting engineering focus across 40-60 AP/AR PRs in v1 would dilute the 7 Control Centers + voice + ERP-channel.

**Contains (DEFERRED to v2):**
- PR #131 Tax Matrix
- PR #133 Payment Batch + ACH/NACHA + Positive Pay
- PR #134 OCR Invoice ingestion (Dean: "tempting, matches AI-first principle, but better as v2 hero")
- PR #135 ASC 842 / IFRS 16 Lease accounting
- PR #136 ASC 360 Impairment workflow

---

# SPRINT 4 — Mobile v1 📱

> ⚠️ **Sprint number now collides with the active Sprint 4 (Phase F UI rollout).** This section reflects the original Mobile-v1 thinking from 2026-05-16 and is preserved for historical context. **Mobile work in the locked plan now lands as Sprint 20 — Mobile PWA + DataWedge** (post Sprints 13-18 Control Centers, before Sprint 21 Launch Hardening).

**Status:** **Deferred — scope TBD after Sprint 3 wraps.**

**Goal:** Once the production app is feature-complete (Sprint 2 + Sprint 3 done), pick the production surfaces that actually want a mobile experience and ship them as a PWA. Mobile v1 is *not* a port of the entire app — it's a curated selection driven by what field technicians, plant managers, and approvers actually need on their phones.

**Why deferred** (decision logged 2026-05-16, mid-Sprint-2):
- We don't yet know which Sprint 2/Sprint 3 features will survive scope/UX iteration. Building mobile against a moving target is wasted effort.
- The Plant Floor View (Sprint 2 PR #117) will hint at which workflows are inherently visual and would translate well to mobile.
- Approval Hierarchy + SoD (PR #115, done) is the first workflow we *know* will want mobile — managers approving on the go is the obvious use case.

**Original deferred PRs from old Sprint 2** (now re-scoped for Sprint 4):
- Mobile PWA shell (manifest, service worker, offline IndexedDB queue, sync-on-reconnect, conflict detection)
- Live camera barcode + asset lookup (`getUserMedia` + ZXing-JS for QR + Code-128 + Code-39)

**Likely v1 candidates** (to be confirmed at Sprint 4 kickoff):
- Approve / reject from `/Approvals/Pending` on phone
- Tap-to-scan barcode → asset detail + "log a corrective WO"
- Tap-to-scan part barcode → "issue to current open WO"
- Plant Floor View read-only on phone (status check from anywhere)
- Push notifications for approval requests + critical sensor breaches
- Offline-capable WO completion form

**Anti-scope for Mobile v1:**
- Period close, manual JE entry, configuration screens — desktop-only
- Sprint 3 premium modules (RCM, Weibull, lease accounting, etc.) — desktop-only
- Admin pages — desktop-only

---

# 📈 Tracking + check-the-boxes ritual

After every PR merge, I will:

1. Update the **Progress dashboard** at the top of this file (PR count + %)
2. Mark the relevant PR section `## ✅` (currently `##`)
3. Tick each sub-task checkbox `- [x]`
4. Add the PR link to the title: `## ✅ PR #100 — Security hardening sweep ([#100](https://github.com/.../pull/100))`
5. Append the verify-comment URL to the bottom of that PR's section
6. Re-check the per-PR "Live verification checklist" boxes only after running the live probe
7. If anything failed: leave the box unchecked, note "**REOPENED**: reason" inline

---

# 📝 Per-PR template (when I write each new PR body)

```markdown
## What
{What was shipped}

## Why
{One-line linking back to the audit finding (B-NN) or roadmap item (PR #NNN)}

## How
{Numbered list of files touched}

## Will it break existing data?
{Migration-safety statement}

## Verification plan
{Step-by-step live test on Replit}

## Acceptance
{Bulleted, copy from MASTER_PLAN.md}

Refs MASTER_PLAN.md PR #NNN
```

---

# 🚦 Definition of Sprint Done

A sprint is "done" when:
- Every PR in the sprint is checked complete in this file
- Sprint scorecard at the top shows 100%
- No `## PR #NNN — REOPENED` entries remain
- `audit-*.md` detail reports have been re-read; any gap they listed that this sprint claimed to close has been re-verified live

---

**Initiative tally (post-audit 2026-05-18):**
- **Sprint 0**: 10 initiatives shipped ✅ (#100-#109)
- **Sprint 1**: 6 initiatives shipped ✅ (#112, #114, #116, #119, #120, #121)
- **Sprint 2**: 17 initiatives shipped ✅ (#122-#142 covering Approvals + UX overhaul + Plant Floor + #117 sub-PRs)
- **Sprint 2 follow-ups**: 0 of 5 (MFA, SSO, Full RLS, Onboarding wizard, Admin v2)
- **Sprint 3 ADR-012/013 + tests**: 24 initiatives shipped ✅ (#152-#176 + #171/#174/#175)
- **Sprint 3 premium modules remaining**: 0 of 13 (Calibration, OEE, RCM/FMEA, Weibull, Vendor Scorecards, Landed Cost, Tax Matrix, Blanket/Contract-PO, Payment+ACH, OCR, Lease, Impairment, i18n)
- **Sprint 3.5 — Design system + Phase 3 page rollout + Phase 4 a11y W1**: 37 initiatives shipped ✅ (#179-#215)
- **Sprint 3.5 followups**: 0 of 3 (form labels, CI app-boot hardening, sparse-page redesigns)
- **Sprint 4 Phase F UI**: 1 of 14-17 (#178; Wave 1 UI next)
- **Sprint 5 Voice AI Co-Pilot**: 0 of ~20 (signature feature; depends on Sprint 4 surfaces)
- **Sprint 6 Mobile v1**: TBD

**Total: 95 initiatives shipped / ~108 planned ≈ 88% on shipped scope.**
**Raw PR count to main: 212 (Jan→May 2026).**

**Total bug-tasks tracked: 27 (B-01 through B-27)** — all closed in Sprint 0.

**Velocity:** ~12.5 PRs/day average over 17 calendar days. Highly compressed timeline.

This document is the single source of truth. Every PR in flight should reference its row here. If something isn't on this plan and we're working on it: surface that explicitly. **Mobile is intentionally out of scope until Sprint 4 (Phase F) + Sprint 5 (Voice AI) land.**

---

# 📓 Session log — 2026-05-16 (afternoon/evening)

**Code shipped this session:**

- PR #129 — Plant Floor Live View (color-coded grid + drill-through)
- PR #131 — uint→int build-fix for #129
- PR #132 — `MaintenanceEvent.AssetId` non-nullable + decimal/double cast build-fix for #129
- PR #133 — Real `AssetSensorReadings` + `AssetSensorService` + rule-based `AssetHealthService` + initial industrial-asset seeder (Dean's "DO NOT HARDCODE DATA" correction)
- PR #135 — Equipment Catalog tables (Dean's "Best in Class Process to Produce a Best In Class product" correction). 14 classes, ~50 real Mfr+Model combos, 3 storyline failure narratives. Demo flips from "MAZAK CRANE / TRANE FORKLIFT / KUKA PUMP" to real industrial equipment with class-appropriate sensors.
- PR #136 — Chunked sensor inserts (25K-row batches) + density drop (30d/per-profile → 14d/4-hr-floor → ~135K rows) + cache backfill. Plant Floor sensor values populated with real numerics.

**Brainstorm input → roadmap output:**

| Dean said | Captured as |
|---|---|
| "These cards look terrible…ultra premium luxury user experience" | PR #117.6 — OEM imagery + sparklines + manufacturer color accents + hover lift + density variation |
| "I was kind of hoping they could move their equipment in the layout of their plant. Like if it was L-Shaped or had walls between Depts" | PR #117.7 — Spatial Plant Floor Map (Designer + Map + heatmap overlay + drag-drop layout) |
| "Can we do OEE metrics as well…real OEE not synthesized?" | PR #126 — rewritten as event-sourced OEE per ISO 22400. 6 tables. Engine is pure-function from events. Only the 7 work-center classes get OEE. Storyline ties OEE drops to sensor breaches for the demo arc. |

**Process lessons learned (locked into future operating rules):**

1. **No `pkill dotnet`.** Ever. Replit manages the workflow lifecycle. Killing it manually breaks everything in ways that take 30+ minutes to recover.
2. **Replit shell first, Agent only when stuck.** The Replit Agent is expensive; Replit's web shell does the same work for free, and Claude-in-Chrome can drive it directly.
3. **Mac Terminal git push** is the path for pushing local sandbox commits to GitHub (HTTPS via Keychain). Replit's web shell does pulls back from GitHub (uses session-grant OAuth). Don't conflate the two.
4. **Conflict resolution in GitHub web UI**: the JS-driven "Accept current change" loop can leave stray markers in the committed file. Always verify with `grep -c '<<<<<<<' file.cs` before clicking "Mark as resolved." Cost a real bug this session (committed markers in f495223 → PR #117.4 cleanup needed).
5. **Massive seed operations need chunked persistence + try/catch + progress logging.** A naive `AddRange + SaveChanges` for 1.4M rows silently aborts mid-request, leaves a misleading half-state, and is hellish to debug. Default to chunked inserts for any seeder writing > 25K rows.
6. **Per-asset class sensor profiles** (Dean's "we can't have cranes with temp readings") are non-negotiable for credibility. A forklift's primary sensors are Hour Meter / Battery State — not Spindle Temp. Universal sensor schemes are a tell that the system is fake.
7. **Storyline-driven demo data** (Haas VF-2SS spindle bearing, Lincoln Power Wave S350 arc drift, KUKA KR 210 servo overheat) make the difference between "here's some red lights" and "here's a failure unfolding in real time on a real machine you've heard of." Worth the seeding complexity.

— end —
