// Sprint 14.3 PR-4 (2026-05-27) — Concession service interface.
//
// RETROACTIVE customer acceptance of already-produced non-conforming material.
// Lifecycle: Draft → Submitted → CustomerReview → Accepted → Closed
//                                                ↘ Rejected → (MRB/Scrap/Rework)
// Concurrency-safe via xmin RowVersion + DbUpdateConcurrencyException retry.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering
{
    public interface IConcessionService
    {
        /// <summary>Create a new concession in Draft status.</summary>
        Task<Result<Concession>> CreateAsync(CreateConcessionRequest request, CancellationToken ct = default);

        /// <summary>Submit a Draft concession for customer review.</summary>
        Task<Result<Concession>> SubmitAsync(int concessionId, string submittedBy, CancellationToken ct = default);

        /// <summary>Accept a concession (customer accepts non-conforming material).</summary>
        Task<Result<Concession>> AcceptAsync(int concessionId, string acceptedBy, CancellationToken ct = default);

        /// <summary>Reject a concession with disposition and reason.</summary>
        Task<Result<Concession>> RejectAsync(int concessionId, string rejectedBy, RejectedDisposition disposition, string reason, CancellationToken ct = default);

        /// <summary>Close an Accepted concession (post-acceptance administrative close).</summary>
        Task<Result<Concession>> CloseAsync(int concessionId, string closedBy, CancellationToken ct = default);

        /// <summary>Get a concession by ID.</summary>
        Task<Concession?> GetAsync(int concessionId, CancellationToken ct = default);
    }

    public record CreateConcessionRequest(
        int CompanyId,
        string ConcessionNumber,
        string Title,
        ConcessionType Type,
        int? ItemId = null,
        int? ProductionOrderId = null,
        decimal AffectedQuantity = 0m,
        string? AffectedLotSerials = null,
        int? CustomerId = null,
        string? OriginalSpecification = null,
        string? ActualCondition = null,
        string? Justification = null,
        string? Disposition = null,
        string? Description = null,
        string? RequestedBy = null,
        string? NcrReference = null);
}
