// B8 PR-PRO-7 (2026-05-27) — "Can I Run This?" 8-Check Readiness Service.
//
// THE BIC DIFFERENTIATOR: every MES shows "green/yellow/red" lights for
// operation readiness. SAP/Oracle/Plex do it at the ORDER level — they say
// "Material Short" but leave the OPERATOR to figure out WHY, WHICH COMPONENT,
// and WHAT IS BEING DONE ABOUT IT. We go to the OPERATION level and give
// the answer: "Op 30 Weld — Waiting on PO 45678 Line 2 (SKF-6205-2RSH
// bearing, due June 12, 50 needed / 38 received)."
//
// The 8 checks:
//   1. Materials Ready — walk BOM lines for this op, check supply link fields
//   2. Prior Op Complete — predecessor status + available qty
//   3. Resource Available — WorkCenter status + capacity
//   4. Labor Qualified — operator certification (placeholder until HR entity)
//   5. Quality Clear — hold flags + open CARs + expired deviations
//   6. Documents Current — drawing/WI revision via DMS + ECO impact
//   7. Tooling Ready — tool/fixture/gauge availability + calibration
//   8. Maintenance Clear — Asset status + PM schedule + calibration
//
// Each check returns PASS / WARNING / FAIL + human-readable description.
// Aggregated into a single OperationReadiness record per operation.
//
// REFERENCES:
//   - docs/research/material-supply-link-spec-2026-05-27.md
//   - docs/research/po-cockpit-spec-2026-05-26.md §8.3
//   - PR-PRO-5 WipMove (AvailableQty feeds check #2)
//   - PR-PRO-6 Completion (feeds op status for check #2)
//   - Sprint 14.3 PR-7 Impact Analysis (feeds check #6)
//   - Sprint 14.2 DMS (feeds check #6)

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    /// <summary>
    /// Readiness check result for a single dimension.
    /// </summary>
    public enum ReadinessStatus
    {
        /// <summary>Check passed — no blockers.</summary>
        Pass = 0,
        /// <summary>At risk — supply close to need date, partial, or degraded.</summary>
        Warning = 1,
        /// <summary>Blocked — cannot run this operation until resolved.</summary>
        Fail = 2,
    }

    /// <summary>
    /// One of the 8 readiness check results.
    /// </summary>
    public sealed record ReadinessCheckResult(
        string CheckName,
        ReadinessStatus Status,
        string Description);

    /// <summary>
    /// Material readiness detail for a single BOM line that is blocking or at risk.
    /// </summary>
    public sealed record MaterialReadinessDetail(
        int BomLineId,
        string ChildPartNumber,
        MaterialSupplyStatus SupplyStatus,
        SupplyRisk Risk,
        string? LinkedSupplyRecordNumber,
        string? SupplierOrDepartment,
        DateTime? SupplyRequiredDate,
        DateTime? SupplyPromisedDate,
        decimal QuantityRequired,
        decimal QuantityReceived,
        decimal QuantityRemaining,
        string Description);

    /// <summary>
    /// Full readiness assessment for a single operation.
    /// </summary>
    public sealed record OperationReadiness(
        int OperationId,
        int SequenceNumber,
        string OperationDescription,
        ReadinessStatus OverallStatus,
        IReadOnlyList<ReadinessCheckResult> Checks,
        IReadOnlyList<MaterialReadinessDetail> MaterialDetails);

    /// <summary>
    /// Full readiness assessment for a production order (all operations).
    /// </summary>
    public sealed record ProductionOrderReadiness(
        int ProductionOrderId,
        string OrderNumber,
        ReadinessStatus OverallStatus,
        int PassCount,
        int WarningCount,
        int FailCount,
        IReadOnlyList<OperationReadiness> Operations);

    public interface IOperationReadinessService
    {
        /// <summary>
        /// Run all 8 readiness checks for a single operation.
        /// Returns the full check breakdown + material supply detail.
        /// </summary>
        Task<Result<OperationReadiness>> CheckOperationReadinessAsync(
            int operationId, CancellationToken ct = default);

        /// <summary>
        /// Run all 8 readiness checks for every operation in a production order.
        /// Returns per-op breakdown with order-level summary counts.
        /// </summary>
        Task<Result<ProductionOrderReadiness>> CheckOrderReadinessAsync(
            int productionOrderId, CancellationToken ct = default);

        /// <summary>
        /// Run only the material readiness check (check #1) for a single operation.
        /// Returns detailed per-BOM-line supply link analysis.
        /// </summary>
        Task<Result<IReadOnlyList<MaterialReadinessDetail>>> CheckMaterialReadinessAsync(
            int operationId, CancellationToken ct = default);

        /// <summary>
        /// Refresh supply link fields on all BOM lines for a production order
        /// from the linked supply sources (PO status, WO progress, inventory levels).
        /// Called before readiness check or on-demand by the planner.
        /// </summary>
        Task<Result<int>> RefreshSupplyLinksAsync(
            int productionOrderId, CancellationToken ct = default);

        /// <summary>
        /// Link a BOM line to a specific supply source (PO line, WO, reservation, transfer).
        /// </summary>
        Task<Result<ProductionMaterialStructure>> LinkSupplyAsync(
            LinkSupplyRequest request, CancellationToken ct = default);

        /// <summary>
        /// Unlink a BOM line from its supply source (clears all supply link fields).
        /// </summary>
        Task<Result<ProductionMaterialStructure>> UnlinkSupplyAsync(
            int bomLineId, CancellationToken ct = default);

        /// <summary>
        /// Update the supply status and risk for a single BOM line.
        /// Used by the supply status refresh job and manual override.
        /// </summary>
        Task<Result<ProductionMaterialStructure>> UpdateSupplyStatusAsync(
            UpdateSupplyStatusRequest request, CancellationToken ct = default);

        /// <summary>
        /// Get all BOM lines for an operation with their current supply link status.
        /// </summary>
        Task<IReadOnlyList<ProductionMaterialStructure>> GetSupplyLinksForOperationAsync(
            int operationId, CancellationToken ct = default);

        /// <summary>
        /// Get all BOM lines for a production order with their current supply link status.
        /// </summary>
        Task<IReadOnlyList<ProductionMaterialStructure>> GetSupplyLinksForOrderAsync(
            int productionOrderId, CancellationToken ct = default);
    }

    // ---- REQUEST RECORDS ----

    public sealed record LinkSupplyRequest(
        int CompanyId,
        int BomLineId,
        MaterialSupplyType SupplyType,
        LinkedSupplyRecordType RecordType,
        int? LinkedRecordId,
        int? LinkedLineId,
        string? LinkedRecordNumber,
        string? SupplierOrDepartment,
        string? BuyerOrPlanner,
        DateTime? RequiredDate,
        DateTime? PromisedDate,
        decimal QuantityRequired,
        decimal QuantitySupplied,
        string? Notes,
        string LinkedBy);

    public sealed record UpdateSupplyStatusRequest(
        int BomLineId,
        MaterialSupplyStatus Status,
        SupplyRisk Risk,
        decimal QuantityReceived,
        decimal QuantityRemaining,
        DateTime? AvailableDate,
        bool LateToNeedDate,
        string? Notes,
        string UpdatedBy);
}
