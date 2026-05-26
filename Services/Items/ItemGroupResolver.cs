// B6 Foundation Sprint PR-FS-1 (2026-05-26) — ItemGroupResolver impl.
// HOTFIX PR-FS-1.5.1 (2026-05-26) — Source-aware classification.
//
// Pure read-side lookup. No writes. AppDbContext-only injection (no tenant
// context — system ItemGroups are tenant-agnostic by design).
//
// The convention map dispatch is now Source-aware: Part / Kit branch on
// `ItemMasterSource` (External / Synced → RAW; Internal → SUBASSY-with-
// warning). All other ItemTypes ignore Source. See IItemGroupResolver.cs
// for the full convention table.

using System;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Items;

public sealed class ItemGroupResolver : IItemGroupResolver
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItemGroupResolver> _logger;

    public ItemGroupResolver(AppDbContext db, ILogger<ItemGroupResolver> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the (Id, Code) for an ItemGroup by its Id. Used by the
    /// /Admin/ItemMasterExpansionProbe to display the resolved code without
    /// the page model needing direct DbContext access. Returns (null, null)
    /// if no match.
    /// </summary>
    public async Task<(int? Id, string? Code)> GetByIdAsync(int? itemGroupId, CancellationToken ct)
    {
        if (!itemGroupId.HasValue) return (null, null);
        var hit = await _db.Set<Abs.FixedAssets.Models.Masters.ItemGroup>().AsNoTracking()
            .Where(g => g.Id == itemGroupId.Value)
            .Select(g => new { g.Id, g.Code })
            .FirstOrDefaultAsync(ct);
        return hit == null ? (null, null) : (hit.Id, hit.Code);
    }

    public async Task<int?> ResolveDefaultForItemAsync(Item item, CancellationToken ct)
    {
        // PR-FS-7 tightening: when Part/Kit + Internal + IsSellable=TRUE,
        // route to FG (truly sellable internal item). Otherwise fall through
        // to the Source-aware convention map (PR-FS-1.5.1).
        if ((item.Type == ItemType.Part || item.Type == ItemType.Kit)
            && item.Source == ItemMasterSource.Internal
            && item.IsSellable)
        {
            _logger.LogInformation(
                "ItemGroupResolver: Part/Kit + Internal + IsSellable=TRUE → FG default (PR-FS-7 tightening). Item={ItemId} PartNumber={PN}.",
                item.Id, item.PartNumber);
            var fg = await ResolveByCodeAsync("FG", ct);
            if (fg.HasValue) return fg;
            _logger.LogWarning(
                "ItemGroupResolver: FG ItemGroup not found; falling back to Source-aware convention map.");
        }
        return await ResolveDefaultForItemAsync(item.Type, item.Source, ct);
    }

    public async Task<int?> ResolveDefaultForItemAsync(ItemType itemType, ItemMasterSource source, CancellationToken ct)
    {
        var code = MapToItemGroupCode(itemType, source);

        // Provisional-classification warning: Internal Part / Kit defaults to
        // SUBASSY pending PR-FS-7's IsSellable signal. If the Item is actually
        // a top-level sellable FG, the caller MUST supply an explicit
        // ItemGroupId at create time (ItemMasterService rejects null-on-create
        // for Source=Internal — this code path is only hit by backfill and
        // bulk import seeders).
        if ((itemType == ItemType.Part || itemType == ItemType.Kit)
            && source == ItemMasterSource.Internal)
        {
            _logger.LogWarning(
                "ItemGroupResolver: provisional classification — Item.Type={ItemType} + Source=Internal " +
                "defaulted to '{Code}'. If this is a parent-level SELLABLE item, set ItemGroupId=FG " +
                "explicitly. PR-FS-7's IsSellable signal will tighten this default.",
                itemType, code);
        }

        var id = await ResolveByCodeAsync(code, ct);
        if (!id.HasValue)
        {
            _logger.LogWarning(
                "ItemGroupResolver: no system ItemGroup found for Item.Type={ItemType}, Source={Source} → " +
                "expected Code='{Code}'. Falling back to null — caller must require explicit ItemGroupId.",
                itemType, source, code);
        }
        return id;
    }

    public async Task<int?> ResolveByCodeAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var hit = await _db.Set<Abs.FixedAssets.Models.Masters.ItemGroup>().AsNoTracking()
            .Where(g => g.IsActive && g.IsSystem)
            .Where(g => g.Code.ToLower() == code.ToLower())
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(ct);

        return hit;
    }

    // Convention map — kept in code (not config) because the SYSTEM ItemGroup
    // codes are stable seed templates (PRA-7, see Models/Masters/ItemGroup.cs).
    // When new SYSTEM codes are added, extend this method.
    //
    // PR-FS-1.5.1 hotfix: Part / Kit branch on ItemMasterSource for tier-1
    // ERP semantics. FG = parent-sellable only. Purchased parts → RAW.
    // Internal items → SUBASSY (until PR-FS-7's IsSellable lands).
    private static string MapToItemGroupCode(ItemType itemType, ItemMasterSource source) =>
        itemType switch
        {
            ItemType.Part or ItemType.Kit => source switch
            {
                ItemMasterSource.ExternalERP => "RAW",
                ItemMasterSource.Synced      => "RAW",
                ItemMasterSource.Internal    => "SUBASSY",
                _                            => "RAW",
            },
            ItemType.Consumable  => "CONSUMABLE",
            ItemType.Tool        => "TOOLING",
            ItemType.Safety      => "CONSUMABLE",
            ItemType.Lubricant   => "CONSUMABLE",
            ItemType.Chemical    => "CONSUMABLE",
            ItemType.Electrical  => "SPAREPART",
            ItemType.Mechanical  => "SPAREPART",
            ItemType.Hydraulic   => "SPAREPART",
            ItemType.Pneumatic   => "SPAREPART",
            ItemType.Filter      => "CONSUMABLE",
            ItemType.Bearing     => "SPAREPART",
            ItemType.Belt        => "CONSUMABLE",
            ItemType.Seal        => "CONSUMABLE",
            ItemType.Fastener    => "RAW",
            ItemType.Service     => "SERVICE",
            _                    => "RAW",     // safer default than FG (PR-FS-1's bug)
        };
}
