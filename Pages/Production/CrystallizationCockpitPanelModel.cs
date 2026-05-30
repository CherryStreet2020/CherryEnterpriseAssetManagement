// Theme B7 Wave D PR-3 (2026-05-30) — view-model bag for _CockpitCrystallizationPanel.cshtml.
//
// The "crystallize this job into a reusable standard" panel — THE BIC DIFFERENTIATOR
// surface. Wraps the read-only CrystallizationPreview (PR-5 PreviewCrystallizationAsync)
// with the bits the cockpit needs to render it and offer the (human-confirmed) crystallize
// / reverse actions:
//   * proposed Item identity (part #/rev/description) + structural fingerprint
//   * seeded standard cost + cost source
//   * the would-be standard BOM + standard routing
//   * dedupe match banner (human-confirm — never auto-link, decision #3)
//   * already-crystallized state (offer reverse instead of re-mint)
//
// Populated by the Production Cockpit "Crystallize" tab (live) and the admin probe
// (deterministic Lock-16 surface). Read model only — the actual mint/reverse route
// through IItemCrystallizationService on POST.

using Abs.FixedAssets.Services.Production;

namespace Abs.FixedAssets.Pages.Production;

public sealed class CrystallizationCockpitPanelModel
{
    /// <summary>The read-only crystallization preview: would-be Item + standard BOM/routing/cost + dedupe.</summary>
    public required CrystallizationPreview Preview { get; init; }

    /// <summary>True when this PRO already minted a standard (offer reverse, not re-crystallize).</summary>
    public bool AlreadyCrystallized => Preview.AlreadyCrystallized;

    /// <summary>True when a structural fingerprint match exists — crystallizing must human-confirm link vs new.</summary>
    public bool HasDedupeMatch => Preview.DedupeMatchItemId != null;

    /// <summary>True when there's a real standard to harvest (some BOM or routing captured).</summary>
    public bool HasStandardToHarvest => Preview.BomLines.Count > 0 || Preview.RoutingOps.Count > 0;
}
