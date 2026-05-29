// Sprint 15.4 PR-19 — IInvoiceMatchService implementation.
//
// Patterns: ITenantContext gating, Result envelope, two-phase numbering
// (IM-YYYY-NNNNNN), IsCurrent snapshot flip with 23505 race handling, and
// cross-service transaction enlistment (Session 20 lock) — RunAndApprove opens
// the transaction, persists the match, then calls IApPostingService.PostApproval
// which shares the same scoped AppDbContext and so enlists in the open
// transaction. Match record + invoice approval + journal entry commit atomically.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.AccountsPayable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Purchasing;

public class InvoiceMatchService : IInvoiceMatchService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IApPostingService _apPosting;
    private readonly ILogger<InvoiceMatchService> _logger;

    public InvoiceMatchService(
        AppDbContext db,
        ITenantContext tenantContext,
        IApPostingService apPosting,
        ILogger<InvoiceMatchService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _apPosting = apPosting;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1) RunMatchAsync — compute + persist, no posting.
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<RunInvoiceMatchResult>> RunMatchAsync(
        int invoiceId, InvoiceMatchTolerances? tolerances, DateTime nowUtc,
        CancellationToken ct = default)
    {
        var tol = tolerances ?? InvoiceMatchTolerances.Default;

        var invoice = await LoadScopedInvoiceAsync(invoiceId, ct);
        if (invoice == null)
            return Result.Failure<RunInvoiceMatchResult>(
                $"Invoice {invoiceId} not found or out of tenant scope.");

        var existingTx = _db.Database.CurrentTransaction;
        var tx = existingTx ?? await _db.Database.BeginTransactionAsync(ct);
        var ownsTx = existingTx == null;
        try
        {
            var result = await ComputeAndPersistMatchAsync(invoice, tol, nowUtc, ct);
            await _db.SaveChangesAsync(ct);
            if (ownsTx) await tx.CommitAsync(ct);
            return Result.Success(ToRunResult(result, "Match run complete."));
        }
        catch (DbUpdateException dbex)
        {
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            if (IsPostgresUniqueViolation(dbex))
                return Result.Failure<RunInvoiceMatchResult>(
                    "A concurrent match run for this invoice just landed. Retry.");
            _logger.LogError(dbex, "RunMatchAsync failed for invoice {InvoiceId}.", invoiceId);
            return Result.Failure<RunInvoiceMatchResult>("Failed to persist match result.");
        }
        catch
        {
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            throw;
        }
        finally
        {
            if (ownsTx) await tx.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2) RunAndApproveIfCleanAsync — atomic match + AP approval posting.
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<RunInvoiceMatchResult>> RunAndApproveIfCleanAsync(
        int invoiceId, InvoiceMatchTolerances? tolerances, string approverUsername,
        DateTime nowUtc, CancellationToken ct = default)
    {
        var tol = tolerances ?? InvoiceMatchTolerances.Default;

        var invoice = await LoadScopedInvoiceAsync(invoiceId, ct);
        if (invoice == null)
            return Result.Failure<RunInvoiceMatchResult>(
                $"Invoice {invoiceId} not found or out of tenant scope.");

        var existingTx = _db.Database.CurrentTransaction;
        var tx = existingTx ?? await _db.Database.BeginTransactionAsync(ct);
        var ownsTx = existingTx == null;
        try
        {
            var result = await ComputeAndPersistMatchAsync(invoice, tol, nowUtc, ct);
            await _db.SaveChangesAsync(ct);

            var clean = result.Outcome == InvoiceMatchOutcome.Matched
                     || result.Outcome == InvoiceMatchOutcome.MatchedWithinTolerance;

            string message;
            if (clean)
            {
                // Cross-service enlistment: PostApprovalAsync shares this scoped
                // AppDbContext and its SaveChanges joins the open transaction.
                // overrideMatch=true because THIS service is now the authoritative
                // tolerance-based gate; the legacy exact-match gate inside
                // PostApprovalAsync must not second-guess a within-tolerance pass.
                var posting = await _apPosting.PostApprovalAsync(
                    invoiceId, overrideMatch: true, approverUsername: approverUsername);

                // Codex P1: PostApprovalAsync re-runs the LEGACY exact-match
                // evaluator and persists its (stricter) verdict, which would
                // stamp MatchStatus=Exception on a within-tolerance invoice we
                // just approved. Re-assert this service's authoritative verdict
                // — the invoice is the same tracked entity, so this overrides
                // the legacy write and the SaveChanges below persists it.
                invoice.MatchStatus = InvoiceMatchStatus.FullyMatched;
                invoice.UpdatedAt = nowUtc;

                result.PostedOnMatch = true;
                result.PostedJournalEntryId = posting.JournalEntryId;
                result.UpdatedAt = nowUtc;
                await _db.SaveChangesAsync(ct);
                message = $"Clean match ({result.Outcome}) — approved + posted JE {posting.JournalEntryId}.";
            }
            else
            {
                message = $"Match outcome {result.Outcome} — not posted. Resolve exceptions before approval.";
            }

            if (ownsTx) await tx.CommitAsync(ct);
            return Result.Success(ToRunResult(result, message));
        }
        catch (DbUpdateException dbex)
        {
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            if (IsPostgresUniqueViolation(dbex))
                return Result.Failure<RunInvoiceMatchResult>(
                    "A concurrent match run for this invoice just landed. Retry.");
            _logger.LogError(dbex, "RunAndApprove failed for invoice {InvoiceId}.", invoiceId);
            return Result.Failure<RunInvoiceMatchResult>("Failed to persist/post match result.");
        }
        catch (InvalidOperationException ioe)
        {
            // PostApprovalAsync surfaces business-rule failures (closed period,
            // no resolvable company, etc.) as InvalidOperationException. Roll the
            // whole thing back so the match record doesn't claim a posting that
            // never happened.
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            return Result.Failure<RunInvoiceMatchResult>(
                $"Match persisted but AP approval failed — rolled back. {ioe.Message}");
        }
        catch
        {
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            throw;
        }
        finally
        {
            if (ownsTx) await tx.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Core compute + persist (caller owns the transaction)
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<InvoiceMatchResult> ComputeAndPersistMatchAsync(
        VendorInvoice invoice, InvoiceMatchTolerances tol, DateTime nowUtc, CancellationToken ct)
    {
        // Flip any prior IsCurrent result for this invoice.
        var priorCurrent = await _db.Set<InvoiceMatchResult>()
            .Where(r => r.VendorInvoiceId == invoice.Id && r.IsCurrent)
            .ToListAsync(ct);
        foreach (var prior in priorCurrent)
        {
            prior.IsCurrent = false;
            prior.UpdatedAt = nowUtc;
        }

        var header = new InvoiceMatchResult
        {
            CompanyId = invoice.CompanyId,
            VendorInvoiceId = invoice.Id,
            MatchRunNumber = $"TMP-{Guid.NewGuid():N}",
            TolerancePriceAbs = tol.PriceAbs,
            TolerancePricePct = tol.PricePct,
            ToleranceQtyAbs = tol.QtyAbs,
            ToleranceQtyPct = tol.QtyPct,
            ToleranceDateDays = tol.DateDays,
            IsCurrent = true,
            RunAtUtc = nowUtc,
            CreatedAt = nowUtc,
        };

        int matched = 0, withinTol = 0, exception = 0;
        decimal totalPriceVar = 0m;

        foreach (var invLine in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            var resultLine = ClassifyLine(invLine, invoice.InvoiceDate, tol, nowUtc);
            header.Lines.Add(resultLine);
            totalPriceVar += resultLine.ExtendedPriceVariance;

            switch (resultLine.Outcome)
            {
                case InvoiceMatchLineOutcome.Matched: matched++; break;
                case InvoiceMatchLineOutcome.WithinTolerance: withinTol++; break;
                case InvoiceMatchLineOutcome.Unlinked: break; // informational, not an exception
                default: exception++; break; // NotReceived/OverBilled/Price/Qty/Date
            }
        }

        header.LinesTotal = invoice.Lines.Count;
        header.LinesMatched = matched;
        header.LinesWithinTolerance = withinTol;
        header.LinesException = exception;
        header.TotalPriceVariance = Math.Round(totalPriceVar, 2, MidpointRounding.AwayFromZero);
        header.Outcome = AggregateOutcome(header.LinesTotal, matched, withinTol, exception);

        _db.Add(header);
        await _db.SaveChangesAsync(ct); // assign Id for two-phase numbering

        header.MatchRunNumber = $"IM-{nowUtc:yyyy}-{header.Id:D6}";

        // Drive the invoice's 4-value MatchStatus from the richer outcome.
        invoice.MatchStatus = header.Outcome switch
        {
            InvoiceMatchOutcome.Exception => InvoiceMatchStatus.Exception,
            InvoiceMatchOutcome.Matched => InvoiceMatchStatus.FullyMatched,
            InvoiceMatchOutcome.MatchedWithinTolerance => InvoiceMatchStatus.FullyMatched,
            _ => (matched > 0 || withinTol > 0)
                ? InvoiceMatchStatus.PartialMatch
                : InvoiceMatchStatus.NotMatched,
        };
        invoice.UpdatedAt = nowUtc;

        return header;
    }

    private static InvoiceMatchResultLine ClassifyLine(
        VendorInvoiceLine invLine, DateTime invoiceDate, InvoiceMatchTolerances tol, DateTime nowUtc)
    {
        var po = invLine.PurchaseOrderLine;
        var gr = invLine.GoodsReceiptLine;

        var line = new InvoiceMatchResultLine
        {
            VendorInvoiceLineId = invLine.Id,
            PurchaseOrderLineId = invLine.PurchaseOrderLineId,
            GoodsReceiptLineId = invLine.GoodsReceiptLineId,
            InvoicedQuantity = invLine.Quantity,
            InvoicedUnitPrice = invLine.UnitPrice,
            PoQuantity = po?.QuantityOrdered ?? 0m,
            PoUnitPrice = po?.UnitPrice ?? 0m,
            ReceivedQuantity = gr?.QuantityReceived ?? 0m,
            CreatedAt = nowUtc,
        };

        if (po == null)
        {
            line.Outcome = InvoiceMatchLineOutcome.Unlinked;
            line.Note = "Invoice line not linked to a PO line — cannot 3-way match.";
            return line;
        }

        // Variances.
        line.PriceVariance = Math.Round(invLine.UnitPrice - po.UnitPrice, 4, MidpointRounding.AwayFromZero);
        line.PriceVariancePct = po.UnitPrice != 0m
            ? Math.Round(line.PriceVariance / po.UnitPrice * 100m, 4, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        line.QuantityVariance = Math.Round(invLine.Quantity - line.ReceivedQuantity, 4, MidpointRounding.AwayFromZero);
        line.ExtendedPriceVariance = Math.Round(line.PriceVariance * invLine.Quantity, 2, MidpointRounding.AwayFromZero);

        if (gr == null)
        {
            line.Outcome = InvoiceMatchLineOutcome.NotReceived;
            line.Note = "Linked to PO but no goods receipt — delivery unconfirmed.";
            return line;
        }

        // Guard against an unset invoice date (year 1) producing a spurious
        // multi-thousand-day variance and a false DateException.
        line.DateVarianceDays = (gr.GoodsReceipt != null && invoiceDate.Year > 1)
            ? (int)(invoiceDate.Date - gr.GoodsReceipt.ReceiptDate.Date).TotalDays
            : (int?)null;

        var withinPrice = WithinTol(Math.Abs(line.PriceVariance), tol.PriceAbs,
            line.PriceVariancePct, tol.PricePct);
        var qtyPct = line.ReceivedQuantity != 0m
            ? Math.Abs(line.QuantityVariance) / line.ReceivedQuantity * 100m
            : (decimal?)null;
        var withinQty = WithinTol(Math.Abs(line.QuantityVariance), tol.QtyAbs, qtyPct, tol.QtyPct);
        var withinDate = !line.DateVarianceDays.HasValue
            || Math.Abs(line.DateVarianceDays.Value) <= tol.DateDays;

        // Priority: over-billing first (most dangerous), then price, qty, date.
        if (!withinQty && line.QuantityVariance > 0m)
        {
            line.Outcome = InvoiceMatchLineOutcome.OverBilled;
            line.Note = $"Invoiced {line.InvoicedQuantity} > received {line.ReceivedQuantity}.";
        }
        else if (!withinPrice)
        {
            line.Outcome = InvoiceMatchLineOutcome.PriceException;
            line.Note = $"Price {line.InvoicedUnitPrice} vs PO {line.PoUnitPrice} ({line.PriceVariancePct?.ToString("N2") ?? "—"}%).";
        }
        else if (!withinQty)
        {
            line.Outcome = InvoiceMatchLineOutcome.QuantityException;
            line.Note = $"Invoiced {line.InvoicedQuantity} vs received {line.ReceivedQuantity}.";
        }
        else if (!withinDate)
        {
            line.Outcome = InvoiceMatchLineOutcome.DateException;
            line.Note = $"Invoice {line.DateVarianceDays} day(s) from receipt (tol {tol.DateDays}).";
        }
        else if (line.PriceVariance != 0m || line.QuantityVariance != 0m
                 || (line.DateVarianceDays ?? 0) != 0)
        {
            line.Outcome = InvoiceMatchLineOutcome.WithinTolerance;
        }
        else
        {
            line.Outcome = InvoiceMatchLineOutcome.Matched;
        }

        return line;
    }

    /// <summary>Within tolerance when the absolute OR the percentage band is satisfied.</summary>
    private static bool WithinTol(decimal absVar, decimal absTol, decimal? pct, decimal pctTol)
    {
        if (absVar <= absTol) return true;
        if (pct.HasValue && Math.Abs(pct.Value) <= pctTol) return true;
        return false;
    }

    private static InvoiceMatchOutcome AggregateOutcome(
        int total, int matched, int withinTol, int exception)
    {
        if (total == 0) return InvoiceMatchOutcome.NotMatched;
        if (exception > 0) return InvoiceMatchOutcome.Exception;
        if (matched == total) return InvoiceMatchOutcome.Matched;
        if (withinTol > 0 && (matched + withinTol) == total)
            return InvoiceMatchOutcome.MatchedWithinTolerance;
        // Remaining lines are Unlinked (informational) with no real exception:
        // can't claim a full match. The invoice MatchStatus mapping promotes this
        // to PartialMatch when some lines did match.
        return InvoiceMatchOutcome.NotMatched;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Reads
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<InvoiceMatchResult?> GetCurrentMatchAsync(
        int invoiceId, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<InvoiceMatchResult>()
            .Include(r => r.Lines)
            .Where(r => r.VendorInvoiceId == invoiceId
                && r.IsCurrent
                && r.CompanyId != null
                && visible.Contains(r.CompanyId.Value))
            .OrderByDescending(r => r.RunAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<InvoiceMatchExceptionRow>> GetExceptionRowsAsync(
        int maxRows, int? companyId = null, int? vendorId = null, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        var take = Math.Clamp(maxRows, 1, 500);
        var q =
            from r in _db.Set<InvoiceMatchResult>()
            join inv in _db.Set<VendorInvoice>() on r.VendorInvoiceId equals inv.Id
            join v in _db.Set<Vendor>() on inv.VendorId equals v.Id
            where r.IsCurrent
                && r.Outcome == InvoiceMatchOutcome.Exception
                && r.CompanyId != null
                && visible.Contains(r.CompanyId.Value)
            select new { r, inv, v };

        // Apply the lane filters IN-QUERY, before paging (Codex P2).
        if (companyId.HasValue)
            q = q.Where(x => x.r.CompanyId == companyId);
        if (vendorId.HasValue)
            q = q.Where(x => x.inv.VendorId == vendorId);

        return await q
            .OrderByDescending(x => x.r.RunAtUtc)
            .Take(take)
            .Select(x => new InvoiceMatchExceptionRow(
                x.r.Id, x.r.VendorInvoiceId, x.inv.InvoiceNumber, x.v.Name,
                x.r.MatchRunNumber, x.r.LinesException, x.r.TotalPriceVariance, x.r.RunAtUtc))
            .ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<VendorInvoice?> LoadScopedInvoiceAsync(int invoiceId, CancellationToken ct)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        // Tenant scope pushed into the query (not post-hoc) so a cross-tenant
        // invoice id is never even materialized.
        return await _db.Set<VendorInvoice>()
            .Include(i => i.Lines).ThenInclude(l => l.PurchaseOrderLine)
            .Include(i => i.Lines).ThenInclude(l => l.GoodsReceiptLine).ThenInclude(grl => grl!.GoodsReceipt)
            .FirstOrDefaultAsync(i => i.Id == invoiceId
                && i.CompanyId != null && visible.Contains(i.CompanyId.Value), ct);
    }

    private static RunInvoiceMatchResult ToRunResult(InvoiceMatchResult r, string message) =>
        new(r.Id, r.VendorInvoiceId, r.MatchRunNumber, r.Outcome,
            r.LinesTotal, r.LinesMatched, r.LinesWithinTolerance, r.LinesException,
            r.TotalPriceVariance, r.PostedOnMatch, r.PostedJournalEntryId, message);

    private static async Task SafeRollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct)
    {
        try { await tx.RollbackAsync(ct); } catch { /* already rolled back / disposed */ }
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e != null; e = e.InnerException)
        {
            var sqlStateProp = e.GetType().GetProperty("SqlState");
            if (sqlStateProp?.GetValue(e) is string sqlState && sqlState == "23505")
                return true;
        }
        return false;
    }
}
