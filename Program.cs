using Abs.FixedAssets.Data;
using Abs.FixedAssets.Endpoints;
using Abs.FixedAssets.Middleware;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Use URLs from environment or default to port 5000
builder.WebHost.UseUrls("http://0.0.0.0:5000");

// Get database connection from environment variables (Replit PostgreSQL)
string connectionString;
var pgHost = Environment.GetEnvironmentVariable("PGHOST");
var pgPort = Environment.GetEnvironmentVariable("PGPORT");
var pgUser = Environment.GetEnvironmentVariable("PGUSER");
var pgPassword = Environment.GetEnvironmentVariable("PGPASSWORD");
var pgDatabase = Environment.GetEnvironmentVariable("PGDATABASE");

if (!string.IsNullOrEmpty(pgHost))
{
    // Use SSL for production (Replit requires secure connections)
    var sslMode = builder.Environment.IsDevelopment() ? "Prefer" : "Require";
    // Pool sizing + command timeout sized for concurrent load.
    connectionString = $"Host={pgHost};Port={pgPort};Database={pgDatabase};Username={pgUser};Password={pgPassword};SSL Mode={sslMode};Trust Server Certificate=true;Maximum Pool Size=200;Timeout=30;Command Timeout=60";
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
}

Console.WriteLine($"[Startup] ASPNETCORE_ENVIRONMENT = {builder.Environment.EnvironmentName}");

// Check for EF warning throw mode (Development only)
var throwOnFirstWarning = Environment.GetEnvironmentVariable("THROW_EF_FIRST_WARNING")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
if (throwOnFirstWarning && builder.Environment.IsDevelopment())
{
    Console.WriteLine("[Startup] THROW_EF_FIRST_WARNING=true - EF will throw on First/FirstOrDefault without OrderBy");
}

// MVC/Razor Pages - authentication optional (Replit iframe compatibility)
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Admin/Locations", "/Assets/Locations");
    options.Conventions.AddPageRoute("/Admin/Vendors", "/Materials/Vendors");
    options.Conventions.AddPageRoute("/Admin/PMTemplates", "/Maintenance/PMTemplates");
    options.Conventions.AddPageRoute("/Admin/PMScheduleEdit", "/Maintenance/PMScheduleEdit");
    options.Conventions.AddPageRoute("/Admin/GlAccounts", "/Books/GlAccounts");
    options.Conventions.AddPageRoute("/Admin/PMTemplateEdit", "/Maintenance/PMTemplateEdit");
    options.Conventions.AddPageRoute("/Admin/Requisitions", "/Purchasing/Requisitions");
    options.Conventions.AddPageRoute("/Admin/Kits", "/Materials/Kits");
    options.Conventions.AddPageRoute("/Admin/StockLevels", "/Inventory/StockLevels");
    options.Conventions.AddPageRoute("/Admin/ItemCategories", "/Materials/Categories");
    options.Conventions.AddPageRoute("/Admin/AssetCategories", "/Assets/Categories");
    options.Conventions.AddPageRoute("/Admin/Barcodes", "/Assets/Barcodes");

    // ADR-014 D1 — VoiceContextEmitter page filter. Runs post-handler
    // on every Razor Pages request; if the page model inherits from
    // VoiceReadyPageModel, captures the per-page voice context payload
    // into HttpContext.Items["voice.ctx"] for the Sprint 5 voice client
    // to read.
    options.Conventions.ConfigureFilter(
        new Abs.FixedAssets.Services.Infrastructure.VoiceContextEmitter());
});
builder.Services.AddHttpClient();

// OpenAPI / Swagger — exposed when ENABLE_SWAGGER=true or in Development.
// In production, set ENABLE_SWAGGER=true on the running app (and ideally
// front it with admin auth at the proxy layer) only when an integration
// partner needs the spec.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "CherryAI EAM API",
        Version = "v1",
        Description = "Enterprise Asset Management API surface. Cookie auth — sign in via /Account/Login first; controllers run inside the same auth pipeline."
    });

    var cookieScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
        Name = ".AspNetCore.Cookies",
        Description = "ASP.NET Core authentication cookie. Issued by /Account/Login. Send on all calls that require auth."
    };
    options.AddSecurityDefinition("CookieAuth", cookieScheme);
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            {
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                Id = "CookieAuth"
            }
        }] = Array.Empty<string>()
    });
});

// EF Core slow-query interceptor — logs any DB command >500ms with full
// SQL+params+duration, picking up RequestId from the logger scope.
builder.Services.AddSingleton<Abs.FixedAssets.Services.Diagnostics.SlowQueryInterceptor>();

// EF Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    // SplitQuery globally to avoid Cartesian-product joins from multi-Include
    // detail queries; opt back into SingleQuery per-query via .AsSingleQuery().
    options.UseNpgsql(connectionString, npg =>
    {
        npg.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        // Sprint 12C / ADR-020 §D2 + ADR-021 — register pgvector type mappings.
        // Enables halfvec(1024) column on the Embeddings table.
        npg.UseVector();
    });
    options.AddInterceptors(sp.GetRequiredService<Abs.FixedAssets.Services.Diagnostics.SlowQueryInterceptor>());

    // ALL environments: suppress PendingModelChangesWarning so MigrateAsync()
    // can run in production. The AppDbContext model has accumulated drift vs
    // the latest migration snapshot (shadow-state FKs on FaiProductAccountability,
    // CustomerProject, PMOccurrence, etc.). EF Core 9 elevates this drift to a
    // fatal warning by default. We cannot safely auto-capture the drift because
    // dotnet ef migrations add generates a destructive 46-DropTable + 57-DropColumn
    // migration. Suppressing the warning lets MigrateAsync apply pending migrations
    // normally while we tackle the actual entity FK config cleanup as a separate
    // careful effort. See discovery_helium_vs_prod_db_2026_05_24 memory.
    options.ConfigureWarnings(warnings =>
    {
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
    });

    // Development-only: Configure warning behavior for First/FirstOrDefault without OrderBy
    if (builder.Environment.IsDevelopment())
    {
        options.ConfigureWarnings(warnings =>
        {
            if (throwOnFirstWarning)
            {
                // Throw exception to get stack trace
                warnings.Throw(CoreEventId.FirstWithoutOrderByAndFilterWarning);
            }
            else
            {
                // Log as warning (default behavior, but explicit)
                warnings.Log(CoreEventId.FirstWithoutOrderByAndFilterWarning);
            }
        });
    }
});

// Tenant context configuration
builder.Services.Configure<TenantSettings>(builder.Configuration.GetSection("TenantSettings"));
builder.Services.AddSingleton<ITenantContextOverride, TenantContextOverride>();
builder.Services.AddScoped<ITenantContext>(sp => new TenantContext(sp.GetRequiredService<ITenantContextOverride>()));
builder.Services.AddScoped<ICompanyHierarchyService, CompanyHierarchyService>();

// Your DI services
builder.Services.AddScoped<DepreciationService>();

// ADR-003: central GL account resolver. Memory-cached per (CompanyId,
// AccountKind) with 10-minute TTL inside the resolver itself; the
// service is scoped because it consumes the per-request AppDbContext.
builder.Services.AddScoped<IGlAccountResolver, GlAccountResolver>();

// Sprint 12.7 PR #2 — Controller Control Center source-to-GL drilldown.
// Scoped because it reads from the per-request AppDbContext. Zero
// DbContext mutation (Lock 15 compliant): pure AsNoTracking() queries
// that walk Asset → CipCapitalization → CipProject → CipCosts and recent
// Depreciation JEs → JournalLines → AccountingKey segment context.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Controller.IControllerCockpitService,
    Abs.FixedAssets.Services.Controller.ChainTraceService>();

// Sprint 12.7 PR #4 — Controller Control Center hero KPI band hydration.
// Scoped for the same reasons as PR #2. Lock 15 compliant: pure
// AsNoTracking() reads over JournalLines / GlAccounts / VendorInvoices /
// PurchaseOrders / CipProjects. Computes 4 tiles: Cash position · AP due
// this week · Open POs · WIP balance.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Controller.IFinanceKpiService,
    Abs.FixedAssets.Services.Controller.FinanceKpiService>();

