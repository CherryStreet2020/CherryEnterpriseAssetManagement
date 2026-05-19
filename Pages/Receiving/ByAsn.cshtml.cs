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

// Sprint 11 PR #6 — ASN-driven receive workflow.
// ~10% of real receipts. EDI 856 declares lines + qty + lot in advance;
// the operator scans the ASN barcode + (optionally) overrides quantity.
//
// Real EDI 856 (X12 parser, AS2 endpoint, trading-partner config) is a
// future-sprint scope. PR #6 ships:
//   - Page with ASN ID + line ID + override-quantity inputs
//   - Service path that records the receipt with the ASN reference in
//     SourcePoNumber (prefixed "ASN:") so downstream reporting can tell
//     ASN-driven receipts apart from PO-driven ones.
[Authorize(Policy = "StockReceipt.Create")]
public sealed class ByAsnModel : VoiceReadyPageModel
{
    private readonly IReceivingControlCenterService _receiving;

    public ByAsnModel(IReceivingControlCenterService receiving)
    {
        _receiving = receiving;
    }

    [BindProperty(SupportsGet = true)]
    public string? AsnId { get; set; }

    [BindProperty]
    public ReceiveByAsnCommand Input { get; set; } = new();

    public string? FlashOk { get; set; }
    public string? FlashError { get; set; }
    public string? LastReceiptNumber { get; set; }

    public void OnGet()
    {
        if (!string.IsNullOrEmpty(AsnId))
        {
            Input.AsnId = AsnId;
        }
    }

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

        var result = await _receiving.ReceiveByAsnAsync(userId, key, Input, ct);

        if (result.IsFailure)
        {
            FlashError = result.Error ?? "ReceiveByAsnAsync failed without a message.";
            return Page();
        }

        LastReceiptNumber = result.Value!.ReceiptNumber;
        FlashOk = $"Received ASN line as {LastReceiptNumber} ({result.Value.QuantityReceived} unit(s)).";
        Input = new ReceiveByAsnCommand { AsnId = Input.AsnId };
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
            EntityType = "Receiving.ByAsn",
            EntityId = Input?.AsnId ?? AsnId ?? "",
            RelatedIds = Array.Empty<string>(),
            FocusedField = baseCtx.FocusedField,
            Tab = baseCtx.Tab,
            BuiltAt = baseCtx.BuiltAt,
        };
    }
}
