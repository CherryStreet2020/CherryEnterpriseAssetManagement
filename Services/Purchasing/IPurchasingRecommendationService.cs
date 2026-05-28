// =============================================================================
// CherryAI EAM — IPurchasingRecommendationService (Sprint 15.3 PR-15)
//
// CLOSES Wave 3 of the 20-PR Purchasing Cascade.
//
// Implements Dean's spec §18 — the buyer recommendation engine.
//
//   "A buyer should not have to guess what to buy."  — Dean §18
//
// For each ProductionSupplyDemand, returns a structured PurchasingRecommendation
// covering:
//   - action  (one of 11 §18 RecommendedAction patterns)
//   - reason  (human-readable why)
//   - supplier (suggested vendor + name)
//   - quantity recommendation
//   - required date + suggested order date
//   - existing supply pointers (inventory / linked PO / linked child WO)
//   - risk classification
//
// This service is PURE READ. It composes:
//   - IAutoPurchaseService.EvaluateDemandAsync   (§16 decision engine, PR-14)
//   - IDemandConsolidationService                (informational reference)
//   - Demand state from ProductionSupplyDemand   (status quartet + linkage)
//
// Wired into:
//   - PurchasingControlCenterService.GetSupplyDemandQueueAsync — replaces
//     the placeholder SuggestNextAction with real §18 recommendations on
//     the "Suggested Action" column of the Purchasing CC Supply Demand queue.
//   - /Admin/PurchasingRecommendationProbe — diagnostic probe
//
// REFERENCES:
//   - docs/research/purchasing-subcontracting-supply-demand-dean-research.txt §18
//   - docs/research/purchasing-cascade-design-2026-05-28.md Wave 3 PR-15
//   - Services/Purchasing/IAutoPurchaseService.cs  (§16 — PR-14)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Purchasing;

// ═══════════════════════════════════════════════════════════════════════════
// 11 §18 RECOMMENDATION PATTERNS — direct map from the spec
// ═══════════════════════════════════════════════════════════════════════════

public enum RecommendedAction
{
    /// <summary>No inventory + no PO + buyable policy → create a job-linked PO.</summary>
    CreatePo = 0,
    /// <summary>Inventory available in the right location → reserve and issue.</summary>
    ReserveAndIssue = 1,
    /// <summary>Inventory available but at wrong warehouse → create transfer.</summary>
    CreateTransfer = 2,
    /// <summary>Linked PO exists but late → expedite the PO.</summary>
    ExpeditePo = 3,
    /// <summary>PO exists but not yet linked to this demand → link.</summary>
    LinkPoToDemand = 4,
    /// <summary>No supplier resolved → request sourcing from purchasing leads.</summary>
    RequestSourcing = 5,
    /// <summary>Supplier exists but quote required first → create RFQ.</summary>
    CreateRfq = 6,
    /// <summary>Subcontract op released → create subcontract PO + ship WIP.</summary>
    CreateSubcontractPo = 7,
    /// <summary>WIP ready for outside processing → ship to vendor.</summary>
    ShipToVendor = 8,
    /// <summary>Receipt is in inspection → notify quality / push inspection.</summary>
    NotifyQualityInspection = 9,
    /// <summary>Supplier invoice variance pending → review cost variance.</summary>
    ReviewCostVariance = 10,
    /// <summary>Demand is fully satisfied — no action.</summary>
    NoActionSatisfied = 11,
    /// <summary>Demand is blocked — clear blocker before acting.</summary>
    Unblock = 12,
    /// <summary>Wait — prior operation / approval / vendor processing in flight.</summary>
    Wait = 13,
    /// <summary>Catch-all when nothing else fits — show buyer the demand for triage.</summary>
    ReviewManually = 14,
}

public enum RecommendationRisk
{
    /// <summary>Nothing at risk — standard buy / standard wait.</summary>
    Low = 0,
    /// <summary>Approaching required date or single blocker — keep an eye on it.</summary>
    Medium = 1,
    /// <summary>Late / critical shortage / customer milestone risk.</summary>
    High = 2,
    /// <summary>Project-critical or customer-milestone-blocking — escalate immediately.</summary>
    Critical = 3,
}

