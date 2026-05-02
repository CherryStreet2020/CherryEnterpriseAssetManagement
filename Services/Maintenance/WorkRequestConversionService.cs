using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Webhooks;
using System.Text.Json;

namespace Abs.FixedAssets.Services.Maintenance;

public class ConversionResult
{
    public int WorkRequestId { get; set; }
    public int? WorkOrderId { get; set; }
    public string? WorkOrderNumber { get; set; }
    public string? WorkOrderDescription { get; set; }
    public MaintenanceStatus? WorkOrderStatus { get; set; }
    public int OperationCount { get; set; }
    public int? AuditLogId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public SmartAssistResult? AssistResult { get; set; }
}

public interface IWorkRequestConversionService
{
    Task<ConversionResult> ConvertWithSmartAssistAsync(int workRequestId, string username);
    Task<ConversionResult> ConvertWithSmartAssistAsync(WorkRequest workRequest, string username);
}

public class WorkRequestConversionService : IWorkRequestConversionService
{
    private readonly AppDbContext _db;
    private readonly ISmartAssistService _smartAssist;
    private readonly IOutboxWriter _outbox;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<WorkRequestConversionService> _logger;

    public WorkRequestConversionService(
        AppDbContext db,
        ISmartAssistService smartAssist,
        IOutboxWriter outbox,
        ITenantContext tenantContext,
        ILogger<WorkRequestConversionService> logger)
    {
        _db = db;
        _smartAssist = smartAssist;
        _outbox = outbox;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ConversionResult> ConvertWithSmartAssistAsync(int workRequestId, string username)
    {
        // === COMPANY-SCOPED RELOAD (Security First) ===
        var companyId = _tenantContext.CompanyId ?? 1;

        var workRequest = await _db.WorkRequests
            .Include(r => r.Site)
            .Include(r => r.Asset)
            .Include(r => r.GeneratedWorkOrder)
            .FirstOrDefaultAsync(r => r.Id == workRequestId && _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0));

        if (workRequest == null)
        {
            _logger.LogWarning(
                "WorkRequest {Id} not found or does not belong to Company {CompanyId}",
                workRequestId, companyId);

            return new ConversionResult
            {
                WorkRequestId = workRequestId,
                Success = false,
                Error = "Work request not found or access denied."
            };
        }

        return await ConvertInternalAsync(workRequest, username, companyId);
    }

    public async Task<ConversionResult> ConvertWithSmartAssistAsync(WorkRequest workRequest, string username)
    {
        // === COMPANY-SCOPED RELOAD (Do NOT trust passed object) ===
        var companyId = _tenantContext.CompanyId ?? 1;

        var reloadedRequest = await _db.WorkRequests
            .Include(r => r.Site)
            .Include(r => r.Asset)
            .Include(r => r.GeneratedWorkOrder)
            .FirstOrDefaultAsync(r => r.Id == workRequest.Id && _tenantContext.VisibleCompanyIds.Contains(r.CompanyId ?? 0));

        if (reloadedRequest == null)
        {
            _logger.LogWarning(
                "WorkRequest {Id} not found or does not belong to Company {CompanyId}",
                workRequest.Id, companyId);

            return new ConversionResult
            {
                WorkRequestId = workRequest.Id,
                Success = false,
                Error = "Work request not found or access denied."
            };
        }

        return await ConvertInternalAsync(reloadedRequest, username, companyId);
    }

