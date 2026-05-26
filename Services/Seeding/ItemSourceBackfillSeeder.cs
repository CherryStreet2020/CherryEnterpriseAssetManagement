// B6 Foundation Sprint PR-FS-1.5.1 (2026-05-26) — ItemSourceBackfillSeeder impl.
//
// One-time data-fix that flips legacy `Source=Internal AND ItemGroupId=FG`
// rows to `Source=ExternalERP`. Bounded, idempotent, service-layer only.
//
// See IItemSourceBackfillSeeder.cs for full semantics + signal rationale.

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

public sealed class ItemSourceBackfillSeeder : IItemSourceBackfillSeeder
{
    private readonly AppDbContext _db;
    private readonly IItemGroupResolver _resolver;
    private readonly ILogger<ItemSourceBackfillSeeder> _logger;

    public ItemSourceBackfillSeeder(
        AppDbContext db,
        IItemGroupResolver resolver,
        ILogger<ItemSourceBackfillSeeder> logger)
    {
        _db = db;
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<ItemSourceBackfillResult> BackfillAsync(CancellationToken ct)
    {
        var warnings = new List<string>();
        var totalScanned = await _db.Items.AsNoTracking().CountAsync(ct);

        // Resolve FG.Id via the resolver — avoids hardcoded Id assumptions.
        // Pass Source=Internal so we get the FG row through the legacy code
        // path that originally caused the bug? No — that path now returns
        // SUBASSY. We want the literal FG row Id. Use ResolveByCodeAsync.
        var fgId = await _resolver.ResolveByCodeAsync("FG", ct);
        if (!fgId.HasValue)
        {
            warnings.Add("System ItemGroup 'FG' not found — cannot identify legacy-bug Items. Aborting with zero-op result.");
            _logger.LogWarning("ItemSourceBackfillSeeder: system 'FG' ItemGroup not seeded — bailing out.");
            return new ItemSourceBackfillResult(
                TotalItemsScanned: totalScanned,
                ItemsFlipped: 0,
                ItemsAlreadyExternal: 0,
                ItemsLeftInternal: 0,
                Changes: new List<ItemSourceBackfillChange>(),
                Warnings: warnings);
        }

        // The legacy-bug fingerprint:
        //   Type=Part OR Kit   (only Part/Kit had the buggy default — Tools,
        //                       Fasteners, etc. routed through their own
        //                       convention branches and never hit the FG bug)
        //   AND Source=Internal
        //   AND ItemGroupId=FG.Id
        //
        // Restricting to Type=Part|Kit (Codex P1) is what makes this seeder
        // safe even if an operator has explicitly set ItemGroupId=FG on some
        // OTHER Item type — those would not be flipped.
        var legacy = await _db.Items
            .Where(i => (i.Type == ItemType.Part || i.Type == ItemType.Kit)
                     && i.Source == ItemMasterSource.Internal
                     && i.ItemGroupId == fgId.Value)
            .ToListAsync(ct);

        var changes = new List<ItemSourceBackfillChange>(legacy.Count);
        foreach (var item in legacy)
        {
            changes.Add(new ItemSourceBackfillChange(
                ItemId: item.Id,
                PartNumber: item.PartNumber,
                FromSource: nameof(ItemMasterSource.Internal),
                ToSource: nameof(ItemMasterSource.ExternalERP)));

            item.Source = ItemMasterSource.ExternalERP;
        }

        if (legacy.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        // Reporting counts (post-flip).
        var alreadyExternal = await _db.Items.AsNoTracking()
            .CountAsync(i => i.Source == ItemMasterSource.ExternalERP || i.Source == ItemMasterSource.Synced, ct);
        var leftInternal = await _db.Items.AsNoTracking()
            .CountAsync(i => i.Source == ItemMasterSource.Internal, ct);

        _logger.LogInformation(
            "ItemSourceBackfillSeeder finished — scanned {Total}, flipped {Flipped}, already-external {Already}, left-internal {Internal}.",
            totalScanned, legacy.Count, alreadyExternal, leftInternal);

        return new ItemSourceBackfillResult(
            TotalItemsScanned: totalScanned,
            ItemsFlipped: legacy.Count,
            ItemsAlreadyExternal: alreadyExternal,
            ItemsLeftInternal: leftInternal,
            Changes: changes,
            Warnings: warnings);
    }
}
