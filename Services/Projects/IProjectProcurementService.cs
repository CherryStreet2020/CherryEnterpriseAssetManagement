// Theme B9 Wave 4 PR-10 (2026-05-30) — IProjectProcurementService. OPENS Wave 4.
//
// The buy-side execution layer: plans → firm commitments → receipts, all pegged
// to a CustomerProject. Hosts the close gate:
//   - CloseProject: a project cannot be CLOSED while it has OPEN commitments
//     (Open / PartiallyReceived) unless the caller explicitly waives them.
//
// ADR-025: PageModels / voice read THROUGH this service, never AppDbContext.
// Tenant scope flows THROUGH the parent CustomerProject (these entities have no
// CompanyId). Every incoming FK on a write is tenant-scoped to the project's
// company (session-30 lesson).

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Services.Projects;

public interface IProjectProcurementService
{
    /// <summary>Plans, commitments (with received-to-date + open balance), receipts, and totals (read).</summary>
    Task<Result<ProjectProcurementView>> GetProcurementAsync(int projectId, CancellationToken ct = default);

    Task<Result<int>> CreatePlanAsync(CreateProcurementPlanRequest req, CancellationToken ct = default);

    /// <summary>Raise a firm commitment. Tenant-scopes every incoming FK (plan / phase / PO / vendor).</summary>
    Task<Result<int>> CreateCommitmentAsync(CreateCommitmentRequest req, CancellationToken ct = default);

    /// <summary>Record value received against a commitment; auto-advances its status (Open→Partial→Received).</summary>
    Task<Result<ProjectCommitment>> RecordReceiptAsync(RecordReceiptRequest req, CancellationToken ct = default);

    /// <summary>Manually close a commitment (set-once close stamp).</summary>
    Task<Result<ProjectCommitment>> CloseCommitmentAsync(int commitmentId, string? closedBy = null, CancellationToken ct = default);

    /// <summary>Peg an existing PurchaseOrder to a project (and optionally a WBS phase).</summary>
    Task<Result<int>> LinkPurchaseOrderToProjectAsync(LinkPurchaseOrderRequest req, CancellationToken ct = default);

    /// <summary>The codes of commitments still open on a project (empty ⇒ safe to close).</summary>
    Task<Result<IReadOnlyList<string>>> GetOpenCommitmentsAsync(int projectId, CancellationToken ct = default);

    /// <summary>
    /// Close a project. Blocked while any commitment is Open / PartiallyReceived
    /// unless <c>WaiveOpenCommitments</c> is set. Transitions Active/OnHold→Closed
    /// and stamps <c>ClosedAt</c>.
    /// </summary>
    Task<Result<CustomerProject>> CloseProjectAsync(CloseProjectRequest req, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Read DTOs
// ---------------------------------------------------------------------------

public sealed record ProjectProcurementView(
    int ProjectId,
    decimal PlannedTotal,
    decimal CommittedTotal,
    decimal ReceivedTotal,
    decimal OpenCommitmentTotal,
    int OpenCommitmentCount,
    IReadOnlyList<ProcurementPlanRow> Plans,
    IReadOnlyList<CommitmentRow> Commitments,
    IReadOnlyList<ReceiptRow> Receipts);

public sealed record ProcurementPlanRow(
    int Id,
    string Code,
    string Name,
    ProjectProcurementCategory Category,
    ProjectProcurementPlanStatus Status,
    decimal? PlannedAmount,
    decimal? PlannedQuantity,
    string? UnitOfMeasure,
    System.DateTime? NeedByDate,
    bool IsLongLead,
    int? ProjectPhaseId,
    int? ItemId,
    decimal CommittedAgainstPlan);

public sealed record CommitmentRow(
    int Id,
    string Code,
    string? Description,
    ProjectCommitmentType CommitmentType,
    ProjectCommitmentStatus Status,
    decimal CommittedAmount,
    decimal ReceivedToDate,
    decimal OpenBalance,
    bool IsOpen,
    int? VendorId,
    int? PurchaseOrderId,
    int? ProjectProcurementPlanId,
    System.DateTime? ExpectedReceiptDate);

public sealed record ReceiptRow(
    int Id,
    int ProjectCommitmentId,
    string? ReceiptNumber,
    decimal ReceivedAmount,
    decimal? ReceivedQuantity,
    System.DateTime ReceiptDate,
    int? GoodsReceiptId);

// ---------------------------------------------------------------------------
// Write DTOs
// ---------------------------------------------------------------------------

public sealed record CreateProcurementPlanRequest(
    int CustomerProjectId,
    string Code,
    string Name,
    string? Description = null,
    ProjectProcurementCategory Category = ProjectProcurementCategory.Material,
    decimal? PlannedAmount = null,
    decimal? PlannedQuantity = null,
    string? UnitOfMeasure = null,
    string? Currency = null,
    System.DateTime? NeedByDate = null,
    bool IsLongLead = false,
    int? ProjectPhaseId = null,
    int? ItemId = null,
    int SortOrder = 0,
    string? CreatedBy = null);

public sealed record CreateCommitmentRequest(
    int CustomerProjectId,
    string Code,
    decimal CommittedAmount,
    string? Description = null,
    ProjectCommitmentType CommitmentType = ProjectCommitmentType.PurchaseOrder,
    string? Currency = null,
    decimal? CommittedQuantity = null,
    string? UnitOfMeasure = null,
    int? ProjectProcurementPlanId = null,
    int? ProjectPhaseId = null,
    int? PurchaseOrderId = null,
    int? VendorId = null,
    System.DateTime? CommittedDate = null,
    System.DateTime? ExpectedReceiptDate = null,
    string? CreatedBy = null);

public sealed record RecordReceiptRequest(
    int ProjectCommitmentId,
    decimal ReceivedAmount,
    decimal? ReceivedQuantity = null,
    string? ReceiptNumber = null,
    int? GoodsReceiptId = null,
    System.DateTime? ReceiptDate = null,
    string? Notes = null,
    string? CreatedBy = null);

public sealed record LinkPurchaseOrderRequest(
    int PurchaseOrderId,
    int CustomerProjectId,
    int? ProjectPhaseId = null);

public sealed record CloseProjectRequest(
    int CustomerProjectId,
    bool WaiveOpenCommitments = false,
    string? ClosedBy = null);
