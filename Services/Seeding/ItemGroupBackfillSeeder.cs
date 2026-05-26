// B6 Foundation Sprint PR-FS-1.5 (2026-05-26) — ItemGroupBackfillSeeder impl.
// HOTFIX PR-FS-1.5.1 (2026-05-26) — Reclassify mode + Source-aware resolver.
//
// Service-layer backfill that bulk-classifies Items via the Source-aware
// IItemGroupResolver convention map. Pure data fix — no schema changes.
//
// See IItemGroupBackfillSeeder.cs for full mode semantics.

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

    public Task<ItemGroupBackfillResult> BackfillAsync(CancellationToken ct) =>
        BackfillAsync(ItemGroupBackfillMode.FillNullsOnly, ct);

    public async Task<ItemGroupBackfillResult> BackfillAsync(ItemGroupBackfillMode mode, CancellationToken ct)
    {
        var warnings = new List<string>();
        var totalScanned = await _db.Items.AsNoTracking().CountAsync(ct);

        // Cache: ItemGroupId → Code for the per-bucket count reporting + before-name lookup.
        var codeById = await _db.Set<ItemGroup>().AsNoTracking()
            .Where(g => g.IsSystem)
            .ToDictionaryAsync(g => g.Id, g => g.Code, ct);

        return mode switch
        {
            ItemGroupBackfillMode.FillNullsOnly => await FillNullsAsync(totalScanned, codeById, warnings, ct),
            ItemGroupBackfillMode.Reclassify    => await ReclassifyAsync(totalScanned, codeById, warnings, ct),
            _ => throw new System.ArgumentOutOfRangeException(nameof(mode), mode, "Unknown ItemGroupBackfillMode"),
        };
    }

    private async Task<ItemGroupBackfillResult> FillNullsAsync(
        int totalScanned,
        IReadOnlyDictionary<int, string> codeById,
        List<string> warnings,
        CancellationToken ct)
    {
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
                Warnings: warnings,
                ReclassifyChanges: new List<ItemGroupReclassifyChange>(),
                ItemsReclassified: 0,
                ItemsUnchanged: alreadyClassifiedCount,
                Mode: ItemGroupBackfillMode.FillNullsOnly);
        }

        var perGroup = new Dictionary<string, int>();
        var skipped = new List<int>();

        foreach (var item in unclassified)
        {
            var groupId = await _resolver.ResolveDefaultForItemAsync(item.Type, item.Source, ct);
            if (!groupId.HasValue)
            {
                skipped.Add(item.Id);
                _logger.LogWarning(
                    "ItemGroupBackfillSeeder: skipped Item {ItemId} ({PartNumber}, Type={Type}, Source={Source}) — no convention-matched ItemGroup. Classify manually.",
                    item.Id, item.PartNumber, item.Type, item.Source);
                continue;
            }

            item.ItemGroupId = groupId.Value;

            var code = codeById.TryGetValue(groupId.Value, out var c) ? c : $"Id={groupId.Value}";
            perGroup[code] = perGroup.GetValueOrDefault(code, 0) + 1;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ItemGroupBackfillSeeder (FillNullsOnly) finished — scanned {Total}, classified {Classified}, already-classified {Already}, skipped {Skipped}.",
            totalScanned, unclassified.Count - skipped.Count, alreadyClassifiedCount, skipped.Count);

        return new ItemGroupBackfillResult(
            TotalItemsScanned: totalScanned,
            ItemsClassified: unclassified.Count - skipped.Count,
            ItemsAlreadyClassified: alreadyClassifiedCount,
            ItemsSkippedNoMapping: skipped.Count,
            PerItemGroupClassified: perGroup,
            SkippedItemIds: skipped,
            Warnings: warnings,
            ReclassifyChanges: new List<ItemGroupReclassifyChange>(),
            ItemsReclassified: 0,
            ItemsUnchanged: alreadyClassifiedCount,
            Mode: ItemGroupBackfillMode.FillNullsOnly);
    }

    private async Task<ItemGroupBackfillResult> ReclassifyAsync(
        int totalScanned,
        IReadOnlyDictionary<int, string> codeById,
        List<string> warnings,
        CancellationToken ct)
    {
        // PR-FS-1.5.1 — walk every Item, re-resolve, update only on change.
        // Tracked load so EF tracks the per-row mutations.
        var allItems = await _db.Items.ToListAsync(ct);

        var perGroup = new Dictionary<string, int>();
        var skipped = new List<int>();
        var changes = new List<ItemGroupReclassifyChange>();
        var classified = 0;
        var unchanged = 0;

        foreach (var item in allItems)
        {
            var resolvedId = await _resolver.ResolveDefaultForItemAsync(item.Type, item.Source, ct);
            if (!resolvedId.HasValue)
            {
                skipped.Add(item.Id);
                _logger.LogWarning(
                    "ItemGroupBackfillSeeder (Reclassify): skipped Item {ItemId} ({PartNumber}, Type={Type}, Source={Source}) — no convention-matched ItemGroup.",
                    item.Id, item.PartNumber, item.Type, item.Source);
                continue;
            }

            // Already-correct → leave alone.
            if (item.ItemGroupId == resolvedId.Value)
            {
                unchanged++;
                continue;
            }

            // Mismatch → record before/after, update.
            var fromCode = item.ItemGroupId.HasValue
                && codeById.TryGetValue(item.ItemGroupId.Value, out var fc) ? fc : null;
            var toCode = codeById.TryGetValue(resolvedId.Value, out var tc) ? tc : $"Id={resolvedId.Value}";

            changes.Add(new ItemGroupReclassifyChange(
                ItemId: item.Id,
                PartNumber: item.PartNumber,
                FromCode: fromCode,
                ToCode: toCode));

            item.ItemGroupId = resolvedId.Value;
            classified++;
            perGroup[toCode] = perGroup.GetValueOrDefault(toCode, 0) + 1;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "ItemGroupBackfillSeeder (Reclassify) finished — scanned {Total}, reclassified {Reclassified}, unchanged {Unchanged}, skipped {Skipped}.",
            totalScanned, classified, unchanged, skipped.Count);

        return new ItemGroupBackfillResult(
            TotalItemsScanned: totalScanned,
            ItemsClassified: classified,
            ItemsAlreadyClassified: unchanged,
            ItemsSkippedNoMapping: skipped.Count,
            PerItemGroupClassified: perGroup,
            SkippedItemIds: skipped,
            Warnings: warnings,
            ReclassifyChanges: changes,
            ItemsReclassified: classified,
            ItemsUnchanged: unchanged,
            Mode: ItemGroupBackfillMode.Reclassify);
    }
}
