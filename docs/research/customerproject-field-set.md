# CustomerProject Field-Set Research — Sprint 13.5 PR #1.5

**Author:** IndustryOS architecture research (agent-assisted, 2026-05-23)
**Scope:** What additive fields should land on `CustomerProject` (and one new child table, `ProjectAmendment`) before the Sprint 13.5 PR #2 service layer is built. Four research dimensions: AI infusion, EVM/POC accounting, aerospace/defense quality, change-order amendments.
**Baseline:** Sprint 13.5 PR #1 (commit c8f6d79, 2026-05-22) shipped the foundation schema per the prior 11-ERP survey. This document layers **only the fields PR #2's service layer actually needs to know about on day one** plus the one new child entity (`ProjectAmendments`). Everything else is explicitly deferred with a named target sprint.
**Demo targets:** ABS Machining (Thursday) — multi-vertical Tier-N machining including aerospace/defense. EVS (June 3) — engineer-to-order, "why is job X late" voice query.

---

## 1. Executive summary — the recommended PR #1.5 field set

PR #1.5 is a **pure additive migration**: 14 new columns on `CustomerProjects`, 1 new child table (`ProjectAmendments`, ~14 columns), 0 changes to anything shipped in PR #1. The total adds < 60 minutes of schema work and unblocks the next 4–6 sprints of service + UI work without forcing later migrations against existing data.

### Recommended additions to `CustomerProjects`

| # | Column | Type | Dimension | Why it ships now | Defers to |
|---|---|---|---|---|---|
| 1 | `RiskScore` | smallint NULL (0–100) | AI | Drives `at-risk` queue sort on Program Cockpit + voice "what's at risk?". Asana / Linear / D365 Copilot all surface this. | n/a — ships v1 |
| 2 | `RiskTone` | smallint NULL (0=Green, 1=Amber, 2=Red) | AI | Cheap visual indicator; decouples color choice from numeric threshold tuning. Linear ("On track / At risk / Off track") + Asana Smart Status precedent. | n/a |
| 3 | `AiSummaryText` | text NULL | AI | LLM-generated 1–3 sentence narrative; rendered at top of project detail + read out by voice. Notion AI Summary + Acumatica AI Studio + Joule pattern. | n/a |
| 4 | `AiSummaryModel` | varchar(64) NULL | AI | Versioned model identifier ("anthropic/claude-opus-4-7-1m@2026-05") so we can re-summarize on model upgrade without losing audit trail. | n/a |
| 5 | `AiSummaryGeneratedAt` | timestamptz NULL | AI | Staleness signal; UI fades the summary after N hours. | n/a |
| 6 | `AiRefreshLockedUntil` | timestamptz NULL | AI | Cheap idempotent backoff: if set in the future, the background worker skips. Prevents thundering-herd refresh when many events fire. | n/a |
| 7 | `EstimatedTotalCost` | numeric(18,4) NULL | EVM/POC | Denominator for cost-based POC and the EAC headline number. Required by ASC 606 input method, SAP cost-based PoC, Acumatica POC, every EVM system. | n/a |
| 8 | `PercentComplete` | numeric(5,2) NULL (0.00–100.00) | EVM/POC | Cached, refreshed by `ProjectEvmRollupService`. UI never recomputes inline. | n/a |
| 9 | `ProjectedEndDate` | date NULL | EVM/POC | Distinct from `TargetEndDate` (customer-committed). Linear "off track" requires both. Drives voice "is job X going to slip?". | n/a |
| 10 | `LastEvmRollupAt` | timestamptz NULL | EVM/POC | Staleness signal; UI shows "EVM as of …" + admin endpoint can detect stale rollups. | n/a |
| 11 | `CustomerPoNumber` | varchar(100) NULL | Aero/Def | The PO the customer issued us — *different* from `Code` (our internal id). ABS demo will want this on the FAI report header. Required by AIA G701 and every gov/aerospace contract. | n/a |
| 12 | `ContractType` | smallint NULL (enum) | Aero/Def | FFP / CPFF / TM / IDIQ / Commercial / None. Drives revenue posting rules in Sprint 14 AR; needed earlier as a queryable filter. Per FAR Part 16 + DCAA pattern. | Revenue rules in Sprint 14 |
| 13 | `QualityProgram` | smallint NULL (enum) | Aero/Def | None / ISO9001 / AS9100 / AS9120 / IATF16949 / Custom. Drives FAI requirement in Sprint 14 Quality. ABS demo: shows we know this exists. | FAI workflow Sprint 14 |
| 14 | `ExportControl` | smallint NOT NULL DEFAULT 0 | Aero/Def | 0=None, 1=EAR99, 2=EAR-controlled, 3=ITAR. Drives later RLS visibility filter + watermarks. AS9100 / DFARS suppliers must segregate. | RLS row-filter Sprint 15 |

### New table: `ProjectAmendments`

