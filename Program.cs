using Abs.FixedAssets.Data;
using Abs.FixedAssets.Middleware;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Seeding;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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
    connectionString = $"Host={pgHost};Port={pgPort};Database={pgDatabase};Username={pgUser};Password={pgPassword};SSL Mode={sslMode};Trust Server Certificate=true";
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

// EF Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    
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
builder.Services.AddScoped<CcaService>();
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

// Authentication disabled for development - using fallback policy that allows anonymous
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
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

app.UseAuthentication();
app.UseApiHeaderEnforcement();
app.UseOrgScope();
app.UseTenantContext();
app.UseAuthorization();

app.MapRazorPages().RequireAuthorization();
app.MapControllers();

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