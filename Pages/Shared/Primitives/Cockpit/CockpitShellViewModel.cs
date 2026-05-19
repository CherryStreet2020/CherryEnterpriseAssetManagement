using System;
using System.Collections.Generic;
using Abs.FixedAssets.Services.Navigation.Cockpit;

namespace Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;

// =============================================================================
// View-model layer for the Cockpit shell partials (Sprint 12A PR #5).
// ADR-018 §D3 — pixel-identical extraction of /Receiving/Cockpit-Legacy's
// markup into reusable primitives that every v1 Control Center will compose.
//
// Why a view-model layer (vs. handing the partials the raw ICockpitQueueRow
// + CockpitGroup<TRow>):
//   1. CockpitGroup<TRow> is generic; Razor partials want a non-generic
//      @model type. The view-model adapts the generic lens output into the
//      shape the .cshtml renders.
//   2. The legacy queue card pulls in a domain-specific status pill (PO status).
//      The interface contract exposes optional StatusLabel + StatusTone; the
//      view-model layer flattens those into the final pill-class string.
//   3. The welcome hero shows a 4-stat strip (Overdue / Today / This Week /
//      Upcoming). That's pure presentation; lives here, not in the service.
// =============================================================================

// Outer container — passed to _CockpitShell.cshtml. Owns the queue + preview
// blob + welcome + the page's preview-pane HTML (rendered inline by the page,
// since the preview body shape is per-domain).
public sealed class CockpitShellViewModel
{
    // Left rail — queue. Domain-agnostic.
    public CockpitQueueViewModel Queue { get; init; } = new();

    // Welcome hero shown in the right pane until a row is selected.
    public CockpitWelcomeViewModel Welcome { get; init; } = new();

    // The page's preview-pane partial name. Receiving uses
    // "_CockpitPoQueuePreview". Sprints 13-18 will provide their own.
    // The preview-pane partial is rendered ONLY when this is non-empty;
    // the cockpit.js selectPO function flips its display from hidden to flex
    // when the user clicks a card.
    public string? PreviewPartialName { get; init; }
    public object? PreviewPartialModel { get; init; }

    // The JSON blob (already serialized via CockpitPreviewSerializer.SerializeMany)
    // that cockpit.js reads to hydrate the preview pane. Emitted inside a
    // <script type="application/json" id="__poDetails"> tag.
    public string PreviewBlobJson { get; init; } = "[]";
    public string PreviewBlobElementId { get; init; } = "__poDetails";

    // Sprint 12A PR #5.2 — main-pane preroll. When set, the right pane renders
    // this partial INSTEAD of the welcome hero on first paint. Used by the
    // Receiving CC to land with "Next Up" priority preview already showing.
    // cockpit.js#selectPO swaps to the per-row preview partial on row click.
    public string? PrerollPartialName { get; init; }
    public object? PrerollPartialModel { get; init; }
}

// Left-rail queue. Title + search + grouped cards.
public sealed class CockpitQueueViewModel
{
    public string TitleHtml { get; init; } = "PO Queue";        // optional inline icon allowed
    public string TitleIconClass { get; init; } = "fas fa-inbox";
    public int CountBadge { get; init; }
    public string SearchPlaceholder { get; init; } = "Search...";
    public string SearchElementId { get; init; } = "poSearch";  // legacy id; cockpit.js hooks oninput
    public string FilterFunctionName { get; init; } = "filterQueue";   // window-fn called from the search input
    public string SelectFunctionName { get; init; } = "selectPO";      // window-fn called from each card

    public IReadOnlyList<CockpitQueueGroupViewModel> Groups { get; init; } =
        Array.Empty<CockpitQueueGroupViewModel>();

    public CockpitEmptyViewModel? Empty { get; init; }
}

// One grouped bucket (Overdue / Today / This Week / Later — or any
// lens-defined bucket).
public sealed class CockpitQueueGroupViewModel
{
    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public string Tone { get; init; } = "neutral";   // danger | warning | info | neutral
    public string IconClass { get; init; } = "";
    public IReadOnlyList<ICockpitQueueRow> Rows { get; init; } = Array.Empty<ICockpitQueueRow>();
}

// 4-stat welcome strip + intro copy shown when no row is selected.
public sealed class CockpitWelcomeViewModel
{
    public string IconClass { get; init; } = "fas fa-box-open";
    public string Title { get; init; } = "Select a PO to preview";
    public string Subtitle { get; init; } = "Click a purchase order from the queue to see details.";

    public IReadOnlyList<CockpitWelcomeStat> Stats { get; init; } =
        Array.Empty<CockpitWelcomeStat>();
}

// One stat tile inside the welcome hero strip. Tone drives the value color
// using the same palette as queue cards: danger | warning | info | neutral.
public sealed record CockpitWelcomeStat(string Label, string Value, string Tone = "neutral");

// Empty-queue state.
public sealed class CockpitEmptyViewModel
{
    public string IconClass { get; init; } = "fas fa-check-circle";
    public string IconTone { get; init; } = "success";    // success | info | warning | danger | neutral
    public string Message { get; init; } = "All caught up!";
}
