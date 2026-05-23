// =============================================================================
// CherryAI EAM — IProductionControlCenterService (Sprint 13.5 PR #5)
// ADR-016 §D7 + ADR-018 — service surface backing the Production Control Center.
//
// Parallel to IReceivingControlCenterService. Same Result<T> shape, same
// filter-DTO convention, same KPI band / exception lane / activity feed
// primitives so Pages/Production/ControlCenter.cshtml can compose the
// existing Pages/Shared/Primitives/Cockpit/ partials without any new UI work.
//
// Mutations on ProductionOrder go through IProductionOrderService (PR #3) —
// this Control Center service is READ + BULK-COORDINATION only:
//   - Reads: KPI strip, exception lane, queue, activity feed, next-up, AI hints
//   - Bulk: select N orders + apply UpdateStatus / Assign / Unassign en masse
//     (each row routed through IProductionOrderService so legal-transition map +
//     CHERRY025 control plane + chain emit all still apply per row)
// =============================================================================

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Pages.Shared.Primitives;
using Abs.FixedAssets.Services.Navigation.Cockpit;

namespace Abs.FixedAssets.Services.Production;

public interface IProductionControlCenterService
{
    // ----- Queries (no idempotency, no audit) ------------------------

    /// <summary>
    /// 6-tile KPI band across the top of the Control Center. Tiles:
    /// Past Due / Due Today / In Progress / On Hold / Completed Today /
    /// Quality Hold. Each tile carries a clickable drill route into the
    /// matching queue filter.
    /// </summary>
    Task<Result<ProductionKpiBandData>> GetKpiBandAsync(
        ProductionKpiBandFilter filter,
        CancellationToken ct);

    /// <summary>
    /// Heuristic-ranked list of production orders that need human attention.
    /// Drives the Exceptions tab. Severity assignment:
    ///   critical = past due + not OnHold
    ///   warning  = due today, or OnHold &gt; 3 days
    ///   info     = due this week
    /// </summary>
    Task<Result<ProductionExceptionLanePage>> GetExceptionLaneAsync(
        ProductionExceptionLaneFilter filter,
        CancellationToken ct);

    /// <summary>
    /// Time-bucketed queue of active production orders (Overdue / Today /
    /// This Week / Later). Each row implements ICockpitQueueRow so the
    /// generic _CockpitQueueCard partial renders it. Preview blob is the
    /// detail payload for the right-hand drawer.
    /// </summary>
    Task<Result<ProductionQueueData>> GetProductionQueueAsync(
        ProductionQueueFilter filter,
        CancellationToken ct);

    /// <summary>
    /// Recent ProductionOrder mutations (last 30 entries). v1 sources from
    /// audit columns (ModifiedAt / ModifiedBy / Status). v2 will join the
    /// chain-of-custody graph to surface upstream events (linked PO receipt,
    /// quality hold, etc.).
    /// </summary>
    Task<Result<ProductionActivityFeedData>> GetActivityFeedAsync(
        ProductionActivityFeedFilter filter,
        CancellationToken ct);

    /// <summary>
    /// "Next Up" — single highest-priority order to work on now. Composite
    /// rank = (past-due-days * 10) + priority + (linked-project-active * 5).
    /// Shown as a hero card above the queue when the Queue tab is active.
    /// </summary>
    Task<Result<ProductionNextUpData>> GetNextUpAsync(
        ProductionNextUpFilter filter,
        CancellationToken ct);

    /// <summary>
    /// Three heuristic AI suggestions for the operator: "3 orders past
    /// due — release the next 2 from project X" / "Quality hold on
    /// PRO-2026-00042 needs disposition" / "PRO-2026-00038 OnHold for
    /// 5 days — escalate". Real LLM narration lands in PR #7 (voice).
    /// </summary>
    Task<Result<ProductionAiSuggestionsData>> GetAiSuggestionsAsync(
        ProductionAiSuggestionsFilter filter,
        CancellationToken ct);

    // ----- Bulk mutations (idempotent, per-row routed through IProductionOrderService) -----

    /// <summary>
    /// Apply a status transition to a SET of production orders. Each row
    /// is routed through IProductionOrderService.UpdateStatusAsync so the
    /// legal-transition map + CHERRY025 control plane + chain emit all
    /// still apply per row. Rows that fail are reported back individually;
    /// the bulk operation does NOT roll back on partial failure (each row
    /// is an independent business event).
    /// </summary>
    Task<Result<BulkStatusOutcome>> BulkUpdateStatusAsync(
        BulkStatusRequest request,
        CancellationToken ct);
}

// =============================================================================
// Filter DTOs
// =============================================================================

public sealed record ProductionKpiBandFilter(int? SiteId = null);

public sealed record ProductionQueueFilter(
    ProductionOrderStatus? Status = null,
    int? CustomerProjectId = null,
    int? CustomerId = null,
    int? LocationId = null,
    string? SearchText = null,
    int Take = 200);

public sealed record ProductionNextUpFilter(int? SiteId = null);

public sealed record ProductionAiSuggestionsFilter(int? SiteId = null);

public sealed record ProductionExceptionLaneFilter(
    string? Severity = null,        // critical / warning / info / null = all
    string? Kind = null,
    int Take = 50);

public sealed record ProductionActivityFeedFilter(
    int? SinceSequence = null,
    int Take = 30);

