using Microsoft.AspNetCore.Authorization;
using Abs.FixedAssets.Pages.Shared;

namespace Abs.FixedAssets.Pages.Settings;

// ADR-017 §D9 — Settings drawer landing.
//
// Replaces the inline Administration group in the sidebar. Today this is a
// directory page that links to the existing /Admin/* surfaces; a future PR
// can flesh out the per-section sub-nav (Organization / Users & Access /
// Master data / Data & Integration / System) per the ADR's spec.
[Authorize]
public sealed class IndexModel : VoiceReadyPageModel
{
    public void OnGet() { }
}
