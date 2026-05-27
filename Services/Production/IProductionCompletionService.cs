// B8 PR-PRO-6 (2026-05-27) — Complete + Scrap + Rework service interface.
// Atomic completion posting with preview-before-post.
// AS9100 §8.5.1 + §8.7 nonconforming output control.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    public interface IProductionCompletionService
    {
        // ---- COMPLETE ----

        /// <summary>
        /// Record an operation completion — good + scrap + rework + reject in one atomic posting.
        /// Triggers auto-advance via IProductionWipMoveService if good qty > 0.
        /// Triggers backflush if BackflushMaterials = true.
        /// Triggers FG receipt if IsFinalOperation = true.
        /// </summary>
        Task<Result<ProductionCompletionEvent>> RecordCompletionAsync(
            RecordCompletionRequest request, CancellationToken ct = default);

        // ---- SCRAP ----

        /// <summary>
        /// Record a scrap event with 5-dimensional root cause classification.
        /// Updates operation ScrapQty. Links to NCR if NcrRequired = true.
        /// Enforces supervisor approval threshold if configured.
        /// </summary>
        Task<Result<ProductionScrapEvent>> RecordScrapAsync(
            RecordScrapRequest request, CancellationToken ct = default);

        /// <summary>Approve a scrap event that requires supervisor sign-off.</summary>
        Task<Result<ProductionScrapEvent>> ApproveScrapAsync(
            int scrapEventId, string approvedBy, CancellationToken ct = default);

        // ---- REWORK ----

        /// <summary>
        /// Record a rework decision. Creates the routing (send-back or insert new op)
        /// via IProductionWipMoveService. Updates operation ReworkQty.
        /// </summary>
        Task<Result<ProductionReworkEvent>> RecordReworkAsync(
            RecordReworkRequest request, CancellationToken ct = default);

        // ---- READS ----

        Task<IReadOnlyList<ProductionCompletionEvent>> GetCompletionsForOrderAsync(
            int productionOrderId, CancellationToken ct = default);

        Task<IReadOnlyList<ProductionScrapEvent>> GetScrapForOrderAsync(
            int productionOrderId, CancellationToken ct = default);

        Task<IReadOnlyList<ProductionReworkEvent>> GetReworkForOrderAsync(
            int productionOrderId, CancellationToken ct = default);

        Task<ProductionCompletionEvent?> GetCompletionAsync(int id, CancellationToken ct = default);
        Task<ProductionScrapEvent?> GetScrapAsync(int id, CancellationToken ct = default);
        Task<ProductionReworkEvent?> GetReworkAsync(int id, CancellationToken ct = default);
    }

    // ---- REQUEST RECORDS ----

    public sealed record RecordCompletionRequest(
        int CompanyId,
        int ProductionOrderId,
        int OperationId,
        decimal GoodQuantity,
        decimal ScrapQuantity,
        decimal ReworkQuantity,
        decimal RejectQuantity,
        bool CompleteRemaining,
        bool IsFinalOperation,
        decimal MoveQuantityToNextOp,
        string? EmployeeName,
        int? EmployeeId,
        int? ResourceWorkCenterId,
        bool BackflushMaterials,
        bool AutoIssuePullMaterials,
        bool InspectionRequired,
        string? LotNumbers,
        string? SerialNumbers,
        string? Notes,
        string CompletedBy);

    public sealed record RecordScrapRequest(
        int CompanyId,
        int ProductionOrderId,
        int DetectedAtOperationId,
        int? CausedAtOperationId,
        decimal ScrapQuantity,
        string? ScrapUom,
        int? ScrapReasonCodeId,
        int? DefectCodeId,
        int? CauseCodeId,
        ScrapResponsibleArea ResponsibleArea,
        ScrapDisposition Disposition,
        bool IsComponentScrap,
        bool IsOperationScrap,
        bool ReplacementRequired,
        CostTreatment CostTreatment,
        bool NcrRequired,
        bool SupervisorApprovalRequired,
        string? LotNumbers,
        string? SerialNumbers,
        string? Notes,
        string RecordedBy);

    public sealed record RecordReworkRequest(
        int CompanyId,
        int ProductionOrderId,
        int SourceOperationId,
        int? ReworkOperationId,
        decimal ReworkQuantity,
        ReworkRoutingType RoutingType,
        string? ReworkInstructions,
        int? ReworkReasonCodeId,
        bool ReworkMaterialRequired,
        bool RemoveDefectiveComponent,
        decimal AdditionalLaborPlannedMins,
        int? AssignedWorkCenterId,
        DateTime? DueDate,
        bool QualityHold,
        bool ReinspectRequired,
        bool ScrapAfterFailedReworkAllowed,
        bool ReturnToOriginalFlow,
        CostTreatment CostTreatment,
        int? NcrId,
        int? CarId,
        string? Notes,
        string DecidedBy);
}
