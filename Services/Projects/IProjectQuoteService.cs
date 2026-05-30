// Theme B9 Wave 2 PR-4 (2026-05-30) — IProjectQuoteService.
//
// The quote-to-cash spine's write+read surface for the quote layer. Models the
// spec §4 "multiple quotes are child records of the project" + §"Quote versions":
// each customer-visible revision (Rev A/B/C) freezes a LOCKED SNAPSHOT on submit
// that can never be overwritten — you mint a new revision instead. Tenant-scoped;
// ADR-025 (PageModels read through this service, never AppDbContext directly).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

// ── Request records ───────────────────────────────────────────────
public sealed record CreateRfqRequest(
    int CustomerProjectId, string RfqNumber, string? CustomerRfqReference = null,
    string? Description = null, DateTime? ReceivedDate = null, DateTime? DueDate = null,
    string? OwnerName = null, string? EstimatorName = null, string? SalespersonName = null);

public sealed record CreateQuoteRequest(
    int CustomerProjectId, string QuoteNumber, ProjectQuoteType QuoteType = ProjectQuoteType.Budgetary,
    string? Scenario = null, string? Description = null, string? Currency = null,
    int? ProjectRfqId = null, string? OwnerName = null, string? EstimatorName = null,
    string? SalespersonName = null);

public sealed record AddQuoteLineRequest(
    int RevisionId, int? ItemId = null, string? PartNumber = null, string? Description = null,
    decimal Quantity = 0m, string? Uom = null, decimal? UnitPrice = null, decimal? UnitCost = null,
    int? LeadTimeDays = null, int? LineNo = null, string? Notes = null);

// ── Read DTOs ─────────────────────────────────────────────────────
public sealed record ProjectQuoteRevisionSummary(
    int RevisionId, int QuoteId, string RevisionLabel, int RevisionNumber,
    ProjectQuoteRevisionStatus VersionStatus, bool IsSnapshotLocked,
    decimal? TotalPrice, int LineCount, DateTime? SubmittedDate);

public sealed record ProjectQuoteSummary(
    int QuoteId, string QuoteNumber, ProjectQuoteType QuoteType, ProjectQuoteStatus Status,
    string? Scenario, string Currency, int RevisionCount,
    int? LatestSubmittedRevisionNumber, string? LatestSubmittedRevisionLabel,
    decimal? LatestSubmittedTotalPrice, DateTime? LatestSubmittedDate);

public interface IProjectQuoteService
{
    /// <summary>Record a customer RFQ against a project. RfqNumber unique per company.</summary>
    Task<Result<int>> CreateRfqAsync(CreateRfqRequest req, CancellationToken ct = default);

    /// <summary>Create a quote (+ an initial Draft revision "Rev A"). QuoteNumber unique per company.</summary>
    Task<Result<ProjectQuoteSummary>> CreateQuoteAsync(CreateQuoteRequest req, CancellationToken ct = default);

    /// <summary>Add a new Draft revision (next label/number) to a quote.</summary>
    Task<Result<int>> AddRevisionAsync(int quoteId, CancellationToken ct = default);

    /// <summary>
    /// Add a priced line to a revision. FAILS if the revision's snapshot is locked
    /// (Submitted) — the core "cannot overwrite a submitted quote snapshot" rule.
    /// </summary>
    Task<Result<int>> AddLineAsync(AddQuoteLineRequest req, CancellationToken ct = default);

    /// <summary>
    /// Submit a Draft revision to the customer: freezes the TotalPrice from its lines,
    /// LOCKS the snapshot, supersedes any prior Submitted revision on the same quote,
    /// and marks the quote Active. Idempotency: a locked revision cannot be re-submitted.
    /// </summary>
    Task<Result<ProjectQuoteRevisionSummary>> SubmitRevisionAsync(int revisionId, CancellationToken ct = default);

    /// <summary>All quotes for a project with their latest-submitted-revision rollup. For the Command Center.</summary>
    Task<Result<IReadOnlyList<ProjectQuoteSummary>>> GetQuotesForProjectAsync(int customerProjectId, CancellationToken ct = default);
}