// ADR-025 D5 / Sprint 12.9 PR #3 — IWorkOrderService extracts the 17 direct
// SaveChangesAsync writes off Pages/WorkOrders/Details.cshtml.cs into a typed
// service. PR #3 v1 covers 5 of 17 (operations CRUD + planned-material add);
// PRs #3.1-3.3 finish the JE-posting + WO-level writes. Each subsequent PR
// extends both this DI registration and IWorkOrderService.
builder.Services.AddScoped<Abs.FixedAssets.Services.Maintenance.IWorkOrderService,
    Abs.FixedAssets.Services.Maintenance.WorkOrderService>();

// ADR-025 D5 / Sprint 12.9 PR #4 — IPurchasingService extracts the 12 direct
// SaveChangesAsync writes off Pages/Purchasing/Details.cshtml.cs into a typed
// service (2nd-worst-offender refactor; no JE/inventory complexity vs WO).
// De-risks Sprint 13 Purchasing Control Center.
builder.Services.AddScoped<Abs.FixedAssets.Services.Purchasing.IPurchasingService,
    Abs.FixedAssets.Services.Purchasing.PurchasingService>();

// ADR-025 D5 / Sprint 12.9 PR #5 — IItemMasterService extracts the 11 direct
// SaveChangesAsync writes off Pages/Materials/ItemEdit.cshtml.cs into a typed
// service (3rd-worst-offender refactor; pure CRUD — no JE/inventory).
// De-risks Sprint 7-9 Item Master Expansion + 11-tab ItemEdit rewrite.
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemMasterService,
    Abs.FixedAssets.Services.Items.ItemMasterService>();

// ADR-025 D5 / Sprint 13.5 PR #2 — ICustomerProjectService is the mutation
// surface for the CustomerProject hierarchy (Programs / CustomerProjects /
// ProjectMembers / ProjectPhases / ProjectAmendments) plus the link from
// ProductionOrder.CustomerProjectId. Emits chain-of-custody edges for the
// custody-relevant operations (MEMBER_OF, CONTAINS_PRODUCTION_ORDER) so the
// upstream graph is queryable from day one. Every PageModel and voice intent
// that mutates a CustomerProject calls THIS service — never AppDbContext.
builder.Services.AddScoped<Abs.FixedAssets.Services.Projects.ICustomerProjectService,
    Abs.FixedAssets.Services.Projects.CustomerProjectService>();

// ADR-025 D5 / Sprint 13.5 PR #3 — IProductionOrderService is the mutation
// surface for ProductionOrder (ADR-013). Five methods in v1 — Create /
// UpdateHeader / UpdateStatus / AssignToProject / UnassignFromProject.
// AssignToProjectAsync delegates to ICustomerProjectService.LinkProductionOrderAsync
// so the chain edge (CONTAINS_PRODUCTION_ORDER) and posting-mode rule stay
// in one place. JE-posting / inventory writes (IssueMaterial, Complete,
// Scrap) defer to Sprint 14 PR per the WorkOrderService PR #3.1+ precedent.
builder.Services.AddScoped<Abs.FixedAssets.Services.Production.IProductionOrderService,
    Abs.FixedAssets.Services.Production.ProductionOrderService>();

// ADR-016 §D7 + ADR-018 / Sprint 13.5 PR #5 — IProductionControlCenterService
// is the read surface backing the Production Control Center: KPI band /
// exception lanes / time-bucketed queue / activity feed / Next Up / AI
// suggestions. Plus bulk-status mutation that iterates single-row calls
// through IProductionOrderService so legal-transitions + chain emit +
// CHERRY025 control plane all still apply per row.
builder.Services.AddScoped<Abs.FixedAssets.Services.Production.IProductionControlCenterService,
    Abs.FixedAssets.Services.Production.ProductionControlCenterService>();

// Sprint 13.5 PR #5c — Routing + WorkCenter + ProductionOperation services.
// WorkCenter = dispatch unit master (N:N with Asset for live OEE rollup).
// Routing + RoutingOperation = manufacturing method master, versioned, with
// 5-time decomp (Setup/Run/Queue/Move/Wait) per SAP/Oracle convention.
// ProductionOperation = execution-time instance (snapshot from RoutingOperation
// at release). The UNIVERSAL entity that PR #5e (DowntimeEvent/ScrapEvent/
// ReworkEvent/MaterialConsumption) and PR #5g (OeeEvent) all FK to.
builder.Services.AddScoped<Abs.FixedAssets.Services.Production.IWorkCenterService,
    Abs.FixedAssets.Services.Production.WorkCenterService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Production.IRoutingService,
    Abs.FixedAssets.Services.Production.RoutingService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Production.IProductionOperationService,
    Abs.FixedAssets.Services.Production.ProductionOperationService>();

// Sprint 12.8 PR #2 — IBackwardSchedulingService (STUB of the future Sprint 14
// engine). Stamps PlannedStart/End on a parent ProductionOrder's children +
// each child's ProductionOperations, walking BACKWARD from the parent's
// ScheduledEnd. No calendar, no capacity constraints, no resource leveling —
// the contract is documented in IBackwardSchedulingService.cs and the Sprint
// 14 real engine swaps the impl without changing the interface. PR #5c
// (ABS scenario seeder) is the first caller; PR #5d (/Production/Walkthrough)
// renders the stamped dates.
builder.Services.AddScoped<Abs.FixedAssets.Services.Production.BackwardScheduling.IBackwardSchedulingService,
    Abs.FixedAssets.Services.Production.BackwardScheduling.BackwardSchedulingService>();

// Sprint 12.7 PR #5 — ICfoMotionDemoSeeder. Idempotent demo-data seeder
// for the CFO motion. Pushes /Controller KPI band (Cash / AP / POs / WIP)
// to demo-believable numbers on the SEEDED PLACEHOLDER tenant (looked up
// by CompanyCode='PWH-CAN'). NO real customer names anywhere in the
// implementation, comments, demo prefixes, or display strings. Triggered
// from /Admin/SeedCfoMotionDemo. Lock 14 — runs on dev only; Republish-
// with-Copy syncs to prod at end of sprint window.
builder.Services.AddScoped<Abs.FixedAssets.Services.Seeding.ICfoMotionDemoSeeder,
    Abs.FixedAssets.Services.Seeding.CfoMotionDemoSeeder>();

// Sprint 12.8 PR #5c.1 — ICooMotionDemoSeeder. The production-side sibling
// to ICfoMotionDemoSeeder. Idempotent demo-data seeder that creates the
// 10-level precision-machining scenario (Locations + CustomerProject +
// BOMs + Routings + ProductionOrders + ProductionOperations via Release +
// status mix + backward-schedule) on the seeded PLACEHOLDER tenant
// (CompanyCode='PWH-CAN'). NO real customer / OEM / program references
// anywhere. Triggered from /Admin/SeedCooMotionDemo. Lock 14 — dev only.
builder.Services.AddScoped<Abs.FixedAssets.Services.Seeding.ICooMotionDemoSeeder,
    Abs.FixedAssets.Services.Seeding.CooMotionDemoSeeder>();

// B6 Foundation Sprint PR-FS-1 (2026-05-26) — IItemGroupResolver.
// Read-side lookup helper that maps (ItemType → default ItemGroup Code → Id)
// against the SYSTEM ItemGroups seeded by PRA-7. Used by future backfill
// seeders + bulk-import flows that don't supply ItemGroupId explicitly.
// Pure read; no tenant context needed (system ItemGroups are CompanyId=NULL).
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemGroupResolver,
    Abs.FixedAssets.Services.Items.ItemGroupResolver>();

// B6 Foundation Sprint PR-FS-1.5 (2026-05-26) — IItemGroupBackfillSeeder.
// One-shot bulk-classification of pre-PR-FS-1 Items via IItemGroupResolver
// convention map. Idempotent (only touches Items where ItemGroupId IS NULL).
// Triggered from /Admin/BackfillItemGroups. Lock 14 — runs on dev only.
// HOTFIX PR-FS-1.5.1 (2026-05-26): seeder now supports Reclassify mode
// alongside FillNullsOnly for the post-Source-flip sweep.
builder.Services.AddScoped<Abs.FixedAssets.Services.Seeding.IItemGroupBackfillSeeder,
    Abs.FixedAssets.Services.Seeding.ItemGroupBackfillSeeder>();

