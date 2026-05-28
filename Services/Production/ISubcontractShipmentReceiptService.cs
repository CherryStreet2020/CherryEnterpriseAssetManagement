// Sprint 15.2 PR-6 (2026-05-28) — ISubcontractShipmentReceiptService interface.
//
// All writes for SubcontractShipment + SubcontractReceipt + their lines flow
// through this service so cost posting, status transitions, and inventory
// reconciliation stay atomic.
//
// Per the GO BIG rule: no minimal-path shortcuts. Every operation that mutates
// vendor-WIP, op qty totals, or subcontract op lifecycle is exposed here.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production;

// ═══════════════════════════════════════════════════════════════════════════
// REQUEST + RESULT RECORDS
// ═══════════════════════════════════════════════════════════════════════════

public sealed record CreateSubcontractShipmentRequest(
    int SubcontractOperationId,
    int? SubcontractDemandId,
    int SupplierId,
    int? VendorLocationId,
    int? ShipFromLocationId,
    string? VendorWipLocationCode,
    string? Carrier,
    string? ShippingMethod,
    string? TrackingNumber,
    decimal? FreightCost,
    string? FreightCurrency,
    DateTime? RequiredShipDate,
    DateTime? ExpectedDeliveryDate,
    bool CertRequired,
    string? PackingInstructions,
    string? CreatedBy,
    string? Notes);

public sealed record CreateSubcontractShipmentResult(
    int SubcontractShipmentId,
    string ShipmentNumber,
    string? Message);

public sealed record AddShipmentLineRequest(
    int SubcontractShipmentId,
    int ItemId,
    string? PartNumber,
    string? Description,
    string? DrawingRevision,
    string? LotNumber,
    string? SerialNumber,
    decimal QuantityShipped,
    string Uom,
    decimal? UnitCostSnapshot,
    string? Notes);

public sealed record AddShipmentLineResult(
    int SubcontractShipmentLineId,
    int LineNumber,
    string? Message);

public sealed record ShipmentStatusSummary(
    int SubcontractShipmentId,
    string ShipmentNumber,
    SubcontractShipmentLifecycle Status,
    int LineCount,
    decimal TotalQuantityShipped,
    DateTime? ActualShipDate,
    DateTime? ActualDeliveryDate);

public sealed record CreateSubcontractReceiptRequest(
    int SubcontractOperationId,
    int? SubcontractShipmentId,
    int SupplierId,
    int? VendorLocationId,
    int? ReceivingLocationId,
    string? VendorPackingSlip,
    string? Carrier,
    string? TrackingNumber,
    DateTime? ReceiptDate,
    bool CertReceived,
    string? CertReference,
    bool InspectionRequired,
    string? CreatedBy,
    string? Notes);

public sealed record CreateSubcontractReceiptResult(
    int SubcontractReceiptId,
    string ReceiptNumber,
    string? Message);

public sealed record AddReceiptLineRequest(
    int SubcontractReceiptId,
    int? SubcontractShipmentLineId,
    int ItemId,
    string? PartNumber,
    string? Description,
    string? DrawingRevision,
    string? LotNumber,
    string? SerialNumber,
    decimal QuantityReceived,
    decimal QuantityAccepted,
    decimal QuantityRejected,
    decimal QuantityScrappedAtVendor,
    decimal QuantityShort,
    string Uom,
    SubcontractReceiptScenario Scenario,
    SubcontractReceiptDisposition Disposition,
    string? RejectReason,
    string? NcrReference,
    string? Notes);

public sealed record AddReceiptLineResult(
    int SubcontractReceiptLineId,
    int LineNumber,
    SubcontractReceiptScenario Scenario,
    SubcontractReceiptDisposition Disposition,
    string? Message);

public sealed record ReceiptPostResult(
    int SubcontractReceiptId,
    SubcontractReceiptLifecycle Status,
    int LinesPosted,
    decimal TotalAccepted,
    decimal TotalRejected,
    decimal TotalScrapped,
    bool RequiresApproval,
    string? Message);

public sealed record ReceiptStatusSummary(
    int SubcontractReceiptId,
    string ReceiptNumber,
    SubcontractReceiptLifecycle Status,
    int LineCount,
    decimal TotalAccepted,
    decimal TotalRejected,
    bool ApprovalRequired,
    string? ApprovedBy,
    DateTime? PostedUtc);

public interface ISubcontractShipmentReceiptService
{
    // ── SHIPMENT writes ─────────────────────────────────────────────────────

