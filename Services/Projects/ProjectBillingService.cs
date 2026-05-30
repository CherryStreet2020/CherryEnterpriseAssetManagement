// Theme B9 Wave 5 PR-14 (2026-05-30) — ProjectBillingService impl. CLOSES Wave 5.
//
// Tenant-scoped through the parent CustomerProject. Billing schedule → invoices
// → revenue recognition. Hosts the milestone-achieved + acceptance gates on
// invoicing. Every incoming FK on a write is scoped to the project.

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

public sealed class ProjectBillingService : IProjectBillingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectBillingService> _log;

    public ProjectBillingService(AppDbContext db, ITenantContext tenant, ILogger<ProjectBillingService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    private async Task<(bool ok, string? err, decimal contract, string currency)> ProjectInfoAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, "CustomerProjectId must be > 0.", 0m, "USD");
        var row = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.ContractValue, p.Currency })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? (false, $"Customer project {projectId} not found in your tenant scope.", 0m, "USD")
            : (true, null, row.ContractValue ?? 0m, string.IsNullOrWhiteSpace(row.Currency) ? "USD" : row.Currency!);
    }

    private static bool IsClosedInvoiceState(ProjectBillingStatus s)
        => s == ProjectBillingStatus.Invoiced || s == ProjectBillingStatus.Paid || s == ProjectBillingStatus.Cancelled;

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<Result<ProjectBillingView>> GetBillingAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err, contract, currency) = await ProjectInfoAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectBillingView>(err!);

        var schedules = await _db.ProjectBillingSchedules
            .Where(s => s.CustomerProjectId == projectId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct);
        var invoices = await _db.ProjectInvoiceLinks
            .Where(i => i.CustomerProjectId == projectId)
            .OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id).ToListAsync(ct);
        var recognitions = await _db.ProjectRevenueRecognitions
            .Where(r => r.CustomerProjectId == projectId)
            .OrderBy(r => r.RecognitionDate).ThenBy(r => r.Id).ToListAsync(ct);

        // Which linked milestones are Achieved.
        var msIds = schedules.Where(s => s.ProjectMilestoneId.HasValue).Select(s => s.ProjectMilestoneId!.Value).Distinct().ToList();
        var achievedMsIds = msIds.Count == 0 ? new HashSet<int>() :
            (await _db.ProjectMilestones
                .Where(m => msIds.Contains(m.Id) && m.Status == ProjectMilestoneStatus.Achieved)
                .Select(m => m.Id).ToListAsync(ct)).ToHashSet();

        var invoicedBySchedule = invoices
            .Where(i => i.ProjectBillingScheduleId.HasValue && i.Status != ProjectInvoiceStatus.Void)
            .GroupBy(i => i.ProjectBillingScheduleId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.InvoicedAmount));

        var scheduleRows = schedules.Select(s =>
        {
            bool msAchieved = s.ProjectMilestoneId.HasValue && achievedMsIds.Contains(s.ProjectMilestoneId.Value);
            bool milestoneOk = s.BillingType != ProjectBillingType.Milestone || msAchieved;
            bool acceptanceOk = !s.RequiresAcceptance || s.AcceptanceConfirmed;
            bool ready = milestoneOk && acceptanceOk && !IsClosedInvoiceState(s.Status);
            return new BillingScheduleRow(
                s.Id, s.Code, s.Name, s.BillingType, s.Status, s.ScheduledAmount, s.ScheduledDate,
                s.ProjectMilestoneId, msAchieved, s.RequiresAcceptance, s.AcceptanceConfirmed,
                invoicedBySchedule.TryGetValue(s.Id, out var inv) ? inv : 0m, ready);
        }).ToList();

        var invoiceRows = invoices.Select(i => new InvoiceRow(
            i.Id, i.InvoiceNumber, i.InvoiceDate, i.InvoicedAmount, i.Status, i.ProjectBillingScheduleId)).ToList();
        var revenueRows = recognitions.Select(r => new RevenueRow(
            r.Id, r.Method, r.RecognizedAmount, r.RecognitionDate, r.ProjectBillingScheduleId)).ToList();

        var scheduledTotal = schedules.Where(s => s.Status != ProjectBillingStatus.Cancelled).Sum(s => s.ScheduledAmount);
        var invoicedTotal = invoices.Where(i => i.Status != ProjectInvoiceStatus.Void).Sum(i => i.InvoicedAmount);
        var recognizedTotal = recognitions.Sum(r => r.RecognizedAmount);

        return Result.Success(new ProjectBillingView(
            projectId, currency, contract, scheduledTotal, invoicedTotal, recognizedTotal,
            Math.Max(0m, scheduledTotal - invoicedTotal),
            contract > 0m ? Math.Round(invoicedTotal / contract * 100m, 2) : (decimal?)null,
            scheduleRows, invoiceRows, revenueRows));
    }

    // ------------------------------------------------------------------
    // Create billing schedule
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateBillingScheduleAsync(CreateBillingScheduleRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code)) return Result.Failure<int>("A billing Code is required.");
        if (string.IsNullOrWhiteSpace(req.Name)) return Result.Failure<int>("A billing Name is required.");
        if (req.ScheduledAmount < 0) return Result.Failure<int>("ScheduledAmount cannot be negative.");
        var (ok, err, _, projCurrency) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        if (req.ProjectMilestoneId.HasValue && !await _db.ProjectMilestones.AnyAsync(
                m => m.Id == req.ProjectMilestoneId.Value && m.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Milestone {req.ProjectMilestoneId} is not in this project.");

        var code = req.Code.Trim();
        if (await _db.ProjectBillingSchedules.AnyAsync(s => s.CustomerProjectId == req.CustomerProjectId && s.Code == code, ct))
            return Result.Failure<int>($"Billing Code '{code}' already exists in this project.");

        var sched = new ProjectBillingSchedule
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectMilestoneId = req.ProjectMilestoneId,
            Code = code,
            Name = req.Name.Trim(),
            Description = req.Description,
            BillingType = req.BillingType,
            ScheduledAmount = req.ScheduledAmount,
            // Default to the PROJECT currency (Codex P2 — not a hard-coded USD),
            // so a non-USD project's lines/invoices/totals stay one currency.
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? projCurrency : req.Currency.Trim().ToUpperInvariant(),
            ScheduledDate = req.ScheduledDate,
            PercentOfContract = req.PercentOfContract,
            RequiresAcceptance = req.RequiresAcceptance,
            SortOrder = req.SortOrder,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectBillingSchedules.Add(sched);
        await _db.SaveChangesAsync(ct);
        return Result.Success(sched.Id);
    }

    // ------------------------------------------------------------------
    // Confirm acceptance — set-once.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectBillingSchedule>> ConfirmAcceptanceAsync(int billingScheduleId, string? confirmedBy = null, CancellationToken ct = default)
    {
        if (billingScheduleId <= 0) return Result.Failure<ProjectBillingSchedule>("A valid billing schedule id is required.");
        var sched = await _db.ProjectBillingSchedules.FirstOrDefaultAsync(s => s.Id == billingScheduleId, ct);
        if (sched is null) return Result.Failure<ProjectBillingSchedule>($"Billing schedule {billingScheduleId} not found.");
        var (ok, err, _, _) = await ProjectInfoAsync(sched.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectBillingSchedule>(err!);
        if (sched.AcceptanceConfirmed) return Result.Failure<ProjectBillingSchedule>($"Acceptance is already confirmed for '{sched.Code}'.");

        sched.AcceptanceConfirmed = true;
        sched.AcceptanceConfirmedAt = DateTime.UtcNow;
        sched.AcceptanceConfirmedBy = confirmedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(sched);
    }

    // ------------------------------------------------------------------
    // Record invoice — the §20 gates.
    // ------------------------------------------------------------------
    public async Task<Result<int>> RecordInvoiceAsync(RecordInvoiceRequest req, CancellationToken ct = default)
    {
        if (req is null || req.ProjectBillingScheduleId <= 0) return Result.Failure<int>("A valid ProjectBillingScheduleId is required.");
        if (string.IsNullOrWhiteSpace(req.InvoiceNumber)) return Result.Failure<int>("An InvoiceNumber is required.");
        if (req.InvoicedAmount < 0) return Result.Failure<int>("InvoicedAmount cannot be negative.");

        var sched = await _db.ProjectBillingSchedules.FirstOrDefaultAsync(s => s.Id == req.ProjectBillingScheduleId, ct);
        if (sched is null) return Result.Failure<int>($"Billing schedule {req.ProjectBillingScheduleId} not found.");
        var (ok, err, _, _) = await ProjectInfoAsync(sched.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        // Block re-invoicing a line that is already Invoiced/Paid/Cancelled —
        // otherwise a retry/resubmit would insert a duplicate invoice and inflate
        // billed totals (Codex P2). One invoice per scheduled event in v1.
        if (IsClosedInvoiceState(sched.Status))
            return Result.Failure<int>($"Billing line '{sched.Code}' is {sched.Status} — cannot record another invoice against it.");

        // Gate 1: a Milestone-type line cannot be invoiced until its milestone is Achieved.
        if (sched.BillingType == ProjectBillingType.Milestone)
        {
            bool achieved = sched.ProjectMilestoneId.HasValue && await _db.ProjectMilestones.AnyAsync(
                m => m.Id == sched.ProjectMilestoneId.Value
                    && m.CustomerProjectId == sched.CustomerProjectId
                    && m.Status == ProjectMilestoneStatus.Achieved, ct);
            if (!achieved)
                return Result.Failure<int>(
                    $"Cannot invoice '{sched.Code}' — its billing milestone has not been achieved yet.");
        }

        // Gate 2: a line requiring acceptance cannot be final-billed until confirmed.
        if (sched.RequiresAcceptance && !sched.AcceptanceConfirmed)
            return Result.Failure<int>(
                $"Cannot invoice '{sched.Code}' — it requires customer acceptance, which has not been confirmed.");

        var invoice = new ProjectInvoiceLink
        {
            CustomerProjectId = sched.CustomerProjectId,
            ProjectBillingScheduleId = sched.Id,
            ExternalInvoiceId = req.ExternalInvoiceId,
            InvoiceNumber = req.InvoiceNumber.Trim(),
            InvoiceDate = req.InvoiceDate == default ? DateTime.UtcNow.Date : req.InvoiceDate,
            InvoicedAmount = req.InvoicedAmount,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? sched.Currency : req.Currency.Trim().ToUpperInvariant(),
            Status = ProjectInvoiceStatus.Issued,
            Notes = req.Notes,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectInvoiceLinks.Add(invoice);
        sched.Status = ProjectBillingStatus.Invoiced;
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Invoice {Num} recorded against billing line {Code} on project {ProjectId}.",
            invoice.InvoiceNumber, sched.Code, sched.CustomerProjectId);
        return Result.Success(invoice.Id);
    }

    // ------------------------------------------------------------------
    // Recognize revenue
    // ------------------------------------------------------------------
    public async Task<Result<int>> RecognizeRevenueAsync(RecognizeRevenueRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        if (req.RecognizedAmount < 0) return Result.Failure<int>("RecognizedAmount cannot be negative.");
        if (req.PercentComplete is < 0 or > 100) return Result.Failure<int>("PercentComplete must be 0..100.");
        var (ok, err, _, projCurrency) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        if (req.ProjectBillingScheduleId.HasValue && !await _db.ProjectBillingSchedules.AnyAsync(
                s => s.Id == req.ProjectBillingScheduleId.Value && s.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Billing schedule {req.ProjectBillingScheduleId} is not in this project.");

        var rev = new ProjectRevenueRecognition
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectBillingScheduleId = req.ProjectBillingScheduleId,
            PeriodLabel = string.IsNullOrWhiteSpace(req.PeriodLabel) ? null : req.PeriodLabel.Trim(),
            Method = req.Method,
            RecognizedAmount = req.RecognizedAmount,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? projCurrency : req.Currency.Trim().ToUpperInvariant(),
            RecognitionDate = req.RecognitionDate == default ? DateTime.UtcNow.Date : req.RecognitionDate,
            PercentComplete = req.PercentComplete,
            Notes = req.Notes,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectRevenueRecognitions.Add(rev);
        await _db.SaveChangesAsync(ct);
        return Result.Success(rev.Id);
    }
}