See §5 for the full design. Summary: **append-only event log** of customer-issued contract changes (value delta, scope delta, schedule delta). `CustomerProjects.ContractValue` stays the **original baseline**; the *effective* contract value is a service-layer SUM. This matches Oracle Project Accounting (baseline + change documents), SAP PS (amendment vs baseline), Acumatica (Change Orders for Commitments), and AIA G701 (net-change tracking). Avoids the trap of mutating the baseline.

### Total impact

- **14 new nullable columns on `CustomerProjects`** — all NULL-tolerant, zero default-data work.
- **1 new table (`ProjectAmendments`)** — 14 columns; service layer can ignore until PR #5+.
- **3 indexes** (risk-sort, project-amendment lookup, locked-until backoff).
- **0 RLS changes** — tenant scoping stays at service layer via `CompanyId` per PR #1 baseline.
- **Estimated migration runtime against current data:** sub-second. There are no `CustomerProjects` rows in production yet.

---

## 2. Dimension 1 — AI infusion on a project entity

### 2.1 Survey

| System | AI presence at project level | Shape (row-level vs separate cache vs ephemeral) | What we steal |
|---|---|---|---|
| **Acumatica AI Studio (2025 R2)** | AI Automation can summarize project budget variance, autofill document field values from LLM. Experimental in R2; standard in 2026 R1. | Per-document autofill (field-level), not a single project-summary column. Generated values written back to the row. | Field-level confidence in updating row data via async worker. |
| **SAP Joule (S/4HANA Public Cloud)** | Conversational copilot; can summarize project objects on demand. Joule is **request-time** generation today (no persistent summary column). | Ephemeral — generated when user asks. No row-level cache shipped. | Decision: we DO want a cache (offline voice + cockpit-load latency). Don't copy Joule's ephemeral model. |
| **Microsoft Copilot in D365 Project Operations** | Periodic risk + mitigation generator; writes back into **Issues** and **Risks** tables (separate child entities), and into a generative **Status Report** narrative. | Separate child table for structured (Risks); narrative cached separately. Periodicity-driven refresh. | Periodic refresh model + the *split* of (numeric score on project) + (narrative text). |
| **Asana Smart Status + Smart Goals** | Suggests "On Track / At Risk / Off Track" + drafts narrative summary; user accepts/edits. | Smart-status writes to existing project status fields; narrative is editable text on the status record. | The 3-state tone enum ("Green/Amber/Red"). Cheap UI win. |
| **Linear AI** | Project + Initiative **Health** ("On track / At risk / Off track") + AI-generated weekly digest. AI flags risk from cycle-time, scope-creep, comment-volume signals. | Health is a row column (`health` enum); digest is generated to inbox not stored on project. | Health enum as a separate column from numeric risk score. |
| **Notion AI database properties** | First-class **AI Autofill** property type: AI Summary, Keywords, Custom Autofill. Configurable refresh (manual / on create / on edits / schedule). | Database-column-level — AI output IS a row property. | The "AI as a typed row property" mental model. Validates row-level cache. |
| **ClickUp Brain** | AI Fields on tasks (summary, update, action items, custom). Auto-tracked progress + risk per task. | Row-level (column) cached values. | Idempotency / refresh-on-event pattern. |
| **Monday AI** | AI columns; project status auto-recomputed. | Row-level cached column. | Same row-level pattern as Notion/ClickUp/Monday. |
| **Epicor Prism (vertical AI agents over Kinetic)** | Agent-mediated; surfaces insights and next-best-actions rather than persistent project columns. Vertical (per-industry) agents. | Mostly ephemeral / chat-window. | Validates: Joule + Prism keep AI ephemeral. Asana/Linear/Notion/Click/Mon keep AI on row. We pick row-level cache + ephemeral chat — both. |

### 2.2 Recommendation — what lives on `CustomerProjects` and what doesn't

