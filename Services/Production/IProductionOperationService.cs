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
using Abs.FixedAssets.Models.Production;
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
    private readonly ILogger<ProductionOperationService> _logger;

    public ProductionOperationService(AppDbContext db, ILogger<ProductionOperationService> logger)
    {
        _db = db;
        _logger = logger;
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

        var routing = await _db.Routings.FirstOrDefaultAsync(x => x.Id == r.RoutingId, ct);
        if (routing is null) return Result.Failure<IReadOnlyList<ProductionOperation>>("Routing not found.");
        if (routing.Status != RoutingStatus.Released)
            return Result.Failure<IReadOnlyList<ProductionOperation>>("Can only release from a Released routing.");

        var existingCount = await _db.ProductionOperations.CountAsync(x => x.ProductionOrderId == r.ProductionOrderId, ct);
        if (existingCount > 0)
            return Result.Failure<IReadOnlyList<ProductionOperation>>("This order already has ProductionOperation rows. Use ad-hoc Add to insert.");

        var ops = await _db.RoutingOperations
            .Where(x => x.RoutingId == r.RoutingId)
            .OrderBy(x => x.SequenceNumber)
            .ToListAsync(ct);

        if (ops.Count == 0)
            return Result.Failure<IReadOnlyList<ProductionOperation>>("Routing has no operations to release from.");

        var now = DateTime.UtcNow;

        // PR #5c.2 — Snapshot the tenant lineage at release time. LocationIdSnapshot
        // column existed since PR #5c.1 but the C# property was missing, so EF
        // silently left it at the DB DEFAULT 0 — pattern was dead at runtime.
        // CompanyIdSnapshot added in PR #5c.2 + stamped here from order.CompanyId
        // (which is itself backfilled in the same migration via Location/Customer-
        // Project joins). LocationIdSnapshot falls back to 0 when the order has
        // no LocationId — allowed by the deferred CHECK >= 0; PR #5c.4 seeder
        // tightens.
        var locationIdSnapshot = order.LocationId ?? 0;
        var companyIdSnapshot = order.CompanyId;

        var snapshots = ops.Select(op => new ProductionOperation
        {
            ProductionOrderId = r.ProductionOrderId,
            RoutingOperationId = op.Id,
            LocationIdSnapshot = locationIdSnapshot,
            CompanyIdSnapshot = companyIdSnapshot,
            RoutingRevisionSnapshot = routing.RevisionNumber,
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
        return Result.Success<IReadOnlyList<ProductionOperation>>(snapshots);
    }

    public async Task<Result<ProductionOperation>> UpdateStatusAsync(
        UpdateProductionOperationStatusRequest r, CancellationToken ct)
    {
        var op = await _db.ProductionOperations.FirstOrDefaultAsync(x => x.Id == r.ProductionOperationId, ct);
        if (op is null) return Result.Failure<ProductionOperation>("ProductionOperation not found.");

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
