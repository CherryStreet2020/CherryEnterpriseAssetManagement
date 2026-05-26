// B6 Foundation Sprint PR-FS-4 (2026-05-26) — CostLayerService impl.
//
// FIFO / LIFO / Average / Standard consumption math. Immutable receipts.
// Idempotent record + safe reverse.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Items;

public sealed class CostLayerService : ICostLayerService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CostLayerService> _logger;

    public CostLayerService(AppDbContext db, ILogger<CostLayerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CostLayer> RecordReceiptAsync(
        int itemId,
        int? siteId,
        CostLayerReceiptType receiptType,
        int? receiptReferenceId,
        string? receiptDocumentNumber,
        decimal quantity,
        decimal unitCost,
        string currencyCode,
        string? lotNumber,
        string? serialNumber,
        string? heatNumber,
        string? vendorLot,
        string? vendorReference,
        string? createdBy,
        CancellationToken ct)
    {
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Receipt quantity must be > 0.");
        if (unitCost < 0m)
            throw new ArgumentOutOfRangeException(nameof(unitCost), "Receipt unit cost cannot be negative.");
        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Length != 3)
            throw new ArgumentException("CurrencyCode must be a 3-letter ISO 4217 code.", nameof(currencyCode));

        // Idempotency check: (ReceiptType, ReceiptReferenceId, LotNumber) tuple.
        // Receipts without ReceiptReferenceId (e.g., adjustments) are never
        // idempotent — they always create a new layer.
        if (receiptReferenceId.HasValue)
        {
            var existing = await _db.CostLayers
                .Where(l => l.ItemId == itemId
                         && l.SiteId == siteId
                         && l.ReceiptType == receiptType
                         && l.ReceiptReferenceId == receiptReferenceId.Value
                         && l.LotNumber == lotNumber
                         && l.Status != CostLayerStatus.Reversed)
                .FirstOrDefaultAsync(ct);
            if (existing != null)
            {
                if (existing.ReceivedQuantity == quantity && existing.UnitCost == unitCost)
                {
                    _logger.LogInformation(
                        "CostLayerService: idempotent no-op — layer {LayerId} already exists for Item {ItemId} Site {SiteId} {ReceiptType}/{RefId} Lot {Lot} qty {Qty} cost {Cost}.",
                        existing.Id, itemId, siteId, receiptType, receiptReferenceId, lotNumber, quantity, unitCost);
                    return existing;
                }
                throw new InvalidOperationException(
                    $"CostLayer already exists for ({receiptType}, {receiptReferenceId}, Lot={lotNumber ?? "<null>"}) " +
                    $"with qty={existing.ReceivedQuantity} unitCost={existing.UnitCost}, but caller supplied " +
                    $"qty={quantity} unitCost={unitCost}. Reverse the existing receipt first, then re-record.");
            }
        }

        // Next LayerNumber per (Item, Site, Tenant). Tenant scoping deferred to
        // a follow-up — for now we scope per (Item, Site) which is sufficient
        // for the current single-tenant-per-DB shape.
        var nextLayerNumber = await _db.CostLayers
            .Where(l => l.ItemId == itemId && l.SiteId == siteId)
            .MaxAsync(l => (long?)l.LayerNumber, ct) ?? 0L;
        nextLayerNumber++;

        var now = DateTime.UtcNow;
        var layer = new CostLayer
        {
            ItemId = itemId,
            SiteId = siteId,
            LayerNumber = nextLayerNumber,
            ReceivedAtUtc = now,
            ReceiptType = receiptType,
            ReceiptReferenceId = receiptReferenceId,
            ReceiptDocumentNumber = receiptDocumentNumber,
            ReceivedQuantity = quantity,
            RemainingQuantity = quantity,
            UnitCost = unitCost,
            CurrencyCode = currencyCode,
            LotNumber = lotNumber,
            SerialNumber = serialNumber,
            HeatNumber = heatNumber,
            VendorLot = vendorLot,
            VendorReference = vendorReference,
            Status = CostLayerStatus.Open,
            CreatedAt = now,
            CreatedBy = createdBy,
        };
        _db.CostLayers.Add(layer);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CostLayerService: recorded layer {LayerId} Item {ItemId} Site {SiteId} #{Layer} qty {Qty} @ {Cost} {Currency} ({ReceiptType}/{RefId} Lot={Lot}).",
            layer.Id, itemId, siteId, nextLayerNumber, quantity, unitCost, currencyCode, receiptType, receiptReferenceId, lotNumber);

        return layer;
    }

    public async Task<IReadOnlyList<CostLayerConsumption>> ConsumeQuantityAsync(
        int itemId,
        int? siteId,
        decimal quantity,
        CostMethod costMethod,
        string? consumedBy,
        string? consumptionReason,
        CancellationToken ct)
    {
        if (quantity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Consumption quantity must be > 0.");

        // PR-FS-4 P1 fix (Codex on PR #360): refuse to consume in Standard mode.
        // Silently mapping Standard → FIFO at actual layer costs would misstate
        // the issue valuation AND lose the standard-vs-actual variance signal
        // that posts to the purchase-price-variance GL account via PRA-7
        // PostingProfile. The Sprint 14.4 cost-rollup engine will expose a
        // dedicated standard-cost issue path that consumes layers FIFO (for
        // physical inventory accuracy) but emits consumption rows at standard
        // cost and writes a parallel variance row at (actual - standard).
        // Until that lands, refuse Standard via this entry point.
        if (costMethod == CostMethod.Standard)
        {
            throw new NotSupportedException(
                "CostMethod.Standard is not supported by CostLayerService.ConsumeQuantityAsync " +
                "— standard-cost issue valuation requires explicit standard-cost lookup + variance " +
                "posting which is the Sprint 14.4 cost-rollup engine's responsibility. " +
                "Use FIFO / LIFO / Average / LastPurchase for direct layer consumption.");
        }

        // PR-FS-4 P1 fix #2 (Codex on PR #360): retry on DbUpdateConcurrencyException.
        // EF's IsRowVersion() on CostLayer.RowVersion enforces optimistic concurrency
        // — concurrent consumes for the same (Item, Site) detect the conflict and
        // we re-read state + re-apply the consume math. 3 retries handles the
        // common transient case; persistent contention surfaces as the exception.
        const int maxRetries = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ConsumeQuantityInternalAsync(itemId, siteId, quantity, costMethod, consumedBy, consumptionReason, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    "CostLayerService: concurrency conflict on consume Item={ItemId} Site={SiteId} Qty={Qty} attempt={Attempt}/{Max} — retrying.",
                    itemId, siteId, quantity, attempt, maxRetries);
                // Reset EF change tracker so the next attempt re-reads layers.
                foreach (var entry in _db.ChangeTracker.Entries<CostLayer>().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }

    private async Task<IReadOnlyList<CostLayerConsumption>> ConsumeQuantityInternalAsync(
        int itemId,
        int? siteId,
        decimal quantity,
        CostMethod costMethod,
        string? consumedBy,
        string? consumptionReason,
        CancellationToken ct)
    {
        // Average method: blend across all open layers, decrement proportionally.
        if (costMethod == CostMethod.Average)
        {
            return await ConsumeAverageAsync(itemId, siteId, quantity, costMethod, consumedBy, consumptionReason, ct);
        }

        // FIFO / LIFO / LastPurchase: pull layers in order.
        var layersQuery = _db.CostLayers
            .Where(l => l.ItemId == itemId
                     && l.SiteId == siteId
                     && l.Status == CostLayerStatus.Open
                     && l.RemainingQuantity > 0m);
        var layers = costMethod switch
        {
            CostMethod.LIFO         => await layersQuery.OrderByDescending(l => l.ReceivedAtUtc).ThenByDescending(l => l.LayerNumber).ToListAsync(ct),
            CostMethod.LastPurchase => await layersQuery.OrderByDescending(l => l.ReceivedAtUtc).ThenByDescending(l => l.LayerNumber).ToListAsync(ct),
            _                       => await layersQuery.OrderBy(l => l.ReceivedAtUtc).ThenBy(l => l.LayerNumber).ToListAsync(ct),
        };

        var totalAvailable = layers.Sum(l => l.RemainingQuantity);
        if (totalAvailable < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient open cost-layer quantity for Item {itemId} Site {siteId}: " +
                $"requested {quantity}, available {totalAvailable}. Reverse a consumption " +
                $"or record an adjustment-in before retrying.");
        }

        var now = DateTime.UtcNow;
        var consumptions = new List<CostLayerConsumption>();
        var remaining = quantity;
        foreach (var layer in layers)
        {
            if (remaining <= 0m) break;

            var pull = Math.Min(layer.RemainingQuantity, remaining);
            layer.RemainingQuantity -= pull;
            layer.UpdatedAt = now;
            layer.UpdatedBy = consumedBy;

            if (layer.RemainingQuantity == 0m)
            {
                layer.Status = CostLayerStatus.Exhausted;
                layer.ExhaustedAtUtc = now;
            }

            consumptions.Add(new CostLayerConsumption(
                CostLayerId: layer.Id,
                ItemId: itemId,
                SiteId: siteId,
                QuantityConsumed: pull,
                UnitCost: layer.UnitCost,
                CostConsumed: pull * layer.UnitCost,
                CurrencyCode: layer.CurrencyCode,
                LotNumber: layer.LotNumber,
                SerialNumber: layer.SerialNumber,
                HeatNumber: layer.HeatNumber,
                VendorLot: layer.VendorLot,
                ReceiptReferenceId: layer.ReceiptReferenceId,
                ReceiptType: layer.ReceiptType,
                CostMethodApplied: costMethod));

            remaining -= pull;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "CostLayerService: consumed {Qty} from Item {ItemId} Site {SiteId} via {Method} — {Layers} layers hit, reason: {Reason}.",
            quantity, itemId, siteId, costMethod, consumptions.Count, consumptionReason ?? "<null>");

        return consumptions;
    }

    private async Task<IReadOnlyList<CostLayerConsumption>> ConsumeAverageAsync(
        int itemId,
        int? siteId,
        decimal quantity,
        CostMethod requestedMethod,
        string? consumedBy,
        string? consumptionReason,
        CancellationToken ct)
    {
        var layers = await _db.CostLayers
            .Where(l => l.ItemId == itemId
                     && l.SiteId == siteId
                     && l.Status == CostLayerStatus.Open
                     && l.RemainingQuantity > 0m)
            .OrderBy(l => l.ReceivedAtUtc)
            .ThenBy(l => l.LayerNumber)
            .ToListAsync(ct);

        var totalAvailable = layers.Sum(l => l.RemainingQuantity);
        if (totalAvailable < quantity)
        {
            throw new InvalidOperationException(
                $"Insufficient open cost-layer quantity for Item {itemId} Site {siteId}: " +
                $"requested {quantity}, available {totalAvailable}.");
        }

        // Weighted-avg cost computed from current open layers.
        var totalValue = layers.Sum(l => l.RemainingQuantity * l.UnitCost);
        var avgCost = totalAvailable > 0m ? totalValue / totalAvailable : 0m;

        // Decrement proportionally so the post-consume weighted-avg is preserved.
        var now = DateTime.UtcNow;
        var consumptions = new List<CostLayerConsumption>(layers.Count);
        var remainingToConsume = quantity;
        foreach (var layer in layers)
        {
            if (remainingToConsume <= 0m) break;

            // Proportional pull: layer share of total qty × requested qty.
            var share = layer.RemainingQuantity / totalAvailable;
            var pull = Math.Round(share * quantity, 4, MidpointRounding.AwayFromZero);
            // Cap at remaining; on the last layer, take whatever's left.
            pull = Math.Min(pull, layer.RemainingQuantity);
            pull = Math.Min(pull, remainingToConsume);

            layer.RemainingQuantity -= pull;
            layer.UpdatedAt = now;
            layer.UpdatedBy = consumedBy;
            if (layer.RemainingQuantity == 0m)
            {
                layer.Status = CostLayerStatus.Exhausted;
                layer.ExhaustedAtUtc = now;
            }

            consumptions.Add(new CostLayerConsumption(
                CostLayerId: layer.Id,
                ItemId: itemId,
                SiteId: siteId,
                QuantityConsumed: pull,
                UnitCost: avgCost,  // weighted-avg basis, not layer-specific
                CostConsumed: pull * avgCost,
                CurrencyCode: layer.CurrencyCode,
                LotNumber: layer.LotNumber,
                SerialNumber: layer.SerialNumber,
                HeatNumber: layer.HeatNumber,
                VendorLot: layer.VendorLot,
                ReceiptReferenceId: layer.ReceiptReferenceId,
                ReceiptType: layer.ReceiptType,
                CostMethodApplied: requestedMethod));

            remainingToConsume -= pull;
        }

        // Rounding leftover (typically a few units of 0.0001) — assign to the
        // last touched layer if remaining still > 0.
        if (remainingToConsume > 0m && consumptions.Count > 0)
        {
            var lastConsumption = consumptions[^1];
            var lastLayer = layers.First(l => l.Id == lastConsumption.CostLayerId);
            var extraPull = Math.Min(remainingToConsume, lastLayer.RemainingQuantity);
            if (extraPull > 0m)
            {
                lastLayer.RemainingQuantity -= extraPull;
                if (lastLayer.RemainingQuantity == 0m)
                {
                    lastLayer.Status = CostLayerStatus.Exhausted;
                    lastLayer.ExhaustedAtUtc = now;
                }
                consumptions[^1] = lastConsumption with
                {
                    QuantityConsumed = lastConsumption.QuantityConsumed + extraPull,
                    CostConsumed = (lastConsumption.QuantityConsumed + extraPull) * avgCost,
                };
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "CostLayerService: Average-consumed {Qty} from Item {ItemId} Site {SiteId} at weighted-avg {AvgCost}, {Layers} layers hit.",
            quantity, itemId, siteId, avgCost, consumptions.Count);

        return consumptions;
    }

    public async Task<IReadOnlyList<CostLayer>> GetOpenLayersAsync(int itemId, int? siteId, CancellationToken ct)
    {
        return await _db.CostLayers.AsNoTracking()
            .Where(l => l.ItemId == itemId
                     && l.SiteId == siteId
                     && l.Status == CostLayerStatus.Open
                     && l.RemainingQuantity > 0m)
            .OrderBy(l => l.ReceivedAtUtc)
            .ThenBy(l => l.LayerNumber)
            .ToListAsync(ct);
    }

    public async Task<decimal> GetTotalOpenQuantityAsync(int itemId, int? siteId, CancellationToken ct)
    {
        return await _db.CostLayers.AsNoTracking()
            .Where(l => l.ItemId == itemId
                     && l.SiteId == siteId
                     && l.Status == CostLayerStatus.Open
                     && l.RemainingQuantity > 0m)
            .SumAsync(l => l.RemainingQuantity, ct);
    }

    public async Task<decimal> GetWeightedAverageCostAsync(int itemId, int? siteId, CancellationToken ct)
    {
        var layers = await _db.CostLayers.AsNoTracking()
            .Where(l => l.ItemId == itemId
                     && l.SiteId == siteId
                     && l.Status == CostLayerStatus.Open
                     && l.RemainingQuantity > 0m)
            .Select(l => new { l.RemainingQuantity, l.UnitCost })
            .ToListAsync(ct);

        var totalQty = layers.Sum(l => l.RemainingQuantity);
        if (totalQty <= 0m) return 0m;
        var totalValue = layers.Sum(l => l.RemainingQuantity * l.UnitCost);
        return totalValue / totalQty;
    }

    public async Task<CostLayer> ReverseReceiptAsync(int layerId, string reason, string? reversedBy, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reversal reason is required.", nameof(reason));

        var layer = await _db.CostLayers.FirstOrDefaultAsync(l => l.Id == layerId, ct)
            ?? throw new InvalidOperationException($"CostLayer {layerId} not found.");

        if (layer.Status == CostLayerStatus.Reversed)
            throw new InvalidOperationException($"CostLayer {layerId} is already reversed.");

        if (layer.RemainingQuantity != layer.ReceivedQuantity)
        {
            var consumed = layer.ReceivedQuantity - layer.RemainingQuantity;
            throw new InvalidOperationException(
                $"Cannot reverse CostLayer {layerId} — {consumed} units have been consumed. " +
                $"Reverse the downstream consumptions first.");
        }

        var now = DateTime.UtcNow;
        layer.Status = CostLayerStatus.Reversed;
        layer.RemainingQuantity = 0m;
        layer.ReversedAtUtc = now;
        layer.ReversalReason = reason;
        layer.UpdatedAt = now;
        layer.UpdatedBy = reversedBy;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CostLayerService: reversed layer {LayerId} (Item {ItemId} Site {SiteId} qty {Qty}) reason='{Reason}' by {By}.",
            layerId, layer.ItemId, layer.SiteId, layer.ReceivedQuantity, reason, reversedBy ?? "<null>");

        return layer;
    }
}
