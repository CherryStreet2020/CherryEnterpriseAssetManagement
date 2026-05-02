using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Testing
{
    public class SmokeTestBackgroundService : BackgroundService
    {
        private readonly ISmokeTestRunQueue _queue;
        private readonly ISmokeTestRunStore _store;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITenantContextOverride _tenantContextOverride;
        private readonly ILogger<SmokeTestBackgroundService> _logger;
        private readonly TimeSpan _maxRunDuration = TimeSpan.FromMinutes(10);

        public SmokeTestBackgroundService(
            ISmokeTestRunQueue queue,
            ISmokeTestRunStore store,
            IServiceScopeFactory scopeFactory,
            ITenantContextOverride tenantContextOverride,
            ILogger<SmokeTestBackgroundService> logger)
        {
            _queue = queue;
            _store = store;
            _scopeFactory = scopeFactory;
            _tenantContextOverride = tenantContextOverride;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Smoke test background service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var request = await _queue.DequeueAsync(stoppingToken);
                    await ProcessRunAsync(request, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing smoke test run from queue");
                    await Task.Delay(1000, stoppingToken);
                }
            }

            _logger.LogInformation("Smoke test background service stopped");
        }

        private async Task ProcessRunAsync(SmokeTestRunRequest request, CancellationToken stoppingToken)
        {
            var runId = request.RunId;
            _logger.LogInformation("Starting smoke test run {RunId}", runId);

            var run = _store.GetRun(runId);
            if (run == null)
            {
                _logger.LogWarning("Smoke test run {RunId} not found in store", runId);
                return;
            }

            var existingCts = run.CancellationTokenSource;
            
            using var timeoutCts = new CancellationTokenSource(_maxRunDuration);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken, 
                request.CancellationToken,
                existingCts?.Token ?? CancellationToken.None,
                timeoutCts.Token);

            var tenantId = run.TenantId ?? 1;
            var companyId = run.CompanyId ?? 1;
            var siteId = run.SiteId;

            if (!run.TenantId.HasValue || !run.CompanyId.HasValue)
            {
                _logger.LogWarning("Smoke test run {RunId} missing tenant context, using fallback TenantId={TenantId}, CompanyId={CompanyId}", 
                    runId, tenantId, companyId);
            }

            try
            {
                using var tenantScope = _tenantContextOverride.BeginScope(tenantId, companyId, siteId, null);
                using var serviceScope = _scopeFactory.CreateScope();
                
                var testRunner = serviceScope.ServiceProvider.GetRequiredService<ISmokeTestRunner>();

                var totalTests = testRunner.GetTotalTestCount();
                _store.UpdateProgress(runId, "Initializing...", 0, totalTests);

                _logger.LogInformation("Running smoke tests with TenantId={TenantId}, CompanyId={CompanyId}, SiteId={SiteId}",
                    tenantId, companyId, siteId);

                var results = await testRunner.RunAllTestsAsync(
                    (testName, completed, total) =>
                    {
                        _store.UpdateProgress(runId, testName, completed, total);
                    },
                    linkedCts.Token);

                _store.CompleteRun(runId, results);
                _logger.LogInformation("Smoke test run {RunId} completed: {Passed}/{Total} passed",
                    runId, results.PassedTests, results.TotalTests);
            }
            catch (OperationCanceledException)
            {
                var currentRun = _store.GetRun(runId);
                if (currentRun?.Status != SmokeTestRunStatus.Cancelled)
                {
                    _store.FailRun(runId, "Run timed out (max 10 minutes)");
                    _logger.LogWarning("Smoke test run {RunId} timed out", runId);
                }
            }
            catch (Exception ex)
            {
                _store.FailRun(runId, ex.Message);
                _logger.LogError(ex, "Smoke test run {RunId} failed", runId);
            }

            _store.CleanupOldRuns(TimeSpan.FromHours(1));
        }
    }
}
