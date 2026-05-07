# 04 — Services Layer (50 Services)

## Executive Summary

The services layer consists of **50 service files** across **15 domains**, implementing financial accounting, asset management, maintenance scheduling, procurement, integrations, and seeding. The architecture is primarily **scoped (request-level) instantiation** with singletons for caching, rate-limiting, and background workers. Multi-tenancy is enforced via `TenantContext`, distributed rate-limiting via PostgreSQL, and an outbox pattern for webhook reliability.

---

## 1. Service Inventory by Domain

### Financial & Depreciation (Asset Accounting Engine)

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **DepreciationService** | `Services/DepreciationService.cs` | None | Scoped | Core depreciation schedule builder. Supports SL, DDB, DB-150%, DB-200%, Units-of-Production, Amortization. Handles HalfYear/MidMonth/FullMonth/FullYear conventions. Auto SL switchover when beneficial. Returns `List<DepreciationRow>` per asset/book/period. |
| **DepreciationMath** | `Services/DepreciationMath.cs` | (static) | N/A | Lightweight straight-line helpers. Methods: `MonthlyStraightLine(cost, salvage, life)`, `AccumulatedThrough(inServiceDate, asOfDate, monthlyRate, life)`. |
| **NbvHelper** | `Services/NbvHelper.cs` | (static) | N/A | Date resolution. `ResolveAsOf(DateTime?, AsOfMode)` → today vs. month-end. |
| **DepreciationBackfillService** | `Services/DepreciationBackfillService.cs` | None | Scoped | Backfills historical depreciation when books/conventions change retroactively. Posts correction entries via JournalGenerator. |
| **JournalGenerator** | `Services/JournalGenerator.cs` | (static) | N/A | Generates monthly depreciation journal entries. Methods: `GenerateMonthlyAsync(db, bookId, month)`, `GenerateMonthlyWithAmountAsync(...)`. Enforces period-locking via `FiscalPeriods`. Uses `BookGlAccount` or legacy `Book.GlAccountDepExp` mapping. |
| **UsTaxService** | `Services/UsTaxService.cs` | None | Scoped | US tax depreciation (MACRS, Section 179, bonus depreciation). Delegates to DepreciationService. |

### Capital-in-Progress (CIP)

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **CipService** | `Services/CipService.cs` | None | Scoped | CRUD for CIP projects + cost tracking. Methods: `GetAllProjectsAsync`, `GetActiveProjectsAsync`, `GetProjectAsync(id)`, `CreateProjectAsync`, `UpdateProjectAsync`, `DeleteProjectAsync`, `ConvertToAssetAsync(projectId, targetAsset)`, `ReconcileAllProjectCostsAsync`. |
| **CipCostService** | `Services/Cip/CipCostService.cs` | None | Scoped | Cost line items (labor, materials, overhead) against CIP projects. |
| **CipCapitalizationService** | `Services/Cip/CipCapitalizationService.cs` | None | Scoped | Converts completed CIP projects into fixed assets; computes capitalized basis; generates fixed asset record. |
| **CipAutoCostPostingService** | `Services/Cip/CipAutoCostPostingService.cs` | None | Scoped | Background/timer-driven service to auto-post accumulated costs as GL entries during active construction. |
| **CipTraceQueryService** | `Services/Cip/CipTraceQueryService.cs` | None | Scoped | Hierarchical cost breakdown for CIP projects; traces component costs up the project tree. |

### Maintenance & Work Orders

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **MaintenanceService** | `Services/MaintenanceService.cs` | None | Scoped | Queries & aggregates maintenance events. Methods: `GetAllEventsAsync`, `GetEventsForDashboardAsync(filter, limit)`, `GetUpcomingEventsAsync(days)`, `GetOverdueEventsAsync`, `GetEventAsync(id)`. Tenant/site-scoped via ITenantContext. |
| **CloseoutService** | `Services/Maintenance/CloseoutService.cs` | `ICloseoutService` | Scoped | Closes completed WOs; auto-generates closeout summaries; captures lessons learned. Methods: `CloseWorkOrderAsync(woId, lessons, user, allowIncomplete)`, `SaveLessonAsync(woId, text, tags, user)`, `GetRecurringFailuresAsync(days, limit)`. Posts to outbox on completion. |
| **PMSchedulerService** | `Services/Maintenance/PMSchedulerService.cs` | `IPMSchedulerService` | Scoped | Generates PM work orders on schedule. Methods: `PreviewDueAsync(horizonDays, nowUtc, ...)`, `GenerateDueAsync(...)`, `ComputeDueDatesAsync(schedule, fromUtc, toUtc)`. Handles recurring frequencies (daily/weekly/monthly/quarterly/yearly). |
| **WorkRequestConversionService** | `Services/Maintenance/WorkRequestConversionService.cs` | `IWorkRequestConversionService` | Scoped | Converts work requests → draft or confirmed work orders. Applies default assignments and status routing. |
| **WorkOrderOriginService** | `Services/Maintenance/WorkOrderOriginService.cs` | `IWorkOrderOriginService` | Scoped | Traces WO origin/genealogy (PM schedule, reactive request, inspection). |

