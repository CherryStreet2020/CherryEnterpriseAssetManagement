using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared;
using Abs.FixedAssets.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Admin.StockReceipts;

// Sprint 4 Phase F Wave 1 PR #5 — StockReceipt list page.
[Authorize(Policy = "StockReceipt.View")]
public class IndexModel : VoiceReadyPageModel
{
    private readonly IStockReceiptService _svc;

    public IndexModel(IStockReceiptService svc) => _svc = svc;

    [BindProperty(SupportsGet = true)]
    public StockReceiptStatus? StatusFilter { get; set; }

    public IReadOnlyList<StockReceipt> Receipts { get; private set; } = new List<StockReceipt>();
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var r = await _svc.ListAsync(StatusFilter, HttpContext.RequestAborted);
        if (r.IsFailure)
        {
            ErrorMessage = r.Error;
            return Page();
        }
        Receipts = r.Value!;
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
            EntityType = nameof(StockReceipt),
            EntityId = null,
            RelatedIds = b.RelatedIds,
            FocusedField = b.FocusedField,
            Tab = StatusFilter?.ToString() ?? b.Tab,
            BuiltAt = b.BuiltAt,
        };
    }
}
