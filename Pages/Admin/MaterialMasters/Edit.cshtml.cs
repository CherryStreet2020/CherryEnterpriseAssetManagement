using System;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Admin.MaterialMasters;

// Sprint 4 Phase F Wave 1 PR #2 — MaterialMaster create/edit.
[Authorize(Policy = "MaterialMaster.Edit")]
public class EditModel : VoiceReadyPageModel
{
    private readonly IMaterialMasterService _svc;

    public EditModel(IMaterialMasterService svc) => _svc = svc;

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public string ShopCode { get; set; } = string.Empty;

    [BindProperty]
    public string? AstmDesignation { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public MaterialForm Form { get; set; } = MaterialForm.Plate;

    [BindProperty]
    public decimal? DensityKgPerM3 { get; set; }

    [BindProperty]
    public bool IsAnisotropic { get; set; }

    public bool IsNew => Id is null or 0;
    public string PageTitle => IsNew ? "New Material" : "Edit Material";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id is null or 0) return Page();

        var r = await _svc.GetAsync(Id.Value, HttpContext.RequestAborted);
        if (r.IsFailure)
        {
            ErrorMessage = r.Error;
            return Page();
        }
        var m = r.Value!;
        ShopCode = m.ShopCode;
        AstmDesignation = m.AstmDesignation;
        Description = m.Description;
        Form = m.Form;
        DensityKgPerM3 = m.DensityKgPerM3;
        IsAnisotropic = m.IsAnisotropic;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var actorUserId = 1;
        var idempKey = Guid.NewGuid();

        if (Id is null or 0)
        {
            var req = new CreateMaterialMasterRequest(
                ShopCode, AstmDesignation, Description, Form, DensityKgPerM3, IsAnisotropic);
            var r = await _svc.CreateAsync(req, actorUserId, idempKey, HttpContext.RequestAborted);
            if (r.IsFailure) { ErrorMessage = r.Error; return Page(); }
            return RedirectToPage("Index");
        }
        else
        {
            var req = new UpdateMaterialMasterRequest(
                ShopCode, AstmDesignation, Description, Form, DensityKgPerM3, IsAnisotropic);
            var r = await _svc.UpdateAsync(Id.Value, req, actorUserId, idempKey, HttpContext.RequestAborted);
            if (r.IsFailure) { ErrorMessage = r.Error; return Page(); }
            return RedirectToPage("Index");
        }
    }

    public override VoiceContextPayload BuildContextPayload()
    {
        var b = base.BuildContextPayload();
        return new VoiceContextPayload
        {
            Route = b.Route, UserId = b.UserId, Roles = b.Roles, TenantId = b.TenantId,
            EntityType = nameof(MaterialMaster),
            EntityId = Id?.ToString(),
            RelatedIds = b.RelatedIds, FocusedField = b.FocusedField, Tab = b.Tab, BuiltAt = b.BuiltAt,
        };
    }
}
