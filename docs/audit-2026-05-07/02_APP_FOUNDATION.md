# 02 — Application Foundation

**Project:** Abs.FixedAssets (ASP.NET Core 9.0 + Razor Pages + EF Core 9.0 + PostgreSQL)
**Target Framework:** .NET 9.0
**Deployment:** Replit Autoscale, port 5000
**Scope:** Program.cs, DI, middleware, hosting, auth, multi-tenancy, observability, rate limiting, AI wiring, bootstrap services.

---

## 1. Hosting & Runtime

- **.NET Version:** 9.0 (ASP.NET Core 9.0)
- **Kestrel binding:** `http://0.0.0.0:5000` (hardcoded in Program.cs:16)
- **Launch profile** (`Properties/launchSettings.json`): Development mode, http://0.0.0.0:5000, no browser launch.

**Replit deployment** (`.replit`):
- **Build:** `dotnet publish Abs.FixedAssets.csproj -c Release -o out`
- **Run:** `dotnet out/Abs.FixedAssets.dll` (Autoscale deployment target)
- **Port mapping:** internal 5000 → external 80
- **Modules:** dotnet-9.0, postgresql-16, nodejs-20, python-3.11 (for Playwright tests), git
- **Workflows:** parallel execution of Web Server, smoke tests, integration tests (flows, auth, nav, FA, reports, UI). Validates on startup.

**Environment profiles** (detected via DB name in `SeedGuardService` or `ENVIRONMENT_PROFILE` env var):
- **LAB:** "lab" in DB name OR Development mode
- **DEMO:** "demo" in DB name; requires `ALLOW_DEMO_SEED=true` to seed
- **PRODUCTION:** "prod" in DB name; requires explicit override to seed

A visual banner displays the current environment to prevent operator confusion (per README).

---

## 2. NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.4 | EF Core → PostgreSQL driver |
| Microsoft.EntityFrameworkCore.Tools | 9.0.0 | EF tooling: `dotnet ef migrations`, `dotnet ef dbcontext scaffold` |
| Microsoft.EntityFrameworkCore.Design | 9.0.0 | EF design-time services for migrations |
| ClosedXML | 0.104.2 | Excel (.xlsx) generation for asset reports & exports |
| QuestPDF | 2024.10.0 | PDF generation (certificates, work orders, compliance docs) |
| ZXing.Net.Bindings.SkiaSharp | 0.16.14 | Barcode generation (QR, Code128, Code39 for asset tags) |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 2.88.7 | Barcode/PDF rendering on Linux (Replit) without system deps |
| OpenTelemetry.Exporter.OpenTelemetryProtocol | 1.15.3 | OTLP exporter for traces & metrics |
| OpenTelemetry.Extensions.Hosting | 1.10.0 | OTel integration with .NET DI & hosted services |
| OpenTelemetry.Instrumentation.AspNetCore | 1.10.1 | Auto-instrumentation of HTTP requests |
| OpenTelemetry.Instrumentation.EntityFrameworkCore | 1.10.0-beta.1 | EF Core query tracing (beta) |
| OpenTelemetry.Instrumentation.Http | 1.10.0 | HttpClient instrumentation for outbound calls |
| OpenTelemetry.Instrumentation.Process | 0.5.0-beta.7 | Process-level metrics (memory, GC, threads) |
| OpenTelemetry.Instrumentation.Runtime | 1.10.0 | .NET runtime metrics (allocations, exceptions) |

Notable absence: **Serilog is not used** — logging is the built-in `ILogger`. Worth adding a Serilog sink (Seq, File, Loki) for production.

---

## 3. Dependency Injection (DI) Registrations

All registered via `builder.Services.AddX()` in Program.cs.

### EF Core & Database
- `AddDbContext<AppDbContext>`: Npgsql provider, SplitQuery global behavior, slow-query interceptor, First/FirstOrDefault warning control (dev only).

### Tenant & Multi-Tenancy
- `Configure<TenantSettings>`: appsettings.json section (DeploymentMode, DefaultTenantId, DefaultCompanyId, DefaultSiteId).
- `AddSingleton<ITenantContextOverride, TenantContextOverride>`: AsyncLocal-based override stack for scoped tenant switches in background jobs.
- `AddScoped<ITenantContext, TenantContext>`: request-scoped tenant resolution (SingleTenant or MultiTenant mode).
- `AddScoped<ICompanyHierarchyService, CompanyHierarchyService>`: resolves company & site visibility from user's assigned org nodes.