// ═══════════════════════════════════════════════════════════════════════════
// RECOMMENDATION RECORDS
// ═══════════════════════════════════════════════════════════════════════════

public sealed record PurchasingRecommendation(
    int DemandId,
    string DemandNumber,
    int ProductionOrderId,
    string? ProductionOrderNumber,
    int? OperationSequence,
    string? PartNumber,
    RecommendedAction Action,
    string ActionLabel,
    string Reason,
    int? SuggestedVendorId,
    string? SuggestedVendorName,
    decimal RecommendedQuantity,
    DateTime? RequiredDate,
    DateTime? SuggestedOrderDate,
    int? ExistingPurchaseOrderId,
    string? ExistingPurchaseOrderNumber,
    int? ExistingChildProductionOrderId,
    string? ExistingInventoryHint,
    RecommendationRisk Risk,
    int? DaysUntilRequired,
    string? Notes);

public sealed record PurchasingRecommendationFilter(
    int? CompanyId = null,
    int? SiteId = null,
    int? VendorId = null,
    int? ProductionOrderId = null,
    int? BuyerUserId = null,
    RecommendedAction? OnlyAction = null,
    int Skip = 0,
    int Take = 50);

public sealed record PurchasingRecommendationPage(
    int TotalCount,
    int HighRiskCount,
    int CriticalRiskCount,
    IReadOnlyList<PurchasingRecommendation> Recommendations);

// ═══════════════════════════════════════════════════════════════════════════
// SERVICE INTERFACE
// ═══════════════════════════════════════════════════════════════════════════

public interface IPurchasingRecommendationService
{
    /// <summary>
    /// Compute the §18 recommendation for a single demand. Composes
    /// IAutoPurchaseService for §16 trigger/blocker context, then picks
    /// the right action pattern from §18 based on the demand's supply
    /// status quartet + linkage fields.
    /// </summary>
    Task<Result<PurchasingRecommendation>> GetRecommendationAsync(
        int demandId,
        CancellationToken ct = default);

    /// <summary>
    /// Compute recommendations for every open demand on a PRO. Used by the
    /// Production Cockpit "What should I buy next?" panel and by the
    /// Purchasing CC drill-in view.
    /// </summary>
    Task<Result<IReadOnlyList<PurchasingRecommendation>>> GetRecommendationsForProAsync(
        int productionOrderId,
        CancellationToken ct = default);

    /// <summary>
    /// Tenant-wide recommendation scan with pagination + optional filter on
    /// a specific action pattern. Drives the Purchasing CC "Suggested Action"
    /// column when buyers want to see all "CreatePo" recommendations across
    /// the org or all "ExpeditePo" items together.
    /// </summary>
    Task<Result<PurchasingRecommendationPage>> GetRecommendationsAsync(
        PurchasingRecommendationFilter filter,
        CancellationToken ct = default);

    /// <summary>
    /// Pure helper — given a demand row already in hand, compute the §18
    /// recommendation without an extra database round-trip. Used by
    /// PurchasingControlCenterService.GetSupplyDemandQueueAsync to fold the
    /// recommendation directly into each PurchasingQueueRow's NextActionHint.
    ///
    /// Param contract (fixed in P1-A pre-merge review):
    ///   - productionOrderNumber → PRO's OrderNumber (display in
    ///     ProductionOrderNumber field). Falls back to demand.ProductionOrder?
    ///     .OrderNumber when null.
    ///   - linkedPoNumber → PurchaseOrder.PONumber of the linked PO (display
    ///     in ExistingPurchaseOrderNumber field). Null when no linked PO.
    ///   - suggestedVendorName → Vendor.Name resolved from VendorId (display
    ///     in SuggestedVendorName field).
    /// </summary>
    PurchasingRecommendation BuildFromDemand(
        ProductionSupplyDemand demand,
        string? productionOrderNumber = null,
        string? linkedPoNumber = null,
        string? suggestedVendorName = null);
}
