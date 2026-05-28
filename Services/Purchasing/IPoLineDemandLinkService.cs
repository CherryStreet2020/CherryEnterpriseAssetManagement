// Sprint 15.1 PR-3 (2026-05-28) — PO-Line to Demand Link service interface.
//
// Orchestrates the M:M between PurchaseOrderLine and ProductionSupplyDemand.
// Used by buyers + consolidation engine (PR-14) + receiving (PR-15) +
// invoice-match (PR-19).

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Purchasing;

public sealed record LinkPoLineToDemandRequest(
    int PurchaseOrderLineId,
    int ProductionSupplyDemandId,
    decimal AllocatedQuantity,
    int? PurchaseOrderReleaseId,
    System.DateTime? PromiseDate,
    System.DateTime? NeedByDate,
    string? Notes,
    string? CreatedBy);

public sealed record LinkPoLineToDemandResult(
    int LinkId,
    int PurchaseOrderLineId,
    int ProductionSupplyDemandId,
    decimal AllocatedQuantity,
    decimal AggregateAllocatedAcrossDemand,
    decimal RemainingDemand,
    string? Message);

public sealed record RecordReceiptAgainstLinkRequest(
    int LinkId,
    decimal QuantityReceived,
    System.DateTime? ReceivedAtUtc,
    string? Notes);

public sealed record RecordReceiptAgainstLinkResult(
    int LinkId,
    decimal CumulativeReceivedOnLink,
    PoDemandLinkStatus NewStatus,
    string? Message);

public sealed record ReleasePoLineDemandLinkResult(
    int LinkId,
    decimal QuantityReleased,
    string? Message);

public interface IPoLineDemandLinkService
{
    /// <summary>
    /// Create a new PO-line → demand link, or top up the allocation on an
    /// existing active link for the same tuple. Also creates the matching
    /// ProductionSupplyAllocation in the generic table for unified queries.
    /// Idempotent per (PurchaseOrderLineId, ProductionSupplyDemandId, PurchaseOrderReleaseId).
    /// </summary>
    Task<Result<LinkPoLineToDemandResult>> LinkAsync(
        LinkPoLineToDemandRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Record a receipt against a specific link (consolidation-aware
    /// receipts call this per allocated demand portion).
    /// </summary>
    Task<Result<RecordReceiptAgainstLinkResult>> RecordReceiptAgainstLinkAsync(
        RecordReceiptAgainstLinkRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Release a link — supply freed for re-allocation to other demands.
    /// </summary>
    Task<Result<ReleasePoLineDemandLinkResult>> ReleaseAsync(
        int linkId,
        string? reasonNotes,
        string? releasedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Mark the PO line as Direct-to-Job and stamp the dominant PRO/BOM line
    /// from the highest-quantity active link.
    /// </summary>
    Task<Result<int>> MarkPoLineDirectToJobAsync(
        int purchaseOrderLineId,
        bool isDirectToJob,
        CancellationToken ct = default);

    /// <summary>
    /// Mark a PO line as a subcontract service purchase.
    /// </summary>
    Task<Result<int>> MarkPoLineSubcontractAsync(
        int purchaseOrderLineId,
        bool isSubcontract,
        CancellationToken ct = default);

    /// <summary>List all active links for a PO line.</summary>
    Task<IReadOnlyList<PurchaseOrderLineDemandLink>> GetLinksForPoLineAsync(
        int purchaseOrderLineId,
        CancellationToken ct = default);

    /// <summary>List all active links for a demand.</summary>
    Task<IReadOnlyList<PurchaseOrderLineDemandLink>> GetLinksForDemandAsync(
        int productionSupplyDemandId,
        CancellationToken ct = default);
}
