using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Receiving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Receiving;

// Sprint 12A PR #7 — Match-Orphan confirmation page.
//
// Two-pane confirmation flow for the AI-suggested PO match on the
// Receiving Orphans tab. GET shows the receipt + the candidate PO side
// by side so the receiver can confirm before committing; POST calls
// IReceivingControlCenterService.MatchOrphanReceiptAsync (Sprint 11
// plumb) inside an idempotency envelope and redirects back to
// /Receiving?tab=orphans with a toast.
//
// Route: /Receiving/Match-Orphan/{ReceiptId:int}/{PoNumber}
// Hyphenated + route-segment per the established convention on this
// codebase (mirrors /Receiving/By-Asn/{AsnId?} after the PR #6 hotfix).
[Authorize(Policy = "StockReceipt.Create")]
public sealed class MatchOrphanModel : VoiceReadyPageModel
{
    private readonly AppDbContext _db;
    private readonly IReceivingControlCenterService _receiving;

    public MatchOrphanModel(
        AppDbContext db,
        IReceivingControlCenterService receiving)
    {
        _db = db;
        _receiving = receiving;
    }

    [BindProperty(SupportsGet = true)]
    public int ReceiptId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string PoNumber { get; set; } = string.Empty;

    [BindProperty]
    public string? PoLineId { get; set; }

    [BindProperty]
    public string? OperatorNote { get; set; }

    // Display payload (populated in OnGet + on POST-with-validation-error).
    public StockReceipt? Receipt { get; private set; }
    public PurchaseOrder? Po { get; private set; }
    public PurchaseOrderLine? SuggestedLine { get; private set; }

    public string? FlashError { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        if (Receipt is null)
        {
            return NotFound($"Receipt {ReceiptId} not found.");
        }
        if (Po is null)
        {
            return NotFound($"Purchase Order {PoNumber} not found.");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadAsync(ct);

        if (Receipt is null || Po is null)
        {
            FlashError = "Receipt or PO no longer exists.";
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

        var cmd = new MatchOrphanReceiptCommand
        {
            ReceiptId = ReceiptId,
            PoNumber = PoNumber,
            PoLineId = string.IsNullOrWhiteSpace(PoLineId) ? null : PoLineId,
        };

        var result = await _receiving.MatchOrphanReceiptAsync(userId, key, cmd, ct);

        if (result.IsFailure)
        {
            FlashError = result.Error ?? "MatchOrphanReceiptAsync failed without a message.";
            return Page();
        }

        // Pass a toast hint via TempData; /Receiving picks it up if wired.
        TempData["FlashOk"] = $"Matched {Receipt.ReceiptNumber} to {PoNumber}.";
        return Redirect("/Receiving?tab=orphans");
    }

    public Vendor? PreferredVendor { get; private set; }

    private async Task LoadAsync(CancellationToken ct)
    {
        Receipt = await _db.StockReceipts
            .AsNoTracking()
            .Include(r => r.Item)
            .FirstOrDefaultAsync(r => r.Id == ReceiptId, ct);

        // Resolve preferred vendor via ItemCompanyStockings — separate query
        // to dodge EF shadow-FK bug on the chained Include (i0.ItemId1).
        if (Receipt?.ItemId > 0)
        {
            var stocking = await _db.ItemCompanyStockings
                .AsNoTracking()
                .Include(s => s.PreferredVendor)
                .FirstOrDefaultAsync(s => s.ItemId == Receipt.ItemId && s.PreferredVendorId != null, ct);
            PreferredVendor = stocking?.PreferredVendor;
        }

        if (!string.IsNullOrEmpty(PoNumber))
        {
            Po = await _db.PurchaseOrders
                .AsNoTracking()
                .Include(p => p.Vendor)
                .Include(p => p.Lines!)
                    .ThenInclude(l => l.Item)
                .FirstOrDefaultAsync(p => p.PONumber == PoNumber, ct);
        }

        // Pick the line whose ItemId matches the receipt's ItemId, if any.
        // The receiver can change the line in the dropdown — this is just the
        // AI's best guess pre-filled.
        if (Po?.Lines != null && Receipt is not null)
        {
            SuggestedLine = Po.Lines.FirstOrDefault(l => l.ItemId == Receipt.ItemId);
            if (SuggestedLine != null && string.IsNullOrEmpty(PoLineId))
            {
                PoLineId = SuggestedLine.Id.ToString();
            }
        }
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
            EntityType = "Receiving.MatchOrphan",
            EntityId = $"{ReceiptId}:{PoNumber}",
            RelatedIds = Array.Empty<string>(),
            FocusedField = baseCtx.FocusedField,
            Tab = baseCtx.Tab,
            BuiltAt = baseCtx.BuiltAt,
        };
    }
}
