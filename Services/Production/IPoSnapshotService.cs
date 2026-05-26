// Sprint 14.1 PR-1 (2026-05-26) — IPoSnapshotService.
//
// Per-Lock 15 — service surface only, no direct DbContext from PageModels.
// All ProductionMaterialStructure writes flow through this surface.
//
// Three operations:
//   - CaptureAsync — at PRO release, freeze the Item revision + every
//     MaterialStructureLine into ProductionMaterialStructure rows. Idempotent:
//     if a snapshot already exists for the PO, return the existing summary
//     without re-writing (no duplicate rows, no fingerprint drift).
//   - GetSnapshotAsync — read-only projection of the frozen snapshot for a
//     PRO. Used by /Admin/PoSnapshotProbe, the cost engine (Sprint 14.4),
//     MES material-issue (B8 PO Cockpit), and AS9100 §8.3 traceability.
//   - ClearSnapshotAsync — admin-only recovery path. Deletes snapshot rows
//     and nulls the header timestamps so a subsequent CaptureAsync can
//     re-freeze. Used ONLY when an operator captured against a stale BOM
//     by mistake and the PRO has not yet moved past Released.
//
// Result<T> envelope (ADR-014 §D2) — callers get a typed success result OR a
// structured error message + code so PageModels and voice intents can render
// the right user-facing copy without re-implementing the lookup logic.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production;

/// <summary>
/// Captures + reads the per-ProductionOrder frozen BOM snapshot
/// (<see cref="ProductionMaterialStructure"/>). See entity-level docs for
/// the architectural rationale.
/// </summary>
public interface IPoSnapshotService
{
    /// <summary>
    /// Capture a frozen snapshot of the PRO's source MaterialStructure +
    /// Item revision at the moment of release.
    ///
    /// Idempotent: if a snapshot already exists for the PRO (i.e.,
    /// <see cref="ProductionOrder.SnapshotCapturedAtUtc"/> is non-null),
    /// returns the existing summary without re-writing. Re-capture requires
    /// an explicit <see cref="ClearSnapshotAsync"/> first.
    ///
    /// Validates:
    ///   - PRO exists; otherwise returns NotFound.
    ///   - PRO has a <see cref="ProductionOrder.MaterialStructureId"/>; otherwise
    ///     returns BadRequest ("PRO has no MaterialStructure to snapshot").
    ///   - Source MaterialStructure has at least zero lines (empty BOM is a
    ///     warning — captured as an empty snapshot with header timestamps
    ///     stamped, so subsequent reads can distinguish "snapshotted with
    ///     no lines" from "never snapshotted").
    /// </summary>
    Task<Result<PoSnapshotSummary>> CaptureAsync(
        int productionOrderId,
        string capturedBy,
        CancellationToken ct);

    /// <summary>
    /// Read the frozen snapshot for a PRO. Returns the header timestamps +
    /// the frozen lines. Returns null lines (and null header) if the PRO has
    /// never been snapshotted.
    /// </summary>
    Task<PoSnapshotSummary> GetSnapshotAsync(
        int productionOrderId,
        CancellationToken ct);

    /// <summary>
    /// Admin-only recovery: delete all snapshot rows for a PRO and null the
    /// header timestamps so a subsequent <see cref="CaptureAsync"/> can
    /// re-freeze. Should NEVER be called on PROs past Released status.
    /// </summary>
    Task<Result<PoSnapshotSummary>> ClearSnapshotAsync(
        int productionOrderId,
        string clearedBy,
        string reason,
        CancellationToken ct);
}

/// <summary>
/// Read-only projection of a frozen PRO snapshot. Returned by all three
/// service methods so callers get a consistent shape.
/// </summary>
public sealed record PoSnapshotSummary(
    int ProductionOrderId,
    string OrderNumber,
    int? SourceMaterialStructureId,
    string? SourceMaterialStructureRevision,
    int? SourceItemRevisionId,
    string? SourceItemRevisionCode,
    System.DateTime? SnapshotCapturedAtUtc,
    string? SnapshotCapturedBy,
    System.Collections.Generic.IReadOnlyList<PoSnapshotLine> Lines);

/// <summary>
/// One frozen BOM line in the snapshot projection. Mirrors
/// <see cref="ProductionMaterialStructure"/> but read-only for callers.
/// </summary>
public sealed record PoSnapshotLine(
    int Id,
    int Sequence,
    int ChildItemId,
    string ChildPartNumber,
    string? ChildRevision,
    int? ChildItemRevisionId,
    string? ChildItemFingerprintHash,
    LineKind LineKind,
    BomIssueMethod IssueMethod,
    decimal QuantityPer,
    string? Uom,
    decimal? ScrapPercent,
    int? PhaseSequence,
    bool IsPhantom,
    decimal? FrozenStandardCost,
    decimal? FrozenExtendedCost,
    int? SourceMaterialStructureLineId,
    string? Notes);
