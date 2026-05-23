using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.ControlPlane;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Production;

// Sprint 13.5 PR #5b — /Production/ControlCenter — the Production CONTROL
// CENTER landing page. Composes IProductionControlCenterService (PR #5a)
// data into the BIC Control Center contract:
//
//   ┌──────────────────────────────────────────────────────────┐
//   │ PRODUCTION CONTROL CENTER                                │
//   │ Site · Time Horizon · Search · Voice                     │
//   ├──────────────────────────────────────────────────────────┤
//   │ KPI BAND — 6 hero tiles, clickable to filter             │
//   ├──────────────────────────────────────────────────────────┤
//   │ AI SUMMARY STRIP                                         │
//   ├──────────────────────────────────────────────────────────┤
//   │ TAB BAR — Queue · Exceptions · Activity                  │
//   ├──────────────┬──────────────────────────┬────────────────┤
//   │ Next Up      │ Queue cards (filtered)   │ Quick actions  │
//   │ Filter pills │ Bulk-select toolbar      │ AI suggestions │
//   │              │                          │ Drill links    │
//   └──────────────┴──────────────────────────┴────────────────┘
//
// Every mutation routes through IProductionOrderService / IProductionControl
// CenterService — the page never writes to AppDbContext directly. Marked
// [ControlPlaneExempt] because both service dependencies are non-DbContext
// (the analyzer flags AppDbContext injection specifically; this page only
// reads from the service layer).
[ControlPlaneExempt("Hybrid CC page — every read + write routes through IProductionControlCenterService / IProductionOrderService. No AppDbContext injection.")]
public sealed class ControlCenterModel : PageModel
{
    private readonly IProductionControlCenterService _cc;
    private readonly IProductionOrderService _orders;

    public ControlCenterModel(
        IProductionControlCenterService cc,
        IProductionOrderService orders)
    {
        _cc = cc;
        _orders = orders;
    }

    // Query-string bound state.
    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "lens")]
    public string? LensFilter { get; set; }                // past-due | due-today

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? SearchText { get; set; }

    [BindProperty(SupportsGet = true, Name = "projectId")]
    public int? ProjectIdFilter { get; set; }

    // Hydrated payloads (consumed by the Razor view).
    public ProductionKpiBandData? KpiBand { get; private set; }
    public ProductionQueueData? Queue { get; private set; }
    public ProductionExceptionLanePage? ExceptionLane { get; private set; }
    public ProductionActivityFeedData? ActivityFeed { get; private set; }
    public ProductionNextUpData? NextUp { get; private set; }
    public ProductionAiSuggestionsData? AiSuggestions { get; private set; }

    public const string TabQueue = "queue";
    public const string TabExceptions = "exceptions";
    public const string TabActivity = "activity";
    private static readonly string[] KnownTabs = { TabQueue, TabExceptions, TabActivity };

    public string ActiveTab =>
        !string.IsNullOrEmpty(TabKey)
        && System.Array.Exists(KnownTabs, k => string.Equals(k, TabKey, System.StringComparison.OrdinalIgnoreCase))
            ? TabKey.ToLowerInvariant()
            : TabQueue;

    public ProductionOrderStatus? ResolvedStatusFilter
    {
        get
        {
            if (string.IsNullOrEmpty(StatusFilter)) return null;
            return System.Enum.TryParse<ProductionOrderStatus>(StatusFilter, ignoreCase: true, out var s)
                ? s : null;
        }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        // KPI band + AI summary are always rendered (every tab).
        var kpi = await _cc.GetKpiBandAsync(new ProductionKpiBandFilter(), ct);
        if (kpi.IsSuccess) KpiBand = kpi.Value;

        var ai = await _cc.GetAiSuggestionsAsync(new ProductionAiSuggestionsFilter(), ct);
        if (ai.IsSuccess) AiSuggestions = ai.Value;

        // Per-tab hydration — skips work for non-active tabs (perf win per
        // ADR-018 §D7).
        switch (ActiveTab)
        {
            case TabQueue:
                {
                    var status = ResolvedStatusFilter;
                    // Lens shortcuts compile to status + scheduledEnd filter (handled in service).
                    var queue = await _cc.GetProductionQueueAsync(
                        new ProductionQueueFilter(
                            Status: status,
                            CustomerProjectId: ProjectIdFilter,
                            SearchText: SearchText,
                            Take: 100),
                        ct);
                    if (queue.IsSuccess) Queue = queue.Value;

                    var next = await _cc.GetNextUpAsync(new ProductionNextUpFilter(), ct);
                    if (next.IsSuccess) NextUp = next.Value;
                    break;
                }
            case TabExceptions:
                {
                    var lane = await _cc.GetExceptionLaneAsync(
                        new ProductionExceptionLaneFilter(Severity: null, Kind: null, Take: 50),
                        ct);
                    if (lane.IsSuccess) ExceptionLane = lane.Value;
                    break;
                }
            case TabActivity:
                {
                    var feed = await _cc.GetActivityFeedAsync(
                        new ProductionActivityFeedFilter(Take: 50),
                        ct);
                    if (feed.IsSuccess) ActivityFeed = feed.Value;
                    break;
                }
        }
    }

    // Quick-action verb tray — single-row status transition. Wires through
    // IProductionOrderService so legal-transition map + chain emit + CHERRY025
    // all apply.
    public async Task<IActionResult> OnPostTransitionAsync(
        int orderId,
        ProductionOrderStatus newStatus,
        CancellationToken ct)
    {
        var result = await _orders.UpdateStatusAsync(
            new UpdateProductionOrderStatusRequest(orderId, newStatus, User?.Identity?.Name),
            ct);

        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
        }
        else
        {
            TempData["Success"] = $"PRO-{orderId} → {newStatus}";
        }

        return RedirectToPage(new { tab = ActiveTab, status = StatusFilter, lens = LensFilter, q = SearchText });
    }

    // Bulk-action toolbar — multi-row status transition. Routes through
    // BulkUpdateStatusAsync which itself iterates single-row calls so each
    // mutation gets the full legal-transition / chain-emit / control-plane
    // treatment per row.
    public async Task<IActionResult> OnPostBulkTransitionAsync(
        int[] selectedIds,
        ProductionOrderStatus bulkNewStatus,
        CancellationToken ct)
    {
        if (selectedIds == null || selectedIds.Length == 0)
        {
            TempData["Error"] = "Select at least one order.";
            return RedirectToPage(new { tab = ActiveTab });
        }

        var result = await _cc.BulkUpdateStatusAsync(
            new BulkStatusRequest(selectedIds, bulkNewStatus, User?.Identity?.Name),
            ct);

        if (result.IsFailure)
        {
            TempData["Error"] = result.Error;
        }
        else
        {
            var outcome = result.Value!;
            if (outcome.FailureCount == 0)
            {
                TempData["Success"] = $"Bulk → {bulkNewStatus}: {outcome.SuccessCount} succeeded.";
            }
            else
            {
                TempData["Warning"] = $"Bulk → {bulkNewStatus}: {outcome.SuccessCount} succeeded, {outcome.FailureCount} failed.";
            }
        }

        return RedirectToPage(new { tab = ActiveTab, status = StatusFilter, lens = LensFilter, q = SearchText });
    }
}
