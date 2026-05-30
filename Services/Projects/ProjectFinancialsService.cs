// Theme B9 Wave 5 PR-12 (2026-05-30) — ProjectFinancialsService impl. The margin engine.
//
// Tenant-scoped through the parent CustomerProject. Computes the live position
// (Contract / Budget / Actual / Committed / ETC / EAC / Margin) and freezes it
// into immutable EAC snapshots. CommittedCost is wired from the PR-10
// procurement commitments (open balance). Every incoming FK on a write is
// scoped to the project's company.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;   // CostElementType
using Abs.FixedAssets.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectFinancialsService : IProjectFinancialsService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectFinancialsService> _log;

    public ProjectFinancialsService(AppDbContext db, ITenantContext tenant, ILogger<ProjectFinancialsService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    private async Task<(bool ok, string? err, int? companyId, CustomerProjectStatus status)> ProjectInfoAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, "CustomerProjectId must be > 0.", null, default);
        var row = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.CompanyId, p.Status })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? (false, $"Customer project {projectId} not found in your tenant scope.", null, default)
            : (true, null, row.CompanyId, row.Status);
    }

    private Task<bool> PhaseInProjectAsync(int phaseId, int projectId, CancellationToken ct)
        => _db.ProjectPhases.AnyAsync(p => p.Id == phaseId && p.CustomerProjectId == projectId, ct);

    // ------------------------------------------------------------------
    // The shared live-position computation (used by read + snapshot).
    // ------------------------------------------------------------------
    private sealed class LivePosition
    {
        public string Currency = "USD";
        public decimal ContractValue;
        public decimal BudgetTotal;
        public decimal ActualCostToDate;
        public decimal CommittedCost;
        public decimal EstimateToComplete;
        public decimal EstimateAtCompletion;
        public decimal ProjectedMargin;
        public decimal? ProjectedMarginPercent;
        public decimal? PercentComplete;
        public int? BaselineBudgetId;
        public List<ProjectBudget> Budgets = new();
        public Dictionary<int, decimal> BudgetLineTotals = new();   // budgetId → line total
        public List<ProjectForecast> Forecasts = new();
        public Dictionary<CostElementType, decimal> BudgetByElement = new();
        public Dictionary<CostElementType, decimal> ActualByElement = new();
    }

    private async Task<LivePosition> ComputeAsync(int projectId, CancellationToken ct)
    {
        var pos = new LivePosition();
        var proj = await _db.CustomerProjects
            .Where(p => p.Id == projectId)
            .Select(p => new { p.ContractValue, p.PercentComplete, p.Currency })
            .FirstAsync(ct);
        pos.ContractValue = proj.ContractValue ?? 0m;
        pos.PercentComplete = proj.PercentComplete;
        if (!string.IsNullOrWhiteSpace(proj.Currency)) pos.Currency = proj.Currency!;

        // Budgets + their line totals. Baseline = first locked, else latest.
        pos.Budgets = await _db.ProjectBudgets
            .Where(b => b.CustomerProjectId == projectId && b.Status != ProjectBudgetStatus.Cancelled)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToListAsync(ct);
        var budgetIds = pos.Budgets.Select(b => b.Id).ToList();
        var lines = await _db.ProjectBudgetLines
            .Where(l => budgetIds.Contains(l.ProjectBudgetId)).ToListAsync(ct);
        pos.BudgetLineTotals = lines.GroupBy(l => l.ProjectBudgetId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.BudgetAmount));

        var baseline = pos.Budgets.FirstOrDefault(b => b.IsLocked) ?? pos.Budgets.LastOrDefault();
        pos.BaselineBudgetId = baseline?.Id;
        if (baseline is not null)
        {
            var baseLines = lines.Where(l => l.ProjectBudgetId == baseline.Id).ToList();
            pos.BudgetTotal = baseLines.Sum(l => l.BudgetAmount);
            pos.BudgetByElement = baseLines.GroupBy(l => l.CostElementType)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.BudgetAmount));
        }

        // Actuals.
        var actuals = await _db.ProjectActualCosts
            .Where(a => a.CustomerProjectId == projectId).ToListAsync(ct);
        pos.ActualCostToDate = actuals.Sum(a => a.Amount);
        pos.ActualByElement = actuals.GroupBy(a => a.CostElementType)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // Committed = open balance of Open/PartiallyReceived commitments (PR-10 cross-wire).
        var openCommitments = await _db.ProjectCommitments
            .Where(c => c.CustomerProjectId == projectId
                && (c.Status == ProjectCommitmentStatus.Open || c.Status == ProjectCommitmentStatus.PartiallyReceived))
            .Select(c => new { c.Id, c.CommittedAmount }).ToListAsync(ct);
        if (openCommitments.Count > 0)
        {
            var openIds = openCommitments.Select(c => c.Id).ToList();
            var receivedById = await _db.ProjectReceipts
                .Where(r => openIds.Contains(r.ProjectCommitmentId))
                .GroupBy(r => r.ProjectCommitmentId)
                .Select(g => new { Id = g.Key, Recv = g.Sum(x => x.ReceivedAmount) })
                .ToDictionaryAsync(x => x.Id, x => x.Recv, ct);
            pos.CommittedCost = openCommitments.Sum(c =>
                Math.Max(0m, c.CommittedAmount - (receivedById.TryGetValue(c.Id, out var r) ? r : 0m)));
        }

        // Forecasts → ETC. Latest forecast per element by date then id.
        pos.Forecasts = await _db.ProjectForecasts
            .Where(f => f.CustomerProjectId == projectId)
            .OrderByDescending(f => f.ForecastDate).ThenByDescending(f => f.Id).ToListAsync(ct);
        var latestPerElement = pos.Forecasts
            .GroupBy(f => f.CostElementType)
            .Select(g => g.First())   // already ordered desc → first = latest
            .ToList();
        if (latestPerElement.Count > 0)
            // Per element: prefer an explicit ETC; otherwise derive it from an
            // EAC-only forecast as max(0, EAC − actual-for-that-element) so an
            // EstimateAtCompletion left with a null EstimateToComplete (the
            // ManualEac default) still flows into ETC instead of counting as
            // zero and overstating margin (Codex P2).
            pos.EstimateToComplete = latestPerElement.Sum(f =>
            {
                if (f.EstimateToComplete.HasValue) return f.EstimateToComplete.Value;
                if (f.EstimateAtCompletion.HasValue)
                {
                    var actualEl = pos.ActualByElement.TryGetValue(f.CostElementType, out var ae) ? ae : 0m;
                    return Math.Max(0m, f.EstimateAtCompletion.Value - actualEl);
                }
                return 0m;
            });
        else
            pos.EstimateToComplete = Math.Max(0m, pos.BudgetTotal - pos.ActualCostToDate);

        pos.EstimateAtCompletion = pos.ActualCostToDate + pos.EstimateToComplete;
        pos.ProjectedMargin = pos.ContractValue - pos.EstimateAtCompletion;
        pos.ProjectedMarginPercent = pos.ContractValue > 0m
            ? Math.Round(pos.ProjectedMargin / pos.ContractValue * 100m, 2)
            : (decimal?)null;
        return pos;
    }

    // ------------------------------------------------------------------
    // Read — THE margin engine.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectFinancialsView>> GetFinancialsAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err, _, _) = await ProjectInfoAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectFinancialsView>(err!);

        var pos = await ComputeAsync(projectId, ct);

        var elementKeys = pos.BudgetByElement.Keys.Union(pos.ActualByElement.Keys).Distinct()
            .OrderBy(k => (int)k).ToList();
        var byElement = elementKeys.Select(k =>
        {
            var b = pos.BudgetByElement.TryGetValue(k, out var bb) ? bb : 0m;
            var a = pos.ActualByElement.TryGetValue(k, out var aa) ? aa : 0m;
            return new ElementPositionRow(k, b, a, b - a);
        }).ToList();

        var budgetRows = pos.Budgets.Select(b => new BudgetRow(
            b.Id, b.Code, b.Name, b.BudgetType, b.Status, b.IsLocked,
            pos.BudgetLineTotals.TryGetValue(b.Id, out var t) ? t : 0m)).ToList();

        var forecastRows = pos.Forecasts.Select(f => new ForecastRow(
            f.Id, f.CostElementType, f.Method, f.EstimateToComplete, f.EstimateAtCompletion, f.ForecastDate)).ToList();

        var snap = await _db.ProjectEacSnapshots
            .Where(s => s.CustomerProjectId == projectId)
            .OrderByDescending(s => s.SnapshotDate).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);
        var snapRow = snap is null ? null : new EacSnapshotRow(
            snap.Id, snap.SnapshotDate, snap.SnapshotReason, snap.ContractValue, snap.BudgetTotal,
            snap.ActualCostToDate, snap.CommittedCost, snap.EstimateToComplete, snap.EstimateAtCompletion,
            snap.ProjectedMargin, snap.ProjectedMarginPercent);

        return Result.Success(new ProjectFinancialsView(
            projectId, pos.Currency, pos.ContractValue, pos.BudgetTotal, pos.ActualCostToDate,
            pos.CommittedCost, pos.EstimateToComplete, pos.EstimateAtCompletion, pos.ProjectedMargin,
            pos.ProjectedMarginPercent, pos.PercentComplete, pos.BaselineBudgetId,
            byElement, budgetRows, forecastRows, snapRow));
    }

    // ------------------------------------------------------------------
    // Create budget
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateBudgetAsync(CreateBudgetRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code)) return Result.Failure<int>("A budget Code is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) return Result.Failure<int>("A budget Name is required.");
        var (ok, err, _, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        var code = req.Code.Trim();
        if (await _db.ProjectBudgets.AnyAsync(b => b.CustomerProjectId == req.CustomerProjectId && b.Code == code, ct))
            return Result.Failure<int>($"Budget Code '{code}' already exists in this project.");

        var budget = new ProjectBudget
        {
            CustomerProjectId = req.CustomerProjectId,
            Code = code,
            Name = req.Name.Trim(),
            Description = req.Description,
            BudgetType = req.BudgetType,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            SortOrder = req.SortOrder,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectBudgets.Add(budget);
        await _db.SaveChangesAsync(ct);
        return Result.Success(budget.Id);
    }

    // ------------------------------------------------------------------
    // Add budget line — rejected once the budget is locked.
    // ------------------------------------------------------------------
    public async Task<Result<int>> AddBudgetLineAsync(AddBudgetLineRequest req, CancellationToken ct = default)
    {
        if (req is null || req.ProjectBudgetId <= 0) return Result.Failure<int>("A valid ProjectBudgetId is required.");
        if (req.BudgetAmount < 0) return Result.Failure<int>("BudgetAmount cannot be negative.");
        if (req.Quantity is < 0 || req.UnitCost is < 0) return Result.Failure<int>("Quantity / unit cost cannot be negative.");

        var budget = await _db.ProjectBudgets.FirstOrDefaultAsync(b => b.Id == req.ProjectBudgetId, ct);
        if (budget is null) return Result.Failure<int>($"Budget {req.ProjectBudgetId} not found.");
        var (ok, err, _, _) = await ProjectInfoAsync(budget.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (budget.IsLocked) return Result.Failure<int>($"Budget '{budget.Code}' is locked — its lines are frozen.");

        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, budget.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (await _db.ProjectBudgetLines.AnyAsync(l => l.ProjectBudgetId == req.ProjectBudgetId && l.LineNo == req.LineNo, ct))
            return Result.Failure<int>($"Line {req.LineNo} already exists in this budget.");

        var line = new ProjectBudgetLine
        {
            ProjectBudgetId = req.ProjectBudgetId,
            ProjectPhaseId = req.ProjectPhaseId,
            LineNo = req.LineNo,
            CostElementType = req.CostElementType,
            Description = req.Description,
            Quantity = req.Quantity,
            UnitCost = req.UnitCost,
            BudgetAmount = req.BudgetAmount,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectBudgetLines.Add(line);
        await _db.SaveChangesAsync(ct);
        return Result.Success(line.Id);
    }

    // ------------------------------------------------------------------
    // Lock budget — set-once baseline.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectBudget>> LockBudgetAsync(int budgetId, string? lockedBy = null, CancellationToken ct = default)
    {
        if (budgetId <= 0) return Result.Failure<ProjectBudget>("A valid budget id is required.");
        var budget = await _db.ProjectBudgets.FirstOrDefaultAsync(b => b.Id == budgetId, ct);
        if (budget is null) return Result.Failure<ProjectBudget>($"Budget {budgetId} not found.");
        var (ok, err, _, _) = await ProjectInfoAsync(budget.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectBudget>(err!);
        if (budget.IsLocked) return Result.Failure<ProjectBudget>($"Budget '{budget.Code}' is already locked.");
        if (budget.Status == ProjectBudgetStatus.Cancelled)
            return Result.Failure<ProjectBudget>($"Budget '{budget.Code}' is cancelled and cannot be locked.");

        budget.IsLocked = true;
        budget.Status = ProjectBudgetStatus.Locked;
        budget.LockedAt = DateTime.UtcNow;
        budget.LockedBy = lockedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(budget);
    }

    // ------------------------------------------------------------------
    // Post actual cost — blocked on Closed/Cancelled project.
    // ------------------------------------------------------------------
    public async Task<Result<int>> PostActualCostAsync(PostActualCostRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        if (req.Amount < 0) return Result.Failure<int>("Amount cannot be negative.");
        var (ok, err, companyId, status) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (status == CustomerProjectStatus.Closed || status == CustomerProjectStatus.Cancelled)
            return Result.Failure<int>($"Cannot post actual cost — project is {status}.");

        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (req.ProjectTaskId.HasValue && !await _db.ProjectTasks.AnyAsync(
                t => t.Id == req.ProjectTaskId.Value && t.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Task {req.ProjectTaskId} is not in this project.");

        var actual = new ProjectActualCost
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectPhaseId = req.ProjectPhaseId,
            ProjectTaskId = req.ProjectTaskId,
            CostElementType = req.CostElementType,
            SourceType = req.SourceType,
            SourceId = req.SourceId,
            PostingReference = string.IsNullOrWhiteSpace(req.PostingReference) ? null : req.PostingReference.Trim(),
            Description = req.Description,
            Amount = req.Amount,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            PostingDate = req.PostingDate == default ? DateTime.UtcNow.Date : req.PostingDate,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectActualCosts.Add(actual);
        await _db.SaveChangesAsync(ct);
        return Result.Success(actual.Id);
    }

    // ------------------------------------------------------------------
    // Create forecast
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateForecastAsync(CreateForecastRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        if (req.EstimateToComplete is < 0 || req.EstimateAtCompletion is < 0)
            return Result.Failure<int>("Forecast amounts cannot be negative.");
        var (ok, err, _, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        if (req.ProjectBudgetId.HasValue && !await _db.ProjectBudgets.AnyAsync(
                b => b.Id == req.ProjectBudgetId.Value && b.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Budget {req.ProjectBudgetId} is not in this project.");
        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");

        var forecast = new ProjectForecast
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectBudgetId = req.ProjectBudgetId,
            ProjectPhaseId = req.ProjectPhaseId,
            CostElementType = req.CostElementType,
            Method = req.Method,
            EstimateToComplete = req.EstimateToComplete,
            EstimateAtCompletion = req.EstimateAtCompletion,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            ForecastDate = req.ForecastDate == default ? DateTime.UtcNow.Date : req.ForecastDate,
            Notes = req.Notes,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectForecasts.Add(forecast);
        await _db.SaveChangesAsync(ct);
        return Result.Success(forecast.Id);
    }

    // ------------------------------------------------------------------
    // Snapshot EAC — freeze the live position (immutable margin bridge).
    // ------------------------------------------------------------------
    public async Task<Result<int>> SnapshotEacAsync(SnapshotEacRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err, _, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (req.ProjectBudgetId.HasValue && !await _db.ProjectBudgets.AnyAsync(
                b => b.Id == req.ProjectBudgetId.Value && b.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Budget {req.ProjectBudgetId} is not in this project.");

        var pos = await ComputeAsync(req.CustomerProjectId, ct);

        var elementKeys = pos.BudgetByElement.Keys.Union(pos.ActualByElement.Keys).Distinct().OrderBy(k => (int)k);
        var breakdown = elementKeys.Select(k => new
        {
            element = k.ToString(),
            budget = pos.BudgetByElement.TryGetValue(k, out var b) ? b : 0m,
            actual = pos.ActualByElement.TryGetValue(k, out var a) ? a : 0m,
        }).ToList();

        var snap = new ProjectEACSnapshot
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectBudgetId = req.ProjectBudgetId ?? pos.BaselineBudgetId,
            SnapshotDate = DateTime.UtcNow,
            SnapshotReason = string.IsNullOrWhiteSpace(req.SnapshotReason) ? null : req.SnapshotReason.Trim(),
            ContractValue = pos.ContractValue,
            BudgetTotal = pos.BudgetTotal,
            ActualCostToDate = pos.ActualCostToDate,
            CommittedCost = pos.CommittedCost,
            EstimateToComplete = pos.EstimateToComplete,
            EstimateAtCompletion = pos.EstimateAtCompletion,
            PercentComplete = pos.PercentComplete,
            ProjectedMargin = pos.ProjectedMargin,
            ProjectedMarginPercent = pos.ProjectedMarginPercent,
            Currency = pos.Currency,
            FrozenBreakdownJson = JsonSerializer.Serialize(breakdown),
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectEacSnapshots.Add(snap);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("EAC snapshot {Id} for project {ProjectId}: margin {Margin} ({Pct}%).",
            snap.Id, req.CustomerProjectId, snap.ProjectedMargin, snap.ProjectedMarginPercent);
        return Result.Success(snap.Id);
    }
}
