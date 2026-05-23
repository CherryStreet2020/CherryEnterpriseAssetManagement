// =============================================================================
// CherryAI EAM — ProductionControlCenterService (Sprint 13.5 PR #5)
// Implementation of IProductionControlCenterService.
//
// Tenant scoping mirrors IProductionOrderService — ProductionOrder has no
// direct CompanyId, so we scope through Location.CompanyId, fall back to
// Customer.CompanyId. Orders with neither anchor are excluded from KPIs +
// queue + exception lane (they're unscopable until a future denorm PR adds
// CompanyId to the header).
//
// Reads only — every mutation routes through IProductionOrderService
// (PR #3) per ADR-025. Bulk mutations iterate single-row calls so the
// legal-transition map + chain emit + CHERRY025 control plane all apply
// per row.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.ControlPlane;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Navigation.Cockpit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public sealed class ProductionControlCenterService : IProductionControlCenterService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IProductionOrderService _orderService;
    private readonly ILogger<ProductionControlCenterService> _logger;

    public ProductionControlCenterService(
        AppDbContext db,
        ITenantContext tenantContext,
        IProductionOrderService orderService,
        ILogger<ProductionControlCenterService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _orderService = orderService;
        _logger = logger;
    }

    // ----------------------------------------------------------------
    // Tenant-scoped base query — used by every read method.
    // ----------------------------------------------------------------
    private IQueryable<ProductionOrder> BaseQuery()
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return _db.ProductionOrders
            .AsNoTracking()
            .Where(p =>
                (p.Location != null && p.Location.CompanyId != null && visible.Contains(p.Location.CompanyId.Value)) ||
                (p.Customer != null && visible.Contains(p.Customer.CompanyId)));
    }

    // ----------------------------------------------------------------
    // 1) KPI BAND — 6 tiles
    // ----------------------------------------------------------------
    public async Task<Result<ProductionKpiBandData>> GetKpiBandAsync(
        ProductionKpiBandFilter filter,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var endOfToday = today.AddDays(1);
        var threeDaysAgo = today.AddDays(-3);

        var q = BaseQuery();

        // Single round-trip — compute every count via conditional aggregates.
        var counts = await q.GroupBy(_ => 1).Select(g => new
        {
            PastDue = g.Count(p =>
                p.Status != ProductionOrderStatus.Completed &&
                p.Status != ProductionOrderStatus.Cancelled &&
                p.ScheduledEnd != null && p.ScheduledEnd < today),
            DueToday = g.Count(p =>
                p.Status != ProductionOrderStatus.Completed &&
                p.Status != ProductionOrderStatus.Cancelled &&
                p.ScheduledEnd != null && p.ScheduledEnd >= today && p.ScheduledEnd < endOfToday),
            InProgress = g.Count(p => p.Status == ProductionOrderStatus.InProgress),
            OnHold = g.Count(p => p.Status == ProductionOrderStatus.OnHold),
            CompletedToday = g.Count(p =>
                p.Status == ProductionOrderStatus.Completed &&
                p.ActualEnd != null && p.ActualEnd >= today && p.ActualEnd < endOfToday),
            StaleOnHold = g.Count(p =>
                p.Status == ProductionOrderStatus.OnHold &&
                p.ModifiedAt != null && p.ModifiedAt < threeDaysAgo),
            TotalActive = g.Count(p =>
                p.Status != ProductionOrderStatus.Completed &&
                p.Status != ProductionOrderStatus.Cancelled),
        }).FirstOrDefaultAsync(ct);

        counts ??= new
        {
            PastDue = 0, DueToday = 0, InProgress = 0, OnHold = 0,
            CompletedToday = 0, StaleOnHold = 0, TotalActive = 0
        };

        var tiles = new List<ProductionKpiTile>
        {
            new() {
                Key = "past-due", Label = "Past Due",
                Value = counts.PastDue.ToString(),
                Tone = counts.PastDue > 0 ? "critical" : "neutral",
                IconClass = "fas fa-triangle-exclamation",
                DrillHref = "/Production/ControlCenter?lens=past-due&tab=queue",
                Hint = counts.PastDue == 1 ? "1 order past scheduled end" : $"{counts.PastDue} orders past scheduled end",
            },
            new() {
                Key = "due-today", Label = "Due Today",
                Value = counts.DueToday.ToString(),
                Tone = counts.DueToday > 0 ? "warning" : "neutral",
                IconClass = "fas fa-clock",
                DrillHref = "/Production/ControlCenter?lens=due-today&tab=queue",
            },
            new() {
                Key = "in-progress", Label = "In Progress",
                Value = counts.InProgress.ToString(),
                Tone = "info",
                IconClass = "fas fa-gears",
                DrillHref = "/Production/ControlCenter?status=InProgress&tab=queue",
            },
            new() {
                Key = "on-hold", Label = "On Hold",
                Value = counts.OnHold.ToString(),
                Tone = counts.StaleOnHold > 0 ? "warning" : "neutral",
                IconClass = "fas fa-pause-circle",
                DrillHref = "/Production/ControlCenter?status=OnHold&tab=queue",
                Hint = counts.StaleOnHold > 0
                    ? $"{counts.StaleOnHold} stale (> 3 days)"
                    : null,
            },
            new() {
                Key = "completed-today", Label = "Completed Today",
                Value = counts.CompletedToday.ToString(),
                Tone = "success",
                IconClass = "fas fa-circle-check",
            },
            new() {
                Key = "active-total", Label = "Active Total",
                Value = counts.TotalActive.ToString(),
                Tone = "neutral",
                IconClass = "fas fa-industry",
                DrillHref = "/Production/ControlCenter?tab=queue",
            },
        };

        return Result.Success(new ProductionKpiBandData
        {
            Tiles = tiles,
            TotalActive = counts.TotalActive,
            AsOfText = $"as of {DateTime.UtcNow:HH:mm} UTC",
        });
    }

    // ----------------------------------------------------------------
    // 2) EXCEPTION LANE — heuristic-ranked rows needing attention
    // ----------------------------------------------------------------
    public async Task<Result<ProductionExceptionLanePage>> GetExceptionLaneAsync(
        ProductionExceptionLaneFilter filter,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var threeDaysAgo = today.AddDays(-3);

        // Fetch up to 200 candidate orders that have ANY exception trait,
        // then rank in-memory.
        var candidates = await BaseQuery()
            .Where(p =>
                p.Status != ProductionOrderStatus.Completed &&
                p.Status != ProductionOrderStatus.Cancelled &&
                (
                    (p.ScheduledEnd != null && p.ScheduledEnd < today.AddDays(7)) ||
                    p.Status == ProductionOrderStatus.OnHold
                ))
            .Include(p => p.Customer)
            .Include(p => p.CustomerProject)
            .OrderBy(p => p.ScheduledEnd ?? DateTime.MaxValue)
            .Take(200)
            .Select(p => new
            {
                p.Id, p.OrderNumber, p.Title, p.Status, p.Priority,
                p.ScheduledEnd, p.ModifiedAt, p.QuantityOrdered, p.QuantityCompleted,
                CustomerName = p.Customer != null ? p.Customer.Name : null,
                ProjectCode = p.CustomerProject != null ? p.CustomerProject.Code : null,
                ProjectId = p.CustomerProjectId,
            })
            .ToListAsync(ct);

        var rows = new List<ProductionExceptionLaneItem>(capacity: candidates.Count);
        foreach (var c in candidates)
        {
            int score = c.Priority;
            string severity;
            string headline;

            if (c.ScheduledEnd.HasValue && c.ScheduledEnd.Value < today
                && c.Status != ProductionOrderStatus.OnHold)
            {
                var daysOver = (today - c.ScheduledEnd.Value.Date).Days;
                score += daysOver * 10;
                severity = "critical";
                headline = $"Past due {daysOver}d — {c.Title}";
            }
            else if (c.ScheduledEnd.HasValue && c.ScheduledEnd.Value < today.AddDays(1))
            {
                score += 50;
                severity = "warning";
                headline = $"Due today — {c.Title}";
            }
            else if (c.Status == ProductionOrderStatus.OnHold
                     && c.ModifiedAt.HasValue && c.ModifiedAt.Value < threeDaysAgo)
            {
                score += 30;
                severity = "warning";
                headline = $"On hold > 3 days — {c.Title}";
            }
            else if (c.Status == ProductionOrderStatus.OnHold)
            {
                score += 10;
                severity = "info";
                headline = $"On hold — {c.Title}";
            }
            else
            {
                score += 5;
                severity = "info";
                headline = $"Due in {(c.ScheduledEnd!.Value.Date - today).Days}d — {c.Title}";
            }

            var customerBit = string.IsNullOrEmpty(c.CustomerName) ? "" : $"{c.CustomerName} · ";
            var projBit = string.IsNullOrEmpty(c.ProjectCode) ? "" : $"project {c.ProjectCode} · ";
            var qtyBit = $"{c.QuantityCompleted:0.#}/{c.QuantityOrdered:0.#}";

            rows.Add(new ProductionExceptionLaneItem
            {
                ProductionOrderId = c.Id,
                OrderNumber = c.OrderNumber,
                Headline = headline,
                Subtext = $"{customerBit}{projBit}{c.Status} · qty {qtyBit}",
                Severity = severity,
                Kind = c.Status.ToString(),
                Score = score,
                Href = $"/Production/Details/{c.Id}",
            });
        }

        var ranked = rows
            .Where(r => filter.Severity == null || r.Severity == filter.Severity)
            .Where(r => filter.Kind == null || r.Kind == filter.Kind)
            .OrderByDescending(r => r.Score)
            .Take(filter.Take)
            .ToList();

        var lane = new ProductionExceptionLanePage
        {
            TotalCount = candidates.Count,
            Items = ranked,
            AsOfUtc = DateTime.UtcNow,
        };

        return Result.Success(lane);
    }

    // ----------------------------------------------------------------
    // 3) PRODUCTION QUEUE — time-bucketed rows + preview blob
    // ----------------------------------------------------------------
    public async Task<Result<ProductionQueueData>> GetProductionQueueAsync(
        ProductionQueueFilter filter,
        CancellationToken ct)
    {
        var q = BaseQuery();

        if (filter.Status.HasValue)
            q = q.Where(p => p.Status == filter.Status.Value);
        if (filter.CustomerProjectId.HasValue)
            q = q.Where(p => p.CustomerProjectId == filter.CustomerProjectId.Value);
        if (filter.CustomerId.HasValue)
            q = q.Where(p => p.CustomerId == filter.CustomerId.Value);
        if (filter.LocationId.HasValue)
            q = q.Where(p => p.LocationId == filter.LocationId.Value);

        // Active-only by default (don't drown the queue with closed/cancelled).
        if (!filter.Status.HasValue)
        {
            q = q.Where(p =>
                p.Status != ProductionOrderStatus.Completed &&
                p.Status != ProductionOrderStatus.Cancelled);
        }

        var search = filter.SearchText?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            q = q.Where(p =>
                EF.Functions.ILike(p.OrderNumber, $"%{search}%") ||
                EF.Functions.ILike(p.Title, $"%{search}%"));
        }

        var totalCount = await q.CountAsync(ct);

        var rows = await q
            .Include(p => p.Customer)
            .Include(p => p.CustomerProject)
            .Include(p => p.Item)
            .OrderBy(p => p.ScheduledEnd ?? DateTime.MaxValue)
            .ThenByDescending(p => p.Priority)
            .Take(filter.Take)
            .Select(p => new ProductionQueueRow
            {
                Id              = $"pro-{p.Id}",
                OrderId         = p.Id,
                Primary         = p.OrderNumber,
                Secondary       = p.Title,
                RequiredAt      = p.ScheduledEnd,
                Status          = p.Status,
                StatusLabel     = p.Status.ToString().ToUpper(),
                QuantityOrdered = p.QuantityOrdered,
                QuantityCompleted = p.QuantityCompleted,
                CustomerName    = p.Customer != null ? p.Customer.Name : null,
                ProjectCode     = p.CustomerProject != null ? p.CustomerProject.Code : null,
                ProjectId       = p.CustomerProjectId,
            })
            .ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        foreach (var r in rows)
        {
            // Tone derivation
            if (r.RequiredAt.HasValue && r.RequiredAt.Value < today
                && r.Status != ProductionOrderStatus.Completed && r.Status != ProductionOrderStatus.Cancelled)
            {
                r.Tone = "danger";
                r.StatusTone = "danger";
            }
            else if (r.RequiredAt.HasValue && r.RequiredAt.Value < today.AddDays(1))
            {
                r.Tone = "warning";
                r.StatusTone = "warning";
            }
            else if (r.Status == ProductionOrderStatus.OnHold)
            {
                r.Tone = "warning";
                r.StatusTone = "warning";
            }
            else if (r.Status == ProductionOrderStatus.InProgress)
            {
                r.Tone = "info";
                r.StatusTone = "info";
            }
            else
            {
                r.Tone = "neutral";
                r.StatusTone = "neutral";
            }

            var meta = new List<MetaTriple>();
            if (r.RequiredAt.HasValue)
                meta.Add(new MetaTriple("Due", r.RequiredAt.Value.ToString("MMM dd"), null));
            meta.Add(new MetaTriple("Qty", $"{r.QuantityCompleted:0.#}/{r.QuantityOrdered:0.#}", null));
            if (!string.IsNullOrEmpty(r.CustomerName))
                meta.Add(new MetaTriple("Cust", r.CustomerName, null));
            r.Meta = meta;
        }

        // Preview blob: { "pro-42": { orderNumber, title, status, qty, project... }, ... }
        var preview = rows.ToDictionary(
            r => r.Id,
            r => new
            {
                orderNumber = r.Primary,
                title = r.Secondary,
                status = r.Status.ToString(),
                qtyOrdered = r.QuantityOrdered,
                qtyCompleted = r.QuantityCompleted,
                scheduledEnd = r.RequiredAt?.ToString("yyyy-MM-dd"),
                customer = r.CustomerName,
                project = r.ProjectCode,
                projectId = r.ProjectId,
                detailsHref = $"/Production/Details/{r.OrderId}",
            });

        return Result.Success(new ProductionQueueData
        {
            Rows = rows,
            TotalRowCount = totalCount,
            PreviewBlobJson = JsonSerializer.Serialize(preview),
        });
    }

    // ----------------------------------------------------------------
    // 4) ACTIVITY FEED — recent ProductionOrder mutations
    // ----------------------------------------------------------------
    public async Task<Result<ProductionActivityFeedData>> GetActivityFeedAsync(
        ProductionActivityFeedFilter filter,
        CancellationToken ct)
    {
        var entries = await BaseQuery()
            .Where(p => p.ModifiedAt != null || p.CreatedAt > DateTime.MinValue)
            .OrderByDescending(p => p.ModifiedAt ?? p.CreatedAt)
            .Take(filter.Take)
            .Select(p => new
            {
                p.Id, p.OrderNumber, p.Status, p.CreatedAt, p.CreatedBy,
                p.ModifiedAt, p.ModifiedBy,
            })
            .ToListAsync(ct);

        var feedEntries = entries.Select(e => new ProductionActivityEntry
        {
            Id = $"pro-act-{e.Id}",
            OccurredAtUtc = e.ModifiedAt ?? e.CreatedAt,
            ActorKind = "human",
            ActorName = e.ModifiedBy ?? e.CreatedBy ?? "system",
            Verb = e.ModifiedAt.HasValue ? $"updated → {e.Status}" : "created",
            TargetRef = e.OrderNumber,
            Snippet = $"Production order {e.OrderNumber} now {e.Status}",
            Href = $"/Production/Details/{e.Id}",
        }).ToList();

        return Result.Success(new ProductionActivityFeedData
        {
            Entries = feedEntries,
            AsOfUtc = DateTime.UtcNow,
        });
    }

    // ----------------------------------------------------------------
    // 5) NEXT UP — single highest-priority order
    // ----------------------------------------------------------------
    public async Task<Result<ProductionNextUpData>> GetNextUpAsync(
        ProductionNextUpFilter filter,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var candidates = await BaseQuery()
            .Where(p =>
                p.Status != ProductionOrderStatus.Completed &&
                p.Status != ProductionOrderStatus.Cancelled)
            .Include(p => p.Customer)
            .Include(p => p.CustomerProject)
            .Take(100)
            .Select(p => new
            {
                p.Id, p.OrderNumber, p.Title, p.Status, p.Priority, p.ScheduledEnd,
                CustomerName = p.Customer != null ? p.Customer.Name : null,
                ProjectId = p.CustomerProjectId,
                ProjectCode = p.CustomerProject != null ? p.CustomerProject.Code : null,
                ProjectActive = p.CustomerProject != null
                    && p.CustomerProject.Status == Abs.FixedAssets.Models.Projects.CustomerProjectStatus.Active,
            })
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return Result.Success(new ProductionNextUpData { HasCandidate = false });
        }

        var best = candidates
            .Select(c => new
            {
                Row = c,
                Score =
                    (c.ScheduledEnd.HasValue && c.ScheduledEnd.Value < today
                        ? (today - c.ScheduledEnd.Value.Date).Days * 10 : 0)
                    + c.Priority
                    + (c.ProjectActive ? 5 : 0),
            })
            .OrderByDescending(x => x.Score)
            .First();

        var r = best.Row;
        var reasonParts = new List<string>();
        if (r.ScheduledEnd.HasValue && r.ScheduledEnd.Value < today)
            reasonParts.Add($"Past due {(today - r.ScheduledEnd.Value.Date).Days}d");
        else if (r.ScheduledEnd.HasValue && r.ScheduledEnd.Value < today.AddDays(1))
            reasonParts.Add("Due today");
        if (r.Priority > 50)
            reasonParts.Add($"Priority {r.Priority}");
        if (r.ProjectActive && !string.IsNullOrEmpty(r.ProjectCode))
            reasonParts.Add($"Linked to active project {r.ProjectCode}");

        return Result.Success(new ProductionNextUpData
        {
            HasCandidate = true,
            OrderId = r.Id,
            OrderNumber = r.OrderNumber,
            Title = r.Title,
            Status = r.Status,
            CustomerName = r.CustomerName,
            ProjectCode = r.ProjectCode,
            ProjectId = r.ProjectId,
            CompositeScore = best.Score,
            ReasonText = reasonParts.Count > 0 ? string.Join(" · ", reasonParts) : "Highest composite rank",
        });
    }

    // ----------------------------------------------------------------
    // 6) AI SUGGESTIONS — 3 deterministic heuristics
    // ----------------------------------------------------------------
    public async Task<Result<ProductionAiSuggestionsData>> GetAiSuggestionsAsync(
        ProductionAiSuggestionsFilter filter,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var threeDaysAgo = today.AddDays(-3);

        var snapshot = await BaseQuery().GroupBy(_ => 1).Select(g => new
        {
            PastDue = g.Count(p =>
                p.Status != ProductionOrderStatus.Completed &&
                p.Status != ProductionOrderStatus.Cancelled &&
                p.ScheduledEnd != null && p.ScheduledEnd < today),
            InProgress = g.Count(p => p.Status == ProductionOrderStatus.InProgress),
            OnHold = g.Count(p => p.Status == ProductionOrderStatus.OnHold),
            StaleOnHold = g.Count(p =>
                p.Status == ProductionOrderStatus.OnHold &&
                p.ModifiedAt != null && p.ModifiedAt < threeDaysAgo),
            Planned = g.Count(p => p.Status == ProductionOrderStatus.Planned),
        }).FirstOrDefaultAsync(ct);

        snapshot ??= new
        {
            PastDue = 0, InProgress = 0, OnHold = 0, StaleOnHold = 0, Planned = 0
        };

        var summary = $"{snapshot.PastDue} past due · {snapshot.InProgress} in progress · {snapshot.OnHold} on hold · {snapshot.Planned} planned";

        var suggestions = new List<ProductionAiSuggestion>();

        if (snapshot.PastDue > 0)
        {
            suggestions.Add(new ProductionAiSuggestion
            {
                Headline = snapshot.PastDue == 1
                    ? "1 order is past due"
                    : $"{snapshot.PastDue} orders are past due",
                Subtext = "Review the Past Due tile to triage. Mark unrecoverable orders OnHold so the queue reflects reality.",
                Tone = "critical",
                ActionLabel = "View past due",
                ActionHref = "/Production/ControlCenter?lens=past-due&tab=queue",
            });
        }

        if (snapshot.StaleOnHold > 0)
        {
            suggestions.Add(new ProductionAiSuggestion
            {
                Headline = $"{snapshot.StaleOnHold} orders on hold > 3 days",
                Subtext = "Stale holds rot. Either release with a reason or cancel — don't let them age silently.",
                Tone = "warning",
                ActionLabel = "View stale holds",
                ActionHref = "/Production/ControlCenter?status=OnHold&tab=queue",
            });
        }

        if (snapshot.Planned > 0 && snapshot.PastDue == 0)
        {
            suggestions.Add(new ProductionAiSuggestion
            {
                Headline = snapshot.Planned == 1
                    ? "1 planned order is ready to release"
                    : $"{snapshot.Planned} planned orders are ready to release",
                Subtext = "Release in priority order from the queue, or bulk-select planned rows.",
                Tone = "info",
                ActionLabel = "View planned",
                ActionHref = "/Production/ControlCenter?status=Planned&tab=queue",
            });
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add(new ProductionAiSuggestion
            {
                Headline = "All clear — no exceptions detected",
                Subtext = "Nothing past due, nothing stale on hold. Use this time to plan ahead or run a cycle count.",
                Tone = "success",
            });
        }

        return Result.Success(new ProductionAiSuggestionsData
        {
            Suggestions = suggestions.Take(3).ToList(),
            SummaryText = summary,
        });
    }

    // ----------------------------------------------------------------
    // 7) BULK UPDATE STATUS — iterate single-row calls
    // ----------------------------------------------------------------
    public async Task<Result<BulkStatusOutcome>> BulkUpdateStatusAsync(
        BulkStatusRequest request,
        CancellationToken ct)
    {
        if (request.ProductionOrderIds == null || request.ProductionOrderIds.Count == 0)
        {
            return Result.Failure<BulkStatusOutcome>("No production orders selected.");
        }

        var outcome = new BulkStatusOutcome();
        var failures = new List<BulkStatusFailure>();

        foreach (var id in request.ProductionOrderIds)
        {
            if (ct.IsCancellationRequested) break;

            var single = await _orderService.UpdateStatusAsync(
                new UpdateProductionOrderStatusRequest(id, request.NewStatus, request.ModifiedBy),
                ct);

            if (single.IsSuccess)
            {
                outcome.SuccessCount++;
            }
            else
            {
                outcome.FailureCount++;
                failures.Add(new BulkStatusFailure
                {
                    ProductionOrderId = id,
                    ErrorMessage = single.Error ?? "Unknown failure",
                });
            }
        }

        outcome.Failures = failures;
        return Result.Success(outcome);
    }
}
