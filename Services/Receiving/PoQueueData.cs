// =============================================================================
// CherryAI EAM — PO Queue contracts (Sprint 12A PR #5)
// ADR-018 §D3 — first real Cockpit canvas inside the /Receiving four-tab shell.
//
// What this file holds:
//   - PoQueueData     : tuple returned by GetPoQueueAsync (rows + preview blob)
//   - PoQueueFilter   : input filter for GetPoQueueAsync
//   - PoQueueRow      : ICockpitQueueRow for the left-rail queue cards
//   - PoQueuePreview  : right-pane preview record, opt-in via [CockpitPreviewVisible]
//   - PoQueueLine     : nested line-item record inside a preview entry
//
// Why one file: every type here is a value record < 30 LOC and they only make
// sense as a set. The legacy /Receiving/Cockpit-Legacy hydrated the same shape
// inline; this file is the typed contract that replaces that.
//
// Why preview records use [JsonPropertyName] on nested PoQueueLine: the parent
// PoQueuePreview is serialized via CockpitPreviewSerializer (opt-in
// [CockpitPreviewVisible]) which writes each property's value through the
// default JsonSerializer. For nested objects, JsonSerializer falls back to
// JsonPropertyName conventions, so we explicitly tag every nested field to
// keep the legacy "partNum / desc / uom / ..." camel-case schema that
// wwwroot/js/cockpit.js (selectPO) is hard-coded against.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Abs.FixedAssets.Services.Navigation.Cockpit;

namespace Abs.FixedAssets.Services.Receiving;

// Filter passed to GetPoQueueAsync. SiteCode follows the existing
// IReceivingControlCenterService convention (KpiStripFilter, ExceptionLaneFilter).
public sealed class PoQueueFilter
{
    public string? SiteCode { get; init; }
}

// Combined response — the queue rows (used to render the left-rail cards via
// ByTimeLens) plus the preview blob entries (serialized into the inline
// __poDetails script tag for cockpit.js to hydrate from).
public sealed class PoQueueData
{
    public IReadOnlyList<PoQueueRow> Rows { get; init; } = Array.Empty<PoQueueRow>();
    public IReadOnlyList<PoQueuePreview> Previews { get; init; } = Array.Empty<PoQueuePreview>();
}

// One queue card. Implements the generic ICockpitQueueRow contract so the
// shared _CockpitQueueCard.cshtml partial can render it without knowing
// anything about purchase orders.
//
// Tone is derived in ReceivingControlCenterService:
//   "danger"  → RequiredDate < today  (overdue)
//   "warning" → RequiredDate == today (due today)
//   "info"    → RequiredDate within 7 days
//   "neutral" → later / null
public sealed record PoQueueRow(
    string Id,
    string Primary,
    string Secondary,
    DateTime? RequiredAt,
    string Tone,
    IReadOnlyList<MetaTriple> Meta,
    string? StatusLabel = null,
    string? StatusTone = null,
    // Sprint 12A PR #5.2 — days overdue sub-line on overdue queue cards
    // ("113d late"). Null on non-overdue cards.
    int? DaysOverdue = null,
    // Numeric total value used by the Open POs KPI tile to summarize
    // backlog ($248K backlog, ...). Optional — pure presentation.
    decimal? TotalValue = null) : ICockpitQueueRow;

// Right-pane preview entry — serialized into the page's __poDetails JSON blob.
// Only properties marked [CockpitPreviewVisible] are emitted. Every property
// here is deliberately "yes, ship to the client": no vendor pricing rules, no
// audit metadata, no PII.
//
// JSON key matches the legacy schema (id / num / vendor / orderDate / ...)
// because wwwroot/js/cockpit.js is hard-coded to those keys.
public sealed class PoQueuePreview
{
    [CockpitPreviewVisible("id")]
    public int Id { get; init; }

    [CockpitPreviewVisible("num")]
    public string Num { get; init; } = "";

    [CockpitPreviewVisible("vendor")]
    public string Vendor { get; init; } = "";

    [CockpitPreviewVisible("orderDate")]
    public string OrderDate { get; init; } = "";

    [CockpitPreviewVisible("requiredDate")]
    public string RequiredDate { get; init; } = "";

    [CockpitPreviewVisible("status")]
    public string Status { get; init; } = "";

    [CockpitPreviewVisible("total")]
    public string Total { get; init; } = "";

    [CockpitPreviewVisible("shipTo")]
    public string ShipTo { get; init; } = "";

    [CockpitPreviewVisible("lines")]
    public IReadOnlyList<PoQueueLine> Lines { get; init; } = Array.Empty<PoQueueLine>();
}

// One line inside a preview entry. Uses JsonPropertyName (not
// CockpitPreviewVisible) because CockpitPreviewSerializer hands the nested
// value to the default JsonSerializer and only the parent type gets the
// opt-in filter. JsonPropertyName keeps the camelCase keys cockpit.js expects.
public sealed class PoQueueLine
{
    [JsonPropertyName("partNum")]
    public string PartNum { get; init; } = "";

    [JsonPropertyName("desc")]
    public string Desc { get; init; } = "";

    [JsonPropertyName("uom")]
    public string Uom { get; init; } = "";

    [JsonPropertyName("ordered")]
    public decimal Ordered { get; init; }

    [JsonPropertyName("received")]
    public decimal Received { get; init; }

    [JsonPropertyName("remaining")]
    public decimal Remaining { get; init; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; init; }

    [JsonPropertyName("lineTotal")]
    public decimal LineTotal { get; init; }

    [JsonPropertyName("putaway")]
    public IReadOnlyList<string> Putaway { get; init; } = Array.Empty<string>();
}
