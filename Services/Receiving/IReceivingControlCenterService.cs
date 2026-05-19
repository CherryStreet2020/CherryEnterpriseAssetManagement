// =============================================================================
// CherryAI EAM — IReceivingControlCenterService (Sprint 11 PR #3)
// ADR-016 §D7 — service surface backing the Receiving Control Center.
//
// Every Razor page handler in `/Receiving/*` AND every voice-AI tool in the
// IReceiptVoiceTools surface (PR #4) calls these methods. Same business logic,
// same Result<T> shape, same idempotency story, same audit trail.
//
// Why all returns are Result<T>:
//   Per ADR-014 D2 — service methods never throw on expected failures.
//   Validation, permission, and business-rule failures come back as
//   Result.Failure("message"); only infrastructure outages throw.
//
// Why mutations take an IdempotencyKey:
//   Per ADR-014 D4 — Stripe-style replay safety. Voice-AI fires the same
//   ReceiveByPo command twice when the network blips; the second invocation
//   returns the cached first result instead of double-receiving.
//
// Why queries take filter DTOs instead of long parameter lists:
//   The Receiving Control Center page composes filters dynamically from the
//   active tab + lane filter pills + role scope; passing a struct keeps the
//   call site simple and the service signature stable as filters grow.
// =============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;

namespace Abs.FixedAssets.Services.Receiving;

public interface IReceivingControlCenterService
{
    // ----- Queries (no idempotency, no audit) ------------------------

    /// <summary>
    /// AI-ranked list of receipts that need human attention. Drives the
    /// Exception Lane quadrant.
    /// </summary>
    Task<Result<ExceptionLanePage>> GetExceptionLaneAsync(
        ExceptionLaneFilter filter,
        CancellationToken ct);

    /// <summary>
    /// 8-tile KPI snapshot for the strip across the top of the Control
    /// Center. Per ADR-016 §D9 — dock-to-stock, accuracy, exceptions,
    /// doc-completeness, supplier-on-time, quarantine-cycle, ASN-penetration,
    /// voice-adoption.
    /// </summary>
    Task<Result<KpiStripSnapshot>> GetKpiStripAsync(
        KpiStripFilter filter,
        CancellationToken ct);

    /// <summary>
    /// Recent activity feed (delta since the caller's last-seen sequence).
    /// Drives the collapsible bottom strip.
    /// </summary>
    Task<Result<ActivityFeedDelta>> GetActivityFeedAsync(
        ActivityFeedFilter filter,
        CancellationToken ct);

    /// <summary>
    /// PO queue + preview blob for the Sprint 12A /Receiving PO Queue tab.
    /// Returns receivable POs (Approved / Sent / PartiallyReceived) mapped to
    /// the generic ICockpitQueueRow contract (queue cards) plus the parallel
    /// preview-record list serialized into the page's __poDetails JSON blob.
    /// Per ADR-018 §D3 the canvas is the daily-driver Receiving view.
    /// </summary>
    Task<Result<PoQueueData>> GetPoQueueAsync(
        PoQueueFilter filter,
        CancellationToken ct);

    /// <summary>
    /// ASN queue + preview blob for the Sprint 12A /Receiving ASN Queue tab.
    /// Returns active AdvancedShippingNotices (Expected / InTransit / Arrived /
    /// Receiving) bucketed by ExpectedArrivalDate via ByTimeLens. ASN ingestion
    /// from real EDI 856 lands in Sprint 21; for now consumes seeded data.
    /// </summary>
    Task<Result<AsnQueueData>> GetAsnQueueAsync(
        AsnQueueFilter filter,
        CancellationToken ct);

    /// <summary>
    /// 8-tile KPI band for the page header — the third leg of the Cockpit
    /// canvas per ADR-018 §D3. Mixed workload (Open POs / Overdue / Due
    /// Today / This Week) + quality (Receipts Today / Dock-to-Stock / Doc
    /// Completeness / Exceptions Open). Always rendered above the tab bar
    /// on /Receiving regardless of active tab.
    /// </summary>
    Task<Result<ReceivingKpiBandData>> GetReceivingKpiBandAsync(
        ReceivingKpiBandFilter filter,
        CancellationToken ct);

    /// <summary>
    /// "Next Up" priority preview for the right pane on first paint.
    /// Returns the single highest-priority overdue PO (or earliest-required
    /// when no overdue) plus a teaser for the second-priority PO. Sprint
    /// 12A PR #5.2 — replaces the empty welcome state.
    /// </summary>
    Task<Result<ReceivingNextUpData>> GetReceivingNextUpAsync(
        ReceivingNextUpFilter filter,
        CancellationToken ct);

    /// <summary>
    /// AI Suggestions strip — three smart hints below the workspace. Hardcoded
    /// SQL heuristics for now (batch-by-vendor, orphan-match candidates,
    /// overdue tracking). Sprint 5's voice-AI runtime swaps the producer for
    /// real model calls without changing the contract.
    /// </summary>
    Task<Result<ReceivingAiSuggestionsData>> GetReceivingAiSuggestionsAsync(
        ReceivingAiSuggestionsFilter filter,
        CancellationToken ct);

    /// <summary>
    /// PO-driven receipt — the 80% workflow. Operator confirms the PO line,
    /// scans / types the lot, the system writes the StockReceipt against the
    /// active ReceiptProfile and decrements the PO line's open quantity.
    /// </summary>
    Task<Result<ReceiveResult>> ReceiveByPoAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        ReceiveByPoCommand command,
        CancellationToken ct);

    /// <summary>
    /// ASN-driven receipt — the 10% workflow. ASN already in the system;
    /// operator scans the ASN barcode and confirms (or amends) the
    /// declared quantity.
    /// </summary>
    Task<Result<ReceiveResult>> ReceiveByAsnAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        ReceiveByAsnCommand command,
        CancellationToken ct);

    /// <summary>
    /// Blind receipt — the 5% workflow. No PO, no ASN. Creates an orphan
    /// receipt that the AI MatchOrphanReceipt tool can later attach to a
    /// candidate PO.
    /// </summary>
    Task<Result<ReceiveResult>> BlindReceiveAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        BlindReceiveCommand command,
        CancellationToken ct);

    /// <summary>
    /// Move a receipt into Quarantined status. Used for QC holds, supplier
    /// anomaly flags, MTR mismatches, etc. State-machine guarded.
    /// </summary>
    Task<Result<QuarantineResult>> QuarantineAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        QuarantineCommand command,
        CancellationToken ct);

    /// <summary>
    /// Attach an orphan receipt to a PO line. Backed by the
    /// MatchOrphanReceiptAsync voice tool (PR #4 surfaces it for AI).
    /// </summary>
    Task<Result<MatchResult>> MatchOrphanReceiptAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        MatchOrphanReceiptCommand command,
        CancellationToken ct);
}
