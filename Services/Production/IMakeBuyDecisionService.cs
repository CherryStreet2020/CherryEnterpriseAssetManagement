// Theme B7 Wave C PR-8 (2026-05-29) — IMakeBuyDecisionService (the money-shot).
//
// Given a MakeOrBuy item + qty + due date, decide MAKE vs BUY by weighing six factors
// and write an auditable MakeBuyDecision (PR-7). The disruption: the capacity factor
// (F2) reads the REAL R4-10 Load% / R4-11 finite schedule — not a coarse proxy — which
// is exactly why B11 (the resource model + finite scheduler) shipped before this.
//
//   F1  Eligibility (HARD gate) — which paths the item's policy allows; make needs a
//       routing, buy needs ≥1 approved supplier quote still valid today.
//   F2  Capacity — projected Load% on the make routing's work centers over [today,due]
//       via IResourceLoadService; the highest-load WC is the bottleneck/drum.
//   F3  Cost delta — fully-loaded make (ItemStandardCostElement) vs landed buy
//       (best valid SupplierQuote unit × qty + freight/receiving uplift).
//   F4  Break-even — order qty vs BE = fixed-make / (buy-unit − variable-make-unit).
//   F5  Lead-time fit — make completion vs vendor delivery vs the due date.
//   F6  Quality/risk — supplier on-time-delivery, single-source exposure.
//
//   Hard gates: only-feasible-path wins; IsSourceControlled forces MAKE; a make routing
//   through an over-threshold drum forces BUY when buy is within the policy's cost
//   tolerance. Otherwise BuyScore = Σ(wᵢ·scoreᵢ); BUY ≥ policy threshold.
//
// Tenant scope: the item's company. DecideAsync can persist (the audit row) or run as a
// pure what-if; ExplainAsync re-hydrates a persisted decision.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    /// <summary>One factor's contribution. Score 0..1 where higher favors BUY.</summary>
    public sealed record MakeBuyFactor(
        string Code,
        string Label,
        decimal Score,
        decimal Weight,
        decimal WeightedImpact,
        string Reason);

    /// <summary>The full decision: verdict + score + explainable factors + frozen snapshots.</summary>
    public sealed record MakeBuyDecisionResult(
        int? PersistedDecisionId,
        int ItemId,
        decimal Qty,
        DateTime? DueDate,
        MakeBuyOutcome Outcome,
        decimal BuyScore,
        decimal Confidence,
        bool WasHardGated,
        string? HardGateReason,
        string RationaleText,
        IReadOnlyList<MakeBuyFactor> Factors,
        // Snapshots
        decimal? MakeCostFullyLoaded,
        decimal? BuyCostLanded,
        string? BottleneckWorkCenterCode,
        decimal? BottleneckLoadPct,
        bool RoutedThroughDrum,
        DateTime? MakeCompletionDate,
        DateTime? VendorDeliveryDate,
        int? ChosenSupplierId,
        int? ChosenQuoteId);

    public interface IMakeBuyDecisionService
    {
        /// <summary>
        /// Decide MAKE vs BUY for <paramref name="itemId"/> at <paramref name="qty"/> due
        /// <paramref name="dueDate"/>. When <paramref name="persist"/> is true, writes a
        /// <see cref="MakeBuyDecision"/> audit row; otherwise returns a pure what-if.
        /// </summary>
        Task<Result<MakeBuyDecisionResult>> DecideAsync(
            int itemId, decimal qty, DateTime? dueDate, int? siteId,
            MakeBuyDecisionContext context, bool persist, CancellationToken ct = default);

        /// <summary>Re-hydrate and re-render a previously persisted decision (audit / Cherry Bar).</summary>
        Task<Result<MakeBuyDecisionResult>> ExplainAsync(int decisionId, CancellationToken ct = default);
    }
}
