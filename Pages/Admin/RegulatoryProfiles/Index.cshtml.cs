using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Admin.RegulatoryProfiles;

// Sprint 4 Phase F Wave 1 — /Admin/RegulatoryProfiles list page.
// Voice-ready by ADR-014: inherits VoiceReadyPageModel and exposes
// per-page context for the future voice layer to read.
[Authorize(Policy = "RegulatoryProfile.View")]
public class IndexModel : VoiceReadyPageModel
{
    private readonly IRegulatoryProfileService _svc;

    public IndexModel(IRegulatoryProfileService svc) => _svc = svc;

    public IReadOnlyList<RegulatoryProfile> Profiles { get; private set; } = new List<RegulatoryProfile>();
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var r = await _svc.ListAsync(HttpContext.RequestAborted);
        if (r.IsFailure)
        {
            ErrorMessage = r.Error;
            return Page();
        }
        Profiles = r.Value!;
        return Page();
    }

    public override VoiceContextPayload BuildContextPayload()
    {
        var b = base.BuildContextPayload();
        return new VoiceContextPayload
        {
            Route = b.Route,
            UserId = b.UserId,
            Roles = b.Roles,
            TenantId = b.TenantId,
            EntityType = nameof(RegulatoryProfile),
            EntityId = null, // list view
            RelatedIds = b.RelatedIds,
            FocusedField = b.FocusedField,
            Tab = b.Tab,
            BuiltAt = b.BuiltAt,
        };
    }
}
