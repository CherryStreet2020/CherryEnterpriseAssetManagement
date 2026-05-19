namespace Abs.FixedAssets.Models.Navigation;

// ADR-017 §D1, §D2, §D4 — descriptor for one Control Center sidebar entry.
//
// Why a record:
//   The Control Centers section of the sidebar is data-driven (not hand-rolled
//   HTML). Each sprint that ships a new Control Center adds one entry to
//   ControlCenterRegistry.cs. No re-deploys of the layout itself.
//
// Why a static registry (rather than scanning controllers / convention-based):
//   The roadmap signal — visible-but-disabled placeholders for not-yet-shipped
//   sprints — requires explicit data. A convention-based scan would miss
//   the placeholders entirely (no controller exists yet for the not-shipped
//   Control Centers).
public sealed record ControlCenterDescriptor(
    string Code,            // e.g. "RECEIVING"
    string Title,           // e.g. "Receiving"
    string Route,           // e.g. "/Receiving"
    string IconClass,       // e.g. "fas fa-truck-ramp-box"
    int? SprintNumber,      // 11..18 — when this Control Center ships
    bool IsLive,            // true = active link; false = visible-but-disabled
    string? StatusChip);    // e.g. "live", "soon — Sprint 12" — null for live
