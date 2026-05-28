// Sprint 15.2 PR-8 (2026-05-28) — ISubcontractCostingService interface.
//
// THE §12 COST WIRE-UP.
//
// PR-4/5/6/7 built the substrate (ops + dual-demand + vendor WIP + shipment/
// receipt events + 8-step orchestrator). PR-8 plugs subcontract events into
// the Sprint 14.4 CostTransaction engine so:
//   * Service cost posts at receipt (provisional from PO line)
//   * Freight to/from vendor posts at ship/receive (when captured)
//   * Packaging + expedite + cert + inspection fees post at the appropriate event
//   * Scrap/vendor credit/rework charges post at receipt time when their
//     scenario fires
//   * Invoice arrival true-ups variance vs the provisional service cost
//   * PRO close settlement clears any open subcontract WIP
//
// Per Dean's spec §12 (13 canonical cost elements). 6 new CostTransactionType
// values (SubcontractFreightOut/Return/Expedite/ScrapCharge/VendorCredit/Overhead)
// land alongside the existing SubcontractService(51), TestCertification(81),
// QualityInspection(80), PackagingCrating(82), DutyTariff(61), FreightIn(60),
// PurchasePriceVariance(202), InvoiceVariance(201).
//
// Pure glue — every write goes through ICostTransactionService.PostCostAsync.
// No new entities, no migrations.
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §12
//   - docs/research/purchasing-cascade-design-2026-05-28.md Wave 2 PR-8
//   - Models/Production/CostTransaction.cs (CostTransactionType enum)
//   - Services/Production/CostTransactionService.cs (PostCostAsync surface)

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

/// <summary>Cost-elements posted at shipment-to-vendor time (Step 5).</summary>
public sealed record PostShipmentCostsRequest(
    int SubcontractOperationId,
    int SubcontractShipmentId,
    decimal? FreightOutCost,
    decimal? PackagingCost,
    decimal? ExpediteCost,
    string CurrencyCode,
    string? PostedBy,
    string? Notes);

/// <summary>Cost-elements posted at receive-from-vendor time (Step 7).</summary>
public sealed record PostReceiptCostsRequest(
    int SubcontractOperationId,
    int SubcontractReceiptId,
    decimal QuantityAccepted,
    decimal? ServiceUnitCost,        // From SubcontractOperation.ServiceUnitCost when not overridden
    decimal? FreightReturnCost,
    decimal? CertFee,
    decimal? InspectionFee,
    decimal? ScrapChargeAtVendor,    // Vendor scrap event → recovery/charge
    decimal? VendorCredit,           // Vendor credit for rework/replacement
    string CurrencyCode,
    string? PostedBy,
    string? Notes);

/// <summary>Cost-elements posted when supplier invoice arrives — true-up vs provisional.</summary>
public sealed record PostInvoiceTrueUpRequest(
    int SubcontractOperationId,
    decimal InvoicedAmount,
    decimal QuantityInvoiced,
    string? InvoiceNumber,
    string CurrencyCode,
    string? PostedBy,
    string? Notes);

/// <summary>Settle any open subcontract WIP at PRO close.</summary>
public sealed record SettleAtCloseRequest(
    int SubcontractOperationId,
    string? PostedBy,
    string? Notes);

public sealed record SubcontractCostPostResult(
    int SubcontractOperationId,
    int CostTransactionsPosted,
    decimal TotalExtendedCost,
    string CurrencyCode,
    IReadOnlyList<int> CostTransactionIds,
    string? Message);

public sealed record SubcontractCostSummary(
    int SubcontractOperationId,
    int ProductionOrderId,
    int OperationSequence,
    decimal ServiceCostPosted,
    decimal FreightOutPosted,
    decimal FreightReturnPosted,
    decimal PackagingPosted,
    decimal ExpeditePosted,
    decimal CertFeesPosted,
    decimal InspectionFeesPosted,
    decimal ScrapChargePosted,
    decimal VendorCreditPosted,
    decimal OverheadPosted,
    decimal PpvPosted,
    decimal InvoiceVariancePosted,
    decimal TotalSubcontractCost,
    int TotalTransactionCount);

public interface ISubcontractCostingService
{
    /// <summary>
    /// Post shipment-time cost elements (freight-out, packaging, expedite).
    /// Idempotent on (shipmentId, costType) — duplicate posts at the same
    /// shipment+element are blocked.
    /// </summary>
    Task<Result<SubcontractCostPostResult>> PostShipmentCostsAsync(
        PostShipmentCostsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Post receipt-time cost elements (service cost, freight-return, cert,
    /// inspection, scrap charge, vendor credit). Service cost is computed as
    /// QuantityAccepted × ServiceUnitCost (uses op.ServiceUnitCost if not
    /// passed). Idempotent on (receiptId, costType).
    /// </summary>
    Task<Result<SubcontractCostPostResult>> PostReceiptCostsAsync(
        PostReceiptCostsRequest request, CancellationToken ct = default);

    /// <summary>
    /// True-up against supplier invoice — compares invoiced amount to the
    /// provisional service cost posted at receipt; posts an InvoiceVariance
    /// transaction for any delta. Posts a PurchasePriceVariance transaction
    /// for unit-price drift if applicable.
    /// </summary>
    Task<Result<SubcontractCostPostResult>> PostInvoiceTrueUpAsync(
        PostInvoiceTrueUpRequest request, CancellationToken ct = default);

    /// <summary>
    /// Settle open subcontract costs at PRO close — posts a VarianceSettlement
    /// transaction for any unmatched residual. Idempotent: refuses a second
    /// settle for the same op once one has been posted.
    /// </summary>
    Task<Result<SubcontractCostPostResult>> SettleAtCloseAsync(
        SettleAtCloseRequest request, CancellationToken ct = default);

    /// <summary>Aggregate cost view by element for one subcontract op.</summary>
    Task<Result<SubcontractCostSummary>> GetCostSummaryAsync(
        int subcontractOperationId, CancellationToken ct = default);
}
