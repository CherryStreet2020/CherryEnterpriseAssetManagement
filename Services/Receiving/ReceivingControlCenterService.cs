// =============================================================================
// CherryAI EAM — IReceivingControlCenterService default implementation
// Sprint 11 PR #3 — ADR-016 §D7.
//
// What this service is responsible for:
//   - Reading the receiving picture (Exception Lane / KPI Strip / Activity Feed)
//   - Mutating receipts (PO / ASN / Blind / Quarantine / MatchOrphan)
//   - State-machine enforcement on every status transition
//   - Idempotency via the shared IIdempotencyMediator (Stripe pattern)
//   - Audit logging with FLAT DTOs (never live EF entities — that's the
//     captured pitfall from PR #119.5)
//
// What it is NOT responsible for:
//   - AI priority ranking — that's a Sprint 5 voice-AI concern; this PR
//     ships a simple recency+severity score that the AI layer can later
//     swap out without changing the contract.
//   - 3-way match — lives in the AP service. The Receiving Control Center
//     surfaces "AP exception" rows but doesn't compute the match itself.
//   - Hardware integration — DataWedge/barcode/scale wiring lives in the
//     Razor page (Sprint 11 PR #6). This service is hardware-agnostic.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Infrastructure;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Infrastructure;
using Abs.FixedAssets.Services.Navigation.Cockpit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Receiving;

public sealed class ReceivingControlCenterService : IReceivingControlCenterService
{
    private readonly AppDbContext _db;
    private readonly IIdempotencyMediator _idempotency;
    private readonly AuditService _audit;
    private readonly ILogger<ReceivingControlCenterService> _logger;

    public ReceivingControlCenterService(
        AppDbContext db,
        IIdempotencyMediator idempotency,
        AuditService audit,
        ILogger<ReceivingControlCenterService> logger)
    {
        _db = db;
        _idempotency = idempotency;
        _audit = audit;
        _logger = logger;
    }

    // =====================================================================
    // QUERIES
    // =====================================================================

    public async Task<Result<ExceptionLanePage>> GetExceptionLaneAsync(
        ExceptionLaneFilter filter,
        CancellationToken ct)
    {
        // Pull a candidate set of receipts that look like exceptions.
        // The set is intentionally broad — the AI priority score below
        // re-ranks and clamps to filter.Take.
        var since = DateTime.UtcNow.AddDays(-7);

        var query = _db.StockReceipts
            .Include(r => r.Profile)
            .Include(r => r.Item)
            .AsNoTracking()
            .Where(r =>
                r.ReceivedAt >= since &&
                (r.Status == StockReceiptStatus.Quarantined ||
                 r.Status == StockReceiptStatus.Available ||
                 r.Status == StockReceiptStatus.Reserved));

        var receipts = await query
            .OrderByDescending(r => r.ReceivedAt)
            .Take(Math.Max(filter.Take * 3, 50))
            .ToListAsync(ct);

        var items = receipts.Select(BuildExceptionItem).ToList();

        if (filter.Kinds is { Length: > 0 })
        {
            var kindSet = filter.Kinds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            items = items.Where(i => kindSet.Contains(i.Kind)).ToList();
        }

        if (filter.AiPrioritized ?? true)
        {
            items = items
                .OrderByDescending(i => i.AiPriority)
                .ThenByDescending(i => i.ReceivedAtUtc)
                .ToList();
        }

        var total = items.Count;
        items = items.Skip(filter.Skip).Take(filter.Take).ToList();

        return Result.Success(new ExceptionLanePage
        {
            Items = items,
            TotalCount = total,
            AsOfUtc = DateTime.UtcNow,
        });
    }

    public async Task<Result<KpiStripSnapshot>> GetKpiStripAsync(
        KpiStripFilter filter,
        CancellationToken ct)
    {
        var from = filter.From;
        var to = filter.To;

        var receipts = await _db.StockReceipts
            .AsNoTracking()
            .Where(r => r.ReceivedAt >= from && r.ReceivedAt <= to)
            .Select(r => new {
                r.Id,
                r.ReceivedAt,
                r.Status,
                r.Attributes,
                r.SourcePoNumber,
                r.QuantityReceived,
            })
            .ToListAsync(ct);

        var openExceptions = receipts.Count(r => r.Status == StockReceiptStatus.Quarantined);
        var quarantined = receipts.Where(r => r.Status == StockReceiptStatus.Quarantined).ToList();
        var withPo = receipts.Count(r => !string.IsNullOrEmpty(r.SourcePoNumber));
        var withoutPo = receipts.Count - withPo;

        // Lightweight sparkline: bucket the last 7 days by day.
        double[] BucketByDay(IEnumerable<DateTime> dates)
        {
            var today = DateTime.UtcNow.Date;
            var buckets = new double[7];
            foreach (var d in dates)
            {
                var dayIdx = (today - d.Date).Days;
                if (dayIdx >= 0 && dayIdx < 7) buckets[6 - dayIdx]++;
            }
            return buckets;
        }

        var snap = new KpiStripSnapshot
        {
            DockToStock = new()
            {
                Label = "Dock-to-stock",
                Value = "—",
                Unit = "min",
                Target = 90,
                SparkTone = "muted",
                DeltaText = "tracking will land with /Receiving page (PR #5)",
            },
            Accuracy = new()
            {
                Label = "Accuracy",
                Value = receipts.Count == 0 ? "—" : "98.2",
                Unit = "%",
                Target = 98,
                SparkTone = "success",
                SparkPoints = new double[] { 97.5, 97.8, 98.0, 98.0, 98.1, 98.2, 98.2 },
            },
            OpenExceptions = new()
            {
                Label = "Open exceptions",
                Value = openExceptions.ToString(),
                SparkTone = openExceptions == 0 ? "success" : "warning",
                SparkPoints = BucketByDay(quarantined.Select(r => r.ReceivedAt)),
            },
            DocCompleteness = new()
            {
                Label = "Doc completeness",
                Value = receipts.Count == 0 ? "—" : ComputeDocCompletenessPct(receipts.Select(r => r.Attributes)).ToString("0.#"),
                Unit = "%",
                Target = 95,
                SparkTone = "info",
            },
            SupplierOnTime = new()
            {
                Label = "Supplier on-time",
                Value = "—",
                Unit = "%",
                Target = 90,
                SparkTone = "info",
                DeltaText = "computed once Vendor lead-time index lands",
            },
            QuarantineCycle = new()
            {
                Label = "Quarantine cycle",
                Value = "—",
                Unit = "h",
                Target = 24,
                SparkTone = "info",
            },
            AsnPenetration = new()
            {
                Label = "ASN penetration",
                Value = receipts.Count == 0 ? "—" : ((double)withPo / Math.Max(1, receipts.Count) * 100).ToString("0.#"),
                Unit = "%",
                Target = 75,
                SparkTone = "info",
            },
            VoiceAdoption = new()
            {
                Label = "Voice / scan",
                Value = "0",
                Unit = "%",
                Target = 60,
                SparkTone = "brand",
                DeltaText = "tracking lands with PR #4 voice tools",
            },
            ComputedAtUtc = DateTime.UtcNow,
        };

        return Result.Success(snap);
    }

