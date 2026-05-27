using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;

namespace Abs.FixedAssets.Services.Engineering
{
    public interface IWaiverService
    {
        Task<Result<Waiver>> CreateAsync(CreateWaiverRequest request, CancellationToken ct = default);
        Task<Result<Waiver>> SubmitAsync(int waiverId, string submittedBy, CancellationToken ct = default);
        Task<Result<Waiver>> ApproveAsync(int waiverId, string approvedBy, CancellationToken ct = default);
        Task<Result<Waiver>> RejectAsync(int waiverId, string rejectedBy, string reason, CancellationToken ct = default);
        Task<Result<Waiver>> ActivateAsync(int waiverId, CancellationToken ct = default);
        Task<Result<Waiver>> RevokeAsync(int waiverId, string revokedBy, string reason, CancellationToken ct = default);
        Task<Result<Waiver>> RecordConsumptionAsync(int waiverId, decimal quantity, CancellationToken ct = default);
        Task<Waiver?> GetAsync(int waiverId, CancellationToken ct = default);
    }

    public record CreateWaiverRequest(
        int CompanyId, string WaiverNumber, string Title, WaiverType Type,
        int? ItemId = null, int? CustomerId = null, int? ProductionOrderId = null,
        int? OriginatingEcrId = null, int? RelatedDeviationId = null,
        string? CustomerContractReference = null, decimal? MaxQuantity = null,
        DateTime? EffectiveFromUtc = null, DateTime? ExpirationDateUtc = null,
        string? OriginalSpecification = null, string? WaivedCondition = null,
        string? Justification = null, string? Disposition = null,
        string? Description = null, string? RequestedBy = null);
}