    /// <summary>Create a Draft shipment header for a subcontract op.
    /// Idempotency: a second call with the same (op, supplier, RequiredShipDate)
    /// while still in Draft returns the existing shipment.</summary>
    Task<Result<CreateSubcontractShipmentResult>> CreateShipmentAsync(
        CreateSubcontractShipmentRequest request, CancellationToken ct = default);

    /// <summary>Append one line (item + lot/serial + qty) to a Draft/Picked
    /// shipment. Fails if shipment is past Staged.</summary>
    Task<Result<AddShipmentLineResult>> AddShipmentLineAsync(
        AddShipmentLineRequest request, CancellationToken ct = default);

    /// <summary>Move a shipment to Picked status (lines locked, pick complete).</summary>
    Task<Result<ShipmentStatusSummary>> MarkShipmentPickedAsync(
        int subcontractShipmentId, string? actor, CancellationToken ct = default);

    /// <summary>Move a shipment to InTransit. Records actual ship date.
    /// Also calls SubcontractOperationService.RecordShipmentAsync to roll up
    /// totals onto the op + advance op lifecycle. Creates a VendorWipTransaction
    /// of type ShipToVendor for the supply chain ledger.</summary>
    Task<Result<ShipmentStatusSummary>> MarkShipmentInTransitAsync(
        int subcontractShipmentId, DateTime? actualShipDate, string? actor,
        CancellationToken ct = default);

    /// <summary>Vendor confirms delivery. Sets DeliveredToVendor + actual date.</summary>
    Task<Result<ShipmentStatusSummary>> MarkShipmentDeliveredAsync(
        int subcontractShipmentId, DateTime? deliveredUtc, string? actor,
        CancellationToken ct = default);

    /// <summary>Cancel a shipment that has not been delivered. Reverses any
    /// vendor-WIP ledger entries created at InTransit time.</summary>
    Task<Result<ShipmentStatusSummary>> CancelShipmentAsync(
        int subcontractShipmentId, string? reason, string? actor,
        CancellationToken ct = default);

    // ── RECEIPT writes ──────────────────────────────────────────────────────

    /// <summary>Create a Draft receipt header. Idempotency: a second call with
    /// the same (op, vendor packing slip) while still in Draft returns the
    /// existing receipt.</summary>
    Task<Result<CreateSubcontractReceiptResult>> CreateReceiptAsync(
        CreateSubcontractReceiptRequest request, CancellationToken ct = default);

    /// <summary>Append one line (one of the 10 §11 scenarios) to a Draft
    /// receipt. Validates accept+reject+scrap ≤ received.</summary>
    Task<Result<AddReceiptLineResult>> AddReceiptLineAsync(
        AddReceiptLineRequest request, CancellationToken ct = default);

    /// <summary>Atomically post a Draft receipt:
    ///   1) Validate every line
    ///   2) Roll line totals into the subcontract op (QuantityReceivedBack,
    ///      QuantityAccepted, QuantityRejected, QuantityScrappedAtVendor)
    ///   3) Advance op lifecycle (Complete / PartiallyReceived / Rejected /
    ///      InInspection per disposition)
    ///   4) Set receipt Status = Posted (or PendingApproval if any line is
    ///      OverReceipt / WrongJobOrPo)
    /// Reads + writes guarded by tenant context.</summary>
    Task<Result<ReceiptPostResult>> PostReceiptAsync(
        int subcontractReceiptId, string? actor, CancellationToken ct = default);

    /// <summary>Approve a receipt that was posted as PendingApproval (over/wrong-PO).
    /// Sets Status = Approved and stamps ApprovedBy/ApprovedUtc.</summary>
    Task<Result<ReceiptStatusSummary>> ApproveReceiptAsync(
        int subcontractReceiptId, string? actor, CancellationToken ct = default);

    /// <summary>Reverse a posted receipt — back out qtys + vendor-WIP ledger.
    /// Sets Status = Reversed. Used for wrong-PO mistakes.</summary>
    Task<Result<ReceiptStatusSummary>> ReverseReceiptAsync(
        int subcontractReceiptId, string? reason, string? actor,
        CancellationToken ct = default);

    // ── Reads ───────────────────────────────────────────────────────────────

    Task<Result<ShipmentStatusSummary>> GetShipmentStatusAsync(
        int subcontractShipmentId, CancellationToken ct = default);

    Task<Result<ReceiptStatusSummary>> GetReceiptStatusAsync(
        int subcontractReceiptId, CancellationToken ct = default);

    Task<IReadOnlyList<SubcontractShipment>> GetShipmentsForOpAsync(
        int subcontractOperationId, CancellationToken ct = default);

    Task<IReadOnlyList<SubcontractReceipt>> GetReceiptsForOpAsync(
        int subcontractOperationId, CancellationToken ct = default);
}
