// Theme B9 Wave 6 PR-17 (2026-05-31) — IProjectQualityService.
//
// Quality + acceptance: inspections, NCRs, MRBs, punch items, and customer
// acceptance (research §14). Hosts the §22.4 gate — a customer acceptance
// cannot be confirmed while there is an open blocking NCR, a pending MRB, or an
// open blocking-acceptance punch item. A confirmed RevenueTrigger acceptance
// flips the PR-14 billing AcceptanceConfirmed gate (the real entity the #468
// placeholder flag stood in for).
//
// ADR-025: read THROUGH this service. Tenant scope flows through the project;
// every incoming FK on a write is scoped to the project.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectQualityService
{
    Task<Result<ProjectQualityView>> GetQualityAsync(int projectId, CancellationToken ct = default);

    // Inspections
    Task<Result<int>> CreateInspectionAsync(CreateInspectionRequest req, CancellationToken ct = default);
    Task<Result<ProjectInspection>> CompleteInspectionAsync(CompleteInspectionRequest req, CancellationToken ct = default);

    // NCRs
    Task<Result<int>> CreateNcrAsync(CreateNcrRequest req, CancellationToken ct = default);
    Task<Result<ProjectNCR>> DispositionNcrAsync(DispositionNcrRequest req, CancellationToken ct = default);
    Task<Result<ProjectNCR>> TransitionNcrAsync(int ncrId, ProjectNcrStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default);

    // MRBs
    Task<Result<int>> CreateMrbAsync(CreateMrbRequest req, CancellationToken ct = default);
    Task<Result<ProjectMRB>> DispositionMrbAsync(DispositionMrbRequest req, CancellationToken ct = default);
    Task<Result<ProjectMRB>> TransitionMrbAsync(int mrbId, ProjectMrbStatus newStatus, string? actor = null, string? modifiedBy = null, CancellationToken ct = default);

    // Punch items
    Task<Result<int>> CreatePunchItemAsync(CreatePunchItemRequest req, CancellationToken ct = default);
    Task<Result<ProjectPunchItem>> TransitionPunchItemAsync(int punchId, ProjectPunchStatus newStatus, string? actor = null, string? completionEvidence = null, string? modifiedBy = null, CancellationToken ct = default);

    // Acceptance
    Task<Result<int>> CreateAcceptanceAsync(CreateAcceptanceRequest req, CancellationToken ct = default);
    /// <summary>
    /// THE §22.4 GATE: confirm a customer acceptance. Rejected while any open
    /// blocking NCR, pending MRB, or open blocking-acceptance punch item exists.
    /// On success: Accepted + AcceptedAt; a RevenueTrigger acceptance flips the
    /// PR-14 billing AcceptanceConfirmed on the project's acceptance-gated lines.
    /// </summary>
    Task<Result<ConfirmAcceptanceResult>> ConfirmAcceptanceAsync(ConfirmAcceptanceRequest req, CancellationToken ct = default);
    Task<Result<ProjectAcceptance>> RejectAcceptanceAsync(int acceptanceId, string? actor = null, string? modifiedBy = null, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectQualityView(
    int ProjectId,
    int OpenNcrCount,
    int BlockingNcrCount,
    int PendingMrbCount,
    int OpenPunchCount,
    int BlockingAcceptancePunchCount,
    int OpenInspectionCount,
    bool ShipReady,                 // no blocking NCR / pending MRB / blocking-shipment punch
    bool AcceptanceReady,           // no blocking NCR / pending MRB / blocking-acceptance punch
    IReadOnlyList<string> AcceptanceBlockers,
    IReadOnlyList<InspectionRow> Inspections,
    IReadOnlyList<NcrRow> Ncrs,
    IReadOnlyList<MrbRow> Mrbs,
    IReadOnlyList<PunchRow> PunchItems,
    IReadOnlyList<AcceptanceRow> Acceptances);

public sealed record InspectionRow(int Id, int Number, string? Title, ProjectInspectionType Type,
    ProjectInspectionResult Result, DateTime? InspectionDate, int QuantityInspected, int QuantityAccepted,
    int QuantityRejected, int? AffectedPhaseId);

public sealed record NcrRow(int Id, int Number, string? Title, ProjectNcrSource Source, ProjectNcrSeverity Severity,
    ProjectQualityDisposition Disposition, ProjectNcrStatus Status, bool BlocksShipment, int QuantityAffected,
    int? AffectedPhaseId, bool IsOpen);

public sealed record MrbRow(int Id, int Number, string? Title, int? LinkedNcrId, ProjectQualityDisposition Disposition,
    ProjectMrbStatus Status, bool CustomerApprovalRequired, bool CustomerApproved, bool IsPending);

public sealed record PunchRow(int Id, int Number, string? Title, ProjectPriority Priority, string? Owner,
    DateTime? DueDate, ProjectPunchStatus Status, bool CustomerVisible, bool BlockingShipment, bool BlockingInvoice,
    bool BlockingAcceptance, bool IsOpen);

public sealed record AcceptanceRow(int Id, int Number, ProjectAcceptanceType Type, ProjectAcceptanceStatus Status,
    string? CustomerContact, int AcceptedQuantity, int RejectedQuantity, DateTime? AcceptanceDate,
    bool RevenueTrigger, bool WarrantyTrigger);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateInspectionRequest(int CustomerProjectId, string? Title = null,
    ProjectInspectionType InspectionType = ProjectInspectionType.InProcess, DateTime? InspectionDate = null,
    string? Inspector = null, int? AffectedPhaseId = null, string? ReportReference = null,
    string? Notes = null, string? CreatedBy = null);

public sealed record CompleteInspectionRequest(int ProjectInspectionId, ProjectInspectionResult Result,
    int QuantityInspected = 0, int QuantityAccepted = 0, int QuantityRejected = 0,
    string? CompletedBy = null, string? Notes = null);

public sealed record CreateNcrRequest(int CustomerProjectId, string? Title = null, string? Description = null,
    ProjectNcrSource Source = ProjectNcrSource.Internal, ProjectNcrSeverity Severity = ProjectNcrSeverity.Minor,
    DateTime? DetectedDate = null, int QuantityAffected = 0, bool BlocksShipment = true,
    int? AffectedPhaseId = null, int? LinkedInspectionId = null, string? ContainmentAction = null,
    string? Notes = null, string? CreatedBy = null);

public sealed record DispositionNcrRequest(int ProjectNcrId, ProjectQualityDisposition Disposition,
    string? RootCause = null, string? CorrectiveAction = null, string? ModifiedBy = null);

public sealed record CreateMrbRequest(int CustomerProjectId, string? Title = null, int? LinkedNcrId = null,
    string? BoardMembers = null, DateTime? ReviewDate = null, bool CustomerApprovalRequired = false,
    int? AffectedPhaseId = null, string? Notes = null, string? CreatedBy = null);

public sealed record DispositionMrbRequest(int ProjectMrbId, ProjectQualityDisposition Disposition,
    string? Justification = null, bool? CustomerApproved = null, string? ModifiedBy = null);

public sealed record CreatePunchItemRequest(int CustomerProjectId, string? Title = null, string? Description = null,
    string? Source = null, ProjectPriority Priority = ProjectPriority.Low, string? Owner = null,
    DateTime? DueDate = null, bool CustomerVisible = false, bool BlockingShipment = false,
    bool BlockingInvoice = false, bool BlockingAcceptance = false, int? AffectedPhaseId = null,
    string? CorrectiveAction = null, string? Notes = null, string? CreatedBy = null);

public sealed record CreateAcceptanceRequest(int CustomerProjectId,
    ProjectAcceptanceType AcceptanceType = ProjectAcceptanceType.Customer, string? CustomerContact = null,
    string? RequiredCriteria = null, string? RequiredDocuments = null, bool RevenueTrigger = true,
    bool WarrantyTrigger = false, int? AffectedPhaseId = null, string? Notes = null, string? CreatedBy = null);

public sealed record ConfirmAcceptanceRequest(int ProjectAcceptanceId, string? AcceptedBy = null,
    string? Signature = null, string? InspectionResult = null, int AcceptedQuantity = 0, int RejectedQuantity = 0,
    DateTime? AcceptanceDate = null, string? ModifiedBy = null);

public sealed record ConfirmAcceptanceResult(int ProjectAcceptanceId, ProjectAcceptanceStatus Status,
    bool RevenueTriggered, int BillingLinesConfirmed);
