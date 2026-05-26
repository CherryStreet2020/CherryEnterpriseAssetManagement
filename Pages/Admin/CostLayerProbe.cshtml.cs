using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

// B6 Foundation Sprint PR-FS-4 (2026-05-26) — admin probe for ICostLayerService.
[Authorize(Roles = "Admin")]
public sealed class CostLayerProbeModel : PageModel
{
    private readonly ICostLayerService _svc;
    private readonly AppDbContext _db;
    private readonly ILogger<CostLayerProbeModel> _logger;

    public CostLayerProbeModel(ICostLayerService svc, AppDbContext db, ILogger<CostLayerProbeModel> logger)
    {
        _svc = svc;
        _db = db;
        _logger = logger;
    }

    [BindProperty] public int ItemId { get; set; }
    [BindProperty] public int? SiteId { get; set; }

    public bool NotFound { get; private set; }
    public CostLayerSummary? Summary { get; private set; }
    public IReadOnlyList<CostLayer> OpenLayers { get; private set; } = Array.Empty<CostLayer>();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (ItemId <= 0)
        {
            ModelState.AddModelError(nameof(ItemId), "ItemId must be > 0.");
            return Page();
        }

        var itemExists = await _db.Items.AsNoTracking().AnyAsync(i => i.Id == ItemId, ct);
        if (!itemExists)
        {
            NotFound = true;
            return Page();
        }

        OpenLayers = await _svc.GetOpenLayersAsync(ItemId, SiteId, ct);
        var totalQty = await _svc.GetTotalOpenQuantityAsync(ItemId, SiteId, ct);
        var avg = await _svc.GetWeightedAverageCostAsync(ItemId, SiteId, ct);

        Summary = new CostLayerSummary(
            ItemId: ItemId,
            SiteId: SiteId,
            OpenLayerCount: OpenLayers.Count,
            TotalOpenQuantity: totalQty,
            WeightedAverageCost: avg,
            FifoNextCost: OpenLayers.FirstOrDefault()?.UnitCost,
            LifoNextCost: OpenLayers.LastOrDefault()?.UnitCost);

        _logger.LogInformation(
            "CostLayerProbe: Item={ItemId} Site={SiteId} OpenLayers={Count} TotalQty={Qty} WeightedAvg={Avg}",
            ItemId, SiteId, OpenLayers.Count, totalQty, avg);

        return Page();
    }
}

public sealed record CostLayerSummary(
    int ItemId,
    int? SiteId,
    int OpenLayerCount,
    decimal TotalOpenQuantity,
    decimal WeightedAverageCost,
    decimal? FifoNextCost,
    decimal? LifoNextCost);