public sealed record BulkStatusRequest(
    IReadOnlyList<int> ProductionOrderIds,
    ProductionOrderStatus NewStatus,
    string? ModifiedBy);

// =============================================================================
// Result DTOs
// =============================================================================

public sealed class ProductionKpiBandData
{
    public IReadOnlyList<ProductionKpiTile> Tiles { get; set; } = new List<ProductionKpiTile>();
    public int TotalActive { get; set; }
    public string? AsOfText { get; set; }
}

public sealed class ProductionKpiTile
{
    public string Key { get; set; } = string.Empty;          // "past-due" / "due-today" / etc.
    public string Label { get; set; } = string.Empty;        // "Past Due"
    public string Value { get; set; } = "0";
    public string Tone { get; set; } = "neutral";            // critical / warning / info / success / neutral
    public string? IconClass { get; set; }
    public string? DrillHref { get; set; }                   // /Production/ControlCenter?status=...
    public string? Hint { get; set; }
}

public sealed class ProductionQueueData
{
    public IReadOnlyList<ProductionQueueRow> Rows { get; set; } = new List<ProductionQueueRow>();
    public int TotalRowCount { get; set; }
    public string PreviewBlobJson { get; set; } = "{}";      // map: rowId → preview record
}

/// <summary>
/// One row in the Production queue. Implements ICockpitQueueRow so the
/// generic _CockpitQueueCard partial can render it.
/// </summary>
public sealed class ProductionQueueRow : ICockpitQueueRow
{
    public string Id { get; set; } = string.Empty;
    public string Primary { get; set; } = string.Empty;                                   // OrderNumber
    public string Secondary { get; set; } = string.Empty;                                 // Title
    public System.DateTime? RequiredAt { get; set; }                                      // ScheduledEnd
    public string Tone { get; set; } = "neutral";                                         // danger/warning/info/neutral
    public IReadOnlyList<MetaTriple> Meta { get; set; } = new List<MetaTriple>();
    public string? StatusLabel { get; set; }
    public string? StatusTone { get; set; }

    // Production-specific extras (not on interface — read by preview hydration)
    public int OrderId { get; set; }
    public ProductionOrderStatus Status { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityCompleted { get; set; }
    public string? CustomerName { get; set; }
    public string? ProjectCode { get; set; }
    public int? ProjectId { get; set; }
}

public sealed class ProductionNextUpData
{
    public bool HasCandidate { get; set; }
    public int? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string? Title { get; set; }
    public string? ReasonText { get; set; }                  // "Past due 3 days · Linked to Active project Weir Q2"
    public ProductionOrderStatus? Status { get; set; }
    public string? CustomerName { get; set; }
    public string? ProjectCode { get; set; }
    public int? ProjectId { get; set; }
    public int? CompositeScore { get; set; }
}

public sealed class ProductionAiSuggestionsData
{
    public IReadOnlyList<ProductionAiSuggestion> Suggestions { get; set; } = new List<ProductionAiSuggestion>();
    public string? SummaryText { get; set; }                 // "Today: 3 past due, 7 in progress, 2 waiting on customer"
}

public sealed class ProductionAiSuggestion
{
    public string Headline { get; set; } = string.Empty;
    public string? Subtext { get; set; }
    public string Tone { get; set; } = "info";
    public string? ActionLabel { get; set; }
    public string? ActionHref { get; set; }
}

public sealed class BulkStatusOutcome
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public IReadOnlyList<BulkStatusFailure> Failures { get; set; } = new List<BulkStatusFailure>();
}

public sealed class BulkStatusFailure
{
    public int ProductionOrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

// Exception lane = service result for the Exceptions tab. Page converts
// each ProductionExceptionLaneItem into the generic UI ExceptionRowModel
// that _ExceptionLane.cshtml reads.
public sealed class ProductionExceptionLanePage
{
    public IReadOnlyList<ProductionExceptionLaneItem> Items { get; set; } = new List<ProductionExceptionLaneItem>();
    public int TotalCount { get; set; }
    public System.DateTime AsOfUtc { get; set; } = System.DateTime.UtcNow;
}

public sealed class ProductionExceptionLaneItem
{
    public int ProductionOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string? Subtext { get; set; }
    public string Severity { get; set; } = "info";          // critical / warning / info / success / neutral
    public string Kind { get; set; } = string.Empty;        // status name (used as filter pill kind)
    public int Score { get; set; }                          // composite rank (higher = more urgent)
    public string Href { get; set; } = string.Empty;        // /Production/Details/{id}
}

// Activity feed = service result for the bottom strip.
public sealed class ProductionActivityFeedData
{
    public IReadOnlyList<ProductionActivityEntry> Entries { get; set; } = new List<ProductionActivityEntry>();
    public int HighestSequence { get; set; }
    public System.DateTime AsOfUtc { get; set; } = System.DateTime.UtcNow;
}

public sealed class ProductionActivityEntry
{
    public string Id { get; set; } = string.Empty;
    public System.DateTime OccurredAtUtc { get; set; }
    public string ActorKind { get; set; } = "human";        // human / ai / system
    public string ActorName { get; set; } = string.Empty;
    public string Verb { get; set; } = string.Empty;        // "created" / "updated → Released" / etc.
    public string TargetRef { get; set; } = string.Empty;   // OrderNumber
    public string? Snippet { get; set; }
    public string? Href { get; set; }
}
