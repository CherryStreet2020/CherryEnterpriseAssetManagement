using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Services.Testing;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Data;
using System.Security.Claims;

namespace Abs.FixedAssets.Pages.Admin
{
    // PR #100 (B-03): SystemAdmin role does not exist in the identity seed —
    // the only seeded roles are Admin / Manager / Accountant / Viewer.
    // Listing a non-existent role here was harmless (the OR-of-roles gate
    // still admitted Admin) but misleading and easy to misread as proof that
    // a tighter "system admin" tier exists. Drop the dead role; "Admin" is
    // the canonical superuser tier across the app.
    [Authorize(Roles = "Admin")]
    public class SmokeTestsModel : PageModel
    {
        private readonly ISmokeTestRunner _testRunner;
        private readonly IWebHostEnvironment _env;
        private readonly ISeedGuardService _guardService;
        private readonly ISmokeTestRunQueue _runQueue;
        private readonly ISmokeTestRunStore _runStore;
        private readonly ITenantContext _tenantContext;
        private readonly AppDbContext _db;
        private readonly ILogger<SmokeTestsModel> _logger;

        public SmokeTestsModel(
            ISmokeTestRunner testRunner,
            IWebHostEnvironment env,
            ISeedGuardService guardService,
            ISmokeTestRunQueue runQueue,
            ISmokeTestRunStore runStore,
            ITenantContext tenantContext,
            AppDbContext db,
            ILogger<SmokeTestsModel> logger)
        {
            _testRunner = testRunner;
            _env = env;
            _guardService = guardService;
            _runQueue = runQueue;
            _runStore = runStore;
            _tenantContext = tenantContext;
            _db = db;
            _logger = logger;
        }

        public bool CanRunTests { get; set; }
        public string BlockedReason { get; set; } = string.Empty;
        public bool IsDevelopment { get; set; }
        public string EnvironmentProfile { get; set; } = string.Empty;
        public SmokeTestSummary? TestSummary { get; set; }
        public bool HasRun { get; set; }
        public int TotalTestCount { get; set; }
        public string? DeprecationMessage { get; set; }

        public void OnGet()
        {
            LoadEnvironmentInfo();
        }

        public IActionResult OnGetRunTests()
        {
            DeprecationMessage = "The synchronous RunTests handler has been deprecated. Please use the async background runner instead.";
            TempData["DeprecationMessage"] = DeprecationMessage;
            return RedirectToPage();
        }

        public IActionResult OnPostStartRun()
        {
            LoadEnvironmentInfo();

            if (!CanRunTests)
            {
                return new JsonResult(new { success = false, error = BlockedReason });
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return new JsonResult(new { success = false, error = "User not authenticated" });
            }

            var existingRun = _runStore.GetActiveRunByUser(userId);
            if (existingRun != null)
            {
                return new JsonResult(new { 
                    success = true, 
                    runId = existingRun.RunId.ToString(),
                    alreadyRunning = true,
                    message = "You already have an active test run in progress"
                });
            }

            var runId = Guid.NewGuid();
            var cts = new CancellationTokenSource();
            
            var (tenantId, companyId, siteId) = ResolveEffectiveTenantContext();
            
            _runStore.CreateRun(runId, userId, tenantId, companyId, siteId);
            _runStore.SetCancellationToken(runId, cts);

            var request = new SmokeTestRunRequest
            {
                RunId = runId,
                RequestedAt = DateTime.UtcNow,
                CancellationToken = cts.Token
            };

            try
            {
                _runQueue.Enqueue(request);
                return new JsonResult(new { success = true, runId = runId.ToString() });
            }
            catch (InvalidOperationException ex)
            {
                return new JsonResult(new { success = false, error = ex.Message });
            }
        }

        public IActionResult OnGetStatus(string runId)
        {
            if (!Guid.TryParse(runId, out var id))
            {
                return new JsonResult(new { error = "Invalid run ID" });
            }

            var run = _runStore.GetRun(id);
            if (run == null)
            {
                return new JsonResult(new { error = "Run not found" });
            }

            if (!CanAccessRun(run))
            {
                return new JsonResult(new { error = "Access denied" }) { StatusCode = 403 };
            }

            return new JsonResult(new
            {
                runId = run.RunId.ToString(),
                status = run.Status.ToString(),
                currentTestName = run.CurrentTestName,
                completedCount = run.CompletedCount,
                totalCount = run.TotalCount,
                errorMessage = run.ErrorMessage,
                hasResults = run.Results != null
            });
        }

