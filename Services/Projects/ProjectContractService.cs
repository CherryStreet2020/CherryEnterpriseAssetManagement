// Theme B9 Wave 2 PR-6 (2026-05-30, CLOSES B9 Wave 2) — ProjectContractService impl.
// Tenant-scoped. Hosts the two §20 gates (award validation, contract-review gate)
// and the winning-revision→baseline stamp.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class ProjectContractService : IProjectContractService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ProjectContractService> _log;

    public ProjectContractService(AppDbContext db, ITenantContext tenant, ILogger<ProjectContractService> log)
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

    public async Task<Result<int>> CreateContractAsync(CreateContractRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.ContractNumber))
            return Result.Failure<int>("A contract number is required.");

        var (ok, companyId, err) = await ResolveProjectCompanyAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        var num = req.ContractNumber.Trim();
        bool dup = await _db.ProjectContracts.AnyAsync(c => c.CompanyId == companyId && c.ContractNumber == num, ct);
        if (dup) return Result.Failure<int>($"Contract number '{num}' already exists for this company.");

        var contract = new ProjectContract
        {
            CompanyId = companyId,
            CustomerProjectId = req.CustomerProjectId,
            ContractNumber = num,
            Title = req.Title,
            Description = req.Description,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency!.Trim(),
            Status = ProjectContractStatus.Draft,
            ReviewRequired = req.ReviewRequired,
            ReviewStatus = ProjectContractReviewStatus.NotStarted,
            ReviewDueDate = req.ReviewDueDate,
        };
        _db.ProjectContracts.Add(contract);
        await _db.SaveChangesAsync(ct);
        return Result.Success(contract.Id);
    }

    public async Task<Result<int>> AddLineAsync(AddContractLineRequest req, CancellationToken ct = default)
    {
        if (req is null || req.ContractId <= 0) return Result.Failure<int>("A contract id is required.");

        var c = await _db.ProjectContracts
            .Where(x => x.Id == req.ContractId)
            .Select(x => new { x.Id, x.CompanyId })
            .FirstOrDefaultAsync(ct);
        if (c == null || !_tenant.VisibleCompanyIds.Contains(c.CompanyId))
            return Result.Failure<int>($"Contract {req.ContractId} not found in your tenant scope.");

        if (req.ItemId is { } itemId)
        {
            bool itemOk = await _db.Items.AnyAsync(
                i => i.Id == itemId && (i.CompanyId == null || _tenant.VisibleCompanyIds.Contains(i.CompanyId.Value)), ct);
            if (!itemOk) return Result.Failure<int>($"Item {itemId} is not in your tenant scope.");
        }

        int lineNo = req.LineNo ?? ((await _db.ProjectContractLines
            .Where(l => l.ProjectContractId == req.ContractId)
            .Select(l => (int?)l.LineNo).MaxAsync(ct) ?? 0) + 1);

        var line = new ProjectContractLine
        {
            ProjectContractId = req.ContractId,
            LineNo = lineNo,
            ContractLineReference = req.ContractLineReference,
            ItemId = req.ItemId,
            PartNumber = req.PartNumber,
            Description = req.Description,
            Quantity = req.Quantity,
            Uom = req.Uom,
            UnitPrice = req.UnitPrice,
            ExtendedPrice = req.UnitPrice.HasValue ? req.Quantity * req.UnitPrice.Value : (decimal?)null,
            BaselineStart = req.BaselineStart,
            BaselineFinish = req.BaselineFinish,
        };
        _db.ProjectContractLines.Add(line);
        await _db.SaveChangesAsync(ct);
        return Result.Success(line.Id);
    }

    public async Task<Result<int>> RecordCustomerPoAsync(RecordCustomerPoRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.CustomerPoNumber))
            return Result.Failure<int>("A customer PO number is required.");

        var (ok, companyId, err) = await ResolveProjectCompanyAsync(req.CustomerProjectId, ct);
        if (!ok) return Result.Failure<int>(err!);

        var poNo = req.CustomerPoNumber.Trim();
        bool dup = await _db.ProjectCustomerPOs.AnyAsync(p => p.CompanyId == companyId && p.CustomerPoNumber == poNo, ct);
        if (dup) return Result.Failure<int>($"Customer PO '{poNo}' already exists for this company.");

        if (req.ProjectContractId is { } cid)
        {
            bool contractOk = await _db.ProjectContracts.AnyAsync(
                c => c.Id == cid && c.CompanyId == companyId && c.CustomerProjectId == req.CustomerProjectId, ct);
            if (!contractOk) return Result.Failure<int>($"Contract {cid} is not on this project.");
        }

        var po = new ProjectCustomerPO
        {
            CompanyId = companyId,
            CustomerProjectId = req.CustomerProjectId,
            ProjectContractId = req.ProjectContractId,
            CustomerPoNumber = poNo,
            PoDate = req.PoDate,
            PoValue = req.PoValue,
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency!.Trim(),
            Description = req.Description,
            Status = ProjectCustomerPoStatus.Open,
        };
        _db.ProjectCustomerPOs.Add(po);
        await _db.SaveChangesAsync(ct);
        return Result.Success(po.Id);
    }

    public async Task<Result<ProjectContractSummary>> CompleteReviewAsync(int contractId, string? reviewedByName = null, bool waive = false, CancellationToken ct = default)
    {
        var c = await _db.ProjectContracts.FirstOrDefaultAsync(x => x.Id == contractId, ct);
        if (c == null || !_tenant.VisibleCompanyIds.Contains(c.CompanyId))
            return Result.Failure<ProjectContractSummary>($"Contract {contractId} not found in your tenant scope.");

        c.ReviewStatus = waive ? ProjectContractReviewStatus.Waived : ProjectContractReviewStatus.Complete;
        c.ReviewCompletedAt = DateTime.UtcNow;
        c.ReviewedByName = reviewedByName;
        c.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success(ToSummary(c));
    }

    public async Task<Result<AwardResult>> AwardQuoteRevisionAsync(AwardQuoteRevisionRequest req, CancellationToken ct = default)
    {
        if (req is null || req.ContractId <= 0 || req.RevisionId <= 0)
            return Result.Failure<AwardResult>("A contract id and a revision id are required.");

        var contract = await _db.ProjectContracts.FirstOrDefaultAsync(x => x.Id == req.ContractId, ct);
        if (contract == null || !_tenant.VisibleCompanyIds.Contains(contract.CompanyId))
            return Result.Failure<AwardResult>($"Contract {req.ContractId} not found in your tenant scope.");

        var rev = await _db.ProjectQuoteRevisions.Include(r => r.Quote)
            .FirstOrDefaultAsync(r => r.Id == req.RevisionId, ct);
        if (rev == null || rev.Quote == null || !_tenant.VisibleCompanyIds.Contains(rev.Quote.CompanyId)
            || rev.Quote.CustomerProjectId != contract.CustomerProjectId)
            return Result.Failure<AwardResult>($"Quote revision {req.RevisionId} is not on this contract's project.");

        // A revision can only be awarded once it has a FROZEN price (Submitted/locked).
        if (!rev.IsSnapshotLocked || rev.VersionStatus != ProjectQuoteRevisionStatus.Submitted)
            return Result.Failure<AwardResult>(
                "Only a submitted quote revision can be awarded — submit it first so its price is frozen.");

        // §20: cannot mark project awarded without an APPROVED quote or an AUTHORIZED OVERRIDE.
        // "Approved" = the revision needed no approval (NotRequired) or was Approved.
        bool quoteApproved = rev.ApprovalStatus is ProjectQuoteApprovalStatus.Approved
            or ProjectQuoteApprovalStatus.NotRequired;
        if (!quoteApproved && !req.AuthorizedOverride)
            return Result.Failure<AwardResult>(
                "Cannot award: this quote revision is not approved. Get approval, or award with an authorized override.");

        var now = DateTime.UtcNow;

        // Winning revision → baseline.
        rev.VersionStatus = ProjectQuoteRevisionStatus.Awarded;
        rev.ConvertedToBaseline = true;
        rev.ModifiedAt = now;

        rev.Quote.AwardedRevisionId = rev.Id;
        rev.Quote.Status = ProjectQuoteStatus.Won;
        rev.Quote.ModifiedAt = now;

        contract.AwardedProjectQuoteId = rev.Quote.Id;
        contract.AwardedRevisionId = rev.Id;
        contract.AwardDate = now;
        contract.BaselineContractValue = rev.TotalPrice;
        contract.Status = ProjectContractStatus.Awarded;
        contract.ModifiedAt = now;

        // Stamp the project baseline (the immutable ContractValue; amendments layer on top).
        var project = await _db.CustomerProjects.FirstOrDefaultAsync(p => p.Id == contract.CustomerProjectId, ct);
        if (project != null)
        {
            project.ContractValue = rev.TotalPrice;
            project.ModifiedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        return Result.Success(new AwardResult(
            contract.Id, rev.Quote.Id, rev.Id, contract.BaselineContractValue, now));
    }

    public async Task<Result<ProjectContractSummary>> LaunchProjectAsync(int contractId, CancellationToken ct = default)
    {
        var contract = await _db.ProjectContracts.FirstOrDefaultAsync(x => x.Id == contractId, ct);
        if (contract == null || !_tenant.VisibleCompanyIds.Contains(contract.CompanyId))
            return Result.Failure<ProjectContractSummary>($"Contract {contractId} not found in your tenant scope.");

        // §20 contract-review gate: cannot launch until required review is complete.
        if (contract.ReviewRequired
            && contract.ReviewStatus is not (ProjectContractReviewStatus.Complete or ProjectContractReviewStatus.Waived))
            return Result.Failure<ProjectContractSummary>(
                "Cannot launch the project: the required contract review is not complete.");

        var now = DateTime.UtcNow;
        contract.Status = ProjectContractStatus.Active;
        contract.LaunchedAt = now;
        contract.ModifiedAt = now;

        // Launch the project (Quote → Active).
        var project = await _db.CustomerProjects.FirstOrDefaultAsync(p => p.Id == contract.CustomerProjectId, ct);
        if (project != null && project.Status == CustomerProjectStatus.Quote)
        {
            project.Status = CustomerProjectStatus.Active;
            project.ModifiedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(ToSummary(contract));
    }

    public async Task<Result<IReadOnlyList<ProjectContractSummary>>> GetContractsForProjectAsync(int customerProjectId, CancellationToken ct = default)
    {
        var (ok, _, err) = await ResolveProjectCompanyAsync(customerProjectId, ct);
        if (!ok) return Result.Failure<IReadOnlyList<ProjectContractSummary>>(err!);

        var list = await _db.ProjectContracts
            .Where(c => c.CustomerProjectId == customerProjectId && _tenant.VisibleCompanyIds.Contains(c.CompanyId))
            .OrderBy(c => c.Id)
            .Select(c => new ProjectContractSummary(
                c.Id, c.ContractNumber, c.Status, c.ReviewStatus, c.ReviewRequired,
                c.BaselineContractValue, c.AwardedRevisionId, c.AwardDate, c.LaunchedAt))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<ProjectContractSummary>>(list);
    }

    private static ProjectContractSummary ToSummary(ProjectContract c) => new(
        c.Id, c.ContractNumber, c.Status, c.ReviewStatus, c.ReviewRequired,
        c.BaselineContractValue, c.AwardedRevisionId, c.AwardDate, c.LaunchedAt);
}
