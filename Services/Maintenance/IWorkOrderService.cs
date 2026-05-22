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
