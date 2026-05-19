using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.Shared.ControlCenter;
using Abs.FixedAssets.Pages.Shared.Primitives;
using Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;
using Abs.FixedAssets.Services.Navigation.Cockpit;
using Abs.FixedAssets.Services.Receiving;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

// Two namespaces both define an `ExceptionLaneFilter` type:
//   Services.Receiving.ExceptionLaneFilter — service query filter (input to GetExceptionLaneAsync)
//   Pages.Shared.Primitives.ExceptionLaneFilter — UI filter pill on the lane primitive
// Alias both to keep call sites unambiguous.
using SvcLaneFilter = Abs.FixedAssets.Services.Receiving.ExceptionLaneFilter;
using UiLaneFilter = Abs.FixedAssets.Pages.Shared.Primitives.ExceptionLaneFilter;

namespace Abs.FixedAssets.Pages.Receiving;

// Sprint 11 PR #5 — the Receiving Control Center landing page.
// First live Control Center surface; PR #7 will swap /Receiving (Cockpit) to point here.
//
// Composition:
//   PageModel
//    └── VoiceReadyPageModel              (ADR-014 D1)
//         └── ControlCenterPageModel       (ADR-016 D1)
//              └── ReceivingControlCenterModel (this class)
//
// Data sources:
//   - IReceivingControlCenterService.GetKpiStripAsync   → Shell.KpiStrip
//   - IReceivingControlCenterService.GetExceptionLaneAsync → Shell.ExceptionLane
//   - IReceivingControlCenterService.GetActivityFeedAsync  → Shell.ActivityFeed
//   - Shell.Drawer is a profile-aware ContextDrawerModel hydrated server-side
//     when a row is clicked (deferred to PR #6 — the drawer body comes from
//     ADR-015's DynamicFormViewComponent against the selected receipt's profile).
//
// Per ADR-016 D3, voice posture defaults to push-to-talk. The Voice FAB at
// the bottom-right of the shell fires the cherry:voice:state custom event;
// Sprint 5 wires that to the voice-AI runtime + the 10 IReceiptVoiceTools.
[Authorize(Policy = "StockReceipt.View")]
public sealed class ControlCenterModel : ControlCenterPageModel
{
    private readonly IReceivingControlCenterService _receiving;
    private readonly ILogger<ControlCenterModel> _logger;

    public ControlCenterModel(
        IReceivingControlCenterService receiving,
        ILogger<ControlCenterModel> logger)
    {
        _receiving = receiving;
        _logger = logger;
    }

    public override string ControlCenterCode => "RECEIVING";
    public override string ControlCenterTitle => "Receiving Control Center";

    [BindProperty(SupportsGet = true)]
    public string? SiteCode { get; set; }

    // Sprint 12A PR #4 — `?tab=` query string drives which tab renders.
    // Valid keys: po-queue (default) | asn-queue | orphans | exceptions.
    // Unknown values fall back to the default.
    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? TabKey { get; set; }

    // Tab shell model. Hydrated in OnGetAsync; consumed by ControlCenter.cshtml.
    public CockpitTabShellModel TabShell { get; private set; } = new();

    // PO Queue tab payload. Hydrated only when ActiveTab == TabPoQueue —
    // preserves the PR #4 perf win where non-active tabs make zero service
    // calls. Consumed by ControlCenter.cshtml's po-queue switch arm.
    public CockpitShellViewModel? PoQueueShell { get; private set; }

    // Sprint 12A PR #6 — ASN Queue tab payload. Hydrated only when
    // ActiveTab == TabAsnQueue. Same shell pattern as PO Queue but consumes
    // the AdvancedShippingNotice domain entity via GetAsnQueueAsync.
    public CockpitShellViewModel? AsnQueueShell { get; private set; }

    // Sprint 12A PR #7 — Orphan Queue tab payload. Hydrated only when
    // ActiveTab == TabOrphans. Consumes orphan StockReceipts (NULL
    // SourcePoNumber) via GetOrphanQueueAsync. Preview surfaces 0-3
    // AI-ranked candidate POs with per-signal score breakdown.
    public CockpitShellViewModel? OrphanQueueShell { get; private set; }

