// Theme B9 Wave 4 PR-10 (2026-05-30) — ProjectProcurementService impl. OPENS Wave 4.
//
// Tenant-scoped through the parent CustomerProject. Hosts the procurement spine
// (plan → commitment → receipt) and the project-close gate (cannot close while
// commitments are open unless waived). Every incoming FK on a write is scoped to
// the project's company (session-30 lesson).

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

public sealed class ProjectProcurementService : IProjectProcurementService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectProcurementService> _log;

    public ProjectProcurementService(AppDbContext db, ITenantContext tenant, ILogger<ProjectProcurementService> log)
    {
        _db = db; _tenant = tenant; _log = log;
    }

    // A commitment is "open" (blocks project close) while value is committed but
    // not yet fully received / formally closed.
    private static bool IsOpen(ProjectCommitmentStatus s)
        => s == ProjectCommitmentStatus.Open || s == ProjectCommitmentStatus.PartiallyReceived;

    // Resolve + tenant-check a project, returning its CompanyId for FK scoping.
    private async Task<(bool ok, string? err, int? companyId)> ProjectInfoAsync(int projectId, CancellationToken ct)
    {
        if (projectId <= 0) return (false, "CustomerProjectId must be > 0.", null);
        var row = await _db.CustomerProjects
            .Where(p => p.Id == projectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.CompanyId })
            .FirstOrDefaultAsync(ct);
        return row is null
            ? (false, $"Customer project {projectId} not found in your tenant scope.", null)
            : (true, null, row.CompanyId);
    }

    private Task<bool> PhaseInProjectAsync(int phaseId, int projectId, CancellationToken ct)
        => _db.ProjectPhases.AnyAsync(p => p.Id == phaseId && p.CustomerProjectId == projectId, ct);

    // ------------------------------------------------------------------
    // Read
    // ------------------------------------------------------------------
    public async Task<Result<ProjectProcurementView>> GetProcurementAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err, _) = await ProjectInfoAsync(projectId, ct);
        if (!ok) return Result.Failure<ProjectProcurementView>(err!);

        var plans = await _db.ProjectProcurementPlans
            .Where(p => p.CustomerProjectId == projectId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync(ct);

        var commitments = await _db.ProjectCommitments
            .Where(c => c.CustomerProjectId == projectId)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
        var commitmentIds = commitments.Select(c => c.Id).ToHashSet();

        var receipts = await _db.ProjectReceipts
            .Where(r => r.CustomerProjectId == projectId)
            .OrderBy(r => r.ReceiptDate).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var receivedByCommitment = receipts
            .Where(r => commitmentIds.Contains(r.ProjectCommitmentId))
            .GroupBy(r => r.ProjectCommitmentId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ReceivedAmount));

        var committedByPlan = commitments
            .Where(c => c.ProjectProcurementPlanId.HasValue && c.Status != ProjectCommitmentStatus.Cancelled)
            .GroupBy(c => c.ProjectProcurementPlanId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.CommittedAmount));

        var planRows = plans.Select(p => new ProcurementPlanRow(
            p.Id, p.Code, p.Name, p.Category, p.Status, p.PlannedAmount, p.PlannedQuantity,
            p.UnitOfMeasure, p.NeedByDate, p.IsLongLead, p.ProjectPhaseId, p.ItemId,
            committedByPlan.TryGetValue(p.Id, out var ca) ? ca : 0m)).ToList();

        var commitmentRows = commitments.Select(c =>
        {
            var rec = receivedByCommitment.TryGetValue(c.Id, out var r) ? r : 0m;
            var open = Math.Max(0m, c.CommittedAmount - rec);
            return new CommitmentRow(
                c.Id, c.Code, c.Description, c.CommitmentType, c.Status, c.CommittedAmount,
                rec, open, IsOpen(c.Status), c.VendorId, c.PurchaseOrderId,
                c.ProjectProcurementPlanId, c.ExpectedReceiptDate);
        }).ToList();

        var receiptRows = receipts.Select(r => new ReceiptRow(
            r.Id, r.ProjectCommitmentId, r.ReceiptNumber, r.ReceivedAmount, r.ReceivedQuantity,
            r.ReceiptDate, r.GoodsReceiptId)).ToList();

        var plannedTotal = plans.Where(p => p.Status != ProjectProcurementPlanStatus.Cancelled).Sum(p => p.PlannedAmount ?? 0m);
        var committedTotal = commitments.Where(c => c.Status != ProjectCommitmentStatus.Cancelled).Sum(c => c.CommittedAmount);
        var receivedTotal = receiptRows.Sum(r => r.ReceivedAmount);
        var openRows = commitmentRows.Where(c => c.IsOpen).ToList();

        return Result.Success(new ProjectProcurementView(
            projectId, plannedTotal, committedTotal, receivedTotal,
            openRows.Sum(c => c.OpenBalance), openRows.Count,
            planRows, commitmentRows, receiptRows));
    }

    // ------------------------------------------------------------------
    // Create plan
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreatePlanAsync(CreateProcurementPlanRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code))
            return Result.Failure<int>("A plan Code is required.");
        if (string.IsNullOrWhiteSpace(req.Name))
            return Result.Failure<int>("A plan Name is required.");
        var (ok, err, companyId) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (req.PlannedAmount is < 0) return Result.Failure<int>("PlannedAmount cannot be negative.");
        if (req.PlannedQuantity is < 0) return Result.Failure<int>("PlannedQuantity cannot be negative.");

        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (req.ItemId.HasValue && !await _db.Items.AnyAsync(i => i.Id == req.ItemId.Value && i.CompanyId == companyId, ct))
            return Result.Failure<int>($"Item {req.ItemId} does not belong to this project's company.");

        var code = req.Code.Trim();
        if (await _db.ProjectProcurementPlans.AnyAsync(p => p.CustomerProjectId == req.CustomerProjectId && p.Code == code, ct))
            return Result.Failure<int>($"Plan Code '{code}' already exists in this project.");

        var plan = new ProjectProcurementPlan
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectPhaseId = req.ProjectPhaseId,
            ItemId = req.ItemId,
            Code = code,
            Name = req.Name.Trim(),
            Description = req.Description,
            Category = req.Category,
            PlannedQuantity = req.PlannedQuantity,
            UnitOfMeasure = string.IsNullOrWhiteSpace(req.UnitOfMeasure) ? null : req.UnitOfMeasure.Trim(),
            PlannedAmount = req.PlannedAmount,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            NeedByDate = req.NeedByDate,
            IsLongLead = req.IsLongLead,
            SortOrder = req.SortOrder,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectProcurementPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return Result.Success(plan.Id);
    }

    // ------------------------------------------------------------------
    // Create commitment — tenant-scope EVERY incoming FK to the project /
    // its company (plan, phase, PO, vendor).
    // ------------------------------------------------------------------
    public async Task<Result<int>> CreateCommitmentAsync(CreateCommitmentRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Code))
            return Result.Failure<int>("A commitment Code is required.");
        var (ok, err, companyId) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);
        if (req.CommittedAmount < 0) return Result.Failure<int>("CommittedAmount cannot be negative.");
        if (req.CommittedQuantity is < 0) return Result.Failure<int>("CommittedQuantity cannot be negative.");

        if (req.ProjectProcurementPlanId.HasValue && !await _db.ProjectProcurementPlans.AnyAsync(
                p => p.Id == req.ProjectProcurementPlanId.Value && p.CustomerProjectId == req.CustomerProjectId, ct))
            return Result.Failure<int>($"Plan {req.ProjectProcurementPlanId} is not in this project.");
        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");
        if (req.PurchaseOrderId.HasValue && !await _db.PurchaseOrders.AnyAsync(
                po => po.Id == req.PurchaseOrderId.Value && _tenant.VisibleCompanyIds.Contains(po.CompanyId ?? 0), ct))
            return Result.Failure<int>($"Purchase order {req.PurchaseOrderId} is not in your tenant scope.");
        if (req.VendorId.HasValue && !await _db.Vendors.AnyAsync(
                v => v.Id == req.VendorId.Value && v.CompanyId == companyId, ct))
            return Result.Failure<int>($"Vendor {req.VendorId} does not belong to this project's company.");

        var code = req.Code.Trim();
        if (await _db.ProjectCommitments.AnyAsync(c => c.CustomerProjectId == req.CustomerProjectId && c.Code == code, ct))
            return Result.Failure<int>($"Commitment Code '{code}' already exists in this project.");

        var commitment = new ProjectCommitment
        {
            CustomerProjectId = req.CustomerProjectId,
            ProjectProcurementPlanId = req.ProjectProcurementPlanId,
            ProjectPhaseId = req.ProjectPhaseId,
            PurchaseOrderId = req.PurchaseOrderId,
            VendorId = req.VendorId,
            CommitmentType = req.CommitmentType,
            Code = code,
            Description = req.Description,
            CommittedAmount = req.CommittedAmount,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
            CommittedQuantity = req.CommittedQuantity,
            UnitOfMeasure = string.IsNullOrWhiteSpace(req.UnitOfMeasure) ? null : req.UnitOfMeasure.Trim(),
            Status = ProjectCommitmentStatus.Open,
            CommittedDate = req.CommittedDate ?? DateTime.UtcNow,
            ExpectedReceiptDate = req.ExpectedReceiptDate,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectCommitments.Add(commitment);
        await _db.SaveChangesAsync(ct);
        return Result.Success(commitment.Id);
    }

    // ------------------------------------------------------------------
    // Record receipt — draws the commitment's open balance down and
    // auto-advances its status (Open → PartiallyReceived → Received).
    // ------------------------------------------------------------------
    public async Task<Result<ProjectCommitment>> RecordReceiptAsync(RecordReceiptRequest req, CancellationToken ct = default)
    {
        if (req is null || req.ProjectCommitmentId <= 0)
            return Result.Failure<ProjectCommitment>("A valid ProjectCommitmentId is required.");
        if (req.ReceivedAmount < 0) return Result.Failure<ProjectCommitment>("ReceivedAmount cannot be negative.");
        if (req.ReceivedQuantity is < 0) return Result.Failure<ProjectCommitment>("ReceivedQuantity cannot be negative.");

        var commitment = await _db.ProjectCommitments.FirstOrDefaultAsync(c => c.Id == req.ProjectCommitmentId, ct);
        if (commitment is null) return Result.Failure<ProjectCommitment>($"Commitment {req.ProjectCommitmentId} not found.");
        var (ok, err, _) = await ProjectInfoAsync(commitment.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectCommitment>(err!);

        if (commitment.Status == ProjectCommitmentStatus.Cancelled)
            return Result.Failure<ProjectCommitment>($"Commitment '{commitment.Code}' is cancelled — cannot receive against it.");
        if (commitment.Status == ProjectCommitmentStatus.Closed)
            return Result.Failure<ProjectCommitment>($"Commitment '{commitment.Code}' is closed — cannot receive against it.");

        var receipt = new ProjectReceipt
        {
            ProjectCommitmentId = commitment.Id,
            CustomerProjectId = commitment.CustomerProjectId,
            GoodsReceiptId = req.GoodsReceiptId,
            ReceiptNumber = string.IsNullOrWhiteSpace(req.ReceiptNumber) ? null : req.ReceiptNumber.Trim(),
            ReceivedAmount = req.ReceivedAmount,
            ReceivedQuantity = req.ReceivedQuantity,
            ReceiptDate = req.ReceiptDate ?? DateTime.UtcNow,
            Notes = req.Notes,
            CreatedBy = req.CreatedBy,
        };
        _db.ProjectReceipts.Add(receipt);

        var priorReceived = await _db.ProjectReceipts
            .Where(r => r.ProjectCommitmentId == commitment.Id)
            .SumAsync(r => (decimal?)r.ReceivedAmount, ct) ?? 0m;
        var totalReceived = priorReceived + req.ReceivedAmount;

        commitment.Status = totalReceived >= commitment.CommittedAmount
            ? ProjectCommitmentStatus.Received
            : totalReceived > 0m
                ? ProjectCommitmentStatus.PartiallyReceived
                : ProjectCommitmentStatus.Open;

        await _db.SaveChangesAsync(ct);
        return Result.Success(commitment);
    }

    // ------------------------------------------------------------------
    // Close commitment — set-once.
    // ------------------------------------------------------------------
    public async Task<Result<ProjectCommitment>> CloseCommitmentAsync(int commitmentId, string? closedBy = null, CancellationToken ct = default)
    {
        if (commitmentId <= 0) return Result.Failure<ProjectCommitment>("A valid commitment id is required.");
        var commitment = await _db.ProjectCommitments.FirstOrDefaultAsync(c => c.Id == commitmentId, ct);
        if (commitment is null) return Result.Failure<ProjectCommitment>($"Commitment {commitmentId} not found.");
        var (ok, err, _) = await ProjectInfoAsync(commitment.CustomerProjectId, ct);
        if (!ok) return Result.Failure<ProjectCommitment>(err!);

        if (commitment.Status == ProjectCommitmentStatus.Cancelled)
            return Result.Failure<ProjectCommitment>($"Commitment '{commitment.Code}' is cancelled.");
        if (commitment.Status == ProjectCommitmentStatus.Closed)
            return Result.Failure<ProjectCommitment>($"Commitment '{commitment.Code}' is already closed.");

        commitment.Status = ProjectCommitmentStatus.Closed;
        commitment.ClosedAt = DateTime.UtcNow;
        commitment.ClosedBy = closedBy;
        await _db.SaveChangesAsync(ct);
        return Result.Success(commitment);
    }

    // ------------------------------------------------------------------
    // Link an existing PurchaseOrder to a project (+ optional phase).
    // ------------------------------------------------------------------
    public async Task<Result<int>> LinkPurchaseOrderToProjectAsync(LinkPurchaseOrderRequest req, CancellationToken ct = default)
    {
        if (req is null || req.PurchaseOrderId <= 0) return Result.Failure<int>("A valid PurchaseOrderId is required.");
        var (ok, err, _) = await ProjectInfoAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        var po = await _db.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == req.PurchaseOrderId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0), ct);
        if (po is null) return Result.Failure<int>($"Purchase order {req.PurchaseOrderId} is not in your tenant scope.");

        if (req.ProjectPhaseId.HasValue && !await PhaseInProjectAsync(req.ProjectPhaseId.Value, req.CustomerProjectId, ct))
            return Result.Failure<int>($"Phase {req.ProjectPhaseId} is not in this project.");

        po.CustomerProjectId = req.CustomerProjectId;
        po.ProjectPhaseId = req.ProjectPhaseId;
        po.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(po.Id);
    }

    // ------------------------------------------------------------------
    // Open-commitment read.
    // ------------------------------------------------------------------
    public async Task<Result<IReadOnlyList<string>>> GetOpenCommitmentsAsync(int projectId, CancellationToken ct = default)
    {
        var (ok, err, _) = await ProjectInfoAsync(projectId, ct);
        if (!ok) return Result.Failure<IReadOnlyList<string>>(err!);

        var open = await _db.ProjectCommitments
            .Where(c => c.CustomerProjectId == projectId
                     && (c.Status == ProjectCommitmentStatus.Open || c.Status == ProjectCommitmentStatus.PartiallyReceived))
            .OrderBy(c => c.Id)
            .Select(c => c.Code)
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<string>>(open);
    }

    // ------------------------------------------------------------------
    // Close project — the §20 gate. Blocked while commitments are open
    // unless waived. Transitions Active/OnHold → Closed.
    // ------------------------------------------------------------------
    public async Task<Result<CustomerProject>> CloseProjectAsync(CloseProjectRequest req, CancellationToken ct = default)
    {
        if (req is null || req.CustomerProjectId <= 0)
            return Result.Failure<CustomerProject>("A valid CustomerProjectId is required.");

        var project = await _db.CustomerProjects
            .FirstOrDefaultAsync(p => p.Id == req.CustomerProjectId && _tenant.VisibleCompanyIds.Contains(p.CompanyId ?? 0), ct);
        if (project is null)
            return Result.Failure<CustomerProject>($"Customer project {req.CustomerProjectId} not found in your tenant scope.");

        if (project.Status != CustomerProjectStatus.Active && project.Status != CustomerProjectStatus.OnHold)
            return Result.Failure<CustomerProject>(
                $"Project '{project.Code}' must be Active or OnHold to close (currently {project.Status}).");

        var openCodes = await _db.ProjectCommitments
            .Where(c => c.CustomerProjectId == project.Id
                     && (c.Status == ProjectCommitmentStatus.Open || c.Status == ProjectCommitmentStatus.PartiallyReceived))
            .OrderBy(c => c.Id)
            .Select(c => c.Code)
            .ToListAsync(ct);

        if (openCodes.Count > 0 && !req.WaiveOpenCommitments)
            return Result.Failure<CustomerProject>(
                $"Cannot close '{project.Code}' — {openCodes.Count} open commitment(s): {string.Join(", ", openCodes)}. " +
                "Receive/close them or close with the open-commitment waiver.");

        project.Status = CustomerProjectStatus.Closed;
        project.ClosedAt = DateTime.UtcNow;
        project.ModifiedAt = DateTime.UtcNow;
        project.ModifiedBy = req.ClosedBy;
        await _db.SaveChangesAsync(ct);

        if (openCodes.Count > 0)
            _log.LogWarning("Project {Code} closed with {Count} open commitment(s) WAIVED by {By}: {Codes}.",
                project.Code, openCodes.Count, req.ClosedBy ?? "(unknown)", string.Join(", ", openCodes));
        else
            _log.LogInformation("Project {Code} closed with no open commitments.", project.Code);

        return Result.Success(project);
    }
}
