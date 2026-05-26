// B6 Foundation Sprint PR-FS-2 (2026-05-26) — IItemSiteResolver.
//
// Resolves effective Item attributes for a given (Item, Site) combination
// by cascading through ItemSite → Item → null. SAP MARC equivalent
// resolution pattern.
//
// Cascade order at runtime:
//   1. ItemSite.X       (per-Site override, if any field is set)
//   2. Item.X           (Item Master default)
//   3. null / default   (caller-supplied fallback)
//
// Used by:
//   - MRP / planning services that need per-Site reorder points + lead times
//   - Cost rollup that needs per-Site standard cost (transfer pricing)
//   - Procurement that needs per-Site preferred vendor + buyer
//   - Warehouse defaulting at receive time (DefaultLocation per Site)
//   - GL posting (PostingProfile key includes Site implicitly via Warehouse)
//
// Per Lock 15 — IService surface only, never direct DbContext usage from
// callers.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;

namespace Abs.FixedAssets.Services.Items;

public interface IItemSiteResolver
{
    /// <summary>
    /// Resolve the effective Item attributes for the supplied (itemId, siteId)
    /// combination by cascading through ItemSite → Item. Returns null if the
    /// Item itself doesn't exist.
    /// </summary>
    /// <remarks>
    /// If <paramref name="siteId"/> is null, the cascade returns Item-level
    /// values without consulting any ItemSite row. This makes the resolver
    /// safe to call from contexts where Site isn't known yet.
    /// </remarks>
    Task<ItemEffective?> ResolveEffectiveAsync(int itemId, int? siteId, CancellationToken ct);

    /// <summary>
    /// Get the raw <c>ItemSite</c> override row for a given (Item, Site)
    /// combination, or <c>null</c> if no override exists. Useful for admin
    /// UIs that want to display "Item default vs Site override" side by side.
    /// </summary>
    Task<ItemSite?> GetOverrideAsync(int itemId, int siteId, CancellationToken ct);
}

/// <summary>
/// Resolved/cascaded Item attributes for a given (itemId, siteId). Every
/// field returns the per-Site override if present, else the Item default.
/// Audit fields (OverrideSource) tell the caller WHERE each value came from.
/// </summary>
public sealed record ItemEffective(
    int ItemId,
    int? SiteId,
    string PartNumber,
    string Description,

    // Status
    ItemStatus Status,
    bool IsActive,
    ItemMasterSource Source,
    int? ItemGroupId,

    // Procurement
    bool IsStocked,
    bool IsPurchasable,
    bool IsCriticalSpare,
    StockPolicy StockPolicy,
    ABCClassification ABCClass,

    // Levels
    decimal MinQuantity,
    decimal MaxQuantity,
    decimal ReorderPoint,
    decimal ReorderQuantity,
    decimal SafetyStock,
    decimal? EOQ,
    int LeadTimeDays,
    ReorderMethod ReorderMethod,
    bool AutoReorderEnabled,

    // Costing
    CostMethod CostMethod,
    decimal StandardCost,
    decimal AverageCost,
    decimal LastPurchaseCost,
    decimal? ListPrice,

    // Sourcing
    int? PreferredVendorId,
    int? DefaultBuyerId,
    int? DefaultLocationId,
    string? DefaultWarehouse,
    string? DefaultBin,

    // Tracking / substance
    TrackingType TrackingType,
    int? ShelfLifeDays,
    bool IsHazmat,
    string? StorageRequirements,

    // Audit — every field annotated with its source: "Item" or "ItemSite" or "default".
    IReadOnlyDictionary<string, string> OverrideSource);
