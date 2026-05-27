// B8 PR-PRO-5 (2026-05-27) — IProductionWipMoveService.
//
// WIP movement between operations with AUTO-ADVANCE as the DEFAULT.
// Complete quantity at Op 20 → instantly available at Op 30.
// Manual moves (send-back, skip, split) are the EXCEPTION.
//
// AS9100 §8.5.1 controlled conditions. Full audit trail.

using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;

namespace Abs.FixedAssets.Services.Production
{
    public interface IProductionWipMoveService
    {
        /// <summary>
        /// AUTO-ADVANCE: Called internally when an operation completes quantity.
        /// Creates a WipMove record, updates the next operation's AvailableQty.
        /// If the next operation has a quality hold, the move is created with
        /// Status=Pending and QualityHoldBlocked=true.
        /// THIS IS THE DEFAULT FLOW — no operator action required.
        /// </summary>
        Task<Result<ProductionWipMove>> AutoAdvanceOnCompletionAsync(
            int fromOperationId,
            decimal quantity,
            int? triggeringTransactionId,
            string movedBy,
            CancellationToken ct = default);

        /// <summary>
        /// MANUAL: Explicitly move quantity to the next sequential operation.
        /// Used when auto-advance is disabled or for partial pre-completion moves.
        /// </summary>
        Task<Result<ProductionWipMove>> MoveToNextOperationAsync(
            int fromOperationId,
            decimal quantity,
            string? reason,
            string movedBy,
            CancellationToken ct = default);

        /// <summary>
        /// EXCEPTION: Send material back to a prior operation for rework.
        /// Reason is REQUIRED. Creates an audit trail linking the rework decision.
        /// </summary>
        Task<Result<ProductionWipMove>> SendBackToPriorOperationAsync(
            int fromOperationId,
            int toOperationId,
            decimal quantity,
            string reason,
            string movedBy,
            CancellationToken ct = default);

        /// <summary>
        /// EXCEPTION: Move to a specific (non-sequential) operation.
        /// For skip, parallel routing, or rework-insert destination.
        /// Reason is REQUIRED.
        /// </summary>
        Task<Result<ProductionWipMove>> MoveToSpecificOperationAsync(
            int fromOperationId,
            int toOperationId,
            decimal quantity,
            string reason,
            string movedBy,
            CancellationToken ct = default);

        /// <summary>
        /// EXCEPTION: Place a quality hold on an operation, blocking auto-advance.
        /// Any pending auto-advance moves are held until released.
        /// </summary>
        Task<Result<ProductionOperation>> HoldAtOperationAsync(
            int operationId,
            string holdReason,
            string heldBy,
            CancellationToken ct = default);

        /// <summary>
        /// Release a quality hold. Executes any pending auto-advance moves.
        /// </summary>
        Task<Result<ProductionOperation>> ReleaseHoldAsync(
            int operationId,
            string releasedBy,
            CancellationToken ct = default);

        /// <summary>
        /// Reverse a prior WIP move. Creates a counter-move record.
        /// The original move is marked Reversed. Quantity adjustments applied.
        /// </summary>
        Task<Result<ProductionWipMove>> ReverseMoveAsync(
            int moveId,
            string reason,
            string reversedBy,
            CancellationToken ct = default);

        /// <summary>Get all WIP moves for a production order.</summary>
        Task<IReadOnlyList<ProductionWipMove>> GetMovesForOrderAsync(
            int productionOrderId,
            CancellationToken ct = default);

        /// <summary>Get all WIP moves involving a specific operation (from or to).</summary>
        Task<IReadOnlyList<ProductionWipMove>> GetMovesForOperationAsync(
            int operationId,
            CancellationToken ct = default);

        /// <summary>Get a single move by Id.</summary>
        Task<ProductionWipMove?> GetAsync(int moveId, CancellationToken ct = default);
    }
}