### Items / Procurement

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **ItemRevisionService** | `Services/Items/ItemRevisionService.cs` | `IItemRevisionService` | Scoped | Item versioning. Tracks historical specs, SKUs, sourcing changes. |
| **ItemCrossReferenceService** | `Services/Items/ItemCrossReferenceService.cs` | `IItemCrossReferenceService` | Scoped | Cross-references (equivalent parts from different vendors, OEM vs aftermarket). |
| **ItemSourcingService** | `Services/Items/ItemSourcingService.cs` | `IItemSourcingService` | Scoped | Source items via preferred vendors or catalog lookups. |
| **ItemAlternateService** | `Services/Items/ItemAlternateService.cs` | `IItemAlternateService` | Scoped | Manages alternate/substitute items. |
| **ItemSupersessionService** | `Services/Items/ItemSupersessionService.cs` | `IItemSupersessionService` | Scoped | Tracks item supersessions (new part replaces old). |
| **ItemImageService** | `Services/ItemImageService.cs` | `IItemImageService` | Scoped | Stores/retrieves item images. |
| **BuyabilityScoreService** | `Services/Items/BuyabilityScoreService.cs` | `IBuyabilityScoreService` | Scoped | Grades items on procurement readiness (0-100). Factors: vendor availability, lead time, catalog presence, spec completeness. |
| **EffectiveProcurementService** | `Services/Items/EffectiveProcurementService.cs` | `IEffectiveProcurementService` | Scoped | Determines effective procurement route (vendor A vs B, lead time implications). |
| **PreferredVendorCatalogResolver** | `Services/Items/PreferredVendorCatalogResolver.cs` | `IPreferredVendorCatalogResolver` | Scoped | Resolves items in vendor catalogs (catalog number lookup, availability, pricing). |
| **ItemStockingService** | `Services/ItemStockingService.cs` | `IItemStockingService` | Scoped | Stocking recommendations: EOQ, ROP, lead-time calcs. |
| **InventoryService** | `Services/InventoryService.cs` | None | Scoped | Aggregates inventory counts & transactions. |
| **CatalogMetadataEnrichmentService** | `Services/CatalogMetadataEnrichmentService.cs` | `ICatalogMetadataEnrichmentService` | Scoped | Enriches item records with external catalog metadata (MPN, specs, substitutes). |

### Lookups & Reference Data (CRITICAL — the FK-bound dropdown backbone)

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **LookupService** | `Services/Lookups/LookupService.cs` | `ILookupService` | Scoped | Caches lookup types and values (10-min TTL in MemoryCache). Methods: `GetValuesAsync`, `GetValueByIdAsync`, `GetValueByCodeAsync`, `GetSelectListAsync`, `GetSelectListByIdAsync`, `InvalidateCache`. Cross-tenant/company isolation enforced. |

### AI & Smart Assist

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **AiAssistantService** | `Services/AiAssistantService.cs` | None | Scoped | Aggregates asset, maintenance, financial context for AI/Claude prompts. `GetAssetContextAsync()` builds markdown summary of top assets, locations, maintenance backlog, financials. Calls OpenAI (configurable base URL + key). |
| **SmartAssistService** | `Services/SmartAssistService.cs` | `ISmartAssistService` | Scoped | Smart inference for work orders. Uses keyword matching on request text to suggest asset, priority, failure/action codes, labor hours, craft. Methods: `AnalyzeRequestAsync(request, siteId?)` → `SmartAssistResult`, `GenerateDraftWorkOrderAsync(...)`. **Extensibility comments indicate readiness for LLM integration but not implemented.** |

### User & Tenancy

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **AuthService** | `Services/AuthService.cs` | None | Scoped | User auth + RBAC. Methods: `ValidateUserAsync(username, password)`, `GetUserByIdAsync(id)`, `GetAllUsersAsync`, `CreateUserAsync(username, password, role, fullName?, email?, assignedCompanyId?)`, `UpdateUserAsync(user)`, `SeedDefaultUserAsync()`. Cross-tenant by design (login precedes tenant context). ⚠️ Password hashing via SHA-256 — needs upgrade to Argon2id. |
| **TenantContext** | `Services/TenantContext.cs` | `ITenantContext`, `ITenantContextOverride` | Scoped (context) / Singleton (override) | Per-request tenant/company/site binding via `SetContext(...)`. Supports hierarchical company visibility (`VisibleCompanyIds`, `VisibleSiteIds`). Override uses `AsyncLocal<Stack<T>>` for nested scoping in background jobs. |
| **CompanyService** | `Services/CompanyService.cs` | `ICompanyService` | Scoped | CRUD for company master records. |
| **CompanyHierarchyService** | `Services/CompanyHierarchyService.cs` | `ICompanyHierarchyService` | Scoped | Resolves company rollups (parent/child visibility). |
| **ModuleGuardService** | `Services/ModuleGuardService.cs` | `IModuleGuardService` | Scoped | Feature flags (WorkOrders, Purchasing, AP, Vendors, Inventory). |

