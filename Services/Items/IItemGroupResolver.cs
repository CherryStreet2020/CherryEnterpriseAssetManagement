// B6 Foundation Sprint PR-FS-1 (2026-05-26) — IItemGroupResolver.
// HOTFIX PR-FS-1.5.1 (2026-05-26) — Source-aware classification.
//
// Helper for callers that need to resolve a default ItemGroup from an
// (ItemType, ItemMasterSource) combination. Used by:
//   - Seeders that bulk-import Items from external sources and don't
//     supply ItemGroupId explicitly (ItemGroupBackfillSeeder, future bulk
//     importers).
//   - Future MTO/ETO release flows that need to assert "the right group"
//     before creating ProductionOrders.
//
// PR-FS-1.5.1 fixed the semantic bug in the original PR-FS-1 convention map:
//   - OLD (wrong): `Part / Kit → FG` regardless of provenance. This
//     incorrectly classified all 151 pre-PR-FS-1 dev Items (all purchased
//     parts) as Finished Goods.
//   - NEW (correct): tier-1 ERP semantics — FG is reserved for parent-level
//     sellable items only. Purchased parts (Source=ExternalERP / Synced)
//     default to RAW. Internal items default to SUBASSY (sub-assembly)
//     until PR-FS-7 lands `Item.IsSellable` for accurate FG detection.
//
// New convention table — (ItemType, ItemMasterSource → ItemGroup.Code):
//   Part / Kit | ExternalERP / Synced → RAW          (purchased part default)
//   Part / Kit | Internal             → SUBASSY      (sub-assembly placeholder)
//   Consumable | any                   → CONSUMABLE
//   Tool       | any                   → TOOLING
//   Safety / Lubricant / Chemical / Filter / Belt / Seal | any → CONSUMABLE
//   Electrical / Mechanical / Hydraulic / Pneumatic / Bearing | any → SPAREPART
//   Fastener   | any                   → RAW
//   Service    | any                   → SERVICE
//
// Per Lock 15 — IService surface only, never direct DbContext usage from
// callers.
//
// References:
//   - feedback memory: feedback_item_group_classification_principle.md
//   - feedback memory: feedback_b6_go_big_2026_05_26.md
//   - project memory: project_pr356_semantic_bug_found.md

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Items;

public interface IItemGroupResolver
{
    /// <summary>
    /// Resolve the default <c>ItemGroup.Id</c> for the supplied
    /// <paramref name="itemType"/> and <paramref name="source"/> combination
    /// by looking up the system template <c>ItemGroup</c> with the
    /// convention-matched <c>Code</c>. Returns <c>null</c> if no system
    /// ItemGroup matches (in which case the caller must surface an error
    /// and require explicit classification).
    ///
    /// Source-aware dispatch:
    ///   - <c>Part / Kit</c> + <c>Source=ExternalERP / Synced</c> → <c>RAW</c>
    ///     (purchased part default — SAP <c>ROH</c> equivalent).
    ///   - <c>Part / Kit</c> + <c>Source=Internal</c> → <c>SUBASSY</c>
    ///     (sub-assembly placeholder — SAP <c>HALB</c> equivalent). Logs a
    ///     warning indicating this is a provisional classification pending
    ///     PR-FS-7's <c>IsSellable</c> signal.
    ///   - All other <c>ItemType</c>s ignore <c>source</c> and route by
    ///     <c>ItemType</c> alone (Tool→TOOLING, Fastener→RAW, etc.).
    /// </summary>
    Task<int?> ResolveDefaultForItemAsync(ItemType itemType, ItemMasterSource source, CancellationToken ct);

    /// <summary>
    /// PR-FS-7 (2026-05-26) — preferred overload that consults <c>Item.IsSellable</c>
    /// to TIGHTEN the Part+Internal default. Resolves:
    ///   - Part / Kit + Internal + IsSellable=TRUE  → FG (truly sellable internal item)
    ///   - Part / Kit + Internal + IsSellable=FALSE → SUBASSY (sub-assembly default)
    ///   - All other (Type, Source) combinations route per the existing convention
    ///     map (Source-aware dispatch from PR-FS-1.5.1).
    ///
    /// Closes the loop on the PR-FS-1.5.1 hotfix lesson: the resolver originally
    /// defaulted Part+Internal → SUBASSY pending PR-FS-7's IsSellable signal. With
    /// this overload, the signal is wired and FG-as-default is correctly gated on
    /// the explicit sellable flag.
    /// </summary>
    Task<int?> ResolveDefaultForItemAsync(Item item, CancellationToken ct);

    /// <summary>
    /// Resolve a specific <c>ItemGroup.Id</c> by its <c>Code</c> (e.g.
    /// "RAW", "FG", "CONSUMABLE", "SUBASSY"). Returns <c>null</c> if no
    /// ItemGroup matches. Lookup is case-insensitive.
    /// </summary>
    Task<int?> ResolveByCodeAsync(string code, CancellationToken ct);

    /// <summary>
    /// Project (Id, Code) for an ItemGroup by Id. Used by admin probes that
    /// need to display the resolved code without direct DbContext access
    /// (CHERRY025 compliance).
    /// </summary>
    Task<(int? Id, string? Code)> GetByIdAsync(int? itemGroupId, CancellationToken ct);
}
