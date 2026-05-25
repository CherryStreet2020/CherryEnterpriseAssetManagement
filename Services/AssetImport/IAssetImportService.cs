using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models.AssetImport;

namespace Abs.FixedAssets.Services.AssetImport
{
    // ================================================================
    // Sprint 13.5 PR #337 — Asset bulk-import service contract.
    //
    // PageModels go through this service for ALL read/write — keeps the
    // CHERRY025 control-plane boundary clean (no new AppDbContext
    // injection allowlist entries needed).
    //
    // Spec: docs/research/asset-import-pr337-spec-2026-05-25.md
    // ================================================================

    public interface IAssetImportService
    {
        // -------- Lifecycle (mutations) --------

        Task<AssetImportBatch> ParseAndStageAsync(
            Stream excelStream,
            string fileName,
            long fileSizeBytes,
            int companyId,
            int? organizationId,
            int? siteId,
            int userId,
            string? username,
            CancellationToken ct);

        Task<AssetImportBatch> ValidateRowsAsync(int batchId, CancellationToken ct);

        Task<AssetImportBatch> CommitBatchAsync(int batchId, int userId, string? username, CancellationToken ct);

        Task<AssetImportBatch> DiscardBatchAsync(int batchId, int userId, string? username, CancellationToken ct);

        // -------- Reads (for PageModels) --------

        Task<IReadOnlyList<AssetImportBatch>> ListRecentAsync(int companyId, int limit, CancellationToken ct);

        Task<AssetImportBatch?> GetBatchAsync(int batchId, CancellationToken ct);

        Task<IReadOnlyList<AssetImportRow>> GetRowsAsync(int batchId, CancellationToken ct);

        Task<AssetImportKpis> GetKpisAsync(int companyId, CancellationToken ct);

        // -------- Template generation --------

        // Returns an in-memory .xlsx with one header row and one sample data row.
        byte[] GenerateTemplate();
    }

    public sealed record AssetImportKpis(
        int TotalBatches,
        int CommittedBatches,
        int DraftBatches,
        int FailedOrDiscarded);
}