### Reporting & Export

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **ReportBuilderService** | `Services/ReportBuilderService.cs` | None | Scoped | Generates ad-hoc asset, depreciation, maintenance reports. |
| **ExportService** | `Services/ExportService.cs` | None | Scoped | Excel/CSV export wrapper. Uses ClosedXML or CSV writer. |

### Integrations & Webhooks

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **InboundWebhookService** | `Services/Integrations/InboundWebhookService.cs` | `IInboundWebhookService` | Scoped | Receives inbound webhooks. Validates HMAC-SHA256 signature + timestamp (±5 min). Methods: `ReceiveWebhookAsync(integrationKey, rawBody, timestamp, signature, idempotencyKey, headers)`, `VerifySignature(secret, timestamp, rawBody, signature)`. Deduplicates via idempotency key. |
| **IntegrationMappingService** | `Services/Integrations/IntegrationMappingService.cs` | `IIntegrationMappingService` | Scoped | Maps inbound webhook payloads to EAM domain entities. |
| **InboundEventProcessorHostedService** | `Services/Integrations/InboundEventProcessorHostedService.cs` | `BackgroundService` | HostedService | Long-running poller of `InboundEvents`; routes to handlers. |
| **OutboxWriter** | `Services/Webhooks/OutboxWriter.cs` | `IOutboxWriter` | Scoped | Writes to `OutboxEvents` table for transactional outbox pattern. `WriteAsync(eventType, payload, companyId, tenantId?)`. |
| **WebhookDispatcherHostedService** | `Services/Webhooks/WebhookDispatcherHostedService.cs` | `BackgroundService` | HostedService | Polls `OutboxEvents`; retries with exponential backoff (1, 5, 15, 60, 120, 240, 480, 960 min, 8 attempts max). HMAC-SHA256 signs payloads. 30s timeout per request. |

### Seeding & Bootstrap

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **SeedPackExecutor** | `Services/Seeding/SeedPackExecutor.cs` | `ISeedPackExecutor` | Scoped | Executes a `SeedPack` (demo data template). Tracks per-table inserted/skipped, errors, duration. Guarded by `ISeedGuardService`. Transactional. |
| **MasterDataBootstrapService** | `Services/MasterDataBootstrapService.cs` | `IMasterDataBootstrapService` | Scoped | One-time bootstrap of system reference data (GL accounts, depreciation books, fiscal calendars). |
| **SeedGuardService** | `Services/SeedGuardService.cs` | `ISeedGuardService` | Scoped | Prevents accidental re-seeding. `CheckSeedPermission()` → `(Allowed, Reason)`. |
| **LookupSeedPipeline** | `Services/Seeding/Pipelines/LookupSeedPipeline.cs` | `ISeedPipeline` | Scoped | Populates `LookupTypes` + `LookupValues` from JSON. Idempotent. |
| **OrgAndFinanceSeedPipeline** | `Services/Seeding/Pipelines/OrgAndFinanceSeedPipeline.cs` | `ISeedPipeline` | Scoped | Seeds companies, sites, cost centers, GL accounts, fiscal periods, books. |
| **VendorsAndPartsFoundationSeedPipeline** | `Services/Seeding/Pipelines/VendorsAndPartsFoundationSeedPipeline.cs` | `ISeedPipeline` | Scoped | Seeds vendors, items, BOM, stocking levels, preferred vendor assignments (deterministic seed=42 for reproducibility). |
| **EamExecutionMastersSeedPipeline** | `Services/Seeding/Pipelines/EamExecutionMastersSeedPipeline.cs` | `ISeedPipeline` | Scoped | PM templates, technicians, skills matrix, work request statuses, failure/action codes. |
| **SystemReferenceSeedPipeline** | `Services/Seeding/Pipelines/SystemReferenceSeedPipeline.cs` | `ISeedPipeline` | Scoped | Asset categories, currencies, units, time zones. |
| **DemoPackV1Pipeline** | `Services/Seeding/Pipelines/DemoPackV1Pipeline.cs` | `ISeedPipeline` | Scoped | Legacy demo dataset (pre-v2 schema). |
| **DemoPackV2Pipeline** | `Services/Seeding/Pipelines/DemoPackV2Pipeline.cs` | `ISeedPipeline` | Scoped | Current demo dataset (v2 schema, realistic multi-company scenario). |
| **DemoScenarioSeedPipeline** | `Services/Seeding/Pipelines/DemoScenarioSeedPipeline.cs` | `ISeedPipeline` | Scoped | Scripted workflows (scheduled PM generated and due, active WOs, recent acquisitions). |
| **MasterDataImportService** | `Services/MasterDataImportService.cs` | None | Scoped | Bulk import reference data from CSV/Excel. Validates FKs. |

### Other Services

