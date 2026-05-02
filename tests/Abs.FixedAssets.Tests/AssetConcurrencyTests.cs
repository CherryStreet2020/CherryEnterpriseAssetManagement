using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace Abs.FixedAssets.Tests
{
    /// <summary>
    /// Coverage for Task #7 (asset row versioning + conflict UX).
    /// (a) Two simultaneous edits — second one is rejected as a concurrency conflict.
    /// (b) Razor edit page returns 200 with the conflict banner state on stale POST.
    /// (c) Public API returns 409 with the documented {error,current} shape on stale If-Match,
    ///     and 428 when If-Match is missing.
    /// </summary>
    public class AssetConcurrencyTests
    {
        // The InMemory provider can't map jsonb (LookupValue.Metadata). Same trick as the
        // CCA tests: ignore that property in tests.
        private sealed class TestAppDbContext : AppDbContext
        {
            public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
            protected override void OnModelCreating(ModelBuilder mb)
            {
                base.OnModelCreating(mb);
                mb.Entity<LookupValue>().Ignore(x => x.Metadata);
                // Asset.RowVersion is mapped to PG xmin (xid). InMemory has no xmin, so
                // ignore the property here — the production code uses an EXPLICIT
                // RowVersion comparison (in addition to the EF concurrency token) which
                // is what these tests exercise.
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
                RowVersion = rowVersion
            };
            db.Assets.Add(asset);
            db.SaveChanges();
            return asset;
        }

        // ---------------------------------------------------------------
        // (a) Two simultaneous edits — explicit pre-check rejects the second.
        // ---------------------------------------------------------------
        [Fact]
        public async Task TwoSimultaneousEdits_SecondPostIsRejectedAsConcurrencyConflict()
        {
            using var db = NewDb();
            SeedAsset(db, rowVersion: 500);

            // Both users open the form at row-version 500.
            var userAOriginalRowVersion = 500u;
            var userBOriginalRowVersion = 500u;

            // User A saves first. Production code applies the change and the underlying
            // PG xmin advances; we simulate that here by bumping the stored RowVersion
            // out-of-band.
            var rowAtServer = await db.Assets.FirstAsync(a => a.Id == 1);
            rowAtServer.Description = "User A change";
            rowAtServer.ModifiedAt = DateTime.UtcNow;
            rowAtServer.ModifiedBy = "user-a";
            rowAtServer.RowVersion = 501;
            await db.SaveChangesAsync();

            // User B now POSTs with their stale version. Drive the API path explicitly
            // (same precondition logic the Razor handler uses).
            var apiResult = await InvokeApiUpdateAsync(db, id: 1, ifMatch: BuildBase64ETag(userBOriginalRowVersion), description: "User B change");

            Assert.IsType<ConflictObjectResult>(apiResult);
            var conflict = (ConflictObjectResult)apiResult;
            Assert.Equal(409, conflict.StatusCode);
            // The asset on disk is still User A's value.
            var afterRow = await db.Assets.AsNoTracking().FirstAsync(a => a.Id == 1);
            Assert.Equal("User A change", afterRow.Description);
            // Sanity: User A's change was not silently overwritten by User B.

            // The non-stale prefix `userAOriginalRowVersion` is unused beyond the test
            // narrative; reference it so the compiler stays quiet.
            Assert.Equal(500u, userAOriginalRowVersion);
        }

        // ---------------------------------------------------------------
        // (b) Razor edit page on stale POST: 200 with conflict banner state.
        // ---------------------------------------------------------------
        [Fact]
        public async Task WebEditPage_StalePost_RendersConflictBannerAndPreservesUserEdits()
        {
            using var db = NewDb();
            SeedAsset(db, rowVersion: 700);

            // Server-side advance: the row was edited by another user.
            var server = await db.Assets.FirstAsync(a => a.Id == 1);
            server.Description = "Latest from server";
            server.ModifiedAt = new DateTime(2026, 5, 2, 18, 30, 0, DateTimeKind.Utc);
            server.ModifiedBy = "other-user";
            server.RowVersion = 701;
            await db.SaveChangesAsync();

            var page = BuildAssetModel(db);
            page.Mode = "edit";
            // The user posted with the original (stale) RowVersion of 700 and a
            // changed description.
            page.Asset = new Asset
            {
                Id = 1,
                CompanyId = 1,
                AssetNumber = "AST-TEST-1",
                Description = "User's pending edit",
                RowVersion = 700,
                Status = AssetStatus.Active,
                InServiceDate = new DateTime(2024, 1, 1)
            };

            var result = await page.OnPostAsync();

            Assert.IsType<PageResult>(result);
            Assert.True(page.HasConcurrencyConflict, "HasConcurrencyConflict flag should be set so the view renders the yellow banner.");
            Assert.NotNull(page.ConflictServerCopy);
            Assert.Equal("Latest from server", page.ConflictServerCopy!.Description);
            // ModelState carries the human-readable message.
            Assert.True(page.ModelState.ErrorCount > 0);
            var msg = page.ModelState[string.Empty]!.Errors[0].ErrorMessage;
            Assert.Contains("changed by other-user", msg, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Please refresh", msg);
            // User's pending edit is preserved (not clobbered with the server value).
            Assert.Equal("User's pending edit", page.Asset.Description);
            // RowVersion is NOT silently advanced — the operator must refresh the page
            // (and re-read the latest server values) before the next save.
            Assert.Equal(700u, page.Asset.RowVersion);
        }

        // ---------------------------------------------------------------
        // (c) API contract on stale If-Match.
        // ---------------------------------------------------------------
        [Fact]
        public async Task Api_StaleIfMatch_Returns409_WithErrorAndCurrentShape()
        {
            using var db = NewDb();
            SeedAsset(db, rowVersion: 900);

            // Stale If-Match: client thinks RowVersion=1, server has 900.
            var result = await InvokeApiUpdateAsync(db, id: 1, ifMatch: BuildBase64ETag(1u), description: "stale write");

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(409, conflict.StatusCode);

            // Documented body shape: { error: "concurrency", message, current: <DTO> }.
            // Anonymous types serialize with their declared property names; we use
            // camelCase here so the comparison is case-insensitive and matches both
            // System.Text.Json default and ASP.NET Core's camelCase wire format.
            var json = JsonSerializer.Serialize(conflict.Value,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("concurrency", root.GetProperty("error").GetString());
            Assert.True(root.TryGetProperty("current", out var current));
            Assert.Equal(1, current.GetProperty("id").GetInt32());
            Assert.Equal("Original description", current.GetProperty("description").GetString());
        }

        [Fact]
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
            var raw = AssetsApiController.FormatETag(0xCAFEBABE);
            Assert.StartsWith("\"", raw);
            Assert.EndsWith("\"", raw);
            Assert.True(AssetsApiController.TryParseETag(raw, out var parsed));
            Assert.Equal(0xCAFEBABEu, parsed);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private static string BuildBase64ETag(uint v) => AssetsApiController.FormatETag(v);

        private static AssetModel BuildAssetModel(AppDbContext db)
        {
            var tenant = new StubTenantContext();
            var attach = new AttachmentService(db, new StubWebHostEnv(), tenant);
            var lookup = new StubLookupService();
            var moduleGuard = new StubModuleGuard();
            var page = new AssetModel(db, attach, lookup, tenant, moduleGuard);

            // Minimum PageContext / ModelState plumbing so OnPostAsync can call
            // ModelState.AddModelError and return Page().
            var httpContext = new DefaultHttpContext();
            var modelState = new ModelStateDictionary();
            var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
            page.PageContext = new PageContext(actionContext)
            {
                ViewData = new ViewDataDictionary<AssetModel>(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), modelState)
            };
            return page;
        }

        private static async Task<IActionResult> InvokeApiUpdateAsync(AppDbContext db, int id, string? ifMatch, string description)
        {
            // Seed a real ApiKey with a deterministic SHA-256 hash so the production
            // ApiService.ValidateKeyAsync accepts our request as authenticated.
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
            var controller = new AssetsApiController(db, svc);
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
