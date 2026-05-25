using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.Quality;

namespace Abs.FixedAssets.Services.Quality
{
    // ================================================================
    // Sprint 13.5 PR #338 — IFaiService.
    //
    // AS9102 First Article Inspection workflow contract. Wraps every
    // mutation to FaiReport / FaiCharacteristic / FaiProductAccountability
    // so PageModels stay CHERRY025-clean.
    //
    // Spec: docs/research/fai-ui-pr338-spec-2026-05-25.md
    // ================================================================

    public interface IFaiService
    {
        Task<IReadOnlyList<FaiReport>> ListAsync(int companyId, int? customerProjectId, CancellationToken ct);

        Task<FaiReport?> GetByIdAsync(long id, CancellationToken ct);

        Task<IReadOnlyList<FaiCharacteristic>> GetCharacteristicsAsync(long faiReportId, CancellationToken ct);

        Task<IReadOnlyList<FaiProductAccountability>> GetProductAccountabilityAsync(long faiReportId, CancellationToken ct);

        Task<FaiReport> CreateAsync(FaiCreateRequest req, int userId, string? username, CancellationToken ct);

        Task<FaiCharacteristic> RecordCharacteristicAsync(long faiReportId, FaiCharacteristic row, int userId, string? username, CancellationToken ct);

        Task<FaiReport> SubmitAsync(long faiReportId, int submitterUserId, string? submitterName, CancellationToken ct);

        Task<FaiReport> SignOffAsync(long faiReportId, int approverUserId, string? approverName, CancellationToken ct);
    }

    public sealed record FaiCreateRequest(
        int CompanyId,
        int? TenantId,
        int ItemId,
        int? CustomerProjectId,
        int? CustomerId,
        string PartNumberSnapshot,
        string? PartNameSnapshot,
        string? DrawingNumberSnapshot,
        string? DrawingRevSnapshot,
        FaiType Type,
        FaiPartType PartType,
        FaiReason Reason,
        string? ReasonText);
}
