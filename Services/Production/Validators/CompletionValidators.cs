// =============================================================================
// B8 PR-PRO-11 — Completion/WIP Validators (Rules 13-14)
//
// Guards on IProductionCompletionService + IProductionWipMoveService actions.
// =============================================================================

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Production.Validators;

// ─── 13. Inspection Hold ─────────────────────────────────────────────────────
/// <summary>Inspection hold blocks move/complete.</summary>
public sealed class InspectionHoldValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    public string Name => "InspectionHold";
    public IReadOnlySet<string> ApplicableActionTypes { get; } = new HashSet<string>(
        TransactionActions.AllMoveActions
            .Concat(TransactionActions.OperationCompleteActions)
            .Concat(new[] { TransactionActions.RecordCompletion }));

    public InspectionHoldValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.OperationId is not > 0) return TransactionValidationResult.Valid;

        var op = await _db.Set<ProductionOperation>()
            .AsNoTracking()
            .Where(o => o.Id == ctx.OperationId && o.CompanyIdSnapshot == ctx.CompanyId)
            .Select(o => new { o.QualityHoldActive, o.QualityHoldReason })
            .FirstOrDefaultAsync(ct);

        if (op == null) return TransactionValidationResult.Valid;

        if (op.QualityHoldActive)
            return TransactionValidationResult.Block(Name,
                $"Operation is on quality hold ({op.QualityHoldReason ?? "pending inspection"}). Cannot move or complete until released.",
                "SupervisorOverride");

        return TransactionValidationResult.Valid;
    }
}

// ─── 14. Scrap Threshold ────────────────────────────────────────────────────
/// <summary>Scrap above threshold requires supervisor approval.</summary>
public sealed class ScrapThresholdValidator : IProductionTransactionValidator
{
    private readonly AppDbContext _db;
    /// <summary>Scrap percentage above this threshold requires approval.</summary>
    private const decimal ThresholdPercent = 5.0m;

    public string Name => "ScrapThreshold";
    public IReadOnlySet<string> ApplicableActionTypes { get; } = new HashSet<string>
    {
        TransactionActions.RecordScrap
    };

    public ScrapThresholdValidator(AppDbContext db) => _db = db;

    public async Task<TransactionValidationResult> ValidateAsync(
        TransactionValidationContext ctx, CancellationToken ct)
    {
        if (ctx.Quantity is not > 0 || ctx.ProductionOrderId <= 0)
            return TransactionValidationResult.Valid;

        var pro = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .Where(p => p.Id == ctx.ProductionOrderId && p.CompanyId == ctx.CompanyId)
            .Select(p => new { p.QuantityOrdered })
            .FirstOrDefaultAsync(ct);

        if (pro == null || pro.QuantityOrdered <= 0) return TransactionValidationResult.Valid;

        // Calculate cumulative scrap + this new scrap
        var existingScrap = await _db.Set<ProductionScrapEvent>()
            .AsNoTracking()
            .Where(s => s.ProductionOrderId == ctx.ProductionOrderId && s.CompanyId == ctx.CompanyId)
            .SumAsync(s => s.ScrapQuantity, ct);

        var totalScrap = existingScrap + ctx.Quantity.Value;
        var scrapPercent = (totalScrap / pro.QuantityOrdered) * 100m;

        if (scrapPercent > ThresholdPercent)
        {
            if (!ctx.SupervisorOverride)
                return TransactionValidationResult.Block(Name,
                    $"Scrap rate would reach {scrapPercent:F1}% (threshold: {ThresholdPercent}%). Supervisor approval required.",
                    "SupervisorOverride");

            return TransactionValidationResult.Warn(Name,
                $"Scrap rate is {scrapPercent:F1}% (above {ThresholdPercent}% threshold). Supervisor override applied.");
        }

        return TransactionValidationResult.Valid;
    }
}
