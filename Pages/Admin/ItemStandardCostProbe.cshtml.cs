using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B6 Foundation Sprint PR-FS-3 (2026-05-26) — admin probe for IItemStandardCostService.
// Read-only diagnostic — no writes.
[Authorize(Roles = "Admin")]
public sealed class ItemStandardCostProbeModel : PageModel
{
    private readonly IItemStandardCostService _svc;
    private readonly ILogger<ItemStandardCostProbeModel> _logger;

    public ItemStandardCostProbeModel(
        IItemStandardCostService svc,
        ILogger<ItemStandardCostProbeModel> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public int? SiteId { get; set; }
    [BindProperty] public DateTime? AsOfUtc { get; set; }

    public ItemCostBreakdown? Breakdown { get; private set; }
    public IReadOnlyList<ItemStandardCostElement> History { get; private set; } = Array.Empty<ItemStandardCostElement>();
    public bool NotFound { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ItemId <= 0)
        {
            ModelState.AddModelError(nameof(ItemId), "ItemId must be > 0.");
            return Page();
        }

        Breakdown = await _svc.GetCostBreakdownAsync(ItemId, SiteId, AsOfUtc, ct);
        NotFound = Breakdown is null;

        if (!NotFound)
        {
            History = await _svc.GetHistoryAsync(ItemId, SiteId, ct);
        }

        _logger.LogInformation(
            "ItemStandardCostProbe: ItemId={ItemId} SiteId={SiteId} AsOfUtc={AsOfUtc} Total={Total} Found={Found}",
            ItemId, SiteId, AsOfUtc, Breakdown?.Total, !NotFound);

        return Page();
    }
}
