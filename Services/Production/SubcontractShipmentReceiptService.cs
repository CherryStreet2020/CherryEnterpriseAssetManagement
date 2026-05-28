// Sprint 15.2 PR-6 (2026-05-28) — SubcontractShipmentReceiptService impl.
//
// Implements the §5 step 5 / step 7 surface: physical WIP shipment to vendor
// + physical receive-back from vendor. Cost wiring lands in PR-8 (we tag the
// service hooks here but don't post yet). Cockpit panel + 15 non-negotiable
// validations land in PR-9.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public class SubcontractShipmentReceiptService : ISubcontractShipmentReceiptService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IVendorWipService _vendorWip;
    private readonly ISubcontractOperationService _subOpSvc;
    private readonly ILogger<SubcontractShipmentReceiptService> _log;

    public SubcontractShipmentReceiptService(
        AppDbContext db,
        ITenantContext tenant,
        IVendorWipService vendorWip,
        ISubcontractOperationService subOpSvc,
        ILogger<SubcontractShipmentReceiptService> log)
    {
        _db = db;
        _tenant = tenant;
        _vendorWip = vendorWip;
        _subOpSvc = subOpSvc;
        _log = log;
    }

    // ════════════════════════════════════════════════════════════════════════
    // SHIPMENT WRITES
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<CreateSubcontractShipmentResult>> CreateShipmentAsync(
        CreateSubcontractShipmentRequest r, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == r.SubcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<CreateSubcontractShipmentResult>(
                $"SubcontractOperation {r.SubcontractOperationId} not found or out of tenant scope.");

        // Idempotency: any Draft shipment for same (op, supplier, required-date) → reuse
        var existing = await _db.Set<SubcontractShipment>()
            .Where(s => s.SubcontractOperationId == r.SubcontractOperationId &&
                        s.SupplierId == r.SupplierId &&
                        s.Status == SubcontractShipmentLifecycle.Draft &&
                        s.RequiredShipDate == r.RequiredShipDate)
            .FirstOrDefaultAsync(ct);
        if (existing != null)
        {
            return Result.Success(new CreateSubcontractShipmentResult(
                existing.Id, existing.ShipmentNumber,
                "Existing Draft shipment returned (idempotent)."));
        }

        // Two-phase numbering eliminates the CountAsync race: insert with a
        // placeholder, let EF assign Id, then patch ShipmentNumber using Id.
        // ShipmentNumber unique index is per (CompanyId, ShipmentNumber);
        // the Id is the global atomic counter, so SCSHP-yyyy-{Id:D6} is
        // guaranteed unique. Two saves but atomic correctness > one save.
        var placeholderNumber = $"SCSHP-PEND-{Guid.NewGuid():N}";

        var shipment = new SubcontractShipment
        {
            CompanyId = op.CompanyId,
            SiteId = op.SiteId,
            ShipmentNumber = placeholderNumber,
            SubcontractOperationId = op.Id,
            ProductionOrderId = op.ProductionOrderId,
            OperationSequence = op.OperationSequence,
            SubcontractDemandId = r.SubcontractDemandId,
            ServicePurchaseOrderLineId = op.ServicePurchaseOrderLineId,
            SupplierId = r.SupplierId,
            VendorLocationId = r.VendorLocationId,
            ShipFromLocationId = r.ShipFromLocationId,
            VendorWipLocationCode = r.VendorWipLocationCode ?? op.VendorWipLocation,
            Carrier = r.Carrier,
            ShippingMethod = r.ShippingMethod ?? op.ShippingMethod,
            TrackingNumber = r.TrackingNumber,
            FreightCost = r.FreightCost,
            FreightCurrency = r.FreightCurrency,
            RequiredShipDate = r.RequiredShipDate ?? op.RequiredShipDate,
            ExpectedDeliveryDate = r.ExpectedDeliveryDate,
            CertRequired = r.CertRequired || op.CertRequired,
            PackingInstructions = r.PackingInstructions ?? op.PackagingInstructions,
            Status = SubcontractShipmentLifecycle.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = r.CreatedBy ?? "system",
            Notes = r.Notes,
        };
        _db.Add(shipment);
        await _db.SaveChangesAsync(ct);

        // Patch the human-readable number now that Id is assigned.
        shipment.ShipmentNumber = $"SCSHP-{DateTime.UtcNow:yyyy}-{shipment.Id:D6}";
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "SubcontractShipment created #{Id} {Num} for op {OpId} PRO {PRO}",
            shipment.Id, shipment.ShipmentNumber, op.Id, op.ProductionOrderId);

        return Result.Success(new CreateSubcontractShipmentResult(
            shipment.Id, shipment.ShipmentNumber,
            $"Draft shipment {shipment.ShipmentNumber} created for op #{op.Id}."));
    }

    public async Task<Result<AddShipmentLineResult>> AddShipmentLineAsync(
        AddShipmentLineRequest r, CancellationToken ct = default)
    {
        if (r.QuantityShipped <= 0m)
            return Result.Failure<AddShipmentLineResult>("QuantityShipped must be > 0.");

        var shipment = await _db.Set<SubcontractShipment>()
            .Include(s => s.Lines)
            .Where(s => s.Id == r.SubcontractShipmentId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (shipment == null)
            return Result.Failure<AddShipmentLineResult>(
                $"Shipment {r.SubcontractShipmentId} not found or out of tenant scope.");

        if (shipment.Status != SubcontractShipmentLifecycle.Draft &&
            shipment.Status != SubcontractShipmentLifecycle.Picked)
            return Result.Failure<AddShipmentLineResult>(
                $"Shipment {shipment.ShipmentNumber} is {shipment.Status} — lines locked.");

        var nextLineNo = shipment.Lines.Count == 0 ? 1 : shipment.Lines.Max(l => l.LineNumber) + 1;

        var line = new SubcontractShipmentLine
        {
            CompanyId = shipment.CompanyId,
            SiteId = shipment.SiteId,
            SubcontractShipmentId = shipment.Id,
            LineNumber = nextLineNo,
            ItemId = r.ItemId,
            PartNumber = r.PartNumber,
            Description = r.Description,
            DrawingRevision = r.DrawingRevision,
            LotNumber = r.LotNumber,
            SerialNumber = r.SerialNumber,
            QuantityShipped = r.QuantityShipped,
            Uom = r.Uom ?? "EA",
            UnitCostSnapshot = r.UnitCostSnapshot,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = shipment.CreatedBy,
            Notes = r.Notes,
        };
        _db.Add(line);
        await _db.SaveChangesAsync(ct);

        return Result.Success(new AddShipmentLineResult(
            line.Id, line.LineNumber, $"Line {line.LineNumber} added."));
    }

    public async Task<Result<ShipmentStatusSummary>> MarkShipmentPickedAsync(
        int subcontractShipmentId, string? actor, CancellationToken ct = default)
    {
        var shipment = await LoadShipmentAsync(subcontractShipmentId, ct);
        if (shipment == null)
            return Result.Failure<ShipmentStatusSummary>(
                $"Shipment {subcontractShipmentId} not found or out of tenant scope.");

        if (shipment.Status != SubcontractShipmentLifecycle.Draft)
            return Result.Failure<ShipmentStatusSummary>(
                $"Cannot Pick — shipment is {shipment.Status}.");

        if (!shipment.Lines.Any())
            return Result.Failure<ShipmentStatusSummary>("Shipment has no lines — cannot Pick.");

        shipment.Status = SubcontractShipmentLifecycle.Picked;
        AppendNote(shipment, $"[pick {DateTime.UtcNow:O}] by {actor ?? "system"}");
        await _db.SaveChangesAsync(ct);
        return Result.Success(BuildShipmentSummary(shipment));
    }

    public async Task<Result<ShipmentStatusSummary>> MarkShipmentInTransitAsync(
        int subcontractShipmentId, DateTime? actualShipDate, string? actor,
        CancellationToken ct = default)
    {
        var shipment = await LoadShipmentAsync(subcontractShipmentId, ct);
        if (shipment == null)
            return Result.Failure<ShipmentStatusSummary>(
                $"Shipment {subcontractShipmentId} not found or out of tenant scope.");

        if (shipment.Status != SubcontractShipmentLifecycle.Draft &&
            shipment.Status != SubcontractShipmentLifecycle.Picked &&
            shipment.Status != SubcontractShipmentLifecycle.Staged)
            return Result.Failure<ShipmentStatusSummary>(
                $"Cannot ship — shipment is {shipment.Status}.");

        if (!shipment.Lines.Any())
            return Result.Failure<ShipmentStatusSummary>("Shipment has no lines — cannot ship.");

        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == shipment.SubcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<ShipmentStatusSummary>(
                $"Underlying SubcontractOperation {shipment.SubcontractOperationId} not found or out of tenant scope.");

        // Wrap the multi-line vendor-WIP ledger calls + shipment status update
        // in a single DB transaction so a mid-loop failure cannot leave
        // committed VendorWipTransaction rows pointing at a Draft shipment.
        // (Codex P1 — mirrors the PostReceipt transaction wrap.)
        decimal totalQty = 0m;
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var line in shipment.Lines)
            {
                var ship = await _vendorWip.ShipToVendorAsync(new ShipToVendorRequest(
                    ProductionOrderId: shipment.ProductionOrderId,
                    OperationSequence: shipment.OperationSequence,
                    SupplierId: shipment.SupplierId,
                    VendorLocationId: shipment.VendorLocationId,
                    ItemId: line.ItemId,
                    PartNumber: line.PartNumber,
                    Revision: line.DrawingRevision,
                    LotNumber: line.LotNumber,
                    SerialNumber: line.SerialNumber,
                    Quantity: line.QuantityShipped,
                    UnitValue: line.UnitCostSnapshot ?? 0m,
                    Uom: line.Uom,
                    ShipmentDocument: shipment.ShipmentNumber,
                    FromLocationDescription: null, // Location resolution lands in PR-7 orchestrator
                    ToLocationDescription: shipment.VendorWipLocationCode,
                    SubcontractOperationId: op.Id,
                    RequiredReturnDate: op.RequiredBackDate,
                    Notes: $"Shipment {shipment.ShipmentNumber} line {line.LineNumber}",
                    CreatedBy: actor ?? shipment.CreatedBy
                ), ct);

                if (!ship.IsSuccess)
                    throw new InvalidOperationException(
                        $"Vendor WIP ship-to failed on line {line.LineNumber}: {ship.Error}");

                line.VendorWipTransactionId = ship.Value!.TransactionId;
                totalQty += line.QuantityShipped;
            }

            shipment.Status = SubcontractShipmentLifecycle.InTransit;
            shipment.ActualShipDate = actualShipDate ?? DateTime.UtcNow;
            AppendNote(shipment, $"[ship {DateTime.UtcNow:O}] by {actor ?? "system"} — {totalQty:N4} {shipment.Lines.First().Uom}");

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _log.LogError(ex,
                "MarkShipmentInTransit failed for {Num} — transaction rolled back.",
                shipment.ShipmentNumber);
            return Result.Failure<ShipmentStatusSummary>(ex.Message);
        }

        // Roll total qty into the subcontract op so its own QuantityShipped + status advance.
        // (Op-level rollup is non-atomic vs the WIP ledger by design: the op's
        // own RowVersion guards it; a rollup failure after ledger commit logs
        // a warning rather than corrupting the just-committed ledger entries.)
        var rollup = await _subOpSvc.RecordShipmentAsync(
            op.Id, totalQty, $"Auto-roll from shipment {shipment.ShipmentNumber}", ct);
        if (!rollup.IsSuccess)
        {
            _log.LogWarning("Op rollup failed for shipment {Num}: {Err}",
                shipment.ShipmentNumber, rollup.Error);
        }

        _log.LogInformation(
            "SubcontractShipment {Num}: InTransit ({Qty} units to vendor {SupId}, op {OpId})",
            shipment.ShipmentNumber, totalQty, shipment.SupplierId, op.Id);

        return Result.Success(BuildShipmentSummary(shipment));
    }

    public async Task<Result<ShipmentStatusSummary>> MarkShipmentDeliveredAsync(
        int subcontractShipmentId, DateTime? deliveredUtc, string? actor,
        CancellationToken ct = default)
    {
        var shipment = await LoadShipmentAsync(subcontractShipmentId, ct);
        if (shipment == null)
            return Result.Failure<ShipmentStatusSummary>(
                $"Shipment {subcontractShipmentId} not found or out of tenant scope.");

        if (shipment.Status != SubcontractShipmentLifecycle.InTransit)
            return Result.Failure<ShipmentStatusSummary>(
                $"Cannot mark delivered — shipment is {shipment.Status}.");

        shipment.Status = SubcontractShipmentLifecycle.DeliveredToVendor;
        shipment.ActualDeliveryDate = deliveredUtc ?? DateTime.UtcNow;
        AppendNote(shipment, $"[delivered {DateTime.UtcNow:O}] by {actor ?? "system"}");
        await _db.SaveChangesAsync(ct);
        return Result.Success(BuildShipmentSummary(shipment));
    }

    public async Task<Result<ShipmentStatusSummary>> CancelShipmentAsync(
        int subcontractShipmentId, string? reason, string? actor,
        CancellationToken ct = default)
    {
        var shipment = await LoadShipmentAsync(subcontractShipmentId, ct);
        if (shipment == null)
            return Result.Failure<ShipmentStatusSummary>(
                $"Shipment {subcontractShipmentId} not found or out of tenant scope.");

        if (shipment.Status == SubcontractShipmentLifecycle.DeliveredToVendor ||
            shipment.Status == SubcontractShipmentLifecycle.Reconciled)
            return Result.Failure<ShipmentStatusSummary>(
                $"Cannot cancel — shipment is already {shipment.Status}. Use a return-receipt instead.");

        // NOTE: full vendor-WIP ledger reversal (after MarkShipmentInTransit) is
        // a P3 follow-up. PR-6 cancels only Draft/Picked/Staged/InTransit by
        // status flip + audit note. Reversal entries land in a follow-up.
        shipment.Status = SubcontractShipmentLifecycle.Cancelled;
        AppendNote(shipment, $"[cancel {DateTime.UtcNow:O}] by {actor ?? "system"}: {reason}");
        await _db.SaveChangesAsync(ct);
        return Result.Success(BuildShipmentSummary(shipment));
    }

    // ════════════════════════════════════════════════════════════════════════
    // RECEIPT WRITES
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<CreateSubcontractReceiptResult>> CreateReceiptAsync(
        CreateSubcontractReceiptRequest r, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == r.SubcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null)
            return Result.Failure<CreateSubcontractReceiptResult>(
                $"SubcontractOperation {r.SubcontractOperationId} not found or out of tenant scope.");

        // Idempotency: a Draft receipt with same (op, vendor packing slip) reused
        if (!string.IsNullOrEmpty(r.VendorPackingSlip))
        {
            var existing = await _db.Set<SubcontractReceipt>()
                .Where(rc => rc.SubcontractOperationId == r.SubcontractOperationId &&
                             rc.VendorPackingSlip == r.VendorPackingSlip &&
                             rc.Status == SubcontractReceiptLifecycle.Draft)
                .FirstOrDefaultAsync(ct);
            if (existing != null)
            {
                return Result.Success(new CreateSubcontractReceiptResult(
                    existing.Id, existing.ReceiptNumber,
                    "Existing Draft receipt returned (idempotent)."));
            }
        }

        // Two-phase numbering (see CreateShipmentAsync) for collision safety.
        var placeholderNumber = $"SCRCP-PEND-{Guid.NewGuid():N}";

        var receipt = new SubcontractReceipt
        {
            CompanyId = op.CompanyId,
            SiteId = op.SiteId,
            ReceiptNumber = placeholderNumber,
            VendorPackingSlip = r.VendorPackingSlip,
            SubcontractOperationId = op.Id,
            ProductionOrderId = op.ProductionOrderId,
            OperationSequence = op.OperationSequence,
            SubcontractShipmentId = r.SubcontractShipmentId,
            ServicePurchaseOrderLineId = op.ServicePurchaseOrderLineId,
            SupplierId = r.SupplierId,
            VendorLocationId = r.VendorLocationId,
            ReceivingLocationId = r.ReceivingLocationId,
            ReceiptDate = r.ReceiptDate ?? DateTime.UtcNow,
            Carrier = r.Carrier,
            TrackingNumber = r.TrackingNumber,
            CertReceived = r.CertReceived,
            CertReference = r.CertReference,
            InspectionRequired = r.InspectionRequired || op.InspectionOnReturn,
            Status = SubcontractReceiptLifecycle.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = r.CreatedBy ?? "system",
            Notes = r.Notes,
        };
        _db.Add(receipt);
        await _db.SaveChangesAsync(ct);

        // Patch the human-readable number now that Id is assigned.
        receipt.ReceiptNumber = $"SCRCP-{DateTime.UtcNow:yyyy}-{receipt.Id:D6}";
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "SubcontractReceipt created #{Id} {Num} for op {OpId} PRO {PRO}",
            receipt.Id, receipt.ReceiptNumber, op.Id, op.ProductionOrderId);

        return Result.Success(new CreateSubcontractReceiptResult(
            receipt.Id, receipt.ReceiptNumber,
            $"Draft receipt {receipt.ReceiptNumber} created."));
    }

    public async Task<Result<AddReceiptLineResult>> AddReceiptLineAsync(
        AddReceiptLineRequest r, CancellationToken ct = default)
    {
        if (r.QuantityReceived < 0m)
            return Result.Failure<AddReceiptLineResult>("QuantityReceived must be ≥ 0.");
        if (r.QuantityAccepted < 0m || r.QuantityRejected < 0m ||
            r.QuantityScrappedAtVendor < 0m || r.QuantityShort < 0m)
            return Result.Failure<AddReceiptLineResult>("All quantity fields must be ≥ 0.");
        if (r.QuantityAccepted + r.QuantityRejected > r.QuantityReceived)
            return Result.Failure<AddReceiptLineResult>(
                "accepted + rejected cannot exceed received.");

        var receipt = await _db.Set<SubcontractReceipt>()
            .Include(rc => rc.Lines)
            .Where(rc => rc.Id == r.SubcontractReceiptId &&
                         _tenant.VisibleCompanyIds.Contains(rc.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (receipt == null)
            return Result.Failure<AddReceiptLineResult>(
                $"Receipt {r.SubcontractReceiptId} not found or out of tenant scope.");

        if (receipt.Status != SubcontractReceiptLifecycle.Draft)
            return Result.Failure<AddReceiptLineResult>(
                $"Receipt {receipt.ReceiptNumber} is {receipt.Status} — lines locked.");

        var nextLineNo = receipt.Lines.Count == 0 ? 1 : receipt.Lines.Max(l => l.LineNumber) + 1;

        var line = new SubcontractReceiptLine
        {
            CompanyId = receipt.CompanyId,
            SiteId = receipt.SiteId,
            SubcontractReceiptId = receipt.Id,
            LineNumber = nextLineNo,
            SubcontractShipmentLineId = r.SubcontractShipmentLineId,
            ItemId = r.ItemId,
            PartNumber = r.PartNumber,
            Description = r.Description,
            DrawingRevision = r.DrawingRevision,
            LotNumber = r.LotNumber,
            SerialNumber = r.SerialNumber,
            QuantityReceived = r.QuantityReceived,
            QuantityAccepted = r.QuantityAccepted,
            QuantityRejected = r.QuantityRejected,
            QuantityScrappedAtVendor = r.QuantityScrappedAtVendor,
            QuantityShort = r.QuantityShort,
            Uom = r.Uom ?? "EA",
            Scenario = r.Scenario,
            Disposition = r.Disposition,
            RejectReason = r.RejectReason,
            NcrReference = r.NcrReference,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = receipt.CreatedBy,
            Notes = r.Notes,
        };
        _db.Add(line);
        await _db.SaveChangesAsync(ct);

        return Result.Success(new AddReceiptLineResult(
            line.Id, line.LineNumber, line.Scenario, line.Disposition,
            $"Line {line.LineNumber} added — scenario {line.Scenario}, disposition {line.Disposition}."));
    }

    public async Task<Result<ReceiptPostResult>> PostReceiptAsync(
        int subcontractReceiptId, string? actor, CancellationToken ct = default)
    {
        var receipt = await _db.Set<SubcontractReceipt>()
            .Include(rc => rc.Lines)
            .Where(rc => rc.Id == subcontractReceiptId &&
                         _tenant.VisibleCompanyIds.Contains(rc.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (receipt == null)
            return Result.Failure<ReceiptPostResult>(
                $"Receipt {subcontractReceiptId} not found or out of tenant scope.");

        if (receipt.Status != SubcontractReceiptLifecycle.Draft)
            return Result.Failure<ReceiptPostResult>(
                $"Receipt {receipt.ReceiptNumber} is {receipt.Status} — already posted.");

        if (!receipt.Lines.Any())
            return Result.Failure<ReceiptPostResult>(
                $"Receipt {receipt.ReceiptNumber} has no lines — nothing to post.");

        receipt.Status = SubcontractReceiptLifecycle.Posting;

        // Determine whether any line forces approval gating.
        var requiresApproval = receipt.Lines.Any(l =>
            l.Scenario == SubcontractReceiptScenario.OverReceipt ||
            l.Scenario == SubcontractReceiptScenario.WrongJobOrPo ||
            l.Disposition == SubcontractReceiptDisposition.PendingApproval);

        decimal totalAccepted = 0m, totalRejected = 0m, totalScrapped = 0m, totalReceived = 0m;
        int linesPosted = 0;

        // Wrap the multi-line posting in a DB transaction. If any line's
        // vendor-WIP ledger write fails, the whole receipt rolls back —
        // no partial state. (Codex pre-PR P2 #9.)
        using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var line in receipt.Lines.OrderBy(l => l.LineNumber))
            {
                // FIFO balance selection (Codex pre-PR P2 #10):
                //   * scoped to the right (PRO + op + supplier + item)
                //   * prefer balances that match this subcontract op when known
                //   * require non-zero quantity at vendor so we don't consume
                //     an exhausted row
                //   * order by oldest CreatedAt (true FIFO)
                var balance = await _db.Set<VendorWipBalance>()
                    .Where(b => b.ProductionOrderId == receipt.ProductionOrderId &&
                                b.OperationSequence == receipt.OperationSequence &&
                                b.SupplierId == receipt.SupplierId &&
                                b.ItemId == line.ItemId &&
                                b.QuantityAtVendor > 0m &&
                                (b.SubcontractOperationId == null ||
                                 b.SubcontractOperationId == receipt.SubcontractOperationId) &&
                                _tenant.VisibleCompanyIds.Contains(b.CompanyId))
                    .OrderBy(b => b.CreatedAt)
                    .ThenBy(b => b.Id)
                    .FirstOrDefaultAsync(ct);

                if (balance == null && line.QuantityReceived > 0m)
                {
                    _log.LogWarning(
                        "Receipt {Num} line {Ln}: no eligible VendorWipBalance with stock for PRO {PRO} op {Op} item {ItemId} — vendor-WIP ledger not updated.",
                        receipt.ReceiptNumber, line.LineNumber, receipt.ProductionOrderId,
                        receipt.OperationSequence, line.ItemId);
                }

                if (balance != null)
                {
                    // VendorScrap: scrap-only path (do NOT also call receive
                    // because the scrapped qty never returns to us).
                    // Codex pre-PR P2 #7.
                    var isScrapOnly =
                        line.Scenario == SubcontractReceiptScenario.VendorScrap &&
                        line.QuantityScrappedAtVendor > 0m;

                    if (isScrapOnly)
                    {
                        var scrap = await _vendorWip.RecordScrapAtVendorAsync(
                            new RecordScrapAtVendorRequest(
                                VendorWipBalanceId: balance.Id,
                                QuantityScrapped: line.QuantityScrappedAtVendor,
                                ReasonCode: line.RejectReason ?? "VendorScrap",
                                Notes: $"Receipt {receipt.ReceiptNumber} line {line.LineNumber}",
                                CreatedBy: actor ?? receipt.CreatedBy
                            ), ct);
                        if (!scrap.IsSuccess)
                            throw new InvalidOperationException(
                                $"Vendor-WIP scrap failed on line {line.LineNumber}: {scrap.Error}");
                        line.VendorWipTransactionId = scrap.Value!.TransactionId;
                    }
                    else if (line.QuantityReceived > 0m)
                    {
                        var recv = await _vendorWip.ReceiveFromVendorAsync(
                            new ReceiveFromVendorRequest(
                                VendorWipBalanceId: balance.Id,
                                QuantityReceived: line.QuantityReceived,
                                QuantityAccepted: line.QuantityAccepted,
                                QuantityRejected: line.QuantityRejected,
                                ReceiptDocument: receipt.ReceiptNumber,
                                Notes: $"Receipt {receipt.ReceiptNumber} line {line.LineNumber} — scenario {line.Scenario}",
                                CreatedBy: actor ?? receipt.CreatedBy
                            ), ct);
                        if (!recv.IsSuccess)
                            throw new InvalidOperationException(
                                $"Vendor-WIP receive failed on line {line.LineNumber}: {recv.Error}");
                        line.VendorWipTransactionId = recv.Value!.TransactionId;
                    }
                }

                totalReceived += line.QuantityReceived;
                totalAccepted += line.QuantityAccepted;
                totalRejected += line.QuantityRejected;
                totalScrapped += line.QuantityScrappedAtVendor;
                linesPosted++;
            }

            // Roll into the subcontract op (one call with summed values).
            // Codex P2 — scrap-only receipts (QuantityReceived == 0,
            // QuantityScrappedAtVendor > 0) MUST still update op.QuantityScrappedAtVendor.
            // RecordReceiptAsync only takes (received/accepted/rejected); we
            // patch QuantityScrappedAtVendor directly inside the same tx so
            // op totals stay in sync with the vendor-WIP ledger.
            if (totalReceived > 0m)
            {
                var rollup = await _subOpSvc.RecordReceiptAsync(
                    receipt.SubcontractOperationId, totalReceived, totalAccepted, totalRejected,
                    $"Auto-roll from receipt {receipt.ReceiptNumber} ({linesPosted} lines)", ct);
                if (!rollup.IsSuccess)
                    _log.LogWarning("Op rollup failed for receipt {Num}: {Err}",
                        receipt.ReceiptNumber, rollup.Error);
            }

            if (totalScrapped > 0m)
            {
                var opForScrap = await _db.Set<SubcontractOperation>()
                    .Where(s => s.Id == receipt.SubcontractOperationId &&
                                _tenant.VisibleCompanyIds.Contains(s.CompanyId))
                    .FirstOrDefaultAsync(ct);
                if (opForScrap != null)
                {
                    opForScrap.QuantityScrappedAtVendor += totalScrapped;
                    AppendNoteOnSubOp(opForScrap,
                        $"[scrap-roll {DateTime.UtcNow:O}] receipt {receipt.ReceiptNumber}: +{totalScrapped:N4} at vendor");
                    // Persisted by the outer SaveChangesAsync at end of the
                    // try block — keeps the scrap update inside the same
                    // transaction as the vendor-WIP ledger writes.
                }
                else
                {
                    _log.LogWarning(
                        "Scrap rollup skipped for receipt {Num}: SubcontractOperation {OpId} not loadable.",
                        receipt.ReceiptNumber, receipt.SubcontractOperationId);
                }
            }

            receipt.Status = requiresApproval
                ? SubcontractReceiptLifecycle.PendingApproval
                : SubcontractReceiptLifecycle.Posted;
            receipt.ApprovalRequired = requiresApproval;
            receipt.PostedUtc = DateTime.UtcNow;
            AppendNoteOnReceipt(receipt, $"[post {DateTime.UtcNow:O}] by {actor ?? "system"} — {linesPosted} lines, {totalAccepted:N4} accepted");

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            // Restore Draft status in-memory so the caller can retry.
            receipt.Status = SubcontractReceiptLifecycle.Draft;
            _log.LogError(ex, "PostReceipt failed for {Num} — transaction rolled back.", receipt.ReceiptNumber);
            return Result.Failure<ReceiptPostResult>(ex.Message);
        }

        return Result.Success(new ReceiptPostResult(
            receipt.Id, receipt.Status, linesPosted, totalAccepted, totalRejected, totalScrapped,
            requiresApproval,
            requiresApproval
                ? "Posted as PendingApproval — supervisor must approve before downstream effects fully release."
                : $"Posted — {linesPosted} lines, {totalAccepted:N4} accepted onto op."));
    }

    public async Task<Result<ReceiptStatusSummary>> ApproveReceiptAsync(
        int subcontractReceiptId, string? actor, CancellationToken ct = default)
    {
        var receipt = await LoadReceiptAsync(subcontractReceiptId, ct);
        if (receipt == null)
            return Result.Failure<ReceiptStatusSummary>(
                $"Receipt {subcontractReceiptId} not found or out of tenant scope.");

        if (receipt.Status != SubcontractReceiptLifecycle.PendingApproval)
            return Result.Failure<ReceiptStatusSummary>(
                $"Receipt {receipt.ReceiptNumber} is {receipt.Status} — not pending approval.");

        receipt.Status = SubcontractReceiptLifecycle.Approved;
        receipt.ApprovedBy = actor;
        receipt.ApprovedUtc = DateTime.UtcNow;
        AppendNoteOnReceipt(receipt, $"[approved {DateTime.UtcNow:O}] by {actor ?? "system"}");
        await _db.SaveChangesAsync(ct);
        return Result.Success(BuildReceiptSummary(receipt));
    }

    public async Task<Result<ReceiptStatusSummary>> ReverseReceiptAsync(
        int subcontractReceiptId, string? reason, string? actor, CancellationToken ct = default)
    {
        var receipt = await LoadReceiptAsync(subcontractReceiptId, ct);
        if (receipt == null)
            return Result.Failure<ReceiptStatusSummary>(
                $"Receipt {subcontractReceiptId} not found or out of tenant scope.");

        // Codex pre-PR P2 #8: only PendingApproval may be reversed cleanly
        // in PR-6 (no ledger or op-qty side-effects yet). Posted/Approved
        // reversal must wait for PR-8 (full ledger backout + op-qty rollback),
        // otherwise vendor WIP and op totals silently drift. Reject explicitly.
        if (receipt.Status == SubcontractReceiptLifecycle.Posted ||
            receipt.Status == SubcontractReceiptLifecycle.Approved)
            return Result.Failure<ReceiptStatusSummary>(
                $"Cannot reverse a {receipt.Status} receipt in PR-6 — vendor-WIP ledger backout + op-qty rollback land in PR-8. " +
                "Manually adjust via VendorWipProbe + SubcontractOperationProbe if needed.");

        if (receipt.Status != SubcontractReceiptLifecycle.PendingApproval)
            return Result.Failure<ReceiptStatusSummary>(
                $"Cannot reverse — receipt is {receipt.Status}.");

        receipt.Status = SubcontractReceiptLifecycle.Reversed;
        AppendNoteOnReceipt(receipt, $"[reverse {DateTime.UtcNow:O}] by {actor ?? "system"}: {reason}");
        await _db.SaveChangesAsync(ct);
        return Result.Success(BuildReceiptSummary(receipt));
    }

    // ════════════════════════════════════════════════════════════════════════
    // READS
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<ShipmentStatusSummary>> GetShipmentStatusAsync(
        int subcontractShipmentId, CancellationToken ct = default)
    {
        var shipment = await LoadShipmentAsync(subcontractShipmentId, ct);
        if (shipment == null)
            return Result.Failure<ShipmentStatusSummary>(
                $"Shipment {subcontractShipmentId} not found or out of tenant scope.");
        return Result.Success(BuildShipmentSummary(shipment));
    }

    public async Task<Result<ReceiptStatusSummary>> GetReceiptStatusAsync(
        int subcontractReceiptId, CancellationToken ct = default)
    {
        var receipt = await LoadReceiptAsync(subcontractReceiptId, ct);
        if (receipt == null)
            return Result.Failure<ReceiptStatusSummary>(
                $"Receipt {subcontractReceiptId} not found or out of tenant scope.");
        return Result.Success(BuildReceiptSummary(receipt));
    }

    public async Task<IReadOnlyList<SubcontractShipment>> GetShipmentsForOpAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        return await _db.Set<SubcontractShipment>()
            .Include(s => s.Lines)
            .Where(s => s.SubcontractOperationId == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SubcontractReceipt>> GetReceiptsForOpAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        return await _db.Set<SubcontractReceipt>()
            .Include(r => r.Lines)
            .Where(r => r.SubcontractOperationId == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(r.CompanyId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private async Task<SubcontractShipment?> LoadShipmentAsync(int id, CancellationToken ct)
    {
        return await _db.Set<SubcontractShipment>()
            .Include(s => s.Lines)
            .Where(s => s.Id == id && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
    }

    private async Task<SubcontractReceipt?> LoadReceiptAsync(int id, CancellationToken ct)
    {
        return await _db.Set<SubcontractReceipt>()
            .Include(r => r.Lines)
            .Where(r => r.Id == id && _tenant.VisibleCompanyIds.Contains(r.CompanyId))
            .FirstOrDefaultAsync(ct);
    }

    private static void AppendNote(SubcontractShipment s, string note)
    {
        s.Notes = string.IsNullOrEmpty(s.Notes) ? note : $"{s.Notes}\n{note}";
    }

    private static void AppendNoteOnReceipt(SubcontractReceipt r, string note)
    {
        r.Notes = string.IsNullOrEmpty(r.Notes) ? note : $"{r.Notes}\n{note}";
    }

    private static void AppendNoteOnSubOp(SubcontractOperation op, string note)
    {
        op.Notes = string.IsNullOrEmpty(op.Notes) ? note : $"{op.Notes}\n{note}";
    }

    private static ShipmentStatusSummary BuildShipmentSummary(SubcontractShipment s) =>
        new(s.Id, s.ShipmentNumber, s.Status, s.Lines.Count,
            s.Lines.Sum(l => l.QuantityShipped),
            s.ActualShipDate, s.ActualDeliveryDate);

    private static ReceiptStatusSummary BuildReceiptSummary(SubcontractReceipt r) =>
        new(r.Id, r.ReceiptNumber, r.Status, r.Lines.Count,
            r.Lines.Sum(l => l.QuantityAccepted),
            r.Lines.Sum(l => l.QuantityRejected),
            r.ApprovalRequired, r.ApprovedBy, r.PostedUtc);
}
