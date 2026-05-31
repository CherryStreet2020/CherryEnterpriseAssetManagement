// Theme B9 Wave 6 PR-17 (2026-05-31) — ProjectQualityService impl.
//
// Inspections / NCRs / MRBs / punch items / acceptance, tenant-scoped through
// the project. Hosts the §22.4 acceptance gate and the wiring that flips the
// PR-14 billing AcceptanceConfirmed when a RevenueTrigger acceptance confirms.
// Per-project MAX+1 numbers via plain LINQ (unique-index-backstopped).

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

public sealed class ProjectQualityService : IProjectQualityService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectQualityService> _log;

    public ProjectQualityService(AppDbContext db, ITenantContext tenant, ILogger<ProjectQualityService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    private async Task<(bool ok, string? err)> ProjectOkAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, "CustomerProjectId must be > 0.");
        var visible = await _db.CustomerProjects.AnyAsync(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0), ct);
        return visible ? (true, null) : (false, $"Customer project {projectId} not found in your tenant scope.");
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

    // The §22.4 acceptance blockers for a project.
    private async Task<List<string>> AcceptanceBlockersAsync(int projectId, CancellationToken ct)
    {
        var blockers = new List<string>();
        var openBlockingNcr = await _db.ProjectNCRs.CountAsync(
            n => n.CustomerProjectId == projectId && n.BlocksShipment && n.Status != ProjectNcrStatus.Closed, ct);
        if (openBlockingNcr > 0) blockers.Add($"{openBlockingNcr} open blocking NCR(s)");
        var pendingMrb = await _db.ProjectMRBs.CountAsync(
            m => m.CustomerProjectId == projectId && m.Status == ProjectMrbStatus.Pending, ct);
        if (pendingMrb > 0) blockers.Add($"{pendingMrb} pending MRB(s)");
        var openPunch = await _db.ProjectPunchItems.CountAsync(
            p => p.CustomerProjectId == projectId && p.BlockingAcceptance
                 && p.Status != ProjectPunchStatus.Verified && p.Status != ProjectPunchStatus.Waived, ct);
        if (openPunch > 0) blockers.Add($"{openPunch} open blocking-acceptance punch item(s)");
        return blockers;
    }

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<Result<ProjectQualityView>> GetQualityAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err) = await ProjectOkAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectQualityView>(err!);

        var insp = await _db.ProjectInspections.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.InspectionNumber).ToListAsync(ct);
        var ncrs = await _db.ProjectNCRs.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.NcrNumber).ToListAsync(ct);
        var mrbs = await _db.ProjectMRBs.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.MrbNumber).ToListAsync(ct);
        var punch = await _db.ProjectPunchItems.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.PunchNumber).ToListAsync(ct);
        var acc = await _db.ProjectAcceptances.Where(x => x.CustomerProjectId == projectId).OrderBy(x => x.AcceptanceNumber).ToListAsync(ct);

        static bool NcrOpen(ProjectNCR n) => n.Status != ProjectNcrStatus.Closed;
        static bool PunchOpen(ProjectPunchItem p) => p.Status != ProjectPunchStatus.Verified && p.Status != ProjectPunchStatus.Waived;

        var blockers = await AcceptanceBlockersAsync(projectId, ct);
        bool blockingShipPunch = punch.Any(p => p.BlockingShipment && PunchOpen(p));
        bool blockingNcr = ncrs.Any(n => n.BlocksShipment && NcrOpen(n));
        bool pendingMrb = mrbs.Any(m => m.Status == ProjectMrbStatus.Pending);
        bool shipReady = !blockingNcr && !pendingMrb && !blockingShipPunch;

        var view = new ProjectQualityView(
            projectId,
            ncrs.Count(NcrOpen),
            ncrs.Count(n => n.BlocksShipment && NcrOpen(n)),
            mrbs.Count(m => m.Status == ProjectMrbStatus.Pending),
            punch.Count(PunchOpen),
            punch.Count(p => p.BlockingAcceptance && PunchOpen(p)),
            insp.Count(i => i.Result == ProjectInspectionResult.Pending),
            shipReady,
            blockers.Count == 0,
            blockers,
            insp.Select(i => new InspectionRow(i.Id, i.InspectionNumber, i.Title, i.InspectionType, i.Result,
                i.InspectionDate, i.QuantityInspected, i.QuantityAccepted, i.QuantityRejected, i.AffectedPhaseId)).ToList(),
            ncrs.Select(n => new NcrRow(n.Id, n.NcrNumber, n.Title, n.Source, n.Severity, n.Disposition, n.Status,
                n.BlocksShipment, n.QuantityAffected, n.AffectedPhaseId, NcrOpen(n))).ToList(),
            mrbs.Select(m => new MrbRow(m.Id, m.MrbNumber, m.Title, m.LinkedNcrId, m.Disposition, m.Status,
                m.CustomerApprovalRequired, m.CustomerApproved, m.Status == ProjectMrbStatus.Pending)).ToList(),
            punch.Select(p => new PunchRow(p.Id, p.PunchNumber, p.Title, p.Priority, p.Owner, p.DueDate, p.Status,
                p.CustomerVisible, p.BlockingShipment, p.BlockingInvoice, p.BlockingAcceptance, PunchOpen(p))).ToList(),
            acc.Select(a => new AcceptanceRow(a.Id, a.AcceptanceNumber, a.AcceptanceType, a.Status, a.CustomerContact,
                a.AcceptedQuantity, a.RejectedQuantity, a.AcceptanceDate, a.RevenueTrigger, a.WarrantyTrigger)).ToList());
        return Result.Success(view);
    }

    // ------------------------------------------------------------------
    // Inspections
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateInspectionAsync(CreateInspectionRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err) = await ProjectOkAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        var pe = await ValidatePhaseAsync(req.CustomerProjectId, req.AffectedPhaseId, ct);
        if (pe != null) return Result.Failure<int>(pe);

        var n = (await _db.ProjectInspections.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.InspectionNumber, ct) ?? 0) + 1;
        var insp = new ProjectInspection
        {
            CustomerProjectId = req.CustomerProjectId, InspectionNumber = n, Title = req.Title,
            InspectionType = req.InspectionType, Result = ProjectInspectionResult.Pending, InspectionDate = req.InspectionDate,
            Inspector = req.Inspector, AffectedPhaseId = req.AffectedPhaseId, ReportReference = req.ReportReference,
            Notes = req.Notes, CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectInspections.Add(insp);
        await _db.SaveChangesAsync(ct);
        return Result.Success(insp.Id);
    }

    public async Task<Result<ProjectInspection>> CompleteInspectionAsync(CompleteInspectionRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectInspection>("Request is required.");
        if (req.Result == ProjectInspectionResult.Pending)
            return Result.Failure<ProjectInspection>("Completing an inspection needs a result (Pass/Fail/Conditional).");
        var insp = await LoadScopedAsync(_db.ProjectInspections, req.ProjectInspectionId, x => x.CustomerProjectId, ct);
        if (insp is null) return Result.Failure<ProjectInspection>($"Inspection {req.ProjectInspectionId} not found in your tenant scope.");
        if (insp.Result != ProjectInspectionResult.Pending)
            return Result.Failure<ProjectInspection>($"Inspection is already completed ({insp.Result}).");
        if (req.QuantityInspected < 0 || req.QuantityAccepted < 0 || req.QuantityRejected < 0)
            return Result.Failure<ProjectInspection>("Quantities cannot be negative.");

        insp.Result = req.Result;
        insp.QuantityInspected = req.QuantityInspected;
        insp.QuantityAccepted = req.QuantityAccepted;
        insp.QuantityRejected = req.QuantityRejected;
        if (req.Notes != null) insp.Notes = req.Notes;
        insp.CompletedAt = DateTime.UtcNow;
        insp.CompletedBy = req.CompletedBy;
        insp.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(insp);
    }

    // ------------------------------------------------------------------
    // NCRs
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateNcrAsync(CreateNcrRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err) = await ProjectOkAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        var pe = await ValidatePhaseAsync(req.CustomerProjectId, req.AffectedPhaseId, ct);
        if (pe != null) return Result.Failure<int>(pe);
        if (req.LinkedInspectionId.HasValue && !await _db.ProjectInspections.AnyAsync(
                i => i.Id == req.LinkedInspectionId.Value && i.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Inspection {req.LinkedInspectionId} is not in this project.");

        var n = (await _db.ProjectNCRs.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.NcrNumber, ct) ?? 0) + 1;
        var ncr = new ProjectNCR
        {
            CustomerProjectId = req.CustomerProjectId, NcrNumber = n, Title = req.Title, Description = req.Description,
            Source = req.Source, Severity = req.Severity, DetectedDate = req.DetectedDate ?? DateTime.UtcNow,
            QuantityAffected = req.QuantityAffected, BlocksShipment = req.BlocksShipment, ContainmentAction = req.ContainmentAction,
            Disposition = ProjectQualityDisposition.Pending, Status = ProjectNcrStatus.Open,
            AffectedPhaseId = req.AffectedPhaseId, LinkedInspectionId = req.LinkedInspectionId,
            Notes = req.Notes, CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectNCRs.Add(ncr);
        await _db.SaveChangesAsync(ct);
        return Result.Success(ncr.Id);
    }

    public async Task<Result<ProjectNCR>> DispositionNcrAsync(DispositionNcrRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectNCR>("Request is required.");
        if (req.Disposition == ProjectQualityDisposition.Pending)
            return Result.Failure<ProjectNCR>("A disposition decision is required (not Pending).");
        var ncr = await LoadScopedAsync(_db.ProjectNCRs, req.ProjectNcrId, x => x.CustomerProjectId, ct);
        if (ncr is null) return Result.Failure<ProjectNCR>($"NCR {req.ProjectNcrId} not found in your tenant scope.");
        if (ncr.Status == ProjectNcrStatus.Closed)
            return Result.Failure<ProjectNCR>("NCR is Closed (terminal) and cannot be re-dispositioned.");

        ncr.Disposition = req.Disposition;
        if (req.RootCause != null) ncr.RootCause = req.RootCause;
        if (req.CorrectiveAction != null) ncr.CorrectiveAction = req.CorrectiveAction;
        if (ncr.Status == ProjectNcrStatus.Open || ncr.Status == ProjectNcrStatus.Contained)
            ncr.Status = ProjectNcrStatus.Dispositioned;
        ncr.ModifiedAt = DateTime.UtcNow; ncr.ModifiedBy = req.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(ncr);
    }

    public async Task<Result<ProjectNCR>> TransitionNcrAsync(int ncrId, ProjectNcrStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default)
    {
        var ncr = await LoadScopedAsync(_db.ProjectNCRs, ncrId, x => x.CustomerProjectId, ct);
        if (ncr is null) return Result.Failure<ProjectNCR>($"NCR {ncrId} not found in your tenant scope.");
        if (ncr.Status == ProjectNcrStatus.Closed)
            return Result.Failure<ProjectNCR>("NCR is Closed (terminal) and cannot transition.");
        if (newStatus == ncr.Status) return Result.Failure<ProjectNCR>($"NCR is already {newStatus}.");
        // Cannot close an NCR that hasn't been dispositioned.
        if (newStatus == ProjectNcrStatus.Closed && ncr.Disposition == ProjectQualityDisposition.Pending)
            return Result.Failure<ProjectNCR>("Cannot close an NCR before it is dispositioned.");

        if (newStatus == ProjectNcrStatus.Closed)
        {
            ncr.ClosedAt = DateTime.UtcNow;
            ncr.ClosedBy = actor;
        }
        ncr.Status = newStatus;
        ncr.ModifiedAt = DateTime.UtcNow; ncr.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(ncr);
    }

    // ------------------------------------------------------------------
    // MRBs
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateMrbAsync(CreateMrbRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err) = await ProjectOkAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        var pe = await ValidatePhaseAsync(req.CustomerProjectId, req.AffectedPhaseId, ct);
        if (pe != null) return Result.Failure<int>(pe);
        if (req.LinkedNcrId.HasValue && !await _db.ProjectNCRs.AnyAsync(
                x => x.Id == req.LinkedNcrId.Value && x.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"NCR {req.LinkedNcrId} is not in this project.");

        var n = (await _db.ProjectMRBs.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.MrbNumber, ct) ?? 0) + 1;
        var mrb = new ProjectMRB
        {
            CustomerProjectId = req.CustomerProjectId, MrbNumber = n, Title = req.Title, LinkedNcrId = req.LinkedNcrId,
            BoardMembers = req.BoardMembers, ReviewDate = req.ReviewDate, Disposition = ProjectQualityDisposition.Pending,
            Status = ProjectMrbStatus.Pending, CustomerApprovalRequired = req.CustomerApprovalRequired,
            AffectedPhaseId = req.AffectedPhaseId, Notes = req.Notes, CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectMRBs.Add(mrb);
        await _db.SaveChangesAsync(ct);
        return Result.Success(mrb.Id);
    }

    public async Task<Result<ProjectMRB>> DispositionMrbAsync(DispositionMrbRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ProjectMRB>("Request is required.");
        if (req.Disposition == ProjectQualityDisposition.Pending)
            return Result.Failure<ProjectMRB>("A disposition decision is required (not Pending).");
        var mrb = await LoadScopedAsync(_db.ProjectMRBs, req.ProjectMrbId, x => x.CustomerProjectId, ct);
        if (mrb is null) return Result.Failure<ProjectMRB>($"MRB {req.ProjectMrbId} not found in your tenant scope.");
        if (mrb.Status == ProjectMrbStatus.Closed)
            return Result.Failure<ProjectMRB>("MRB is Closed (terminal) and cannot be re-dispositioned.");
        // A board needing customer approval can't be dispositioned until approved.
        if (mrb.CustomerApprovalRequired && !(req.CustomerApproved ?? mrb.CustomerApproved))
            return Result.Failure<ProjectMRB>("This MRB requires customer approval before it can be dispositioned.");

        mrb.Disposition = req.Disposition;
        if (req.Justification != null) mrb.Justification = req.Justification;
        if (req.CustomerApproved.HasValue) mrb.CustomerApproved = req.CustomerApproved.Value;
        mrb.Status = ProjectMrbStatus.Dispositioned;
        mrb.ModifiedAt = DateTime.UtcNow; mrb.ModifiedBy = req.ModifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(mrb);
    }

    public async Task<Result<ProjectMRB>> TransitionMrbAsync(int mrbId, ProjectMrbStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default)
    {
        var mrb = await LoadScopedAsync(_db.ProjectMRBs, mrbId, x => x.CustomerProjectId, ct);
        if (mrb is null) return Result.Failure<ProjectMRB>($"MRB {mrbId} not found in your tenant scope.");
        if (mrb.Status == ProjectMrbStatus.Closed)
            return Result.Failure<ProjectMRB>("MRB is Closed (terminal) and cannot transition.");
        if (newStatus == mrb.Status) return Result.Failure<ProjectMRB>($"MRB is already {newStatus}.");
        if (newStatus == ProjectMrbStatus.Closed && mrb.Disposition == ProjectQualityDisposition.Pending)
            return Result.Failure<ProjectMRB>("Cannot close an MRB before it is dispositioned.");
        if (newStatus == ProjectMrbStatus.Closed) { mrb.ClosedAt = DateTime.UtcNow; mrb.ClosedBy = actor; }
        mrb.Status = newStatus;
        mrb.ModifiedAt = DateTime.UtcNow; mrb.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(mrb);
    }

    // ------------------------------------------------------------------
    // Punch items
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreatePunchItemAsync(CreatePunchItemRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err) = await ProjectOkAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        var pe = await ValidatePhaseAsync(req.CustomerProjectId, req.AffectedPhaseId, ct);
        if (pe != null) return Result.Failure<int>(pe);

        var n = (await _db.ProjectPunchItems.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.PunchNumber, ct) ?? 0) + 1;
        var pi = new ProjectPunchItem
        {
            CustomerProjectId = req.CustomerProjectId, PunchNumber = n, Title = req.Title, Source = req.Source,
            Description = req.Description, Priority = req.Priority, Owner = req.Owner, DueDate = req.DueDate,
            Status = ProjectPunchStatus.Open, CustomerVisible = req.CustomerVisible, BlockingShipment = req.BlockingShipment,
            BlockingInvoice = req.BlockingInvoice, BlockingAcceptance = req.BlockingAcceptance,
            CorrectiveAction = req.CorrectiveAction, AffectedPhaseId = req.AffectedPhaseId, Notes = req.Notes,
            CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectPunchItems.Add(pi);
        await _db.SaveChangesAsync(ct);
        return Result.Success(pi.Id);
    }

    public async Task<Result<ProjectPunchItem>> TransitionPunchItemAsync(int punchId, ProjectPunchStatus newStatus, string? actor = null, string? completionEvidence = null, string? modifiedBy = null, CancellationToken ct = default)
    {
        var pi = await LoadScopedAsync(_db.ProjectPunchItems, punchId, x => x.CustomerProjectId, ct);
        if (pi is null) return Result.Failure<ProjectPunchItem>($"Punch item {punchId} not found in your tenant scope.");
        if (pi.Status is ProjectPunchStatus.Verified or ProjectPunchStatus.Waived)
            return Result.Failure<ProjectPunchItem>($"Punch item is {pi.Status} (terminal) and cannot transition.");
        if (newStatus == pi.Status) return Result.Failure<ProjectPunchItem>($"Punch item is already {newStatus}.");

        if (newStatus is ProjectPunchStatus.Verified or ProjectPunchStatus.Waived)
        {
            pi.ClosedAt = DateTime.UtcNow;
            pi.ClosedBy = actor;
            pi.ClosedDate ??= DateTime.UtcNow.Date;
        }
        if (completionEvidence != null) pi.CompletionEvidence = completionEvidence;
        pi.Status = newStatus;
        pi.ModifiedAt = DateTime.UtcNow; pi.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(pi);
    }

    // ------------------------------------------------------------------
    // Acceptance (the §22.4 gate)
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateAcceptanceAsync(CreateAcceptanceRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<int>("Request is required.");
        var (ok, err) = await ProjectOkAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        var pe = await ValidatePhaseAsync(req.CustomerProjectId, req.AffectedPhaseId, ct);
        if (pe != null) return Result.Failure<int>(pe);

        var n = (await _db.ProjectAcceptances.Where(x => x.CustomerProjectId == req.CustomerProjectId)
            .MaxAsync(x => (int?)x.AcceptanceNumber, ct) ?? 0) + 1;
        var a = new ProjectAcceptance
        {
            CustomerProjectId = req.CustomerProjectId, AcceptanceNumber = n, AcceptanceType = req.AcceptanceType,
            Status = ProjectAcceptanceStatus.Pending, CustomerContact = req.CustomerContact, RequiredCriteria = req.RequiredCriteria,
            RequiredDocuments = req.RequiredDocuments, RevenueTrigger = req.RevenueTrigger, WarrantyTrigger = req.WarrantyTrigger,
            AffectedPhaseId = req.AffectedPhaseId, Notes = req.Notes, CreatedBy = req.CreatedBy, CreatedAt = DateTime.UtcNow,
        };
        _db.ProjectAcceptances.Add(a);
        await _db.SaveChangesAsync(ct);
        return Result.Success(a.Id);
    }

    public async Task<Result<ConfirmAcceptanceResult>> ConfirmAcceptanceAsync(ConfirmAcceptanceRequest req, CancellationToken ct = default)
    {
        if (req is null) return Result.Failure<ConfirmAcceptanceResult>("Request is required.");
        var a = await LoadScopedAsync(_db.ProjectAcceptances, req.ProjectAcceptanceId, x => x.CustomerProjectId, ct);
        if (a is null) return Result.Failure<ConfirmAcceptanceResult>($"Acceptance {req.ProjectAcceptanceId} not found in your tenant scope.");
        if (a.Status is ProjectAcceptanceStatus.Accepted or ProjectAcceptanceStatus.Rejected)
            return Result.Failure<ConfirmAcceptanceResult>($"Acceptance is {a.Status} (terminal) and cannot be re-confirmed.");

        // THE §22.4 GATE.
        var blockers = await AcceptanceBlockersAsync(a.CustomerProjectId, ct);
        if (blockers.Count > 0)
            return Result.Failure<ConfirmAcceptanceResult>(
                "Cannot confirm acceptance — open quality blockers: " + string.Join("; ", blockers) + ".");

        if (req.AcceptedQuantity < 0 || req.RejectedQuantity < 0)
            return Result.Failure<ConfirmAcceptanceResult>("Quantities cannot be negative.");

        a.Status = ProjectAcceptanceStatus.Accepted;
        a.AcceptedAt = DateTime.UtcNow;
        a.AcceptanceDate = req.AcceptanceDate ?? DateTime.UtcNow.Date;
        a.AcceptedBy = req.AcceptedBy;
        a.Signature = req.Signature;
        if (req.InspectionResult != null) a.InspectionResult = req.InspectionResult;
        a.AcceptedQuantity = req.AcceptedQuantity;
        a.RejectedQuantity = req.RejectedQuantity;
        a.ModifiedAt = DateTime.UtcNow; a.ModifiedBy = req.ModifiedBy;

        // Wire the real Acceptance to the PR-14 billing gate: a RevenueTrigger
        // acceptance confirms the project's acceptance-gated billing lines that
        // aren't already confirmed (the #468 set-once placeholder, now driven by
        // a real entity). Same DbContext ⇒ one unit of work with the acceptance.
        int billingConfirmed = 0;
        if (a.RevenueTrigger)
        {
            var lines = await _db.ProjectBillingSchedules
                .Where(s => s.CustomerProjectId == a.CustomerProjectId && s.RequiresAcceptance && !s.AcceptanceConfirmed)
                .ToListAsync(ct);
            foreach (var s in lines)
            {
                s.AcceptanceConfirmed = true;
                s.AcceptanceConfirmedAt = DateTime.UtcNow;
                s.AcceptanceConfirmedBy = req.AcceptedBy ?? "ProjectAcceptance";
                billingConfirmed++;
            }
        }
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Acceptance #{Num} (project {Pid}) confirmed; revenueTrigger={Rt}, billing lines confirmed={N}.",
            a.AcceptanceNumber, a.CustomerProjectId, a.RevenueTrigger, billingConfirmed);
        return Result.Success(new ConfirmAcceptanceResult(a.Id, a.Status, a.RevenueTrigger, billingConfirmed));
    }

    public async Task<Result<ProjectAcceptance>> RejectAcceptanceAsync(int acceptanceId, string? actor = null, string? modifiedBy = null, CancellationToken ct = default)
    {
        var a = await LoadScopedAsync(_db.ProjectAcceptances, acceptanceId, x => x.CustomerProjectId, ct);
        if (a is null) return Result.Failure<ProjectAcceptance>($"Acceptance {acceptanceId} not found in your tenant scope.");
        if (a.Status is ProjectAcceptanceStatus.Accepted or ProjectAcceptanceStatus.Rejected)
            return Result.Failure<ProjectAcceptance>($"Acceptance is {a.Status} (terminal) and cannot be rejected.");
        a.Status = ProjectAcceptanceStatus.Rejected;
        a.AcceptedBy = actor;
        a.AcceptanceDate = DateTime.UtcNow.Date;
        a.ModifiedAt = DateTime.UtcNow; a.ModifiedBy = modifiedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(a);
    }
}
