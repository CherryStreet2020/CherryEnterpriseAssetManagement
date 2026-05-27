// Sprint 14.3 PR-2 (2026-05-27) — Deviation service interface.
//
// Short-term engineering exceptions. Lifecycle:
//   Draft → Submitted → UnderReview → Approved → Active → Expired/Closed
// Concurrency-safe via xmin RowVersion + DbUpdateConcurrencyException retry.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering
{
    public interface IDeviationService
    {
        /// <summary>Create a new deviation in Draft status.</summary>
        Task<Result<Deviation>> CreateAsync(CreateDeviationRequest request, CancellationToken ct = default);

        /// <summary>Submit a Draft deviation for review.</summary>
        Task<Result<Deviation>> SubmitAsync(int deviationId, string submittedBy, CancellationToken ct = default);

        /// <summary>Approve a deviation (moves to Approved, then auto-activates if effective date is now or past).</summary>
        Task<Result<Deviation>> ApproveAsync(int deviationId, string approvedBy, CancellationToken ct = default);

        /// <summary>Reject a deviation with reason.</summary>
        Task<Result<Deviation>> RejectAsync(int deviationId, string rejectedBy, string reason, CancellationToken ct = default);

        /// <summary>Activate an Approved deviation (makes it usable by production).</summary>
        Task<Result<Deviation>> ActivateAsync(int deviationId, CancellationToken ct = default);

        /// <summary>Close an Active deviation before natural expiry.</summary>
        Task<Result<Deviation>> CloseAsync(int deviationId, string closedBy, CancellationToken ct = default);

        /// <summary>Record consumption against an Active deviation.</summary>
        Task<Result<Deviation>> RecordConsumptionAsync(int deviationId, decimal quantity, CancellationToken ct = default);

        /// <summary>Get a deviation by ID.</summary>
        Task<Deviation?> GetAsync(int deviationId, CancellationToken ct = default);

        /// <summary>List deviations for an Item.</summary>
        Task<IReadOnlyList<Deviation>> GetForItemAsync(int itemId, bool includeExpired = false, CancellationToken ct = default);
    }

    public record CreateDeviationRequest(
        int CompanyId,
        string DeviationNumber,
        string Title,
        DeviationType Type,
        int? ItemId = null,
        int? ProductionOrderId = null,
        int? OriginatingEcrId = null,
        decimal? MaxQuantity = null,
        DateTime? EffectiveFromUtc = null,
        DateTime? ExpirationDateUtc = null,
        int? MaxProductionOrders = null,
        bool AffectsForm = false,
        bool AffectsFit = false,
        bool AffectsFunction = false,
        bool SafetyImpact = false,
        bool CustomerApprovalRequired = false,
        string? OriginalSpecification = null,
        string? DeviatedCondition = null,
        string? Justification = null,
        string? Disposition = null,
        string? Description = null,
        string? RequestedBy = null);
}
