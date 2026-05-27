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
        private readonly ICostTransactionService _costSvc;
        private readonly ILogger<ProductionCompletionService> _log;

        public ProductionCompletionService(
            AppDbContext db,
            ITransactionValidationPipeline pipeline,
            IProductionWipMoveService wipMoveSvc,
            ICostTransactionService costSvc,
            ILogger<ProductionCompletionService> log)
        {
            _db = db;
            _pipeline = pipeline;
            _wipMoveSvc = wipMoveSvc;
            _costSvc = costSvc;
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

            // ── Cost posting — CompletionToFg for good qty ──────────────
            if (req.GoodQuantity > 0)
            {
                _ = PostCompletionCostSafe(
                    CostTransactionType.CompletionToFg, ProductionCostBucket.DirectMaterial,
                    req.CompanyId, req.ProductionOrderId, req.OperationId,
                    req.GoodQuantity, null, 0m, // unit cost determined by cost engine rollup
                    "CompletionEvent", evt.Id,
                    req.LotNumbers, req.SerialNumbers,
                    req.CompletedBy, ct);
            }

            // ── Child-to-parent transfer (Layer B) ──────────────────────
            // When this is the final operation on a child PRO that has a parent,
            // transfer the child's accumulated cost to the parent as a supply
            // cost transfer. The parent sees this as a component cost; the child's
            // internal detail becomes drilldown-only (non-additive at parent boundary).
            if (req.IsFinalOperation && req.GoodQuantity > 0)
            {
                _ = PostChildToParentTransferSafe(
                    req.ProductionOrderId, req.CompanyId, req.GoodQuantity,
                    req.CompletedBy, ct);
            }

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

            // ── Cost posting — scrap cost treatment determines type ──────
            var scrapCostType = req.CostTreatment switch
            {
                CostTreatment.ScrapAccount => CostTransactionType.ScrapToAccount,
                CostTreatment.CustomerCharge => CostTransactionType.ScrapCustomerCharge,
                CostTreatment.VendorChargeback => CostTransactionType.ScrapVendorChargeback,
                _ => CostTransactionType.ScrapAbsorbToJob, // AbsorbToJob + WarrantyAbsorb
            };
            _ = PostCompletionCostSafe(
                scrapCostType, ProductionCostBucket.Scrap,
                req.CompanyId, req.ProductionOrderId, req.DetectedAtOperationId,
                req.ScrapQuantity, req.ScrapUom, 0m, // unit cost resolved by cost engine
                "ScrapEvent", evt.Id,
                req.LotNumbers, req.SerialNumbers,
                req.RecordedBy, ct);

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

            // ── Cost posting — rework labor + material if applicable ─────
            if (req.AdditionalLaborPlannedMins > 0)
            {
                _ = PostCompletionCostSafe(
                    CostTransactionType.ReworkLaborCost, ProductionCostBucket.Rework,
                    req.CompanyId, req.ProductionOrderId, req.SourceOperationId,
                    req.AdditionalLaborPlannedMins, "MIN", 0m, // rate resolved by cost engine
                    "ReworkEvent", evt.Id,
                    null, null, req.DecidedBy, ct);
            }
            if (req.ReworkMaterialRequired)
            {
                _ = PostCompletionCostSafe(
                    CostTransactionType.ReworkMaterial, ProductionCostBucket.Rework,
                    req.CompanyId, req.ProductionOrderId, req.SourceOperationId,
                    req.ReworkQuantity, null, 0m, // cost resolved when material is actually issued
                    "ReworkEvent", evt.Id,
                    null, null, req.DecidedBy, ct);
            }

            return Result.Success(evt);
        }

        // ================================================================
        // COST POSTING HELPER
        // ================================================================

        /// <summary>
        /// Fire-and-forget cost posting. Logs warning on failure but never blocks
        /// the production completion/scrap/rework event.
        /// </summary>
        private async Task PostCompletionCostSafe(
            CostTransactionType costType, ProductionCostBucket bucket,
            int companyId, int productionOrderId, int operationId,
            decimal quantity, string? uom, decimal unitCost,
            string sourceType, int sourceId,
            string? lotNumbers, string? serialNumbers,
            string postedBy, CancellationToken ct)
        {
            try
            {
                await _costSvc.PostCostAsync(
                    CostObjectType.ProductionOrder, productionOrderId,
                    costType, bucket,
                    companyId, null, productionOrderId,
                    operationId, null, null,
                    quantity, uom, unitCost,
                    sourceType, sourceId,
                    lotNumbers, serialNumbers, null,
                    true, null, postedBy, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Cost posting failed for {SourceType} {SourceId} ({Type}) on PRO {PRO}. " +
                    "Event succeeded — cost will need manual reconciliation.",
                    sourceType, sourceId, costType, productionOrderId);
            }
        }

        // ================================================================
        // CHILD-TO-PARENT COST TRANSFER (Layer B)
        // ================================================================

        /// <summary>
        /// When a child PRO's final operation completes, transfer the child's
        /// accumulated actual cost to the parent PRO as a supply cost transfer.
        /// This is Layer B in the 3-layer cost-object graph model:
        ///   - The parent sees the child as a single supply transfer value
        ///   - The child's internal detail (Layer A) stays visible by drilldown
        ///   - RollupAdditiveFlag = false on child detail at the parent boundary
        ///   - The transfer value IS additive at the parent level
        /// Fire-and-forget: never blocks the completion event.
        /// </summary>
        private async Task PostChildToParentTransferSafe(
            int childProductionOrderId, int companyId, decimal goodQuantity,
            string postedBy, CancellationToken ct)
        {
            try
            {
                // Load the child PRO to check for parent link
                var childPro = await _db.Set<ProductionOrder>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == childProductionOrderId, ct);

                if (childPro?.ParentProductionOrderId is not int parentProId)
                    return; // No parent — this is a top-level PRO, no transfer needed

                // Refresh the child's cost summary so we have accurate totals
                var summaryResult = await _costSvc.RefreshSummaryAsync(childProductionOrderId, postedBy, ct);
                var childSummary = summaryResult.IsSuccess ? summaryResult.Value : null;

                // Get the child's actual cost breakdown (from summary or PRO header)
                decimal materialCost = childSummary?.ActualMaterialCost ?? childPro.MaterialCost ?? 0m;
                decimal laborCost = childSummary?.ActualLaborCost ?? childPro.LaborCost ?? 0m;
                decimal overheadCost = childSummary?.ActualBurdenCost ?? childPro.OverheadCost ?? 0m;
                decimal subcontractCost = childSummary?.ActualSubcontractCost ?? childPro.SubcontractCost ?? 0m;
                decimal otherCost = (childSummary?.ActualOutsideProcessingCost ?? 0m)
                                  + (childSummary?.ActualToolingCost ?? 0m)
                                  + (childSummary?.ActualFreightLandedCost ?? 0m);

                decimal totalCost = materialCost + laborCost + overheadCost + subcontractCost + otherCost;
                if (totalCost <= 0 && goodQuantity <= 0)
                    return; // Nothing to transfer

                decimal unitCost = goodQuantity > 0 ? totalCost / goodQuantity : 0m;

                // Load parent PRO for site info
                var parentPro = await _db.Set<ProductionOrder>()
                    .AsNoTracking()
                    .Select(p => new { p.Id, p.LocationId })
                    .FirstOrDefaultAsync(p => p.Id == parentProId, ct);

                // Post the transfer: child → parent (Layer B)
                var transferResult = await _costSvc.PostTransferAsync(
                    sourceCostObjectType: CostObjectType.ProductionOrder,
                    sourceCostObjectId: childProductionOrderId,
                    sourceSiteId: childPro.LocationId,
                    destCostObjectType: CostObjectType.ProductionOrder,
                    destCostObjectId: parentProId,
                    destSiteId: parentPro?.LocationId,
                    companyId: companyId,
                    transferType: CostTransferType.ChildCompletionToParent,
                    quantity: goodQuantity,
                    uom: null,
                    unitCost: unitCost,
                    materialCost: materialCost,
                    laborCost: laborCost,
                    overheadCost: overheadCost,
                    subcontractCost: subcontractCost,
                    otherCost: otherCost,
                    isProvisional: false,
                    notes: $"Child PRO {childProductionOrderId} final completion → Parent PRO {parentProId}. " +
                           $"{goodQuantity} units at ${unitCost:N4}/unit = ${totalCost:N2} total.",
                    postedBy: postedBy,
                    ct: ct);

                if (transferResult.IsSuccess)
                {
                    // Also post a CostTransaction on the PARENT side for the supply transfer
                    // (additive at parent level — this IS the component cost the parent sees)
                    await _costSvc.PostCostAsync(
                        CostObjectType.ProductionOrder, parentProId,
                        CostTransactionType.ChildSupplyTransfer, ProductionCostBucket.ChildSupply,
                        companyId, parentPro?.LocationId, parentProId,
                        null, null, null,
                        goodQuantity, null, unitCost,
                        "CostTransfer", transferResult.Value!.Id,
                        null, null, null,
                        rollupAdditive: true,
                        $"Supply from child PRO {childProductionOrderId}",
                        postedBy, ct);

                    // Refresh the parent's cost summary to include the transfer
                    _ = _costSvc.RefreshSummaryAsync(parentProId, postedBy, ct);

                    _log.LogInformation(
                        "CHILD-TO-PARENT TRANSFER: PRO {Child} → PRO {Parent}. " +
                        "{Qty} units, ${Total:N2} (Mat=${Mat:N2} Lab=${Lab:N2} OH=${OH:N2} Sub=${Sub:N2} Other=${Other:N2})",
                        childProductionOrderId, parentProId, goodQuantity, totalCost,
                        materialCost, laborCost, overheadCost, subcontractCost, otherCost);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Child-to-parent cost transfer failed for PRO {ChildPro}. " +
                    "Completion succeeded — transfer will need manual reconciliation.",
                    childProductionOrderId);
            }
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
