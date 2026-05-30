// ADR-025 D5 / Sprint 13.5 PR #2 — CustomerProjectService implementation.
//
// Eight mutation methods on the Sprint 13.5 hierarchy:
//   1. CreateAsync                       — new project (+chain node)
//   2. UpdateHeaderAsync                 — editable header
//   3. UpdateStatusAsync                 — status transition w/ legal-map check
//   4. AddMemberAsync                    — ProjectMember (+MEMBER_OF edge)
//   5. AddPhaseAsync                     — ProjectPhase (no chain emit; WBS-only)
//   6. LinkProductionOrderAsync          — set FK + posting mode (+CONTAINS_PRODUCTION_ORDER edge)
//   7. CreateAmendmentAsync              — Draft amendment, MAX+1 numbering under lock
//   8. TransitionAmendmentStatusAsync    — workflow transition w/ legal-map check
//
// Chain emit failure isolation matches WorkOrderService.CapitalizeAsync —
// if RecordEdgeAsync throws (DB hiccup, missing tenant, etc.) we LogWarning
// and continue. The primary mutation has already committed; the chain edge
// can be recovered by the Sprint 12D PR #6 backfill pass.
//
// Reads (cockpit projections, list pages, voice "explain why" upstream
// traversal) still go through PageModel._context per the WorkOrderService
// precedent — this service owns mutations only.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Projects;

