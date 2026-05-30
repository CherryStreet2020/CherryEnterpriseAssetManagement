# Theme B9 — Customer Project Manager — Cascade Design

**Source spec:** `docs/research/project-management-fields-and-functions-2026-05-26.md` (22 sections, ~57 entities, the quote-to-cash → plan-to-produce → procure-to-pay → closeout command center).
**Goal:** make `CustomerProject` the commercial + operational + financial **container** that beats SAP S/4 Project System / Oracle Primavera P6 / MSFT Project — with the manufacturing-execution layer they make painful.
**Dean's Wave-1 decision (2026-05-30, session 29):** lead with the **Project Command Center + "Can we still hit the promise?" indicator + project graph** — the visible BIC money-shot — built on the existing substrate, demo-ready for the **EVS pitch June 3**, then backfill the data spine wave by wave.

## Substrate already shipped (verified 2026-05-30)
- Entities: `Program → CustomerProject → ProjectPhase` (self-nesting), `ProjectMember`, `ProjectAmendment` (proto change-order), `ProjectManager`.
- `CustomerProject` is rich: Mode / CostingMode / RevenueMode, ContractValue, Currency, target dates, EVM rollup (`EstimatedTotalCost`, `PercentComplete`, `ProjectedEndDate`, `LastEvmRollupAt`), AI-summary fields (`AiSummaryText/Model/GeneratedAt/RefreshLockedUntil`), `RiskScore`/`RiskTone`, ContractType, QualityProgram, ExportControl, CustomerPoNumber.
- `ProductionOrder.CustomerProjectId` FK already links jobs to projects; `ICustomerProjectService.LinkProductionOrderAsync` exists.
- `ICustomerProjectService`: Create / UpdateHeader / UpdateStatus / AddMember / AddPhase / LinkProductionOrder / CreateAmendment / TransitionAmendmentStatus.
- Pages: `/CustomerProjects` Index / Create / Details. **No command center / graph / promise indicator yet.**
- Reusable infra to lean on: PRO Cockpit pattern (`IProductionCockpitService`, tab-shell), `IOperationReadinessService` (8-check job readiness), ChainTrace / ChainOfCustody graph services, DMS (Document/Version/Link), QMS (NCR/inspection), cost rollup + variance/EVM.

## Architecture rulings (mine — not gated)
- **WBS** = extend the existing self-nesting `ProjectPhase` into the WBS backbone (add cost-bucket / owner / 100%-rule roll-up fields) rather than a parallel `ProjectWBS` tree. One tree, no duplication.
- **Parenting existing execution objects** = follow the established `ProductionOrder.CustomerProjectId` precedent: add a nullable `CustomerProjectId` FK directly to the execution objects (PO, etc.) with a filtered index, not link tables — tight, queryable, matches the shipped pattern.
- All hard-locks apply: real mfg fixtures, no customer names in code (CompanyCode lookup), xmin concurrency, ADR-025 (no AppDbContext in real PageModels), tenant scoping on every read/write, typed migrations, enum DB defaults match model, filtered unique indexes for nullable scope keys.

---

## Wave 1 — Project Command Center (the BIC money-shot · demo-ready for EVS) — 3 PRs

The command center renders REAL data from today's substrate (linked jobs + readiness, phases, amendments, EVM, chain-trace) with graceful "wired in a later wave" empty-states for areas not yet built — exactly how the PRO Cockpit shipped a tab-shell with placeholder tabs.