### Core Business Services (Scoped)
- **Accounting:** `DepreciationService`, `CcaService`, `CcaBackfillService`, `UsTaxService`, `HistoricJournalBackfillService`
- **Maintenance:** `MaintenanceService`, `AuditService`
- **CIP:** `CipService`, `CipCostService`, `CipAutoCostPostingService`, `CipCapitalizationService`, `CipTraceQueryService`
- **Inventory:** `InventoryService`, `IItemStockingService`, `BarcodeService`
- **Purchasing/Procurement:** `BulkOperationsService`, `ImportService`, `InvoiceMatchingService`
- **AI & Reporting:** `AiAssistantService`, `ReportBuilderService`, `SmartAssistService`
- **API:** `ApiService`, `ExportService`, `TemplateService`, `MasterDataImportService`, `AttachmentService`

### Lookup & Configuration
- `AddScoped<ILookupService, LookupService>`: cached enumeration lookups (the FK-bound dropdown backbone)
- `AddScoped<IModuleGuardService, ModuleGuardService>`: feature flags (WorkOrders, Purchasing, AP, Vendors, Inventory)
- `AddScoped<ICompanyService, CompanyService>`
- `AddMemoryCache()`: shared `IMemoryCache`

### Bootstrap & Seeding
- `AddScoped<IMasterDataBootstrapService, MasterDataBootstrapService>`: system reference seed
- `AddScoped<ISeedGuardService, SeedGuardService>`: prevents seeding in DEMO/PROD without override
- `AddScoped<ISeedPackExecutor, SeedPackExecutor>`: executes seed pipelines (system, org, vendors, demo)
- `AddSeedingServices()`: extension method registers all 8 seed pipelines

### Webhooks & Integrations
- `AddScoped<IOutboxWriter, OutboxWriter>`: outbox pattern for reliable webhook delivery
- `AddHostedService<WebhookDispatcherHostedService>`: background polling of OutboxEvents
- `AddScoped<IInboundWebhookService, InboundWebhookService>`: receives inbound integration events
- `AddScoped<IIntegrationMappingService, IntegrationMappingService>`: maps external event → domain event
- `AddHostedService<InboundEventProcessorHostedService>`: background processing of InboundEvents

### Revisions & Cross-References (Items)
- `AddScoped<IPMTemplateRevisionService, PMTemplateRevisionService>`: PM template versioning
- `AddScoped<IItemRevisionService, ItemRevisionService>`: inventory item revisions
- `AddScoped<IItemCrossReferenceService, ItemCrossReferenceService>`: OEM/MFR cross-refs
- `AddScoped<IItemSourcingService, ItemSourcingService>`: vendor sourcing strategy
- `AddScoped<IItemAlternateService, ItemAlternateService>`: item substitutes
- `AddScoped<IItemSupersessionService, ItemSupersessionService>`: item obsolescence

### Maintenance Planning
- `AddScoped<IWorkRequestConversionService, WorkRequestConversionService>`: work requests → work orders
- `AddScoped<ICloseoutService, CloseoutService>`: WO closeout logic
- `AddScoped<IWorkOrderOriginService, WorkOrderOriginService>`: track WO source
- `AddScoped<IPMSchedulerService, PMSchedulerService>`: calendar-based PM generation

### Testing & Diagnostics
- `AddScoped<ISmokeTestDataFactory, SmokeTestDataFactory>`: generate test data
- `AddScoped<ISmokeTestRunner, SmokeTestRunner>`: execute smoke tests (dev-only API: GET /api/smoke/run)
- `AddSingleton<ISmokeTestRunQueue, SmokeTestRunQueue>`: async test queue
- `AddSingleton<ISmokeTestRunStore, SmokeTestRunStore>`: store test results
- `AddHostedService<SmokeTestBackgroundService>`: async test scheduler

