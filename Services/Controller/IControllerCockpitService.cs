using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abs.FixedAssets.Services.Controller;

// Sprint 12.7 PR #2 — Controller Control Center source-to-GL drilldown service.
//
// Powers the Drilldown tab of /Controller. Single entry-point `TraceAsync(query)`
// parses the caller's query string (loose grammar — see ParseEntityRef below),
// resolves it to a typed entity reference, and walks the chain of related
// entities + GL postings in the right order for the CFO to scan.
//
// Walks supported in this PR:
//
//   Asset:N   →  Asset header
//                CipCapitalization (1+) → CipProject → CipCosts (top 10)
//                Recent Depreciation JEs (top 12) → JournalLines (top 6 per JE)
//
//   JE:N      →  JournalEntry header
//                JournalLines with AccountingKey 8-segment context
//                Reverse-walk via JE.Source: "Depreciation" / "CIP" / "AP" / "Receiving"
//
// Walks NOT supported in this PR (queued for later):
//
//   PO:N      →  Receipt + Invoice + posting JEs   [PR #3+, after voice intent]
//   INV:N     →  AP posting JE                     [PR #3+]
//   WO:N      →  Maintenance posting JE            [PR #4+]
//
// Lock 15 compliance:
//   - Constructor injects AppDbContext for AsNoTracking() reads only — zero
//     DbContext mutation, no SaveChanges calls.
//   - No raw SQL. Typed LINQ queries throughout.
//   - No magic strings for account numbers — defers to IGlAccountResolver
//     when the resolver is needed (depreciation account lookup for the
//     Asset arm). Source-system identification uses JE.Source field values
//     established by JournalGenerator + posting services.
//
// Race-and-tenant notes:
//   - All queries scope through Asset.CompanyId / JournalEntry.Book.CompanyId.
//     Cross-tenant leakage is impossible if the caller passes a query whose
//     entity exists in another tenant — the resolver finds the entity
//     by Id but the chain walk reads accompanying rows by tenant FK.
//   - Asset → CIP capitalization chain is FK-traversed (no join-fan), so
//     concurrent writes during the trace can only ADD new costs/runs that
//     post-date the read snapshot; nothing in the trace deletes history.
//
// Performance shape:
//   - One round-trip for Asset + CipCapitalizations + CipProject (one query
//     with Includes; ~10 rows worst-case).
//   - One round-trip for top-10 CipCosts per project (small set).
//   - One round-trip for top-12 Depreciation JEs (filtered by Source +
//     account-string match on lines).
//   - One round-trip per featured JE for its lines (top-12 JEs × 6 lines = 72
//     rows worst-case).
//   - Total: ~5 round-trips, all bounded. No N+1 risk because every collection
//     read is .Take(N)-bounded.
public interface IControllerCockpitService
{
    /// <summary>
    /// Parse the caller's query, resolve it to a typed entity reference,
    /// and walk the chain. Empty/unparseable queries return
    /// <see cref="ChainTraceResult.NotResolved"/> with helpful narration.
    /// </summary>
    /// <param name="query">Loose entity reference. Accepted forms:
    /// <list type="bullet">
    /// <item><c>ASSET-1234</c> / <c>ASSET 1234</c> / <c>asset:1234</c> — explicit asset</item>
    /// <item><c>JE-1234</c> / <c>journal:1234</c> — explicit journal entry</item>
    /// <item><c>1234</c> — bare integer, assumed asset Id</item>
    /// </list>
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<ChainTraceResult> TraceAsync(string? query, CancellationToken ct = default);
}

/// <summary>
/// Outcome of <see cref="IControllerCockpitService.TraceAsync"/>. Razor reads
/// <see cref="IsResolved"/> to decide between rendering the chain or the
/// guidance message.
/// </summary>
public sealed class ChainTraceResult
{
    public bool IsResolved { get; init; }
    public string Headline { get; init; } = "";
    public string? Subtitle { get; init; }
    public string? Narration { get; init; }
    public IReadOnlyList<ChainStep> Steps { get; init; } = System.Array.Empty<ChainStep>();

    public static ChainTraceResult NotResolved(string headline, string narration) => new()
    {
        IsResolved = false,
        Headline = headline,
        Narration = narration,
        Steps = System.Array.Empty<ChainStep>(),
    };
}

/// <summary>
/// One step in the source-to-GL chain. Step ordering is the natural
/// reading order — TOP of the chain (anchor entity) first, GL postings
/// last. Razor renders these as a vertical timeline.
/// </summary>
/// <param name="StepType">Discriminator. Drives the icon + tone of the
/// rendered card. Known values: <c>Asset</c> · <c>CipProject</c> ·
/// <c>CipCost</c> · <c>JournalEntry</c> · <c>JournalLine</c> ·
/// <c>Vendor</c> · <c>Invoice</c> · <c>PurchaseOrder</c>.</param>
/// <param name="StepKey">Stable key used by Razor for keyed reconciliation
/// (e.g. <c>"ASSET-4231"</c>, <c>"JE-1284"</c>).</param>
/// <param name="Eyebrow">Short label above the headline, ALL CAPS by
/// convention (e.g. <c>"ASSET"</c>, <c>"DEPRECIATION JE"</c>).</param>
/// <param name="Headline">The primary identification line (e.g.
/// <c>"Mazak Integrex i-300ST — #ASSET-4231"</c>).</param>
/// <param name="Subtext">One-line explanation (e.g.
/// <c>"Acq Cost $1.8M · NBV $1.24M · In service Mar 15, 2024"</c>).</param>
/// <param name="AmountText">Optional formatted monetary value
/// (e.g. <c>"$14,167.00"</c>). NULL when not applicable.</param>
/// <param name="DateText">Optional formatted date (e.g.
/// <c>"Apr 30, 2026"</c>). NULL when not applicable.</param>
/// <param name="DeepLinkHref">If set, the step renders as an anchor.
/// (e.g. <c>"/Assets/Asset?id=4231"</c>).</param>
/// <param name="Narration">One-sentence plain-English explanation. Drives
/// the natural-language voice flow in PR #3. (e.g. <c>"Capitalized on
/// Mar 15, 2024 from Capital Project CP-2024-007 via PO from Mazak USA,
/// $1.8M."</c>).</param>
/// <param name="SegmentChips">Optional AccountingKey segment chips
/// ("CC=110100", "Dept=2009"). Rendered as small pills under the
/// JournalLine step. NULL/empty when the step isn't a JournalLine or the
/// AccountingKey is unresolved.</param>
public sealed record ChainStep(
    string StepType,
    string StepKey,
    string Eyebrow,
    string Headline,
    string? Subtext = null,
    string? AmountText = null,
    string? DateText = null,
    string? DeepLinkHref = null,
    string? Narration = null,
    IReadOnlyList<string>? SegmentChips = null);
