// ADR-025 D5 — IWorkOrderService (Sprint 12.9 PR #3+).
//
// Centralizes write paths off Pages/WorkOrders/Details.cshtml.cs — the worst
// offender in the 2026-05-20 audit (17 direct SaveChangesAsync calls). The
// interface declares every logical write the page performs; the concrete
// WorkOrderService implements them with the same logic the page used to
// have inline, plus the ADR-025 service-layer guarantees (tenant scoping,
// Result<T> envelope, future idempotency-keyed posting paths).
//
// Phasing (all 17 writes do NOT land in one PR — too large/risky):
//
//   PR #3   — 5 plain-CRUD writes (no JE, no inventory):
//             AddOperation, MoveOperation, UpdateOperationStatus,
//             AddOperationTool, AddPlannedMaterial
//   PR #3.1 — 3 JE-posting writes (operation-level labor + parts):
//             AddLabor, IssueOperationPart, ReturnOperationPart
//   PR #3.2 — 4 JE-posting writes (WO-level materials):
//             AddOperationPart, IssueMaterial, ReturnMaterial,
//             RemovePlannedMaterial, LoadTemplateMaterials
//   PR #3.3 — 3 WO-level writes (edit, dispatch, capitalize):
//             EditWorkOrder, DispatchUpdate, Capitalize
//
// The PageModel stays in Analyzers/ControlPlaneAllowlist.txt through PRs
// #3 → #3.2; PR #3.3 removes it once all 17 writes have moved.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Maintenance;

/// <summary>
/// Domain service for Work Order mutations (Sprint 12.9 PR #3+).
/// New PageModel callers should inject <see cref="IWorkOrderService"/> instead
/// of <c>AppDbContext</c> for any mutation; reads can still go through the
/// PageModel's own <c>_context</c> for thin display projections.
/// </summary>
public interface IWorkOrderService
{
    /// <summary>
    /// Add a new <see cref="WorkOrderOperation"/> to a work order. Computes
    /// the next sequence number, resolves the operation type via the lookup
    /// service, and uppercases title/description per the legacy convention.
    /// </summary>
    Task<Result<WorkOrderOperation>> AddOperationAsync(AddOperationRequest request, CancellationToken ct);

    /// <summary>
    /// Move an operation up or down within its work order by swapping the
    /// adjacent operation's <c>Sequence</c> value. Returns the moved
    /// operation so callers can resolve its <c>WorkOrderId</c> for the
    /// post-mutation redirect.
    /// </summary>
    Task<Result<WorkOrderOperation>> MoveOperationAsync(MoveOperationRequest request, CancellationToken ct);

    /// <summary>
    /// Update an operation's status (Pending / InProgress / Completed),
    /// resolving the lookup value and stamping start/complete timestamps
    /// when the new status warrants them.
    /// </summary>
    Task<Result<WorkOrderOperation>> UpdateOperationStatusAsync(UpdateOperationStatusRequest request, CancellationToken ct);

    /// <summary>
    /// Attach a tool requirement (<see cref="WorkOrderOperationTool"/>) to
    /// an operation. Simple metadata write — no inventory movement.
    /// Returns the parent operation so callers can resolve its
    /// <c>WorkOrderId</c> for the post-mutation redirect.
    /// </summary>
    Task<Result<WorkOrderOperation>> AddOperationToolAsync(AddOperationToolRequest request, CancellationToken ct);

    /// <summary>
    /// Add a planned material (<see cref="WorkOrderPart"/>) to a work order.
    /// Validates the item is visible to the current tenant; uses the item's
    /// <c>StandardCost</c> as the planned unit cost.
    /// </summary>
    Task<Result<WorkOrderPart>> AddPlannedMaterialAsync(AddPlannedMaterialRequest request, CancellationToken ct);

    // === Phase 2 (Sprint 12.9 PR #3.1) — operation-level JE-posting writes ===

    /// <summary>
    /// Record a labor entry against a work-order operation. Bumps the operation's
    /// <c>ActualHours</c> and posts a balanced <c>WO-LBR</c> journal entry
    /// (DR MaintenanceLabor / CR AccruedLabor) at the operation's resolved
    /// company. Zero-hours or zero-rate entries skip the JE.
    /// </summary>
    Task<Result<WorkOrderOperation>> AddLaborAsync(AddLaborRequest request, CancellationToken ct);

