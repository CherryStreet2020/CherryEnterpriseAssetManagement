// Theme B9 Wave 2 PR-5 (2026-05-30) — IProjectEstimateService.
//
// The internal cost-model side of the quote-to-cash spine. Build a working
// ProjectEstimate (cost-element lines typed with the B7 CostElementType split),
// roll it up by bucket, then FREEZE it into an immutable ProjectEstimateSnapshot
// at quote submission — optionally attaching the snapshot to a quote revision and
// stamping that revision's estimated margin. Tenant-scoped; ADR-025.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;   // CostElementType
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

// ── Request records ───────────────────────────────────────────────
public sealed record CreateEstimateRequest(
    int CustomerProjectId, string EstimateNumber, string? Title = null, string? Description = null,
    string? Currency = null, int? ProjectQuoteId = null, decimal? TargetMarginPct = null,
    decimal? ContingencyPct = null, string? EstimatorName = null);

public sealed record AddEstimateLineRequest(
    int EstimateId, CostElementType CostElementType = CostElementType.Material,
    string? Description = null, int? ItemId = null, decimal Quantity = 0m, string? Uom = null,
    decimal? UnitCost = null, decimal? Hours = null, decimal? Rate = null, int? LineNo = null,
    string? Notes = null);

public sealed record SnapshotEstimateRequest(
    int EstimateId, int? RevisionId = null, decimal? QuotedPrice = null, string? CapturedBy = null);

// ── Read DTOs ─────────────────────────────────────────────────────
public sealed record ProjectEstimateRollup(
    int EstimateId, string EstimateNumber, ProjectEstimateStatus Status, string Currency,
    decimal MaterialCost, decimal LaborCost, decimal SubcontractCost, decimal OverheadCost,
    decimal OtherCost, decimal DirectTotalCost, decimal? ContingencyPct, decimal TotalCost,
    decimal? TargetMarginPct, int LineCount);

public sealed record ProjectEstimateSnapshotSummary(
    int SnapshotId, int? RevisionId, decimal TotalCost, decimal DirectTotalCost,
    decimal? QuotedPrice, decimal? EstimatedMarginPct, int LineCount, DateTime CapturedAt);

public interface IProjectEstimateService
{
    /// <summary>Create a working estimate. EstimateNumber unique per company.</summary>
    Task<Result<int>> CreateEstimateAsync(CreateEstimateRequest req, CancellationToken ct = default);

    /// <summary>Add a cost-element line. FAILS if the estimate has been snapshotted (locked).</summary>
    Task<Result<int>> AddLineAsync(AddEstimateLineRequest req, CancellationToken ct = default);

    /// <summary>Roll up the estimate by cost bucket (material/labor/subcontract/overhead/other) + contingency.</summary>
    Task<Result<ProjectEstimateRollup>> GetEstimateAsync(int estimateId, CancellationToken ct = default);

    /// <summary>
    /// Freeze the estimate into an immutable ProjectEstimateSnapshot (rollup + a JSON
    /// freeze of the lines) and lock the working estimate. When a RevisionId is given,
    /// attaches the snapshot to that quote revision (SourceEstimateSnapshotId) and
    /// stamps the revision's EstimatedMarginPct from the quoted price.
    /// </summary>
    Task<Result<ProjectEstimateSnapshotSummary>> SnapshotEstimateAsync(SnapshotEstimateRequest req, CancellationToken ct = default);

    /// <summary>Read a frozen snapshot.</summary>
    Task<Result<ProjectEstimateSnapshotSummary>> GetSnapshotAsync(int snapshotId, CancellationToken ct = default);
}
