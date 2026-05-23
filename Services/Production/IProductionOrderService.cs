// ADR-025 D5 / Sprint 13.5 PR #3 — IProductionOrderService.
//
// First mutation surface for ProductionOrder (ADR-013 PR #119.12). Greenfield
// — no PageModels mutate ProductionOrders today; PR #3 establishes the
// canonical service before PR #4 builds the /Production UI shell on top of it.
//
// Scope discipline (Sprint 12.9 WorkOrderService precedent — first PR is the
// minimum to make the UI work; JE/inventory writes phase in):
//   IN  — Create / UpdateHeader / UpdateStatus / AssignToProject /
//         UnassignFromProject. These are the five mutations PR #4 list +
//         detail views need on day one.
//   OUT — IssueMaterial / CompleteQuantity / ScrapQuantity (Sprint 14 PR
//         when the JE-posting infrastructure for production is in place,
//         matching the WorkOrderService PR #3.1-#3.3 cadence).
//   OUT — Bulk operations (release-set, scrap-batch) — PR #4 polish.
//
// AssignToProjectAsync delegates to ICustomerProjectService.LinkProductionOrderAsync
// so the FK mutation + CONTAINS_PRODUCTION_ORDER chain edge stay in ONE place.
// UnassignFromProjectAsync nulls the FK trio directly; no chain teardown
// because the chain-of-custody graph is append-only by design (history
// survives unlink).
//
// References:
//   - ADR-013 (ProductionOrder header)
//   - ADR-014 §D2 (Result<T>)
//   - ADR-022 (chain-of-custody graph)
//   - ADR-025 D5 (service-layer-first / ControlPlaneAnalyzer)
//   - ADR-026 (Seven Customer Modes)
//   - Models/Production/ProductionOrder.cs (entity surface)
//   - Models/Production/ProductionType.cs (Status + Type enums)
//   - Services/Projects/ICustomerProjectService.cs (PR #2 link delegate)

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Production;

/// <summary>
/// Domain service for <see cref="ProductionOrder"/> mutations (Sprint 13.5 PR #3).
/// PageModel and voice-intent callers inject <see cref="IProductionOrderService"/>
/// instead of <c>AppDbContext</c> for any mutation; reads can still go through the
/// PageModel's own <c>_context</c> for thin display projections.
/// </summary>
public interface IProductionOrderService
{
    /// <summary>
    /// Create a new <see cref="ProductionOrder"/> in <see cref="ProductionOrderStatus.Planned"/>.
    /// Validates the caller-supplied <c>OrderNumber</c> is unique, the
    /// Location (or Customer, if no Location) is tenant-visible, and the
    /// principal Item belongs to the same company. Stamps audit fields.
    /// Emits a <c>ProductionOrder</c> chain node (label = OrderNumber) so
    /// downstream services (PR #6 subcontract chain, future receiving
    /// PRODUCED_BY edges) can attach edges from day one.
    /// </summary>
    Task<Result<ProductionOrder>> CreateAsync(CreateProductionOrderRequest request, CancellationToken ct);

    /// <summary>
    /// Update editable header fields on a <see cref="ProductionOrder"/> —
    /// title, description, scheduled dates, priority, quantity-ordered,
    /// UoM, principal Item, Location, Customer, MasterProductionOrderId,
    /// MaterialStructureId. Rejects mutations on orders whose
    /// <see cref="ProductionOrderStatus"/> is Completed or Cancelled
    /// (terminal). Does NOT touch project/phase/posting-mode fields —
    /// those go through <see cref="AssignToProjectAsync"/>.
    /// </summary>
    Task<Result<ProductionOrder>> UpdateHeaderAsync(UpdateProductionOrderHeaderRequest request, CancellationToken ct);

    /// <summary>
    /// Transition a <see cref="ProductionOrder"/>'s <see cref="ProductionOrderStatus"/>.
    /// Enforces the legal-transition map:
    /// Planned→Released|Cancelled; Released→InProgress|OnHold|Cancelled;
    /// InProgress→OnHold|Completed|Cancelled; OnHold→Released|InProgress|Cancelled;
    /// Completed and Cancelled are terminal. Stamps <c>ActualStart</c>
    /// on first entry to InProgress, <c>ActualEnd</c> on Completed.
    /// </summary>
    Task<Result<ProductionOrder>> UpdateStatusAsync(UpdateProductionOrderStatusRequest request, CancellationToken ct);

    /// <summary>
    /// Assign this <see cref="ProductionOrder"/> to a <see cref="CustomerProject"/>
    /// (and optionally a phase within it). Delegates to
    /// <see cref="Abs.FixedAssets.Services.Projects.ICustomerProjectService.LinkProductionOrderAsync"/>
    /// so the FK mutation, posting-mode requirement, tenant-scope check,
    /// and <c>CONTAINS_PRODUCTION_ORDER</c> chain edge stay in one place.
    /// Lets the ProductionOrder edit screen call from the job-side without
    /// duplicating the link logic.
    /// </summary>
    Task<Result<ProductionOrder>> AssignToProjectAsync(AssignToProjectRequest request, CancellationToken ct);

    /// <summary>
    /// Unassign this <see cref="ProductionOrder"/> from its
    /// <see cref="CustomerProject"/> (and phase, and posting mode). Nulls
    /// the FK trio. Does NOT emit a chain-teardown edge — the
    /// chain-of-custody graph (ADR-022) is append-only by design;
    /// historical project membership survives the unlink. Rejected if
    /// the project is Closed/Cancelled and the caller isn't admin (admin
    /// override is a Sprint 16 feature — until then anyone can unlink
    /// from active/onhold/quote projects only).
    /// </summary>
    Task<Result<ProductionOrder>> UnassignFromProjectAsync(UnassignFromProjectRequest request, CancellationToken ct);
}

// === Request DTOs ===

/// <summary>Inputs for <see cref="IProductionOrderService.CreateAsync"/>.</summary>
public sealed record CreateProductionOrderRequest(
    string OrderNumber,
    ProductionType Type,
    string Title,
    string? Description,
    int? ItemId,
    int? LocationId,
    int? CustomerId,
    decimal QuantityOrdered,
    string? Uom,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    int Priority,
    int? MasterProductionOrderId,
    int? MaterialStructureId,
    string? CreatedBy);

/// <summary>Inputs for <see cref="IProductionOrderService.UpdateHeaderAsync"/>.</summary>
public sealed record UpdateProductionOrderHeaderRequest(
    int ProductionOrderId,
    string Title,
    string? Description,
    int? ItemId,
    int? LocationId,
    int? CustomerId,
    decimal QuantityOrdered,
    string? Uom,
    DateTime? ScheduledStart,
    DateTime? ScheduledEnd,
    int Priority,
    int? MasterProductionOrderId,
    int? MaterialStructureId,
    string? ModifiedBy);

/// <summary>Inputs for <see cref="IProductionOrderService.UpdateStatusAsync"/>.</summary>
public sealed record UpdateProductionOrderStatusRequest(
    int ProductionOrderId,
    ProductionOrderStatus NewStatus,
    string? ModifiedBy);

/// <summary>Inputs for <see cref="IProductionOrderService.AssignToProjectAsync"/>.</summary>
public sealed record AssignToProjectRequest(
    int ProductionOrderId,
    int CustomerProjectId,
    int? ProjectPhaseId,
    ProjectPostingMode PostingMode,
    string? ModifiedBy);

/// <summary>Inputs for <see cref="IProductionOrderService.UnassignFromProjectAsync"/>.</summary>
public sealed record UnassignFromProjectRequest(
    int ProductionOrderId,
    string? ModifiedBy);
