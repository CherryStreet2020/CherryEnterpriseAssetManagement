---
title: MASTER_PLAN.md Audit + Re-Ordering (2026-05-24)
date: 2026-05-24
status: Locked for Dean review — replaces ad-hoc Priority 1.X numbering with a clean execution spine
sprint: cross-cutting (touches every priority block)
trigger: Dean directive 2026-05-24 after Master Files Baseline insertion — "audit the master plan and put everything in order, including things we have skipped over but still need to resolve"
inputs:
  - "MASTER_PLAN.md (1763 lines, last edited 2026-05-24 by master-files-baseline insertion)"
  - "git log on main HEAD = 177fb53 (PR #5d Operator Workbench + LaborEntry + ReasonCode)"
  - "docs/ADR-*.md inventory"
  - "All memory files + 5 hard locks"
  - "docs/research/master-files-baseline-2026-05-24.md (the trigger for this audit)"
outputs:
  - "This memo (the audit + reordered spine)"
  - "MASTER_PLAN.md updates per §11 below (additive — preserves history)"
  - "Memory: reference_master_plan_audit_2026_05_24.md (pointer)"
---

# MASTER_PLAN.md Audit + Re-Ordering — 2026-05-24

## 0. Executive summary (Dean read-this section)

The plan is **healthy in substance** (12 of 14 Sprint 13.5 main PRs shipped through last night, plus 5 hardening PRs; 8 of 8 Sprint 12D AGE PRs live; Sprint 12C closed; Sprint 12.9 closed at all 10 PRs) but **organizationally entropic**. Eight months of organic growth has produced:

