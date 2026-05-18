// =============================================================================
// CherryAI EAM — Control Center scaffold view-models
// Sprint 11 PR #2 — typed view-models for the four-quadrant Control Center
// primitives (KPI Strip, Exception Lane, Activity Feed). The fourth quadrant —
// Detail Drawer — reuses the existing ContextDrawerModel (Primitives/_ContextDrawer).
//
// ADR-016 §D1 locks the four-quadrant scaffold; these models are the data
// contracts each Control Center page (Receiving first, then Purchasing /
// Maintenance / Planning / etc.) populates in OnGet.
// =============================================================================

using System.Collections.Generic;

namespace Abs.FixedAssets.Pages.Shared.Primitives;

/// <summary>
/// KPI Strip — horizontal row of 6–10 KPI tiles across the top of every
/// Control Center. Each tile reuses the existing <see cref="KpiTileModel"/>
/// primitive; this model adds strip-level chrome (eyebrow, range selector,
/// "drill to all KPIs" link).
/// </summary>
public sealed class KpiStripModel
{
    /// <summary>Optional caps label above the strip (e.g. "Today's receiving").</summary>
    public string? Eyebrow { get; set; }

    /// <summary>Optional range pill text (e.g. "Last 14 days").</summary>
    public string? RangeText { get; set; }

    /// <summary>The KPI tiles to render left-to-right.</summary>
    public List<KpiTileModel> Tiles { get; set; } = new();

    /// <summary>Optional "see all" link to a detailed KPI page.</summary>
    public string? AllKpisHref { get; set; }

    /// <summary>Optional extra CSS classes on the root element.</summary>
    public string? ExtraClasses { get; set; }
}

/// <summary>
/// Exception Lane — the role's "what needs me right now" feed. Sortable list
/// of <see cref="ExceptionRowModel"/>, AI-priority-ranked. Keyboard-navigable
/// (J / K / Enter / Esc).
/// </summary>
public sealed class ExceptionLaneModel
{
    /// <summary>Lane title (e.g. "Exceptions").</summary>
    public string Title { get; set; } = "Exceptions";

    /// <summary>Right-aligned count text (e.g. "12 open").</summary>
    public string? CountText { get; set; }

    /// <summary>Optional filter pills (label + active state + href).</summary>
    public List<ExceptionLaneFilter> Filters { get; set; } = new();

    /// <summary>Row entries in display order (typically AI-priority-ranked).</summary>
    public List<ExceptionRowModel> Rows { get; set; } = new();

    /// <summary>Empty-state copy when Rows is empty.</summary>
    public string EmptyMessage { get; set; } = "All clear — no exceptions waiting.";

    /// <summary>Empty-state icon name (passed to _EmptyStateV2).</summary>
    public string EmptyIcon { get; set; } = "sparkle";

    /// <summary>Optional extra classes on the root.</summary>
    public string? ExtraClasses { get; set; }
}

public sealed class ExceptionLaneFilter
{
    public string Label { get; set; } = string.Empty;
    public bool Active { get; set; }
    public string? Href { get; set; }
    public int? Count { get; set; }
}

/// <summary>
/// One exception row inside the lane. Clicking it opens the Detail Drawer for
/// the underlying entity. Server-side handler decides the drawer body.
/// </summary>
public sealed class ExceptionRowModel
{
    /// <summary>Stable id for this row (used by drawer open + URL state).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Mono eyebrow code (e.g. "RCV-26-04891").</summary>
    public string? Eyebrow { get; set; }

    /// <summary>Headline text (e.g. "Heat H-12345 — 2 short on PO-19821").</summary>
    public string Headline { get; set; } = string.Empty;

    /// <summary>Second-line context (e.g. "ACME Steel · expected 25, received 23 · 12 min ago").</summary>
    public string? Subtext { get; set; }

    /// <summary>
    /// Severity drives row chrome + dot color: critical | warning | info | success | neutral.
    /// </summary>
    public string Severity { get; set; } = "info";

    /// <summary>Optional status pill on the right side (e.g. "QC HOLD").</summary>
    public StatusPillModel? StatusPill { get; set; }

    /// <summary>Optional SLA chip (e.g. "2h" remaining). Tone neutral by default.</summary>
    public string? SlaText { get; set; }

    /// <summary>SLA tone: neutral | warning | danger.</summary>
    public string SlaTone { get; set; } = "neutral";

