using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.Shared.ControlCenter;
using Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;
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
//   - PR #1 (THIS): route + shell + 4 tabs + placeholder KPIs + NavRegistry
//                   entry under Finance group.
//   - PR #2:        Source-to-GL drilldown service. Walks JournalLine →
//                   AccountingKey → SourceDocument → upstream via the AGE
//                   substrate from Sprint 12D. Hydrates the Drilldown tab.
//   - PR #3:        Voice intent `WhyIsNbv` + LLM narration on the Cherry Bar
//                   that drives the Drilldown chain when the controller asks
//                   "why is NBV $X on Asset Y?".
//   - PR #4:        KPI band real-data wire-up: Cash position · AR aging
//                   · AP aging · open POs · WIP · unrealized gains.
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

    public IndexModel(ILogger<IndexModel> logger)
    {
        _logger = logger;
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

    public Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        HydratePageHeader();
        HydrateKpiBand();
        HydrateTabShell();
        HydrateTabContent();

        return Task.FromResult<IActionResult>(Page());
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

    private void HydrateKpiBand()
    {
        // PR #1 — 4 placeholder hero tiles. Real numbers ship in PR #4 via a
        // dedicated Finance KPI read-service. Tone stays "neutral" so the
        // band reads as informational (work in progress) rather than
        // alarming (red/amber).
        KpiBand = new CockpitKpiBandViewModel
        {
            HeroMode          = true,
            ShowLiveIndicator = false, // page header owns the LIVE chip
            Tiles = new[]
            {
                new CockpitKpiTileViewModel
                {
                    Label   = "Cash position",
                    Value   = "—",
                    SubText = "PR #4 wire-up",
                    Tone    = "neutral",
                },
                new CockpitKpiTileViewModel
                {
                    Label   = "AR aging > 30d",
                    Value   = "—",
                    SubText = "PR #4 wire-up",
                    Tone    = "neutral",
                },
                new CockpitKpiTileViewModel
                {
                    Label   = "AP due this week",
                    Value   = "—",
                    SubText = "PR #4 wire-up",
                    Tone    = "neutral",
                },
                new CockpitKpiTileViewModel
                {
                    Label   = "WIP balance",
                    Value   = "—",
                    SubText = "PR #4 wire-up",
                    Tone    = "neutral",
                },
            },
        };
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

    private void HydrateTabContent()
    {
        // Every tab renders a CockpitShellViewModel — queue (left) + welcome
        // (right) — so the canvas matches Receiving's BIC composition. PR #1
        // ships empty queues + descriptive welcome states that name which
        // future PR fills the data.
        TabContent = ActiveTab switch
        {
            TabDrilldown  => DrilldownTab(),
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

    private static CockpitShellViewModel DrilldownTab() => new()
    {
        Queue = new CockpitQueueViewModel
        {
            TitleHtml         = "Trace by entity",
            TitleIconClass    = "fas fa-route",
            SearchPlaceholder = "Asset #, JE#, PO#, Invoice#...",
            SearchElementId   = "drilldownSearch",
            Empty = new CockpitEmptyViewModel
            {
                IconClass = "fas fa-route",
                IconTone  = "info",
                Message   = "Source-to-GL drilldown service wires in via PR #2.",
            },
        },
        Welcome = new CockpitWelcomeViewModel
        {
            IconClass = "fas fa-route",
            Title     = "Source-to-GL traceability",
            Subtitle  = "Type an Asset #, JE #, PO #, or Invoice # to walk the chain — JournalLine → AccountingKey → SourceDocument → upstream. Voice intent ('why is NBV $X on Asset #Y?') lands in PR #3 on the Cherry Bar.",
        },
        PreviewBlobJson      = "[]",
        PreviewBlobElementId = "__drilldownDetails",
    };

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
            // PR #3 (voice intent WhyIsNbv) will read this to scope the
            // narration to the active tab — e.g. on Drilldown the voice
            // payload should propose chain-walks, not period-close prompts.
            Tab          = ActiveTab,
            BuiltAt      = baseCtx.BuiltAt,
        };
    }
}
