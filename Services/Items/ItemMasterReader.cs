// B6 Foundation Sprint PR-FS-7 (2026-05-26) — ItemMasterReader impl.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Items;

public sealed class ItemMasterReader : IItemMasterReader
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItemMasterReader> _logger;

    public ItemMasterReader(AppDbContext db, ILogger<ItemMasterReader> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ItemMasterExpansionFields?> GetExpansionFieldsAsync(int itemId, CancellationToken ct)
    {
        var hit = await _db.Items.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new ItemMasterExpansionFields(
                i.Id, i.PartNumber, i.Description, i.Type, i.Source,
                i.PlanningPolicy, i.MakeBuyCode, i.LotSizingRule, i.MrpPlannerCode,
                i.IsSellable, i.IsPhantom, i.RequiresKitting,
                i.AS9100Critical, i.KeyCharacteristic, i.RequiresFai, i.InspectionPlanId,
                i.ECCN, i.ScheduleB, i.IntrastatCode, i.EAR99,
                i.FrozenStandardCost, i.FrozenStandardCostEffectiveAtUtc,
                i.ItemFamily, i.LifecycleStage))
            .FirstOrDefaultAsync(ct);

        if (hit == null)
        {
            _logger.LogInformation("ItemMasterReader: Item {ItemId} not found.", itemId);
        }
        return hit;
    }
}
