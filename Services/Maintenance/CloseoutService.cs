using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Cip;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using System.Text;
using System.Text.Json;

namespace Abs.FixedAssets.Services.Maintenance;

public class CloseoutResult
{
    public int WorkOrderId { get; set; }
    public string? ResolutionSummary { get; set; }
    public int? AuditLogId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class LessonSaveResult
{
    public int LessonId { get; set; }
    public int? AuditLogId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class RecurringFailure
{
    public string FailureCode { get; set; } = string.Empty;
    public int AssetId { get; set; }
    public string? AssetNumber { get; set; }
    public string? AssetDescription { get; set; }
    public int SiteId { get; set; }
    public string? SiteName { get; set; }
    public int Count { get; set; }
    public DateTime? LastOccurrence { get; set; }
}

public interface ICloseoutService
{
    string GenerateCloseoutSummary(MaintenanceEvent workOrder, List<WorkOrderOperation>? operations = null);
    Task<CloseoutResult> CloseWorkOrderAsync(int workOrderId, string? lessonsLearned, string username, bool allowIncompleteOperations = false);
    Task<LessonSaveResult> SaveLessonAsync(int workOrderId, string lessonText, string? tags, string username);
    Task<List<RecurringFailure>> GetRecurringFailuresAsync(int days = 30, int limit = 5);
}

public class CloseoutService : ICloseoutService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<CloseoutService> _logger;
    private readonly ILookupService? _lookupService;
    private readonly CipAutoCostPostingService? _cipAutoCostPosting;

    public CloseoutService(AppDbContext db, ITenantContext tenantContext, IOutboxWriter outbox, ILogger<CloseoutService> logger, ILookupService? lookupService = null, CipAutoCostPostingService? cipAutoCostPosting = null)
    {
        _db = db;
        _tenantContext = tenantContext;
        _outbox = outbox;
        _logger = logger;
        _lookupService = lookupService;
        _cipAutoCostPosting = cipAutoCostPosting;
    }

    public string GenerateCloseoutSummary(MaintenanceEvent workOrder, List<WorkOrderOperation>? operations = null)
    {
        var sb = new StringBuilder();

        sb.Append($"Work Order {workOrder.WorkOrderNumber ?? $"#{workOrder.Id}"} completed. ");

        if (!string.IsNullOrEmpty(workOrder.FailureCode))
        {
            sb.Append($"Failure code: {workOrder.FailureCode}. ");
        }

        if (!string.IsNullOrEmpty(workOrder.CorrectiveAction))
        {
            sb.Append($"Corrective action: {workOrder.CorrectiveAction}. ");
        }

        if (!string.IsNullOrEmpty(workOrder.RootCause))
        {
            sb.Append($"Root cause identified: {workOrder.RootCause}. ");
        }

        if (workOrder.LaborHours.HasValue && workOrder.LaborHours > 0)
        {
            sb.Append($"Labor: {workOrder.LaborHours:F1} hours. ");
        }

        if (workOrder.ActualCost.HasValue && workOrder.ActualCost > 0)
        {
            sb.Append($"Total cost: ${workOrder.ActualCost:N2}. ");
        }

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("Operations completed:");

        var ops = operations ?? workOrder.Operations?.ToList() ?? new List<WorkOrderOperation>();
        if (ops.Any())
        {
            foreach (var op in ops.OrderBy(o => o.Sequence))
            {
                var status = op.Status == OperationStatus.Completed ? "Done" : op.Status.ToString();
                sb.AppendLine($"  - {op.Title ?? op.OperationNumber}: {status}");
            }
        }
        else
        {
            sb.AppendLine("  - No operations recorded");
        }

        if (!string.IsNullOrEmpty(workOrder.Notes))
        {
            sb.AppendLine();
            sb.AppendLine($"Notes: {workOrder.Notes}");
        }

        return sb.ToString().Trim();
    }

    private int GetCompanyId() => _tenantContext.CompanyId ?? 1;
        private List<int> GetVisibleCompanyIds() => _tenantContext.VisibleCompanyIds;

    public async Task<CloseoutResult> CloseWorkOrderAsync(int workOrderId, string? lessonsLearned, string username, bool allowIncompleteOperations = false)
    {
        var result = new CloseoutResult { WorkOrderId = workOrderId };
        var companyId = GetCompanyId();

        try
        {
            var workOrder = await _db.MaintenanceEvents
                .Include(m => m.Operations)
                .Include(m => m.Asset)
                .Where(m => m.Asset != null && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync(m => m.Id == workOrderId);

            if (workOrder == null)
            {
                result.Success = false;
                result.Error = $"Work Order {workOrderId} not found";
                return result;
            }

            var operations = workOrder.Operations?.ToList() ?? new List<WorkOrderOperation>();
            
            var incompleteOps = operations.Where(o => o.Status != OperationStatus.Completed && o.Status != OperationStatus.Cancelled).ToList();
            if (incompleteOps.Any() && !allowIncompleteOperations)
            {
                result.Success = false;
                result.Error = $"Cannot close work order with {incompleteOps.Count} incomplete operation(s). Complete all operations first or use the override flag.";
                return result;
            }
            
            var summary = GenerateCloseoutSummary(workOrder, operations);

            workOrder.Status = MaintenanceStatus.Completed;
            if (_lookupService != null)
            {
                var completedLv = await _lookupService.GetValueByCodeAsync(null, null, "MaintenanceStatus", ((int)MaintenanceStatus.Completed).ToString());
                workOrder.StatusLookupValueId = completedLv?.Id;
            }
            workOrder.CompletedDate = DateTime.UtcNow;
            workOrder.CompletedBy = username;
            workOrder.ClosedAt = DateTime.UtcNow;
            workOrder.ClosedBy = username;
            workOrder.ResolutionSummary = summary;

            if (!string.IsNullOrEmpty(lessonsLearned))
            {
                workOrder.LessonsLearned = lessonsLearned;
            }

            // PR #103 (B-12): Roll the WO-LBR / WO-ISS / WO-RTN JE totals
            // back to the WorkOrder header fields. After PR #98 made this
            // the default close path on the modern surface, LaborCost /
            // PartsCost / MaterialsCost / ActualCost were never populated
            // on close — the JE-side data was correct (PR #89 + #92) but
            // the WO header still showed nulls, breaking dashboards and
            // the "Total Cost" stat that downstream reports read.
            //
            // Source of truth = the JE table, same surface the PR #93
            // Maintenance Spend report sums from. Reference pattern
            // "WO-{LBR|ISS|RTN}-{woId}-…" set by the helpers in PR #89/#92.
            var laborPrefix = $"WO-LBR-{workOrderId}-";
            var issuePrefix = $"WO-ISS-{workOrderId}-";
            var returnPrefix = $"WO-RTN-{workOrderId}-";

            var laborTotal = await _db.JournalEntries
                .Where(j => j.Source == "WO-LBR" && j.Reference != null && j.Reference.StartsWith(laborPrefix))
                .SelectMany(j => j.Lines)
                .Where(l => l.Debit > 0m)
                .SumAsync(l => (decimal?)l.Debit) ?? 0m;
            var materialsIssued = await _db.JournalEntries
                .Where(j => j.Source == "WO-ISS" && j.Reference != null && j.Reference.StartsWith(issuePrefix))
                .SelectMany(j => j.Lines)
                .Where(l => l.Debit > 0m)
                .SumAsync(l => (decimal?)l.Debit) ?? 0m;
            var materialsReturned = await _db.JournalEntries
                .Where(j => j.Source == "WO-RTN" && j.Reference != null && j.Reference.StartsWith(returnPrefix))
                .SelectMany(j => j.Lines)
                .Where(l => l.Debit > 0m)
                .SumAsync(l => (decimal?)l.Debit) ?? 0m;

            workOrder.LaborCost = laborTotal;
            workOrder.MaterialsCost = materialsIssued - materialsReturned;
            // PartsCost is a legacy field kept for the closeout-summary form;
            // its sole source is the issued/returned material JEs same as
            // MaterialsCost, so we double-write to keep the older display
            // path happy without forking the data.
            workOrder.PartsCost = workOrder.MaterialsCost;
            workOrder.ActualCost = (workOrder.LaborCost ?? 0m)
                                 + (workOrder.MaterialsCost ?? 0m)
                                 + (workOrder.OutsideVendorCost ?? 0m);

            // PR #103 (B-13): Flip the linked PMOccurrence to Completed so
            // PMSchedulerService knows this occurrence has been fulfilled
            // and the scheduler stops re-firing it. Pre-fix, the Completed
            // enum value (3) was unreachable from any code path; calendar
            // PM-driven WOs closed cleanly but the occurrence row stayed
            // Status=Created forever, so subsequent scheduler ticks could
            // re-emit a duplicate. WorkOrder.PMOccurrenceId nullable —
            // corrective / ad-hoc WOs have no occurrence to flip.
            if (workOrder.PMOccurrenceId.HasValue)
            {
                var occurrence = await _db.PMOccurrences
                    .FirstOrDefaultAsync(o => o.Id == workOrder.PMOccurrenceId.Value);
                if (occurrence != null && occurrence.Status != PMOccurrenceStatus.Completed)
                {
                    occurrence.Status = PMOccurrenceStatus.Completed;
                }
            }

            var auditEntry = new AuditLog
            {
                EntityType = "MaintenanceEvent",
                EntityId = workOrderId,
                Action = SmartAssistConstants.CloseoutAuditAction,
                Username = username,
                Timestamp = DateTime.UtcNow,
                Description = $"Closeout summary generated for Work Order {workOrder.WorkOrderNumber}" + (allowIncompleteOperations ? " [override: incomplete ops allowed]" : ""),
                AfterJson = JsonSerializer.Serialize(new
                {
                    WorkOrderId = workOrderId,
                    WorkOrderNumber = workOrder.WorkOrderNumber,
                    SummaryLength = summary.Length,
                    OperationsCount = operations.Count,
                    IncompleteOpsCount = incompleteOps.Count,
                    AllowIncompleteOperations = allowIncompleteOperations,
                    HasLessonsLearned = !string.IsNullOrEmpty(lessonsLearned)
                })
            };
            _db.AuditLogs.Add(auditEntry);

            await _db.SaveChangesAsync();

            var siteId = workOrder.Asset?.LocationRef?.SiteId;

            // S1-3: route the just-closed WO into CipAutoCostPostingService
            // when the WO carries a CipProjectId. The service is null in
            // tests that don't pass it (the optional ctor parameter); skip
            // gracefully. Idempotent on retry; failure is logged and does
            // not roll back the close.
            if (_cipAutoCostPosting != null)
            {
                try
                {
                    await _cipAutoCostPosting.PostFromWorkOrderAsync(workOrderId);
                }
                catch (Exception cipEx)
                {
                    _logger.LogError(cipEx,
                        "CIP auto-cost posting failed for closed work order {WorkOrderId}",
                        workOrderId);
                }
            }

            await _outbox.EnqueueAsync(
                companyId,
                siteId,
                new WorkOrderClosedV1(
                    WorkOrderId: workOrderId,
                    WorkOrderNumber: workOrder.WorkOrderNumber,
                    Status: workOrder.Status.ToString(),
                    AssetId: workOrder.AssetId,
                    ClosedAt: workOrder.ClosedAt,
                    ClosedBy: username),
                correlationId: $"closeout-{workOrderId}"
            );

            await _outbox.EnqueueAsync(
                companyId,
                siteId,
                new CloseoutSummaryGeneratedV1(
                    WorkOrderId: workOrderId,
                    WorkOrderNumber: workOrder.WorkOrderNumber,
                    SummaryLength: summary.Length,
                    OperationsCount: operations.Count,
                    HasLessonsLearned: !string.IsNullOrEmpty(lessonsLearned)),
                correlationId: $"closeout-{workOrderId}"
            );

            result.ResolutionSummary = summary;
            result.AuditLogId = auditEntry.Id;
            result.Success = true;

            _logger.LogInformation("Work Order {WorkOrderId} closed with summary", workOrderId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close Work Order {WorkOrderId}", workOrderId);
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<LessonSaveResult> SaveLessonAsync(int workOrderId, string lessonText, string? tags, string username)
    {
        var result = new LessonSaveResult();
        var companyId = GetCompanyId();

        try
        {
            var workOrder = await _db.MaintenanceEvents
                .Include(m => m.Asset)
                .ThenInclude(a => a!.LocationRef)
                .Where(m => m.Asset != null && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
                .FirstOrDefaultAsync(m => m.Id == workOrderId);

            if (workOrder == null)
            {
                result.Success = false;
                result.Error = $"Work Order {workOrderId} not found";
                return result;
            }

            var siteId = workOrder.Asset?.LocationRef?.SiteId;

            var lesson = new LessonLearned
            {
                CompanyId = companyId,
                SiteId = siteId,
                AssetCategoryId = workOrder.Asset?.AssetCategoryId,
                Tags = tags,
                Text = lessonText,
                Title = $"Lesson from WO {workOrder.WorkOrderNumber}",
                SourceWorkOrderId = workOrderId,
                FailureCode = workOrder.FailureCode,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };

            _db.LessonsLearned.Add(lesson);

            var auditEntry = new AuditLog
            {
                EntityType = "LessonLearned",
                EntityId = 0,
                Action = SmartAssistConstants.LessonSavedAuditAction,
                Username = username,
                Timestamp = DateTime.UtcNow,
                Description = $"Lesson saved from Work Order {workOrder.WorkOrderNumber}",
                AfterJson = JsonSerializer.Serialize(new
                {
                    SourceWorkOrderId = workOrderId,
                    WorkOrderNumber = workOrder.WorkOrderNumber,
                    LessonTextLength = lessonText.Length,
                    Tags = tags
                })
            };
            _db.AuditLogs.Add(auditEntry);

            await _db.SaveChangesAsync();

            auditEntry.EntityId = lesson.Id;
            await _db.SaveChangesAsync();

            await _outbox.EnqueueAsync(
                companyId,
                siteId,
                new LessonSavedV1(
                    LessonId: lesson.Id,
                    SourceWorkOrderId: workOrderId,
                    WorkOrderNumber: workOrder.WorkOrderNumber,
                    FailureCode: workOrder.FailureCode,
                    Tags: tags,
                    CreatedBy: username),
                correlationId: $"lesson-{lesson.Id}"
            );

            result.LessonId = lesson.Id;
            result.AuditLogId = auditEntry.Id;
            result.Success = true;

            _logger.LogInformation("Lesson {LessonId} saved from Work Order {WorkOrderId}", lesson.Id, workOrderId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save lesson from Work Order {WorkOrderId}", workOrderId);
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<List<RecurringFailure>> GetRecurringFailuresAsync(int days = 30, int limit = 5)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        var companyId = GetCompanyId();

        var failures = await _db.MaintenanceEvents
            .Include(m => m.Asset)
            .Where(m => m.Asset != null && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0) &&
                        m.Status == MaintenanceStatus.Completed &&
                        m.CompletedDate >= cutoffDate &&
                        m.FailureCode != null &&
                        m.FailureCode != "")
            .GroupBy(m => new { m.FailureCode, m.AssetId })
            .Select(g => new
            {
                g.Key.FailureCode,
                g.Key.AssetId,
                Count = g.Count(),
                LastOccurrence = g.Max(m => m.CompletedDate)
            })
            .Where(g => g.Count >= 2)
            .OrderByDescending(g => g.Count)
            .Take(limit)
            .ToListAsync();

        var assetIds = failures.Select(f => f.AssetId).Distinct().ToList();
        var assets = await _db.Assets
            .Where(a => assetIds.Contains(a.Id) && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0))
            .Include(a => a.LocationRef)
            .ThenInclude(l => l!.Site)
            .ToDictionaryAsync(a => a.Id);

        return failures.Select(f =>
        {
            var asset = assets.GetValueOrDefault(f.AssetId);
            return new RecurringFailure
            {
                FailureCode = f.FailureCode ?? "",
                AssetId = f.AssetId,
                AssetNumber = asset?.AssetNumber,
                AssetDescription = asset?.Description,
                SiteId = asset?.LocationRef?.SiteId ?? 0,
                SiteName = asset?.LocationRef?.Site?.Name,
                Count = f.Count,
                LastOccurrence = f.LastOccurrence
            };
        }).ToList();
    }
}
