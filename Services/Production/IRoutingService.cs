// Sprint 13.5 PR #5c — IRoutingService + RoutingService impl.
//
// First mutation surface for Routing + RoutingOperation. v1 minimum to make
// the admin Routings page + a basic Routing Builder work. Drag-to-reorder UI
// + smart-default-from-prior composer + voice editing land in PR #5c.1.
//
// Methods (6):
//   1. CreateAsync             — new Routing in Draft status (header only)
//   2. AddOperationAsync       — append a RoutingOperation (or insert via SequenceNumber)
//   3. UpdateOperationAsync    — edit a RoutingOperation
//   4. ReorderOperationAsync   — change a RoutingOperation's SequenceNumber
//   5. ReleaseAsync            — Draft -> Released (with EffectiveFrom + ApprovedBy stamp)
//   6. ObsoleteAsync           — Released -> Obsolete (with EffectiveTo stamp)
//
// CloneFromAsync (the smart-default composer) lands in PR #5c.1 since it
// requires reading "last N similar orders" — needs ProductionOrder + Routing
// join logic that's UI-driven rather than service-foundation.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public interface IRoutingService
{
    Task<Result<Routing>> CreateAsync(CreateRoutingRequest request, CancellationToken ct);
    Task<Result<RoutingOperation>> AddOperationAsync(AddRoutingOperationRequest request, CancellationToken ct);
    Task<Result<RoutingOperation>> UpdateOperationAsync(UpdateRoutingOperationRequest request, CancellationToken ct);
    Task<Result<RoutingOperation>> ReorderOperationAsync(ReorderRoutingOperationRequest request, CancellationToken ct);
    Task<Result<Routing>> ReleaseAsync(ReleaseRoutingRequest request, CancellationToken ct);
    Task<Result<Routing>> ObsoleteAsync(ObsoleteRoutingRequest request, CancellationToken ct);
}

public sealed record CreateRoutingRequest(
    int CompanyId,
    string Code,
    string RevisionNumber,
    string Name,
    string? Description,
    int ItemId,
    RoutingType Type,
    decimal LotBaseSize,
    string? UnitOfMeasure,
    bool IsDefault,
    int? SourceRoutingId,
    string? CreatedBy);

public sealed record AddRoutingOperationRequest(
    int RoutingId,
    int SequenceNumber,
    int WorkCenterId,
    ProductionOperationType OperationType,
    string Description,
    decimal SetupTimeMins,
    decimal RunTimePerUnitMins,
    decimal QueueTimeMins,
    decimal MoveTimeMins,
    decimal WaitTimeMins,
    decimal YieldPct,
    int? PredecessorOperationId,
    bool IsParallel,
    bool IsOptional,
    string? Instructions,
    string? RequiredSkillCodes,
    string? RequiredToolingIds,
    string? CreatedBy);

public sealed record UpdateRoutingOperationRequest(
    int RoutingOperationId,
    int WorkCenterId,
    ProductionOperationType OperationType,
    string Description,
    decimal SetupTimeMins,
    decimal RunTimePerUnitMins,
    decimal QueueTimeMins,
    decimal MoveTimeMins,
    decimal WaitTimeMins,
    decimal YieldPct,
    int? PredecessorOperationId,
    bool IsParallel,
    bool IsOptional,
    string? Instructions,
    string? RequiredSkillCodes,
    string? RequiredToolingIds,
    string? ModifiedBy);

public sealed record ReorderRoutingOperationRequest(
    int RoutingOperationId,
    int NewSequenceNumber,
    string? ModifiedBy);

public sealed record ReleaseRoutingRequest(
    int RoutingId,
    string? ApprovedBy);

public sealed record ObsoleteRoutingRequest(
    int RoutingId,
    string? ModifiedBy);

