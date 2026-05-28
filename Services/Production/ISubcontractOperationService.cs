// Sprint 15.1 PR-4 (2026-05-28) — ISubcontractOperationService interface.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production;

public sealed record CreateSubcontractOperationRequest(
    int ProductionOrderId,
    int OperationSequence,
    string OperationCode,
    string OperationDescription,
    int? SupplierId,
    int? ServiceItemId,
    decimal QuantityToShip,
    decimal ServiceUnitCost,
    int? PriorOperationSequence,
    int? ReturnOperationSequence,
    System.DateTime? RequiredShipDate,
    System.DateTime? RequiredBackDate,
    bool ShipWipRequired,
    bool GenerateSubcontractPo,
    int? WipItemId,
    decimal? FixedLeadTimeDays,
    decimal? VariableLeadTimeDaysPerUnit,
    string? CreatedBy,
    string? Notes);

public sealed record CreateSubcontractOperationResult(
    int SubcontractOperationId,
    int ProductionOrderId,
    int OperationSequence,
    string? Message);

public sealed record CreateSubcontractDemandResult(
    int SubcontractDemandId,
    int? ServicePurchaseDemandId,
    int? WipMovementDemandId,
    string? Message);

public sealed record SubcontractStatusSummary(
    int SubcontractOperationId,
    int ProductionOrderId,
    int OperationSequence,
    SubcontractOperationStatus Status,
    SubcontractPoCreationStatus PoStatus,
    SubcontractShipmentStatus ShipmentStatus,
    SubcontractReceiptStatus ReceiptStatus,
    decimal QuantityToShip,
    decimal QuantityShipped,
    decimal QuantityReceivedBack,
    decimal QuantityAccepted,
    decimal QuantityRejected);

public interface ISubcontractOperationService
{
    /// <summary>
    /// Create a new subcontract operation row for a PRO + routing op.
    /// Idempotent per (ProductionOrderId, OperationSequence).
    /// </summary>
    Task<Result<CreateSubcontractOperationResult>> CreateSubcontractOperationAsync(
        CreateSubcontractOperationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Create the two linked ProductionSupplyDemand rows (service + WIP)
    /// and bind them via a SubcontractDemand row.
    /// Idempotent per SubcontractOperationId.
    /// </summary>
    Task<Result<CreateSubcontractDemandResult>> CreateSubcontractDemandAsync(
        int subcontractOperationId,
        string? createdBy,
        CancellationToken ct = default);

    /// <summary>
    /// Transition the subcontract op lifecycle (validates allowed paths).
    /// </summary>
    Task<Result<SubcontractOperationStatus>> TransitionStatusAsync(
        int subcontractOperationId,
        SubcontractOperationStatus newStatus,
        string? reason,
        string? actor,
        CancellationToken ct = default);

    /// <summary>
    /// Record WIP shipped to vendor — updates QuantityShipped, shipment status,
    /// op status, and the WIP demand allocation.
    /// </summary>
    Task<Result<SubcontractStatusSummary>> RecordShipmentAsync(
        int subcontractOperationId,
        decimal quantityShipped,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Record WIP returned from vendor — updates received, accepted, rejected
    /// quantities; advances receipt status + op status.
    /// </summary>
    Task<Result<SubcontractStatusSummary>> RecordReceiptAsync(
        int subcontractOperationId,
        decimal quantityReceived,
        decimal quantityAccepted,
        decimal quantityRejected,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Mark the subcontract op Complete after acceptance.
    /// </summary>
    Task<Result<SubcontractStatusSummary>> MarkCompleteAsync(
        int subcontractOperationId,
        string? actor,
        CancellationToken ct = default);

    /// <summary>Snapshot of a single subcontract op.</summary>
    Task<Result<SubcontractStatusSummary>> GetStatusAsync(
        int subcontractOperationId,
        CancellationToken ct = default);

    /// <summary>All subcontract ops on a PRO.</summary>
    Task<IReadOnlyList<SubcontractOperation>> GetSubcontractOperationsForProAsync(
        int productionOrderId,
        CancellationToken ct = default);
}