**On the row (PR #1.5):**

- `RiskScore` (smallint 0–100, NULL means "not yet scored")
- `RiskTone` (smallint enum 0=Green/1=Amber/2=Red)
- `AiSummaryText` (text)
- `AiSummaryModel` (varchar 64)
- `AiSummaryGeneratedAt` (timestamptz)
- `AiRefreshLockedUntil` (timestamptz) — backoff so a flood of events doesn't trigger N concurrent LLM calls.

**Not on the row — kept ephemeral or in existing tables:**

- **Embedding vector** — already lives in `Embeddings` table per Sprint 12C (`halfvec(1024)`, polymorphic `(EntityType='CustomerProject', EntityId=...)`). Adding a vector column to `CustomerProjects` would double-store and break the Sprint 12C abstraction. The PR #2 service must just *enqueue* an embed job when AI summary changes.
- **Suggested next actions** — these are highly volatile and per-user-role. Keep as a chat-window / cockpit-side render at request time, not a cached column. Re-derive from chain-of-evidence + risk signals. (D365's Issues/Risks tables are an alternative — we defer to Sprint 14.)
- **Voice context preset** — per ADR-014, voice infrastructure already has Tag Helper-driven context. No new column needed; the project's `Id`, `Code`, `Name`, `Status`, `Mode`, `PrimaryCustomerId`, `RiskScore`, `AiSummaryText` are already accessible via the standard `VoiceReadyPageModel`.

### 2.3 Refresh strategy (service-layer guidance for PR #2)

Recommended pattern for `IProjectAiService` (will be built in Sprint 13.5 PR #4 or later, NOT in PR #1.5):

1. **Event-driven enqueue.** Whenever a downstream signal fires (ProductionOrder status change, EVM rollup completes, ProjectAmendment created, ChainOfCustody edge added with relevant edge type), the event handler enqueues a job onto an outbox.
2. **Idempotent worker.** The worker reads the job, checks `AiRefreshLockedUntil > now()` — if yes, drops. Otherwise sets the lock to `now() + 5 min`, calls the LLM, writes back `AiSummaryText / Model / GeneratedAt`, then clears the lock.
3. **Soft cap on cost.** Worker maintains a per-CompanyId per-day budget counter. When exceeded, falls back to no-AI mode (UI shows the most recent cached summary with staleness label).
4. **Risk score is deterministic — no LLM needed.** Compute from inputs available in PR #1.5: `(ProjectedEndDate > TargetEndDate)`, `(CostToDate / EstimatedTotalCost > PercentComplete)`, count of open ProductionOrders past schedule, count of amendments in last 30 days. **This is critical** — Asana / Linear / D365 all let LLMs *narrate* risk but compute the score from structured signals. LLM scoring is non-deterministic and audit-hostile.

### 2.4 Anti-patterns explicitly called out

- **Do not put the embedding vector on `CustomerProjects`.** It belongs in `Embeddings` per Sprint 12C ADR. Double-storage breaks the abstraction and write-amplifies every row update.
- **Do not let the LLM compute `RiskScore`.** Score is structured; narrative is text. Mixing them makes you unable to A/B model upgrades.
- **Do not store suggested-actions as a row column.** They go stale in seconds (a click on the cockpit invalidates them). Render at request time from current state.
- **Do not skip the model version (`AiSummaryModel`).** Model upgrades change summary style/length; auditors need to know which model wrote what.
- **Do not refresh on every write.** That's how you 100x your LLM bill. The `AiRefreshLockedUntil` + outbox pattern is non-negotiable.
- **Do not make `RiskTone` derived from `RiskScore` alone.** Tone needs to also encode "we don't know yet" (NULL) vs. "we know and it's green" (0). Storing both is cheap and explicit.

### 2.5 What stays deferred to v2

| Deferred item | Why | Target sprint |
|---|---|---|
| Suggested next-action **table** | D365 model. Useful but volatile; render-time is fine for v1. | Sprint 14+ |
| Per-action user feedback ("Apply / Dismiss / Snooze") + recommendation outcomes telemetry | Per the strategic-absorption locked 2026-05-20 (Item 4). Worth doing as a cross-cutting `RecommendationOutcomes` table, not a per-entity column. | Sprint 14 (cross-cutting) |
| Voice-form-spec emission per project mode | Already handled by ADR-015 dynamic voice-form-spec — no per-project column needed. | Already shipped |
| `AiLock` / human-override flag | YAGNI for v1. The summary is read-only narrative; if a user wants to override, they can disable AI on the project (future). | Sprint 16 if we add user-editable project fields |
| Confidence score on the AI summary | LLM-self-rated confidence is unreliable. Skip until we have a calibration framework. | Sprint 21 (launch hardening) |

---

## 3. Dimension 2 — EVM / POC accounting fields

### 3.1 Survey

| System | POC method support | Required fields | Where stored |
|---|---|---|---|
| **SAP S/4HANA PS** | Cost-based POC, duration POC, work POC, milestone POC. Standard "Cost-based PoC" = `(Actual Cost / Plan Cost) × 100`. Result Analysis posts revenue accrual. | `EstimatedTotalCost` (Plan Cost), `ActualCost` (rollup), `PercentComplete`, EVPOC table for periodic snapshots. | Row + EVPOC snapshot table keyed on object + period. |
| **Oracle Project Accounting** | Cost-cost (most common), labor hours, units. Baseline budget required before POC works. | Baseline budget (original cost), current commitment, actual cost, % complete, ETC (Estimate To Complete), EAC. | Baseline + Current Working budget versions. |
| **D365 Project Operations** | Cost-cost, effort-based, units, milestone. | EstimatedCost, ActualCost, ProgressPercent (per task + roll-up). | Per-task + project-roll-up. |
| **Acumatica Construction Edition** | POC at project; Cost Projection Calculator updates ETC. | RevisedBudget, CostToDate, CommittedCost, EstimatedCostAtCompletion, RevenueRecognizedToDate. | Project + Cost Budget tab + commitment line items. |
| **IFS Cloud** | Cost-cost POC, milestone POC, manual. | Estimated cost, actual cost, percent complete. | Project + activity. |
| **Epicor Advanced Project Management** | Full EVM (BCWS / BCWP / ACWP / CV / SV / EAC). | All EVM PMB elements. | Project + WBS phases. |
| **ANSI/EIA-748 (the standard)** | Defines 32 (becoming 27 in Rev E) criteria across 5 categories. Required data elements: **BCWS, BCWP, ACWP, BAC, EAC**, plus CV, SV, CPI, SPI as derivatives. | Performance Measurement Baseline (PMB), monthly status reporting. | Required at "Control Account" level — coarser than WBS leaf. |
| **DCAA EVMS** | Required on cost-plus contracts over thresholds. Same fundamentals as ANSI/EIA-748. | Same as above + monthly Cost Performance Report (CPR) / IPMR. | Per Control Account; baseline change log required. |
| **ASC 606 (US GAAP)** | Permits over-time revenue when criteria met; allows input or output method. Input method = costs incurred / total expected costs. Excludes "unproductive inputs" (defective material, wasted labor). | Total expected costs (denominator), costs incurred (numerator), revenue recognized to date. | Per performance obligation — analogous to our `CustomerProject`. |

### 3.2 Recommendation — row-level vs snapshot

**On the row (PR #1.5) — minimum viable for ASC 606 input method + Acumatica-style POC:**

- `EstimatedTotalCost` (the denominator) — set at baseline, mutated only by amendments.
- `PercentComplete` (cached, refreshed by `ProjectEvmRollupService`).
- `ProjectedEndDate` (forecast end vs `TargetEndDate` committed — drives "off track" detection).
- `LastEvmRollupAt` (staleness marker).

**Not on the row — `ProjectEvmSnapshot` (deferred to PR #1.6 / Sprint 14):**

- Time-series of `(SnapshotDate, BCWS, BCWP, ACWP, BAC, EAC, CV, SV, CPI, SPI)`. This is the **EVM compliance shape** required by DCAA. It is *additive* and does not need to ship in PR #1.5.
- Justification: snapshots are append-only and queried as a time-series. Putting BCWS/BCWP/ACWP on the row would force overwrite on every recompute and lose history. Every surveyed system that does real EVM has a snapshot table.

**`CostToDate`, `BillingsToDate`, `OverUnderBilling` — explicitly excluded from PR #1.5.**

These three are *derivatives* read from posting tables (`ApPosting`, `GlPosting`, `ArInvoice`) and should be computed by the rollup service, not stored on the project row. Storing them risks lock-step write amplification on every ledger post. Add them to `ProjectEvmSnapshot` when that table lands.

### 3.3 EVM Baseline lock

The standard EVM pattern is to **lock a Performance Measurement Baseline (PMB)** at project kickoff: original `ContractValue`, original `EstimatedTotalCost`, original `TargetEndDate`. After lock, changes flow exclusively through amendments (Dimension 4). The fields on `CustomerProjects.ContractValue` + the new `EstimatedTotalCost` + existing `TargetStartDate/EndDate` already give us the baseline shape — no `EvmBaseline` JSON snapshot column needed. The `ProjectAmendments` table is the change log.

If we later need historical baseline snapshots (e.g., "what was the baseline 60 days ago"), that's `ProjectBaselineSnapshot` and lives next to `ProjectEvmSnapshot`. Defer to Sprint 14.

### 3.4 Anti-patterns explicitly called out

- **Do not store `CostToDate` on the project row.** Triggers cascading writes on every AP / GL posting. Compute from the ledger.
- **Do not let `PercentComplete` be user-edited freely.** Per ASC 606, the input method is *prescriptive*: progress = costs-incurred / total-expected-costs (with unproductive inputs excluded). User-editable PoC becomes a revenue-fraud vector. Keep `PercentComplete` service-computed; if a user wants to override, that's a separate `ManualPercentComplete` field with audit trail in Sprint 14.
- **Do not put EVM CV / SV / CPI / SPI on the project row.** They are time-series. Snapshot table.
- **Do not compute POC at request time from raw postings.** Million-row scans on every Program Cockpit load. Cache via `ProjectEvmRollupService`, refresh on event + nightly cron.
- **Do not couple `ProjectedEndDate` to MS Project / Primavera-style critical-path math.** v1 = simple linear extrapolation: if 50% complete after 100 days, projected end = `Start + 200 days`. Good enough for the "why is job X late?" voice query.

### 3.5 What stays deferred to v2

| Deferred item | Why | Target sprint |
|---|---|---|
| `ProjectEvmSnapshot` table (time-series EVM) | DCAA compliance is a Sprint 14 concern; v1 ships AAS / commercial customers first. | Sprint 14 |
| `ProjectBaselineSnapshot` | "What was the baseline 60 days ago" is a v2 audit need. | Sprint 14 |
| Critical-path-aware `ProjectedEndDate` | Needs full task DAG. We have flat `ProjectPhases` today. | Sprint 18 (scheduling cockpit) |
| Multi-currency revenue recognition | We have `Currency`; FX revaluation is a Sprint 14+ AR concern. | Sprint 14 |
| Cost-Plus award fee accruals | Specific to CPAF gov contracts. | Sprint 21 (launch hardening) |
| ETC (Estimate to Complete) as a stored field | Service-computed from `EstimatedTotalCost - CostToDate`. No need to store. | n/a |

---

## 4. Dimension 3 — Aerospace / defense / regulated-industry quality fields

### 4.1 Survey

| Source | Field signal | Lives at customer level? Project level? Item/lot level? |
|---|---|---|
| **AS9102 First Article Inspection** (the standard) | Form fields: Part Number, Part Name, Drawing Number, Drawing Rev, Part Serial Number → **Part Type** (Rev C), Customer, FAI Report Number, Reason for FAI, manufacturer info. ~10 sections with "CR" (Conditionally Required) and "O" (Optional) qualifiers. | **Item/lot level** primarily — but each FAI is tied to a project/contract. Customer is on the form header. |
| **AS9100 (the QMS)** | Quality program scoping; risk management requirements at project level. | Customer (which QMS they require) + Project (which artifacts apply). |
| **ITAR / EAR (export control)** | ECCN (5-char alphanumeric for EAR-controlled items), USML category for ITAR. EAR99 is default for unrestricted. | **Item level** for ECCN; **project level** for "is this whole project ITAR-controlled" segregation flag. |
| **DCAA contract types** | FFP / CPFF / T&M / IDIQ. CPFF and CPIF require auditable cost accounting + CAS compliance + incurred-cost tracking. | **Project / contract level**. |
| **CAGE Code** | 5-char alphanumeric supplier identifier; required by DFARS 252.204-7001. NCAGE for foreign vendors. | **Vendor level**, not project level. (Already exists on `Vendor` entity per PR #4 — confirm.) |
| **DUNS** | 9-digit identifier; replaced by UEI for SAM registration, still common in supplier portals. | **Customer / Vendor level**. |
| **Customer PO Number** | Universally required on contract paperwork (AIA G701, mil contracts, commercial). Distinct from internal project code. | **Project level** — the PO that birthed the project. |

### 4.2 Recommendation — Customer-entity vs CustomerProject-entity split

Right separation:

**On `Customer` entity (already exists — verify in PR #1.5 or defer to PR #6 polish):**

- `DefaultQualityProgram` (per-customer default; many customers always demand AS9100)
- `DefaultExportControl` (Boeing always treats us as ITAR-capable; Acme Plumbing is EAR99)
- `CageCode` / `Duns` / `Uei` (supplier identifiers when the customer is also a gov supplier)
- `RequiresFaiByDefault` (some customers want FAI on every project; some never)

**On `CustomerProject` entity (THIS migration, PR #1.5):**

- `CustomerPoNumber` — different on every project even within the same customer
- `ContractType` — even within one customer, projects may be FFP or T&M
- `QualityProgram` — overrides customer default (a customer who normally needs AS9100 might have one ISO9001 project)
- `ExportControl` — overrides customer default (some projects within an ITAR-eligible customer are commercial)

**Quotation + RequisitionId — explicitly excluded from PR #1.5.**

The user-supplied candidate `QuotationId` (link to source quote) is real and useful but the `Quotation` entity does not yet exist in IndustryOS. Adding the FK now would be a dead reference. Defer to whichever sprint creates `Quotations` (likely Sprint 14 Sales).

**FAI is NOT a CustomerProject field — it's a child entity.**

Per the ABS customer profile, the FAI artifact is 126 rows of dimensional checks. It's a child of `Item` + `ProductionOrder` (the job that produced the first article), threaded to the project via the existing `Iqc` chain node. PR #1.5 adds `QualityProgram` so the project *declares* it needs FAI; the FAI workflow itself is Sprint 14.

### 4.3 Anti-patterns explicitly called out

- **Do not store classified information.** `ExportControl` enum is a **visibility flag** ("this row needs ITAR-cleared user filter"), NOT a place to store ITAR-controlled technical data. Technical data lives in the doc-management system (out of scope for v1). We are storing the *jurisdiction marker*, period. The DFARS / CMMC compliance posture is a Sprint 21 launch concern.
- **Do not put `CageCode` on `CustomerProject`.** CAGE is the *vendor's* identifier (DLA-issued). If we are the vendor to a gov prime, our own CageCode lives on `Company`. If the customer is itself a gov supplier, that goes on `Customer`. Never project.
- **Do not duplicate `QualityProgram` across project + every job.** Inheritance: project declares; jobs inherit unless overridden. Same model as `ExportControl`.
- **Do not let `RequiresFai` be a separate boolean.** `QualityProgram != None` is the signal; FAI requirement falls out of the program. Boolean explosion is how schemas turn into mud.
- **Do not turn `ContractType` into a heavyweight type hierarchy.** Smallint enum + service-layer rules. Five values cover 99%.
- **`TrustedSupplierTier` (candidate field) — rejected.** It belongs on `Vendor`, not `CustomerProject`. A project doesn't have a supplier tier; a vendor does.
- **`ProgramSecurityClassification` (candidate field) — rejected for v1.** Confidential/Secret/etc. classification is a CMMC/NIST 800-171 concern. Including it implies we handle the data correctly, which we do not yet. Defer to Sprint 21 launch hardening; surface a single `ExportControl` enum now to validate the shape.

### 4.4 What stays deferred to v2

| Deferred item | Why | Target sprint |
|---|---|---|
| `RequiresFai` separate flag | Implied by `QualityProgram` enum. | n/a — folded |
| `DcaaCompliant` boolean | Implied by `ContractType in (CPFF, CPIF, CPAF)`. | n/a — folded |
| `CageCode` on Project | Belongs on `Vendor` / `Company`, not Project. | Sprint 14 (vendor onboarding polish) |
| `TrustedSupplierTier` | Belongs on `Vendor`. | Sprint 14 |
| `ProgramSecurityClassification` (Confidential / Secret) | Implies controlled-data handling we don't have. | Sprint 21 launch hardening |
| Full ECCN string field | Item-level field, not project-level. | Sprint 15 (Subcontracting / ADR-022 satellite) |
| FAI workflow + dimensional check rows | Child entity, big feature. | Sprint 14 Quality |
| `QuotationId` FK | `Quotation` entity doesn't exist yet. | Sprint 14 Sales |

---

## 5. Dimension 4 — Change orders / contract amendments

### 5.1 Survey

| System | Amendment model | Baseline mutation? |
|---|---|---|
| **SAP S/4HANA PS** | Amendments to PO at the project; budget recalc via change documents. Original baseline preserved. | No — baseline preserved; current commitment summed. |
| **Oracle Project Accounting** | **Change Documents** create financial impact records; baseline plan version is locked at "Original Baseline"; current working plan version is mutable. New baseline can be created at any time but the original is preserved. | **No** — Original Baseline is immutable; new baselines are new versions. |
| **D365 Project Operations** | Project Operations leverages PO change-request workflows; Engineering Change Management for products. Project-level contract amendment is lighter — usually expressed as new contract lines. | Mixed. |
| **Acumatica (Construction Edition)** | First-class **Change Order workflow**. Three commitment values: **Original**, **Revised (cost budget after CO)**, **Committed**. Original is locked; Revised is computed `Original + sum(approved COs)`. | **No** — original locked; revised = original + Σ approved. |
| **Epicor APM** | Change orders adjust the WBS; baseline snapshots preserved. | No. |
| **IFS Cloud** | Project change management; baseline retained. | No. |
| **AIA G701 (industry standard form)** | Defines fields: Project, Owner, Architect, Contractor, Change Order Number (sequential per project), Date, Description of Change, **Net Change by Previously Authorized Change Orders**, **Contract Sum prior to this Change Order**, **Amount of increase or decrease**, **New Contract Sum**, **Days of increase/decrease in Contract Time**, **New Date of Substantial Completion**, **Signatures (Owner, Architect, Contractor)**. | **No** — the baseline ("original Contract Sum") is preserved; running net change is tracked. |
| **DCAA / FAR** | Modifications classified as bilateral (negotiated) or unilateral. All preserve baseline; impact tracked separately. | No. |

**Universal pattern across all 8 systems: the original baseline is locked; amendments are an append-only child entity; the "current effective" value is a computed SUM.** Acumatica's "Original / Revised / Committed" triple is the cleanest expression.

### 5.2 Recommended `ProjectAmendments` table shape

```
ProjectAmendments
─────────────────
Id                      bigserial PK
CustomerProjectId       integer NOT NULL FK→CustomerProjects (CASCADE)
AmendmentNumber         integer NOT NULL                   -- per-project sequence (1, 2, 3 …)
EffectiveDate           date NOT NULL                       -- when the change takes effect
ChangeType              smallint NOT NULL                   -- 0=Scope, 1=Schedule, 2=Value, 3=Combined
Reason                  varchar(2000) NULL                  -- short narrative (customer's words)
ScopeNarrative          text NULL                           -- what was added/removed; longer-form
ValueDelta              numeric(18,4) NOT NULL DEFAULT 0    -- positive or negative
TargetStartDateDelta    integer NULL                        -- days; nullable (no schedule change)
TargetEndDateDelta      integer NULL                        -- days; nullable
SourceQuotationId       integer NULL                        -- FK to future Quotation table (nullable for v1)
CustomerReference       varchar(100) NULL                   -- customer's CO number / PO change order ref
Status                  smallint NOT NULL DEFAULT 0         -- 0=Draft, 1=Submitted, 2=Approved, 3=Rejected, 4=Withdrawn, 5=Voided
ApprovedById            integer NULL                        -- internal approver
ApprovedByName          varchar(100) NULL                   -- snapshot for audit
ApprovedAt              timestamptz NULL
CustomerSignatureAt     timestamptz NULL                    -- when customer countersigned
Notes                   text NULL                           -- internal notes
CreatedAt               timestamptz NOT NULL DEFAULT now()
CreatedBy               varchar(100) NULL
ModifiedAt              timestamptz NULL
ModifiedBy              varchar(100) NULL

UNIQUE (CustomerProjectId, AmendmentNumber)
INDEX  (CustomerProjectId, Status, EffectiveDate)
```

### 5.3 Relationship to `CustomerProjects.ContractValue`

**Recommendation: keep `CustomerProjects.ContractValue` as the ORIGINAL baseline. Compute the "effective" contract value at the service layer.**

```
EffectiveContractValue = CustomerProjects.ContractValue
                        + SUM(ProjectAmendments.ValueDelta
                              WHERE Status = Approved
                                AND EffectiveDate <= asOfDate)
```

This is the Acumatica / Oracle / SAP / AIA G701 pattern. Three consequences:

1. **`ContractValue` becomes immutable after first amendment.** Service layer enforces this. A correction to the original (mistyped baseline) is itself a `ChangeType=Value` amendment of type "Correction" — clean audit trail. (We can add `ChangeType=4 Correction` if needed; not in v1.)
2. **Reports show "Original / Revised / Committed" triple.** Per Acumatica. `Revised = Original + SUM(approved CO ValueDelta)`. `Committed = SUM of open POs + posted costs + open WO commitments`. Read-time computation; no extra columns on `CustomerProjects`.
3. **Revenue recognition reads `EffectiveContractValue`** as of the relevant period end. This is the ASC 606 transaction-price update mechanism (modifications treated as part of original contract when change extends original).

### 5.4 Why a separate table (not JSONB or audit log)

- **Discoverable in cockpit UI.** Program Manager needs to see "this project has 3 amendments totaling +$45K" — that's a queryable child table, not a buried JSON blob.
- **Voice-narratable.** "What changed on the Weir Q2 Power Frame project?" — easy SQL when amendments are first-class.
- **Customer-side approvals capturable.** `CustomerSignatureAt` and a future `CustomerSignatureDocId` are typed columns; useless in a JSON blob.
- **EVM-compatible.** When the EVM snapshot table lands (Sprint 14), it joins to `ProjectAmendments` to track baseline-vs-amended variance.

### 5.5 Linkage to Jobs (ProductionOrders)

**Out of scope for PR #1.5.** A future `ProjectAmendmentJobLink` M:N table can connect an amendment to the ProductionOrders affected. v1 ships the amendment header only; service layer can render "this amendment touches Production Orders X, Y, Z" via ChainOfCustody traversal or a manual link table in Sprint 14.

### 5.6 Anti-patterns explicitly called out

- **Do not mutate `CustomerProjects.ContractValue` when an amendment is approved.** Loses baseline. Breaks ASC 606 contract-price tracking. Every surveyed system avoids this.
- **Do not delete amendments.** Use `Status=Voided`. Audit trail.
- **Do not let amendment number be a GUID or string.** Per-project monotonic integer for human use ("CO #3 increases value by $12K"). Universal across AIA / SAP / Oracle.
- **Do not couple amendment approval to AR posting.** Service layer concern; an amendment can be approved without any revenue posting (e.g., scope-only change with no value delta). Decoupling means revenue rules can evolve independently.
- **Do not require all delta fields to be non-null.** A scope-only change has `ValueDelta = 0` and null schedule deltas. The fields are nullable / zero-defaulted to reflect this.
- **Do not omit `CustomerReference`.** ABS will get a customer-issued change-order number ("Weir CO-2026-014"); we need to capture it for traceability.

### 5.7 What stays deferred to v2

| Deferred item | Why | Target sprint |
|---|---|---|
| `ProjectAmendmentJobLink` (M:N to Production Orders) | Per-job impact rollup is a Sprint 14 concern; v1 ships header only. | Sprint 14 |
| Customer-side digital signature capture | Needs e-signature integration. | Sprint 21 launch |
| Amendment-driven `ProjectBaselineSnapshot` regeneration | Needs EVM snapshot table. | Sprint 14 |
| Workflow / approval routing (multi-step) | Service-layer state machine. | Sprint 14 (cockpit) |
| Attached PDFs / scanned signed COs | Doc-management cross-cutting concern. | Sprint 17 |
| Reversing-amendment ("oops" rollback) tooling | YAGNI for v1; manual `Voided` status. | Sprint 16 |

---

## 6. Anti-patterns called out across all four dimensions (consolidated)

These are the things that will rot the schema if we miss them now.

1. **Storing the embedding vector on `CustomerProjects`.** It lives in `Embeddings`. Sprint 12C abstraction.
2. **Letting an LLM compute `RiskScore`.** Structured signals only; LLM narrates.
3. **Refreshing AI on every write.** Outbox + `AiRefreshLockedUntil` backoff is non-negotiable.
4. **Storing `CostToDate` / `BillingsToDate` on the row.** Cascading writes from every ledger post. Compute from ledger.
5. **Letting `PercentComplete` be user-edited freely.** ASC 606 revenue-fraud vector.
6. **Putting EVM CV/SV/CPI/SPI on the row.** Time-series → snapshot table.
7. **Mutating `ContractValue` on amendment approval.** Loses baseline; breaks ASC 606.
8. **Storing classified info under `ExportControl`.** It's a visibility flag, not technical data.
9. **Putting `CageCode` on Project.** Vendor/Company concern.
10. **Booleans for things implied by enums** (`RequiresFai`, `DcaaCompliant`). Schema-mud.
11. **GUID amendment numbers.** Humans need sequential.
12. **Coupling Project FK to Job at the Production Order LINE level.** Header only.
13. **Adding `QuotationId` FK before `Quotations` table exists.** Dead reference.
14. **Per-action `ApplyDismiss` columns on `CustomerProjects`.** Belongs in a cross-cutting `RecommendationOutcomes` table per strategic-absorption Item 4.

---

## 7. What stays deferred — index by target sprint

| Target sprint | Deferred items |
|---|---|
| **Sprint 14 (Manufacturing polish + Sales + Quality)** | `ProjectEvmSnapshot` table · `ProjectBaselineSnapshot` · `Quotations` + FK back-fill · FAI workflow + dimensional rows · `ProjectAmendmentJobLink` · multi-step amendment approval · cross-cutting `RecommendationOutcomes` table · `Customer.DefaultQualityProgram` / `DefaultExportControl` / `RequiresFaiByDefault` |
| **Sprint 15 (Subcontracting)** | Full ECCN string field on Items · `SubcontractCert` chain node · `ExportControl` row-filter RLS |
| **Sprint 16** | `AiLock` / human-override · Reversing-amendment ("oops") tooling · User-editable `ManualPercentComplete` override |
| **Sprint 17** | Doc-management for attached signed COs / FAI PDFs |
| **Sprint 18 (Scheduling cockpit)** | Critical-path-aware `ProjectedEndDate` |
| **Sprint 21 (Launch hardening)** | `ProgramSecurityClassification` (Confidential/Secret) · Customer-side digital signature · LLM confidence calibration · CPAF award fee accruals |

Total deferred items: ~20. None of them block PR #1.5.

---

## 8. Sources (vetted)

**AI infusion:** Acumatica 2025 R2 + AI Studio FAQ · SAP Joule (S/4HANA Public) · Microsoft Copilot in D365 Project Operations + Risk Assessment · Asana Smart Status · Linear Initiative+Project updates & AI features · Notion AI database properties · ClickUp Brain · Epicor Prism · pg_semantic_cache architecture notes.

**EVM / POC:** NDIA EIA-748 + 32 (Rev E: 27) criteria · Humphreys planning-ahead for Rev E · BCWS/BCWP/ACWP/CV/SV definitions · SAP S/4HANA POC + cost-based PoC · ASC 606 percentage-of-completion · CLA POC overview · Foundation Software ASC 606 transfer-of-control · DoD EVMSIG 14MAR2019 · Oracle baseline draft budget.

**Aerospace / defense quality:** AS9102 FAI requirements (BPR Hub, Endevco Rev C FAQ) · Advisera AS9100 Rev D + FAI · CVG Strategy ECCN/ITAR-EAR · MIT ECCN key concepts · Shipping Solutions USML vs ECCN · Cabrillo federal contract types · DCAA + FFP · CRS R44490 DUNS/CAGE · DFARS 252.204-7001 + Subpart 204.72 · Sweetspot CAGE Code glossary.

**Change orders / amendments:** AIA G701-2017 + Procore guide + AIA instructions · Acumatica Construction Change Orders (Original / Revised / Committed) + workflow shape · Oracle Project Accounting baseline preservation · SAP PS WBS amendment community thread.

---

**See also:**
- `Migrations/20260522_AddCustomerProjectFoundation.cs` — PR #1 baseline migration
- `Migrations/20260522_AddChainOfCustodyGraph.cs` — style template for the PR #1.5 migration
- `.ship/drafts/sprint-13.5-pr1.5-fields.sql` — the draft additive migration SQL produced alongside this memo
