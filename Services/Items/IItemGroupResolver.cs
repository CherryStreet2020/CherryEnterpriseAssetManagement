// B6 Foundation Sprint PR-FS-1 (2026-05-26) — IItemGroupResolver.
//
// Helper for callers that need to resolve a default ItemGroup from a
// (tenant, ItemType) combination. Used by:
//   - Future seeders that bulk-import Items from external sources and don't
//     supply ItemGroupId explicitly
//   - The backfill seeder PR (sibling to this one) that retroactively
//     classifies the 151 existing Items on dev
//   - Future MTO/ETO release flows that need to assert "the right group"
//     before creating ProductionOrders
//
// Lookup strategy: map Item.Type → default ItemGroup Code → ItemGroupId.
// All ItemGroups are SYSTEM templates (CompanyId NULL) so the resolution
// works across every tenant without per-tenant seeding.
//
// Map (Item.Type → ItemGroup.Code):
//   Part        → FG          (finished goods default; can be overridden)
//   Consumable  → CONSUMABLE
//   Tool        → TOOLING
//   Safety      → CONSUMABLE
//   Lubricant   → CONSUMABLE
//   Chemical    → CONSUMABLE
//   Electrical  → SPAREPART
//   Mechanical  → SPAREPART
//   Hydraulic   → SPAREPART
//   Pneumatic   → SPAREPART
//   Filter      → CONSUMABLE
//   Bearing     → SPAREPART
//   Belt        → CONSUMABLE
//   Seal        → CONSUMABLE
//   Fastener    → RAW
//   Kit         → FG
//   Service     → SERVICE
//
// Per Lock 15 — IService surface only, never direct DbContext usage from
// callers.

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Items;

public interface IItemGroupResolver
{
    /// <summary>
    /// Resolve the default <c>ItemGroup.Id</c> for the supplied
    /// <paramref name="itemType"/> by looking up the system template
    /// <c>ItemGroup</c> with the convention-matched <c>Code</c>. Returns
    /// <c>null</c> if no system ItemGroup matches (in which case the caller
    /// must surface an error and require explicit classification).
    /// </summary>
    Task<int?> ResolveDefaultForItemTypeAsync(ItemType itemType, CancellationToken ct);

    /// <summary>
    /// Resolve a specific <c>ItemGroup.Id</c> by its <c>Code</c> (e.g.
    /// "RAW", "FG", "CONSUMABLE"). Returns <c>null</c> if no ItemGroup
    /// matches. Lookup is case-insensitive.
    /// </summary>
    Task<int?> ResolveByCodeAsync(string code, CancellationToken ct);
}
