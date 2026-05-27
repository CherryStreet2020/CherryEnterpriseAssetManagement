---
title: MASTER_PLAN.md Audit + Re-Ordering (2026-05-27)
date: 2026-05-27
status: Canonical sequence reference for B6/14.x/B8/B7/B9 cascade — supersedes master-plan-audit-2026-05-24.md
sprint: cross-cutting (touches B6 Foundation closeout + post-B6 cascade + Theme B7/B8/B9 sequencing)
trigger: Dean directive 2026-05-27 AM session 10 kickoff — refresh audit after B6 Foundation completion + Sprint 14.1/14.2/14.3 substrate ships
inputs:
  - "git log on main HEAD = 813d399 (PR #367 Sprint 14.3 PR-1 ECR/ECO Change Control substrate)"
  - "Walked Models/ (84 top-level files + 14 sub-dirs), Services/ (40 top-level + 35 sub-dirs), Pages/Admin/ (43 top-level pages + 13 sub-dirs), Migrations/ (165 files)"
  - "AppDbContext.cs xmin/IsRowVersion audit (38+ MapXminRowVersion calls; 3 remaining IsRowVersion() latent bugs at lines 2701/2754/2816)"
  - "docs/research/po-cockpit-spec-2026-05-26.md (Theme B8 spec, 299 lines)"
  - "docs/research/project-management-fields-and-functions-2026-05-26.md (Theme B9 spec)"
  - "docs/research/b6-foundation-sprint-design-2026-05-26.md (B6 cascade design)"
  - "docs/research/item-master-b6-audit-synthesis-2026-05-26.md (B6 audit synthesis)"
  - "docs/research/master-plan-audit-2026-05-24.md (prior audit, the 16-Wave spine baseline)"
  - "All ~85 memory files including the 3 new HARD LOCKS encoded since prior audit"
outputs:
  - "This memo (refreshed audit + reordered spine)"
  - "Memory pointer reference_master_plan_audit_2026_05_27.md (replaces stale 05-24 reference)"
  - "MEMORY.md trimmed + repointed (over budget at 27.7KB; pulls verbose project_pr*.md entries down to one-line index format per the warning)"
---

# MASTER_PLAN.md Audit + Re-Ordering — 2026-05-27

## 0. Executive summary (Dean read-this section)

