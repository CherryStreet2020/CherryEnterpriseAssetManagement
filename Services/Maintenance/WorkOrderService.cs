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
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Maintenance;

public sealed class WorkOrderService : IWorkOrderService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILookupService _lookupService;
    private readonly IGlAccountResolver _glResolver;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<WorkOrderService> _logger;

    public WorkOrderService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILookupService lookupService,
        IGlAccountResolver glResolver,
        IOutboxWriter outbox,
        ILogger<WorkOrderService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lookupService = lookupService;
        _glResolver = glResolver;
        _outbox = outbox;
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

    // === Phase 3 (Sprint 12.9 PR #3.2) — WO-header material writes ===

    public async Task<Result<WorkOrderOperation>> AddOperationPartAsync(
        AddOperationPartRequest request,
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

        var part = new WorkOrderOperationPart
        {
            WorkOrderOperationId = request.OperationId,
            ItemId = request.ItemId,
            QuantityPlanned = request.QuantityPlanned,
            UnitCost = request.UnitCost,
            Notes = request.Notes?.ToUpper(),
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkOrderOperationParts.Add(part);
        await _db.SaveChangesAsync(ct);
        return Result.Success(operation);
    }

    public async Task<Result<WorkOrderPart>> IssueMaterialAsync(
        IssueMaterialRequest request,
        CancellationToken ct)
    {
        var companyId = _tenantContext.CompanyId ?? 1;
        var part = await _db.WorkOrderParts
            .Include(p => p.WorkOrder).ThenInclude(m => m!.Asset)
            .Where(p => p.Id == request.WorkOrderPartId
                && p.WorkOrder != null
                && p.WorkOrder.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrder.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (part == null)
        {
            return Result.Failure<WorkOrderPart>($"Work order part {request.WorkOrderPartId} not found or not visible.");
        }

        if (request.QuantityIssue <= 0)
        {
            return Result.Success(part);
        }

        // For planned materials: cannot issue more than (planned - already issued).
        // For unplanned: allow any quantity (auto-extends planned).
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
        part.IssuedDate = DateTime.UtcNow;
        part.IssuedBy = string.IsNullOrWhiteSpace(request.IssuedBy) ? "SYSTEM" : request.IssuedBy;
        part.UpdatedAt = DateTime.UtcNow;

        var newOnHand = await ApplyItemMovementAsync(part, actualIssue, isIssue: true);
        await PostMaterialMovementJournalEntryAsync(part, actualIssue, isIssue: true);

        await _db.SaveChangesAsync(ct);

        // Outbox event — published in the same lifecycle as the DB write so
        // downstream consumers (item-movement reporting, voice-AI cache
        // invalidation) see a consistent view.
        await _outbox.EnqueueAsync(
            part.WorkOrder?.Asset?.CompanyId ?? companyId,
            siteId: null,
            new ItemIssuedV1(
                ItemId: part.ItemId,
                LocationId: part.IssuedFromLocationId,
                CompanyId: part.WorkOrder?.Asset?.CompanyId,
                WorkOrderId: part.WorkOrderId,
                WorkOrderPartId: part.Id,
                WorkOrderNumber: part.WorkOrder?.WorkOrderNumber ?? string.Empty,
                AssetId: part.WorkOrder?.AssetId,
                Quantity: actualIssue,
                UnitCost: part.UnitCost,
                NewQuantityOnHand: newOnHand,
                LotNumber: part.LotNumber,
                SerialNumber: part.SerialNumber,
                IssuedBy: part.IssuedBy,
                IssuedAt: part.IssuedDate ?? DateTime.UtcNow),
            correlationId: $"item-issue-wo{part.WorkOrderId}-p{part.Id}-{DateTime.UtcNow.Ticks}"
        );

        return Result.Success(part);
    }

    public async Task<Result<WorkOrderPart>> ReturnMaterialAsync(
        ReturnMaterialRequest request,
        CancellationToken ct)
    {
        var part = await _db.WorkOrderParts
            .Include(p => p.WorkOrder).ThenInclude(m => m!.Asset)
            .Where(p => p.Id == request.WorkOrderPartId
                && p.WorkOrder != null
                && p.WorkOrder.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrder.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (part == null)
        {
            return Result.Failure<WorkOrderPart>($"Work order part {request.WorkOrderPartId} not found or not visible.");
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
        part.QuantityUsed = Math.Max(0, part.QuantityIssued - part.QuantityReturned);
        part.UpdatedAt = DateTime.UtcNow;

        await ApplyItemMovementAsync(part, actualReturn, isIssue: false);
        await PostMaterialMovementJournalEntryAsync(part, actualReturn, isIssue: false);

        await _db.SaveChangesAsync(ct);
        return Result.Success(part);
    }

    public async Task<Result<int>> RemovePlannedMaterialAsync(
        RemovePlannedMaterialRequest request,
        CancellationToken ct)
    {
        var part = await _db.WorkOrderParts
            .Include(p => p.WorkOrder).ThenInclude(m => m!.Asset)
            .Where(p => p.Id == request.WorkOrderPartId
                && p.WorkOrder != null
                && p.WorkOrder.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(p.WorkOrder.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (part == null)
        {
            return Result.Failure<int>($"Work order part {request.WorkOrderPartId} not found or not visible.");
        }

        // Guardrail: cannot remove if already issued — must return first.
        if (part.QuantityIssued > 0)
        {
            // Bit-identical legacy behavior: silently redirect without removing.
            return Result.Success(part.WorkOrderId);
        }

        var woId = part.WorkOrderId;
        _db.WorkOrderParts.Remove(part);
        await _db.SaveChangesAsync(ct);
        return Result.Success(woId);
    }

    public async Task<Result<LoadTemplateMaterialsOutcome>> LoadTemplateMaterialsAsync(
        LoadTemplateMaterialsRequest request,
        CancellationToken ct)
    {
        var wo = await _db.WorkOrders
            .Include(m => m.Asset)
            .Where(m => m.Id == request.WorkOrderId
                && m.Asset != null
                && _tenantContext.VisibleCompanyIds.Contains(m.Asset.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (wo == null)
        {
            return Result.Failure<LoadTemplateMaterialsOutcome>($"Work order {request.WorkOrderId} not found or not visible.");
        }

        // S1-2: prefer PMTemplateAssetId FK; fall back to legacy CustomField1
        // marker for in-flight rows that predate the migration.
        int? pmtaId = wo.PMTemplateAssetId;
        if (pmtaId == null
            && !string.IsNullOrEmpty(wo.CustomField1)
            && wo.CustomField1.StartsWith("PMTA:"))
        {
            int.TryParse(wo.CustomField1.Substring(5), out var parsed);
            pmtaId = parsed > 0 ? parsed : null;
        }

        if (pmtaId == null)
        {
            return Result.Success(new LoadTemplateMaterialsOutcome(
                WorkOrderId: request.WorkOrderId,
                Added: 0,
                Status: LoadTemplateMaterialsStatus.NoTemplate,
                Message: "No PM Template linked to this Work Order."));
        }

        var pmta = await _db.PMTemplateAssets
            .Include(a => a.PMTemplate)
                .ThenInclude(t => t!.Items!)
                    .ThenInclude(i => i.Item)
            .FirstOrDefaultAsync(a => a.Id == pmtaId, ct);

        if (pmta?.PMTemplate?.Items == null || !pmta.PMTemplate.Items.Any())
        {
            return Result.Success(new LoadTemplateMaterialsOutcome(
                WorkOrderId: request.WorkOrderId,
                Added: 0,
                Status: LoadTemplateMaterialsStatus.EmptyTemplate,
                Message: "PM Template has no materials defined."));
        }

        var existingItemIds = await _db.WorkOrderParts
            .Where(p => p.WorkOrderId == request.WorkOrderId)
            .Select(p => p.ItemId)
            .ToListAsync(ct);

        int added = 0;
        foreach (var templateItem in pmta.PMTemplate.Items)
        {
            if (existingItemIds.Contains(templateItem.ItemId)) continue;

            var woPart = new WorkOrderPart
            {
                WorkOrderId = request.WorkOrderId,
                ItemId = templateItem.ItemId,
                QuantityPlanned = templateItem.Quantity,
                UnitCost = templateItem.Item?.StandardCost ?? 0,
                Notes = templateItem.Notes?.ToUpper(),
                CreatedAt = DateTime.UtcNow
            };
            _db.WorkOrderParts.Add(woPart);
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            return Result.Success(new LoadTemplateMaterialsOutcome(
                WorkOrderId: request.WorkOrderId,
                Added: added,
                Status: LoadTemplateMaterialsStatus.Loaded,
                Message: $"Loaded {added} material(s) from PM Template."));
        }

        return Result.Success(new LoadTemplateMaterialsOutcome(
            WorkOrderId: request.WorkOrderId,
            Added: 0,
            Status: LoadTemplateMaterialsStatus.AllAlreadyExist,
            Message: "All template materials already exist on this Work Order."));
    }

    // === Private helpers moved from Pages/WorkOrders/Details.cshtml.cs (PR #3.2) ===

    /// <summary>
    /// Applies an inventory movement for a <see cref="WorkOrderPart"/>
    /// issue or return. Decrements (issue) or increments (return)
    /// <see cref="ItemInventory"/> at the part's
    /// <c>IssuedFromLocationId</c> and creates an
    /// <see cref="ItemTransaction"/> audit row.
    /// </summary>
    private async Task<decimal?> ApplyItemMovementAsync(
        WorkOrderPart part,
        decimal qty,
        bool isIssue)
    {
        var sign = isIssue ? -1m : 1m;
        var companyId = part.WorkOrder?.Asset?.CompanyId ?? _tenantContext.CompanyId;
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

        var txn = new ItemTransaction
        {
            TransactionNumber = $"WO{part.WorkOrderId}-{(isIssue ? "ISS" : "RTN")}-{DateTime.UtcNow.Ticks}",
            ItemId = part.ItemId,
            Type = isIssue ? TransactionType.Issue : TransactionType.Return,
            Quantity = qty,
            UnitCost = part.UnitCost,
            FromLocationId = isIssue ? part.IssuedFromLocationId : null,
            ToLocationId = isIssue ? null : part.IssuedFromLocationId,
            LotNumber = part.LotNumber,
            SerialNumber = part.SerialNumber
        };
        _db.Set<ItemTransaction>().Add(txn);

        return newOnHand;
    }

    /// <summary>
    /// Posts the WO-header material JE pair: DR MaintenanceMaterials /
    /// CR Inventory on issue; reversed on return. <c>Source</c> is
    /// <c>WO-ISS</c> or <c>WO-RTN</c>.
    /// </summary>
    private async Task<JournalEntry?> PostMaterialMovementJournalEntryAsync(
        WorkOrderPart part,
        decimal qty,
        bool isIssue)
    {
        if (qty <= 0m) return null;
        var amount = qty * part.UnitCost;
        if (amount <= 0m) return null;

        var resolvedCompanyId = part.WorkOrder?.Asset?.CompanyId
            ?? _tenantContext.CompanyId
            ?? 0;
        if (resolvedCompanyId == 0) return null;

        var ctx = new GlResolveContext(
            WorkOrderId: part.WorkOrderId,
            AssetId: part.WorkOrder?.AssetId);
        var materialsAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.MaintenanceMaterials, ctx);
        var inventoryAccount = await _glResolver.ResolveAsync(resolvedCompanyId, GlAccountKind.Inventory, ctx);

        var ticks = DateTime.UtcNow.Ticks;
        var src = isIssue ? "WO-ISS" : "WO-RTN";
        var jeReference = $"{src}-{part.WorkOrderId}-p{part.Id}-{ticks}";
        var woNumber = part.WorkOrder?.WorkOrderNumber ?? $"WO#{part.WorkOrderId}";
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
            Description = $"Materials {verb} {woNumber} (qty {qty} @ {part.UnitCost:C})",
            CreatedUtc = DateTime.UtcNow,
            Lines = new List<JournalLine>
            {
                new JournalLine
                {
                    LineNo = 1,
                    Account = drAccount,
                    Description = isIssue
                        ? $"Maintenance materials - {woNumber}"
                        : $"Inventory restored - {woNumber}",
                    Debit = amount,
                    Credit = 0m
                },
                new JournalLine
                {
                    LineNo = 2,
                    Account = crAccount,
                    Description = isIssue
                        ? $"Inventory {verb} {woNumber}"
                        : $"Maintenance materials reversed - {woNumber}",
                    Debit = 0m,
                    Credit = amount
                }
            }
        };
        _db.JournalEntries.Add(je);
        return je;
    }
}
