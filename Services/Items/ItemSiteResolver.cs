// B6 Foundation Sprint PR-FS-2 (2026-05-26) — ItemSiteResolver impl.
//
// Pure read-side cascade. No writes. AppDbContext-only injection (the
// resolver assumes the caller has already authorized scope via
// ITenantContext upstream — this is a read after RLS).

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

public sealed class ItemSiteResolver : IItemSiteResolver
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItemSiteResolver> _logger;

    public ItemSiteResolver(AppDbContext db, ILogger<ItemSiteResolver> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ItemEffective?> ResolveEffectiveAsync(int itemId, int? siteId, CancellationToken ct)
    {
        var item = await _db.Items.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);
        if (item == null)
        {
            _logger.LogWarning("ItemSiteResolver: Item {ItemId} not found — returning null.", itemId);
            return null;
        }

        ItemSite? siteOverride = null;
        if (siteId.HasValue)
        {
            siteOverride = await _db.ItemSites.AsNoTracking()
                .FirstOrDefaultAsync(s => s.ItemId == itemId && s.SiteId == siteId.Value, ct);
        }

        return BuildEffective(item, siteOverride, siteId);
    }

    public async Task<ItemSite?> GetOverrideAsync(int itemId, int siteId, CancellationToken ct)
    {
        return await _db.ItemSites.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ItemId == itemId && s.SiteId == siteId, ct);
    }

    // ============================================================
    // Internal cascade builder.
    //
    // For each per-Site nullable override, return override.Value if not null,
    // else fall back to Item.Value. Audit dictionary tracks which source the
    // resolved value came from so admin UIs can highlight overrides.
    // ============================================================
    private static ItemEffective BuildEffective(Item item, ItemSite? siteOverride, int? siteId)
    {
        var src = new Dictionary<string, string>();

        T Pick<T>(string field, T? overrideVal, T itemVal) where T : struct
        {
            if (overrideVal.HasValue)
            {
                src[field] = "ItemSite";
                return overrideVal.Value;
            }
            src[field] = "Item";
            return itemVal;
        }

        T? PickNullable<T>(string field, T? overrideVal, T? itemVal) where T : struct
        {
            if (overrideVal.HasValue)
            {
                src[field] = "ItemSite";
                return overrideVal.Value;
            }
            src[field] = "Item";
            return itemVal;
        }

        string? PickString(string field, string? overrideVal, string? itemVal)
        {
            // PR-FS-2 P2 fix (Codex on PR #358): use `!= null` not `!IsNullOrEmpty`.
            // null means "no override"; empty string is a deliberate override that
            // clears the inherited value (e.g., per-Site DefaultWarehouse blank to
            // suppress Item-level legacy warehouse default).
            if (overrideVal is not null)
            {
                src[field] = "ItemSite";
                return overrideVal;
            }
            src[field] = "Item";
            return itemVal;
        }

        return new ItemEffective(
            ItemId: item.Id,
            SiteId: siteId,
            PartNumber: item.PartNumber,
            Description: item.Description,

            Status:   Pick("Status", siteOverride?.Status, item.Status),
            // IsActive cascade: AND-together — Item-level inactive ALWAYS wins, but
            // ItemSite-level inactive can suppress an Item-level active. Audit source
            // reports "ItemSite" only when the site override actively suppresses.
            IsActive: ResolveIsActive(item, siteOverride, src),
            Source:   item.Source,
            ItemGroupId: PickNullable("ItemGroupId", siteOverride?.ItemGroupId, item.ItemGroupId),

            IsStocked:        Pick("IsStocked",        siteOverride?.IsStocked,        item.IsStocked),
            IsPurchasable:    Pick("IsPurchasable",    siteOverride?.IsPurchasable,    item.IsPurchasable),
            IsCriticalSpare:  Pick("IsCriticalSpare",  siteOverride?.IsCriticalSpare,  item.IsCriticalSpare),
            StockPolicy:      Pick("StockPolicy",      siteOverride?.StockPolicy,      item.StockPolicy),
            ABCClass:         Pick("ABCClass",         siteOverride?.ABCClass,         item.ABCClass),

            MinQuantity:        Pick("MinQuantity",        siteOverride?.MinQuantity,        item.MinQuantity),
            MaxQuantity:        Pick("MaxQuantity",        siteOverride?.MaxQuantity,        item.MaxQuantity),
            ReorderPoint:       Pick("ReorderPoint",       siteOverride?.ReorderPoint,       item.ReorderPoint),
            ReorderQuantity:    Pick("ReorderQuantity",    siteOverride?.ReorderQuantity,    item.ReorderQuantity),
            SafetyStock:        Pick("SafetyStock",        siteOverride?.SafetyStock,        item.SafetyStock),
            EOQ:                PickNullable("EOQ",        siteOverride?.EOQ,                item.EOQ),
            LeadTimeDays:       Pick("LeadTimeDays",       siteOverride?.LeadTimeDays,       item.LeadTimeDays),
            ReorderMethod:      Pick("ReorderMethod",      siteOverride?.ReorderMethod,      item.ReorderMethod),
            AutoReorderEnabled: Pick("AutoReorderEnabled", siteOverride?.AutoReorderEnabled, item.AutoReorderEnabled),

            CostMethod:       Pick("CostMethod",       siteOverride?.CostMethod,       item.CostMethod),
            StandardCost:     Pick("StandardCost",     siteOverride?.StandardCost,     item.StandardCost),
            AverageCost:      Pick("AverageCost",      siteOverride?.AverageCost,      item.AverageCost),
            LastPurchaseCost: Pick("LastPurchaseCost", siteOverride?.LastPurchaseCost, item.LastPurchaseCost),
            ListPrice:        PickNullable("ListPrice", siteOverride?.ListPrice,       item.ListPrice),

            PreferredVendorId: PickNullable("PreferredVendorId", siteOverride?.PreferredVendorId, item.PrimaryVendorId),
            DefaultBuyerId:    PickNullable("DefaultBuyerId",    siteOverride?.DefaultBuyerId,    item.DefaultBuyerId),
            DefaultLocationId: PickNullable("DefaultLocationId", siteOverride?.DefaultLocationId, item.DefaultLocationId),
            DefaultWarehouse:  PickString("DefaultWarehouse",    siteOverride?.DefaultWarehouse,  item.Warehouse),
            DefaultBin:        PickString("DefaultBin",          siteOverride?.DefaultBin,        item.Bin),

            TrackingType:        Pick("TrackingType",        siteOverride?.TrackingType,        item.TrackingType),
            ShelfLifeDays:       PickNullable("ShelfLifeDays", siteOverride?.ShelfLifeDays,     item.ShelfLifeDays),
            IsHazmat:            Pick("IsHazmat",            siteOverride?.IsHazmat,            item.IsHazmat),
            StorageRequirements: PickString("StorageRequirements", siteOverride?.StorageRequirements, item.StorageRequirements),

            OverrideSource: src);
    }

    private static bool ResolveIsActive(Item item, ItemSite? siteOverride, Dictionary<string, string> src)
    {
        // Item.IsActive=false → Inactive (source: Item)
        // Item.IsActive=true + no override → Active (source: Item)
        // Item.IsActive=true + override.IsActive=true → Active (source: Item — override didn't change anything)
        // Item.IsActive=true + override.IsActive=false → Inactive (source: ItemSite — override suppressed)
        if (!item.IsActive)
        {
            src["IsActive"] = "Item";
            return false;
        }
        if (siteOverride != null && !siteOverride.IsActive)
        {
            src["IsActive"] = "ItemSite";
            return false;
        }
        src["IsActive"] = "Item";
        return true;
    }
}
