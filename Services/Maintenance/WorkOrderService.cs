// ADR-025 D5 — WorkOrderService implementation (Sprint 12.9 PR #3).
//
// First 5 of 17 write paths extracted from Pages/WorkOrders/Details.cshtml.cs
// to a service-layer class. Each method preserves the EXACT legacy logic
// that previously lived inline in the PageModel — same tenant scoping,
// same field-uppercasing convention, same status-side-effect rules.
//
// What's intentionally NOT here (queued for PR #3.1 → #3.3):
//   - AddLaborAsync          (involves JE posting via MaintenanceLabor / AccruedLabor)
//   - AddOperationPartAsync  (Sprint 12.9 PR #3.2)
//   - IssueOperationPartAsync, ReturnOperationPartAsync (JE + inventory)
//   - IssueMaterialAsync, ReturnMaterialAsync, RemovePlannedMaterialAsync,
//     LoadTemplateMaterialsAsync (JE + inventory)
//   - EditWorkOrderAsync, DispatchUpdateAsync, CapitalizeAsync (WO-level)
//
// Until PR #3.3 ships, Pages/WorkOrders/Details.cshtml.cs stays in
// Analyzers/ControlPlaneAllowlist.txt (still has 12 direct writes).

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Maintenance;

public sealed class WorkOrderService : IWorkOrderService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILookupService _lookupService;
    private readonly ILogger<WorkOrderService> _logger;

    public WorkOrderService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILookupService lookupService,
        ILogger<WorkOrderService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lookupService = lookupService;
        _logger = logger;
    }

    public async Task<Result<WorkOrderOperation>> AddOperationAsync(
        AddOperationRequest request,
        CancellationToken ct)
    {
        // Verify the work order belongs to a visible company before any write.
        var woVisible = await _db.WorkOrders
            .Include(w => w.Asset)
            .Where(w => w.Id == request.WorkOrderId)
            .Where(w => w.Asset != null && _tenantContext.VisibleCompanyIds.Contains(w.Asset.CompanyId ?? 0))
            .AnyAsync(ct);
        if (!woVisible)
        {
            return Result.Failure<WorkOrderOperation>($"Work order {request.WorkOrderId} not found or not visible to current tenant.");
        }

        var maxSeq = await _db.WorkOrderOperations
            .Where(o => o.WorkOrderId == request.WorkOrderId)
            .MaxAsync(o => (int?)o.Sequence, ct) ?? 0;

        var nextNum = await _db.WorkOrderOperations
            .Where(o => o.WorkOrderId == request.WorkOrderId)
            .CountAsync(ct) + 1;

        var resolvedType = OperationType.Mechanical;
        int? resolvedTypeLvId = request.TypeLookupValueId > 0 ? request.TypeLookupValueId : (int?)null;
        var typeLv = await _lookupService.GetValueByIdAsync(null, null, request.TypeLookupValueId);
        if (typeLv != null)
        {
            resolvedTypeLvId = typeLv.Id;
            if (int.TryParse(typeLv.Code, out var enumVal))
                resolvedType = (OperationType)enumVal;
        }

        var operation = new WorkOrderOperation
        {
            WorkOrderId = request.WorkOrderId,
            OperationNumber = $"OP-{nextNum:D3}",
            Sequence = maxSeq + 10,
            Title = request.Title?.ToUpper() ?? "NEW OPERATION",
            Type = resolvedType,
            TypeLookupValueId = resolvedTypeLvId,
            CraftId = request.CraftId,
            PlannedHours = request.PlannedHours,
            Description = request.Description?.ToUpper(),
            Status = OperationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkOrderOperations.Add(operation);
        await _db.SaveChangesAsync(ct);

        return Result.Success(operation);
    }

    public async Task<Result<WorkOrderOperation>> MoveOperationAsync(
        MoveOperationRequest request,
        CancellationToken ct)
    {
        var operation = await _db.WorkOrderOperations
            .Include(o => o.WorkOrder).ThenInclude(m => m!.Asset)
            .Where(o => o.Id == request.OperationId
                && o.WorkOrder != null
                && o.WorkOrder.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(o.WorkOrder.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (operation == null)
        {
            return Result.Failure<WorkOrderOperation>($"Operation {request.OperationId} not found or not visible.");
        }

        var allOps = await _db.WorkOrderOperations
            .Where(o => o.WorkOrderId == operation.WorkOrderId)
            .OrderBy(o => o.Sequence)
            .ToListAsync(ct);

        var idx = allOps.FindIndex(o => o.Id == request.OperationId);
        if (request.Direction == "up" && idx > 0)
        {
            (allOps[idx].Sequence, allOps[idx - 1].Sequence) = (allOps[idx - 1].Sequence, allOps[idx].Sequence);
        }
        else if (request.Direction == "down" && idx >= 0 && idx < allOps.Count - 1)
        {
            (allOps[idx].Sequence, allOps[idx + 1].Sequence) = (allOps[idx + 1].Sequence, allOps[idx].Sequence);
        }
        // Direction values outside {"up", "down"} are silently ignored, matching
        // the legacy handler's behavior (no validation, just a no-op SaveChanges).

        await _db.SaveChangesAsync(ct);
        return Result.Success(operation);
    }

    public async Task<Result<WorkOrderOperation>> UpdateOperationStatusAsync(
        UpdateOperationStatusRequest request,
        CancellationToken ct)
    {
        var operation = await _db.WorkOrderOperations
            .Include(o => o.WorkOrder).ThenInclude(m => m!.Asset)
            .Where(o => o.Id == request.OperationId
                && o.WorkOrder != null
                && o.WorkOrder.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(o.WorkOrder.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (operation == null)
        {
            return Result.Failure<WorkOrderOperation>($"Operation {request.OperationId} not found or not visible.");
        }

        var resolvedStatus = OperationStatus.Pending;
        int? resolvedStatusLvId = request.StatusLookupValueId > 0 ? request.StatusLookupValueId : (int?)null;
        var statusLv = await _lookupService.GetValueByIdAsync(null, null, request.StatusLookupValueId);
        if (statusLv != null)
        {
            resolvedStatusLvId = statusLv.Id;
            if (int.TryParse(statusLv.Code, out var enumVal))
                resolvedStatus = (OperationStatus)enumVal;
        }

        operation.Status = resolvedStatus;
        operation.StatusLookupValueId = resolvedStatusLvId;
        if (resolvedStatus == OperationStatus.InProgress && operation.ActualStartDate == null)
            operation.ActualStartDate = DateTime.UtcNow;
        if (resolvedStatus == OperationStatus.Completed)
        {
            operation.ActualEndDate = DateTime.UtcNow;
            operation.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(operation);
    }

    public async Task<Result<WorkOrderOperation>> AddOperationToolAsync(
        AddOperationToolRequest request,
        CancellationToken ct)
    {
        var operation = await _db.WorkOrderOperations
            .Include(o => o.WorkOrder).ThenInclude(m => m!.Asset)
            .Where(o => o.Id == request.OperationId
                && o.WorkOrder != null
                && o.WorkOrder.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(o.WorkOrder.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (operation == null)
        {
            return Result.Failure<WorkOrderOperation>($"Operation {request.OperationId} not found or not visible.");
        }

        var tool = new WorkOrderOperationTool
        {
            WorkOrderOperationId = request.OperationId,
            ToolName = request.ToolName?.ToUpper() ?? "TOOL",
            QuantityRequired = request.QuantityRequired,
            Notes = request.Notes?.ToUpper(),
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkOrderOperationTools.Add(tool);
        await _db.SaveChangesAsync(ct);
        return Result.Success(operation);
    }

    public async Task<Result<WorkOrderPart>> AddPlannedMaterialAsync(
        AddPlannedMaterialRequest request,
        CancellationToken ct)
    {
        // S1-7: tenant-scope the item lookup. Same shape as the page's
        // main Items query — items can be company-scoped or global (null).
        var item = await _db.Items
            .Where(i => i.Id == request.ItemId
                && (i.CompanyId == null
                    || _tenantContext.VisibleCompanyIds.Contains(i.CompanyId.Value)))
            .FirstOrDefaultAsync(ct);
        if (item == null)
        {
            return Result.Failure<WorkOrderPart>($"Item {request.ItemId} not found or not visible to current tenant.");
        }

        var part = new WorkOrderPart
        {
            WorkOrderId = request.WorkOrderId,
            ItemId = request.ItemId,
            QuantityPlanned = request.QuantityPlanned,
            UnitCost = item.StandardCost,
            Notes = request.Notes?.ToUpper(),
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkOrderParts.Add(part);
        await _db.SaveChangesAsync(ct);
        return Result.Success(part);
    }
}
