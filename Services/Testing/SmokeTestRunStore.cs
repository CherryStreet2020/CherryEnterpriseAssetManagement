using System.Collections.Concurrent;

namespace Abs.FixedAssets.Services.Testing
{
    public enum SmokeTestRunStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    public class SmokeTestRun
    {
        public Guid RunId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public SmokeTestRunStatus Status { get; set; }
        public string? CurrentTestName { get; set; }
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public SmokeTestSummary? Results { get; set; }
        public string? ErrorMessage { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        public string? InitiatingUserId { get; set; }
        public int? TenantId { get; set; }
        public int? CompanyId { get; set; }
        public int? SiteId { get; set; }
    }

    public interface ISmokeTestRunStore
    {
        void CreateRun(Guid runId, string? userId = null, int? tenantId = null, int? companyId = null, int? siteId = null);
        SmokeTestRun? GetRun(Guid runId);
        SmokeTestRun? GetActiveRunByUser(string userId);
        void UpdateProgress(Guid runId, string currentTestName, int completedCount, int totalCount);
        void CompleteRun(Guid runId, SmokeTestSummary results);
        void FailRun(Guid runId, string errorMessage);
        void CancelRun(Guid runId);
        void SetCancellationToken(Guid runId, CancellationTokenSource cts);
        IEnumerable<SmokeTestRun> GetRecentRuns(int count = 10);
        void CleanupOldRuns(TimeSpan maxAge);
    }

    public class SmokeTestRunStore : ISmokeTestRunStore
    {
        private readonly ConcurrentDictionary<Guid, SmokeTestRun> _runs = new();
        private readonly object _cleanupLock = new();

        public void CreateRun(Guid runId, string? userId = null, int? tenantId = null, int? companyId = null, int? siteId = null)
        {
            var run = new SmokeTestRun
            {
                RunId = runId,
                StartedAt = DateTime.UtcNow,
                Status = SmokeTestRunStatus.Queued,
                CompletedCount = 0,
                TotalCount = 0,
                InitiatingUserId = userId,
                TenantId = tenantId,
                CompanyId = companyId,
                SiteId = siteId
            };
            _runs[runId] = run;
        }

        public SmokeTestRun? GetRun(Guid runId)
        {
            return _runs.TryGetValue(runId, out var run) ? run : null;
        }

        public SmokeTestRun? GetActiveRunByUser(string userId)
        {
            return _runs.Values
                .FirstOrDefault(r => r.InitiatingUserId == userId &&
                    (r.Status == SmokeTestRunStatus.Queued || r.Status == SmokeTestRunStatus.Running));
        }

        public void UpdateProgress(Guid runId, string currentTestName, int completedCount, int totalCount)
        {
            if (_runs.TryGetValue(runId, out var run))
            {
                run.Status = SmokeTestRunStatus.Running;
                run.CurrentTestName = currentTestName;
                run.CompletedCount = completedCount;
                run.TotalCount = totalCount;
            }
        }

        public void CompleteRun(Guid runId, SmokeTestSummary results)
        {
            if (_runs.TryGetValue(runId, out var run))
            {
                run.Status = SmokeTestRunStatus.Completed;
                run.CompletedAt = DateTime.UtcNow;
                run.Results = results;
                run.CompletedCount = results.TotalTests;
                run.TotalCount = results.TotalTests;
                run.CurrentTestName = null;
            }
        }

        public void FailRun(Guid runId, string errorMessage)
        {
            if (_runs.TryGetValue(runId, out var run))
            {
                run.Status = SmokeTestRunStatus.Failed;
                run.CompletedAt = DateTime.UtcNow;
                run.ErrorMessage = errorMessage;
                run.CurrentTestName = null;
            }
        }

        public void CancelRun(Guid runId)
        {
            if (_runs.TryGetValue(runId, out var run))
            {
                run.CancellationTokenSource?.Cancel();
                run.Status = SmokeTestRunStatus.Cancelled;
                run.CompletedAt = DateTime.UtcNow;
                run.ErrorMessage = "Run was cancelled";
                run.CurrentTestName = null;
            }
        }

        public void SetCancellationToken(Guid runId, CancellationTokenSource cts)
        {
            if (_runs.TryGetValue(runId, out var run))
            {
                run.CancellationTokenSource = cts;
            }
        }

        public IEnumerable<SmokeTestRun> GetRecentRuns(int count = 10)
        {
            return _runs.Values
                .OrderByDescending(r => r.StartedAt)
                .Take(count)
                .ToList();
        }

        public void CleanupOldRuns(TimeSpan maxAge)
        {
            lock (_cleanupLock)
            {
                var cutoff = DateTime.UtcNow - maxAge;
                var oldRuns = _runs.Where(kvp => kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var runId in oldRuns)
                {
                    _runs.TryRemove(runId, out _);
                }
            }
        }
    }
}
