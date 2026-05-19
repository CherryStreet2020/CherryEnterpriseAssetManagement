// =============================================================================
// CherryAI EAM — NextUp + AI Suggestions contracts (Sprint 12A PR #5.2)
//
// Replaces the empty "Select a PO to preview" welcome state with a server-
// rendered priority preview pane: highest-priority overdue PO already loaded,
// action chips visible, "Up next" teaser at the bottom. The page lands with
// the operator's most urgent action already in front of them.
//
// AI Suggestions are hardcoded for this PR — three smart suggestions seeded
// by simple SQL heuristics (batch-by-vendor, orphan match, overdue tracking).
// Sprint 5's voice-AI runtime will swap the producer with a real model call
// without changing this contract.
// =============================================================================

using System;
using System.Collections.Generic;

namespace Abs.FixedAssets.Services.Receiving;

public sealed class ReceivingNextUpFilter
{
    public string? SiteCode { get; init; }
}

public sealed class ReceivingNextUpData
{
    // The highest-priority PO. Null when the queue is empty.
    public NextUpPo? Priority { get; init; }

    // The second-priority PO, surfaced as an "Up next" teaser at the
    // bottom of the preview pane. Null when only one PO is in the queue.
    public NextUpTeaser? UpNext { get; init; }
}

public sealed class NextUpPo
{
    public int Id { get; init; }
    public string PoNumber { get; init; } = "";
    public string Vendor { get; init; } = "";
    public string OrderDateText { get; init; } = "";
    public string RequiredDateText { get; init; } = "";
    public string Status { get; init; } = "";
    public string StatusLabel { get; init; } = "";  // pretty form: "Approved" / "Partial" / "Sent"
    public string StatusTone { get; init; } = "approved";
    public string TotalText { get; init; } = "—";  // "$4,165"
    public string ShipTo { get; init; } = "—";
    public int LineCount { get; init; }
    public int? DaysOverdue { get; init; }   // null when not overdue
    public IReadOnlyList<NextUpLine> Lines { get; init; } = Array.Empty<NextUpLine>();
}

public sealed class NextUpLine
{
    public string PartNumber { get; init; } = "—";
    public string Description { get; init; } = "—";
    public string Uom { get; init; } = "EA";
    public decimal Ordered { get; init; }
    public decimal Received { get; init; }
    public decimal Remaining { get; init; }
    public string LineTotalText { get; init; } = "—";  // "$879.75"
}

public sealed class NextUpTeaser
{
    public int Id { get; init; }
    public string PoNumber { get; init; } = "";
    public string Vendor { get; init; } = "";
    public int LineCount { get; init; }
    public string TotalText { get; init; } = "—";
}

// =============================================================================
// AI Suggestions
// =============================================================================

public sealed class ReceivingAiSuggestionsFilter
{
    public string? SiteCode { get; init; }
}

public sealed class ReceivingAiSuggestionsData
{
    public IReadOnlyList<AiSuggestion> Suggestions { get; init; } = Array.Empty<AiSuggestion>();
}

// One AI suggestion strip row.
//
// Action is an optional inline button label ("Batch", "Review", "Notify").
// When ActionHref is null, the action chip is hidden — the suggestion is
// informational only.
public sealed class AiSuggestion
{
    // Stable machine key for telemetry / a11y. Examples:
    //   "batch-by-vendor", "match-orphans", "overdue-tracking"
    public string Code { get; init; } = "";

    // Human-readable suggestion text. Single sentence, ≤ 100 chars.
    public string Text { get; init; } = "";

    // Optional action chip text (e.g. "Batch", "Review", "Notify").
    public string? ActionText { get; init; }
    // Where the action takes the user.
    public string? ActionHref { get; init; }

    // Tabler icon class for the leading icon. Defaults to sparkles.
    public string IconClass { get; init; } = "fas fa-sparkles";
}
