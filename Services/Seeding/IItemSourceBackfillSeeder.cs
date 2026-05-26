// B6 Foundation Sprint PR-FS-1.5.1 (2026-05-26) — IItemSourceBackfillSeeder.
//
// One-time data-fix seeder that flips `Item.Source` from
// `ItemMasterSource.Internal` to `ItemMasterSource.ExternalERP` for the
// legacy dev Items that were auto-defaulted to `Internal` by the enum but
// are actually purchased parts (Dean confirmed at end of session 5).
//
// PROBLEM: PR-FS-1.5 ran the ItemGroupBackfillSeeder against 151 pre-PR-FS-1
// dev Items. The original PR-FS-1 convention map defaulted Part → FG, so
// all 151 landed in FG. After the PR-FS-1.5.1 hotfix to the resolver,
// Part + Source=Internal now maps to SUBASSY, and Part + Source=ExternalERP
// maps to RAW. But the 151 legacy Items currently have Source=Internal by
// enum default — so without this seeder, the Reclassify sweep would move
// them FG → SUBASSY when the correct target is RAW.
//
// SIGNAL — bounded and self-documenting:
//   `Source = Internal` AND `ItemGroupId = FG.Id`
//
// That's the unique fingerprint of an Item hit by the PR-FS-1.5 bug:
//   - The Item was created before PR-FS-1's gate (no explicit ItemGroupId).
//   - The PR-FS-1.5 backfill classified it to FG via the buggy Part → FG
//     convention.
//   - It still has `Source=Internal` because nothing has set it yet.
//
// New Items created after PR-FS-1 by ItemMasterService cannot match this
// fingerprint: either they're truly Source=Internal AND explicitly
// classified by the operator (ItemGroupId is supplied at create — and FG is
// a valid explicit choice for true FGs), OR they're Source=ExternalERP.
// The seeder is therefore safe to run as many times as desired — idempotent
// and bounded.
//
// LOCK 14 — dev-only seeder. Republish-with-Copy syncs to prod at end of
// sprint window. NEVER run against prod directly.
//
// LOCK 15 — service-layer-only writes via AppDbContext. No raw SQL.
//
// References:
//   - feedback memory: feedback_item_group_classification_principle.md
//   - project memory: project_pr356_semantic_bug_found.md
//   - feedback memory: feedback_b6_go_big_2026_05_26.md

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abs.FixedAssets.Services.Seeding;

public interface IItemSourceBackfillSeeder
{
    /// <summary>
    /// Flip <c>Item.Source</c> from <c>Internal</c> to <c>ExternalERP</c>
    /// for every Item matching the legacy-FG-bug fingerprint
    /// (<c>Source=Internal</c> AND <c>ItemGroupId=FG.Id</c>). Idempotent
    /// — re-running on a clean DB matches zero Items.
    /// </summary>
    Task<ItemSourceBackfillResult> BackfillAsync(CancellationToken ct);
}

/// <summary>
/// Single before/after change captured during the Source flip.
/// </summary>
public sealed record ItemSourceBackfillChange(
    int ItemId,
    string PartNumber,
    string FromSource,
    string ToSource);

/// <summary>
/// Result surfaced to the admin trigger page.
/// </summary>
public sealed record ItemSourceBackfillResult(
    int TotalItemsScanned,
    int ItemsFlipped,
    int ItemsAlreadyExternal,
    int ItemsLeftInternal,
    IReadOnlyList<ItemSourceBackfillChange> Changes,
    IReadOnlyList<string> Warnings);
