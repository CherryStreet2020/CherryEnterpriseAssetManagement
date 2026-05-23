using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.ControlPlane;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared.ControlCenter;
using Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;
using Abs.FixedAssets.Services.Navigation.Cockpit;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Mvc;

namespace Abs.FixedAssets.Pages.Production;

// =============================================================================
// Sprint 13.5 PR #5b.1 — Production Control Center (REWRITE for visual parity
// + design upgrade).
//
// Dean called PR #5b "awful" because I built .cc-* from scratch instead of
// reusing the shared Cockpit primitives. This rewrite composes those exact
// primitives the way Pages/Receiving/ControlCenter.cshtml does — same dark
// theme, same two-pane queue+preview shell, same KPI hero band, same LIVE +
// Voice cluster. Layered on top: the MES design from the research synthesis
// (kill the OEE gauge, cards not rows, routing stepper, voice-driven downtime).
//
// Composition (inheritance):
//   PageModel
//    └── VoiceReadyPageModel             (ADR-014 §D1)
//         └── ControlCenterPageModel      (ADR-016 §D1)
//              └── ControlCenterModel     (this class)
//
// Tabs:
//   queue       — CockpitShellViewModel (queue+preview, the real CC canvas)
//   exceptions  — heuristic-ranked exception lane
//   activity    — recent ProductionOrder mutations
//   (machines / downtime / quality tabs land in PR #5e once event tables exist)
//
// All mutations route through IProductionOrderService / IProductionControlCenter
// Service — the page never writes to AppDbContext.
// =============================================================================
[ControlPlaneExempt("Hybrid CC page — reads + writes route through IProductionControlCenterService / IProductionOrderService. No AppDbContext.")]
public sealed class ControlCenterModel : ControlCenterPageModel
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

    public override string ControlCenterCode => "PRODUCTION";
    public override string ControlCenterTitle => "Production Control Center";

    // ----- Query-string state ----------------------------------------

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "status")]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "lens")]
    public string? LensFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? SearchText { get; set; }

    [BindProperty(SupportsGet = true, Name = "projectId")]
    public int? ProjectIdFilter { get; set; }

    // ----- Hydrated payloads consumed by the view --------------------

    public CockpitPageHeaderViewModel? PageHeader { get; private set; }
    public CockpitKpiBandViewModel? KpiBand { get; private set; }
    public CockpitTabShellModel TabShell { get; private set; } = new();
    public CockpitShellViewModel? QueueShell { get; private set; }
    public ProductionExceptionLanePage? ExceptionLane { get; private set; }
    public ProductionActivityFeedData? ActivityFeed { get; private set; }
    public ProductionAiSuggestionsData? AiSuggestions { get; private set; }
    public ProductionNextUpData? NextUp { get; private set; }

    // ----- Tab keys --------------------------------------------------

    public const string TabQueue = "queue";
    public const string TabExceptions = "exceptions";
    public const string TabActivity = "activity";
    private static readonly string[] KnownTabs = { TabQueue, TabExceptions, TabActivity };

    public string ActiveTab =>
        !string.IsNullOrEmpty(TabKey)
        && Array.Exists(KnownTabs, k => string.Equals(k, TabKey, StringComparison.OrdinalIgnoreCase))
            ? TabKey.ToLowerInvariant()
            : TabQueue;

    public ProductionOrderStatus? ResolvedStatusFilter
    {
        get
        {
            if (string.IsNullOrEmpty(StatusFilter)) return null;
            return Enum.TryParse<ProductionOrderStatus>(StatusFilter, ignoreCase: true, out var s) ? s : null;
        }
    }

    public async Task OnGetAsync(CancellationToken ct)
    {
        // Page header + KPI band + AI strip + tab shell — always present.
        HydratePageHeader();
        await HydrateKpiBandAsync(ct);
        await HydrateAiSuggestionsAsync(ct);

        TabShell = new CockpitTabShellModel
        {
            ActiveTabKey = ActiveTab,
            BaseRoute = "/Production/ControlCenter",
            Tabs = new List<CockpitTab>
            {
                new(TabQueue,      "Dispatch",   "fas fa-list-check",         IsDefault: true),
                new(TabExceptions, "Exceptions", "fas fa-triangle-exclamation"),
                new(TabActivity,   "Activity",   "fas fa-clock-rotate-left"),
            },
        };

        // Per-tab hydration (perf win per ADR-018 §D7).
        switch (ActiveTab)
        {
            case TabQueue:
                await HydrateQueueTabAsync(ct);
                await HydrateNextUpAsync(ct);
                break;
            case TabExceptions:
                {
                    var lane = await _cc.GetExceptionLaneAsync(
                        new ProductionExceptionLaneFilter(Severity: null, Kind: null, Take: 50), ct);
                    if (lane.IsSuccess) ExceptionLane = lane.Value;
                    break;
                }
            case TabActivity:
                {
                    var feed = await _cc.GetActivityFeedAsync(
                        new ProductionActivityFeedFilter(Take: 50), ct);
                    if (feed.IsSuccess) ActivityFeed = feed.Value;
                    break;
                }
        }
    }

    private void HydratePageHeader()
    {
        PageHeader = new CockpitPageHeaderViewModel
        {
            Title            = "Production",
            Scope            = "All sites",
            Subtitle         = "Push-to-talk voice (hold Space)",
            ShowLive         = true,
            RefreshedAtText  = "updated just now",
            ShowVoiceButton  = true,
            VoiceButtonLabel = "Voice",
        };
    }

    private async Task HydrateKpiBandAsync(CancellationToken ct)
    {
        var kpi = await _cc.GetKpiBandAsync(new ProductionKpiBandFilter(), ct);
        if (kpi.IsFailure || kpi.Value is null)
        {
            KpiBand = new CockpitKpiBandViewModel { ShowLiveIndicator = false, HeroMode = true };
            return;
        }
        var d = kpi.Value;
        ProductionKpiTile? Find(string key) =>
            d.Tiles?.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

        // Hero mode — 4 wide tiles in priority order. Page header owns LIVE.
        // Order from MES research: lead with what's screaming (past due, on hold)
        // before the routine counts. OEE / Throughput / Down-Machines tiles get
        // wired in when PR #5c (operations) + PR #5e (downtime events) ship.
        KpiBand = new CockpitKpiBandViewModel
        {
            HeroMode = true,
            ShowLiveIndicator = false,
            Tiles = new[]
            {
                BandTile(Find("past-due"),    "Past Due",    "danger"),
                BandTile(Find("due-today"),   "Due Today",   "warning"),
                BandTile(Find("in-progress"), "In Progress", "info"),
                BandTile(Find("on-hold"),     "On Hold",     "warning"),
            },
        };
    }

    private static CockpitKpiTileViewModel BandTile(ProductionKpiTile? t, string fallbackLabel, string fallbackTone) =>
        new()
        {
            Label = t?.Label ?? fallbackLabel,
            Value = t?.Value ?? "0",
            Tone  = t?.Tone ?? fallbackTone,
            SubText = t?.Hint,
            DrillHref = t?.DrillHref,
        };

    private async Task HydrateAiSuggestionsAsync(CancellationToken ct)
    {
        var ai = await _cc.GetAiSuggestionsAsync(new ProductionAiSuggestionsFilter(), ct);
        if (ai.IsSuccess) AiSuggestions = ai.Value;
    }

    private async Task HydrateNextUpAsync(CancellationToken ct)
    {
        var next = await _cc.GetNextUpAsync(new ProductionNextUpFilter(), ct);
        NextUp = next.IsSuccess ? next.Value : new ProductionNextUpData();
    }

    private async Task HydrateQueueTabAsync(CancellationToken ct)
    {
        var status = ResolvedStatusFilter;
        var queue = await _cc.GetProductionQueueAsync(
            new ProductionQueueFilter(
                Status: status,
                CustomerProjectId: ProjectIdFilter,
                SearchText: SearchText,
                Take: 200),
            ct);

        if (queue.IsFailure || queue.Value is null)
        {
            QueueShell = new CockpitShellViewModel
            {
                Queue = new CockpitQueueViewModel
                {
                    TitleHtml = "Dispatch Queue",
                    TitleIconClass = "fas fa-industry",
                    SearchPlaceholder = "Search PRO# or title...",
                    SearchElementId = "proSearch",
                    FilterFunctionName = "filterQueue",
                    SelectFunctionName = "selectProductionOrder",
                    Empty = new CockpitEmptyViewModel
                    {
                        IconClass = "fas fa-triangle-exclamation",
                        IconTone = "warning",
                        Message = queue.Error ?? "Queue unavailable.",
                    },
                },
                Welcome = new CockpitWelcomeViewModel
                {
                    IconClass = "fas fa-industry",
                    Title = "Select a production order to preview",
                    Subtitle = "Click an order from the queue to see its routing, materials, live progress, and the action tray.",
                },
                PreviewBlobJson = "{}",
                PreviewBlobElementId = "__productionDetails",
            };
            return;
        }

        var data = queue.Value;

        // Group rows through the default time-bucket lens (Overdue / Today / This Week / Later).
        var lens = new ByTimeLens<ProductionQueueRow>();
        var groups = lens.Group(data.Rows);

        var groupVms = groups.Select(g => new CockpitQueueGroupViewModel
        {
            Code = g.Code,
            Label = g.Label,
            Tone = g.Tone,
            IconClass = g.Icon,
            Rows = g.Rows.Cast<ICockpitQueueRow>().ToList(),
        }).ToList();

        int CountFor(string code) =>
            groups.FirstOrDefault(g => string.Equals(g.Code, code, StringComparison.OrdinalIgnoreCase))?.Rows.Count ?? 0;

        var welcome = new CockpitWelcomeViewModel
        {
            IconClass = "fas fa-industry",
            Title = "Select a production order to preview",
            Subtitle = "Click an order to see its routing stepper, live progress, materials/operators/tools, and the action verb tray (Start / Pause / Log Downtime / Log Scrap / Complete).",
            Stats = new[]
            {
                new CockpitWelcomeStat("Past Due",  CountFor("overdue").ToString(),   "danger"),
                new CockpitWelcomeStat("Today",     CountFor("today").ToString(),     "warning"),
                new CockpitWelcomeStat("This Week", CountFor("this-week").ToString()),
                new CockpitWelcomeStat("Later",     CountFor("later").ToString(),     "muted"),
            },
        };

        QueueShell = new CockpitShellViewModel
        {
            Queue = new CockpitQueueViewModel
            {
                TitleHtml = "Dispatch Queue",
                TitleIconClass = "fas fa-industry",
                CountBadge = 0,  // KPI band owns the count
                SearchPlaceholder = "Search PRO# or title...",
                SearchElementId = "proSearch",
                FilterFunctionName = "filterQueue",
                SelectFunctionName = "selectProductionOrder",
                Groups = groupVms,
                Empty = data.Rows.Count == 0
                    ? new CockpitEmptyViewModel { IconClass = "fas fa-check-circle", IconTone = "success", Message = "Queue is clear — nothing active." }
                    : null,
            },
            Welcome = welcome,
            PreviewPartialName = "_CockpitProductionPreview",
            PreviewPartialModel = null,
            PreviewBlobJson = data.PreviewBlobJson,
            PreviewBlobElementId = "__productionDetails",
        };
    }

    // ----- POST handlers (preserved from PR #5b) ---------------------

    public async Task<IActionResult> OnPostTransitionAsync(
        int orderId,
        ProductionOrderStatus newStatus,
        CancellationToken ct)
    {
        var result = await _orders.UpdateStatusAsync(
            new UpdateProductionOrderStatusRequest(orderId, newStatus, User?.Identity?.Name), ct);

        if (result.IsFailure) TempData["Error"] = result.Error;
        else TempData["Success"] = $"PRO-{orderId} → {newStatus}";

        return RedirectToPage(new { tab = ActiveTab, status = StatusFilter, lens = LensFilter, q = SearchText });
    }

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
            new BulkStatusRequest(selectedIds, bulkNewStatus, User?.Identity?.Name), ct);

        if (result.IsFailure) TempData["Error"] = result.Error;
        else
        {
            var o = result.Value!;
            TempData[o.FailureCount == 0 ? "Success" : "Warning"] =
                o.FailureCount == 0
                    ? $"Bulk → {bulkNewStatus}: {o.SuccessCount} succeeded."
                    : $"Bulk → {bulkNewStatus}: {o.SuccessCount} succeeded, {o.FailureCount} failed.";
        }

        return RedirectToPage(new { tab = ActiveTab, status = StatusFilter, lens = LensFilter, q = SearchText });
    }
}
