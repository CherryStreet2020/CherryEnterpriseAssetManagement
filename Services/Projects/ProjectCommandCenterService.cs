// Theme B9 Wave 1 PR-1 (2026-05-30) — ProjectCommandCenterService impl.
// READ-ONLY aggregation over the CustomerProject substrate (see the interface
// for the why). Tenant-scoped on every read. No mutations.

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

public sealed class ProjectCommandCenterService : IProjectCommandCenterService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IProjectQuoteService _quotes;
    private readonly ILogger<ProjectCommandCenterService> _log;

    public ProjectCommandCenterService(
        AppDbContext db, ITenantContext tenant, IProjectQuoteService quotes,
        ILogger<ProjectCommandCenterService> log)
    {
        _db = db; _tenant = tenant; _quotes = quotes; _log = log;
    }

    public async Task<Result<ProjectCommandCenterData>> GetCommandCenterAsync(
        int customerProjectId, CancellationToken ct = default)
    {
        if (customerProjectId <= 0)
            return Result.Failure<ProjectCommandCenterData>("CustomerProjectId must be > 0.");

        var p = await _db.CustomerProjects
            .Where(x => x.Id == customerProjectId
                && _tenant.VisibleCompanyIds.Contains(x.CompanyId ?? 0))
            .Select(x => new
            {
                x.Id, x.Code, x.Name, x.Status, x.Mode,
                CustomerName = x.PrimaryCustomer != null ? x.PrimaryCustomer.Name : null,
                x.ProjectManagerName, x.CustomerPoNumber, x.ContractType, x.QualityProgram,
                x.ContractValue, x.Currency,
                x.TargetStartDate, x.TargetEndDate, x.ProjectedEndDate,
                x.EstimatedTotalCost, x.PercentComplete, x.LastEvmRollupAt,
                x.RiskScore, x.RiskTone, x.AiSummaryText, x.AiSummaryGeneratedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (p == null)
            return Result.Failure<ProjectCommandCenterData>(
                $"Customer project {customerProjectId} not found in your tenant scope.");

        // ── Linked jobs (ProductionOrder.CustomerProjectId), tenant-scoped ──
        var jobStatuses = await _db.ProductionOrders
            .Where(o => o.CustomerProjectId == customerProjectId
                && _tenant.VisibleCompanyIds.Contains(o.CompanyId))
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = jobStatuses
            .OrderByDescending(s => s.Count)
            .Select(s => new ProjectJobStatusCount(s.Status.ToString(), s.Count))
            .ToList();
        int jobTotal = jobStatuses.Sum(s => s.Count);
        int jobCompleted = jobStatuses.Where(s => s.Status is ProductionOrderStatus.Completed or ProductionOrderStatus.Closed).Sum(s => s.Count);
        int jobOnHold = jobStatuses.Where(s => s.Status == ProductionOrderStatus.OnHold).Sum(s => s.Count);
        // Open = actively in-flight: not terminal (Completed/Closed/Cancelled) and not OnHold,
        // so Open + Completed + OnHold + Cancelled partition the total without overlap.
        int jobOpen = jobStatuses.Where(s => s.Status is not (ProductionOrderStatus.Completed or ProductionOrderStatus.Closed or ProductionOrderStatus.Cancelled or ProductionOrderStatus.OnHold)).Sum(s => s.Count);
        var jobs = new ProjectJobRollup(jobTotal, jobOpen, jobCompleted, jobOnHold, byStatus);

        int phaseCount = await _db.ProjectPhases
            .CountAsync(ph => ph.CustomerProjectId == customerProjectId, ct);

        // ── Amendments (the change log) ──
        var amendments = await _db.ProjectAmendments
            .Where(a => a.CustomerProjectId == customerProjectId)
            .Select(a => new { a.Status, a.ValueDelta })
            .ToListAsync(ct);

        int amTotal = amendments.Count;
        int amApproved = amendments.Count(a => a.Status == ProjectAmendmentStatus.Approved);
        int amOpen = amendments.Count(a => a.Status is ProjectAmendmentStatus.Draft or ProjectAmendmentStatus.Submitted);
        decimal approvedDelta = amendments.Where(a => a.Status == ProjectAmendmentStatus.Approved).Sum(a => a.ValueDelta);
        decimal pendingDelta = amendments.Where(a => a.Status is ProjectAmendmentStatus.Draft or ProjectAmendmentStatus.Submitted).Sum(a => a.ValueDelta);
        var amRollup = new ProjectAmendmentRollup(amTotal, amOpen, amApproved, approvedDelta, pendingDelta);

        // ── Commercial / margin ──
        decimal? effectiveContract = p.ContractValue.HasValue
            ? p.ContractValue.Value + approvedDelta
            : (approvedDelta != 0 ? approvedDelta : (decimal?)null);
        decimal? projectedMargin = (effectiveContract.HasValue && p.EstimatedTotalCost.HasValue)
            ? effectiveContract.Value - p.EstimatedTotalCost.Value
            : (decimal?)null;
        decimal? projectedMarginPct = (projectedMargin.HasValue && effectiveContract is { } ec && ec != 0)
            ? Math.Round(projectedMargin.Value / ec * 100m, 1)
            : (decimal?)null;

        int? daysLate = (p.ProjectedEndDate.HasValue && p.TargetEndDate.HasValue)
            ? (int)(p.ProjectedEndDate.Value.Date - p.TargetEndDate.Value.Date).TotalDays
            : (int?)null;

        // ── Quotes (B9 Wave 2 PR-4) — answers "What did we quote?" with live data ──
        IReadOnlyList<ProjectQuoteSummary> quotes = System.Array.Empty<ProjectQuoteSummary>();
        var quotesResult = await _quotes.GetQuotesForProjectAsync(customerProjectId, ct);
        if (quotesResult.IsSuccess && quotesResult.Value is { } qv) quotes = qv;

        var questions = BuildQuestions(p.ContractValue, p.CustomerPoNumber, effectiveContract,
            p.Currency, amRollup, jobs, phaseCount, daysLate, projectedMargin, projectedMarginPct,
            p.EstimatedTotalCost, p.ProjectManagerName, quotes);

        var data = new ProjectCommandCenterData(
            ProjectId: p.Id, Code: p.Code, Name: p.Name, Status: p.Status, Mode: p.Mode,
            CustomerName: p.CustomerName, ProjectManagerName: p.ProjectManagerName,
            CustomerPoNumber: p.CustomerPoNumber, ContractType: p.ContractType, QualityProgram: p.QualityProgram,
            ContractValue: p.ContractValue, EffectiveContractValue: effectiveContract, Currency: p.Currency,
            TargetStartDate: p.TargetStartDate, TargetEndDate: p.TargetEndDate, ProjectedEndDate: p.ProjectedEndDate,
            DaysLateVsTarget: daysLate,
            EstimatedTotalCost: p.EstimatedTotalCost, PercentComplete: p.PercentComplete, LastEvmRollupAt: p.LastEvmRollupAt,
            ProjectedMargin: projectedMargin, ProjectedMarginPct: projectedMarginPct,
            Jobs: jobs, PhaseCount: phaseCount, Amendments: amRollup,
            RiskScore: p.RiskScore, RiskTone: p.RiskTone,
            AiSummaryText: p.AiSummaryText, AiSummaryGeneratedAt: p.AiSummaryGeneratedAt,
            Questions: questions);

        return Result.Success(data);
    }

    // The "answer these immediately" panel (spec §22.4). v1 answers what the current
    // substrate supports; the rest are honest Pending states wired in later B9 waves.
    private static IReadOnlyList<CommandCenterQuestion> BuildQuestions(
        decimal? contractValue, string? customerPo, decimal? effectiveContract, string currency,
        ProjectAmendmentRollup am, ProjectJobRollup jobs, int phaseCount, int? daysLate,
        decimal? projectedMargin, decimal? projectedMarginPct, decimal? estTotalCost, string? pm,
        IReadOnlyList<ProjectQuoteSummary> quotes)
    {
        string Money(decimal v) => $"{currency} {v:N0}";

        // "What did we quote?" — answered from the B9 W2 quote spine.
        var submitted = quotes.Where(q => q.LatestSubmittedTotalPrice.HasValue).ToList();
        string quoteAnswer;
        CommandCenterAnswerState quoteState;
        if (quotes.Count == 0)
        {
            quoteAnswer = "No quotes recorded for this project yet.";
            quoteState = CommandCenterAnswerState.Pending;
        }
        else if (submitted.Count == 0)
        {
            quoteAnswer = $"{quotes.Count} quote(s) in draft — none submitted to the customer yet.";
            quoteState = CommandCenterAnswerState.Attention;
        }
        else
        {
            // The actual LATEST submitted quote = newest submission date (Codex P2),
            // tie-broken by quote id so the result is deterministic.
            var top = submitted
                .OrderByDescending(q => q.LatestSubmittedDate)
                .ThenByDescending(q => q.QuoteId)
                .First();
            quoteAnswer = $"{quotes.Count} quote(s); latest submitted: {top.QuoteNumber} Rev {top.LatestSubmittedRevisionLabel} at {top.Currency} {top.LatestSubmittedTotalPrice!.Value:N0}.";
            quoteState = CommandCenterAnswerState.Answered;
        }

        var list = new List<CommandCenterQuestion>
        {
            new("What did we quote?", quoteAnswer, quoteState),

            // Answerable from header today
            new("What did the customer buy?",
                contractValue.HasValue
                    ? $"{Money(contractValue.Value)}" + (string.IsNullOrWhiteSpace(customerPo) ? "" : $" on PO {customerPo}")
                    : (string.IsNullOrWhiteSpace(customerPo) ? "No contract value or customer PO recorded yet." : $"Customer PO {customerPo} (no value recorded)."),
                contractValue.HasValue || !string.IsNullOrWhiteSpace(customerPo) ? CommandCenterAnswerState.Answered : CommandCenterAnswerState.Pending),

            // Answerable from amendments
            new("What changed?",
                am.Total == 0 ? "No contract amendments." :
                    $"{am.Approved} approved ({Money(am.ApprovedValueDelta)}), {am.Open} open ({Money(am.PendingValueDelta)} pending exposure).",
                am.Open > 0 ? CommandCenterAnswerState.Attention : CommandCenterAnswerState.Answered),

            // Answerable from linked jobs
            new("What are we building?",
                jobs.Total == 0 ? "No production jobs linked yet." :
                    $"{jobs.Total} linked job(s): {jobs.Open} open, {jobs.Completed} done" + (jobs.OnHold > 0 ? $", {jobs.OnHold} on hold" : "") + $" · {phaseCount} phase(s).",
                jobs.OnHold > 0 ? CommandCenterAnswerState.Attention : CommandCenterAnswerState.Answered),

            // Wave 4
            new("What are we buying?", "Project procurement & commitments land in Wave 4.", CommandCenterAnswerState.Pending, "Wave 4"),

            // Answerable from schedule
            new("What is late?",
                !daysLate.HasValue ? "No projected-vs-target schedule yet." :
                    daysLate.Value > 0 ? $"Projected end is {daysLate.Value} day(s) past target." :
                    daysLate.Value < 0 ? $"Projected end is {-daysLate.Value} day(s) ahead of target." : "On the target end date.",
                daysLate is > 0 ? CommandCenterAnswerState.Attention : daysLate.HasValue ? CommandCenterAnswerState.Answered : CommandCenterAnswerState.Pending),

            // Partially answerable: est cost vs effective contract
            new("What is over budget?",
                (effectiveContract.HasValue && estTotalCost.HasValue)
                    ? (estTotalCost.Value > effectiveContract.Value
                        ? $"Est. cost {Money(estTotalCost.Value)} exceeds contract {Money(effectiveContract.Value)}."
                        : $"Est. cost {Money(estTotalCost.Value)} within contract {Money(effectiveContract.Value)}.")
                    : "Full budget engine lands in Wave 5.",
                (effectiveContract.HasValue && estTotalCost.HasValue && estTotalCost.Value > effectiveContract.Value)
                    ? CommandCenterAnswerState.Attention
                    : (effectiveContract.HasValue && estTotalCost.HasValue) ? CommandCenterAnswerState.Answered : CommandCenterAnswerState.Pending),

            // Wave 5
            new("What can we bill?", "Billing schedule & revenue land in Wave 5.", CommandCenterAnswerState.Pending, "Wave 5"),
            new("What blocks shipment?", "Project quality blockers (NCR/MRB/punch) land in Wave 6.", CommandCenterAnswerState.Pending, "Wave 6"),
            new("What blocks acceptance?", "Customer acceptance lifecycle lands in Wave 6.", CommandCenterAnswerState.Pending, "Wave 6"),

            // Answerable from EVM
            new("What will margin be?",
                projectedMargin.HasValue
                    ? $"{Money(projectedMargin.Value)}" + (projectedMarginPct.HasValue ? $" ({projectedMarginPct.Value:0.#}%)" : "")
                    : "Needs an effective contract value and an estimate-at-completion.",
                projectedMargin.HasValue ? (projectedMargin.Value < 0 ? CommandCenterAnswerState.Attention : CommandCenterAnswerState.Answered) : CommandCenterAnswerState.Pending),

            // Answerable from header
            new("Who owns the next action?",
                string.IsNullOrWhiteSpace(pm) ? "No project manager assigned." : pm!,
                string.IsNullOrWhiteSpace(pm) ? CommandCenterAnswerState.Attention : CommandCenterAnswerState.Answered),
        };
        return list;
    }
}
