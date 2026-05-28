// Sprint 15.2 PR-7 (2026-05-28) — SubcontractFlowService impl.
//
// Walks the §5 8-step flow. Pure orchestration — no new entities, no
// migrations, no direct SQL. Calls into:
//   * ISubcontractOperationService (PR-4): create op + dual-demands
//   * ISubcontractShipmentReceiptService (PR-6): ship + receive
//   * IVendorWipService (PR-5): vendor WIP read aggregates
//   * IOperationReadinessService (B8 PR-PRO-7): next-op readiness check
//   * IPurchasingService (existing): caller drives PO creation; we just LINK
//
// Per the GO BIG rule: every step is exposed; the aggregate FlowStateSummary
// answers "where is this op in the 8 steps?" so the Cockpit panel (PR-9) and
// the BIC differentiator UX can render the supervisor view from one read.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public class SubcontractFlowService : ISubcontractFlowService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ISubcontractOperationService _subOpSvc;
    private readonly ISubcontractShipmentReceiptService _shipRcptSvc;
    private readonly IOperationReadinessService _readinessSvc;
    private readonly ILogger<SubcontractFlowService> _log;

    public SubcontractFlowService(
        AppDbContext db,
        ITenantContext tenant,
        ISubcontractOperationService subOpSvc,
        ISubcontractShipmentReceiptService shipRcptSvc,
        IOperationReadinessService readinessSvc,
        ILogger<SubcontractFlowService> log)
    {
        _db = db;
        _tenant = tenant;
        _subOpSvc = subOpSvc;
        _shipRcptSvc = shipRcptSvc;
        _readinessSvc = readinessSvc;
        _log = log;
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 1 — Evaluate routing for subcontract ops
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<EvaluateRoutingResult>> EvaluateRoutingForSubcontractAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        var pro = await _db.Set<ProductionOrder>()
            .Where(p => p.Id == productionOrderId &&
                        _tenant.VisibleCompanyIds.Contains(p.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (pro == null)
            return Result.Failure<EvaluateRoutingResult>(
                $"ProductionOrder {productionOrderId} not found or out of tenant scope.");

        var existing = await _db.Set<SubcontractOperation>()
            .Where(s => s.ProductionOrderId == productionOrderId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .OrderBy(s => s.OperationSequence)
            .ToListAsync(ct);

        var candidates = existing
            .Select(s => new SubcontractCandidate(
                ProductionOrderId: s.ProductionOrderId,
                OperationSequence: s.OperationSequence,
                OperationCode: s.OperationCode,
                OperationDescription: s.OperationDescription,
                SupplierId: s.SupplierId,
                ServiceItemId: s.ServiceItemId,
                QuantityRequired: s.QuantityToShip,
                Reason: $"Existing SubcontractOperation #{s.Id} (status {s.Status})"))
            .ToList();

        var msg = candidates.Count == 0
            ? $"PRO #{productionOrderId}: no SubcontractOperation rows yet. " +
              "Step 2 EnsureOpsAndDemandsAsync requires caller-supplied candidates " +
              "until the IsOutsideProcessing routing flag lands."
            : $"PRO #{productionOrderId}: {candidates.Count} existing subcontract op(s) discovered.";

        return Result.Success(new EvaluateRoutingResult(
            productionOrderId, candidates, existing.Count, msg));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 2 — Ensure SubcontractOperation rows + dual-demands exist
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<EnsureOpsAndDemandsResult>> EnsureOpsAndDemandsAsync(
        int productionOrderId,
        IReadOnlyList<SubcontractCandidate> candidates,
        string? createdBy,
        CancellationToken ct = default)
    {
        if (candidates == null || candidates.Count == 0)
            return Result.Failure<EnsureOpsAndDemandsResult>(
                "No candidates provided. Step 2 requires Step-1 output or caller-supplied list.");

        var pro = await _db.Set<ProductionOrder>()
            .Where(p => p.Id == productionOrderId &&
                        _tenant.VisibleCompanyIds.Contains(p.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (pro == null)
            return Result.Failure<EnsureOpsAndDemandsResult>(
                $"ProductionOrder {productionOrderId} not found or out of tenant scope.");

        int opsCreated = 0, opsExisted = 0, demandsCreated = 0, demandsExisted = 0;

        foreach (var c in candidates)
        {
            if (c.ProductionOrderId != productionOrderId)
                continue; // safety: caller can only ensure ops for the requested PRO

            // Step 2a: ensure SubcontractOperation row exists (PR-4 idempotent)
            var createOp = await _subOpSvc.CreateSubcontractOperationAsync(
                new CreateSubcontractOperationRequest(
                    ProductionOrderId: c.ProductionOrderId,
                    OperationSequence: c.OperationSequence,
                    OperationCode: c.OperationCode,
                    OperationDescription: c.OperationDescription,
                    SupplierId: c.SupplierId,
                    ServiceItemId: c.ServiceItemId,
                    QuantityToShip: c.QuantityRequired,
                    ServiceUnitCost: 0m,
                    PriorOperationSequence: c.OperationSequence - 10,
                    ReturnOperationSequence: c.OperationSequence + 10,
                    RequiredShipDate: null,
                    RequiredBackDate: null,
                    ShipWipRequired: true,
                    GenerateSubcontractPo: true,
                    WipItemId: null,
                    FixedLeadTimeDays: null,
                    VariableLeadTimeDaysPerUnit: null,
                    CreatedBy: createdBy ?? "subcontract-flow",
                    Notes: $"Auto-materialized by SubcontractFlow Step 2 — {c.Reason}"),
                ct);

            if (!createOp.IsSuccess)
                return Result.Failure<EnsureOpsAndDemandsResult>(
                    $"Step 2a CreateSubcontractOperation failed for op seq {c.OperationSequence}: {createOp.Error}");

            var opId = createOp.Value!.SubcontractOperationId;
            var wasNew = !createOp.Value.Message?.StartsWith("Already exists") ?? true;
            if (wasNew) opsCreated++; else opsExisted++;

            // Step 2b: ensure dual-demand binding exists (PR-4 idempotent)
            var dualResult = await _subOpSvc.CreateSubcontractDemandAsync(opId, createdBy, ct);
            if (!dualResult.IsSuccess)
                return Result.Failure<EnsureOpsAndDemandsResult>(
                    $"Step 2b CreateSubcontractDemand failed for op #{opId}: {dualResult.Error}");

            var dualWasNew = !dualResult.Value!.Message?.StartsWith("Already created") ?? true;
            if (dualWasNew) demandsCreated++; else demandsExisted++;
        }

        return Result.Success(new EnsureOpsAndDemandsResult(
            productionOrderId, opsCreated, opsExisted, demandsCreated, demandsExisted,
            $"Step 2 complete: ops created={opsCreated}, existed={opsExisted}; " +
            $"dual-demand bindings created={demandsCreated}, existed={demandsExisted}."));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 3 — Link service PO line to SubcontractOperation
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<LinkServicePoLineResult>> LinkServicePoLineAsync(
        int subcontractOperationId,
        int servicePurchaseOrderLineId,
        string? actor,
        CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<LinkServicePoLineResult>(
                $"SubcontractOperation {subcontractOperationId} not found or out of tenant scope.");

        var poLine = await _db.Set<PurchaseOrderLine>()
            .Include(l => l.PurchaseOrder)
            .Where(l => l.Id == servicePurchaseOrderLineId)
            .FirstOrDefaultAsync(ct);
        if (poLine == null)
            return Result.Failure<LinkServicePoLineResult>(
                $"PurchaseOrderLine {servicePurchaseOrderLineId} not found.");
        if (poLine.PurchaseOrder?.CompanyId is int poCompanyId &&
            !_tenant.VisibleCompanyIds.Contains(poCompanyId))
            return Result.Failure<LinkServicePoLineResult>(
                "PurchaseOrderLine parent PO is out of tenant scope.");

        op.ServicePurchaseOrderLineId = servicePurchaseOrderLineId;
        op.PoCreationStatus = SubcontractPoCreationStatus.Created;
        op.Notes = string.IsNullOrEmpty(op.Notes)
            ? $"[step3-link {DateTime.UtcNow:O}] PO line {servicePurchaseOrderLineId} linked by {actor ?? "system"}"
            : $"{op.Notes}\n[step3-link {DateTime.UtcNow:O}] PO line {servicePurchaseOrderLineId} linked by {actor ?? "system"}";

        await _db.SaveChangesAsync(ct);

        // Codex pre-PR P2 #9: route lifecycle promotion through the audited
        // TransitionStatusAsync rather than direct write. Keeps audit-trail
        // notes + log line consistent with all other lifecycle changes.
        if (op.Status == SubcontractOperationStatus.NotReady ||
            op.Status == SubcontractOperationStatus.ReadyToBuy)
        {
            await _subOpSvc.TransitionStatusAsync(
                op.Id, SubcontractOperationStatus.PoCreated,
                $"Step-3: service PO line {servicePurchaseOrderLineId} linked",
                actor ?? "subcontract-flow", ct);
            // Re-read so we return the post-transition status.
            op = await _db.Set<SubcontractOperation>()
                .Where(s => s.Id == op.Id && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
                .FirstOrDefaultAsync(ct) ?? op;
        }

        _log.LogInformation(
            "SubcontractFlow Step 3: linked PO line {LineId} to subcontract op {OpId}",
            servicePurchaseOrderLineId, op.Id);

        return Result.Success(new LinkServicePoLineResult(
            op.Id, servicePurchaseOrderLineId, op.PoCreationStatus,
            $"PO line #{servicePurchaseOrderLineId} linked. PoCreationStatus={op.PoCreationStatus}, Status={op.Status}."));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 4 — Readiness gate (prior op complete + PO created)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<ReadinessGateResult>> EvaluateReadinessGateAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<ReadinessGateResult>(
                $"SubcontractOperation {subcontractOperationId} not found or out of tenant scope.");

        bool priorOpComplete = true;
        decimal priorCompletedQty = 0m;
        decimal priorRequiredQty = 0m;
        if (op.PriorOperationSequence.HasValue)
        {
            var prior = await _db.Set<ProductionOperation>()
                .Where(po => po.ProductionOrderId == op.ProductionOrderId &&
                              po.SequenceNumber == op.PriorOperationSequence.Value &&
                              _tenant.VisibleCompanyIds.Contains(po.CompanyIdSnapshot))
                .FirstOrDefaultAsync(ct);
            if (prior != null)
            {
                priorCompletedQty = prior.CompletedQty;
                priorRequiredQty = prior.PlannedQty;
                priorOpComplete = prior.CompletedQty >= op.QuantityToShip;
            }
            else
            {
                // No prior ProductionOperation row → treat as complete (the
                // routing may not have a strict predecessor in our model yet).
                priorOpComplete = true;
            }
        }

        bool poCreated = op.ServicePurchaseOrderLineId.HasValue ||
                         op.PoCreationStatus == SubcontractPoCreationStatus.Created ||
                         op.PoCreationStatus == SubcontractPoCreationStatus.Approved ||
                         op.PoCreationStatus == SubcontractPoCreationStatus.SentToSupplier ||
                         op.PoCreationStatus == SubcontractPoCreationStatus.SupplierAcknowledged;

        bool isReady = priorOpComplete && poCreated;

        string? blocking = null;
        if (!priorOpComplete)
            blocking = $"Prior op {op.PriorOperationSequence}: completed {priorCompletedQty:N4} of {op.QuantityToShip:N4} required.";
        else if (!poCreated)
            blocking = $"Service PO line not yet linked. PoCreationStatus={op.PoCreationStatus}.";

        // If ready and op is still NotReady/PoCreated, auto-advance to ReadyToShip.
        if (isReady &&
            (op.Status == SubcontractOperationStatus.NotReady ||
             op.Status == SubcontractOperationStatus.ReadyToBuy ||
             op.Status == SubcontractOperationStatus.PoCreated))
        {
            await _subOpSvc.TransitionStatusAsync(
                op.Id, SubcontractOperationStatus.ReadyToShip,
                "Step-4 readiness gate cleared", "subcontract-flow", ct);
        }

        return Result.Success(new ReadinessGateResult(
            op.Id, isReady, priorOpComplete, poCreated,
            priorCompletedQty, priorRequiredQty, blocking));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 5 — Ship WIP to vendor (create shipment + line + mark in transit)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<Step5ShipResult>> ExecuteShipmentAsync(
        Step5ShipRequest r, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == r.SubcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<Step5ShipResult>(
                $"SubcontractOperation {r.SubcontractOperationId} not found or out of tenant scope.");

        if (!op.SupplierId.HasValue)
            return Result.Failure<Step5ShipResult>(
                $"Op #{op.Id} has no supplier — cannot ship.");

        // 5a — Create shipment header
        var demandBinding = await _db.Set<SubcontractDemand>()
            .Where(d => d.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(d.CompanyId))
            .FirstOrDefaultAsync(ct);

        var createShip = await _shipRcptSvc.CreateShipmentAsync(
            new CreateSubcontractShipmentRequest(
                SubcontractOperationId: op.Id,
                SubcontractDemandId: demandBinding?.Id,
                SupplierId: op.SupplierId.Value,
                VendorLocationId: r.VendorLocationId,
                ShipFromLocationId: r.ShipFromLocationId,
                VendorWipLocationCode: null,
                Carrier: r.Carrier,
                ShippingMethod: r.ShippingMethod,
                TrackingNumber: r.TrackingNumber,
                FreightCost: r.FreightCost,
                FreightCurrency: r.FreightCurrency,
                RequiredShipDate: r.RequiredShipDate,
                ExpectedDeliveryDate: r.ExpectedDeliveryDate,
                CertRequired: r.CertRequired,
                PackingInstructions: r.PackingInstructions,
                CreatedBy: r.CreatedBy,
                Notes: r.Notes),
            ct);
        if (!createShip.IsSuccess)
            return Result.Failure<Step5ShipResult>(
                $"Step 5a CreateShipment failed: {createShip.Error}");
        var shipmentId = createShip.Value!.SubcontractShipmentId;

        // 5b — Add the (single) line, OR reuse an existing line on retry.
        // Codex P1: CreateShipment is idempotent per (op, supplier, required-date)
        // but AddShipmentLine appends unconditionally. A retry after 5a+5b
        // succeeded but 5c failed would append a 2nd line → MarkInTransit
        // would ship every line → double-post vendor WIP. Defense: re-load
        // the shipment with its Lines collection; reuse the first line on
        // retry, only add if there are zero lines.
        var shipmentWithLines = await _db.Set<SubcontractShipment>()
            .Include(s => s.Lines)
            .Where(s => s.Id == shipmentId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (shipmentWithLines == null)
            return Result.Failure<Step5ShipResult>(
                $"Step 5b internal: just-created shipment #{shipmentId} not loadable.");

        int shipmentLineId;
        if (shipmentWithLines.Lines.Any())
        {
            shipmentLineId = shipmentWithLines.Lines
                .OrderBy(l => l.LineNumber).First().Id;
            _log.LogInformation(
                "SubcontractFlow Step 5 retry-safe path: reusing existing line #{LineId} on shipment {Num} (had {Count} line(s)).",
                shipmentLineId, createShip.Value.ShipmentNumber, shipmentWithLines.Lines.Count);
        }
        else
        {
            var addLine = await _shipRcptSvc.AddShipmentLineAsync(
                new AddShipmentLineRequest(
                    SubcontractShipmentId: shipmentId,
                    ItemId: r.WipItemId,
                    PartNumber: r.PartNumber,
                    Description: r.Description,
                    DrawingRevision: r.DrawingRevision,
                    LotNumber: r.LotNumber,
                    SerialNumber: r.SerialNumber,
                    QuantityShipped: r.QuantityShipped,
                    Uom: r.Uom ?? "EA",
                    UnitCostSnapshot: r.UnitCostSnapshot,
                    Notes: r.Notes),
                ct);
            if (!addLine.IsSuccess)
                return Result.Failure<Step5ShipResult>(
                    $"Step 5b AddShipmentLine failed: {addLine.Error}");
            shipmentLineId = addLine.Value!.SubcontractShipmentLineId;
        }

        // 5c — Mark InTransit (creates VendorWipTransaction per line, rolls op qty)
        var inTransit = await _shipRcptSvc.MarkShipmentInTransitAsync(
            shipmentId, DateTime.UtcNow, r.CreatedBy ?? "subcontract-flow", ct);
        if (!inTransit.IsSuccess)
            return Result.Failure<Step5ShipResult>(
                $"Step 5c MarkShipmentInTransit failed: {inTransit.Error}");

        // Re-fetch op for current status snapshot (tenant-scoped).
        var opAfter = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == op.Id && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

        return Result.Success(new Step5ShipResult(
            shipmentId,
            createShip.Value.ShipmentNumber,
            shipmentLineId,
            inTransit.Value!.Status,
            opAfter?.Status ?? op.Status,
            $"Step 5 complete: shipment {createShip.Value.ShipmentNumber} in transit with {r.QuantityShipped:N4} {r.Uom}."));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 6 — Vendor processing status (aggregate read)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<VendorProcessingStatus>> GetVendorProcessingStatusAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<VendorProcessingStatus>(
                $"SubcontractOperation {subcontractOperationId} not found or out of tenant scope.");

        // Codex pre-PR P2 #3: dependent reads must re-assert tenant scope
        // even though op is already checked (defense-in-depth per Lock 16).
        var openShipments = await _db.Set<SubcontractShipment>()
            .CountAsync(s =>
                s.SubcontractOperationId == op.Id &&
                _tenant.VisibleCompanyIds.Contains(s.CompanyId) &&
                s.Status != SubcontractShipmentLifecycle.Cancelled &&
                s.Status != SubcontractShipmentLifecycle.Reconciled, ct);
        var openReceipts = await _db.Set<SubcontractReceipt>()
            .CountAsync(r =>
                r.SubcontractOperationId == op.Id &&
                _tenant.VisibleCompanyIds.Contains(r.CompanyId) &&
                r.Status != SubcontractReceiptLifecycle.Reversed &&
                r.Status != SubcontractReceiptLifecycle.Closed, ct);
        var balanceCount = await _db.Set<VendorWipBalance>()
            .CountAsync(b => b.SubcontractOperationId == op.Id &&
                              _tenant.VisibleCompanyIds.Contains(b.CompanyId), ct);
        var qtyAtVendor = await _db.Set<VendorWipBalance>()
            .Where(b => b.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(b.CompanyId))
            .SumAsync(b => (decimal?)b.QuantityAtVendor, ct) ?? 0m;

        int? daysLate = null;
        if (op.RequiredBackDate.HasValue &&
            op.QuantityReceivedBack < op.QuantityToShip)
        {
            var diff = (DateTime.UtcNow - op.RequiredBackDate.Value).TotalDays;
            if (diff > 0) daysLate = (int)Math.Ceiling(diff);
        }

        return Result.Success(new VendorProcessingStatus(
            op.Id, op.ProductionOrderId, op.OperationSequence,
            op.Status, op.ShipmentStatus, op.ReceiptStatus,
            op.QuantityShipped, qtyAtVendor, op.QuantityReceivedBack,
            op.QuantityAccepted, op.QuantityRejected, op.QuantityScrappedAtVendor,
            openShipments, openReceipts, balanceCount,
            op.RequiredBackDate, daysLate));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 7 — Receive subcontract PO + WIP (create receipt + line + post)
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<Step7ReceiveResult>> ExecuteReceiptAsync(
        Step7ReceiveRequest r, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == r.SubcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<Step7ReceiveResult>(
                $"SubcontractOperation {r.SubcontractOperationId} not found or out of tenant scope.");
        if (!op.SupplierId.HasValue)
            return Result.Failure<Step7ReceiveResult>(
                $"Op #{op.Id} has no supplier — cannot receive.");

        // 7a — Create receipt header
        var createRcpt = await _shipRcptSvc.CreateReceiptAsync(
            new CreateSubcontractReceiptRequest(
                SubcontractOperationId: op.Id,
                SubcontractShipmentId: r.SubcontractShipmentId,
                SupplierId: op.SupplierId.Value,
                VendorLocationId: null,
                ReceivingLocationId: r.ReceivingLocationId,
                VendorPackingSlip: r.VendorPackingSlip,
                Carrier: r.Carrier,
                TrackingNumber: r.TrackingNumber,
                ReceiptDate: r.ReceiptDate,
                CertReceived: r.CertReceived,
                CertReference: r.CertReference,
                InspectionRequired: r.InspectionRequired,
                CreatedBy: r.CreatedBy,
                Notes: r.Notes),
            ct);
        if (!createRcpt.IsSuccess)
            return Result.Failure<Step7ReceiveResult>(
                $"Step 7a CreateReceipt failed: {createRcpt.Error}");
        var receiptId = createRcpt.Value!.SubcontractReceiptId;

        // 7b — Add the (single) line, OR reuse an existing line on retry.
        // Codex P1 (twin of Step 5): CreateReceipt is idempotent per (op,
        // packing slip) but AddReceiptLine appends unconditionally. A retry
        // after 7a+7b succeeded but 7c failed would append a 2nd line →
        // PostReceipt would post every line → double-receive vendor WIP +
        // overstate op accepted/rejected qtys.
        var receiptWithLines = await _db.Set<SubcontractReceipt>()
            .Include(rc => rc.Lines)
            .Where(rc => rc.Id == receiptId &&
                         _tenant.VisibleCompanyIds.Contains(rc.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (receiptWithLines == null)
            return Result.Failure<Step7ReceiveResult>(
                $"Step 7b internal: just-created receipt #{receiptId} not loadable.");

        int receiptLineId;
        if (receiptWithLines.Lines.Any())
        {
            receiptLineId = receiptWithLines.Lines
                .OrderBy(l => l.LineNumber).First().Id;
            _log.LogInformation(
                "SubcontractFlow Step 7 retry-safe path: reusing existing line #{LineId} on receipt {Num} (had {Count} line(s)).",
                receiptLineId, createRcpt.Value.ReceiptNumber, receiptWithLines.Lines.Count);
        }
        else
        {
            var addLine = await _shipRcptSvc.AddReceiptLineAsync(
                new AddReceiptLineRequest(
                    SubcontractReceiptId: receiptId,
                    SubcontractShipmentLineId: null,
                    ItemId: r.WipItemId,
                    PartNumber: r.PartNumber,
                    Description: r.Description,
                    DrawingRevision: r.DrawingRevision,
                    LotNumber: r.LotNumber,
                    SerialNumber: r.SerialNumber,
                    QuantityReceived: r.QuantityReceived,
                    QuantityAccepted: r.QuantityAccepted,
                    QuantityRejected: r.QuantityRejected,
                    QuantityScrappedAtVendor: r.QuantityScrappedAtVendor,
                    QuantityShort: r.QuantityShort,
                    Uom: r.Uom ?? "EA",
                    Scenario: r.Scenario,
                    Disposition: r.Disposition,
                    RejectReason: r.RejectReason,
                    NcrReference: r.NcrReference,
                    Notes: r.Notes),
                ct);
            if (!addLine.IsSuccess)
                return Result.Failure<Step7ReceiveResult>(
                    $"Step 7b AddReceiptLine failed: {addLine.Error}");
            receiptLineId = addLine.Value!.SubcontractReceiptLineId;
        }

        // 7c — Post atomically
        var post = await _shipRcptSvc.PostReceiptAsync(receiptId,
            r.CreatedBy ?? "subcontract-flow", ct);
        if (!post.IsSuccess)
            return Result.Failure<Step7ReceiveResult>(
                $"Step 7c PostReceipt failed: {post.Error}");

        var opAfter = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == op.Id && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

        return Result.Success(new Step7ReceiveResult(
            receiptId,
            createRcpt.Value.ReceiptNumber,
            receiptLineId,
            post.Value!.Status,
            opAfter?.Status ?? op.Status,
            post.Value.RequiresApproval,
            opAfter?.QuantityAccepted ?? op.QuantityAccepted,
            $"Step 7 complete: receipt {createRcpt.Value.ReceiptNumber} posted (scenario {r.Scenario})." +
            (post.Value.RequiresApproval ? " — PendingApproval gate (Over/WrongJobOrPo)." : "")));
    }

    // ════════════════════════════════════════════════════════════════════════
    // STEP 8 — Advance to next op + run readiness check
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<AdvanceToNextOpResult>> AdvanceToNextOpAsync(
        int subcontractOperationId, string? actor, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<AdvanceToNextOpResult>(
                $"SubcontractOperation {subcontractOperationId} not found or out of tenant scope.");

        // Mark Complete via PR-4 service (idempotent if already Complete/Closed).
        var markComplete = await _subOpSvc.MarkCompleteAsync(op.Id, actor, ct);
        if (!markComplete.IsSuccess)
            return Result.Failure<AdvanceToNextOpResult>(
                $"Step 8a MarkComplete failed: {markComplete.Error}");

        // Codex pre-PR P2 #4: prefer SubcontractOperation.ReturnOperationSequence
        // (the spec §6B canonical "return-to-op") over a generic next-sequence
        // scan. Falls back to next-sequence if ReturnOperationSequence is unset.
        ProductionOperation? nextOp = null;
        if (op.ReturnOperationSequence.HasValue)
        {
            nextOp = await _db.Set<ProductionOperation>()
                .Where(po => po.ProductionOrderId == op.ProductionOrderId &&
                              po.SequenceNumber == op.ReturnOperationSequence.Value &&
                              _tenant.VisibleCompanyIds.Contains(po.CompanyIdSnapshot))
                .FirstOrDefaultAsync(ct);
        }
        if (nextOp == null)
        {
            nextOp = await _db.Set<ProductionOperation>()
                .Where(po => po.ProductionOrderId == op.ProductionOrderId &&
                              po.SequenceNumber > op.OperationSequence &&
                              _tenant.VisibleCompanyIds.Contains(po.CompanyIdSnapshot))
                .OrderBy(po => po.SequenceNumber)
                .FirstOrDefaultAsync(ct);
        }

        bool readyForNext = false;
        string? readinessSummary = null;
        int? nextSeq = null;

        if (nextOp != null)
        {
            nextSeq = nextOp.SequenceNumber;
            var readinessCheck = await _readinessSvc.CheckOperationReadinessAsync(nextOp.Id, ct);
            if (readinessCheck.IsSuccess)
            {
                readyForNext = readinessCheck.Value!.OverallStatus == ReadinessStatus.Pass;
                var passCount = readinessCheck.Value.Checks.Count(c => c.Status == ReadinessStatus.Pass);
                var failCount = readinessCheck.Value.Checks.Count(c => c.Status == ReadinessStatus.Fail);
                var warnCount = readinessCheck.Value.Checks.Count(c => c.Status == ReadinessStatus.Warning);
                readinessSummary = $"Next op {nextOp.SequenceNumber} readiness: {readinessCheck.Value.OverallStatus} " +
                                   $"(pass={passCount}, warn={warnCount}, fail={failCount})";
            }
            else
            {
                readinessSummary = $"Readiness check error: {readinessCheck.Error}";
            }
        }
        else
        {
            readinessSummary = "No subsequent routing operation — subcontract op closes flow.";
        }

        return Result.Success(new AdvanceToNextOpResult(
            op.Id, markComplete.Value!.Status, nextSeq, readyForNext, readinessSummary,
            $"Step 8 complete: op #{op.Id} marked Complete. {readinessSummary}"));
    }

    // ════════════════════════════════════════════════════════════════════════
    // AGGREGATE — where is this op in the 8-step flow?
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<FlowStateSummary>> GetFlowStateAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<FlowStateSummary>(
                $"SubcontractOperation {subcontractOperationId} not found or out of tenant scope.");

        // Codex pre-PR P2 #5: tenant-scope every dependent read.
        var demand = await _db.Set<SubcontractDemand>()
            .Where(d => d.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(d.CompanyId))
            .FirstOrDefaultAsync(ct);
        var latestShip = await _db.Set<SubcontractShipment>()
            .Where(s => s.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var latestRcpt = await _db.Set<SubcontractReceipt>()
            .Where(r => r.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(r.CompanyId))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        // Map current state to one of the 8 steps (the "where am I" rendering).
        // Determined by the highest-completed marker, not a strict linear walk.
        int currentStep;
        string currentStepName;
        var completed = new List<string>();
        string? hint;

        if (op.Status == SubcontractOperationStatus.Complete ||
            op.Status == SubcontractOperationStatus.Closed)
        {
            currentStep = 8;
            currentStepName = "Step 8 — Complete (next op signaled)";
            completed.AddRange(new[] { "1. Routing scan", "2. Ops + demands", "3. PO link", "4. Readiness gate", "5. Shipped", "6. Vendor processed", "7. Received" });
            hint = "Op finished — next routing op should now be checked for readiness.";
        }
        else if (latestRcpt != null && latestRcpt.Status == SubcontractReceiptLifecycle.Posted)
        {
            currentStep = 7;
            currentStepName = "Step 7 — Received";
            completed.AddRange(new[] { "1. Routing scan", "2. Ops + demands", "3. PO link", "4. Readiness gate", "5. Shipped", "6. Vendor processed" });
            hint = "Receipt posted — run Step 8 to mark op Complete and signal next op.";
        }
        else if (op.QuantityShipped > 0m && op.QuantityReceivedBack < op.QuantityToShip)
        {
            currentStep = 6;
            currentStepName = "Step 6 — At vendor";
            completed.AddRange(new[] { "1. Routing scan", "2. Ops + demands", "3. PO link", "4. Readiness gate", "5. Shipped" });
            hint = "Shipped; waiting on vendor return. Run Step 7 when material arrives.";
        }
        else if (op.Status == SubcontractOperationStatus.ReadyToShip)
        {
            currentStep = 5;
            currentStepName = "Step 5 — Ready to ship";
            completed.AddRange(new[] { "1. Routing scan", "2. Ops + demands", "3. PO link", "4. Readiness gate" });
            hint = "Readiness gate cleared. Run Step 5 to ship WIP to vendor.";
        }
        else if (op.PoCreationStatus == SubcontractPoCreationStatus.Created ||
                 op.PoCreationStatus == SubcontractPoCreationStatus.Approved ||
                 op.PoCreationStatus == SubcontractPoCreationStatus.SentToSupplier ||
                 op.PoCreationStatus == SubcontractPoCreationStatus.SupplierAcknowledged ||
                 op.Status == SubcontractOperationStatus.PoCreated)
        {
            currentStep = 4;
            currentStepName = "Step 4 — Readiness gate";
            completed.AddRange(new[] { "1. Routing scan", "2. Ops + demands", "3. PO link" });
            hint = "PO linked. Run Step 4 to evaluate prior-op + PO gate before shipping.";
        }
        else if (demand != null)
        {
            currentStep = 3;
            currentStepName = "Step 3 — Link service PO line";
            completed.AddRange(new[] { "1. Routing scan", "2. Ops + demands" });
            hint = "Dual-demand created. Run Step 3 once a service PO line is available.";
        }
        else
        {
            currentStep = 2;
            currentStepName = "Step 2 — Ops + demands";
            completed.Add("1. Routing scan");
            hint = "Op materialized but no dual-demand yet. Run Step 2 EnsureOpsAndDemands.";
        }

        return Result.Success(new FlowStateSummary(
            op.Id, op.ProductionOrderId, op.OperationSequence,
            currentStep, currentStepName,
            op.Status, demand?.Status,
            latestShip?.Status, latestRcpt?.Status,
            op.QuantityShipped, op.QuantityReceivedBack, op.QuantityAccepted,
            completed.ToArray(), hint));
    }
}
