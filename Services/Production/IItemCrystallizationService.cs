// Theme B7 Wave B PR-5 (2026-05-29) — IItemCrystallizationService.
//
// THE BIC DIFFERENTIATOR. Crystallization-at-ship: promote a master-optional
// (PoFirst / ETO) Production Order's AS-BUILT BOM + AS-RUN routing + ACTUAL cost
// into a reusable Item Master standard — OR dedupe-and-link to an existing one
// (always human-confirmed, decision #3) — the instant the job ships.
//
// No incumbent ERP harvests a costed, plannable standard from as-run actuals at
// ship (§1 of the research spec). This service is the claim.
//
// Three ops:
//   - PreviewCrystallizationAsync — read-only. Shows the would-be Item + standard
//     BOM + standard Routing + seeded cost, AND the dedupe match preview
//     ("matches TI-BRKT-0042 Rev C — link instead of create?").
//   - CrystallizeAsync — atomic mint (or dedupe-link). Cross-service transaction
//     enlistment per HARD LOCK. Sets ProductionOrder.CrystallizedItemId. Writes
//     the ItemCrystallization audit record. Compliance-guarded (§7): never mint a
//     part#/rev that contradicts the as-built record; dedupe is NEVER auto-linked.
//   - ReverseCrystallizationAsync — reversible; unlinks the PRO + marks the event
//     reversed. NEVER rewrites the as-built history (ProductionMaterialStructure /
//     ProductionOperation are untouched).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production;

// ── DTOs ────────────────────────────────────────────────────────────

/// <summary>A would-be standard BOM line in a crystallization preview.</summary>
public sealed record CrystallizationBomLinePreview(
    int Sequence, int ChildItemId, string ChildPartNumber, string? ChildRevision,
    decimal QuantityPer, string? Uom, LineKind LineKind);

/// <summary>A would-be standard routing operation in a crystallization preview.</summary>
public sealed record CrystallizationRoutingOpPreview(
    int Sequence, int WorkCenterId, ProductionOperationType OperationType,
    string Description, decimal SetupMins, decimal RunMinsPerUnit);

/// <summary>
/// Read-only projection of what a crystallization WOULD produce, plus the dedupe
/// match preview. No writes occur.
/// </summary>
public sealed record CrystallizationPreview(
    int SourceProductionOrderId,
    string OrderNumber,
    bool IsPoFirst,
    string? ProposedPartNumber,
    string? ProposedRevision,
    string? ProposedDescription,
    string StructureFingerprintHash,
    decimal? SeededStandardCost,
    CrystallizationCostSource CostSource,
    IReadOnlyList<CrystallizationBomLinePreview> BomLines,
    IReadOnlyList<CrystallizationRoutingOpPreview> RoutingOps,
    // Dedupe match (null when no fingerprint hit)
    int? DedupeMatchItemId,
    string? DedupeMatchPartNumber,
    int? DedupeMatchCrystallizationId,
    bool AlreadyCrystallized,
    int? ExistingCrystallizedItemId,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Request to crystallize. When a dedupe match exists the caller MUST resolve it
/// explicitly (decision #3 — never auto-link): set <see cref="ConfirmDedupeLink"/>
/// + <see cref="LinkToExistingItemId"/> to link, or <see cref="ForceCreateNew"/>
/// to mint a new master anyway. With neither, the service returns a
/// confirmation-required failure rather than guessing.
/// </summary>
public sealed record CrystallizeRequest(
    int ProductionOrderId,
    string By,
    bool ConfirmDedupeLink = false,
    int? LinkToExistingItemId = null,
    bool ForceCreateNew = false,
    string? RationaleOverride = null);

/// <summary>Outcome of a crystallization.</summary>
public sealed record CrystallizeResult(
    int CrystallizationId,
    string CrystallizationNumber,
    CrystallizationOutcome Outcome,
    int? CreatedItemId,
    int? MatchedItemId,
    int? MaterialStructureId,
    int? RoutingId,
    decimal? SeededStandardCost,
    string Message);

public sealed record ReverseCrystallizationResult(
    int CrystallizationId, string CrystallizationNumber, string Message);

// ── Service ─────────────────────────────────────────────────────────

public interface IItemCrystallizationService
{
    /// <summary>Read-only preview of the would-be master + dedupe match. No writes.</summary>
    Task<Result<CrystallizationPreview>> PreviewCrystallizationAsync(
        int productionOrderId, CancellationToken ct = default);

    /// <summary>
    /// Atomic crystallize: mint Item + standard BOM + standard Routing + seeded
    /// standard cost, OR link to a human-confirmed dedupe match. Sets
    /// ProductionOrder.CrystallizedItemId and writes the ItemCrystallization audit
    /// record. Cross-service-transaction-safe.
    /// </summary>
    Task<Result<CrystallizeResult>> CrystallizeAsync(
        CrystallizeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Reverse a crystallization: unlink the PRO and mark the event reversed. The
    /// as-built records are never rewritten; the minted master (if any) is retained
    /// as an independent record.
    /// </summary>
    Task<Result<ReverseCrystallizationResult>> ReverseCrystallizationAsync(
        int crystallizationId, string reason, string by, CancellationToken ct = default);
}
