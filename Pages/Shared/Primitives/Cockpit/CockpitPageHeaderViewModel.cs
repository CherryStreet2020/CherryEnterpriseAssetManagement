using System;

namespace Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;

// =============================================================================
// _CockpitPageHeader.cshtml view-model — Sprint 12A PR #5.2.
//
// Anchored title bar at the top of every v1 Control Center. Replaces the
// in-band eyebrow text (which collided with the collapsed sidebar's brand
// logo on PR #5.1). Renders ABOVE the KPI band.
//
// Slots from left to right:
//   [ Title · Scope ]            [ LIVE · Refreshed · Voice ]
//   [ Subtitle text ]
// =============================================================================
public sealed class CockpitPageHeaderViewModel
{
    public string Title { get; init; } = "";       // e.g. "Receiving"
    public string? Scope { get; init; }            // e.g. "All sites" or "PWH-001"
    public string? Subtitle { get; init; }         // e.g. "Push-to-talk voice (hold Space)"
    public bool ShowLive { get; init; } = true;
    public string? RefreshedAtText { get; init; }  // e.g. "updated just now"
    public bool ShowVoiceButton { get; init; } = true;
    public string VoiceButtonLabel { get; init; } = "Voice";
}
