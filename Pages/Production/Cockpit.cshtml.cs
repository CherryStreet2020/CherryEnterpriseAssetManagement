using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;
using Abs.FixedAssets.Services.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Production;

// =============================================================================
// B8 PR-PRO-8 + PRO-10 — PRO Cockpit Control Center.
//
// Per-Production-Order deep-dive surface. 12 tabs, 16-metric summary bar,
// 24-column BOM grid, 22-column Routing grid, readiness integration.
//
// PR-PRO-10 (2026-05-28): 3-mode UI gating — Planner / Supervisor / Operator.
// Same cockpit, 3 views. Auto-defaults from User.Role. Admin can toggle freely.
//   Planner  — full visibility, all actions, cost columns, schedule edits
//   Supervisor — queue + exceptions + labor + scrap + quality actions
//   Operator — start/stop, issue, complete, scrap, rework, view instructions
//
// Route: /Production/Orders/{id}/Cockpit?tab=X&mode=Y
// =============================================================================

/// <summary>Cockpit view mode — gates visible columns, actions, and drawer content.</summary>
public enum CockpitMode
{
    /// <summary>Full visibility — all actions, cost columns, schedule edits.</summary>
    Planner = 0,
    /// <summary>Queue + exceptions + labor + scrap + quality actions.</summary>
    Supervisor = 1,
    /// <summary>Start/stop, issue, complete, scrap, rework, view instructions.</summary>
    Operator = 2,
}
[Authorize]
public sealed class CockpitModel : PageModel
{
    private readonly IProductionCockpitService _cockpit;
    private readonly ICostRollupService _rollupSvc;
    private readonly ICostTransactionService _costTxnSvc;
    private readonly IProductionVarianceCloseService _varianceSvc;
    private readonly IItemCrystallizationService _crystallization;
    private readonly ILogger<CockpitModel> _logger;

    public CockpitModel(
        IProductionCockpitService cockpit,
        ICostRollupService rollupSvc,
        ICostTransactionService costTxnSvc,
        IProductionVarianceCloseService varianceSvc,
        IItemCrystallizationService crystallization,
        ILogger<CockpitModel> logger)
    {
        _cockpit = cockpit;
        _rollupSvc = rollupSvc;
        _costTxnSvc = costTxnSvc;
        _varianceSvc = varianceSvc;
        _crystallization = crystallization;
        _logger = logger;
    }

    // ----- Route parameters -----
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabKey { get; set; }

    [BindProperty(SupportsGet = true, Name = "mode")]
    public string? ModeKey { get; set; }

    // ----- Mode gating (PR-PRO-10) -----
    public CockpitMode ActiveMode { get; private set; } = CockpitMode.Planner;
    public bool CanToggleMode { get; private set; }

    /// <summary>True when the mode hides cost/financial columns.</summary>
    public bool HideCostColumns => ActiveMode == CockpitMode.Operator;
    /// <summary>True when the mode hides schedule-edit capabilities.</summary>
    public bool HideScheduleEdits => ActiveMode == CockpitMode.Operator;
    /// <summary>True when showing the full planner view.</summary>
    public bool IsPlannerMode => ActiveMode == CockpitMode.Planner;

    // ----- Hydrated payloads -----
    public CockpitPageHeaderViewModel? PageHeader { get; private set; }
    public CockpitKpiBandViewModel? KpiBand { get; private set; }
    public CockpitTabShellModel TabShell { get; private set; } = new();
    public CockpitData? Data { get; private set; }
    public string? ErrorMessage { get; private set; }

    // ----- Sprint 14.4 PR-5: Cost Cockpit View data -----
    public ProductionOrderCostSummary? CostSummary { get; private set; }
    public CostRollupResult? CostRollupData { get; private set; }
    public IReadOnlyList<ProductionVariance> CostVariances { get; private set; } = Array.Empty<ProductionVariance>();
    public string CostViewMode { get; set; } = "financial"; // "financial" or "exploded"

    // ----- B7 Wave D PR-1: Make-or-Buy Cockpit panel data -----
    /// <summary>The PRO item's latest persisted make-or-buy decision, re-hydrated via ExplainAsync. Null = none/no item.</summary>
    public MakeBuyCockpitPanelModel? MakeBuyPanel { get; private set; }
    /// <summary>Empty-state / status message for the Make/Buy tab when no panel renders.</summary>
    public string? MakeBuyMessage { get; private set; }

    // ----- B7 Wave D PR-3: Crystallization Cockpit panel data (CLOSES B7) -----
    /// <summary>The read-only crystallization preview for this PRO. Null = nothing to preview.</summary>
    public CrystallizationCockpitPanelModel? CrystallizationPanel { get; private set; }
    /// <summary>Empty-state / status message for the Crystallize tab when no panel renders.</summary>
    public string? CrystallizationMessage { get; private set; }
    /// <summary>True/error flag so the Crystallize tab can color the last action result.</summary>
    public bool CrystallizationActionError { get; private set; }

