// Sprint 14.4 PR-1 (2026-05-27) — Cost Transaction Service implementation.
// Atomic cost posting + transfer + summary refresh for the production sub-ledger.
// xmin concurrency on all entities.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public class CostTransactionService : ICostTransactionService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CostTransactionService> _log;
    private static long _txnCounter = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public CostTransactionService(AppDbContext db, ILogger<CostTransactionService> log)
    {
        _db = db;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════
    // POST COST — create an atomic cost transaction
    // ═══════════════════════════════════════════════════════════

    public async Task<Result<CostTransaction>> PostCostAsync(
        CostObjectType costObjectType, int costObjectId,
        CostTransactionType transactionType, ProductionCostBucket costBucket,
        int companyId, int? siteId, int? productionOrderId,
        int? operationId, int? bomLineId, int? itemId,
        decimal quantity, string? uom, decimal unitCost,
        string? sourceTransactionType, int? sourceTransactionId,
        string? lotNumber, string? serialNumber, string? heatNumber,
        bool rollupAdditive, string? notes, string? postedBy,
        CancellationToken ct = default)
    {
        var ts = DateTime.UtcNow;
        var txnNumber = $"CTX-{ts:yyyyMMddHHmmssfff}-{Interlocked.Increment(ref _txnCounter) % 100000:D5}";

        var txn = new CostTransaction
        {
            TenantId = null, // resolved by ITenantContext in PR-2
            CompanyId = companyId,
            SiteId = siteId,
            CostObjectType = costObjectType,
            CostObjectId = costObjectId,
            TransactionNumber = txnNumber,
            TransactionType = transactionType,
            CostBucket = costBucket,
            SourceTransactionType = sourceTransactionType,
            SourceTransactionId = sourceTransactionId,
            ProductionOrderId = productionOrderId,
            OperationId = operationId,
            BomLineId = bomLineId,
            ItemId = itemId,
            Quantity = quantity,
            Uom = uom,
            UnitCost = unitCost,
            ExtendedCost = quantity * unitCost,
            CostElement = MapBucketToElement(costBucket),
            EffectiveCostDate = ts,
            RollupAdditiveFlag = rollupAdditive,
            LotNumber = lotNumber,
            SerialNumber = serialNumber,
            HeatNumber = heatNumber,
            IncludedInJobMargin = true,
            IncludedInProjectMargin = true,
            Notes = notes,
            CreatedBy = postedBy,
        };

        _db.Set<CostTransaction>().Add(txn);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "CostTransaction {Number}: {Type} {Bucket} ${Cost:N2} on {ObjType}#{ObjId} (PRO={PRO}, Site={Site})",
            txn.TransactionNumber, transactionType, costBucket,
            txn.ExtendedCost, costObjectType, costObjectId, productionOrderId, siteId);

        return Result.Success(txn);
    }

    // ═══════════════════════════════════════════════════════════
    // POST TRANSFER — move value between cost objects (Layer B)
    // ═══════════════════════════════════════════════════════════

    public async Task<Result<CostTransfer>> PostTransferAsync(
        CostObjectType sourceCostObjectType, int sourceCostObjectId, int? sourceSiteId,
        CostObjectType destCostObjectType, int destCostObjectId, int? destSiteId,
        int companyId, CostTransferType transferType,
        decimal quantity, string? uom, decimal unitCost,
        decimal materialCost, decimal laborCost, decimal overheadCost,
        decimal subcontractCost, decimal otherCost,
        bool isProvisional, string? notes, string? postedBy,
        CancellationToken ct = default)
    {
        var ts = DateTime.UtcNow;
        var xferNumber = $"CXF-{ts:yyyyMMddHHmmssfff}-{Interlocked.Increment(ref _txnCounter) % 100000:D5}";

        var xfer = new CostTransfer
        {
            CompanyId = companyId,
            TransferNumber = xferNumber,
            SourceCostObjectType = sourceCostObjectType,
            SourceCostObjectId = sourceCostObjectId,
            SourceSiteId = sourceSiteId,
            DestinationCostObjectType = destCostObjectType,
            DestinationCostObjectId = destCostObjectId,
            DestinationSiteId = destSiteId,
            TransferQuantity = quantity,
            Uom = uom,
            TransferUnitCost = unitCost,
            TransferExtendedCost = quantity * unitCost,
            MaterialCostTransferred = materialCost,
            LaborCostTransferred = laborCost,
            OverheadCostTransferred = overheadCost,
            SubcontractCostTransferred = subcontractCost,
            OtherCostTransferred = otherCost,
            TransferType = transferType,
            IsProvisional = isProvisional,
            IsFinal = !isProvisional,
            FinalizedAtUtc = isProvisional ? null : ts,
            Notes = notes,
            CreatedBy = postedBy,
        };

        _db.Set<CostTransfer>().Add(xfer);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "CostTransfer {Number}: {Type} ${Cost:N2} from {SrcType}#{SrcId} → {DstType}#{DstId} (Site {SrcSite}→{DstSite})",
            xfer.TransferNumber, transferType, xfer.TransferExtendedCost,
            sourceCostObjectType, sourceCostObjectId,
            destCostObjectType, destCostObjectId,
            sourceSiteId, destSiteId);

        return Result.Success(xfer);
    }

    // ═══════════════════════════════════════════════════════════
    // READ — query cost transactions
    // ═══════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<CostTransaction>> GetForCostObjectAsync(
        CostObjectType costObjectType, int costObjectId, CancellationToken ct = default)
    {
        return await _db.Set<CostTransaction>()
            .AsNoTracking()
            .Where(t => t.CostObjectType == costObjectType && t.CostObjectId == costObjectId)
            .OrderBy(t => t.EffectiveCostDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CostTransaction>> GetForProductionOrderAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<CostTransaction>()
            .AsNoTracking()
            .Where(t => t.ProductionOrderId == productionOrderId)
            .OrderBy(t => t.EffectiveCostDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CostTransfer>> GetTransfersForObjectAsync(
        CostObjectType costObjectType, int costObjectId, CancellationToken ct = default)
    {
        return await _db.Set<CostTransfer>()
            .AsNoTracking()
            .Where(t => (t.SourceCostObjectType == costObjectType && t.SourceCostObjectId == costObjectId)
                     || (t.DestinationCostObjectType == costObjectType && t.DestinationCostObjectId == costObjectId))
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<ProductionOrderCostSummary?> GetSummaryAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<ProductionOrderCostSummary>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductionOrderId == productionOrderId, ct);
    }

    // ═══════════════════════════════════════════════════════════
    // REFRESH SUMMARY — rebuild denormalized cost summary for PRO
    // ═══════════════════════════════════════════════════════════

    public async Task<Result<ProductionOrderCostSummary>> RefreshSummaryAsync(
        int productionOrderId, string? refreshedBy, CancellationToken ct = default)
    {
        var pro = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);
        if (pro == null)
            return Result.Failure<ProductionOrderCostSummary>($"Production order {productionOrderId} not found.");

        // Pull all cost transactions for this PRO
        var txns = await _db.Set<CostTransaction>()
            .AsNoTracking()
            .Where(t => t.ProductionOrderId == productionOrderId && t.RollupAdditiveFlag && !t.IsReversal)
            .ToListAsync(ct);

        // Pull transfers where this PRO is the destination (child supply transfers IN)
        var transfersIn = await _db.Set<CostTransfer>()
            .AsNoTracking()
            .Where(t => t.DestinationCostObjectType == CostObjectType.ProductionOrder
                     && t.DestinationCostObjectId == productionOrderId
                     && !t.IsReversal)
            .ToListAsync(ct);

        // Pull transfers where this PRO is the source (child cost transferred OUT)
        var transfersOut = await _db.Set<CostTransfer>()
            .AsNoTracking()
            .Where(t => t.SourceCostObjectType == CostObjectType.ProductionOrder
                     && t.SourceCostObjectId == productionOrderId
                     && !t.IsReversal)
            .ToListAsync(ct);

        var ts = DateTime.UtcNow;

        // Find or create summary
        var summary = await _db.Set<ProductionOrderCostSummary>()
            .FirstOrDefaultAsync(s => s.ProductionOrderId == productionOrderId, ct);

        if (summary == null)
        {
            summary = new ProductionOrderCostSummary
            {
                CompanyId = pro.CompanyId,
                ProductionOrderId = productionOrderId,
            };
            _db.Set<ProductionOrderCostSummary>().Add(summary);
        }

        // Aggregate actuals by bucket
        summary.ActualMaterialCost = SumBuckets(txns, ProductionCostBucket.DirectMaterial, ProductionCostBucket.PurchasedToJob);
        summary.ActualLaborCost = SumBucket(txns, ProductionCostBucket.DirectLabor);
        summary.ActualMachineCost = SumBucket(txns, ProductionCostBucket.MachineTime);
        summary.ActualBurdenCost = SumBuckets(txns, ProductionCostBucket.LaborBurden, ProductionCostBucket.MachineBurden, ProductionCostBucket.ManufacturingOverhead);
        summary.ActualOutsideProcessingCost = SumBucket(txns, ProductionCostBucket.OutsideProcessing);
        summary.ActualSubcontractCost = SumBucket(txns, ProductionCostBucket.Subcontract);
        summary.ActualFreightLandedCost = SumBucket(txns, ProductionCostBucket.LandedCost);
        summary.ActualToolingCost = SumBucket(txns, ProductionCostBucket.Tooling);
        summary.ActualScrapReworkCost = SumBuckets(txns, ProductionCostBucket.Scrap, ProductionCostBucket.Rework);
        summary.ActualTotalCost = txns.Sum(t => t.ExtendedCost);

        // Transfer tracking
        summary.ParentCostTransferIn = transfersIn.Sum(t => t.TransferExtendedCost);
        summary.ChildCostTransferOut = transfersOut.Sum(t => t.TransferExtendedCost);

        // WIP = actual total - completed value - settled value
        summary.WipBalance = summary.ActualTotalCost + summary.ParentCostTransferIn
                           - summary.CompletedValue - summary.ClosedSettledValue;

        // Variance = actual - estimated
        summary.CostVariance = summary.ActualTotalCost - summary.EstimatedTotalCost;

        // EAC = actual + committed + forecast remaining
        summary.EstimateAtCompletion = summary.ActualTotalCost + summary.OpenCommittedCost + summary.ForecastRemainingCost;

        // Stamp PRO header cost columns
        pro = await _db.Set<ProductionOrder>()
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);
        if (pro != null && !pro.FreezeCost)
        {
            pro.MaterialCost = summary.ActualMaterialCost;
            pro.LaborCost = summary.ActualLaborCost;
            pro.OverheadCost = summary.ActualBurdenCost;
            pro.SubcontractCost = summary.ActualSubcontractCost;
            pro.ActualCost = summary.ActualTotalCost;
        }

        summary.LastRollupTimestamp = ts;
        summary.RollupStatus = "Complete";
        summary.UpdatedAtUtc = ts;
        summary.UpdatedBy = refreshedBy;

        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Cost summary refreshed for PRO {ProId}: Actual=${Actual:N2} WIP=${Wip:N2} Variance=${Var:N2}",
            productionOrderId, summary.ActualTotalCost, summary.WipBalance, summary.CostVariance);

        return Result.Success(summary);
    }

    // ═══════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════

    private static decimal SumBucket(IReadOnlyList<CostTransaction> txns, ProductionCostBucket bucket)
        => txns.Where(t => t.CostBucket == bucket).Sum(t => t.ExtendedCost);

    private static decimal SumBuckets(IReadOnlyList<CostTransaction> txns, params ProductionCostBucket[] buckets)
        => txns.Where(t => buckets.Contains(t.CostBucket)).Sum(t => t.ExtendedCost);

    private static CostElementType MapBucketToElement(ProductionCostBucket bucket) => bucket switch
    {
        ProductionCostBucket.DirectMaterial or ProductionCostBucket.PurchasedToJob => CostElementType.Material,
        ProductionCostBucket.ChildSupply => CostElementType.Material,
        ProductionCostBucket.DirectLabor => CostElementType.Labor,
        ProductionCostBucket.MachineTime => CostElementType.Labor,
        ProductionCostBucket.LaborBurden or ProductionCostBucket.MachineBurden => CostElementType.VariableOverhead,
        ProductionCostBucket.ManufacturingOverhead => CostElementType.FixedOverhead,
        ProductionCostBucket.OutsideProcessing or ProductionCostBucket.Subcontract => CostElementType.Subcontract,
        ProductionCostBucket.Tooling => CostElementType.Tooling,
        ProductionCostBucket.LandedCost => CostElementType.Other,
        ProductionCostBucket.Quality or ProductionCostBucket.Packaging => CostElementType.Other,
        ProductionCostBucket.Scrap or ProductionCostBucket.Rework => CostElementType.Other,
        ProductionCostBucket.Engineering => CostElementType.Labor,
        ProductionCostBucket.Adjustment or ProductionCostBucket.Variance => CostElementType.Other,
        _ => CostElementType.Other,
    };
}
