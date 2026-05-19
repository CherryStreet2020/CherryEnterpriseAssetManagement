using System;
using System.Collections.Generic;

namespace Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;

// ADR-018 §D2 — the four-tab shell model.
//
// A Cockpit-first Control Center page renders a tab bar over its content.
// Receiving's four tabs (PO Queue · ASN Queue · Orphans · Exceptions) all
// implement the Cockpit pattern except Exceptions (anomaly triage — uses the
// four-quadrant scaffold from Sprint 11).
//
// Tab state lives in the URL via `?tab=<key>` so deep-links, back-button and
// refresh all preserve the active tab. The default tab (no `?tab=` param) is
// determined by the page; for /Receiving it is `po-queue`.
//
// Each subsequent v1 Control Center (Sprints 14-18) provides its own
// CockpitTabShellModel with role-specific tabs. The model is purely
// declarative — the page composes per-tab content separately.
public sealed class CockpitTabShellModel
{
    // The currently-active tab's Key. Pages bind this from `?tab=<key>` and
    // fall back to the first IsDefault tab when the param is missing/unknown.
    public string ActiveTabKey { get; init; } = "";

    // The four tabs in display order.
    public IReadOnlyList<CockpitTab> Tabs { get; init; } = Array.Empty<CockpitTab>();

    // Page route used to build tab hrefs, e.g. "/Receiving". Tab anchors will
    // render as href="{BaseRoute}?tab={tab.Key}".
    public string BaseRoute { get; init; } = "";
}

// One tab.
//
// CountBadge displays a small numeric pill (e.g. "5 open" next to "Exceptions").
// When null or 0 the badge is hidden. Tone drives the badge color:
//   "danger" (red), "warning" (amber), "info" (blue), "neutral" (gray, default).
public sealed record CockpitTab(
    string Key,
    string Label,
    string? IconClass = null,
    int? CountBadge = null,
    string BadgeTone = "neutral",
    bool IsDefault = false);
