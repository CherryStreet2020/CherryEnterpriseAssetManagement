// Sprint 15.1 PR-2 (2026-05-28) — ProductionSupplyDemandService implementation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Production;
using Abs.FixedAssets.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Production;

public class ProductionSupplyDemandService : IProductionSupplyDemandService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ProductionSupplyDemandService> _logger;

    public ProductionSupplyDemandService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<ProductionSupplyDemandService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════
    // GenerateDemandsFromProAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<GenerateDemandsResult>> GenerateDemandsFromProAsync(
        int productionOrderId, string? createdBy, CancellationToken ct = default)
    {
        var pro = await _db.Set<ProductionOrder>()
            .Where(p => p.Id == productionOrderId &&
                        _tenantContext.VisibleCompanyIds.Contains(p.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (pro == null)
            return Result.Failure<GenerateDemandsResult>(
                $"ProductionOrder {productionOrderId} not found or not in tenant scope.");

        // Load BOM lines (frozen snapshot)
        var bomLines = await _db.Set<ProductionMaterialStructure>()
            .Where(b => b.ProductionOrderId == productionOrderId)
            .OrderBy(b => b.Sequence)
            .ToListAsync(ct);

        if (bomLines.Count == 0)
        {
            return Result.Success(new GenerateDemandsResult(
                productionOrderId, 0, 0, 0,
                $"No BOM lines on PRO {pro.OrderNumber} — nothing to generate."));
        }

        // Find existing demands so we can skip dupes
        var existingDemandBomLineIds = await _db.Set<ProductionSupplyDemand>()
            .Where(d => d.ProductionOrderId == productionOrderId && d.BomLineId != null)
            .Select(d => d.BomLineId!.Value)
            .ToListAsync(ct);

        var existingSet = new HashSet<int>(existingDemandBomLineIds);

        var created = 0;
        var skipped = 0;
        var nowUtc = DateTime.UtcNow;

        foreach (var bomLine in bomLines)
        {
            if (existingSet.Contains(bomLine.Id))
            {
                skipped++;
                continue;
            }

            // Map BOM-line supply type → SupplyPolicy default
            var policy = bomLine.MaterialSupplyType switch
            {
                MaterialSupplyType.PurchaseToJob       => SupplyPolicy.BuyDirectToJob,
                MaterialSupplyType.PurchaseToInventory => SupplyPolicy.InventoryFirstThenBuy,
                MaterialSupplyType.MakeToJob           => SupplyPolicy.MakeDirectToJob,
                MaterialSupplyType.MakeToInventory     => SupplyPolicy.MakeToStockReserve,
                MaterialSupplyType.Transfer            => SupplyPolicy.TransferFromWarehouse,
                MaterialSupplyType.ExistingInventory   => SupplyPolicy.InventoryFirstThenBuy,
                MaterialSupplyType.Floorstock          => SupplyPolicy.Floorstock,
                MaterialSupplyType.Subcontract         => SupplyPolicy.BuyToVendorLocation,
                _ => SupplyPolicy.ManualBuyerDecision,
            };

            var demand = new ProductionSupplyDemand
            {
                CompanyId = pro.CompanyId,
                SiteId = null,
                DemandNumber = $"DMD-{pro.OrderNumber ?? pro.Id.ToString()}-{bomLine.Sequence:000}",
                ProductionOrderId = productionOrderId,
                BomLineId = bomLine.Id,
                OperationSequence = bomLine.ConsumingOperationSequence,
                ProjectId = null,
                CustomerId = pro.CustomerId,
                ParentDemandId = null,
                ItemId = bomLine.ChildItemId,
                PartNumber = bomLine.ChildPartNumber,
                Revision = bomLine.ChildRevision,
                Description = null,
                Uom = bomLine.Uom,
                RequiredQuantity = bomLine.SupplyQuantityRequired > 0
                    ? bomLine.SupplyQuantityRequired
                    : bomLine.QuantityPer * pro.QuantityOrdered,
                ReservedQuantity = 0m,
                SuppliedQuantity = 0m,
                ReceivedQuantity = 0m,
                RemainingQuantity = bomLine.SupplyQuantityRequired > 0
                    ? bomLine.SupplyQuantityRequired
                    : bomLine.QuantityPer * pro.QuantityOrdered,
                RequiredDate = pro.ScheduledEnd ?? pro.PromiseDate,
                NeedByDate = bomLine.SupplyRequiredDate ?? pro.ScheduledEnd ?? pro.PromiseDate,
                SourceType = DemandSourceType.BomLine,
                SupplyPolicy = policy,
                InspectionRequired = false,
                CertRequired = false,
                CustomerOwned = false,
                Consigned = false,
                ItarOrExportControlled = false,
                SourceStatus = DemandSourceStatus.NotDetermined,
                SupplyStatus = DemandSupplyStatus.NotSupplied,
                ShortageStatus = DemandShortageStatus.NoShortage,
                CostStatus = DemandCostStatus.NotCommitted,
                AlertStatus = DemandAlertStatus.None,
                CreatedAt = nowUtc,
                CreatedBy = createdBy ?? "system",
                LastRefreshedUtc = nowUtc,
            };

            _db.Set<ProductionSupplyDemand>().Add(demand);
            created++;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ProductionSupplyDemand: generated {Created} demand rows (skipped {Skipped} existing) for PRO {ProId} ({Number})",
            created, skipped, productionOrderId, pro.OrderNumber);

        return Result.Success(new GenerateDemandsResult(
            productionOrderId, created, skipped, 0,
            $"Generated {created} demand rows from {bomLines.Count} BOM lines (skipped {skipped} existing)."));
    }

    // ═══════════════════════════════════════════════════════════════════
    // RefreshSupplyStatusAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<RefreshSupplyResult>> RefreshSupplyStatusAsync(
        int demandId, CancellationToken ct = default)
    {
        var demand = await _db.Set<ProductionSupplyDemand>()
            .Include(d => d.Allocations)
            .Include(d => d.LinkedPurchaseOrderLine)
            .Where(d => d.Id == demandId &&
                        _tenantContext.VisibleCompanyIds.Contains(d.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (demand == null)
            return Result.Failure<RefreshSupplyResult>(
                $"Demand {demandId} not found or not in tenant scope.");

        // Roll up allocations
        var totalAllocated = demand.Allocations
            .Where(a => a.Status != AllocationStatus.Cancelled && a.Status != AllocationStatus.Released)
            .Sum(a => a.AllocatedQuantity);
        var totalConsumed = demand.Allocations.Sum(a => a.ConsumedQuantity);

        demand.SuppliedQuantity = totalAllocated;
        demand.ReceivedQuantity = totalConsumed;
        demand.RemainingQuantity = Math.Max(0m, demand.RequiredQuantity - totalAllocated);

        // SourceStatus
        if (demand.LinkedPurchaseOrderLineId.HasValue ||
            demand.LinkedChildProductionOrderId.HasValue ||
            !string.IsNullOrEmpty(demand.LinkedInventoryReservation) ||
            !string.IsNullOrEmpty(demand.LinkedTransferOrder))
        {
            if (demand.SourceStatus == DemandSourceStatus.NotDetermined)
                demand.SourceStatus = DemandSourceStatus.AutoResolved;
        }

        // SupplyStatus
        if (totalAllocated <= 0m)
            demand.SupplyStatus = DemandSupplyStatus.NotSupplied;
        else if (totalConsumed >= demand.RequiredQuantity && demand.RequiredQuantity > 0m)
            demand.SupplyStatus = DemandSupplyStatus.FullyFulfilled;
        else if (totalConsumed > 0m)
            demand.SupplyStatus = DemandSupplyStatus.PartiallyFulfilled;
        else if (totalAllocated >= demand.RequiredQuantity)
            demand.SupplyStatus = DemandSupplyStatus.Committed;
        else
            demand.SupplyStatus = DemandSupplyStatus.Planned;

        // ShortageStatus — date-based + quantity-based
        var now = DateTime.UtcNow;
        var needBy = demand.NeedByDate ?? demand.RequiredDate;
        if (totalAllocated < demand.RequiredQuantity && demand.SupplyStatus != DemandSupplyStatus.FullyFulfilled)
        {
            if (needBy.HasValue && needBy.Value < now)
                demand.ShortageStatus = DemandShortageStatus.Late;
            else if (totalAllocated <= 0m && demand.RequiredQuantity > 0m)
                demand.ShortageStatus = DemandShortageStatus.Short;
            else if (needBy.HasValue && (needBy.Value - now).TotalDays < 7)
                demand.ShortageStatus = DemandShortageStatus.Critical;
            else
                demand.ShortageStatus = DemandShortageStatus.Warning;
        }
        else
        {
            demand.ShortageStatus = DemandShortageStatus.NoShortage;
        }

        // CostStatus
        if (totalConsumed > 0m)
            demand.CostStatus = DemandCostStatus.Actualized;
        else if (totalAllocated > 0m)
            demand.CostStatus = DemandCostStatus.Committed;
        else
            demand.CostStatus = DemandCostStatus.NotCommitted;

        // AlertStatus rollup
        demand.AlertStatus = demand.ShortageStatus switch
        {
            DemandShortageStatus.Late or DemandShortageStatus.Critical or DemandShortageStatus.Short
                => DemandAlertStatus.Critical,
            DemandShortageStatus.Warning or DemandShortageStatus.OnHold
                => DemandAlertStatus.Warning,
            _ => DemandAlertStatus.None,
        };

        demand.LastRefreshedUtc = now;

        await _db.SaveChangesAsync(ct);

        return Result.Success(new RefreshSupplyResult(
            demand.Id, demand.SourceStatus, demand.SupplyStatus,
            demand.ShortageStatus, demand.CostStatus,
            $"Refreshed: Source={demand.SourceStatus}, Supply={demand.SupplyStatus}, " +
            $"Shortage={demand.ShortageStatus}, Cost={demand.CostStatus}. " +
            $"Allocated={totalAllocated:N4}, Consumed={totalConsumed:N4}, Required={demand.RequiredQuantity:N4}."));
    }

    public async Task<Result<int>> RefreshSupplyStatusForProAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        var demandIds = await _db.Set<ProductionSupplyDemand>()
            .Where(d => d.ProductionOrderId == productionOrderId &&
                        _tenantContext.VisibleCompanyIds.Contains(d.CompanyId))
            .Select(d => d.Id)
            .ToListAsync(ct);

        var updated = 0;
        foreach (var id in demandIds)
        {
            var r = await RefreshSupplyStatusAsync(id, ct);
            if (r.IsSuccess) updated++;
        }

        return Result.Success(updated);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Read APIs
    // ═══════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<ProductionSupplyDemand>> GetUnresolvedDemandsAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<ProductionSupplyDemand>()
            .Where(d => d.ProductionOrderId == productionOrderId &&
                        _tenantContext.VisibleCompanyIds.Contains(d.CompanyId) &&
                        (d.SupplyStatus == DemandSupplyStatus.NotSupplied ||
                         d.SupplyStatus == DemandSupplyStatus.Planned ||
                         d.SupplyStatus == DemandSupplyStatus.PartiallyFulfilled))
            .OrderBy(d => d.NeedByDate ?? DateTime.MaxValue)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductionSupplyDemand>> GetDemandsForProAsync(
        int productionOrderId, CancellationToken ct = default)
    {
        return await _db.Set<ProductionSupplyDemand>()
            .Where(d => d.ProductionOrderId == productionOrderId &&
                        _tenantContext.VisibleCompanyIds.Contains(d.CompanyId))
            .OrderBy(d => d.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductionSupplyAllocation>> GetAllocationsForDemandAsync(
        int demandId, CancellationToken ct = default)
    {
        return await _db.Set<ProductionSupplyAllocation>()
            .Where(a => a.ProductionSupplyDemandId == demandId &&
                        _tenantContext.VisibleCompanyIds.Contains(a.CompanyId))
            .OrderBy(a => a.Id)
            .ToListAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    // AllocateSupplyAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<AllocateSupplyResult>> AllocateSupplyAsync(
        AllocateSupplyRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0m)
            return Result.Failure<AllocateSupplyResult>("Allocated quantity must be > 0.");

        var demand = await _db.Set<ProductionSupplyDemand>()
            .Where(d => d.Id == request.DemandId &&
                        _tenantContext.VisibleCompanyIds.Contains(d.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (demand == null)
            return Result.Failure<AllocateSupplyResult>(
                $"Demand {request.DemandId} not found or not in tenant scope.");

        // Idempotency: same demand + supply tuple → update existing
        var existing = await _db.Set<ProductionSupplyAllocation>()
            .Where(a => a.ProductionSupplyDemandId == request.DemandId &&
                        a.SupplyType == request.SupplyType &&
                        a.SupplyRecordId == request.SupplyRecordId &&
                        a.SupplyRecordLineId == request.SupplyRecordLineId &&
                        a.Status != AllocationStatus.Cancelled &&
                        a.Status != AllocationStatus.Released)
            .FirstOrDefaultAsync(ct);

        ProductionSupplyAllocation allocation;
        if (existing != null)
        {
            existing.AllocatedQuantity += request.Quantity;
            existing.RemainingQuantity = existing.AllocatedQuantity - existing.ConsumedQuantity;
            allocation = existing;
        }
        else
        {
            allocation = new ProductionSupplyAllocation
            {
                CompanyId = demand.CompanyId,
                SiteId = demand.SiteId,
                ProductionSupplyDemandId = demand.Id,
                SupplyType = request.SupplyType,
                SupplyRecordId = request.SupplyRecordId,
                SupplyRecordLineId = request.SupplyRecordLineId,
                PurchaseOrderLineId = request.SupplyType == AllocationSupplyType.PurchaseOrderLine
                    ? request.SupplyRecordLineId : null,
                ChildProductionOrderId = request.SupplyType == AllocationSupplyType.ChildProductionOrder
                    ? request.SupplyRecordId : null,
                AllocatedQuantity = request.Quantity,
                ConsumedQuantity = 0m,
                RemainingQuantity = request.Quantity,
                Status = AllocationStatus.Active,
                PromiseDate = request.PromiseDate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy ?? "system",
                Notes = request.Notes,
            };
            _db.Set<ProductionSupplyAllocation>().Add(allocation);
        }

        await _db.SaveChangesAsync(ct);

        // Refresh demand roll-ups
        await RefreshSupplyStatusAsync(demand.Id, ct);

        _logger.LogInformation(
            "ProductionSupplyDemand: allocated {Qty} of {SupplyType}:{RecId}/{LineId} to demand {DmdId}",
            request.Quantity, request.SupplyType, request.SupplyRecordId, request.SupplyRecordLineId, demand.Id);

        return Result.Success(new AllocateSupplyResult(
            allocation.Id, demand.Id, allocation.AllocatedQuantity,
            Math.Max(0m, demand.RequiredQuantity - allocation.AllocatedQuantity),
            $"Allocated {request.Quantity:N4} via allocation #{allocation.Id}"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // ReleaseAllocationAsync
    // ═══════════════════════════════════════════════════════════════════

    public async Task<Result<ReleaseAllocationResult>> ReleaseAllocationAsync(
        int allocationId, string? reasonNotes, string? releasedBy, CancellationToken ct = default)
    {
        var allocation = await _db.Set<ProductionSupplyAllocation>()
            .Where(a => a.Id == allocationId &&
                        _tenantContext.VisibleCompanyIds.Contains(a.CompanyId))
            .FirstOrDefaultAsync(ct);

        if (allocation == null)
            return Result.Failure<ReleaseAllocationResult>(
                $"Allocation {allocationId} not found or not in tenant scope.");

        if (allocation.Status == AllocationStatus.Released ||
            allocation.Status == AllocationStatus.Cancelled)
            return Result.Failure<ReleaseAllocationResult>(
                $"Allocation {allocationId} is already {allocation.Status}.");

        var releasedQty = allocation.AllocatedQuantity - allocation.ConsumedQuantity;
        allocation.Status = AllocationStatus.Released;
        allocation.ReleasedAtUtc = DateTime.UtcNow;
        allocation.Notes = string.IsNullOrEmpty(allocation.Notes)
            ? $"Released by {releasedBy ?? "system"}: {reasonNotes}"
            : $"{allocation.Notes}\n[Released by {releasedBy ?? "system"}: {reasonNotes}]";

        await _db.SaveChangesAsync(ct);

        await RefreshSupplyStatusAsync(allocation.ProductionSupplyDemandId, ct);

        return Result.Success(new ReleaseAllocationResult(
            allocation.Id, allocation.ProductionSupplyDemandId, releasedQty,
            $"Released {releasedQty:N4} from allocation #{allocation.Id}"));
    }
}