    // Sprint 12A PR #5.1 — KPI band model. Always hydrated on every tab,
    // rendered above the tab bar. Third leg of the Cockpit canvas per
    // ADR-018 §D3.
    public CockpitKpiBandViewModel? KpiBand { get; private set; }

    // Sprint 12A PR #5.2 — anchored page header (replaces in-band eyebrow),
    // Next Up priority preview (replaces empty welcome state), and AI
    // Suggestions strip (Sprint 5 voice-AI stub).
    public CockpitPageHeaderViewModel? PageHeader { get; private set; }
    public ReceivingNextUpData? NextUp { get; private set; }
    public ReceivingAiSuggestionsData? AiSuggestions { get; private set; }

    public const string TabPoQueue = "po-queue";
    public const string TabAsnQueue = "asn-queue";
    public const string TabOrphans = "orphans";
    public const string TabExceptions = "exceptions";

    private static readonly string[] KnownTabs = { TabPoQueue, TabAsnQueue, TabOrphans, TabExceptions };

    // Resolve the active tab key honoring the `?tab=` param with a default.
    public string ActiveTab =>
        !string.IsNullOrEmpty(TabKey) && KnownTabs.Contains(TabKey, StringComparer.OrdinalIgnoreCase)
            ? TabKey.ToLowerInvariant()
            : TabPoQueue;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        // Common shell header — used by every tab.
        Shell.Eyebrow = "RECEIVING CONTROL CENTER";
        Shell.Headline = "Today's dock";
        Shell.Subtitle = string.IsNullOrEmpty(SiteCode)
            ? "All sites · push-to-talk voice (hold Space)"
            : $"{SiteCode} · push-to-talk voice (hold Space)";
        Shell.VoicePosture = "push-to-talk";
        Shell.ShowVoiceButton = true;

        // -------------------------------------------------------------------
        // Sprint 12A PR #4 — tab shell wiring.
        // The four-tab Cockpit shell per ADR-018 §D2. PO Queue is the default;
        // ASN Queue and Orphans render placeholders until PRs #6 and #7 ship
        // their real cockpit consumers. Exceptions hosts the Sprint 11
        // four-quadrant scaffold via the existing _ControlCenterShell partial.
        // -------------------------------------------------------------------
        TabShell = new CockpitTabShellModel
        {
            ActiveTabKey = ActiveTab,
            BaseRoute = "/Receiving",
            Tabs = new List<CockpitTab>
            {
                new(TabPoQueue,    "PO Queue",    "fas fa-file-invoice",      IsDefault: true),
                new(TabAsnQueue,   "ASN Queue",   "fas fa-truck-fast"),
                new(TabOrphans,    "Orphans",     "fas fa-question-circle"),
                new(TabExceptions, "Exceptions",  "fas fa-triangle-exclamation"),
            },
        };

        // Sprint 12A PR #5.2 — page header (anchored title bar) + KPI band
        // (4-tile hero) + AI suggestions strip are always hydrated. Next Up
        // priority preview only hydrates on the PO Queue tab (the only tab
        // that consumes it). Each is failure-safe.
        HydratePageHeader();
        await HydrateKpiBandAsync(ct);
        await HydrateAiSuggestionsAsync(ct);

        if (string.Equals(ActiveTab, TabExceptions, StringComparison.OrdinalIgnoreCase))
        {
            await HydrateExceptionsTabAsync(ct);
        }
        else if (string.Equals(ActiveTab, TabPoQueue, StringComparison.OrdinalIgnoreCase))
        {
            await HydratePoQueueTabAsync(ct);
            await HydrateNextUpAsync(ct);
        }
        else if (string.Equals(ActiveTab, TabAsnQueue, StringComparison.OrdinalIgnoreCase))
        {
            await HydrateAsnQueueTabAsync(ct);
        }
        else if (string.Equals(ActiveTab, TabOrphans, StringComparison.OrdinalIgnoreCase))
        {
            await HydrateOrphanQueueTabAsync(ct);
        }

