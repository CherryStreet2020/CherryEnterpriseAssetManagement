// Theme B9 Wave 2 PR-5 (2026-05-30) — ProjectEstimateService impl.
// Tenant-scoped. New estimate inherits CompanyId from the tenant-verified parent
// project; lines scope through the estimate. The snapshot freezes the rollup +
// a JSON copy of the lines and locks the working estimate.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectEstimateService : IProjectEstimateService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectEstimateService> _log;

    public ProjectEstimateService(AppDbContext db, ITenantContext tenant, ILogger<ProjectEstimateService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    private async Task<(bool ok, int companyId, string? err)> ResolveProjectCompanyAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, 0, "CustomerProjectId must be > 0.");
        var row = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.CompanyId })
            .FirstOrDefaultAsync(ct);
        if (row == null) return (false, 0, $"Customer project {projectId} not found in your tenant scope.");
        if (row.CompanyId is null) return (false, 0, $"Customer project {projectId} has no company assigned.");
        return (true, row.CompanyId.Value, null);
    }

    public async Task<Result<int>> CreateEstimateAsync(CreateEstimateRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.EstimateNumber))
            return Result.Failure<int>("An estimate number is required.");

        var (ok, companyId, err) = await ResolveProjectCompanyAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        var num = req.EstimateNumber.Trim();
        bool dup = await _db.ProjectEstimates.AnyAsync(x => x.CompanyId == companyId && x.EstimateNumber == num, ct);
        if (dup) return Result.Failure<int>($"Estimate number '{num}' already exists for this company.");

        if (req.ProjectQuoteId is { } qid)
        {
            bool quoteOk = await _db.ProjectQuotes.AnyAsync(
                q => q.Id == qid && q.CompanyId == companyId && q.CustomerProjectId == req.CustomerProjectId, ct);
            if (!quoteOk) return Result.Failure<int>($"Quote {qid} is not on this project.");
        }

        var est = new ProjectEstimate
        {
            CompanyId = companyId,
            CustomerProjectId = req.CustomerProjectId,
            ProjectQuoteId = req.ProjectQuoteId,
            EstimateNumber = num,
            Title = req.Title,
            Description = req.Description,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency!.Trim(),
            Status = ProjectEstimateStatus.Draft,
            TargetMarginPct = req.TargetMarginPct,
            ContingencyPct = req.ContingencyPct,
            EstimatorName = req.EstimatorName,
        };
        _db.ProjectEstimates.Add(est);
        await _db.SaveChangesAsync(ct);
        return Result.Success(est.Id);
    }

    public async Task<Result<int>> AddLineAsync(AddEstimateLineRequest req, CancellationToken ct = default)
    {
        if (req is null || req.EstimateId <= 0) return Result.Failure<int>("An estimate id is required.");

        var est = await _db.ProjectEstimates
            .Where(x => x.Id == req.EstimateId)
            .Select(x => new { x.Id, x.Status, x.CompanyId })
            .FirstOrDefaultAsync(ct);
        if (est == null || !_tenant.VisibleCompanyIds.Contains(est.CompanyId))
            return Result.Failure<int>($"Estimate {req.EstimateId} not found in your tenant scope.");

        if (est.Status != ProjectEstimateStatus.Draft)
            return Result.Failure<int>("This estimate has been snapshotted and is locked — create a new estimate to keep building.");

        // Tenant-scope the optional item FK (PR-4 P1 lesson — scope EVERY incoming FK).
        if (req.ItemId is { } itemId)
        {
            bool itemOk = await _db.Items.AnyAsync(
                i => i.Id == itemId && (i.CompanyId == null || _tenant.VisibleCompanyIds.Contains(i.CompanyId.Value)), ct);
            if (!itemOk) return Result.Failure<int>($"Item {itemId} is not in your tenant scope.");
        }

        int lineNo = req.LineNo ?? ((await _db.ProjectEstimateLines
            .Where(l => l.ProjectEstimateId == req.EstimateId)
            .Select(l => (int?)l.LineNo).MaxAsync(ct) ?? 0) + 1);

        var line = new ProjectEstimateLine
        {
            ProjectEstimateId = req.EstimateId,
            LineNo = lineNo,
            CostElementType = req.CostElementType,
            Description = req.Description,
            ItemId = req.ItemId,
            Quantity = req.Quantity,
            Uom = req.Uom,
            UnitCost = req.UnitCost,
            Hours = req.Hours,
            Rate = req.Rate,
            ExtendedCost = ComputeExtended(req.Quantity, req.UnitCost, req.Hours, req.Rate),
            Notes = req.Notes,
        };
        _db.ProjectEstimateLines.Add(line);
        await _db.SaveChangesAsync(ct);
        return Result.Success(line.Id);
    }

    public async Task<Result<ProjectEstimateRollup>> GetEstimateAsync(int estimateId, CancellationToken ct = default)
    {
        var est = await _db.ProjectEstimates
            .Where(x => x.Id == estimateId && _tenant.VisibleCompanyIds.Contains(x.CompanyId))
            .Select(x => new { x.Id, x.EstimateNumber, x.Status, x.Currency, x.ContingencyPct, x.TargetMarginPct })
            .FirstOrDefaultAsync(ct);
        if (est == null) return Result.Failure<ProjectEstimateRollup>($"Estimate {estimateId} not found in your tenant scope.");

        var lines = await _db.ProjectEstimateLines
            .Where(l => l.ProjectEstimateId == estimateId)
            .Select(l => new { l.CostElementType, l.ExtendedCost })
            .ToListAsync(ct);

        var b = Bucketize(lines.Select(l => (l.CostElementType, l.ExtendedCost ?? 0m)));
        decimal total = ApplyContingency(b.direct, est.ContingencyPct);

        return Result.Success(new ProjectEstimateRollup(
            est.Id, est.EstimateNumber, est.Status, est.Currency,
            b.material, b.labor, b.subcontract, b.overhead, b.other, b.direct,
            est.ContingencyPct, total, est.TargetMarginPct, lines.Count));
    }

    public async Task<Result<ProjectEstimateSnapshotSummary>> SnapshotEstimateAsync(SnapshotEstimateRequest req, CancellationToken ct = default)
    {
        if (req is null || req.EstimateId <= 0) return Result.Failure<ProjectEstimateSnapshotSummary>("An estimate id is required.");

        var est = await _db.ProjectEstimates
            .FirstOrDefaultAsync(x => x.Id == req.EstimateId, ct);
        if (est == null || !_tenant.VisibleCompanyIds.Contains(est.CompanyId))
            return Result.Failure<ProjectEstimateSnapshotSummary>($"Estimate {req.EstimateId} not found in your tenant scope.");
        if (est.Status != ProjectEstimateStatus.Draft)
            return Result.Failure<ProjectEstimateSnapshotSummary>("This estimate has already been snapshotted.");

        var lines = await _db.ProjectEstimateLines
            .Where(l => l.ProjectEstimateId == req.EstimateId)
            .OrderBy(l => l.LineNo)
            .ToListAsync(ct);
        if (lines.Count == 0)
            return Result.Failure<ProjectEstimateSnapshotSummary>("Add at least one cost line before snapshotting the estimate.");

        // Optional target revision — must be on the same project/company.
        ProjectQuoteRevision? rev = null;
        if (req.RevisionId is { } revId)
        {
            rev = await _db.ProjectQuoteRevisions.Include(r => r.Quote)
                .FirstOrDefaultAsync(r => r.Id == revId, ct);
            if (rev == null || rev.Quote == null || !_tenant.VisibleCompanyIds.Contains(rev.Quote.CompanyId)
                || rev.Quote.CustomerProjectId != est.CustomerProjectId)
                return Result.Failure<ProjectEstimateSnapshotSummary>($"Quote revision {revId} is not on this estimate's project.");
        }

        var b = Bucketize(lines.Select(l => (l.CostElementType, l.ExtendedCost ?? 0m)));
        decimal total = ApplyContingency(b.direct, est.ContingencyPct);

        // Price the cost was measured against: explicit quotedPrice, else the revision's frozen total.
        decimal? price = req.QuotedPrice ?? rev?.TotalPrice;
        decimal? margin = (price is { } p && p != 0m) ? Math.Round((p - total) / p * 100m, 4) : (decimal?)null;

        var frozenJson = JsonSerializer.Serialize(lines.Select(l => new
        {
            l.LineNo, CostElement = l.CostElementType.ToString(), l.Description,
            l.Quantity, l.Uom, l.UnitCost, l.Hours, l.Rate, l.ExtendedCost,
        }));

        var snap = new ProjectEstimateSnapshot
        {
            CompanyId = est.CompanyId,
            CustomerProjectId = est.CustomerProjectId,
            ProjectEstimateId = est.Id,
            ProjectQuoteRevisionId = req.RevisionId,
            Currency = est.Currency,
            MaterialCost = b.material, LaborCost = b.labor, SubcontractCost = b.subcontract,
            OverheadCost = b.overhead, OtherCost = b.other, DirectTotalCost = b.direct,
            ContingencyPct = est.ContingencyPct, TotalCost = total,
            QuotedPrice = price, EstimatedMarginPct = margin, TargetMarginPct = est.TargetMarginPct,
            LineCount = lines.Count, FrozenLinesJson = frozenJson,
            CapturedAt = DateTime.UtcNow, CapturedBy = req.CapturedBy,
        };
        _db.ProjectEstimateSnapshots.Add(snap);

        est.Status = ProjectEstimateStatus.Snapshotted;
        est.ModifiedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct); // need snap.Id before wiring the revision link

        if (rev != null)
        {
            rev.SourceEstimateSnapshotId = snap.Id;
            rev.EstimatedMarginPct = margin;
            rev.ModifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return Result.Success(new ProjectEstimateSnapshotSummary(
            snap.Id, snap.ProjectQuoteRevisionId, snap.TotalCost, snap.DirectTotalCost,
            snap.QuotedPrice, snap.EstimatedMarginPct, snap.LineCount, snap.CapturedAt));
    }

    public async Task<Result<ProjectEstimateSnapshotSummary>> GetSnapshotAsync(int snapshotId, CancellationToken ct = default)
    {
        var s = await _db.ProjectEstimateSnapshots
            .Where(x => x.Id == snapshotId && _tenant.VisibleCompanyIds.Contains(x.CompanyId))
            .Select(x => new ProjectEstimateSnapshotSummary(
                x.Id, x.ProjectQuoteRevisionId, x.TotalCost, x.DirectTotalCost,
                x.QuotedPrice, x.EstimatedMarginPct, x.LineCount, x.CapturedAt))
            .FirstOrDefaultAsync(ct);
        return s == null
            ? Result.Failure<ProjectEstimateSnapshotSummary>($"Estimate snapshot {snapshotId} not found in your tenant scope.")
            : Result.Success(s);
    }

    // Direct dollar value for a line: prefer qty×unitcost, else hours×rate.
    private static decimal? ComputeExtended(decimal qty, decimal? unitCost, decimal? hours, decimal? rate)
    {
        if (unitCost.HasValue) return qty * unitCost.Value;
        if (hours.HasValue && rate.HasValue) return hours.Value * rate.Value;
        return null;
    }

    // Map the B7 cost-element split into the spec's quote buckets.
    private static (decimal material, decimal labor, decimal subcontract, decimal overhead, decimal other, decimal direct)
        Bucketize(IEnumerable<(CostElementType type, decimal amount)> lines)
    {
        decimal material = 0, labor = 0, subcontract = 0, overhead = 0, other = 0;
        foreach (var (type, amount) in lines)
        {
            switch (type)
            {
                case CostElementType.Material: material += amount; break;
                case CostElementType.Labor:
                case CostElementType.Setup: labor += amount; break;
                case CostElementType.Subcontract: subcontract += amount; break;
                case CostElementType.VariableOverhead:
                case CostElementType.FixedOverhead:
                case CostElementType.Tooling: overhead += amount; break;
                default: other += amount; break;
            }
        }
        decimal direct = material + labor + subcontract + overhead + other;
        return (material, labor, subcontract, overhead, other, direct);
    }

    private static decimal ApplyContingency(decimal direct, decimal? contingencyPct) =>
        contingencyPct is { } c && c != 0m
            ? Math.Round(direct * (1m + c / 100m), 4)
            : direct;
}
