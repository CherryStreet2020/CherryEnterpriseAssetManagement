// Theme B9 Wave 1 PR-3 (2026-05-30, CLOSES B9 Wave 1) — ProjectGraphService impl.
// READ-ONLY assembly of the project lifecycle graph from the CustomerProject
// substrate. Tenant-scoped on every read. No mutations.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectGraphService : IProjectGraphService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectGraphService> _log;

    // Cap node fan-out so a very large project can't blow up the canvas.
    private const int MaxPhases = 60;
    private const int MaxJobs = 120;

    public ProjectGraphService(AppDbContext db, ITenantContext tenant, ILogger<ProjectGraphService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    public async Task<Result<ProjectGraph>> GetGraphAsync(int customerProjectId, CancellationToken ct = default)
    {
        if (customerProjectId <= 0)
            return Result.Failure<ProjectGraph>("CustomerProjectId must be > 0.");

        var p = await _db.CustomerProjects
            .Where(x => x.Id == customerProjectId && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
            .Select(x => new
            {
                x.Id, x.Code, x.Name, x.Status,
                x.ContractValue, x.EstimatedTotalCost, x.PercentComplete, x.LastEvmRollupAt, x.Currency,
            })
            .FirstOrDefaultAsync(ct);
        if (p == null)
            return Result.Failure<ProjectGraph>(
                $"Customer project {customerProjectId} not found in your tenant scope.");

        var nodes = new List<ProjectGraphNode>(capacity: 32);
        var ccy = string.IsNullOrWhiteSpace(p.Currency) ? "USD" : p.Currency;

        // ── Stage 0: Quote (Wave 2). Dimmed placeholder; the binding contract /
        //    versioned quote spine lands in Wave 2. ──
        const string quoteId = "stage-quote";
        nodes.Add(new ProjectGraphNode(
            Id: quoteId, Stage: ProjectGraphStage.Quote, State: ProjectGraphNodeState.Future,
            Tone: ProjectGraphTone.Neutral,
            Label: "Quote",
            Sublabel: p.Status == CustomerProjectStatus.Quote ? "Project is in quote stage" : "Versioned quote / RFQ",
            DeepLinkHref: null, WaveLabel: "Wave 2", ParentId: null));

        // ── Stage 1: Project (today, root of the live data) ──
        const string projectId = "project";
        nodes.Add(new ProjectGraphNode(
            Id: projectId, Stage: ProjectGraphStage.Project, State: ProjectGraphNodeState.Present,
            Tone: ProjectStatusTone(p.Status),
            Label: $"{p.Code}",
            Sublabel: Trim($"{p.Name} · {p.Status}", 60),
            DeepLinkHref: $"/CustomerProjects/CommandCenter/{p.Id}",
            WaveLabel: null, ParentId: quoteId));

        // ── Stage 2: WBS / ProjectPhase tree (today) ──
        var phases = await _db.ProjectPhases
            .Where(ph => ph.CustomerProjectId == customerProjectId)
            .OrderBy(ph => ph.SortOrder).ThenBy(ph => ph.Id)
            .Select(ph => new { ph.Id, ph.Code, ph.Name, ph.ParentPhaseId })
            .Take(MaxPhases)
            .ToListAsync(ct);

        var renderedPhaseIds = phases.Select(ph => ph.Id).ToHashSet();
        foreach (var ph in phases)
        {
            // Parent = parent phase when it was rendered, else the project root.
            var parent = (ph.ParentPhaseId.HasValue && renderedPhaseIds.Contains(ph.ParentPhaseId.Value))
                ? $"phase-{ph.ParentPhaseId.Value}"
                : projectId;
            nodes.Add(new ProjectGraphNode(
                Id: $"phase-{ph.Id}", Stage: ProjectGraphStage.Wbs, State: ProjectGraphNodeState.Present,
                Tone: ProjectGraphTone.Neutral,
                Label: Trim(string.IsNullOrWhiteSpace(ph.Name) ? ph.Code : ph.Name, 40),
                Sublabel: $"Phase {ph.Code}",
                DeepLinkHref: $"/CustomerProjects/Details/{p.Id}",
                WaveLabel: null, ParentId: parent));
        }

        // ── Stage 3: Job / ProductionOrder execution (today) ──
        var jobs = await _db.ProductionOrders
            .Where(o => o.CustomerProjectId == customerProjectId
                && _tenant.VisibleCompanyIds.Contains(o.CompanyId))
            .OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.OrderNumber, o.Status, o.ProjectPhaseId, o.QuantityOrdered, o.Description })
            .Take(MaxJobs)
            .ToListAsync(ct);

        foreach (var j in jobs)
        {
            // Nest under the job's phase when that phase is on-canvas; else the project root.
            var parent = (j.ProjectPhaseId.HasValue && renderedPhaseIds.Contains(j.ProjectPhaseId.Value))
                ? $"phase-{j.ProjectPhaseId.Value}"
                : projectId;
            nodes.Add(new ProjectGraphNode(
                Id: $"job-{j.Id}", Stage: ProjectGraphStage.Job, State: ProjectGraphNodeState.Present,
                Tone: JobStatusTone(j.Status),
                Label: Trim(j.OrderNumber, 28),
                Sublabel: Trim($"{j.Status} · qty {j.QuantityOrdered:0.##}", 36),
                DeepLinkHref: $"/Production/Orders/{j.Id}/Cockpit",
                WaveLabel: null, ParentId: parent));
        }

        // ── Commercial spine off the project: Purchasing → Receipt → Cost → Billing → Acceptance ──
        // Purchasing + Receipt are Wave 4; Cost is live today when an EVM rollup exists;
        // Billing is Wave 5; Acceptance is Wave 6. Chained so the lifecycle reads L→R.
        const string purchasingId = "stage-purchasing";
        nodes.Add(new ProjectGraphNode(
            Id: purchasingId, Stage: ProjectGraphStage.Purchasing, State: ProjectGraphNodeState.Future,
            Tone: ProjectGraphTone.Neutral,
            Label: "Purchasing", Sublabel: "Project POs & commitments",
            DeepLinkHref: null, WaveLabel: "Wave 4", ParentId: projectId));

        const string receiptId = "stage-receipt";
        nodes.Add(new ProjectGraphNode(
            Id: receiptId, Stage: ProjectGraphStage.Receipt, State: ProjectGraphNodeState.Future,
            Tone: ProjectGraphTone.Neutral,
            Label: "Receipts", Sublabel: "Material received to project",
            DeepLinkHref: null, WaveLabel: "Wave 4", ParentId: purchasingId));

        // Cost / EVM — live when a rollup ran or an estimate exists.
        bool hasEvm = p.EstimatedTotalCost.HasValue || p.PercentComplete.HasValue || p.LastEvmRollupAt.HasValue;
        const string costId = "stage-cost";
        if (hasEvm)
        {
            var pctText = p.PercentComplete.HasValue ? $"{p.PercentComplete.Value:0.#}% complete" : "EVM rollup";
            var eacText = p.EstimatedTotalCost.HasValue ? $"EAC {ccy} {p.EstimatedTotalCost.Value:N0}" : null;
            var costTone = (p.EstimatedTotalCost.HasValue && p.ContractValue.HasValue && p.EstimatedTotalCost.Value > p.ContractValue.Value)
                ? ProjectGraphTone.Bad
                : ProjectGraphTone.Good;
            nodes.Add(new ProjectGraphNode(
                Id: costId, Stage: ProjectGraphStage.Cost, State: ProjectGraphNodeState.Present,
                Tone: costTone,
                Label: "Cost / EVM",
                Sublabel: eacText is null ? pctText : $"{pctText} · {eacText}",
                DeepLinkHref: $"/CustomerProjects/CommandCenter/{p.Id}",
                WaveLabel: null, ParentId: receiptId));
        }
        else
        {
            nodes.Add(new ProjectGraphNode(
                Id: costId, Stage: ProjectGraphStage.Cost, State: ProjectGraphNodeState.Future,
                Tone: ProjectGraphTone.Neutral,
                Label: "Cost / EVM", Sublabel: "No rollup yet",
                DeepLinkHref: null, WaveLabel: "Wave 5", ParentId: receiptId));
        }

        nodes.Add(new ProjectGraphNode(
            Id: "stage-billing", Stage: ProjectGraphStage.Billing, State: ProjectGraphNodeState.Future,
            Tone: ProjectGraphTone.Neutral,
            Label: "Billing", Sublabel: "Invoices & revenue recognition",
            DeepLinkHref: null, WaveLabel: "Wave 5", ParentId: costId));

        nodes.Add(new ProjectGraphNode(
            Id: "stage-acceptance", Stage: ProjectGraphStage.Acceptance, State: ProjectGraphNodeState.Future,
            Tone: ProjectGraphTone.Neutral,
            Label: "Acceptance", Sublabel: "Customer sign-off & closeout",
            DeepLinkHref: null, WaveLabel: "Wave 6", ParentId: "stage-billing"));

        var headline = BuildHeadline(p.Code, phases.Count, jobs.Count, hasEvm);
        return Result.Success(new ProjectGraph(
            p.Id, p.Code, p.Name, headline, phases.Count, jobs.Count, nodes));
    }

    public async Task<Result<ProjectGraph>> GetGraphByRefAsync(string projectRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectRef))
            return Result.Failure<ProjectGraph>("Which project? Say a project code or id.");

        var raw = projectRef.Trim();
        int? pid = null;
        if (int.TryParse(raw, out var asId) && asId > 0)
            pid = await _db.CustomerProjects
                .Where(x => x.Id == asId && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);

        if (pid is null)
        {
            pid = await _db.CustomerProjects
                .Where(x => x.Code == raw && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            pid ??= await _db.CustomerProjects
                .Where(x => x.Code != null && x.Code.StartsWith(raw) && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
                .OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
        }

        if (pid is null or 0)
            return Result.Failure<ProjectGraph>($"I couldn't find a project matching '{raw}' in your scope.");

        return await GetGraphAsync(pid.Value, ct);
    }

    private static string BuildHeadline(string code, int phaseCount, int jobCount, bool hasEvm)
    {
        var execBit = $"{phaseCount} phase{(phaseCount == 1 ? "" : "s")} and {jobCount} linked job{(jobCount == 1 ? "" : "s")}";
        var costBit = hasEvm ? " with a live cost rollup" : "";
        return $"Project {code}: {execBit}{costBit}. Quote, purchasing, receipts, billing and acceptance are mapped as future-wave stages.";
    }

    private static ProjectGraphTone ProjectStatusTone(CustomerProjectStatus s) => s switch
    {
        CustomerProjectStatus.Active => ProjectGraphTone.Good,
        CustomerProjectStatus.OnHold => ProjectGraphTone.Bad,
        CustomerProjectStatus.Closed => ProjectGraphTone.Done,
        CustomerProjectStatus.Cancelled => ProjectGraphTone.Done,
        _ => ProjectGraphTone.Neutral, // Quote
    };

    private static ProjectGraphTone JobStatusTone(ProductionOrderStatus s) => s switch
    {
        ProductionOrderStatus.Released or ProductionOrderStatus.InProgress => ProjectGraphTone.Good,
        ProductionOrderStatus.OnHold => ProjectGraphTone.Bad,
        ProductionOrderStatus.Planned or ProductionOrderStatus.Firmed => ProjectGraphTone.Warning,
        ProductionOrderStatus.Completed or ProductionOrderStatus.Closed => ProjectGraphTone.Done,
        ProductionOrderStatus.Cancelled => ProjectGraphTone.Done,
        _ => ProjectGraphTone.Neutral,
    };

    private static string Trim(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..(max - 1)] + "…");
}
