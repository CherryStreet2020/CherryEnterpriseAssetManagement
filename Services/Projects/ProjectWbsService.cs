// Theme B9 Wave 3 PR-7 (2026-05-30) — ProjectWbsService impl.
//
// Tenant-scoped through the parent CustomerProject. Hosts the weighted WBS
// roll-up, the 100%-rule validator, and the BaselineProjectWbs §20 gate
// (owner + cost bucket on every leaf + 100%-rule, set-once).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectWbsService : IProjectWbsService
{
    // Weight-sum tolerance for the 100%-rule (rounding slack).
    private const decimal WeightTolerance = 0.01m;

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectWbsService> _log;

    public ProjectWbsService(AppDbContext db, ITenantContext tenant, ILogger<ProjectWbsService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    // Tenant gate: a project is reachable only if its CompanyId is visible.
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

    // ------------------------------------------------------------------
    // Read: WBS tree + weighted roll-up.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectWbsRollup>> GetWbsRollupAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, _, err) = await ResolveProjectCompanyAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectWbsRollup>(err!);

        var phases = await _db.ProjectPhases
            .Where(p => p.CustomerProjectId == projectId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new RawPhase
            {
                Id = p.Id,
                ParentPhaseId = p.ParentPhaseId,
                Code = p.Code,
                Name = p.Name,
                WbsType = p.WbsType,
                WbsLevel = p.WbsLevel,
                Status = p.Status,
                ResponsibleOwner = p.ResponsibleOwner,
                ControlAccount = p.ControlAccount,
                WeightPercent = p.WeightPercent,
                CustomerVisible = p.CustomerVisible,
                IsBaselined = p.IsBaselined,
                PlannedCost = p.PlannedCost,
                ActualCost = p.ActualCost,
                CommittedCost = p.CommittedCost,
                ForecastCost = p.ForecastCost,
                PercentComplete = p.PercentComplete,
            })
            .ToListAsync(ct);

        var roots = BuildTree(phases, out _);
        foreach (var r in roots) Fold(r);

        decimal tp = roots.Sum(r => r.PlannedCost);
        decimal ta = roots.Sum(r => r.ActualCost);
        decimal tc = roots.Sum(r => r.CommittedCost);
        decimal tf = roots.Sum(r => r.ForecastCost);
        decimal rolled = WeightedPercent(roots);
        bool anyBaselined = phases.Any(p => p.IsBaselined);

        return Result.Success(new ProjectWbsRollup(
            projectId, anyBaselined, tp, ta, tc, tf, rolled, roots));
    }

    // ------------------------------------------------------------------
    // Write: update a phase's WBS attributes (null = leave unchanged).
    // ------------------------------------------------------------------
    public async Task<Result<ProjectPhase>> UpdatePhaseWbsAsync(UpdatePhaseWbsRequest req, CancellationToken ct = default)
    {
        if (req is null || req.PhaseId <= 0)
            return Result.Failure<ProjectPhase>("A valid PhaseId is required.");

        var phase = await _db.ProjectPhases.FirstOrDefaultAsync(p => p.Id == req.PhaseId, ct);
        if (phase == null)
            return Result.Failure<ProjectPhase>($"Phase {req.PhaseId} not found.");

        // Tenant gate via the parent project (also hides cross-tenant phases).
        var (ok, _, err) = await ResolveProjectCompanyAsync(phase.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectPhase>(err!);

        if (req.WeightPercent is < 0 or > 100)
            return Result.Failure<ProjectPhase>("WeightPercent must be between 0 and 100.");
        if (req.PercentComplete is < 0 or > 100)
            return Result.Failure<ProjectPhase>("PercentComplete must be between 0 and 100.");
        if (req.PlannedCost is < 0 || req.ActualCost is < 0 || req.CommittedCost is < 0 || req.ForecastCost is < 0)
            return Result.Failure<ProjectPhase>("Cost values cannot be negative.");

        if (req.WbsType.HasValue) phase.WbsType = req.WbsType.Value;
        if (req.ResponsibleOwner != null) phase.ResponsibleOwner = req.ResponsibleOwner.Trim();
        if (req.ResponsibleDepartment != null) phase.ResponsibleDepartment = req.ResponsibleDepartment.Trim();
        if (req.ControlAccount != null) phase.ControlAccount = req.ControlAccount.Trim();
        if (req.PlannedCost.HasValue) phase.PlannedCost = req.PlannedCost;
        if (req.ActualCost.HasValue) phase.ActualCost = req.ActualCost;
        if (req.CommittedCost.HasValue) phase.CommittedCost = req.CommittedCost;
        if (req.ForecastCost.HasValue) phase.ForecastCost = req.ForecastCost;
        if (req.WeightPercent.HasValue) phase.WeightPercent = req.WeightPercent;
        if (req.PercentComplete.HasValue) phase.PercentComplete = req.PercentComplete;
        if (req.ForecastStart.HasValue) phase.ForecastStart = req.ForecastStart;
        if (req.ForecastFinish.HasValue) phase.ForecastFinish = req.ForecastFinish;
        if (req.ActualStart.HasValue) phase.ActualStart = req.ActualStart;
        if (req.ActualFinish.HasValue) phase.ActualFinish = req.ActualFinish;
        if (req.Status.HasValue) phase.Status = req.Status.Value;
        if (req.CustomerVisible.HasValue) phase.CustomerVisible = req.CustomerVisible.Value;

        await _db.SaveChangesAsync(ct);
        return Result.Success(phase);
    }

    // ------------------------------------------------------------------
    // Validate the 100%-rule: every sibling group sums to 100 (±tolerance).
    // ------------------------------------------------------------------
    public async Task<Result<HundredPercentRuleResult>> ValidateHundredPercentRuleAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, _, err) = await ResolveProjectCompanyAsync(projectId, ct);
        if (!ok) return Result.Failure<HundredPercentRuleResult>(err!);

        var phases = await _db.ProjectPhases
            .Where(p => p.CustomerProjectId == projectId)
            .Select(p => new { p.Id, p.ParentPhaseId, p.WeightPercent })
            .ToListAsync(ct);

        var violations = new List<WeightGroupViolation>();
        foreach (var grp in phases.GroupBy(p => p.ParentPhaseId))
        {
            decimal sum = grp.Sum(g => g.WeightPercent ?? 0m);
            if (Math.Abs(sum - 100m) > WeightTolerance)
            {
                violations.Add(new WeightGroupViolation(
                    grp.Key,
                    grp.Key is null ? "Project roots" : $"Children of phase {grp.Key}",
                    sum));
            }
        }

        return Result.Success(new HundredPercentRuleResult(violations.Count == 0, violations));
    }

    // ------------------------------------------------------------------
    // Baseline the WBS — the §20 gate. Set-once.
    // ------------------------------------------------------------------
    public async Task<Result<BaselineWbsOutcome>> BaselineProjectWbsAsync(BaselineProjectWbsRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<BaselineWbsOutcome>("Request is required.");
        var (ok, _, err) = await ResolveProjectCompanyAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<BaselineWbsOutcome>(err!);

        var phases = await _db.ProjectPhases
            .Where(p => p.CustomerProjectId == req.CustomerProjectId)
            .ToListAsync(ct);

        if (phases.Count == 0)
            return Result.Failure<BaselineWbsOutcome>("Cannot baseline a project with no WBS phases.");

        // Set-once guard: an explicit re-baseline flag is required to overwrite
        // a frozen baseline (Lock: set-once fields need a re-entry guard).
        bool alreadyBaselined = phases.Any(p => p.IsBaselined);
        if (alreadyBaselined && !req.AllowRebaseline)
            return Result.Failure<BaselineWbsOutcome>(
                "This project WBS is already baselined. Pass AllowRebaseline to re-baseline it.");

        // Leaves = phases that are not the parent of any other phase.
        var parentIds = phases.Where(p => p.ParentPhaseId.HasValue)
                               .Select(p => p.ParentPhaseId!.Value)
                               .ToHashSet();
        var leaves = phases.Where(p => !parentIds.Contains(p.Id)).ToList();

        // Gate 1+2: every leaf needs a Responsible Owner AND a cost bucket.
        var missing = new List<string>();
        foreach (var leaf in leaves)
        {
            bool noOwner = string.IsNullOrWhiteSpace(leaf.ResponsibleOwner);
            bool noCost = leaf.PlannedCost is null;
            if (noOwner && noCost)
                missing.Add($"'{leaf.Code}' (owner + planned cost)");
            else if (noOwner)
                missing.Add($"'{leaf.Code}' (owner)");
            else if (noCost)
                missing.Add($"'{leaf.Code}' (planned cost)");
        }
        if (missing.Count > 0)
            return Result.Failure<BaselineWbsOutcome>(
                "Cannot baseline — these leaf WBS elements are missing required data: " +
                string.Join(", ", missing) + ".");

        // Gate 3: the 100%-rule must hold across every sibling group.
        var weightViolations = new List<string>();
        foreach (var grp in phases.GroupBy(p => p.ParentPhaseId))
        {
            decimal sum = grp.Sum(g => g.WeightPercent ?? 0m);
            if (Math.Abs(sum - 100m) > WeightTolerance)
            {
                string scope = grp.Key is null ? "project roots" : $"children of phase {grp.Key}";
                weightViolations.Add($"{scope} sum to {sum:0.##}%");
            }
        }
        if (weightViolations.Count > 0)
            return Result.Failure<BaselineWbsOutcome>(
                "Cannot baseline — the 100%-rule is violated: " +
                string.Join("; ", weightViolations) + " (each sibling group must total 100%).");

        // Passed. Freeze the baseline on every phase.
        var now = DateTime.UtcNow;
        foreach (var p in phases)
        {
            p.IsBaselined = true;
            p.BaselinedAt = now;
            p.BaselinedBy = req.BaselinedBy;
            // Freeze the baseline schedule from the current forecast.
            p.BaselineStart = p.ForecastStart;
            p.BaselineFinish = p.ForecastFinish;
        }
        await _db.SaveChangesAsync(ct);

        decimal totalPlanned = leaves.Sum(l => l.PlannedCost ?? 0m);
        _log.LogInformation(
            "Baselined WBS for project {ProjectId}: {Phases} phases, {Leaves} leaves, planned {Total}. Rebaseline={Re}.",
            req.CustomerProjectId, phases.Count, leaves.Count, totalPlanned, alreadyBaselined);

        return Result.Success(new BaselineWbsOutcome(
            req.CustomerProjectId, phases.Count, leaves.Count, totalPlanned, alreadyBaselined));
    }

    // ------------------------------------------------------------------
    // Tree construction + bottom-up fold (cost sum + weighted %-complete).
    // ------------------------------------------------------------------
    private sealed class RawPhase
    {
        public int Id { get; set; }
        public int? ParentPhaseId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public WbsType WbsType { get; set; }
        public int WbsLevel { get; set; }
        public ProjectPhaseStatus Status { get; set; }
        public string? ResponsibleOwner { get; set; }
        public string? ControlAccount { get; set; }
        public decimal? WeightPercent { get; set; }
        public bool CustomerVisible { get; set; }
        public bool IsBaselined { get; set; }
        public decimal? PlannedCost { get; set; }
        public decimal? ActualCost { get; set; }
        public decimal? CommittedCost { get; set; }
        public decimal? ForecastCost { get; set; }
        public decimal? PercentComplete { get; set; }
    }

    private static List<WbsNode> BuildTree(List<RawPhase> phases, out Dictionary<int, WbsNode> byId)
    {
        var parentIds = phases.Where(p => p.ParentPhaseId.HasValue)
                              .Select(p => p.ParentPhaseId!.Value).ToHashSet();
        byId = new Dictionary<int, WbsNode>();
        foreach (var p in phases)
        {
            byId[p.Id] = new WbsNode
            {
                Id = p.Id,
                ParentPhaseId = p.ParentPhaseId,
                Code = p.Code,
                Name = p.Name,
                WbsType = p.WbsType,
                WbsLevel = p.WbsLevel,
                Status = p.Status,
                ResponsibleOwner = p.ResponsibleOwner,
                ControlAccount = p.ControlAccount,
                WeightPercent = p.WeightPercent,
                CustomerVisible = p.CustomerVisible,
                IsBaselined = p.IsBaselined,
                IsLeaf = !parentIds.Contains(p.Id),
                // Seed leaf-or-own values; parents are overwritten in Fold.
                PlannedCost = p.PlannedCost ?? 0m,
                ActualCost = p.ActualCost ?? 0m,
                CommittedCost = p.CommittedCost ?? 0m,
                ForecastCost = p.ForecastCost ?? 0m,
                PercentComplete = p.PercentComplete ?? 0m,
            };
        }

        var roots = new List<WbsNode>();
        foreach (var p in phases)
        {
            var node = byId[p.Id];
            if (p.ParentPhaseId.HasValue && byId.TryGetValue(p.ParentPhaseId.Value, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }

    // Bottom-up: a parent's cost is the sum of its children; its %-complete is
    // the weighted roll-up (WeightPercent → cost → equal, in that fallback order).
    private static void Fold(WbsNode node)
    {
        if (node.Children.Count == 0) return; // leaf keeps its own seeded values

        foreach (var c in node.Children) Fold(c);

        node.PlannedCost = node.Children.Sum(c => c.PlannedCost);
        node.ActualCost = node.Children.Sum(c => c.ActualCost);
        node.CommittedCost = node.Children.Sum(c => c.CommittedCost);
        node.ForecastCost = node.Children.Sum(c => c.ForecastCost);
        node.PercentComplete = WeightedPercent(node.Children);
    }

    private static decimal WeightedPercent(IReadOnlyList<WbsNode> children)
    {
        if (children.Count == 0) return 0m;

        // 1) WeightPercent when every child has one and they sum > 0.
        if (children.All(c => c.WeightPercent.HasValue))
        {
            decimal wsum = children.Sum(c => c.WeightPercent!.Value);
            if (wsum > 0m)
                return Math.Round(children.Sum(c => c.PercentComplete * c.WeightPercent!.Value) / wsum, 2);
        }

        // 2) Planned-cost weighting.
        decimal csum = children.Sum(c => c.PlannedCost);
        if (csum > 0m)
            return Math.Round(children.Sum(c => c.PercentComplete * c.PlannedCost) / csum, 2);

        // 3) Equal weighting.
        return Math.Round(children.Average(c => c.PercentComplete), 2);
    }
}
