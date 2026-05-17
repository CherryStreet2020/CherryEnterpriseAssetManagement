using Microsoft.AspNetCore.Authorization;
using Abs.FixedAssets.Pages.Shared;

namespace Abs.FixedAssets.Pages.Admin;

/// <summary>
/// Internal design-system showcase. Renders every primitive from
/// Pages/Shared/Primitives/ in every state. Linked from the Admin nav so the
/// team can verify visual changes touch every primitive identically.
/// </summary>
[Authorize(Roles = "Admin")]
public class DesignSystemModel : VoiceReadyPageModel
{
    public void OnGet() { /* no data load — pure showcase */ }
}
