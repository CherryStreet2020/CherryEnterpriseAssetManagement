using System;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Admin.RegulatoryProfiles;

// Sprint 4 Phase F Wave 1 — RegulatoryProfile create/edit.
// Single form serves both /Admin/RegulatoryProfiles/Edit (new)
// and /Admin/RegulatoryProfiles/Edit?id={n} (update).
[Authorize(Policy = "RegulatoryProfile.Edit")]
public class EditModel : VoiceReadyPageModel
{
    private readonly IRegulatoryProfileService _svc;

    public EditModel(IRegulatoryProfileService svc) => _svc = svc;

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public RegulatoryRegime Regime { get; set; } = RegulatoryRegime.None;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public bool IsExternalRegime { get; set; } = true;

    [BindProperty]
    public int? MinimumRetentionYears { get; set; }

    [BindProperty]
    public string? GatesJson { get; set; }

    [BindProperty]
    public bool IsActive { get; set; } = true;

    public bool IsNew => Id is null or 0;
    public string PageTitle => IsNew ? "New Regulatory Profile" : $"Edit Profile";
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (Id is null or 0)
        {
            // Default Gates payload to give the admin a template
            GatesJson ??= """
            {
              "requireSerialOnReceipt": false,
              "requireHeatNumberOnReceipt": false,
              "requireMrbDispositionOnQuarantine": false,
              "minimumRetentionYears": 7,
              "auditEvents": ["receipt", "issue", "release"]
            }
            """;
            return Page();
        }

        var r = await _svc.GetAsync(Id.Value, HttpContext.RequestAborted);
        if (r.IsFailure)
        {
            ErrorMessage = r.Error;
            return Page();
        }

        var p = r.Value!;
        Name = p.Name;
        Regime = p.Regime;
        Description = p.Description;
        IsExternalRegime = p.IsExternalRegime;
        MinimumRetentionYears = p.MinimumRetentionYears;
        GatesJson = p.Gates;
        IsActive = p.IsActive;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var actorUserId = 1; // TODO: pull from User.FindFirstValue once user-id is on the principal
        var idempKey = Guid.NewGuid(); // page-submit-scope dedupe

        if (Id is null or 0)
        {
            var req = new CreateRegulatoryProfileRequest(
                Name, Regime, Description, IsExternalRegime, MinimumRetentionYears, GatesJson);
            var r = await _svc.CreateAsync(req, actorUserId, idempKey, HttpContext.RequestAborted);
            if (r.IsFailure)
            {
                ErrorMessage = r.Error;
                return Page();
            }
            return RedirectToPage("Index");
        }
        else
        {
            var req = new UpdateRegulatoryProfileRequest(
                Name, Regime, Description, IsExternalRegime, MinimumRetentionYears, GatesJson);
            var r = await _svc.UpdateAsync(Id.Value, req, actorUserId, idempKey, HttpContext.RequestAborted);
            if (r.IsFailure)
            {
                ErrorMessage = r.Error;
                return Page();
            }
            return RedirectToPage("Index");
        }
    }

    public async Task<IActionResult> OnPostToggleActiveAsync()
    {
        if (Id is null or 0) return BadRequest();
        var actorUserId = 1;
        var idempKey = Guid.NewGuid();
        var r = await _svc.SetActiveAsync(Id.Value, !IsActive, actorUserId, idempKey, HttpContext.RequestAborted);
        if (r.IsFailure)
        {
            ErrorMessage = r.Error;
            return Page();
        }
        return RedirectToPage("Edit", new { id = Id });
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
            EntityId = Id?.ToString(),
            RelatedIds = b.RelatedIds,
            FocusedField = b.FocusedField,
            Tab = b.Tab,
            BuiltAt = b.BuiltAt,
        };
    }
}
