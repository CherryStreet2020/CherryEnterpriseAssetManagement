// B6 Foundation Sprint PR-FS-1.5 (2026-05-26) — ItemGroupBackfillSeeder impl.
//
// Service-layer backfill that bulk-classifies pre-PR-FS-1 Items via
// IItemGroupResolver convention map. Pure data fix — no schema changes.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Abs.FixedAssets.Services.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Seeding;

public sealed class ItemGroupBackfillSeeder : IItemGroupBackfillSeeder
{
    private readonly AppDbContext _db;
    private readonly IItemGroupResolver _resolver;
    private readonly ILogger<ItemGroupBackfillSeeder> _logger;

    public ItemGroupBackfillSeeder(
        AppDbContext db,
        IItemGroupResolver resolver,
        ILogger<ItemGroupBackfillSeeder> logger)
    {
        _db = db;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<ItemGroupBackfillResult> BackfillAsync(CancellationToken ct)
    {
        var warnings = new List<string>();
        var totalScanned = await _db.Items.AsNoTracking().CountAsync(ct);

        // Walk Items where ItemGroupId is NULL (the unclassified ones).
        // Tracked load — we mutate them in-place.
        var unclassified = await _db.Items
            .Where(i => i.ItemGroupId == null)
            .ToListAsync(ct);

        var alreadyClassifiedCount = totalScanned - unclassified.Count;
        if (unclassified.Count == 0)
        {
            return new ItemGroupBackfillResult(
                TotalItemsScanned: totalScanned,
                ItemsClassified: 0,
                ItemsAlreadyClassified: alreadyClassifiedCount,
                ItemsSkippedNoMapping: 0,
                PerItemGroupClassified: new Dictionary<string, int>(),
                SkippedItemIds: new List<int>(),
                Warnings: warnings);
        }

        // Cache: ItemGroupId → Code for the per-bucket count reporting.
        var codeById = await _db.Set<ItemGroup>().AsNoTracking()
            .Where(g => g.IsSystem)
            .ToDictionaryAsync(g => g.Id, g => g.Code, ct);

        var perGroup = new Dictionary<string, int>();
        var skipped = new List<int>();

        foreach (var item in unclassified)
        {
            var groupId = await _resolver.ResolveDefaultForItemTypeAsync(item.Type, ct);
            if (!groupId.HasValue)
            {
                skipped.Add(item.Id);
                _logger.LogWarning(
                    "ItemGroupBackfillSeeder: skipped Item {ItemId} ({PartNumber}, Type={Type}) — no convention-matched ItemGroup. Classify manually.",
                    item.Id, item.PartNumber, item.Type);
                continue;
            }

            item.ItemGroupId = groupId.Value;

            var code = codeById.TryGetValue(groupId.Value, out var c) ? c : $"Id={groupId.Value}";
            perGroup[code] = perGroup.GetValueOrDefault(code, 0) + 1;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ItemGroupBackfillSeeder finished — scanned {Total}, classified {Classified}, already-classified {Already}, skipped {Skipped}.",
            totalScanned, unclassified.Count - skipped.Count, alreadyClassifiedCount, skipped.Count);

        return new ItemGroupBackfillResult(
            TotalItemsScanned: totalScanned,
            ItemsClassified: unclassified.Count - skipped.Count,
            ItemsAlreadyClassified: alreadyClassifiedCount,
            ItemsSkippedNoMapping: skipped.Count,
            PerItemGroupClassified: perGroup,
            SkippedItemIds: skipped,
            Warnings: warnings);
    }
}