    /// <summary>
    /// Issue an operation-level part. Decrements inventory at
    /// <c>IssuedFromLocationId</c> (or creates the stock row if absent),
    /// writes an <see cref="ItemTransaction"/> audit row, and posts a balanced
    /// <c>WO-ISS-OP</c> journal entry (DR MaintenanceMaterials / CR Inventory)
    /// at the part's <c>UnitCost</c>. Bounded by planned quantity when
    /// planned &gt; 0; otherwise auto-extends planned to match (unplanned pull).
    /// </summary>
    Task<Result<WorkOrderOperationPart>> IssueOperationPartAsync(IssueOperationPartRequest request, CancellationToken ct);

    /// <summary>
    /// Return an operation-level part. Reverses the inventory movement +
    /// posts a reversing <c>WO-RTN-OP</c> journal entry. Bounded by net
    /// issued (<c>QuantityIssued - QuantityReturned</c>) — over-return
    /// requests are silently capped.
    /// </summary>
    Task<Result<WorkOrderOperationPart>> ReturnOperationPartAsync(ReturnOperationPartRequest request, CancellationToken ct);

    // === Phase 3 (Sprint 12.9 PR #3.2) — WO-header material writes ===

    /// <summary>
    /// Add an operation-level part (<see cref="WorkOrderOperationPart"/>) at
    /// an explicit unit cost. Distinct from the broader
    /// <see cref="IssueOperationPartAsync"/> flow because this method only
    /// PLANS the part — no inventory movement, no JE posting.
    /// </summary>
    Task<Result<WorkOrderOperation>> AddOperationPartAsync(AddOperationPartRequest request, CancellationToken ct);

    /// <summary>
    /// Issue a WO-header material. Decrements inventory, writes
    /// <see cref="ItemTransaction"/>, posts <c>WO-ISS</c> JE
    /// (DR MaintenanceMaterials / CR Inventory), and enqueues an
    /// <c>ItemIssuedV1</c> outbox event for downstream consumers.
    /// </summary>
    Task<Result<WorkOrderPart>> IssueMaterialAsync(IssueMaterialRequest request, CancellationToken ct);

    /// <summary>
    /// Return a WO-header material. Reverses inventory + posts <c>WO-RTN</c>
    /// reversing JE. Capped at net issued.
    /// </summary>
    Task<Result<WorkOrderPart>> ReturnMaterialAsync(ReturnMaterialRequest request, CancellationToken ct);

    /// <summary>
    /// Remove a planned (not yet issued) WO-header material. Rejected if
    /// <c>QuantityIssued &gt; 0</c> — already-issued parts must be returned,
    /// not removed. Returns the WO id so callers can redirect.
    /// </summary>
    Task<Result<int>> RemovePlannedMaterialAsync(RemovePlannedMaterialRequest request, CancellationToken ct);

    /// <summary>
    /// Bulk-load planned materials onto a work order from its linked PM
    /// Template. Resolves the template via the WO's <c>PMTemplateAssetId</c>
    /// FK (or the legacy <c>CustomField1</c> "PMTA:" marker). Skips items
    /// that already exist on the WO. Returns a structured outcome the
    /// caller maps to TempData messages.
    /// </summary>
    Task<Result<LoadTemplateMaterialsOutcome>> LoadTemplateMaterialsAsync(LoadTemplateMaterialsRequest request, CancellationToken ct);

    // === Phase 4 (Sprint 12.9 PR #3.3 — final) — WO-level writes ===

    /// <summary>
    /// Edit a work order's header fields (type / priority / schedule /
    /// vendor / technician / cost / description / notes / failure code).
    /// Rejected if the WO is already Completed or Cancelled.
    /// </summary>
    Task<Result<EditWorkOrderOutcome>> EditWorkOrderAsync(EditWorkOrderRequest request, CancellationToken ct);