### Content & Enrichment
- `AddScoped<IItemImageService, ItemImageService>`: asset photo storage/retrieval
- `AddScoped<ICatalogMetadataEnrichmentService, CatalogMetadataEnrichmentService>`: enrich items from vendor catalogs
- `AddScoped<IBuyabilityScoreService, BuyabilityScoreService>`: score items for procurement strategy
- `AddScoped<IEffectiveProcurementService, IEffectiveProcurementService>`: lowest-cost sourcing analysis
- `AddScoped<IPreferredVendorCatalogResolver, PreferredVendorCatalogResolver>`: map item → preferred vendor catalog

### Other Services
- `AddScoped<AuthService>`: user authentication & default user seeding
- `AddScoped<IPeriodGuard, PeriodGuard>`: period-lock validation
- `AddScoped<DepreciationBackfillService>`: backfill depreciation for prior periods
- `AddHttpClient()`: `IHttpClientFactory` for outbound calls (OpenAI, webhooks, integrations)

### Health & Rate Limiting
- `AddHealthChecks()`:
  - `DbHealthCheck` (tag: "ready") — PostgreSQL connectivity
  - `SkiaHealthCheck` (tag: "ready") — barcode/PDF library availability
- `AddSingleton<IDistributedLoginRateLimiter, PostgresLoginRateLimiter>`: 100/min per (IP, username), atomic upsert on RateLimitCounters table
- `AddHostedService<RateLimitCounterCleanupService>`: prune stale rate-limit counters

### Response & Observability
- `AddResponseCompression()`: Brotli (fastest) + Gzip for HTML & JSON (~70-80% bandwidth savings)
- `AddOpenTelemetry()`: traces + metrics, OTLP exporter (conditional on `OTEL_EXPORTER_OTLP_ENDPOINT`)

### Authentication & Authorization
- `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)`: cookie-based auth
- `.AddCookie(options)`: 8-hour expiration, sliding renewal, HttpOnly + SameSite=Lax (dev) / Lax + Secure in prod
- `AddAuthorization(options)`: policies — **AdminOnly**, **AccountantOrAdmin**, **AllUsers**; fallback=null (Razor Pages require explicit `[Authorize]`)

---

## 4. Middleware Pipeline (in order)

