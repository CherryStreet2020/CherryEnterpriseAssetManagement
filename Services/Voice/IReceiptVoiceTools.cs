using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Receiving;

namespace Abs.FixedAssets.Services.Voice;

// ADR-015 D10 + ADR-016 D8 — Receipt voice-tool contract.
//
// Two-phase rollout:
//
//   Phase 1 (this PR, Sprint 11 PR #4) — production-ready implementations of
//   the 10 tools ADR-016 D8 locks. The deterministic / EF-backed tools run
//   real bodies; the LLM-flavored tools (MatchOrphanReceipt, ExplainException,
//   OcrParseMillCert) ship with deterministic placeholders that Sprint 5 will
//   swap with real LLM/OCR calls. The signatures stay stable across the swap.
//
//   Phase 2 (Sprint 5) — voice-AI runtime calls these tools via the MCP
//   server. The LLM picks the right tool from natural language; the typed
//   service code does the heavy lifting (graph walks, profile-aware
//   filtering, audit logging with ActorKind=AiOnBehalfOf).
//
// Why tools, not SQL composition? The ADR-015 D10 spike found the LLM
// consistently misses three classes of query when forced to compose SQL:
//   1. Graph traversals (chain of custody through Nest → Remnant → Shipment).
//   2. "No rows yet" pivots (expected receipts live in PurchaseOrderLines).
//   3. Cross-profile natural-key resolution (heat # vs lot # vs serial # vs METRC tag).
//
// Reference: ADR-015 D10 + ADR-016 §D8 + docs/research/voice-ai-spike-adr015-d10.md §6.3.
public interface IReceiptVoiceTools
{
    // ====================================================================
    // ADR-015 D10 — the original 4 tools.
    // ====================================================================

    /// <summary>
    /// Graph-walks the receipt-to-finished-good chain for a given natural key
    /// (serial, lot, or receipt id). Returns nodes + edges suitable for the
    /// voice-AI to summarize.
    /// </summary>
    Task<Result<ChainOfCustodyGraph>> TraceChainOfCustodyAsync(
        string naturalKey,
        string direction,
        CancellationToken ct);

    /// <summary>
    /// Lists open PO lines expected to arrive in the given window.
    /// </summary>
    Task<Result<IReadOnlyList<ExpectedReceiptItem>>> ListExpectedReceiptsAsync(
        System.DateTime fromUtc,
        System.DateTime toUtc,
        int? forUserId,
        CancellationToken ct);

    /// <summary>
    /// Bulk-quarantines all receipts matching a profile-aware filter.
    /// Idempotency key required.
    /// </summary>
    Task<Result<int>> QuarantineByFilterAsync(
        string profileCode,
        IReadOnlyDictionary<string, object?> attributeFilter,
        string reason,
        int actorUserId,
        System.Guid idempotencyKey,
        CancellationToken ct);

    /// <summary>
    /// Resolves a receipt by any natural key. Disambiguates by profile scope.
    /// </summary>
    Task<Result<IReadOnlyList<StockReceipt>>> LookupReceiptAsync(
        string naturalKey,
        string? profileHint,
        CancellationToken ct);

    // ====================================================================
    // ADR-016 D8 — 6 new tools.
    // ====================================================================

    /// <summary>
    /// Today's expected arrivals at the dock — combines open POs, declared
    /// ASNs, carrier-tracking ETAs (when available), and historical lead-time
    /// means to predict what's coming and when.
    /// </summary>
    Task<Result<IReadOnlyList<ExpectedArrival>>> ListExpectedArrivalsAsync(
        string? siteCode,
        System.DateTime windowStartUtc,
        System.DateTime windowEndUtc,
        CancellationToken ct);

    /// <summary>
    /// AI candidate-PO guesser for orphan receipts. Returns up to 3 candidate
    /// POs ranked by (vendor match + item match + recency).
    /// </summary>
    Task<Result<IReadOnlyList<OrphanMatchCandidate>>> MatchOrphanReceiptAsync(
        int receiptId,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Natural-language summary of why a receipt is on the exception lane.
    /// </summary>
    Task<Result<ExceptionExplanation>> ExplainExceptionAsync(
        int receiptId,
        CancellationToken ct);

    /// <summary>
    /// Voice-driven receipt — wraps IReceivingControlCenterService.ReceiveByPoAsync.
    /// </summary>
    Task<Result<ReceiveResult>> ReceiveByVoiceAsync(
        int actorUserId,
        IdempotencyKey idempotencyKey,
        ReceiveByPoCommand command,
        VoiceContext voiceContext,
        CancellationToken ct);

    /// <summary>
    /// Voice-driven quarantine — wraps IReceivingControlCenterService.QuarantineAsync.
    /// </summary>
    Task<Result<QuarantineResult>> QuarantineByVoiceAsync(
        int actorUserId,
        IdempotencyKey idempotencyKey,
        QuarantineCommand command,
        VoiceContext voiceContext,
        CancellationToken ct);

    /// <summary>
    /// Parse a mill-cert PDF and extract heat # / mill / chemistry / mechanicals.
    /// PR #4 ships the contract; Sprint 5 wires the OCR runtime.
    /// </summary>
    Task<Result<MillCertExtraction>> OcrParseMillCertAsync(
        byte[] pdfBytes,
        string profileCode,
        CancellationToken ct);
}

// ====================================================================
// DTOs used by the tool catalog. Records, immutable by construction.
// ====================================================================

public sealed record ChainOfCustodyGraph(
    IReadOnlyList<ChainNode> Nodes,
    IReadOnlyList<ChainEdge> Edges,
    string RootNaturalKey,
    string Direction);

public sealed record ChainNode(
    string EntityType,
    long EntityId,
    string DisplayLabel,
    System.DateTime? EventAt);

public sealed record ChainEdge(
    string FromEntityType, long FromEntityId,
    string ToEntityType,   long ToEntityId,
    string Relation);

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

public sealed record ExpectedArrival(
    string Source,
    string Reference,
    string? VendorName,
    int? ItemId,
    string? ItemDescription,
    decimal ExpectedQuantity,
    string? Uom,
    System.DateTime ExpectedAtUtc,
    double ConfidenceScore,
    string? Notes);

public sealed record OrphanMatchCandidate(
    string PoNumber,
    string? PoLineId,
    string? VendorName,
    int? ItemId,
    string? ItemDescription,
    decimal? RemainingQuantity,
    double Confidence,
    string Rationale);

public sealed record ExceptionExplanation(
    int ReceiptId,
    string ReceiptNumber,
    string Kind,
    string Severity,
    string Headline,
    string DetailedNarrative,
    IReadOnlyList<string> SuggestedActions);

public sealed record VoiceContext(
    System.Guid AiSessionId,
    string? CommandText,
    string? ModelVersion,
    decimal? Confidence);

public sealed record MillCertExtraction(
    string? HeatNumber,
    string? Mill,
    string? Grade,
    IReadOnlyDictionary<string, string>? Chemistry,
    IReadOnlyDictionary<string, string>? Mechanicals,
    double? OcrConfidence,
    string? RawText);