    /// <summary>
    /// Quick dispatch reassignment: priority + scheduled date + technician.
    /// Narrower than <see cref="EditWorkOrderAsync"/>. Delegates the actual
    /// update to <c>MaintenanceService.UpdateDispatchAsync</c> and persists
    /// the priority-lookup FK alongside.
    /// </summary>
    Task<Result<DispatchUpdateOutcome>> DispatchUpdateAsync(DispatchUpdateRequest request, CancellationToken ct);

    /// <summary>
    /// Capitalize a completed work order's cost as a capital improvement
    /// against its linked asset. Increments <c>Asset.AcquisitionCost</c>,
    /// creates a <see cref="CapitalImprovement"/> row, stamps
    /// <c>WO.CustomField2 = "IMPR:{improvementId}"</c>, posts the JE via
    /// <c>ICapitalImprovementPostingService</c>, and refreshes the asset's
    /// depreciation snapshot. Many guardrails — see <see cref="CapitalizeOutcome"/>.
    /// </summary>
    Task<Result<CapitalizeOutcome>> CapitalizeAsync(CapitalizeWorkOrderRequest request, CancellationToken ct);
}

// === Request DTOs ===

/// <summary>Inputs for <see cref="IWorkOrderService.AddOperationAsync"/>.</summary>
public sealed record AddOperationRequest(
    int WorkOrderId,
    string? Title,
    int TypeLookupValueId,
    int? CraftId,
    decimal PlannedHours,
    string? Description);

/// <summary>Inputs for <see cref="IWorkOrderService.MoveOperationAsync"/>.</summary>
public sealed record MoveOperationRequest(
    int OperationId,
    string Direction);

/// <summary>Inputs for <see cref="IWorkOrderService.UpdateOperationStatusAsync"/>.</summary>
public sealed record UpdateOperationStatusRequest(
    int OperationId,
    int StatusLookupValueId);

/// <summary>Inputs for <see cref="IWorkOrderService.AddOperationToolAsync"/>.</summary>
public sealed record AddOperationToolRequest(
    int OperationId,
    string? ToolName,
    int QuantityRequired,
    string? Notes);

/// <summary>Inputs for <see cref="IWorkOrderService.AddPlannedMaterialAsync"/>.</summary>
public sealed record AddPlannedMaterialRequest(
    int WorkOrderId,
    int ItemId,
    decimal QuantityPlanned,
    string? Notes);

/// <summary>Inputs for <see cref="IWorkOrderService.AddLaborAsync"/>.</summary>
public sealed record AddLaborRequest(
    int OperationId,
    int? TechnicianId,
    decimal Hours,
    decimal HourlyRate,
    string? Notes);

/// <summary>Inputs for <see cref="IWorkOrderService.IssueOperationPartAsync"/>.</summary>
public sealed record IssueOperationPartRequest(
    int OperationPartId,
    decimal QuantityIssue,
    string? IssuedBy);

/// <summary>Inputs for <see cref="IWorkOrderService.ReturnOperationPartAsync"/>.</summary>
public sealed record ReturnOperationPartRequest(
    int OperationPartId,
    decimal QuantityReturn);

/// <summary>Inputs for <see cref="IWorkOrderService.AddOperationPartAsync"/>.</summary>
public sealed record AddOperationPartRequest(
    int OperationId,
    int ItemId,
    decimal QuantityPlanned,
    decimal UnitCost,
    string? Notes);

/// <summary>Inputs for <see cref="IWorkOrderService.IssueMaterialAsync"/>.</summary>
public sealed record IssueMaterialRequest(
    int WorkOrderPartId,
    decimal QuantityIssue,
    string? IssuedBy);

/// <summary>Inputs for <see cref="IWorkOrderService.ReturnMaterialAsync"/>.</summary>
public sealed record ReturnMaterialRequest(
    int WorkOrderPartId,
    decimal QuantityReturn);

/// <summary>Inputs for <see cref="IWorkOrderService.RemovePlannedMaterialAsync"/>.</summary>
public sealed record RemovePlannedMaterialRequest(int WorkOrderPartId);

/// <summary>Inputs for <see cref="IWorkOrderService.LoadTemplateMaterialsAsync"/>.</summary>
public sealed record LoadTemplateMaterialsRequest(int WorkOrderId);