    /// <summary>
    /// AI priority 0..100. Drives visual rank affordance — the higher, the more
    /// prominent. Rows are usually pre-sorted by this in the service layer.
    /// </summary>
    public int AiPriority { get; set; }

    /// <summary>If set, opens the named drawer via CherryDS.drawer.open().</summary>
    public string? OpenDrawerId { get; set; }

    /// <summary>If set, click navigates to this href (alternative to drawer).</summary>
    public string? Href { get; set; }

    /// <summary>If true, an AI-suggestion sparkle icon is rendered before the headline.</summary>
    public bool HasAiSuggestion { get; set; }
}

/// <summary>
/// Activity Feed — bottom collapsible strip showing the recent stream of
/// receipts / exceptions / voice actions / AI tool calls. Bloomberg-density.
/// Real-time-ready (SignalR hook lives on the rendered DOM).
/// </summary>
public sealed class ActivityFeedModel
{
    /// <summary>Feed title (e.g. "Activity").</summary>
    public string Title { get; set; } = "Activity";

    /// <summary>If true, feed starts collapsed (32px header strip). Default true.</summary>
    public bool CollapsedByDefault { get; set; } = true;

    /// <summary>Recent entries newest-first.</summary>
    public List<ActivityEntryModel> Entries { get; set; } = new();

    /// <summary>Empty-state copy.</summary>
    public string EmptyMessage { get; set; } = "Quiet here.";

    /// <summary>If set, drives the live update endpoint for SignalR / fetch poll.</summary>
    public string? LiveSource { get; set; }

    /// <summary>Optional extra classes on the root.</summary>
    public string? ExtraClasses { get; set; }
}

public sealed class ActivityEntryModel
{
    /// <summary>Stable id (for SignalR de-dupe + drawer deep-link).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display timestamp (e.g. "2:14 PM", "12 min ago").</summary>
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>
    /// Actor kind drives the icon: human | ai | system. Mirrors ADR-014's
    /// AuditLog.ActorKind so the audit trail and feed line up.
    /// </summary>
    public string ActorKind { get; set; } = "human";

    /// <summary>Actor display name (e.g. "Maria Hernandez", "Cherry AI", "System").</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Verb phrase (e.g. "received", "quarantined", "matched").</summary>
    public string Verb { get; set; } = string.Empty;

    /// <summary>Target entity reference (e.g. "RCV-26-04891").</summary>
    public string? TargetRef { get; set; }

    /// <summary>Optional inline snippet (e.g. "25 plates · ACME · heat H-12345").</summary>
    public string? Snippet { get; set; }

    /// <summary>If set, clicking the row opens the named drawer.</summary>
    public string? OpenDrawerId { get; set; }

    /// <summary>If set, clicking the row navigates to this href.</summary>
    public string? Href { get; set; }
}

/// <summary>
/// Shell composition — the four quadrant models packaged for the
/// <c>_ControlCenterShell.cshtml</c> partial that lays them out.
/// </summary>
public sealed class ControlCenterShellModel
{
    /// <summary>Page-level eyebrow (e.g. "RECEIVING CONTROL CENTER").</summary>
    public string? Eyebrow { get; set; }

    /// <summary>Page-level headline (e.g. "Today's dock").</summary>
    public string? Headline { get; set; }

    /// <summary>Page-level subtitle (e.g. "Eastside plant · day shift").</summary>
    public string? Subtitle { get; set; }

    /// <summary>The KPI strip across the top. Required.</summary>
    public KpiStripModel KpiStrip { get; set; } = new();

    /// <summary>The exception lane (center-left). Required.</summary>
    public ExceptionLaneModel ExceptionLane { get; set; } = new();

    /// <summary>
    /// The detail drawer (right rail). Reuses existing
    /// <see cref="ContextDrawerModel"/>. Caller supplies the drawer with
    /// profile-aware body HTML.
    /// </summary>
    public ContextDrawerModel? Drawer { get; set; }

    /// <summary>The activity feed (bottom collapsible). Optional.</summary>
    public ActivityFeedModel? ActivityFeed { get; set; }

    /// <summary>
    /// Voice posture for this Control Center. ADR-016 D3 — push-to-talk
    /// is the default; always-on is opt-in per-user.
    /// </summary>
    public string VoicePosture { get; set; } = "push-to-talk";

    /// <summary>If true, render the bottom-right voice button. Default true.</summary>
    public bool ShowVoiceButton { get; set; } = true;
}
