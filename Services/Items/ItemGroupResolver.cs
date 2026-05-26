// B6 Foundation Sprint PR-FS-1 (2026-05-26) — ItemGroupResolver impl.
//
// Pure read-side lookup. No writes. AppDbContext-only injection (no tenant
// context — system ItemGroups are tenant-agnostic by design).

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

    public async Task<int?> ResolveDefaultForItemTypeAsync(ItemType itemType, CancellationToken ct)
    {
        var code = MapItemTypeToItemGroupCode(itemType);
        var id = await ResolveByCodeAsync(code, ct);
        if (!id.HasValue)
        {
            _logger.LogWarning(
                "ItemGroupResolver: no system ItemGroup found for Item.Type={ItemType} → expected Code='{Code}'. " +
                "Falling back to null — caller must require explicit ItemGroupId.",
                itemType, code);
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
    private static string MapItemTypeToItemGroupCode(ItemType itemType) =>
        itemType switch
        {
            ItemType.Part        => "FG",         // finished goods default
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
            ItemType.Kit         => "FG",
            ItemType.Service     => "SERVICE",
            _                    => "FG"
        };
}
