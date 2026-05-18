using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Admin;
using Abs.FixedAssets.Services.Forms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Admin.StockReceipts;

// Sprint 4 Phase F Wave 1 PR #5 + ADR-015 Migration PR #3 —
// StockReceipt create/edit.
//
// Profile-driven: typed core fields ([BindProperty]) handle identity /
// quantity / status / notes; everything else is rendered dynamically
// from ReceiptProfile.UiFormSpec via the DynamicForm ViewComponent and
// parsed back via AttributesFormReader.
[Authorize(Policy = "StockReceipt.Edit")]
public class EditModel : VoiceReadyPageModel
{
    private readonly IStockReceiptService _svc;
    private readonly ReceiptAttributesValidator _validator;

    public EditModel(IStockReceiptService svc, ReceiptAttributesValidator validator)
    {
        _svc = svc;
        _validator = validator;
    }

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    // ── Typed core (identity / quantity / status / notes) ────
    [BindProperty] public string ReceiptNumber { get; set; } = string.Empty;
    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public int? MaterialMasterId { get; set; }

    [BindProperty] public string? LotNumber { get; set; }
    [BindProperty] public string? SerialNumber { get; set; }
    [BindProperty] public string? SourcePoNumber { get; set; }
    [BindProperty] public string? SourcePoLineId { get; set; }

    [BindProperty] public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    [BindProperty] public int? ReceivedByUserId { get; set; }
    [BindProperty] public int? LocationId { get; set; }

    [BindProperty] public decimal QuantityReceived { get; set; }
    [BindProperty] public decimal QuantityRemaining { get; set; }
    [BindProperty] public string? Uom { get; set; }

    [BindProperty] public StockReceiptStatus Status { get; set; } = StockReceiptStatus.Available;
    [BindProperty] public string? Notes { get; set; }

    // Hidden field; server-side overridden in GetProfileForSubmitAsync.
    [BindProperty] public string ProfileCode { get; set; } = "STEEL";

    // ── Profile + Attributes (the dynamic core) ──────────────
    public ReceiptProfile Profile { get; private set; } = default!;
    public IReadOnlyDictionary<string, object?> Attributes { get; private set; }
        = new Dictionary<string, object?>();

    public bool IsNew => Id is null or 0;
    public string PageTitle => IsNew ? "New Stock Receipt" : "Edit Stock Receipt";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew)
        {
            var profileRes = await _svc.GetDefaultProfileForCreateAsync(HttpContext.RequestAborted);
            if (profileRes.IsFailure)
            {
                ErrorMessage = profileRes.Error;
                return Page();
            }
            Profile = profileRes.Value!;
            ProfileCode = Profile.Code;
            Attributes = DeserializeAttributes(Profile.DefaultAttributes);
            return Page();
        }

        var r = await _svc.GetWithProfileAsync(Id!.Value, HttpContext.RequestAborted);
        if (r.IsFailure) { ErrorMessage = r.Error; return Page(); }

        var (entity, profile) = r.Value!;
        Profile = profile;
        ProfileCode = profile.Code;
        Attributes = DeserializeAttributes(entity.Attributes);

        // Hydrate typed core
        ReceiptNumber = entity.ReceiptNumber;
        ItemId = entity.ItemId;
        MaterialMasterId = entity.MaterialMasterId;
        LotNumber = entity.LotNumber;
        SerialNumber = entity.SerialNumber;
        SourcePoNumber = entity.SourcePoNumber;
        SourcePoLineId = entity.SourcePoLineId;
        ReceivedAt = entity.ReceivedAt;
        ReceivedByUserId = entity.ReceivedByUserId;
        LocationId = entity.LocationId;
        QuantityReceived = entity.QuantityReceived;
        QuantityRemaining = entity.QuantityRemaining;
        Uom = entity.Uom;
        Status = entity.Status;
        Notes = entity.Notes;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Always reload profile server-side. For Update, the receipt's
        // existing profile wins regardless of form-supplied ProfileCode.
        var profileRes = await _svc.GetProfileForSubmitAsync(
            Id, ProfileCode, ItemId, HttpContext.RequestAborted);
        if (profileRes.IsFailure)
        {
            ErrorMessage = profileRes.Error;
            Profile = StubProfile();
            Attributes = new Dictionary<string, object?>();
            return Page();
        }
        Profile = profileRes.Value!;
        ProfileCode = Profile.Code;

        var spec = JsonSerializer.Deserialize<UiFormSpec>(
            Profile.UiFormSpec,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new UiFormSpec();

        var attrs = AttributesFormReader.Read(Request.Form, spec, out var coercionErrors);
        Attributes = attrs;

        foreach (var err in coercionErrors)
            ModelState.AddModelError($"attrs[{err.Key}]", err.Message);

        // Run JSON Schema validation BEFORE the service call so per-field
        // errors land on ModelState — the Razor partial reads them out
        // via attrs[<key>] lookups.
        var schemaErrors = _validator.Validate(Profile, attrs);
        foreach (var (key, message) in JsonPointerToModelKey.Translate(schemaErrors))
            ModelState.AddModelError(key, message);

        if (!ModelState.IsValid) return Page();

        var idempKey = Guid.NewGuid();
        var actor = ResolveActorUserId();

        if (IsNew)
        {
            var req = new CreateStockReceiptRequest(
                ReceiptNumber, ItemId, MaterialMasterId, Profile.Code,
                LotNumber, SerialNumber, SourcePoNumber, SourcePoLineId,
                ReceivedAt, ReceivedByUserId, LocationId,
                QuantityReceived, Uom, Status, Notes, attrs);
            var r = await _svc.CreateAsync(req, actor, idempKey, HttpContext.RequestAborted);
            if (r.IsFailure) { ErrorMessage = r.Error; return Page(); }
            return RedirectToPage("Index");
        }
        else
        {
            var req = new UpdateStockReceiptRequest(
                ReceiptNumber, ItemId, MaterialMasterId,
                LotNumber, SerialNumber, SourcePoNumber, SourcePoLineId,
                ReceivedAt, ReceivedByUserId, LocationId,
                QuantityReceived, QuantityRemaining, Uom, Notes, attrs);
            var r = await _svc.UpdateAsync(Id!.Value, req, actor, idempKey, HttpContext.RequestAborted);
            if (r.IsFailure) { ErrorMessage = r.Error; return Page(); }
            return RedirectToPage("Index");
        }
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
            EntityType = nameof(StockReceipt),
            EntityId = Id?.ToString(),
            RelatedIds = b.RelatedIds,
            FocusedField = b.FocusedField,
            Tab = b.Tab,
            BuiltAt = b.BuiltAt,
        };
    }

    private static IReadOnlyDictionary<string, object?> DeserializeAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object?>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json)
                   ?? new Dictionary<string, object?>();
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }

    private static ReceiptProfile StubProfile() => new()
    {
        Code = "UNKNOWN",
        Name = "Unknown",
        JsonSchema = "{}",
        UiFormSpec = "{\"groups\":[]}",
        DefaultAttributes = "{}",
        PromotedFacets = "[]",
        RegulatoryProfileIds = "[]",
    };

    private int ResolveActorUserId()
    {
        var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(raw, out var n) ? n : 0;
    }
}
