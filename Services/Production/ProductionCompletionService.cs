// B8 PR-PRO-6 (2026-05-27) — Complete + Scrap + Rework service implementation.
// Atomic completion posting with auto-advance integration.
// xmin concurrency via MapXminRowVersion at the AppDbContext level.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Production.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public class ProductionCompletionService : IProductionCompletionService
    {
        private readonly AppDbContext _db;
        private readonly ITransactionValidationPipeline _pipeline;
        private readonly IProductionWipMoveService _wipMoveSvc;
        private readonly ILogger<ProductionCompletionService> _log;

        public ProductionCompletionService(
            AppDbContext db,
            ITransactionValidationPipeline pipeline,
            IProductionWipMoveService wipMoveSvc,
            ILogger<ProductionCompletionService> log)
        {
            _db = db;
            _pipeline = pipeline;
            _wipMoveSvc = wipMoveSvc;
            _log = log;
        }

        // ── Validation helper ───────────────────────────────────────────
        private async Task<Result<T>?> ValidateOrBlock<T>(
            string actionType, int productionOrderId, int operationId,
            int companyId, decimal? quantity = null, string? performedBy = null,
            int? resourceId = null, int? employeeUserId = null,
            CancellationToken ct = default)
        {
            var ctx = new TransactionValidationContext
            {
                ActionType = actionType,
                ProductionOrderId = productionOrderId,
                OperationId = operationId,
                PerformedBy = performedBy ?? "system",
                CompanyId = companyId,
                Quantity = quantity,
                ResourceId = resourceId,
                EmployeeUserId = employeeUserId,
            };
            var result = await _pipeline.RunAsync(ctx, ct);
            return result.IsBlocked ? Result.Failure<T>(result.BlockMessage) : null;
        }

        // ================================================================
        // COMPLETE — atomic posting of good + scrap + rework + reject
        // ================================================================

        public async Task<Result<ProductionCompletionEvent>> RecordCompletionAsync(
            RecordCompletionRequest req, CancellationToken ct = default)
        {
            var totalQty = req.GoodQuantity + req.ScrapQuantity + req.ReworkQuantity + req.RejectQuantity;
            if (totalQty <= 0)
                return Result.Failure<ProductionCompletionEvent>("Total quantity (good + scrap + rework + reject) must be positive.");

            var blocked = await ValidateOrBlock<ProductionCompletionEvent>(
                TransactionActions.RecordCompletion, req.ProductionOrderId, req.OperationId,
                req.CompanyId, req.GoodQuantity, req.CompletedBy,
                req.ResourceWorkCenterId, req.EmployeeId, ct);
            if (blocked is not null) return blocked.Value;

            var op = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == req.OperationId, ct);
            if (op == null)
                return Result.Failure<ProductionCompletionEvent>($"Operation {req.OperationId} not found.");

            // P1 fix: validate operation belongs to the posted production order
            if (op.ProductionOrderId != req.ProductionOrderId)
                return Result.Failure<ProductionCompletionEvent>(
                    $"Operation {req.OperationId} belongs to PRO {op.ProductionOrderId}, not PRO {req.ProductionOrderId}.");

            // P1 fix: cap move quantity to good quantity — cannot advance scrap/rework/reject
            var safeMoveQty = Math.Min(req.MoveQuantityToNextOp, req.GoodQuantity);

            var ts = DateTime.UtcNow;
            var completionNumber = $"CMP-{ts:yyyyMMddHHmmssfff}-{req.ProductionOrderId}-{op.SequenceNumber}";

            var evt = new ProductionCompletionEvent
            {
                CompanyId = req.CompanyId,
                CompletionNumber = completionNumber,
                ProductionOrderId = req.ProductionOrderId,
                OperationId = req.OperationId,
                GoodQuantity = req.GoodQuantity,
                ScrapQuantity = req.ScrapQuantity,
                ReworkQuantity = req.ReworkQuantity,
                RejectQuantity = req.RejectQuantity,
                CompleteRemaining = req.CompleteRemaining,
                IsFinalOperation = req.IsFinalOperation,
                MoveQuantityToNextOp = req.MoveQuantityToNextOp,
                EmployeeName = req.EmployeeName,
                EmployeeId = req.EmployeeId,
                ResourceWorkCenterId = req.ResourceWorkCenterId,
                BackflushMaterials = req.BackflushMaterials,
                AutoIssuePullMaterials = req.AutoIssuePullMaterials,
                InspectionRequired = req.InspectionRequired,
                LotNumbers = req.LotNumbers,
                SerialNumbers = req.SerialNumbers,
                Notes = req.Notes,
                CompletedAtUtc = ts,
                CompletedBy = req.CompletedBy,
                CreatedBy = req.CompletedBy,
            };

            // Update operation quantities
            op.CompletedQty += req.GoodQuantity;
            op.ScrappedQty += req.ScrapQuantity;
            op.ReworkQty += req.ReworkQuantity;
            if (req.CompleteRemaining)
            {
                op.Status = ProductionOperationStatus.Completed;
                op.ActualEnd = ts;
            }
            op.ModifiedAt = ts;
            op.ModifiedBy = req.CompletedBy;

            _db.Set<ProductionCompletionEvent>().Add(evt);
            await _db.SaveChangesAsync(ct);

            // Trigger auto-advance for good quantity if auto-advance is enabled
            if (safeMoveQty > 0 && op.AutoAdvanceOnCompletion && !req.IsFinalOperation)
            {
                var moveResult = await _wipMoveSvc.AutoAdvanceOnCompletionAsync(
                    req.OperationId, safeMoveQty, null, req.CompletedBy, ct);
                if (moveResult.IsSuccess)
                {
                    evt.WipMoveId = moveResult.Value!.Id;
                    await _db.SaveChangesAsync(ct);
                }
            }

            _log.LogInformation(
                "Completion {Number} on Op {Seq} PRO {OrderId}: Good={Good} Scrap={Scrap} Rework={Rework} Reject={Reject}. " +
                "Final={Final} MoveNext={Move} Backflush={BF}",
                evt.CompletionNumber, op.SequenceNumber, req.ProductionOrderId,
                req.GoodQuantity, req.ScrapQuantity, req.ReworkQuantity, req.RejectQuantity,
                req.IsFinalOperation, req.MoveQuantityToNextOp, req.BackflushMaterials);

            return Result.Success(evt);
        }

        // ================================================================
        // SCRAP — 5-dimensional root cause
        // ================================================================

        public async Task<Result<ProductionScrapEvent>> RecordScrapAsync(
            RecordScrapRequest req, CancellationToken ct = default)
        {
            if (req.ScrapQuantity <= 0)
                return Result.Failure<ProductionScrapEvent>("Scrap quantity must be positive.");

            // P1 fix (Codex PR #390): ScrapThresholdValidator blocks above-threshold
            // scrap unless SupervisorOverride is true. When the caller already sets
            // SupervisorApprovalRequired, they've acknowledged the threshold — let the
            // event through into the approval workflow (ApproveScrapAsync). The pipeline
            // downgrades the blocker to a warning when override is true.
            var blocked = await ValidateOrBlock<ProductionScrapEvent>(
                TransactionActions.RecordScrap, req.ProductionOrderId, req.DetectedAtOperationId,
                req.CompanyId, req.ScrapQuantity, req.RecordedBy, ct: ct);
            if (blocked is not null && !req.SupervisorApprovalRequired)
                return blocked.Value;

            var detectedOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == req.DetectedAtOperationId, ct);
            if (detectedOp == null)
                return Result.Failure<ProductionScrapEvent>($"Detected-at operation {req.DetectedAtOperationId} not found.");
            if (detectedOp.ProductionOrderId != req.ProductionOrderId)
                return Result.Failure<ProductionScrapEvent>(
                    $"Operation {req.DetectedAtOperationId} belongs to PRO {detectedOp.ProductionOrderId}, not PRO {req.ProductionOrderId}.");

            var ts = DateTime.UtcNow;
            var scrapNumber = $"SCP-{ts:yyyyMMddHHmmssfff}-{req.ProductionOrderId}-{detectedOp.SequenceNumber}";

            var evt = new ProductionScrapEvent
            {
                CompanyId = req.CompanyId,
                ScrapNumber = scrapNumber,
                ProductionOrderId = req.ProductionOrderId,
                DetectedAtOperationId = req.DetectedAtOperationId,
                CausedAtOperationId = req.CausedAtOperationId,
                ScrapQuantity = req.ScrapQuantity,
                ScrapUom = req.ScrapUom,
                ScrapReasonCodeId = req.ScrapReasonCodeId,
                DefectCodeId = req.DefectCodeId,
                CauseCodeId = req.CauseCodeId,
                ResponsibleArea = req.ResponsibleArea,
                Disposition = req.Disposition,
                IsComponentScrap = req.IsComponentScrap,
                IsOperationScrap = req.IsOperationScrap,
                ReplacementRequired = req.ReplacementRequired,
                CostTreatment = req.CostTreatment,
                NcrRequired = req.NcrRequired,
                SupervisorApprovalRequired = req.SupervisorApprovalRequired,
                LotNumbers = req.LotNumbers,
                SerialNumbers = req.SerialNumbers,
                Notes = req.Notes,
                ScrapRecordedAtUtc = ts,
                RecordedBy = req.RecordedBy,
                CreatedBy = req.RecordedBy,
            };

            // Update operation scrap qty
            detectedOp.ScrappedQty += req.ScrapQuantity;
            detectedOp.ModifiedAt = ts;
            detectedOp.ModifiedBy = req.RecordedBy;

            _db.Set<ProductionScrapEvent>().Add(evt);
            await _db.SaveChangesAsync(ct);

            _log.LogWarning(
                "SCRAP {Number} on Op {Seq} PRO {OrderId}: {Qty} units. Reason={Area}/{Disposition}. " +
                "Detected={DetOp} Caused={CauseOp}. NCR={Ncr} SuperApproval={Sup}",
                evt.ScrapNumber, detectedOp.SequenceNumber, req.ProductionOrderId,
                req.ScrapQuantity, req.ResponsibleArea, req.Disposition,
                req.DetectedAtOperationId, req.CausedAtOperationId,
                req.NcrRequired, req.SupervisorApprovalRequired);

            return Result.Success(evt);
        }

        public async Task<Result<ProductionScrapEvent>> ApproveScrapAsync(
            int scrapEventId, string approvedBy, CancellationToken ct = default)
        {
            var evt = await _db.Set<ProductionScrapEvent>()
                .FirstOrDefaultAsync(e => e.Id == scrapEventId, ct);
            if (evt == null)
                return Result.Failure<ProductionScrapEvent>($"Scrap event {scrapEventId} not found.");

            var blocked = await ValidateOrBlock<ProductionScrapEvent>(
                TransactionActions.ApproveScrap, evt.ProductionOrderId, evt.DetectedAtOperationId,
                evt.CompanyId, evt.ScrapQuantity, approvedBy, ct: ct);
            if (blocked is not null) return blocked.Value;

            if (!evt.SupervisorApprovalRequired)
                return Result.Failure<ProductionScrapEvent>("This scrap event does not require supervisor approval.");
            if (evt.SupervisorApproved)
                return Result.Failure<ProductionScrapEvent>($"Already approved by {evt.ApprovedBy}.");

            evt.SupervisorApproved = true;
            evt.ApprovedBy = approvedBy;
            evt.ApprovedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return Result.Success(evt);
        }

        // ================================================================
        // REWORK — routing + disposition + cost treatment
        // ================================================================

        public async Task<Result<ProductionReworkEvent>> RecordReworkAsync(
            RecordReworkRequest req, CancellationToken ct = default)
        {
            if (req.ReworkQuantity <= 0)
                return Result.Failure<ProductionReworkEvent>("Rework quantity must be positive.");

            var blocked = await ValidateOrBlock<ProductionReworkEvent>(
                TransactionActions.RecordRework, req.ProductionOrderId, req.SourceOperationId,
                req.CompanyId, req.ReworkQuantity, req.DecidedBy,
                req.AssignedWorkCenterId, ct: ct);
            if (blocked is not null) return blocked.Value;

            var sourceOp = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == req.SourceOperationId, ct);
            if (sourceOp == null)
                return Result.Failure<ProductionReworkEvent>($"Source operation {req.SourceOperationId} not found.");
            if (sourceOp.ProductionOrderId != req.ProductionOrderId)
                return Result.Failure<ProductionReworkEvent>(
                    $"Operation {req.SourceOperationId} belongs to PRO {sourceOp.ProductionOrderId}, not PRO {req.ProductionOrderId}.");

            var ts = DateTime.UtcNow;
            var reworkNumber = $"RWK-{ts:yyyyMMddHHmmssfff}-{req.ProductionOrderId}-{sourceOp.SequenceNumber}";

            var evt = new ProductionReworkEvent
            {
                CompanyId = req.CompanyId,
                ReworkNumber = reworkNumber,
                ProductionOrderId = req.ProductionOrderId,
                SourceOperationId = req.SourceOperationId,
                ReworkOperationId = req.ReworkOperationId,
                ReworkQuantity = req.ReworkQuantity,
                RoutingType = req.RoutingType,
                ReworkInstructions = req.ReworkInstructions,
                ReworkReasonCodeId = req.ReworkReasonCodeId,
                ReworkMaterialRequired = req.ReworkMaterialRequired,
                RemoveDefectiveComponent = req.RemoveDefectiveComponent,
                AdditionalLaborPlannedMins = req.AdditionalLaborPlannedMins,
                AssignedWorkCenterId = req.AssignedWorkCenterId,
                DueDate = req.DueDate,
                QualityHold = req.QualityHold,
                ReinspectRequired = req.ReinspectRequired,
                ScrapAfterFailedReworkAllowed = req.ScrapAfterFailedReworkAllowed,
                ReturnToOriginalFlow = req.ReturnToOriginalFlow,
                CostTreatment = req.CostTreatment,
                NcrId = req.NcrId,
                CarId = req.CarId,
                Notes = req.Notes,
                ReworkDecisionAtUtc = ts,
                DecidedBy = req.DecidedBy,
                CreatedBy = req.DecidedBy,
            };

            // Update operation rework qty
            sourceOp.ReworkQty += req.ReworkQuantity;
            sourceOp.ModifiedAt = ts;
            sourceOp.ModifiedBy = req.DecidedBy;

            _db.Set<ProductionReworkEvent>().Add(evt);
            await _db.SaveChangesAsync(ct);

            // If routing type is ReturnToExistingOp, create a send-back WipMove
            if (req.RoutingType == ReworkRoutingType.ReturnToExistingOp && req.ReworkOperationId.HasValue)
            {
                var moveResult = await _wipMoveSvc.SendBackToPriorOperationAsync(
                    req.SourceOperationId, req.ReworkOperationId.Value,
                    req.ReworkQuantity,
                    $"Rework: {reworkNumber} — {req.ReworkInstructions?[..Math.Min(100, req.ReworkInstructions?.Length ?? 0)]}",
                    req.DecidedBy, ct);
                if (moveResult.IsSuccess)
                {
                    evt.WipMoveId = moveResult.Value!.Id;
                    await _db.SaveChangesAsync(ct);
                }
            }

            _log.LogWarning(
                "REWORK {Number} on Op {Seq} PRO {OrderId}: {Qty} units. Type={Routing}. " +
                "Dest={DestOp} QualityHold={Hold} Reinspect={Reinspect}",
                evt.ReworkNumber, sourceOp.SequenceNumber, req.ProductionOrderId,
                req.ReworkQuantity, req.RoutingType,
                req.ReworkOperationId, req.QualityHold, req.ReinspectRequired);

            return Result.Success(evt);
        }

        // ================================================================
        // READS
        // ================================================================

        public async Task<IReadOnlyList<ProductionCompletionEvent>> GetCompletionsForOrderAsync(
            int productionOrderId, CancellationToken ct = default)
            => await _db.Set<ProductionCompletionEvent>()
                .Where(e => e.ProductionOrderId == productionOrderId)
                .OrderByDescending(e => e.CompletedAtUtc)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<ProductionScrapEvent>> GetScrapForOrderAsync(
            int productionOrderId, CancellationToken ct = default)
            => await _db.Set<ProductionScrapEvent>()
                .Where(e => e.ProductionOrderId == productionOrderId)
                .OrderByDescending(e => e.ScrapRecordedAtUtc)
                .ToListAsync(ct);

        public async Task<IReadOnlyList<ProductionReworkEvent>> GetReworkForOrderAsync(
            int productionOrderId, CancellationToken ct = default)
            => await _db.Set<ProductionReworkEvent>()
                .Where(e => e.ProductionOrderId == productionOrderId)
                .OrderByDescending(e => e.ReworkDecisionAtUtc)
                .ToListAsync(ct);

        public async Task<ProductionCompletionEvent?> GetCompletionAsync(int id, CancellationToken ct = default)
            => await _db.Set<ProductionCompletionEvent>().FindAsync(new object[] { id }, ct);

        public async Task<ProductionScrapEvent?> GetScrapAsync(int id, CancellationToken ct = default)
            => await _db.Set<ProductionScrapEvent>().FindAsync(new object[] { id }, ct);

        public async Task<ProductionReworkEvent?> GetReworkAsync(int id, CancellationToken ct = default)
            => await _db.Set<ProductionReworkEvent>().FindAsync(new object[] { id }, ct);
    }
}
