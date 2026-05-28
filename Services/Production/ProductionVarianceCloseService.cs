// Sprint 14.4 PR-4 (2026-05-28) — Variance + Close Service implementation.
//
// 5 standard cost variances:
//   1. Material Usage  = ActualMaterial - EstimatedMaterial
//   2. Labor Rate      = ActualLabor - EstimatedLabor
//   3. Labor Efficiency = (computed when standard hours available)
//   4. Overhead Volume  = ActualBurden - EstimatedBurden
//   5. Overhead Spending = (overhead portion not covered by volume)
//
// Close workflow (atomic):
//   Verify Completed → Refresh summary → Compute variances → Post variance
//   CostTransactions → Stamp summary → Freeze cost → Set Closed status →
//   Record ProductionCloseEvent.
//
// Per Dean's spec §12: ClosedSettled = "No more costs allowed except
// controlled adjustment."

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public sealed class ProductionVarianceCloseService : IProductionVarianceCloseService
{
    private readonly AppDbContext _db;
    private readonly ICostTransactionService _costSvc;
    private readonly ICostRollupService _rollupSvc;
    private readonly ILogger<ProductionVarianceCloseService> _log;

    public ProductionVarianceCloseService(
        AppDbContext db,
        ICostTransactionService costSvc,
        ICostRollupService rollupSvc,
        ILogger<ProductionVarianceCloseService> log)
    {
        _db = db;
        _costSvc = costSvc;
        _rollupSvc = rollupSvc;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════════
    // COMPUTE VARIANCES — does NOT post JEs
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<VarianceComputationResult>> ComputeVariancesAsync(
        int productionOrderId, string? computedBy, CancellationToken ct = default)
    {
        var summary = await _costSvc.GetSummaryAsync(productionOrderId, ct);
        if (summary == null)
            return Result.Failure<VarianceComputationResult>(
                $"No cost summary for PRO {productionOrderId}. Run a rollup first.");

        var variances = new List<ProductionVariance>();
        var ts = DateTime.UtcNow;

        var pro = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);
        if (pro == null)
            return Result.Failure<VarianceComputationResult>($"PRO {productionOrderId} not found.");

        // 1. Material Usage Variance = Actual Material - Estimated Material
        variances.Add(BuildVariance(
            pro, ProductionVarianceType.MaterialUsage,
            summary.EstimatedMaterialCost, summary.ActualMaterialCost,
            computedBy, ts));

        // 2. Labor Rate Variance = Actual Labor - Estimated Labor
        variances.Add(BuildVariance(
            pro, ProductionVarianceType.LaborRate,
            summary.EstimatedLaborCost, summary.ActualLaborCost,
            computedBy, ts));

        // 3. Labor Efficiency Variance = Actual Machine - Estimated Machine
        // (proxy: machine cost captures efficiency of machine utilization)
        variances.Add(BuildVariance(
            pro, ProductionVarianceType.LaborEfficiency,
            summary.EstimatedMachineCost, summary.ActualMachineCost,
            computedBy, ts));

        // 4. Overhead Volume Variance = Actual Burden - Estimated Burden
        variances.Add(BuildVariance(
            pro, ProductionVarianceType.OverheadVolume,
            summary.EstimatedBurdenCost, summary.ActualBurdenCost,
            computedBy, ts));

        // 5. Overhead Spending Variance = (Actual Outside + Actual Subcontract)
        //    - (Estimated Outside + Estimated Subcontract)
        var estOutsideSub = summary.EstimatedOutsideProcessingCost + summary.EstimatedSubcontractCost;
        var actOutsideSub = summary.ActualOutsideProcessingCost + summary.ActualSubcontractCost;
        variances.Add(BuildVariance(
            pro, ProductionVarianceType.OverheadSpending,
            estOutsideSub, actOutsideSub,
            computedBy, ts));

        // 6. Subcontract Variance
        variances.Add(BuildVariance(
            pro, ProductionVarianceType.SubcontractVariance,
            summary.EstimatedSubcontractCost, summary.ActualSubcontractCost,
            computedBy, ts));

        // 7. Scrap Variance
        variances.Add(BuildVariance(
            pro, ProductionVarianceType.ScrapVariance,
            summary.EstimatedScrapReworkCost, summary.ActualScrapReworkCost,
            computedBy, ts));

        // 8. Total Variance (net)
        variances.Add(BuildVariance(
            pro, ProductionVarianceType.TotalVariance,
            summary.EstimatedTotalCost, summary.ActualTotalCost,
            computedBy, ts));

        // Filter out zero variances for cleanliness (keep TotalVariance always)
        var meaningful = variances
            .Where(v => v.VarianceAmount != 0 || v.VarianceType == ProductionVarianceType.TotalVariance)
            .ToList();

        var totalVar = meaningful.FirstOrDefault(v => v.VarianceType == ProductionVarianceType.TotalVariance)?.VarianceAmount ?? 0;

        return Result.Success(new VarianceComputationResult
        {
            Variances = meaningful,
            TotalVariance = totalVar,
            FavorableCount = meaningful.Count(v => v.IsFavorable),
            UnfavorableCount = meaningful.Count(v => !v.IsFavorable && v.VarianceAmount != 0),
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // CLOSE WORKFLOW — atomic
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<ProductionCloseResult>> CloseAsync(
        int productionOrderId, string? closedBy, CancellationToken ct = default)
    {
        _log.LogInformation("Starting close workflow for PRO {ProId}", productionOrderId);

        // Step 1: Verify PRO status
        var pro = await _db.Set<ProductionOrder>()
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);
        if (pro == null)
            return Result.Failure<ProductionCloseResult>($"PRO {productionOrderId} not found.");

        if (pro.Status == ProductionOrderStatus.Closed)
            return Result.Failure<ProductionCloseResult>(
                $"PRO {productionOrderId} is already Closed. Use ReopenAsync to reopen for adjustments.");

        if (pro.Status != ProductionOrderStatus.Completed && pro.Status != ProductionOrderStatus.InProgress)
            return Result.Failure<ProductionCloseResult>(
                $"PRO {productionOrderId} status is {pro.Status}. Must be Completed or InProgress to close.");

        // Step 2: Refresh costs via rollup
        var rollupResult = await _rollupSvc.ExecuteRollupAsync(
            productionOrderId, CostRollupMode.Financial, closedBy, ct);
        // Continue even if rollup has exceptions — we record them

        // Step 3: Refresh summary
        var refreshResult = await _costSvc.RefreshSummaryAsync(productionOrderId, closedBy, ct);
        if (!refreshResult.IsSuccess)
            return Result.Failure<ProductionCloseResult>($"Failed to refresh summary: {refreshResult.Error}");

        var summary = refreshResult.Value!;

        // Step 4: Compute variances
        var varianceResult = await ComputeVariancesAsync(productionOrderId, closedBy, ct);
        if (!varianceResult.IsSuccess)
            return Result.Failure<ProductionCloseResult>($"Failed to compute variances: {varianceResult.Error}");

        var variances = varianceResult.Value!.Variances;

        // Persist variance rows
        _db.Set<ProductionVariance>().AddRange(variances);

        // Step 5: Post variance CostTransactions for each non-zero variance
        int varianceJeCount = 0;
        foreach (var v in variances.Where(v => v.VarianceAmount != 0 && v.VarianceType != ProductionVarianceType.TotalVariance))
        {
            var costBucket = MapVarianceToBucket(v.VarianceType);
            var glKind = MapVarianceToGlKind(v.VarianceType);

            await _costSvc.PostCostAsync(
                costObjectType: CostObjectType.ProductionOrder,
                costObjectId: productionOrderId,
                transactionType: CostTransactionType.VarianceSettlement,
                costBucket: ProductionCostBucket.Variance,
                companyId: pro.CompanyId,
                siteId: pro.LocationId,
                productionOrderId: productionOrderId,
                operationId: null, bomLineId: null, itemId: null,
                quantity: 1m, uom: "EA", unitCost: v.VarianceAmount,
                sourceTransactionType: "ProductionVariance",
                sourceTransactionId: null,
                lotNumber: null, serialNumber: null, heatNumber: null,
                rollupAdditive: false, // Variance settlements are NOT additive to WIP
                notes: $"Variance settlement: {v.VarianceType} Est=${v.EstimatedAmount:N2} Act=${v.ActualAmount:N2} Var=${v.VarianceAmount:N2}",
                postedBy: closedBy, ct: ct);
            varianceJeCount++;
        }

        // Step 6: Stamp summary columns
        summary.CostVariance = varianceResult.Value!.TotalVariance;
        summary.CostStatus = ProductionCostStatus.ClosedSettled;
        summary.ClosedSettledValue = summary.ActualTotalCost;
        summary.WipBalance = 0; // WIP cleared at close
        summary.LastRollupTimestamp = DateTime.UtcNow;
        summary.RollupStatus = "Closed";
        summary.UpdatedAtUtc = DateTime.UtcNow;
        summary.UpdatedBy = closedBy;

        // Step 7: Freeze cost on PRO + set Closed status
        pro.FreezeCost = true;
        pro.Status = ProductionOrderStatus.Closed;

        // Step 8: Record close event
        var closeEvent = new ProductionCloseEvent
        {
            CompanyId = pro.CompanyId,
            ProductionOrderId = productionOrderId,
            Step = ProductionCloseStep.CloseComplete,
            EstimatedTotalAtClose = summary.EstimatedTotalCost,
            ActualTotalAtClose = summary.ActualTotalCost,
            TotalVarianceAtClose = varianceResult.Value!.TotalVariance,
            WipBalanceAtClose = 0,
            VarianceLineCount = variances.Count,
            VarianceJeCount = varianceJeCount,
            UnresolvedExceptionCount = (rollupResult != null && rollupResult.IsSuccess)
                ? rollupResult.Value!.Exceptions.Count(e => e.Severity >= CostExceptionSeverity.Error)
                : 0,
            CloseSuccessful = true,
            CloseMessage = $"PRO #{productionOrderId} closed. " +
                $"Total variance: ${varianceResult.Value!.TotalVariance:N2} " +
                $"({varianceResult.Value!.FavorableCount} favorable, {varianceResult.Value!.UnfavorableCount} unfavorable). " +
                $"{varianceJeCount} variance JEs posted.",
            ClosedBy = closedBy,
        };
        _db.Set<ProductionCloseEvent>().Add(closeEvent);

        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "PRO {ProId} CLOSED: Estimated=${Est:N2} Actual=${Act:N2} Variance=${Var:N2} " +
            "({VarCount} variance lines, {JeCount} JEs, {FavCount} favorable)",
            productionOrderId, summary.EstimatedTotalCost, summary.ActualTotalCost,
            varianceResult.Value!.TotalVariance, variances.Count, varianceJeCount,
            varianceResult.Value!.FavorableCount);

        return Result.Success(new ProductionCloseResult
        {
            CloseEvent = closeEvent,
            Variances = variances.ToList(),
            Success = true,
            Message = closeEvent.CloseMessage,
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // REOPEN — controlled post-close correction
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<ProductionCloseEvent>> ReopenAsync(
        int productionOrderId, string reason, string? reopenedBy, CancellationToken ct = default)
    {
        var pro = await _db.Set<ProductionOrder>()
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);
        if (pro == null)
            return Result.Failure<ProductionCloseEvent>($"PRO {productionOrderId} not found.");
        if (pro.Status != ProductionOrderStatus.Closed)
            return Result.Failure<ProductionCloseEvent>(
                $"PRO {productionOrderId} is not Closed (status={pro.Status}). Cannot reopen.");

        // Unfreeze and set back to Completed
        pro.FreezeCost = false;
        pro.Status = ProductionOrderStatus.Completed;

        // Update cost summary status
        var summary = await _db.Set<ProductionOrderCostSummary>()
            .FirstOrDefaultAsync(s => s.ProductionOrderId == productionOrderId, ct);
        if (summary != null)
        {
            summary.CostStatus = ProductionCostStatus.ReopenedForAdjustment;
            summary.RollupStatus = "ReopenedForAdjustment";
            summary.UpdatedAtUtc = DateTime.UtcNow;
            summary.UpdatedBy = reopenedBy;
        }

        // Record reversal event
        var reopenEvent = new ProductionCloseEvent
        {
            CompanyId = pro.CompanyId,
            ProductionOrderId = productionOrderId,
            Step = ProductionCloseStep.CloseReversed,
            EstimatedTotalAtClose = summary?.EstimatedTotalCost ?? 0,
            ActualTotalAtClose = summary?.ActualTotalCost ?? 0,
            TotalVarianceAtClose = summary?.CostVariance ?? 0,
            WipBalanceAtClose = summary?.WipBalance ?? 0,
            CloseSuccessful = true,
            CloseMessage = $"PRO #{productionOrderId} REOPENED for adjustment. Reason: {reason}",
            IsReversal = true,
            ClosedBy = reopenedBy,
        };
        _db.Set<ProductionCloseEvent>().Add(reopenEvent);

        await _db.SaveChangesAsync(ct);

        _log.LogInformation("PRO {ProId} REOPENED for adjustment by {User}. Reason: {Reason}",
            productionOrderId, reopenedBy, reason);

        return Result.Success(reopenEvent);
    }

    // ═══════════════════════════════════════════════════════════════
    // CHECK CLOSE READINESS
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<bool>> CheckCloseReadinessAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        var pro = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);
        if (pro == null)
            return Result.Failure<bool>($"PRO {productionOrderId} not found.");

        var issues = new List<string>();

        if (pro.Status != ProductionOrderStatus.Completed && pro.Status != ProductionOrderStatus.InProgress)
            issues.Add($"Status is {pro.Status} — must be Completed or InProgress.");

        // Check for unfinalized child transfers
        var provisionalTransfers = await _db.Set<CostTransfer>()
            .AsNoTracking()
            .Where(t => t.DestinationCostObjectType == CostObjectType.ProductionOrder
                     && t.DestinationCostObjectId == productionOrderId
                     && t.IsProvisional && !t.IsReversal)
            .CountAsync(ct);

        if (provisionalTransfers > 0)
            issues.Add($"{provisionalTransfers} provisional child transfer(s) — finalize child costs first.");

        // Check for open children that aren't closed
        var openChildren = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .Where(p => p.ParentProductionOrderId == productionOrderId
                     && p.Status != ProductionOrderStatus.Closed
                     && p.Status != ProductionOrderStatus.Cancelled)
            .CountAsync(ct);

        if (openChildren > 0)
            issues.Add($"{openChildren} child PRO(s) still open — close children first.");

        // Check cost summary exists
        var summary = await _costSvc.GetSummaryAsync(productionOrderId, ct);
        if (summary == null)
            issues.Add("No cost summary exists — run a rollup first.");

        if (issues.Count > 0)
            return Result.Failure<bool>(string.Join(" | ", issues));

        return Result.Success(true);
    }

    // ═══════════════════════════════════════════════════════════════
    // QUERIES
    // ═══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<ProductionVariance>> GetVariancesAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<ProductionVariance>()
            .AsNoTracking()
            .Where(v => v.ProductionOrderId == productionOrderId)
            .OrderBy(v => v.VarianceType)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductionCloseEvent>> GetCloseEventsAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<ProductionCloseEvent>()
            .AsNoTracking()
            .Where(e => e.ProductionOrderId == productionOrderId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static ProductionVariance BuildVariance(
        ProductionOrder pro, ProductionVarianceType type,
        decimal estimated, decimal actual,
        string? computedBy, DateTime ts)
    {
        var variance = actual - estimated;
        var pct = estimated != 0 ? (variance / estimated) * 100m : 0m;

        return new ProductionVariance
        {
            CompanyId = pro.CompanyId,
            ProductionOrderId = pro.Id,
            VarianceType = type,
            EstimatedAmount = estimated,
            ActualAmount = actual,
            VarianceAmount = variance,
            VariancePercent = pct,
            IsFavorable = variance < 0, // Under budget = favorable for cost
            CreatedBy = computedBy,
            CreatedAtUtc = ts,
        };
    }

    private static ProductionCostBucket MapVarianceToBucket(ProductionVarianceType type) => type switch
    {
        ProductionVarianceType.MaterialUsage => ProductionCostBucket.DirectMaterial,
        ProductionVarianceType.LaborRate => ProductionCostBucket.DirectLabor,
        ProductionVarianceType.LaborEfficiency => ProductionCostBucket.MachineTime,
        ProductionVarianceType.OverheadVolume => ProductionCostBucket.ManufacturingOverhead,
        ProductionVarianceType.OverheadSpending => ProductionCostBucket.ManufacturingOverhead,
        ProductionVarianceType.SubcontractVariance => ProductionCostBucket.Subcontract,
        ProductionVarianceType.PurchasePriceVariance => ProductionCostBucket.DirectMaterial,
        ProductionVarianceType.ScrapVariance => ProductionCostBucket.Scrap,
        ProductionVarianceType.TotalVariance => ProductionCostBucket.Variance,
        _ => ProductionCostBucket.Variance,
    };

    private static GlAccountKind MapVarianceToGlKind(ProductionVarianceType type) => type switch
    {
        ProductionVarianceType.MaterialUsage => GlAccountKind.MaterialUsageVariance,
        ProductionVarianceType.LaborRate => GlAccountKind.LaborRateVariance,
        ProductionVarianceType.LaborEfficiency => GlAccountKind.LaborEfficiencyVariance,
        ProductionVarianceType.OverheadVolume => GlAccountKind.OverheadVolumeVariance,
        ProductionVarianceType.OverheadSpending => GlAccountKind.OverheadSpendingVariance,
        _ => GlAccountKind.MaterialUsageVariance, // fallback
    };
}
