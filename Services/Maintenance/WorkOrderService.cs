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
using System.Collections.Generic;
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
    private readonly IGlAccountResolver _glResolver;
    private readonly ILogger<WorkOrderService> _logger;

    public WorkOrderService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILookupService lookupService,
        IGlAccountResolver glResolver,
        ILogger<WorkOrderService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lookupService = lookupService;
        _glResolver = glResolver;
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

    // === Phase 2 (Sprint 12.9 PR #3.1) — operation-level JE-posting writes ===

    public async Task<Result<WorkOrderOperation>> AddLaborAsync(
        AddLaborRequest request,
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

        var labor = new WorkOrderOperationLabor
        {
            WorkOrderOperationId = request.OperationId,
            TechnicianId = request.TechnicianId,
            WorkDate = DateTime.UtcNow.Date,
            Hours = request.Hours,
            HourlyRate = request.HourlyRate,
            Notes = request.Notes?.ToUpper(),
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkOrderOperationLabors.Add(labor);
        operation.ActualHours += request.Hours;

        // Post the GL impact of the labor entry. DR MaintenanceLabor / CR
        // AccruedLabor — see PR #92 + the legacy PostLaborJournalEntryAsync
        // helper this method now subsumes.
        await PostLaborJournalEntryAsync(operation, labor);

        await _db.SaveChangesAsync(ct);
        return Result.Success(operation);
    }

    public async Task<Result<WorkOrderOperationPart>> IssueOperationPartAsync(
        IssueOperationPartRequest request,
        CancellationToken ct)
    {
        var part = await _db.WorkOrderOperationParts
            .Include(p => p.WorkOrderOperation)
                .ThenInclude(op => op!.WorkOrder)
                    .ThenInclude(m => m!.Asset)
            .Where(p => p.Id == request.OperationPartId
                && p.WorkOrderOperation != null
                && p.WorkOrderOperation.WorkOrder != null
                && p.WorkOrderOperation.WorkOrder.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrderOperation.WorkOrder.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (part == null)
        {
            return Result.Failure<WorkOrderOperationPart>($"Operation part {request.OperationPartId} not found or not visible.");
        }

        if (request.QuantityIssue <= 0)
        {
            // Legacy behavior: silently no-op on non-positive quantities,
            // return the loaded part so the caller still has WorkOrderId for
            // the redirect.
            return Result.Success(part);
        }

        // Same planned-vs-unplanned rule as WO-level: bounded by planned
        // when planned > 0, otherwise extends planned to match (unplanned
        // pull). Keeps the operation part counters internally consistent.
        decimal actualIssue = request.QuantityIssue;
        if (part.QuantityPlanned > 0)
        {
            var maxIssuable = part.QuantityPlanned - part.QuantityIssued;
            if (maxIssuable <= 0)
            {
                return Result.Success(part);
            }
            actualIssue = Math.Min(request.QuantityIssue, maxIssuable);
        }
        else
        {
            part.QuantityPlanned += actualIssue;
        }

        part.QuantityIssued += actualIssue;
        part.QuantityUsed = part.QuantityIssued - part.QuantityReturned;
        part.IssuedAt = DateTime.UtcNow;
        part.IssuedBy = string.IsNullOrWhiteSpace(request.IssuedBy) ? "SYSTEM" : request.IssuedBy;

        await ApplyOperationPartMovementAsync(part, actualIssue, isIssue: true);
        await PostOperationPartJournalEntryAsync(part, actualIssue, isIssue: true);

        await _db.SaveChangesAsync(ct);
        return Result.Success(part);
    }

    public async Task<Result<WorkOrderOperationPart>> ReturnOperationPartAsync(
        ReturnOperationPartRequest request,
        CancellationToken ct)
    {
        var part = await _db.WorkOrderOperationParts
            .Include(p => p.WorkOrderOperation)
                .ThenInclude(op => op!.WorkOrder)
                    .ThenInclude(m => m!.Asset)
            .Where(p => p.Id == request.OperationPartId
                && p.WorkOrderOperation != null
                && p.WorkOrderOperation.WorkOrder != null
                && p.WorkOrderOperation.WorkOrder.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrderOperation.WorkOrder.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (part == null)
        {
            return Result.Failure<WorkOrderOperationPart>($"Operation part {request.OperationPartId} not found or not visible.");
        }

        if (request.QuantityReturn <= 0)
        {
            return Result.Success(part);
        }

        var maxReturnable = part.QuantityIssued - part.QuantityReturned;
        if (maxReturnable <= 0)
        {
            return Result.Success(part);
        }
        var actualReturn = Math.Min(request.QuantityReturn, maxReturnable);

        part.QuantityReturned += actualReturn;
        part.QuantityUsed = Math.Max(0m, part.QuantityIssued - part.QuantityReturned);

        await ApplyOperationPartMovementAsync(part, actualReturn, isIssue: false);
        await PostOperationPartJournalEntryAsync(part, actualReturn, isIssue: false);

        await _db.SaveChangesAsync(ct);
        return Result.Success(part);
    }

    // === Private helpers moved from Pages/WorkOrders/Details.cshtml.cs ===

    /// <summary>
    /// Posts the labor JE pair: DR MaintenanceLabor / CR AccruedLabor.
    /// Skipped when hours or rate is zero. Added to the ChangeTracker but
    /// not saved — caller's <c>SaveChangesAsync</c> flushes it alongside
    /// the labor row in a single transaction so they cannot diverge.
    /// </summary>
    private async Task<JournalEntry?> PostLaborJournalEntryAsync(
        WorkOrderOperation operation,
        WorkOrderOperationLabor labor)
    {
        if (labor.Hours <= 0m || labor.HourlyRate <= 0m) return null;
        var amount = labor.Hours * labor.HourlyRate;
        if (amount <= 0m) return null;

        var resolvedCompanyId = operation.WorkOrder?.Asset?.CompanyId
            ?? _tenantContext.CompanyId
            ?? 0;
        if (resolvedCompanyId == 0) return null;

        var ctx = new GlResolveContext(
            WorkOrderId: operation.WorkOrderId,
            AssetId: operation.WorkOrder?.AssetId);
        var laborAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.MaintenanceLabor, ctx);
        var accruedAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.AccruedLabor, ctx);

        var ticks = DateTime.UtcNow.Ticks;
        var jeReference = $"WO-LBR-{operation.WorkOrderId}-op{operation.Id}-{ticks}";
        var woNumber = operation.WorkOrder?.WorkOrderNumber ?? $"WO#{operation.WorkOrderId}";

        var je = new JournalEntry
        {
            BookId = null,
            Batch = jeReference,
            Period = int.Parse(DateTime.UtcNow.ToString("yyyyMM")),
            PostingDate = DateTime.UtcNow.Date,
            Source = "WO-LBR",
            Reference = jeReference,
            Description = $"Labor posted to {woNumber} (op {operation.Sequence}, {labor.Hours:0.00}h @ {labor.HourlyRate:C})",
            CreatedUtc = DateTime.UtcNow,
            Lines = new List<JournalLine>
            {
                new JournalLine
                {
                    LineNo = 1,
                    Account = laborAccount,
                    Description = $"Maintenance labor - {woNumber}",
                    Debit = amount,
                    Credit = 0m
                },
                new JournalLine
                {
                    LineNo = 2,
                    Account = accruedAccount,
                    Description = $"Accrued labor - {woNumber}",
                    Debit = 0m,
                    Credit = amount
                }
            }
        };
        _db.JournalEntries.Add(je);
        return je;
    }

    /// <summary>
    /// Operation-part inventory movement. Decrements (issue) or increments
    /// (return) <see cref="ItemInventory"/> at the part's
    /// <c>IssuedFromLocationId</c> and creates an <see cref="ItemTransaction"/>
    /// audit row. If <c>IssuedFromLocationId</c> is null we still write the
    /// transaction but can't update a specific inventory row — ops team
    /// reconciles later via cycle count.
    /// </summary>
    private async Task<decimal?> ApplyOperationPartMovementAsync(
        WorkOrderOperationPart part,
        decimal qty,
        bool isIssue)
    {
        var sign = isIssue ? -1m : 1m;
        var companyId = part.WorkOrderOperation?.WorkOrder?.Asset?.CompanyId ?? _tenantContext.CompanyId;
        decimal? newOnHand = null;

        if (part.IssuedFromLocationId.HasValue)
        {
            var inv = await _db.Set<ItemInventory>()
                .FirstOrDefaultAsync(i =>
                    i.ItemId == part.ItemId &&
                    i.LocationId == part.IssuedFromLocationId.Value &&
                    i.CompanyId == companyId);
            if (inv == null)
            {
                inv = new ItemInventory
                {
                    ItemId = part.ItemId,
                    LocationId = part.IssuedFromLocationId.Value,
                    CompanyId = companyId,
                    QuantityOnHand = sign * qty,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Set<ItemInventory>().Add(inv);
            }
            else
            {
                inv.QuantityOnHand += sign * qty;
                inv.UpdatedAt = DateTime.UtcNow;
                if (isIssue) inv.LastIssueDate = DateTime.UtcNow;
            }
            newOnHand = inv.QuantityOnHand;
        }

        var workOrderId = part.WorkOrderOperation?.WorkOrderId ?? 0;
        _db.Set<ItemTransaction>().Add(new ItemTransaction
        {
            TransactionNumber = $"WO{workOrderId}-OP{part.WorkOrderOperationId}-{(isIssue ? "ISS" : "RTN")}-{DateTime.UtcNow.Ticks}",
            ItemId = part.ItemId,
            Type = isIssue ? TransactionType.Issue : TransactionType.Return,
            Quantity = qty,
            UnitCost = part.UnitCost,
            FromLocationId = isIssue ? part.IssuedFromLocationId : null,
            ToLocationId = isIssue ? null : part.IssuedFromLocationId,
            LotNumber = part.LotNumber,
            SerialNumber = part.SerialNumber
        });
        return newOnHand;
    }

    /// <summary>
    /// Operation-part variant of the WO-header material JE. DR
    /// MaintenanceMaterials / CR Inventory on issue; reversed on return.
    /// Distinct <c>Source</c> code (<c>WO-ISS-OP</c> / <c>WO-RTN-OP</c>) so
    /// downstream reports can split operation-scoped moves from
    /// WO-header moves. Reference includes the WO id so the CloseoutService
    /// rollup catches both prefixes (<c>WO-ISS-{woId}-</c> +
    /// <c>WO-ISS-OP-{woId}-op{opId}-</c>).
    /// </summary>
    private async Task<JournalEntry?> PostOperationPartJournalEntryAsync(
        WorkOrderOperationPart part,
        decimal qty,
        bool isIssue)
    {
        if (qty <= 0m) return null;
        var amount = qty * part.UnitCost;
        if (amount <= 0m) return null;

        var workOrder = part.WorkOrderOperation?.WorkOrder;
        var resolvedCompanyId = workOrder?.Asset?.CompanyId
            ?? _tenantContext.CompanyId
            ?? 0;
        if (resolvedCompanyId == 0) return null;
        var workOrderId = workOrder?.Id ?? 0;
        var assetId = workOrder?.AssetId;

        var ctx = new GlResolveContext(WorkOrderId: workOrderId, AssetId: assetId);
        var materialsAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.MaintenanceMaterials, ctx);
        var inventoryAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.Inventory, ctx);

        var ticks = DateTime.UtcNow.Ticks;
        var src = isIssue ? "WO-ISS-OP" : "WO-RTN-OP";
        var jeReference = $"{src}-{workOrderId}-op{part.WorkOrderOperationId}-p{part.Id}-{ticks}";
        var woNumber = workOrder?.WorkOrderNumber ?? $"WO#{workOrderId}";
        var verb = isIssue ? "issued to" : "returned from";

        var drAccount = isIssue ? materialsAccount : inventoryAccount;
        var crAccount = isIssue ? inventoryAccount  : materialsAccount;

        var je = new JournalEntry
        {
            BookId = null,
            Batch = jeReference,
            Period = int.Parse(DateTime.UtcNow.ToString("yyyyMM")),
            PostingDate = DateTime.UtcNow.Date,
            Source = src,
            Reference = jeReference,
            Description = $"Operation-part materials {verb} {woNumber} op#{part.WorkOrderOperationId} (qty {qty} @ {part.UnitCost:C})",
            CreatedUtc = DateTime.UtcNow,
            Lines = new List<JournalLine>
            {
                new JournalLine
                {
                    LineNo = 1,
                    Account = drAccount,
                    Description = isIssue
                        ? $"Op-part materials - {woNumber}"
                        : $"Op-part inventory restored - {woNumber}",
                    Debit = amount,
                    Credit = 0m
                },
                new JournalLine
                {
                    LineNo = 2,
                    Account = crAccount,
                    Description = isIssue
                        ? $"Op-part inventory {verb} {woNumber}"
                        : $"Op-part materials reversed - {woNumber}",
                    Debit = 0m,
                    Credit = amount
                }
            }
        };
        _db.JournalEntries.Add(je);
        return je;
    }
}
