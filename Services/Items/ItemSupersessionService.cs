using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Items;

public interface IItemSupersessionService
{
    Task<ItemSupersession> SetSupersessionAsync(int oldItemId, int newItemId, DateTime? effectiveFromUtc, string? reason, int? userId = null);
    Task RemoveSupersessionAsync(int oldItemId);
    Task<List<Item>> GetSupersessionChainAsync(int itemId);
    Task<Item?> ResolveCurrentItemAsync(int itemId);
    Task<ItemSupersession?> GetSupersessionAsync(int oldItemId);
    Task<ItemSupersession?> GetSupersededByAsync(int newItemId);
}

public class ItemSupersessionService : IItemSupersessionService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ItemSupersessionService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ItemSupersession> SetSupersessionAsync(int oldItemId, int newItemId, DateTime? effectiveFromUtc, string? reason, int? userId = null)
    {
        if (oldItemId == newItemId)
        {
            throw new InvalidOperationException("An item cannot supersede itself.");
        }

        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required for supersession operations");

        if (await WouldCreateCycleAsync(oldItemId, newItemId, tenantId))
        {
            throw new InvalidOperationException("Setting this supersession would create a cycle.");
        }

        var existing = await _db.ItemSupersessions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.OldItemId == oldItemId);

        if (existing != null)
        {
            existing.NewItemId = newItemId;
            existing.EffectiveFromUtc = effectiveFromUtc;
            existing.Reason = reason;

            await _db.SaveChangesAsync();
            await LogAuditAsync("ITEM.SUPERSESSION.SET", oldItemId, $"Updated supersession: {oldItemId} -> {newItemId}", userId);
            return existing;
        }

        var newSup = new ItemSupersession
        {
            TenantId = tenantId,
            OldItemId = oldItemId,
            NewItemId = newItemId,
            EffectiveFromUtc = effectiveFromUtc,
            Reason = reason,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _db.ItemSupersessions.Add(newSup);
        await _db.SaveChangesAsync();
        await LogAuditAsync("ITEM.SUPERSESSION.SET", oldItemId, $"Set supersession: {oldItemId} -> {newItemId}", userId);

        return newSup;
    }

    public async Task RemoveSupersessionAsync(int oldItemId)
    {
        var tenantId = _tenantContext.TenantId;
        var existing = await _db.ItemSupersessions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.OldItemId == oldItemId);

        if (existing != null)
        {
            _db.ItemSupersessions.Remove(existing);
            await _db.SaveChangesAsync();
            await LogAuditAsync("ITEM.SUPERSESSION.REMOVED", oldItemId, $"Removed supersession from item {oldItemId}", null);
        }
    }

    public async Task<List<Item>> GetSupersessionChainAsync(int itemId)
    {
        var tenantId = _tenantContext.TenantId;
        var chain = new List<Item>();
        var visited = new HashSet<int>();
        var currentId = itemId;

        while (true)
        {
            if (visited.Contains(currentId))
            {
                break;
            }

            var item = await _db.Items.Where(i => i.Id == currentId).FirstOrDefaultAsync();
            if (item == null) break;

            chain.Add(item);
            visited.Add(currentId);

            var sup = await _db.ItemSupersessions
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.OldItemId == currentId);

            if (sup == null) break;

            currentId = sup.NewItemId;
        }

        return chain;
    }

    public async Task<Item?> ResolveCurrentItemAsync(int itemId)
    {
        var chain = await GetSupersessionChainAsync(itemId);
        return chain.LastOrDefault();
    }

    public async Task<ItemSupersession?> GetSupersessionAsync(int oldItemId)
    {
        var tenantId = _tenantContext.TenantId;
        return await _db.ItemSupersessions
            .Include(x => x.NewItem)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.OldItemId == oldItemId);
    }

    public async Task<ItemSupersession?> GetSupersededByAsync(int newItemId)
    {
        var tenantId = _tenantContext.TenantId;
        return await _db.ItemSupersessions
            .Include(x => x.OldItem)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.NewItemId == newItemId);
    }

    private async Task<bool> WouldCreateCycleAsync(int oldItemId, int newItemId, int tenantId)
    {
        var visited = new HashSet<int> { oldItemId };
        var currentId = newItemId;

        while (true)
        {
            if (visited.Contains(currentId))
            {
                return true;
            }

            visited.Add(currentId);

            var sup = await _db.ItemSupersessions
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.OldItemId == currentId);

            if (sup == null) break;

            currentId = sup.NewItemId;
        }

        return false;
    }

    private async Task LogAuditAsync(string action, int entityId, string details, int? userId)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            EntityType = "Item",
            EntityId = entityId,
            Action = action,
            Description = details,
            Username = userId?.ToString() ?? "System",
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