// B6 Foundation Sprint PR-FS-7 (2026-05-26) — IItemMasterReader.
// Read-only projection of the 18-column expansion fields for admin probes.
// Service-only pattern (CHERRY025-compliant) — page models can use this
// instead of injecting AppDbContext directly.
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemMasterReader,
    Abs.FixedAssets.Services.Items.ItemMasterReader>();

// Sprint 14.1 PR-1 (2026-05-26) — IPoSnapshotService.
// Per-PO frozen BOM snapshot service. Captures Item revision + every
// MaterialStructureLine into ProductionMaterialStructures at PRO release so
// the cost engine, MES material-issue, AS9100 §8.3 traceability, and
// ECR-ECO impact analysis all read from a deterministic per-PO snapshot
// instead of the live engineering view. Idempotent; admin-only Clear path.
builder.Services.AddScoped<Abs.FixedAssets.Services.Production.IPoSnapshotService,
    Abs.FixedAssets.Services.Production.PoSnapshotService>();

// Sprint 14.2 PR-1 (2026-05-26 evening) — IDocumentService.
// DMS substrate. Document = controlled engineering artifact with lifecycle.
// DocumentVersion = monotonic per-revision satellite with content hash +
// supersession chain + atomic release. ItemDocumentLink = M:N between
// Items and Documents by purpose (BillOfDrawing / Specification / etc.).
// Substrate for drawing-pinning in ProductionMaterialStructure (Sprint
// 14.3) and Arena PLM ECR/ECO integration.
builder.Services.AddScoped<Abs.FixedAssets.Services.Engineering.IDocumentService,
    Abs.FixedAssets.Services.Engineering.DocumentService>();

// B6 Foundation Sprint PR-FS-6 (2026-05-26) — ICustomerItemXrefService.
// Customer-PN ↔ Item bidirectional translation (SAP CMIR equivalent).
// Used at SO ingestion (customer's PN → our Item) and ship/invoice rendering
// (our Item → customer's PN). Multi-OEM scoped; effective-dated with
// supersession audit trail.
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.ICustomerItemXrefService,
    Abs.FixedAssets.Services.Items.CustomerItemXrefService>();

// B6 Foundation Sprint PR-FS-5 (2026-05-26) — IItemSourcingRuleService.
// Multi-source Approved Vendor List + priority + approval state machine +
// customer-mandated AS9100 §8.4.1 flagging. SAP S/4 Source List equivalent.
// Drives MRP + Make-or-Buy decision input (Theme B7) + material-issue
// routing (Theme B8 PR-PO-3).
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemSourcingRuleService,
    Abs.FixedAssets.Services.Items.ItemSourcingRuleService>();

// B6 Foundation Sprint PR-FS-4 (2026-05-26) — ICostLayerService.
// FIFO/LIFO/Average inventory valuation layers. SAP MM "stock with values"
// equivalent. Each material receipt creates an immutable layer at receipt
// cost; issues consume per the configured CostMethod. Foundation for
// Sprint 14.4 cost-rollup engine + purchase-price variance posting (PRA-7).
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.ICostLayerService,
    Abs.FixedAssets.Services.Items.CostLayerService>();

// B6 Foundation Sprint PR-FS-3 (2026-05-26) — IItemStandardCostService.
// SAP Cost Component Split equivalent. Effective-dated per-Item/per-Site cost
// element rows. Read API for cascade (per-Site → Item-level → $0) + write API
// that closes prior row + inserts new on each amount change. Foundation for
// Sprint 14.4 cost-rollup engine + CFO cost-composition reporting.
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemStandardCostService,
    Abs.FixedAssets.Services.Items.ItemStandardCostService>();

// B6 Foundation Sprint PR-FS-2 (2026-05-26) — IItemSiteResolver.
// Read-side cascade ItemSite → Item for per-Site override resolution.
// SAP MARC equivalent. Used by MRP, cost rollup, procurement defaulting,
// warehouse receive-time defaulting.
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemSiteResolver,
    Abs.FixedAssets.Services.Items.ItemSiteResolver>();

// B6 Foundation Sprint PR-FS-1.5.1 (2026-05-26) — IItemSourceBackfillSeeder.
// One-time data fix that flips Items with Source=Internal AND ItemGroupId=FG
// (the legacy-bug fingerprint from PR-FS-1.5) to Source=ExternalERP, so the
// subsequent ItemGroupBackfill Reclassify sweep moves them FG → RAW per the
// new Source-aware convention. Bounded by the legacy-bug signal so it can
// safely run on any DB. Triggered from /Admin/BackfillItemSource. Lock 14.
builder.Services.AddScoped<Abs.FixedAssets.Services.Seeding.IItemSourceBackfillSeeder,
    Abs.FixedAssets.Services.Seeding.ItemSourceBackfillSeeder>();

// Sprint 13.5 PR #5d — ILaborService backs the Operator Workbench clock-in/out
// event writer (distinct from LaborConfig.cs lookup tables: LaborType / Craft /
// Skill / LaborRate are catalog config; LaborService writes execution-time
// LaborEntry rows the operator creates by hitting "Clock In" on the Workbench).
builder.Services.AddScoped<Abs.FixedAssets.Services.Production.ILaborService,
    Abs.FixedAssets.Services.Production.LaborService>();

// Sprint 13.5 PRA-4 — IUomService backs the unified UOM master.
// Replaces the two parallel enums (Models.Item.UnitOfMeasure inventory +
// Models.Telemetry.UnitOfMeasure sensors) with one master table per the
// master-files-baseline-2026-05-24 memo. Affine factor + offset to category
// base unit handles in-category conversion arithmetically; UomConversion
// table carries per-item + cross-category overrides only.
builder.Services.AddScoped<Abs.FixedAssets.Services.Masters.IUomService,
    Abs.FixedAssets.Services.Masters.UomService>();

// ADR-022 / Sprint 12D PR #2 — chain-of-custody graph (virtual Apache AGE).
// Two regular Postgres tables (ChainNodes + ChainEdges) traversed via
// recursive CTEs, rendered via cytoscape.js. Q3 2026 swaps the storage
// backend for real Apache AGE behind the same interface.
builder.Services.AddScoped<Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService,
    Abs.FixedAssets.Services.ChainOfCustody.ChainOfCustodyService>();

// ADR-001 / S1-1: GR/IR accrual + inventory movement on goods receipt.
builder.Services.AddScoped<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Receiving.IReceivingPostingService>(
    sp => sp.GetRequiredService<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>());
// ADR-025 D2 (Sprint 12.9 PR #2) — also resolvable via the IPostingService<T>
// generic contract so future Sprint 13/14 Control Center services + the voice
// MCP tool layer can depend on the typed receipt API.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Posting.IPostingService<Abs.FixedAssets.Services.Posting.ReceiveGoodsRequest>>(
    sp => sp.GetRequiredService<Abs.FixedAssets.Services.Receiving.ReceivingPostingService>());

// ADR-002 / S1-5: AP posting (approve / payment / void) with three-way
// match gate via InvoiceMatchingService.
builder.Services.AddScoped<Abs.FixedAssets.Services.AccountsPayable.ApPostingService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.AccountsPayable.IApPostingService>(
    sp => sp.GetRequiredService<Abs.FixedAssets.Services.AccountsPayable.ApPostingService>());
// ADR-025 D2 (Sprint 12.9 PR #2) — also resolvable via IPostingService<T>.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Posting.IPostingService<Abs.FixedAssets.Services.Posting.ApInvoiceApprovalRequest>>(
    sp => sp.GetRequiredService<Abs.FixedAssets.Services.AccountsPayable.ApPostingService>());
builder.Services.AddScoped<IPeriodGuard, PeriodGuard>();
builder.Services.AddScoped<IFiscalCalendarService, FiscalCalendarService>();
// MP #112: Period Close Orchestration — sequenced one-click month-end close
// (preflight + depreciation + lock + audited snapshot). Sits above PeriodGuard +
// JournalGenerator + AuditService; doesn't replace them.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Finance.IPeriodCloseOrchestrationService,
    Abs.FixedAssets.Services.Finance.PeriodCloseOrchestrationService>();
// Sprint 2 PR #115: Approval Hierarchy + SoD — resolves doc to workflow
// by (Type, amount), enforces creator-cannot-approve + role gate +
// N-of-M-from-role, persists every decision as an ApprovalAction row.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Approvals.IApprovalService,
    Abs.FixedAssets.Services.Approvals.ApprovalService>();
