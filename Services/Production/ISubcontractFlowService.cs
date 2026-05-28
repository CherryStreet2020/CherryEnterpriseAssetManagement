// Sprint 15.2 PR-7 (2026-05-28) — ISubcontractFlowService interface.
//
// THE §5 EIGHT-STEP ORCHESTRATOR.
//
// PR-4 (SubcontractOperation + Dual-Demand), PR-5 (Vendor WIP), and PR-6
// (Shipment + Receipt) shipped the substrate. This service is the BUSINESS
// FLOW glue that walks an outside-processing operation through Dean's §5
// canonical 8 steps in one cohesive API:
//
//   1. PRO release → evaluate routing for subcontract ops
//   2. Create subcontract demands (service + WIP — the §9 dual demand)
//   3. Link a subcontract PO line to the service demand
//   4. Gate on prior operation completion
//   5. Ship WIP to vendor (PR-6 ISubcontractShipmentReceiptService)
//   6. Track vendor processing status
//   7. Receive subcontract PO + return WIP (PR-6)
//   8. Move to next operation + readiness check (B8 IOperationReadinessService)
//
// Pure glue — no new entities, no new migrations. State lives across
// SubcontractOperation / SubcontractDemand / SubcontractShipment /
// SubcontractReceipt / VendorWipBalance. The orchestrator's job is to:
//   * Surface a stateless aggregate view of "where is this op in the 8 steps?"
//   * Provide a one-call helper for each step that bundles the underlying
//     service calls (e.g., Step 5 = create shipment + add line + mark in
//     transit, all in one call)
//   * Compute readiness on demand by delegating to IOperationReadinessService
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §5
//   - docs/research/purchasing-cascade-design-2026-05-28.md Wave 2 PR-7

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

/// <summary>
/// One subcontract op candidate identified by Step-1 routing scan.
/// Doesn't yet exist as a SubcontractOperation row — caller decides which
/// to materialize via Step 2.
/// </summary>
public sealed record SubcontractCandidate(
    int ProductionOrderId,
    int OperationSequence,
    string OperationCode,
    string OperationDescription,
    int? SupplierId,
    int? ServiceItemId,
    decimal QuantityRequired,
    string Reason);

/// <summary>Result of Step 1 (read-only scan).</summary>
public sealed record EvaluateRoutingResult(
    int ProductionOrderId,
    IReadOnlyList<SubcontractCandidate> Candidates,
    int ExistingSubcontractOpCount,
    string? Message);

/// <summary>Result of Step 2 (ops + dual-demands ensured).</summary>
public sealed record EnsureOpsAndDemandsResult(
    int ProductionOrderId,
    int OpsCreated,
    int OpsAlreadyExisted,
    int DemandsCreated,
    int DemandsAlreadyExisted,
    string? Message);

/// <summary>Result of Step 3 (link PO line to service demand).</summary>
public sealed record LinkServicePoLineResult(
    int SubcontractOperationId,
    int ServicePurchaseOrderLineId,
    SubcontractPoCreationStatus NewPoStatus,
    string? Message);

/// <summary>Result of Step 4 (readiness gate).</summary>
public sealed record ReadinessGateResult(
    int SubcontractOperationId,
    bool IsReady,
    bool PriorOpComplete,
    bool ServicePoCreated,
    decimal PriorOpCompletedQuantity,
    decimal PriorOpRequiredQuantity,
    string? BlockingReason);

/// <summary>Step 5 inputs — one call wraps create-shipment + add-line + mark-in-transit.</summary>
public sealed record Step5ShipRequest(
    int SubcontractOperationId,
    int? VendorLocationId,
    int? ShipFromLocationId,
    string? Carrier,
    string? ShippingMethod,
    string? TrackingNumber,
    decimal? FreightCost,
    string? FreightCurrency,
    DateTime? RequiredShipDate,
    DateTime? ExpectedDeliveryDate,
    int WipItemId,
    string? PartNumber,
    string? Description,
    string? DrawingRevision,
    string? LotNumber,
    string? SerialNumber,
    decimal QuantityShipped,
    string Uom,
    decimal? UnitCostSnapshot,
    bool CertRequired,
    string? PackingInstructions,
    string? CreatedBy,
    string? Notes);

/// <summary>Result of Step 5 (one shipment, one line, marked InTransit).</summary>
public sealed record Step5ShipResult(
    int SubcontractShipmentId,
    string ShipmentNumber,
    int SubcontractShipmentLineId,
    SubcontractShipmentLifecycle ShipmentStatus,
    SubcontractOperationStatus OpStatus,
    string? Message);

