# Structural integrity audit — 2026-05-08

**Posture:** screening pass before pivoting to product-roadmap work (Sprint 1 PWA, Sprint 2 voice-to-WO, Sprint 3 conversational asset twin).
**Question being answered:** does the core business-process plumbing have a load-bearing flaw that would force days of rework mid-feature?
**Answer:** **yes — multiple.** The system has the SHAPE of an EAM but the financial plumbing between PO → Receipt → Invoice → GL → Asset/CIP is largely disconnected. Inventory, GL postings, and CIP cost accumulation do not happen automatically anywhere in the working flows. The only end-to-end financial paths that work today are: depreciation posting, asset disposal posting, and asset improvement → snapshot recompute (PR [#27](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/27)).

This document is the canonical findings list. Sprint 0.5 work tracks against it. Severity-1 findings block the roadmap pivot; Severity-2 should fix before 1.0; Severity-3 is the residual smell list.

---

## End-to-end flow narratives

The load-bearing artifact. If we cannot write a clean narrative for a flow, the chain is broken — that itself is the finding.

### 1. Purchasing → Receiving → AP

PO is created in [Pages/Purchasing/Index.cshtml.cs](../../Pages/Purchasing/Index.cshtml.cs) and edited in [Pages/Purchasing/Details.cshtml.cs](../../Pages/Purchasing/Details.cshtml.cs). Status flips Draft → PendingApproval → Approved happen in `Details.cshtml.cs:260-293` via `SyncStatusFkAsync` — **no period guard, no concurrency token, no outbox event.**

Goods Receipt is written in `Pages/Receiving/Receive.cshtml.cs:206-261`. It creates a `GoodsReceipt` + `GoodsReceiptLine` rows and increments `PurchaseOrderLine.QuantityReceived` (`:246`) — **but does NOT increment `ItemInventory.QuantityOnHand`, does NOT create an `ItemTransaction`, and does NOT post a GR-accrual journal entry**. PeriodGuard (`:188`) gates only the receipt-row write since no GL entry follows.

AP invoice is captured in `Pages/AccountsPayable/Details.cshtml.cs:160-217`. `OnPostApproveAsync` flips status and stamps `ApprovedAt`; `OnPostRecordPaymentAsync` updates `AmountPaid` — **but neither creates a JournalEntry, neither calls `_periodGuard`, and neither calls `CipAutoCostPostingService.PostFromVendorInvoiceLineAsync`**. The `InvoiceMatchingService` computes `MatchStatus` defensively but is not invoked by the approve path.

**The chain is broken at GR→Inventory, GR→GL, Invoice→GL, Invoice→Asset/CIP.** There is no automatic path from PO/GR/Invoice to capitalize an asset's `AcquisitionCost`. **Certainty: HIGH.**

### 2. Work Order lifecycle

WorkRequest → MaintenanceEvent conversion lives in `Services/Maintenance/WorkRequestConversionService.cs`. PMTemplate → MaintenanceEvent is created by `Services/Maintenance/PMSchedulerService.cs:296-389` — generates the WO, copies operations from the released revision, adds `WorkOrderPart` from `template.Items`.

Crucially, line `:330` writes `CustomField1 = $"PMTA:{occurrence.Id}"` — but `Services/MaintenanceService.cs:248-259` (`ParsePMTALinkage`) reads that string as a **`PMTemplateAsset.Id`**, then queries `_context.Set<PMTemplateAsset>()` (`:222`) using that occurrence.Id. **The ID namespaces don't match — `PMOccurrence.Id` is being looked up as `PMTemplateAsset.Id`.** `LastCompletedDate` and `NextDueDate` updates on the PM assignment will silently miss-target rows or no-op.

WO operations / labor / parts / outside-vendor costs flow through `Pages/Maintenance/Details.cshtml.cs:165-199`: the user manually types `laborCost`, `partsCost`, `materialsCost`, `outsideVendorCost` and the page sets `evt.ActualCost = sum-of-four` (`:195`). **There is no auto-rollup from `WorkOrderOperationLabor.TotalCost`, `WorkOrderOperationPart.TotalCost`, or `WorkOrderPart.QuantityUsed * UnitCost`** — the per-operation labor/parts data is purely ornamental for cost.

WO closeout in `Services/Maintenance/CloseoutService.cs:128-241` only writes status + summary + emits `WorkOrderClosedV1` outbox; **does not post a JournalEntry, does not increment any Asset cost, does not invoke `CipAutoCostPostingService.PostFromWorkOrderAsync`**.

Asset cost only changes via the **manual** `OnPostCapitalizeAsync` button on `Pages/Maintenance/Details.cshtml.cs:269-311` — which writes `asset.AcquisitionCost += amount`, creates a `CapitalImprovement`, **but skips `_periodGuard` and skips `_depBackfill.RecomputeAssetAsync`**. **Certainty: HIGH.**

### 3. CIP lifecycle

`CipProject` is created in `Services/CipService.cs:60-67`. Costs accumulate via `CipCost` rows. Three pathways exist in `Services/Cip/CipAutoCostPostingService.cs`: `PostFromWorkOrderAsync(:26)`, `PostFromReceiptLineAsync(:70)`, `PostFromVendorInvoiceLineAsync(:125)` — and the trace queries in `Services/Cip/CipTraceQueryService.cs` are wired correctly via `CipCost.SourceType` + `*Id` FKs.

**However: a repo-wide grep finds the only references to `PostFromReceiptLineAsync` / `PostFromVendorInvoiceLineAsync` / `PostFromWorkOrderAsync` outside `Services/Cip/` are in `Program.cs:155` (DI registration) and audit/scan files. None of the Receiving, AP, or Maintenance pages call them.** PO Lines and Invoice Lines marked `CipProjectId` never produce a `CipCost` automatically — only manual entries via `Services/Cip/CipCostService.cs:43-88` do. PO header has `CipProjectId` in the model (`Models/PurchaseOrder.cs:111`) but no Purchasing UI sets it.

Settlement is `Services/Cip/CipCapitalizationService.cs:83-168` — creates the new Asset, hardcodes `Account = "1500"` and `"1400"` string literals (`:124-138`), creates a JournalEntry, and creates `CipCapitalization` + `CipCapitalizationCost` mappings. **Settlement does not call `_periodGuard`, ignores Asset Book GL accounts, does not call `DepreciationBackfillService.RecomputeAssetAsync`, does not enqueue `cip.capitalized` outbox, does not stamp tenant scope on the new Asset** (the legacy `CipService.CapitalizeProjectAsync` does at `Services/CipService.cs:137`, but the typed-DTO `CipCapitalizationService` does not). No partial settlement is supported. **Certainty: HIGH.**

### 4. Asset Management

`Asset` (`Models/Asset.cs`) has a proper xmin RowVersion (`:516`), tenant via `CompanyId`, parent/child via `ParentAssetId`, lifetime cost via `AcquisitionCost`. **Asset has no FK to PO origin or CIP origin** — only string fields `PurchaseOrderNumber`/`InvoiceNumber` (`:92,95`) and the inverse `CipProject.ConvertedAssetId`. `AssetBookSettings` and `DepreciationRunDetail` (`Models/FiscalPeriod.cs:152`) exist.

Capital improvement: `Pages/Assets/Improve.cshtml.cs:99,127,143` correctly chains PeriodGuard → `Asset.AcquisitionCost += Cost` → `_depBackfill.RecomputeAssetAsync` (PR [#27](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/27)).

Disposal: `Pages/Assets/Dispose.cshtml.cs:128-207` correctly chains PeriodGuard → resolve GL accounts (book + book-gl-accounts) → write JournalEntry — but creates **no outbox event** for `asset.disposed`. There is no `AssetDisposal` model — only `PartialDisposal` and direct fields on Asset. **Certainty: HIGH.**

### 5. BOM / Items

`Item` (`Models/Item.cs:153-528`) has master-data, `ItemAlternate`, `ItemSupersession`, `ItemApprovedVendor`, `ItemCompanyStocking`, `ItemRevision`, `ItemImage`, `ItemInventory` (`:883-941`), `ItemTransaction` (`:955+`).

`ItemInventory.QuantityOnHand` is **read** by AI assistant (`Services/AiAssistantService.cs:278`) but **never written by any flow**. `ItemTransaction` is **never instantiated anywhere** in `Services/` or `Pages/Receiving/`.

There is **no BOM table** in the schema — no `BillOfMaterial`, `AssetBom`, or `AssetComponent` models exist. PMTemplate has `PMTemplateItem` (a one-level recommended-parts list, `Models/PMTemplate.cs:161`) but no nested BOM. Asset componentization is via `Asset.ParentAssetId` only.

Inventory-to-issuance path: `Pages/WorkOrders/Details.cshtml.cs:310-345` (`OnPostIssueMaterialAsync`) updates `WorkOrderPart.QuantityIssued/QuantityUsed` but **does not decrement `ItemInventory.QuantityOnHand`, does not create an `ItemTransaction`, does not post to GL**. Same page at `:99` and `:292` queries `_context.Items` with no tenant filter (cross-tenant leak in the picker dropdown). **Certainty: HIGH.**

### 6. PM (Preventative Maintenance)

Two parallel mechanisms coexist.

**(a) Modern:** `PMTemplate` + `PMSchedule` + `PMOccurrence` + `PMTemplateAsset` driven by `Services/Maintenance/PMSchedulerService.cs`. Tenant-scoped, idempotent unique index on `(Tenant,Company,Site,Template,DueDate)` (`Models/PMSchedule.cs:92`). NextDueDate logic at `:419-484` supports `IntervalDays`, `Weekly` (DOW mask), `Monthly` (DOM). **No meter-based recurrence implementation** despite `PMTriggerType.Meter`/`CalendarOrMeter` (`Models/PMTemplate.cs:8-12`) and `MeterReading` (`:226+`) existing — `ComputeDueDatesAsync` only consumes `CadenceType` (Calendar).

**(b) Legacy:** `MaintenanceSchedule` + `MaintenanceService.GenerateEventsFromSchedulesAsync` (`Services/MaintenanceService.cs:300-332`) — **no tenant scope**, simple recurrence. Both feed `MaintenanceEvent`.

Templates have `CalendarInterval`/`CalendarIntervalValue` (`Models/PMTemplate.cs:54-57`) but `PMTemplateAsset` (`:186+`) has its own `OverrideCalendarInterval`/`OverrideCalendarValue` — neither is read by `PMSchedulerService` (it reads `PMSchedule.CadenceType`). **The PMTemplate→PMTemplateAsset assignment is decorative** — `PMScheduler.CreateOccurrenceAndWorkOrderAsync` enumerates `PMTemplateAssets` (`:259`) only for the asset list; the cadence comes from `PMSchedule`. **Certainty: HIGH.**

---

## Severity 1 — would-block-roadmap-work

| # | Title | Files | What's wrong | Impact | Fix sizing |
|---|---|---|---|---|---|
| **S1-1** | **Receiving never moves inventory or accrues GL** | `Pages/Receiving/Receive.cshtml.cs:206-261` | Creates GR rows, increments `PurchaseOrderLine.QuantityReceived`, period-gates the GR write, but does NOT touch `ItemInventory.QuantityOnHand`, does NOT create `ItemTransaction`, does NOT create an accrual `JournalEntry` (Dr Inventory / Cr GR-Accrued). | Mobile receiving will inherit zero downstream effects. Stock counts will be wrong forever. Three-way match has nothing to match against on the inventory side. Partner integrations expecting `inventory.adjusted` or `gl.posted` get nothing. | **Design needed.** ~600–1000 LOC: `AccrualService`, `ItemMovementService`, GL account resolver. |
| **S1-2** | **PMTA marker collides — `PMSchedulerService` writes occurrence.Id, `MaintenanceService` reads as `PMTemplateAsset.Id`** | `Services/Maintenance/PMSchedulerService.cs:330` (`CustomField1 = $"PMTA:{occurrence.Id}"`); `Services/MaintenanceService.cs:215,222,248-259` | Two different record types' `Id` columns are conflated. WO completion's `UpdatePMAssignmentOnCompletionAsync` will silently miss-target rows or no-op. | PM "next due" date never advances correctly when WOs close — the headline PM lifecycle is broken. Voice-to-WO will close work orders that don't update the schedule. | **Design needed.** Add `PMOccurrenceId` and `PMTemplateAssetId` columns to `MaintenanceEvent`; deprecate `CustomField1` for this. ~80 LOC + migration. CLAUDE.md already names this as a "hack." |
| **S1-3** | **`CipAutoCostPostingService` is dead code** | `Services/Cip/CipAutoCostPostingService.cs:26-169`; only callers are DI registration `Program.cs:155` | The three Post-from-* methods designed to ingest CIP costs from Receipt, Invoice, and Work Order are never invoked in any page. PO line / invoice line / WO marked `CipProjectId` never produce a `CipCost`. | The whole CIP cost-accumulation chain is broken end-to-end. CIP capitalization can only ever capture *manual* `CipCost` entries, never PO/AP/WO sources. Financial-statement-impacting flaw. | ~150 LOC: invoke the three methods at the right SaveChanges sites in `Pages/Receiving/Receive.cshtml.cs`, `Pages/AccountsPayable/Details.cshtml.cs:OnPostApproveAsync`, `Pages/Maintenance/Details.cshtml.cs:OnPostCompleteAsync`. |
| **S1-4** | **CIP capitalization hardcodes GL accounts and skips PeriodGuard / depreciation backfill / tenant-stamp** | `Services/Cip/CipCapitalizationService.cs:83-168` | Lines `:124-138`: literal strings `"1500"` and `"1400"` for the JE account fields. Line `:101-110`: new Asset created with no `CompanyId` set. Method never calls `_periodGuard.EnsureCanPostAsync` even though it stamps `JournalEntry` rows. Depreciation does not start (no `RecomputeAssetAsync` or AssetBookSettings). | Capitalization on the wrong tenant company; depreciation never starts on capitalized assets; capitalizing into a closed period silently succeeds. | ~120 LOC: pull GL from BookGlAccount, set `CompanyId` from project, add PeriodGuard, add depreciation kickoff. |
| **S1-5** | **AP invoice approval/payment never posts to GL or invokes PeriodGuard** | `Pages/AccountsPayable/Details.cshtml.cs:160-233` | `OnPostApproveAsync` flips status, `OnPostRecordPaymentAsync` updates `AmountPaid`, `OnPostVoidAsync` flips status. None creates a JournalEntry (Dr Expense or Inventory / Cr AP) or checks PeriodGuard. `InvoiceMatchingService` is never invoked. | Invoice "Approved" and "Paid" mean nothing in the GL. There's no AP balance, no expense posting, no asset-cost increment from invoice. Voice-to-WO + invoice automation will produce ledgers with zero AP entries. | **Design needed.** ~400–700 LOC: AP-posting service, InvoiceMatchingService gate-call, period guard, GL resolution per line. |
| **S1-6** | **WO `ActualCost` is manual entry, not rolled up from operations** | `Pages/Maintenance/Details.cshtml.cs:165-199`; `Models/WorkOrderOperation.cs:128-130,133+` (Labor/Tools/Parts collections); `Models/WorkOrderOperation.cs:226+` (`WorkOrderOperationPart`) | Per-operation labor (`WorkOrderOperationLabor.TotalCost`) and per-operation parts (`WorkOrderOperationPart.TotalCost`) are computed properties; `Pages/WorkOrders/Details.cshtml.cs:47,49` aggregates them for display. But the WO completion handler ignores them and writes whatever the user types. `WorkOrderPart.QuantityUsed * UnitCost` is also bypassed. | Mobile/voice "complete this WO" flows have no canonical cost source. Cost flow to Asset (via Capitalize) uses an unreliable manually-keyed value. Reports will diverge from operation-level data. | ~150 LOC + UI revision. |
| **S1-7** | **WO part issuance does not decrement inventory or post to GL** | `Pages/WorkOrders/Details.cshtml.cs:310-369` (`OnPostIssueMaterialAsync` / `OnPostReturnMaterialAsync`); `Pages/WorkOrders/Details.cshtml.cs:99,292` (untenanted `_context.Items` query) | `OnPostIssueMaterialAsync` only updates the `WorkOrderPart` counter columns. No `ItemInventory.QuantityOnHand -= actualIssue`, no `ItemTransaction(Type=Issue)`, no Dr WO-cost / Cr Inventory journal. Item lookup `:99,292` has no tenant filter — cross-tenant leak in the picker dropdown. | Inventory drift forever. Cost on WO, cost on asset, and cost in inventory all disagree. Mobile-WO completion screams the same problem as S1-1. | ~250 LOC + tenant-scope fix. |
| **S1-8** | **PO state machine has no concurrency token** | `Models/PurchaseOrder.cs:27+` (no `[Timestamp]` RowVersion); `Pages/Purchasing/Details.cshtml.cs:260-293` | Asset uses xmin (`Models/Asset.cs:516`); PurchaseOrder, MaintenanceEvent, GoodsReceipt, VendorInvoice, CipProject all have no concurrency token. Two parallel "Approve" presses can flip Draft→Approved twice; a Draft→Approved race against Draft→Cancelled has no protection. | Voice/mobile features that re-issue commands will produce ghost transitions. Required for PWA offline replay. | ~30 LOC + migration per entity. Plus a service-layer `Update` pattern. |

---

## Severity 2 — should-fix-before-1.0

| # | Title | Files | What's wrong | Impact | Fix sizing |
|---|---|---|---|---|---|
| **S2-1** | Two parallel PM scheduler stacks | `Services/Maintenance/PMSchedulerService.cs` (modern) vs `Services/MaintenanceService.cs:284-355` (legacy `MaintenanceSchedule`) | `MaintenanceService.GenerateEventsFromSchedulesAsync` reads `MaintenanceSchedules` with **no tenant filter** (`:303`); legacy schedules can leak across tenants. Both write to `MaintenanceEvents`. | Cross-tenant schedule activation. Confusing maintenance landscape. | Deprecate `MaintenanceSchedule` and migrate to `PMSchedule`. ~200 LOC. |
| **S2-2** | PM has no meter-based recurrence | `Services/Maintenance/PMSchedulerService.cs:419-484`; `Models/PMTemplate.cs:8-12,59-63` | `PMTriggerType.Meter` and `CalendarOrMeter` enums exist; `PMTemplate.MeterInterval` exists; `MeterReading` exists. The scheduler only honors calendar `CadenceType`. Meter-due never fires. | Real PM strategies need meter triggers (every 250 hrs etc.). | ~150 LOC: extend `ComputeDueDatesAsync` to cross-reference latest `MeterReading`. |
| **S2-3** | CIP trace queries skip tenant scope | `Services/Cip/CipTraceQueryService.cs:21-92` | `GetRelatedWorkOrdersAsync`, `GetRelatedPurchaseOrdersAsync`, `GetRelatedVendorInvoicesAsync`, `GetRelatedJournalsAsync` all return rows by ID set without checking `VisibleCompanyIds`. | A user from Company B could see PO/Invoice/WO that belong to Company A if a CIP cost ID is shared. Real leak under shared service tenants. | ~40 LOC. |
| **S2-4** | Asset has no FK to originating PO/Invoice/CIP cost | `Models/Asset.cs:92-95` | `PurchaseOrderNumber` / `InvoiceNumber` are strings. `Asset.OriginatingPurchaseOrderId`, `OriginatingVendorInvoiceId`, `OriginatingCipProjectId` would be the right shape (`CipCapitalization.ConvertedAssetId` already half-handles CIP). | Audit trail to financial source is text-search only. Partner integrations cannot trace asset back to PO. | ~30 LOC + migration. |
| ~~**S2-5**~~ | ~~Disposal does not emit outbox event~~ ✅ Closed via [#51](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/51) | `Pages/Assets/Dispose.cshtml.cs` now emits `asset.disposed` v1 | — | — | — |
| ~~**S2-6**~~ | ~~Missing outbox events for the real domain transitions~~ ✅ Closed across [#50](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/50)–[#56](https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/pull/56) | 13 new typed V1 records: invoice.approved/paid/voided, asset.created/improved/disposed, po.approved/received, item.received, cip.capitalized, depreciation.posted, pm.occurrence.generated, item.issued. Catalog now lists 18 registered events. | — | — | — |
| **S2-7** | No central GL account resolver | `Services/Cip/CipCapitalizationService.cs:124,131` (string literals `"1500"`/`"1400"`); `Pages/Assets/Dispose.cshtml.cs:170-188` (ad-hoc cascade); `Services/JournalGenerator.cs:74-86` (`Book.GlAccountDepExp/AccumDep`) | Each posting site does its own GL resolution. Hard-coded literals will break against multi-tenant chart-of-accounts. | Tenant onboarding requires hand-editing `CipCapitalizationService`. | ~250 LOC: `IGlAccountResolver` with strategy per (CompanyId, AccountKind). |
| **S2-8** | Concurrency tokens missing on `MaintenanceEvent` / `GoodsReceipt` / `VendorInvoice` / `CipProject` | All four model files | Same posture as S1-8 but lower priority for non-state-machine writes. | Concurrent edit clobbering. | ~80 LOC + 4 migrations. |
| **S2-9** | `OnPostCapitalizeAsync` (WO → Asset improvement) skips PeriodGuard and depreciation recompute | `Pages/Maintenance/Details.cshtml.cs:269-311` | `Asset.AcquisitionCost` is incremented and `CapitalImprovement` created with no period gate and no `_depBackfill.RecomputeAssetAsync(...)` follow-up — unlike `Pages/Assets/Improve.cshtml.cs:99,143` which does both. | Capitalization into closed period; depreciation doesn't catch up. | ~30 LOC. |
| **S2-10** | `InvoiceMatchingService` is never invoked from approve flow | `Services/InvoiceMatchingService.cs:202-212` (`UpdateInvoiceMatchStatusAsync` is callable but `Pages/AccountsPayable/Details.cshtml.cs` doesn't call it on approve) | `MatchStatus` field exists on the invoice but stays at `NotMatched` unless someone clicks an unrelated handler. | Three-way match status is meaningless. | ~10 LOC. |
| **S2-11** | PO has `CipProjectId` field but no UI sets it | `Models/PurchaseOrder.cs:111`; no setter in `Pages/Purchasing/Create.cshtml.cs` or `Details.cshtml.cs` | The CIP-PO linkage is unreachable from the UI. | CIP-aware PO creation is impossible without API. | ~30 LOC UI. |

---

## Severity 3 — refinements (appendix)

- `MaintenanceEvent.CustomField1..CustomField10` (`Models/AssetMaintenance.cs:140-149`) — generic untyped string fields used for hacks (PMTA, IMPR). Promote to typed columns.
- `CipCost.GlAccount` is a string (`Models/ConstructionInProgress.cs:127`); `CipCost.Vendor` is a string (`:118`). Vendor became an FK (`VendorId`/`VendorRef` at `:170-171`) but the string twin still exists.
- `Asset.GLAssetAccount` / `GLAccumDepAccount` / `GLDepExpenseAccount` are strings (`Models/Asset.cs:121-127`) — duplicated and divergent from `Book.GlAccountDepExp`/`Book.GlAccountAccumDep` and `BookGlAccount`. Three sources of truth.
- `MaintenanceSchedule.NextDueDate` calculation in `Services/MaintenanceService.cs:334-355` uses `AddMonths(IntervalValue * 3)` for `Quarterly` — ok — but for `Custom` falls back to `AddDays(IntervalValue)` with no error handling.
- No `AssetDisposal` model — disposal is denormalized onto Asset columns. `PartialDisposal` exists but full disposal does not have an explicit row.
- No BOM / asset-component model. Componentization is `ParentAssetId` only.
- `_context.Items` queries in `Pages/WorkOrders/Details.cshtml.cs` and `Pages/Inventory/*.cshtml.cs` should be tenant-scoped (and audited).
- `Receiving/Receive.cshtml.cs:185` has `po.CompanyId ?? _tenantContext.CompanyId ?? 0` fallback; combined with no explicit CompanyId-required validation upstream, `0` could leak.
- `MaintenanceService.GenerateWorkOrderNumberAsync` (`:142-163`) is **not tenant-scoped** — two tenants generating numbers concurrently could collide on `WO-25-00001`.
- `LookupValue` resolution in services often passes `null, null` for tenant/company (`Services/MaintenanceService.cs:30`, `Services/Maintenance/CloseoutService.cs:163`) — cache works, but tenant-specific lookup overrides won't be honored.

---

## Cross-cutting check summary

- **Tenant scope:** Holes in `CipTraceQueryService` (S2-3), `MaintenanceService.GenerateEventsFromSchedulesAsync` (S2-1), `Pages/WorkOrders/Details.cshtml.cs:99,292` (S1-7), and several `_lookupService.GetValueByCodeAsync(null, null, ...)` calls.
- **Period locking:** Only invoked in `Pages/Maintenance/Details.cshtml.cs:179` (WO complete), `Pages/Receiving/Receive.cshtml.cs:188` (GR), `Pages/Assets/Dispose.cshtml.cs:128`, `Pages/Assets/Improve.cshtml.cs:99`, and inside `Services/JournalGenerator.cs:62-68` (depreciation post). **Missing on AP approve, AP payment, AP void, CIP capitalization, `OnPostCapitalize` (WO→Asset improvement), PO Approve, PO Receive secondary updates, ItemTransaction (none exist).**
- **Status enum + LookupValueId pairing:** Per `HANDOFF_STATUS.md` the table is complete. Spot-check confirms `PurchaseOrder.StatusLookupValueId`, `MaintenanceEvent.StatusLookupValueId`, `GoodsReceipt.StatusLookupValueId`, `VendorInvoice.StatusLookupValueId`, `CipProject.StatusLookupValueId` all present. Exception: `InvoiceMatchStatus`, `PMOccurrenceStatus`, `WorkOrderApprovalStatus`, `ReleaseStatus`, `DepreciationRunStatus`, `PeriodStatus` are enum-only (per HANDOFF table).
- **GL account mapping:** No central resolver (S2-7). PO line / GR line / AP invoice line have nullable `GlAccountId` FKs; Asset has 3 string columns; Book has its own; BookGlAccount overrides Book. Disposal page has the most thorough cascade. CIP capitalization hardcodes string literals.
- **Concurrency:** Only Asset has xmin (S1-8 / S2-8).
- **Outbox events:** 5 typed events. **Missing for** asset.created, asset.disposed, asset.improved, po.approved, po.received, invoice.approved, invoice.posted, invoice.paid, cip.cost.added, cip.capitalized, depreciation.posted, pm.occurrence.generated, item.received, item.issued.

---

## Sprint 0.5 plan

The user's directive is **best-in-class, no stone unturned**. Sprint 0.5 addresses every Severity-1 finding plus every Severity-2 finding before pivoting to product-roadmap work. Severity-3 items move into the residual followup tracker.

### Order of work (cheapest-first, design-needed pieces in parallel)

| # | Item | Type | Sizing | Dependencies |
|---|---|---|---|---|
| 1 | This audit document | Persist | — | — |
| 2 | ADR-001: Receiving accrual + inventory movement (S1-1) | Design | ~1 page | — |
| 3 | ADR-002: AP posting + invoice matching (S1-5) | Design | ~1 page | — |
| 4 | ADR-003: Central GL account resolver (S2-7) | Design | ~1 page | — (referenced by S1-1, S1-5, S1-4) |
| 5 | S1-3: Wire `CipAutoCostPostingService` calls into Receiving / AP / Maintenance | Code | ~150 LOC | None |
| 6 | S1-2: PM linkage proper (`PMOccurrenceId`, `PMTemplateAssetId` columns) | Code + migration | ~80 LOC | None |
| 7 | S1-8 / S2-8: Concurrency tokens batch on PO/MaintenanceEvent/GR/Invoice/CipProject | Code + migrations | ~150 LOC | None |
| 8 | S2-3: Tenant scope on `CipTraceQueryService` | Code | ~40 LOC | None |
| 9 | S2-9: PeriodGuard + depreciation recompute on `OnPostCapitalizeAsync` | Code | ~30 LOC | None |
| 10 | S2-10: Wire `InvoiceMatchingService` into AP approve flow | Code | ~10 LOC | None |
| 11 | S2-1: Deprecate `MaintenanceSchedule`, migrate to `PMSchedule` | Code | ~200 LOC | None |
| 12 | S2-4: Asset → originating PO/Invoice/CIP FKs | Code + migration | ~30 LOC | None |
| 13 | S2-2: PM meter-based recurrence | Code | ~150 LOC | None |
| 14 | S2-11: PO Create/Details UI sets `CipProjectId` | Code | ~30 LOC | After 5 |
| 15 | S2-5, S2-6: Outbox events for the missing domain transitions | Code | ~250 LOC | After 17–19 |
| 16 | S1-1 implementation: Receiving accrual + inventory movement | Code | ~600–1000 LOC | After ADR-001, ADR-003 |
| 17 | S1-4 implementation: CIP capitalization fixes | Code | ~120 LOC | After ADR-003 |
| 18 | S1-5 implementation: AP posting | Code | ~400–700 LOC | After ADR-002, ADR-003 |
| 19 | S1-6: WO cost rollup from operations | Code | ~150 LOC | After 16 |
| 20 | S1-7: WO part issuance ledger + tenant-scope fix | Code | ~250 LOC | After 16 |

### Success criteria for Sprint 0.5

- **End-to-end financial flow works:** PO approve → GR (inventory + accrual JE) → Invoice approve (AP JE + match) → Payment (cash JE) → optional capitalize (asset cost + depreciation kickoff). A working ledger from receipt through asset depreciation.
- **PM lifecycle works:** PM template release → PMSchedule.NextDueDate → PMOccurrence + WO generation → WO close advances `PMTemplateAsset.LastCompletedDate` AND `NextDueDate`. Meter-based PM fires when meter reading crosses interval.
- **WO lifecycle works:** Operations have labor + parts that roll into the WO's ActualCost; part issuance decrements inventory and posts to GL.
- **CIP lifecycle works:** PO/AP/WO marked with `CipProjectId` automatically posts cost to CIP project; capitalization respects PeriodGuard + tenant + GL accounts + kicks off depreciation.
- **Concurrency:** Every state-machine entity has an xmin RowVersion.
- **Outbox:** Every business state change emits a typed event.

After Sprint 0.5 lands, pivoting to Sprint 1 (PWA mobile WO execution) is safe — the underlying ledger and inventory writes will actually happen.

---

## Method notes

- Audit performed by an automated codebase reader on 2026-05-08 against `main` at commit `4008c89` (CLAUDE.md Sprint 0 #8 closing PR).
- Citations are file:line form against current `main`.
- Reading covered `Models/`, `Services/`, `Pages/Purchasing/`, `Pages/Receiving/`, `Pages/AccountsPayable/`, `Pages/Maintenance/`, `Pages/WorkOrders/`, `Pages/Assets/`, `Pages/Inventory/`, `Pages/Admin/Webhooks/`, plus the audit-2026-05-07 frozen snapshot.
- This document is the canonical reference; subsequent ADRs link back here for context.
