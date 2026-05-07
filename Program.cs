using Abs.FixedAssets.Data;
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
    options.UseNpgsql(connectionString, npg => npg.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    options.AddInterceptors(sp.GetRequiredService<Abs.FixedAssets.Services.Diagnostics.SlowQueryInterceptor>());
    
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
            
            // Suppress PendingModelChangesWarning for migrations
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
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
builder.Services.AddScoped<IPeriodGuard, PeriodGuard>();
builder.Services.AddScoped<DepreciationBackfillService>();
builder.Services.AddScoped<HistoricJournalBackfillService>();
builder.Services.AddScoped<CcaService>();
builder.Services.AddScoped<CcaBackfillService>();
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
});

var app = builder.Build();

// Ensure database is created with current model and seed default data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    
    // Use migrations in Development or when AUTO_MIGRATE=true
    // This ensures schema changes are properly applied and avoids drift
    var autoMigrate = Environment.GetEnvironmentVariable("AUTO_MIGRATE");
    var isDev = app.Environment.IsDevelopment();
    
    if (isDev || autoMigrate?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
    {
        Console.WriteLine("[Startup] Applying database migrations...");
        await db.Database.MigrateAsync();
        Console.WriteLine("[Startup] Database migrations applied successfully");
    }
    else
    {
        // Production: Only ensure database exists, do not auto-migrate
        // Migrations should be applied through a controlled deployment process
        Console.WriteLine("[Startup] Production mode - skipping auto-migration");
        Console.WriteLine("[Startup] To enable auto-migration, set AUTO_MIGRATE=true");
        db.Database.EnsureCreated();
    }
    
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