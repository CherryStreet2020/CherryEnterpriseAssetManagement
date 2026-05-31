// Theme B9 Wave 6 PR-18 (2026-05-31) — ProjectServiceService impl. CLOSES B9.
//
// Service handoff + warranty + the data-driven project review. Tenant-scoped
// through the project. The review composes a closeout-readiness picture from the
// project's own substrate (change control / RAID / quality / billing) and stamps
// the CustomerProject AI-summary fields. Per-project MAX+1 numbers via plain LINQ.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectServiceService : IProjectServiceService
{
    public const string ReviewModel = "project-review-v1";

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectServiceService> _log;

    public ProjectServiceService(AppDbContext db, ITenantContext tenant, ILogger<ProjectServiceService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    private async Task<CustomerProject?> LoadProjectAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return null;
        return await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string?> ValidatePhaseAsync(int projectId, int? phaseId, CancellationToken ct)
        => phaseId.HasValue && !await _db.ProjectPhases.AnyAsync(p => p.Id == phaseId.Value && p.CustomerProjectId == projectId, ct)
            ? $"Phase {phaseId} is not in this project." : null;

    private async Task<T?> LoadScopedAsync<T>(DbSet<T> set, int id, Func<T, int> projectIdOf, CancellationToken ct) where T : class
    {
        if (id <= 0) return null;
        var row = await set.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id, ct);
        if (row is null) return null;
        var pid = projectIdOf(row);
        return await _db.CustomerProjects.AnyAsync(p => p.Id == pid && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0), ct) ? row : null;
    }

    // Quality blockers reused by the closeout view + review.
    private async Task<int> QualityBlockerCountAsync(int projectId, CancellationToken ct)
    {
        var ncr = await _db.ProjectNCRs.CountAsync(n => n.CustomerProjectId == projectId && n.BlocksShipment && n.Status != ProjectNcrStatus.Closed, ct);
        var mrb = await _db.ProjectMRBs.CountAsync(m => m.CustomerProjectId == projectId && m.Status == ProjectMrbStatus.Pending, ct);
        var punch = await _db.ProjectPunchItems.CountAsync(p => p.CustomerProjectId == projectId && p.BlockingAcceptance
            && p.Status != ProjectPunchStatus.Verified && p.Status != ProjectPunchStatus.Waived, ct);
        return ncr + mrb + punch;
    }

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<Result<ProjectServiceView>> GetServiceAsync(int projectId, CancellationToken ct = default)
    {
        var project = await LoadProjectAsync(projectId, ct);
        if (project is null) return Result.Failure<ProjectServiceView>($"Customer project {projectId} not found in your tenant scope.");

        var handoffs = await _db.ProjectServiceHandoffs.Where(h => h.CustomerProjectId == projectId).OrderBy(h => h.HandoffNumber).ToListAsync(ct);
        var warranties = await _db.ProjectWarranties.Where(w => w.CustomerProjectId == projectId).OrderBy(w => w.WarrantyNumber).ToListAsync(ct);

        static bool Signed(ProjectServiceHandoff h) => h.Status is ProjectHandoffStatus.SignedOff or ProjectHandoffStatus.Closed;

        var blockers = new List<string>();
        var unsigned = handoffs.Where(h => !Signed(h)).Select(h => h.HandoffNumber).ToList();
        if (unsigned.Count > 0) blockers.Add($"{unsigned.Count} service handoff(s) not signed off (#{string.Join(", #", unsigned)})");
        var qb = await QualityBlockerCountAsync(projectId, ct);
        if (qb > 0) blockers.Add($"{qb} open quality blocker(s)");

        var view = new ProjectServiceView(
            projectId, blockers.Count == 0, blockers,
            handoffs.Select(h => new HandoffRow(h.Id, h.HandoffNumber, h.Title, h.InstalledAssetId, h.SerialNumber,
                h.InstallLocation, h.InstallDate, h.CommissioningDate, h.StartupChecklistComplete, h.TrainingCompleted,
                h.CustomerSignoff, h.Status, h.AffectedPhaseId, Signed(h))).ToList(),
            warranties.Select(w => new WarrantyRow(w.Id, w.WarrantyNumber, w.Title, w.ProjectServiceHandoffId,
                w.WarrantyType, w.StartDate, w.EndDate, w.Provider, w.Status, w.ClaimCount)).ToList());
        return Result.Success(view);
    }

    // ------------------------------------------------------------------
    // Service handoff
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateServiceHandoffAsync(CreateServiceHandoffRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var project = await LoadProjectAsync(req.CustomerProjectId, ct);
        if (project is null) return Result.Failure<int>($"Customer project {req.CustomerProjectId} not found in your tenant scope.");
        var pe = await ValidatePhaseAsync(req.CustomerProjectId, req.AffectedPhaseId, ct);
        if (pe != null) return Result.Failure<int>(pe);

        var n = (await _db.ProjectServiceHandoffs.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.HandoffNumber, ct) ?? 0) + 1;
        var h = new ProjectServiceHandoff
        {
            CustomerProjectId = req.CustomerProjectId, HandoffNumber = n, Title = req.Title,
            InstalledAssetId = req.InstalledAssetId, SerialNumber = req.SerialNumber, CustomerAssetNumber = req.CustomerAssetNumber,
            InstallLocation = req.InstallLocation, InstallDate = req.InstallDate, CommissioningDate = req.CommissioningDate,
            ServiceContractReference = req.ServiceContractReference, PmTemplateReference = req.PmTemplateReference,
            AsBuiltBomReference = req.AsBuiltBomReference, AsBuiltDrawingReference = req.AsBuiltDrawingReference,
            Status = ProjectHandoffStatus.Draft, AffectedPhaseId = req.AffectedPhaseId, Notes = req.Notes,
            CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectServiceHandoffs.Add(h);
        await _db.SaveChangesAsync(ct);
        return Result.Success(h.Id);
    }

    public async Task<Result<ProjectServiceHandoff>> UpdateHandoffProgressAsync(UpdateHandoffProgressRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectServiceHandoff>("Request is required.");
        var h = await LoadScopedAsync(_db.ProjectServiceHandoffs, req.ProjectServiceHandoffId, x => x.CustomerProjectId, ct);
        if (h is null) return Result.Failure<ProjectServiceHandoff>($"Handoff {req.ProjectServiceHandoffId} not found in your tenant scope.");
        if (h.Status is ProjectHandoffStatus.SignedOff or ProjectHandoffStatus.Closed)
            return Result.Failure<ProjectServiceHandoff>($"Handoff is {h.Status} and can no longer be edited.");

        if (req.StartupChecklistComplete.HasValue) h.StartupChecklistComplete = req.StartupChecklistComplete.Value;
        if (req.TrainingCompleted.HasValue) h.TrainingCompleted = req.TrainingCompleted.Value;
        if (req.InstallDate.HasValue) h.InstallDate = req.InstallDate;
        if (req.CommissioningDate.HasValue) h.CommissioningDate = req.CommissioningDate;
        if (req.InstalledAssetId.HasValue) h.InstalledAssetId = req.InstalledAssetId;
        if (req.SerialNumber != null) h.SerialNumber = req.SerialNumber;
        if (req.AsBuiltBomReference != null) h.AsBuiltBomReference = req.AsBuiltBomReference;
        if (req.AsBuiltDrawingReference != null) h.AsBuiltDrawingReference = req.AsBuiltDrawingReference;
        // Recording commissioning advances Draft → Commissioned.
        if (h.Status == ProjectHandoffStatus.Draft && h.CommissioningDate.HasValue)
            h.Status = ProjectHandoffStatus.Commissioned;
        h.ModifiedAt = DateTime.UtcNow; h.ModifiedBy = req.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(h);
    }

    public async Task<Result<ProjectServiceHandoff>> SignOffHandoffAsync(SignOffHandoffRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectServiceHandoff>("Request is required.");
        var h = await LoadScopedAsync(_db.ProjectServiceHandoffs, req.ProjectServiceHandoffId, x => x.CustomerProjectId, ct);
        if (h is null) return Result.Failure<ProjectServiceHandoff>($"Handoff {req.ProjectServiceHandoffId} not found in your tenant scope.");
        if (h.Status is ProjectHandoffStatus.SignedOff or ProjectHandoffStatus.Closed)
            return Result.Failure<ProjectServiceHandoff>($"Handoff is already {h.Status}.");
        // Gate: cannot sign off until startup checklist + training are complete.
        if (!h.StartupChecklistComplete || !h.TrainingCompleted)
            return Result.Failure<ProjectServiceHandoff>(
                "Cannot sign off the handoff until the startup checklist and training are complete.");

        h.CustomerSignoff = true;
        h.CustomerSignoffBy = req.CustomerSignoffBy;
        h.CustomerSignoffAt = DateTime.UtcNow;
        h.Status = ProjectHandoffStatus.SignedOff;
        h.ModifiedAt = DateTime.UtcNow; h.ModifiedBy = req.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(h);
    }

    public async Task<Result<ProjectServiceHandoff>> TransitionHandoffAsync(int handoffId, ProjectHandoffStatus newStatus, string? modifiedBy = null, CancellationToken ct = default)
    {
        var h = await LoadScopedAsync(_db.ProjectServiceHandoffs, handoffId, x => x.CustomerProjectId, ct);
        if (h is null) return Result.Failure<ProjectServiceHandoff>($"Handoff {handoffId} not found in your tenant scope.");
        if (h.Status == ProjectHandoffStatus.Closed)
            return Result.Failure<ProjectServiceHandoff>("Handoff is Closed (terminal) and cannot transition.");
        if (newStatus == h.Status) return Result.Failure<ProjectServiceHandoff>($"Handoff is already {newStatus}.");
        // Sign-off goes through SignOffHandoffAsync so its gate + stamps apply.
        if (newStatus == ProjectHandoffStatus.SignedOff)
            return Result.Failure<ProjectServiceHandoff>("Use SignOffHandoff to sign off (it enforces the checklist/training gate).");
        // Cannot close a handoff that was never signed off.
        if (newStatus == ProjectHandoffStatus.Closed && h.Status != ProjectHandoffStatus.SignedOff)
            return Result.Failure<ProjectServiceHandoff>("Cannot close a handoff before it is signed off.");
        h.Status = newStatus;
        h.ModifiedAt = DateTime.UtcNow; h.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(h);
    }

    // ------------------------------------------------------------------
    // Warranty
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateWarrantyAsync(CreateWarrantyRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var project = await LoadProjectAsync(req.CustomerProjectId, ct);
        if (project is null) return Result.Failure<int>($"Customer project {req.CustomerProjectId} not found in your tenant scope.");
        if (req.ProjectServiceHandoffId.HasValue && !await _db.ProjectServiceHandoffs.AnyAsync(
                h => h.Id == req.ProjectServiceHandoffId.Value && h.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Handoff {req.ProjectServiceHandoffId} is not in this project.");
        if (req.StartDate.HasValue && req.EndDate.HasValue && req.EndDate.Value < req.StartDate.Value)
            return Result.Failure<int>("Warranty EndDate cannot be before StartDate.");

        var n = (await _db.ProjectWarranties.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.WarrantyNumber, ct) ?? 0) + 1;
        var w = new ProjectWarranty
        {
            CustomerProjectId = req.CustomerProjectId, WarrantyNumber = n, Title = req.Title,
            ProjectServiceHandoffId = req.ProjectServiceHandoffId, WarrantyType = req.WarrantyType,
            StartDate = req.StartDate, EndDate = req.EndDate, Provider = req.Provider, Terms = req.Terms,
            Status = ProjectWarrantyStatus.Pending, Notes = req.Notes, CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectWarranties.Add(w);
        await _db.SaveChangesAsync(ct);
        return Result.Success(w.Id);
    }

    public async Task<Result<ProjectWarranty>> ActivateWarrantyAsync(ActivateWarrantyRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectWarranty>("Request is required.");
        var w = await LoadScopedAsync(_db.ProjectWarranties, req.ProjectWarrantyId, x => x.CustomerProjectId, ct);
        if (w is null) return Result.Failure<ProjectWarranty>($"Warranty {req.ProjectWarrantyId} not found in your tenant scope.");
        if (w.Status == ProjectWarrantyStatus.Void)
            return Result.Failure<ProjectWarranty>("Warranty is Void (terminal) and cannot be activated.");
        var start = req.StartDate ?? w.StartDate ?? DateTime.UtcNow.Date;
        var end = req.EndDate ?? w.EndDate;
        if (end.HasValue && end.Value < start)
            return Result.Failure<ProjectWarranty>("Warranty EndDate cannot be before StartDate.");
        w.StartDate = start; w.EndDate = end; w.Status = ProjectWarrantyStatus.Active;
        w.ModifiedAt = DateTime.UtcNow; w.ModifiedBy = req.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(w);
    }

    public async Task<Result<ProjectWarranty>> TransitionWarrantyAsync(int warrantyId, ProjectWarrantyStatus newStatus, string? modifiedBy = null, CancellationToken ct = default)
    {
        var w = await LoadScopedAsync(_db.ProjectWarranties, warrantyId, x => x.CustomerProjectId, ct);
        if (w is null) return Result.Failure<ProjectWarranty>($"Warranty {warrantyId} not found in your tenant scope.");
        if (w.Status == ProjectWarrantyStatus.Void)
            return Result.Failure<ProjectWarranty>("Warranty is Void (terminal) and cannot transition.");
        if (newStatus == w.Status && newStatus != ProjectWarrantyStatus.Claimed)
            return Result.Failure<ProjectWarranty>($"Warranty is already {newStatus}.");
        if (newStatus == ProjectWarrantyStatus.Claimed) w.ClaimCount += 1;
        w.Status = newStatus;
        w.ModifiedAt = DateTime.UtcNow; w.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(w);
    }

    // ------------------------------------------------------------------
    // AI project review — data-driven synthesis onto the AI-summary fields
    // ------------------------------------------------------------------
    public async Task<Result<ProjectReviewResult>> GenerateProjectReviewAsync(int projectId, string? generatedBy = null, CancellationToken ct = default)
    {
        var project = await LoadProjectAsync(projectId, ct);
        if (project is null) return Result.Failure<ProjectReviewResult>($"Customer project {projectId} not found in your tenant scope.");

        var ci = CultureInfo.InvariantCulture;
        var currency = string.IsNullOrWhiteSpace(project.Currency) ? "USD" : project.Currency!;
        var contract = project.ContractValue ?? 0m;

        // Projected margin from the EVM estimate-at-completion (Contract − ETC est).
        decimal? margin = project.ContractValue.HasValue && project.EstimatedTotalCost.HasValue
            ? project.ContractValue.Value - project.EstimatedTotalCost.Value : (decimal?)null;
        decimal? marginPct = (margin.HasValue && contract > 0m) ? Math.Round(margin.Value / contract * 100m, 1) : (decimal?)null;

        var openCr = await _db.ProjectChangeRequests.CountAsync(c => c.CustomerProjectId == projectId
            && c.Status != ProjectChangeRequestStatus.Rejected && c.Status != ProjectChangeRequestStatus.Cancelled
            && c.Status != ProjectChangeRequestStatus.Converted, ct);
        var openRisks = await _db.ProjectRisks.CountAsync(r => r.CustomerProjectId == projectId
            && r.Status != ProjectRiskStatus.Closed && r.Status != ProjectRiskStatus.Accepted, ct);
        var openIssues = await _db.ProjectIssues.CountAsync(i => i.CustomerProjectId == projectId
            && i.Status != ProjectIssueStatus.Closed, ct);
        var qualityBlockers = await QualityBlockerCountAsync(projectId, ct);
        var acceptanceConfirmed = await _db.ProjectAcceptances.AnyAsync(a => a.CustomerProjectId == projectId
            && a.Status == ProjectAcceptanceStatus.Accepted, ct);

        var scheduled = await _db.ProjectBillingSchedules.Where(s => s.CustomerProjectId == projectId
            && s.Status != ProjectBillingStatus.Cancelled).SumAsync(s => (decimal?)s.ScheduledAmount, ct) ?? 0m;
        var invoiced = await _db.ProjectInvoiceLinks.Where(i => i.CustomerProjectId == projectId
            && i.Status != ProjectInvoiceStatus.Void).SumAsync(i => (decimal?)i.InvoicedAmount, ct) ?? 0m;
        decimal? pctBilled = contract > 0m ? Math.Round(invoiced / contract * 100m, 1) : (decimal?)null;

        var handoffs = await _db.ProjectServiceHandoffs.Where(h => h.CustomerProjectId == projectId).ToListAsync(ct);
        bool anyHandoff = handoffs.Count > 0;
        bool allHandoffsSigned = anyHandoff && handoffs.All(h => h.Status is ProjectHandoffStatus.SignedOff or ProjectHandoffStatus.Closed);
        bool warrantyRecorded = await _db.ProjectWarranties.AnyAsync(w => w.CustomerProjectId == projectId
            && w.Status != ProjectWarrantyStatus.Void, ct);

        // Closeout checklist.
        var checklist = new List<ReviewChecklistItem>
        {
            new("Contract value baselined", project.ContractValue.HasValue, project.ContractValue.HasValue ? null : "No contract value set"),
            new("All change requests resolved", openCr == 0, openCr == 0 ? null : $"{openCr} change request(s) still open"),
            new("No open RAID risks/issues", openRisks == 0 && openIssues == 0, (openRisks == 0 && openIssues == 0) ? null : $"{openRisks} risk(s), {openIssues} issue(s) open"),
            new("No open quality blockers", qualityBlockers == 0, qualityBlockers == 0 ? null : $"{qualityBlockers} blocking NCR/MRB/punch"),
            new("Customer acceptance confirmed", acceptanceConfirmed, acceptanceConfirmed ? null : "No accepted customer acceptance on file"),
            new("Service handoff signed off", !anyHandoff || allHandoffsSigned, anyHandoff && !allHandoffsSigned ? "A handoff is not yet signed off" : (anyHandoff ? null : "No equipment handoff (n/a)")),
            new("Warranty recorded", !anyHandoff || warrantyRecorded, (anyHandoff && !warrantyRecorded) ? "Equipment handed off but no warranty recorded" : null),
            new("Billing complete", scheduled > 0m && invoiced >= scheduled, scheduled > 0m && invoiced < scheduled ? $"{pctBilled?.ToString(ci)}% of contract billed" : null),
        };
        bool closeoutReady = checklist.All(c => c.Done);

        // Compose the narrative.
        var sb = new StringBuilder();
        sb.Append($"Project review — {project.Code} \"{project.Name}\" ({project.Status}). ");
        sb.Append($"Contract {contract.ToString("N0", ci)} {currency}");
        if (project.PercentComplete.HasValue) sb.Append($", {project.PercentComplete.Value.ToString("0.#", ci)}% complete");
        sb.Append(". ");
        if (margin.HasValue)
            sb.Append($"Projected margin {margin.Value.ToString("N0", ci)} {currency}{(marginPct.HasValue ? $" ({marginPct.Value.ToString(ci)}%)" : "")}. ");
        else
            sb.Append("Projected margin not yet estimable (no estimate-at-completion). ");
        sb.Append(openCr > 0 ? $"{openCr} open change request(s). " : "No open change requests. ");
        sb.Append((openRisks + openIssues) > 0 ? $"{openRisks} open risk(s), {openIssues} open issue(s). " : "RAID clear. ");
        sb.Append(qualityBlockers > 0 ? $"{qualityBlockers} open quality blocker(s) hold acceptance/ship. " : "No open quality blockers. ");
        if (pctBilled.HasValue) sb.Append($"{pctBilled.Value.ToString(ci)}% of contract billed. ");
        sb.Append(closeoutReady
            ? "Closeout READY — all checklist items satisfied."
            : $"Closeout NOT ready — {checklist.Count(c => !c.Done)} item(s) outstanding: " +
              string.Join("; ", checklist.Where(c => !c.Done).Select(c => c.Item)) + ".");
        var narrative = sb.ToString();

        // Stamp the existing AI-summary fields (preview-only synthesis).
        var now = DateTime.UtcNow;
        project.AiSummaryText = narrative;
        project.AiSummaryModel = ReviewModel;
        project.AiSummaryGeneratedAt = now;
        project.ModifiedAt = now;
        project.ModifiedBy = generatedBy ?? project.ModifiedBy;
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Generated project review for {Code} (closeoutReady={Ready}).", project.Code, closeoutReady);
        return Result.Success(new ProjectReviewResult(projectId, narrative, margin, marginPct, openCr, openRisks,
            openIssues, qualityBlockers, pctBilled, closeoutReady, checklist, ReviewModel, now));
    }
}