        return Page();
    }

    private void HydratePageHeader()
    {
        PageHeader = new CockpitPageHeaderViewModel
        {
            Title           = "Receiving",
            Scope           = string.IsNullOrEmpty(SiteCode) ? "All sites" : SiteCode,
            Subtitle        = "Push-to-talk voice (hold Space)",
            ShowLive        = true,
            RefreshedAtText = "updated just now",
            ShowVoiceButton = true,
            VoiceButtonLabel = "Voice",
        };
    }

    private async Task HydrateNextUpAsync(CancellationToken ct)
    {
        var filter = new ReceivingNextUpFilter { SiteCode = SiteCode };
        var result = await _receiving.GetReceivingNextUpAsync(filter, ct);
        NextUp = result.IsSuccess ? result.Value : new ReceivingNextUpData();

        // Wire NextUp into PoQueueShell as the preroll partial so the right
        // pane lands with the priority preview instead of an empty welcome.
        if (PoQueueShell is not null)
        {
            PoQueueShell = new CockpitShellViewModel
            {
                Queue = PoQueueShell.Queue,
                Welcome = PoQueueShell.Welcome,
                PreviewPartialName = PoQueueShell.PreviewPartialName,
                PreviewPartialModel = PoQueueShell.PreviewPartialModel,
                PreviewBlobJson = PoQueueShell.PreviewBlobJson,
                PreviewBlobElementId = PoQueueShell.PreviewBlobElementId,
                PrerollPartialName = "_CockpitNextUp",
                PrerollPartialModel = NextUp,
            };
        }
    }

    private async Task HydrateAiSuggestionsAsync(CancellationToken ct)
    {
        var filter = new ReceivingAiSuggestionsFilter { SiteCode = SiteCode };
        var result = await _receiving.GetReceivingAiSuggestionsAsync(filter, ct);
        AiSuggestions = result.IsSuccess ? result.Value : new ReceivingAiSuggestionsData();
    }

    // Pulls the 8-tile KPI band data and shapes it into the view-model the
    // _CockpitKpiBand partial consumes. ADR-018 §D3 — third leg of the
    // Cockpit canvas. Failure path returns a minimal informational band so
    // the page header stays present (never NRE in Razor).
    private async Task HydrateKpiBandAsync(CancellationToken ct)
    {
        var bandFilter = new ReceivingKpiBandFilter { SiteCode = SiteCode };
        var bandResult = await _receiving.GetReceivingKpiBandAsync(bandFilter, ct);

        if (bandResult.IsFailure || bandResult.Value is null)
        {
            _logger.LogWarning("GetReceivingKpiBandAsync failed: {Error}", bandResult.Error);
            KpiBand = new CockpitKpiBandViewModel
            {
                Eyebrow = "TODAY'S DOCK",
                RefreshedAtText = "data unavailable",
                ShowLiveIndicator = false,
            };
            return;
        }

        var d = bandResult.Value;
        // PR #5.2 — Hero mode = 4 tiles, no in-band eyebrow (page header owns it).
        // Order: Overdue (the urgent number) · Due Today · Open POs (the headline)
        // · Exceptions Open. Dropped: This Week · Receipts Today · Dock-to-Stock
        // · Doc Completeness — those move to a future Trends dashboard.
        KpiBand = new CockpitKpiBandViewModel
        {
            HeroMode = true,
            ShowLiveIndicator = false,  // page header owns the LIVE chip now
            Tiles = new[]
            {
                BandTile(d.Overdue),
                BandTile(d.DueToday),
                BandTile(d.OpenPos),
                BandTile(d.ExceptionsOpen),
            },
        };
    }

    private static CockpitKpiTileViewModel BandTile(ReceivingKpiTile t) => new()
    {
        Label = t.Label,
        Value = t.Value,
        Unit = t.Unit,
        TargetText = t.TargetText,
        SubText = t.SubText,
        Tone = t.Tone,
        SparkPoints = t.SparkPoints,
        DrillHref = t.DrillHref,
        DrillScroll = t.DrillScroll,
    };

    // Pulls the PO Queue rows + preview blob via
    // IReceivingControlCenterService.GetPoQueueAsync, runs them through the
    // default ByTimeLens, and assembles the CockpitShellViewModel the
    // ControlCenter.cshtml po-queue branch composes from the shared
    // Pages/Shared/Primitives/Cockpit/* partials.
    //
    // The legacy /Receiving/Cockpit-Legacy page hydrated the same data inline.
    // Pixel-identical render is the ADR-018 §D3 promise — the lens, partials,
    // welcome stats, and preview JSON shape are all extracted byte-for-byte
    // from Pages/Receiving/Index.cshtml.
    private async Task HydratePoQueueTabAsync(CancellationToken ct)
    {
        var filter = new PoQueueFilter { SiteCode = SiteCode };
        var queueResult = await _receiving.GetPoQueueAsync(filter, ct);

        if (queueResult.IsFailure || queueResult.Value is null)
        {
            _logger.LogWarning("GetPoQueueAsync failed for site {SiteCode}: {Error}", SiteCode, queueResult.Error);
            PoQueueShell = new CockpitShellViewModel
            {
                Queue = new CockpitQueueViewModel
                {
                    TitleHtml = "PO Queue",
                    TitleIconClass = "fas fa-inbox",
                    SearchPlaceholder = "Search PO#, vendor...",
                    Empty = new CockpitEmptyViewModel
                    {
                        IconClass = "fas fa-triangle-exclamation",
                        IconTone = "warning",
                        Message = queueResult.Error ?? "Queue unavailable.",
                    },
                },
                Welcome = new CockpitWelcomeViewModel
                {
                    IconClass = "fas fa-box-open",
                    Title = "Select a PO to preview",
                    Subtitle = "Click a purchase order from the queue to see line details and begin receiving.",
                },
                PreviewBlobJson = "[]",
            };
            return;
        }

        var data = queueResult.Value;
        var lens = new ByTimeLens<PoQueueRow>();
        var groups = lens.Group(data.Rows);

        // ByTimeLens groups → view-model groups. Rows widen to ICockpitQueueRow
        // since the partials are domain-agnostic.
        var groupVms = groups.Select(g => new CockpitQueueGroupViewModel
        {
            Code = g.Code,
            Label = g.Label,
            Tone = g.Tone,
            IconClass = g.Icon,
            Rows = g.Rows.Cast<ICockpitQueueRow>().ToList(),
        }).ToList();

        // 4-stat welcome strip matches the legacy /Receiving/Cockpit-Legacy
        // counts. ByTimeLens groups are keyed by code so we read each bucket
        // by code; missing buckets read as 0.
        int CountFor(string code) =>
            groups.FirstOrDefault(g => string.Equals(g.Code, code, StringComparison.OrdinalIgnoreCase))?.Rows.Count ?? 0;

        var welcome = new CockpitWelcomeViewModel
        {
            IconClass = "fas fa-box-open",
            Title = "Select a PO to preview",
            Subtitle = "Click a purchase order from the queue to see line details and begin receiving.",
            Stats = new[]
            {
                new CockpitWelcomeStat("Overdue",  CountFor("overdue").ToString(),   "danger"),
                new CockpitWelcomeStat("Due Today", CountFor("today").ToString(),    "warning"),
                new CockpitWelcomeStat("This Week", CountFor("this-week").ToString()),
                new CockpitWelcomeStat("Upcoming", CountFor("later").ToString(),     "muted"),
            },
        };

        PoQueueShell = new CockpitShellViewModel
        {
            Queue = new CockpitQueueViewModel
            {
                TitleHtml = "PO Queue",
                TitleIconClass = "fas fa-inbox",
                CountBadge = 0,    // PR #5.2 — KPI band owns the count now (Open POs tile)
                SearchPlaceholder = "Search PO#, vendor...",
                SearchElementId = "poSearch",
                FilterFunctionName = "filterQueue",
                SelectFunctionName = "selectPO",
                Groups = groupVms,
                Empty = data.Rows.Count == 0
                    ? new CockpitEmptyViewModel { IconClass = "fas fa-check-circle", IconTone = "success", Message = "All caught up!" }
                    : null,
            },
            Welcome = welcome,
            PreviewPartialName = "_CockpitPoQueuePreview",
            PreviewPartialModel = null,
            PreviewBlobJson = CockpitPreviewSerializer.SerializeMany(data.Previews),
            PreviewBlobElementId = "__poDetails",
            // PR #5.2 — preroll renders the "Next Up" priority pane on first
            // paint instead of an empty welcome state. NextUp is hydrated
            // in HydrateNextUpAsync (called after HydratePoQueueTabAsync).
        };
    }

    // Sprint 12A PR #6 — hydrates the ASN Queue tab.
    //
    // Pulls the AdvancedShippingNotice queue via GetAsnQueueAsync, runs it
    // through the default ByTimeLens, and assembles a CockpitShellViewModel
    // that the /Receiving ASN Queue tab consumes. Mirrors the PO Queue tab
    // structure with ASN-specific naming + SelectFunctionName = "selectAsn"
    // so cockpit.js routes to the ASN preview hydrator.
    private async Task HydrateAsnQueueTabAsync(CancellationToken ct)
    {
        var filter = new AsnQueueFilter { SiteCode = SiteCode };
        var queueResult = await _receiving.GetAsnQueueAsync(filter, ct);

        if (queueResult.IsFailure || queueResult.Value is null)
        {
            _logger.LogWarning("GetAsnQueueAsync failed for site {SiteCode}: {Error}", SiteCode, queueResult.Error);
            AsnQueueShell = new CockpitShellViewModel
            {
                Queue = new CockpitQueueViewModel
                {
                    TitleHtml = "ASN Queue",
                    TitleIconClass = "fas fa-truck-fast",
                    SearchPlaceholder = "Search ASN#, vendor...",
                    Empty = new CockpitEmptyViewModel
                    {
                        IconClass = "fas fa-triangle-exclamation",
                        IconTone = "warning",
                        Message = queueResult.Error ?? "ASN queue unavailable.",
                    },
                },
                Welcome = new CockpitWelcomeViewModel
                {
                    IconClass = "fas fa-truck-fast",
                    Title = "Select an ASN to preview",
                    Subtitle = "Click an ASN from the queue to see the manifest and start receiving.",
                },
                PreviewBlobJson = "[]",
                PreviewBlobElementId = "__asnDetails",
            };
            return;
        }

        var data = queueResult.Value;
        var lens = new ByTimeLens<AsnQueueRow>();
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
            IconClass = "fas fa-truck-fast",
            Title = "Select an ASN to preview",
            Subtitle = "Click an ASN from the queue to see the manifest and start receiving by scanning the ASN barcode.",
            Stats = new[]
            {
                new CockpitWelcomeStat("Late",       CountFor("overdue").ToString(),   "danger"),
                new CockpitWelcomeStat("Today",      CountFor("today").ToString(),     "warning"),
                new CockpitWelcomeStat("This Week",  CountFor("this-week").ToString()),
                new CockpitWelcomeStat("Upcoming",   CountFor("later").ToString(),     "muted"),
            },
        };

        AsnQueueShell = new CockpitShellViewModel
        {
            Queue = new CockpitQueueViewModel
            {
                TitleHtml = "ASN Queue",
                TitleIconClass = "fas fa-truck-fast",
                CountBadge = 0,
                SearchPlaceholder = "Search ASN#, vendor...",
                SearchElementId = "asnSearch",
                FilterFunctionName = "filterQueue",
                SelectFunctionName = "selectAsn",
                Groups = groupVms,
                Empty = data.Rows.Count == 0
                    ? new CockpitEmptyViewModel { IconClass = "fas fa-check-circle", IconTone = "success", Message = "No inbound shipments — dock is clear." }
                    : null,
            },
            Welcome = welcome,
            PreviewPartialName = "_CockpitAsnQueuePreview",
            PreviewPartialModel = null,
            PreviewBlobJson = CockpitPreviewSerializer.SerializeMany(data.Previews),
            PreviewBlobElementId = "__asnDetails",
        };
    }

    // Sprint 12A PR #7 — Orphan Queue tab.
    //
    // Pulls orphan StockReceipts (NULL SourcePoNumber) via GetOrphanQueueAsync,
    // groups them through ByTimeLens (bucketing by ReceivedAt — older = more
    // urgent), and assembles a CockpitShellViewModel. Preview blob carries the
    // 0-3 AI-suggested candidate POs per orphan; cockpit.js#selectOrphan renders
    // the candidate panel with per-signal score chips + per-candidate Match CTAs.
    private async Task HydrateOrphanQueueTabAsync(CancellationToken ct)
    {
        var filter = new OrphanQueueFilter { SiteCode = SiteCode };
        var queueResult = await _receiving.GetOrphanQueueAsync(filter, ct);

        if (queueResult.IsFailure || queueResult.Value is null)
        {
            _logger.LogWarning("GetOrphanQueueAsync failed for site {SiteCode}: {Error}", SiteCode, queueResult.Error);
            OrphanQueueShell = new CockpitShellViewModel
            {
                Queue = new CockpitQueueViewModel
                {
                    TitleHtml = "Orphans",
                    TitleIconClass = "fas fa-question-circle",
                    SearchPlaceholder = "Search receipt#, part#...",
                    Empty = new CockpitEmptyViewModel
                    {
                        IconClass = "fas fa-triangle-exclamation",
                        IconTone = "warning",
                        Message = queueResult.Error ?? "Orphan queue unavailable.",
                    },
                },
                Welcome = new CockpitWelcomeViewModel
                {
                    IconClass = "fas fa-question-circle",
                    Title = "Select an orphan to match",
                    Subtitle = "Click a receipt to see AI-suggested matching POs.",
                },
                PreviewBlobJson = "[]",
                PreviewBlobElementId = "__orphanDetails",
            };
            return;
        }

        var data = queueResult.Value;
        var lens = new ByTimeLens<OrphanQueueRow>();
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

        // Count orphans with at least one candidate suggested by the AI.
        var withCandidates = data.Previews.Count(p => p.Candidates.Count > 0);

        var welcome = new CockpitWelcomeViewModel
        {
            IconClass = "fas fa-question-circle",
            Title = "Select an orphan to match",
            Subtitle = "Click a receipt to see AI-suggested matching POs ranked by item, vendor, and recency.",
            Stats = new[]
            {
                new CockpitWelcomeStat("Orphans",        data.Rows.Count.ToString(), "warning"),
                new CockpitWelcomeStat("AI-matchable",   withCandidates.ToString(),  "success"),
                new CockpitWelcomeStat("Aged 10d+",      CountFor("overdue").ToString(), "danger"),
                new CockpitWelcomeStat("This Week",      CountFor("this-week").ToString()),
            },
        };

        OrphanQueueShell = new CockpitShellViewModel
        {
            Queue = new CockpitQueueViewModel
            {
                TitleHtml = "Orphans",
                TitleIconClass = "fas fa-question-circle",
                CountBadge = 0,
                SearchPlaceholder = "Search receipt#, part#...",
                SearchElementId = "orphanSearch",
                FilterFunctionName = "filterQueue",
                SelectFunctionName = "selectOrphan",
                Groups = groupVms,
                Empty = data.Rows.Count == 0
                    ? new CockpitEmptyViewModel { IconClass = "fas fa-check-circle", IconTone = "success", Message = "No orphan receipts — every arrival is tied to a PO." }
                    : null,
            },
            Welcome = welcome,
            PreviewPartialName = "_CockpitOrphanPreview",
            PreviewPartialModel = null,
            PreviewBlobJson = CockpitPreviewSerializer.SerializeMany(data.Previews),
            PreviewBlobElementId = "__orphanDetails",
        };
    }

    // Pulls KPI strip + exception lane + activity feed + drawer placeholder
    // into Shell. This is the Sprint 11 four-quadrant scaffold's data, now
    // scoped to the Exceptions sub-tab per ADR-018 §D8.
    private async Task HydrateExceptionsTabAsync(CancellationToken ct)
    {
        var kpiFilter = new KpiStripFilter
        {
            SiteCode = SiteCode,
            From = DateTime.UtcNow.AddDays(-14),
            To = DateTime.UtcNow,
        };
        var kpiResult = await _receiving.GetKpiStripAsync(kpiFilter, ct);
        Shell.KpiStrip = BuildKpiStrip(kpiResult);

        var laneFilter = new SvcLaneFilter
        {
            SiteCode = SiteCode,
            AiPrioritized = true,
            Take = 25,
        };
        var laneResult = await _receiving.GetExceptionLaneAsync(laneFilter, ct);
        Shell.ExceptionLane = BuildExceptionLane(laneResult);

        var feedFilter = new ActivityFeedFilter
        {
            SiteCode = SiteCode,
            SinceSequence = 0,
            Take = 30,
        };
        var feedResult = await _receiving.GetActivityFeedAsync(feedFilter, ct);
        Shell.ActivityFeed = BuildActivityFeed(feedResult);

        Shell.Drawer = new ContextDrawerModel
        {
            Id = "receiving-cc-drawer",
            Title = "Receipt detail",
            Subtitle = "Select a row to see attributes",
            Width = 480,
            BodyHtml = "<div style=\"padding: 24px; color: var(--text-muted); font-size: 13px;\">" +
                       "Click any row in the Exception Lane to open the profile-aware detail drawer. " +
                       "Each profile (STEEL · PHARMA · FOOD · ...) renders its own field set via ADR-015's " +
                       "DynamicFormViewComponent. PR #6 wires the live row-to-drawer flow." +
                       "</div>",
        };
    }

    public override VoiceContextPayload BuildContextPayload()
    {
        var baseCtx = base.BuildContextPayload();
        return new VoiceContextPayload
        {
            Route = baseCtx.Route,
            UserId = baseCtx.UserId,
            Roles = baseCtx.Roles,
            TenantId = baseCtx.TenantId,
            EntityType = "ControlCenter.Receiving",
            EntityId = SiteCode ?? "ALL",
            RelatedIds = Shell.ExceptionLane?.Rows?.Select(r => r.Id).Where(id => !string.IsNullOrEmpty(id)).ToArray() ?? Array.Empty<string>(),
            FocusedField = baseCtx.FocusedField,
            // Per ADR-014 D7 / ADR-018 §D7 — voice scope tracks the active tab
            // so commands like "show me overdue receipts" target the right canvas.
            Tab = ActiveTab,
            BuiltAt = baseCtx.BuiltAt,
        };
    }

    // =====================================================================
    // BUILDERS
    // =====================================================================

    private static KpiStripModel BuildKpiStrip(Result<KpiStripSnapshot> result)
    {
        if (result.IsFailure || result.Value is null)
        {
            return new KpiStripModel
            {
                Eyebrow = "Today's receiving",
                RangeText = "Last 14 days",
                Tiles = new List<KpiTileModel>(),
            };
        }
        var s = result.Value;
        return new KpiStripModel
        {
            Eyebrow = "Today's receiving",
            RangeText = "Last 14 days",
            Tiles = new List<KpiTileModel>
            {
                TileFromSnapshot(s.DockToStock),
                TileFromSnapshot(s.Accuracy),
                TileFromSnapshot(s.OpenExceptions),
                TileFromSnapshot(s.DocCompleteness),
                TileFromSnapshot(s.SupplierOnTime),
                TileFromSnapshot(s.QuarantineCycle),
                TileFromSnapshot(s.AsnPenetration),
                TileFromSnapshot(s.VoiceAdoption),
            },
        };
    }

    private static KpiTileModel TileFromSnapshot(KpiTileSnapshot s) => new()
    {
        Label = s.Label,
        Value = s.Value,
        Unit = s.Unit,
        DeltaText = s.DeltaText,
        DeltaDirection = s.DeltaDirection,
        SparkPoints = s.SparkPoints is { Length: > 0 } ? s.SparkPoints : null,
        SparkTone = s.SparkTone,
    };

    private static ExceptionLaneModel BuildExceptionLane(Result<ExceptionLanePage> result)
    {
        if (result.IsFailure || result.Value is null)
        {
            return new ExceptionLaneModel
            {
                Title = "Exceptions",
                CountText = "service unavailable",
                Rows = new List<ExceptionRowModel>(),
                EmptyMessage = result.Error ?? "Lane unavailable.",
                EmptyIcon = "wrench",
            };
        }
        var page = result.Value;
        return new ExceptionLaneModel
        {
            Title = "Exceptions",
            CountText = $"{page.TotalCount} open · ranked by AI",
            Filters = BuildLaneFilters(page),
            Rows = page.Items.Select(i => new ExceptionRowModel
            {
                Id = i.ReceiptId.ToString(),
                Eyebrow = string.IsNullOrEmpty(i.PoNumber)
                    ? i.ReceiptNumber
                    : $"{i.ReceiptNumber} · {i.PoNumber}",
                Headline = i.Headline,
                Subtext = i.Subtext,
                Severity = i.Severity,
                StatusPill = new StatusPillModel
                {
                    Label = i.Kind.ToUpperInvariant(),
                    Tone = MapSeverityToTone(i.Severity),
                    ShowDot = true,
                },
                SlaText = FormatSla(i.SlaRemaining),
                SlaTone = i.SlaTone,
                AiPriority = i.AiPriority,
                HasAiSuggestion = i.HasAiSuggestion,
                OpenDrawerId = "receiving-cc-drawer",
            }).ToList(),
            EmptyMessage = "All clear — no exceptions waiting.",
            EmptyIcon = "sparkle",
        };
    }

    private static List<UiLaneFilter> BuildLaneFilters(ExceptionLanePage page)
    {
        // The page model exposes only severity + kind. We surface the
        // distinct kinds + a total. v1 keeps it simple; v2 wires deep-links.
        var byKind = page.Items.GroupBy(i => i.Kind).ToDictionary(g => g.Key, g => g.Count());
        var result = new List<UiLaneFilter>
        {
            new() { Label = "All", Active = true, Count = page.TotalCount },
        };
        foreach (var (kind, count) in byKind.OrderByDescending(kv => kv.Value))
        {
            result.Add(new UiLaneFilter { Label = char.ToUpper(kind[0]) + kind.Substring(1), Count = count });
        }
        return result;
    }

    private static ActivityFeedModel BuildActivityFeed(Result<ActivityFeedDelta> result)
    {
        if (result.IsFailure || result.Value is null)
        {
            return new ActivityFeedModel
            {
                Title = "Activity",
                CollapsedByDefault = false,
                Entries = new List<ActivityEntryModel>(),
                EmptyMessage = "Quiet here.",
            };
        }
        var delta = result.Value;
        return new ActivityFeedModel
        {
            Title = "Activity",
            CollapsedByDefault = false,
            Entries = delta.Entries.Select(e => new ActivityEntryModel
            {
                Id = e.Sequence.ToString(),
                Timestamp = FormatRelative(e.OccurredAtUtc),
                ActorKind = e.ActorKind,
                ActorName = e.ActorName,
                Verb = e.Verb,
                TargetRef = e.TargetRef,
                Snippet = e.Snippet,
            }).ToList(),
            EmptyMessage = "No activity in the last hour.",
        };
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private static string MapSeverityToTone(string severity) => severity switch
    {
        "critical" => "danger",
        "warning"  => "warning",
        "info"     => "info",
        "success"  => "success",
        _          => "neutral",
    };

    private static string? FormatSla(TimeSpan? remaining)
    {
        if (remaining is null) return null;
        var s = remaining.Value;
        if (s.TotalMinutes < 60) return $"{(int)s.TotalMinutes}m";
        if (s.TotalHours < 24)   return $"{(int)s.TotalHours}h {s.Minutes}m";
        return $"{(int)s.TotalDays}d";
    }

    private static string FormatRelative(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1)  return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24)   return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }
}