| Service | File Path | Interface | Lifetime | Purpose |
|---|---|---|---|---|
| **AuditService** | `Services/AuditService.cs` | None | Scoped | Logs sensitive changes (asset cost updates, depreciation recalc, user edits). |
| **AttachmentService** | `Services/AttachmentService.cs` | None | Scoped | File attachments. Stores in blob storage or disk. |
| **BarcodeService** | `Services/BarcodeService.cs` | `IBarcodeService` | Scoped | Generates barcodes (Code128, QR) for assets/items. Uses SkiaSharp. |
| **TemplateService** | `Services/TemplateService.cs` | None | Scoped | Manages WO, invoice, report templates. |
| **ApiService** | `Services/ApiService.cs` | None | Scoped | Wrapper for outbound HTTP calls. Includes retry & timeout. |
| **CcaService** | `Services/CcaService.cs` | None | Scoped | Canadian Capital Cost Allowance (CCA) calculations. |
| **CcaBackfillService** | `Services/CcaBackfillService.cs` | None | Scoped | Retroactively calculates CCA for historical assets. |
| **CcaClassSuggester** | `Services/CcaClassSuggester.cs` | None | Scoped | Suggests CCA class for new assets based on type/category keywords. |
| **HistoricJournalBackfillService** | `Services/HistoricJournalBackfillService.cs` | None | Scoped | Generates historical GL entries for assets added retroactively. |
| **BulkOperationsService** | `Services/BulkOperationsService.cs` | None | Scoped | Batch operations (bulk creation, status updates, depreciation recalc). Transactional with per-record success/failure. |
| **PeriodGuard** | `Services/PeriodGuard.cs` | `IPeriodGuard` | Scoped | Enforces fiscal period locking. `IsOpenAsync(companyId, date)`, `GetPeriodAsync(companyId, date)`. |
| **InvoiceMatchingService** | `Services/InvoiceMatchingService.cs` | None | Scoped | 3-way invoice matching (PO/receipt/invoice). ⚠️ Hardcoded penny tolerance (0.01m); make configurable. |
| **ImportService** | `Services/ImportService.cs` | None | Scoped | Generic CSV/Excel data import. |
| **PMTemplateRevisionService** | `Services/Revisions/PMTemplateRevisionService.cs` | `IPMTemplateRevisionService` | Scoped | PM template versioning. |

---

## 2. Critical Service Deep-Dives

### LookupService — the FK-bound dropdown backbone

**File:** `Services/Lookups/LookupService.cs` | **Scoped** | `ILookupService`

The LookupService is what makes the entire FK-bound dropdown architecture work. Every dropdown in the UI binds to a `LookupValue.Id`, and on save the page model resolves the LookupValue to get the `Code`, then syncs both the FK and the legacy enum field.

**Key features:**
- **10-min in-memory cache** (`MemoryCache`) per `(tenantId, companyId, lookupKey, includeInactive)` tuple
- **Fallback hierarchy:** company-level lookup → tenant-level lookup → missing (logs warning, returns empty)
- **Cross-tenant isolation:** validates TenantId/CompanyId on lookups by ID to prevent data leakage
- **SelectList rendering:** two overloads — by code (string) and by ID (int) — both with placeholder + selected value
- **Cache invalidation:** full compact only (no fine-grained key invalidation)

**Public methods:**
```csharp
Task<List<LookupValueDto>> GetValuesAsync(int? tenantId, int? companyId, string lookupKey, bool includeInactive = false)
Task<LookupValueDto?> GetValueByIdAsync(int? tenantId, int? companyId, int lookupValueId)
Task<LookupValueDto?> GetValueByCodeAsync(int? tenantId, int? companyId, string lookupKey, string code)
Task<List<SelectListItem>> GetSelectListAsync(int? tenantId, int? companyId, string lookupKey, string? selectedValue = null, string placeholder = "-- Select --")
Task<List<SelectListItem>> GetSelectListByIdAsync(int? tenantId, int? companyId, string lookupKey, int? selectedId = null, string placeholder = "-- Select --")
void InvalidateCache()
```

**Common lookup keys:** `MaintenanceStatus`, `AssetStatus`, `FailureCode`, `ActionCode`, `CauseCode`, `WorkOrderType`, `Priority`, `MaintenanceType`, plus 68 others (76 total).

**Issues to know:**
- Cache not tagged per company → tenant-level update forces full compact (not fine-grained)
- Missing lookup keys log a warning but return empty list (silent fail in UI)
- This is THE pattern; every new dropdown should follow it. Never hardcode an enum-driven dropdown.

---

### DepreciationService + DepreciationMath + NbvHelper — the financial engine

**Files:** `Services/DepreciationService.cs`, `Services/DepreciationMath.cs`, `Services/NbvHelper.cs`

The depreciation subsystem is the core fixed-asset accounting engine. It computes monthly depreciation schedules under multiple methods and conventions.

**DepreciationService key methods:**
```csharp
List<DepreciationRow> BuildSchedule(Asset asset, DateTime asOfMonthEnd)
List<DepreciationRow> BuildScheduleWithSettings(
    Asset asset,
    DateTime asOfMonthEnd,
    AssetBookSettings? bookSettings,
    bool switchToSLWhenBeneficial = true,
    Dictionary<int, decimal>? unitsPerPeriod = null)
```