// Sprint 2 PR #117.1: Plant Floor Live View — real sensor history table.
// AssetSensorService writes every reading to AssetSensorReadings AND
// updates the denormalized Asset.Current* cache columns atomically.
// AssetHealthService computes HealthScore from real signals (sensor
// breaches + corrective WO freq + overdue WO count) — no more random.
// IndustrialAssetSeeder fixes brand+type pairings and seeds 30 days of
// readings on first hit.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Reliability.IAssetSensorService,
    Abs.FixedAssets.Services.Reliability.AssetSensorService>();
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Reliability.IAssetHealthService,
    Abs.FixedAssets.Services.Reliability.AssetHealthService>();
// Sprint 2 PR #118.2 — Telemetry write path (ADR-011 industrial sensor
// architecture). SensorIngestService is the single entry point for every
// sensor reading; it writes the SensorEvent hypertable, upserts
// AssetSensorLatest, and computes IsOutOfSpec / Tone from SensorProfile
// thresholds in one transaction. SensorAlarmService + SensorSnapshotService
// implementations land in PR #118.3 / #118.6.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Telemetry.ISensorIngestService,
    Abs.FixedAssets.Services.Telemetry.SensorIngestService>();
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Seeding.IIndustrialAssetSeeder,
    Abs.FixedAssets.Services.Seeding.IndustrialAssetSeeder>();
// Sprint 2 PR #117.2: Equipment Catalog seeder. Runs before IndustrialAssetSeeder
// so EquipmentClass + EquipmentModel + SensorProfile tables are populated; the
// asset seeder then reads from them instead of hardcoded C# arrays.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Seeding.IEquipmentCatalogSeeder,
    Abs.FixedAssets.Services.Seeding.EquipmentCatalogSeeder>();
// Sprint 2 PR #118.5: 30-day telemetry historical backfill (ADR-011 Layer 1).
// Runs ONCE after IndustrialAssetSeeder. Uses Npgsql Binary COPY for bulk
// insert (~2M rows in 10-30 seconds). Idempotent — skips if SensorEvents
// already has > 1000 rows.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Seeding.ITelemetryHistoricalBackfillSeeder,
    Abs.FixedAssets.Services.Seeding.TelemetryHistoricalBackfillSeeder>();

// Sprint 3 PR #119.2 (ADR-012 v0.2): Unified WorkOrder configuration backbone.
// WorkOrderFieldVisibility is the SAP-OIAN-pattern field-selection table that
// drives per-classification UX with zero code branching in the renderer.
// Cache is Singleton (process-wide); service is Scoped (so it can inject
// AppDbContext). Seeder runs ONCE on startup with ~75 global default rows
// covering Maintenance/Quality/Engineering/HSE/CIP.
builder.Services.AddSingleton<Abs.FixedAssets.Services.WorkOrders.WorkOrderFieldVisibilityCache>();
builder.Services.AddScoped<
    Abs.FixedAssets.Services.WorkOrders.IWorkOrderFieldVisibilityService,
    Abs.FixedAssets.Services.WorkOrders.WorkOrderFieldVisibilityService>();
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Seeding.IWorkOrderFieldVisibilitySeeder,
    Abs.FixedAssets.Services.Seeding.WorkOrderFieldVisibilitySeeder>();

// Sprint 3 PR #119.3 (ADR-012 v0.2): Per-classification state engine.
// Same Singleton-cache + Scoped-service split. Guard plugins ship in
// Phase D satellites and register themselves as keyed IWorkOrderTransitionGuard
// services (DI key = WorkOrderStatusTransition.GuardServiceName, e.g.
// "CipCapitalizationGuard", "PssrCompletionGuard", "QaEffectivenessGuard").
builder.Services.AddSingleton<Abs.FixedAssets.Services.WorkOrders.WorkOrderStatusCache>();
builder.Services.AddScoped<
    Abs.FixedAssets.Services.WorkOrders.IWorkOrderStatusEngine,
    Abs.FixedAssets.Services.WorkOrders.WorkOrderStatusEngine>();
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Seeding.IWorkOrderStatusSeeder,
    Abs.FixedAssets.Services.Seeding.WorkOrderStatusSeeder>();

// Sprint 3 PR #119.4 (ADR-012 v0.2): Polymorphic approval chain.
// Registered BEFORE the status engine in DI order doesn't matter (the
// status engine's constructor dependency on IWorkOrderApprovalService
// is resolved lazily by DI). No cache — approvals change on user
// action and we don't want stale gate decisions.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.WorkOrders.IWorkOrderApprovalService,
    Abs.FixedAssets.Services.WorkOrders.WorkOrderApprovalService>();

// Sprint 3 PR #119.5 (ADR-012 v0.2): Atomic WO-number generator.
// SAP NRIV pattern. SELECT FOR UPDATE inside a short transaction
// guarantees no two concurrent WO creates ever get the same number.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.WorkOrders.INumberSequenceService,
    Abs.FixedAssets.Services.WorkOrders.NumberSequenceService>();
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Seeding.INumberSequenceSeeder,
    Abs.FixedAssets.Services.Seeding.NumberSequenceSeeder>();
builder.Services.AddScoped<DepreciationBackfillService>();
// PR #102 (B-10): Capital Improvement → JE service. Wired into
// Pages/Assets/Improve and Pages/WorkOrders/Details::Capitalize.
builder.Services.AddScoped<ICapitalImprovementPostingService, CapitalImprovementPostingService>();
builder.Services.AddScoped<HistoricJournalBackfillService>();
builder.Services.AddScoped<CcaService>();
builder.Services.AddScoped<CcaBackfillService>();
// Sprint 1 fixture seeder service. Originally invokable from /Admin/Sprint1Fixture;
// that page was deleted as an orphan in PR #116a but the service stays registered
// so it can be wired back in from a future Admin v2 surface (Sprint 2 PR #118) if
// needed. Currently has no callers — safe.
builder.Services.AddScoped<Abs.FixedAssets.Services.Seeding.Sprint1FixtureSeeder>();
// Sprint 1 PR #110: per-asset reliability metrics service.
builder.Services.AddScoped<Abs.FixedAssets.Services.Reliability.ReliabilityMetricsService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<UsTaxService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<MaintenanceService>();
builder.Services.AddScoped<CipService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Cip.CipCostService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Cip.CipAutoCostPostingService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Cip.CipCapitalizationService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Cip.CipTraceQueryService>();
builder.Services.AddScoped<BulkOperationsService>();
builder.Services.AddScoped<AiAssistantService>();
builder.Services.AddScoped<ReportBuilderService>();
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<TemplateService>();
builder.Services.AddScoped<MasterDataImportService>();
// Sprint 13.5 PR #337 — /Admin/AssetImport bulk Excel upload service.
builder.Services.AddScoped<Abs.FixedAssets.Services.AssetImport.IAssetImportService,
                          Abs.FixedAssets.Services.AssetImport.AssetImportService>();
// Sprint 13.5 PR #338 — Quality / FAI service (AS9102 First Article Inspection).
builder.Services.AddScoped<Abs.FixedAssets.Services.Quality.IFaiService,
                          Abs.FixedAssets.Services.Quality.FaiService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<InvoiceMatchingService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<Abs.FixedAssets.Services.Lookups.ILookupService, Abs.FixedAssets.Services.Lookups.LookupService>();
builder.Services.AddScoped<IBarcodeService, BarcodeService>();
builder.Services.AddScoped<IItemStockingService, ItemStockingService>();
builder.Services.AddScoped<IModuleGuardService, ModuleGuardService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IMasterDataBootstrapService, MasterDataBootstrapService>();
builder.Services.AddScoped<ISeedGuardService, SeedGuardService>();
builder.Services.AddScoped<ISmartAssistService, SmartAssistService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Webhooks.IOutboxWriter, Abs.FixedAssets.Services.Webhooks.OutboxWriter>();
// Strongly-typed event registry — discovered once at startup by scanning
// the executing assembly for [DomainEvent]-decorated records. Singleton
// because the registry is immutable after construction.
builder.Services.AddSingleton(
    Abs.FixedAssets.Services.Webhooks.Events.DomainEventRegistry.FromAssembly(
        typeof(Abs.FixedAssets.Services.Webhooks.Events.IDomainEvent).Assembly));
