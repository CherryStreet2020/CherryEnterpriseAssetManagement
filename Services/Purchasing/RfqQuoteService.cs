// Sprint 15.4 PR-20 — IRfqQuoteService implementation (CLOSES the cascade).
//
// Patterns: ITenantContext gating, Result envelope, two-phase numbering
// (RFQ-YYYY-NNNNNN), transactional multi-row writes. The composite ranker
// composes PR-18 ISupplierPerformanceService.GetCompositeInputsAsync for the
// SupplierOTD dimension and falls back gracefully when a supplier has no
// snapshot. ConvertQuoteToPoLine builds a Draft PO + lines + demand links
// atomically, mirroring PurchasingService's PO-creation shape.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Purchasing;

public class RfqQuoteService : IRfqQuoteService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISupplierPerformanceService _supplierPerformance;
    private readonly ILogger<RfqQuoteService> _logger;

    public RfqQuoteService(
        AppDbContext db,
        ITenantContext tenantContext,
        ISupplierPerformanceService supplierPerformance,
        ILogger<RfqQuoteService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _supplierPerformance = supplierPerformance;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1) CreateRfqAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<CreateRfqResult>> CreateRfqAsync(
        CreateRfqRequest request, DateTime nowUtc, CancellationToken ct = default)
    {
        var companyId = _tenantContext.CompanyId;
        if (companyId == null)
            return Result.Failure<CreateRfqResult>("No tenant company in scope.");
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result.Failure<CreateRfqResult>("RFQ title is required.");
        if (request.Lines == null || request.Lines.Count == 0)
            return Result.Failure<CreateRfqResult>("An RFQ needs at least one line.");

        var existingTx = _db.Database.CurrentTransaction;
        var tx = existingTx ?? await _db.Database.BeginTransactionAsync(ct);
        var ownsTx = existingTx == null;
        try
        {
            var rfq = new SupplierRFQ
            {
                CompanyId = companyId,
                RfqNumber = $"TMP-{Guid.NewGuid():N}",
                Title = request.Title.Trim(),
                Status = RfqStatus.Draft,
                RequiredByDate = request.RequiredByDate,
                CreatedByUserId = request.CreatedByUserId,
                Notes = request.Notes,
                CreatedAt = nowUtc,
            };
            var lineNo = 1;
            foreach (var l in request.Lines)
            {
                rfq.Lines.Add(new SupplierRFQLine
                {
                    LineNumber = lineNo++,
                    ItemId = l.ItemId,
                    PartNumber = l.PartNumber,
                    Description = string.IsNullOrWhiteSpace(l.Description) ? "(no description)" : l.Description.Trim(),
                    UOM = string.IsNullOrWhiteSpace(l.Uom) ? "EA" : l.Uom.Trim(),
                    Quantity = l.Quantity,
                    RequiredDate = l.RequiredDate,
                    ProductionSupplyDemandId = l.ProductionSupplyDemandId,
                    ProductionOrderId = l.ProductionOrderId,
                    BomLineId = l.BomLineId,
                    OperationSequence = l.OperationSequence,
                    CreatedAt = nowUtc,
                });
            }
            _db.Add(rfq);
            await _db.SaveChangesAsync(ct);

            rfq.RfqNumber = $"RFQ-{nowUtc:yyyy}-{rfq.Id:D6}";
            await _db.SaveChangesAsync(ct);
            if (ownsTx) await tx.CommitAsync(ct);

            return Result.Success(new CreateRfqResult(rfq.Id, rfq.RfqNumber, rfq.Lines.Count));
        }
        catch (DbUpdateException dbex)
        {
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            if (IsUniqueViolation(dbex))
                return Result.Failure<CreateRfqResult>("A concurrent RFQ create collided. Retry.");
            _logger.LogError(dbex, "CreateRfqAsync failed.");
            return Result.Failure<CreateRfqResult>("Failed to create RFQ.");
        }
        catch { if (ownsTx) await SafeRollbackAsync(tx, ct); throw; }
        finally { if (ownsTx) await tx.DisposeAsync(); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2) IssueRfqAsync — invite suppliers (one Invited quote each)
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<int>> IssueRfqAsync(
        int rfqId, IReadOnlyList<int> vendorIds, DateTime? quotesDueUtc,
        DateTime nowUtc, CancellationToken ct = default)
    {
        if (vendorIds == null || vendorIds.Count == 0)
            return Result.Failure<int>("At least one supplier is required to issue an RFQ.");

        var rfq = await LoadScopedRfqAsync(rfqId, ct);
        if (rfq == null)
            return Result.Failure<int>($"RFQ {rfqId} not found or out of scope.");
        if (rfq.Status is RfqStatus.Cancelled or RfqStatus.Closed)
            return Result.Failure<int>($"RFQ is {rfq.Status} — cannot issue.");

        // Validate vendors are in tenant scope.
        var visible = _tenantContext.VisibleCompanyIds;
        var validVendorIds = await _db.Set<Vendor>()
            .Where(v => vendorIds.Contains(v.Id)
                && v.CompanyId != null && visible.Contains(v.CompanyId.Value))
            .Select(v => v.Id)
            .ToListAsync(ct);

        var alreadyInvited = rfq.Quotes.Select(q => q.VendorId).ToHashSet();
        var invited = 0;
        foreach (var vid in validVendorIds.Distinct())
        {
            if (alreadyInvited.Contains(vid)) continue; // idempotent
            rfq.Quotes.Add(new SupplierQuote
            {
                CompanyId = rfq.CompanyId,
                VendorId = vid,
                Status = SupplierQuoteStatus.Invited,
                Currency = "USD",
                CreatedAt = nowUtc,
            });
            invited++;
        }

        if (rfq.Status == RfqStatus.Draft) rfq.Status = RfqStatus.Issued;
        rfq.IssuedAtUtc ??= nowUtc;
        rfq.QuotesDueUtc = quotesDueUtc ?? rfq.QuotesDueUtc;
        rfq.UpdatedAt = nowUtc;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException dbex) when (IsUniqueViolation(dbex))
        {
            // Concurrent double-issue raced past the in-memory alreadyInvited
            // check; the (SupplierRFQId, VendorId) unique index rejected it.
            return Result.Failure<int>("A concurrent issue for this RFQ just landed. Retry.");
        }
        return Result.Success(invited);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3) RecordQuoteAsync — fill a supplier's prices + lead time
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<SupplierQuote>> RecordQuoteAsync(
        RecordQuoteRequest request, DateTime nowUtc, CancellationToken ct = default)
    {
        var rfq = await LoadScopedRfqAsync(request.SupplierRFQId, ct);
        if (rfq == null)
            return Result.Failure<SupplierQuote>($"RFQ {request.SupplierRFQId} not found or out of scope.");

        var quote = rfq.Quotes.FirstOrDefault(q => q.VendorId == request.VendorId);
        if (quote == null)
            return Result.Failure<SupplierQuote>(
                $"Vendor {request.VendorId} was not invited to this RFQ — issue to them first.");
        if (quote.Status is SupplierQuoteStatus.Awarded or SupplierQuoteStatus.Rejected)
            return Result.Failure<SupplierQuote>($"Quote is {quote.Status} — cannot re-record.");
        if (request.Lines == null || request.Lines.Count == 0)
            return Result.Failure<SupplierQuote>("A quote needs at least one priced line.");

        var rfqLineIds = rfq.Lines.Select(l => l.Id).ToHashSet();

        var existingTx = _db.Database.CurrentTransaction;
        var tx = existingTx ?? await _db.Database.BeginTransactionAsync(ct);
        var ownsTx = existingTx == null;
        try
        {
            // Replace any prior lines for this quote (re-record).
            var priorLines = await _db.Set<SupplierQuoteLine>()
                .Where(ql => ql.SupplierQuoteId == quote.Id).ToListAsync(ct);
            if (priorLines.Count > 0) _db.RemoveRange(priorLines);

            decimal total = 0m;
            foreach (var ql in request.Lines)
            {
                if (!rfqLineIds.Contains(ql.SupplierRFQLineId))
                    return await FailAsync(tx, ownsTx, ct,
                        $"Quote line references RFQ line {ql.SupplierRFQLineId} not on this RFQ.");
                var lineTotal = Math.Round(ql.QuotedQuantity * ql.QuotedUnitPrice, 2, MidpointRounding.AwayFromZero);
                total += lineTotal;
                _db.Add(new SupplierQuoteLine
                {
                    SupplierQuoteId = quote.Id,
                    SupplierRFQLineId = ql.SupplierRFQLineId,
                    QuotedQuantity = ql.QuotedQuantity,
                    QuotedUnitPrice = ql.QuotedUnitPrice,
                    LineTotal = lineTotal,
                    LeadTimeDays = Math.Max(0, ql.LeadTimeDays),
                    CreatedAt = nowUtc,
                });
            }

            quote.VendorQuoteReference = request.VendorQuoteReference;
            quote.ValidUntilDate = request.ValidUntilDate;
            quote.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim();
            quote.LeadTimeDays = Math.Max(0, request.LeadTimeDays);
            quote.TotalQuotedAmount = total;
            quote.Status = SupplierQuoteStatus.Received;
            quote.ReceivedAtUtc = nowUtc;
            quote.UpdatedAt = nowUtc;
            // A new/updated quote invalidates ANY prior ranking for the whole
            // RFQ (Codex P2). If it was already Evaluated, drop it back to
            // QuotesReceived and clear every quote's ranking outputs so the buyer
            // must re-rank before they can award on stale data.
            foreach (var q in rfq.Quotes)
            {
                q.CompositeScore = null;
                q.PriceScore = null;
                q.LeadTimeScore = null;
                q.RankPosition = null;
                q.IsWinner = false;
                q.ScoreReason = null;
                if (q.Id != quote.Id) q.UpdatedAt = nowUtc;
            }
            if (rfq.Status is RfqStatus.Draft or RfqStatus.Issued or RfqStatus.Evaluated)
                rfq.Status = RfqStatus.QuotesReceived;
            rfq.EvaluatedAtUtc = null;
            rfq.UpdatedAt = nowUtc;

            await _db.SaveChangesAsync(ct);
            if (ownsTx) await tx.CommitAsync(ct);
            return Result.Success(quote);
        }
        catch { if (ownsTx) await SafeRollbackAsync(tx, ct); throw; }
        finally { if (ownsTx) await tx.DisposeAsync(); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4) RankQuotesAsync — ⭐ THE ENHANCEMENT
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<RankQuotesResult>> RankQuotesAsync(
        int rfqId, RankWeights? weights, DateTime nowUtc, CancellationToken ct = default)
    {
        var w = weights ?? RankWeights.Default;
        if (w.PriceWeight < 0 || w.LeadTimeWeight < 0 || w.OtdWeight < 0
            || (w.PriceWeight + w.LeadTimeWeight + w.OtdWeight) <= 0)
            return Result.Failure<RankQuotesResult>("Invalid rank weights.");

        var rfq = await LoadScopedRfqAsync(rfqId, ct);
        if (rfq == null)
            return Result.Failure<RankQuotesResult>($"RFQ {rfqId} not found or out of scope.");
        // Codex P2: never re-rank a terminal RFQ — that would demote Awarded/
        // Closed back to Evaluated while AwardedQuoteId/ResultingPurchaseOrderId
        // still point at a decision made on the prior ranking.
        if (rfq.Status is RfqStatus.Awarded or RfqStatus.Closed or RfqStatus.Cancelled)
            return Result.Failure<RankQuotesResult>(
                $"RFQ is {rfq.Status} — cannot re-rank a completed RFQ.");

        var quotes = rfq.Quotes
            .Where(q => q.Status is SupplierQuoteStatus.Received
                or SupplierQuoteStatus.Shortlisted or SupplierQuoteStatus.Awarded)
            .ToList();
        if (quotes.Count == 0)
            return Result.Failure<RankQuotesResult>("No received quotes to rank.");

        // Vendor name lookup + OTD inputs (PR-18) per vendor.
        var vendorIds = quotes.Select(q => q.VendorId).Distinct().ToList();
        var vendorNames = await _db.Set<Vendor>()
            .Where(v => vendorIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name, ct);
        var otdByVendor = new Dictionary<int, decimal?>();
        foreach (var vid in vendorIds)
        {
            var inputs = await _supplierPerformance.GetCompositeInputsAsync(vid, ct);
            otdByVendor[vid] = (inputs.HasCurrentSnapshot && inputs.OnTimeDeliveryPct.HasValue)
                ? Clamp0to100(inputs.OnTimeDeliveryPct.Value)
                : (decimal?)null;
        }

        // Normalization baselines.
        var pricedTotals = quotes.Where(q => q.TotalQuotedAmount > 0m)
            .Select(q => q.TotalQuotedAmount).ToList();
        var bestPrice = pricedTotals.Count > 0 ? pricedTotals.Min() : 0m;
        var bestLead = quotes.Min(q => Math.Max(0, q.LeadTimeDays));

        var scored = new List<(SupplierQuote q, decimal price, decimal lead, decimal? otd, decimal composite)>();
        foreach (var q in quotes)
        {
            // Price score: cheapest = 100; unpriced (0) = 0.
            decimal priceScore = (q.TotalQuotedAmount > 0m && bestPrice > 0m)
                ? Round4(bestPrice / q.TotalQuotedAmount * 100m)
                : 0m;
            // Lead score: (bestLead+1)/(thisLead+1)*100 — shortest = 100, handles 0 cleanly.
            var thisLead = Math.Max(0, q.LeadTimeDays);
            decimal leadScore = Round4((decimal)(bestLead + 1) / (thisLead + 1) * 100m);

            var otd = otdByVendor[q.VendorId];
            decimal composite;
            if (otd.HasValue)
            {
                var denom = w.PriceWeight + w.LeadTimeWeight + w.OtdWeight;
                composite = (priceScore * w.PriceWeight + leadScore * w.LeadTimeWeight + otd.Value * w.OtdWeight) / denom;
            }
            else
            {
                // Fallback: price + lead only, weights re-normalized.
                var denom = w.PriceWeight + w.LeadTimeWeight;
                composite = denom > 0
                    ? (priceScore * w.PriceWeight + leadScore * w.LeadTimeWeight) / denom
                    : 0m;
            }
            composite = Round4(composite);

            q.SupplierOnTimeDeliveryPct = otd;
            q.PriceScore = priceScore;
            q.LeadTimeScore = leadScore;
            scored.Add((q, priceScore, leadScore, otd, composite));
        }

        // Rank: composite desc, then cheaper, then quote id (stable).
        var ordered = scored
            .OrderByDescending(s => s.composite)
            .ThenBy(s => s.q.TotalQuotedAmount > 0m ? s.q.TotalQuotedAmount : decimal.MaxValue)
            .ThenBy(s => s.q.Id)
            .ToList();

        var rows = new List<RankedQuoteRow>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var s = ordered[i];
            var rank = i + 1;
            var isWinner = rank == 1 && s.composite > 0m;
            var reason = BuildReason(s.composite, s.q.TotalQuotedAmount, s.price,
                Math.Max(0, s.q.LeadTimeDays), s.lead, s.otd);

            s.q.CompositeScore = s.composite;
            s.q.RankPosition = rank;
            s.q.IsWinner = isWinner;
            s.q.ScoreReason = reason;
            s.q.UpdatedAt = nowUtc;

            rows.Add(new RankedQuoteRow(
                s.q.Id, s.q.VendorId,
                vendorNames.TryGetValue(s.q.VendorId, out var nm) ? nm : $"Vendor {s.q.VendorId}",
                s.q.TotalQuotedAmount, Math.Max(0, s.q.LeadTimeDays), s.otd,
                s.price, s.lead, s.otd, s.composite, rank, isWinner, reason));
        }

        rfq.Status = RfqStatus.Evaluated;
        rfq.EvaluatedAtUtc = nowUtc;
        rfq.UpdatedAt = nowUtc;
        await _db.SaveChangesAsync(ct);

        var winner = rows.FirstOrDefault(r => r.IsWinner);
        return Result.Success(new RankQuotesResult(
            rfqId, rows.Count, winner?.SupplierQuoteId, rows));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5) AwardQuoteAsync
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<SupplierQuote>> AwardQuoteAsync(
        int quoteId, DateTime nowUtc, CancellationToken ct = default)
    {
        var quote = await LoadScopedQuoteAsync(quoteId, ct);
        if (quote == null)
            return Result.Failure<SupplierQuote>($"Quote {quoteId} not found or out of scope.");

        var rfq = await LoadScopedRfqAsync(quote.SupplierRFQId, ct);
        if (rfq == null)
            return Result.Failure<SupplierQuote>("Owning RFQ not found.");
        if (rfq.Status is RfqStatus.Cancelled or RfqStatus.Closed)
            return Result.Failure<SupplierQuote>($"RFQ is {rfq.Status} — cannot award.");
        // Award requires the RFQ to have been ranked (Evaluated) so the winner
        // carries a composite score / rank.
        if (rfq.Status != RfqStatus.Evaluated)
            return Result.Failure<SupplierQuote>(
                $"RFQ is {rfq.Status} — rank the quotes (Evaluate) before awarding.");
        if (quote.Status is not (SupplierQuoteStatus.Received or SupplierQuoteStatus.Shortlisted))
            return Result.Failure<SupplierQuote>($"Quote is {quote.Status} — only a received/shortlisted quote can be awarded.");

        foreach (var q in rfq.Quotes)
        {
            if (q.Id == quoteId)
            {
                q.Status = SupplierQuoteStatus.Awarded;
                q.IsWinner = true;
            }
            else if (q.Status is SupplierQuoteStatus.Received or SupplierQuoteStatus.Shortlisted)
            {
                q.Status = SupplierQuoteStatus.Rejected;
                q.IsWinner = false;
            }
            q.UpdatedAt = nowUtc;
        }
        rfq.AwardedQuoteId = quoteId;
        rfq.Status = RfqStatus.Awarded;
        rfq.UpdatedAt = nowUtc;

        await _db.SaveChangesAsync(ct);
        return Result.Success(quote);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6) ConvertQuoteToPoLineAsync — awarded quote → Draft PO + demand links
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<ConvertQuoteResult>> ConvertQuoteToPoLineAsync(
        int quoteId, DateTime nowUtc, CancellationToken ct = default)
    {
        var quote = await LoadScopedQuoteAsync(quoteId, ct);
        if (quote == null)
            return Result.Failure<ConvertQuoteResult>($"Quote {quoteId} not found or out of scope.");
        if (quote.Status != SupplierQuoteStatus.Awarded)
            return Result.Failure<ConvertQuoteResult>("Only an awarded quote can be converted to a PO.");

        var rfq = await LoadScopedRfqAsync(quote.SupplierRFQId, ct);
        if (rfq == null)
            return Result.Failure<ConvertQuoteResult>("Owning RFQ not found.");
        if (rfq.ResultingPurchaseOrderId.HasValue)
            return Result.Failure<ConvertQuoteResult>(
                $"RFQ already converted to PO #{rfq.ResultingPurchaseOrderId}.");

        var quoteLines = await _db.Set<SupplierQuoteLine>()
            .Where(ql => ql.SupplierQuoteId == quoteId).ToListAsync(ct);
        if (quoteLines.Count == 0)
            return Result.Failure<ConvertQuoteResult>("Awarded quote has no priced lines.");

        var rfqLineById = rfq.Lines.ToDictionary(l => l.Id);

        var existingTx = _db.Database.CurrentTransaction;
        var tx = existingTx ?? await _db.Database.BeginTransactionAsync(ct);
        var ownsTx = existingTx == null;
        try
        {
            // PONumber — same yy-NNNNN sequence the rest of the app uses.
            var lastPO = await _db.Set<PurchaseOrder>()
                .OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
            var nextNum = 1;
            if (lastPO != null && lastPO.PONumber.Contains('-'))
            {
                var parts = lastPO.PONumber.Split('-');
                if (parts.Length >= 2 && int.TryParse(parts[^1], out var n)) nextNum = n + 1;
            }
            var po = new PurchaseOrder
            {
                PONumber = $"PO-{nowUtc:yy}-{nextNum:D5}",
                Status = POStatus.Draft,
                VendorId = quote.VendorId,
                OrderDate = nowUtc.Date,
                RequiredDate = rfq.RequiredByDate,
                Currency = quote.Currency,
                Notes = $"Created from {rfq.RfqNumber} (awarded quote #{quote.Id}).",
                CompanyId = rfq.CompanyId,
                Subtotal = quote.TotalQuotedAmount,
                Total = quote.TotalQuotedAmount,
                CreatedAt = nowUtc,
            };
            _db.Add(po);
            await _db.SaveChangesAsync(ct); // assign po.Id

            var lineNo = 1;
            var demandLinks = 0;
            foreach (var ql in quoteLines.OrderBy(x => x.Id))
            {
                rfqLineById.TryGetValue(ql.SupplierRFQLineId, out var rfqLine);
                var poLine = new PurchaseOrderLine
                {
                    PurchaseOrderId = po.Id,
                    LineNumber = lineNo++,
                    ItemId = rfqLine?.ItemId,
                    IsNonItemMaster = rfqLine?.ItemId == null,
                    Description = rfqLine?.Description ?? "(from quote)",
                    PartNumber = rfqLine?.PartNumber,
                    UOM = rfqLine?.UOM ?? "EA",
                    QuantityOrdered = ql.QuotedQuantity,
                    UnitPrice = ql.QuotedUnitPrice,
                    LineTotal = ql.LineTotal,
                    RequiredDate = rfqLine?.RequiredDate ?? rfq.RequiredByDate,
                    ProductionOrderId = rfqLine?.ProductionOrderId,
                    BomLineId = rfqLine?.BomLineId,
                    OperationSequence = rfqLine?.OperationSequence,
                };
                _db.Add(poLine);
                await _db.SaveChangesAsync(ct); // assign poLine.Id for the demand link

                // Carry §17 demand linkage forward.
                if (rfqLine?.ProductionSupplyDemandId != null && rfqLine.ProductionOrderId != null
                    && rfq.CompanyId != null)
                {
                    _db.Add(new PurchaseOrderLineDemandLink
                    {
                        CompanyId = rfq.CompanyId.Value,
                        PurchaseOrderLineId = poLine.Id,
                        ProductionSupplyDemandId = rfqLine.ProductionSupplyDemandId.Value,
                        ProductionOrderId = rfqLine.ProductionOrderId.Value,
                        BomLineId = rfqLine.BomLineId,
                        OperationSequence = rfqLine.OperationSequence,
                        AllocatedQuantity = ql.QuotedQuantity,
                        RemainingQuantity = ql.QuotedQuantity,
                        UnitPriceAtLink = ql.QuotedUnitPrice,
                        NeedByDate = rfqLine.RequiredDate,
                        CreatedAt = nowUtc,
                        CreatedBy = "RFQ-Convert",
                        Notes = $"From {rfq.RfqNumber} awarded quote #{quote.Id}.",
                    });
                    demandLinks++;
                }
            }

            rfq.ResultingPurchaseOrderId = po.Id;
            rfq.Status = RfqStatus.Closed;
            rfq.UpdatedAt = nowUtc;
            await _db.SaveChangesAsync(ct);
            if (ownsTx) await tx.CommitAsync(ct);

            return Result.Success(new ConvertQuoteResult(
                po.Id, po.PONumber, quoteLines.Count, demandLinks));
        }
        catch (DbUpdateException dbex)
        {
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            if (IsUniqueViolation(dbex))
                return Result.Failure<ConvertQuoteResult>("A concurrent PO number collision — retry.");
            _logger.LogError(dbex, "ConvertQuoteToPoLineAsync failed for quote {QuoteId}.", quoteId);
            return Result.Failure<ConvertQuoteResult>("Failed to convert quote to PO.");
        }
        catch { if (ownsTx) await SafeRollbackAsync(tx, ct); throw; }
        finally { if (ownsTx) await tx.DisposeAsync(); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Reads
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<SupplierRFQ?> GetRfqAsync(int rfqId, CancellationToken ct = default)
        => await LoadScopedRfqAsync(rfqId, ct);

    public async Task<IReadOnlyList<RankedQuoteRow>> GetRankedQuotesAsync(
        int rfqId, CancellationToken ct = default)
    {
        var rfq = await LoadScopedRfqAsync(rfqId, ct);
        if (rfq == null) return Array.Empty<RankedQuoteRow>();

        var vendorIds = rfq.Quotes.Select(q => q.VendorId).Distinct().ToList();
        var vendorNames = await _db.Set<Vendor>()
            .Where(v => vendorIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.Name, ct);

        return rfq.Quotes
            .Where(q => q.CompositeScore.HasValue)
            .OrderBy(q => q.RankPosition ?? int.MaxValue)
            .Select(q => new RankedQuoteRow(
                q.Id, q.VendorId,
                vendorNames.TryGetValue(q.VendorId, out var nm) ? nm : $"Vendor {q.VendorId}",
                q.TotalQuotedAmount, Math.Max(0, q.LeadTimeDays), q.SupplierOnTimeDeliveryPct,
                q.PriceScore ?? 0m, q.LeadTimeScore ?? 0m, q.SupplierOnTimeDeliveryPct, q.CompositeScore!.Value,
                q.RankPosition ?? 0, q.IsWinner, q.ScoreReason ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<RfqListRow>> GetRfqListAsync(
        int maxRows, int? companyId = null, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        var take = Math.Clamp(maxRows, 1, 500);
        var q = _db.Set<SupplierRFQ>()
            .Where(r => r.CompanyId != null && visible.Contains(r.CompanyId.Value));
        if (companyId.HasValue) q = q.Where(r => r.CompanyId == companyId);

        var rfqs = await q
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .Select(r => new
            {
                r.Id, r.RfqNumber, r.Title, r.Status, r.RequiredByDate, r.CreatedAt,
                LineCount = r.Lines.Count,
                QuoteCount = r.Quotes.Count,
                WinnerVendorId = r.Quotes.Where(x => x.IsWinner).Select(x => (int?)x.VendorId).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var winnerIds = rfqs.Where(r => r.WinnerVendorId != null)
            .Select(r => r.WinnerVendorId!.Value).Distinct().ToList();
        var names = winnerIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Set<Vendor>().Where(v => winnerIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.Name, ct);

        return rfqs.Select(r => new RfqListRow(
            r.Id, r.RfqNumber, r.Title, r.Status, r.LineCount, r.QuoteCount,
            r.WinnerVendorId != null && names.TryGetValue(r.WinnerVendorId.Value, out var nm) ? nm : null,
            r.RequiredByDate, r.CreatedAt)).ToList();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<SupplierRFQ?> LoadScopedRfqAsync(int rfqId, CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<SupplierRFQ>()
            .Include(r => r.Lines)
            .Include(r => r.Quotes)
            .FirstOrDefaultAsync(r => r.Id == rfqId
                && r.CompanyId != null && visible.Contains(r.CompanyId.Value), ct);
    }

    private async Task<SupplierQuote?> LoadScopedQuoteAsync(int quoteId, CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<SupplierQuote>()
            .FirstOrDefaultAsync(q => q.Id == quoteId
                && q.CompanyId != null && visible.Contains(q.CompanyId.Value), ct);
    }

    private static string BuildReason(
        decimal composite, decimal total, decimal priceScore, int lead, decimal leadScore, decimal? otd)
    {
        var otdPart = otd.HasValue
            ? $", OTD {otd.Value:N0}%"
            : ", no OTD history (price+lead only)";
        return $"Composite {composite:N1} — ${total:N0} (price {priceScore:N0}), "
             + $"{lead}d lead (lead {leadScore:N0}){otdPart}.";
    }

    private static decimal Round4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
    private static decimal Clamp0to100(decimal v) => v < 0m ? 0m : v > 100m ? 100m : v;

    private static async Task<Result<SupplierQuote>> FailAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, bool ownsTx,
        CancellationToken ct, string msg)
    {
        if (ownsTx) await SafeRollbackAsync(tx, ct);
        return Result.Failure<SupplierQuote>(msg);
    }

    private static async Task SafeRollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct)
    {
        try { await tx.RollbackAsync(ct); } catch { /* already gone */ }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e != null; e = e.InnerException)
        {
            var p = e.GetType().GetProperty("SqlState");
            if (p?.GetValue(e) is string s && s == "23505") return true;
        }
        return false;
    }
}