    // ----- Tab keys -----
    public const string TabOverview   = "overview";
    public const string TabBom        = "bom";
    public const string TabRouting    = "routing";
    public const string TabLabor      = "labor";
    public const string TabScrap      = "scrap";
    public const string TabInventory  = "inventory";
    public const string TabCost       = "cost";
    public const string TabQuality    = "quality";
    public const string TabDocuments  = "documents";
    public const string TabSchedule   = "schedule";
    public const string TabGenealogy  = "genealogy";
    public const string TabAudit      = "audit";
    public const string TabMakeBuy    = "makebuy";
    public const string TabCrystallize = "crystallize";

    private static readonly string[] KnownTabs =
    {
        TabOverview, TabBom, TabRouting, TabLabor, TabScrap,
        TabInventory, TabCost, TabQuality, TabDocuments,
        TabSchedule, TabGenealogy, TabAudit, TabMakeBuy, TabCrystallize
    };

    public string ActiveTab =>
        !string.IsNullOrEmpty(TabKey)
        && Array.Exists(KnownTabs, k => string.Equals(k, TabKey, StringComparison.OrdinalIgnoreCase))
            ? TabKey.ToLowerInvariant()
            : TabOverview;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (Id <= 0)
        {
            ErrorMessage = "Invalid Production Order ID.";
            return Page();
        }

        var result = await _cockpit.GetCockpitDataAsync(Id, ct);
        if (result.IsFailure)
        {
            ErrorMessage = result.Error;
            return Page();
        }

        Data = result.Value;
        ResolveMode();
        HydratePageHeader();
        HydrateKpiBand();
        HydrateTabShell();

        // Sprint 14.4 PR-5: Lazy-load cost data only when cost tab is active
        if (ActiveTab == TabCost && !HideCostColumns)
            await LoadCostDataAsync(ct);

        // B7 Wave D PR-1: Lazy-load the make-or-buy decision only when its tab is active.
        // Gated like the Cost tab — the panel exposes make/buy cost + supplier quote, which
        // Operator mode (HideCostColumns) deliberately hides.
        if (ActiveTab == TabMakeBuy)
        {
            if (HideCostColumns)
                MakeBuyMessage = "Make-or-buy detail (cost comparison + supplier quote) is hidden "
                    + "in Operator view. Switch to Supervisor or Planner mode to see the decision.";
            else
                await LoadMakeBuyAsync(ct);
        }

        // B7 Wave D PR-3: Lazy-load the crystallization preview only when its tab is active.
        // Gated like Cost/Make-Buy — the preview exposes the seeded standard cost, which
        // Operator mode (HideCostColumns) deliberately hides.
        if (ActiveTab == TabCrystallize)
        {
            if (HideCostColumns)
                CrystallizationMessage = "Crystallization (seeded standard cost + would-be standard) is hidden "
                    + "in Operator view. Switch to Supervisor or Planner mode to harvest a standard.";
            else
                await LoadCrystallizationAsync(ct);
        }

