// ADR-025 D5 / Sprint 13.5 PR #2 — ICustomerProjectService.
//
// First mutation surface for the Sprint 13.5 CustomerProject hierarchy
// (Programs / CustomerProjects / ProjectMembers / ProjectPhases /
// ProjectAmendments + the nullable ProductionOrder.CustomerProjectId FK).
// Every PageModel and voice-intent handler that mutates a CustomerProject
// or its children calls THIS interface — never AppDbContext directly.
//
// Companion: every mutating call that touches custody-relevant state emits
// chain edges via IChainOfCustodyService (ADR-022). Reads stay in PageModels
// for thin display projections per the WorkOrderService precedent.
//
// Scope discipline (per locked Sprint 13.5 plan):
//   IN  — Create / UpdateHeader / UpdateStatus / AddMember / AddPhase /
//         LinkProductionOrder / CreateAmendment / TransitionAmendmentStatus.
//         These are the eight mutations PR #4 UI shell + PR #5a cockpit +
//         PR #7 voice intents require.
//   OUT — RemoveMember + UnlinkProductionOrder (rare; PR #5b polish).
//   OUT — Phase/Amendment chain emit (phases are internal WBS; amendment
//         audit trail = the append-only table + the
//         fn_block_amendment_status_regression trigger).
//   OUT — Program CRUD (Programs are intentionally empty in v1 per ADR-026
//         — added when a defense customer asks for portfolio rollup).
//
// References:
//   - ADR-014 §D2 (Result<T> envelope) + §D5 (IdempotencyMediator for voice)
//   - ADR-022 (chain-of-custody graph)
//   - ADR-025 D5 (service-layer-first / ControlPlaneAnalyzer)
//   - ADR-026 (Seven Customer Modes — Status × Mode × CostingMode × RevenueMode)
//   - Models/Projects/CustomerProjects.cs (entity surface)
//   - Models/Projects/ProjectAmendment.cs (append-only contract change log)
//   - docs/research/customerproject-field-set.md (PR #1.5 field research)

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

/// <summary>
/// Domain service for <see cref="CustomerProject"/> mutations (Sprint 13.5 PR #2).
/// PageModel and voice-intent callers inject <see cref="ICustomerProjectService"/>
/// instead of <c>AppDbContext</c> for any mutation; reads can still go through the
/// PageModel's own <c>_context</c> for thin display projections.
/// </summary>
public interface ICustomerProjectService
{
    /// <summary>
    /// Create a new <see cref="CustomerProject"/> in <see cref="CustomerProjectStatus.Quote"/>.
    /// Validates the company is tenant-visible, the primary customer (if any)
    /// belongs to the same company, and enforces the ADR-026 export-control
    /// rule: when <c>Company.ProjectExportControlRequired</c> is true the
    /// caller MUST supply a non-<see cref="ExportControl.None"/> value.
    /// Emits a <c>CustomerProject</c> chain node (label = project code).
    /// </summary>
    Task<Result<CustomerProject>> CreateAsync(CreateCustomerProjectRequest request, CancellationToken ct);

    /// <summary>
    /// Update editable header fields on a <see cref="CustomerProject"/> — name,
    /// description, dates, project manager, mode flags. Rejects mutations on
    /// projects whose <see cref="CustomerProjectStatus"/> is Closed or Cancelled
    /// (terminal). Does NOT emit chain edges (header edits aren't custody-relevant).
    /// </summary>
    Task<Result<CustomerProject>> UpdateHeaderAsync(UpdateCustomerProjectHeaderRequest request, CancellationToken ct);

    /// <summary>
    /// Transition a <see cref="CustomerProject"/>'s <see cref="CustomerProjectStatus"/>.
    /// Enforces the legal-transition map (Quote→Active/Cancelled; Active→OnHold/Closed/Cancelled;
    /// OnHold→Active/Cancelled; Closed/Cancelled are terminal). Stamps
    /// <c>ClosedAt</c> when transitioning to Closed.
    /// </summary>
    Task<Result<CustomerProject>> UpdateStatusAsync(UpdateCustomerProjectStatusRequest request, CancellationToken ct);

    /// <summary>
    /// Add a <see cref="ProjectMember"/> row (joint-venture / pass-through /
    /// sub-customer). Enforces UNIQUE (CustomerProjectId, CustomerId, Role).
    /// Emits a <c>Customer --MEMBER_OF--&gt; CustomerProject</c> chain edge so
    /// the project's customer list shows up in the upstream graph for any
    /// downstream artefact (FAI / invoice / GL).
    /// </summary>
    Task<Result<ProjectMember>> AddMemberAsync(AddProjectMemberRequest request, CancellationToken ct);

    /// <summary>
    /// Add a <see cref="ProjectPhase"/> (WBS leaf). v1 UI assumes a flat list;
    /// <see cref="ProjectPhase.ParentPhaseId"/> is reserved for v2 tree-deepening
    /// without a schema migration. Enforces UNIQUE (CustomerProjectId, Code).
    /// Does NOT emit chain edges (phases are internal WBS).
    /// </summary>
    Task<Result<ProjectPhase>> AddPhaseAsync(AddProjectPhaseRequest request, CancellationToken ct);

    /// <summary>
    /// Link a <see cref="Models.ProductionOrder"/> to a <see cref="CustomerProject"/>
    /// (or to a phase within it). Per the schema comment on
    /// <c>ProductionOrder.ProjectPostingMode</c>, the posting mode IS REQUIRED
    /// when this FK is set — the service enforces that here. Optionally pegs
    /// the job to a phase. Emits a <c>CustomerProject --CONTAINS_PRODUCTION_ORDER--&gt;
    /// ProductionOrder</c> chain edge so the audit story (machine event →
    /// project) is intact from day one.
    /// </summary>
    Task<Result<LinkProductionOrderOutcome>> LinkProductionOrderAsync(LinkProductionOrderRequest request, CancellationToken ct);

