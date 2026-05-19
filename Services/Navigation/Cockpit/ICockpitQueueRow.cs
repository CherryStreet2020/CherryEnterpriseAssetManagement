using System;
using System.Collections.Generic;

namespace Abs.FixedAssets.Services.Navigation.Cockpit;

// ADR-018 §D5 — generic queue-row contract every Cockpit queue tab implements.
//
// The Receiving PO Queue, Receiving ASN Queue, Receiving Orphans, Purchasing
// Open POs / Requisitions / RFQ, Maintenance WO queue, etc. all adapt their
// domain row to this contract so the shared _CockpitQueue + _CockpitQueueCard
// partials (PR #4) can render them without knowing the domain.
//
// Tone drives visual emphasis (left-border accent, group-label color):
//   "danger"  → overdue / past-SLA / critical
//   "warning" → due today / approaching SLA
//   "info"    → due this week / on plan
//   "neutral" → upcoming / not yet relevant
//
// Meta is 0-3 small KV strips below the secondary line (e.g.
// "Required 01/24 · Lines 4 · Value $4,165"). Keep it short — the row is
// 60-80px tall.
public interface ICockpitQueueRow
{
    string Id { get; }                                  // row identity; emitted as data-id
    string Primary { get; }                             // e.g. PO number, work-order number
    string Secondary { get; }                           // e.g. vendor name, asset name
    DateTime? RequiredAt { get; }                       // drives the default ByTimeLens grouping
    string Tone { get; }                                // "danger" | "warning" | "info" | "neutral"
    IReadOnlyList<MetaTriple> Meta { get; }
}

// A single label/value pair shown in a Cockpit queue card's meta row.
// Tone is optional; when set, the value renders in that tone's accent color.
public sealed record MetaTriple(string Label, string Value, string? Tone = null);