        return Page();
    }

    // ----- PR-PRO-10: Mode resolution -----
    private void ResolveMode()
    {
        // Admin users can toggle freely; everyone else auto-defaults
        var isAdmin = User.IsInRole("Admin");
        CanToggleMode = isAdmin;

        // Auto-default from User.Role
        var role = User.Claims
            .FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value ?? "Viewer";

        var defaultMode = role switch
        {
            "Admin" => CockpitMode.Planner,
            "Accountant" => CockpitMode.Supervisor,
            _ => CockpitMode.Operator,
        };
        ActiveMode = defaultMode;

        // Explicit ?mode= override — admin can pick any mode, non-admin can only
        // stay at their default or go MORE restrictive (never escalate)
        if (!string.IsNullOrEmpty(ModeKey)
            && Enum.TryParse<CockpitMode>(ModeKey, ignoreCase: true, out var parsed))
        {
            if (isAdmin || parsed >= defaultMode) // higher enum = more restrictive
                ActiveMode = parsed;
        }
    }

    private void HydratePageHeader()
    {
        if (Data == null) return;
        var s = Data.Summary;
        PageHeader = new CockpitPageHeaderViewModel
        {
            Title = $"PRO {s.OrderNumber}",
            Scope = s.PartNumber != null ? $"{s.PartNumber} Rev {s.Revision}" : "No item linked",
            Subtitle = s.Description ?? "",
            ShowLive = true,
            RefreshedAtText = $"updated {DateTime.UtcNow:HH:mm} UTC",
            ShowVoiceButton = true,
            VoiceButtonLabel = "Voice",
        };
    }

    private void HydrateKpiBand()
    {
        if (Data == null) return;
        var s = Data.Summary;

        // Determine tone for key metrics
        var statusTone = s.Status switch
        {
            ProductionOrderStatus.OnHold => "danger",
            ProductionOrderStatus.Completed or ProductionOrderStatus.Closed => "success",
            ProductionOrderStatus.InProgress => "info",
            _ => "neutral",
        };

        var matTone = s.MaterialReadinessPercent >= 100 ? "success"
            : s.MaterialReadinessPercent >= 80 ? "warning" : "danger";

        var opTone = s.OperationProgressPercent >= 100 ? "success"
            : s.OperationProgressPercent >= 50 ? "info" : "neutral";

        var dueTone = s.DaysLate.HasValue && s.DaysLate > 0 ? "danger"
            : s.DaysLate.HasValue && s.DaysLate == 0 ? "warning" : "success";

        KpiBand = new CockpitKpiBandViewModel
        {
            ShowLiveIndicator = false,
            HeroMode = true,
            Tiles = new List<CockpitKpiTileViewModel>
            {
                new()
                {
                    Label = "Status",
                    Value = s.Status.ToString(),
                    Tone = statusTone,
                },
                new()
                {
                    Label = "Material Ready",
                    Value = $"{s.MaterialReadinessPercent:F0}%",
                    Tone = matTone,
                    SubText = s.OpenMaterialShortages > 0
                        ? $"{s.OpenMaterialShortages} shortage(s)" : null,
                },
                new()
                {
                    Label = "Op Progress",
                    Value = $"{s.OperationProgressPercent:F0}%",
                    Tone = opTone,
                    SubText = s.CurrentOperation,
                },
                new()
                {
                    Label = s.DaysLate.HasValue && s.DaysLate > 0
                        ? $"{s.DaysLate}d Late" : "On Track",
                    Value = s.DueDate?.ToString("MMM d") ?? "No date",
                    Tone = dueTone,
                    SubText = $"Good: {s.QuantityCompleted} / Ord: {s.QuantityOrdered}",
                },
            },
        };
    }

    private void HydrateTabShell()
    {
        TabShell = new CockpitTabShellModel
        {
            ActiveTabKey = ActiveTab,
            BaseRoute = $"/Production/Orders/{Id}/Cockpit",
            Tabs = new List<CockpitTab>
            {
                new(TabOverview,  "Overview",    "fas fa-gauge-high",           IsDefault: true),
                new(TabBom,       "BOM",         "fas fa-cubes"),
                new(TabRouting,   "Routing",     "fas fa-route"),
                new(TabLabor,     "Labor",       "fas fa-users"),
                new(TabScrap,     "Scrap/NCR",   "fas fa-triangle-exclamation"),
                new(TabInventory, "Inventory",   "fas fa-boxes-stacked"),
                new(TabCost,      "Cost/WIP",    "fas fa-coins"),
                new(TabQuality,   "Quality",     "fas fa-clipboard-check"),
                new(TabDocuments, "Documents",   "fas fa-file-lines"),
                new(TabSchedule,  "Schedule",    "fas fa-calendar-days"),
                new(TabGenealogy, "Genealogy",   "fas fa-sitemap"),
                new(TabAudit,     "Audit Trail", "fas fa-clock-rotate-left"),
                new(TabMakeBuy,   "Make/Buy",    "fas fa-scale-balanced"),
                new(TabCrystallize, "Crystallize", "fas fa-gem"),
            },
        };
    }

    // ----- B7 Wave D PR-1: Make-or-Buy panel lazy-loading -----
    // Surfaces the PRO item's latest persisted MakeBuyDecision (PR-7 schema), re-hydrated
    // through the engine's ExplainAsync (PR-8) — the "why did we make vs buy this?" record.
    // Read-only. Per ADR-025 the data read lives in IProductionCockpitService (which owns
    // AppDbContext); this page only maps the service DTO to the panel view-model.
    private async Task LoadMakeBuyAsync(CancellationToken ct)
    {
        var panel = await _cockpit.GetMakeBuyPanelAsync(Id, ct);
        if (panel.IsFailure)
        {
            MakeBuyMessage = panel.Error;
            return;
        }

        var p = panel.Value!;
        if (p.Data is null)
        {
            MakeBuyMessage = p.EmptyReason ?? "No make-or-buy decision to show.";
            return;
        }

        MakeBuyPanel = new MakeBuyCockpitPanelModel
        {
            Result = p.Data.Result,
            PartNumber = p.Data.PartNumber,
            Description = p.Data.Description,
            DecidedAtUtc = p.Data.DecidedAtUtc,
            Context = p.Data.Context,
            SupplierName = p.Data.SupplierName,
            BuyThreshold = p.Data.BuyThreshold,
        };
    }

    // ----- B7 Wave D PR-3: Crystallization panel lazy-loading + write actions (CLOSES B7) -----
    // Read path: read-only PreviewCrystallizationAsync via IProductionCockpitService (ADR-025 —
    // the data read lives in the service). Write path (crystallize / reverse) routes through
    // IItemCrystallizationService directly (a service, not AppDbContext — ADR-025 compliant);
    // each is user-initiated by an explicit button click and human-confirmed for dedupe.
    private async Task LoadCrystallizationAsync(CancellationToken ct)
    {
        var panel = await _cockpit.GetCrystallizationPanelAsync(Id, ct);
        if (panel.IsFailure)
        {
            CrystallizationMessage = panel.Error;
            return;
        }

        var p = panel.Value!;
        if (p.Data is null)
        {
            CrystallizationMessage = p.EmptyReason ?? "Nothing to crystallize for this order.";
            return;
        }

        CrystallizationPanel = new CrystallizationCockpitPanelModel { Preview = p.Data.Preview };
    }

    /// <summary>Crystallize the job into a reusable standard. <paramref name="link"/> confirms a
    /// dedupe-link to an existing master (decision #3 — never auto-linked); otherwise mints new.</summary>
    public async Task<IActionResult> OnPostCrystallizeAsync(bool link, int? linkItemId, CancellationToken ct)
    {
        var request = link && linkItemId is > 0
            ? new CrystallizeRequest(Id, Actor(), ConfirmDedupeLink: true, LinkToExistingItemId: linkItemId)
            : new CrystallizeRequest(Id, Actor(), ForceCreateNew: true);

        var result = await _crystallization.CrystallizeAsync(request, ct);
        CrystallizationActionError = result.IsFailure;
        CrystallizationMessage = result.IsSuccess ? result.Value!.Message : result.Error;

        return await ReloadForCrystallizeTabAsync(ct);
    }

    /// <summary>Reverse the latest crystallization on this PRO. The as-built history is never rewritten.</summary>
    public async Task<IActionResult> OnPostReverseCrystallizationAsync(string? reason, CancellationToken ct)
    {
        var result = await _crystallization.ReverseLatestForProductionOrderAsync(
            Id,
            string.IsNullOrWhiteSpace(reason) ? "Reversed from the Production Cockpit." : reason,
            Actor(), ct);
        CrystallizationActionError = result.IsFailure;
        CrystallizationMessage = result.IsSuccess ? result.Value!.Message : result.Error;

        return await ReloadForCrystallizeTabAsync(ct);
    }

    // Re-hydrate the cockpit shell after a crystallize/reverse write so the page
    // re-renders on the Crystallize tab with the refreshed preview.
    private async Task<IActionResult> ReloadForCrystallizeTabAsync(CancellationToken ct)
    {
        var actionMsg = CrystallizationMessage;
        var actionErr = CrystallizationActionError;

        TabKey = TabCrystallize;
        var result = await _cockpit.GetCockpitDataAsync(Id, ct);
        if (result.IsFailure)
        {
            ErrorMessage = result.Error;
            return Page();
        }
        Data = result.Value;
        ResolveMode();
        HydratePageHeader();
        HydrateKpiBand();
        HydrateTabShell();

        if (!HideCostColumns)
            await LoadCrystallizationAsync(ct);

        // Keep the action result message (LoadCrystallizationAsync may have set its own).
        CrystallizationMessage = actionMsg;
        CrystallizationActionError = actionErr;
        return Page();
    }

    private string Actor() => User.Identity?.Name ?? "cockpit-crystallize";

    // ----- Sprint 14.4 PR-5: Cost data lazy-loading -----
    private async Task LoadCostDataAsync(CancellationToken ct)
    {
        try
        {
            // Load cost summary
            CostSummary = await _costTxnSvc.GetSummaryAsync(Id, ct);

            // Determine rollup mode from query string
            if (Request.Query.TryGetValue("costview", out var cv)
                && string.Equals(cv.FirstOrDefault(), "exploded", StringComparison.OrdinalIgnoreCase))
            {
                CostViewMode = "exploded";
            }

            // Execute rollup (or load latest run)
            var latestRun = await _rollupSvc.GetLatestRunAsync(Id, ct);
            if (latestRun != null)
            {
                var lines = await _rollupSvc.GetLinesAsync(latestRun.Id, ct);
                var exceptions = await _rollupSvc.GetExceptionsAsync(latestRun.Id, ct);
                CostRollupData = new CostRollupResult
                {
                    Run = latestRun,
                    Lines = lines,
                    Exceptions = exceptions,
                };
            }

            // Load variances
            CostVariances = await _varianceSvc.GetVariancesAsync(Id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cost data for PRO {ProId}", Id);
            // Non-fatal — cost tab shows "no data" instead of crashing
        }
    }
}