    private async Task<ConversionResult> ConvertInternalAsync(WorkRequest workRequest, string username, int companyId)
    {
        var result = new ConversionResult
        {
            WorkRequestId = workRequest.Id
        };

        // === IDEMPOTENCY CHECK ===
        if (workRequest.GeneratedWorkOrderId.HasValue)
        {
            // Company-safe WO lookup: MaintenanceEvents doesn't have CompanyId,
            // so we verify via the linked Asset's company
            var existingWo = await _db.MaintenanceEvents
                .Include(w => w.Asset)
                .FirstOrDefaultAsync(w => w.Id == workRequest.GeneratedWorkOrderId.Value);

            if (existingWo != null)
            {
                // Verify ownership via Asset's company (if Asset exists)
                var woCompanyId = existingWo.Asset?.CompanyId;

                if (woCompanyId.HasValue && woCompanyId.Value != companyId)
                {
                    _logger.LogWarning(
                        "Company mismatch on existing WO: WorkOrder {WoId} belongs to Company {WoCompany}, context is {CtxCompany}",
                        existingWo.Id, woCompanyId.Value, companyId);

                    return new ConversionResult
                    {
                        WorkRequestId = workRequest.Id,
                        Success = false,
                        Error = "Access denied: Linked work order belongs to a different company."
                    };
                }

                _logger.LogInformation(
                    "Idempotency: WorkRequest {RequestId} already converted to WorkOrder {WorkOrderId}",
                    workRequest.Id, existingWo.Id);

                return new ConversionResult
                {
                    WorkRequestId = workRequest.Id,
                    WorkOrderId = existingWo.Id,
                    WorkOrderNumber = existingWo.WorkOrderNumber,
                    WorkOrderDescription = existingWo.Description,
                    WorkOrderStatus = existingWo.Status,
                    Success = true
                };
            }
        }

        // === BROKEN STATE CHECK ===
        if (workRequest.Status == WorkRequestStatus.ConvertedToWO && !workRequest.GeneratedWorkOrderId.HasValue)
        {
            _logger.LogError(
                "Data integrity issue: WorkRequest {RequestId} has Status=ConvertedToWO but GeneratedWorkOrderId is null",
                workRequest.Id);

            return new ConversionResult
            {
                WorkRequestId = workRequest.Id,
                Success = false,
                Error = "Work request is marked converted but has no linked work order. Contact admin."
            };
        }

        // === STATUS GATING ===
        if (workRequest.Status == WorkRequestStatus.Rejected)
        {
            return new ConversionResult
            {
                WorkRequestId = workRequest.Id,
                Success = false,
                Error = "Cannot convert a rejected work request."
            };
        }

        try
        {
            var assistResult = await _smartAssist.AnalyzeRequestAsync(workRequest, workRequest.SiteId);
            result.AssistResult = assistResult;

            if (assistResult.SuggestedAssetId == null)
            {
                if (workRequest.AssetId.HasValue)
                {
                    assistResult.SuggestedAssetId = workRequest.AssetId.Value;
                    var asset = await _db.Assets.Where(a => a.Id == workRequest.AssetId.Value).FirstOrDefaultAsync();
                    assistResult.SuggestedAssetName = asset?.Description;
                }
                else
                {
                    result.Success = false;
                    result.Error = "Could not determine asset. Please select one manually.";
                    return result;
                }
            }

            // === ATOMIC TRANSACTION (Ambient-Safe for Smoke Tests) ===
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? localTx = null;
            var hasAmbientEf = _db.Database.CurrentTransaction != null;
            if (!hasAmbientEf)
            {
                try
                {
                    localTx = await _db.Database.BeginTransactionAsync();
                }
                catch (Exception ex) when (
                    ex.Message.Contains("already in a transaction", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("cannot participate in another transaction", StringComparison.OrdinalIgnoreCase))
                {
                    // Ambient transaction exists outside EF tracking (smoke test harness / shared connection).
                    // Npgsql may throw NpgsqlOperationInProgressException or InvalidOperationException.
                    // Proceed without creating a local transaction.
                    localTx = null;
                    _logger.LogDebug(ex, "Ambient transaction detected outside EF; skipping local BeginTransaction.");
                }
            }

            try
            {
                var woNumber = await GenerateWorkOrderNumberAsync();

                var workOrder = new MaintenanceEvent
                {
                    AssetId = assistResult.SuggestedAssetId.Value,
                    Type = MaintenanceType.Corrective,
                    Description = (SmartAssistConstants.DraftPrefix + workRequest.RequestText).Truncate(200),
                    ScheduledDate = DateTime.UtcNow.Date,
                    Status = MaintenanceStatus.Scheduled,
                    Priority = assistResult.SuggestedPriority switch
                    {
                        WorkRequestPriority.Emergency => MaintenancePriority.Critical,
                        WorkRequestPriority.Critical => MaintenancePriority.Critical,
                        WorkRequestPriority.High => MaintenancePriority.High,
                        WorkRequestPriority.Medium => MaintenancePriority.Medium,
                        _ => MaintenancePriority.Low
                    },
                    LaborHours = assistResult.EstimatedLaborHours,
                    WorkOrderNumber = woNumber,
                    FailureCode = assistResult.SuggestedFailureCode,
                    CorrectiveAction = assistResult.SuggestedActionCode,
                    Notes = $"Generated from Work Request #{workRequest.RequestNumber}\n\n{assistResult.Explanation}",
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow,
                    RequestedById = null,
                    RequestedAt = workRequest.RequestedAt,
                    ApprovalStatus = WorkOrderApprovalStatus.PendingApproval
                };

                _db.MaintenanceEvents.Add(workOrder);

                var seq = 10;
                var operations = new List<WorkOrderOperation>();
                foreach (var taskDesc in assistResult.SuggestedTasks)
                {
                    var operation = new WorkOrderOperation
                    {
                        MaintenanceEvent = workOrder,
                        OperationNumber = $"OP{seq:D3}",
                        Sequence = seq,
                        Type = OperationType.Inspection,
                        Title = taskDesc.Truncate(200),
                        Status = OperationStatus.Pending,
                        PlannedHours = Math.Round(assistResult.EstimatedLaborHours / assistResult.SuggestedTasks.Count, 2),
                        CreatedBy = username,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.WorkOrderOperations.Add(operation);
                    operations.Add(operation);
                    seq += 10;
                }

                workRequest.Status = WorkRequestStatus.ConvertedToWO;
                workRequest.IsAIAssisted = true;
                workRequest.AIConfidence = assistResult.Confidence;
                workRequest.AIExplanation = assistResult.Explanation;
                workRequest.ModifiedAt = DateTime.UtcNow;
                workRequest.ModifiedBy = username;

                await _db.SaveChangesAsync();

                workRequest.GeneratedWorkOrderId = workOrder.Id;

                var auditEntry = new AuditLog
                {
                    EntityType = "WorkRequest",
                    EntityId = workRequest.Id,
                    Action = SmartAssistConstants.AuditAction,
                    Username = username,
                    Timestamp = DateTime.UtcNow,
                    Description = $"Smart Assist generated Work Order {woNumber} from Request {workRequest.RequestNumber}",
                    AfterJson = JsonSerializer.Serialize(new
                    {
                        WorkRequestId = workRequest.Id,
                        WorkOrderId = workOrder.Id,
                        WorkOrderNumber = workOrder.WorkOrderNumber,
                        Confidence = assistResult.Confidence,
                        UsedAI = assistResult.UsedAI,
                        FactorsCount = assistResult.FactorsUsed.Count,
                        TasksGenerated = assistResult.SuggestedTasks.Count
                    })
                };
                _db.AuditLogs.Add(auditEntry);

                await _outbox.EnqueueAsync(
                    companyId,
                    workRequest.SiteId,
                    WebhookEventTypes.WorkOrderCreated,
                    "MaintenanceEvent",
                    workOrder.Id.ToString(),
                    new
                    {
                        WorkOrderId = workOrder.Id,
                        WorkOrderNumber = workOrder.WorkOrderNumber,
                        Status = workOrder.Status.ToString(),
                        Priority = workOrder.Priority.ToString(),
                        AssetId = workOrder.AssetId,
                        SourceWorkRequestId = workRequest.Id,
                        OperationCount = operations.Count,
                        CreatedAt = workOrder.CreatedAt
                    },
                    $"workrequest-{workRequest.Id}"
                );

                await _db.SaveChangesAsync();

                if (localTx != null)
                {
                    await localTx.CommitAsync();
                }

                result.WorkOrderId = workOrder.Id;
                result.WorkOrderNumber = workOrder.WorkOrderNumber;
                result.WorkOrderDescription = workOrder.Description;
                result.WorkOrderStatus = workOrder.Status;
                result.OperationCount = operations.Count;
                result.AuditLogId = auditEntry.Id;
                result.Success = true;

                _logger.LogInformation(
                    "Smart Assist converted WorkRequest {RequestId} to WorkOrder {WorkOrderId} with {OperationCount} operations",
                    workRequest.Id, workOrder.Id, operations.Count);

                return result;
            }
            catch
            {
                if (localTx != null)
                {
                    await localTx.RollbackAsync();
                }
                throw;
            }
            finally
            {
                if (localTx != null)
                {
                    await localTx.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Smart Assist conversion failed for WorkRequest {Id}", workRequest.Id);
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    private async Task<string> GenerateWorkOrderNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"WO-{today:yyyyMM}-";
        var count = await _db.MaintenanceEvents
            .CountAsync(e => e.WorkOrderNumber != null && e.WorkOrderNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }
}

internal static class StringTruncateExtension
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
