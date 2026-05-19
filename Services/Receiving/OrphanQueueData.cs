// =============================================================================
// CherryAI EAM — Orphan Queue contracts (Sprint 12A PR #7)
//
// Drives the /Receiving "Orphans" tab — the third real Cockpit canvas inside
// the four-tab shell.
//
// "Orphan" = a StockReceipt with no SourcePoNumber. These are the everyday
// fingerprints of imperfect supplier paperwork:
//   - walk-in delivery without a PO yet entered
//   - blanket-PO release without a specific release number on the slip
//   - NCR replacement against an already-closed PO
//   - paper-only PO that never made it into the ERP
//   - damaged label / driver-said-the-paper-was-on-the-box
//
// What makes this tab different from PO Queue / ASN Queue:
//   - it's powered by AI-suggested candidate POs, not just raw rows
//   - each preview surfaces 0-3 ranked PO suggestions with score breakdown
//   - one-click "Match" CTA hands off to the existing
//     IReceivingControlCenterService.MatchOrphanReceiptAsync command
//
// Bucketing semantic on the time axis:
//   ASN Queue   — bucket by ETA (future-facing)
//   PO Queue    — bucket by RequiredDate (need-by)
//   Orphan Queue — bucket by ReceivedAt (the LONGER it sits, the worse it is)
//
// Same ByTimeLens<T> primitive — different domain meaning for the date.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Abs.FixedAssets.Services.Navigation.Cockpit;

namespace Abs.FixedAssets.Services.Receiving;

public sealed class OrphanQueueFilter
{
    public string? SiteCode { get; init; }
}

public sealed class OrphanQueueData
{
    public IReadOnlyList<OrphanQueueRow> Rows { get; init; } = Array.Empty<OrphanQueueRow>();
    public IReadOnlyList<OrphanQueuePreview> Previews { get; init; } = Array.Empty<OrphanQueuePreview>();
}

// Queue card row. ICockpitQueueRow contract.
//
// RequiredAt holds ReceivedAt — the date that drives bucketing. The cockpit
// renders cards into buckets via ByTimeLens — Today / This week / Older.
// For orphans, OLDER is the worst case, so we INVERT the tone semantics:
// the older the orphan, the more urgent the tone.
//
// DaysAged is the count of days since ReceivedAt — surfaced in the meta
// row so receivers see the dwell at a glance.
public sealed record OrphanQueueRow(
    string Id,
    string Primary,
    string Secondary,
    DateTime? RequiredAt,
    string Tone,
    IReadOnlyList<MetaTriple> Meta,
    string? StatusLabel = null,
    string? StatusTone = null,
    int? DaysAged = null) : ICockpitQueueRow;

// Preview-pane record — serialized via CockpitPreviewSerializer into the
// __orphanDetails JSON blob the cockpit.js#selectOrphan function hydrates.
public sealed class OrphanQueuePreview
{
    [CockpitPreviewVisible("id")]
    public int Id { get; init; }

    [CockpitPreviewVisible("receiptNumber")]
    public string ReceiptNumber { get; init; } = "";

    [CockpitPreviewVisible("itemPartNumber")]
    public string ItemPartNumber { get; init; } = "";

    [CockpitPreviewVisible("itemDescription")]
    public string ItemDescription { get; init; } = "";

    [CockpitPreviewVisible("preferredVendor")]
    public string? PreferredVendor { get; init; }

    [CockpitPreviewVisible("lotNumber")]
    public string? LotNumber { get; init; }

    [CockpitPreviewVisible("quantity")]
    public string Quantity { get; init; } = "";  // "20 EA"

    [CockpitPreviewVisible("receivedAt")]
    public string ReceivedAt { get; init; } = "";

    [CockpitPreviewVisible("daysAged")]
    public int DaysAged { get; init; }

    [CockpitPreviewVisible("notes")]
    public string? Notes { get; init; }

    // 0-3 ranked candidate POs the AI scoring suggests. Top score first.
    [CockpitPreviewVisible("candidates")]
    public IReadOnlyList<OrphanCandidatePo> Candidates { get; init; } = Array.Empty<OrphanCandidatePo>();
}

// One AI-suggested candidate PO line for matching. Each surfaces both the
// total score and the per-signal breakdown so the receiver sees WHY the AI
// thinks this PO is the match.
//
// Scoring (max 100):
//   ItemMatch     40   — this PO has a line for the same ItemId
//   VendorMatch   40   — this PO is from the item's preferred vendor
//   Recency       20   — the PO was opened within the last 14 days
//
// JsonPropertyName drives the nested-object serialization (see AsnQueueLine
// for the same pattern — CockpitPreviewSerializer's attribute filter only
// applies to the parent class).
public sealed class OrphanCandidatePo
{
    [JsonPropertyName("poNumber")]
    public string PoNumber { get; init; } = "";

    [JsonPropertyName("vendor")]
    public string Vendor { get; init; } = "";

    [JsonPropertyName("orderDate")]
    public string OrderDate { get; init; } = "";

    [JsonPropertyName("requiredDate")]
    public string? RequiredDate { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("score")]
    public int Score { get; init; }

    [JsonPropertyName("itemMatch")]
    public bool ItemMatch { get; init; }

    [JsonPropertyName("vendorMatch")]
    public bool VendorMatch { get; init; }

    [JsonPropertyName("recencyMatch")]
    public bool RecencyMatch { get; init; }

    [JsonPropertyName("matchUrl")]
    public string MatchUrl { get; init; } = "";

    // Short rationale chips — e.g. "Same Item", "Preferred Vendor", "Opened 3d ago"
    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}