/// <summary>
/// Outcome of <see cref="IWorkOrderService.LoadTemplateMaterialsAsync"/>. The
/// PageModel translates <see cref="Status"/> into a TempData message slot
/// (Success / Warning / Error) to preserve the legacy UX.
/// </summary>
public sealed record LoadTemplateMaterialsOutcome(
    int WorkOrderId,
    int Added,
    LoadTemplateMaterialsStatus Status,
    string? Message);

/// <summary>Status flag for <see cref="LoadTemplateMaterialsOutcome"/>.</summary>
public enum LoadTemplateMaterialsStatus
{
    /// <summary>At least one item was added.</summary>
    Loaded,
    /// <summary>WO has no PM Template linked. Maps to TempData["Error"].</summary>
    NoTemplate,
    /// <summary>Template exists but has no materials. Maps to TempData["Warning"].</summary>
    EmptyTemplate,
    /// <summary>Every template item already exists on the WO. Maps to TempData["Warning"].</summary>
    AllAlreadyExist
}

// === Phase 4 (Sprint 12.9 PR #3.3) — WO-level requests + outcomes ===

/// <summary>Inputs for <see cref="IWorkOrderService.EditWorkOrderAsync"/>.</summary>
public sealed record EditWorkOrderRequest(
    int WorkOrderId,
    int MaintenanceTypeLookupValueId,
    int PriorityLookupValueId,
    DateTime ScheduledDate,
    string? WorkOrderNumber,
    string? Vendor,
    int? TechnicianId,
    decimal EstimatedCost,
    string? Description,
    string? Notes,
    int? FailureCodeId);

/// <summary>Outcome of <see cref="IWorkOrderService.EditWorkOrderAsync"/>.</summary>
public sealed record EditWorkOrderOutcome(
    int WorkOrderId,
    EditWorkOrderStatus Status,
    string? Message);

/// <summary>Status flag for <see cref="EditWorkOrderOutcome"/>.</summary>
public enum EditWorkOrderStatus
{
    /// <summary>WO header updated. Maps to TempData["Success"].</summary>
    Updated,
    /// <summary>WO is Completed or Cancelled. Maps to TempData["Error"].</summary>
    TerminalStateRejected
}

/// <summary>Inputs for <see cref="IWorkOrderService.DispatchUpdateAsync"/>.</summary>
public sealed record DispatchUpdateRequest(
    int WorkOrderId,
    int PriorityLookupValueId,
    DateTime ScheduledDate,
    int? TechnicianId);

/// <summary>Outcome of <see cref="IWorkOrderService.DispatchUpdateAsync"/>.</summary>
public sealed record DispatchUpdateOutcome(int WorkOrderId);

/// <summary>Inputs for <see cref="IWorkOrderService.CapitalizeAsync"/>.</summary>
public sealed record CapitalizeWorkOrderRequest(
    int WorkOrderId,
    decimal Amount,
    string Description,
    string? CreatedBy);

/// <summary>Outcome of <see cref="IWorkOrderService.CapitalizeAsync"/>.</summary>
public sealed record CapitalizeOutcome(
    int WorkOrderId,
    CapitalizeStatus Status,
    int? ImprovementId,
    string? AssetNumber,
    decimal Amount,
    string? Message);

/// <summary>Status flag for <see cref="CapitalizeOutcome"/>.</summary>
public enum CapitalizeStatus
{
    /// <summary>Capitalized + JE posted + depreciation refreshed. TempData["Success"].</summary>
    Capitalized,
    /// <summary>WO not Completed, or no asset link. TempData["Error"].</summary>
    NotEligible,
    /// <summary>Already capitalized (CustomField2 starts with "IMPR:"). TempData["Error"].</summary>
    AlreadyCapitalized,
    /// <summary>Amount &lt;= 0. TempData["Error"].</summary>
    InvalidAmount,
    /// <summary>Fiscal period closed. TempData["Error"].</summary>
    PeriodClosed,
    /// <summary>Improvement persisted but JE posting refused. TempData["Error"]. AcquisitionCost is already bumped.</summary>
    PostingRefused
}
