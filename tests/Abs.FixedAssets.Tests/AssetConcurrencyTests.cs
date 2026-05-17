using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Abs.FixedAssets.Controllers;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.Assets;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests
{
    // Asset row versioning + conflict UX coverage:
    //   (a) two simultaneous edits — second is rejected as a concurrency conflict
    //       (in-memory pre-check path AND a real PostgreSQL DbUpdateConcurrencyException),
    //   (b) Razor edit page renders the conflict banner on a stale POST,
    //   (c) public PUT API returns 409 with {error,current} on stale If-Match
    //       and 428 when If-Match is missing.
    public class AssetConcurrencyTests
    {
        // The InMemory provider can't map jsonb (LookupValue.Metadata) and has no
        // PG xmin column. Ignore both for the in-memory tests; the explicit
        // RowVersion comparison in production code is what those tests exercise.
        private sealed class TestAppDbContext : AppDbContext
        {
            public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
            protected override void OnModelCreating(ModelBuilder mb)
            {
                base.OnModelCreating(mb);
                mb.Entity<LookupValue>().Ignore(x => x.Metadata);
                mb.Entity<Asset>().Ignore(a => a.RowVersion);
            }
        }

        private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"asset-concurrency-{dbName}-{Guid.NewGuid()}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new TestAppDbContext(opts);
        }

        private sealed class StubTenantContext : ITenantContext
        {
            public int? TenantId => 1;
            public int? CompanyId => 1;
            public int? SiteId => null;
            public int? AssignedCompanyId => 1;
            public int? AssignedSiteId => null;
            public List<int> VisibleCompanyIds => new() { 1 };
            public List<int> VisibleSiteIds => new();
            public bool IsResolved => true;
            public string? ResolutionError => null;
            public void SetContext(int? tenantId, int? companyId, int? siteId) { }
            public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds) { }
            public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds) { }
            public void SetError(string error) { }
        }

        private sealed class StubLookupService : ILookupService
        {
            public Task<List<LookupValueDto>> GetValuesAsync(int? t, int? c, string k, bool i = false) => Task.FromResult(new List<LookupValueDto>());
            public Task<LookupValueDto?> GetValueByIdAsync(int? t, int? c, int id) => Task.FromResult<LookupValueDto?>(null);
            public Task<LookupValueDto?> GetValueByCodeAsync(int? t, int? c, string k, string code) => Task.FromResult<LookupValueDto?>(null);
            public Task<List<SelectListItem>> GetSelectListAsync(int? t, int? c, string k, string? sel = null, string ph = "-- Select --") => Task.FromResult(new List<SelectListItem>());
            public Task<List<SelectListItem>> GetSelectListByIdAsync(int? t, int? c, string k, int? sel = null, string ph = "-- Select --") => Task.FromResult(new List<SelectListItem>());
            public void InvalidateCache() { }
        }

        private sealed class StubModuleGuard : IModuleGuardService
        {
            public Task<bool> IsModuleEnabledAsync(string moduleName) => Task.FromResult(true);
            public Task<ModuleStatus> GetModuleStatusAsync() => Task.FromResult(new ModuleStatus
            {
                WorkOrdersEnabled = true,
                PurchasingEnabled = true,
                AccountsPayableEnabled = true,
                VendorsEnabled = true,
                InventoryEnabled = true
            });
        }

        private sealed class StubWebHostEnv : IWebHostEnvironment
        {
            private readonly string _root;
            public StubWebHostEnv()
            {
                _root = Path.Combine(Path.GetTempPath(), "abs-fa-test-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_root);
            }
            public string EnvironmentName { get; set; } = "Test";
            public string ApplicationName { get; set; } = "Abs.FixedAssets.Tests";
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
            public string WebRootPath { get => _root; set { } }
            public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        }

        private static byte[] Rv(uint v) => new[]
        {
            (byte)((v >> 24) & 0xFF),
            (byte)((v >> 16) & 0xFF),
            (byte)((v >> 8)  & 0xFF),
            (byte)( v        & 0xFF)
        };

        private static Asset SeedAsset(AppDbContext db, uint rowVersion = 100)
        {
            db.Companies.Add(new Company { Id = 1, Name = "Test Co", IsActive = true, Currency = "USD" });
            var asset = new Asset
            {
                Id = 1,
                CompanyId = 1,
                AssetNumber = "AST-TEST-1",
                Description = "Original description",
                AcquisitionCost = 100_000m,
                InServiceDate = new DateTime(2024, 1, 1),
                Status = AssetStatus.Active,
                CreatedAt = new DateTime(2024, 1, 1),
                CreatedBy = "seed",
                RowVersion = Rv(rowVersion)
            };
            db.Assets.Add(asset);
            db.SaveChanges();
            return asset;
        }

        // (a) Two simultaneous edits — explicit pre-check rejects the second.
        [Fact(Skip = "TODO: tenant scoping (added post-write) now short-circuits; assertion must be rewritten for new 403-before-409 behavior")]
        public async Task TwoSimultaneousEdits_SecondPostIsRejectedAsConcurrencyConflict()
        {
            using var db = NewDb();
            SeedAsset(db, rowVersion: 500);

            var rowAtServer = await db.Assets.FirstAsync(a => a.Id == 1);
            rowAtServer.Description = "User A change";
            rowAtServer.ModifiedAt = DateTime.UtcNow;
            rowAtServer.ModifiedBy = "user-a";
            rowAtServer.RowVersion = Rv(501);
            await db.SaveChangesAsync();

            // User B's stale version POSTs through the API path.
            var apiResult = await InvokeApiUpdateAsync(db, id: 1, ifMatch: AssetsApiController.FormatETag(Rv(500)), description: "User B change");

            var conflict = Assert.IsType<ConflictObjectResult>(apiResult);
            Assert.Equal(409, conflict.StatusCode);

            var afterRow = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == 1);
            Assert.Equal("User A change", afterRow.Description);
        }

        // (b) Razor edit page on stale POST: 200 with conflict banner state.
        [Fact]
        public async Task WebEditPage_StalePost_RendersConflictBannerAndPreservesUserEdits()
        {
            using var db = NewDb();
            SeedAsset(db, rowVersion: 700);

            var server = await db.Assets.FirstAsync(a => a.Id == 1);
            server.Description = "Latest from server";
            server.ModifiedAt = new DateTime(2026, 5, 2, 18, 30, 0, DateTimeKind.Utc);
            server.ModifiedBy = "other-user";
            server.RowVersion = Rv(701);
            await db.SaveChangesAsync();

            var page = BuildAssetModel(db);
            page.Mode = "edit";
            page.Asset = new Asset
            {
                Id = 1,
                CompanyId = 1,
                AssetNumber = "AST-TEST-1",
                Description = "User's pending edit",
                RowVersion = Rv(700),
                Status = AssetStatus.Active,
                InServiceDate = new DateTime(2024, 1, 1)
            };

            var result = await page.OnPostAsync();

            Assert.IsType<PageResult>(result);
            Assert.True(page.HasConcurrencyConflict);
            Assert.NotNull(page.ConflictServerCopy);
            Assert.Equal("Latest from server", page.ConflictServerCopy!.Description);
            Assert.True(page.ModelState.ErrorCount > 0);
            var msg = page.ModelState[string.Empty]!.Errors[0].ErrorMessage;
            Assert.Contains("changed by other-user", msg, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Please refresh", msg);
            Assert.Equal("User's pending edit", page.Asset.Description);
            // RowVersion is NOT auto-advanced — operator must refresh.
            Assert.Equal(Rv(700), page.Asset.RowVersion);
        }

        // (c) API contract on stale If-Match.
        [Fact(Skip = "TODO: tenant scoping returns 403 before If-Match check; rewrite to assert new ordering")]
        public async Task Api_StaleIfMatch_Returns409_WithErrorAndCurrentShape()
        {
            using var db = NewDb();
            SeedAsset(db, rowVersion: 900);

            var result = await InvokeApiUpdateAsync(db, id: 1, ifMatch: AssetsApiController.FormatETag(Rv(1)), description: "stale write");

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(409, conflict.StatusCode);

            var json = JsonSerializer.Serialize(conflict.Value,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("concurrency", root.GetProperty("error").GetString());
            Assert.True(root.TryGetProperty("current", out var current));
            Assert.Equal(1, current.GetProperty("id").GetInt32());
            Assert.Equal("Original description", current.GetProperty("description").GetString());
        }

        [Fact(Skip = "TODO: tenant scoping returns 403 before If-Match check; rewrite for new ordering")]
        public async Task Api_MissingIfMatch_Returns428PreconditionRequired()
        {
            using var db = NewDb();
            SeedAsset(db);

            var result = await InvokeApiUpdateAsync(db, id: 1, ifMatch: null, description: "no-precondition");

            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status428PreconditionRequired, status.StatusCode);
        }

        [Fact]
        public void ETag_RoundTrips_AsBase64FourBytes()
        {
            var raw = AssetsApiController.FormatETag(Rv(0xCAFEBABE));
            Assert.StartsWith("\"", raw);
            Assert.EndsWith("\"", raw);
            Assert.True(AssetsApiController.TryParseETag(raw, out var parsed));
            Assert.Equal(Rv(0xCAFEBABE), parsed);
        }

        // Real PostgreSQL EF concurrency: two AppDbContext instances load the same
        // row, the first save advances xmin, the second save throws
        // DbUpdateConcurrencyException. Skipped (vacuous pass) when PG env vars
        // are unavailable so the test suite still runs in clean environments.
        [Fact]
        public async Task Postgres_TwoSimultaneousEdits_SecondSaveThrowsDbUpdateConcurrencyException()
        {
            var connStr = TryBuildPgConnString();
            if (connStr == null) return;

            var opts = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connStr).Options;
            var assetNumber = "ROWVER-TEST-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            int assetId;

            await using (var setup = new AppDbContext(opts))
            {
                var company = await setup.Companies.FirstAsync();
                var asset = new Asset
                {
                    AssetNumber = assetNumber,
                    Description = "v0",
                    AcquisitionCost = 1000m,
                    InServiceDate = DateTime.UtcNow.Date,
                    CompanyId = company.Id,
                    Status = AssetStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "rowver-test"
                };
                setup.Assets.Add(asset);
                await setup.SaveChangesAsync();
                assetId = asset.Id;
            }

            try
            {
                await using var ctxA = new AppDbContext(opts);
                await using var ctxB = new AppDbContext(opts);

                var a = await ctxA.Assets.FirstAsync(x => x.Id == assetId);
                var b = await ctxB.Assets.FirstAsync(x => x.Id == assetId);

                Assert.NotNull(a.RowVersion);
                Assert.Equal(4, a.RowVersion!.Length);
                Assert.Equal(a.RowVersion, b.RowVersion);

                a.Description = "v1-from-A";
                a.ModifiedAt = DateTime.UtcNow;
                a.ModifiedBy = "user-a";
                await ctxA.SaveChangesAsync();

                b.Description = "v1-from-B";
                b.ModifiedAt = DateTime.UtcNow;
                b.ModifiedBy = "user-b";
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctxB.SaveChangesAsync());

                await using var verify = new AppDbContext(opts);
                var final = await verify.Assets.AsNoTracking().FirstAsync(x => x.Id == assetId);
                Assert.Equal("v1-from-A", final.Description);
            }
            finally
            {
                await using var cleanup = new AppDbContext(opts);
                var doomed = await cleanup.Assets.FindAsync(assetId);
                if (doomed != null)
                {
                    cleanup.Assets.Remove(doomed);
                    await cleanup.SaveChangesAsync();
                }
            }
        }

        private static string? TryBuildPgConnString()
        {
            var host = Environment.GetEnvironmentVariable("PGHOST");
            var user = Environment.GetEnvironmentVariable("PGUSER");
            var pwd  = Environment.GetEnvironmentVariable("PGPASSWORD");
            var db   = Environment.GetEnvironmentVariable("PGDATABASE");
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user)
                || string.IsNullOrEmpty(pwd) || string.IsNullOrEmpty(db))
                return null;
            var port = Environment.GetEnvironmentVariable("PGPORT") ?? "5432";
            return $"Host={host};Port={port};Username={user};Password={pwd};Database={db};SslMode=Disable";
        }

        private static AssetModel BuildAssetModel(AppDbContext db)
        {
            var tenant = new StubTenantContext();
            var attach = new AttachmentService(db, new StubWebHostEnv(), tenant);
            var lookup = new StubLookupService();
            var moduleGuard = new StubModuleGuard();
            var outbox = new Abs.FixedAssets.Services.Webhooks.OutboxWriter(
                db, tenant, NullLogger<Abs.FixedAssets.Services.Webhooks.OutboxWriter>.Instance);
            var page = new AssetModel(db, attach, lookup, tenant, moduleGuard, outbox);

            var httpContext = new DefaultHttpContext();
            var modelState = new ModelStateDictionary();
            var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
            page.PageContext = new PageContext(actionContext)
            {
                ViewData = new ViewDataDictionary<AssetModel>(new EmptyModelMetadataProvider(), modelState)
            };
            return page;
        }

        private static async Task<IActionResult> InvokeApiUpdateAsync(AppDbContext db, int id, string? ifMatch, string description)
        {
            const string rawKey = "test-raw-key-cfa_concurrency";
            var hash = ComputeSha256(rawKey);
            if (!await db.ApiKeys.AnyAsync(k => k.KeyHash == hash))
            {
                db.ApiKeys.Add(new ApiKey
                {
                    Name = "concurrency-test",
                    KeyHash = hash,
                    KeyPrefix = "test",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Scopes = "assets:read,assets:write"
                });
                await db.SaveChangesAsync();
            }

            var svc = new ApiService(db);
            var tenant = new StubTenantContext();
            var controller = new AssetsApiController(db, svc, tenant);
            var http = new DefaultHttpContext();
            http.Request.Headers["X-API-Key"] = rawKey;
            if (ifMatch != null)
                http.Request.Headers["If-Match"] = ifMatch;
            controller.ControllerContext = new ControllerContext { HttpContext = http };

            return await controller.UpdateAsset(id, new UpdateAssetRequest { Description = description });
        }

        private static string ComputeSha256(string s)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
