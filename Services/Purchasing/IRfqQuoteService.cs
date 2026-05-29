// Sprint 15.4 PR-20 — RFQ / Quote Flow service (CLOSES the purchasing cascade).
//
// Spec ref: docs/research/purchasing-cascade-design-2026-05-28.md PR-20 +
// Dean's locked Wave 4 enhancement (ranked quote comparison + auto-recommended
// winner). The composite ranker blends Price 50% + LeadTime 30% + SupplierOTD
// 20% (weights configurable); SupplierOTD comes from PR-18
// ISupplierPerformanceService.GetCompositeInputsAsync, with a graceful fallback
// to price+lead-time-only when a supplier has no Rolling90Days snapshot.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Purchasing;

// === Request DTOs ===

public sealed record RfqLineInput(
    string Description,
    decimal Quantity,
    string Uom,
    int? ItemId,
    string? PartNumber,
    System.DateTime? RequiredDate,
    int? ProductionSupplyDemandId,
    int? ProductionOrderId,
    int? BomLineId,
    int? OperationSequence);

public sealed record CreateRfqRequest(
    string Title,
    System.DateTime? RequiredByDate,
    int? CreatedByUserId,
    string? Notes,
    IReadOnlyList<RfqLineInput> Lines);

public sealed record QuoteLineInput(
    int SupplierRFQLineId,
    decimal QuotedQuantity,
    decimal QuotedUnitPrice,
    int LeadTimeDays);

public sealed record RecordQuoteRequest(
    int SupplierRFQId,
    int VendorId,
    string? VendorQuoteReference,
    System.DateTime? ValidUntilDate,
    string Currency,
    int LeadTimeDays,
    IReadOnlyList<QuoteLineInput> Lines);

/// <summary>Configurable composite weights. Defaults Price .5 / LeadTime .3 / OTD .2.</summary>
public sealed record RankWeights(
    decimal PriceWeight = 0.5m,
    decimal LeadTimeWeight = 0.3m,
    decimal OtdWeight = 0.2m)
{
    public static RankWeights Default { get; } = new();
}

// === Result records ===

public sealed record CreateRfqResult(int SupplierRFQId, string RfqNumber, int LinesCreated);

public sealed record RankedQuoteRow(
    int SupplierQuoteId,
    int VendorId,
    string VendorName,
    decimal TotalQuotedAmount,
    int LeadTimeDays,
    decimal? SupplierOnTimeDeliveryPct,
    decimal PriceScore,
    decimal LeadTimeScore,
    decimal? OtdScore,
    decimal CompositeScore,
    int RankPosition,
    bool IsWinner,
    string ScoreReason);

public sealed record RankQuotesResult(
    int SupplierRFQId,
    int QuotesRanked,
    int? WinningQuoteId,
    IReadOnlyList<RankedQuoteRow> Ranked);

public sealed record ConvertQuoteResult(
    int PurchaseOrderId,
    string PoNumber,
    int LinesCreated,
    int DemandLinksCreated);

/// <summary>RFQ list row for the §21 CC "Supplier RFQs" tab.</summary>
public sealed record RfqListRow(
    int SupplierRFQId,
    string RfqNumber,
    string Title,
    RfqStatus Status,
    int LineCount,
    int QuoteCount,
    string? WinnerVendorName,
    System.DateTime? RequiredByDate,
    System.DateTime CreatedAt);

public interface IRfqQuoteService
{
    /// <summary>Create a Draft RFQ + lines. Two-phase numbered RFQ-YYYY-NNNNNN.</summary>
    Task<Result<CreateRfqResult>> CreateRfqAsync(
        CreateRfqRequest request, System.DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Issue the RFQ to a set of suppliers — creates one Invited SupplierQuote per
    /// vendor and moves the RFQ to Issued. Idempotent per vendor (won't double-invite).
    /// </summary>
    Task<Result<int>> IssueRfqAsync(
        int rfqId, IReadOnlyList<int> vendorIds, System.DateTime? quotesDueUtc,
        System.DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Record a supplier's quote: per-line prices + lead time. Computes the quote
    /// total + header lead time, moves the quote to Received and the RFQ to
    /// QuotesReceived. The vendor must already have an Invited quote on the RFQ.
    /// </summary>
    Task<Result<SupplierQuote>> RecordQuoteAsync(
        RecordQuoteRequest request, System.DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// ⭐ THE ENHANCEMENT. Rank every received quote by a blended composite score
    /// (Price + LeadTime + SupplierOTD, configurable weights), stamp CompositeScore
    /// / RankPosition / IsWinner / ScoreReason / SupplierOnTimeDeliveryPct, set the
    /// RFQ to Evaluated. SupplierOTD comes from PR-18; quotes with no OTD snapshot
    /// score on price+lead-time only (weights re-normalized). Returns the ranked
    /// list (winner first).
    /// </summary>
    Task<Result<RankQuotesResult>> RankQuotesAsync(
        int rfqId, RankWeights? weights, System.DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Award a quote: mark it Awarded, the others Rejected, set RFQ.AwardedQuoteId
    /// + Status=Awarded. Requires the RFQ to have been evaluated.
    /// </summary>
    Task<Result<SupplierQuote>> AwardQuoteAsync(
        int quoteId, System.DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Convert the awarded quote into a Draft PurchaseOrder + lines, carrying each
    /// RFQ line's §17 demand linkage forward into a PurchaseOrderLineDemandLink.
    /// Atomic. Sets RFQ.ResultingPurchaseOrderId. Requires an awarded quote.
    /// </summary>
    Task<Result<ConvertQuoteResult>> ConvertQuoteToPoLineAsync(
        int quoteId, System.DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Full RFQ with lines + quotes (tenant-scoped), or null.</summary>
    Task<SupplierRFQ?> GetRfqAsync(int rfqId, CancellationToken ct = default);

    /// <summary>Ranked quote rows for an RFQ — drives the comparison partial.</summary>
    Task<IReadOnlyList<RankedQuoteRow>> GetRankedQuotesAsync(int rfqId, CancellationToken ct = default);

    /// <summary>RFQ list rows for the §21 CC "Supplier RFQs" tab.</summary>
    Task<IReadOnlyList<RfqListRow>> GetRfqListAsync(
        int maxRows, int? companyId = null, CancellationToken ct = default);
}