public sealed class RoutingService : IRoutingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RoutingService> _logger;

    public RoutingService(AppDbContext db, ITenantContext tenantContext, ILogger<RoutingService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Result<Routing>> CreateAsync(CreateRoutingRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Code)) return Result.Failure<Routing>("Code is required.");
        if (string.IsNullOrWhiteSpace(r.Name)) return Result.Failure<Routing>("Name is required.");
        if (!_tenantContext.VisibleCompanyIds.Contains(r.CompanyId))
            return Result.Failure<Routing>("Company is not in your tenant scope.");
        if (r.LotBaseSize <= 0) return Result.Failure<Routing>("LotBaseSize must be > 0.");

        var dup = await _db.Routings
            .AnyAsync(x => x.CompanyId == r.CompanyId && x.Code == r.Code && x.RevisionNumber == r.RevisionNumber, ct);
        if (dup) return Result.Failure<Routing>($"Routing '{r.Code}' rev '{r.RevisionNumber}' already exists.");

        var routing = new Routing
        {
            CompanyId = r.CompanyId,
            Code = r.Code.Trim(),
            RevisionNumber = string.IsNullOrEmpty(r.RevisionNumber) ? "A" : r.RevisionNumber.Trim(),
            Name = r.Name.Trim(),
            Description = r.Description,
            ItemId = r.ItemId,
            Type = r.Type,
            Status = RoutingStatus.Draft,
            LotBaseSize = r.LotBaseSize,
            UnitOfMeasure = r.UnitOfMeasure,
            IsDefault = r.IsDefault,
            SourceRoutingId = r.SourceRoutingId,
            CreatedBy = r.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        _db.Routings.Add(routing);
        await _db.SaveChangesAsync(ct);
        return Result.Success(routing);
    }

    public async Task<Result<RoutingOperation>> AddOperationAsync(AddRoutingOperationRequest r, CancellationToken ct)
    {
        var routing = await _db.Routings.FirstOrDefaultAsync(x => x.Id == r.RoutingId, ct);
        if (routing is null) return Result.Failure<RoutingOperation>("Routing not found.");
        if (!_tenantContext.VisibleCompanyIds.Contains(routing.CompanyId))
            return Result.Failure<RoutingOperation>("Routing is not in your tenant scope.");
        if (routing.Status == RoutingStatus.Obsolete)
            return Result.Failure<RoutingOperation>("Cannot add operations to an Obsolete routing.");

        var op = new RoutingOperation
        {
            RoutingId = r.RoutingId,
            SequenceNumber = r.SequenceNumber > 0 ? r.SequenceNumber : 10,
            WorkCenterId = r.WorkCenterId,
            OperationType = r.OperationType,
            Description = r.Description,
            SetupTimeMins = r.SetupTimeMins,
            RunTimePerUnitMins = r.RunTimePerUnitMins,
            QueueTimeMins = r.QueueTimeMins,
            MoveTimeMins = r.MoveTimeMins,
            WaitTimeMins = r.WaitTimeMins,
            YieldPct = r.YieldPct == 0 ? 100 : r.YieldPct,
            PredecessorOperationId = r.PredecessorOperationId,
            IsParallel = r.IsParallel,
            IsOptional = r.IsOptional,
            Instructions = r.Instructions,
            RequiredSkillCodes = r.RequiredSkillCodes,
            RequiredToolingIds = r.RequiredToolingIds,
            CreatedBy = r.CreatedBy,
            CreatedAt = DateTime.UtcNow,
        };
        _db.RoutingOperations.Add(op);
        await _db.SaveChangesAsync(ct);
        return Result.Success(op);
    }

    public async Task<Result<RoutingOperation>> UpdateOperationAsync(UpdateRoutingOperationRequest r, CancellationToken ct)
    {
        var op = await _db.RoutingOperations.FirstOrDefaultAsync(x => x.Id == r.RoutingOperationId, ct);
        if (op is null) return Result.Failure<RoutingOperation>("RoutingOperation not found.");

        op.WorkCenterId = r.WorkCenterId;
        op.OperationType = r.OperationType;
        op.Description = r.Description;
        op.SetupTimeMins = r.SetupTimeMins;
        op.RunTimePerUnitMins = r.RunTimePerUnitMins;
        op.QueueTimeMins = r.QueueTimeMins;
        op.MoveTimeMins = r.MoveTimeMins;
        op.WaitTimeMins = r.WaitTimeMins;
        op.YieldPct = r.YieldPct;
        op.PredecessorOperationId = r.PredecessorOperationId;
        op.IsParallel = r.IsParallel;
        op.IsOptional = r.IsOptional;
        op.Instructions = r.Instructions;
        op.RequiredSkillCodes = r.RequiredSkillCodes;
        op.RequiredToolingIds = r.RequiredToolingIds;
        op.ModifiedAt = DateTime.UtcNow;
        op.ModifiedBy = r.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(op);
    }

    public async Task<Result<RoutingOperation>> ReorderOperationAsync(ReorderRoutingOperationRequest r, CancellationToken ct)
    {
        if (r.NewSequenceNumber <= 0)
            return Result.Failure<RoutingOperation>("SequenceNumber must be > 0.");
        var op = await _db.RoutingOperations.FirstOrDefaultAsync(x => x.Id == r.RoutingOperationId, ct);
        if (op is null) return Result.Failure<RoutingOperation>("RoutingOperation not found.");
        op.SequenceNumber = r.NewSequenceNumber;
        op.ModifiedAt = DateTime.UtcNow;
        op.ModifiedBy = r.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(op);
    }

    public async Task<Result<Routing>> ReleaseAsync(ReleaseRoutingRequest r, CancellationToken ct)
    {
        var routing = await _db.Routings.FirstOrDefaultAsync(x => x.Id == r.RoutingId, ct);
        if (routing is null) return Result.Failure<Routing>("Routing not found.");
        if (!_tenantContext.VisibleCompanyIds.Contains(routing.CompanyId))
            return Result.Failure<Routing>("Routing is not in your tenant scope.");
        if (routing.Status == RoutingStatus.Released)
            return Result.Success(routing); // idempotent
        if (routing.Status == RoutingStatus.Obsolete)
            return Result.Failure<Routing>("Cannot release an Obsolete routing.");

        var opCount = await _db.RoutingOperations.CountAsync(x => x.RoutingId == r.RoutingId, ct);
        if (opCount == 0) return Result.Failure<Routing>("Cannot release a routing with zero operations.");

        routing.Status = RoutingStatus.Released;
        routing.EffectiveFrom = DateTime.UtcNow;
        routing.ApprovedAt = DateTime.UtcNow;
        routing.ApprovedBy = r.ApprovedBy;
        routing.ModifiedAt = DateTime.UtcNow;
        routing.ModifiedBy = r.ApprovedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(routing);
    }

    public async Task<Result<Routing>> ObsoleteAsync(ObsoleteRoutingRequest r, CancellationToken ct)
    {
        var routing = await _db.Routings.FirstOrDefaultAsync(x => x.Id == r.RoutingId, ct);
        if (routing is null) return Result.Failure<Routing>("Routing not found.");
        if (!_tenantContext.VisibleCompanyIds.Contains(routing.CompanyId))
            return Result.Failure<Routing>("Routing is not in your tenant scope.");
        routing.Status = RoutingStatus.Obsolete;
        routing.EffectiveTo = DateTime.UtcNow;
        routing.IsActive = false;
        routing.ModifiedAt = DateTime.UtcNow;
        routing.ModifiedBy = r.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(routing);
    }
}
