using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.Shared.ControlCenter;
using Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;
using Abs.FixedAssets.Services.Controller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Controller;

// Sprint 12.7 PR #1 — Controller Control Center shell (the CFO motion).
//
// First v1 Control Center on the FINANCE side of the spine. Composes all
// four Cockpit primitives per Lock 3:
//
//   - _CockpitPageHeader  : anchored title bar with LIVE + Voice button
//   - _CockpitKpiBand     : 4-tile hero (Cash · AR · AP · WIP) — placeholder
//                           values this PR; PR #4 wires the real numbers
//   - _CockpitTabShell    : 4 tabs — Books / Drilldown / Close Prep / Audit Trail
//   - _CockpitShell       : per-tab queue+preview canvas, welcome state only
//                           this PR
//
// Sprint 12.7 PR boundaries:
//
//   - PR #1 (shipped 5176a1b): route + shell + 4 tabs + placeholder KPIs +
//                   NavRegistry entry under Finance group.
//   - PR #2 (shipped 614a738): Source-to-GL drilldown service. Walks
//                   Asset → CipCapitalization → CipProject → CipCosts and
//                   JE → reverse-CIP-origin → lines. Hydrates the Drilldown
//                   tab via _CockpitChainTrace partial.
//   - PR #3 (shipped aad2590): Voice intent `ExplainChainTrace` on the Cherry
//                   Bar — the CFO motion. Push-to-talk transcripts like "why
//                   is NBV on asset 4231" / "drill down on JE 47" route
//                   through IntentClassifier → IControllerCockpitService.TraceAsync,
//                   then ChainStep.Narration strings narrate aloud via TTS.
//   - PR #4 (THIS): KPI band real-data wire-up via IFinanceKpiService:
//                   Cash position · AP due this week · Open POs · WIP balance.
//                   AR aging + unrealized FX gains from the original PR boundary
//                   doc are deferred — they need CustomerInvoice + FX
//                   revaluation engine not in IndustryOS yet (honest scope per
//                   the Codex P1 pattern).
//   - PR #5:        Demo data + walkthrough page + Republish-with-Copy box.
//
// Demo target: CFO Paul Marcotte (ABS Machining). Headline question:
//   "Why is NBV $1.2M on Asset #4231?"
// Cherry walks JournalLine → AccountingKey → DepreciationRun → AssetBasis →
// CapitalProject → WO → PO → Invoice, narrated naturally, every step
// clickable. PRA-5g (merged `13c05b7`) provided the depreciation half of
// the prerequisite — every depreciation JournalLine now stamps
// AccountingKeyId, making the chain queryable end-to-end.
//
// Lock 15 compliance: no DbContext mutation in this PageModel and no
// IService injection beyond ILogger this PR. PR #2 adds
// IControllerCockpitService for the drilldown reads; KPI hydration follows
// in PR #4 via dedicated Finance read-services.
//
// Lock 1 compliance: user-facing text reads "Controller Control Center";
// "Cockpit" appears only in code-internal comments referencing the
// primitive composition pattern.
[Authorize]
public sealed class IndexModel : ControlCenterPageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IControllerCockpitService _drilldown;
    private readonly IFinanceKpiService _financeKpi;

    public IndexModel(
        ILogger<IndexModel> logger,
        IControllerCockpitService drilldown,
        IFinanceKpiService financeKpi)
    {
        _logger = logger;
        _drilldown = drilldown;
        _financeKpi = financeKpi;
    }

    public override string ControlCenterCode  => "CONTROLLER";
    public override string ControlCenterTitle => "Controller Control Center";

    // Tab keys — kept const so switch arms + voice context payload stay
    // string-typo-proof.
    public const string TabBooks      = "books";
    public const string TabDrilldown  = "drilldown";
    public const string TabClosePrep  = "close-prep";
    public const string TabAuditTrail = "audit-trail";

    private static readonly string[] KnownTabs =
        { TabBooks, TabDrilldown, TabClosePrep, TabAuditTrail };

    // `?tab=<key>` drives which tab renders. Unknown values fall back to the
    // default (books).
    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabKey { get; set; }

    // Sprint 12.7 PR #2 — `?q=<entity-ref>` drives the source-to-GL drilldown.
    // Format: ASSET-1234 / JE-1234 / bare integer (assumed Asset). Only
    // active when ActiveTab == TabDrilldown.
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    // Sprint 12.7 PR #2 — chain-trace result hydrated from
    // IControllerCockpitService when Query is non-empty on the Drilldown tab.
    // Razor consumes this via the _CockpitChainTrace partial as the
    // CockpitShell preroll.
    public ChainTraceResult? DrilldownResult { get; private set; }

    public string ActiveTab =>
        !string.IsNullOrEmpty(TabKey)
        && KnownTabs.Contains(TabKey, StringComparer.OrdinalIgnoreCase)
            ? TabKey.ToLowerInvariant()
            : TabBooks;

    // Hydrated in OnGetAsync. Consumed by Index.cshtml.
    public CockpitPageHeaderViewModel PageHeader { get; private set; } = new();
    public CockpitKpiBandViewModel    KpiBand    { get; private set; } = new();
    public CockpitTabShellModel       TabShell   { get; private set; } = new();
    public CockpitShellViewModel      TabContent { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        HydratePageHeader();
        await HydrateKpiBandAsync(ct);
        HydrateTabShell();
        await HydrateTabContentAsync(ct);

        return Page();
    }

    private void HydratePageHeader()
    {
        PageHeader = new CockpitPageHeaderViewModel
        {
            Title            = "Controller",
            Scope            = "All books",
            Subtitle         = "Source-to-GL traceability · push-to-talk voice (hold Space)",
            ShowLive         = true,
            RefreshedAtText  = "wiring up — real data lands PR #4",
            ShowVoiceButton  = true,
            VoiceButtonLabel = "Ask Cherry",
        };
    }

    private async Task HydrateKpiBandAsync(CancellationToken ct)
    {
        // Sprint 12.7 PR #4 — real-data wire-up via IFinanceKpiService.
        // PR #1 shipped the 4 placeholder tiles; this PR replaces them with
        // live values from JournalLines / VendorInvoices / PurchaseOrders /
        // CipProjects.
        //
        // Tile selection (Hero mode = 4 wide):
        //   1. Cash position       — GL sum across CashAndReceivables accounts
        //   2. AP due this week    — VendorInvoice outstanding ≤ +7d
        //   3. Open POs            — PurchaseOrder Status IN (Approved/Sent/Partial)
        //   4. WIP balance         — CipProject.TotalCosts where Status=Active
        //
        // AR aging + Unrealized FX gains from the original PR #4 boundary
        // doc are deferred — they need a CustomerInvoice entity + FX
        // revaluation engine that don't exist yet. Honest scope per the
        // Codex P1 / "honest answer" pattern established in PR #346.
        //
        // Tenant scoping — read CompanyId from the user's tenant_id claim
        // when present. Falls back to null (cross-tenant aggregate) for
        // unauthenticated / admin scenarios. The service itself filters
        // each query through CompanyId when it has one.
        var companyId = ResolveCompanyIdFromClaims();

        FinanceKpiBand bandData;
        try
        {
            bandData = await _financeKpi.GetBandAsync(companyId, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FinanceKpiService.GetBandAsync failed for CompanyId={CompanyId}", companyId);
            KpiBand = new CockpitKpiBandViewModel
            {
                HeroMode          = true,
                ShowLiveIndicator = false,
                Tiles = new[]
                {
                    UnavailableTile("Cash position"),
                    UnavailableTile("AP due this week"),
                    UnavailableTile("Open POs"),
                    UnavailableTile("WIP balance"),
                },
            };
            return;
        }

        KpiBand = new CockpitKpiBandViewModel
        {
            HeroMode          = true,
            ShowLiveIndicator = false, // page header owns the LIVE chip
            Tiles = new[]
            {
                ToTileViewModel(bandData.CashPosition),
                ToTileViewModel(bandData.ApDueThisWeek),
                ToTileViewModel(bandData.OpenPos),
                ToTileViewModel(bandData.WipBalance),
            },
        };
    }

    /// <summary>
    /// Map a service-tier <see cref="FinanceKpiTile"/> into the band
    /// primitive's view-model. Keeps the service domain-agnostic
    /// (no Razor / view-model dependency) and the page model thin.
    /// </summary>
    private static CockpitKpiTileViewModel ToTileViewModel(FinanceKpiTile tile) => new()
    {
        Label   = tile.Label,
        Value   = tile.Value,
        SubText = tile.SubText,
        Tone    = tile.Tone,
    };

    /// <summary>
    /// Standard "data unavailable" placeholder used when the whole band
    /// query path throws. Keeps the band renderable on the page so the
    /// header + tabs still appear instead of NRE-ing in Razor.
    /// </summary>
    private static CockpitKpiTileViewModel UnavailableTile(string label) => new()
    {
        Label   = label,
        Value   = "—",
        SubText = "data unavailable",
        Tone    = "neutral",
    };

    /// <summary>
    /// Resolve the active CompanyId from the signed-in user's
    /// <c>tenant_id</c> claim. Returns NULL when no claim is present or
    /// the value isn't parseable — the service interprets NULL as
    /// "aggregate across all companies" (admin / system view).
    /// </summary>
    private int? ResolveCompanyIdFromClaims()
    {
        var raw = User?.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    private void HydrateTabShell()
    {
        TabShell = new CockpitTabShellModel
        {
            ActiveTabKey = ActiveTab,
            BaseRoute    = "/Controller",
            Tabs = new List<CockpitTab>
            {
                new(TabBooks,      "Books",       "fas fa-book",            IsDefault: true),
                new(TabDrilldown,  "Drilldown",   "fas fa-route"),
                new(TabClosePrep,  "Close Prep",  "fas fa-calendar-check"),
                new(TabAuditTrail, "Audit Trail", "fas fa-clipboard-list"),
            },
        };
    }

    private async Task HydrateTabContentAsync(CancellationToken ct)
    {
        // Every tab renders a CockpitShellViewModel — queue (left) + welcome
        // (right) — so the canvas matches Receiving's BIC composition. PR #2
        // wires the Drilldown tab to IControllerCockpitService for the
        // source-to-GL chain trace; other tabs still ship welcome states
        // pointing at the PR that fills them.
        TabContent = ActiveTab switch
        {
            TabDrilldown  => await DrilldownTabAsync(ct),
            TabClosePrep  => ClosePrepTab(),
            TabAuditTrail => AuditTrailTab(),
            _             => BooksTab(),
        };
    }

    private static CockpitShellViewModel BooksTab() => new()
    {
        Queue = new CockpitQueueViewModel
        {
            TitleHtml         = "Recent JEs",
            TitleIconClass    = "fas fa-pen-to-square",
            SearchPlaceholder = "Search JE#, account, source...",
            SearchElementId   = "jeSearch",
            Empty = new CockpitEmptyViewModel
            {
                IconClass = "fas fa-circle-info",
                IconTone  = "info",
                Message   = "Recent JE feed wires in via PR #4.",
            },
        },
        Welcome = new CockpitWelcomeViewModel
        {
            IconClass = "fas fa-book",
            Title     = "Today's books",
            Subtitle  = "The KPI band above shows the headline metrics. Use Drilldown when you need to ask 'why is NBV $X on Asset Y?' or trace any number back to its sources. Close Prep + Audit Trail pull the rest together.",
        },
        PreviewBlobJson      = "[]",
        PreviewBlobElementId = "__jeDetails",
    };

    private async Task<CockpitShellViewModel> DrilldownTabAsync(CancellationToken ct)
    {
        // Sprint 12.7 PR #2 — when ?q= is supplied, walk the chain via
        // IControllerCockpitService and render the trace as the right-pane
        // preroll. When ?q= is empty, the partial renders the friendly
        // "Type an Asset #..." help state — same partial, IsResolved=false
        // for the empty path keeps the surface consistent.
        DrilldownResult = string.IsNullOrWhiteSpace(Query)
            ? null
            : await _drilldown.TraceAsync(Query, ct);

        // Expose the current query to the partial so the search form can
        // pre-populate its input. ViewData propagation works automatically
        // through PartialAsync calls.
        ViewData["DrillQuery"] = Query ?? "";

        return new CockpitShellViewModel
        {
            Queue = new CockpitQueueViewModel
            {
                TitleHtml         = "Trace by entity",
                TitleIconClass    = "fas fa-route",
                SearchPlaceholder = "Asset #, JE#, PO#, Invoice#...",
                SearchElementId   = "drilldownSearch",
                Empty = new CockpitEmptyViewModel
                {
                    IconClass = "fas fa-circle-info",
                    IconTone  = "info",
                    Message   = "Drill via the search box on the right pane. Recent traces queue ships in PR #3.",
                },
            },
            Welcome = new CockpitWelcomeViewModel
            {
                IconClass = "fas fa-route",
                Title     = "Source-to-GL traceability",
                Subtitle  = "Type an Asset #, JE #, PO #, or Invoice # in the search box above to walk the chain. Voice intent ('why is NBV $X on Asset #Y?') lands in PR #3 on the Cherry Bar.",
            },
            PreviewBlobJson      = "[]",
            PreviewBlobElementId = "__drilldownDetails",
            // PR #2 — right pane preroll renders the chain trace partial.
            // The partial handles both the search form (always visible) and
            // the resolved/unresolved trace below it.
            PrerollPartialName  = "Primitives/Cockpit/_CockpitChainTrace",
            PrerollPartialModel = DrilldownResult,
        };
    }

    private static CockpitShellViewModel ClosePrepTab() => new()
    {
        Queue = new CockpitQueueViewModel
        {
            TitleHtml         = "Open periods",
            TitleIconClass    = "fas fa-calendar-check",
            SearchPlaceholder = "Period, status...",
            SearchElementId   = "periodSearch",
            Empty = new CockpitEmptyViewModel
            {
                IconClass = "fas fa-calendar-check",
                IconTone  = "info",
                Message   = "Open period feed wires in via PR #4.",
            },
        },
        Welcome = new CockpitWelcomeViewModel
        {
            IconClass = "fas fa-calendar-check",
            Title     = "Period close staging",
            Subtitle  = "Open periods, reconciliations pending, and close-blocking JEs surface here. The /Periods page already runs the close orchestration; this tab pulls its status into the Controller view.",
        },
        PreviewBlobJson      = "[]",
        PreviewBlobElementId = "__periodDetails",
    };

    private static CockpitShellViewModel AuditTrailTab() => new()
    {
        Queue = new CockpitQueueViewModel
        {
            TitleHtml         = "Recent activity",
            TitleIconClass    = "fas fa-clipboard-list",
            SearchPlaceholder = "Actor, action, entity...",
            SearchElementId   = "auditSearch",
            Empty = new CockpitEmptyViewModel
            {
                IconClass = "fas fa-clipboard-list",
                IconTone  = "info",
                Message   = "Audit trail feed wires in via PR #4.",
            },
        },
        Welcome = new CockpitWelcomeViewModel
        {
            IconClass = "fas fa-clipboard-list",
            Title     = "Audit trail",
            Subtitle  = "Recent financial transactions across every module — AP, Receiving, CIP, depreciation, period close — with actor + timestamp + entity. The full audit log lives at /Admin/AuditLog.",
        },
        PreviewBlobJson      = "[]",
        PreviewBlobElementId = "__auditDetails",
    };

    public override VoiceContextPayload BuildContextPayload()
    {
        var baseCtx = base.BuildContextPayload();
        return new VoiceContextPayload
        {
            Route        = baseCtx.Route,
            UserId       = baseCtx.UserId,
            Roles        = baseCtx.Roles,
            TenantId     = baseCtx.TenantId,
            EntityType   = "ControlCenter.Controller",
            EntityId     = ControlCenterCode,
            RelatedIds   = baseCtx.RelatedIds,
            FocusedField = baseCtx.FocusedField,
            // PR #3 (voice intent ExplainChainTrace, shipped) reads this
            // to scope the narration to the active tab — e.g. on Drilldown
            // the voice payload should propose chain-walks, not period-close
            // prompts. The VoiceInvokeEndpoint receives this on every POST.
            Tab          = ActiveTab,
            BuiltAt      = baseCtx.BuiltAt,
        };
    }
}