- **PR-1 — `IProjectCommandCenterService` + Command Center page shell.** Read-aggregation service (ADR-025: data reads in the service) composing the project header, KPI band (contract value, EVM %complete/EAC, margin if available, linked-job count, open amendments), and the "command-center questions" panel (what did we quote / what did the customer buy / what's late / what's over budget / what can we bill / who owns the next action) — each answered from substrate or shown as a typed empty-state. New `/CustomerProjects/{id}/CommandCenter` page mirroring the PRO Cockpit shell. Admin probe. Read-only.
- **PR-2 — "Can we still hit the promise?" indicator.** `IProjectPromiseService.EvaluateAsync(projectId)` → `{ PromiseStatus (Green/Yellow/Red/Black), Reasons[] }` computed from available signals: linked-job readiness (reuse `IOperationReadinessService`), late phases/target dates, open/unapproved amendments, EVM SPI/CPI, material shortages on linked jobs. Reason codes from the spec (long-lead PO late, drawing not approved, job not released, operation behind, customer approval pending, material shortage, open blocking NCR, change order not approved). Prominent promise badge + reasons on the command center. Voice intent `ProjectPromiseStatus` ("can we still hit the promise on project X").
- **PR-3 — Project graph.** Clickable graph node-walk `Quote → Project → WBS/Phase → Job → PO → Receipt → Cost → Billing → Acceptance`, rendering the nodes that exist today (Project → Phases → linked PROs → their material/PO chain via the existing ChainTrace substrate), with future nodes shown dimmed. Reuse the chain-trace graph rendering. Voice intent `ShowProjectGraph`.

## Wave 2 — Quote-to-cash spine (sales → contract → baseline)
- PR-4 `ProjectRFQ` / `ProjectQuote` / `ProjectQuoteRevision` / `ProjectQuoteLine` + locked submitted-snapshot (validation: cannot overwrite a submitted quote snapshot).
- PR-5 `ProjectEstimate` / `ProjectEstimateLine` / `ProjectEstimateSnapshot` (ties to B7 estimate-as-standard).
- PR-6 `ProjectContract` / `ProjectContractLine` / `ProjectCustomerPO` + contract-review gate (cannot launch until review complete) + award validation (cannot mark awarded without an approved quote or authorized override). Winning-quote → baseline.

## Wave 3 — WBS + schedule
- PR-7 WBS hardening on `ProjectPhase` (owner, cost bucket, 100%-rule roll-up; cannot baseline without owner + cost buckets).
- PR-8 `ProjectMilestone` / `ProjectTask` / `ProjectTaskDependency` (cannot complete milestone with open blocking tasks).
- PR-9 Gantt surface + critical-path read.

## Wave 4 — Execution links + procurement + labor
- PR-10 `CustomerProjectId` FK on PurchaseOrder + `ProjectProcurementPlan` / `ProjectCommitment` / `ProjectReceipt` (cannot close project with open commitments unless waived).
- PR-11 `ProjectResourcePlan` / `ProjectResourceAssignment` / `ProjectTimeEntry` / `ProjectExpense`.

## Wave 5 — Financials (the margin engine)
- PR-12 `ProjectBudget` / `ProjectBudgetLine` / `ProjectActualCost` / `ProjectForecast` / `ProjectEACSnapshot` (cannot close with unposted actuals / unresolved WIP).
- PR-13 **Quote-vs-actual comparison** surface (quoted vs actual material/labor/subcontract/lead-time/margin/delivery) — the estimating-improvement gold.
- PR-14 `ProjectBillingSchedule` / `ProjectInvoiceLink` / `ProjectRevenueRecognition` (cannot final-bill without required acceptance).

## Wave 6 — Change control + governance + quality + closeout
- PR-15 `ProjectChangeRequest` → `ProjectChangeOrder` (extend `ProjectAmendment`; approval path required before applying customer scope change).
- PR-16 Governance: `ProjectRisk` / `ProjectIssue` / `ProjectActionItem` / `ProjectDecision` / `ProjectMeeting`.
- PR-17 Quality/acceptance: `ProjectInspection` / `ProjectNCR` / `ProjectMRB` / `ProjectPunchItem` / `ProjectAcceptance` (cannot ship with blocking NCR/MRB/punch).
- PR-18 Service/warranty handoff: `ProjectServiceHandoff` / `ProjectWarranty` (cannot close equipment project without installed-asset/warranty decision) + **AI-assisted project review** (weekly status, margin-erosion explanation, closeout checklist) on the existing AI-summary fields.

## Cross-cutting (every wave)
`ProjectLifecycleHistory` + `ProjectAuditLog` entries (all baseline/budget/margin/quote/change edits logged — spec §20). Dashboards/KPIs (§21) accrete onto the command center as each wave lands its data.

---

**Estimate:** ~18 PRs / 6 waves. Wave 1 (3 PRs) is demo-ready on existing substrate. The 15 non-negotiable validations (§20) are enforced as their owning entities land.
