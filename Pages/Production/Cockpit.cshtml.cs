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
    private readonly ILogger<CockpitModel> _logger;

    public CockpitModel(
        IProductionCockpitService cockpit,
        ICostRollupService rollupSvc,
        ICostTransactionService costTxnSvc,
        IProductionVarianceCloseService varianceSvc,
        ILogger<CockpitModel> logger)
    {
        _cockpit = cockpit;
        _rollupSvc = rollupSvc;
        _costTxnSvc = costTxnSvc;
        _varianceSvc = varianceSvc;
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

    private static readonly string[] KnownTabs =
    {
        TabOverview, TabBom, TabRouting, TabLabor, TabScrap,
        TabInventory, TabCost, TabQuality, TabDocuments,
        TabSchedule, TabGenealogy, TabAudit
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
            },
        };
    }

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
