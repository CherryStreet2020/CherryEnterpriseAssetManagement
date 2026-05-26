// B6 Foundation Sprint PR-FS-3 (2026-05-26) — ItemStandardCostService impl.
//
// Effective-date-aware read + write. Cascade: per-Site → Item-level → $0.
// Idempotent SetCostElementAsync (no-op write when the value didn't change).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Items;

public sealed class ItemStandardCostService : IItemStandardCostService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItemStandardCostService> _logger;

    public ItemStandardCostService(AppDbContext db, ILogger<ItemStandardCostService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ItemCostBreakdown?> GetCostBreakdownAsync(
        int itemId,
        int? siteId,
        DateTime? asOfUtc,
        CancellationToken ct)
    {
        // Item existence check — return null if not found (mirrors ItemSiteResolver pattern).
        var itemExists = await _db.Items.AsNoTracking().AnyAsync(i => i.Id == itemId, ct);
        if (!itemExists)
        {
            _logger.LogWarning("ItemStandardCostService: Item {ItemId} not found.", itemId);
            return null;
        }

        var asOf = asOfUtc ?? DateTime.UtcNow;

        // Pull effective rows for the (Item, Site?) combination at the as-of point.
        // We pull BOTH Item-level (SiteId IS NULL) AND per-Site rows in one query
        // so we can resolve the cascade in memory without N+1.
        var effective = await _db.ItemStandardCostElements.AsNoTracking()
            .Where(c => c.ItemId == itemId && c.IsActive)
            .Where(c => c.SiteId == null || c.SiteId == siteId)
            .Where(c => c.EffectiveFromUtc <= asOf)
            .Where(c => c.EffectiveToUtc == null || c.EffectiveToUtc > asOf)
            .ToListAsync(ct);

        var src = new Dictionary<CostElementType, string>();
        decimal Resolve(CostElementType type)
        {
            // Per-Site override wins if present.
            if (siteId.HasValue)
            {
                var siteRow = effective
                    .Where(c => c.SiteId == siteId.Value && c.ElementType == type)
                    .OrderByDescending(c => c.EffectiveFromUtc)
                    .FirstOrDefault();
                if (siteRow != null)
                {
                    src[type] = "ItemSite";
                    return siteRow.Amount;
                }
            }
            var itemRow = effective
                .Where(c => c.SiteId == null && c.ElementType == type)
                .OrderByDescending(c => c.EffectiveFromUtc)
                .FirstOrDefault();
            if (itemRow != null)
            {
                src[type] = "Item";
                return itemRow.Amount;
            }
            src[type] = "default";
            return 0m;
        }

        var material = Resolve(CostElementType.Material);
        var labor = Resolve(CostElementType.Labor);
        var varOh = Resolve(CostElementType.VariableOverhead);
        var fixOh = Resolve(CostElementType.FixedOverhead);
        var subcon = Resolve(CostElementType.Subcontract);
        var setup = Resolve(CostElementType.Setup);
        var tooling = Resolve(CostElementType.Tooling);
        var other = Resolve(CostElementType.Other);

        // Currency: take from any active row; default USD if no rows exist.
        var currency = effective.FirstOrDefault()?.CurrencyCode ?? "USD";

        return new ItemCostBreakdown(
            ItemId: itemId,
            SiteId: siteId,
            AsOfUtc: asOf,
            CurrencyCode: currency,
            Material: material,
            Labor: labor,
            VariableOverhead: varOh,
            FixedOverhead: fixOh,
            Subcontract: subcon,
            Setup: setup,
            Tooling: tooling,
            Other: other,
            Total: material + labor + varOh + fixOh + subcon + setup + tooling + other,
            OverrideSource: src);
    }

    public async Task<ItemStandardCostElement> SetCostElementAsync(
        int itemId,
        int? siteId,
        CostElementType elementType,
        decimal amount,
        CostElementSource source,
        string? calculationNotes,
        string? createdBy,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Find currently-effective row for this (Item, Site, ElementType).
        var current = await _db.ItemStandardCostElements
            .Where(c => c.ItemId == itemId
                     && c.SiteId == siteId
                     && c.ElementType == elementType
                     && c.IsActive
                     && c.EffectiveFromUtc <= now
                     && (c.EffectiveToUtc == null || c.EffectiveToUtc > now))
            .OrderByDescending(c => c.EffectiveFromUtc)
            .FirstOrDefaultAsync(ct);

        // Idempotency: if the current row already has the requested amount + source,
        // return it without writing. (Note: minor floating-point comparison — use exact
        // decimal equality since amounts are decimal(18,4).)
        if (current != null && current.Amount == amount && current.Source == source)
        {
            _logger.LogInformation(
                "ItemStandardCostService: SetCostElement no-op for Item={ItemId} Site={SiteId} Type={Type} (amount + source unchanged at {Amount} {Source}).",
                itemId, siteId, elementType, amount, source);
            return current;
        }

        // Close the prior row (if any) by stamping EffectiveTo=now.
        if (current != null)
        {
            current.EffectiveToUtc = now;
            current.UpdatedAt = now;
            current.UpdatedBy = createdBy;
        }

        // Insert new effective-dated row.
        var newRow = new ItemStandardCostElement
        {
            ItemId = itemId,
            SiteId = siteId,
            ElementType = elementType,
            Amount = amount,
            Source = source,
            CurrencyCode = "USD",
            EffectiveFromUtc = now,
            EffectiveToUtc = null,
            IsActive = true,
            CalculationNotes = calculationNotes,
            CreatedAt = now,
            CreatedBy = createdBy,
        };
        _db.ItemStandardCostElements.Add(newRow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ItemStandardCostService: SetCostElement Item={ItemId} Site={SiteId} Type={Type} Amount={Amount} Source={Source} — new effective row Id={NewId}, prior row Id={PriorId} closed.",
            itemId, siteId, elementType, amount, source, newRow.Id, current?.Id);

        return newRow;
    }

    public async Task<IReadOnlyList<ItemStandardCostElement>> GetHistoryAsync(
        int itemId,
        int? siteId,
        CancellationToken ct)
    {
        return await _db.ItemStandardCostElements.AsNoTracking()
            .Where(c => c.ItemId == itemId
                     && (siteId == null
                         ? (c.SiteId == null)
                         : (c.SiteId == siteId || c.SiteId == null)))
            .OrderBy(c => c.ElementType)
            .ThenByDescending(c => c.EffectiveFromUtc)
            .ToListAsync(ct);
    }
}
