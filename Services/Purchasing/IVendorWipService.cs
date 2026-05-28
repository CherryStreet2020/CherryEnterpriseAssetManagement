// Sprint 15.1 PR-5 (2026-05-28) — IVendorWipService interface.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Purchasing;

public sealed record RegisterVendorLocationRequest(
    int SupplierId,
    string LocationCode,
    string? SupplierSiteCode,
    string? Address,
    int? LinkedWarehouseId,
    int? LinkedBinLocationId,
    VendorLocationType LocationType,
    bool VendorManaged,
    bool CustomerOwnedMaterialAllowed,
    bool ConsignedMaterialAllowed,
    bool WipAllowed,
    bool InspectionRequiredOnReturn,
    string? DefaultShippingMethod,
    int DefaultTransitDays,
    int? DefaultReceivingLocationId,
    int? DefaultReturnToOperationSequence,
    string? Notes,
    string? CreatedBy);

public sealed record ShipToVendorRequest(
    int ProductionOrderId,
    int OperationSequence,
    int SupplierId,
    int? VendorLocationId,
    int? ItemId,
    string? PartNumber,
    string? Revision,
    string? LotNumber,
    string? SerialNumber,
    decimal Quantity,
    decimal UnitValue,
    string? Uom,
    string? ShipmentDocument,
    string? FromLocationDescription,
    string? ToLocationDescription,
    int? SubcontractOperationId,
    System.DateTime? RequiredReturnDate,
    string? Notes,
    string? CreatedBy);

public sealed record ReceiveFromVendorRequest(
    int VendorWipBalanceId,
    decimal QuantityReceived,
    decimal QuantityAccepted,
    decimal QuantityRejected,
    string? ReceiptDocument,
    string? Notes,
    string? CreatedBy);

public sealed record RecordScrapAtVendorRequest(
    int VendorWipBalanceId,
    decimal QuantityScrapped,
    string? ReasonCode,
    string? Notes,
    string? CreatedBy);

public sealed record VendorWipMovementResult(
    int TransactionId,
    int VendorWipBalanceId,
    decimal QuantityAtVendor,
    VendorWipInventoryStatus InventoryStatus,
    string? Message);

public sealed record VendorWipBalanceSummary(
    int BalanceId,
    int ProductionOrderId,
    int OperationSequence,
    int SupplierId,
    string? PartNumber,
    decimal QuantityShipped,
    decimal QuantityAtVendor,
    decimal QuantityReceivedBack,
    decimal QuantityAccepted,
    decimal QuantityRejected,
    decimal QuantityScrappedAtVendor,
    decimal QuantityLost,
    VendorWipInventoryStatus InventoryStatus,
    int AgingDaysAtVendor);

public interface IVendorWipService
{
    /// <summary>Register a new vendor location (idempotent per supplier + code).</summary>
    Task<Result<int>> RegisterVendorLocationAsync(
        RegisterVendorLocationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Ship physical WIP to a vendor. Creates or top-ups a VendorWipBalance
    /// row + writes a ShipToVendor transaction. Updates the running balance.
    /// </summary>
    Task<Result<VendorWipMovementResult>> ShipToVendorAsync(
        ShipToVendorRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Receive WIP back from vendor. Updates balance quantities, writes a
    /// ReceivedBack transaction (+ InspectionAccepted/Rejected if applicable).
    /// </summary>
    Task<Result<VendorWipMovementResult>> ReceiveFromVendorAsync(
        ReceiveFromVendorRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Record material scrapped at the vendor (vendor process scrap, not us).
    /// </summary>
    Task<Result<VendorWipMovementResult>> RecordScrapAtVendorAsync(
        RecordScrapAtVendorRequest request,
        CancellationToken ct = default);

    /// <summary>Get the aggregate balance summary for a specific balance row.</summary>
    Task<Result<VendorWipBalanceSummary>> GetBalanceAsync(
        int vendorWipBalanceId,
        CancellationToken ct = default);

    /// <summary>Get all balances for a Production Order.</summary>
    Task<IReadOnlyList<VendorWipBalance>> GetBalancesForProAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>Get all balances at a specific supplier (across PROs).</summary>
    Task<IReadOnlyList<VendorWipBalance>> GetBalancesForSupplierAsync(
        int supplierId,
        CancellationToken ct = default);

    /// <summary>List all transactions on a balance row (movement history).</summary>
    Task<IReadOnlyList<VendorWipTransaction>> GetTransactionsForBalanceAsync(
        int vendorWipBalanceId,
        CancellationToken ct = default);

    /// <summary>List vendor locations for a supplier.</summary>
    Task<IReadOnlyList<VendorLocation>> GetVendorLocationsForSupplierAsync(
        int supplierId,
        CancellationToken ct = default);
}
