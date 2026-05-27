// B8 PR-PRO-5 (2026-05-27) — ProductionWipMoveService implementation.
// Auto-advance on completion as the DEFAULT. Manual moves as the EXCEPTION.
// xmin concurrency via MapXminRowVersion at the AppDbContext level.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public class ProductionWipMoveService : IProductionWipMoveService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProductionWipMoveService> _log;

        public ProductionWipMoveService(AppDbContext db, ILogger<ProductionWipMoveService> log)
        {
            _db = db;
            _log = log;
        }

        // ================================================================
        // AUTO-ADVANCE — the BIC default flow
        // ================================================================

        public async Task<Result<ProductionWipMove>> AutoAdvanceOnCompletionAsync(
            int fromOperationId, decimal quantity, int? triggeringTransactionId,
            string movedBy, CancellationToken ct = default)
        {
            if (quantity <= 0)
                return Result.Failure<ProductionWipMove>("Quantity must be positive.");

            var fromOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == fromOperationId, ct);
            if (fromOp == null)
                return Result.Failure<ProductionWipMove>($"Operation {fromOperationId} not found.");

            if (!fromOp.AutoAdvanceOnCompletion)
                return Result.Failure<ProductionWipMove>(
                    $"Auto-advance disabled on operation {fromOp.SequenceNumber}. Use manual move.");

            // Find the next sequential operation on the same PRO
            var nextOp = await _db.Set<ProductionOperation>()
                .Where(o => o.ProductionOrderId == fromOp.ProductionOrderId
                    && o.SequenceNumber > fromOp.SequenceNumber
                    && o.Status != ProductionOperationStatus.Skipped
                    && o.Status != ProductionOperationStatus.Scrapped)
                .OrderBy(o => o.SequenceNumber)
                .FirstOrDefaultAsync(ct);

            if (nextOp == null)
            {
                // This was the last operation — no move needed, quantity goes to FG receipt
                _log.LogInformation(
                    "Auto-advance: Op {Seq} is the final operation on PRO {OrderId}. " +
                    "{Qty} units ready for finished goods receipt.",
                    fromOp.SequenceNumber, fromOp.ProductionOrderId, quantity);
                return Result.Failure<ProductionWipMove>(
                    $"Operation {fromOp.SequenceNumber} is the final operation — " +
                    $"{quantity} units ready for finished goods receipt. No WIP move created.");
            }

            // Check if next op has a quality hold
            bool qualityBlocked = nextOp.QualityHoldActive;

            var move = CreateMoveRecord(fromOp, nextOp, quantity, WipMoveType.AutoAdvance,
                qualityBlocked ? WipMoveStatus.Pending : WipMoveStatus.Completed,
                "Auto-advance on completion", triggeringTransactionId, movedBy);

            if (qualityBlocked)
            {
                move.QualityHoldBlocked = true;
                move.QualityHoldReason = nextOp.QualityHoldReason;
                _log.LogInformation(
                    "Auto-advance HELD: Op {FromSeq}→{ToSeq} on PRO {OrderId}. " +
                    "{Qty} units pending quality hold release. Reason: {Reason}",
                    fromOp.SequenceNumber, nextOp.SequenceNumber,
                    fromOp.ProductionOrderId, quantity, nextOp.QualityHoldReason);
            }
            else
            {
                // Immediately update destination operation's available quantity
                nextOp.AvailableQty += quantity;
                nextOp.ModifiedAt = DateTime.UtcNow;
                nextOp.ModifiedBy = movedBy;
                _log.LogInformation(
                    "Auto-advance: Op {FromSeq}→{ToSeq} on PRO {OrderId}. " +
                    "{Qty} units now available at Op {ToSeq} (total available: {Available})",
                    fromOp.SequenceNumber, nextOp.SequenceNumber,
                    fromOp.ProductionOrderId, quantity, nextOp.SequenceNumber, nextOp.AvailableQty);
            }

            _db.Set<ProductionWipMove>().Add(move);
            await _db.SaveChangesAsync(ct);
            return Result.Success(move);
        }

        // ================================================================
        // MANUAL MOVE — explicit operator/supervisor action
        // ================================================================

        public async Task<Result<ProductionWipMove>> MoveToNextOperationAsync(
            int fromOperationId, decimal quantity, string? reason,
            string movedBy, CancellationToken ct = default)
        {
            if (quantity <= 0)
                return Result.Failure<ProductionWipMove>("Quantity must be positive.");

            var fromOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == fromOperationId, ct);
            if (fromOp == null)
                return Result.Failure<ProductionWipMove>($"Operation {fromOperationId} not found.");

            var nextOp = await _db.Set<ProductionOperation>()
                .Where(o => o.ProductionOrderId == fromOp.ProductionOrderId
                    && o.SequenceNumber > fromOp.SequenceNumber
                    && o.Status != ProductionOperationStatus.Skipped
                    && o.Status != ProductionOperationStatus.Scrapped)
                .OrderBy(o => o.SequenceNumber)
                .FirstOrDefaultAsync(ct);

            if (nextOp == null)
                return Result.Failure<ProductionWipMove>(
                    $"No next operation found after sequence {fromOp.SequenceNumber}.");

            var move = CreateMoveRecord(fromOp, nextOp, quantity, WipMoveType.ManualMoveNext,
                WipMoveStatus.Completed, reason ?? "Manual move to next operation", null, movedBy);

            nextOp.AvailableQty += quantity;
            nextOp.ModifiedAt = DateTime.UtcNow;
            nextOp.ModifiedBy = movedBy;

            _db.Set<ProductionWipMove>().Add(move);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Manual move: Op {FromSeq}→{ToSeq} on PRO {OrderId}. {Qty} units.",
                fromOp.SequenceNumber, nextOp.SequenceNumber,
                fromOp.ProductionOrderId, quantity);

            return Result.Success(move);
        }

        // ================================================================
        // SEND BACK — rework to a prior operation
        // ================================================================

        public async Task<Result<ProductionWipMove>> SendBackToPriorOperationAsync(
            int fromOperationId, int toOperationId, decimal quantity,
            string reason, string movedBy, CancellationToken ct = default)
        {
            if (quantity <= 0)
                return Result.Failure<ProductionWipMove>("Quantity must be positive.");
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure<ProductionWipMove>("Reason is REQUIRED for send-back moves.");

            var fromOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == fromOperationId, ct);
            if (fromOp == null)
                return Result.Failure<ProductionWipMove>($"From operation {fromOperationId} not found.");

            var toOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == toOperationId, ct);
            if (toOp == null)
                return Result.Failure<ProductionWipMove>($"To operation {toOperationId} not found.");

            if (toOp.ProductionOrderId != fromOp.ProductionOrderId)
                return Result.Failure<ProductionWipMove>(
                    "Cannot send back to an operation on a different production order.");

            if (toOp.SequenceNumber >= fromOp.SequenceNumber)
                return Result.Failure<ProductionWipMove>(
                    $"Send-back requires a PRIOR operation. Op {toOp.SequenceNumber} is not before Op {fromOp.SequenceNumber}.");

            var move = CreateMoveRecord(fromOp, toOp, quantity, WipMoveType.SendBack,
                WipMoveStatus.Completed, reason, null, movedBy);

            toOp.AvailableQty += quantity;
            toOp.ReworkQty += quantity;
            toOp.ModifiedAt = DateTime.UtcNow;
            toOp.ModifiedBy = movedBy;

            _db.Set<ProductionWipMove>().Add(move);
            await _db.SaveChangesAsync(ct);

            _log.LogWarning(
                "SEND-BACK: Op {FromSeq}→{ToSeq} on PRO {OrderId}. {Qty} units for rework. Reason: {Reason}",
                fromOp.SequenceNumber, toOp.SequenceNumber,
                fromOp.ProductionOrderId, quantity, reason);

            return Result.Success(move);
        }

        // ================================================================
        // MOVE TO SPECIFIC — non-sequential
        // ================================================================

        public async Task<Result<ProductionWipMove>> MoveToSpecificOperationAsync(
            int fromOperationId, int toOperationId, decimal quantity,
            string reason, string movedBy, CancellationToken ct = default)
        {
            if (quantity <= 0)
                return Result.Failure<ProductionWipMove>("Quantity must be positive.");
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure<ProductionWipMove>("Reason is REQUIRED for non-sequential moves.");

            var fromOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == fromOperationId, ct);
            if (fromOp == null)
                return Result.Failure<ProductionWipMove>($"From operation {fromOperationId} not found.");

            var toOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == toOperationId, ct);
            if (toOp == null)
                return Result.Failure<ProductionWipMove>($"To operation {toOperationId} not found.");

            if (toOp.ProductionOrderId != fromOp.ProductionOrderId)
                return Result.Failure<ProductionWipMove>(
                    "Cannot move to an operation on a different production order. Use job-to-job transfer.");

            if (toOp.Id == fromOp.Id)
                return Result.Failure<ProductionWipMove>("Cannot move to the same operation.");

            var move = CreateMoveRecord(fromOp, toOp, quantity, WipMoveType.MoveToSpecific,
                WipMoveStatus.Completed, reason, null, movedBy);

            toOp.AvailableQty += quantity;
            toOp.ModifiedAt = DateTime.UtcNow;
            toOp.ModifiedBy = movedBy;

            _db.Set<ProductionWipMove>().Add(move);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Non-sequential move: Op {FromSeq}→{ToSeq} on PRO {OrderId}. {Qty} units. Reason: {Reason}",
                fromOp.SequenceNumber, toOp.SequenceNumber,
                fromOp.ProductionOrderId, quantity, reason);

            return Result.Success(move);
        }

        // ================================================================
        // QUALITY HOLD / RELEASE
        // ================================================================

        public async Task<Result<ProductionOperation>> HoldAtOperationAsync(
            int operationId, string holdReason, string heldBy, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(holdReason))
                return Result.Failure<ProductionOperation>("Hold reason is REQUIRED.");

            var op = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == operationId, ct);
            if (op == null)
                return Result.Failure<ProductionOperation>($"Operation {operationId} not found.");
            if (op.QualityHoldActive)
                return Result.Failure<ProductionOperation>(
                    $"Operation {op.SequenceNumber} already on hold: {op.QualityHoldReason}");

            op.QualityHoldActive = true;
            op.QualityHoldReason = holdReason;
            op.ModifiedAt = DateTime.UtcNow;
            op.ModifiedBy = heldBy;
            await _db.SaveChangesAsync(ct);

            _log.LogWarning(
                "QUALITY HOLD placed on Op {Seq} PRO {OrderId} by {By}. Reason: {Reason}. " +
                "Auto-advance to this operation is now BLOCKED.",
                op.SequenceNumber, op.ProductionOrderId, heldBy, holdReason);

            return Result.Success(op);
        }

        public async Task<Result<ProductionOperation>> ReleaseHoldAsync(
            int operationId, string releasedBy, CancellationToken ct = default)
        {
            var op = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == operationId, ct);
            if (op == null)
                return Result.Failure<ProductionOperation>($"Operation {operationId} not found.");
            if (!op.QualityHoldActive)
                return Result.Failure<ProductionOperation>(
                    $"Operation {op.SequenceNumber} is not on hold.");

            op.QualityHoldActive = false;
            op.ModifiedAt = DateTime.UtcNow;
            op.ModifiedBy = releasedBy;

            // Execute any pending moves that were blocked by the hold
            var pendingMoves = await _db.Set<ProductionWipMove>()
                .Where(m => m.ToOperationId == operationId
                    && m.Status == WipMoveStatus.Pending
                    && m.QualityHoldBlocked)
                .ToListAsync(ct);

            decimal releasedQty = 0;
            foreach (var pending in pendingMoves)
            {
                pending.Status = WipMoveStatus.Completed;
                pending.QualityHoldReleasedAtUtc = DateTime.UtcNow;
                pending.QualityHoldReleasedBy = releasedBy;
                pending.UpdatedAt = DateTime.UtcNow;
                pending.UpdatedBy = releasedBy;
                releasedQty += pending.Quantity;
            }

            // Credit the released quantity to AvailableQty
            op.AvailableQty += releasedQty;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "QUALITY HOLD released on Op {Seq} PRO {OrderId} by {By}. " +
                "{Count} pending moves released, {Qty} units now available.",
                op.SequenceNumber, op.ProductionOrderId, releasedBy,
                pendingMoves.Count, releasedQty);

            return Result.Success(op);
        }

        // ================================================================
        // REVERSE MOVE
        // ================================================================

        public async Task<Result<ProductionWipMove>> ReverseMoveAsync(
            int moveId, string reason, string reversedBy, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure<ProductionWipMove>("Reason is REQUIRED for move reversal.");

            var original = await _db.Set<ProductionWipMove>()
                .FirstOrDefaultAsync(m => m.Id == moveId, ct);
            if (original == null)
                return Result.Failure<ProductionWipMove>($"Move {moveId} not found.");
            if (original.Status == WipMoveStatus.Reversed)
                return Result.Failure<ProductionWipMove>($"Move {moveId} already reversed.");
            if (original.Status == WipMoveStatus.Cancelled)
                return Result.Failure<ProductionWipMove>($"Move {moveId} is cancelled, cannot reverse.");

            // Create the counter-move
            var toOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == original.ToOperationId, ct);
            var fromOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == original.FromOperationId, ct);

            if (toOp == null || fromOp == null)
                return Result.Failure<ProductionWipMove>("Cannot reverse — operation records not found.");

            var reversal = new ProductionWipMove
            {
                CompanyId = original.CompanyId,
                TenantId = original.TenantId,
                MoveNumber = $"REV-{original.MoveNumber}",
                ProductionOrderId = original.ProductionOrderId,
                FromOperationId = original.ToOperationId,       // reversed direction
                ToOperationId = original.FromOperationId,       // reversed direction
                FromSequenceNumber = original.ToSequenceNumber,
                ToSequenceNumber = original.FromSequenceNumber,
                MoveType = WipMoveType.Reversal,
                Status = WipMoveStatus.Completed,
                Quantity = original.Quantity,
                UnitOfMeasure = original.UnitOfMeasure,
                MoveReason = $"Reversal of {original.MoveNumber}: {reason}",
                OriginalMoveId = original.Id,
                MovedBy = reversedBy,
                CreatedBy = reversedBy,
            };

            // Adjust quantities
            if (original.Status == WipMoveStatus.Completed)
            {
                toOp.AvailableQty = Math.Max(0, toOp.AvailableQty - original.Quantity);
                toOp.ModifiedAt = DateTime.UtcNow;
                toOp.ModifiedBy = reversedBy;
            }

            original.Status = WipMoveStatus.Reversed;
            original.UpdatedAt = DateTime.UtcNow;
            original.UpdatedBy = reversedBy;

            _db.Set<ProductionWipMove>().Add(reversal);
            await _db.SaveChangesAsync(ct);

            _log.LogWarning(
                "MOVE REVERSED: {OriginalNumber} (Op {FromSeq}→{ToSeq}) on PRO {OrderId}. {Qty} units. Reason: {Reason}",
                original.MoveNumber, original.FromSequenceNumber, original.ToSequenceNumber,
                original.ProductionOrderId, original.Quantity, reason);

            return Result.Success(reversal);
        }

        // ================================================================
        // READS
        // ================================================================

        public async Task<IReadOnlyList<ProductionWipMove>> GetMovesForOrderAsync(
            int productionOrderId, CancellationToken ct = default)
            => await _db.Set<ProductionWipMove>()
                .Where(m => m.ProductionOrderId == productionOrderId)
                .OrderByDescending(m => m.MovedAtUtc)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<ProductionWipMove>> GetMovesForOperationAsync(
            int operationId, CancellationToken ct = default)
            => await _db.Set<ProductionWipMove>()
                .Where(m => m.FromOperationId == operationId || m.ToOperationId == operationId)
                .OrderByDescending(m => m.MovedAtUtc)
                .ToListAsync(ct);

        public async Task<ProductionWipMove?> GetAsync(int moveId, CancellationToken ct = default)
            => await _db.Set<ProductionWipMove>().FindAsync(new object[] { moveId }, ct);

        // ================================================================
        // HELPER — create a WipMove record with collision-resistant numbering
        // ================================================================

        private ProductionWipMove CreateMoveRecord(
            ProductionOperation fromOp, ProductionOperation toOp,
            decimal quantity, WipMoveType moveType, WipMoveStatus status,
            string? reason, int? triggeringTransactionId, string movedBy)
        {
            var ts = DateTime.UtcNow;
            var moveNumber = $"WM-{ts:yyyyMMddHHmmss}-{fromOp.ProductionOrderId}-{fromOp.SequenceNumber}-{toOp.SequenceNumber}";

            return new ProductionWipMove
            {
                CompanyId = fromOp.CompanyIdSnapshot,
                ProductionOrderId = fromOp.ProductionOrderId,
                FromOperationId = fromOp.Id,
                ToOperationId = toOp.Id,
                FromSequenceNumber = fromOp.SequenceNumber,
                ToSequenceNumber = toOp.SequenceNumber,
                MoveType = moveType,
                Status = status,
                Quantity = quantity,
                MoveReason = reason,
                TriggeredByTransactionId = triggeringTransactionId,
                MovedAtUtc = ts,
                MovedBy = movedBy,
                CreatedBy = movedBy,
                MoveNumber = moveNumber,
            };
        }
    }
}
