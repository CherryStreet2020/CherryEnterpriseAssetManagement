using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Admin.MaterialMasters;

// Sprint 4 Phase F Wave 1 PR #2 — MaterialMaster list page.
[Authorize(Policy = "MaterialMaster.View")]
public class IndexModel : VoiceReadyPageModel
{
    private readonly IMaterialMasterService _svc;

    public IndexModel(IMaterialMasterService svc) => _svc = svc;

    public IReadOnlyList<MaterialMaster> Materials { get; private set; } = new List<MaterialMaster>();
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var r = await _svc.ListAsync(HttpContext.RequestAborted);
        if (r.IsFailure)
        {
            ErrorMessage = r.Error;
            return Page();
        }
        Materials = r.Value!;
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
            EntityType = nameof(MaterialMaster),
            EntityId = null,
            RelatedIds = b.RelatedIds,
            FocusedField = b.FocusedField,
            Tab = b.Tab,
            BuiltAt = b.BuiltAt,
        };
    }
}
