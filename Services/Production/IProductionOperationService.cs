// Sprint 13.5 PR #5c — IProductionOperationService + impl.
//
// The bridge between Routing (template) and ProductionOrder (instance). When
// a ProductionOrder is released against a Routing, ReleaseFromRoutingAsync
// snapshots each RoutingOperation into a ProductionOperation row on the
// order (SNAPSHOT DISCIPLINE — editing the Routing master AFTER release
// must NOT change in-flight orders). RoutingRevisionSnapshot is stamped on
// each row for audit.
//
// Methods (3 v1):
//   1. ReleaseFromRoutingAsync — snapshot a Routing's ops into a Production
//      Order's ProductionOperation rows
//   2. UpdateStatusAsync       — transition the 8-state ProductionOperation
//      status machine (Scheduled → Released → InSetup → Running → Paused →
//      Completed | Skipped | Scrapped)
//   3. RecordActualsAsync      — record qty completed / scrap / rework + actual
//      times (PR #5d Operator Workbench is the primary caller)
//
// PR #5d will add: ClockInAsync / ClockOutAsync (Labor table writes).
// PR #5e will add: hooks for DowntimeEvent / ScrapEvent linkage.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.ChainOfCustody;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.ChainOfCustody;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public interface IProductionOperationService
{
    Task<Result<IReadOnlyList<ProductionOperation>>> ReleaseFromRoutingAsync(
        ReleaseFromRoutingRequest request, CancellationToken ct);

    Task<Result<ProductionOperation>> UpdateStatusAsync(
        UpdateProductionOperationStatusRequest request, CancellationToken ct);

    Task<Result<ProductionOperation>> RecordActualsAsync(
        RecordOperationActualsRequest request, CancellationToken ct);
}

public sealed record ReleaseFromRoutingRequest(
    int ProductionOrderId,
    int RoutingId,
    string? ReleasedBy);

public sealed record UpdateProductionOperationStatusRequest(
    int ProductionOperationId,
    ProductionOperationStatus NewStatus,
    string? ModifiedBy,
    string? SkipReason);

public sealed record RecordOperationActualsRequest(
    int ProductionOperationId,
    decimal? CompletedQty,
    decimal? ScrappedQty,
    decimal? ReworkQty,
    decimal? ActualSetupMins,
    decimal? ActualRunMins,
    DateTime? ActualStart,
    DateTime? ActualEnd,
    string? OperatorUserIdsCsv,
    string? Notes,
    string? ModifiedBy);