builder.Services.AddScoped<Abs.FixedAssets.Services.Maintenance.IWorkRequestConversionService, Abs.FixedAssets.Services.Maintenance.WorkRequestConversionService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Maintenance.ICloseoutService, Abs.FixedAssets.Services.Maintenance.CloseoutService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Maintenance.IWorkOrderOriginService, Abs.FixedAssets.Services.Maintenance.WorkOrderOriginService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Maintenance.IPMSchedulerService, Abs.FixedAssets.Services.Maintenance.PMSchedulerService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Revisions.IPMTemplateRevisionService, Abs.FixedAssets.Services.Revisions.PMTemplateRevisionService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemRevisionService, Abs.FixedAssets.Services.Items.ItemRevisionService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemCrossReferenceService, Abs.FixedAssets.Services.Items.ItemCrossReferenceService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemSourcingService, Abs.FixedAssets.Services.Items.ItemSourcingService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemAlternateService, Abs.FixedAssets.Services.Items.ItemAlternateService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IItemSupersessionService, Abs.FixedAssets.Services.Items.ItemSupersessionService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.IItemImageService, Abs.FixedAssets.Services.ItemImageService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.ICatalogMetadataEnrichmentService, Abs.FixedAssets.Services.CatalogMetadataEnrichmentService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IBuyabilityScoreService, Abs.FixedAssets.Services.Items.BuyabilityScoreService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IEffectiveProcurementService, Abs.FixedAssets.Services.Items.EffectiveProcurementService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Items.IPreferredVendorCatalogResolver, Abs.FixedAssets.Services.Items.PreferredVendorCatalogResolver>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Testing.ISmokeTestDataFactory, Abs.FixedAssets.Services.Testing.SmokeTestDataFactory>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Testing.ISmokeTestRunner, Abs.FixedAssets.Services.Testing.SmokeTestRunner>();
builder.Services.AddSingleton<Abs.FixedAssets.Services.Testing.ISmokeTestRunQueue, Abs.FixedAssets.Services.Testing.SmokeTestRunQueue>();
builder.Services.AddSingleton<Abs.FixedAssets.Services.Testing.ISmokeTestRunStore, Abs.FixedAssets.Services.Testing.SmokeTestRunStore>();
builder.Services.AddHostedService<Abs.FixedAssets.Services.Testing.SmokeTestBackgroundService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Seeding.ISeedPackExecutor, Abs.FixedAssets.Services.Seeding.SeedPackExecutor>();
builder.Services.AddSeedingServices();
builder.Services.AddHostedService<Abs.FixedAssets.Services.Webhooks.WebhookDispatcherHostedService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Integrations.IInboundWebhookService, Abs.FixedAssets.Services.Integrations.InboundWebhookService>();
builder.Services.AddScoped<Abs.FixedAssets.Services.Integrations.IIntegrationMappingService, Abs.FixedAssets.Services.Integrations.IntegrationMappingService>();
builder.Services.AddHostedService<Abs.FixedAssets.Services.Integrations.InboundEventProcessorHostedService>();
builder.Services.AddControllers();

// Health checks
//   /_live   -> liveness (process up; canonical path; GFE-safe)
//   /healthz -> liveness alias (works in dev; intercepted by GFE in prod)
//   /readyz  -> readiness (DB reachable + SkiaSharp lib present; tag "ready")
builder.Services.AddHealthChecks()
    .AddCheck<Abs.FixedAssets.Services.Health.DbHealthCheck>("db", tags: new[] { "ready" })
    .AddCheck<Abs.FixedAssets.Services.Health.SkiaHealthCheck>("skia", tags: new[] { "ready" });

// Response compression — Brotli + Gzip for HTML and JSON. Saves
// ~70-80% bandwidth on Razor pages and large API payloads.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "application/json", "text/html; charset=utf-8" });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o =>
    o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o =>
    o.Level = System.IO.Compression.CompressionLevel.Fastest);

// Phase 4 — distributed login rate limiter. Replaces the in-process
// PartitionedRateLimiter from Phase 3 (which only counted within one
// container, so an attacker fanning across Replit Autoscale instances
// trivially bypassed it). PostgresLoginRateLimiter performs an atomic
// upsert against the "RateLimitCounters" table so the 100/min budget
// per (IP, Username) is enforced cluster-wide. Fails open on DB outage.
builder.Services.AddSingleton<
    Abs.FixedAssets.Services.RateLimiting.IDistributedLoginRateLimiter,
    Abs.FixedAssets.Services.RateLimiting.PostgresLoginRateLimiter>();
builder.Services.AddHostedService<Abs.FixedAssets.Services.RateLimiting.RateLimitCounterCleanupService>();

// Phase 4 — OpenTelemetry traces + metrics. Service identity, ASP.NET
// Core / HttpClient / EF Core instrumentation, and runtime metrics.
// OTel: ASP.NET Core + HttpClient + EF Core traces; ASP.NET Core + HttpClient
// + Runtime + Process metrics + EF Core meter source. OTLP/HTTP exporter only
// registered when OTEL_EXPORTER_OTLP_ENDPOINT is set.
{
    var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    var otelEnabled = !string.IsNullOrWhiteSpace(otlpEndpoint);
    var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(rb => rb
            .AddService(serviceName: "cherryai-eam", serviceVersion: serviceVersion)
            .AddAttributes(new[]
            {
                new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName),
            }))
        .WithTracing(t =>
        {
            t.AddAspNetCoreInstrumentation(opts =>
            {
                opts.Filter = httpCtx =>
                {
                    var p = httpCtx.Request.Path.Value ?? "";
                    return !p.StartsWith("/_live", StringComparison.OrdinalIgnoreCase)
                        && !p.StartsWith("/healthz", StringComparison.OrdinalIgnoreCase)
                        && !p.StartsWith("/readyz", StringComparison.OrdinalIgnoreCase);
                };
                // Defense in depth: even though OTel ASP.NET Core 1.10.x does
                // not capture request/response headers by default, we eagerly
                // null out the well-known sensitive header tag keys so that a
                // future config change (or a custom enricher upstream) cannot
                // accidentally export Cookie / Authorization / Set-Cookie.
                opts.EnrichWithHttpRequest = (activity, _) =>
                {
                    activity.SetTag("http.request.header.cookie", null);
                    activity.SetTag("http.request.header.authorization", null);
                };
                opts.EnrichWithHttpResponse = (activity, _) =>
                {
                    activity.SetTag("http.response.header.set_cookie", null);
                };
            });
            t.AddHttpClientInstrumentation(opts =>
            {
                opts.EnrichWithHttpRequestMessage = (activity, _) =>
                {
                    activity.SetTag("http.request.header.cookie", null);
                    activity.SetTag("http.request.header.authorization", null);
                };
                opts.EnrichWithHttpResponseMessage = (activity, _) =>
                {
                    activity.SetTag("http.response.header.set_cookie", null);
                };
            });
            t.AddEntityFrameworkCoreInstrumentation();
            if (otelEnabled)
            {
                t.AddOtlpExporter(o => o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf);
            }
        })
        .WithMetrics(m =>
        {
            m.AddAspNetCoreInstrumentation();
            m.AddHttpClientInstrumentation();
            m.AddRuntimeInstrumentation();
            m.AddProcessInstrumentation();
            // EF Core 9 emits its own metrics under this Meter source.
            m.AddMeter("Microsoft.EntityFrameworkCore");
            if (otelEnabled)
            {
                m.AddOtlpExporter(o => o.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf);
            }
        });

    Console.WriteLine($"[Startup] OpenTelemetry registered (otlp_exporter={(otelEnabled ? "ENABLED -> " + otlpEndpoint : "disabled")})");
}

