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
// B8 PR-PRO-8 (2026-05-27) — PRO Cockpit Control Center.
//
// Per-Production-Order deep-dive surface. 12 tabs, 16-metric summary bar,
// 24-column BOM grid, 22-column Routing grid, readiness integration.
//
// Route: /Production/Orders/{id}/Cockpit
//
// Design principle (LOCKED): "Every BOM line and every routing operation
// should show planned vs actual, status, exception, cost impact,
// traceability, and the next allowed transaction."
//
// Tabs: Overview · BOM · Routing · Labor · Scrap/Rework/NCR · Inventory Tx ·
//       Cost/WIP · Quality · Documents · Schedule · Genealogy · Audit
//
// Composes shared Cockpit primitives (same ones Receiving + Production CC use).
// =============================================================================
[Authorize]
public sealed class CockpitModel : PageModel
{
    private readonly IProductionCockpitService _cockpit;
    private readonly ILogger<CockpitModel> _logger;

    public CockpitModel(IProductionCockpitService cockpit, ILogger<CockpitModel> logger)
    {
        _cockpit = cockpit;
        _logger = logger;
    }

    // ----- Route parameters -----
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabKey { get; set; }

    // ----- Hydrated payloads -----
    public CockpitPageHeaderViewModel? PageHeader { get; private set; }
    public CockpitKpiBandViewModel? KpiBand { get; private set; }
    public CockpitTabShellModel TabShell { get; private set; } = new();
    public CockpitData? Data { get; private set; }
    public string? ErrorMessage { get; private set; }

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
        HydratePageHeader();
        HydrateKpiBand();
        HydrateTabShell();

        return Page();
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
}
