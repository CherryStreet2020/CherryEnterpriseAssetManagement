// B6 Foundation Sprint PR-FS-1.5 (2026-05-26) — IItemGroupBackfillSeeder.
//
// Idempotent service-driven backfill of `Item.ItemGroupId` for the 151
// existing dev Items that pre-date PR-FS-1's gate. Uses IItemGroupResolver
// to map (Item.Type → conventional ItemGroup Code → Id) per the convention
// table established in PR-FS-1.
//
// IDEMPOTENCY: only touches Items where ItemGroupId IS NULL. Re-running
// the backfill on already-classified Items is a no-op.
//
// SAFETY: skips Items whose Type has no convention-matched ItemGroup
// (resolver returns null) — operator must classify those manually before
// the next sweep. Surfaces a per-ItemGroup count + a list of skipped Item
// Ids in the result.
//
// LOCK 14 — dev-only seeder. Republish-with-Copy syncs to prod at end of
// sprint window. NEVER run against prod directly.
//
// LOCK 15 — service-layer-only writes via AppDbContext. No raw SQL, no
// admin-page direct DbContext mutations.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abs.FixedAssets.Services.Seeding;

public interface IItemGroupBackfillSeeder
{
    /// <summary>
    /// Backfill <c>Item.ItemGroupId</c> for every Item where it is currently
    /// NULL. Items are mapped to a default ItemGroup via
    /// <c>IItemGroupResolver.ResolveDefaultForItemTypeAsync</c>. Idempotent —
    /// re-running on already-classified Items is a no-op.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Result envelope with per-ItemGroup counts (Code → number of Items
    /// classified) + a list of skipped Item Ids (Items whose Type had no
    /// conventional ItemGroup mapping).
    /// </returns>
    Task<ItemGroupBackfillResult> BackfillAsync(CancellationToken ct);
}

/// <summary>
/// Result surfaced to the admin trigger page.
/// </summary>
public sealed record ItemGroupBackfillResult(
    int TotalItemsScanned,
    int ItemsClassified,
    int ItemsAlreadyClassified,
    int ItemsSkippedNoMapping,
    IReadOnlyDictionary<string, int> PerItemGroupClassified,
    IReadOnlyList<int> SkippedItemIds,
    IReadOnlyList<string> Warnings);
