using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Receiving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Receiving;

// Sprint 11 PR #6 — Blind-receive workflow.
// ~5% of real receipts. Freight arrives with no PO, no ASN. Operator records
// what's on the dock; the AI MatchOrphanReceipt tool (PR #4) attaches a
// candidate PO later when one is identified.
[Authorize(Policy = "StockReceipt.Create")]
public sealed class BlindModel : VoiceReadyPageModel
{
    private readonly IReceivingControlCenterService _receiving;

    public BlindModel(IReceivingControlCenterService receiving)
    {
        _receiving = receiving;
    }

    [BindProperty]
    public BlindReceiveCommand Input { get; set; } = new();

    public string? FlashOk { get; set; }
    public string? FlashError { get; set; }
    public string? LastReceiptNumber { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            FlashError = "Please correct the highlighted fields.";
            return Page();
        }

        int userId = 0;
        var sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(sub, out var parsed)) userId = parsed;

        var key = new IdempotencyKey
        {
            Key = Guid.NewGuid(),
            UserId = userId,
        };

        var result = await _receiving.BlindReceiveAsync(userId, key, Input, ct);

        if (result.IsFailure)
        {
            FlashError = result.Error ?? "BlindReceiveAsync failed without a message.";
            return Page();
        }

        LastReceiptNumber = result.Value!.ReceiptNumber;
        FlashOk = $"Orphan receipt {LastReceiptNumber} created — {result.Value.QuantityReceived} unit(s). " +
                  $"Use the MatchOrphanReceipt tool in the Control Center to attach a PO when one is identified.";
        Input = new BlindReceiveCommand();
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
            EntityType = "Receiving.Blind",
            EntityId = "BLIND",
            RelatedIds = Array.Empty<string>(),
            FocusedField = baseCtx.FocusedField,
            Tab = baseCtx.Tab,
            BuiltAt = baseCtx.BuiltAt,
        };
    }
}
