using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.Shared.ControlCenter;
using Abs.FixedAssets.Pages.Shared.Primitives;
using Abs.FixedAssets.Pages.Shared.Primitives.Cockpit;
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

        // Only the Exceptions tab needs the heavyweight Shell hydration (KPI
        // strip + exception lane + activity feed). Other tabs render
        // placeholders so the page-load cost stays minimal until the cockpit
        // consumers wire in PRs #5–#7.
        if (string.Equals(ActiveTab, TabExceptions, StringComparison.OrdinalIgnoreCase))
        {
            await HydrateExceptionsTabAsync(ct);
        }

        return Page();
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
