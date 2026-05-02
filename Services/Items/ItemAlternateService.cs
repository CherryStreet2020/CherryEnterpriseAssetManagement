using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Items;

public interface IItemAlternateService
{
    Task<ItemAlternate> AddAlternateAsync(int itemId, int alternateItemId, AlternateType type, int rank, string? reason, bool isApproved, int? userId = null);
    Task RemoveAlternateAsync(int itemId, int alternateItemId);
    Task<List<ItemAlternate>> GetAlternatesAsync(int itemId);
    Task<ItemAlternate?> GetBestAlternateAsync(int itemId);
}

public class ItemAlternateService : IItemAlternateService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ItemAlternateService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ItemAlternate> AddAlternateAsync(int itemId, int alternateItemId, AlternateType type, int rank, string? reason, bool isApproved, int? userId = null)
    {
        if (itemId == alternateItemId)
        {
            throw new InvalidOperationException("An item cannot be an alternate of itself.");
        }

        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required for alternate operations");

        var existing = await _db.ItemAlternates
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ItemId == itemId && x.AlternateItemId == alternateItemId);

        if (existing != null)
        {
            existing.AlternateType = type;
            existing.Rank = rank;
            existing.Reason = reason;
            existing.IsApproved = isApproved;

            await _db.SaveChangesAsync();
            await LogAuditAsync("ITEM.ALTERNATE.ADDED", itemId, $"Updated alternate item {alternateItemId} with rank {rank}", userId);
            return existing;
        }

        var newAlt = new ItemAlternate
        {
            TenantId = tenantId,
            ItemId = itemId,
            AlternateItemId = alternateItemId,
            AlternateType = type,
            Rank = rank,
            Reason = reason,
            IsApproved = isApproved,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _db.ItemAlternates.Add(newAlt);
        await _db.SaveChangesAsync();
        await LogAuditAsync("ITEM.ALTERNATE.ADDED", itemId, $"Added alternate item {alternateItemId} with rank {rank}", userId);

        return newAlt;
    }

    public async Task RemoveAlternateAsync(int itemId, int alternateItemId)
    {
        var tenantId = _tenantContext.TenantId;
        var existing = await _db.ItemAlternates
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ItemId == itemId && x.AlternateItemId == alternateItemId);

        if (existing != null)
        {
            _db.ItemAlternates.Remove(existing);
            await _db.SaveChangesAsync();
            await LogAuditAsync("ITEM.ALTERNATE.REMOVED", itemId, $"Removed alternate item {alternateItemId}", null);
        }
    }

    public async Task<List<ItemAlternate>> GetAlternatesAsync(int itemId)
    {
        var tenantId = _tenantContext.TenantId;
        return await _db.ItemAlternates
            .Include(x => x.AlternateItem)
            .Where(x => x.TenantId == tenantId && x.ItemId == itemId)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.AlternateItemId)
            .ToListAsync();
    }

    public async Task<ItemAlternate?> GetBestAlternateAsync(int itemId)
    {
        var tenantId = _tenantContext.TenantId;
        return await _db.ItemAlternates
            .Include(x => x.AlternateItem)
            .Where(x => x.TenantId == tenantId && x.ItemId == itemId && x.IsApproved)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.AlternateItemId)
            .FirstOrDefaultAsync();
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