**Supported methods:**
- **StraightLine / Amortization:** constant monthly depreciation
- **DoubleDecliningBalance (DDB):** 2× SL rate, applies conventions, auto-switches to SL when beneficial
- **DecliningBalance-150 / 200:** multiplier-based variants
- **UnitsOfProduction:** based on `unitsPerPeriod` dictionary
- **NoDepreciation:** explicit zero method

**Supported conventions:**
- **HalfYear:** half depreciation in acquisition year
- **MidMonth:** depreciation starts mid-month of in-service date
- **FullMonth:** full month depreciation (per GAAP)
- **FullYear:** full year in acquisition year (aggressive)

**Key calculations:**
- **Section 179 deduction:** reduces depreciable basis before computing schedule
- **Bonus depreciation:** percentage applied before schedule
- **Salvage value:** subtracted from cost (except in some tax regimes)
- **Remaining useful life:** dynamically adjusted per period for DDB switchover

**Per-asset, per-book isolation:** `AssetBookSettings` overrides allow different depreciation policies per book without modifying the asset. This is the right design for multi-book (GAAP + Tax + IFRS).

**JournalGenerator reflection fragility:** if `DepreciationService` is not in DI container, the journal generator falls back via reflection to a straight-line formula. **Renaming `BuildSchedule()` will silently break journal generation without compile error.** Worth replacing with a hard interface.

---

### MaintenanceService + CloseoutService + PMSchedulerService — the maintenance engine

**Files:** `Services/MaintenanceService.cs`, `Services/Maintenance/CloseoutService.cs`, `Services/Maintenance/PMSchedulerService.cs`

**MaintenanceService (read/dashboard):**
```csharp
Task<List<MaintenanceEvent>> GetAllEventsAsync()
Task<List<MaintenanceEvent>> GetEventsForDashboardAsync(string? filter, int limit = 250)  // "overdue" | "scheduled" | "inprogress" | "completed"
Task<List<MaintenanceEvent>> GetUpcomingEventsAsync(int days = 30)
Task<List<MaintenanceEvent>> GetOverdueEventsAsync()
Task<MaintenanceEvent?> GetEventAsync(int id)
```
Scopes queries to visible companies/sites via `ITenantContext`. Supports tenant-context override for cross-scope admin queries.

**CloseoutService:**
```csharp
string GenerateCloseoutSummary(MaintenanceEvent workOrder, List<WorkOrderOperation>? operations = null)
Task<CloseoutResult> CloseWorkOrderAsync(int workOrderId, string? lessonsLearned, string username, bool allowIncompleteOperations = false)
Task<LessonSaveResult> SaveLessonAsync(int workOrderId, string lessonText, string? tags, string username)
Task<List<RecurringFailure>> GetRecurringFailuresAsync(int days = 30, int limit = 5)
```

