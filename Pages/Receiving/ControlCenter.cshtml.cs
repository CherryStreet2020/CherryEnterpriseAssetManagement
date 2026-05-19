using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Pages.Shared.ControlCenter;
using Abs.FixedAssets.Pages.Shared.Primitives;
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

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Shell.Eyebrow = "RECEIVING CONTROL CENTER";
        Shell.Headline = "Today's dock";
        Shell.Subtitle = string.IsNullOrEmpty(SiteCode)
            ? "All sites · push-to-talk voice (hold Space)"
            : $"{SiteCode} · push-to-talk voice (hold Space)";
        Shell.VoicePosture = "push-to-talk";
        Shell.ShowVoiceButton = true;

        // KPI strip — real EF-backed snapshot for the period. The service
        // method is forgiving: if no receipts are in range it returns dashes
        // rather than zeros, so the page never looks broken on a fresh tenant.
        var kpiFilter = new KpiStripFilter
        {
            SiteCode = SiteCode,
            From = DateTime.UtcNow.AddDays(-14),
            To = DateTime.UtcNow,
        };
        var kpiResult = await _receiving.GetKpiStripAsync(kpiFilter, ct);
        Shell.KpiStrip = BuildKpiStrip(kpiResult);

        // Exception lane — AI-priority-ranked. ListExpectedArrivals is wired
        // by PR #5 too via the voice-tools layer but the page consumes the
        // lane direct from the service for now.
        var laneFilter = new SvcLaneFilter
        {
            SiteCode = SiteCode,
            AiPrioritized = true,
            Take = 25,
        };
        var laneResult = await _receiving.GetExceptionLaneAsync(laneFilter, ct);
        Shell.ExceptionLane = BuildExceptionLane(laneResult);

        // Activity feed — recent AuditLog rows. Sprint 5 voice-AI rows will
        // appear here automatically (ActorKind=AiOnBehalfOf).
        var feedFilter = new ActivityFeedFilter
        {
            SiteCode = SiteCode,
            SinceSequence = 0,
            Take = 30,
        };
        var feedResult = await _receiving.GetActivityFeedAsync(feedFilter, ct);
        Shell.ActivityFeed = BuildActivityFeed(feedResult);

        // Drawer placeholder. PR #6 wires the profile-aware body via
        // DynamicFormViewComponent for the row currently in focus.
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

        return Page();
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
            Tab = baseCtx.Tab,
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
