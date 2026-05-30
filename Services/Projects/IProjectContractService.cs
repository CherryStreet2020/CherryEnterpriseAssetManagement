// Theme B9 Wave 2 PR-6 (2026-05-30, CLOSES B9 Wave 2) — IProjectContractService.
//
// The capstone of the quote-to-cash spine. Two §20 gates the whole spine hinges on:
//   • Award validation — "cannot mark project awarded without an approved quote or
//     authorized override". On award the winning revision becomes the project
//     baseline (CustomerProject.ContractValue is stamped from its frozen total).
//   • Contract-review gate — "cannot launch project until required contract review
//     is complete".
// Tenant-scoped; ADR-025.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

// ── Request records ───────────────────────────────────────────────
public sealed record CreateContractRequest(
    int CustomerProjectId, string ContractNumber, string? Title = null, string? Description = null,
    string? Currency = null, bool ReviewRequired = true, DateTime? ReviewDueDate = null);

public sealed record AddContractLineRequest(
    int ContractId, string? ContractLineReference = null, int? ItemId = null, string? PartNumber = null,
    string? Description = null, decimal Quantity = 0m, string? Uom = null, decimal? UnitPrice = null,
    DateTime? BaselineStart = null, DateTime? BaselineFinish = null, int? LineNo = null);

public sealed record RecordCustomerPoRequest(
    int CustomerProjectId, string CustomerPoNumber, int? ProjectContractId = null, DateTime? PoDate = null,
    decimal? PoValue = null, string? Currency = null, string? Description = null);

public sealed record AwardQuoteRevisionRequest(
    int ContractId, int RevisionId, bool AuthorizedOverride = false, string? AwardedByName = null);

// ── Read DTOs ─────────────────────────────────────────────────────
public sealed record AwardResult(
    int ContractId, int AwardedQuoteId, int AwardedRevisionId,
    decimal? BaselineContractValue, DateTime AwardDate);

public sealed record ProjectContractSummary(
    int ContractId, string ContractNumber, ProjectContractStatus Status,
    ProjectContractReviewStatus ReviewStatus, bool ReviewRequired,
    decimal? BaselineContractValue, int? AwardedRevisionId, DateTime? AwardDate, DateTime? LaunchedAt);

public interface IProjectContractService
{
    /// <summary>Create a contract for a project. ContractNumber unique per company.</summary>
    Task<Result<int>> CreateContractAsync(CreateContractRequest req, CancellationToken ct = default);

    /// <summary>Add a contract deliverable line (CLIN).</summary>
    Task<Result<int>> AddLineAsync(AddContractLineRequest req, CancellationToken ct = default);

    /// <summary>Record a customer purchase order against the project/contract.</summary>
    Task<Result<int>> RecordCustomerPoAsync(RecordCustomerPoRequest req, CancellationToken ct = default);

    /// <summary>Mark the contract review complete (or waived) — satisfies the launch gate.</summary>
    Task<Result<ProjectContractSummary>> CompleteReviewAsync(int contractId, string? reviewedByName = null, bool waive = false, CancellationToken ct = default);

    /// <summary>
    /// Award a quote revision (§20: cannot award without an approved quote or an
    /// authorized override). The winning revision becomes the project BASELINE:
    /// CustomerProject.ContractValue is stamped from its frozen total, the revision
    /// goes Awarded + ConvertedToBaseline, and the quote goes Won.
    /// </summary>
    Task<Result<AwardResult>> AwardQuoteRevisionAsync(AwardQuoteRevisionRequest req, CancellationToken ct = default);

    /// <summary>
    /// Launch the project off this contract (§20: cannot launch until required
    /// contract review is complete). Flips the contract Active + the project Active.
    /// </summary>
    Task<Result<ProjectContractSummary>> LaunchProjectAsync(int contractId, CancellationToken ct = default);

    /// <summary>Contract summaries for a project (for the Command Center).</summary>
    Task<Result<IReadOnlyList<ProjectContractSummary>>> GetContractsForProjectAsync(int customerProjectId, CancellationToken ct = default);
}