public sealed class ProductionOperationService : IProductionOperationService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IChainOfCustodyService _chainOfCustody;
    private readonly ILogger<ProductionOperationService> _logger;

    public ProductionOperationService(
        AppDbContext db,
        ITenantContext tenantContext,
        IChainOfCustodyService chainOfCustody,
        ILogger<ProductionOperationService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _chainOfCustody = chainOfCustody;
        _logger = logger;
    }

    // PR #5c.1 — central tenant-scope check.  ProductionOperation has no direct
    // CompanyId; we derive scope through the parent ProductionOrder → Location.CompanyId
    // (same pattern as ProductionOrderService).
    private async Task<int?> ResolveOrderCompanyIdAsync(int productionOrderId, CancellationToken ct)
    {
        var row = await _db.ProductionOrders
            .Where(o => o.Id == productionOrderId)
            .Select(o => new { o.LocationId, o.CustomerId })
            .FirstOrDefaultAsync(ct);
        if (row is null) return null;
        if (row.LocationId.HasValue)
        {
            var locCompanyId = await _db.Locations.Where(l => l.Id == row.LocationId.Value).Select(l => (int?)l.CompanyId).FirstOrDefaultAsync(ct);
            if (locCompanyId.HasValue) return locCompanyId.Value;
        }
        if (row.CustomerId.HasValue)
        {
            var custCompanyId = await _db.Customers.Where(c => c.Id == row.CustomerId.Value).Select(c => (int?)c.CompanyId).FirstOrDefaultAsync(ct);
            if (custCompanyId.HasValue) return custCompanyId.Value;
        }
        return null;
    }

    // Legal-transition map for the 8-state ProductionOperationStatus machine.
    private static readonly Dictionary<ProductionOperationStatus, ProductionOperationStatus[]> _legalTransitions = new()
    {
        [ProductionOperationStatus.Scheduled] = new[]
        {
            ProductionOperationStatus.Released, ProductionOperationStatus.Skipped, ProductionOperationStatus.Scrapped
        },
        [ProductionOperationStatus.Released] = new[]
        {
            ProductionOperationStatus.InSetup, ProductionOperationStatus.Running,
            ProductionOperationStatus.Paused, ProductionOperationStatus.Skipped, ProductionOperationStatus.Scrapped
        },
        [ProductionOperationStatus.InSetup] = new[]
        {
            ProductionOperationStatus.Running, ProductionOperationStatus.Paused, ProductionOperationStatus.Scrapped
        },
        [ProductionOperationStatus.Running] = new[]
        {
            ProductionOperationStatus.Paused, ProductionOperationStatus.Completed, ProductionOperationStatus.Scrapped
        },
        [ProductionOperationStatus.Paused] = new[]
        {
            ProductionOperationStatus.Running, ProductionOperationStatus.Completed, ProductionOperationStatus.Scrapped
        },
        // Terminal states
        [ProductionOperationStatus.Completed] = Array.Empty<ProductionOperationStatus>(),
        [ProductionOperationStatus.Skipped]   = Array.Empty<ProductionOperationStatus>(),
        [ProductionOperationStatus.Scrapped]  = Array.Empty<ProductionOperationStatus>(),
    };

    public async Task<Result<IReadOnlyList<ProductionOperation>>> ReleaseFromRoutingAsync(
        ReleaseFromRoutingRequest r, CancellationToken ct)
    {
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(x => x.Id == r.ProductionOrderId, ct);
        if (order is null) return Result.Failure<IReadOnlyList<ProductionOperation>>("ProductionOrder not found.");

        // PR #5c.1 — tenant scope check (was missing entirely).
        var orderCompanyId = await ResolveOrderCompanyIdAsync(r.ProductionOrderId, ct);
        if (orderCompanyId is null || !_tenantContext.VisibleCompanyIds.Contains(orderCompanyId.Value))
            return Result.Failure<IReadOnlyList<ProductionOperation>>("ProductionOrder is not in your tenant scope.");

        var routing = await _db.Routings.FirstOrDefaultAsync(x => x.Id == r.RoutingId, ct);
        if (routing is null) return Result.Failure<IReadOnlyList<ProductionOperation>>("Routing not found.");
        if (routing.Status != RoutingStatus.Released)
            return Result.Failure<IReadOnlyList<ProductionOperation>>("Can only release from a Released routing.");
        // PR #5c.1 — routing tenant check too (defense in depth).
        if (!_tenantContext.VisibleCompanyIds.Contains(routing.CompanyId))
            return Result.Failure<IReadOnlyList<ProductionOperation>>("Routing is not in your tenant scope.");
        // PR #5c.1 — site-scoped routing must match the order's site.
        if (routing.LocationId is not null && order.LocationId != routing.LocationId)
            return Result.Failure<IReadOnlyList<ProductionOperation>>(
                $"Routing is site-scoped to LocationId={routing.LocationId} but the order is at LocationId={(object?)order.LocationId ?? "null"}. Use a site-wide template or matching-site routing.");

        var existingCount = await _db.ProductionOperations.CountAsync(x => x.ProductionOrderId == r.ProductionOrderId, ct);
        if (existingCount > 0)
            return Result.Failure<IReadOnlyList<ProductionOperation>>("This order already has ProductionOperation rows. Use ad-hoc Add to insert.");

        var ops = await _db.RoutingOperations
            .Where(x => x.RoutingId == r.RoutingId)
            .OrderBy(x => x.SequenceNumber)
            .ToListAsync(ct);

        if (ops.Count == 0)
            return Result.Failure<IReadOnlyList<ProductionOperation>>("Routing has no operations to release from.");

        // PR #5c.1 — snapshot LocationId from the order at release time so PR #5e's
        // DowntimeEvent/ScrapEvent/etc. can site-scope without joining all the way
        // back to ProductionOrder.LocationId.
        if (!order.LocationId.HasValue)
            return Result.Failure<IReadOnlyList<ProductionOperation>>("ProductionOrder must have a LocationId to release operations (snapshot discipline requires a site).");
        var locationSnapshot = order.LocationId.Value;

        var now = DateTime.UtcNow;
        var snapshots = ops.Select(op => new ProductionOperation
        {
            ProductionOrderId = r.ProductionOrderId,
            RoutingOperationId = op.Id,
            RoutingRevisionSnapshot = routing.RevisionNumber,
            LocationIdSnapshot = locationSnapshot,
            SequenceNumber = op.SequenceNumber,
            WorkCenterId = op.WorkCenterId,
            OperationType = op.OperationType,
            Status = ProductionOperationStatus.Scheduled,
            Description = op.Description,
            PlannedSetupMins = op.SetupTimeMins,
            PlannedRunMins = op.RunTimePerUnitMins * order.QuantityOrdered,  // scales with order qty
            PlannedQueueMins = op.QueueTimeMins,
            PlannedMoveMins = op.MoveTimeMins,
            PlannedWaitMins = op.WaitTimeMins,
            PlannedQty = order.QuantityOrdered,
            Instructions = op.Instructions,
            CreatedBy = r.ReleasedBy,
            CreatedAt = now,
        }).ToList();

        _db.ProductionOperations.AddRange(snapshots);
        await _db.SaveChangesAsync(ct);

        // PR #5c.1 — emit chain edges per the BIC entity checklist.
        //   (1) ProductionOrder → Routing  (HAS_ROUTING — set on release)
        //   (2) ProductionOrder → ProductionOperation  (ORDER_HAS_OPERATION — per row)
        //   (3) ProductionOperation → WorkCenter  (OPERATION_AT_WORKCENTER — per row)
        try
        {
            await _chainOfCustody.RecordEdgeAsync(new RecordEdgeRequest(
                FromNodeType: "ProductionOrder", FromEntityId: order.Id, FromLabel: order.OrderNumber,
                ToNodeType:   "Routing",         ToEntityId:   routing.Id, ToLabel: routing.Code + "/" + routing.RevisionNumber,
                EdgeType: ChainEdgeTypes.HasRouting), ct);

            foreach (var po in snapshots)
            {
                await _chainOfCustody.RecordEdgeAsync(new RecordEdgeRequest(
                    FromNodeType: "ProductionOrder",     FromEntityId: order.Id, FromLabel: order.OrderNumber,
                    ToNodeType:   "ProductionOperation", ToEntityId:   po.Id,    ToLabel: "Op-" + po.SequenceNumber,
                    EdgeType: ChainEdgeTypes.OrderHasOperation), ct);

                await _chainOfCustody.RecordEdgeAsync(new RecordEdgeRequest(
                    FromNodeType: "ProductionOperation", FromEntityId: po.Id,           FromLabel: "Op-" + po.SequenceNumber,
                    ToNodeType:   "WorkCenter",          ToEntityId:   po.WorkCenterId, ToLabel: "WC-" + po.WorkCenterId,
                    EdgeType: ChainEdgeTypes.OperationAtWorkCenter), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chain edge emission failed for Order/Routing/Operation/WC (non-fatal)");
        }

        return Result.Success<IReadOnlyList<ProductionOperation>>(snapshots);
    }

    public async Task<Result<ProductionOperation>> UpdateStatusAsync(
        UpdateProductionOperationStatusRequest r, CancellationToken ct)
    {
        var op = await _db.ProductionOperations.FirstOrDefaultAsync(x => x.Id == r.ProductionOperationId, ct);
        if (op is null) return Result.Failure<ProductionOperation>("ProductionOperation not found.");

        // PR #5c.1 — tenant scope check (was missing entirely).
        var orderCompanyId = await ResolveOrderCompanyIdAsync(op.ProductionOrderId, ct);
        if (orderCompanyId is null || !_tenantContext.VisibleCompanyIds.Contains(orderCompanyId.Value))
            return Result.Failure<ProductionOperation>("ProductionOperation is not in your tenant scope.");

        if (op.Status == r.NewStatus) return Result.Success(op);  // idempotent

        if (!_legalTransitions.TryGetValue(op.Status, out var legal) || !legal.Contains(r.NewStatus))
            return Result.Failure<ProductionOperation>(
                $"Illegal transition {op.Status} -> {r.NewStatus}. " +
                $"Legal from {op.Status}: {string.Join(", ", legal ?? Array.Empty<ProductionOperationStatus>())}");

        if (r.NewStatus == ProductionOperationStatus.Skipped && string.IsNullOrWhiteSpace(r.SkipReason))
            return Result.Failure<ProductionOperation>("SkipReason is required when transitioning to Skipped.");

        var now = DateTime.UtcNow;
        op.Status = r.NewStatus;
        if (r.NewStatus == ProductionOperationStatus.Running && op.ActualStart is null)
            op.ActualStart = now;
        if (r.NewStatus == ProductionOperationStatus.Completed || r.NewStatus == ProductionOperationStatus.Scrapped)
            op.ActualEnd = now;
        if (!string.IsNullOrWhiteSpace(r.SkipReason)) op.SkipReason = r.SkipReason;
        op.ModifiedAt = now;
        op.ModifiedBy = r.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(op);
    }

    public async Task<Result<ProductionOperation>> RecordActualsAsync(
        RecordOperationActualsRequest r, CancellationToken ct)
    {
        var op = await _db.ProductionOperations.FirstOrDefaultAsync(x => x.Id == r.ProductionOperationId, ct);
        if (op is null) return Result.Failure<ProductionOperation>("ProductionOperation not found.");

        // PR #5c.1 — tenant scope check (was missing entirely).
        var orderCompanyId = await ResolveOrderCompanyIdAsync(op.ProductionOrderId, ct);
        if (orderCompanyId is null || !_tenantContext.VisibleCompanyIds.Contains(orderCompanyId.Value))
            return Result.Failure<ProductionOperation>("ProductionOperation is not in your tenant scope.");

        if (r.CompletedQty.HasValue)     op.CompletedQty = r.CompletedQty.Value;
        if (r.ScrappedQty.HasValue)      op.ScrappedQty = r.ScrappedQty.Value;
        if (r.ReworkQty.HasValue)        op.ReworkQty = r.ReworkQty.Value;
        if (r.ActualSetupMins.HasValue)  op.ActualSetupMins = r.ActualSetupMins.Value;
        if (r.ActualRunMins.HasValue)    op.ActualRunMins = r.ActualRunMins.Value;
        if (r.ActualStart.HasValue)      op.ActualStart = r.ActualStart.Value;
        if (r.ActualEnd.HasValue)        op.ActualEnd = r.ActualEnd.Value;
        if (r.OperatorUserIdsCsv != null) op.OperatorUserIdsCsv = r.OperatorUserIdsCsv;
        if (!string.IsNullOrWhiteSpace(r.Notes))
            op.Notes = string.IsNullOrEmpty(op.Notes) ? r.Notes : op.Notes + "\n" + r.Notes;
        op.ModifiedAt = DateTime.UtcNow;
        op.ModifiedBy = r.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(op);
    }
}