public sealed class CustomerProjectService : ICustomerProjectService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService _chainOfCustody;
    private readonly ILogger<CustomerProjectService> _logger;

    public CustomerProjectService(
        AppDbContext db,
        ITenantContext tenantContext,
        Abs.FixedAssets.Services.ChainOfCustody.IChainOfCustodyService chainOfCustody,
        ILogger<CustomerProjectService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _chainOfCustody = chainOfCustody;
        _logger = logger;
    }

    // ----------------------------------------------------------------
    // 1. CreateAsync
    // ----------------------------------------------------------------
    public async Task<Result<CustomerProject>> CreateAsync(
        CreateCustomerProjectRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Result.Failure<CustomerProject>("Project Code is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<CustomerProject>("Project Name is required.");

        if (!_tenantContext.VisibleCompanyIds.Contains(request.CompanyId))
            return Result.Failure<CustomerProject>(
                $"Company {request.CompanyId} is not visible to the current tenant.");

        var company = await _db.Companies
            .Where(c => c.Id == request.CompanyId)
            .Select(c => new { c.Id, c.ProjectExportControlRequired })
            .FirstOrDefaultAsync(ct);
        if (company == null)
            return Result.Failure<CustomerProject>($"Company {request.CompanyId} not found.");

        // ADR-026 export-control rule — if the company requires export control
        // metadata on every project, the caller MUST supply something other
        // than ExportControl.None. Service layer is the gate; no DB CHECK
        // because the rule is company-conditional.
        if (company.ProjectExportControlRequired && request.ExportControl == ExportControl.None)
            return Result.Failure<CustomerProject>(
                "Company requires Export Control to be set on every project (ProjectExportControlRequired=true).");

        if (request.PrimaryCustomerId.HasValue)
        {
            var customerOk = await _db.Customers
                .Where(c => c.Id == request.PrimaryCustomerId.Value && c.CompanyId == request.CompanyId)
                .AnyAsync(ct);
            if (!customerOk)
                return Result.Failure<CustomerProject>(
                    $"PrimaryCustomer {request.PrimaryCustomerId} does not belong to company {request.CompanyId}.");
        }

        // UNIQUE (CompanyId, Code) — match the schema constraint with a
        // friendly pre-check so PageModels can route the message into
        // ModelState instead of swallowing a DbUpdateException.
        var codeTaken = await _db.CustomerProjects
            .Where(p => p.CompanyId == request.CompanyId && p.Code == request.Code)
            .AnyAsync(ct);
        if (codeTaken)
            return Result.Failure<CustomerProject>(
                $"Project Code '{request.Code}' is already in use for this company.");

        var project = new CustomerProject
        {
            CompanyId            = request.CompanyId,
            ProgramId            = request.ProgramId,
            PrimaryCustomerId    = request.PrimaryCustomerId,
            Code                 = request.Code,
            Name                 = request.Name,
            Description          = request.Description,
            Status               = CustomerProjectStatus.Quote,
            Mode                 = request.Mode,
            CostingMode          = request.CostingMode,
            RevenueMode          = request.RevenueMode,
            ContractValue        = request.ContractValue,
            Currency             = string.IsNullOrWhiteSpace(request.Currency) ? "CAD" : request.Currency,
            TargetStartDate      = request.TargetStartDate,
            TargetEndDate        = request.TargetEndDate,
            ProjectManagerName   = request.ProjectManagerName,
            ProjectManagerId     = request.ProjectManagerId,
            CustomerPoNumber     = request.CustomerPoNumber,
            ContractType         = request.ContractType,
            QualityProgram       = request.QualityProgram,
            ExportControl        = request.ExportControl,
            CreatedAt            = DateTime.UtcNow,
            CreatedBy            = request.CreatedBy
        };

        _db.CustomerProjects.Add(project);
        await _db.SaveChangesAsync(ct);

        // Ensure the chain node exists from day one so the upstream graph
        // is queryable even before any edges are wired in.
        try
        {
            await _chainOfCustody.EnsureNodeAsync(
                new Abs.FixedAssets.Services.ChainOfCustody.EnsureNodeRequest(
                    NodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.CustomerProject,
                    EntityId: project.Id,
                    Label:    project.Code),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Chain node emit failed for CustomerProject {ProjectId}. Backfill recovers.",
                project.Id);
        }

        return Result.Success(project);
    }

    // ----------------------------------------------------------------
    // 2. UpdateHeaderAsync
    // ----------------------------------------------------------------
    public async Task<Result<CustomerProject>> UpdateHeaderAsync(
        UpdateCustomerProjectHeaderRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<CustomerProject>("Project Name is required.");

        var project = await _db.CustomerProjects
            .Where(p => p.Id == request.CustomerProjectId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (project == null)
            return Result.Failure<CustomerProject>(
                $"Customer project {request.CustomerProjectId} not found or not visible.");

        if (project.Status == CustomerProjectStatus.Closed || project.Status == CustomerProjectStatus.Cancelled)
            return Result.Failure<CustomerProject>(
                $"Project is {project.Status} — header edits are not allowed on terminal-status projects.");

        project.Name               = request.Name;
        project.Description        = request.Description;
        project.Mode               = request.Mode;
        project.CostingMode        = request.CostingMode;
        project.RevenueMode        = request.RevenueMode;
        project.ContractValue      = request.ContractValue;
        project.Currency           = string.IsNullOrWhiteSpace(request.Currency) ? project.Currency : request.Currency;
        project.TargetStartDate    = request.TargetStartDate;
        project.TargetEndDate      = request.TargetEndDate;
        project.ProjectManagerName = request.ProjectManagerName;
        project.ProjectManagerId   = request.ProjectManagerId;
        project.CustomerPoNumber   = request.CustomerPoNumber;
        project.ContractType       = request.ContractType;
        project.QualityProgram     = request.QualityProgram;
        project.ExportControl      = request.ExportControl;
        project.ModifiedAt         = DateTime.UtcNow;
        project.ModifiedBy         = request.ModifiedBy;

        await _db.SaveChangesAsync(ct);
        return Result.Success(project);
    }

    // ----------------------------------------------------------------
    // 3. UpdateStatusAsync — legal-transition map enforced in code.
    // ----------------------------------------------------------------
    public async Task<Result<CustomerProject>> UpdateStatusAsync(
        UpdateCustomerProjectStatusRequest request,
        CancellationToken ct)
    {
        var project = await _db.CustomerProjects
            .Where(p => p.Id == request.CustomerProjectId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (project == null)
            return Result.Failure<CustomerProject>(
                $"Customer project {request.CustomerProjectId} not found or not visible.");

        if (!IsLegalProjectStatusTransition(project.Status, request.NewStatus))
            return Result.Failure<CustomerProject>(
                $"Illegal status transition: {project.Status} → {request.NewStatus}.");

        project.Status     = request.NewStatus;
        project.ModifiedAt = DateTime.UtcNow;
        project.ModifiedBy = request.ModifiedBy;
        if (request.NewStatus == CustomerProjectStatus.Closed)
            project.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result.Success(project);
    }

    private static bool IsLegalProjectStatusTransition(
        CustomerProjectStatus from,
        CustomerProjectStatus to)
    {
        if (from == to) return true; // idempotent no-op
        return (from, to) switch
        {
            (CustomerProjectStatus.Quote, CustomerProjectStatus.Active)     => true,
            (CustomerProjectStatus.Quote, CustomerProjectStatus.Cancelled)  => true,
            (CustomerProjectStatus.Active, CustomerProjectStatus.OnHold)    => true,
            (CustomerProjectStatus.Active, CustomerProjectStatus.Closed)    => true,
            (CustomerProjectStatus.Active, CustomerProjectStatus.Cancelled) => true,
            (CustomerProjectStatus.OnHold, CustomerProjectStatus.Active)    => true,
            (CustomerProjectStatus.OnHold, CustomerProjectStatus.Cancelled) => true,
            // Closed and Cancelled are terminal.
            _ => false
        };
    }

    // ----------------------------------------------------------------
    // 4. AddMemberAsync — Customer → CustomerProject MEMBER_OF edge.
    // ----------------------------------------------------------------
    public async Task<Result<ProjectMember>> AddMemberAsync(
        AddProjectMemberRequest request,
        CancellationToken ct)
    {
        var project = await _db.CustomerProjects
            .Where(p => p.Id == request.CustomerProjectId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.Id, p.Code, p.CompanyId })
            .FirstOrDefaultAsync(ct);
        if (project == null)
            return Result.Failure<ProjectMember>(
                $"Customer project {request.CustomerProjectId} not found or not visible.");

        var customer = await _db.Customers
            .Where(c => c.Id == request.CustomerId && c.CompanyId == project.CompanyId)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync(ct);
        if (customer == null)
            return Result.Failure<ProjectMember>(
                $"Customer {request.CustomerId} does not belong to the project's company.");

        var duplicate = await _db.ProjectMembers
            .Where(m => m.CustomerProjectId == request.CustomerProjectId
                     && m.CustomerId == request.CustomerId
                     && m.Role == request.Role)
            .AnyAsync(ct);
        if (duplicate)
            return Result.Failure<ProjectMember>(
                $"Customer {request.CustomerId} is already a {request.Role} member of this project.");

        var member = new ProjectMember
        {
            CustomerProjectId = request.CustomerProjectId,
            CustomerId        = request.CustomerId,
            Role              = request.Role,
            SharePct          = request.SharePct,
            CreatedAt         = DateTime.UtcNow
        };

        _db.ProjectMembers.Add(member);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _chainOfCustody.RecordEdgeAsync(
                new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                    FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.Customer,
                    FromEntityId: customer.Id,
                    FromLabel:    customer.Name,
                    ToNodeType:   Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.CustomerProject,
                    ToEntityId:   project.Id,
                    ToLabel:      project.Code,
                    EdgeType:     Abs.FixedAssets.Models.ChainOfCustody.ChainEdgeTypes.MemberOf),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Chain edge emit failed for ProjectMember {MemberId} (Customer {CustomerId} → Project {ProjectId}). Backfill recovers.",
                member.Id, customer.Id, project.Id);
        }

        return Result.Success(member);
    }

    // ----------------------------------------------------------------
    // 5. AddPhaseAsync — no chain emit (phases are internal WBS).
    // ----------------------------------------------------------------
    public async Task<Result<ProjectPhase>> AddPhaseAsync(
        AddProjectPhaseRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return Result.Failure<ProjectPhase>("Phase Code is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<ProjectPhase>("Phase Name is required.");

        var project = await _db.CustomerProjects
            .Where(p => p.Id == request.CustomerProjectId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.Id })
            .FirstOrDefaultAsync(ct);
        if (project == null)
            return Result.Failure<ProjectPhase>(
                $"Customer project {request.CustomerProjectId} not found or not visible.");

        // B9 Wave 3 PR-7 — derive WbsLevel from the parent (root = 1).
        int wbsLevel = 1;
        if (request.ParentPhaseId.HasValue)
        {
            var parent = await _db.ProjectPhases
                .Where(p => p.Id == request.ParentPhaseId.Value
                         && p.CustomerProjectId == request.CustomerProjectId)
                .Select(p => new { p.WbsLevel })
                .FirstOrDefaultAsync(ct);
            if (parent is null)
                return Result.Failure<ProjectPhase>(
                    $"Parent phase {request.ParentPhaseId} not found within this project.");
            wbsLevel = parent.WbsLevel + 1;
        }

        if (request.WeightPercent is < 0 or > 100)
            return Result.Failure<ProjectPhase>("WeightPercent must be between 0 and 100.");
        if (request.PercentComplete is < 0 or > 100)
            return Result.Failure<ProjectPhase>("PercentComplete must be between 0 and 100.");
        if (request.PlannedCost is < 0)
            return Result.Failure<ProjectPhase>("PlannedCost cannot be negative.");

        var codeTaken = await _db.ProjectPhases
            .Where(p => p.CustomerProjectId == request.CustomerProjectId && p.Code == request.Code)
            .AnyAsync(ct);
        if (codeTaken)
            return Result.Failure<ProjectPhase>(
                $"Phase Code '{request.Code}' is already in use for this project.");

        var phase = new ProjectPhase
        {
            CustomerProjectId = request.CustomerProjectId,
            ParentPhaseId     = request.ParentPhaseId,
            Code              = request.Code,
            Name              = request.Name,
            Description       = request.Description,
            SortOrder         = request.SortOrder,
            CreatedAt         = DateTime.UtcNow,
            CreatedBy         = request.CreatedBy,
            // WBS attributes
            WbsType           = request.WbsType,
            WbsLevel          = wbsLevel,
            ResponsibleOwner  = string.IsNullOrWhiteSpace(request.ResponsibleOwner) ? null : request.ResponsibleOwner.Trim(),
            ControlAccount    = string.IsNullOrWhiteSpace(request.ControlAccount) ? null : request.ControlAccount.Trim(),
            PlannedCost       = request.PlannedCost,
            WeightPercent     = request.WeightPercent,
            PercentComplete   = request.PercentComplete,
        };

        _db.ProjectPhases.Add(phase);
        await _db.SaveChangesAsync(ct);
        return Result.Success(phase);
    }

    // ----------------------------------------------------------------
    // 6. LinkProductionOrderAsync — CustomerProject → ProductionOrder
    //    CONTAINS_PRODUCTION_ORDER edge. Posting mode is mandatory per
    //    the schema comment on ProductionOrder.ProjectPostingMode.
    // ----------------------------------------------------------------
    public async Task<Result<LinkProductionOrderOutcome>> LinkProductionOrderAsync(
        LinkProductionOrderRequest request,
        CancellationToken ct)
    {
        var project = await _db.CustomerProjects
            .Where(p => p.Id == request.CustomerProjectId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.Id, p.Code, p.CompanyId, p.Status })
            .FirstOrDefaultAsync(ct);
        if (project == null)
            return Result.Failure<LinkProductionOrderOutcome>(
                $"Customer project {request.CustomerProjectId} not found or not visible.");

        if (project.Status == CustomerProjectStatus.Closed || project.Status == CustomerProjectStatus.Cancelled)
            return Result.Failure<LinkProductionOrderOutcome>(
                $"Project is {project.Status} — no new jobs can be linked.");

        // ProductionOrder has no direct CompanyId yet (PR #3 may denormalize
        // it). Until then we tenant-scope through the nullable Location and
        // Customer FKs; an order with neither set is unlinkable to a project.
        var orderScope = await _db.ProductionOrders
            .Where(p => p.Id == request.ProductionOrderId)
            .Select(p => new
            {
                Order      = p,
                CompanyVia = (int?)(p.Location != null ? p.Location.CompanyId : null)
                             ?? (int?)(p.Customer != null ? p.Customer.CompanyId : null)
            })
            .FirstOrDefaultAsync(ct);
        if (orderScope == null)
            return Result.Failure<LinkProductionOrderOutcome>(
                $"Production order {request.ProductionOrderId} not found.");

        if (orderScope.CompanyVia == null)
            return Result.Failure<LinkProductionOrderOutcome>(
                $"Production order {request.ProductionOrderId} has no Location or Customer — assign one before linking to a project.");

        if (!_tenantContext.VisibleCompanyIds.Contains(orderScope.CompanyVia.Value))
            return Result.Failure<LinkProductionOrderOutcome>(
                $"Production order {request.ProductionOrderId} is not visible to the current tenant.");

        if (orderScope.CompanyVia != project.CompanyId)
            return Result.Failure<LinkProductionOrderOutcome>(
                "Production order and customer project belong to different companies.");

        var productionOrder = orderScope.Order;

        if (request.ProjectPhaseId.HasValue)
        {
            var phaseOk = await _db.ProjectPhases
                .Where(p => p.Id == request.ProjectPhaseId.Value
                         && p.CustomerProjectId == request.CustomerProjectId)
                .AnyAsync(ct);
            if (!phaseOk)
                return Result.Failure<LinkProductionOrderOutcome>(
                    $"Phase {request.ProjectPhaseId} not found within project {request.CustomerProjectId}.");
        }

        productionOrder.CustomerProjectId   = request.CustomerProjectId;
        productionOrder.ProjectPhaseId      = request.ProjectPhaseId;
        productionOrder.ProjectPostingMode  = request.PostingMode;
        productionOrder.ModifiedAt          = DateTime.UtcNow;
        productionOrder.ModifiedBy          = request.ModifiedBy;

        await _db.SaveChangesAsync(ct);

        try
        {
            await _chainOfCustody.RecordEdgeAsync(
                new Abs.FixedAssets.Services.ChainOfCustody.RecordEdgeRequest(
                    FromNodeType: Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.CustomerProject,
                    FromEntityId: project.Id,
                    FromLabel:    project.Code,
                    ToNodeType:   Abs.FixedAssets.Models.ChainOfCustody.ChainNodeTypes.ProductionOrder,
                    ToEntityId:   productionOrder.Id,
                    ToLabel:      productionOrder.OrderNumber ?? $"PRO-{productionOrder.Id}",
                    EdgeType:     Abs.FixedAssets.Models.ChainOfCustody.ChainEdgeTypes.ContainsProductionOrder),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Chain edge emit failed for CustomerProject {ProjectId} → ProductionOrder {OrderId}. Backfill recovers.",
                project.Id, productionOrder.Id);
        }

        return Result.Success(new LinkProductionOrderOutcome(
            productionOrder.Id,
            request.CustomerProjectId,
            request.ProjectPhaseId,
            request.PostingMode));
    }

    // ----------------------------------------------------------------
    // 7. CreateAmendmentAsync — MAX+1 numbering under a row-level lock.
    //    The schema's UNIQUE INDEX ix_projectamendments_project_number
    //    is the backstop if the FOR UPDATE is somehow bypassed.
    // ----------------------------------------------------------------
    public async Task<Result<ProjectAmendment>> CreateAmendmentAsync(
        CreateAmendmentRequest request,
        CancellationToken ct)
    {
        var project = await _db.CustomerProjects
            .Where(p => p.Id == request.CustomerProjectId
                && _tenantContext.VisibleCompanyIds.Contains(p.CompanyId ?? 0))
            .Select(p => new { p.Id, p.Status, p.ContractValue })
            .FirstOrDefaultAsync(ct);
        if (project == null)
            return Result.Failure<ProjectAmendment>(
                $"Customer project {request.CustomerProjectId} not found or not visible.");

        if (project.Status == CustomerProjectStatus.Cancelled)
            return Result.Failure<ProjectAmendment>(
                "Cannot create amendments on a Cancelled project.");

        // Sanity guard: a single amendment cannot drive the contract negative
        // versus baseline. Customers can still bring it to zero; just not below.
        // (Effective value = baseline + SUM(approved deltas); see schema header.)
        if (project.ContractValue.HasValue
            && project.ContractValue.Value + request.ValueDelta < 0)
            return Result.Failure<ProjectAmendment>(
                "Amendment ValueDelta would drive the baseline contract value below zero.");

        // Row-level lock on the parent so MAX(AmendmentNumber)+1 is collision-
        // free. The accompanying transaction commits with SaveChangesAsync.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        _ = await _db.Database.ExecuteSqlRawAsync(
            "SELECT \"Id\" FROM \"CustomerProjects\" WHERE \"Id\" = {0} FOR UPDATE",
            new object[] { request.CustomerProjectId },
            ct);

        var nextNumber = await _db.ProjectAmendments
            .Where(a => a.CustomerProjectId == request.CustomerProjectId)
            .MaxAsync(a => (int?)a.AmendmentNumber, ct) ?? 0;
        nextNumber++;

        var amendment = new ProjectAmendment
        {
            CustomerProjectId    = request.CustomerProjectId,
            AmendmentNumber      = nextNumber,
            EffectiveDate        = request.EffectiveDate,
            ChangeType           = request.ChangeType,
            Reason               = request.Reason,
            ScopeNarrative       = request.ScopeNarrative,
            ValueDelta           = request.ValueDelta,
            TargetStartDateDelta = request.TargetStartDateDelta,
            TargetEndDateDelta   = request.TargetEndDateDelta,
            SourceQuotationId    = request.SourceQuotationId,
            CustomerReference    = request.CustomerReference,
            Notes                = request.Notes,
            Status               = ProjectAmendmentStatus.Draft,
            CreatedAt            = DateTime.UtcNow,
            CreatedBy            = request.CreatedBy
        };

        _db.ProjectAmendments.Add(amendment);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result.Success(amendment);
    }

    // ----------------------------------------------------------------
    // 8. TransitionAmendmentStatusAsync — Draft → Submitted → Approved/
    //    Rejected/Withdrawn; any non-Voided → Voided. The Postgres
    //    trigger fn_block_amendment_status_regression is the backstop.
    // ----------------------------------------------------------------
    public async Task<Result<ProjectAmendment>> TransitionAmendmentStatusAsync(
        TransitionAmendmentStatusRequest request,
        CancellationToken ct)
    {
        var amendment = await _db.ProjectAmendments
            .Include(a => a.Project)
            .Where(a => a.Id == request.ProjectAmendmentId
                && a.Project != null
                && _tenantContext.VisibleCompanyIds.Contains(a.Project.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);
        if (amendment == null)
            return Result.Failure<ProjectAmendment>(
                $"Amendment {request.ProjectAmendmentId} not found or not visible.");

        if (!IsLegalAmendmentTransition(amendment.Status, request.NewStatus))
            return Result.Failure<ProjectAmendment>(
                $"Illegal amendment status transition: {amendment.Status} → {request.NewStatus}.");

        amendment.Status     = request.NewStatus;
        amendment.ModifiedAt = DateTime.UtcNow;
        amendment.ModifiedBy = request.ModifiedBy;

        if (request.NewStatus == ProjectAmendmentStatus.Approved)
        {
            amendment.ApprovedAt          = DateTime.UtcNow;
            amendment.ApprovedById        = request.ApprovedById;
            amendment.ApprovedByName      = request.ApprovedByName;
            amendment.CustomerSignatureAt = request.CustomerSignatureAt ?? amendment.CustomerSignatureAt;
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(amendment);
    }

    private static bool IsLegalAmendmentTransition(
        ProjectAmendmentStatus from,
        ProjectAmendmentStatus to)
    {
        if (from == to) return true; // idempotent no-op
        // Voided is the universal exit from any non-Voided state. Otherwise:
        if (to == ProjectAmendmentStatus.Voided)
            return from != ProjectAmendmentStatus.Voided;
        return (from, to) switch
        {
            (ProjectAmendmentStatus.Draft,     ProjectAmendmentStatus.Submitted) => true,
            (ProjectAmendmentStatus.Draft,     ProjectAmendmentStatus.Withdrawn) => true,
            (ProjectAmendmentStatus.Submitted, ProjectAmendmentStatus.Approved)  => true,
            (ProjectAmendmentStatus.Submitted, ProjectAmendmentStatus.Rejected)  => true,
            (ProjectAmendmentStatus.Submitted, ProjectAmendmentStatus.Withdrawn) => true,
            // Approved / Rejected / Withdrawn are terminal except for the
            // universal Voided exit handled above.
            _ => false
        };
    }
}
