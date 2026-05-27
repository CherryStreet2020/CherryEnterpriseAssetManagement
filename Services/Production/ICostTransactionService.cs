// Sprint 14.4 PR-1 (2026-05-27) — Cost Transaction Service interface.
// CRUD + query operations on the production cost sub-ledger.
// PR-2 will add the posting integration that calls these from
// material/labor/operation/completion services.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production;

public interface ICostTransactionService
{
    // ── Write ───────────────────────────────────────────────────
    Task<Result<CostTransaction>> PostCostAsync(
        CostObjectType costObjectType, int costObjectId,
        CostTransactionType transactionType, ProductionCostBucket costBucket,
        int companyId, int? siteId, int? productionOrderId,
        int? operationId, int? bomLineId, int? itemId,
        decimal quantity, string? uom, decimal unitCost,
        string? sourceTransactionType, int? sourceTransactionId,
        string? lotNumber, string? serialNumber, string? heatNumber,
        bool rollupAdditive, string? notes, string? postedBy,
        CancellationToken ct = default);

    Task<Result<CostTransfer>> PostTransferAsync(
        CostObjectType sourceCostObjectType, int sourceCostObjectId, int? sourceSiteId,
        CostObjectType destCostObjectType, int destCostObjectId, int? destSiteId,
        int companyId, CostTransferType transferType,
        decimal quantity, string? uom, decimal unitCost,
        decimal materialCost, decimal laborCost, decimal overheadCost,
        decimal subcontractCost, decimal otherCost,
        bool isProvisional, string? notes, string? postedBy,
        CancellationToken ct = default);

    // ── Read ────────────────────────────────────────────────────
    Task<IReadOnlyList<CostTransaction>> GetForCostObjectAsync(
        CostObjectType costObjectType, int costObjectId, CancellationToken ct = default);

    Task<IReadOnlyList<CostTransaction>> GetForProductionOrderAsync(
        int productionOrderId, CancellationToken ct = default);

    Task<IReadOnlyList<CostTransfer>> GetTransfersForObjectAsync(
        CostObjectType costObjectType, int costObjectId, CancellationToken ct = default);

    Task<ProductionOrderCostSummary?> GetSummaryAsync(
        int productionOrderId, CancellationToken ct = default);

    // ── Summary refresh ─────────────────────────────────────────
    Task<Result<ProductionOrderCostSummary>> RefreshSummaryAsync(
        int productionOrderId, string? refreshedBy, CancellationToken ct = default);
}