- **CloseoutResult:** auto-generated summary (WO#, failure code, corrective action, technician, cost) + optional tagged lesson
- **Recurring failures:** identifies repeat failure codes on same asset within timeframe — surfaces chronic issues
- **Outbox posting:** completion triggers `OutboxEvent` for webhook subscribers (e.g., remote monitoring systems, SAP)

**PMSchedulerService:**
```csharp
Task<List<PMGenerationPreview>> PreviewDueAsync(int horizonDays, DateTime nowUtc, int? tenantId, int? companyId, int? siteId)
Task<PMGenerationResult> GenerateDueAsync(int horizonDays, DateTime nowUtc, string? initiatedByUserId, int? tenantId, int? companyId, int? siteId)
Task<List<DateTime>> ComputeDueDatesAsync(PMSchedule schedule, DateTime fromUtc, DateTime toUtc)
```

- **Preview mode:** shows what *would* be generated without persisting
- **Cron-like schedules:** daily, weekly, monthly, quarterly, yearly via `RecurrencePattern` enum
- **Deduplication:** checks if a WO already exists for asset+schedule on the due date
- **Result tracking:** created/skipped/error counts + error messages

**Cross-service deps:** `CloseoutService` → `IOutboxWriter`; `PMSchedulerService` → `IIntegrationMappingService` (optional); both → `ITenantContext`, `ILookupService` (optional, for status/code resolution).

---

### Integrations + Webhooks — outbox + inbound

**Files:** `Services/Webhooks/OutboxWriter.cs`, `Services/Webhooks/WebhookDispatcherHostedService.cs`, `Services/Integrations/InboundWebhookService.cs`, `Services/Integrations/InboundEventProcessorHostedService.cs`

This subsystem implements a **transactional outbox pattern** for reliable webhook delivery and a **signature-verified inbound webhook receiver**. It's well-implemented and is one of the project's strongest competitive advantages (vs. SAP requiring SAP PI/PO middleware).

**Outbound path (OutboxWriter → WebhookDispatcherHostedService):**

1. **OutboxWriter (transactional):**
   ```csharp
   Task<int> WriteAsync(string eventType, object payload, int companyId, int? tenantId?)
   ```
   Writes to `OutboxEvents` table within the same transaction as the business change (e.g., WO closure). Guarantees no event is lost even if app crashes post-commit.

2. **WebhookDispatcherHostedService (background worker):**
   - Polls `OutboxEvents` every 10 seconds
   - Queries `Status == Pending` AND `NextAttemptAt <= UtcNow`, batches up to 50 per cycle
   - For each event, finds matching `WebhookSubscriptions` by company + event type
   - **Signature:** computes `HMAC-SHA256(timestamp + rawBody, secret)`, appends to headers
   - **Retry logic:** exponential backoff `[1, 5, 15, 60, 120, 240, 480, 960]` minutes (8 attempts max)
   - **Timeout:** 30s per HTTP request
   - Updates event Status to Succeeded or Failed + LastError

**Inbound path (InboundWebhookService → InboundEventProcessorHostedService):**

1. **InboundWebhookService (API entry point):**
   ```csharp
   Task<(bool Success, string Message, int? EventId)> ReceiveWebhookAsync(
       string integrationKey, string rawBody, string? timestamp,
       string? signature, string? idempotencyKey, Dictionary<string, string> headers)
   bool VerifySignature(string secret, string timestamp, string rawBody, string signature)
   ```
   - Validates `integrationKey` against active `IntegrationEndpoints`
   - Verifies timestamp (within ±5 minutes — replay protection)
   - Verifies HMAC-SHA256 signature
   - **Idempotency:** checks if `idempotencyKey` already processed; if so, returns cached result
   - Stores raw event in `InboundEvents` with status Pending

2. **InboundEventProcessorHostedService (background worker):**
   - Polls `InboundEvents` with Status==Pending
   - Routes to handler based on event type (e.g., `WorkOrderCreated` → `WorkRequestConversionService`)
   - Updates status to Processed or Failed + error reason

**Security considerations:**
- HMAC-SHA256 with endpoint secret (256-bit recommended)
- 5-min timestamp tolerance prevents replay attacks
- Idempotency keys prevent double-processing
- Headers carefully logged (no Authorization/Set-Cookie leak)

**Improvement opportunity:** Outbox payloads are typed as `object` and serialized to JSON. **No strongly-typed event schema.** Adding a `IDomainEvent` interface with strict schemas would let consumers depend on contracts, not hope.

---

### Seeding / SeedPackExecutor + 8 Pipelines

**Files:** `Services/Seeding/SeedPackExecutor.cs` + `Services/Seeding/Pipelines/*.cs`

The seeding system is orchestrated by `SeedPackExecutor`, which runs a sequence of `ISeedPipeline` implementations in order, each idempotent and atomic.

**SeedPackExecutor:**
```csharp
Task<SeedPackResult> ExecuteAsync(SeedPack pack)
```
- Wraps entire seed in transaction
- Checks `SeedGuardService` for permission (one-shot flag in DB)
- Runs pipelines sequentially
- Tracks per-table inserted/skipped counts
- Rolls back on any unhandled exception
- Returns `SeedPackResult`: timing, errors, warnings, table-by-table breakdown

**SeedPack configuration:**
```csharp
public class SeedPack
{
    public string Name { get; set; }
    public string Id { get; set; }
    public int CompanyCount { get; set; }
    public int SiteCount { get; set; }
    public int LocationCount { get; set; }
    public int VendorCount { get; set; }
    public int TechnicianCount { get; set; }
    public int AssetCount { get; set; }
    public int PMTemplateCount { get; set; }
}
```

**The 8 pipelines (in execution order):**

1. **LookupSeedPipeline** — populates `LookupTypes` + `LookupValues` from JSON. Idempotent (checks type key + tenant + company)
2. **OrgAndFinanceSeedPipeline** — companies, sites, locations, cost centers, GL accounts, books, fiscal periods. Roots the company hierarchy
3. **VendorsAndPartsFoundationSeedPipeline** — vendors, items, BOMs, stocking levels, preferred vendor links. Deterministic (seed=42 for reproducibility)
4. **EamExecutionMastersSeedPipeline** — PM templates, technicians, skills matrix, work request statuses, failure/action codes
5. **SystemReferenceSeedPipeline** — asset categories, currencies, units, time zones
6. **DemoPackV1Pipeline** — legacy demo data (pre-v2 schema compatibility)
7. **DemoPackV2Pipeline** — current full-featured demo (multi-company, maintenance backlog, mix of active/completed assets)
8. **DemoScenarioSeedPipeline** — runs *after* demo data; creates workflow scenarios (scheduled PM generated and due, active work orders, recent acquisitions, overdue tasks) to populate dashboards

**Guard logic:**
```csharp
public (bool Allowed, string Reason) CheckSeedPermission()
```
- Checks env var `ALLOW_RESEED=true` (default: no re-seeding)
- In Development: allows seeding if DB is empty (auto-seed if `RUN_SEED=true` or `AUTO_SEED_ON_EMPTY=true`)
- In Production: seeding explicitly blocked

**Transactionality:** all pipelines run within a single database transaction. Any failure → entire pack rolled back.

⚠️ **Risk:** seeding guard checks via DB record but no distributed lock (e.g., PostgreSQL advisory lock). Two concurrent requests might both see "not seeded" and begin seeding. Mitigation today: seed only at startup before request pipeline; Replit single-instance mostly mitigates. Harden before scale-out.

---

### AiAssistantService + SmartAssistService — the AI surface

**Files:** `Services/AiAssistantService.cs`, `Services/SmartAssistService.cs`

**AiAssistantService (context building):**
```csharp
Task<string> GetAssetContextAsync()  // returns markdown
```
Builds a comprehensive markdown summary for Claude/GPT prompts:
- Total assets, active count, total value, NBV, fully depreciated count, recent acquisitions
- Asset distribution by location (top locations by value)
- Top 10 highest-value assets with direct links
- Maintenance status (overdue, upcoming 30-day window, by type)
- Inventory valuation, PO backlog
- Financial summary (depreciation by book, recent journal entries)

Used by Razor pages to inject context into sidebar AI prompts. Reads from `AppDbContext` directly (no caching).

**SmartAssistService (work request inference):**
```csharp
Task<SmartAssistResult> AnalyzeRequestAsync(WorkRequest request, int? siteId)
Task<MaintenanceEvent?> GenerateDraftWorkOrderAsync(WorkRequest request, SmartAssistResult assist, string username)
```

Analyzes plain-text work requests, suggests:
- **Asset:** best-match by keyword or location
- **Priority:** keyword-based escalation (emergency > critical > high > medium > low)
- **Failure code:** matched against `FailureKeywords` dict (motor → MOTOR-FAIL, etc.)
- **Cause & action codes:** keyword-matched
- **Estimated labor hours:** defaults to 2.0
- **Craft/trade:** matched from keyword signatures
- **Confidence level:** High/Medium/Low based on match quality

**Static keyword dictionaries:**
```csharp
static Dictionary<string, WorkRequestPriority> PriorityKeywords
static Dictionary<string, string> FailureKeywords
static Dictionary<string, string> ActionKeywords
```

Examples:
- "fire", "smoke", "explosion" → Emergency
- "bearing", "pump", "motor" → BEARING-WEAR, PUMP-FAIL, MOTOR-FAIL
- "replace", "repair" → REPLACE, REPAIR

**Status:** comments indicate readiness for LLM integration but it's not implemented. **This is the single most strategic upgrade opportunity** — swap the regex with a Claude API call and the whole product gets demonstrably smarter overnight. See section 09 of the audit for the disruption play.

---

### ModuleGuardService — feature flags

**File:** `Services/ModuleGuardService.cs` | **Scoped** | `IModuleGuardService`

```csharp
Task<bool> IsModuleEnabledAsync(string moduleName)
Task<ModuleStatus> GetModuleStatusAsync()
```

**Flags:** `WorkOrdersEnabled`, `PurchasingEnabled`, `AccountsPayableEnabled`, `VendorsEnabled`, `InventoryEnabled`

**Implementation:** loads from the *first* root company (`ParentCompanyId == null`), caches per request. Module names are case-insensitive; unrecognized names default to enabled (fail-safe).

**Use case:** Large enterprises may want to disable non-core modules (e.g., a manufacturing plant disables AP/Purchasing if handled centrally).

---

### TenantContext — the multi-tenancy resolver

**File:** `Services/TenantContext.cs` | Scoped (per-request) / Singleton (override)

The linchpin for multi-tenant isolation. Supplies tenant, company, and site context to all scoped services.

**Primary interface (`ITenantContext`):**
```csharp
int? TenantId { get; }
int? CompanyId { get; }
int? SiteId { get; }
int? AssignedCompanyId { get; }
int? AssignedSiteId { get; }
List<int> VisibleCompanyIds { get; }
List<int> VisibleSiteIds { get; }
bool IsResolved { get; }
string? ResolutionError { get; }

void SetContext(int? tenantId, int? companyId, int? siteId)
void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds)
void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds)
void SetError(string error)
```

**Override mechanism (`ITenantContextOverride`):**
```csharp
IDisposable BeginScope(int tenantId, int companyId, int? siteId = null, int? userId = null)
TenantContextOverrideValues? GetCurrentOverride()
```

Uses `AsyncLocal<Stack<TenantContextOverrideValues>>` for nested scoping (background jobs, batch ops):
```csharp
using (_tenantContext.BeginScope(tenantId: 2, companyId: 3))
{
    // All services see tenant 2, company 3
    await service.DoWorkAsync();
} // Scope pops on dispose
```

**Visibility rules:**
- `VisibleCompanyIds` allows a user to see a hierarchy (e.g., parent + assigned children)
- Middleware populates context from user's `AssignedCompanyId` + hierarchy service
- Queries filter by `VisibleCompanyIds` (not just `CompanyId`)

**Development mode:** SingleTenant mode supports hardcoded defaults for simplified testing.

---

## 3. Cross-Cutting Concerns

### Caching
- **LookupService:** 10-min TTL in-memory (full compact invalidation only)
- **ModuleGuardService:** per-request cache
- **EF Core query splitting:** split queries globally to avoid Cartesian joins; opt-in to single-query per call

### Retries & resilience
- **WebhookDispatcher:** exponential backoff (1-960 min), max 8 attempts
- **HttpClient in ApiService:** default timeout; retry policy if configured
- **Rate limiter:** fails open on DB error (never locks users out for infra failure)

### Idempotency
- **Outbox pattern:** events stored before response; delivery is at-least-once
- **Inbound webhooks:** idempotency key deduplicates duplicate deliveries
- **Seed pipelines:** idempotent (check existence before insert); safe to re-run

### Signing & security
- **HMAC-SHA256** for webhook signatures (both directions)
- ⚠️ **Password hashing:** SHA-256 (must upgrade to Argon2id)
- **Rate limiting:** per-IP+username via PostgreSQL atomic upsert (cluster-wide)
- **Auth cookies:** HttpOnly, SameSite=Lax, SecurePolicy=Always (prod)

### Observability
- **Slow query interceptor:** logs any query >500ms with full SQL + params + RequestId
- **OpenTelemetry:** ASP.NET Core, HttpClient, EF Core instrumentation; optional OTLP export
- **Structured logging:** context captured via `ILogger<T>` (request-scoped)

---

## 4. Missing / Thin Services (Enterprise EAM Gaps)

Based on a complete enterprise EAM, these are absent or minimal — see `08_COMPETITIVE_GAP_ANALYSIS.md` for full treatment:

1. **Condition Monitoring / Predictive Maintenance** — no ML-driven RUL, no live sensor integration
2. **Warranty Management** — no entity (only paid-warranty fields on the asset; no claims, no expiry alerts)
3. **Calibration Scheduler** — fields exist on Asset, no service or workflow
4. **Spare Parts Demand Forecasting** — stocking calc but no time-series/ML forecasting
5. **Mobile App Sync** — no offline-capable mobile service
6. **Condition Assessment** — no structured condition ratings or photo workflow
7. **Energy Monitoring** — no utility consumption tracking
8. **Safety Compliance / LOTO** — no lockout-tagout or safety permit workflows
9. **Document Management** — attachments exist but no DMS-grade versioning, ACLs
10. **Contractor Management** — no licensing, insurance expiry, safety training tracking
11. **Environmental/Disposal Compliance** — no e-waste or hazardous material tracking
12. **Geospatial / Linear Asset Service** — no segment/station/KP modeling

---

## 5. Anomalies & Code Quality

### Things to keep an eye on

1. **AuthService is Scoped** — registered as `AddScoped<AuthService>()` but used in `/Account/Login` (before tenant context exists). Should be **Transient** or **Singleton** since it's stateless and used cross-tenant. Risk: accidental tenant leakage if context is mistakenly populated pre-auth.

2. **MaintenanceService has overloaded constructors** — three constructors (no `ILookupService`, with tenant, with both). Makeshift DI; should use optional `ILookupService?` parameter. Risk: constructor-selection ambiguity in edge cases.

3. **DepreciationService reflection fallback** — `JournalGenerator` reflects on `BuildSchedule()` at runtime; silent fallback to SL if reflection fails. Renaming the method breaks journal generation without compile error. Fix: hard interface.

4. **Rate limiter logging salt** — if `RATELIMIT_LOG_SALT` not set, per-process random salt is used (no cross-instance correlation). Ops cannot easily grep logs for repeated attack IPs across instances. Document requirement to set env var for production.

5. **SmartAssistService is keyword-driven only** — extensibility comments mention LLM readiness but not implemented. High false-negative rate on complex requests.

6. **Seeding one-shot guard can be bypassed** — checked via DB record but no distributed lock (PostgreSQL advisory lock). Two concurrent requests might both see "not seeded" and begin seeding. Replit single-instance mitigates today.

7. **InvoiceMatchingService hardcoded tolerance** — 0.01m penny tolerance. Make configurable per company or vendor.

8. **Some null-checks missing** — `SmartAssistService.GenerateDraftWorkOrderAsync()` assumes `WorkRequest.AssetId` is populated; throws if null.

9. **No strongly-typed event payloads in outbox** — `OutboxWriter.WriteAsync(..., object payload, ...)` accepts `object`; payload serialized as JSON string. Schema drift risk.

---

## 6. Summary

**Strengths to preserve:**
- Comprehensive depreciation engine (8+ methods, multiple conventions)
- Transactional outbox pattern (no event loss)
- Distributed rate-limiting (cluster-safe)
- Idempotent seeding pipelines
- Clean separation: tenant context, module guards, audit logging

**Quick wins to prioritize:**
- Tighten constructor patterns (reduce overloads)
- Formalize event schemas (not just `object`)
- Add distributed locking to seeding guard
- Integrate Claude into SmartAssistService (the visionary upgrade)
- Implement warranty, calibration, condition-monitoring services

The services layer is **operationally sound and ready for production**, with incremental improvements in schema validation, error handling, and advanced EAM features being the path to enterprise leadership.
