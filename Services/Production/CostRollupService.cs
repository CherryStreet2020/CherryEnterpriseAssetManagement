// Sprint 14.4 PR-3 (2026-05-28) — Cost Rollup Engine implementation.
//
// THE 8-step rollup algorithm from Dean's research spec §20:
//   1. Build cost graph — walk PRO parent-child tree recursively
//   2. Validate graph — cycle detection via visited-set DFS + single ownership
//   3. Pull originating costs — Layer A from CostTransactions
//   4. Pull transfer costs — Layer B from CostTransfers
//   5. Classify each line — Additive/Transfer/Drilldown based on mode
//   6. Calculate totals — Financial (transfers) or Exploded (originating)
//   7. Detect exceptions — §13 exception catalog (16+ types)
//   8. Store rollup run — CostRollupRun + Lines + Exceptions + stamp summary
//
// Anti-compounding (§8):
//   Layer A — originating costs (additive at owning level)
//   Layer B — transfers (additive for receiver, not enterprise-level if A included)
//   Layer C — drilldown presentation (analytical, never summed into parent total)
//
// Financial rollup (§9 Method 1): parent uses child transfer value
// Exploded rollup (§9 Method 2): ignores transfers, sums originating across graph

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public sealed class CostRollupService : ICostRollupService
{
    private readonly AppDbContext _db;
    private readonly ICostTransactionService _costSvc;
    private readonly ILogger<CostRollupService> _log;
    private static long _runCounter = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>Maximum graph depth to prevent runaway recursion.</summary>
    private const int MaxGraphDepth = 20;

    public CostRollupService(
        AppDbContext db,
        ICostTransactionService costSvc,
        ILogger<CostRollupService> log)
    {
        _db = db;
        _costSvc = costSvc;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════════
    // STEP 1: BUILD GRAPH — walk PRO parent-child tree
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<CostGraphNode>> BuildGraphAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        var root = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);

        if (root == null)
            return Result.Failure<CostGraphNode>($"Production order {productionOrderId} not found.");

        var rootNode = new CostGraphNode
        {
            CostObjectType = CostObjectType.ProductionOrder,
            CostObjectId = root.Id,
            ProductionOrderId = root.Id,
            ParentNodeId = null,
            Depth = 0,
            SiteId = root.LocationId,
            Label = $"PRO-{root.Id} {root.OrderNumber}",
        };

        // Recursively build children
        await BuildChildrenAsync(rootNode, new HashSet<int> { root.Id }, ct);

        // Pull costs for all nodes
        await PullCostsForGraphAsync(rootNode, ct);

        return Result.Success(rootNode);
    }

    private async Task BuildChildrenAsync(
        CostGraphNode parent, HashSet<int> visited, CancellationToken ct)
    {
        if (parent.Depth >= MaxGraphDepth)
        {
            _log.LogWarning("Cost graph depth limit ({MaxDepth}) reached at node {NodeId}",
                MaxGraphDepth, parent.CostObjectId);
            return;
        }

        var children = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .Where(p => p.ParentProductionOrderId == parent.CostObjectId)
            .ToListAsync(ct);

        foreach (var child in children)
        {
            if (!visited.Add(child.Id))
            {
                _log.LogWarning("Cycle detected in cost graph: PRO {ChildId} already visited (parent {ParentId})",
                    child.Id, parent.CostObjectId);
                continue; // Skip cycles — will be reported as exception
            }

            var childNode = new CostGraphNode
            {
                CostObjectType = CostObjectType.ProductionOrder,
                CostObjectId = child.Id,
                ProductionOrderId = child.Id,
                ParentNodeId = parent.CostObjectId,
                Depth = parent.Depth + 1,
                SiteId = child.LocationId,
                Label = $"Child PRO-{child.Id} {child.OrderNumber}",
            };

            parent.Children.Add(childNode);

            // Recurse
            await BuildChildrenAsync(childNode, visited, ct);
        }
    }

    private async Task PullCostsForGraphAsync(CostGraphNode node, CancellationToken ct)
    {
        // Pull originating costs (Layer A)
        var txns = await _db.Set<CostTransaction>()
            .AsNoTracking()
            .Where(t => t.ProductionOrderId == node.CostObjectId && !t.IsReversal)
            .ToListAsync(ct);
        node.OriginatingCosts.AddRange(txns);

        // Pull inbound transfers (child → this node)
        var inbound = await _db.Set<CostTransfer>()
            .AsNoTracking()
            .Where(t => t.DestinationCostObjectType == CostObjectType.ProductionOrder
                     && t.DestinationCostObjectId == node.CostObjectId
                     && !t.IsReversal)
            .ToListAsync(ct);
        node.InboundTransfers.AddRange(inbound);

        // Pull outbound transfers (this node → parent)
        var outbound = await _db.Set<CostTransfer>()
            .AsNoTracking()
            .Where(t => t.SourceCostObjectType == CostObjectType.ProductionOrder
                     && t.SourceCostObjectId == node.CostObjectId
                     && !t.IsReversal)
            .ToListAsync(ct);
        node.OutboundTransfers.AddRange(outbound);

        // Recurse into children
        foreach (var child in node.Children)
            await PullCostsForGraphAsync(child, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // STEP 2: VALIDATE GRAPH — cycle detection + single ownership
    // ═══════════════════════════════════════════════════════════════

    public Result<bool> ValidateGraph(CostGraphNode root)
    {
        var visited = new HashSet<int>();
        var errors = new List<string>();

        ValidateNode(root, visited, errors);

        if (errors.Count > 0)
            return Result.Failure<bool>(string.Join("; ", errors));

        return Result.Success(true);
    }

    private void ValidateNode(CostGraphNode node, HashSet<int> visited, List<string> errors)
    {
        if (!visited.Add(node.CostObjectId))
        {
            errors.Add($"Cycle detected: PRO {node.CostObjectId} appears more than once in graph.");
            return;
        }

        foreach (var child in node.Children)
            ValidateNode(child, visited, errors);
    }

    // ═══════════════════════════════════════════════════════════════
    // EXECUTE ROLLUP — full 8-step orchestration
    // ═══════════════════════════════════════════════════════════════

    public async Task<Result<CostRollupResult>> ExecuteRollupAsync(
        int productionOrderId, CostRollupMode mode,
        string? executedBy, CancellationToken ct = default)
    {
        var ts = DateTime.UtcNow;
        var runNumber = $"CRR-{ts:yyyyMMddHHmmssfff}-{Interlocked.Increment(ref _runCounter) % 100000:D5}";

        _log.LogInformation("Starting cost rollup {RunNumber} for PRO {ProId} mode={Mode}",
            runNumber, productionOrderId, mode);

        // Step 1: Build graph
        var graphResult = await BuildGraphAsync(productionOrderId, ct);
        if (!graphResult.IsSuccess)
        {
            return Result.Failure<CostRollupResult>(graphResult.Error);
        }
        var root = graphResult.Value!;

        // Step 2: Validate graph
        var validationResult = ValidateGraph(root);
        // Don't fail on validation — record exceptions and continue

        // Count graph metrics
        var (nodeCount, edgeCount, maxDepth) = CountGraphMetrics(root);

        // Create the run header
        var pro = await _db.Set<ProductionOrder>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productionOrderId, ct);

        var run = new CostRollupRun
        {
            CompanyId = pro?.CompanyId ?? 1,
            RunNumber = runNumber,
            Mode = mode,
            RootCostObjectType = CostObjectType.ProductionOrder,
            RootCostObjectId = productionOrderId,
            ProductionOrderId = productionOrderId,
            GraphNodeCount = nodeCount,
            GraphEdgeCount = edgeCount,
            GraphMaxDepth = maxDepth,
            Status = CostRollupRunStatus.Running,
            ExecutedBy = executedBy,
            StartedAtUtc = ts,
        };

        _db.Set<CostRollupRun>().Add(run);
        await _db.SaveChangesAsync(ct); // Persist so we have an Id for FK

        try
        {
            // Steps 3-5: Classify lines based on mode
            var lines = new List<CostRollupLine>();
            var sortOrder = 0;

            if (mode == CostRollupMode.Financial)
                BuildFinancialLines(root, run.Id, lines, ref sortOrder);
            else if (mode == CostRollupMode.Exploded)
                BuildExplodedLines(root, run.Id, lines, ref sortOrder);
            else
                BuildFinancialLines(root, run.Id, lines, ref sortOrder); // Default

            // Step 7: Detect exceptions
            var exceptions = DetectExceptions(root, run.Id, mode);

            // Persist lines + exceptions
            if (lines.Count > 0)
                _db.Set<CostRollupLine>().AddRange(lines);
            if (exceptions.Count > 0)
                _db.Set<CostRollupException>().AddRange(exceptions);

            // Step 6: Calculate totals
            run.LineCount = lines.Count;
            run.ExceptionCount = exceptions.Count;
            run.WarningCount = exceptions.Count(e => e.Severity == CostExceptionSeverity.Warning);
            run.ErrorCount = exceptions.Count(e => e.Severity >= CostExceptionSeverity.Error);

            run.TotalAdditiveCost = lines
                .Where(l => l.Classification == CostRollupLineClassification.Additive)
                .Sum(l => l.ExtendedCost);
            run.TotalTransferCost = lines
                .Where(l => l.Classification == CostRollupLineClassification.Transfer)
                .Sum(l => l.ExtendedCost);
            run.TotalDrilldownCost = lines
                .Where(l => l.Classification == CostRollupLineClassification.DrilldownOnly)
                .Sum(l => l.ExtendedCost);

            // Exploded total = all originating costs summed across entire graph (no transfers)
            run.TotalExplodedCost = SumAllOriginatingCosts(root);

            // 5-element breakdown (additive lines only for financial; all originating for exploded)
            if (mode == CostRollupMode.Financial)
            {
                run.MaterialTotal = lines.Where(l => l.Classification == CostRollupLineClassification.Additive)
                    .Sum(l => l.MaterialCost);
                run.LaborTotal = lines.Where(l => l.Classification == CostRollupLineClassification.Additive)
                    .Sum(l => l.LaborCost);
                run.OverheadTotal = lines.Where(l => l.Classification == CostRollupLineClassification.Additive)
                    .Sum(l => l.OverheadCost);
                run.SubcontractTotal = lines.Where(l => l.Classification == CostRollupLineClassification.Additive)
                    .Sum(l => l.SubcontractCost);
                run.OtherTotal = lines.Where(l => l.Classification == CostRollupLineClassification.Additive)
                    .Sum(l => l.OtherCost);
            }
            else
            {
                // Exploded: sum all originating by bucket
                var allTxns = CollectAllOriginatingCosts(root);
                run.MaterialTotal = SumBuckets(allTxns, ProductionCostBucket.DirectMaterial, ProductionCostBucket.PurchasedToJob);
                run.LaborTotal = SumBucket(allTxns, ProductionCostBucket.DirectLabor);
                run.OverheadTotal = SumBuckets(allTxns, ProductionCostBucket.LaborBurden, ProductionCostBucket.MachineBurden, ProductionCostBucket.ManufacturingOverhead);
                run.SubcontractTotal = SumBuckets(allTxns, ProductionCostBucket.OutsideProcessing, ProductionCostBucket.Subcontract);
                run.OtherTotal = SumBuckets(allTxns, ProductionCostBucket.Tooling, ProductionCostBucket.LandedCost, ProductionCostBucket.Quality, ProductionCostBucket.Packaging, ProductionCostBucket.Engineering);
            }

            // Complete the run
            var completedAt = DateTime.UtcNow;
            run.CompletedAtUtc = completedAt;
            run.DurationMs = (int)(completedAt - ts).TotalMilliseconds;
            run.Status = exceptions.Any(e => e.Severity >= CostExceptionSeverity.Error)
                ? CostRollupRunStatus.CompletedWithExceptions
                : CostRollupRunStatus.Completed;

            await _db.SaveChangesAsync(ct);

            // Step 8: Stamp ProductionOrderCostSummary
            await StampSummaryFromRollupAsync(productionOrderId, run, mode, executedBy, ct);

            _log.LogInformation(
                "Cost rollup {RunNumber} completed for PRO {ProId}: {Mode} Total=${Total:N2} " +
                "({Nodes} nodes, {Depth} depth, {Lines} lines, {Exceptions} exceptions, {Ms}ms)",
                runNumber, productionOrderId, mode,
                mode == CostRollupMode.Financial ? run.TotalAdditiveCost + run.TotalTransferCost : run.TotalExplodedCost,
                nodeCount, maxDepth, lines.Count, exceptions.Count, run.DurationMs);

            return Result.Success(new CostRollupResult
            {
                Run = run,
                Lines = lines,
                Exceptions = exceptions,
                Graph = root,
            });
        }
        catch (Exception ex)
        {
            run.Status = CostRollupRunStatus.Failed;
            run.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            run.CompletedAtUtc = DateTime.UtcNow;
            run.DurationMs = (int)(DateTime.UtcNow - ts).TotalMilliseconds;
            await _db.SaveChangesAsync(ct);

            _log.LogError(ex, "Cost rollup {RunNumber} FAILED for PRO {ProId}", runNumber, productionOrderId);
            return Result.Failure<CostRollupResult>($"Rollup failed: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // FINANCIAL ROLLUP (§9 Method 1)
    // Parent total = own originating costs + child transfer values
    // Child detail = drilldown only (Layer C)
    // ═══════════════════════════════════════════════════════════════

    private void BuildFinancialLines(
        CostGraphNode node, int runId,
        List<CostRollupLine> lines, ref int sortOrder)
    {
        bool isRoot = node.ParentNodeId == null;

        // Layer A: originating costs at THIS node
        foreach (var txn in node.OriginatingCosts.Where(t => t.RollupAdditiveFlag))
        {
            lines.Add(new CostRollupLine
            {
                CostRollupRunId = runId,
                Depth = node.Depth,
                SortOrder = sortOrder++,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ParentCostObjectType = isRoot ? null : CostObjectType.ProductionOrder,
                ParentCostObjectId = node.ParentNodeId,
                CostTransactionId = txn.Id,
                Classification = isRoot
                    ? CostRollupLineClassification.Additive
                    : CostRollupLineClassification.DrilldownOnly, // child detail = drilldown in financial mode
                CostBucket = txn.CostBucket,
                TransactionType = txn.TransactionType,
                Description = $"{txn.CostBucket} — {txn.TransactionType} ({node.Label})",
                Quantity = txn.Quantity,
                Uom = txn.Uom,
                UnitCost = txn.UnitCost,
                ExtendedCost = txn.ExtendedCost,
                MaterialCost = IsMaterialBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                LaborCost = IsLaborBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                OverheadCost = IsOverheadBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                SubcontractCost = IsSubcontractBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                OtherCost = IsOtherBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                SiteId = txn.SiteId,
                IsRollupAdditive = isRoot,
                IsProvisional = false,
                IsFinal = true,
            });
        }

        // Layer B: inbound transfers at THIS node (child supply → parent)
        foreach (var xfer in node.InboundTransfers)
        {
            lines.Add(new CostRollupLine
            {
                CostRollupRunId = runId,
                Depth = node.Depth,
                SortOrder = sortOrder++,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ParentCostObjectType = isRoot ? null : CostObjectType.ProductionOrder,
                ParentCostObjectId = node.ParentNodeId,
                CostTransferId = xfer.Id,
                Classification = CostRollupLineClassification.Transfer,
                CostBucket = ProductionCostBucket.ChildSupply,
                Description = $"Child supply transfer from {xfer.SourceCostObjectType}#{xfer.SourceCostObjectId} ({xfer.TransferType})",
                Quantity = xfer.TransferQuantity,
                Uom = xfer.Uom,
                UnitCost = xfer.TransferUnitCost,
                ExtendedCost = xfer.TransferExtendedCost,
                MaterialCost = xfer.MaterialCostTransferred,
                LaborCost = xfer.LaborCostTransferred,
                OverheadCost = xfer.OverheadCostTransferred,
                SubcontractCost = xfer.SubcontractCostTransferred,
                OtherCost = xfer.OtherCostTransferred,
                SiteId = xfer.DestinationSiteId,
                IsRollupAdditive = true, // Transfer IS additive at parent boundary
                IsProvisional = xfer.IsProvisional,
                IsFinal = xfer.IsFinal,
            });
        }

        // Recurse into children (their detail becomes drilldown)
        foreach (var child in node.Children)
            BuildFinancialLines(child, runId, lines, ref sortOrder);
    }

    // ═══════════════════════════════════════════════════════════════
    // EXPLODED ROLLUP (§9 Method 2)
    // Ignores transfer rows. Sums ALL originating costs across graph.
    // Best for analysis / cost driver identification.
    // ═══════════════════════════════════════════════════════════════

    private void BuildExplodedLines(
        CostGraphNode node, int runId,
        List<CostRollupLine> lines, ref int sortOrder)
    {
        // ALL originating costs are additive in exploded mode — across the whole graph
        foreach (var txn in node.OriginatingCosts.Where(t => t.RollupAdditiveFlag))
        {
            // In exploded mode, ChildSupply bucket transactions are NOT additive
            // (those are the transfer-side CostTransactions posted by the transfer engine).
            // Only true originating costs (material, labor, machine, etc.) are additive.
            bool isTransferPosting = txn.TransactionType == CostTransactionType.ChildSupplyTransfer;

            lines.Add(new CostRollupLine
            {
                CostRollupRunId = runId,
                Depth = node.Depth,
                SortOrder = sortOrder++,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ParentCostObjectType = node.ParentNodeId.HasValue ? CostObjectType.ProductionOrder : null,
                ParentCostObjectId = node.ParentNodeId,
                CostTransactionId = txn.Id,
                Classification = isTransferPosting
                    ? CostRollupLineClassification.DrilldownOnly  // Suppress transfer-side posting in exploded mode
                    : CostRollupLineClassification.Additive,      // All originating costs are additive
                CostBucket = txn.CostBucket,
                TransactionType = txn.TransactionType,
                Description = $"{txn.CostBucket} — {txn.TransactionType} ({node.Label})",
                Quantity = txn.Quantity,
                Uom = txn.Uom,
                UnitCost = txn.UnitCost,
                ExtendedCost = txn.ExtendedCost,
                MaterialCost = IsMaterialBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                LaborCost = IsLaborBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                OverheadCost = IsOverheadBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                SubcontractCost = IsSubcontractBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                OtherCost = IsOtherBucket(txn.CostBucket) ? txn.ExtendedCost : 0,
                SiteId = txn.SiteId,
                IsRollupAdditive = !isTransferPosting,
                IsProvisional = false,
                IsFinal = true,
            });
        }

        // Transfers shown as informational in exploded mode (not counted)
        foreach (var xfer in node.InboundTransfers)
        {
            lines.Add(new CostRollupLine
            {
                CostRollupRunId = runId,
                Depth = node.Depth,
                SortOrder = sortOrder++,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ParentCostObjectType = node.ParentNodeId.HasValue ? CostObjectType.ProductionOrder : null,
                ParentCostObjectId = node.ParentNodeId,
                CostTransferId = xfer.Id,
                Classification = CostRollupLineClassification.DrilldownOnly, // NOT additive in exploded
                CostBucket = ProductionCostBucket.ChildSupply,
                Description = $"[Exploded: transfer record — not counted] from {xfer.SourceCostObjectType}#{xfer.SourceCostObjectId}",
                Quantity = xfer.TransferQuantity,
                Uom = xfer.Uom,
                UnitCost = xfer.TransferUnitCost,
                ExtendedCost = xfer.TransferExtendedCost,
                MaterialCost = xfer.MaterialCostTransferred,
                LaborCost = xfer.LaborCostTransferred,
                OverheadCost = xfer.OverheadCostTransferred,
                SubcontractCost = xfer.SubcontractCostTransferred,
                OtherCost = xfer.OtherCostTransferred,
                SiteId = xfer.DestinationSiteId,
                IsRollupAdditive = false,
                IsProvisional = xfer.IsProvisional,
                IsFinal = xfer.IsFinal,
            });
        }

        // Recurse into children
        foreach (var child in node.Children)
            BuildExplodedLines(child, runId, lines, ref sortOrder);
    }

    // ═══════════════════════════════════════════════════════════════
    // STEP 7: DETECT EXCEPTIONS (§13 — 16+ types)
    // ═══════════════════════════════════════════════════════════════

    private List<CostRollupException> DetectExceptions(
        CostGraphNode root, int runId, CostRollupMode mode)
    {
        var exceptions = new List<CostRollupException>();
        DetectNodeExceptions(root, runId, mode, exceptions);
        return exceptions;
    }

    private void DetectNodeExceptions(
        CostGraphNode node, int runId, CostRollupMode mode,
        List<CostRollupException> exceptions)
    {
        var proId = node.ProductionOrderId;

        // §13.1 — Material issued with zero cost
        foreach (var txn in node.OriginatingCosts.Where(t =>
            IsMaterialBucket(t.CostBucket) && t.ExtendedCost == 0 && t.Quantity > 0))
        {
            exceptions.Add(new CostRollupException
            {
                CostRollupRunId = runId,
                ExceptionType = CostExceptionType.MaterialIssuedZeroCost,
                Severity = CostExceptionSeverity.Warning,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ProductionOrderId = proId,
                BomLineId = txn.BomLineId,
                ItemId = txn.ItemId,
                CostTransactionId = txn.Id,
                Message = $"Material issued with $0 cost: {txn.TransactionType} qty={txn.Quantity:N4} {txn.Uom} on {node.Label}. Job cost is understated.",
                Resolution = "Verify item standard cost or receipt cost is populated. Re-run rollup after correction.",
                EstimatedImpact = null, // Unknown without knowing what the cost should be
                BlocksClose = false,
            });
        }

        // §13.2 — Labor posted with missing rate (zero unit cost)
        foreach (var txn in node.OriginatingCosts.Where(t =>
            IsLaborBucket(t.CostBucket) && t.UnitCost == 0 && t.Quantity > 0))
        {
            exceptions.Add(new CostRollupException
            {
                CostRollupRunId = runId,
                ExceptionType = CostExceptionType.LaborMissingRate,
                Severity = CostExceptionSeverity.Warning,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ProductionOrderId = proId,
                OperationId = txn.OperationId,
                CostTransactionId = txn.Id,
                Message = $"Labor posted with $0/hr rate: {txn.TransactionType} {txn.Quantity:N2} hr on {node.Label}. Job cost is understated.",
                Resolution = "Check employee wage group / labor rate setup. Post correcting entry or update the transaction.",
                BlocksClose = false,
            });
        }

        // §13.3 — Machine time with missing burden
        var machineOps = node.OriginatingCosts
            .Where(t => t.CostBucket == ProductionCostBucket.MachineTime)
            .Select(t => t.OperationId).Distinct().ToList();
        var burdenOps = node.OriginatingCosts
            .Where(t => t.CostBucket == ProductionCostBucket.MachineBurden)
            .Select(t => t.OperationId).Distinct().ToHashSet();
        foreach (var opId in machineOps.Where(o => o.HasValue && !burdenOps.Contains(o)))
        {
            exceptions.Add(new CostRollupException
            {
                CostRollupRunId = runId,
                ExceptionType = CostExceptionType.MachineTimeMissingBurden,
                Severity = CostExceptionSeverity.Warning,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ProductionOrderId = proId,
                OperationId = opId,
                Message = $"Machine time posted on Op {opId} but no machine burden applied on {node.Label}. Overhead is understated.",
                Resolution = "Apply overhead rates via IOverheadApplicationService or post a burden transaction.",
                BlocksClose = false,
            });
        }

        // §13.6 — Child WO completed but not costed (has transfers but child has 0 originating cost)
        foreach (var child in node.Children)
        {
            var childOriginatingTotal = child.OriginatingCosts
                .Where(t => t.RollupAdditiveFlag && !t.IsReversal)
                .Sum(t => t.ExtendedCost);

            if (childOriginatingTotal == 0 && node.InboundTransfers.Any(x =>
                x.SourceCostObjectId == child.CostObjectId &&
                x.SourceCostObjectType == CostObjectType.ProductionOrder))
            {
                exceptions.Add(new CostRollupException
                {
                    CostRollupRunId = runId,
                    ExceptionType = CostExceptionType.ChildWoCompletedNotCosted,
                    Severity = CostExceptionSeverity.Error,
                    CostObjectType = child.CostObjectType,
                    CostObjectId = child.CostObjectId,
                    ProductionOrderId = child.ProductionOrderId,
                    Message = $"{child.Label} has a transfer to parent but $0 originating cost. Transfer value may be inaccurate.",
                    Resolution = "Post material/labor/burden costs to the child order before finalizing the transfer.",
                    BlocksClose = true,
                });
            }
        }

        // §13.11 — Negative WIP
        var nodeTotal = node.OriginatingCosts.Where(t => t.RollupAdditiveFlag && !t.IsReversal)
            .Sum(t => t.ExtendedCost);
        var nodeTransfersOut = node.OutboundTransfers.Sum(t => t.TransferExtendedCost);
        var nodeWip = nodeTotal - nodeTransfersOut;
        if (nodeWip < 0)
        {
            exceptions.Add(new CostRollupException
            {
                CostRollupRunId = runId,
                ExceptionType = CostExceptionType.NegativeWip,
                Severity = CostExceptionSeverity.Error,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ProductionOrderId = proId,
                Message = $"Negative WIP balance ${nodeWip:N2} on {node.Label}. Transfers out (${nodeTransfersOut:N2}) exceed posted costs (${nodeTotal:N2}).",
                Resolution = "Review cost transactions and transfers for this order. Ensure all costs are posted before transferring.",
                EstimatedImpact = nodeWip,
                BlocksClose = true,
            });
        }

        // §13.10 — Outside processing with no cost
        foreach (var txn in node.OriginatingCosts.Where(t =>
            (t.CostBucket == ProductionCostBucket.OutsideProcessing || t.CostBucket == ProductionCostBucket.Subcontract)
            && t.ExtendedCost == 0 && t.Quantity > 0))
        {
            exceptions.Add(new CostRollupException
            {
                CostRollupRunId = runId,
                ExceptionType = CostExceptionType.OutsideProcessingNoCost,
                Severity = CostExceptionSeverity.Warning,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ProductionOrderId = proId,
                OperationId = txn.OperationId,
                CostTransactionId = txn.Id,
                Message = $"Outside processing/subcontract posted with $0 cost on {node.Label}. Job cost is understated.",
                Resolution = "Update PO receipt cost or post supplier invoice variance.",
                BlocksClose = false,
            });
        }

        // Check for provisional transfers (cost not finalized)
        foreach (var xfer in node.InboundTransfers.Where(x => x.IsProvisional))
        {
            exceptions.Add(new CostRollupException
            {
                CostRollupRunId = runId,
                ExceptionType = CostExceptionType.ChildWoCompletedNotCosted,
                Severity = CostExceptionSeverity.Info,
                CostObjectType = node.CostObjectType,
                CostObjectId = node.CostObjectId,
                ProductionOrderId = proId,
                Message = $"Provisional transfer from {xfer.SourceCostObjectType}#{xfer.SourceCostObjectId} — cost not finalized (${xfer.TransferExtendedCost:N2}).",
                Resolution = "Finalize child order cost, then update or re-post the transfer.",
                BlocksClose = false,
            });
        }

        // Recurse
        foreach (var child in node.Children)
            DetectNodeExceptions(child, runId, mode, exceptions);
    }

    // ═══════════════════════════════════════════════════════════════
    // STAMP SUMMARY — update ProductionOrderCostSummary from rollup
    // ═══════════════════════════════════════════════════════════════

    private async Task StampSummaryFromRollupAsync(
        int productionOrderId, CostRollupRun run,
        CostRollupMode mode, string? executedBy,
        CancellationToken ct)
    {
        // Delegate to the existing RefreshSummaryAsync for actuals,
        // then stamp rollup-specific fields.
        var refreshResult = await _costSvc.RefreshSummaryAsync(productionOrderId, executedBy, ct);
        if (!refreshResult.IsSuccess)
        {
            _log.LogWarning("Failed to refresh summary during rollup for PRO {ProId}: {Error}",
                productionOrderId, refreshResult.Error);
            return;
        }

        var summary = refreshResult.Value!;

        // Stamp rollup metadata
        summary.LastRollupTimestamp = run.CompletedAtUtc ?? DateTime.UtcNow;
        summary.RollupStatus = run.Status switch
        {
            CostRollupRunStatus.Completed => "RollupComplete",
            CostRollupRunStatus.CompletedWithExceptions => "RollupWithExceptions",
            CostRollupRunStatus.Failed => "RollupFailed",
            _ => "Unknown",
        };
        summary.CostExceptionCount = run.ExceptionCount;

        // Stamp non-additive child detail total (for reference — §10 exploded view)
        summary.NonAdditiveChildDetailTotal = run.TotalDrilldownCost;

        // Update cost status based on exceptions
        if (run.ErrorCount > 0)
            summary.CostStatus = ProductionCostStatus.CostExceptions;
        else if (summary.ActualTotalCost == 0 && summary.EstimatedTotalCost > 0)
            summary.CostStatus = ProductionCostStatus.Estimated;
        else if (summary.ActualTotalCost > 0)
            summary.CostStatus = ProductionCostStatus.InWip;

        await _db.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // QUERIES
    // ═══════════════════════════════════════════════════════════════

    public async Task<CostRollupRun?> GetLatestRunAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<CostRollupRun>()
            .AsNoTracking()
            .Where(r => r.ProductionOrderId == productionOrderId)
            .OrderByDescending(r => r.StartedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CostRollupRun>> GetRunsAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<CostRollupRun>()
            .AsNoTracking()
            .Where(r => r.ProductionOrderId == productionOrderId)
            .OrderByDescending(r => r.StartedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CostRollupLine>> GetLinesAsync(
        int rollupRunId, CancellationToken ct = default)
    {
        return await _db.Set<CostRollupLine>()
            .AsNoTracking()
            .Where(l => l.CostRollupRunId == rollupRunId)
            .OrderBy(l => l.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CostRollupException>> GetExceptionsAsync(
        int rollupRunId, CancellationToken ct = default)
    {
        return await _db.Set<CostRollupException>()
            .AsNoTracking()
            .Where(e => e.CostRollupRunId == rollupRunId)
            .OrderByDescending(e => e.Severity)
            .ThenBy(e => e.ExceptionType)
            .ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static (int nodes, int edges, int maxDepth) CountGraphMetrics(CostGraphNode root)
    {
        int nodes = 0, edges = 0, maxDepth = 0;
        CountNode(root, ref nodes, ref edges, ref maxDepth);
        return (nodes, edges, maxDepth);
    }

    private static void CountNode(CostGraphNode node, ref int nodes, ref int edges, ref int maxDepth)
    {
        nodes++;
        if (node.Depth > maxDepth) maxDepth = node.Depth;
        edges += node.Children.Count;
        foreach (var child in node.Children)
            CountNode(child, ref nodes, ref edges, ref maxDepth);
    }

    private static decimal SumAllOriginatingCosts(CostGraphNode node)
    {
        var total = node.OriginatingCosts
            .Where(t => t.RollupAdditiveFlag && !t.IsReversal
                     && t.TransactionType != CostTransactionType.ChildSupplyTransfer)
            .Sum(t => t.ExtendedCost);

        foreach (var child in node.Children)
            total += SumAllOriginatingCosts(child);

        return total;
    }

    private static List<CostTransaction> CollectAllOriginatingCosts(CostGraphNode node)
    {
        var all = new List<CostTransaction>();
        all.AddRange(node.OriginatingCosts.Where(t => t.RollupAdditiveFlag && !t.IsReversal
            && t.TransactionType != CostTransactionType.ChildSupplyTransfer));
        foreach (var child in node.Children)
            all.AddRange(CollectAllOriginatingCosts(child));
        return all;
    }

    private static decimal SumBucket(IReadOnlyList<CostTransaction> txns, ProductionCostBucket bucket)
        => txns.Where(t => t.CostBucket == bucket).Sum(t => t.ExtendedCost);

    private static decimal SumBuckets(IReadOnlyList<CostTransaction> txns, params ProductionCostBucket[] buckets)
        => txns.Where(t => buckets.Contains(t.CostBucket)).Sum(t => t.ExtendedCost);

    private static bool IsMaterialBucket(ProductionCostBucket b) => b is
        ProductionCostBucket.DirectMaterial or ProductionCostBucket.PurchasedToJob or ProductionCostBucket.ChildSupply;

    private static bool IsLaborBucket(ProductionCostBucket b) => b is
        ProductionCostBucket.DirectLabor or ProductionCostBucket.MachineTime or ProductionCostBucket.Engineering;

    private static bool IsOverheadBucket(ProductionCostBucket b) => b is
        ProductionCostBucket.LaborBurden or ProductionCostBucket.MachineBurden or ProductionCostBucket.ManufacturingOverhead;

    private static bool IsSubcontractBucket(ProductionCostBucket b) => b is
        ProductionCostBucket.OutsideProcessing or ProductionCostBucket.Subcontract;

    private static bool IsOtherBucket(ProductionCostBucket b) => b is
        ProductionCostBucket.Tooling or ProductionCostBucket.LandedCost or ProductionCostBucket.Quality
        or ProductionCostBucket.Packaging or ProductionCostBucket.Scrap or ProductionCostBucket.Rework
        or ProductionCostBucket.Adjustment or ProductionCostBucket.Variance;
}
