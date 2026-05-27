// Sprint 14.3 PR-6 (2026-05-27) — Corrective Action service interface.
//
// Full 8D-style lifecycle for CAR/CAPA:
//   Draft → Issued → UnderInvestigation → RootCauseIdentified
//   → CorrectiveActionPlanned → ImplementationInProgress
//   → VerificationPending → Closed

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering
{
    public interface ICorrectiveActionService
    {
        /// <summary>Create a new CAR in Draft status.</summary>
        Task<Result<CorrectiveActionRequest>> CreateAsync(CreateCarRequest request, CancellationToken ct = default);

        /// <summary>Formally issue the CAR (Draft → Issued).</summary>
        Task<Result<CorrectiveActionRequest>> IssueAsync(int carId, string issuedBy, string? assignedTo = null,
            string? responsibleDepartment = null, CancellationToken ct = default);

        /// <summary>Begin investigation (Issued → UnderInvestigation).</summary>
        Task<Result<CorrectiveActionRequest>> BeginInvestigationAsync(int carId, string investigator,
            string? containmentAction = null, CancellationToken ct = default);

        /// <summary>Record root cause (UnderInvestigation → RootCauseIdentified).</summary>
        Task<Result<CorrectiveActionRequest>> RecordRootCauseAsync(int carId, string rootCauseAnalysis,
            string methodology, string identifiedBy, CancellationToken ct = default);

        /// <summary>Plan corrective + preventive actions (RootCauseIdentified → CorrectiveActionPlanned).</summary>
        Task<Result<CorrectiveActionRequest>> PlanCorrectiveActionAsync(int carId,
            string correctiveActionPlan, string? preventiveActionPlan = null,
            DateTime? dueDate = null, CancellationToken ct = default);

        /// <summary>Begin implementation (CorrectiveActionPlanned → ImplementationInProgress).</summary>
        Task<Result<CorrectiveActionRequest>> BeginImplementationAsync(int carId, string implementedBy,
            CancellationToken ct = default);

        /// <summary>Complete implementation (ImplementationInProgress → VerificationPending).</summary>
        Task<Result<CorrectiveActionRequest>> CompleteImplementationAsync(int carId,
            string implementationNotes, string implementedBy, CancellationToken ct = default);

        /// <summary>Record verification results and close (VerificationPending → Closed).</summary>
        Task<Result<CorrectiveActionRequest>> VerifyAndCloseAsync(int carId, string verificationMethod,
            string verificationResults, bool effective, string verifiedBy,
            CancellationToken ct = default);

        /// <summary>Cancel a CAR (any non-terminal status → Cancelled).</summary>
        Task<Result<CorrectiveActionRequest>> CancelAsync(int carId, string cancelledBy,
            string reason, CancellationToken ct = default);

        /// <summary>Get a CAR by ID with navigation properties.</summary>
        Task<CorrectiveActionRequest?> GetAsync(int carId, CancellationToken ct = default);

        /// <summary>List CARs for an Item (optionally including closed).</summary>
        Task<IReadOnlyList<CorrectiveActionRequest>> GetForItemAsync(int itemId,
            bool includeClosed = false, CancellationToken ct = default);
    }

    public record CreateCarRequest(
        int CompanyId,
        string CarNumber,
        string Title,
        CarSource Source,
        CarSeverity Severity = CarSeverity.Minor,
        int? ItemId = null,
        int? ProductionOrderId = null,
        int? CustomerId = null,
        int? VendorId = null,
        int? OriginatingEcrId = null,
        int? RelatedDeviationId = null,
        int? RelatedConcessionId = null,
        string? NcrReference = null,
        string? CustomerComplaintReference = null,
        string? AuditFindingReference = null,
        string? NonConformanceDescription = null,
        decimal? AffectedQuantity = null,
        string? AffectedLotSerials = null,
        bool AffectsForm = false,
        bool AffectsFit = false,
        bool AffectsFunction = false,
        bool SafetyImpact = false,
        bool RegulatoryImpact = false,
        string? Description = null,
        string? CreatedBy = null);
}
