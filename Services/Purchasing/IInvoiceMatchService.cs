// Sprint 15.4 PR-19 — 3-Way Match service (PO ↔ Receipt ↔ Invoice).
//
// Spec ref: docs/research/purchasing-cascade-design-2026-05-28.md PR-19.
//
// The BIC upgrade of the legacy InvoiceMatchingService: a tolerance-driven,
// PERSISTED 3-way match that classifies each line's price/qty/date variance,
// freezes an InvoiceMatchResult, drives VendorInvoice.MatchStatus, surfaces
// exceptions to the Purchasing CC Cost Exceptions tab, and — on a clean match —
// posts the AP approval (incl PPV) atomically in the SAME transaction via
// IApPostingService (cross-service transaction enlistment, Session 20 lock).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Purchasing;

/// <summary>
/// Configurable 3-way match tolerances. A line is "within tolerance" when its
/// variance is ≤ the absolute OR the percentage band (whichever is looser).
/// Defaults: price ±$0.01 or ±2%, qty must equal received (no slack),
/// invoice within 7 days of the receipt.
/// </summary>
public sealed record InvoiceMatchTolerances(
    decimal PriceAbs = 0.01m,
    decimal PricePct = 2.0m,
    decimal QtyAbs = 0m,
    decimal QtyPct = 0m,
    int DateDays = 7)
{
    public static InvoiceMatchTolerances Default { get; } = new();
}

/// <summary>Outcome of a match run — drives the probe + the atomic-approve path.</summary>
public sealed record RunInvoiceMatchResult(
    int InvoiceMatchResultId,
    int VendorInvoiceId,
    string MatchRunNumber,
    InvoiceMatchOutcome Outcome,
    int LinesTotal,
    int LinesMatched,
    int LinesWithinTolerance,
    int LinesException,
    decimal TotalPriceVariance,
    bool PostedOnMatch,
    int? PostedJournalEntryId,
    string? Message);

/// <summary>One invoice with an open match exception — feeds the §21 Cost Exceptions tab.</summary>
public sealed record InvoiceMatchExceptionRow(
    int InvoiceMatchResultId,
    int VendorInvoiceId,
    string InvoiceNumber,
    string VendorName,
    string MatchRunNumber,
    int LinesException,
    decimal TotalPriceVariance,
    System.DateTime RunAtUtc);

public interface IInvoiceMatchService
{
    /// <summary>
    /// Run a 3-way match for the invoice: compare each line to its PO line
    /// (price/qty) + receipt line (received qty / receipt date), classify
    /// against the tolerances, freeze a fresh InvoiceMatchResult (flipping the
    /// prior IsCurrent), and set VendorInvoice.MatchStatus from the aggregate.
    /// Does NOT post. Tenant-scoped.
    /// </summary>
    Task<Result<RunInvoiceMatchResult>> RunMatchAsync(
        int invoiceId,
        InvoiceMatchTolerances? tolerances,
        System.DateTime nowUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Run the match and, if the aggregate is Matched or MatchedWithinTolerance,
    /// post the AP approval (Dr GR-accrued/expense + PPV / Cr AP) via
    /// IApPostingService INSIDE the match transaction so the match result, the
    /// invoice approval, and the journal entry commit atomically. On Exception
    /// the result is persisted and nothing is posted (caller resolves via the
    /// Cost Exceptions tab). Returns the run summary with PostedOnMatch + JE id.
    /// </summary>
    Task<Result<RunInvoiceMatchResult>> RunAndApproveIfCleanAsync(
        int invoiceId,
        InvoiceMatchTolerances? tolerances,
        string approverUsername,
        System.DateTime nowUtc,
        CancellationToken ct = default);

    /// <summary>Current match result (with lines) for an invoice, or null if never run.</summary>
    Task<InvoiceMatchResult?> GetCurrentMatchAsync(
        int invoiceId,
        CancellationToken ct = default);

    /// <summary>
    /// Current match results in Exception outcome across the tenant, most
    /// recent first. Drives the Purchasing CC Cost Exceptions tab.
    /// </summary>
    Task<IReadOnlyList<InvoiceMatchExceptionRow>> GetExceptionRowsAsync(
        int maxRows,
        CancellationToken ct = default);
}
