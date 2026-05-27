// B8 PR-PRO-4 (2026-05-27) — Production operation transaction service.
// 19 actions with state machine enforcement. Each action atomically
// creates a transaction record + updates ProductionOperation status/quantities.
// Absorbs B3 (mixed PO modes) + B5b (subcontract auto-complete on receipt).

using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production
{
    public class ProductionOperationTransactionService : IProductionOperationTransactionService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ProductionOperationTransactionService> _log;

        public ProductionOperationTransactionService(AppDbContext db, ILogger<ProductionOperationTransactionService> log)
        { _db = db; _log = log; }

        // =====================================================================
        // 1. START — Released → Running (or InSetup if setup required)
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> StartAsync(
            int operationId, string performedBy, int? assetId = null, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Released)
                return Fail($"Cannot start — status is {op.Status}, expected Released.");

            var before = op.Status;
            op.Status = op.PlannedSetupMins > 0 ? ProductionOperationStatus.InSetup : ProductionOperationStatus.Running;
            op.ActualStart = DateTime.UtcNow;
            if (assetId.HasValue) op.AssetId = assetId.Value;
            Stamp(op, performedBy);

            return await Post(op, OperationTransactionType.Start, before, performedBy, assetId: assetId, ct: ct);
        }

        // =====================================================================
        // 2. PAUSE — Running → Paused
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> PauseAsync(
            int operationId, string performedBy, string? reason = null, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Running)
                return Fail($"Cannot pause — status is {op.Status}, expected Running.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Paused;
            Stamp(op, performedBy);
            return await Post(op, OperationTransactionType.Pause, before, performedBy, notes: reason, ct: ct);
        }

        // =====================================================================
        // 3. RESUME — Paused → Running
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> ResumeAsync(
            int operationId, string performedBy, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Paused)
                return Fail($"Cannot resume — status is {op.Status}, expected Paused.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Running;
            Stamp(op, performedBy);
            return await Post(op, OperationTransactionType.Resume, before, performedBy, ct: ct);
        }

        // =====================================================================
        // 4. STOP — Running → Released (abnormal stop)
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> StopAsync(
            int operationId, string performedBy, string? reason = null, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Running && op.Status != ProductionOperationStatus.InSetup)
                return Fail($"Cannot stop — status is {op.Status}, expected Running or InSetup.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Released;
            Stamp(op, performedBy);
            return await Post(op, OperationTransactionType.Stop, before, performedBy, notes: reason, ct: ct);
        }

        // =====================================================================
        // 5. START SETUP — Released → InSetup
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> StartSetupAsync(
            int operationId, string performedBy, int? assetId = null, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Released)
                return Fail($"Cannot start setup — status is {op.Status}, expected Released.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.InSetup;
            op.ActualStart ??= DateTime.UtcNow;
            if (assetId.HasValue) op.AssetId = assetId.Value;
            Stamp(op, performedBy);
            return await Post(op, OperationTransactionType.StartSetup, before, performedBy, assetId: assetId, ct: ct);
        }

        // =====================================================================
        // 6. COMPLETE SETUP — InSetup → Running
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> CompleteSetupAsync(
            int operationId, string performedBy, decimal setupMinutes, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.InSetup)
                return Fail($"Cannot complete setup — status is {op.Status}, expected InSetup.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Running;
            op.ActualSetupMins += setupMinutes;
            Stamp(op, performedBy);
            return await Post(op, OperationTransactionType.CompleteSetup, before, performedBy,
                setupMinutes: setupMinutes, ct: ct);
        }

        // =====================================================================
        // 7. START RUN — InSetup → Running (explicit run start after setup)
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> StartRunAsync(
            int operationId, string performedBy, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.InSetup)
                return Fail($"Cannot start run — status is {op.Status}, expected InSetup.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Running;
            Stamp(op, performedBy);
            return await Post(op, OperationTransactionType.StartRun, before, performedBy, ct: ct);
        }

        // =====================================================================
        // 8. COMPLETE RUN — Running → Completed (run phase done)
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> CompleteRunAsync(
            int operationId, string performedBy, decimal runMinutes, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Running)
                return Fail($"Cannot complete run — status is {op.Status}, expected Running.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Completed;
            op.ActualRunMins += runMinutes;
            op.ActualEnd = DateTime.UtcNow;
            Stamp(op, performedBy);
            return await Post(op, OperationTransactionType.CompleteRun, before, performedBy,
                runMinutes: runMinutes, ct: ct);
        }

        // =====================================================================
        // 9. COMPLETE — generic completion with quantities
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> CompleteAsync(
            CompleteOperationRequest req, CancellationToken ct = default)
        {
            var op = await FindOp(req.OperationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Running)
                return Fail($"Cannot complete — status is {op.Status}, expected Running.");
            if (req.GoodQuantity <= 0)
                return Fail("Good quantity must be greater than zero.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Completed;
            op.CompletedQty += req.GoodQuantity;
            op.ScrappedQty += req.ScrapQuantity;
            op.ReworkQty += req.ReworkQuantity;
            op.ActualEnd = DateTime.UtcNow;
            Stamp(op, req.PerformedBy);

            return await Post(op, OperationTransactionType.Complete, before, req.PerformedBy,
                goodQty: req.GoodQuantity, scrapQty: req.ScrapQuantity, reworkQty: req.ReworkQuantity,
                rejectQty: req.RejectQuantity, backflush: req.BackflushMaterials,
                lotSerials: req.CompletedLotSerials, destLoc: req.DestinationLocation,
                scrapReason: req.ScrapReasonCode, defect: req.DefectCode,
                inspection: req.InspectionRequired, notes: req.Notes, ct: ct);
        }

        // =====================================================================
        // 10. PARTIAL COMPLETE — report partial quantity, stay Running
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> PartialCompleteAsync(
            CompleteOperationRequest req, CancellationToken ct = default)
        {
            var op = await FindOp(req.OperationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Running)
                return Fail($"Cannot partial complete — status is {op.Status}, expected Running.");
            if (req.GoodQuantity <= 0)
                return Fail("Good quantity must be greater than zero.");

            var before = op.Status;
            // Stay Running — only increment quantities
            op.CompletedQty += req.GoodQuantity;
            op.ScrappedQty += req.ScrapQuantity;
            op.ReworkQty += req.ReworkQuantity;
            Stamp(op, req.PerformedBy);

            return await Post(op, OperationTransactionType.PartialComplete, before, req.PerformedBy,
                goodQty: req.GoodQuantity, scrapQty: req.ScrapQuantity, reworkQty: req.ReworkQuantity,
                notes: req.Notes, ct: ct);
        }

        // =====================================================================
        // 11. FINAL COMPLETE — complete + mark as final op → triggers FG receipt
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> FinalCompleteAsync(
            CompleteOperationRequest req, CancellationToken ct = default)
        {
            var op = await FindOp(req.OperationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Running)
                return Fail($"Cannot final complete — status is {op.Status}, expected Running.");
            if (req.GoodQuantity <= 0)
                return Fail("Good quantity must be greater than zero.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Completed;
            op.CompletedQty += req.GoodQuantity;
            op.ScrappedQty += req.ScrapQuantity;
            op.ReworkQty += req.ReworkQuantity;
            op.ActualEnd = DateTime.UtcNow;
            Stamp(op, req.PerformedBy);

            return await Post(op, OperationTransactionType.FinalComplete, before, req.PerformedBy,
                goodQty: req.GoodQuantity, scrapQty: req.ScrapQuantity, reworkQty: req.ReworkQuantity,
                isFinal: true, backflush: req.BackflushMaterials,
                lotSerials: req.CompletedLotSerials, destLoc: req.DestinationLocation,
                notes: req.Notes, ct: ct);
        }

        // =====================================================================
        // 12. REVERSE COMPLETION — Completed → Running
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> ReverseCompletionAsync(
            int operationId, string performedBy, string? reason = null, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Completed)
                return Fail($"Cannot reverse — status is {op.Status}, expected Completed.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Running;
            op.ActualEnd = null; // Clear the end timestamp
            Stamp(op, performedBy);

            var txn = await Post(op, OperationTransactionType.ReverseCompletion, before, performedBy, notes: reason, ct: ct);
            if (txn.IsSuccess) txn.Value!.IsReversal = true;
            return txn;
        }

        // =====================================================================
        // 13. SKIP OPERATION — Released → Skipped (optional ops only)
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> SkipOperationAsync(
            int operationId, string performedBy, string reason, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();
            if (op.Status != ProductionOperationStatus.Released)
                return Fail($"Cannot skip — status is {op.Status}, expected Released.");

            var before = op.Status;
            op.Status = ProductionOperationStatus.Skipped;
            op.SkipReason = reason;
            Stamp(op, performedBy);

            var txn = CreateTxn(op, OperationTransactionType.SkipOperation, before, performedBy);
            txn.SkipReason = reason;
            _db.Set<ProductionOperationTransaction>().Add(txn);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Op {Seq} on PRO {PRO} skipped: {Reason}", op.SequenceNumber, op.ProductionOrderId, reason);
            return Result.Success(txn);
        }

        // =====================================================================
        // 14. ADD OPERATION — insert new op at runtime
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> AddOperationAsync(
            AddOperationRequest req, CancellationToken ct = default)
        {
            var newSeq = req.AfterOperationSequence + 5; // Insert between existing sequences

            var newOp = new ProductionOperation
            {
                ProductionOrderId = req.ProductionOrderId,
                SequenceNumber = newSeq,
                Description = req.Description,
                WorkCenterId = req.WorkCenterId,
                PlannedRunMins = req.PlannedRunMins,
                PlannedSetupMins = req.PlannedSetupMins,
                Status = ProductionOperationStatus.Released,
                CreatedBy = req.PerformedBy,
            };
            _db.Set<ProductionOperation>().Add(newOp);
            await _db.SaveChangesAsync(ct); // Get the new Id

            var txn = new ProductionOperationTransaction
            {
                CompanyId = newOp.CompanyIdSnapshot,
                TransactionNumber = GenTxnNum("ADD", newOp.Id),
                TransactionType = OperationTransactionType.AddOperation,
                ProductionOrderId = req.ProductionOrderId,
                OperationId = newOp.Id,
                OperationSequence = newSeq,
                StatusBefore = ProductionOperationStatus.Released,
                StatusAfter = ProductionOperationStatus.Released,
                NewOperationId = newOp.Id,
                NewOperationSequence = newSeq,
                PerformedBy = req.PerformedBy,
                CreatedBy = req.PerformedBy,
                Notes = req.Notes,
            };
            _db.Set<ProductionOperationTransaction>().Add(txn);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Added op {Seq} '{Desc}' to PRO {PRO}", newSeq, req.Description, req.ProductionOrderId);
            return Result.Success(txn);
        }

        // =====================================================================
        // 15. INSERT REWORK OPERATION — insert rework op after current
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> InsertReworkOperationAsync(
            InsertReworkRequest req, CancellationToken ct = default)
        {
            var sourceOp = await FindOp(req.OperationId, ct);
            if (sourceOp is null) return OpNotFound();

            var reworkSeq = sourceOp.SequenceNumber + 5;
            var reworkOp = new ProductionOperation
            {
                ProductionOrderId = sourceOp.ProductionOrderId,
                SequenceNumber = reworkSeq,
                Description = $"REWORK — {sourceOp.Description}",
                WorkCenterId = req.WorkCenterId,
                PlannedRunMins = req.PlannedRunMins,
                OperationType = ProductionOperationType.Rework,
                Status = ProductionOperationStatus.Released,
                Instructions = req.ReworkInstructions,
                CreatedBy = req.PerformedBy,
            };
            _db.Set<ProductionOperation>().Add(reworkOp);
            await _db.SaveChangesAsync(ct);

            var txn = CreateTxn(sourceOp, OperationTransactionType.InsertReworkOperation,
                sourceOp.Status, req.PerformedBy);
            txn.NewOperationId = reworkOp.Id;
            txn.NewOperationSequence = reworkSeq;
            txn.ReworkInstructions = req.ReworkInstructions;
            txn.StatusAfter = sourceOp.Status; // Source op doesn't change

            _db.Set<ProductionOperationTransaction>().Add(txn);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Inserted rework op {Seq} after op {SrcSeq} on PRO {PRO}",
                reworkSeq, sourceOp.SequenceNumber, sourceOp.ProductionOrderId);
            return Result.Success(txn);
        }

        // =====================================================================
        // 16. CHANGE RESOURCE — reassign work center / machine
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> ChangeResourceAsync(
            int operationId, int newWorkCenterId, int? newAssetId, string performedBy,
            CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();

            var before = op.Status;
            var prevWc = op.WorkCenterId;
            var prevAsset = op.AssetId;

            op.WorkCenterId = newWorkCenterId;
            op.AssetId = newAssetId;
            Stamp(op, performedBy);

            var txn = CreateTxn(op, OperationTransactionType.ChangeResource, before, performedBy);
            txn.PreviousWorkCenterId = prevWc;
            txn.PreviousAssetId = prevAsset;
            txn.WorkCenterId = newWorkCenterId;
            txn.AssetId = newAssetId;
            txn.StatusAfter = before; // No status change

            _db.Set<ProductionOperationTransaction>().Add(txn);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Op {Seq} resource changed: WC {Old}→{New}, Asset {OldA}→{NewA}",
                op.SequenceNumber, prevWc, newWorkCenterId, prevAsset, newAssetId);
            return Result.Success(txn);
        }

        // =====================================================================
        // 17. ADD EMPLOYEE — add operator to the operation
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> AddEmployeeAsync(
            int operationId, string employeeId, string performedBy, CancellationToken ct = default)
        {
            var op = await FindOp(operationId, ct);
            if (op is null) return OpNotFound();

            var before = op.Status;
            var existing = op.OperatorUserIdsCsv ?? "";
            if (!existing.Contains(employeeId))
                op.OperatorUserIdsCsv = string.IsNullOrEmpty(existing) ? employeeId : $"{existing},{employeeId}";
            Stamp(op, performedBy);

            var txn = CreateTxn(op, OperationTransactionType.AddEmployee, before, performedBy);
            txn.OperatorId = employeeId;
            txn.StatusAfter = before; // No status change

            _db.Set<ProductionOperationTransaction>().Add(txn);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Employee {Emp} added to op {Seq} on PRO {PRO}",
                employeeId, op.SequenceNumber, op.ProductionOrderId);
            return Result.Success(txn);
        }

        // =====================================================================
        // 18. LOG TIME — record time without state change
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> LogTimeAsync(
            LogTimeRequest req, CancellationToken ct = default)
        {
            var op = await FindOp(req.OperationId, ct);
            if (op is null) return OpNotFound();

            var before = op.Status;
            op.ActualSetupMins += req.SetupMinutes;
            op.ActualRunMins += req.RunMinutes;
            Stamp(op, req.PerformedBy);

            return await Post(op, OperationTransactionType.LogTime, before, req.PerformedBy,
                runMinutes: req.RunMinutes, setupMinutes: req.SetupMinutes,
                machineMinutes: req.MachineMinutes, laborMinutes: req.LaborMinutes,
                laborCost: req.LaborCost, machineCost: req.MachineCost,
                crewSize: req.CrewSize, notes: req.Notes, ct: ct);
        }

        // =====================================================================
        // 19. EDIT TIME — modify a prior time entry (audit trail)
        // =====================================================================
        public async Task<Result<ProductionOperationTransaction>> EditTimeAsync(
            int originalTransactionId, decimal newRunMinutes, decimal newSetupMinutes,
            string editedBy, string reason, CancellationToken ct = default)
        {
            var original = await _db.Set<ProductionOperationTransaction>()
                .FindAsync(new object[] { originalTransactionId }, ct);
            if (original is null) return Fail("Original transaction not found.");
            if (original.TransactionType != OperationTransactionType.LogTime)
                return Fail("Can only edit LogTime transactions.");

            var op = await FindOp(original.OperationId, ct);
            if (op is null) return OpNotFound();

            // Adjust the operation's actual times
            var runDelta = newRunMinutes - original.RunMinutes;
            var setupDelta = newSetupMinutes - original.SetupMinutes;
            op.ActualRunMins += runDelta;
            op.ActualSetupMins += setupDelta;
            Stamp(op, editedBy);

            var txn = CreateTxn(op, OperationTransactionType.EditTime, op.Status, editedBy);
            txn.RunMinutes = newRunMinutes;
            txn.SetupMinutes = newSetupMinutes;
            txn.OriginalTransactionId = originalTransactionId;
            txn.Notes = $"Edited from Run={original.RunMinutes}/Setup={original.SetupMinutes}. Reason: {reason}";
            txn.StatusAfter = op.Status;

            _db.Set<ProductionOperationTransaction>().Add(txn);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Time edit on txn {Orig}: run {OldR}→{NewR}, setup {OldS}→{NewS}",
                originalTransactionId, original.RunMinutes, newRunMinutes, original.SetupMinutes, newSetupMinutes);
            return Result.Success(txn);
        }

        // =====================================================================
        // READ
        // =====================================================================

        public async Task<ProductionOperationTransaction?> GetAsync(int transactionId, CancellationToken ct = default)
            => await _db.Set<ProductionOperationTransaction>()
                .Include(t => t.ProductionOrder)
                .Include(t => t.Operation)
                .Include(t => t.OriginalTransaction)
                .FirstOrDefaultAsync(t => t.Id == transactionId, ct);

        public async Task<IReadOnlyList<ProductionOperationTransaction>> GetForOperationAsync(
            int operationId, CancellationToken ct = default)
            => await _db.Set<ProductionOperationTransaction>()
                .Where(t => t.OperationId == operationId)
                .OrderByDescending(t => t.TransactionDateUtc)
                .ToListAsync(ct);

        // =====================================================================
        // HELPERS
        // =====================================================================

        private async Task<ProductionOperation?> FindOp(int id, CancellationToken ct)
            => await _db.Set<ProductionOperation>().FindAsync(new object[] { id }, ct);

        private static Result<ProductionOperationTransaction> OpNotFound()
            => Result.Failure<ProductionOperationTransaction>("Operation not found.");

        private static Result<ProductionOperationTransaction> Fail(string msg)
            => Result.Failure<ProductionOperationTransaction>(msg);

        private static void Stamp(ProductionOperation op, string by)
        { op.ModifiedAt = DateTime.UtcNow; op.ModifiedBy = by; }

        private static string GenTxnNum(string prefix, int opId)
        {
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var rnd = Guid.NewGuid().ToString("N")[..6];
            return $"{prefix}-{ts}-{opId}-{rnd}";
        }

        private ProductionOperationTransaction CreateTxn(
            ProductionOperation op, OperationTransactionType type,
            ProductionOperationStatus before, string performedBy)
        {
            var prefix = type switch
            {
                OperationTransactionType.Start => "OPS",
                OperationTransactionType.Pause => "OPP",
                OperationTransactionType.Resume => "OPR",
                OperationTransactionType.Stop => "OPX",
                OperationTransactionType.LogTime => "OPT",
                OperationTransactionType.EditTime => "OPE",
                OperationTransactionType.AddEmployee => "OPA",
                OperationTransactionType.StartSetup => "OSS",
                OperationTransactionType.CompleteSetup => "OSC",
                OperationTransactionType.StartRun => "ORS",
                OperationTransactionType.CompleteRun => "ORC",
                OperationTransactionType.Complete => "OPC",
                OperationTransactionType.PartialComplete => "OPQ",
                OperationTransactionType.FinalComplete => "OPF",
                OperationTransactionType.ReverseCompletion => "OPV",
                OperationTransactionType.SkipOperation => "OPK",
                OperationTransactionType.AddOperation => "ADD",
                OperationTransactionType.InsertReworkOperation => "RWK",
                OperationTransactionType.ChangeResource => "CHR",
                _ => "OPZ"
            };

            return new ProductionOperationTransaction
            {
                CompanyId = op.CompanyIdSnapshot,
                TransactionNumber = GenTxnNum(prefix, op.Id),
                TransactionType = type,
                ProductionOrderId = op.ProductionOrderId,
                OperationId = op.Id,
                OperationSequence = op.SequenceNumber,
                StatusBefore = before,
                StatusAfter = op.Status,
                WorkCenterId = op.WorkCenterId,
                AssetId = op.AssetId,
                PerformedBy = performedBy,
                CreatedBy = performedBy,
            };
        }

        private async Task<Result<ProductionOperationTransaction>> Post(
            ProductionOperation op, OperationTransactionType type,
            ProductionOperationStatus before, string performedBy,
            decimal goodQty = 0, decimal scrapQty = 0, decimal reworkQty = 0, decimal rejectQty = 0,
            decimal runMinutes = 0, decimal setupMinutes = 0, decimal machineMinutes = 0, decimal laborMinutes = 0,
            decimal? laborCost = null, decimal? machineCost = null,
            int? assetId = null, int? crewSize = null,
            bool isFinal = false, bool backflush = false,
            string? lotSerials = null, string? destLoc = null,
            string? scrapReason = null, string? defect = null,
            bool inspection = false, string? notes = null,
            CancellationToken ct = default)
        {
            var txn = CreateTxn(op, type, before, performedBy);
            txn.GoodQuantity = goodQty;
            txn.ScrapQuantity = scrapQty;
            txn.ReworkQuantity = reworkQty;
            txn.RejectQuantity = rejectQty;
            txn.RunMinutes = runMinutes;
            txn.SetupMinutes = setupMinutes;
            txn.MachineMinutes = machineMinutes;
            txn.LaborMinutes = laborMinutes;
            txn.LaborCost = laborCost;
            txn.MachineCost = machineCost;
            if (crewSize.HasValue) txn.CrewSize = crewSize;
            txn.IsFinalOperation = isFinal;
            txn.BackflushMaterials = backflush;
            txn.CompletedLotSerials = lotSerials;
            txn.DestinationLocation = destLoc;
            txn.ScrapReasonCode = scrapReason;
            txn.DefectCode = defect;
            txn.InspectionRequired = inspection;
            txn.Notes = notes;

            _db.Set<ProductionOperationTransaction>().Add(txn);
            await _db.SaveChangesAsync(ct);

            _log.LogInformation("{Type} op {Seq} (PRO {PRO}): {Before}→{After} good={Good} scrap={Scrap}",
                type, op.SequenceNumber, op.ProductionOrderId, before, op.Status, goodQty, scrapQty);
            return Result.Success(txn);
        }
    }
}
