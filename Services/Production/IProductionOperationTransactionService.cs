// B8 PR-PRO-4 (2026-05-27) — Production operation transaction service interface.
//
// 19 actions against ProductionOperation (the per-PO routing execution entity).
// Each action creates a ProductionOperationTransaction record AND
// updates the ProductionOperation status/quantities atomically.
// Absorbs B3 (mixed PO modes) + B5b (subcontract auto-complete on receipt).

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    public interface IProductionOperationTransactionService
    {
        // ===== State transitions ============================================

        Task<Result<ProductionOperationTransaction>> StartAsync(int operationId, string performedBy, int? assetId = null, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> PauseAsync(int operationId, string performedBy, string? reason = null, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> ResumeAsync(int operationId, string performedBy, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> StopAsync(int operationId, string performedBy, string? reason = null, CancellationToken ct = default);

        // ===== Setup / run phases ==========================================

        Task<Result<ProductionOperationTransaction>> StartSetupAsync(int operationId, string performedBy, int? assetId = null, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> CompleteSetupAsync(int operationId, string performedBy, decimal setupMinutes, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> StartRunAsync(int operationId, string performedBy, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> CompleteRunAsync(int operationId, string performedBy, decimal runMinutes, CancellationToken ct = default);

        // ===== Completion ==================================================

        Task<Result<ProductionOperationTransaction>> CompleteAsync(CompleteOperationRequest request, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> PartialCompleteAsync(CompleteOperationRequest request, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> FinalCompleteAsync(CompleteOperationRequest request, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> ReverseCompletionAsync(int operationId, string performedBy, string? reason = null, CancellationToken ct = default);

        // ===== Skip / add / rework ==========================================

        Task<Result<ProductionOperationTransaction>> SkipOperationAsync(int operationId, string performedBy, string reason, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> AddOperationAsync(AddOperationRequest request, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> InsertReworkOperationAsync(InsertReworkRequest request, CancellationToken ct = default);

        // ===== Resource / employee ==========================================

        Task<Result<ProductionOperationTransaction>> ChangeResourceAsync(int operationId, int newWorkCenterId, int? newAssetId, string performedBy, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> AddEmployeeAsync(int operationId, string employeeId, string performedBy, CancellationToken ct = default);

        // ===== Time =========================================================

        Task<Result<ProductionOperationTransaction>> LogTimeAsync(LogTimeRequest request, CancellationToken ct = default);
        Task<Result<ProductionOperationTransaction>> EditTimeAsync(int originalTransactionId, decimal newRunMinutes, decimal newSetupMinutes, string editedBy, string reason, CancellationToken ct = default);

        // ===== Read =========================================================

        Task<ProductionOperationTransaction?> GetAsync(int transactionId, CancellationToken ct = default);
        Task<IReadOnlyList<ProductionOperationTransaction>> GetForOperationAsync(int operationId, CancellationToken ct = default);
    }

    // ===== Request records ================================================

    public record CompleteOperationRequest(
        int OperationId,
        string PerformedBy,
        decimal GoodQuantity,
        decimal ScrapQuantity = 0,
        decimal ReworkQuantity = 0,
        decimal RejectQuantity = 0,
        bool BackflushMaterials = false,
        string? CompletedLotSerials = null,
        string? DestinationLocation = null,
        string? ScrapReasonCode = null,
        string? DefectCode = null,
        bool InspectionRequired = false,
        string? Notes = null);

    public record AddOperationRequest(
        int ProductionOrderId,
        int AfterOperationSequence,
        string Description,
        int WorkCenterId,
        decimal PlannedRunMins = 0,
        decimal PlannedSetupMins = 0,
        string PerformedBy = "system",
        string? Notes = null);

    public record InsertReworkRequest(
        int OperationId,
        string ReworkInstructions,
        int WorkCenterId,
        decimal PlannedRunMins = 0,
        string PerformedBy = "system",
        string? Notes = null);

    public record LogTimeRequest(
        int OperationId,
        string PerformedBy,
        decimal RunMinutes = 0,
        decimal SetupMinutes = 0,
        decimal MachineMinutes = 0,
        decimal LaborMinutes = 0,
        decimal? LaborCost = null,
        decimal? MachineCost = null,
        int? CrewSize = null,
        string? Notes = null);
}