    /// <summary>
    /// Create a <see cref="ProjectAmendment"/> in <see cref="ProjectAmendmentStatus.Draft"/>.
    /// Computes <c>AmendmentNumber</c> as MAX+1 inside a row-level lock on
    /// the parent project (preventing concurrent inserts from colliding).
    /// Per the schema comment: NEVER a GUID. Validates ValueDelta does not
    /// drive EffectiveContractValue negative (a sanity check; customers can
    /// still cut value to zero — just not below).
    /// </summary>
    Task<Result<ProjectAmendment>> CreateAmendmentAsync(CreateAmendmentRequest request, CancellationToken ct);

    /// <summary>
    /// Transition a <see cref="ProjectAmendment"/> through its workflow
    /// (Draft → Submitted → Approved | Rejected | Withdrawn; any → Voided).
    /// Service enforces the legal-transition map; the
    /// <c>fn_block_amendment_status_regression</c> Postgres trigger
    /// backstops against direct-SQL bypass. Stamps <c>ApprovedAt</c> +
    /// snapshots <c>ApprovedByName</c> when entering Approved.
    /// </summary>
    Task<Result<ProjectAmendment>> TransitionAmendmentStatusAsync(TransitionAmendmentStatusRequest request, CancellationToken ct);
}

// === Request DTOs ===

/// <summary>Inputs for <see cref="ICustomerProjectService.CreateAsync"/>.</summary>
public sealed record CreateCustomerProjectRequest(
    int CompanyId,
    int? ProgramId,
    int? PrimaryCustomerId,
    string Code,
    string Name,
    string? Description,
    CustomerProjectMode Mode,
    CustomerProjectCostingMode CostingMode,
    CustomerProjectRevenueMode RevenueMode,
    decimal? ContractValue,
    string Currency,
    DateTime? TargetStartDate,
    DateTime? TargetEndDate,
    string? ProjectManagerName,
    int? ProjectManagerId,
    string? CustomerPoNumber,
    ContractType? ContractType,
    QualityProgram? QualityProgram,
    ExportControl ExportControl,
    string? CreatedBy);

/// <summary>Inputs for <see cref="ICustomerProjectService.UpdateHeaderAsync"/>.</summary>
public sealed record UpdateCustomerProjectHeaderRequest(
    int CustomerProjectId,
    string Name,
    string? Description,
    CustomerProjectMode Mode,
    CustomerProjectCostingMode CostingMode,
    CustomerProjectRevenueMode RevenueMode,
    decimal? ContractValue,
    string Currency,
    DateTime? TargetStartDate,
    DateTime? TargetEndDate,
    string? ProjectManagerName,
    int? ProjectManagerId,
    string? CustomerPoNumber,
    ContractType? ContractType,
    QualityProgram? QualityProgram,
    ExportControl ExportControl,
    string? ModifiedBy);

/// <summary>Inputs for <see cref="ICustomerProjectService.UpdateStatusAsync"/>.</summary>
public sealed record UpdateCustomerProjectStatusRequest(
    int CustomerProjectId,
    CustomerProjectStatus NewStatus,
    string? ModifiedBy);

/// <summary>Inputs for <see cref="ICustomerProjectService.AddMemberAsync"/>.</summary>
public sealed record AddProjectMemberRequest(
    int CustomerProjectId,
    int CustomerId,
    ProjectMemberRole Role,
    decimal? SharePct);

/// <summary>Inputs for <see cref="ICustomerProjectService.AddPhaseAsync"/>.</summary>
public sealed record AddProjectPhaseRequest(
    int CustomerProjectId,
    int? ParentPhaseId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    string? CreatedBy);

/// <summary>Inputs for <see cref="ICustomerProjectService.LinkProductionOrderAsync"/>.</summary>
public sealed record LinkProductionOrderRequest(
    int ProductionOrderId,
    int CustomerProjectId,
    int? ProjectPhaseId,
    ProjectPostingMode PostingMode,
    string? ModifiedBy);

/// <summary>Outcome of <see cref="ICustomerProjectService.LinkProductionOrderAsync"/>.</summary>
public sealed record LinkProductionOrderOutcome(
    int ProductionOrderId,
    int CustomerProjectId,
    int? ProjectPhaseId,
    ProjectPostingMode PostingMode);

/// <summary>Inputs for <see cref="ICustomerProjectService.CreateAmendmentAsync"/>.</summary>
public sealed record CreateAmendmentRequest(
    int CustomerProjectId,
    DateTime EffectiveDate,
    ProjectAmendmentChangeType ChangeType,
    string? Reason,
    string? ScopeNarrative,
    decimal ValueDelta,
    int? TargetStartDateDelta,
    int? TargetEndDateDelta,
    int? SourceQuotationId,
    string? CustomerReference,
    string? Notes,
    string? CreatedBy);

/// <summary>Inputs for <see cref="ICustomerProjectService.TransitionAmendmentStatusAsync"/>.</summary>
public sealed record TransitionAmendmentStatusRequest(
    long ProjectAmendmentId,
    ProjectAmendmentStatus NewStatus,
    int? ApprovedById,
    string? ApprovedByName,
    DateTime? CustomerSignatureAt,
    string? ModifiedBy);