- **31 distinct priority blocks** with numbering jumps (1, 1.5, 1.55, 1.6, 1.605, 1.6075, 1.6080, 1.609, 1.61, 1.62, 1.63, 1.65, 1.66, 1.7, 2, 3, 4, 5, 6) plus inline insertions
- **15 items "shipped" with squash SHAs that differ from current main HEAD** — memory carries the GitHub PR merge SHAs (e.g. `fdc4740` for PR #5d) while actual `main` HEAD = `177fb53`. The PR-vs-main divergence is normal squash-merge behavior, but it makes audit-by-SHA noisy.
- **3 missing ADRs** that are referenced in multiple priority blocks: ADR-019 (Asset↔WorkCenter↔Machine↔Department hierarchy, claimed for Sprint 14 PR #14.0), ADR-023 (multi-modal pipeline for mill certs, queued 2026-05-19), ADR-024 (two-faced — both "Tool Registry & Recommendation Lifecycle" AND "Q3 host migration stub" claim this number).
- **3 sibling ADR-025 files** (service-layer-standard / posting-service-contract / roslyn-analyzer-design) — the same ADR number across three docs. Should be one ADR with three sections OR three separately-numbered ADRs.
- **ADR-026 (Seven Customer Modes Contract)** never written despite being Sprint 13.5 PR #8 scope.
- **7 Sprint 12B deferred-work items** sit inside a single paragraph instead of on the priority spine.
- **5 Sprint 12D follow-ups** (PR #7 perf, PR #8 Q3 stub, recommendation outcomes Item 5, AAS research Item 6, intent-bootstrap PR #270 follow-up) are flagged ⏳ but not slotted.
- **8-PR Master Files Baseline cascade** (PRA-4 through PRA-11) shipped to repo this morning as a memo + Priority 1.66 paragraph insertion — but not as its own top-level priority block. Same for the MES Event Layer cascade (PR #5e/5f/5g).
- **Sprint 12.7 Controller Cockpit** (Blueprint v4 Absorption Item 3, Dean-approved 2026-05-20 as a "1-week net-new sprint" between Sprint 12.5 and Sprint 13) — no priority slot.
- **Sprint 14 PR #14.0 ADR-019** named in the plan but the ADR has never been drafted.
- **Sprint 4 Phase F Wave 1** still sits at Priority 1 with 4 PRs unshipped (RegulatoryProfile admin / MaterialMaster admin / Vendor edit / StockReceipts) — superseded in many ways by Sprint 13.5 surfaces but not formally retired.
- **6 tech-debt items** in the cleanup queue (snapshot regen, 9 skipped xUnit tests, Playwright age-check, branch cleanup, ghost-PR investigation, naming alignment) — no slot.
- **PR #138 a11y audit** marked 🟡 — status unclear.

This memo (a) confirms what's done, (b) surfaces every skipped-or-undeferred item, and (c) renders a clean execution spine so the next 4-6 weeks of work can run without re-deriving sequence every morning.

**Three things Dean needs to call:**

1. **ABS Thursday pre-demo scope.** Memo locks PRA-4 + PRA-5a + PR #5c.4 ship Mon-Wed (Master Files Baseline §1). Confirm.
2. **Sprint 4 Phase F Wave 1 disposition.** RegulatoryProfile admin / MaterialMaster admin / Vendor edit / StockReceipts admin — keep, fold, or retire?
3. **Sprint 12.7 Controller Cockpit timing.** Blueprint v4 Absorption Item 3 was approved as a separate Fortune-100-CFO pitch surface. Where does it actually land? Between 12.5 + 13, or post-EVS, or post-launch?

The rest of this memo is the audit detail + the reordered spine.

---

## 1. State of the union (what is true on 2026-05-24)

### 1.1 Repo facts (verified against git)

| Fact | Value | Source |
|---|---|---|
| `main` HEAD SHA | `177fb53b8d7fa690ada32add95eb115a35d86917` | `git log -1` |
| HEAD subject | `feat(production): Operator Workbench + LaborEntry + ReasonCode (PR #5d)` | `git log -1` |
| HEAD date | 2026-05-24 (early AM Sunday) | `git log -1` |
| Last 15 commits | All Sprint 13.5 work (PRs #296 PRA-1 through #311 PR #5d via squash) | `git log -25` |
| Live URL | https://industryos.app | Replit Autoscale |
| Live deploy | f5252cd8 (auto-deployed after #5d merge) | memory `project_pr_5d_shipped.md` |
| Total merged PRs to main (counted 2026-05-18) | 212 + ~99 since = ~311 | Inferred (last numbered PR is #311) |
| `Migrations/` count | 78+ (last audit) | line 66 of MASTER_PLAN.md |
| `Models/*.cs` count (inventoried today for baseline) | ~140 across 13 subfolders | This morning's master files audit |
| ADRs on disk | 12 files, numbers 013-018, 020-022, 025 (x3) | `ls docs/ADR-*.md` |

### 1.2 Memory drift to correct

| Memory file | Stale fact | Correct fact |
|---|---|---|
| `project_pr_5d_shipped.md` | Merge SHA `fdc4740` | Merge SHA on `main` is `177fb53`; `fdc4740` was the GitHub squash-merge intermediate SHA (the PR's tip pre-merge). Both refer to the same logical commit; only `177fb53` exists on `main`. |
| `project_industryos_current_state.md` | Main HEAD `fdc4740 (post PR #5d)` | Main HEAD `177fb53 (post PR #5d)` — same logical content, correct SHA. |
| `MEMORY.md` line 2 | `Main HEAD: fdc4740 (post PR #5d)` | Same correction. |

These are cosmetic — content is right, SHA is wrong. Updating to `177fb53` in §13 of this memo so subsequent sessions don't get confused.

### 1.3 Sprints / blocks at 100% (verified)

- ✅ **Sprint 0** (10/10 PRs — audit bugs)
- ✅ **Sprint 1** (6/6 — reliability + finance credibility)
- ✅ **Sprint 2** (17/17 — Approvals + UX + Plant Floor + #117 sub-PRs)
- ✅ **Sprint 3 Phases A-E** (24 schema PRs — ADR-012/013 polymorphic backbone)
- ✅ **Sprint 3.5** (37/37 — design system + Phase 3 rollout + a11y Wave 1)
- ✅ **Sprint 11** (7/7 — Receiving Control Center PILOT)
- ✅ **Sprint 11.5** (2/2 — Control-Center-First sidebar)
- ✅ **Sprint 12A** (8/8 — Receiving Cockpit rebuild)
- ✅ **Sprint 12C** (5/5 — Vector Layer)
- ✅ **Sprint 12.9** (10/10 — Control Plane Hardening, last shipped PR #281)
- ✅ **Sprint 12D** (6/8 — Apache AGE; PRs #7 perf + #8 Q3 stub remain ⏳ but the demo headline is LIVE)
- ✅ **Sprint 12B** closed at **2-of-6 done + 1 substrate done + 7 deferred-with-triggers**
- ✅ **Priority 1.6075** (snapshot drift fix shipped PR #271)
- 🟡 **Sprint 13.5** in flight — 12 of 14 main + 5 hardening shipped through `177fb53` (PR #5d)

### 1.4 Sprints / blocks NOT started (queued)

- ⏳ **Sprint 4 Phase F Wave 1** — 4 PRs (RegulatoryProfile/MaterialMaster/Vendor/StockReceipts admin) **status unclear; see §6.1**
- ⏳ **Sprint 5 Voice AI Co-Pilot** — ~20 PRs, partially superseded by Voice MVP shipped in 12B/12C
- ⏳ **Sprint 7/8/9 Item Master + Multi-Dim Inventory** — 18 PRs, partially absorbed into Master Files Baseline PRA-4 (UOM) + PRA-11 (Pack hierarchy)
- ⏳ **Sprint 12.5 Enterprise Hardening** — 17 numbered PRs (MFA/SSO/RLS/Admin v2/onboarding/a11y/tech debt)
- ⏳ **Sprint 12.7 Controller Cockpit** — Blueprint v4 Absorption Item 3, Dean-approved 2026-05-20, ~5 PRs (no slot yet)
- ⏳ **Sprint 13 Full Purchasing CC** — 8 PRs
- ⏳ **Sprint 14 Maintenance CC** — ~9 PRs (folds in Calibration / OEE / RCM+FMEA / Weibull), PR #14.0 requires ADR-019 (not drafted)
- ⏳ **Sprint 15 Planning CC** — ~4 PRs
- ⏳ **Sprint 16 Scheduling CC** — ~4 PRs
- ⏳ **Sprint 17 Inventory CC** — ~5 PRs (folds in Landed Cost #130)
- ⏳ **Sprint 18 Shipping CC** — ~4 PRs
- ⏳ **Sprint 19 Spatial Plant Floor Map** — 5 PRs
- ⏳ **Sprint 20 Mobile PWA + DataWedge** — TBD PRs
- ⏳ **Sprint 21 MCP Server + Agentic AI** — ~6 PRs (needs Dean's partner-list call)
- ⏳ **Sprint 22 i18n + Launch Polish** — ~5 PRs (en/es/fr-CA)
- ⏳ **Sprint 23 v1.0 LAUNCH** — gate
- 🚫 **Sprint 24+ v2 AP/AR** — Tax Matrix / Payment Batch / OCR Invoice / ASC 842 / ASC 360 — **locked V2**

### 1.5 Things INSERTED today but not yet on the priority spine

- **Master Files Baseline cascade** — PRA-4 (UOM) → PRA-5a (COA additive) → PRA-5b (COA segment refactor) → PRA-6 (Currency/PaymentTerm/TaxCode) → PRA-7 (Warehouse/Bin/Lot/Serial/ItemGroup) → PRA-8 (Employee/Wage/Dept→GL) → PRA-9 (PriceList/Discount/Rebate) → PRA-10 (TaxAuthority/TaxRate) → PRA-11 (Pack hierarchy)
- **MES Event Layer cascade** (resumes after baseline lands) — PR #5e (event tables) → PR #5f (Lot/Serial genealogy) → PR #5g (OeeEvent + KPI tiles)
- **PR #5c.4 (tenant-aware seeder)** — queued, slotted AFTER PRA-5a per baseline

---

## 2. Items "skipped over" — surfaced for resolution

This section is the **audit answer** to "things we have skipped over but still need to resolve." Each row has a (a) description, (b) where it currently lives in the plan, (c) why it's been skipped, (d) recommended resolution.

### 2.1 ADRs that should exist but don't

| ADR | Claimed purpose | Currently | Recommended resolution |
|---|---|---|---|
| **ADR-019** | "Asset ↔ WorkCenter ↔ Machine ↔ Department hierarchy" — Sprint 14 PR #14.0 prep PR; foundation for planning, scheduling, OEE, downtime allocation, Spatial Map | Not drafted. Sprint 14 not started. | **Draft as part of PRA-7** (Warehouse + Bin + Lot + ItemGroup) — those tables overlap with WorkCenter-Department-Machine hierarchy. Move from "Sprint 14 PR #14.0" to "PRA-7 sibling doc" and update MASTER_PLAN. |
| **ADR-023** | "Multi-modal pipeline for mill certs" — queued 2026-05-19 (memory `project_database_direction_2026_05_19.md`) | Not drafted. OCR mill-cert work parked in Sprint 12B.2 deferred-work block. | **Park** until 12B.2 trigger fires (customer signs that needs auto-heat-capture). Document the placeholder in a stub ADR file `docs/ADR-023-STUB.md` so subsequent sessions see the reservation. |
| **ADR-024** | Two-faced: (a) "Tool Registry & Recommendation Lifecycle" (Blueprint v4 Absorption Item 2, Sprint 12D PR #2.5 — folded into AGE work); (b) "Q3 host migration stub" (Sprint 12D PR #8 — Q3 2026) | Not drafted. The number is double-claimed. | **Split**: ADR-024 = Tool Registry (the real architectural decision). Renumber the Q3 host migration stub to ADR-027. Document both in `docs/ADR-024-tool-registry-and-recommendation-lifecycle.md` and `docs/ADR-027-q3-host-migration-real-apache-age.md`. |
| **ADR-026** | "Seven Customer Modes Contract" — Sprint 13.5 PR #8 scope; documents the 7 customer modes (pure job-shop, project-with-jobs, ETO turbine, commitment-only, recurring production, joint-venture, internal R&D) | Not drafted despite being the contract that locks how Master Files Baseline + MES events route customer data. | **Draft as Sprint 13.5 PR #8 still** — recommend after PRA-5b (segment refactor) lands, because the AccountingKey segment design needs the 7-mode contract to align. |

### 2.2 ADR-025 trichotomy — clean up

Three files exist at the same ADR number:
- `docs/ADR-025-service-layer-standard.md`
- `docs/ADR-025-posting-service-contract.md`
- `docs/ADR-025-roslyn-analyzer-design.md`

The numbering is inconsistent. ADR convention is one number per decision.

**Recommended:**
- Keep `ADR-025-service-layer-standard.md` as the primary (it's the original anchor)
- Renumber `ADR-025-posting-service-contract.md` → `ADR-025a-posting-service-contract.md` OR move to ADR-028
- Renumber `ADR-025-roslyn-analyzer-design.md` → `ADR-025b-roslyn-analyzer-design.md` OR move to ADR-029
- Simpler call: keep all three as `ADR-025-*` siblings, document the convention in `docs/ADR-INDEX.md` (which should exist anyway).

Low priority — cosmetic. Slotted in tech-debt queue (§6.6).

### 2.3 Skipped Sprint 4 Phase F Wave 1

The plan's Priority 1 (line 125) still lists these as next-up:
1. RegulatoryProfile admin
2. MaterialMaster admin
3. Vendor edit
4. StockReceipts

**Status:** None shipped. Superseded in **substance** by Sprint 13.5 surfaces (PR #4 BIC nav + /Production CC + Master Files admin pages cover similar ground) but not formally retired.

**Question for Dean:** these 4 PRs are now either (a) still needed as separate Wave 1 admin pages, (b) folded into the Master Files Baseline (PRA-5 / PRA-7 add the missing master tables; admin pages become trivially generated), or (c) retired in favor of the Sprint 13.5 nav + admin work. Recommend (b) — fold the Wave 1 admin into PRA-7 (Warehouse/Bin/Lot/Serial) and PRA-8 (Employee/Wage/Dept) admin pages. Retire the Sprint 4 Wave 1 listing.

### 2.4 Sprint 12B deferred-work block (7 items inside a paragraph)

Currently buried inside Priority 1.62 (line 311). Each has a named trigger. Promoting them to the priority spine so they're not lost:

| ID | Title | Trigger to start | Sequenced after |
|---|---|---|---|
| 12B.2 | OCR Mill Cert runtime wiring | First customer that requires auto-heat-capture, OR Sprint 14 Quality work picks up FAI workflow | Sprint 14 OR customer signal |
| 12B.3.1 | Voice-receipt end-to-end smoke test | After Sprint 13 ships (same idempotency layer gets exercised there) | Sprint 13 |
| 12B.4 | DataWedge focus mode on Zebra TC52/TC57 | First customer with Zebra hardware enters pilot (ABS candidate) | Customer signal (ABS pilot post-demo) |
| 12B.5 | Mobile + Zebra responsive cockpit (~3-4 PRs) | After Sprint 13 + 13.5 ship and cockpit pattern is proven across 3+ Control Centers | Post-Sprint-13.5 (now) |
| 12B.6a | `ItemReceivedV2` with chain enrichment | Folds into Sprint 13.5 PR #6 (subcontract chain edges) | **Sprint 13.5 PR #6** (not yet shipped) |
| 12B.6b | GET `/api/v1/receipts` pull endpoint | First ERP-integration customer requires pull over push | Sprint 21 |
| 12B.6c | Per-ERP outbound format adapters (SAP IDoc / Oracle REST / NetSuite / D365) | Per-customer connector lands when customer-pull signal arrives | Sprint 21 |

**Action:** add a "Deferred-Work Register" section to MASTER_PLAN.md that aggregates these (plus other deferred items from other sprints).

### 2.5 Sprint 12D follow-ups not slotted

| ID | Title | Currently | Recommended slot |
|---|---|---|---|
| 12D PR #7 | Performance harness (1000-node traversal p95 < 200ms target) + edge-table failover playbook | ⏳ flagged, no date | Post-ABS (Fri May 29). Low-risk; can ship in parallel with baseline cascade. |
| 12D PR #8 | Q3 host migration ADR-024 stub (now ADR-027 per §2.1) | ⏳ flagged, no date | Q3 2026 (the deadline IS Q3); leave as backlog with reserved ADR number. |
| 12C PR #4 | Voice E2E re-verify + Shadi demo (was "NOW UNBLOCKED" 2026-05-21) | ⏳ flagged, no date | Slot post-ABS — pairs naturally with EVS Jun 3 pitch prep. |
| 12C PR #270 follow-up | Embedding bootstrap: unique EntityIds per prototype so all 12 prototypes land as individual rows | ⏳ backlog, "not blocking June 3" | Sprint 22 launch polish OR when L2 vector coverage gap is felt |
| Absorption Item 5 | Recommendation outcomes table + AI KPI dashboard | Planned Sprint 12D PR #7 (per absorption doc), but Sprint 12D PR #7 is now the perf harness | Renumber to **Sprint 12D PR #9** OR slot in Sprint 21 (Agentic AI launch package — the KPI dashboard is the customer-facing read-out of agent behavior). |
| Absorption Item 6 | AAS / IDTA research spike (2-day, output is `docs/research/aas-idta-investigation.md`) | Backlog, pre-Sprint 19 | Run ~Sprint 17-18 timeframe so Sprint 19 (Spatial Plant Floor Map) starts with the recommendation in hand. |

### 2.6 Sprint 12.7 Controller Cockpit (Blueprint v4 Absorption Item 3) — Dean-approved, no slot

Per absorption doc (memory file referenced as `docs/strategic-absorption-2026-05-20.md`), Sprint 12.7 is a **net-new 1-week 5-PR sprint** that creates a Fortune-100-CFO pitch motion alongside the EVS/Joe AI-APS demo. Scope: Controller asks "why is NBV $1.2M on Asset #4231?" → AI walks JE lines back to Invoice → PO → WO → CapitalProject → AssetBasis → DepreciationRun, narrated naturally, every step a clickable drill link. Lands on `/Controller`.

**Currently:** mentioned inside Priority 1.61 Item 3 paragraph. No top-level slot.

**Recommended:** lands **post-launch (Sprint 24+ window)** OR pulled forward as **post-EVS** (Jun 4-10 window) to test on Joe + similar buyers. Recommend **post-EVS** because the AGE chain-of-custody substrate from Sprint 12D PR #4 + the AccountingKey segment refactor from PRA-5b are the prerequisites and both will be in place by Jun 5. Sprint 12.7 then becomes a natural showcase of the just-landed PRA-5b segment posting chain. Acceptance: live demo on /Controller with the question + voice answer + 5-deep drill chain.

### 2.7 Master Files Baseline cascade — not on priority spine

Inserted today as inline note inside Priority 1.66 Sprint 13.5 row. The cascade is 8 PRs that significantly shape runway. Should be its own **Priority 1.665** block.

### 2.8 MES Event Layer cascade — not on priority spine

PR #5e + #5f + #5g resume after Master Files Baseline. Currently inline note. Should be its own **Priority 1.667** block.

### 2.9 Tech debt + cleanup queue items not slotted

From MASTER_PLAN.md line 427:

| # | Item | Recommended slot |
|---|---|---|
| TD-1 | 9 [Fact(Skip=...)] xUnit tests to rewrite | Sprint 12.5 Workstream D |
| TD-2 | EF model snapshot regen | Shipped PR #271 — mark done in plan |
| TD-3 | Playwright suite age-check | Sprint 12.5 Workstream D |
| TD-4 | Branch cleanup (66 local + 136 remote) | Run NOW (1 hour task, no PR needed; just `git remote prune origin && git branch --merged main | xargs git branch -d`) |
| TD-5 | PR-number gap investigation (#3, #15, #81, #130) | Sprint 12.5 Workstream D — produce 1-page memo "what happened to these PR numbers" |
| TD-6 | Naming alignment cosmetic (WorkOrder/ApprovalWorkflow/VoiceContextEmitter) | Slot in any future PR that touches those files — opportunistic, no dedicated PR |
| TD-7 | **NEW from this audit:** ADR-025 trichotomy cleanup (§2.2) | Sprint 12.5 Workstream D |
| TD-8 | **NEW from this audit:** Memory SHA drift fix (§1.2) | Done in §13 below |

### 2.10 PR #138 a11y audit — status unclear

MASTER_PLAN.md line 1486 shows PR #138 as 🟡. The last a11y sweep was Sprint 3.5 (axe-core CI gate live, WCAG 2.1 AA verified across 22 surfaces). Sprint 12.5 Workstream C (form-labels sweep / color contrast / CI app-boot hardening) is the slot. **Action:** confirm PR #138 is folded into Sprint 12.5 Workstream C and retire the 🟡 status.

### 2.11 PR #117.7 Spatial Plant Floor Map

Was "homeless" per memory. Now lives at Sprint 19. **Confirmed slotted.** No action.

### 2.12 PR #117.6 Plant Floor Card Redesign

Folded into #116d page #4. **Confirmed slotted.** No action.

### 2.13 Strategic Absorption (Priority 1.61) — 9 items, partial completion

Per session-progress block:

| # | Item | Status |
|---|---|---|
| 1 | Marketing reframe "Chain of Evidence from Machine Event to General Ledger" | Backlog (Dean-owned, no code) |
| 2 | Tool Registry v0 — `ai.AgentToolCalls` + `ai.Recommendations` + `ai.AIApprovals` tables | Planned Sprint 12D PR #2.5 — **not yet shipped** despite Sprint 12D being closed at 8 of 8 main PRs. **Likely needs re-slotting to Sprint 21 (Agentic AI launch package).** |
| 3 | Controller Cockpit + source-to-GL voice drilldown | Sprint 12.7 — see §2.6 |
| 4 | "LLM proposes / Domain service executes / Workflow approves / Audit records / Human owns risk thresholds" — ADR-014 D11 amendment | Drafting — **action: confirm the amendment is in the latest ADR-014 doc.** |
| 5 | Recommendation outcomes table + AI KPI dashboard | Planned Sprint 12D PR #7, then displaced by perf harness. See §2.5 — re-slot to Sprint 21. |
| 6 | AAS / IDTA research spike | Backlog pre-Sprint 19. See §2.5 — confirm spike runs ~Sprint 17-18 timeframe. |
| 7 | Vertical Packs framing | Backlog Sprint 22 launch polish |
| 8 | Automated RLS tests + tenant-leak gates | ✅ SHIPPED Sprint 12.9 PR #7 |
| 9 | Control Plane Standardization + Service Layer Standard | ✅ SHIPPED Sprint 12.9 (10 PRs) |

So 2 of 9 absorption items are done. Item 4 needs verification. Items 1, 7 are no-code. Items 2, 5 need re-slotting. Item 3 needs the Sprint 12.7 slot decision. Item 6 needs a calendar slot.

---

## 3. The reordered execution spine (THIS IS THE NEW PRIORITY ORDER)

The 31 organically-numbered priority blocks collapse into a clean chronological execution sequence below. Numbering uses `[Wave N]` to avoid colliding with the legacy "Priority 1.X" labels.

### Wave 1 — IN FLIGHT (started 2026-05-22, ending Sprint 13.5)

| # | Title | Status | Key gate |
|---|---|---|---|
| 1.1 | Sprint 13.5 Manufacturing Domain + CustomerProject + Master Files PRA-1/2/3 | **12 of 14 main + 5 hardening shipped** (PR #5d at `177fb53`) | PR #5c.4 (queued) + PR #6 (subcontract chain + 12B.6a fold) + PR #7 (voice intents) + PR #8 (ADR-026) remain |
| 1.2 | **Master Files Baseline — PRE-ABS slice** (PRA-4 UOM + PRA-5a COA additive + PR #5c.4 seeder) | **NEXT — starts Mon May 25** | **Ships by Wed May 27 pre-ABS** |
| 1.3 | **ABS Thursday demo gate** | — | **Thu May 28** |

### Wave 2 — POST-ABS, PRE-EVS (May 29 – Jun 2)

| # | Title | Status | Key gate |
|---|---|---|---|
| 2.1 | Master Files Baseline — PRA-5b COA segment refactor | Queued | Fri May 29 – Sat May 30 |
| 2.2 | Master Files Baseline — PRA-6 Currency/PaymentTerm/TaxCode masters | Queued | Sun May 31 – Mon Jun 1 AM |
| 2.3 | Master Files Baseline — PRA-7 Warehouse/Bin/Lot/Serial/ItemGroup → PostingProfile | Queued | Mon Jun 1 PM – Tue Jun 2 |
| 2.4 | Sprint 12D PR #7 Performance harness (parallel — small) | Queued | Parallel with PRA cascade |
| 2.5 | **ADR-019 draft** (Asset↔WorkCenter↔Machine↔Department hierarchy) — folded into PRA-7 sibling | Queued | Lands with PRA-7 |
| 2.6 | **ADR-026 draft** (Seven Customer Modes Contract) — Sprint 13.5 PR #8 | Queued | Lands with PRA-5b (segment design needs the contract) |
| 2.7 | Sprint 13.5 PR #6 subcontract chain edges + 12B.6a ItemReceivedV2 | Queued | Slot Sun Jun 1 or Mon Jun 2 |
| 2.8 | Sprint 13.5 PR #7 voice intents (ExplainProjectRisk / WhyIsJobXLate / ListProjectsAtRisk / ProjectCostStatus) | Queued | Slot Tue Jun 2 |
| 2.9 | **EVS Wednesday pitch gate** | — | **Wed Jun 3** |

### Wave 3 — POST-EVS, PRE-CONTROLLER (Jun 4 – Jun 10)

| # | Title | Status | Key gate |
|---|---|---|---|
| 3.1 | Master Files Baseline — PRA-8 Employee + WageGroup + LaborRate matrix + Department→GL profile | Queued | Thu Jun 4 – Fri Jun 5 AM |
| 3.2 | Master Files Baseline — PRA-9 PriceList + Discount + Rebate | Queued | Fri Jun 5 PM – Sat Jun 6 |
| 3.3 | Master Files Baseline — PRA-10 TaxAuthority + TaxRate (effective-dated) | Queued | Sun Jun 7 |
| 3.4 | Master Files Baseline — PRA-11 Pack hierarchy (Each/Inner/Case/Pallet) | Queued | Mon Jun 8 AM |
| 3.5 | **MES Event Layer cascade — PR #5e** event tables (DowntimeEvent/ScrapEvent/ReworkEvent/MaterialConsumption + services) | Queued | Mon Jun 8 – Tue Jun 9 |
| 3.6 | **MES Event Layer cascade — PR #5f** LotGenealogy + SerialGenealogy (built on PRA-7 + ADR-022) | Queued | Tue Jun 9 – Wed Jun 10 |
| 3.7 | **MES Event Layer cascade — PR #5g** OeeEvent rollup + OEE/Throughput/Down-Machines KPI tiles on Production CC | Queued | Wed Jun 10 |

### Wave 4 — Sprint 12.7 Controller Cockpit (Jun 11 – Jun 17)

Per §2.6 — pulled forward post-EVS to use the just-landed AGE chain + AccountingKey segments + posting profiles.

| # | Title | Status |
|---|---|---|
| 4.1 | Sprint 12.7 PR #1 — `/Controller` route + Controller Cockpit shell (composes 4 Cockpit primitives per lock) | Queued |
| 4.2 | Sprint 12.7 PR #2 — source-to-GL drilldown service (walks JournalLine → Invoice → PO → WO → CapitalProject → AssetBasis → DepreciationRun) | Queued |
| 4.3 | Sprint 12.7 PR #3 — voice intent "why is NBV $X on Asset Y" + LLM narration | Queued |
| 4.4 | Sprint 12.7 PR #4 — KPI band: cash position / receivables aging / payables aging / open POs / WIP / unrealized gains | Queued |
| 4.5 | Sprint 12.7 PR #5 — Demo data + walkthrough page | Queued |

### Wave 5 — Sprint 13 Full Purchasing CC (Jun 18 – Jun 24)

8 PRs, unchanged from existing Priority 1.65. Folds in Vendor Scorecards #129 + Blanket/Contract-PO #132.

### Wave 6 — Sprint 12.5 Enterprise Hardening (Jun 25 – Jul 8)

17 numbered PRs in 4 workstreams (A Security, B Onboarding, C a11y, D Tech Debt). Lands AFTER 13.5 / Master Files / 12.7 / 13. Specifically includes:
- **Workstream A** — MFA TOTP / SSO SAML+OIDC / Postgres RLS deepening
- **Workstream B** — Admin v2 (#118) + First-run onboarding wizard (#119)
- **Workstream C** — Form labels (#138 / Sprint 3.5 leftover) + color contrast + CI a11y hardening
- **Workstream D** — Sparse-page redesigns + 9 skipped xUnit tests + Playwright age-check + ghost-PR investigation + ADR-025 trichotomy cleanup + branch cleanup

Sprint 12B deferred-work items 12B.3.1 (voice-receipt smoke) folds into Workstream A (idempotency under contention).

### Wave 7 — Sprint 14 Maintenance CC (Jul 9 – Jul 22)

~9 PRs. Folds in Calibration #125 + OEE #126 (real, not synthesized) + RCM+FMEA #127 + Weibull #128. Prerequisite: ADR-019 (draft scheduled Wave 2.5).

12B.2 (OCR Mill Cert) trigger fires here if Sprint 14 Quality work picks up FAI workflow.

### Wave 8 — Sprint 15 Planning CC (Jul 23 – Jul 29)

~4 PRs.

### Wave 9 — Sprint 16 Scheduling CC (Jul 30 – Aug 5)

~4 PRs.

### Wave 10 — Sprint 17 Inventory CC (Aug 6 – Aug 14)

~5 PRs. Folds in Landed Cost #130.

**Trigger:** Run AAS / IDTA research spike (Absorption Item 6) in parallel — 2 days, output `docs/research/aas-idta-investigation.md`. Pre-Sprint 19 prerequisite.

### Wave 11 — Sprint 18 Shipping CC (Aug 15 – Aug 21)

~4 PRs.

### Wave 12 — Sprint 19 Spatial Plant Floor Map (Aug 22 – Sep 2)

5 PRs. Built on AAS research from Wave 10. Demo-killer-behind-the-demo-killer feature.

### Wave 13 — Sprint 20 Mobile PWA + DataWedge (Sep 3 – Sep 12)

TBD PRs. **Trigger:** 12B.4 DataWedge focus mode AND 12B.5 Mobile responsive cockpit BOTH fold in here.

### Wave 14 — Sprint 21 MCP Server + Agentic AI Launch Package (Sep 13 – Sep 24)

~6 PRs + folds in:
- Absorption Item 2 (Tool Registry tables + ADR-024)
- Absorption Item 5 (Recommendation outcomes + AI KPI dashboard)
- 12B.6b GET `/api/v1/receipts` pull endpoint
- 12B.6c per-ERP outbound format adapters (multi-sprint workstream — first adapter ships here, others customer-pull-triggered)
- **Trigger:** Dean specifies partner/target list BEFORE sprint kick

### Wave 15 — Sprint 22 i18n + Launch Polish (Sep 25 – Oct 5)

~5 PRs (en/es/fr-CA). Folds in:
- Vertical Packs framing (Absorption Item 7)
- PR #270 follow-up (unique EntityIds per intent prototype, deeper L2 vector coverage)

### Wave 16 — Sprint 23 v1.0 LAUNCH (Oct 6+)

Go-live.

### Wave 17 — Sprint 24+ v2 AP/AR (post-launch)

🚫 V2-locked: Tax Matrix #131 · Payment Batch #133 · OCR Invoice #134 · ASC 842 #135 · ASC 360 #136. Customer-pull-triggered.

### Backlog / triggered

- Sprint 4 Phase F Wave 1 (RegulatoryProfile/MaterialMaster/Vendor/StockReceipts admin) — **RETIRED** per §2.3 recommendation, folded into PRA-7 / PRA-8 admin pages
- Sprint 5 Voice AI Co-Pilot ~20 PRs — partially superseded by Voice MVP + Vector L2 + AGE; re-baseline AFTER Sprint 22 to identify what's still missing
- Sprint 7/8/9 Item Master + Multi-Dim Inventory ~18 PRs — partially absorbed into PRA-4 + PRA-11; re-baseline AFTER Master Files Baseline closes
- Sprint 10 Sales-side & Costing depth (OPTIONAL) — Dean greenlight only
- Sprint 12D PR #8 / ADR-027 Q3 host migration to real Apache AGE — Q3 2026 backlog

---

## 4. Cleaned-up "Priority X.X" → Wave map

For the next session reading MASTER_PLAN.md after the cleanup, this is the legend:

| Legacy Priority | → Wave |
|---|---|
| Priority 1 (Sprint 4 Phase F Wave 1) | **RETIRED** (folded into PRA-7/PRA-8 admin) |
| Priority 1.5 (Sprint 11) | ✅ Complete |
| Priority 1.55 (Sprint 11.5) | ✅ Complete |
| Priority 1.6 (Sprint 12A) | ✅ Complete |
| Priority 1.605 (Sprint 12C) | ✅ Complete |
| Priority 1.6075 (Snapshot drift fix) | ✅ Complete |
| Priority 1.6080 (Sprint 12.9) | ✅ Complete |
| Priority 1.609 (Sprint 12D) | ✅ 6 of 8 (PR #7 perf → Wave 2.4; PR #8 Q3 → backlog) |
| Priority 1.61 (Blueprint v4 Absorption — 9 items) | Split: Item 8/9 ✅; Item 3 → Wave 4; Item 2/5 → Wave 14; Item 6 → Wave 10; Items 1/4/7 → no-code backlog |
| Priority 1.62 (Sprint 12B) | ✅ Closed at 2-of-6 + 1 substrate + 7 deferred. Deferred items distributed across Waves 6/13/14 per triggers |
| Priority 1.63 (Sprint 12.5) | Wave 6 |
| Priority 1.65 (Sprint 13) | Wave 5 |
| Priority 1.66 (Sprint 13.5) | Wave 1 (in flight) |
| Priority 1.7 (Sprints 14-18) | Waves 7-11 |
| Priority 2 (Sprint 3.5 followups) | Wave 6 Workstream C/D |
| Priority 3 (Sprint 2 follow-ups) | Wave 6 Workstream A/B + Wave 14 |
| Priority 4 (Sprint 3 premium modules) | Distributed across Waves 5/7/10/15/17 |
| Priority 5 (Sprint 5 Voice AI) | Re-baseline post-Wave-15 |
| Priority 6 (Sprint 7/8/9 Item Master) | Re-baseline post-Master-Files-Baseline (Wave 3 close) |

**NEW slots not yet in the plan:**
- Wave 1.2 — Master Files Baseline pre-ABS slice (PRA-4 / PRA-5a / PR #5c.4)
- Wave 2.1-2.3 — Master Files Baseline post-ABS slice (PRA-5b / PRA-6 / PRA-7)
- Wave 3.1-3.4 — Master Files Baseline post-EVS slice (PRA-8 / PRA-9 / PRA-10 / PRA-11)
- Wave 3.5-3.7 — MES Event Layer cascade (PR #5e / PR #5f / PR #5g)
- Wave 4 — Sprint 12.7 Controller Cockpit (5 PRs)

---

## 5. Dependency graph (which wave blocks which)

```
Wave 1.1 (Sprint 13.5 in flight)
   └→ Wave 1.2 (Master Files Baseline pre-ABS)
         └→ Wave 1.3 (ABS demo)
               └→ Wave 2 (post-ABS / pre-EVS — multiple parallel tracks)
                     ├→ Wave 2.1 (PRA-5b COA segment refactor)
                     │     └→ Wave 2.6 (ADR-026 Seven Customer Modes)
                     │     └→ Wave 4 (Sprint 12.7 Controller — needs AccountingKey segments)
                     ├→ Wave 2.2 (PRA-6 Currency/PaymentTerm/TaxCode)
                     ├→ Wave 2.3 (PRA-7 Warehouse/Bin/Lot/Serial)
                     │     └→ Wave 2.5 (ADR-019 hierarchy)
                     │     └→ Wave 3.5/3.6 (MES events need Lot/Serial masters)
                     │     └→ Wave 7 (Sprint 14 needs ADR-019)
                     ├→ Wave 2.4 (Sprint 12D perf harness — parallel, independent)
                     ├→ Wave 2.7 (Sprint 13.5 PR #6 — subcontract chain edges)
                     └→ Wave 2.8 (Sprint 13.5 PR #7 — voice intents)
                           └→ Wave 2.9 (EVS demo)
                                 └→ Wave 3 (post-EVS / pre-Controller)
                                       ├→ Wave 3.1 (PRA-8 Employee/Wage/Dept→GL)
                                       │     └→ Wave 7 (Sprint 14 needs HR-side)
                                       ├→ Wave 3.2 (PRA-9 PriceList/Discount)
                                       │     └→ Wave 5 (Sprint 13 Purchasing CC uses pricing)
                                       ├→ Wave 3.3 (PRA-10 TaxAuthority/TaxRate)
                                       │     └→ Wave 5 (Purchasing CC uses tax compute)
                                       ├→ Wave 3.4 (PRA-11 Pack hierarchy)
                                       │     └→ Wave 11 (Sprint 18 Shipping CC needs pack)
                                       └→ Wave 3.5/3.6/3.7 (MES events)
                                             └→ Wave 4 (Sprint 12.7 needs MES events on Controller drill)
                                                   └→ Wave 5 (Sprint 13)
                                                         └→ Wave 6 (Sprint 12.5 Enterprise Hardening)
                                                               └→ Wave 7 (Sprint 14)
                                                                     └→ Waves 8, 9, 10, 11, 12, 13, 14, 15, 16
```

---

## 6. Things still requiring Dean's 1-click direction

Beyond the 8 Master-Files-Baseline open questions (memo §9), this audit surfaces **5 additional questions**:

| # | Question | Recommendation |
|---|---|---|
| A1 | Sprint 4 Phase F Wave 1 disposition (§2.3) | RETIRE — fold into PRA-7/PRA-8 admin pages |
| A2 | Sprint 12.7 Controller Cockpit timing (§2.6) | Pull forward to Wave 4 (Jun 11-17, immediately post-EVS) — uses the just-landed PRA-5b segments + AGE substrate |
| A3 | Sprint 14 ADR-019 timing (§2.1) | Draft as PRA-7 sibling (Wave 2.3 / 2026-06-01) so Sprint 14 launches with ADR in hand |
| A4 | ADR-024 double-claim (§2.1) | ADR-024 = Tool Registry (Wave 14 / Sprint 21); new ADR-027 = Q3 host migration to real Apache AGE |
| A5 | ADR-025 trichotomy cleanup (§2.2) | Cosmetic — slot in Wave 6 Workstream D (Sprint 12.5 tech debt). Document convention in `docs/ADR-INDEX.md`. |

---

## 7. Acceptance criteria — when is the plan "in order"?

After this audit + the MASTER_PLAN.md updates per §11 below:

- [x] Every shipped block has a verified SHA (and the memory SHA drift is corrected)
- [ ] Every queued block has a Wave number in execution order
- [ ] Every deferred item has a named trigger
- [ ] No item is "skipped without resolution" — each surfaced gap (§2.1–§2.13) has a recommended action
- [ ] Master Files Baseline is its own priority block in MASTER_PLAN
- [ ] MES Event Layer cascade is its own priority block in MASTER_PLAN
- [ ] Sprint 12.7 Controller Cockpit has a Wave slot (pending Dean's call A2)
- [ ] ADRs 019/023/024/026 have a path (drafted, stubbed, or backlogged with reason)
- [ ] Tech debt items each have a Wave slot
- [ ] Dean has signed off on the 5 audit questions in §6

When all 9 boxes are checked, the plan is in order. As of this memo: 1 of 9.

---

## 8. What this audit does NOT do

- Does not re-litigate Dean-locked decisions (terminology / CC quality bar / reuse primitives / no shortcuts / Replit auto-diff / 5 hard memory locks)
- Does not re-baseline Sprint 5/7/8/9 — that's a separate exercise after Master Files Baseline + MES events close
- Does not retire any shipped work
- Does not change the ABS Thursday + EVS June 3 demo dates
- Does not modify the v2-locked AP/AR items (Tax/Payment/OCR/ASC842/ASC360)
- Does not introduce new sprints that weren't already implied by absorption / hidden-work items

---

## 9. References

- `MASTER_PLAN.md` — the document this audit re-orders
- `docs/research/master-files-baseline-2026-05-24.md` — the trigger memo
- `docs/research/master-files-audit.md` — superseded by baseline
- `docs/research/item-master-and-multi-dim-inventory.md` — Sprint 7-9 plan (partially absorbed)
- `docs/strategic-absorption-2026-05-20.md` — Blueprint v4 Absorption (9 items)
- `Memory: project_sprint_13_5_status` — sprint runway
- `Memory: project_mes_gap_analysis` — MES cascade context
- `Memory: reference_master_files_baseline` — new baseline pointer
- All ADRs in `docs/ADR-*.md` (13/14/15/16/17/18/20/21/22/25 ×3)
- Missing ADRs flagged in §2.1: 019, 023, 024 (with renumber to 027), 026

---

## 10. Memory pointer

A companion memory file `reference_master_plan_audit_2026_05_24.md` is being written alongside this memo so the reordered spine + the 5 open audit questions survive memory wipes.

---

## 11. MASTER_PLAN.md updates being made now

Surgical edits to preserve history while installing the audit findings:

1. **Add `📋 Master Plan Audit 2026-05-24` block** at top of Next-Up Queue (line ~113) pointing to this memo + the 5 open audit questions
2. **Promote Master Files Baseline** to its own Priority 1.665 block between 1.66 and 1.7
3. **Promote MES Event Layer cascade** to its own Priority 1.667 block between 1.665 and 1.7
4. **Promote Sprint 12.7 Controller Cockpit** to Priority 1.7a (post-EVS slot)
5. **Promote Sprint 12B Deferred-Work Register** to a clean table at end of Priority 1.62
6. **Mark Priority 1 (Sprint 4 Phase F Wave 1) as RETIRED** with note "folded into PRA-7/PRA-8"
7. **Annotate ADR-019/023/024/026 status** in the cleanup queue (line 427)
8. **Add `Wave N` annotation** to each Sprint row in the dashboard so the execution order is visible at a glance
9. **Update HEAD SHA reference** from `fdc4740` → `177fb53` (cosmetic; same logical commit)

---

## 12. Sign-off

This audit was performed as a single-session exercise on 2026-05-24 immediately after the Master Files Baseline memo. It reads (a) MASTER_PLAN.md in full, (b) git log on main HEAD = 177fb53, (c) all ADRs on disk, (d) all relevant memory files. The reordering is grounded in (i) the 5 hard memory locks, (ii) the BIC entity checklist, (iii) the ABS Thursday + EVS June 3 demo dates as fixed boundaries.

Awaiting Dean's call on the 5 §6 audit questions + the 8 Master Files Baseline §9 questions. Once both sets are answered, the queued PR work resumes Mon May 25 AM with PRA-4 (UOM master).

---

## 13. Memory SHA drift correction (filed alongside this memo)

The following memory files reference `fdc4740` as the Sprint 13.5 PR #5d merge SHA. The actual main HEAD is `177fb53` — the squash-merge produces a different SHA on the target branch than the PR's tip pre-merge. Files to update:

- `MEMORY.md` line 2 — change `fdc4740 (post PR #5d)` to `177fb53 (post PR #5d)`
- `project_industryos_current_state.md` — same
- `project_pr_5d_shipped.md` — note both SHAs ("PR squash-tip `fdc4740` → main HEAD `177fb53`")
- `project_sprint_13_5_status.md` line for PR #5d — same dual-SHA note

Filed in §13 of this memo so subsequent sessions correct themselves.