        public IActionResult OnGetResults(string runId)
        {
            if (!Guid.TryParse(runId, out var id))
            {
                return new JsonResult(new { error = "Invalid run ID" });
            }

            var run = _runStore.GetRun(id);
            if (run == null)
            {
                return new JsonResult(new { error = "Run not found" });
            }

            if (!CanAccessRun(run))
            {
                return new JsonResult(new { error = "Access denied" }) { StatusCode = 403 };
            }

            if (run.Results == null)
            {
                return new JsonResult(new { error = "Results not available yet" });
            }

            var summary = run.Results;
            return new JsonResult(new
            {
                success = true,
                totalTests = summary.TotalTests,
                passedTests = summary.PassedTests,
                failedTests = summary.FailedTests,
                allPassed = summary.AllPassed,
                totalDurationMs = summary.TotalDurationMs,
                rollbackVerified = summary.RollbackVerified,
                rollbackDetails = summary.RollbackDetails,
                beforeCounts = summary.BeforeCounts,
                afterCounts = summary.AfterCounts,
                results = summary.Results.Select(r => new
                {
                    testName = r.TestName,
                    passed = r.Passed,
                    error = r.Error,
                    details = r.Details,
                    durationMs = r.DurationMs
                }).ToList()
            });
        }

        public IActionResult OnPostCancelRun(string runId)
        {
            if (!Guid.TryParse(runId, out var id))
            {
                return new JsonResult(new { success = false, error = "Invalid run ID" });
            }

            var run = _runStore.GetRun(id);
            if (run == null)
            {
                return new JsonResult(new { success = false, error = "Run not found" });
            }

            if (!CanAccessRun(run))
            {
                return new JsonResult(new { success = false, error = "Access denied" }) { StatusCode = 403 };
            }

            _runStore.CancelRun(id);
            return new JsonResult(new { success = true });
        }

        private void LoadEnvironmentInfo()
        {
            IsDevelopment = _env.IsDevelopment();
            EnvironmentProfile = _guardService.GetEnvironmentProfile();
            CanRunTests = _testRunner.CanRunTests();
            BlockedReason = _testRunner.GetBlockedReason();
            TotalTestCount = _testRunner.GetTotalTestCount();
            
            if (TempData["DeprecationMessage"] is string msg)
            {
                DeprecationMessage = msg;
            }
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? User.FindFirst("sub")?.Value
                ?? User.Identity?.Name;
        }

        private int? GetCurrentTenantId()
        {
            if (_tenantContext.TenantId.HasValue)
            {
                return _tenantContext.TenantId;
            }
            var claim = User.FindFirst("TenantId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private int? GetCurrentCompanyId()
        {
            if (_tenantContext.CompanyId.HasValue)
            {
                return _tenantContext.CompanyId;
            }
            var claim = User.FindFirst("CompanyId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private int? GetCurrentSiteId()
        {
            if (_tenantContext.SiteId.HasValue)
            {
                return _tenantContext.SiteId;
            }
            var claim = User.FindFirst("SiteId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        private (int? TenantId, int? CompanyId, int? SiteId) ResolveEffectiveTenantContext()
        {
            var tenantId = GetCurrentTenantId();
            var companyId = GetCurrentCompanyId();
            var siteId = GetCurrentSiteId();

            if (tenantId.HasValue && companyId.HasValue)
            {
                return (tenantId, companyId, siteId);
            }

            if (IsDevelopment)
            {
                var firstTenant = _db.Tenants.OrderBy(t => t.Id).FirstOrDefault();
                var firstCompany = _db.Companies.OrderBy(c => c.Id).FirstOrDefault();
                var firstSite = _db.Sites.OrderBy(s => s.Id).FirstOrDefault();

                if (firstTenant != null && firstCompany != null)
                {
                    _logger.LogWarning("Smoke test fallback tenant resolution: TenantId={TenantId}, CompanyId={CompanyId}, SiteId={SiteId}",
                        firstTenant.Id, firstCompany.Id, firstSite?.Id);

                    return (
                        tenantId ?? firstTenant.Id,
                        companyId ?? firstCompany.Id,
                        siteId ?? firstSite?.Id
                    );
                }
            }

            return (tenantId ?? 1, companyId ?? 1, siteId);
        }

        private bool CanAccessRun(SmokeTestRun run)
        {
            var currentUserId = GetCurrentUserId();
            
            // PR #100 (B-03): drop SystemAdmin reference; role doesn't exist.
            if (User.IsInRole("Admin"))
            {
                return true;
            }
            
            if (!string.IsNullOrEmpty(currentUserId) && run.InitiatingUserId == currentUserId)
            {
                return true;
            }
            
            return false;
        }
    }
}