// Authentication disabled for development - using fallback policy that allows anonymous
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        // Task #17 — lock auth cookie posture so a future refactor cannot
        // silently weaken it. HttpOnly blocks XSS reads; SameSite=Lax keeps
        // top-level GET nav working but blocks cross-site POST CSRF on the
        // session cookie; SecurePolicy.Always in production forces TLS so the
        // cookie can never be sent over plaintext (the Replit edge already
        // terminates HTTPS, so the proxied http hop inside the container is
        // irrelevant — UseForwardedHeaders restores X-Forwarded-Proto).
        // In Development we use SameAsRequest so http://localhost:5000 in
        // the dev workflow can still set the cookie for Playwright tests.
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsProduction()
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Authorization with proper role-based policies
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = null; // Auth enforced via MapRazorPages().RequireAuthorization(); controllers remain open
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AccountantOrAdmin", policy => policy.RequireRole("Admin", "Accountant"));
    options.AddPolicy("AllUsers", policy => policy.RequireAuthenticatedUser());

    // ADR-014 D5 — Phase F resource-based policies. Voice-AI never
    // gets its own identity; AI executes as the invoking user and
    // is gated by these policies via IAuthorizationService.AuthorizeAsync.
    Abs.FixedAssets.Authorization.AuthorizationPolicies.Configure(options);
});

// ADR-014 / Sprint 4 PR #1 — Voice-ready infrastructure DI registration.
// IdempotencyMediator dedups voice + UI mutations (Stripe pattern).
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Infrastructure.IIdempotencyMediator,
    Abs.FixedAssets.Services.Infrastructure.IdempotencyMediator>();

// Sprint 4 Phase F Wave 1 — Admin services.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Admin.IRegulatoryProfileService,
    Abs.FixedAssets.Services.Admin.RegulatoryProfileService>();
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Admin.IMaterialMasterService,
    Abs.FixedAssets.Services.Admin.MaterialMasterService>();
// Sprint 4 Phase F Wave 1 PR #4 — Vendor edit voice-ready service.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Admin.IVendorService,
    Abs.FixedAssets.Services.Admin.VendorService>();
// Sprint 4 Phase F Wave 1 PR #5 — StockReceipt admin service.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Admin.IStockReceiptService,
    Abs.FixedAssets.Services.Admin.StockReceiptService>();
// ADR-015 Migration PR #3 — JSON Schema validator for receipt Attributes.
// Scoped so it picks up the request-scoped IMemoryCache (singleton under
// the hood). Used by StockReceiptService for service-layer validation
// and by the Edit page directly for per-field ModelState placement.
builder.Services.AddScoped<Abs.FixedAssets.Services.Admin.ReceiptAttributesValidator>();
// Sprint 11 PR #3 (ADR-016 D7) — Receiving Control Center service.
// IReceivingControlCenterService is consumed by both the /Receiving/ControlCenter
// page model (PR #5) AND ReceiptVoiceTools (PR #4 — depends on this registration).
// PR #5.1 hotfix: this line was dropped during the PR #4 git-reset recovery
// and the app's runtime DI validator caught the missing registration at
// Build() time, blocking cold start.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Receiving.IReceivingControlCenterService,
    Abs.FixedAssets.Services.Receiving.ReceivingControlCenterService>();

// ADR-015 D10 + ADR-016 D8 — Receipt voice-tool catalog.
// Sprint 11 PR #4 promotes the stub to the production implementation
// (ReceiptVoiceTools) backed by AppDbContext + IReceivingControlCenterService.
// Three of the ten tools (MatchOrphanReceipt, ExplainException,
// OcrParseMillCert) ship with deterministic bodies; Sprint 5 swaps with LLM/OCR.
// ReceiptVoiceToolsStub stays in the codebase as a test fixture.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Voice.IReceiptVoiceTools,
    Abs.FixedAssets.Services.Voice.ReceiptVoiceTools>();

