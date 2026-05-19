using System;
using System.Collections.Generic;

namespace Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;

// =============================================================================
// _CockpitKpiBand.cshtml view-model — Sprint 12A PR #5.1 / ADR-018 §D3.
//
// The KPI band is the THIRD leg of the Cockpit canvas (queue + preview + band).
// Persistent strip ABOVE the tab bar on every v1 Control Center — workload
// tiles on the left (clickable to filter the queue), quality tiles on the
// right (clickable to drill into Exceptions or trend popovers).
//
// The partial is domain-agnostic — Sprints 13-18 build their own KpiTile list
// and pass it in. The tiles themselves know how to drill (DrillHref or
// DrillScroll on each tile).
//
// Each Control Center provides:
//   - Eyebrow text (e.g. "Today's dock")
//   - Optional refreshed-at text + live indicator
//   - 8 tiles (4 workload + 4 quality is the recommended layout, but the
//     partial renders whatever you give it as a single horizontally-scrolling row)
// =============================================================================
public sealed class CockpitKpiBandViewModel
{
    public string Eyebrow { get; init; } = "";
    public string? RefreshedAtText { get; init; }
    public bool ShowLiveIndicator { get; init; } = true;
    public IReadOnlyList<CockpitKpiTileViewModel> Tiles { get; init; } = Array.Empty<CockpitKpiTileViewModel>();

    // PR #5.2 — hero mode: 4 wide tiles, bigger value, sub-text. Replaces the
    // original 8-tile band where eyebrow + status live inside the band.
    // When true, the page header partial owns the eyebrow + LIVE chip and
    // the band doesn't render its own header row.
    public bool HeroMode { get; init; }
}

// One tile in the band.
//
// Tone palette (drives top-accent stripe + value color):
//   "danger" (red), "warning" (amber), "info" (blue), "success" (green),
//   "brand" (cherry), "neutral" (muted)
//
// Drill behavior (mutually exclusive, both optional):
//   - DrillHref: anchor href — hard navigate (e.g. ?tab=exceptions)
//   - DrillScroll: data-drill-scroll attr — cockpit.js scrolls to .cockpit__group[data-group="{value}"]
//     and adds a brief cherry-red ring pulse. Used by workload tiles to jump
//     to a queue bucket without leaving the page.
public sealed class CockpitKpiTileViewModel
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "—";
    public string? Unit { get; init; }
    public string? TargetText { get; init; }
    public string? SubText { get; init; }   // PR #5.2 — context line under the value
    public string Tone { get; init; } = "neutral";
    public IReadOnlyList<double> SparkPoints { get; init; } = Array.Empty<double>();
    public string? DrillHref { get; init; }
    public string? DrillScroll { get; init; }
}