| # | Middleware | Purpose |
|---|---|---|
| 1 | `UseForwardedHeaders` | Restore X-Forwarded-For & X-Forwarded-Proto from Replit edge proxy |
| 2 | `UseServerTiming` | Emit Server-Timing header (total pipeline duration) |
| 3 | `UseSecurityHeaders` | CSP (nonce-based), X-Content-Type-Options, Referrer-Policy, Permissions-Policy |
| 4 | `UseResponseCompression` | Brotli/Gzip before body written |
| 5 | Custom iframe/caching fix | Remove X-Frame-Options, disable HTML caching (Cache-Control: no-cache) |
| 6 | Custom redirects | /Admin/* → /Assets|Materials|Maintenance|Books|Purchasing (301 permanent) |
| 7 | `UseStaticFiles` | Serve /css, /js, /lib, /images |
| 8 | `UseRouting` | Route matching |
| 9 | `UseRequestId` | Inject X-Request-Id (per-request unique ID for log scopes) |
| 10 | Username snoop (POST /Account/Login) | Read form to extract username, cache in HttpContext.Items for rate limiter |
| 11 | `UseDistributedLoginRateLimit` | 100/min per (IP, username) from RateLimitCounters table |
| 12 | `UseAuthentication` | Validate/deserialize auth cookie → ClaimsPrincipal |
| 13 | `UseApiHeaderEnforcement` | Validate API header signatures (where applicable) |
| 14 | `UseOrgScope` | Multi-tenant org node resolution from X-Org-Node-Id header |
| 15 | `UseTenantContext` | SingleTenant or MultiTenant resolution + company hierarchy |
| 16 | `UseAuthorization` | Evaluate authorization policies |
| 17 | Health endpoints (/_live, /healthz, /readyz) | Anonymous; no tag filter for /_live/healthz (liveness only) |
| 18 | /_otel/diag | OTel diagnostic endpoint (anonymous, reports TracerProvider/MeterProvider state) |
| 19 | `MapRazorPages().RequireAuthorization()` | Razor Pages (all require auth) |
| 20 | `MapControllers()` | API controllers (auth optional; per-controller `[Authorize]`) |
| 21 | /api/smoke/run (dev only) | Manual smoke test trigger |

### Security Headers (SecurityHeadersMiddleware)
- **CSP:** per-request nonce on `script-src` & `style-src`; allows CDN (cloudflare, jsdelivr); inline event handlers/style attributes still allowed (`script-src-attr/style-src-attr 'unsafe-inline'`) — TODO comment notes future tightening to `addEventListener` + CSS classes.
- **Frame-Ancestors:** whitelists Replit preview iframe (`*.replit.dev`, `*.replit.app`, `*.repl.co`, `*.replit.com`)
- **Permissions-Policy:** blocks camera, microphone, geolocation, payment, USB, etc.
- **Referrer-Policy:** `strict-origin-when-cross-origin`

---

## 5. Background & Hosted Services

All implement `IHostedService` and auto-start at app launch.

| Service | Type | Schedule | Purpose |
|---|---|---|---|
| `RateLimitCounterCleanupService` | IHostedService | Hourly | Purge RateLimitCounters >24h to prevent table bloat |
| `WebhookDispatcherHostedService` | BackgroundService | Continuous polling | Poll OutboxEvents; deliver webhooks with HMAC signing + exponential backoff (1, 5, 15, 60, 120, 240, 480, 960 min, 8 attempts max, 30s timeout) |
| `InboundEventProcessorHostedService` | BackgroundService | Continuous polling | Consume InboundEvents queue; apply IntegrationMappings; post domain events |
| `SmokeTestBackgroundService` | IHostedService | On-demand (dev) | Async test scheduler; queues tests from /api/smoke/run endpoint |

**Notable absence:** no timer-based maintenance jobs (e.g., scheduled depreciation runs, periodic inventory reconciliation, PM schedule generation cron). PM generation is currently manual or API-triggered. This is a candidate for adding a `PmGenerationCronService` that runs daily.

---

## 6. Authentication & Authorization

**Scheme:** ASP.NET Core Cookie authentication.

**Cookie posture (security-hardened):**
- HttpOnly: true (blocks XSS reads)
- SameSite: Lax in both dev and prod (prevents CSRF on cross-site POST; allows top-level GET nav)
- SecurePolicy: `SameAsRequest` in dev, `Always` in prod (forces HTTPS)
- Expiration: 8 hours sliding renewal
- Paths: LoginPath=/Account/Login, LogoutPath=/Account/Logout, AccessDeniedPath=/Account/AccessDenied

**Roles & policies:**
- **Roles in DB** (User.Role field): Admin, Accountant, Viewer (per HANDOFF_STATUS.md)
- **Policies:**
  - `AdminOnly`: RequireRole("Admin")
  - `AccountantOrAdmin`: RequireRole("Admin", "Accountant")
  - `AllUsers`: RequireAuthenticatedUser()
- Fallback: null — no anonymous fallback; Razor Pages explicitly require `[Authorize]`

**⚠️ Status: Authentication is currently DISABLED for development.** Per Program.cs comment around line 282 and README, the fallback policy currently allows anonymous to ease iframe testing in Replit. **This MUST be re-enabled before any production customer touches the system.** The pipeline is wired up correctly; just remove the anonymous fallback.

**⚠️ Password hashing is SHA-256.** AuthService uses SHA-256 for passwords. This needs to be **upgraded to Argon2id** (recommended) or bcrypt before launch. SHA-256 is no longer acceptable for password storage.

---

## 7. Multi-Tenancy

**Modes:** SingleTenant (default per appsettings.json) or MultiTenant.

```json
"TenantSettings": {
  "DeploymentMode": "SingleTenant",
  "DefaultTenantId": 1,
  "DefaultCompanyId": 2,
  "DefaultSiteId": 1
}
```

**Resolution (`TenantContextMiddleware`):**

1. **SingleTenant mode:**
   - On first request, resolve default Tenant & Company from DB (cached + semaphore-locked to avoid race)
   - Fallback: use ID 1 from config; if table empty, resolve to first row
   - Cache in static `_singleTenantCache` (memoized across all requests)

2. **MultiTenant mode:**
   - Parse headers: `X-CherryAI-Tenant`, `X-CherryAI-Company`, `X-CherryAI-Site` (numeric ID or code)
   - Validate tenant existence & IsActive flag
   - Return 400 JSON error if tenant not found (except exempt paths: /Account/Login, /health, /healthz, /readyz, /_live)

3. **Company hierarchy resolution:**
   - User's `AssignedCompanyId` + assigned role → visible company IDs (via `ICompanyHierarchyService`)
   - Fallback: if current `CompanyId` not in visible list, use `AssignedCompanyId` or first visible
   - Site visibility: via `GetVisibleSiteIdsAsync` (filters sites by assigned company)

4. **Site resolution (from cookie):**
   - Read `cherryai_site_id` cookie
   - If ID is in `VisibleSiteIds`, set as active SiteId
   - Used for asset/location scoping within a company

**OrgScope Middleware (advanced):**
- Parses `X-Org-Node-Id` header (UUID) for organizational hierarchy nav
- NodeTypes: holding, company, site, location
- Validates user's `AssignedCompanyId` against org node's company before switching context
- Returns 403 if org node's company not in user's visible scope (API requests)

**API vs UI behavior:**
- API (path /api) → 403 Forbidden if scope validation fails
- UI (Razor) → silently rejects; caller proceeds without scope change

**`ITenantContextOverride` (AsyncLocal stack):**
For background jobs and batch operations that need to switch tenant context temporarily without polluting the request scope. Uses `AsyncLocal<Stack<TenantContextOverrideValues>>` — clever, but document perf implications if used heavily.

---

## 8. Configuration Sources

**Priority (highest to lowest):**

1. **Environment variables:**
   - `PGHOST`, `PGPORT`, `PGUSER`, `PGPASSWORD`, `PGDATABASE` — Replit PostgreSQL
   - `THROW_EF_FIRST_WARNING` — EF warning behavior (dev)
   - `OTEL_EXPORTER_OTLP_ENDPOINT` — OpenTelemetry endpoint (optional)
   - `AUTO_MIGRATE` — force migrations (prod safeguard)
   - `RUN_SEED` — explicit seed trigger
   - `AUTO_SEED_ON_EMPTY` — auto-seed if DB empty (dev)
   - `DEMO_DATA_ENABLED` — override DemoData config
   - `ALLOW_DEMO_SEED` — permit seed in DEMO/PROD env
   - `ASPNETCORE_ENVIRONMENT` — Development/Production
   - `ENVIRONMENT_PROFILE` — LAB/DEMO/PRODUCTION (fallback)
   - `AI_INTEGRATIONS_OPENAI_BASE_URL`, `AI_INTEGRATIONS_OPENAI_API_KEY` — OpenAI config
   - `RATELIMIT_LOG_SALT` — rate limit log key salt

2. **appsettings.json** (env-neutral): Logging defaults, TenantSettings, DemoData.Enabled=false, AllowedHosts="*"

3. **appsettings.Development.json:**
   - ⚠️ **ConnectionString fallback contains plaintext Azure SQL credentials** (`Server=abs-fixedassets-sql.database.windows.net,1433;...`) — should move to user-secrets.
   - DemoData.Enabled: true

4. **appsettings.Production.json:** JSON formatter, scopes included, UTC timestamps. No connection string (uses env vars).

**Database URL parsing (SeedGuardService):**
- `DATABASE_URL` env var (postgres:// or postgresql://) or DefaultConnection string
- Extracts DB name to detect LAB/DEMO/PROD environment
- Format: `postgresql://user:pass@host:port/dbname`

---

## 9. Module Guards & Feature Flags

**Service:** `IModuleGuardService` (`ModuleGuardService`)

**Modules:** WorkOrders, Purchasing, AccountsPayable, Vendors, Inventory

**Resolution:**
- Queries first active root company (`ParentCompanyId=null`) or any company
- Reads `Enable*` boolean flags (EnableWorkOrders, EnablePurchasing, etc.)
- Caches in `_cachedStatus` (per DI scope)
- Fallback: all modules enabled if company not found

```csharp
IsModuleEnabledAsync(string moduleName)
  // Maps:
  //   "workorders", "maintenance" → WorkOrdersEnabled
  //   "purchasing", "requisitions", "purchaseorders" → PurchasingEnabled
  //   "vendors" → VendorsEnabled
  //   "inventory", "items", "stocklevels" → InventoryEnabled
  //   etc.
```

This is what powers conditional sidebar nav rendering and "module disabled" page redirects.

---

## 10. Health Checks, Logging, Observability

### Health endpoints

| Endpoint | Purpose | Checks | Auth |
|---|---|---|---|
| `/_live` | Liveness probe (process alive) | None (process responsive) | Anonymous |
| `/healthz` | Legacy liveness alias | None | Anonymous |
| `/readyz` | Readiness probe | DbHealthCheck, SkiaHealthCheck | Anonymous |
| `/_otel/diag` | OTel diagnostics | Reports TracerProvider, MeterProvider, OTLP state | Anonymous |

### Logging
- **Development:** Console, default=Information, Microsoft=Warning
- **Production:** JSON console formatter (single-line, no pretty-print), UTC timestamps, scopes included
- ⚠️ Serilog **not detected** — using built-in ILogger only. Recommend adding a Serilog sink (Seq, File, Loki) for production observability.
- **SlowQueryInterceptor** (`Services/Diagnostics/SlowQueryInterceptor.cs`): logs EF commands >500ms with full SQL + params + duration (picks up RequestId from logger scope). Threshold is hardcoded — no config override.

### OpenTelemetry
**Traces:**
- AspNetCore instrumentation (excludes /_live, /healthz, /readyz from sampling)
- HttpClient instrumentation (outbound API calls)
- EF Core instrumentation (queries, commands)
- Defense-in-depth: nulls out `Cookie`, `Authorization`, `Set-Cookie` headers in trace tags

**Metrics:**
- AspNetCore (requests, duration, status codes)
- HttpClient (request count, duration)
- Runtime (allocations, GC, exceptions)
- Process (memory, thread count)
- EF Core Meter source: "Microsoft.EntityFrameworkCore"

**Exporter:** OTLP/HTTP (HttpProtobuf) only if `OTEL_EXPORTER_OTLP_ENDPOINT` env var set
- Service name: "cherryai-eam"
- Deployment environment: ASPNETCORE_ENVIRONMENT tag

---

## 11. Rate Limiting

**Implementation:** Distributed (PostgreSQL-backed), not in-process.

**Service:** `PostgresLoginRateLimiter` (`IDistributedLoginRateLimiter`)

**Budget:** 100 permits per 60-second window per partition key (IP + username).

**Mechanism:**
1. Username snooped from POST /Account/Login form (RequestIdMiddleware fallback)
2. Partition key: `{IP}:{username}` (sourced from `HttpContext.Items["LoginUsername"]`)
3. Window: 1-minute UTC buckets (truncated to minute boundary)
4. **Atomic upsert** via raw SQL (not EF, for atomicity):

```sql
INSERT INTO "RateLimitCounters" (PartitionKey, WindowStartUtc, Count, CreatedAtUtc, UpdatedAtUtc)
VALUES (@key, @windowStart, 1, @now, @now)
ON CONFLICT (PartitionKey, WindowStartUtc) DO UPDATE
  SET Count = Count + 1, UpdatedAtUtc = EXCLUDED.UpdatedAtUtc
RETURNING Count;
```

5. **Fail-open:** DB error → allow request, log hashed key (SHA256(salt || key), first 6 hex chars). This is the right call — never lock users out for infra failure.
6. **Cleanup:** `RateLimitCounterCleanupService` prunes rows >24h old hourly.

**Schema:** `RateLimitCounters` table with PartitionKey, WindowStartUtc (unique composite), Count, CreatedAtUtc, UpdatedAtUtc.

---

## 12. Auto-Migrate & Seed on Startup

**Startup flow (Program.cs:318-416):**

1. **Migrations (conditional):**
   - If Development OR `AUTO_MIGRATE=true` → `db.Database.MigrateAsync()`
   - If Production → `db.Database.EnsureCreated()` only (no auto-migrate; require manual deployment)
   - Console: `[Startup] Applying/skipping database migrations...`

2. **Default Books (if empty):**
   - **GAAP** (Straight-Line, Full-Month convention, 120 months useful life)
   - **TAX** (Double-Declining-Balance, Half-Year convention, 84 months useful life)

3. **CCA Classes (Canada):** `CcaClassSeeder.SeedCcaClassesAsync(db)`

4. **Lookup Reference Data:** `LookupDirectSeeder.SeedAsync(db)` (idempotent from JSON). Failure is non-fatal; logs warning only.

5. **Default Users:** `AuthService.SeedDefaultUserAsync()`

6. **Demo Data (conditional guard):**
   - **Condition A (explicit):** `RUN_SEED=true` AND Development → run `Seed.InitializeAsync(db)`
   - **Condition B (auto on empty):** `AUTO_SEED_ON_EMPTY=true` AND `db.Companies.Any()==false` AND Development → run `Seed.InitializeAsync(db)`
   - **Condition C (blocked):** `RUN_SEED=true` AND NOT Development → log warning, skip
   - **Fallback:** if no companies & no seed trigger → log initialization guide, start with empty DB

7. **Seeding validation:**
   - `SeedGuardService.CheckSeedPermission()` → checks `ASPNETCORE_ENVIRONMENT`, DB name, `ALLOW_DEMO_SEED` override
   - Console: `[Startup] Demo Data Mode: {ENABLED|DISABLED}`
   - Console: `[Startup] Environment Profile: {LAB|DEMO|PRODUCTION}`

8. **CIP Reconciliation:** `CipService.ReconcileAllProjectCostsAsync()` (startup safety check, non-fatal on error).

---

## 13. AI Integration

**Service:** `AiAssistantService` (Scoped)

**Configuration:**
- Base URL: `AI_INTEGRATIONS_OPENAI_BASE_URL` env var (defaults to https://api.openai.com/v1)
- API Key: `AI_INTEGRATIONS_OPENAI_API_KEY` env var (empty string fallback — **NOT CONFIGURED BY DEFAULT**)
- HttpClient: authenticated with Bearer token; Accept: application/json

**Capabilities (inferred from service methods):**
- `GetAssetContextAsync()`: aggregates asset DB summary (total count, active count, value, depreciation, top assets by value, location summary)
- Returns Markdown-formatted report with hyperlinks back to asset pages (/Assets, /Assets/Details/{id})

**Status:** OpenAI integration present but key not configured in appsettings. Service will fail at runtime if called without env var set. No smart assistant UI wiring observed yet — chat module exists at `/Pages/AI/` but is bare-bones.

This is a key area to upgrade: swap the inference engine in `SmartAssistService` from regex/keyword matching to a Claude API call, and enrich the AI chat with conversational asset twin functionality (chat with any asset's full record).

---

## 14. Bootstrap Services

### `MasterDataBootstrapService` (`IMasterDataBootstrapService`)
Seeded at startup but not auto-invoked; intended for admin API or manual trigger.

- **`RunSystemReferenceSeedAsync()`:** WorkOrderTypes (PM, CM, PDM, EM, PRJ, INSP, CAL, SAF, INST, DEMO, RELO, MOD), FailureCodes (mech-wear, elec-short, etc.), CauseCodes, PriorityLevels, Crafts, NumberingSequences, PaymentTerms, Currencies, Section179Limits (2020-2026), BonusDepreciationRates
- **`RunCustomerMasterLoadAsync()`:** GL Accounts (23 standard chart), Sites (5 demo), Departments, CostCenters (9), AssetCategories (12)
- **`RunDemoSeedAsync()`:** PM Templates (8 templates, calendar & meter-based)
- **`ImportFromCsvAsync<T>()`:** generic CSV → DB importer with quoted field handling
- All return `SeedResult`: domain, total, inserted, updated, skipped, failed, errors

### `SeedGuardService` (`ISeedGuardService`)
- `CheckSeedPermission()`: validates environment, DB name, overrides before seeding
- `GetEnvironmentProfile()`: detects LAB/DEMO/PRODUCTION from DB name or config
- `GetMaskedConnectionString()`: masks passwords (first 12 chars + ***) for logging
- `IsLabEnvironment()`, `IsDemoEnvironment()`, `IsDemoDataEnabled()`

⚠️ **Risk:** seeding guard checks via DB record but no distributed lock (e.g., PostgreSQL advisory lock). Two concurrent requests might both see "not seeded" and begin seeding. Mitigation: seed only at startup before request pipeline; Replit single-instance mostly mitigates today, but harden before scale-out.

---

## 15. Notable Observations & Risks

### Security posture (positive)
1. CSP + per-request nonce on inline scripts
2. Distributed rate limiting (atomic Postgres upsert)
3. Cookie hardened: HttpOnly + SameSite=Lax + SecurePolicy=Always in prod
4. OTel redaction strips Cookie/Authorization/Set-Cookie from traces
5. Fail-open rate limiter (no lockout on infra outage)

### Risks & TODOs
1. **⚠️ Plaintext Azure SQL credentials in `appsettings.Development.json`** — move to user-secrets or Azure Key Vault before this leaks via git
2. **⚠️ Auth disabled in dev** — confirmed; remove anonymous fallback before prod
3. **⚠️ Password hashing is SHA-256** — upgrade to Argon2id with per-user salt
4. **⚠️ OpenAI key not configured** — service will fail silently or throw at runtime
5. **No Serilog/structured sink** — recommend adding for prod observability
6. CSP allows inline event handlers and inline styles — TODO comment notes future hardening (`addEventListener`, CSS classes)
7. ModuleGuardService caches per DI scope — invalidation on config change is unclear
8. Migration mode complexity — document deployment safeguards clearly (when AUTO_MIGRATE=true is appropriate)
9. SmokeTestBackgroundService queue is in-memory — lost on restart
10. SlowQueryInterceptor threshold (500ms) is hardcoded — make configurable
11. RateLimitCounters cleanup runs hourly — consider explicit TTL index on DB
12. **No CORS configuration** — verify if needed for browser-based external integrations
13. Hard-coded /Admin/* redirects — consider moving to config

### Code quality
1. Program.cs is ~620 lines — consider further extraction to extension methods
2. AsyncLocal-based tenant override is clever but non-obvious; document
3. `SeedGuardService` DB name extraction tries postgres:// first, then key=value pairs; could miss edge cases (SQL Server named instances etc.)
4. AppDbContext has many DbSets but no OnModelCreating fluent config in this file — verify all entities have migrations

---

## 16. Directory Structure (Reference)

```
/
├── Program.cs                          # Main startup wiring (~620 lines)
├── appsettings*.json                   # Config layers
├── .replit                             # Replit deployment & workflow definitions
├── Middleware/                         # Tenant, RequestId, Security, OrgScope, LoginRateLimit, ApiHeader, ServerTiming, NonceTagHelper
├── Data/AppDbContext.cs                # ~99+ DbSets
├── Services/                           # 50 service classes across 13 subdirectories
│   ├── Health/, Diagnostics/, RateLimiting/, Lookups/, Navigation/
│   ├── Cip/, Seeding/, Testing/, Maintenance/
│   ├── Webhooks/, Integrations/, Revisions/, Items/
├── Models/                             # 66 entity definitions (see 03_DOMAIN_MODELS_AND_SCHEMA.md)
├── Pages/                              # 410 Razor pages (see 05_PAGES_AND_UI.md)
├── Controllers/                        # 10 API controllers (see 06_CONTROLLERS_AND_API.md)
├── Helpers/                            # UiTerms.cs (UI terminology helpers)
├── Properties/launchSettings.json      # Dev launch profile
├── Abs.FixedAssets.csproj              # NuGet references
├── out/                                # Published build artifacts
├── config/                             # Lookup JSON configs (detail_contract.json, lookup_baselines.json)
├── docs/                               # 64 documentation files (see 07_PROJECT_DOCUMENTATION.md)
└── seed/reference-data/                # 76 lookup type JSON files
```

---

## Summary

CherryAI EAM is a **mature, multi-tenant ASP.NET Core 9.0 application** with:

✅ Production-ready infrastructure (Replit Autoscale, PostgreSQL, distributed rate limiting)
✅ Sophisticated multi-tenancy & org hierarchy routing
✅ Modern observability (OpenTelemetry, health checks, slow-query tracking)
✅ Security-hardened (CSP + nonce, cookie posture, OTel trace redaction)
⚠️ Development shortcuts to clear (auth disabled, secrets in appsettings.Development.json, OpenAI not configured, SHA-256 passwords)
⚠️ Startup complexity (conditional migrations, seeding guards, bootstrap services)

**For onboarding:** Start with Program.cs (DI order), then `TenantContextMiddleware` (multi-tenancy resolution), then `AppDbContext` (domain breadth), then individual service layers (business logic).
