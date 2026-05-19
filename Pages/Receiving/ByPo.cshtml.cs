using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Receiving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Receiving;

// Sprint 11 PR #6 — PO-driven receive workflow.
// 80% of real receipts hit this path. The operator pulls up a PO, scans
// or types the lot, confirms expected quantity, and posts.
//
// All form mutations route through IReceivingControlCenterService.ReceiveByPoAsync
// (PR #3) — which means: idempotency-mediated (ADR-014 D4), state-machine
// guarded, audit-logged with flat DTOs. The page just collects the input.
[Authorize(Policy = "StockReceipt.Create")]
public sealed class ByPoModel : VoiceReadyPageModel
{
    private readonly IReceivingControlCenterService _receiving;

    public ByPoModel(IReceivingControlCenterService receiving)
    {
        _receiving = receiving;
    }

    [BindProperty(SupportsGet = true)]
    public string? PoNumber { get; set; }

    [BindProperty]
    public ReceiveByPoCommand Input { get; set; } = new();

    public string? FlashOk { get; set; }
    public string? FlashError { get; set; }
    public string? LastReceiptNumber { get; set; }

    public void OnGet()
    {
        if (!string.IsNullOrEmpty(PoNumber))
        {
            Input.PoNumber = PoNumber;
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            FlashError = "Please correct the highlighted fields.";
            return Page();
        }

        // Best-effort user id from claims. Defaults to 0 if unauthenticated
        // (shouldn't reach here — Authorize guards the route).
        int userId = 0;
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(sub, out var parsed)) userId = parsed;

        // Each POST gets its own idempotency key. Submitting twice via a
        // double-click is safely deduped at the service layer.
        var key = new IdempotencyKey
        {
            Key = Guid.NewGuid(),
            UserId = userId,
        };

        var result = await _receiving.ReceiveByPoAsync(userId, key, Input, ct);

        if (result.IsFailure)
        {
            FlashError = result.Error ?? "ReceiveByPoAsync failed without a message.";
            return Page();
        }

        LastReceiptNumber = result.Value!.ReceiptNumber;
        FlashOk = $"Received {result.Value.QuantityReceived} as {LastReceiptNumber}.";
        // Clear form fields for the next receipt; keep PoNumber so the
        // operator can rip through a multi-line PO without retyping it.
        Input = new ReceiveByPoCommand { PoNumber = Input.PoNumber };
        return Page();
    }

    public override VoiceContextPayload BuildContextPayload()
    {
        var baseCtx = base.BuildContextPayload();
        return new VoiceContextPayload
        {
            Route = baseCtx.Route,
            UserId = baseCtx.UserId,
            Roles = baseCtx.Roles,
            TenantId = baseCtx.TenantId,
            EntityType = "Receiving.ByPo",
            EntityId = Input?.PoNumber ?? PoNumber ?? "",
            RelatedIds = Array.Empty<string>(),
            FocusedField = baseCtx.FocusedField,
            Tab = baseCtx.Tab,
            BuiltAt = baseCtx.BuiltAt,
        };
    }
}
