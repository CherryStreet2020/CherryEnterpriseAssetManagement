// B6 Foundation Sprint PR-FS-1.5 (2026-05-26) — IItemGroupBackfillSeeder.
// HOTFIX PR-FS-1.5.1 (2026-05-26) — Reclassify mode + Source-aware resolver.
//
// Idempotent service-driven backfill of `Item.ItemGroupId`. Two modes:
//
//   1. FillNullsOnly (default) — only touches Items where ItemGroupId IS
//      NULL. Used for incremental backfill of newly-imported Items that
//      didn't supply ItemGroupId. Re-running is a no-op.
//
//   2. Reclassify — walks every Item (regardless of current ItemGroupId)
//      and re-resolves classification via the latest convention map.
//      Updates only Items whose resolved group differs from current.
//      Used for the PR-FS-1.5.1 hotfix sweep: after IItemSourceBackfillSeeder
//      flips legacy Source=Internal Items to Source=ExternalERP, the
//      Reclassify mode picks them up and moves them from FG → RAW per the
//      new Source-aware convention.
//
// IDEMPOTENCY:
//   - FillNullsOnly: only touches NULL rows. Second run is a no-op.
//   - Reclassify: only writes Items whose resolved group changed. Second
//     run (with no upstream changes) is a no-op.
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

/// <summary>
/// Mode flag for <see cref="IItemGroupBackfillSeeder.BackfillAsync"/>.
/// </summary>
public enum ItemGroupBackfillMode
{
    /// <summary>
    /// Touch only Items where <c>ItemGroupId IS NULL</c>. Default behavior.
    /// </summary>
    FillNullsOnly = 0,

    /// <summary>
    /// Walk every Item and re-resolve classification per the latest
    /// convention map. Update Items whose resolved group differs from
    /// their current <c>ItemGroupId</c>. Used by the PR-FS-1.5.1 hotfix
    /// sweep.
    /// </summary>
    Reclassify = 1,
}

public interface IItemGroupBackfillSeeder
{
    /// <summary>
    /// Backfill or reclassify <c>Item.ItemGroupId</c> values per the
    /// supplied <paramref name="mode"/>. Items are mapped to a default
    /// ItemGroup via
    /// <c>IItemGroupResolver.ResolveDefaultForItemAsync(Type, Source)</c>.
    /// Idempotent — re-running with the same upstream state is a no-op.
    /// </summary>
    /// <param name="mode">
    /// <see cref="ItemGroupBackfillMode.FillNullsOnly"/> (default) only
    /// touches NULL rows. <see cref="ItemGroupBackfillMode.Reclassify"/>
    /// walks every Item and updates only the ones whose resolved group
    /// changed.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Result envelope with per-ItemGroup counts (Code → number of Items
    /// classified) + a list of skipped Item Ids (Items whose Type had no
    /// conventional ItemGroup mapping) + a list of before-after
    /// reclassification deltas when <paramref name="mode"/> is
    /// <see cref="ItemGroupBackfillMode.Reclassify"/>.
    /// </returns>
    Task<ItemGroupBackfillResult> BackfillAsync(ItemGroupBackfillMode mode, CancellationToken ct);

    /// <summary>
    /// Convenience overload — equivalent to <c>BackfillAsync(FillNullsOnly, ct)</c>.
    /// </summary>
    Task<ItemGroupBackfillResult> BackfillAsync(CancellationToken ct);
}

/// <summary>
/// Single before/after change captured during a reclassify sweep.
/// </summary>
public sealed record ItemGroupReclassifyChange(
    int ItemId,
    string PartNumber,
    string? FromCode,
    string ToCode);

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
    IReadOnlyList<string> Warnings,
    // PR-FS-1.5.1 — reclassify-mode deltas. Empty for FillNullsOnly mode.
    IReadOnlyList<ItemGroupReclassifyChange> ReclassifyChanges,
    int ItemsReclassified,
    int ItemsUnchanged,
    ItemGroupBackfillMode Mode);
