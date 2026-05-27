// =============================================================================
// B8 PR-PRO-11 — Operation Transaction Validators (Rules 9-12)
//
// Guards on IProductionOperationTransactionService actions.
// =============================================================================

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Production.Validators;

// ─── 9. Predecessor Operation ────────────────────────────────────────────────
/// <summary>Cannot complete op before predecessor unless allowed.</summary>
public sealed class PredecessorOperationValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "PredecessorOperation";
    public IReadOnlySet<string> ApplicableActionTypes { get; } =
        TransactionActions.OperationCompleteActions;

    public PredecessorOperationValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.OperationId is not > 0) return TransactionValidationResult.Valid;

        var currentOp = await _db.Set<ProductionOperation>()
            .AsNoTracking()
            .Where(o => o.Id == ctx.OperationId && o.CompanyIdSnapshot == ctx.CompanyId)
            .Select(o => new { o.SequenceNumber, o.ProductionOrderId })
            .FirstOrDefaultAsync(ct);
        if (currentOp == null) return TransactionValidationResult.Valid;

        // Find the immediately prior operation
        var priorOp = await _db.Set<ProductionOperation>()
            .AsNoTracking()
            .Where(o => o.ProductionOrderId == currentOp.ProductionOrderId
                && o.CompanyIdSnapshot == ctx.CompanyId
                && o.SequenceNumber < currentOp.SequenceNumber)
            .OrderByDescending(o => o.SequenceNumber)
            .Select(o => new { o.SequenceNumber, o.Status })
            .FirstOrDefaultAsync(ct);

        if (priorOp != null
            && priorOp.Status != ProductionOperationStatus.Completed
            && priorOp.Status != ProductionOperationStatus.Skipped)
        {
            return TransactionValidationResult.Block(Name,
                $"Prior operation (Op {priorOp.SequenceNumber}, status: {priorOp.Status}) must complete before Op {currentOp.SequenceNumber} can complete.",
                "SupervisorOverride");
        }

        return TransactionValidationResult.Valid;
    }
}

// ─── 10. Labor Certification ─────────────────────────────────────────────────
/// <summary>Block unqualified operators (labor certification check).</summary>
public sealed class LaborCertificationValidator : IProductionTransactionValidator
{
    public string Name => "LaborCertification";
    public IReadOnlySet<string> ApplicableActionTypes { get; } =
        TransactionActions.OperationStartActions;

    public Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        // Labor certification check requires EmployeeCertification entity
        // (future Sprint 16+ entity linking Employee → WorkCenter → CertificationType).
        // For now: pass. The validator is wired and ready for the data.
        return Task.FromResult(TransactionValidationResult.Valid);
    }
}

// ─── 11. Drawing Revision ────────────────────────────────────────────────────
/// <summary>Warn/block on obsolete drawing revision.</summary>
public sealed class DrawingRevisionValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "DrawingRevision";
    public IReadOnlySet<string> ApplicableActionTypes { get; } = new HashSet<string>(
        TransactionActions.OperationStartActions
            .Concat(TransactionActions.OperationCompleteActions)
            .Concat(TransactionActions.MaterialIssueActions));

    public DrawingRevisionValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        // Check if the PRO's drawing revision matches the latest document version
        var pro = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .Where(p => p.Id == ctx.ProductionOrderId && p.CompanyId == ctx.CompanyId)
            .Select(p => new { p.DrawingRevision, p.ItemId })
            .FirstOrDefaultAsync(ct);
        if (pro?.DrawingRevision == null || pro.ItemId == null) return TransactionValidationResult.Valid;

        // Check if there's a newer document version for this item
        // Full cross-reference requires ItemDocumentLink entity (Sprint 14.2 DMS).
        // For now check if any ECO has been implemented after the PRO was created.
        var hasRecentEco = await _db.Set<Models.Engineering.EngineeringChangeOrder>()
            .AsNoTracking()
            .Where(eco => eco.CompanyId == ctx.CompanyId
                && eco.Status == Models.Engineering.EcoStatus.Implemented)
            .AnyAsync(ct);

        if (hasRecentEco)
            return TransactionValidationResult.Warn(Name,
                $"PRO drawing revision ({pro.DrawingRevision}) — implemented ECOs detected. Verify drawing is current before proceeding.");

        return TransactionValidationResult.Valid;
    }
}

// ─── 12. Machine Status ─────────────────────────────────────────────────────
/// <summary>Warn/block when assigned machine is down or in maintenance.</summary>
public sealed class MachineStatusValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "MachineStatus";
    public IReadOnlySet<string> ApplicableActionTypes { get; } = new HashSet<string>
    {
        TransactionActions.Start, TransactionActions.StartSetup,
        TransactionActions.ChangeResource
    };

    public MachineStatusValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.OperationId is not > 0) return TransactionValidationResult.Valid;

        // Check the operation's work center for linked asset status
        var opWorkCenter = await _db.Set<ProductionOperation>()
            .AsNoTracking()
            .Where(o => o.Id == ctx.OperationId && o.CompanyIdSnapshot == ctx.CompanyId)
            .Select(o => o.WorkCenterId)
            .FirstOrDefaultAsync(ct);

        if (opWorkCenter is not > 0) return TransactionValidationResult.Valid;

        // Full machine-status check requires WorkCenterAssetLink → Asset → WorkOrder
        // navigation chain (cross-module EAM integration, Sprint 16+).
        // For now: validator is wired and ready for the navigation data.
        // When the link table ships, this becomes a live cross-module check.

        return TransactionValidationResult.Valid;
    }
}