**B6 Foundation Sprint is CLOSED** (10 ships over one session day 2026-05-26: PR #355 → #356 → #357 hotfix → #358 → #359 → #360 → #361 → #362 → #363, plus inserted backfills). Item Master fully classifiable + multi-plant capable + 8-element cost component split + FIFO/LIFO/Average inventory valuation + customer item cross-reference + Source List sourcing + 18-column expansion all live and E2E-verified on dev preview.

**Post-B6 cascade has 3 substrate ships in the bank** (2026-05-26 evening → 2026-05-27 early):
- **Sprint 14.1 PR-1** (PR #364 + PR #365 xmin hotfix) — per-PO frozen BOM snapshot (ProductionMaterialStructure + IPoSnapshotService).
- **Sprint 14.2 PR-1** (PR #366 + fixup 645ec5c) — DMS (Document + DocumentVersion + ItemDocumentLink + IDocumentService) — controlled engineering artifacts with revision lifecycle + atomic supersede on release.
- **Sprint 14.3 PR-1** (PR #367 + fixup 97e3008) — ECR/ECO Change Control (EngineeringChangeRequest + EngineeringChangeOrder + EcoLineItem + EcoApproval + IEcrEcoService) — full Engineering Change Management with multi-stage approval + effectivity rules + atomic DocumentVersion supersede integration.

**Main HEAD: `813d399`.** Three consecutive ships with zero hotfix needed after PR #365 (the xmin pattern was applied prophylactically on PR #366 + PR #367, validating the new HARD LOCK). The probes-exercise-writes corollary caught what no unit test could (cross-tenant gap on PR #366; in-order-approval enforcement + FAI inheritance live on PR #367).

**Two latent bugs known + 1 architectural gap identified:**
1. **xmin backfill backlog** — 3 entities (`CustomerItemXref` PR #362, `ItemSourcingRule` PR #361, `CostLayer` PR #360) still ship `e.Property(x => x.RowVersion).IsRowVersion()` on `bytea NOT NULL` per the AppDbContext walk (lines 2701/2754/2816). They will throw PG 23502 on first INSERT. The existing probes for these are read-or-update-only (Resolve/Supersede/Obsolete bypass the INSERT path), so the bug is dormant. **§4 below recommends inserting a defensive bundled PR-XminBackfill before any write-path UI touches one of those services.**
2. **Sprint 14.3 PR-1 was substrate (~30h); spec scope is ~80-100h** — PR-2 through PR-7 (Deviation/Waiver/Concession + Customer Notice + Supplier PCN + CAR/CAPA + impact analysis + redline + FAI re-trigger) remain. **§3 below sequences these vs. B8 PO Cockpit cascade.**
3. **B8 PO Cockpit cascade ready to launch** — all three substrate prerequisites (14.1 + 14.2 + 14.3 PR-1) cleared. Spec is locked at 12 PRs / 172-240h. **§3 makes B8 the next major cascade, ahead of 14.3 PR-2..7 completion.**

**Demo runway:**
- ✅ ABS Machining demo is **TODAY (Thursday May 28)**. Prod (industryos.app) verified live with CFO motion (`/Controller`) + COO motion (`/CustomerProjects/Index`) + FAI UI. Sprint 14.x substrate ships are code-only (Lock 17) → dev preview only → cannot break demo runway.
- ⏳ EVS Wednesday pitch is **June 3** (per the original 16-Wave spine Wave 2.9). **§5 below confirms the cascade hits EVS with B8 PR-PO-1+PO-2 customer-facing UI live or with the Sprint 14.3 PR-2..3 admin-side ECR/Deviation lifecycle live.**

**Three things Dean needs to call** (these are §6 audit questions):

1. **B8 vs Sprint 14.3 PR-2..7 sequencing.** Recommend launching B8 PR-PO-1 (~10h header expansion) IMMEDIATELY after the xmin backfill PR, then ALTERNATING B8 ships with Sprint 14.3 PR-2..7 ships so the cockpit is being built in parallel to the change-control surface. Confirm or override.
2. **xmin backfill — bundled PR or per-touch fix.** Recommend a single ~1.5h defensive PR `feat(xmin-backfill): convert CostLayer + ItemSourcingRule + CustomerItemXref to xmin pattern` AHEAD of any write-path UI. The latent bug is identical 3×, so fixing all 3 in one PR is cheaper than per-touch.
3. **Original Wave 7 (Sprint 14 Maintenance CC: Calibration / OEE / RCM+FMEA / Weibull) renumber.** The label "Sprint 14" is now claimed by the B6/14.x post-foundation cascade. Recommend renumbering the legacy Sprint 14 Maintenance CC to **Sprint 18 Maintenance CC**, slotted post-B8 cascade close. Confirm.

The rest of this memo is the audit detail + reordered spine.

---

## 1. State of the union (what is true on 2026-05-27 AM)

### 1.1 Repo facts (verified against git + filesystem walk)

| Fact | Value | Source |
|---|---|---|
| `main` HEAD SHA | `813d399` | `git log -1 --oneline` |
| HEAD subject | `feat(14.3-1): ECR/ECO Change Control substrate (#367)` | `git log -1 --oneline` |
| HEAD date | 2026-05-27 (early AM Wednesday) | `git log -1` |
| Last 5 commits (B6 close + 14.x cascade) | 813d399 / 8350a0f / e7dd547 / e79da2d / eaff0dc | `git log --oneline -5` |
| Live prod URL | https://industryos.app | Replit Autoscale (untouched since PR #354 Republish-with-Copy 2026-05-26 PM) |
| Live dev preview URL | https://e8802fe3-…replit.dev/ | Lock 17 E2E target — all 3 14.x substrate ships verified live here |
| `Migrations/` count | **165** files (~82 chronologically; the rest are `.Designer.cs` pairs) | `ls Migrations/*.cs \| wc -l` |
| `Models/` top-level | **84** entity files | `ls Models/*.cs \| wc -l` |
| `Models/` sub-dirs | **14** (AssetImport / Catalog / ChainOfCustody / Embeddings / Engineering / Infrastructure / Masters / Navigation / Production / Projects / Quality / Revisions / Telemetry / WorkOrders) | `find Models -mindepth 1 -maxdepth 1 -type d` |
| `Services/` top-level | **40** service files | `ls Services/*.cs \| wc -l` |
| `Services/` sub-dirs | **35** (AccountsPayable / Admin / Approvals / AssetImport / ChainOfCustody / Cip / Controller / Diagnostics / Engineering / Finance / Forms / Health / Infrastructure / Integrations / Items / Lookups / Maintenance / Masters / Naming / Navigation / Posting / Production / Projects / Purchasing / Quality / RateLimiting / Receiving / Reliability / Revisions / Seeding / Telemetry / Testing / Voice / Webhooks / WorkOrders) | `find Services -mindepth 1 -maxdepth 1 -type d` |
| `Pages/Admin/` top-level | **43** `.cshtml` pages | `ls Pages/Admin/*.cshtml \| wc -l` |
| `Pages/Admin/` sub-dirs | 13 | `find Pages/Admin -mindepth 1 -maxdepth 1 -type d` |
| Test files | 72 | `find Tests tests -name "*.cs" \| wc -l` |
| Top-level ADRs | 12+ (013-018 / 020-022 / 025 trichotomy / 028 = parent-child FK) plus the lowercase/numbered `docs/adr/ADR-*.md` set | `ls docs/ADR-*.md docs/adr/*.md` |

### 1.2 What's NEW on disk since the 2026-05-24 audit

**Engineering substrate (Models/Engineering/) — 5 entities, ALL net-new since the prior audit:**
- `Document.cs`, `EcoApproval.cs`, `EcoLineItem.cs`, `EngineeringChangeOrder.cs`, `EngineeringChangeRequest.cs` (DocumentVersion + ItemDocumentLink classes live inside Document.cs per the substrate file inspection)

**Masters/ expansion — net-new since prior audit:**
- `CostLayer.cs` (PR-FS-4)
- `CustomerItemXref.cs` (PR-FS-6)
- `ItemGroup.cs` ✅ existed prior, FK wire-up in PR-FS-1
- `ItemSite.cs` (PR-FS-2)
- `ItemSourcingRule.cs` (PR-FS-5)
- `ItemStandardCostElement.cs` (PR-FS-3)
- Plus all PRA-* masters that landed pre-audit-baseline (UoM/BIN/LOT/Pack/PriceList/Tax/Wage/LaborRate/Calendar/Country/etc.)

**Production/ expansion — net-new since prior audit:**
- `ProductionMaterialStructure.cs` (14.1 PR-1) — per-PO frozen snapshot
- `MaterialStructure.cs` + `MaterialStructureLine.cs` (Sprint 13.5 PR #5c) — kept as snapshot capture source

**Services/Items/ explosion — net-new since prior audit:**
- `CostLayerService.cs` + `ICostLayerService.cs` (PR-FS-4)
- `CustomerItemXrefService.cs` + `ICustomerItemXrefService.cs` (PR-FS-6)
- `IItemGroupResolver.cs` + `ItemGroupResolver.cs` (PR-FS-1 + PR-FS-1.5.1 source-aware resolver)
- `IItemMasterReader.cs` + `ItemMasterReader.cs` (PR-FS-7)
- `IItemSiteResolver.cs` + `ItemSiteResolver.cs` (PR-FS-2)
- `IItemSourcingRuleService.cs` + `ItemSourcingRuleService.cs` (PR-FS-5)
- `IItemStandardCostService.cs` + `ItemStandardCostService.cs` (PR-FS-3)
- `BuyabilityScoreService.cs` — net-new MakeBuyCode-driven scoring (Theme B7 substrate dropped quietly?)
- `EffectiveProcurementService.cs` — net-new sourcing rule application (downstream of PR-FS-5)
- `PreferredVendorCatalogResolver.cs` — net-new vendor preference resolution (downstream of PR-FS-5 + Item.PrimaryVendorId fallback path)

**Services/Engineering/ — 4 net-new files:**
- `DocumentService.cs` + `IDocumentService.cs` (14.2 PR-1)
- `EcrEcoService.cs` + `IEcrEcoService.cs` (14.3 PR-1)

**Services/Production/ expansion — net-new since prior audit:**
- `PoSnapshotService.cs` + `IPoSnapshotService.cs` (14.1 PR-1)
- `BackwardScheduling/IBackwardSchedulingService.cs` (PR #350 stub — Sprint 14 swaps impl)

**Services/Controller/ — Sprint 12.7 substrate confirmed live:**
- `ChainTraceService.cs` (PR #346) — walks Asset→Cip + JE→reverse-Cip-origin honest-answer
- `FinanceKpiService.cs` + `IFinanceKpiService.cs` (PR #348) — real-data wire-up
- `IControllerCockpitService.cs` (PR #346)

**Services/Seeding/ — Demo motion seeders live:**
- `CfoMotionDemoSeeder.cs` (PR #353 — tenant-generic, customer-name-revert hotfix)
- `CooMotionDemoSeeder.cs` (PR #354 — 136-row Phase + BOM + Routing + 36-op release)

**Pages/Admin/ probes — 11 substrate probes confirmed live on disk:**
- `PoSnapshotProbe.cshtml` (14.1)
- `DocumentProbe.cshtml` (14.2)
- `EcrEcoProbe.cshtml` (14.3 — 8 write buttons)
- `ItemMasterExpansionProbe.cshtml` (PR-FS-7)
- `ItemSiteProbe.cshtml` (PR-FS-2)
- `ItemStandardCostProbe.cshtml` (PR-FS-3)
- `CostLayerProbe.cshtml` (PR-FS-4)
- `ItemSourcingProbe.cshtml` (PR-FS-5)
- `CustomerItemXrefProbe.cshtml` (PR-FS-6)
- `BackfillItemGroups.cshtml` (PR-FS-1.5)
- `BackfillItemSource.cshtml` (PR-FS-1.5.1)
- `SeedCfoMotionDemo.cshtml` (PR #353)
- `SeedCooMotionDemo.cshtml` (PR #354)

**Migrations/ — last 15 chronologically (the post-baseline-audit cascade):**
- `20260525163613_AddAssetImportTables_PR337` (asset import pre-B6)
- `20260526135013_AddProductionOrderCostsAndParentFk_Sprint128Pr1` (PR #349)
- `20260526183900_AddItemItemGroupIdFsPr1` (PR-FS-1)
- `20260526202723_AddItemSiteFsPr2` (PR-FS-2)
- `20260526204540_AddItemStandardCostElementFsPr3` (PR-FS-3)
- `20260526211151_AddCostLayerFsPr4` + `20260526212620_AddCostLayerRowVersionFsPr4P1` (PR-FS-4 + Codex P1 fix)
- `20260526214236_AddItemSourcingRuleFsPr5` + `20260526215137_AddItemSourcingRuleTransferFkFsPr5P2` (PR-FS-5 + Codex P2 fix)
- `20260526220538_AddCustomerItemXrefFsPr6` (PR-FS-6)
- `20260526222644_AddItemMasterExpansionFsPr7` (PR-FS-7, closes B6 Foundation)
- `20260526231956_AddPoSnapshotFs141Pr1` + `20260526235021_DropPoSnapshotBytesRowVersionFs141Pr1P1` (14.1 PR-1 + xmin hotfix)
- `20260527001156_AddDmsSubstrateFs142Pr1` (14.2 PR-1)
- `20260527093511_AddEcrEcoChangeControlFs143Pr1` (14.3 PR-1)

**This is a 14-migration spurt over a 24-hour window — the most productive period in the project so far.**

### 1.3 What's COMPLETE (verified against the codebase walk)

- ✅ **B6 Foundation Sprint** (PR #355 → PR #363, 10 ships total) — Item Master fully classifiable + multi-plant + 8-element cost split + FIFO/LIFO/Average inventory valuation + customer xref + sourcing rules + 18-column expansion
- ✅ **Sprint 14.1 PR-1** (PR #364 + PR #365) — per-PO frozen BOM snapshot
- ✅ **Sprint 14.2 PR-1** (PR #366) — DMS substrate
- ✅ **Sprint 14.3 PR-1** (PR #367) — ECR/ECO Change Control substrate
- ✅ **Sprint 12.7 Controller Cockpit** (PRs #346-#348) — chain trace + finance KPI + voice intent live
- ✅ **CFO + COO Motion Demo seeders** (PRs #353 + #354) — both live on dev + Republish-with-Copy verified live on prod 2026-05-26 PM

### 1.4 What's QUEUED (the next-phase work, in execution order)

| Wave | Title | Status | Est. effort |
|---|---|---|---|
| **A** | **PR-XminBackfill** — defensive 1.5h cleanup. Drop bytea RowVersion + remap to xmin on CostLayer + ItemSourcingRule + CustomerItemXref (3 entities, identical fix per the AppDbContext walk at lines 2701/2754/2816). | RECOMMENDED INSERT | 1.5h |
| **B** | **B8 PO Cockpit cascade — PR-PO-1** (header field expansion: 8 OrderType variants + 8-state status machine + Priority/Planner/Supervisor + Freeze Flags + PromiseDate + LotSerialRequirementType + WorkInstructionsRevision + DrawingRevision) | NEXT BIG CASCADE | 8-12h |
| **C** | **Sprint 14.3 PR-2** — Deviation entity (variant change type: short-term divergence from approved spec) | Queued | 6-10h |
| **D** | **Sprint 14.3 PR-3** — Waiver entity (long-term divergence with customer approval) | Queued | 4-8h |
| **E** | **B8 PR-PO-2** — `ProductionMaterialStructureLine` field expansion (10 qty columns + 17-state BOM Line Status + flags + Supply Type + Issue Timing + Lot/Serial tracking + substitution model + per-line accounts). Absorbs B5a. | Queued after PR-PO-1 + xmin backfill | 16-20h |
| **F** | **Sprint 14.3 PR-4** — Concession entity (customer-approved retroactive acceptance of non-conforming material) | Queued | 4-8h |
| **G** | **Sprint 14.3 PR-5** — Customer Notice + Supplier PCN (Process Change Notification) — both share an ICustomerNotificationService + ISupplierNotificationService surface | Queued | 8-12h |
| **H** | **B8 PR-PO-3** — `ProductionMaterialTransaction` + 12-action service (Issue / IssueAll / IssueKit / PartialIssue / OverIssue / Return / ReverseIssue / TransferToJob / TransferFromJob / Substitute / Split / ScrapComponent). 6 enforced job-to-job transfer rules. Absorbs B2. | Queued after PR-PO-2 | 20-30h |
| **I** | **Sprint 14.3 PR-6** — CAR (Corrective Action Request) + CAPA (Corrective + Preventive Action) entities + ICorrectiveActionService surface | Queued | 10-14h |
| **J** | **B8 PR-PO-4** — `ProductionOperationTransaction` + state machine + 19 actions. Absorbs B3 + B5b. | Queued after PR-PO-3 | 20-30h |
| **K** | **Sprint 14.3 PR-7** — Impact analysis service + redline drawing markup tools + closed-loop FAI re-trigger on Engineering Change | Queued | 16-22h |
| **L** | **B8 PR-PO-5** — `ProductionWipMove` + Move-to-Next-Op / Send-Back-to-Prior-Op | Queued | 6-8h |
| **M** | **B8 PR-PO-6** — Complete + Scrap + Rework modals (15+16+16 fields each, preview-before-post) | Queued | 16-20h |
| **N** | **B8 PR-PO-7** — "Can I run this?" 8-readiness-check indicator (Materials Ready / Prior Op Complete / Resource Available / Labor Qualified / Quality Clear / Documents Current / Tooling Ready / Maintenance Clear). Cross-module integration. **Highest-leverage smarter-than-BIC differentiator per spec §8.3.** | Queued after PR-PO-4 | 12-16h |
| **O** | **B8 PR-PO-8** — `/Production/Orders/{id}/Cockpit` Control Center surface. 12 tabs + 24-column BOM grid + 22-column Routing grid + 16-metric top summary bar. | Queued after PR-PO-6 | 20-30h |
| **P** | **B8 PR-PO-9** — Transaction drawer UI pattern (right-side panel, reusable across BOM + Routing grids) | Queued after PR-PO-8 | 12-16h |
| **Q** | **B8 PR-PO-10** — 3-mode UI (Planner / Supervisor / Operator) gating | Queued | 6-8h |
| **R** | **B8 PR-PO-11** — 14 validation services as guards on PR-PO-3/4/5 transactions | Queued (parallelizable) | 16-20h |
| **S** | **B8 PR-PO-12** — `IProductionReportingService` + 17 dashboard tiles (consumes PR-PO-3/4/5 data) | Queued (last) | 20-30h |
| **T** | **Sprint 14.4 — Cost engine** consumes PR-FS-3 (ItemStandardCostElement) + PR-FS-4 (CostLayer) data | Queued | 30-50h |
| **U** | **Sprint 14.5 — Unified Item view** (smarter-than-BIC customer-facing Item card: bill of drawings via IDocumentService + change history via IEcrEcoService + sourcing via IItemSourcingRuleService + cost via PR-FS-3/4) | Queued | 16-24h |
| **V** | **Sprint 18 (renumbered) — Maintenance CC** (formerly Sprint 14 Maintenance CC: Calibration / OEE / RCM+FMEA / Weibull). Prerequisite: ADR-019 (still not drafted; recommend folding into PR-PO-7 work since that wave touches the same hierarchy) | Queued post-B8 | ~50-80h |
| **W** | **Theme B7 — PO-as-Standard + Make-or-Buy duality** (RESEARCH-BEFORE-BUILD; generalizes B6) | Research first | TBD |
| **X** | **Theme B9 — Customer Project Manager Module** (RESEARCH-BEFORE-BUILD; spec exists; positions CustomerProject as parent commercial+operational+financial container) | Research first | TBD (likely 100-150h) |

### 1.5 The xmin latent bug — concrete location and fix

The HARD LOCK encoded since the 05-24 audit (`feedback_xmin_pattern_for_concurrency_lock.md`) is now an enforced convention. Three entities still carry the legacy pattern. Direct citation from the AppDbContext walk:

| Line | Entity | Current code | Required fix | First-INSERT throws |
|---|---|---|---|---|
| 2701 | `CustomerItemXref` | `e.Property(x => x.RowVersion).IsRowVersion();` | `e.MapXminRowVersion(x => x.RowVersion);` + change `byte[] RowVersion` → `byte[]? RowVersion` on entity | PG 23502 NULL violation |
| 2754 | `ItemSourcingRule` | `e.Property(x => x.RowVersion).IsRowVersion();` | Same | Same |
| 2816 | `CostLayer` | `e.Property(x => x.RowVersion).IsRowVersion();` (Codex P1 fix on PR #360 introduced this — was the *correct* fix at the time, before the xmin lock was discovered on PR #364/365) | Same | Same |

**Why dormant:** the existing probes are read-or-update-only. `CustomerItemXrefProbe` exercises Resolve (SELECT) + Supersede (UPDATE) + Obsolete (UPDATE). `ItemSourcingProbe` reads (the probe specifically renders "0 rules, MRP falls back to Item.PrimaryVendorId"). `CostLayerProbe` reads (renders empty summary panel). The INSERT path is never taken by the probes. Production code that DOES insert (e.g., `CostLayerService.RecordReceiptAsync`) will throw on first call from a real cockpit UI.

**Required migration:** dropping a `bytea NOT NULL` column from 3 tables. Same shape as `20260526235021_DropPoSnapshotBytesRowVersionFs141Pr1P1.cs` (the PR #365 hotfix), 3× repeated. Typed `migrationBuilder.DropColumn` + 3× `MapXminRowVersion` re-wire in `AppDbContext.cs`. Designer + Snapshot regen. ~1.5 hours including E2E verification on dev preview.

---

## 2. Reconciliation with the 16-Wave spine

The original 16-Wave spine from `master-plan-audit-2026-05-24.md` is structurally still correct but **chronologically compressed** — what was scheduled across 16 weeks compressed into ~7 days of session work for the substrate layers, leaving the customer-facing UI cascade (B8 PO Cockpit) as the new long tail.

### Wave-by-wave reconciliation

| Wave | Original (05-24 audit) | Status now (05-27) | Action |
|---|---|---|---|
| **Wave 1.1** | Sprint 13.5 in flight (12/14 main + 5 hardening) | ✅ COMPLETE (PR #5d at `177fb53`; subsequent ships landed PR #345-#367) | Mark closed |
| **Wave 1.2** | Master Files Baseline pre-ABS slice (PRA-4 UOM + PRA-5a COA additive + PR #5c.4 seeder) | ✅ COMPLETE | Mark closed |
| **Wave 1.3** | ABS Thursday demo gate (May 28) | ⏳ TODAY — substrate UNCHANGED; demo runs on CFO + COO motion + FAI UI all live on prod | Keep gate |
| **Wave 2.1-2.3** | Post-ABS PRA-5b / PRA-6 / PRA-7 cascade | ✅ COMPLETE | Mark closed |
| **Wave 2.4** | Sprint 12D PR #7 Performance harness | ⏳ Still queued ("not blocking demo") | Re-slot to backlog post-B8 |
| **Wave 2.5** | ADR-019 draft (Asset↔WorkCenter↔Machine↔Department) | ⏳ Still not drafted | **Fold into B8 PR-PO-7** (the "Maintenance Clear" readiness check forces the hierarchy decision in code; ADR follows from the implementation) |
| **Wave 2.6** | ADR-026 (Seven Customer Modes Contract) | ⏳ Still not drafted | Still parked — recommend post-EVS / before Theme B9 research |
| **Wave 2.7** | Sprint 13.5 PR #6 subcontract chain | ⏳ Still queued | Re-slot to backlog post-B8 |
| **Wave 2.8** | Sprint 13.5 PR #7 voice intents (ExplainProjectRisk / WhyIsJobXLate) | ⏳ Still queued | Fold into Sprint 14.5 unified Item view voice surface |
| **Wave 2.9** | EVS Wednesday pitch gate (June 3) | ⏳ ~7 days away | **Keep gate — recommend B8 PR-PO-1+PO-2 + Sprint 14.3 PR-2..3 land by EVS** |
| **Wave 3.1-3.4** | Post-EVS PRA-8 / PRA-9 / PRA-10 / PRA-11 | ✅ COMPLETE (PRA-8/9/10/11 all live in Models/Masters/ — verified) | Mark closed |
| **Wave 3.5-3.7** | MES Event Layer cascade (PR #5e/5f/5g) | ⏳ Still queued | Re-slot to **B8 cascade adjacency** — PR-PO-3 (ProductionMaterialTransaction) IS the MES event substrate at the material side; PR-PO-4 IS the MES event substrate at the operation side. Sprint 13.5 PR #5e/5f/5g become tile/dashboard work that follows PR-PO-12 in the B8 cascade. |
| **Wave 4** | Sprint 12.7 Controller Cockpit (5 PRs) | ✅ COMPLETE (PR #346-#348 cover PR #1-#4; PR #5 demo data folded into CFO motion seed) | Mark closed |
| **Wave 5** | Sprint 13 Full Purchasing CC (8 PRs) | ⏳ Still queued — UNCHANGED | Re-slot post-B8 cascade close |
| **Wave 6** | Sprint 12.5 Enterprise Hardening (17 PRs) | ⏳ Still queued — UNCHANGED | Re-slot post-launch / pre-v1.0 |
| **Wave 7** | Sprint 14 Maintenance CC (Calibration / OEE / RCM+FMEA / Weibull) | **RENUMBER** — "Sprint 14" is now claimed by B6/14.x cascade. New label: **Sprint 18 Maintenance CC**. | Renumber + re-slot post-B8 close |
| **Wave 8-11** | Sprints 15/16/17/18 Planning/Scheduling/Inventory/Shipping CC | ⏳ Still queued — UNCHANGED. Renumber Shipping/Inventory order if Sprint 14 → Sprint 18 collides. | Renumber + keep |
| **Wave 12** | Sprint 19 Spatial Plant Floor Map (5 PRs) | ⏳ Still queued | Keep |
| **Wave 13** | Sprint 20 Mobile PWA + DataWedge | ⏳ Still queued | Keep |
| **Wave 14** | Sprint 21 MCP Server + Agentic AI Launch | ⏳ Still queued — Tool Registry (Absorption Item 2) + Recommendation Outcomes (Item 5) fold here | Keep |
| **Wave 15** | Sprint 22 i18n + Launch Polish | ⏳ Still queued | Keep |
| **Wave 16** | Sprint 23 v1.0 LAUNCH gate | ⏳ Still queued | Keep |

**Net effect:** Waves 1.1 / 1.2 / 2.1-2.3 / 3.1-3.4 / 4 are CLOSED. The 16-Wave spine is **5/16 closed**. The new long tail is the B8 PO Cockpit cascade + Sprint 14.3 PR-2..7 + Sprint 14.4 cost engine + Sprint 14.5 unified Item view — collectively ~250-350h of code that sits BETWEEN the old Wave 4 and the old Wave 5 (Sprint 13 Purchasing CC), expanding the spine to ~21 waves total.

### Inserted waves (net-new since the 05-24 audit baseline)

| New wave | Slot | What it is |
|---|---|---|
| **Wave 4.5** | Between old Wave 4 and old Wave 5 | **B6 Foundation Sprint** (10 ships, CLOSED) |
| **Wave 4.6** | Between Wave 4.5 and old Wave 5 | **Sprint 14.1 substrate** (per-PO snapshot) — PR-1 CLOSED |
| **Wave 4.7** | Between Wave 4.6 and old Wave 5 | **Sprint 14.2 substrate** (DMS) — PR-1 CLOSED |
| **Wave 4.8** | Between Wave 4.7 and old Wave 5 | **Sprint 14.3 substrate** (ECR/ECO) — PR-1 CLOSED; PR-2..7 in flight |
| **Wave 4.85** | Inserted defensive ahead of Wave 4.9 | **PR-XminBackfill** — 3-entity cleanup (1.5h) |
| **Wave 4.9** | Between Wave 4.85 and old Wave 5 | **B8 PO Cockpit cascade** — 12 PRs / 172-240h |
| **Wave 4.95** | Between Wave 4.9 and old Wave 5 | **Sprint 14.4 cost engine** + **Sprint 14.5 unified Item view** |

The old Wave 5+ all shift right (in calendar time) but keep the same dependency relationships.

---

## 3. Dependency graph + recommended sequence

```
Wave 1.1-4 (CLOSED through PR #367)
  └→ Wave 4.85 — PR-XminBackfill (1.5h DEFENSIVE)
       └→ Wave 4.9 — B8 PO Cockpit cascade (parallel with Sprint 14.3 PR-2..7)
       │    │
       │    ├─ B8 PR-PO-1 (header expansion) ─┐
       │    │                                  ├→ B8 PR-PO-3 (material tx) ─┐
       │    ├─ B8 PR-PO-2 (BOM line)──────────┘                              │
       │    │                                                                ├→ B8 PR-PO-4 (op tx) ─┐
       │    │                                                                │                       │
       │    │  ┌── Sprint 14.3 PR-2 (Deviation) ────┐                       │                       │
       │    │  ├── Sprint 14.3 PR-3 (Waiver) ──────┤                       │                       │
       │    └──┼── Sprint 14.3 PR-4 (Concession) ──┼──┐                    │                       │
       │       ├── Sprint 14.3 PR-5 (Customer/Supplier Notice) ──┼──→ feeds PR-PO-4 ECO effectivity │
       │       ├── Sprint 14.3 PR-6 (CAR/CAPA) ────┘  │                    │                       │
       │       └── Sprint 14.3 PR-7 (Impact + FAI re-trigger + redline) ──┴────────────────────────┤
       │                                                                                            │
       │                                                                  ┌→ B8 PR-PO-5 (WIP move) ─┤
       │                                                                  │                          │
       │                                                                  ├→ B8 PR-PO-6 (modals) ────┤
       │                                                                  │                          │
       │                                                                  ├→ B8 PR-PO-7 (Can I run?) ─┤
       │                                                                  │  (also drafts ADR-019)    │
       │                                                                  │                          │
       │                                                                  ├→ B8 PR-PO-8 (cockpit UI) ─┤
       │                                                                  │   └→ PR-PO-9 (drawer)     │
       │                                                                  │       └→ PR-PO-10 (modes) │
       │                                                                  │                          │
       │                                                                  ├→ B8 PR-PO-11 (validators) │
       │                                                                  │  (parallel)              │
       │                                                                  │                          │
       │                                                                  └→ B8 PR-PO-12 (reports) ──┘
       │
       └→ Wave 4.95 — Sprint 14.4 cost engine
                        └→ Sprint 14.5 unified Item view (smarter-than-BIC)
                             └→ (old) Wave 5 Sprint 13 Full Purchasing CC
                                  └→ ... 16-Wave tail continues
```

### Recommended chronological execution sequence

| # | Ship | Effort | Why now |
|---|---|---|---|
| 1 | **PR-XminBackfill** | 1.5h | Defensive — fixes the 3 latent bugs before any cockpit-side INSERT path lights up. Identical fix 3×; cheapest as a bundle. |
| 2 | **B8 PR-PO-1** | 8-12h | Header expansion is foundational for everything downstream — Status state machine is read by every other PR-PO ship. |
| 3 | **Sprint 14.3 PR-2 (Deviation)** | 6-10h | Parallel work — smallest 14.3 PR; gets the variant change-type pattern locked. |
| 4 | **B8 PR-PO-2** | 16-20h | Unlocks PR-PO-3 transactions. Absorbs B5a. |
| 5 | **Sprint 14.3 PR-3 (Waiver)** | 4-8h | Parallel — sibling to Deviation. |
| 6 | **B8 PR-PO-3** | 20-30h | The big one — material transactions. 12 actions + 6 job-to-job rules. Absorbs B2. |
| 7 | **Sprint 14.3 PR-4 (Concession)** | 4-8h | Parallel — completes the Deviation/Waiver/Concession triad. |
| 8 | **B8 PR-PO-4** | 20-30h | Operation transactions. State machine + 19 actions. Absorbs B3 + B5b. |
| 9 | **Sprint 14.3 PR-5 (Customer/Supplier Notice)** | 8-12h | Parallel — locks the outbound notification pattern. Touches webhook outbox. |
| 10 | **B8 PR-PO-5 (WIP move)** | 6-8h | Small. Closes the transaction-substrate trio. |
| 11 | **Sprint 14.3 PR-6 (CAR/CAPA)** | 10-14h | Parallel — closed-loop quality substrate. |
| 12 | **B8 PR-PO-6 (modals)** | 16-20h | Customer-facing UI begins here. Complete/Scrap/Rework modals. |
| 13 | **Sprint 14.3 PR-7 (Impact + FAI re-trigger + redline)** | 16-22h | Closes Sprint 14.3 cascade. Cross-references B8 PR-PO-3/4. |
| 14 | **B8 PR-PO-7 ("Can I run this?")** | 12-16h | **Highest-leverage smarter-than-BIC differentiator.** Drafts ADR-019 in code. |
| 15 | **B8 PR-PO-8 (cockpit UI)** | 20-30h | The customer-facing cockpit lands. |
| 16 | **B8 PR-PO-9 (drawer)** | 12-16h | Reusable UI pattern. |
| 17 | **B8 PR-PO-10 (modes)** | 6-8h | 3-mode gating. |
| 18 | **B8 PR-PO-11 (validators)** | 16-20h | Parallel — guards on transaction services. |
| 19 | **B8 PR-PO-12 (reports + 17 dashboards)** | 20-30h | Closes B8 cascade. |
| 20 | **Sprint 14.4 cost engine** | 30-50h | Consumes PR-FS-3 + PR-FS-4 data + B8 PR-PO-3 transaction events. |
| 21 | **Sprint 14.5 unified Item view** | 16-24h | The smarter-than-BIC Item card. Customer-facing payoff for the entire B6/14.x cascade. |

**Total B6/14.x + B8 effort: ~280-380h.**

**Parallelization gain:** B8 + Sprint 14.3 PR-2..7 ships can land alternately because they touch different model + service surfaces (B8 = Production/ + Materials transactions; 14.3 = Engineering/ change-control entities). Ship-cycle through this with ~half-day cadence per PR ≈ 25-40 session days of work.

### EVS June 3 cut-line

7 calendar days from 2026-05-27. At 2-3 ships per session day historical pace (last 36 hours = 13 ships = ~9 ships/day-equivalent at peak), the realistic EVS June 3 demo cut is:

- ✅ **PR-XminBackfill** (today)
- ✅ **B8 PR-PO-1** header expansion (today / tomorrow)
- ✅ **B8 PR-PO-2** BOM line expansion (next session)
- ✅ **Sprint 14.3 PR-2** Deviation (parallel)
- ✅ **Sprint 14.3 PR-3** Waiver (parallel)
- ✅ **B8 PR-PO-3** material transactions (next 2 sessions)
- ✅ **Sprint 14.3 PR-4** Concession (parallel)

**EVS-realistic demo target:** an admin probe (`/Admin/PoCockpitProbe`?) that exercises Issue + Return + Substitute material transactions on a CooMotion DEMO-COO-PRO-1005 sandbox, alongside a `/Admin/EcrEcoProbe` that fires a full Deviation→Waiver→Concession cycle. Both are admin probes for engineering verification — same pattern that the 14.1/14.2/14.3 substrate probes use today. **Customer-facing UI (B8 PR-PO-8 cockpit) is NOT realistic for EVS** — it's still 5-7 ships out at that point. The EVS pitch sells the **architecture** (chain of custody from ECO → DocumentVersion → ProductionMaterialStructure → ProductionMaterialTransaction → CostLayer → GL) live on dev preview.

---

## 4. Latent xmin bug backlog — decision

**Recommendation: bundle into a single defensive PR (PR-XminBackfill) — INSERT IT AS THE VERY NEXT SHIP, AHEAD OF B8 PR-PO-1.**

**Reasoning:**

1. **Identical fix 3× → bundle is cheaper.** Drop `bytea RowVersion NOT NULL` column + remap to xmin + change `byte[] RowVersion` → `byte[]? RowVersion`. 3 entities, identical 5-line edit each. One migration. ~1.5h total vs ~3h if done per-touch (each later PR pays the cost of re-loading context).

2. **Risk window is short.** Any next ship that calls `CostLayerService.RecordReceiptAsync` (PR-PO-3 will), `ItemSourcingRuleService.AddRuleAsync` (B7 will), or `CustomerItemXrefService.AddXrefAsync` (Sprint 14.5 unified Item view will) detonates the bug. Better to clear it pre-emptively than chase a 23502 mid-cascade.

3. **Pattern is already proven** (PR #365 hotfix for ProductionMaterialStructure used the same migration shape). Copy-paste-amend.

4. **No new test surface needed.** The existing CostLayer / ItemSourcingRule / CustomerItemXref unit tests pass under either pattern; the bug only fires under the EF-managed concurrency-token codepath which the existing tests don't exercise. The fix is invisible to consumers.

**Alternative considered:** fix on first INSERT touch. Rejected — adds inter-PR friction and forces every "next-cascade" ship author to know about the latent bug.

**Acceptance criteria for PR-XminBackfill:**
- 3 entities converted to xmin pattern in `AppDbContext.cs` (lines 2701/2754/2816)
- 1 typed migration that does `migrationBuilder.DropColumn("RowVersion", ...)` × 3
- Designer + Snapshot regen
- 3 `byte[] RowVersion` → `byte[]? RowVersion` on entity properties
- Smoke test: probe pages still render (read paths unchanged)
- Write-path smoke: light up `/Admin/CostLayerProbe` with a synthetic Insert button (1 extra HTML button + 1 form handler in `.cshtml.cs`) to prove the INSERT path now works — required per the new **Lock 16 corollary** (probes must exercise writes)

---

## 5. Demo runway alignment

### ABS Machining demo — TODAY (Thursday May 28, 2026)

**Status:** SAFE. Prod (industryos.app) was Republished-with-Copy on 2026-05-26 PM (per memory `project_republish_prod_verify_2026_05_26.md`) AFTER PR #354 (CooMotion seeder). All subsequent 14.x ships are dev-only (Lock 17 code-only deploy). Demo runway untouched.

**What's live on prod for ABS:**
- CFO motion at `/Controller` — $6.2M / $493.1K / 120 / $2.4M KPI tiles + ChainTraceService walks
- COO motion at `/CustomerProjects/Index` — DEMO-COO-PROJ-001 "Precision Bracket Assembly — Q3 2026 (Demo)" with 4 Phases + 10 BOMs + 10 Routings + 10 PROs + 36 ops
- FAI UI (PR #338) — Standard-vs-Actual at dimension level
- Sprint 13.5 Production Control Center + Receiving CC + Operator Workbench + LaborEntry + ReasonCode
- Master Files Baseline PRA-1..11 (UOM / COA / Currency / PaymentTerm / TaxCode / Warehouse / Bin / Lot / Serial / ItemGroup / Employee / WageGroup / LaborRate / Dept→GL / PriceList / Discount / Rebate / TaxAuthority / TaxRate / Pack hierarchy)

**What's on dev only (NOT on the demo path):**
- B6 Foundation Sprint admin probes (`/Admin/ItemMasterExpansionProbe` etc.)
- 14.1/14.2/14.3 substrate probes (`/Admin/PoSnapshotProbe`, `/Admin/DocumentProbe`, `/Admin/EcrEcoProbe`)

**No action needed for ABS demo prep beyond keeping dev separate from prod.**

### EVS Wednesday pitch — June 3, 2026 (7 days out)

**Status:** TARGETED — the cascade in §3 hits EVS with B8 PR-PO-1/PO-2/PO-3 + Sprint 14.3 PR-2/PR-3/PR-4 admin-probe surface live on dev. Recommend:

**EVS demo narrative (per §3 EVS-realistic cut-line):**
> "Here's what we shipped in the 7 days since ABS. The Engineering Change → Document → Production Order chain of custody is end-to-end live. I can fire an ECO that supersedes a drawing, the drawing supersedes itself atomically inside the IDocumentService, and on the production-floor side the ProductionMaterialTransaction service issues material against the new revision while the prior revision's frozen snapshot survives unchanged on jobs in flight. Watch."

**Live demo path:**
1. `/Admin/EcrEcoProbe` — fire Create ECR → Submit → Approve (cascades F/F/F→RequiresFAI=True) → Release ECO → Implement
2. `/Admin/DocumentProbe` — fire AddVersion → Approve → Release (atomic supersede)
3. `/Admin/PoSnapshotProbe` — show frozen snapshot on PRO 6 with $4.25/$11.22/$20.655 line costs preserved
4. `/Admin/PoCockpitProbe` (NEW for EVS) — fire Issue / Return / Substitute on a CooMotion DEMO-COO-PRO-1005 BOM line
5. `/Admin/EcrEcoProbe` — fire Deviation → Waiver → Concession cycle on a child item

**EVS-realistic effort: ~5-7 ships at peak cadence (last 24h precedent). Doable.** Sprint 14.4 cost engine + Sprint 14.5 unified Item view + B8 PR-PO-7/8 customer-facing UI are post-EVS.

---

## 6. Things still requiring Dean's 1-click direction

| # | Question | Recommendation |
|---|---|---|
| **A1** | **PR-XminBackfill** — bundle the 3 latent fixes (CostLayer / ItemSourcingRule / CustomerItemXref) into a single defensive ~1.5h ship before B8 PR-PO-1? | YES — see §4. |
| **A2** | **B8 + Sprint 14.3 PR-2..7 sequencing** — alternate ships (B8 ship → 14.3 ship → B8 ship → ...) so both cascades land in parallel? | YES — see §3 sequence table. Different model surfaces, low merge risk. |
| **A3** | **Renumber legacy Sprint 14 Maintenance CC → Sprint 18 Maintenance CC?** ("Sprint 14" is now claimed by B6/14.x cascade. Old Sprint 14 had: Calibration #125 / OEE #126 / RCM+FMEA #127 / Weibull #128 / ADR-019.) | YES — Sprint 18 slot, post-B8 cascade. Fold ADR-019 draft INTO B8 PR-PO-7 (the "Maintenance Clear" readiness check forces the asset-hierarchy decision in code). |
| **A4** | **EVS demo path** — admin-probe-driven (PoCockpitProbe + EcrEcoProbe cycle) or hold for customer-facing PR-PO-8 cockpit ship? | Admin-probe-driven. PR-PO-8 customer cockpit is realistically post-EVS. |
| **A5** | **Theme B7 + B9 research timing** — start B7 (PO-as-Standard + Make-or-Buy) and B9 (Customer Project Manager) research spikes in parallel during B8 build cadence, or sequential post-B8? | Sequential post-B8 — research spike during a "low-shipping-velocity" wave. B7 ahead of B9 (B7 is smaller / generalization of B6; B9 is greenfield 100-150h that needs its own dedicated cycle). |
| **A6** | **Sprint 13.5 PR #6/7 (subcontract chain + voice intents) — keep queued or retire?** Sub-chain edges are partially absorbed into IEcrEcoService + IDocumentService linkage; voice intents are sub-functions of Cherry Bar | Recommend RETIRE PR #6 (subcontract chain — folded into B8 PR-PO-4 outside-operation type variant). KEEP PR #7 (voice intents — fold into Sprint 14.5 unified Item view voice surface). |
| **A7** | **ADR-019 / ADR-023 / ADR-024 / ADR-026 from prior audit** | Reaffirm: ADR-019 drafted in-code with B8 PR-PO-7. ADR-023 still parked (12B.2 trigger unfired). ADR-024 still split into Tool Registry (ADR-024) + Q3 host migration (ADR-027). ADR-026 Seven Customer Modes still parked — recommend post-EVS / before Theme B9 research. |

---

## 7. Acceptance criteria — when is the plan "in order"?

After this audit:

- [x] Every shipped block has a verified SHA (main HEAD = `813d399`; all 14.x ships traced to merge commits in §1.2)
- [x] Every queued ship has an effort estimate (§1.4 column) + execution slot (§3 sequence)
- [x] Every deferred item has a named trigger (Sprint 13.5 PR #6 → B8 PR-PO-4; voice intents → Sprint 14.5; ADR-019 → B8 PR-PO-7; etc.)
- [x] Latent bugs surfaced + decided (xmin → PR-XminBackfill bundled defensive ship)
- [x] B6 Foundation Sprint formally CLOSED in the plan
- [x] Sprint 14.1/14.2/14.3 PR-1 substrate ships formally TRACKED in the plan
- [x] B8 PO Cockpit cascade has Wave slot (Wave 4.9)
- [x] Sprint 14.4 + 14.5 have Wave slots (Wave 4.95)
- [x] Sprint 14 Maintenance CC renumbered to Sprint 18 (pending Dean A3 confirmation)
- [x] Demo runway aligned (ABS today protected; EVS June 3 targeted with admin-probe-driven cut)
- [ ] Dean signs off on §6 audit questions A1-A7

When the §6 sign-off lands, the plan is in order. As of this memo: 10 of 11.

---

## 8. What this audit does NOT do

- Does not re-litigate Dean-locked decisions (HARD LOCKS / BIC Entity Checklist / Lock 5-17 / Cockpit primitives / no-fake-data / no-customer-names / GO BIG)
- Does not re-baseline Sprints 5/7/8/9 (still partially absorbed by B6 + Master Files; re-baseline post-B8 cascade close)
- Does not retire any shipped work
- Does not change the ABS Thursday + EVS June 3 demo dates
- Does not modify the v2-locked AP/AR items (Tax / Payment / OCR / ASC 842 / ASC 360)
- Does not start Theme B7 or B9 research — both are queued for post-B8

---

## 9. References

- `MASTER_PLAN.md` — the document this audit re-orders
- `docs/research/master-plan-audit-2026-05-24.md` — prior audit (now superseded by this memo for sequencing purposes; kept on disk for historical reference)
- `docs/research/po-cockpit-spec-2026-05-26.md` — Theme B8 spec (verbatim from Dean upload)
- `docs/research/project-management-fields-and-functions-2026-05-26.md` — Theme B9 spec (verbatim from Dean upload)
- `docs/research/b6-foundation-sprint-design-2026-05-26.md` — B6 cascade design (now CLOSED)
- `docs/research/item-master-b6-audit-synthesis-2026-05-26.md` — B6 audit synthesis (now CLOSED)
- `docs/research/master-files-baseline-2026-05-24.md` — Master Files Baseline (now CLOSED)
- Memory: `MEMORY.md` index + `project_pr3*.md` series (the ship logs that this audit reconciles)
- Memory: `feedback_xmin_pattern_for_concurrency_lock.md` — the HARD LOCK behind §4
- Memory: `reference_sprint_naming_no_vendor_implication.md` — the no-vendor-in-sprint-titles convention
- Memory: `feedback_b6_go_big_2026_05_26.md` — the GO BIG directive
- Memory: `reference_po_cockpit_spec_2026_05_26.md` — Theme B8 pointer
- Memory: `reference_dean_brainstorm_b7_2026_05_26.md` — Theme B7 pointer (research-before-build)
- Memory: `reference_dean_brainstorm_b9_2026_05_26.md` — Theme B9 pointer (research-before-build)

---

## 10. Memory pointer

A companion memory file `reference_master_plan_audit_2026_05_27.md` is being written alongside this memo so the reordered spine + the 7 open audit questions + the xmin-backfill recommendation + the Sprint 18 renumber proposal survive memory wipes.

The prior `reference_master_plan_audit_2026_05_24.md` pointer is being marked SUPERSEDED in MEMORY.md (kept on disk as historical reference).

---

## 11. MEMORY.md updates being made now

`MEMORY.md` is currently 27.7KB (limit 24.4KB — warning fired in the auto-memory boot). This audit triggers a consolidation:

1. **Add `📋 Master Plan Audit 2026-05-27` block** at top of the index pointing to this memo
2. **Mark `[Master Plan audit 2026-05-24]` line as SUPERSEDED** with a one-line forward pointer to this 2026-05-27 memo
3. **Trim verbose `project_pr*.md` index lines** — the warning notes "index entries are too long. Only part of it was loaded." Lines for PR #354/#355/#356/#357/#358/#359/#360/#361/#362/#363/#364/#365/#366/#367 are currently 200-700 chars each — trim each to one line under ~150 chars with the standard `- [Title](file.md) — one-line hook` shape per the index-format guidance.
4. **NEW HARD LOCK entries** stay (xmin pattern + sprint-naming + probe-write-paths)
5. **Date stamp + line count** at the top of the trimmed file to make the budget-check loop visible

---

## 12. Sign-off

This audit was performed as a single-session exercise on 2026-05-27 AM immediately after the HANDOFF prompt landed. It (a) reads MEMORY.md + prior audit + B6 design + B8 spec + B9 spec, (b) walks Models/ + Services/ + Pages/Admin/ + Migrations/ + AppDbContext.cs xmin audit, (c) reconciles the 16-Wave spine, (d) sequences the next ~21 ships into a single chronological order, (e) calls out the latent xmin backlog with a concrete fix path, and (f) aligns the cascade against the ABS Thursday + EVS June 3 demo gates.

**Awaiting Dean's call on the 7 §6 audit questions.** Once those land, the queued PR work resumes with PR-XminBackfill (1.5h defensive) → B8 PR-PO-1 (8-12h header expansion) → cascade per §3 sequence.

---

## 13. Lessons carried forward from the 24-hour cascade

These are not new HARD LOCKS — they're observations from the 14-ship spurt that should inform how the B8 cascade gets executed.

1. **Codex-as-co-author worked.** Every B6 PR + every 14.x substrate PR had Codex-flagged P1s caught. The 3 latent xmin entities exist because they were shipped BEFORE the xmin lock was discovered on PR #364/365. The new HARD LOCK + the probes-exercise-writes corollary close that gap going forward.

2. **The probe-as-write-fixture pattern is the right Lock 16 implementation.** PR #366 with 5 write buttons caught a cross-tenant gap no unit test had. PR #367 with 8 write buttons proved 13-op end-to-end on dev. Every B8 PR-PO-N ship MUST include a probe with write buttons exercising the new service ops — this is now the §6.A1 + Lock 16 corollary covenant.

3. **Sprint-title vendor purity matters.** The 14.3 rename from "Arena PLM ECR/ECO" to "ECR/ECO Change Control" caught a real risk (selling EVS / ABS on an architecture that implies vendor integration we don't have). Apply the same purity to Theme B7 + B9 — never "SAP PS" or "Oracle Primavera" in sprint titles; always describe what we actually do.

4. **xmin pattern is now the project default.** All 38+ entities in AppDbContext.cs use `MapXminRowVersion`. The 3 stragglers (§1.5) are pre-lock artifacts. Going forward, NO new entity uses `IsRowVersion()` on `bytea`.

5. **Enum DB defaults must match model defaults.** Codex caught this on PR #363 (MakeBuyCode + LifecycleStage) and PR #367 had 0 enum-default findings because the lesson was applied prophylactically. The next 7 enums in B8 PR-PO-1 (OrderType / Status / HoldReason / Priority / LotSerialRequirementType / Freeze flags / etc.) MUST land with `e.Property(x => x.Enum).HasDefaultValue(EnumName.SemanticDefault)` from day one.

6. **Realistic mfg data — even in admin probes.** Every probe ships with real part numbers (BRG-6207-2RS / BAR-1018), real vendors (Grainger / Ryerson / Sandvik), real OEM customers (GE Aviation / Boeing / Pratt & Whitney), real specs (BAMS-3320 / AMS 6520). The B8 PR-PO-N probes must do the same — Trent bracket assembly / SKF bearing / Ryerson steel / Grainger fastener / phantom mount are the fixture lexicon.

7. **Lock 17 saves Republish friction for code-only PRs.** Every 14.x substrate PR was code-only → dev preview only → no prod touch. ABS demo runway protected. Apply rigorously through B8 cascade — only Republish-with-Copy when a data change actually needs to land on prod (which for B8 will probably be at PR-PO-8 customer-facing UI launch, not before).

---

## 14. Session 12 Addendum (2026-05-27 PM — post-session 11 audit refresh)

**Main HEAD: `e527640`** (PR #379 — B8 PR-PRO-4 ProductionOperationTransaction).

**Since the AM audit was written at HEAD `813d399`, 12 additional PRs have shipped (PR #368 → #379).** All 10 Waves A through J from §1.4 are now CLOSED. The cascade executed in this order:

| Wave | Ship | PR | Merge SHA | Session | Status |
|---|---|---|---|---|---|
| **A** | PR-XminBackfill | #368 | `02a17fc` | 10 AM | ✅ CLOSED |
| **B** | B8 PR-PRO-1 header expansion | #369 | `fcb96c6` | 10 | ✅ CLOSED |
| **C** | Sprint 14.3 PR-2 Deviation | #370 | `df1fccd` | 10 | ✅ CLOSED |
| **E** | B8 PR-PRO-2 BOM line expansion | #372 | `3bf312a` | 10 | ✅ CLOSED |
| **D** | Sprint 14.3 PR-3 Waiver | #373 | `619510a` | 10 | ✅ CLOSED |
| **F** | Sprint 14.3 PR-4 Concession | #374 | `e8bb9be` | 10 | ✅ CLOSED |
| — | fixup #374 Razor dropdowns | #375 | `98e5838` | 10 | ✅ CLOSED |
| **G** | Sprint 14.3 PR-5 CustomerNotice + SupplierPCN | #376 | `68c8640` | 11 | ✅ CLOSED |
| **H** | B8 PR-PRO-3 MaterialTransaction (12-action) | #377 | `32c1882` | 11 | ✅ CLOSED |
| **I** | Sprint 14.3 PR-6 CAR/CAPA (8D lifecycle) | #378 | `e37eaa7` | 11 | ✅ CLOSED |
| **J** | B8 PR-PRO-4 OperationTransaction (19-action) | #379 | `e527640` | 11 | ✅ CLOSED |

**Sprint 14.3 Engineering Change Management substrate: 6/7 PRs CLOSED.** Only PR-7 (Impact Analysis + FAI re-trigger + redline) remains to close Sprint 14.3.

**B8 PRO Cockpit substrate: 4/12 PRs CLOSED.** PR-PRO-1 through PR-PRO-4 (header + BOM line + material tx + operation tx) form the complete transaction substrate. PR-PRO-5 through PR-PRO-12 (WipMove + modals + readiness + cockpit UI + drawer + modes + validators + reports) remain.

### Updated next queue (Waves K through X)

| Wave | Ship | Status | Est. |
|---|---|---|---|
| **K** | Sprint 14.3 PR-7 — Impact analysis + redline markup + FAI re-trigger. **CLOSES Sprint 14.3.** | **NOW** | 16-22h |
| **L** | B8 PR-PRO-5 — ProductionWipMove + Move-to-Next/Send-Back | Next | 6-8h |
| **M** | B8 PR-PRO-6 — Complete + Scrap + Rework modals | Queued | 16-20h |
| **N** | B8 PR-PRO-7 — "Can I run this?" 8-readiness-check indicator | Queued | 12-16h |
| **O** | B8 PR-PRO-8 — `/Production/Orders/{id}/Cockpit` CC surface | Queued | 20-30h |
| **P** | B8 PR-PRO-9 — Transaction drawer UI pattern | Queued | 12-16h |
| **Q** | B8 PR-PRO-10 — 3-mode UI gating | Queued | 6-8h |
| **R** | B8 PR-PRO-11 — 14 validation services | Queued | 16-20h |
| **S** | B8 PR-PRO-12 — IProductionReportingService + 17 dashboards | Queued | 20-30h |
| **T** | Sprint 14.4 — Cost engine | Queued | 30-50h |
| **U** | Sprint 14.5 — Unified Item view | Queued | 16-24h |
| **V** | Sprint 18 — Maintenance CC | Queued | 50-80h |
| **W** | Theme B7 — PO-as-Standard + Make-or-Buy | Research | TBD |
| **X** | Theme B9 — Customer Project Manager | Research | TBD |

### Velocity note

Sessions 10+11 shipped 12 PRs in ~2 session windows — maintaining the ~4 ships/session pace from B6 Foundation. At this velocity, Sprint 14.3 PR-7 (CLOSES 14.3) + B8 PR-PRO-5 (WipMove) are realistic for session 12.

---

*End of audit.*
