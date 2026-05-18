using System;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Admin.StockReceipts;

// Sprint 4 Phase F Wave 1 PR #5 — StockReceipt create/edit.
//
// Single page handles both Create (Id == null/0) and Edit (Id > 0).
// Same VoiceReadyPageModel + IStockReceiptService pattern as PR #217
// MaterialMaster.
[Authorize(Policy = "StockReceipt.Edit")]
public class EditModel : VoiceReadyPageModel
{
    private readonly IStockReceiptService _svc;

    public EditModel(IStockReceiptService svc) => _svc = svc;

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    // ── Identity ─────────────────────────────────────────────
    [BindProperty] public string ReceiptNumber { get; set; } = string.Empty;
    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public int? MaterialMasterId { get; set; }

    // ── Traceability ─────────────────────────────────────────
    [BindProperty] public string? HeatNumber { get; set; }
    [BindProperty] public string? LotNumber { get; set; }
    [BindProperty] public string? MillCertUrl { get; set; }
    [BindProperty] public string? Mill { get; set; }
    [BindProperty] public string? SourcePoNumber { get; set; }
    [BindProperty] public string? SourcePoLineId { get; set; }

    // ── Receipt event ────────────────────────────────────────
    [BindProperty] public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    [BindProperty] public int? ReceivedByUserId { get; set; }
    [BindProperty] public int? LocationId { get; set; }

    // ── Dimensions ───────────────────────────────────────────
    [BindProperty] public decimal? LengthMm { get; set; }
    [BindProperty] public decimal? WidthMm { get; set; }
    [BindProperty] public decimal? ThicknessMm { get; set; }
    [BindProperty] public decimal? UsableLengthMm { get; set; }
    [BindProperty] public decimal? UsableWidthMm { get; set; }

    // ── Quantity ─────────────────────────────────────────────
    [BindProperty] public decimal QuantityReceived { get; set; }
    [BindProperty] public decimal QuantityRemaining { get; set; }
    [BindProperty] public string? Uom { get; set; }

    // ── Status / Notes ───────────────────────────────────────
    [BindProperty] public StockReceiptStatus Status { get; set; } = StockReceiptStatus.Available;
    [BindProperty] public string? Notes { get; set; }

    public bool IsNew => Id is null or 0;
    public string PageTitle => IsNew ? "New Stock Receipt" : "Edit Stock Receipt";
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (IsNew) return Page();

        var r = await _svc.GetAsync(Id!.Value, HttpContext.RequestAborted);
        if (r.IsFailure)
        {
            ErrorMessage = r.Error;
            return Page();
        }
        var e = r.Value!;
        ReceiptNumber = e.ReceiptNumber;
        ItemId = e.ItemId;
        MaterialMasterId = e.MaterialMasterId;
        HeatNumber = e.HeatNumber;
        LotNumber = e.LotNumber;
        MillCertUrl = e.MillCertUrl;
        Mill = e.Mill;
        SourcePoNumber = e.SourcePoNumber;
        SourcePoLineId = e.SourcePoLineId;
        ReceivedAt = e.ReceivedAt;
        ReceivedByUserId = e.ReceivedByUserId;
        LocationId = e.LocationId;
        LengthMm = e.LengthMm;
        WidthMm = e.WidthMm;
        ThicknessMm = e.ThicknessMm;
        UsableLengthMm = e.UsableLengthMm;
        UsableWidthMm = e.UsableWidthMm;
        QuantityReceived = e.QuantityReceived;
        QuantityRemaining = e.QuantityRemaining;
        Uom = e.Uom;
        Status = e.Status;
        Notes = e.Notes;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var actorUserId = ResolveActorUserId();
        var idempKey = Guid.NewGuid();

        if (IsNew)
        {
            var req = new CreateStockReceiptRequest(
                ReceiptNumber, ItemId, MaterialMasterId,
                HeatNumber, LotNumber, MillCertUrl, Mill,
                SourcePoNumber, SourcePoLineId,
                ReceivedAt, ReceivedByUserId, LocationId,
                LengthMm, WidthMm, ThicknessMm,
                QuantityReceived, Uom, Status, Notes);
            var r = await _svc.CreateAsync(req, actorUserId, idempKey, HttpContext.RequestAborted);
            if (r.IsFailure) { ErrorMessage = r.Error; return Page(); }
            return RedirectToPage("Index");
        }
        else
        {
            var req = new UpdateStockReceiptRequest(
                ReceiptNumber, ItemId, MaterialMasterId,
                HeatNumber, LotNumber, MillCertUrl, Mill,
                SourcePoNumber, SourcePoLineId,
                ReceivedAt, ReceivedByUserId, LocationId,
                LengthMm, WidthMm, ThicknessMm,
                UsableLengthMm, UsableWidthMm,
                QuantityReceived, QuantityRemaining, Uom, Notes);
            var r = await _svc.UpdateAsync(Id!.Value, req, actorUserId, idempKey, HttpContext.RequestAborted);
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

    private int ResolveActorUserId()
    {
        var raw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(raw, out var n) ? n : 0;
    }
}