// Sprint 12C / ADR-020 §D2 + ADR-021 — Voyage AI embedding client +
// embedding queue + hosted worker.
//
// Pipeline shape (ADR-021 §D3 — Mode B / external worker):
//   - Producer (admin endpoint today; entity services in PR #2): call
//     IEmbeddingBackfillService.EnqueueAsync after a save.
//   - Consumer (EmbeddingWorker BackgroundService): polls every 5s,
//     batches 32 rows, calls Voyage, upserts Embeddings, deletes queue.
//
// VOYAGE_API_KEY env var must be set in Replit Secrets for the worker
// to make API calls. Until it's set, the worker logs warnings + leaves
// rows in the queue with Attempts++ (recoverable as soon as the key
// is configured).
builder.Services.AddHttpClient<
    Abs.FixedAssets.Services.Voice.IVoyageClient,
    Abs.FixedAssets.Services.Voice.VoyageClient>(c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Voice.IEmbeddingBackfillService,
    Abs.FixedAssets.Services.Voice.EmbeddingBackfillService>();
builder.Services.AddHostedService<Abs.FixedAssets.Services.Voice.EmbeddingWorker>();

// Sprint 12C PR #3 / ADR-021 — Hybrid Intent Router + prototype seeder.
//
// HybridIntentRouter is the new injection point for voice intent
// classification. VoiceInvokeEndpoint depends on IHybridIntentRouter
// (not the static IntentClassifier) per ADR-025.
//
// IntentEmbeddingsBootstrap is an IHostedService that runs once on
// startup. It enqueues IntentPrototypes.All into the existing
// PendingEmbeddings queue; the EmbeddingWorker drains them in <5s.
// Idempotent — re-enqueues with the same ContentHash no-op.
builder.Services.AddScoped<
    Abs.FixedAssets.Services.Voice.IHybridIntentRouter,
    Abs.FixedAssets.Services.Voice.HybridIntentRouter>();
builder.Services.AddHostedService<Abs.FixedAssets.Services.Voice.IntentEmbeddingsBootstrap>();

var app = builder.Build();

// Apply EF Core migrations on startup (all environments).
//
// Lock 11 (2026-05-25): EnsureCreated() was removed because it silently created
// missing tables without tracking, masking model-snapshot drift for weeks. With
// MigrateAsync running everywhere, snapshot drift fails LOUDLY at Publish time
// via PendingModelChangesWarning — forcing immediate fix. Combined with
// Lock 12 (raw-SQL migrations must update the snapshot) and the
// snapshot-drift-check CI gate, this closes the entire drift failure mode.
//
// Disabling auto-migrate: set AUTO_MIGRATE_DISABLE=true (escape hatch only —
// production should always migrate).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var disable = Environment.GetEnvironmentVariable("AUTO_MIGRATE_DISABLE");

    if (disable?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
    {
        Console.WriteLine("[Startup] AUTO_MIGRATE_DISABLE=true — skipping migrations (escape hatch active)");
    }
    else
    {
        Console.WriteLine("[Startup] Applying database migrations...");
        await db.Database.MigrateAsync();
        Console.WriteLine("[Startup] Database migrations applied successfully");
    }
    
    // Serialize startup seeding across concurrent app instances. If two
    // processes start at the same time (e.g., during a Replit rolling
    // restart or a multi-instance deploy), only one runs the seed work;
    // the other skips it and proceeds straight to listening. EF Core
    // already serializes migrations via its own __EFMigrationsHistory
    // locking, so the lock only needs to cover the seed block.
    var seedGuardForLock = scope.ServiceProvider.GetRequiredService<ISeedGuardService>();
    var seedLockAcquired = await seedGuardForLock.TryAcquireSeedLockAsync(db);
    if (!seedLockAcquired)
    {
        Console.WriteLine("[Startup] Another instance holds the seed advisory lock; skipping seed work this startup");
    }
    else
    {
        Console.WriteLine("[Startup] Seed advisory lock acquired");
    }

    try
    {
        if (!seedLockAcquired)
        {
            // Skip the entire seed block; another instance is doing the work.
        }
        else
        {

    // Seed default depreciation books if they don't exist
    if (!db.Books.Any())
    {
        db.Books.AddRange(
            new Book { Code = "GAAP", Name = "GAAP Book", Method = DepreciationMethod.StraightLine, Convention = DepreciationConvention.FullMonth, UsefulLifeOverrideMonths = 120 },
            new Book { Code = "TAX", Name = "Tax Book", Method = DepreciationMethod.DoubleDecliningBalance, Convention = DepreciationConvention.HalfYear, UsefulLifeOverrideMonths = 84 }
        );
        db.SaveChanges();
        Console.WriteLine("[Startup] Seeded default GAAP and TAX books");
    }
    
    // Seed CCA classes
    await CcaClassSeeder.SeedCcaClassesAsync(db);

    // Seed lookup reference data from JSON files (idempotent)
    try
    {
        await Abs.FixedAssets.Services.Seeding.Pipelines.LookupDirectSeeder.SeedAsync(db);
        Console.WriteLine("[Startup] Lookup reference data seeded successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] WARNING: Lookup seed failed: {ex.Message}");
    }

    // Seed default users
    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.SeedDefaultUserAsync();
    
    // Seed comprehensive demo data (companies, vendors, locations, assets)
    // EXPLICIT SEEDING ONLY - never seeds automatically unless explicitly enabled
    var runSeed = Environment.GetEnvironmentVariable("RUN_SEED");
    var autoSeedOnEmpty = Environment.GetEnvironmentVariable("AUTO_SEED_ON_EMPTY");
    var isDevelopment = app.Environment.IsDevelopment();
    var hasCompanies = await db.Companies.AnyAsync();
    
    if (runSeed?.Equals("true", StringComparison.OrdinalIgnoreCase) == true && isDevelopment)
    {
        Console.WriteLine("[Startup] RUN_SEED=true detected in Development - running seed...");
        await Seed.InitializeAsync(db);
    }
    else if (runSeed?.Equals("true", StringComparison.OrdinalIgnoreCase) == true && !isDevelopment)
    {
        Console.WriteLine("[Startup] WARNING: RUN_SEED=true ignored - not in Development environment");
    }
    else if (!hasCompanies && autoSeedOnEmpty?.Equals("true", StringComparison.OrdinalIgnoreCase) == true && isDevelopment)
    {
        // Auto-seed only if explicitly enabled AND database is empty AND in Development
        Console.WriteLine("[Startup] AUTO_SEED_ON_EMPTY=true AND database empty - running seed...");
        await Seed.InitializeAsync(db);
    }
    else if (!hasCompanies)
    {
        Console.WriteLine("[Startup] DATABASE EMPTY - No companies found");
        Console.WriteLine("[Startup] To seed demo data, set RUN_SEED=true and restart");
        Console.WriteLine("[Startup] App will start with empty database - Dashboard shows initialization guide");
    }

    var guardService = scope.ServiceProvider.GetRequiredService<ISeedGuardService>();
    var demoDataEnabled = guardService.IsDemoDataEnabled();
    Console.WriteLine($"[Startup] Demo Data Mode: {(demoDataEnabled ? "ENABLED" : "DISABLED")}");
    Console.WriteLine($"[Startup] Environment Profile: {guardService.GetEnvironmentProfile()}");

    var cipService = scope.ServiceProvider.GetRequiredService<CipService>();
    try
    {
        await cipService.ReconcileAllProjectCostsAsync();
        Console.WriteLine("[Startup] CIP TotalCosts reconciliation completed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] WARNING: CIP reconciliation failed: {ex.Message}");
    }

    // Fiscal-calendar coverage: every Company gets calendar-year FYs and
    // 12 monthly FiscalPeriods covering [today - 1y, today + 2y]. Idempotent
    // — only fills the gaps. Without this, PeriodGuard rejects every JE
    // posting on a fresh DB. Closes DEF-004 from the 2026-05-08 E2E run.
    try
    {
        var calendarService = scope.ServiceProvider.GetRequiredService<IFiscalCalendarService>();
        var rows = await calendarService.EnsureCoverageForAllCompaniesAsync(DateTime.UtcNow);
        Console.WriteLine($"[Startup] Fiscal calendar coverage: {rows} new row(s) materialized");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] WARNING: Fiscal calendar coverage failed: {ex.Message}");
    }

    // PR #117.8 — Industrial sensor + storyline seed.
    //
    // Moved out of Pages/Plant/Index.cshtml.cs::OnGetAsync, which was
    // calling SeedAsync on EVERY page hit and adding 6+ seconds to the
    // Plant Index load. The seeder is idempotent (PR #117.6 simplified
    // it to a single-pass single-SaveChanges design at ~5K rows), so
    // running it once per startup is safe and keeps the demo lighting
    // up the first time a fresh DB is hit. Wrapped in try/catch so a
    // seeder failure can never block the app from coming up.
    try
    {
        var industrialSeeder = scope.ServiceProvider
            .GetRequiredService<Abs.FixedAssets.Services.Seeding.IIndustrialAssetSeeder>();
        await industrialSeeder.SeedAsync();
        Console.WriteLine("[Startup] Industrial sensor + storyline seed completed");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] WARNING: Industrial seeder failed: {ex.Message}");
    }

    // PR #118.5 — Telemetry historical backfill (ADR-011 Layer 1).
    //
    // After the legacy IndustrialAssetSeeder populates the old
    // AssetSensorReadings table for backward-compat, this seeder
    // populates the NEW substrate (SensorEvents hypertable + the
    // AssetSensorLatest read cache). ~30 days at 15-min resolution,
    // plus 7 days at 1-min for the three storyline assets. Patterns:
    // daily cycle, weekend dip, noise, occasional out-of-spec spikes,
    // storyline rising trends.
    //
    // Idempotent — skips if SensorEvents already has > 1000 rows so
    // subsequent restarts don't re-seed. Wrapped in try/catch so a
    // seeder failure can never block app startup.
    try
    {
        var backfillSeeder = scope.ServiceProvider
            .GetRequiredService<Abs.FixedAssets.Services.Seeding.ITelemetryHistoricalBackfillSeeder>();
        var rows = await backfillSeeder.SeedAsync();
        Console.WriteLine($"[Startup] Telemetry historical backfill: {rows} SensorEvent rows seeded");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] WARNING: Telemetry historical backfill failed: {ex.Message}");
    }

    // PR #119.2 — WorkOrderFieldVisibility seeder (ADR-012 v0.2 config backbone).
    //
    // Seeds ~75 global default rows covering Maintenance/Quality/Engineering/
    // HSE/CIP × the high-value header fields. Idempotent — skips if any
    // global (TenantId IS NULL) row already exists. Per-tenant overrides
    // ship via the admin UI (Sprint 4), never via this seeder.
    //
    // Wrapped in try/catch so a seeder failure can never block app startup.
    try
    {
        var fieldVisSeeder = scope.ServiceProvider
            .GetRequiredService<Abs.FixedAssets.Services.Seeding.IWorkOrderFieldVisibilitySeeder>();
        var rows = await fieldVisSeeder.SeedAsync();
        Console.WriteLine($"[Startup] WorkOrderFieldVisibility: {rows} global default rows seeded");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] WARNING: WorkOrderFieldVisibility seeder failed: {ex.Message}");
    }

    // PR #119.3 — WorkOrderStatus seeder (state-engine config).
    // Seeds StatusProfile + StatusLabel + StatusTransition rows for the
    // five unified-WorkOrder classifications. Idempotent — bails if any
    // profile row exists. Wrapped in try/catch.
    try
    {
        var statusSeeder = scope.ServiceProvider
            .GetRequiredService<Abs.FixedAssets.Services.Seeding.IWorkOrderStatusSeeder>();
        var rows = await statusSeeder.SeedAsync();
        Console.WriteLine($"[Startup] WorkOrderStatus engine: {rows} config rows seeded");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] WARNING: WorkOrderStatus seeder failed: {ex.Message}");
    }

    // PR #119.5 — NumberSequence seeder (atomic WO-number generator).
    // Primes one global row per (Classification, current year) so the
    // first WO of the year doesn't pay the auto-create cost. Idempotent.
    try
    {
        var numSeqSeeder = scope.ServiceProvider
            .GetRequiredService<Abs.FixedAssets.Services.Seeding.INumberSequenceSeeder>();
        var rows = await numSeqSeeder.SeedAsync();
        Console.WriteLine($"[Startup] NumberSequence: {rows} global rows seeded");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] WARNING: NumberSequence seeder failed: {ex.Message}");
    }

        } // end: if (seedLockAcquired)
    }
    finally
    {
        if (seedLockAcquired)
        {
            try
            {
                await seedGuardForLock.ReleaseSeedLockAsync(db);
                Console.WriteLine("[Startup] Seed advisory lock released");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup] WARNING: Failed to release seed advisory lock cleanly: {ex.Message}. The lock will release automatically when the connection closes.");
            }
        }
    }
}

// Handle forwarded headers from Replit proxy
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Server-Timing header (very early so it captures total pipeline duration).
app.UseServerTiming();

// Phase 4 — security headers (CSP, X-Content-Type-Options, Referrer-Policy,
// Permissions-Policy). Set before the body is written. CSP frame-ancestors
// allows the Replit edge so the canvas/preview iframe keeps working; see
// SecurityHeadersMiddleware for the full policy and rationale.
app.UseSecurityHeaders();

