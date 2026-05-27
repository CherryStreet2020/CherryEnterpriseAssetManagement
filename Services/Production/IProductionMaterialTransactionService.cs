// B8 PR-PRO-3 (2026-05-27) — Production material transaction service interface.
//
// 12 actions against frozen BOM lines (ProductionMaterialStructure).
// Each action creates a ProductionMaterialTransaction record AND
// updates the BOM line's execution quantities (IssuedQuantity,
// ConsumedQuantity, etc.) in a single SaveChangesAsync call.
//
// 6 ENFORCED JOB-TO-JOB TRANSFER RULES:
//   1. Cannot transfer material already consumed unless reversing consumption first.
//   2. Cannot transfer lot/serial to a job whose part/revision/spec does not allow it.
//   3. Cannot transfer customer-owned / consigned material to a different customer job without approval.
//   4. Cannot transfer material out of a job if it creates a critical shortage unless supervisor override.
//   5. Must preserve cost and genealogy.
//   6. Audit user + timestamp + reverse action.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    public interface IProductionMaterialTransactionService
    {
        /// <summary>Issue material from inventory to a BOM line.</summary>
        Task<Result<ProductionMaterialTransaction>> IssueAsync(IssueMaterialRequest request, CancellationToken ct = default);

        /// <summary>Issue all remaining required quantity for a BOM line.</summary>
        Task<Result<ProductionMaterialTransaction>> IssueAllAsync(int bomLineId, string performedBy,
            string? lotNumber = null, string? fromWarehouse = null, string? fromBin = null,
            CancellationToken ct = default);

        /// <summary>Issue all BOM lines in a kit group.</summary>
        Task<Result<IReadOnlyList<ProductionMaterialTransaction>>> IssueKitAsync(int productionOrderId,
            string kitGroup, string performedBy, string? fromWarehouse = null,
            CancellationToken ct = default);

        /// <summary>Issue less than the remaining required quantity.</summary>
        Task<Result<ProductionMaterialTransaction>> PartialIssueAsync(IssueMaterialRequest request, CancellationToken ct = default);

        /// <summary>Issue more than the required quantity (reason required).</summary>
        Task<Result<ProductionMaterialTransaction>> OverIssueAsync(IssueMaterialRequest request,
            string reasonCode, string reasonDescription, CancellationToken ct = default);

        /// <summary>Return previously issued material back to inventory.</summary>
        Task<Result<ProductionMaterialTransaction>> ReturnAsync(ReturnMaterialRequest request, CancellationToken ct = default);

        /// <summary>Reverse a prior issue (creates a paired reversal record).</summary>
        Task<Result<ProductionMaterialTransaction>> ReverseIssueAsync(int originalTransactionId,
            string performedBy, string? reason = null, CancellationToken ct = default);

        /// <summary>Transfer material from this job to another job. Enforces 6 transfer rules.</summary>
        Task<Result<ProductionMaterialTransaction>> TransferToJobAsync(TransferMaterialRequest request, CancellationToken ct = default);

        /// <summary>Record material transferred IN from another job (paired with TransferToJob).</summary>
        Task<Result<ProductionMaterialTransaction>> TransferFromJobAsync(TransferMaterialRequest request, CancellationToken ct = default);

        /// <summary>Substitute one component for another on a BOM line.</summary>
        Task<Result<ProductionMaterialTransaction>> SubstituteAsync(SubstituteMaterialRequest request, CancellationToken ct = default);

        /// <summary>Split a BOM line requirement into multiple lots.</summary>
        Task<Result<ProductionMaterialTransaction>> SplitAsync(int bomLineId, decimal splitQuantity,
            string? newLotNumber, string performedBy, CancellationToken ct = default);

        /// <summary>Scrap issued material (removes from usable inventory).</summary>
        Task<Result<ProductionMaterialTransaction>> ScrapComponentAsync(ScrapMaterialRequest request, CancellationToken ct = default);

        /// <summary>Get a transaction by ID with navigation properties.</summary>
        Task<ProductionMaterialTransaction?> GetAsync(int transactionId, CancellationToken ct = default);

        /// <summary>List all transactions for a BOM line.</summary>
        Task<IReadOnlyList<ProductionMaterialTransaction>> GetForBomLineAsync(int bomLineId, CancellationToken ct = default);

        /// <summary>List all transactions for a production order.</summary>
        Task<IReadOnlyList<ProductionMaterialTransaction>> GetForProductionOrderAsync(int productionOrderId, CancellationToken ct = default);
    }

    // ===== Request records ================================================

    public record IssueMaterialRequest(
        int BomLineId,
        decimal Quantity,
        string PerformedBy,
        string? LotNumber = null,
        string? SerialNumber = null,
        string? HeatNumber = null,
        string? VendorLot = null,
        string? CertificateNumber = null,
        string? FromWarehouse = null,
        string? FromBin = null,
        decimal? ActualUnitCost = null,
        int? OperationSequence = null,
        string? Notes = null);

    public record ReturnMaterialRequest(
        int BomLineId,
        decimal Quantity,
        string PerformedBy,
        string? LotNumber = null,
        string? SerialNumber = null,
        string? ToWarehouse = null,
        string? ToBin = null,
        string? ReasonCode = null,
        string? ReasonDescription = null,
        string? Notes = null);

    public record TransferMaterialRequest(
        int SourceBomLineId,
        int DestinationProductionOrderId,
        int DestinationBomLineId,
        decimal Quantity,
        string PerformedBy,
        string TransferReason,
        string? LotNumber = null,
        string? SerialNumber = null,
        bool SupervisorOverride = false,
        string? SupervisorOverrideBy = null,
        bool TransferApprovalRequired = false,
        string? TransferApprovedBy = null,
        string? Notes = null);

    public record SubstituteMaterialRequest(
        int BomLineId,
        int SubstituteItemId,
        decimal Quantity,
        string PerformedBy,
        string SubstitutionReason,
        string? SubstitutionAuthReference = null,
        bool CustomerApproved = false,
        string? LotNumber = null,
        string? SerialNumber = null,
        decimal? ActualUnitCost = null,
        string? Notes = null);

    public record ScrapMaterialRequest(
        int BomLineId,
        decimal Quantity,
        string PerformedBy,
        string ReasonCode,
        string ReasonDescription,
        string? LotNumber = null,
        string? SerialNumber = null,
        string? Notes = null);
}
