using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Voice;

// ADR-015 D10 (voice-AI spike) — Receipt voice-tool contract.
//
// The spike found that the LLM consistently misses three classes of
// query when forced to compose multi-table SQL or cross-profile lookups:
//   1. Graph traversals (chain of custody through Nest → Remnant → Shipment).
//   2. "No rows yet" pivots (expected receipts live in PurchaseOrderLines).
//   3. Cross-profile natural-key resolution (heat # vs lot # vs serial # vs METRC tag).
//
// The fix is to expose these as **tools** in the voice-AI tool catalog,
// not as SQL-composition prompts. The LLM picks the right tool given
// natural language; the typed service code does the heavy lifting.
//
// This interface is the contract. Bodies are stubbed (`NotImplementedException`)
// in this migration. Real implementations land in Sprint 5 alongside the
// voice-AI runtime. By shipping the contract now, the migration locks the
// tool catalog shape and lets the voice-AI integration test against stable
// interfaces.
//
// Reference: ADR-015 D10 + docs/research/voice-ai-spike-adr015-d10.md §6.3.
public interface IReceiptVoiceTools
{
    /// <summary>
    /// Graph-walks the receipt-to-finished-good chain for a given natural key
    /// (serial, lot, or receipt id). Returns nodes + edges suitable for the
    /// voice-AI to summarize ("This serial came in on PO 12345 from Nucor,
    /// was cut on nest N-99, and shipped to customer CUST-44 on 2026-04-01").
    /// </summary>
    /// <param name="naturalKey">Serial #, lot #, or receipt #.</param>
    /// <param name="direction">"upstream" / "downstream" / "both".</param>
    Task<Result<ChainOfCustodyGraph>> TraceChainOfCustodyAsync(
        string naturalKey,
        string direction,
        CancellationToken ct);

    /// <summary>
    /// Lists open PO lines expected to arrive in the given window. Powers
    /// "/Receiving/Inbox" + voice utterance "what should I receive this week."
    /// Filters out lines already fully received and lines past their PO close date.
    /// </summary>
    /// <param name="fromUtc">Window start (inclusive).</param>
    /// <param name="toUtc">Window end (inclusive).</param>
    /// <param name="forUserId">Optional — narrow to a specific receiver's queue.</param>
    Task<Result<IReadOnlyList<ExpectedReceiptItem>>> ListExpectedReceiptsAsync(
        System.DateTime fromUtc,
        System.DateTime toUtc,
        int? forUserId,
        CancellationToken ct);

    /// <summary>
    /// Bulk-quarantines all receipts matching a profile-aware filter.
    /// Voice example: "Quarantine all spinach received from SmartGreens on lot SG-2026-44".
    /// The LLM MUST first call <c>RequestConfirmation</c> (separate, voice-runtime
    /// tool) and wait for explicit user "yes" before invoking this method.
    /// Idempotency key required — repeated calls with the same key return the
    /// first run's result.
    /// </summary>
    Task<Result<int>> QuarantineByFilterAsync(
        string profileCode,
        IReadOnlyDictionary<string, object?> attributeFilter,
        string reason,
        int actorUserId,
        System.Guid idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Resolves a receipt by any natural key the voice user might offer:
    /// receipt # / lot # / serial # / heat # / NDC / GTIN / METRC tag / UDI.
    /// Returns 0..N matches across all profiles (a heat # can match multiple
    /// rows; a serial # should match exactly one). Disambiguates by profile
    /// scope per the spike's CROSS_PROFILE_GLOSSARY.
    /// </summary>
    Task<Result<IReadOnlyList<StockReceipt>>> LookupReceiptAsync(
        string naturalKey,
        string? profileHint,
        CancellationToken ct);
}

// ---- DTOs used by the tool catalog. Minimal shapes — Sprint 5 may expand. ----

public sealed record ChainOfCustodyGraph(
    IReadOnlyList<ChainNode> Nodes,
    IReadOnlyList<ChainEdge> Edges,
    string RootNaturalKey,
    string Direction);

public sealed record ChainNode(
    string EntityType,   // "StockReceipt" / "Nest" / "Remnant" / "Shipment" / "PurchaseOrderLine"
    long EntityId,
    string DisplayLabel,
    System.DateTime? EventAt);

public sealed record ChainEdge(
    string FromEntityType, long FromEntityId,
    string ToEntityType,   long ToEntityId,
    string Relation);     // "cut-from" / "remnant-of" / "shipped-as" / "received-against"

public sealed record ExpectedReceiptItem(
    long PurchaseOrderLineId,
    string PoNumber,
    string SupplierName,
    int ItemId,
    string ItemDescription,
    decimal ExpectedQuantity,
    decimal QuantityReceivedSoFar,
    string? Uom,
    System.DateTime ExpectedAtUtc,
    bool HasAsn,
    int? DefaultReceiptProfileId,
    string? DefaultReceiptProfileCode);
