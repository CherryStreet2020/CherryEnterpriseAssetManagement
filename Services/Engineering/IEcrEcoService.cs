// Sprint 14.3 PR-1 (2026-05-27) — IEcrEcoService.
//
// Service surface for the ECR/ECO Change Control workflow. Lock 15
// compliant — service-only DI, no DbContext leakage. All mutation
// operations return Result<T> for structured success/failure handling.
//
// LIFECYCLES:
//   ECR: Draft → Submitted → UnderReview → Approved (creates ECO)
//                                          ↘ Rejected
//                                          ↘ Cancelled
//   ECO: Draft → InApproval → Approved → Released → Implemented → Closed
//                                                  ↘ Cancelled
//
// ATOMIC SUPERSEDE INTEGRATION: ReleaseEcoAsync walks the ECO's line items
// and, for each line with NewDocumentVersionId set, delegates to
// IDocumentService.ReleaseVersionAsync — which atomically supersedes the
// prior Released version per the PR #366 DMS substrate. The whole release
// is one transactional unit (PR-FS-6 atomic-supersede lesson).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering;

public interface IEcrEcoService
{
    // ===== ECR operations ===============================================

    /// <summary>
    /// Create an ECR in Draft status. Validates uniqueness of
    /// (CompanyId, EcrNumber). All linked FKs (Item / Document / PRO /
    /// Customer) are optional but validated for tenant consistency
    /// when supplied.
    /// </summary>
    Task<Result<EngineeringChangeRequest>> CreateEcrAsync(
        int companyId,
        string ecrNumber,
        string title,
        string? description,
        ChangeReason changeReason,
        ChangeUrgency urgency,
        bool affectsForm,
        bool affectsFit,
        bool affectsFunction,
        bool affectsSafety,
        bool affectsCustomers,
        bool affectsRegulatory,
        int? linkedItemId,
        int? linkedDocumentId,
        int? linkedProductionOrderId,
        int? linkedCustomerId,
        string requestedBy,
        CancellationToken ct);

    /// <summary>
    /// Submit a Draft ECR for review. Flips Status Draft → Submitted +
    /// stamps SubmittedAtUtc.
    /// </summary>
    Task<Result<EngineeringChangeRequest>> SubmitEcrAsync(
        int ecrId,
        string submittedBy,
        CancellationToken ct);

    /// <summary>
    /// Approve an ECR and atomically create the resulting ECO in Draft
    /// status. Stamps ECR.Status=Approved + DecidedAtUtc + DecidedBy +
    /// ResultingEcoId. ECO inherits Urgency from ECR, copies the F/F/F
    /// flags into RequiresFaiRetrigger, Customer flag into
    /// RequiresCustomerNotice, Regulatory flag into RequiresRegulatoryNotice.
    /// Single SaveChangesAsync for atomicity (PR-FS-6 lesson).
    /// </summary>
    Task<Result<EcrApprovalResult>> ApproveEcrAndCreateEcoAsync(
        int ecrId,
        string ecoNumber,
        string ecoTitle,
        EcoEffectivityType effectivityType,
        DateTime? effectiveFromUtc,
        string? effectivitySerialFrom,
        string? effectivitySerialTo,
        string? effectivityLotFrom,
        string? effectivityLotTo,
        int? effectivityProductionOrderId,
        string approvedBy,
        CancellationToken ct);

    /// <summary>
    /// Reject a Submitted or UnderReview ECR. Stamps Status=Rejected +
    /// DecidedAtUtc + DecidedBy + RejectionReason.
    /// </summary>
    Task<Result<EngineeringChangeRequest>> RejectEcrAsync(
        int ecrId,
        string rejectionReason,
        string rejectedBy,
        CancellationToken ct);

    // ===== ECO operations ================================================

    /// <summary>
    /// Add an EcoLineItem (an affected Item / Document / DocumentVersion).
    /// Cross-tenant validated. UNIQUE on (EcoId, Sequence) auto-advanced
    /// when Sequence is left at 0.
    /// </summary>
    Task<Result<EcoLineItem>> AddEcoLineItemAsync(
        int ecoId,
        int? affectedItemId,
        int? affectedDocumentId,
        int? affectedDocumentVersionId,
        int? newDocumentVersionId,
        string? changeDescription,
        string? beforeValue,
        string? afterValue,
        EcoLineItemDisposition disposition,
        string? notes,
        string createdBy,
        CancellationToken ct);

    /// <summary>
    /// Add an approval stage to an ECO. UNIQUE on (EcoId, StageOrder) —
    /// idempotent: if a stage at that order already exists, returns it.
    /// Only allowed while ECO is in Draft or InApproval status.
    /// </summary>
    Task<Result<EcoApproval>> AddEcoApprovalStageAsync(
        int ecoId,
        int stageOrder,
        string approvalRole,
        string? requiredApprover,
        string createdBy,
        CancellationToken ct);

    /// <summary>
    /// Approve an ECO approval stage. Enforces in-order approval — fails
    /// if any earlier-StageOrder stage is still Pending or Rejected.
    /// When ALL non-Skipped/NotRequired stages reach Approved, ECO
    /// automatically flips Status from InApproval → Approved + stamps.
    /// Single SaveChangesAsync.
    /// </summary>
    Task<Result<EcoApproval>> ApproveEcoStageAsync(
        int approvalId,
        string approvedBy,
        string? decisionNotes,
        CancellationToken ct);

    /// <summary>
    /// Release an Approved ECO. Stamps ReleasedAtUtc + ReleasedBy +
    /// EffectiveFromUtc. For each line item with NewDocumentVersionId
    /// set, delegates to IDocumentService.ReleaseVersionAsync which
    /// atomically supersedes the prior Released version. Single
    /// SaveChangesAsync wraps the whole release (PR-FS-6 atomic lesson).
    /// </summary>
    Task<Result<EngineeringChangeOrder>> ReleaseEcoAsync(
        int ecoId,
        string releasedBy,
        DateTime? effectiveFromUtc,
        CancellationToken ct);

    /// <summary>
    /// Mark a Released ECO as Implemented (change has propagated to
    /// production floor).
    /// </summary>
    Task<Result<EngineeringChangeOrder>> ImplementEcoAsync(
        int ecoId,
        string implementedBy,
        CancellationToken ct);

    /// <summary>
    /// Close-loop an Implemented ECO (FAI re-baseline complete if
    /// RequiresFaiRetrigger=true).
    /// </summary>
    Task<Result<EngineeringChangeOrder>> CloseEcoAsync(
        int ecoId,
        string closedBy,
        CancellationToken ct);

    // ===== Read operations ===============================================

    /// <summary>Get an ECR with its linked ECO (if any).</summary>
    Task<EngineeringChangeRequest?> GetEcrAsync(int ecrId, CancellationToken ct);

    /// <summary>Get an ECO with its line items + approval stages loaded.</summary>
    Task<EngineeringChangeOrder?> GetEcoAsync(int ecoId, CancellationToken ct);
}

/// <summary>
/// Result envelope for ApproveEcrAndCreateEcoAsync — contains both the
/// approved ECR and the newly-created ECO.
/// </summary>
public sealed record EcrApprovalResult(
    EngineeringChangeRequest Ecr,
    EngineeringChangeOrder Eco);
