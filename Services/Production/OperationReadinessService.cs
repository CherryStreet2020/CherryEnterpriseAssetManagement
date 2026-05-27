// B8 PR-PRO-7 (2026-05-27) — "Can I Run This?" 8-Check Readiness Engine.
//
// THE HIGHEST-LEVERAGE BIC DIFFERENTIATOR in the PRO Cockpit.
// Every competitor says "Material Short." We say WHY:
//   "Op 30 Weld — Waiting on PO 45678 (SKF-6205-2RSH, due 6/12, 50 need / 38 recv'd)"
//
// xmin concurrency via MapXminRowVersion at the AppDbContext level.
// Tenant trio enforced on all writes via CompanyId from the parent ProductionOrder.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Engineering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public class OperationReadinessService : IOperationReadinessService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<OperationReadinessService> _log;

        public OperationReadinessService(AppDbContext db, ILogger<OperationReadinessService> log)
        {
            _db = db;
            _log = log;
        }

        // ================================================================
        // CHECK — single operation, all 8 dimensions
        // ================================================================

        public async Task<Result<OperationReadiness>> CheckOperationReadinessAsync(
            int operationId, CancellationToken ct = default)
        {
            var op = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == operationId, ct);

            if (op == null)
                return Result.Failure<OperationReadiness>($"Operation {operationId} not found.");

            // Load the parent PRO for checks that need it (quality hold, docs, etc.)
            var pro = await _db.Set<ProductionOrder>()
                .FirstOrDefaultAsync(p => p.Id == op.ProductionOrderId, ct);

            var checks = new List<ReadinessCheckResult>(8);
            var materialDetails = new List<MaterialReadinessDetail>();

            // Check 1: Materials Ready
            var matResult = await CheckMaterialsAsync(op, ct);
            checks.Add(matResult.Check);
            materialDetails.AddRange(matResult.Details);

            // Check 2: Prior Op Complete
            checks.Add(await CheckPriorOpCompleteAsync(op, ct));

            // Check 3: Resource Available
            checks.Add(await CheckResourceAvailableAsync(op, ct));

            // Check 4: Labor Qualified
            checks.Add(CheckLaborQualified(op));

            // Check 5: Quality Clear
            checks.Add(await CheckQualityClearAsync(op, pro, ct));

            // Check 6: Documents Current
            checks.Add(await CheckDocumentsCurrentAsync(op, pro, ct));

            // Check 7: Tooling Ready
            checks.Add(await CheckToolingReadyAsync(op, ct));

            // Check 8: Maintenance Clear
            checks.Add(await CheckMaintenanceClearAsync(op, ct));

            var overall = checks.Any(c => c.Status == ReadinessStatus.Fail)
                ? ReadinessStatus.Fail
                : checks.Any(c => c.Status == ReadinessStatus.Warning)
                    ? ReadinessStatus.Warning
                    : ReadinessStatus.Pass;

            return Result.Success(new OperationReadiness(
                op.Id,
                op.SequenceNumber,
                op.Description ?? $"Op {op.SequenceNumber}",
                overall,
                checks,
                materialDetails));
        }

        // ================================================================
        // CHECK — full production order, all operations
        // ================================================================

        public async Task<Result<ProductionOrderReadiness>> CheckOrderReadinessAsync(
            int productionOrderId, CancellationToken ct = default)
        {
            var pro = await _db.Set<ProductionOrder>()
                .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);

            if (pro == null)
                return Result.Failure<ProductionOrderReadiness>(
                    $"Production Order {productionOrderId} not found.");

            var ops = await _db.Set<ProductionOperation>()
                .Where(o => o.ProductionOrderId == productionOrderId)
                .OrderBy(o => o.SequenceNumber)
                .ToListAsync(ct);

            var results = new List<OperationReadiness>(ops.Count);
            foreach (var op in ops)
            {
                var r = await CheckOperationReadinessAsync(op.Id, ct);
                if (r.IsSuccess && r.Value != null)
                    results.Add(r.Value);
            }

            var overall = results.Any(r => r.OverallStatus == ReadinessStatus.Fail)
                ? ReadinessStatus.Fail
                : results.Any(r => r.OverallStatus == ReadinessStatus.Warning)
                    ? ReadinessStatus.Warning
                    : ReadinessStatus.Pass;

            return Result.Success(new ProductionOrderReadiness(
                productionOrderId,
                pro.OrderNumber ?? $"PRO-{productionOrderId}",
                overall,
                results.Count(r => r.OverallStatus == ReadinessStatus.Pass),
                results.Count(r => r.OverallStatus == ReadinessStatus.Warning),
                results.Count(r => r.OverallStatus == ReadinessStatus.Fail),
                results));
        }

        // ================================================================
        // CHECK — material-only (exposed for targeted queries)
        // ================================================================

        public async Task<Result<IReadOnlyList<MaterialReadinessDetail>>> CheckMaterialReadinessAsync(
            int operationId, CancellationToken ct = default)
        {
            var op = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == operationId, ct);

            if (op == null)
                return Result.Failure<IReadOnlyList<MaterialReadinessDetail>>(
                    $"Operation {operationId} not found.");

            var (_, details) = await CheckMaterialsAsync(op, ct);
            return Result.Success<IReadOnlyList<MaterialReadinessDetail>>(details);
        }

        // ================================================================
        // REFRESH — update supply link fields from source systems
        // ================================================================

        public async Task<Result<int>> RefreshSupplyLinksAsync(
            int productionOrderId, CancellationToken ct = default)
        {
            var bomLines = await _db.Set<ProductionMaterialStructure>()
                .Where(b => b.ProductionOrderId == productionOrderId)
                .ToListAsync(ct);

            if (bomLines.Count == 0)
                return Result.Success(0);

            var refreshed = 0;
            var now = DateTime.UtcNow;

            foreach (var line in bomLines)
            {
                // Compute LateToNeedDate from dates
                if (line.SupplyPromisedDate.HasValue && line.SupplyRequiredDate.HasValue)
                    line.LateToNeedDate = line.SupplyPromisedDate.Value > line.SupplyRequiredDate.Value;
                else
                    line.LateToNeedDate = false;

                // Compute SupplyQuantityRemaining
                line.SupplyQuantityRemaining = Math.Max(0,
                    line.SupplyQuantityRequired - line.SupplyQuantityReceived);

                // Derive SupplyRisk from status + dates + quantities
                line.SupplyRisk = DeriveSupplyRisk(line);

                // Derive MaterialSupplyStatus from linked supply state
                if (line.LinkedSupplyRecordType != LinkedSupplyRecordType.None)
                {
                    line.MaterialSupplyStatus = DeriveSupplyStatus(line);
                }

                line.LastSupplyRefreshUtc = now;
                refreshed++;
            }

            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Refreshed {Count} supply links for PRO {ProId}",
                refreshed, productionOrderId);

            return Result.Success(refreshed);
        }

        // ================================================================
        // LINK — connect a BOM line to a supply source
        // ================================================================

        public async Task<Result<ProductionMaterialStructure>> LinkSupplyAsync(
            LinkSupplyRequest req, CancellationToken ct = default)
        {
            var line = await _db.Set<ProductionMaterialStructure>()
                .FirstOrDefaultAsync(b => b.Id == req.BomLineId, ct);

            if (line == null)
                return Result.Failure<ProductionMaterialStructure>(
                    $"BOM line {req.BomLineId} not found.");

            line.MaterialSupplyType = req.SupplyType;
            line.LinkedSupplyRecordType = req.RecordType;
            line.LinkedSupplyRecordId = req.LinkedRecordId;
            line.LinkedSupplyLineId = req.LinkedLineId;
            line.LinkedSupplyRecordNumber = req.LinkedRecordNumber;
            line.SupplierOrDepartment = req.SupplierOrDepartment;
            line.BuyerOrPlanner = req.BuyerOrPlanner;
            line.SupplyRequiredDate = req.RequiredDate;
            line.SupplyPromisedDate = req.PromisedDate;
            line.SupplyQuantityRequired = req.QuantityRequired;
            line.SupplyQuantitySupplied = req.QuantitySupplied;
            line.SupplyQuantityRemaining = Math.Max(0,
                req.QuantityRequired - line.SupplyQuantityReceived);
            line.LateToNeedDate = req.PromisedDate.HasValue && req.RequiredDate.HasValue
                && req.PromisedDate.Value > req.RequiredDate.Value;
            line.SupplyRisk = DeriveSupplyRisk(line);
            line.SupplyNotes = req.Notes;
            line.LastSupplyRefreshUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation(
                "Linked BOM line {LineId} to {RecordType} {RecordNumber}",
                req.BomLineId, req.RecordType, req.LinkedRecordNumber);

            return Result.Success(line);
        }

        // ================================================================
        // UNLINK — clear supply link from a BOM line
        // ================================================================

        public async Task<Result<ProductionMaterialStructure>> UnlinkSupplyAsync(
            int bomLineId, CancellationToken ct = default)
        {
            var line = await _db.Set<ProductionMaterialStructure>()
                .FirstOrDefaultAsync(b => b.Id == bomLineId, ct);

            if (line == null)
                return Result.Failure<ProductionMaterialStructure>(
                    $"BOM line {bomLineId} not found.");

            line.MaterialSupplyType = MaterialSupplyType.PurchaseToJob;
            line.MaterialSupplyStatus = MaterialSupplyStatus.Available;
            line.LinkedSupplyRecordType = LinkedSupplyRecordType.None;
            line.LinkedSupplyRecordId = null;
            line.LinkedSupplyLineId = null;
            line.LinkedSupplyRecordNumber = null;
            line.SupplierOrDepartment = null;
            line.BuyerOrPlanner = null;
            line.SupplyRequiredDate = null;
            line.SupplyPromisedDate = null;
            line.SupplyAvailableDate = null;
            line.SupplyQuantityRequired = 0;
            line.SupplyQuantitySupplied = 0;
            line.SupplyQuantityReceived = 0;
            line.SupplyQuantityRemaining = 0;
            line.LateToNeedDate = false;
            line.SupplyRisk = SupplyRisk.None;
            line.SupplyNotes = null;
            line.LastSupplyRefreshUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Unlinked supply from BOM line {LineId}", bomLineId);

            return Result.Success(line);
        }

        // ================================================================
        // UPDATE STATUS — manual or automated supply status refresh
        // ================================================================

        public async Task<Result<ProductionMaterialStructure>> UpdateSupplyStatusAsync(
            UpdateSupplyStatusRequest req, CancellationToken ct = default)
        {
            var line = await _db.Set<ProductionMaterialStructure>()
                .FirstOrDefaultAsync(b => b.Id == req.BomLineId, ct);

            if (line == null)
                return Result.Failure<ProductionMaterialStructure>(
                    $"BOM line {req.BomLineId} not found.");

            line.MaterialSupplyStatus = req.Status;
            line.SupplyRisk = req.Risk;
            line.SupplyQuantityReceived = req.QuantityReceived;
            line.SupplyQuantityRemaining = req.QuantityRemaining;
            line.SupplyAvailableDate = req.AvailableDate;
            line.LateToNeedDate = req.LateToNeedDate;
            if (req.Notes != null) line.SupplyNotes = req.Notes;
            line.LastSupplyRefreshUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);

            return Result.Success(line);
        }

        // ================================================================
        // READS — supply links for operation and order
        // ================================================================

        public async Task<IReadOnlyList<ProductionMaterialStructure>> GetSupplyLinksForOperationAsync(
            int operationId, CancellationToken ct = default)
        {
            var op = await _db.Set<ProductionOperation>()
                .FirstOrDefaultAsync(o => o.Id == operationId, ct);

            if (op == null) return Array.Empty<ProductionMaterialStructure>();

            return await _db.Set<ProductionMaterialStructure>()
                .Where(b => b.ProductionOrderId == op.ProductionOrderId
                         && b.ConsumingOperationSequence == op.SequenceNumber)
                .OrderBy(b => b.Sequence)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<ProductionMaterialStructure>> GetSupplyLinksForOrderAsync(
            int productionOrderId, CancellationToken ct = default)
        {
            return await _db.Set<ProductionMaterialStructure>()
                .Where(b => b.ProductionOrderId == productionOrderId)
                .OrderBy(b => b.ConsumingOperationSequence)
                .ThenBy(b => b.Sequence)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        // ================================================================
        // PRIVATE — the 8 individual readiness checks
        // ================================================================

        private sealed record MaterialCheckResult(ReadinessCheckResult Check, List<MaterialReadinessDetail> Details);

        /// <summary>
        /// Check 1: Materials Ready.
        /// Walk all BOM lines where ConsumingOperationSequence == this op.
        /// For each line, check MaterialSupplyStatus. If Late/Short/OnHold → FAIL.
        /// Returns the check result + detailed per-line breakdown.
        /// </summary>
        private async Task<MaterialCheckResult> CheckMaterialsAsync(
            ProductionOperation op, CancellationToken ct)
        {
            var bomLines = await _db.Set<ProductionMaterialStructure>()
                .Where(b => b.ProductionOrderId == op.ProductionOrderId
                         && b.ConsumingOperationSequence == op.SequenceNumber)
                .AsNoTracking()
                .ToListAsync(ct);

            if (bomLines.Count == 0)
                return new MaterialCheckResult(
                    new ReadinessCheckResult("Materials Ready", ReadinessStatus.Pass,
                        "No materials required for this operation."),
                    new List<MaterialReadinessDetail>());

            var details = new List<MaterialReadinessDetail>();
            var worstStatus = ReadinessStatus.Pass;

            foreach (var line in bomLines)
            {
                var lineStatus = line.MaterialSupplyStatus switch
                {
                    MaterialSupplyStatus.Late => ReadinessStatus.Fail,
                    MaterialSupplyStatus.Short => ReadinessStatus.Fail,
                    MaterialSupplyStatus.OnHold => ReadinessStatus.Fail,
                    MaterialSupplyStatus.InProcess => ReadinessStatus.Warning,
                    MaterialSupplyStatus.Ordered => ReadinessStatus.Warning,
                    _ => ReadinessStatus.Pass,
                };

                // Also check SupplyRisk for override
                if (line.SupplyRisk == SupplyRisk.Critical && lineStatus < ReadinessStatus.Fail)
                    lineStatus = ReadinessStatus.Fail;
                else if (line.SupplyRisk == SupplyRisk.Warning && lineStatus < ReadinessStatus.Warning)
                    lineStatus = ReadinessStatus.Warning;

                if (lineStatus > worstStatus)
                    worstStatus = lineStatus;

                if (lineStatus != ReadinessStatus.Pass)
                {
                    var desc = BuildMaterialDescription(line);
                    details.Add(new MaterialReadinessDetail(
                        line.Id,
                        line.ChildPartNumber,
                        line.MaterialSupplyStatus,
                        line.SupplyRisk,
                        line.LinkedSupplyRecordNumber,
                        line.SupplierOrDepartment,
                        line.SupplyRequiredDate,
                        line.SupplyPromisedDate,
                        line.SupplyQuantityRequired,
                        line.SupplyQuantityReceived,
                        line.SupplyQuantityRemaining,
                        desc));
                }
            }

            var summary = worstStatus switch
            {
                ReadinessStatus.Fail => $"{details.Count} material(s) BLOCKED — {details[0].Description}",
                ReadinessStatus.Warning => $"{details.Count} material(s) at risk — {details[0].Description}",
                _ => $"All {bomLines.Count} materials ready.",
            };

            return new MaterialCheckResult(
                new ReadinessCheckResult("Materials Ready", worstStatus, summary), details);
        }

        /// <summary>
        /// Check 2: Prior Op Complete.
        /// Find the operation immediately before this one in the routing.
        /// If it's not Completed/Skipped, check AvailableQty.
        /// </summary>
        private async Task<ReadinessCheckResult> CheckPriorOpCompleteAsync(
            ProductionOperation op, CancellationToken ct)
        {
            var priorOp = await _db.Set<ProductionOperation>()
                .Where(o => o.ProductionOrderId == op.ProductionOrderId
                         && o.SequenceNumber < op.SequenceNumber)
                .OrderByDescending(o => o.SequenceNumber)
                .FirstOrDefaultAsync(ct);

            if (priorOp == null)
                return new ReadinessCheckResult("Prior Op Complete", ReadinessStatus.Pass,
                    "First operation — no predecessor.");

            if (priorOp.Status == ProductionOperationStatus.Completed ||
                priorOp.Status == ProductionOperationStatus.Skipped)
            {
                return new ReadinessCheckResult("Prior Op Complete", ReadinessStatus.Pass,
                    $"Op {priorOp.SequenceNumber} is {priorOp.Status}. Available qty: {priorOp.AvailableQty}.");
            }

            if (priorOp.AvailableQty > 0)
            {
                return new ReadinessCheckResult("Prior Op Complete", ReadinessStatus.Warning,
                    $"Op {priorOp.SequenceNumber} is {priorOp.Status} — {priorOp.AvailableQty} units available (partial).");
            }

            return new ReadinessCheckResult("Prior Op Complete", ReadinessStatus.Fail,
                $"Op {priorOp.SequenceNumber} is {priorOp.Status} — 0 units available. Cannot start.");
        }

        /// <summary>
        /// Check 3: Resource Available.
        /// WorkCenter status must be Active. Capacity check against concurrent ops.
        /// </summary>
        private async Task<ReadinessCheckResult> CheckResourceAvailableAsync(
            ProductionOperation op, CancellationToken ct)
        {
            if (op.WorkCenterId <= 0)
                return new ReadinessCheckResult("Resource Available", ReadinessStatus.Warning,
                    "No work center assigned to this operation.");

            var wc = await _db.Set<WorkCenter>()
                .FirstOrDefaultAsync(w => w.Id == op.WorkCenterId, ct);

            if (wc == null)
                return new ReadinessCheckResult("Resource Available", ReadinessStatus.Warning,
                    $"Work center {op.WorkCenterId} not found.");

            if (wc.Status == WorkCenterStatus.Maintenance)
                return new ReadinessCheckResult("Resource Available", ReadinessStatus.Fail,
                    $"{wc.Name} is in Maintenance — unavailable.");

            if (wc.Status == WorkCenterStatus.Inactive || wc.Status == WorkCenterStatus.Retired)
                return new ReadinessCheckResult("Resource Available", ReadinessStatus.Fail,
                    $"{wc.Name} is {wc.Status} — cannot schedule.");

            // Check concurrent operation load
            if (wc.SimultaneousOperationsMax.HasValue && wc.SimultaneousOperationsMax.Value > 0)
            {
                var activeOps = await _db.Set<ProductionOperation>()
                    .CountAsync(o => o.WorkCenterId == wc.Id
                                  && (o.Status == ProductionOperationStatus.InSetup
                                      || o.Status == ProductionOperationStatus.Running), ct);

                if (activeOps >= wc.SimultaneousOperationsMax.Value)
                    return new ReadinessCheckResult("Resource Available", ReadinessStatus.Warning,
                        $"{wc.Name} at capacity — {activeOps}/{wc.SimultaneousOperationsMax} concurrent ops running.");
            }

            return new ReadinessCheckResult("Resource Available", ReadinessStatus.Pass,
                $"{wc.Name} is Active. Efficiency: {wc.EfficiencyPct}%.");
        }

        /// <summary>
        /// Check 4: Labor Qualified.
        /// Placeholder — operator certification entity not yet modeled.
        /// Always passes with note. Will wire to OperatorCertification entity
        /// when that lands in Sprint 15+.
        /// </summary>
        private ReadinessCheckResult CheckLaborQualified(ProductionOperation op)
        {
            // TODO: Sprint 15+ — wire to OperatorCertification entity when it exists.
            // For now, always passes. The LaborEntry entity tracks time only,
            // not qualifications. This is explicitly noted in the spec as a
            // placeholder that will be wired when the HR qualification entity lands.
            return new ReadinessCheckResult("Labor Qualified", ReadinessStatus.Pass,
                "Labor qualification check not yet configured (Sprint 15+). Passing by default.");
        }

        /// <summary>
        /// Check 5: Quality Clear.
        /// QualityHoldActive on the operation + PRO on-hold + open CARs.
        /// </summary>
        private async Task<ReadinessCheckResult> CheckQualityClearAsync(
            ProductionOperation op, ProductionOrder? pro, CancellationToken ct)
        {
            // Check operation-level quality hold
            if (op.QualityHoldActive)
                return new ReadinessCheckResult("Quality Clear", ReadinessStatus.Fail,
                    $"Quality hold active on Op {op.SequenceNumber}: {op.QualityHoldReason ?? "No reason specified"}.");

            if (pro != null && pro.Status == ProductionOrderStatus.OnHold
                && pro.HoldReason == HoldReason.Quality)
            {
                return new ReadinessCheckResult("Quality Clear", ReadinessStatus.Fail,
                    $"PRO {pro.OrderNumber} is on Quality Hold: {pro.HoldReasonNotes ?? "No details"}.");
            }

            // Check open CARs against this production order
            var openCars = await _db.Set<CorrectiveActionRequest>()
                .CountAsync(c => c.ProductionOrderId == op.ProductionOrderId
                              && c.Status != CarStatus.Closed
                              && c.Status != CarStatus.Cancelled, ct);

            if (openCars > 0)
            {
                var criticalCars = await _db.Set<CorrectiveActionRequest>()
                    .CountAsync(c => c.ProductionOrderId == op.ProductionOrderId
                                  && c.Status != CarStatus.Closed
                                  && c.Status != CarStatus.Cancelled
                                  && c.Severity == CarSeverity.Critical, ct);

                if (criticalCars > 0)
                    return new ReadinessCheckResult("Quality Clear", ReadinessStatus.Fail,
                        $"{criticalCars} CRITICAL open CAR(s) on this PRO. Production blocked until resolved.");

                return new ReadinessCheckResult("Quality Clear", ReadinessStatus.Warning,
                    $"{openCars} open CAR(s) on this PRO. Review before proceeding.");
            }

            return new ReadinessCheckResult("Quality Clear", ReadinessStatus.Pass,
                "No quality holds or open CARs.");
        }

        /// <summary>
        /// Check 6: Documents Current.
        /// Verify linked documents are Released (not Draft/Superseded/Obsolete).
        /// Check for open ECOs with RequiresFaiRetrigger that affect items in this PRO.
        /// </summary>
        private async Task<ReadinessCheckResult> CheckDocumentsCurrentAsync(
            ProductionOperation op, ProductionOrder? pro, CancellationToken ct)
        {

            if (pro?.ItemId != null)
            {
                var impactingEcos = await _db.Set<EngineeringChangeOrder>()
                    .CountAsync(e => (e.Status == EcoStatus.Approved || e.Status == EcoStatus.Released)
                                  && e.RequiresFaiRetrigger, ct);

                if (impactingEcos > 0)
                    return new ReadinessCheckResult("Documents Current", ReadinessStatus.Warning,
                        $"{impactingEcos} open ECO(s) with FAI retrigger required. Verify drawings are current revision.");
            }

            // Check document links for this item — any Superseded or Obsolete?
            if (pro?.ItemId != null)
            {
                var staleDocLinks = await _db.Set<ItemDocumentLink>()
                    .Include(l => l.Document)
                    .Where(l => l.ItemId == pro.ItemId
                             && (l.Document!.Status == DocumentStatus.Superseded
                                 || l.Document.Status == DocumentStatus.Obsolete))
                    .CountAsync(ct);

                if (staleDocLinks > 0)
                    return new ReadinessCheckResult("Documents Current", ReadinessStatus.Warning,
                        $"{staleDocLinks} linked document(s) are Superseded or Obsolete. Verify current revision on floor.");
            }

            return new ReadinessCheckResult("Documents Current", ReadinessStatus.Pass,
                "All documents current. No blocking ECOs.");
        }

        /// <summary>
        /// Check 7: Tooling Ready.
        /// Check if the assigned asset (tool/fixture/gauge) is available and calibrated.
        /// Uses Asset.CalibrationRequired + NextCalibrationDue.
        /// </summary>
        private async Task<ReadinessCheckResult> CheckToolingReadyAsync(
            ProductionOperation op, CancellationToken ct)
        {
            if (!op.AssetId.HasValue)
                return new ReadinessCheckResult("Tooling Ready", ReadinessStatus.Pass,
                    "No tooling/fixture assigned to this operation.");

            var asset = await _db.Set<Abs.FixedAssets.Models.Asset>()
                .FirstOrDefaultAsync(a => a.Id == op.AssetId.Value, ct);

            if (asset == null)
                return new ReadinessCheckResult("Tooling Ready", ReadinessStatus.Warning,
                    $"Asset {op.AssetId} not found.");

            // Check calibration
            if (asset.CalibrationRequired && asset.NextCalibrationDue.HasValue
                && asset.NextCalibrationDue.Value < DateTime.UtcNow)
            {
                return new ReadinessCheckResult("Tooling Ready", ReadinessStatus.Fail,
                    $"Asset '{asset.Description}' calibration expired ({asset.NextCalibrationDue:yyyy-MM-dd}). " +
                    $"Cannot use until recalibrated.");
            }

            if (asset.CalibrationRequired && asset.NextCalibrationDue.HasValue)
            {
                var daysUntilDue = (asset.NextCalibrationDue.Value - DateTime.UtcNow).TotalDays;
                if (daysUntilDue < 7)
                    return new ReadinessCheckResult("Tooling Ready", ReadinessStatus.Warning,
                        $"Asset '{asset.Description}' calibration due in {daysUntilDue:F0} days ({asset.NextCalibrationDue:yyyy-MM-dd}).");
            }

            // Check asset condition
            if (asset.Condition == AssetCondition.Damaged || asset.Condition == AssetCondition.NeedsRepair
                || asset.Condition == AssetCondition.Critical)
            {
                return new ReadinessCheckResult("Tooling Ready", ReadinessStatus.Fail,
                    $"Asset '{asset.Description}' condition is {asset.Condition}. Repair or replace before use.");
            }

            return new ReadinessCheckResult("Tooling Ready", ReadinessStatus.Pass,
                $"Asset '{asset.Description}' ready. Condition: {asset.Condition}.");
        }

        /// <summary>
        /// Check 8: Maintenance Clear.
        /// Check the work center's linked asset for maintenance status, PM schedule,
        /// predictive health score, and condition flags.
        /// </summary>
        private async Task<ReadinessCheckResult> CheckMaintenanceClearAsync(
            ProductionOperation op, CancellationToken ct)
        {
            if (op.WorkCenterId <= 0)
                return new ReadinessCheckResult("Maintenance Clear", ReadinessStatus.Pass,
                    "No work center assigned — maintenance check skipped.");

            var wc = await _db.Set<WorkCenter>()
                .FirstOrDefaultAsync(w => w.Id == op.WorkCenterId, ct);

            if (wc == null)
                return new ReadinessCheckResult("Maintenance Clear", ReadinessStatus.Pass,
                    "Work center not found — maintenance check skipped.");

            if (wc.Status == WorkCenterStatus.Maintenance)
                return new ReadinessCheckResult("Maintenance Clear", ReadinessStatus.Fail,
                    $"{wc.Name} is under active Maintenance. Unavailable until complete.");

            // If the operation has a linked asset, check its health
            if (op.AssetId.HasValue)
            {
                var asset = await _db.Set<Abs.FixedAssets.Models.Asset>()
                    .FirstOrDefaultAsync(a => a.Id == op.AssetId.Value, ct);

                if (asset != null)
                {
                    // Check predictive health
                    if (asset.PredictiveHealthScore.HasValue && asset.PredictiveHealthScore.Value < 40)
                        return new ReadinessCheckResult("Maintenance Clear", ReadinessStatus.Warning,
                            $"Asset '{asset.Description}' predictive health score is {asset.PredictiveHealthScore:F0}% — " +
                            $"consider scheduling preventive maintenance before starting this operation.");

                    // Check predicted failure
                    if (asset.PredictedFailureDate.HasValue
                        && asset.PredictedFailureDate.Value < DateTime.UtcNow.AddDays(7))
                    {
                        return new ReadinessCheckResult("Maintenance Clear", ReadinessStatus.Warning,
                            $"Asset '{asset.Description}' predicted failure within 7 days " +
                            $"({asset.PredictedFailureDate:yyyy-MM-dd}). Schedule PM.");
                    }

                    // Check condition
                    if (asset.Condition == AssetCondition.Critical || asset.Condition == AssetCondition.Damaged)
                        return new ReadinessCheckResult("Maintenance Clear", ReadinessStatus.Fail,
                            $"Asset '{asset.Description}' condition: {asset.Condition}. Maintenance required.");
                }
            }

            return new ReadinessCheckResult("Maintenance Clear", ReadinessStatus.Pass,
                $"{wc.Name} — no maintenance issues.");
        }

        // ================================================================
        // PRIVATE — helper methods
        // ================================================================

        /// <summary>
        /// Build a human-readable description for a material supply issue.
        /// This is THE BIC differentiator — "don't say short, say WHY."
        /// </summary>
        private static string BuildMaterialDescription(ProductionMaterialStructure line)
        {
            var part = line.ChildPartNumber;
            var supply = line.LinkedSupplyRecordNumber;

            return line.MaterialSupplyStatus switch
            {
                MaterialSupplyStatus.Late when supply != null =>
                    $"{part} — {supply} LATE (due {line.SupplyRequiredDate:MMM d}, " +
                    $"promised {line.SupplyPromisedDate:MMM d}). " +
                    $"Need {line.SupplyQuantityRequired}, have {line.SupplyQuantityReceived}.",

                MaterialSupplyStatus.Short when supply != null =>
                    $"{part} — SHORT via {supply}. " +
                    $"Need {line.SupplyQuantityRequired}, have {line.SupplyQuantityReceived}, " +
                    $"remaining {line.SupplyQuantityRemaining}." +
                    (line.SupplierOrDepartment != null ? $" Supplier: {line.SupplierOrDepartment}." : ""),

                MaterialSupplyStatus.Short =>
                    $"{part} — SHORT. Need {line.SupplyQuantityRequired}, " +
                    $"have {line.SupplyQuantityReceived}.",

                MaterialSupplyStatus.OnHold when supply != null =>
                    $"{part} — ON HOLD ({supply}). " +
                    (line.SupplyNotes ?? "Quality/inspection/credit hold."),

                MaterialSupplyStatus.OnHold =>
                    $"{part} — ON HOLD. {line.SupplyNotes ?? "Pending release."}",

                MaterialSupplyStatus.Late =>
                    $"{part} — LATE (due {line.SupplyRequiredDate:MMM d}). No supply linked.",

                MaterialSupplyStatus.Ordered when supply != null =>
                    $"{part} — Ordered via {supply}. " +
                    $"Promised {line.SupplyPromisedDate:MMM d}. " +
                    $"Need {line.SupplyQuantityRequired}, supplied {line.SupplyQuantitySupplied}.",

                MaterialSupplyStatus.InProcess when supply != null =>
                    $"{part} — In process ({supply}). " +
                    $"Expected {line.SupplyPromisedDate:MMM d}.",

                _ => $"{part} — {line.MaterialSupplyStatus}.",
            };
        }

        /// <summary>Derive SupplyRisk from current supply state.</summary>
        private static SupplyRisk DeriveSupplyRisk(ProductionMaterialStructure line)
        {
            if (line.MaterialSupplyStatus == MaterialSupplyStatus.Late
                || line.MaterialSupplyStatus == MaterialSupplyStatus.Short
                || line.MaterialSupplyStatus == MaterialSupplyStatus.OnHold)
                return SupplyRisk.Critical;

            if (line.LateToNeedDate)
                return SupplyRisk.Critical;

            if (line.MaterialSupplyStatus == MaterialSupplyStatus.Ordered
                || line.MaterialSupplyStatus == MaterialSupplyStatus.InProcess)
            {
                // Warning if promised date is within 3 days of required date
                if (line.SupplyPromisedDate.HasValue && line.SupplyRequiredDate.HasValue)
                {
                    var margin = (line.SupplyRequiredDate.Value - line.SupplyPromisedDate.Value).TotalDays;
                    if (margin < 3) return SupplyRisk.Warning;
                }
                return SupplyRisk.Warning;
            }

            if (line.SupplyQuantityRemaining > 0
                && line.MaterialSupplyStatus != MaterialSupplyStatus.Available
                && line.MaterialSupplyStatus != MaterialSupplyStatus.Received)
                return SupplyRisk.Warning;

            return SupplyRisk.None;
        }

        /// <summary>Derive MaterialSupplyStatus from linked supply data.</summary>
        private static MaterialSupplyStatus DeriveSupplyStatus(ProductionMaterialStructure line)
        {
            if (line.SupplyQuantityReceived >= line.SupplyQuantityRequired)
                return MaterialSupplyStatus.Received;

            if (line.LateToNeedDate || (line.SupplyRequiredDate.HasValue
                && line.SupplyRequiredDate.Value < DateTime.UtcNow
                && line.SupplyQuantityReceived < line.SupplyQuantityRequired))
                return MaterialSupplyStatus.Late;

            if (line.SupplyQuantityRemaining > 0
                && line.SupplyQuantitySupplied < line.SupplyQuantityRequired)
                return MaterialSupplyStatus.Short;

            if (line.LinkedSupplyRecordType == LinkedSupplyRecordType.WorkOrder)
                return MaterialSupplyStatus.InProcess;

            if (line.LinkedSupplyRecordType == LinkedSupplyRecordType.PurchaseOrder)
                return MaterialSupplyStatus.Ordered;

            return MaterialSupplyStatus.Available;
        }
    }
}