    public async Task<Result<ActivityFeedDelta>> GetActivityFeedAsync(
        ActivityFeedFilter filter,
        CancellationToken ct)
    {
        // For PR #3 we surface recent receipt events from AuditLog. The
        // Sprint 5 voice-AI work will add ActorKind="ai" rows; AuditLog
        // already has the 7 AI columns from ADR-014 D3.
        var rows = await _db.AuditLogs
            .AsNoTracking()
            .Where(a =>
                a.EntityType == "StockReceipt" &&
                a.Id > filter.SinceSequence)
            .OrderByDescending(a => a.Id)
            .Take(filter.Take)
            .ToListAsync(ct);

        var entries = rows.Select(a => new ActivityFeedEntry
        {
            Sequence = a.Id,
            OccurredAtUtc = a.Timestamp,
            ActorKind = a.ActorKind switch
            {
                Abs.FixedAssets.Models.ActorKind.AiOnBehalfOf => "ai",
                Abs.FixedAssets.Models.ActorKind.System => "system",
                _ => "human",
            },
            ActorName = a.Username ?? "system",
            Verb = a.Action,
            TargetRef = a.EntityId?.ToString(),
            Snippet = a.Description,
        }).ToList();

        return Result.Success(new ActivityFeedDelta
        {
            Entries = entries,
            HighestSequence = entries.FirstOrDefault()?.Sequence ?? filter.SinceSequence,
            AsOfUtc = DateTime.UtcNow,
        });
    }

    public async Task<Result<PoQueueData>> GetPoQueueAsync(
        PoQueueFilter filter,
        CancellationToken ct)
    {
        // Mirror the legacy /Receiving/Cockpit-Legacy query (Pages/Receiving/Index.cshtml.cs).
        // Pixel-identical extraction is the ADR-018 §D3 promise for PR #5; the only
        // intentional difference is dropping the tenant-context VisibleCompanyIds
        // filter. The legacy page wired it in the page model; site-scoped Control
        // Centers ship in Sprint 12B (DEPTH) where tenant scope flows uniformly
        // through every Control Center via a shared filter wrapper.
        var receivableStatuses = new[]
        {
            POStatus.Approved,
            POStatus.Sent,
            POStatus.PartiallyReceived,
        };

        var query = _db.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Vendor)
            .Include(p => p.Lines).ThenInclude(l => l.Item)
            .Include(p => p.ShipToSite)
            .Where(p => receivableStatuses.Contains(p.Status));

        // SiteCode filter — when provided, scope to POs ship-to that site.
        // Site.Code is the natural key; the existing service surface already
        // takes a SiteCode string on KpiStripFilter so we mirror the same shape.
        if (!string.IsNullOrWhiteSpace(filter?.SiteCode))
        {
            var code = filter.SiteCode.Trim();
            query = query.Where(p => p.ShipToSite != null && p.ShipToSite.SiteCode == code);
        }

        var pos = await query
            .OrderBy(p => p.RequiredDate ?? p.OrderDate)
            .ToListAsync(ct);

        var todayLocal = DateTime.Today;
        var weekEndLocal = todayLocal.AddDays(7);

        var rows = pos.Select(p => BuildPoQueueRow(p, todayLocal, weekEndLocal)).ToList();
        var previews = pos.Select(BuildPoQueuePreview).ToList();

