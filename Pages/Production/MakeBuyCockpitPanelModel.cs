// Theme B7 Wave D PR-1 (2026-05-30) — view-model bag for _CockpitMakeBuyPanel.cshtml.
//
// The "why did we make vs buy this?" panel. Wraps the explainable
// MakeBuyDecisionResult (PR-8 DecideAsync / ExplainAsync) with the bits the
// surface needs to render it humanly: the item identity, when/why it was decided,
// and the resolved supplier name behind the frozen ChosenSupplierId snapshot.
//
// Populated by the Production Cockpit Make/Buy tab (live, read-only — renders the
// PRO item's latest persisted decision via ExplainAsync) and by the admin probe
// (deterministic Lock-16 surface — Setup → Decide&persist → render → Explain).

using System;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;

namespace Abs.FixedAssets.Pages.Production;

public sealed class MakeBuyCockpitPanelModel
{
    /// <summary>The explainable decision: verdict + BuyScore + F1–F6 factors + frozen snapshots.</summary>
    public required MakeBuyDecisionResult Result { get; init; }

    /// <summary>Item identity for the header (resolved from the decision's ItemId).</summary>
    public required string PartNumber { get; init; }
    public string? Description { get; init; }

    /// <summary>Audit context — when the decision was made and what triggered it.</summary>
    public DateTime? DecidedAtUtc { get; init; }
    public MakeBuyDecisionContext? Context { get; init; }

    /// <summary>Human name behind the frozen ChosenSupplierId snapshot (BUY verdicts).</summary>
    public string? SupplierName { get; init; }

    // ── Convenience accessors for the view ───────────────────────────────────
    public MakeBuyOutcome Outcome => Result.Outcome;
    public bool IsBuy => Result.Outcome == MakeBuyOutcome.Buy;

    /// <summary>Make − Buy cost delta (positive = buy is cheaper). Null if either snapshot is missing.</summary>
    public decimal? CostDelta =>
        Result.MakeCostFullyLoaded.HasValue && Result.BuyCostLanded.HasValue
            ? Result.MakeCostFullyLoaded.Value - Result.BuyCostLanded.Value
            : (decimal?)null;
}
