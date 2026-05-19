// =============================================================================
// CherryAI EAM — ASN Queue contracts (Sprint 12A PR #6)
//
// Mirrors PoQueueData.cs but for the AdvancedShippingNotice domain. Drives
// the /Receiving "ASN Queue" tab — the second real Cockpit canvas inside
// the four-tab shell.
//
// Domain difference vs PO Queue:
//   PO Queue cards bucket by RequiredDate (when the buyer NEEDS the goods).
//   ASN Queue cards bucket by ExpectedArrivalDate (when the truck SHOWS UP).
//   Same ByTimeLens<T> — different semantic on the date axis. The cockpit
//   primitive is domain-agnostic; only the labels/sub-text shift.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Abs.FixedAssets.Services.Navigation.Cockpit;

namespace Abs.FixedAssets.Services.Receiving;

public sealed class AsnQueueFilter
{
    public string? SiteCode { get; init; }
}

public sealed class AsnQueueData
{
    public IReadOnlyList<AsnQueueRow> Rows { get; init; } = Array.Empty<AsnQueueRow>();
    public IReadOnlyList<AsnQueuePreview> Previews { get; init; } = Array.Empty<AsnQueuePreview>();
}

// Queue card row. ICockpitQueueRow contract. RequiredAt drives the ByTimeLens
// bucket assignment (Overdue ETA / Today / This week / Later).
public sealed record AsnQueueRow(
    string Id,
    string Primary,
    string Secondary,
    DateTime? RequiredAt,
    string Tone,
    IReadOnlyList<MetaTriple> Meta,
    string? StatusLabel = null,
    string? StatusTone = null,
    // Late by N days — populated when ETA is in the past.
    int? DaysLate = null) : ICockpitQueueRow;

// Preview-pane record — serialized via CockpitPreviewSerializer into the
// __asnDetails JSON blob the cockpit.js#selectPO function hydrates.
//
// JSON keys match the cockpit.js#selectPO contract that PR #5's Receiving
// preview blob established. The script id changes (__asnDetails) so the two
// queues stay independent.
public sealed class AsnQueuePreview
{
    [CockpitPreviewVisible("id")]
    public int Id { get; init; }

    [CockpitPreviewVisible("num")]
    public string Num { get; init; } = "";     // ASN number, shown as the card title

    [CockpitPreviewVisible("vendor")]
    public string Vendor { get; init; } = "";

    [CockpitPreviewVisible("orderDate")]
    public string OrderDate { get; init; } = "";   // Ship date (vendor side)

    [CockpitPreviewVisible("requiredDate")]
    public string RequiredDate { get; init; } = ""; // Expected arrival

    [CockpitPreviewVisible("status")]
    public string Status { get; init; } = "";

    [CockpitPreviewVisible("total")]
    public string Total { get; init; } = "";    // Total expected quantity ("142 units")

    [CockpitPreviewVisible("shipTo")]
    public string ShipTo { get; init; } = "";

    [CockpitPreviewVisible("carrier")]
    public string? Carrier { get; init; }

    [CockpitPreviewVisible("tracking")]
    public string? Tracking { get; init; }

    [CockpitPreviewVisible("sourcePo")]
    public string? SourcePo { get; init; }

    [CockpitPreviewVisible("lines")]
    public IReadOnlyList<AsnQueueLine> Lines { get; init; } = Array.Empty<AsnQueueLine>();
}

// One manifest line inside the preview. JsonPropertyName drives the keys
// the nested-line serializer uses (CockpitPreviewSerializer's reflection
// only attribute-filters the parent; nested objects fall to default JSON
// behavior — see PoQueueLine for the same pattern).
public sealed class AsnQueueLine
{
    [JsonPropertyName("partNum")]
    public string PartNum { get; init; } = "";

    [JsonPropertyName("desc")]
    public string Desc { get; init; } = "";

    [JsonPropertyName("uom")]
    public string Uom { get; init; } = "";

    [JsonPropertyName("expected")]
    public decimal Expected { get; init; }

    [JsonPropertyName("received")]
    public decimal Received { get; init; }

    [JsonPropertyName("remaining")]
    public decimal Remaining { get; init; }

    [JsonPropertyName("lot")]
    public string? Lot { get; init; }

    [JsonPropertyName("heat")]
    public string? Heat { get; init; }
}
