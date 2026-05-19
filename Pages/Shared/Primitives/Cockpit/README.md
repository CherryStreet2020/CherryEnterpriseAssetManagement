# Cockpit primitives

**Status:** Scaffolded in Sprint 12A PR #2 (commit-pending). Partials land in subsequent PRs.

This directory will house the reusable Razor partials that compose the **Cockpit** pattern locked in [ADR-018 §D3](../../../docs/ADR-018-cockpit-first-pattern.md). The Cockpit is the daily-driver canvas for every v1 Control Center: a time-bucketed queue on the left, a JSON-hydrated preview pane on the right, and a KPI tile strip in the page header.

## What's already shipped (PR #2)

- `wwwroot/css/cockpit.css` — all `.cockpit__*` selectors, extracted verbatim from `Pages/Receiving/Index.cshtml`. Loaded globally via `_ModernLayout.cshtml`.
- `wwwroot/js/cockpit.js` — `window.filterQueue(q)` + `window.selectPO(id)` hydration, extracted verbatim from the same legacy page. Reads `<script id="__poDetails" type="application/json">` blob.

## What lands next (PR #3)

- `Services/Navigation/Cockpit/ICockpitQueueRow.cs` — the queue-row contract: `Id / Primary / Secondary / RequiredAt / Tone / Meta[]`.
- `Services/Navigation/Cockpit/ICockpitLens<TQueueRow>.cs` — the grouping strategy: takes a flat list of rows, returns `IReadOnlyList<CockpitGroup<TQueueRow>>`.
- `Services/Navigation/Cockpit/ByTimeLens<T>.cs` — the default lens (Overdue / Today / This Week / Later) with unit tests for bucket boundaries.
- `Services/Navigation/Cockpit/CockpitPreviewSerializer.cs` — JSON-blob emitter with `[CockpitPreviewVisible]` opt-in attribute for PII safety.

## What lands in PR #4 + #5

- `_CockpitShell.cshtml` — the outer `.cockpit` grid container.
- `_CockpitQueue.cshtml` — left rail: title + count + search input + slot for grouped cards.
- `_CockpitQueueGroup.cshtml` — one time-bucket group (label tone + count + cards).
- `_CockpitQueueCard.cshtml` — one queue row. Generic — takes `ICockpitQueueRow`. Selectable via `data-id`.
- `_CockpitPreview.cshtml` — right pane wrapper with welcome + preview states.
- `_CockpitWelcome.cshtml` — no-row-selected hero with 4-stat strip.
- `_CockpitEmpty.cshtml` — empty-queue state.
- `_CockpitTabShell.cshtml` — the four-tab Control Center shell that hosts cockpits in tabs (PO Queue · ASN Queue · Orphans · Exceptions).

## Why scaffold the directory before the partials land

So that the CSS + JS files have a documented home in the codebase from day one, and so that PR #3's authors (and future Control Center sprints) know exactly where new primitives belong. Empty directories don't git-track; this README + the asset files prove the architecture decision.

## Reusability promise

These primitives have **no dependency on the Receiving domain**. Sprints 13-18 will compose them with each role's own:
- `IControlCenterService<TQueueRow>` (queue + preview data)
- `ICockpitLens<TQueueRow>` (role-specific grouping — Maintenance by priority/SLA, Inventory by zone/age, etc.)
- Voice tool set
- Per-role Exceptions tab content (which is itself the four-quadrant scaffold from Sprint 11)