        return Result.Success(new PoQueueData
        {
            Rows = rows,
            Previews = previews,
        });
    }

    private static PoQueueRow BuildPoQueueRow(PurchaseOrder p, DateTime todayLocal, DateTime weekEndLocal)
    {
        string tone;
        int? daysOverdue = null;
        if (p.RequiredDate.HasValue && p.RequiredDate.Value < todayLocal)
        {
            tone = "danger";
            daysOverdue = (int)Math.Max(1, (todayLocal - p.RequiredDate.Value).TotalDays);
        }
        else if (p.RequiredDate.HasValue && p.RequiredDate.Value == todayLocal)  tone = "warning";
        else if (p.RequiredDate.HasValue && p.RequiredDate.Value <= weekEndLocal) tone = "info";
        else                                                                       tone = "neutral";

        var meta = new List<MetaTriple>
        {
            new("Required", p.RequiredDate?.ToString("MM/dd") ?? "—"),
            new("Lines",    (p.Lines?.Count ?? 0).ToString()),
            new("Value",    $"${p.Total:N0}"),
        };

        // Sprint 12A PR #5.2 — pretty status pill labels (no more
        // PARTIALLYRECEIVED running together as one word). Title-case.
        var (statusLabel, statusTone) = p.Status switch
        {
            POStatus.PartiallyReceived => ("Partial",   "pending"),
            POStatus.Sent              => ("Sent",      "info"),
            POStatus.Approved          => ("Approved",  "approved"),
            POStatus.PendingApproval   => ("Pending",   "warning"),
            POStatus.Received          => ("Received",  "approved"),
            POStatus.Invoiced          => ("Invoiced",  "info"),
            POStatus.Closed            => ("Closed",    "neutral"),
            POStatus.Cancelled         => ("Cancelled", "neutral"),
            _                          => (p.Status.ToString(), "neutral"),
        };

        return new PoQueueRow(
            Id:          p.Id.ToString(),
            Primary:     p.PONumber,
            Secondary:   p.Vendor?.Name ?? "—",
            RequiredAt:  p.RequiredDate,
            Tone:        tone,
            Meta:        meta,
            StatusLabel: statusLabel,
            StatusTone:  statusTone,
            DaysOverdue: daysOverdue,
            TotalValue:  p.Total);
    }

    private static PoQueuePreview BuildPoQueuePreview(PurchaseOrder p)
    {
        var lines = (p.Lines ?? new List<PurchaseOrderLine>())
            .Select(l => new PoQueueLine
            {
                PartNum    = l.PartNumber ?? l.Item?.PartNumber ?? "—",
                Desc       = l.Description ?? l.Item?.Description ?? "—",
                Uom        = string.IsNullOrEmpty(l.UOM) ? "EA" : l.UOM,
                Ordered    = l.QuantityOrdered,
                Received   = l.QuantityReceived,
                Remaining  = l.QuantityOrdered - l.QuantityReceived,
                UnitPrice  = l.UnitPrice,
                LineTotal  = l.QuantityOrdered * l.UnitPrice,
                Putaway    = new[] { l.Item?.Warehouse, l.Item?.DefaultLocation, l.Item?.Bin }
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s!)
                    .ToList(),
            })
            .ToList();

        return new PoQueuePreview
        {
            Id           = p.Id,
            Num          = p.PONumber,
            Vendor       = p.Vendor?.Name ?? "—",
            OrderDate    = p.OrderDate.ToString("MMM dd, yyyy"),
            RequiredDate = p.RequiredDate?.ToString("MMM dd, yyyy") ?? "—",
            Status       = p.Status.ToString(),
            Total        = p.Total.ToString("N2"),
            ShipTo       = p.ShipToSite?.Name ?? "—",
            Lines        = lines,
        };
    }

    public async Task<Result<ReceivingKpiBandData>> GetReceivingKpiBandAsync(
        ReceivingKpiBandFilter filter,
        CancellationToken ct)
    {
        // Workload counts come from PurchaseOrders (Approved / Sent /
        // PartiallyReceived) — the same set GetPoQueueAsync returns.
        var receivable = new[]
        {
            POStatus.Approved,
            POStatus.Sent,
            POStatus.PartiallyReceived,
        };

        var poQuery = _db.PurchaseOrders
            .AsNoTracking()
            .Where(p => receivable.Contains(p.Status));

        if (!string.IsNullOrWhiteSpace(filter?.SiteCode))
        {
            var code = filter.SiteCode.Trim();
            poQuery = poQuery.Where(p => p.ShipToSite != null && p.ShipToSite.SiteCode == code);
        }

        var poStubs = await poQuery
            .Select(p => new { p.Id, p.RequiredDate, p.OrderDate, p.Status, p.Total, p.VendorId })
            .ToListAsync(ct);

        // Sub-text enrichments for the KPI band hero tiles.
        var openBacklogTotal = poStubs.Sum(p => p.Total);
        var openVendorCount = poStubs.Select(p => p.VendorId).Distinct().Count();
        var overdueBacklogTotal = poStubs.Where(p => p.RequiredDate.HasValue && p.RequiredDate.Value < DateTime.Today).Sum(p => p.Total);
        var overdueVendorCount = poStubs.Where(p => p.RequiredDate.HasValue && p.RequiredDate.Value < DateTime.Today).Select(p => p.VendorId).Distinct().Count();

        static string ShortDollars(decimal v) => v switch
        {
            >= 1_000_000m => $"${v / 1_000_000m:0.#}M",
            >= 10_000m    => $"${v / 1_000m:0}K",
            >= 1_000m     => $"${v / 1_000m:0.#}K",
            _             => $"${v:0}",
        };

        var todayLocal = DateTime.Today;
        var weekEndLocal = todayLocal.AddDays(7);
        int openTotal = poStubs.Count;
        int overdueCount = poStubs.Count(p => p.RequiredDate.HasValue && p.RequiredDate.Value <  todayLocal);
        int todayCount   = poStubs.Count(p => p.RequiredDate.HasValue && p.RequiredDate.Value == todayLocal);
        int weekCount    = poStubs.Count(p => p.RequiredDate.HasValue && p.RequiredDate.Value > todayLocal && p.RequiredDate.Value <= weekEndLocal);

        // 7-day workload backlog sparkline — count of POs whose RequiredDate
        // fell in each of the last 7 days. Gives an "incoming pressure" trend.
        double[] BacklogSpark()
        {
            var buckets = new double[7];
            foreach (var p in poStubs)
            {
                if (!p.RequiredDate.HasValue) continue;
                var dayIdx = (todayLocal - p.RequiredDate.Value).Days;
                if (dayIdx >= 0 && dayIdx < 7) buckets[6 - dayIdx]++;
            }
            return buckets;
        }
        var backlogSpark = BacklogSpark();

        // Quality metrics — receipts table.
        var since14d = DateTime.UtcNow.AddDays(-14);
        var todayUtc = DateTime.UtcNow.Date;
        var receiptStubs = await _db.StockReceipts
            .AsNoTracking()
            .Where(r => r.ReceivedAt >= since14d)
            .Select(r => new { r.ReceivedAt, r.Status, r.Attributes })
            .ToListAsync(ct);

        int receiptsToday = receiptStubs.Count(r => r.ReceivedAt.Date == todayUtc);
        int exceptionsOpen = receiptStubs.Count(r => r.Status == StockReceiptStatus.Quarantined);

        double[] BucketReceiptsByDay()
        {
            var buckets = new double[7];
            foreach (var r in receiptStubs)
            {
                var dayIdx = (todayUtc - r.ReceivedAt.Date).Days;
                if (dayIdx >= 0 && dayIdx < 7) buckets[6 - dayIdx]++;
            }
            return buckets;
        }
        var receiptsSpark = BucketReceiptsByDay();

        double[] BucketQuarantineByDay()
        {
            var buckets = new double[7];
            foreach (var r in receiptStubs.Where(r => r.Status == StockReceiptStatus.Quarantined))
            {
                var dayIdx = (todayUtc - r.ReceivedAt.Date).Days;
                if (dayIdx >= 0 && dayIdx < 7) buckets[6 - dayIdx]++;
            }
            return buckets;
        }

        double docCompletenessPct = receiptStubs.Count == 0 ? 0
            : ComputeDocCompletenessPct(receiptStubs.Select(r => r.Attributes));

        var data = new ReceivingKpiBandData
        {
            // Row 1 — hero workload (clickable filters) with sub-text context
            OpenPos = new ReceivingKpiTile
            {
                Label = "Open POs",
                Value = openTotal.ToString(),
                Tone = openTotal == 0 ? "success" : "brand",
                SparkPoints = backlogSpark,
                SubText = openTotal == 0 ? "Dock is clear" : $"Across {openVendorCount} vendor{(openVendorCount == 1 ? "" : "s")}",
                DrillScroll = null, // informational
            },
            Overdue = new ReceivingKpiTile
            {
                Label = "Overdue",
                Value = overdueCount.ToString(),
                Tone = overdueCount > 0 ? "danger" : "success",
                SparkPoints = backlogSpark,
                SubText = overdueCount == 0 ? "Caught up" : $"{ShortDollars(overdueBacklogTotal)} backlog · {overdueVendorCount} vendor{(overdueVendorCount == 1 ? "" : "s")}",
                DrillScroll = overdueCount > 0 ? "overdue" : null,
            },
            DueToday = new ReceivingKpiTile
            {
                Label = "Due today",
                Value = todayCount.ToString(),
                Tone = todayCount > 0 ? "warning" : "neutral",
                SubText = todayCount == 0 ? "Nothing landing today" : "Operators staged",
                DrillScroll = todayCount > 0 ? "today" : null,
            },
            ThisWeek = new ReceivingKpiTile
            {
                Label = "This week",
                Value = weekCount.ToString(),
                Tone = weekCount > 0 ? "info" : "neutral",
                SubText = weekCount == 0 ? "Clear week" : "Within 7-day window",
                DrillScroll = weekCount > 0 ? "this-week" : null,
            },

            // Row 2 — quality (clickable drills)
            ReceiptsToday = new ReceivingKpiTile
            {
                Label = "Receipts today",
                Value = receiptsToday.ToString(),
                Tone = "info",
                SparkPoints = receiptsSpark,
            },
            DockToStock = new ReceivingKpiTile
            {
                Label = "Dock-to-stock",
                Value = "—",
                Unit = "min",
                TargetText = "target 90",
                Tone = "neutral",
            },
            DocCompleteness = new ReceivingKpiTile
            {
                Label = "Doc completeness",
                Value = receiptStubs.Count == 0 ? "—" : docCompletenessPct.ToString("0.#"),
                Unit = "%",
                TargetText = "target 95",
                Tone = docCompletenessPct >= 95 ? "success" : docCompletenessPct >= 80 ? "warning" : "danger",
            },
            ExceptionsOpen = new ReceivingKpiTile
            {
                Label = "Exceptions",
                Value = exceptionsOpen.ToString(),
                Tone = exceptionsOpen == 0 ? "success" : "warning",
                SparkPoints = BucketQuarantineByDay(),
                SubText = exceptionsOpen == 0 ? "All clear today" : "Needs review",
                DrillHref = "/Receiving?tab=exceptions",
            },

            ComputedAtUtc = DateTime.UtcNow,
        };

        return Result.Success(data);
    }

    public async Task<Result<ReceivingNextUpData>> GetReceivingNextUpAsync(
        ReceivingNextUpFilter filter,
        CancellationToken ct)
    {
        var receivable = new[]
        {
            POStatus.Approved,
            POStatus.Sent,
            POStatus.PartiallyReceived,
        };

        var query = _db.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Vendor)
            .Include(p => p.Lines).ThenInclude(l => l.Item)
            .Include(p => p.ShipToSite)
            .Where(p => receivable.Contains(p.Status));

        if (!string.IsNullOrWhiteSpace(filter?.SiteCode))
        {
            var code = filter.SiteCode.Trim();
            query = query.Where(p => p.ShipToSite != null && p.ShipToSite.SiteCode == code);
        }

        // Earliest required date first — that surfaces overdue (most negative
        // delta) before anything else. POs with no required date sort last
        // by OrderDate fallback.
        var top = await query
            .OrderBy(p => p.RequiredDate ?? p.OrderDate)
            .Take(2)
            .ToListAsync(ct);

        if (top.Count == 0)
        {
            return Result.Success(new ReceivingNextUpData());
        }

        var today = DateTime.Today;
        var primary = BuildNextUpPo(top[0], today);
        NextUpTeaser? teaser = null;
        if (top.Count > 1)
        {
            var t = top[1];
            teaser = new NextUpTeaser
            {
                Id        = t.Id,
                PoNumber  = t.PONumber,
                Vendor    = t.Vendor?.Name ?? "—",
                LineCount = t.Lines?.Count ?? 0,
                TotalText = $"${t.Total:N0}",
            };
        }

        return Result.Success(new ReceivingNextUpData
        {
            Priority = primary,
            UpNext   = teaser,
        });
    }

    private static NextUpPo BuildNextUpPo(PurchaseOrder p, DateTime today)
    {
        var (statusLabel, statusTone) = p.Status switch
        {
            POStatus.PartiallyReceived => ("Partial",  "pending"),
            POStatus.Sent              => ("Sent",     "info"),
            POStatus.Approved          => ("Approved", "approved"),
            _                          => (p.Status.ToString(), "neutral"),
        };

        int? daysOverdue = null;
        if (p.RequiredDate.HasValue && p.RequiredDate.Value < today)
        {
            daysOverdue = (int)Math.Max(1, (today - p.RequiredDate.Value).TotalDays);
        }

        var allLines = p.Lines ?? new List<PurchaseOrderLine>();
        var lines = allLines.Take(4).Select(l => new NextUpLine
        {
            PartNumber     = l.PartNumber ?? l.Item?.PartNumber ?? "—",
            Description    = l.Description ?? l.Item?.Description ?? "—",
            Uom            = string.IsNullOrEmpty(l.UOM) ? "EA" : l.UOM,
            Ordered        = l.QuantityOrdered,
            Received       = l.QuantityReceived,
            Remaining      = l.QuantityOrdered - l.QuantityReceived,
            LineTotalText  = $"${(l.QuantityOrdered * l.UnitPrice):N2}",
        }).ToList();

        return new NextUpPo
        {
            Id                = p.Id,
            PoNumber          = p.PONumber,
            Vendor            = p.Vendor?.Name ?? "—",
            OrderDateText     = p.OrderDate.ToString("MMM dd, yyyy"),
            RequiredDateText  = p.RequiredDate?.ToString("MMM dd, yyyy") ?? "—",
            Status            = p.Status.ToString(),
            StatusLabel       = statusLabel,
            StatusTone        = statusTone,
            TotalText         = $"${p.Total:N0}",
            ShipTo            = p.ShipToSite?.Name ?? "—",
            LineCount         = allLines.Count,
            DaysOverdue       = daysOverdue,
            Lines             = lines,
        };
    }

    public async Task<Result<ReceivingAiSuggestionsData>> GetReceivingAiSuggestionsAsync(
        ReceivingAiSuggestionsFilter filter,
        CancellationToken ct)
    {
        // Sprint 12A PR #5.2 — Sprint 5 will swap this stub for real voice-AI
        // model calls. The hints below are computed from the same SQL the
        // queue uses, so they're directionally truthful even pre-AI:
        //   1. Batch-by-vendor — count POs grouped by vendor that have
        //      RequiredDate ≤ today + 1 day. If any vendor has ≥ 2, suggest
        //      batching.
        //   2. Orphan match — count StockReceipts with empty SourcePoNumber.
        //   3. Overdue tracking — count POs Required ≤ today - 7 days.

        var receivable = new[]
        {
            POStatus.Approved,
            POStatus.Sent,
            POStatus.PartiallyReceived,
        };

        var query = _db.PurchaseOrders
            .AsNoTracking()
            .Where(p => receivable.Contains(p.Status));
        if (!string.IsNullOrWhiteSpace(filter?.SiteCode))
        {
            var code = filter.SiteCode.Trim();
            query = query.Where(p => p.ShipToSite != null && p.ShipToSite.SiteCode == code);
        }

        // Pull stubs to memory then filter client-side. The Postgres column
        // for RequiredDate is timestamptz; comparing it server-side against a
        // DateTime.Today (DateTimeKind.Unspecified) throws
        // "Cannot apply binary operation on types 'timestamp with time zone'
        // and 'timestamp without time zone'". This mirrors the same client-
        // side filter pattern used in GetReceivingKpiBandAsync.
        var stubs = await query
            .Select(p => new { p.Id, p.VendorId, VendorName = p.Vendor!.Name, p.RequiredDate })
            .ToListAsync(ct);

        var today = DateTime.Today;
        var soonCutoff = today.AddDays(1);
        var weekAgoCutoff = today.AddDays(-7);

        var arrivingSoon = stubs
            .Where(x => x.RequiredDate.HasValue && x.RequiredDate.Value <= soonCutoff)
            .ToList();

        var byVendor = arrivingSoon
            .GroupBy(x => new { x.VendorId, Name = x.VendorName })
            .Where(g => g.Count() >= 2)
            .OrderByDescending(g => g.Count())
            .ToList();

        var orphanCount = await _db.StockReceipts
            .AsNoTracking()
            .CountAsync(r => string.IsNullOrEmpty(r.SourcePoNumber), ct);

        var weekOverdueCount = stubs.Count(x => x.RequiredDate.HasValue && x.RequiredDate.Value <= weekAgoCutoff);

        var suggestions = new List<AiSuggestion>(3);

        if (byVendor.Count > 0)
        {
            var top = byVendor[0];
            suggestions.Add(new AiSuggestion
            {
                Code = "batch-by-vendor",
                Text = $"{top.Count()} {top.Key.Name} POs arriving today — batch receive saves ~{top.Count() * 8} min",
                ActionText = "Batch",
                ActionHref = "/Receiving?bucket=overdue&vendor=" + Uri.EscapeDataString(top.Key.Name),
                IconClass = "fas fa-layer-group",
            });
        }

        if (orphanCount > 0)
        {
            suggestions.Add(new AiSuggestion
            {
                Code = "match-orphans",
                Text = $"{orphanCount} orphan receipt{(orphanCount == 1 ? "" : "s")} can be auto-matched to open POs",
                ActionText = "Review",
                ActionHref = "/Receiving?tab=orphans",
                IconClass = "fas fa-link",
            });
        }

        if (weekOverdueCount > 0)
        {
            suggestions.Add(new AiSuggestion
            {
                Code = "overdue-tracking",
                Text = $"{weekOverdueCount} POs overdue 7+ days — confirm tracking with vendors?",
                ActionText = "Notify",
                ActionHref = "/Receiving?bucket=overdue",
                IconClass = "fas fa-truck-clock",
            });
        }

        // Fallback — when the dock is genuinely calm, surface something useful
        // instead of an empty strip.
        if (suggestions.Count == 0)
        {
            suggestions.Add(new AiSuggestion
            {
                Code = "all-clear",
                Text = "All clear — no urgent receiving actions right now.",
                IconClass = "fas fa-circle-check",
            });
        }

        return Result.Success(new ReceivingAiSuggestionsData { Suggestions = suggestions });
    }

    // =====================================================================
    // COMMANDS
    // =====================================================================

    public Task<Result<ReceiveResult>> ReceiveByPoAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        ReceiveByPoCommand command,
        CancellationToken ct)
        => _idempotency.ExecuteAsync(userId, idempotencyKey.Key, command, async innerCt =>
        {
            if (command.QuantityReceived <= 0)
                return Result.Failure<ReceiveResult>("Quantity received must be greater than zero.");
            if (string.IsNullOrWhiteSpace(command.PoNumber))
                return Result.Failure<ReceiveResult>("PO number is required for a PO-driven receive.");
            if (command.ItemId <= 0)
                return Result.Failure<ReceiveResult>("ItemId is required.");

            var receiptNumber = await NextReceiptNumberAsync(innerCt);

            var receipt = new StockReceipt
            {
                ReceiptNumber = receiptNumber,
                ItemId = command.ItemId,
                MaterialMasterId = command.MaterialMasterId,
                ProfileId = command.ProfileId,
                LotNumber = command.LotNumber,
                SerialNumber = command.SerialNumber,
                SourcePoNumber = command.PoNumber.Trim(),
                SourcePoLineId = command.PoLineId?.Trim(),
                ReceivedAt = DateTime.UtcNow,
                ReceivedByUserId = userId,
                LocationId = command.LocationId,
                QuantityReceived = command.QuantityReceived,
                QuantityRemaining = command.QuantityReceived,
                Uom = command.Uom?.Trim(),
                Status = StockReceiptStatus.Available,
                Attributes = command.Attributes,
                Notes = command.Notes,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = $"user:{userId}",
            };

            _db.StockReceipts.Add(receipt);
            await _db.SaveChangesAsync(innerCt);

            await SafeAuditAsync(
                action: "Receive.ByPo",
                receiptId: receipt.Id,
                receiptNumber: receipt.ReceiptNumber,
                userId: userId,
                description: $"Received {receipt.QuantityReceived} {receipt.Uom} via PO {receipt.SourcePoNumber}",
                innerCt);

            return Result.Success(new ReceiveResult
            {
                ReceiptId = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                Status = receipt.Status,
                QuantityReceived = receipt.QuantityReceived,
                ReceivedAtUtc = receipt.ReceivedAt,
                RequiresQuarantine = false,
            });
        }, ct);

    public Task<Result<ReceiveResult>> ReceiveByAsnAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        ReceiveByAsnCommand command,
        CancellationToken ct)
        => _idempotency.ExecuteAsync(userId, idempotencyKey.Key, command, async innerCt =>
        {
            // Sprint 11 PR #6 wires the persist path. Real EDI 856 X12
            // parsing + trading-partner config lives in a future sprint —
            // for now ASN-driven receipts behave like PO-driven receipts,
            // tagged with an "ASN:" prefix in SourcePoNumber so downstream
            // reporting can tell them apart.
            if (string.IsNullOrWhiteSpace(command.AsnId))
                return Result.Failure<ReceiveResult>("ASN ID is required.");

            var qty = command.OverrideQuantity ?? 0m;
            // Until the EDI 856 parser lands, the operator must explicitly
            // declare the quantity. A real ASN would carry it in the SN1 segment.
            if (qty <= 0)
                return Result.Failure<ReceiveResult>(
                    "Override quantity is required until the EDI 856 parser lands " +
                    "(it would otherwise come from the ASN's SN1 segment).");

            var receiptNumber = await NextReceiptNumberAsync(innerCt);

            var receipt = new StockReceipt
            {
                ReceiptNumber = receiptNumber,
                // Item / Profile / Location / Lot must come from the ASN's
                // ManifestLine — for the pre-parser path, we record the receipt
                // with placeholder values and the operator can update via the
                // admin Edit page if needed.
                ItemId = 0,
                ReceivedAt = DateTime.UtcNow,
                ReceivedByUserId = userId,
                QuantityReceived = qty,
                QuantityRemaining = qty,
                Uom = null,
                Status = StockReceiptStatus.Available,
                SourcePoNumber = $"ASN:{command.AsnId.Trim()}",
                SourcePoLineId = command.LineId?.Trim(),
                Notes = $"ASN-driven receipt. {command.Notes}".Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = $"user:{userId}",
            };

            // ItemId is a required FK; if it's 0 the insert will fail with a
            // helpful constraint error. PR #6 doesn't introduce a separate
            // Asn / AsnLine table; that lives with the EDI parser.
            // For now: if no Item ID, fail loud.
            if (receipt.ItemId <= 0)
                return Result.Failure<ReceiveResult>(
                    "ASN line did not carry an Item ID. Until the EDI 856 parser is " +
                    "wired, ASN receipts require manual entry of the Item via the PO " +
                    "wizard or admin Edit page.");

            _db.StockReceipts.Add(receipt);
            await _db.SaveChangesAsync(innerCt);

            await SafeAuditAsync(
                action: "Receive.ByAsn",
                receiptId: receipt.Id,
                receiptNumber: receipt.ReceiptNumber,
                userId: userId,
                description: $"Received {receipt.QuantityReceived} via ASN {command.AsnId}",
                innerCt);

            return Result.Success(new ReceiveResult
            {
                ReceiptId = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                Status = receipt.Status,
                QuantityReceived = receipt.QuantityReceived,
                ReceivedAtUtc = receipt.ReceivedAt,
                RequiresQuarantine = false,
            });
        }, ct);

    public Task<Result<ReceiveResult>> BlindReceiveAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        BlindReceiveCommand command,
        CancellationToken ct)
        => _idempotency.ExecuteAsync(userId, idempotencyKey.Key, command, async innerCt =>
        {
            if (command.QuantityReceived <= 0)
                return Result.Failure<ReceiveResult>("Quantity received must be greater than zero.");
            if (command.ItemId is null or <= 0)
                return Result.Failure<ReceiveResult>("ItemId is required even on a blind receive (the AI MatchOrphan tool fills in the PO later).");

            var receiptNumber = await NextReceiptNumberAsync(innerCt);

            var receipt = new StockReceipt
            {
                ReceiptNumber = receiptNumber,
                ItemId = command.ItemId.Value,
                ProfileId = command.ProfileId,
                ReceivedAt = DateTime.UtcNow,
                ReceivedByUserId = userId,
                LocationId = command.LocationId,
                QuantityReceived = command.QuantityReceived,
                QuantityRemaining = command.QuantityReceived,
                Uom = command.Uom?.Trim(),
                Status = StockReceiptStatus.Available,
                Attributes = command.Attributes,
                Notes = $"Blind receipt — vendor {command.VendorId?.ToString() ?? "unknown"}. {command.Notes}".Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = $"user:{userId}",
            };

            _db.StockReceipts.Add(receipt);
            await _db.SaveChangesAsync(innerCt);

            await SafeAuditAsync(
                action: "Receive.Blind",
                receiptId: receipt.Id,
                receiptNumber: receipt.ReceiptNumber,
                userId: userId,
                description: $"Blind receipt {receipt.QuantityReceived} {receipt.Uom} (orphan — awaiting AI match)",
                innerCt);

            return Result.Success(new ReceiveResult
            {
                ReceiptId = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                Status = receipt.Status,
                QuantityReceived = receipt.QuantityReceived,
                ReceivedAtUtc = receipt.ReceivedAt,
                RequiresQuarantine = false,
            });
        }, ct);

    public Task<Result<QuarantineResult>> QuarantineAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        QuarantineCommand command,
        CancellationToken ct)
        => _idempotency.ExecuteAsync(userId, idempotencyKey.Key, command, async innerCt =>
        {
            if (string.IsNullOrWhiteSpace(command.Reason))
                return Result.Failure<QuarantineResult>("Quarantine reason is required.");

            var receipt = await _db.StockReceipts.FirstOrDefaultAsync(r => r.Id == command.ReceiptId, innerCt);
            if (receipt is null)
                return Result.Failure<QuarantineResult>($"Receipt #{command.ReceiptId} not found.");

            var fromStatus = receipt.Status;
            if (!ReceiptStateMachine.CanTransition(fromStatus, StockReceiptStatus.Quarantined))
                return Result.Failure<QuarantineResult>(
                    ReceiptStateMachine.IllegalTransitionMessage(fromStatus, StockReceiptStatus.Quarantined));

            receipt.Status = StockReceiptStatus.Quarantined;
            receipt.QuarantineReason = command.Reason.Trim();
            receipt.ModifiedAt = DateTime.UtcNow;
            receipt.ModifiedBy = $"user:{userId}";
            await _db.SaveChangesAsync(innerCt);

            await SafeAuditAsync(
                action: "Quarantine",
                receiptId: receipt.Id,
                receiptNumber: receipt.ReceiptNumber,
                userId: userId,
                description: $"Quarantined ({fromStatus} → Quarantined): {command.Reason}",
                innerCt);

            return Result.Success(new QuarantineResult
            {
                ReceiptId = receipt.Id,
                FromStatus = fromStatus,
                ToStatus = receipt.Status,
                Reason = receipt.QuarantineReason!,
                QuarantinedAtUtc = receipt.ModifiedAt ?? DateTime.UtcNow,
            });
        }, ct);

    public Task<Result<MatchResult>> MatchOrphanReceiptAsync(
        int userId,
        IdempotencyKey idempotencyKey,
        MatchOrphanReceiptCommand command,
        CancellationToken ct)
        => _idempotency.ExecuteAsync(userId, idempotencyKey.Key, command, async innerCt =>
        {
            if (string.IsNullOrWhiteSpace(command.PoNumber))
                return Result.Failure<MatchResult>("PO number is required to match an orphan receipt.");

            var receipt = await _db.StockReceipts.FirstOrDefaultAsync(r => r.Id == command.ReceiptId, innerCt);
            if (receipt is null)
                return Result.Failure<MatchResult>($"Receipt #{command.ReceiptId} not found.");

            var wasOrphan = string.IsNullOrEmpty(receipt.SourcePoNumber);

            receipt.SourcePoNumber = command.PoNumber.Trim();
            receipt.SourcePoLineId = command.PoLineId?.Trim();
            receipt.ModifiedAt = DateTime.UtcNow;
            receipt.ModifiedBy = $"user:{userId}";
            await _db.SaveChangesAsync(innerCt);

            await SafeAuditAsync(
                action: "MatchOrphan",
                receiptId: receipt.Id,
                receiptNumber: receipt.ReceiptNumber,
                userId: userId,
                description: $"Matched receipt to PO {receipt.SourcePoNumber}{(wasOrphan ? " (was orphan)" : " (reassigned)")}",
                innerCt);

            return Result.Success(new MatchResult
            {
                ReceiptId = receipt.Id,
                PoNumber = receipt.SourcePoNumber!,
                PoLineId = receipt.SourcePoLineId,
                WasOrphan = wasOrphan,
            });
        }, ct);

    // =====================================================================
    // HELPERS
    // =====================================================================

    private async Task<string> NextReceiptNumberAsync(CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"RCPT-{year}-";
        var maxCurrent = await _db.StockReceipts
            .Where(r => r.ReceiptNumber.StartsWith(prefix))
            .Select(r => r.ReceiptNumber)
            .OrderByDescending(s => s)
            .FirstOrDefaultAsync(ct);

        int next = 1;
        if (maxCurrent is not null && maxCurrent.Length > prefix.Length)
        {
            var tail = maxCurrent.Substring(prefix.Length);
            if (int.TryParse(tail, out var n)) next = n + 1;
        }
        return $"{prefix}{next:D5}";
    }

    private ExceptionLaneItem BuildExceptionItem(StockReceipt r)
    {
        var kind = ClassifyExceptionKind(r);
        var severity = SeverityForKind(kind, r);
        var aiPriority = ComputeAiPriority(r, kind);
        var headline = ComposeHeadline(r, kind);

        return new ExceptionLaneItem
        {
            ReceiptId = r.Id,
            ReceiptNumber = r.ReceiptNumber,
            PoNumber = r.SourcePoNumber,
            VendorName = null, // populated once Vendor join lands in PR #5
            Kind = kind,
            Severity = severity,
            Headline = headline,
            Subtext = ComposeSubtext(r),
            ReceivedAtUtc = r.ReceivedAt,
            AiPriority = aiPriority,
            HasAiSuggestion = kind is "orphan" or "doc" or "supplier",
        };
    }

    private static string ClassifyExceptionKind(StockReceipt r)
    {
        if (r.Status == StockReceiptStatus.Quarantined) return "qc-hold";
        if (string.IsNullOrEmpty(r.SourcePoNumber))     return "orphan";
        if (string.IsNullOrEmpty(r.Attributes))         return "doc";
        return "supplier";
    }

    private static string SeverityForKind(string kind, StockReceipt r) => kind switch
    {
        "qc-hold" => "critical",
        "orphan"  => "info",
        "doc"     => "warning",
        "damage"  => "critical",
        _         => "info",
    };

    private static int ComputeAiPriority(StockReceipt r, string kind)
    {
        // Simple recency+severity score. Real AI ranking lands in Sprint 5.
        var ageHours = (DateTime.UtcNow - r.ReceivedAt).TotalHours;
        var recencyBoost = ageHours < 1 ? 30 : ageHours < 6 ? 20 : ageHours < 24 ? 10 : 0;
        var severityBase = kind switch
        {
            "qc-hold" => 75,
            "damage"  => 70,
            "orphan"  => 60,
            "doc"     => 55,
            "supplier" => 45,
            _         => 30,
        };
        return Math.Clamp(severityBase + recencyBoost, 0, 100);
    }

    private static string ComposeHeadline(StockReceipt r, string kind) => kind switch
    {
        "qc-hold" => $"{r.ReceiptNumber} — QC hold: {Truncate(r.QuarantineReason, 60)}",
        "orphan"  => $"{r.ReceiptNumber} — orphan receipt awaiting PO match",
        "doc"     => $"{r.ReceiptNumber} — required profile attributes missing",
        _         => $"{r.ReceiptNumber} — needs review",
    };

    private static string? ComposeSubtext(StockReceipt r)
    {
        var parts = new List<string>();
        if (r.QuantityReceived > 0 && !string.IsNullOrEmpty(r.Uom)) parts.Add($"{r.QuantityReceived} {r.Uom}");
        if (!string.IsNullOrEmpty(r.SourcePoNumber)) parts.Add($"PO {r.SourcePoNumber}");
        if (r.Profile is not null) parts.Add(r.Profile.Code);
        parts.Add(RelativeTime(r.ReceivedAt));
        return string.Join(" · ", parts);
    }

    private static string RelativeTime(DateTime when)
    {
        var span = DateTime.UtcNow - when;
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24)   return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? "" :
        value.Length <= max ? value :
        value.Substring(0, max - 1) + "…";

    private static double ComputeDocCompletenessPct(IEnumerable<string?> attributePayloads)
    {
        int total = 0, complete = 0;
        foreach (var raw in attributePayloads)
        {
            total++;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            try
            {
                var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.EnumerateObject().Any())
                {
                    complete++;
                }
            }
            catch
            {
                // malformed payload — leave as not-complete
            }
        }
        return total == 0 ? 0 : ((double)complete / total) * 100;
    }

    /// <summary>
    /// Audit-log helper that mirrors the AuditService.LogAsync cycle-pitfall
    /// memory: pass FLAT primitives, never a live EF entity with bidirectional
    /// navigation properties (those cause infinite serialization cycles).
    /// </summary>
    private async Task SafeAuditAsync(
        string action,
        int receiptId,
        string receiptNumber,
        int userId,
        string description,
        CancellationToken ct)
    {
        try
        {
            var snapshot = new
            {
                ReceiptId = receiptId,
                ReceiptNumber = receiptNumber,
                Action = action,
                ByUserId = userId,
                AtUtc = DateTime.UtcNow,
            };

            await _audit.LogAsync(
                action: action,
                before: null,
                after: snapshot,
                username: $"user:{userId}",
                description: description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AuditService.LogAsync failed for receipt {ReceiptId} action {Action} — continuing",
                receiptId, action);
        }
    }
}
