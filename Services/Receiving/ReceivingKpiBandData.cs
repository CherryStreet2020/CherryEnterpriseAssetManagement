// =============================================================================
// CherryAI EAM — Receiving Control Center KPI band data (Sprint 12A PR #5.1)
// ADR-018 §D3 — third leg of the Cockpit canvas: persistent KPI tile strip
// in the page header, always visible above the tabs.
//
// Why a dedicated method (not GetKpiStripAsync):
//   GetKpiStripAsync returns the 8 *quality* KPIs from ADR-016 §D9
//   (dock-to-stock, accuracy, etc.) — those are right for the Exceptions
//   tab's analytical four-quadrant scaffold. The page-header band is a
//   MIXED workload + quality strip: 4 clickable workload counters (Open POs
//   / Overdue / Due Today / This Week) PLUS 4 health tiles (Receipts Today
//   / Dock-to-Stock / Doc Completeness / Exceptions Open). The workload
//   counters need today/this-week-relative counts, not a 14-day range, so
//   a separate query keeps the contract clean.
//
// Why each tile carries its own DrillTarget:
//   The KPI band is purely declarative — clicking a tile triggers a
//   pre-defined navigation or filter via cockpit.js. The page model
//   builds the targets (knows the route + tab keys); the partial just
//   stamps them as data attributes.
// =============================================================================

using System;
using System.Collections.Generic;

namespace Abs.FixedAssets.Services.Receiving;

public sealed class ReceivingKpiBandFilter
{
    public string? SiteCode { get; init; }
}

public sealed class ReceivingKpiBandData
{
    // First row — workload (clickable filters into PO Queue buckets)
    public ReceivingKpiTile OpenPos      { get; init; } = new();
    public ReceivingKpiTile Overdue      { get; init; } = new();
    public ReceivingKpiTile DueToday     { get; init; } = new();
    public ReceivingKpiTile ThisWeek     { get; init; } = new();

    // Second row — quality (clickable drills to Exceptions tab or trend popover)
    public ReceivingKpiTile ReceiptsToday   { get; init; } = new();
    public ReceivingKpiTile DockToStock     { get; init; } = new();
    public ReceivingKpiTile DocCompleteness { get; init; } = new();
    public ReceivingKpiTile ExceptionsOpen  { get; init; } = new();

    public DateTime ComputedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class ReceivingKpiTile
{
    public string Label { get; init; } = "";

    // Display value (already formatted for the tile). Examples: "78", "0",
    // "92.3", "—" (unknown). The partial does NOT format — passthrough only.
    public string Value { get; init; } = "—";

    // Optional unit shown to the right of the value ("%", "min", "h").
    public string? Unit { get; init; }

    // Optional target text shown faded under the value ("target 90").
    public string? TargetText { get; init; }

    // Tone drives the top-accent stripe + value color:
    //   "danger" | "warning" | "info" | "success" | "neutral" | "brand"
    public string Tone { get; init; } = "neutral";

    // 7-day sparkline values (any scale — the partial normalizes 0..1).
    // Empty array hides the sparkline.
    public IReadOnlyList<double> SparkPoints { get; init; } = Array.Empty<double>();

    // Click navigation. One of (mutually exclusive):
    //   - DrillHref: hard navigation to a URL ("/Receiving?tab=exceptions")
    //   - DrillScroll: scroll to a queue group ("overdue" | "today" | "this-week" | "later")
    //   - null: tile is informational only
    public string? DrillHref { get; init; }
    public string? DrillScroll { get; init; }
}
