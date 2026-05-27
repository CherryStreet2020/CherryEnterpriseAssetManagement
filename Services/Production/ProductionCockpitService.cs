// B8 PR-PRO-8 (2026-05-27) — PRO Cockpit data aggregation.
// READ-ONLY service that composes data from all prior PRO services
// into the shapes needed by the /Production/Orders/{id}/Cockpit page.

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public class ProductionCockpitService : IProductionCockpitService
    {
        private readonly AppDbContext _db;
        private readonly IOperationReadinessService _readiness;
        private readonly ITenantContext _tenant;
        private readonly ILogger<ProductionCockpitService> _log;

        public ProductionCockpitService(
            AppDbContext db,
            IOperationReadinessService readiness,
            ITenantContext tenant,
            ILogger<ProductionCockpitService> log)
        {
            _db = db;
            _readiness = readiness;
            _tenant = tenant;
            _log = log;
        }

        public async Task<Result<CockpitData>> GetCockpitDataAsync(
            int productionOrderId, CancellationToken ct = default)
        {
            var pro = await _db.Set<ProductionOrder>()
                .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);

            if (pro == null)
                return Result.Failure<CockpitData>(
                    $"Production Order {productionOrderId} not found.");

            // P1 fix (Codex): enforce tenant scope — prevent IDOR across tenants.
            if (_tenant.VisibleCompanyIds.Count > 0
                && !_tenant.VisibleCompanyIds.Contains(pro.CompanyId))
                return Result.Failure<CockpitData>(
                    $"Production Order {productionOrderId} not accessible for current tenant.");

            var summaryResult = await BuildSummaryBarAsync(pro, ct);
            var bomRows = await BuildBomGridAsync(pro, ct);
            var routingRows = await BuildRoutingGridAsync(pro, ct);

            // Run readiness checks (may fail gracefully)
            ProductionOrderReadiness? readiness = null;
            var readinessResult = await _readiness.CheckOrderReadinessAsync(productionOrderId, ct);
            if (readinessResult.IsSuccess) readiness = readinessResult.Value;

            return Result.Success(new CockpitData(
                pro, summaryResult, bomRows, routingRows, readiness));
        }

        public async Task<Result<CockpitSummaryBar>> GetSummaryBarAsync(
            int productionOrderId, CancellationToken ct = default)
        {
            var pro = await LoadAndValidateAsync(productionOrderId, ct);
            if (pro == null)
                return Result.Failure<CockpitSummaryBar>(
                    $"Production Order {productionOrderId} not found or not accessible.");

            return Result.Success(await BuildSummaryBarAsync(pro, ct));
        }

        public async Task<Result<IReadOnlyList<CockpitBomRow>>> GetBomGridAsync(
            int productionOrderId, CancellationToken ct = default)
        {
            var pro = await LoadAndValidateAsync(productionOrderId, ct);
            if (pro == null)
                return Result.Failure<IReadOnlyList<CockpitBomRow>>(
                    $"Production Order {productionOrderId} not found or not accessible.");

            return Result.Success<IReadOnlyList<CockpitBomRow>>(
                await BuildBomGridAsync(pro, ct));
        }

        public async Task<Result<IReadOnlyList<CockpitRoutingRow>>> GetRoutingGridAsync(
            int productionOrderId, CancellationToken ct = default)
        {
            var pro = await LoadAndValidateAsync(productionOrderId, ct);
            if (pro == null)
                return Result.Failure<IReadOnlyList<CockpitRoutingRow>>(
                    $"Production Order {productionOrderId} not found or not accessible.");

            return Result.Success<IReadOnlyList<CockpitRoutingRow>>(
                await BuildRoutingGridAsync(pro, ct));
        }

        // ================================================================
        // PRIVATE — load + tenant validation (shared across all public methods)
        // ================================================================

        private async Task<ProductionOrder?> LoadAndValidateAsync(
            int productionOrderId, CancellationToken ct)
        {
            var pro = await _db.Set<ProductionOrder>()
                .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);

            if (pro == null) return null;

            // P1 fix (Codex): enforce tenant scope
            if (_tenant.VisibleCompanyIds.Count > 0
                && !_tenant.VisibleCompanyIds.Contains(pro.CompanyId))
                return null;

            return pro;
        }

        // ================================================================
        // PRIVATE — build summary bar
        // ================================================================

        private async Task<CockpitSummaryBar> BuildSummaryBarAsync(
            ProductionOrder pro, CancellationToken ct)
        {
            // Load the item being built
            var item = pro.ItemId.HasValue
                ? await _db.Set<Item>().FirstOrDefaultAsync(i => i.Id == pro.ItemId.Value, ct)
                : null;

            // Operation progress
            var ops = await _db.Set<ProductionOperation>()
                .Where(o => o.ProductionOrderId == pro.Id)
                .ToListAsync(ct);

            var completedOps = ops.Count(o => o.Status == ProductionOperationStatus.Completed
                                           || o.Status == ProductionOperationStatus.Skipped);
            var opProgressPct = ops.Count > 0 ? (decimal)completedOps / ops.Count * 100 : 0;

            // Current + next operation
            var currentOp = ops
                .Where(o => o.Status == ProductionOperationStatus.Running
                         || o.Status == ProductionOperationStatus.InSetup)
                .OrderBy(o => o.SequenceNumber)
                .FirstOrDefault();
            var nextOp = ops
                .Where(o => o.Status == ProductionOperationStatus.Scheduled
                         || o.Status == ProductionOperationStatus.Released)
                .OrderBy(o => o.SequenceNumber)
                .FirstOrDefault();

            // Material readiness
            var bomLines = await _db.Set<ProductionMaterialStructure>()
                .Where(b => b.ProductionOrderId == pro.Id)
                .ToListAsync(ct);

            var readyLines = bomLines.Count(b =>
                b.MaterialSupplyStatus == MaterialSupplyStatus.Available
                || b.MaterialSupplyStatus == MaterialSupplyStatus.Received);
            var matReadinessPct = bomLines.Count > 0 ? (decimal)readyLines / bomLines.Count * 100 : 100;

            // Shortages
            var shortages = bomLines.Count(b =>
                b.MaterialSupplyStatus == MaterialSupplyStatus.Short
                || b.MaterialSupplyStatus == MaterialSupplyStatus.Late
                || b.MaterialSupplyStatus == MaterialSupplyStatus.OnHold);

            // Quality holds
            var qualityHolds = ops.Count(o => o.QualityHoldActive);
            if (pro.Status == ProductionOrderStatus.OnHold && pro.HoldReason == HoldReason.Quality)
                qualityHolds++;

            // P2 fix (Codex): count ALL open labor entries (ClockOutAt == null),
            // not just today's. Overnight/long-running shifts start before midnight
            // UTC but are still active. The "clocked in" state is what matters.
            var opIds = ops.Select(o => o.Id).ToList();
            var activeOps = await _db.Set<LaborEntry>()
                .Where(l => opIds.Contains(l.ProductionOperationId)
                         && l.ClockOutAt == null)
                .Select(l => l.OperatorUserId)
                .Distinct()
                .CountAsync(ct);

            // Days late
            int? daysLate = null;
            if (pro.ScheduledEnd.HasValue && pro.ScheduledEnd.Value < DateTime.UtcNow
                && pro.Status != ProductionOrderStatus.Completed
                && pro.Status != ProductionOrderStatus.Closed)
            {
                daysLate = (int)(DateTime.UtcNow - pro.ScheduledEnd.Value).TotalDays;
            }

            // WIP cost
            var wipCost = (pro.MaterialCost ?? 0) + (pro.LaborCost ?? 0)
                        + (pro.OverheadCost ?? 0) + (pro.SubcontractCost ?? 0);

            return new CockpitSummaryBar(
                OrderNumber: pro.OrderNumber,
                PartNumber: item?.PartNumber,
                Revision: item?.Revision,
                Description: pro.Description ?? item?.Description,
                Status: pro.Status,
                DueDate: pro.ScheduledEnd,
                DaysLate: daysLate,
                QuantityOrdered: pro.QuantityOrdered,
                QuantityCompleted: pro.QuantityCompleted,
                QuantityScrapped: pro.QuantityScrapped,
                QuantityRework: pro.QuantityRework,
                QuantityRemaining: pro.QuantityOrdered - pro.QuantityCompleted - pro.QuantityScrapped,
                MaterialReadinessPercent: Math.Round(matReadinessPct, 1),
                OperationProgressPercent: Math.Round(opProgressPct, 1),
                WipCost: wipCost,
                OpenQualityHolds: qualityHolds,
                OpenMaterialShortages: shortages,
                ActiveOperators: activeOps,
                CurrentOperation: currentOp != null
                    ? $"Op {currentOp.SequenceNumber} — {currentOp.Description}"
                    : null,
                NextOperation: nextOp != null
                    ? $"Op {nextOp.SequenceNumber} — {nextOp.Description}"
                    : null);
        }

        // ================================================================
        // PRIVATE — build BOM grid rows
        // ================================================================

        private async Task<List<CockpitBomRow>> BuildBomGridAsync(
            ProductionOrder pro, CancellationToken ct)
        {
            var lines = await _db.Set<ProductionMaterialStructure>()
                .Where(b => b.ProductionOrderId == pro.Id)
                .OrderBy(b => b.ConsumingOperationSequence)
                .ThenBy(b => b.Sequence)
                .AsNoTracking()
                .ToListAsync(ct);

            return lines.Select(l =>
            {
                var requiredInclScrap = l.QuantityPer * (1 + (l.ScrapPercent ?? 0) / 100);
                var remainingToIssue = Math.Max(0, requiredInclScrap - l.IssuedQuantity);
                var cost = l.FrozenExtendedCost ?? (l.QuantityPer * (l.FrozenStandardCost ?? 0));

                // Build supply link description (THE BIC DIFFERENTIATOR)
                string? supplyDesc = null;
                if (l.LinkedSupplyRecordType != LinkedSupplyRecordType.None)
                {
                    supplyDesc = l.MaterialSupplyStatus switch
                    {
                        MaterialSupplyStatus.Late =>
                            $"LATE — {l.LinkedSupplyRecordNumber} (due {l.SupplyRequiredDate:MMM d})",
                        MaterialSupplyStatus.Short =>
                            $"SHORT — need {l.SupplyQuantityRequired}, have {l.SupplyQuantityReceived}",
                        MaterialSupplyStatus.OnHold =>
                            $"HOLD — {l.LinkedSupplyRecordNumber}",
                        MaterialSupplyStatus.Ordered =>
                            $"Ordered — {l.LinkedSupplyRecordNumber} (ETA {l.SupplyPromisedDate:MMM d})",
                        MaterialSupplyStatus.InProcess =>
                            $"In process — {l.LinkedSupplyRecordNumber}",
                        MaterialSupplyStatus.Received =>
                            $"Received via {l.LinkedSupplyRecordNumber}",
                        _ => l.LinkedSupplyRecordNumber,
                    };
                }

                return new CockpitBomRow(
                    Id: l.Id,
                    Line: l.Sequence,
                    OperationSequence: l.ConsumingOperationSequence,
                    PartNumber: l.ChildPartNumber,
                    Description: null,  // TODO: join Item.Description when needed
                    Revision: l.ChildRevision,
                    RequiredQty: l.QuantityPer,
                    RequiredInclScrap: requiredInclScrap,
                    Uom: l.Uom,
                    Available: l.SupplyQuantityReceived,
                    Reserved: l.ReservedQuantity,
                    Picked: l.PickedQuantity,
                    Staged: l.StagedQuantity,
                    Issued: l.IssuedQuantity,
                    Consumed: l.ConsumedQuantity,
                    RemainingToIssue: remainingToIssue,
                    Short: l.ShortQuantity,
                    SourceLocation: null,  // TODO: warehouse/bin join
                    LotSerial: l.ReservedLotNumber ?? l.IssuedLotNumber,
                    SupplyType: l.SupplyType.ToString(),
                    Backflush: l.IssueMethod == BomIssueMethod.Backflush,
                    SubstituteAllowed: l.SubstituteAllowed,
                    Status: l.LineStatus.ToString(),
                    Cost: cost,
                    SupplyLinkDescription: supplyDesc,
                    SupplyRisk: l.SupplyRisk.ToString());
            }).ToList();
        }

        // ================================================================
        // PRIVATE — build routing grid rows
        // ================================================================

        private async Task<List<CockpitRoutingRow>> BuildRoutingGridAsync(
            ProductionOrder pro, CancellationToken ct)
        {
            var ops = await _db.Set<ProductionOperation>()
                .Where(o => o.ProductionOrderId == pro.Id)
                .OrderBy(o => o.SequenceNumber)
                .AsNoTracking()
                .ToListAsync(ct);

            // Load work centers for name resolution
            var wcIds = ops.Where(o => o.WorkCenterId > 0).Select(o => o.WorkCenterId).Distinct().ToList();
            var workCenters = await _db.Set<WorkCenter>()
                .Where(w => wcIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, w => w.Name, ct);

            // Run per-op readiness for the material-ready column
            var readinessResult = await _readiness.CheckOrderReadinessAsync(pro.Id, ct);
            var readinessLookup = new Dictionary<int, OperationReadiness>();
            if (readinessResult.IsSuccess && readinessResult.Value != null)
            {
                foreach (var opR in readinessResult.Value.Operations)
                    readinessLookup[opR.OperationId] = opR;
            }

            return ops.Select(o =>
            {
                workCenters.TryGetValue(o.WorkCenterId, out var wcName);
                readinessLookup.TryGetValue(o.Id, out var opReadiness);

                // Find the material readiness check specifically
                var matCheck = opReadiness?.Checks.FirstOrDefault(c => c.CheckName == "Materials Ready");
                var matReady = matCheck?.Status.ToString() ?? "Unknown";

                return new CockpitRoutingRow(
                    Id: o.Id,
                    Sequence: o.SequenceNumber,
                    OperationCode: o.OperationType.ToString(),
                    Description: o.Description,
                    WorkCenterName: wcName,
                    ResourceName: null,  // TODO: specific resource when entity exists
                    Status: o.Status.ToString(),
                    PlannedStart: o.PlannedStart,
                    PlannedFinish: o.PlannedEnd,
                    ActualStart: o.ActualStart,
                    ActualFinish: o.ActualEnd,
                    SetupEstimate: o.PlannedSetupMins,
                    SetupActual: o.ActualSetupMins,
                    RunEstimate: o.PlannedRunMins,
                    RunActual: o.ActualRunMins,
                    LaborEstimate: o.PlannedSetupMins + o.PlannedRunMins,
                    LaborActual: o.ActualSetupMins + o.ActualRunMins,
                    GoodQty: o.CompletedQty,
                    ScrapQty: o.ScrappedQty,
                    ReworkQty: o.ReworkQty,
                    RemainingQty: Math.Max(0, o.PlannedQty - o.CompletedQty - o.ScrappedQty),
                    MaterialReady: matReady,
                    InspectionRequired: false,  // TODO: wire to per-op inspection flag when it exists
                    ActiveEmployee: null,  // TODO: join current labor entry
                    LastActivity: null,    // TODO: latest transaction timestamp
                    ReadinessStatus: opReadiness?.OverallStatus.ToString() ?? "Unknown",
                    ReadinessSummary: opReadiness != null
                        ? $"{opReadiness.Checks.Count(c => c.Status == ReadinessStatus.Pass)}/8 pass"
                        : null);
            }).ToList();
        }
    }
}