// Response compression must be before any middleware that writes the body.
app.UseResponseCompression();

// Middleware to fix iframe loading and caching issues in Replit
app.Use(async (context, next) =>
{
    // Remove X-Frame-Options to allow iframe embedding
    context.Response.Headers.Remove("X-Frame-Options");
    // Disable caching for HTML pages to prevent stale content
    if (!context.Request.Path.StartsWithSegments("/css") && 
        !context.Request.Path.StartsWithSegments("/js") &&
        !context.Request.Path.StartsWithSegments("/lib") &&
        !context.Request.Path.StartsWithSegments("/images"))
    {
        context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        context.Response.Headers["Pragma"] = "no-cache";
        context.Response.Headers["Expires"] = "0";
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    var redirects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "/Admin/Locations", "/Assets/Locations" },
        { "/Admin/Vendors", "/Materials/Vendors" },
        { "/Admin/PMTemplates", "/Maintenance/PMTemplates" },
        { "/Admin/PMScheduleEdit", "/Maintenance/PMScheduleEdit" },
        { "/Admin/GlAccounts", "/Books/GlAccounts/1" },
    };
    if (redirects.TryGetValue(path, out var canonical))
    {
        var qs = context.Request.QueryString.Value ?? "";
        context.Response.Redirect(canonical + qs, permanent: true);
        return;
    }
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseRequestId();

// Snoop the username on POST /Account/Login so the rate limiter can
// partition by (IP, Username). ReadFormAsync is idempotent — Razor Pages
// will reuse the cached IFormCollection on the model-binding pass.
app.UseWhen(
    ctx => HttpMethods.IsPost(ctx.Request.Method)
        && ctx.Request.Path.StartsWithSegments("/Account/Login"),
    branch => branch.Use(async (ctx, next) =>
    {
        try
        {
            if (ctx.Request.HasFormContentType)
            {
                var form = await ctx.Request.ReadFormAsync();
                var u = form["Username"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(u)) ctx.Items["LoginUsername"] = u;
            }
        }
        catch { /* malformed form — let downstream handle it */ }
        await next();
    }));

// Phase 4 — distributed login rate limiter. Must run after the username
// snoop above so HttpContext.Items["LoginUsername"] is populated. Only
// acts on POST /Account/Login; all other paths pass through.
app.UseDistributedLoginRateLimit();

app.UseAuthentication();
app.UseApiHeaderEnforcement();
app.UseOrgScope();
app.UseTenantContext();
app.UseAuthorization();

// Health endpoints — anonymous, must be before RequireAuthorization mappings.
// Liveness is exposed on TWO paths:
//   /_live   — canonical, used in production. Avoids the "/healthz" path
//              which is intercepted by GCP's Google Front End (the edge in
//              front of Replit Autoscale) for ITS OWN internal probing,
//              causing external GETs of /healthz to receive a GFE 404
//              ("via: 1.1 google", Google logo body) before the request
//              ever reaches the container.
//   /healthz — legacy alias; kept so existing dev tooling, dashboards,
//              and the original Phase 1 contract still work locally.
//              In production the GFE will swallow it; that's expected.
var livenessOptions = new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // liveness: don't run any checks, just confirm process is responsive
    AllowCachingResponses = false,
};
app.MapHealthChecks("/_live", livenessOptions).AllowAnonymous();
app.MapHealthChecks("/healthz", livenessOptions).AllowAnonymous();
// /api/health is the conventional ops/monitoring endpoint that closes
// DEF-007 from the 2026-05-08 E2E run. Same liveness semantics as
// /_live and /healthz; just lives under the /api prefix so external
// monitors can probe it alongside the rest of the API surface.
app.MapHealthChecks("/api/health", livenessOptions).AllowAnonymous();

app.MapHealthChecks("/readyz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    AllowCachingResponses = false,
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                exception = e.Value.Exception?.Message,
            }),
        };
        await System.Text.Json.JsonSerializer.SerializeAsync(ctx.Response.Body, payload);
    },
}).AllowAnonymous();

// /api/version closes DEF-007. Returns build metadata that monitors
// and partner-integration debugging both want at hand. Cheap to compute
// (reads from the entry assembly + env vars stamped at build time).
app.MapGet("/api/version", () =>
{
    var asm = System.Reflection.Assembly.GetEntryAssembly();
    var name = asm?.GetName();
    var info = asm == null
        ? null
        : (System.Reflection.AssemblyInformationalVersionAttribute?)
            System.Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
    return Results.Json(new
    {
        product = "CherryAI EAM",
        assembly = name?.Name,
        version = name?.Version?.ToString(),
        informationalVersion = info?.InformationalVersion,
        gitSha = Environment.GetEnvironmentVariable("GIT_SHA"),
        buildTimeUtc = Environment.GetEnvironmentVariable("BUILD_TIME_UTC"),
        environment = app.Environment.EnvironmentName,
        runtime = Environment.Version.ToString(),
        startedAtUtc = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("O"),
    });
}).AllowAnonymous();

// Phase 4 — OTel diagnostics endpoint. Resolves TracerProvider/MeterProvider
// from DI to confirm both pipelines registered, and reports the active
// service.name / OTLP exporter state. Anonymous so it can be probed cheaply.
app.MapGet("/_otel/diag", (IServiceProvider sp) =>
{
    var tp = sp.GetService<OpenTelemetry.Trace.TracerProvider>();
    var mp = sp.GetService<OpenTelemetry.Metrics.MeterProvider>();
    var otlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    return Results.Json(new
    {
        tracerProvider = tp != null,
        meterProvider = mp != null,
        serviceName = "cherryai-eam",
        instrumentation = new[] { "AspNetCore", "HttpClient", "EFCore", "Runtime", "Process" },
        meterSources = new[] { "Microsoft.AspNetCore.Hosting", "System.Net.Http", "Microsoft.EntityFrameworkCore" },
        otlpExporter = string.IsNullOrWhiteSpace(otlp) ? "disabled" : "enabled",
    });
}).AllowAnonymous();

// Sprint 11 Voice MVP — POST /_voice/invoke for the in-page voice client.
// Lights up the dormant IReceiptVoiceTools (ADR-015 D10 + ADR-016 D8).
// First production voice surface. Read-only intents only in this PR;
// mutating intents (ReceiveByVoice, QuarantineByVoice) land in Sprint 5.
app.MapVoiceEndpoints().RequireAuthorization();

// Sprint 12C / ADR-021 — POST /_admin/embed/backfill + GET /_admin/embed/status.
// Triggers bulk embedding of existing entities; reports worker queue
// health for live verification.
app.MapAdminEmbedEndpoints().RequireAuthorization();

app.MapRazorPages().RequireAuthorization();
app.MapControllers();

// Swagger / OpenAPI. Enabled in Development by default; in any other
// environment, opt in via ENABLE_SWAGGER=true on the running app.
// The spec lives at /swagger/v1/swagger.json; the UI at /swagger.
var enableSwagger = app.Environment.IsDevelopment()
    || string.Equals(Environment.GetEnvironmentVariable("ENABLE_SWAGGER"), "true", StringComparison.OrdinalIgnoreCase);

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CherryAI EAM API v1");
        c.DocumentTitle = "CherryAI EAM — API Explorer";
        c.RoutePrefix = "swagger";
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/smoke/run", async (Abs.FixedAssets.Services.Testing.ISmokeTestRunner runner) =>
    {
        if (!runner.CanRunTests())
        {
            return Results.BadRequest(new { success = false, error = runner.GetBlockedReason() });
        }

        var summary = await runner.RunAllTestsAsync((_, _, _) => { }, CancellationToken.None);

        var failures = summary.Results
            .Where(r => !r.Passed)
            .Select(r => new { r.TestName, r.Error, r.Details })
            .ToList();

        var tests = summary.Results
            .Select(r => new { r.TestName, r.Passed })
            .ToList();

        return Results.Json(new
        {
            success = true,
            allPassed = summary.AllPassed,
            total = summary.TotalTests,
            passed = summary.PassedTests,
            failed = summary.FailedTests,
            durationMs = summary.TotalDurationMs,
            rollbackVerified = summary.RollbackVerified,
            rollbackDetails = summary.RollbackDetails,
            tests,
            failures
        });
    });
}

app.Run();