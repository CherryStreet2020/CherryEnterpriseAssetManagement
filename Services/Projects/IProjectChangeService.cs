// Theme B9 Wave 6 PR-15 (2026-05-30) — IProjectChangeService. OPENS Wave 6.
//
// Change control: the disciplined request → impact analysis → approval →
// change-order path. The change ORDER is the existing ProjectAmendment; this
// service owns the upstream ProjectChangeRequest and the conversion.
//
// The §20 gate (research §11 — "a full customer project module without change
// orders will fail"): you cannot APPLY a customer scope change — i.e. convert a
// change request into a ProjectAmendment (change order) that moves the
// effective contract value — before its required approval(s) clear.
//
// ADR-025: PageModels + voice read THROUGH this service, never AppDbContext.
// Tenant scope flows through the parent CustomerProject; every incoming FK on a
// write is scoped to the project.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectChangeService
{
    /// <summary>Change requests + change-order rollup + effective-contract math (read).</summary>
    Task<Result<ProjectChangeView>> GetChangesAsync(int projectId, CancellationToken ct = default);

    /// <summary>Create a change request in Draft; ChangeRequestNumber = MAX+1 per project.</summary>
    Task<Result<int>> CreateChangeRequestAsync(CreateChangeRequestRequest req, CancellationToken ct = default);

    /// <summary>
    /// Record / update the impact analysis (the "Estimate change" action). Allowed
    /// while Draft / UnderReview / Estimated; auto-advances Draft/UnderReview →
    /// Estimated. Money is interpreted in the project currency.
    /// </summary>
    Task<Result<ProjectChangeRequest>> UpdateImpactAsync(UpdateChangeImpactRequest req, CancellationToken ct = default);

    /// <summary>
    /// Move the change request through its disposition workflow per the legal-map
    /// (Draft→UnderReview→Estimated→[InternalApproved]→[SubmittedToCustomer]→
    /// CustomerApproved; any non-terminal → Rejected/Cancelled). Stamps the
    /// set-once approval / submission / rejection fields. Terminal states reject
    /// re-entry.
    /// </summary>
    Task<Result<ProjectChangeRequest>> TransitionAsync(TransitionChangeRequest req, CancellationToken ct = default);

    /// <summary>
    /// Convert an approved change request into a ProjectAmendment (the change
    /// order) and cross-link the two. THE §20 GATE: rejects conversion until the
    /// request's required approval(s) clear, and rejects double-conversion.
    /// </summary>
    Task<Result<ConvertToChangeOrderResult>> ConvertToChangeOrderAsync(ConvertToChangeOrderRequest req, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectChangeView(
    int ProjectId,
    string Currency,
    decimal BaselineContractValue,
    decimal ApprovedChangeValue,     // SUM of Approved amendment ValueDeltas
    decimal EffectiveContractValue,  // baseline + approved change value
    decimal PendingRevenueExposure,  // SUM RevenueImpact of in-flight requests
    int OpenChangeRequestCount,      // non-terminal requests
    int ChangeOrderCount,            // ProjectAmendments on the project
    IReadOnlyList<ChangeRequestRow> Requests);

public sealed record ChangeRequestRow(
    int Id,
    int Number,
    string? Title,
    ProjectChangeSource Source,
    ProjectChangeCategory Category,
    ProjectChangeRequestStatus Status,
    string? RequestedByName,
    DateTime RequestDate,
    decimal CostImpact,
    decimal RevenueImpact,
    decimal? MarginImpactPct,
    int? ScheduleImpactDays,
    ProjectChangeRiskLevel RiskImpact,
    string Currency,
    int? AffectedPhaseId,
    bool RequiresInternalApproval,
    bool RequiresCustomerApproval,
    bool InternalApproved,
    bool CustomerApproved,
    long? ResultingProjectAmendmentId,
    bool IsConverted,
    bool CanConvert);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateChangeRequestRequest(
    int CustomerProjectId,
    string? Title = null,
    ProjectChangeSource Source = ProjectChangeSource.Customer,
    ProjectChangeCategory Category = ProjectChangeCategory.CustomerScope,
    string? RequestedByName = null,
    string? Description = null,
    DateTime? RequestDate = null,
    ProjectChangeRiskLevel RiskImpact = ProjectChangeRiskLevel.None,
    bool RequiresInternalApproval = true,
    bool RequiresCustomerApproval = true,
    int? AffectedPhaseId = null,
    string? CustomerReference = null,
    string? CustomerPoRevision = null,
    string? Currency = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record UpdateChangeImpactRequest(
    int ProjectChangeRequestId,
    decimal CostImpact,
    decimal RevenueImpact,
    decimal? MarginImpactPct = null,
    int? ScheduleImpactDays = null,
    ProjectChangeRiskLevel RiskImpact = ProjectChangeRiskLevel.None,
    string? ImpactNarrative = null,
    ProjectChangeBillingTreatment BillingTreatment = ProjectChangeBillingTreatment.None,
    ProjectChangeCostTreatment CostTreatment = ProjectChangeCostTreatment.None,
    string? ModifiedBy = null);

public sealed record TransitionChangeRequest(
    int ProjectChangeRequestId,
    ProjectChangeRequestStatus NewStatus,
    string? ActorName = null,
    string? ModifiedBy = null);

public sealed record ConvertToChangeOrderRequest(
    int ProjectChangeRequestId,
    DateTime EffectiveDate,
    ProjectAmendmentChangeType ChangeType = ProjectAmendmentChangeType.Combined,
    bool ApproveImmediately = false,
    string? ApprovedByName = null,
    string? CustomerReference = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record ConvertToChangeOrderResult(
    int ProjectChangeRequestId,
    long ProjectAmendmentId,
    int AmendmentNumber,
    ProjectAmendmentStatus AmendmentStatus,
    decimal ValueDelta);
