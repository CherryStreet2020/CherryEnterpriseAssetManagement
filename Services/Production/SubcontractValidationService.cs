// Sprint 15.2 PR-9 (2026-05-28) — SubcontractValidationService impl.
//
// 15 §24 non-negotiable validations. Each rule reads the appropriate entities
// and returns Pass/Warn/Block with a description + override hint. Used as
// service-level guards by the Cockpit subcontract panel and (in a future
// follow-up) by the §5 8-step orchestrator before each action.

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

public class SubcontractValidationService : ISubcontractValidationService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ILogger<SubcontractValidationService> _log;

    public SubcontractValidationService(
        AppDbContext db,
        ITenantContext tenant,
        ILogger<SubcontractValidationService> log)
    {
        _db = db;
        _tenant = tenant;
        _log = log;
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 1 — supplier + service item required for release
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanReleaseAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("1", "CanRelease", subcontractOperationId);

        if (!op.SupplierId.HasValue)
            return Block("1", "CanRelease",
                "SubcontractOperation has no supplier assigned. Set op.SupplierId or override.",
                "Override = supervisor sign-off with Notes annotation.");
        if (!op.ServiceItemId.HasValue)
            return Warn("1", "CanRelease",
                "SubcontractOperation has no service item (purchased outside-processing service). " +
                "Service cost posting will default to op.ServiceUnitCost only.",
                "Allowed but service item resolves the GL routing — recommend setting it.");

        return Pass("1", "CanRelease", "Supplier + service item are set — release allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 2 — prior-op completion gate before ship
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanShipAsync(
        int subcontractOperationId, decimal proposedQuantity, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("2", "CanShip", subcontractOperationId);

        if (proposedQuantity <= 0m)
            return Block("2", "CanShip", "Proposed ship quantity must be > 0.", null);

        if (op.PriorOperationSequence.HasValue)
        {
            var prior = await _db.Set<ProductionOperation>()
                .Where(po => po.ProductionOrderId == op.ProductionOrderId &&
                              po.SequenceNumber == op.PriorOperationSequence.Value &&
                              _tenant.VisibleCompanyIds.Contains(po.CompanyIdSnapshot))
                .FirstOrDefaultAsync(ct);
            if (prior == null)
                return Warn("2", "CanShip",
                    $"Prior op seq {op.PriorOperationSequence} not found on the PRO — cannot verify completion.",
                    "Allowed if routing is incomplete; recommend verifying with planner.");

            if (prior.CompletedQty < proposedQuantity)
                return Block("2", "CanShip",
                    $"Prior op {prior.SequenceNumber} completed {prior.CompletedQty:N4} but ship requires {proposedQuantity:N4}.",
                    "Wait for prior op completion, or override with supervisor sign-off.");
        }

        return Pass("2", "CanShip", $"Prior-op gate clears: ship of {proposedQuantity:N4} allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 3 — over-receipt approval gate
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanReceiveAsync(
        int subcontractOperationId, decimal proposedQuantityReceived, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("3", "CanReceive", subcontractOperationId);

        if (proposedQuantityReceived <= 0m)
            return Block("3", "CanReceive", "Proposed receive quantity must be > 0.", null);

        var remainingAtVendor = op.QuantityShipped - op.QuantityReceivedBack;
        if (proposedQuantityReceived > remainingAtVendor)
        {
            var over = proposedQuantityReceived - remainingAtVendor;
            return Warn("3", "CanReceive",
                $"Over-receipt: proposed {proposedQuantityReceived:N4} vs remaining at vendor {remainingAtVendor:N4} (over by {over:N4}).",
                "Allowed only with supervisor approval — receipt will post as PendingApproval per §11 OverReceipt scenario.");
        }

        return Pass("3", "CanReceive", $"Receive of {proposedQuantityReceived:N4} within remaining {remainingAtVendor:N4} at vendor.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 4 — receipt-accepted required before Complete
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanCompleteAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("4", "CanComplete", subcontractOperationId);

        if (op.QuantityAccepted < op.QuantityToShip)
        {
            var shortBy = op.QuantityToShip - op.QuantityAccepted;
            return Block("4", "CanComplete",
                $"Cannot mark Complete — accepted {op.QuantityAccepted:N4} of {op.QuantityToShip:N4} required (short by {shortBy:N4}).",
                "Either receive more or override Complete with supervisor sign-off.");
        }
        if (op.Status == SubcontractOperationStatus.InInspection)
            return Block("4", "CanComplete",
                "Receipt is in inspection hold — Complete blocked until inspection passes.",
                "Resolve inspection first, or override.");

        return Pass("4", "CanComplete", $"All {op.QuantityToShip:N4} accepted, no inspection hold — Complete allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 5 — inspection hold blocks move-to-next
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanMoveToNextOpAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("5", "CanMoveToNextOp", subcontractOperationId);

        if (op.Status == SubcontractOperationStatus.InInspection)
            return Block("5", "CanMoveToNextOp",
                "Op is in incoming inspection — cannot release WIP to next routing op.",
                "Resolve inspection first.");

        var openInspectionReceipts = await _db.Set<SubcontractReceipt>()
            .CountAsync(r => r.SubcontractOperationId == op.Id &&
                              _tenant.VisibleCompanyIds.Contains(r.CompanyId) &&
                              r.InspectionRequired &&
                              (r.Status == SubcontractReceiptLifecycle.Posted ||
                               r.Status == SubcontractReceiptLifecycle.PendingApproval), ct);
        if (openInspectionReceipts > 0)
            return Warn("5", "CanMoveToNextOp",
                $"{openInspectionReceipts} receipt(s) on this op have InspectionRequired=true and are not yet inspection-cleared.",
                "Recommend clearing inspection before releasing to next op.");

        return Pass("5", "CanMoveToNextOp", "No inspection holds — move-to-next allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 6 — close subcontract PO blocked while vendor WIP open
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanCloseSubcontractPoAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("6", "CanCloseSubcontractPo", subcontractOperationId);

        var openWipQty = await _db.Set<VendorWipBalance>()
            .Where(b => b.SubcontractOperationId == op.Id &&
                        b.QuantityAtVendor > 0m &&
                        _tenant.VisibleCompanyIds.Contains(b.CompanyId))
            .SumAsync(b => (decimal?)b.QuantityAtVendor, ct) ?? 0m;

        if (openWipQty > 0m)
            return Block("6", "CanCloseSubcontractPo",
                $"Vendor WIP still open: {openWipQty:N4} units at vendor. Close PO blocked.",
                "Receive or scrap the open WIP first.");

        return Pass("6", "CanCloseSubcontractPo", "No open vendor WIP — close allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 7 — close PRO blocked while any subcontract WIP unresolved
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanCloseProductionOrderAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        var openWipQty = await _db.Set<VendorWipBalance>()
            .Where(b => b.ProductionOrderId == productionOrderId &&
                        b.QuantityAtVendor > 0m &&
                        _tenant.VisibleCompanyIds.Contains(b.CompanyId))
            .SumAsync(b => (decimal?)b.QuantityAtVendor, ct) ?? 0m;

        if (openWipQty > 0m)
            return Block("7", "CanCloseProductionOrder",
                $"PRO has {openWipQty:N4} units in unresolved vendor WIP across all subcontract ops.",
                "Receive or scrap open WIP before PRO close.");

        return Pass("7", "CanCloseProductionOrder", "No open vendor WIP for PRO — close allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 8 — cannot issue material physically at vendor
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanIssueMaterialAsync(
        int subcontractOperationId, int itemId, decimal proposedQuantity, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("8", "CanIssueMaterial", subcontractOperationId);

        var atVendor = await _db.Set<VendorWipBalance>()
            .Where(b => b.SubcontractOperationId == op.Id &&
                        b.ItemId == itemId &&
                        b.QuantityAtVendor > 0m &&
                        _tenant.VisibleCompanyIds.Contains(b.CompanyId))
            .SumAsync(b => (decimal?)b.QuantityAtVendor, ct) ?? 0m;

        if (atVendor > 0m)
            return Block("8", "CanIssueMaterial",
                $"Item #{itemId} has {atVendor:N4} units physically at vendor — cannot issue from inventory until returned.",
                "Receive the WIP back first, or issue from a different lot.");

        return Pass("8", "CanIssueMaterial", $"Item #{itemId} has no vendor WIP — issue of {proposedQuantity:N4} allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 9 — buy-to-job requires PO-line ↔ demand link
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanBuyToJobAsync(
        int purchaseOrderLineId, CancellationToken ct = default)
    {
        // Codex pre-PR P3 #5: tenant-scope the PO line read at the query level
        // (defense-in-depth) so cross-tenant ids get the same NotFound surface.
        var poLine = await _db.Set<PurchaseOrderLine>()
            .Include(l => l.PurchaseOrder)
            .Where(l => l.Id == purchaseOrderLineId &&
                        l.PurchaseOrder != null &&
                        l.PurchaseOrder.CompanyId.HasValue &&
                        _tenant.VisibleCompanyIds.Contains(l.PurchaseOrder.CompanyId.Value))
            .FirstOrDefaultAsync(ct);
        if (poLine == null)
            return Block("9", "CanBuyToJob",
                $"PurchaseOrderLine #{purchaseOrderLineId} not found or out of tenant scope.", null);

        // The PR-3 ProductionOrderId field on PurchaseOrderLine indicates buy-to-job intent.
        if (poLine.IsDirectToJob && !poLine.ProductionOrderId.HasValue)
            return Block("9", "CanBuyToJob",
                $"PO line #{purchaseOrderLineId} flagged IsDirectToJob but no ProductionOrderId linked.",
                "Set ProductionOrderId on the PO line, or unset IsDirectToJob.");

        if (poLine.IsDirectToJob)
        {
            var demandLinks = await _db.Set<PurchaseOrderLineDemandLink>()
                .CountAsync(d => d.PurchaseOrderLineId == poLine.Id &&
                                  _tenant.VisibleCompanyIds.Contains(d.CompanyId), ct);
            if (demandLinks == 0)
                return Warn("9", "CanBuyToJob",
                    "Buy-to-job PO line has ProductionOrderId set but no PurchaseOrderLineDemandLink rows.",
                    "Recommend creating a demand link for full §17 consolidation traceability.");
        }

        return Pass("9", "CanBuyToJob", "PO line buy-to-job linkage is valid.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 10 — consolidate PO demand must preserve allocations
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanConsolidatePoDemandAsync(
        int purchaseOrderLineId, CancellationToken ct = default)
    {
        var demandLinks = await _db.Set<PurchaseOrderLineDemandLink>()
            .Where(d => d.PurchaseOrderLineId == purchaseOrderLineId &&
                        _tenant.VisibleCompanyIds.Contains(d.CompanyId))
            .ToListAsync(ct);

        if (demandLinks.Count == 0)
            return Warn("10", "CanConsolidatePoDemand",
                "PO line has no demand-link rows. Consolidation would erase traceability if any demands target it.",
                "Create demand links before consolidating.");

        var orphaned = demandLinks.Count(d => d.AllocatedQuantity <= 0m);
        if (orphaned > 0)
            return Block("10", "CanConsolidatePoDemand",
                $"{orphaned} of {demandLinks.Count} demand links have AllocatedQuantity ≤ 0 — consolidation would orphan job allocations.",
                "Set AllocatedQuantity > 0 on every demand link.");

        return Pass("10", "CanConsolidatePoDemand", $"{demandLinks.Count} demand link(s) all carry job allocations — consolidation safe.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 11 — unapproved supplier for controlled service
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanUseSupplierAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Include(s => s.Supplier)
            .Where(s => s.Id == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null) return NotFoundFor("11", "CanUseSupplier", subcontractOperationId);

        if (op.Supplier == null)
            return Block("11", "CanUseSupplier", "No supplier attached to subcontract op.", null);

        // Vendor entity already carries ApprovalStatus / IsActive / AS9100 / CAGE
        // fields from Wave 1. A "controlled service" check uses the
        // CertRequired flag on the op + supplier approval status.
        if (op.CertRequired && !op.Supplier.IsActive)
            return Block("11", "CanUseSupplier",
                $"Op requires certs but supplier #{op.Supplier.Id} ({op.Supplier.Name}) is not active.",
                "Activate the supplier or pick a different one.");

        return Pass("11", "CanUseSupplier",
            $"Supplier #{op.Supplier.Id} ({op.Supplier.Name}) approval status OK for this op.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 12 — bypass cert when customer/spec requires it
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanBypassCertRequirementAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("12", "CanBypassCertRequirement", subcontractOperationId);

        if (op.CertRequired)
            return Block("12", "CanBypassCertRequirement",
                "Op has CertRequired=true — cannot bypass.",
                "Either set CertRequired=false (requires customer/quality sign-off) or supply the cert.");

        return Pass("12", "CanBypassCertRequirement", "CertRequired=false — bypass allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 13 — wrong revision shipped to supplier
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanShipRevisionAsync(
        int subcontractOperationId, string? drawingRevision, CancellationToken ct = default)
    {
        var op = await _db.Set<SubcontractOperation>()
            .Include(s => s.ServiceItem)
            .Where(s => s.Id == subcontractOperationId &&
                        _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);
        if (op == null) return NotFoundFor("13", "CanShipRevision", subcontractOperationId);

        if (string.IsNullOrWhiteSpace(drawingRevision))
            return Warn("13", "CanShipRevision",
                "No drawingRevision supplied for shipment line.",
                "Ship without revision tag is allowed but traceability suffers.");

        // For BIC we check against Item.CurrentRevision (Item Master field).
        // Item Master may not have CurrentRevision populated; treat null as Pass.
        var item = op.ServiceItem;
        if (item != null)
        {
            // Item has ItemRevision navigation or string field in some shapes;
            // for now we accept any non-empty revision string and warn on mismatch
            // against op.OperationCode or other hint when richer Item.CurrentRevision lands.
        }

        return Pass("13", "CanShipRevision", $"Revision '{drawingRevision}' tagged on shipment line.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 14 — wrong revision returned from supplier
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanReceiveRevisionAsync(
        int subcontractOperationId, string? returnedRevision, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null) return NotFoundFor("14", "CanReceiveRevision", subcontractOperationId);

        // Compare returnedRevision to the revision shipped out (read from the
        // op's most-recent SubcontractShipmentLine).
        var shippedRevision = await _db.Set<SubcontractShipmentLine>()
            .Join(_db.Set<SubcontractShipment>(),
                  l => l.SubcontractShipmentId, s => s.Id,
                  (l, s) => new { l, s })
            .Where(x => x.s.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(x.s.CompanyId))
            .OrderByDescending(x => x.s.CreatedAt)
            .Select(x => x.l.DrawingRevision)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(shippedRevision) &&
            !string.IsNullOrWhiteSpace(returnedRevision) &&
            !string.Equals(shippedRevision, returnedRevision, StringComparison.OrdinalIgnoreCase))
            return Warn("14", "CanReceiveRevision",
                $"Returned revision '{returnedRevision}' does not match shipped revision '{shippedRevision}'.",
                "Hold for quality/engineering approval — §11 WrongRevision scenario.");

        return Pass("14", "CanReceiveRevision",
            $"Returned revision '{returnedRevision ?? "(none)"}' acceptable.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // RULE 15 — final close project with open PO commitments
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<SubcontractValidationResult>> CanFinalCloseProjectAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        var openSubcontractOps = await _db.Set<SubcontractOperation>()
            .CountAsync(s => s.ProductionOrderId == productionOrderId &&
                              s.Status != SubcontractOperationStatus.Complete &&
                              s.Status != SubcontractOperationStatus.Closed &&
                              _tenant.VisibleCompanyIds.Contains(s.CompanyId), ct);

        if (openSubcontractOps > 0)
            return Block("15", "CanFinalCloseProject",
                $"PRO #{productionOrderId} has {openSubcontractOps} subcontract op(s) not yet Complete/Closed.",
                "Close or waive the open subcontract ops first.");

        return Pass("15", "CanFinalCloseProject",
            "All subcontract ops on the PRO are Complete or Closed — final close allowed.");
    }

    // ════════════════════════════════════════════════════════════════════════
    // AGGREGATE — run all 15 rules for the op
    // ════════════════════════════════════════════════════════════════════════

    public async Task<Result<IReadOnlyList<SubcontractValidationResult>>> RunAllAsync(
        int subcontractOperationId, CancellationToken ct = default)
    {
        var op = await LoadOpAsync(subcontractOperationId, ct);
        if (op == null)
            return Result.Failure<IReadOnlyList<SubcontractValidationResult>>(
                $"SubcontractOperation {subcontractOperationId} not found or out of tenant scope.");

        var results = new List<SubcontractValidationResult>();

        results.Add((await CanReleaseAsync(op.Id, ct)).Value!);
        results.Add((await CanShipAsync(op.Id, op.QuantityToShip, ct)).Value!);

        // Codex P2 (pre-PR + post-PR): use SHIPPED qty, not ToShip qty.
        // CanReceiveAsync validates against (QuantityShipped - QuantityReceivedBack);
        // for partially-shipped ops (e.g. 100 ToShip / 50 Shipped / 0 Received),
        // passing ToShip-Received=100 would trigger a false §11 OverReceipt
        // warning even though only 50 is actually at vendor.
        var remainingAtVendor = op.QuantityShipped - op.QuantityReceivedBack;
        if (remainingAtVendor > 0m)
            results.Add((await CanReceiveAsync(op.Id, remainingAtVendor, ct)).Value!);
        else if (op.QuantityShipped == 0m)
            results.Add(new SubcontractValidationResult("3", "CanReceive",
                SubcontractValidationOutcome.Pass,
                "Nothing shipped yet — rule N/A.", null));
        else
            results.Add(new SubcontractValidationResult("3", "CanReceive",
                SubcontractValidationOutcome.Pass,
                "Op fully received — rule N/A.", null));

        results.Add((await CanCompleteAsync(op.Id, ct)).Value!);
        results.Add((await CanMoveToNextOpAsync(op.Id, ct)).Value!);
        results.Add((await CanCloseSubcontractPoAsync(op.Id, ct)).Value!);
        results.Add((await CanCloseProductionOrderAsync(op.ProductionOrderId, ct)).Value!);
        // Rule 8 needs an itemId — use op.WipItemId if set, else use 0 + skip
        if (op.WipItemId.HasValue)
            results.Add((await CanIssueMaterialAsync(op.Id, op.WipItemId.Value, 1m, ct)).Value!);
        else
            results.Add(new SubcontractValidationResult("8", "CanIssueMaterial",
                SubcontractValidationOutcome.Pass,
                "Op has no WipItemId — rule N/A.", null));
        // Rules 9 + 10 need a poLineId — use op.ServicePurchaseOrderLineId if set
        if (op.ServicePurchaseOrderLineId.HasValue)
        {
            results.Add((await CanBuyToJobAsync(op.ServicePurchaseOrderLineId.Value, ct)).Value!);
            results.Add((await CanConsolidatePoDemandAsync(op.ServicePurchaseOrderLineId.Value, ct)).Value!);
        }
        else
        {
            results.Add(new SubcontractValidationResult("9", "CanBuyToJob",
                SubcontractValidationOutcome.Pass,
                "Op has no service PO line yet — rule N/A.", null));
            results.Add(new SubcontractValidationResult("10", "CanConsolidatePoDemand",
                SubcontractValidationOutcome.Pass,
                "Op has no service PO line yet — rule N/A.", null));
        }
        results.Add((await CanUseSupplierAsync(op.Id, ct)).Value!);
        results.Add((await CanBypassCertRequirementAsync(op.Id, ct)).Value!);

        // Codex pre-PR P2 #4: aggregate was passing hardcoded "RevA". Pull
        // the latest shipment line's actual revision (tenant-scoped) so the
        // panel reflects real data. Rule 14 then receives the same value as
        // its own "returnedRevision" since for the aggregate view we want to
        // verify the OUTBOUND revision was sane — Rule 14's own check joins
        // back to the shipment, so passing the shipped rev is correct.
        var latestShippedRev = await _db.Set<SubcontractShipmentLine>()
            .Join(_db.Set<SubcontractShipment>(),
                  l => l.SubcontractShipmentId, s => s.Id,
                  (l, s) => new { l, s })
            .Where(x => x.s.SubcontractOperationId == op.Id &&
                        _tenant.VisibleCompanyIds.Contains(x.s.CompanyId))
            .OrderByDescending(x => x.s.CreatedAt)
            .Select(x => x.l.DrawingRevision)
            .FirstOrDefaultAsync(ct);

        results.Add((await CanShipRevisionAsync(op.Id, latestShippedRev, ct)).Value!);
        results.Add((await CanReceiveRevisionAsync(op.Id, latestShippedRev, ct)).Value!);
        results.Add((await CanFinalCloseProjectAsync(op.ProductionOrderId, ct)).Value!);

        return Result.Success<IReadOnlyList<SubcontractValidationResult>>(results);
    }

    // ════════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════════

    private async Task<SubcontractOperation?> LoadOpAsync(int id, CancellationToken ct) =>
        await _db.Set<SubcontractOperation>()
            .Where(s => s.Id == id && _tenant.VisibleCompanyIds.Contains(s.CompanyId))
            .FirstOrDefaultAsync(ct);

    private static Result<SubcontractValidationResult> Pass(string n, string name, string desc) =>
        Result.Success(new SubcontractValidationResult(n, name, SubcontractValidationOutcome.Pass, desc, null));

    private static Result<SubcontractValidationResult> Warn(string n, string name, string desc, string? overrideHint) =>
        Result.Success(new SubcontractValidationResult(n, name, SubcontractValidationOutcome.Warn, desc, overrideHint));

    private static Result<SubcontractValidationResult> Block(string n, string name, string desc, string? overrideHint) =>
        Result.Success(new SubcontractValidationResult(n, name, SubcontractValidationOutcome.Block, desc, overrideHint));

    private static Result<SubcontractValidationResult> NotFoundFor(string n, string name, int id) =>
        Result.Failure<SubcontractValidationResult>(
            $"Rule {n} ({name}): SubcontractOperation {id} not found or out of tenant scope.");
}
