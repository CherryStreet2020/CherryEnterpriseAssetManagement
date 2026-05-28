// Sprint 15.1 PR-3 (2026-05-28) — PO-Line ↔ Demand link service.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Infrastructure;
using Abs.FixedAssets.Services.Production;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Purchasing;

public class PoLineDemandLinkService : IPoLineDemandLinkService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IProductionSupplyDemandService _demandService;
    private readonly ILogger<PoLineDemandLinkService> _logger;

    public PoLineDemandLinkService(
        AppDbContext db,
        ITenantContext tenantContext,
        IProductionSupplyDemandService demandService,
        ILogger<PoLineDemandLinkService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _demandService = demandService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // LinkAsync — create or top up
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<LinkPoLineToDemandResult>> LinkAsync(
        LinkPoLineToDemandRequest request, CancellationToken ct = default)
    {
        if (request.AllocatedQuantity <= 0m)
            return Result.Failure<LinkPoLineToDemandResult>("AllocatedQuantity must be > 0.");

        var poLine = await _db.PurchaseOrderLines
            .Include(p => p.PurchaseOrder)
            .Where(p => p.Id == request.PurchaseOrderLineId &&
                        _tenantContext.VisibleCompanyIds.Contains(p.PurchaseOrder!.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);

        if (poLine == null)
            return Result.Failure<LinkPoLineToDemandResult>(
                $"PurchaseOrderLine {request.PurchaseOrderLineId} not found or not in tenant scope.");

        var demand = await _db.Set<ProductionSupplyDemand>()
            .Where(d => d.Id == request.ProductionSupplyDemandId &&
                        _tenantContext.VisibleCompanyIds.Contains(d.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (demand == null)
            return Result.Failure<LinkPoLineToDemandResult>(
                $"ProductionSupplyDemand {request.ProductionSupplyDemandId} not found or not in tenant scope.");

        // Same-tenant guard across the two sides
        var poCompany = poLine.PurchaseOrder!.CompanyId ?? 0;
        if (poCompany != 0 && poCompany != demand.CompanyId)
            return Result.Failure<LinkPoLineToDemandResult>(
                $"Cross-tenant link refused: PO company {poCompany} vs. demand company {demand.CompanyId}.");

        // Idempotency: same PO line + demand + release → top up existing
        var existing = await _db.Set<PurchaseOrderLineDemandLink>()
            .Where(l => l.PurchaseOrderLineId == request.PurchaseOrderLineId &&
                        l.ProductionSupplyDemandId == request.ProductionSupplyDemandId &&
                        l.PurchaseOrderReleaseId == request.PurchaseOrderReleaseId &&
                        l.Status != PoDemandLinkStatus.Released &&
                        l.Status != PoDemandLinkStatus.Cancelled)
            .FirstOrDefaultAsync(ct);

        PurchaseOrderLineDemandLink link;
        if (existing != null)
        {
            existing.AllocatedQuantity += request.AllocatedQuantity;
            existing.RemainingQuantity = existing.AllocatedQuantity - existing.ReceivedQuantity;
            existing.PromiseDate = request.PromiseDate ?? existing.PromiseDate;
            existing.NeedByDate = request.NeedByDate ?? existing.NeedByDate;
            existing.Notes = string.IsNullOrEmpty(existing.Notes)
                ? request.Notes
                : (string.IsNullOrEmpty(request.Notes) ? existing.Notes : $"{existing.Notes}\n{request.Notes}");
            link = existing;
        }
        else
        {
            link = new PurchaseOrderLineDemandLink
            {
                CompanyId = demand.CompanyId,
                SiteId = demand.SiteId,
                PurchaseOrderLineId = poLine.Id,
                PurchaseOrderReleaseId = request.PurchaseOrderReleaseId,
                ProductionSupplyDemandId = demand.Id,
                ProductionOrderId = demand.ProductionOrderId,
                BomLineId = demand.BomLineId,
                OperationSequence = demand.OperationSequence,
                AllocatedQuantity = request.AllocatedQuantity,
                ReceivedQuantity = 0m,
                RemainingQuantity = request.AllocatedQuantity,
                UnitPriceAtLink = poLine.UnitPrice,
                Status = PoDemandLinkStatus.Active,
                PromiseDate = request.PromiseDate,
                NeedByDate = request.NeedByDate ?? demand.NeedByDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy ?? "system",
                Notes = request.Notes,
            };
            _db.Add(link);
        }

        await _db.SaveChangesAsync(ct);

        // Mirror into the generic ProductionSupplyAllocation so unified
        // queries (Purchasing CC supply-demand tab) see this allocation too.
        await _demandService.AllocateSupplyAsync(
            new AllocateSupplyRequest(
                demand.Id,
                AllocationSupplyType.PurchaseOrderLine,
                SupplyRecordId: poLine.Id,
                SupplyRecordLineId: request.PurchaseOrderReleaseId,
                Quantity: request.AllocatedQuantity,
                PromiseDate: request.PromiseDate,
                Notes: $"Mirrored from PoLineDemandLink #{link.Id}",
                CreatedBy: request.CreatedBy ?? "system"),
            ct);

        // Stamp dominant linkage on the PO line itself (denormalized) when
        // the PO line currently has no PRO assignment.
        if (!poLine.ProductionOrderId.HasValue)
        {
            poLine.ProductionOrderId = demand.ProductionOrderId;
            poLine.BomLineId = demand.BomLineId;
            poLine.OperationSequence = demand.OperationSequence;
            poLine.ProjectId = demand.ProjectId;
            await _db.SaveChangesAsync(ct);
        }

        // Refresh demand status
        await _demandService.RefreshSupplyStatusAsync(demand.Id, ct);

        // Compute aggregate allocated across all active links for this demand
        var aggregate = await _db.Set<PurchaseOrderLineDemandLink>()
            .Where(l => l.ProductionSupplyDemandId == demand.Id &&
                        l.Status != PoDemandLinkStatus.Released &&
                        l.Status != PoDemandLinkStatus.Cancelled)
            .SumAsync(l => l.AllocatedQuantity, ct);

        var refreshed = await _db.Set<ProductionSupplyDemand>()
            .Where(d => d.Id == demand.Id)
            .Select(d => new { d.RequiredQuantity, d.RemainingQuantity })
            .FirstOrDefaultAsync(ct);
        var remainingDemand = refreshed?.RemainingQuantity
            ?? Math.Max(0m, demand.RequiredQuantity - aggregate);

        _logger.LogInformation(
            "PoLineDemandLink: PO line {PoLineId} ↔ demand {DemandId} = {Qty}; aggregate={Agg}, remaining={Rem}",
            poLine.Id, demand.Id, link.AllocatedQuantity, aggregate, remainingDemand);

        return Result.Success(new LinkPoLineToDemandResult(
            link.Id, poLine.Id, demand.Id,
            link.AllocatedQuantity, aggregate, remainingDemand,
            $"Linked: {link.AllocatedQuantity:N4} units (aggregate {aggregate:N4} on demand, {remainingDemand:N4} remaining)"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // RecordReceiptAgainstLinkAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<RecordReceiptAgainstLinkResult>> RecordReceiptAgainstLinkAsync(
        RecordReceiptAgainstLinkRequest request, CancellationToken ct = default)
    {
        if (request.QuantityReceived <= 0m)
            return Result.Failure<RecordReceiptAgainstLinkResult>("QuantityReceived must be > 0.");

        var link = await _db.Set<PurchaseOrderLineDemandLink>()
            .Where(l => l.Id == request.LinkId &&
                        _tenantContext.VisibleCompanyIds.Contains(l.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (link == null)
            return Result.Failure<RecordReceiptAgainstLinkResult>(
                $"Link {request.LinkId} not found or not in tenant scope.");

        if (link.Status == PoDemandLinkStatus.Released || link.Status == PoDemandLinkStatus.Cancelled)
            return Result.Failure<RecordReceiptAgainstLinkResult>(
                $"Link {request.LinkId} is {link.Status} — receipts blocked.");

        link.ReceivedQuantity += request.QuantityReceived;
        link.RemainingQuantity = Math.Max(0m, link.AllocatedQuantity - link.ReceivedQuantity);

        if (link.FirstReceiptUtc == null)
            link.FirstReceiptUtc = request.ReceivedAtUtc ?? DateTime.UtcNow;

        if (link.ReceivedQuantity >= link.AllocatedQuantity)
        {
            link.Status = PoDemandLinkStatus.FullyReceived;
            link.FullyReceivedUtc = request.ReceivedAtUtc ?? DateTime.UtcNow;
        }
        else
        {
            link.Status = PoDemandLinkStatus.PartiallyReceived;
        }

        if (!string.IsNullOrEmpty(request.Notes))
        {
            link.Notes = string.IsNullOrEmpty(link.Notes)
                ? request.Notes
                : $"{link.Notes}\n[receipt {DateTime.UtcNow:O}: {request.Notes}]";
        }

        await _db.SaveChangesAsync(ct);
        await _demandService.RefreshSupplyStatusAsync(link.ProductionSupplyDemandId, ct);

        return Result.Success(new RecordReceiptAgainstLinkResult(
            link.Id, link.ReceivedQuantity, link.Status,
            $"Received {request.QuantityReceived:N4}; cumulative {link.ReceivedQuantity:N4} of {link.AllocatedQuantity:N4}. Status={link.Status}."));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ReleaseAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<ReleasePoLineDemandLinkResult>> ReleaseAsync(
        int linkId, string? reasonNotes, string? releasedBy, CancellationToken ct = default)
    {
        var link = await _db.Set<PurchaseOrderLineDemandLink>()
            .Where(l => l.Id == linkId &&
                        _tenantContext.VisibleCompanyIds.Contains(l.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (link == null)
            return Result.Failure<ReleasePoLineDemandLinkResult>(
                $"Link {linkId} not found or not in tenant scope.");

        if (link.Status == PoDemandLinkStatus.Released || link.Status == PoDemandLinkStatus.Cancelled)
            return Result.Failure<ReleasePoLineDemandLinkResult>(
                $"Link {linkId} is already {link.Status}.");

        var qtyReleased = link.AllocatedQuantity - link.ReceivedQuantity;
        link.Status = PoDemandLinkStatus.Released;
        link.ReleasedUtc = DateTime.UtcNow;
        link.Notes = string.IsNullOrEmpty(link.Notes)
            ? $"Released by {releasedBy ?? "system"}: {reasonNotes}"
            : $"{link.Notes}\n[Released by {releasedBy ?? "system"}: {reasonNotes}]";

        await _db.SaveChangesAsync(ct);
        await _demandService.RefreshSupplyStatusAsync(link.ProductionSupplyDemandId, ct);

        return Result.Success(new ReleasePoLineDemandLinkResult(
            link.Id, qtyReleased, $"Released {qtyReleased:N4} from link #{link.Id}"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // MarkPoLineDirectToJobAsync / MarkPoLineSubcontractAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<int>> MarkPoLineDirectToJobAsync(
        int purchaseOrderLineId, bool isDirectToJob, CancellationToken ct = default)
    {
        var poLine = await _db.PurchaseOrderLines
            .Include(p => p.PurchaseOrder)
            .Where(p => p.Id == purchaseOrderLineId &&
                        _tenantContext.VisibleCompanyIds.Contains(p.PurchaseOrder!.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);

        if (poLine == null)
            return Result.Failure<int>($"PO line {purchaseOrderLineId} not found or not in tenant scope.");

        poLine.IsDirectToJob = isDirectToJob;

        // If marking direct-to-job, stamp dominant linkage from highest-qty active link
        if (isDirectToJob && !poLine.ProductionOrderId.HasValue)
        {
            var dominant = await _db.Set<PurchaseOrderLineDemandLink>()
                .Where(l => l.PurchaseOrderLineId == purchaseOrderLineId &&
                            l.Status != PoDemandLinkStatus.Released &&
                            l.Status != PoDemandLinkStatus.Cancelled)
                .OrderByDescending(l => l.AllocatedQuantity)
                .FirstOrDefaultAsync(ct);
            if (dominant != null)
            {
                poLine.ProductionOrderId = dominant.ProductionOrderId;
                poLine.BomLineId = dominant.BomLineId;
                poLine.OperationSequence = dominant.OperationSequence;
            }
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(poLine.Id);
    }

    public async Task<Result<int>> MarkPoLineSubcontractAsync(
        int purchaseOrderLineId, bool isSubcontract, CancellationToken ct = default)
    {
        var poLine = await _db.PurchaseOrderLines
            .Include(p => p.PurchaseOrder)
            .Where(p => p.Id == purchaseOrderLineId &&
                        _tenantContext.VisibleCompanyIds.Contains(p.PurchaseOrder!.CompanyId ?? 0))
            .FirstOrDefaultAsync(ct);

        if (poLine == null)
            return Result.Failure<int>($"PO line {purchaseOrderLineId} not found or not in tenant scope.");

        poLine.IsSubcontract = isSubcontract;
        await _db.SaveChangesAsync(ct);
        return Result.Success(poLine.Id);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Read APIs
    // ═══════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<PurchaseOrderLineDemandLink>> GetLinksForPoLineAsync(
        int purchaseOrderLineId, CancellationToken ct = default)
    {
        return await _db.Set<PurchaseOrderLineDemandLink>()
            .Include(l => l.ProductionSupplyDemand)
            .Where(l => l.PurchaseOrderLineId == purchaseOrderLineId &&
                        _tenantContext.VisibleCompanyIds.Contains(l.CompanyId))
            .OrderBy(l => l.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PurchaseOrderLineDemandLink>> GetLinksForDemandAsync(
        int productionSupplyDemandId, CancellationToken ct = default)
    {
        return await _db.Set<PurchaseOrderLineDemandLink>()
            .Include(l => l.PurchaseOrderLine)
            .Where(l => l.ProductionSupplyDemandId == productionSupplyDemandId &&
                        _tenantContext.VisibleCompanyIds.Contains(l.CompanyId))
            .OrderBy(l => l.Id)
            .ToListAsync(ct);
    }
}