/// <summary>Aggregated cross-entity status for Step 6 (read-only).</summary>
public sealed record VendorProcessingStatus(
    int SubcontractOperationId,
    int ProductionOrderId,
    int OperationSequence,
    SubcontractOperationStatus OpStatus,
    SubcontractShipmentStatus ShipmentStatus,
    SubcontractReceiptStatus ReceiptStatus,
    decimal QuantityShipped,
    decimal QuantityAtVendor,
    decimal QuantityReceivedBack,
    decimal QuantityAccepted,
    decimal QuantityRejected,
    decimal QuantityScrappedAtVendor,
    int OpenShipmentCount,
    int OpenReceiptCount,
    int VendorWipBalanceCount,
    DateTime? RequiredBackDate,
    int? DaysLate);

/// <summary>Step 7 inputs — one call wraps create-receipt + add-line + post.</summary>
public sealed record Step7ReceiveRequest(
    int SubcontractOperationId,
    int? SubcontractShipmentId,
    int? ReceivingLocationId,
    string? VendorPackingSlip,
    string? Carrier,
    string? TrackingNumber,
    DateTime? ReceiptDate,
    int WipItemId,
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
    bool CertReceived,
    string? CertReference,
    bool InspectionRequired,
    string? CreatedBy,
    string? Notes);

/// <summary>Result of Step 7.</summary>
public sealed record Step7ReceiveResult(
    int SubcontractReceiptId,
    string ReceiptNumber,
    int SubcontractReceiptLineId,
    SubcontractReceiptLifecycle ReceiptStatus,
    SubcontractOperationStatus OpStatus,
    bool RequiresApproval,
    decimal QuantityAcceptedSoFar,
    string? Message);

/// <summary>Result of Step 8 (advance to next op + signal readiness).</summary>
public sealed record AdvanceToNextOpResult(
    int SubcontractOperationId,
    SubcontractOperationStatus OpStatus,
    int? NextOperationSequence,
    bool NextOpReadinessReady,
    string? NextOpReadinessSummary,
    string? Message);

/// <summary>Aggregate view: where is this op in the 8-step flow?</summary>
public sealed record FlowStateSummary(
    int SubcontractOperationId,
    int ProductionOrderId,
    int OperationSequence,
    int CurrentStep, // 1..8
    string CurrentStepName,
    SubcontractOperationStatus OpStatus,
    SubcontractDemandStatus? DemandBindingStatus,
    SubcontractShipmentLifecycle? LatestShipmentStatus,
    SubcontractReceiptLifecycle? LatestReceiptStatus,
    decimal QuantityShipped,
    decimal QuantityReceivedBack,
    decimal QuantityAccepted,
    string[] CompletedStepNames,
    string? NextActionHint);

public interface ISubcontractFlowService
{
    // Step 1: routing scan — read-only, no writes
    Task<Result<EvaluateRoutingResult>> EvaluateRoutingForSubcontractAsync(
        int productionOrderId, CancellationToken ct = default);

    // Step 2: create ops + dual-demands for all candidates (idempotent)
    Task<Result<EnsureOpsAndDemandsResult>> EnsureOpsAndDemandsAsync(
        int productionOrderId,
        IReadOnlyList<SubcontractCandidate> candidates,
        string? createdBy,
        CancellationToken ct = default);

    // Step 3: attach the caller-provided service PO line to the SubcontractOperation
    Task<Result<LinkServicePoLineResult>> LinkServicePoLineAsync(
        int subcontractOperationId,
        int servicePurchaseOrderLineId,
        string? actor,
        CancellationToken ct = default);

    // Step 4: evaluate prior-op-complete + PO-created readiness gate
    Task<Result<ReadinessGateResult>> EvaluateReadinessGateAsync(
        int subcontractOperationId, CancellationToken ct = default);

    // Step 5: one-call ship — create shipment + add 1 line + mark in transit
    Task<Result<Step5ShipResult>> ExecuteShipmentAsync(
        Step5ShipRequest request, CancellationToken ct = default);

    // Step 6: aggregate cross-entity status snapshot (read-only)
    Task<Result<VendorProcessingStatus>> GetVendorProcessingStatusAsync(
        int subcontractOperationId, CancellationToken ct = default);

    // Step 7: one-call receive — create receipt + add 1 line + post
    Task<Result<Step7ReceiveResult>> ExecuteReceiptAsync(
        Step7ReceiveRequest request, CancellationToken ct = default);

    // Step 8: mark Complete + run readiness check on next routing op
    Task<Result<AdvanceToNextOpResult>> AdvanceToNextOpAsync(
        int subcontractOperationId, string? actor, CancellationToken ct = default);

    // Aggregate: where is this op in the 8-step flow right now?
    Task<Result<FlowStateSummary>> GetFlowStateAsync(
        int subcontractOperationId, CancellationToken ct = default);
}
