// =============================================================================
// CherryAI EAM — Receipt lifecycle state machine (Sprint 11 PR #3)
// ADR-016 §D12 — guards the StockReceipt.Status transitions across the four
// pilot workflows (PO / ASN / Blind / Partial) and the deferred v2 surface.
// =============================================================================

using System.Collections.Generic;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Receiving;

/// <summary>
/// Authorized transitions between StockReceiptStatus values. Centralized so
/// the service layer, the AI voice tools, and any future bulk-operation
/// pipeline all agree on what's legal.
///
/// Legend:
///   Available           - on the shelf, ready to consume
///   Reserved            - earmarked for an upcoming nest / job
///   PartiallyConsumed   - some quantity cut, some remaining
///   FullyConsumed       - quantity remaining is zero
///   Quarantined         - failed receiving inspection / supplier anomaly
///   Scrapped            - destroyed, can't be returned
///   Returned            - sent back to supplier
/// </summary>
public static class ReceiptStateMachine
{
    private static readonly Dictionary<StockReceiptStatus, HashSet<StockReceiptStatus>> Allowed = new()
    {
        [StockReceiptStatus.Available] = new()
        {
            StockReceiptStatus.Reserved,
            StockReceiptStatus.PartiallyConsumed,
            StockReceiptStatus.FullyConsumed,
            StockReceiptStatus.Quarantined,
            StockReceiptStatus.Scrapped,
            StockReceiptStatus.Returned,
        },
        [StockReceiptStatus.Reserved] = new()
        {
            StockReceiptStatus.Available,
            StockReceiptStatus.PartiallyConsumed,
            StockReceiptStatus.FullyConsumed,
            StockReceiptStatus.Quarantined,
        },
        [StockReceiptStatus.PartiallyConsumed] = new()
        {
            StockReceiptStatus.FullyConsumed,
            StockReceiptStatus.Quarantined,
            StockReceiptStatus.Scrapped,
        },
        [StockReceiptStatus.FullyConsumed] = new()
        {
            // Terminal — cannot un-consume. Reopening requires a reversal entry.
        },
        [StockReceiptStatus.Quarantined] = new()
        {
            StockReceiptStatus.Available,    // QC released
            StockReceiptStatus.Scrapped,     // QC condemned
            StockReceiptStatus.Returned,     // RMA back to supplier
        },
        [StockReceiptStatus.Scrapped] = new()
        {
            // Terminal.
        },
        [StockReceiptStatus.Returned] = new()
        {
            // Terminal — supplier may issue replacement under a new ReceiptNumber.
        },
    };

    public static bool CanTransition(StockReceiptStatus from, StockReceiptStatus to) =>
        Allowed.TryGetValue(from, out var set) && set.Contains(to);

    public static IReadOnlyCollection<StockReceiptStatus> AllowedNext(StockReceiptStatus from) =>
        Allowed.TryGetValue(from, out var set)
            ? (IReadOnlyCollection<StockReceiptStatus>)set
            : System.Array.Empty<StockReceiptStatus>();

    /// <summary>
    /// Human-readable failure message for an illegal transition. Used by the
    /// service layer when constructing Result.Failure.
    /// </summary>
    public static string IllegalTransitionMessage(StockReceiptStatus from, StockReceiptStatus to)
    {
        var allowed = AllowedNext(from);
        if (allowed.Count == 0)
        {
            return $"Receipt is {from} — that is a terminal state and cannot be changed.";
        }
        return $"Cannot move receipt from {from} to {to}. Allowed transitions: {string.Join(", ", allowed)}.";
    }
}
