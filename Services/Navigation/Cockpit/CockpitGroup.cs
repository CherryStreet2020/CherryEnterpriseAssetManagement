using System.Collections.Generic;

namespace Abs.FixedAssets.Services.Navigation.Cockpit;

// ADR-018 §D4 — one bucket of rows produced by an ICockpitLens.
//
// Code is the stable machine key for the bucket ("overdue", "today",
// "this-week", "later", "priority-1", "by-zone-A12", etc.). Label is the
// human-readable header rendered in the queue. Tone drives the bucket-label
// color (matches the row tone palette: "danger" | "warning" | "info" |
// "neutral"). Icon is a Font Awesome class string ("fa-exclamation-triangle",
// etc.) — the partial chooses how to render it.
//
// Sorted/ordered by the lens before return; consumers render in list order.
public sealed record CockpitGroup<TRow>(
    string Code,
    string Label,
    string Tone,
    string Icon,
    IReadOnlyList<TRow> Rows);
