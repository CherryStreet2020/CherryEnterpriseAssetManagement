using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Items;

public interface IItemSourcingService
{
    Task<ItemApprovedVendor> SetApprovedVendorAsync(int itemId, int vendorId, AvlApprovalStatus status, bool isPreferred, string? notes, int? userId = null);
    Task RemoveApprovedVendorAsync(int itemId, int vendorId);
    Task<List<ItemApprovedVendor>> GetApprovedVendorsAsync(int itemId);
    Task<ItemApprovedVendor?> SetPreferredVendorAsync(int itemId, int vendorId, int? userId = null);
    Task<ItemApprovedVendor?> GetPreferredVendorAsync(int itemId);
}

public class ItemSourcingService : IItemSourcingService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ItemSourcingService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ItemApprovedVendor> SetApprovedVendorAsync(int itemId, int vendorId, AvlApprovalStatus status, bool isPreferred, string? notes, int? userId = null)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required for AVL operations");
        var companyId = _tenantContext.CompanyId;
        var siteId = _tenantContext.SiteId;

        var existing = await _db.ItemApprovedVendors
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ItemId == itemId && x.VendorId == vendorId);

        if (existing != null)
        {
            existing.ApprovalStatus = status;
            existing.Notes = notes;
            
            if (isPreferred && !existing.IsPreferred)
            {
                await ClearPreferredAsync(itemId, tenantId);
                existing.IsPreferred = true;
            }
            else if (!isPreferred && existing.IsPreferred)
            {
                existing.IsPreferred = false;
            }

            await _db.SaveChangesAsync();
            await LogAuditAsync("ITEM.AVL.UPDATED", itemId, $"Updated vendor {vendorId} AVL status to {status}", userId);
            return existing;
        }

        if (isPreferred)
        {
            await ClearPreferredAsync(itemId, tenantId);
        }

        var newAvl = new ItemApprovedVendor
        {
            TenantId = tenantId,
            CompanyId = companyId,
            SiteId = siteId,
            ItemId = itemId,
            VendorId = vendorId,
            ApprovalStatus = status,
            IsPreferred = isPreferred,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId
        };

        _db.ItemApprovedVendors.Add(newAvl);
        await _db.SaveChangesAsync();
        await LogAuditAsync("ITEM.AVL.UPDATED", itemId, $"Added vendor {vendorId} to AVL with status {status}", userId);

        return newAvl;
    }

    public async Task RemoveApprovedVendorAsync(int itemId, int vendorId)
    {
        var tenantId = _tenantContext.TenantId;
        var existing = await _db.ItemApprovedVendors
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ItemId == itemId && x.VendorId == vendorId);

        if (existing != null)
        {
            _db.ItemApprovedVendors.Remove(existing);
            await _db.SaveChangesAsync();
            await LogAuditAsync("ITEM.AVL.UPDATED", itemId, $"Removed vendor {vendorId} from AVL", null);
        }
    }

    public async Task<List<ItemApprovedVendor>> GetApprovedVendorsAsync(int itemId)
    {
        var tenantId = _tenantContext.TenantId;
        return await _db.ItemApprovedVendors
            .Include(x => x.Vendor)
            .Where(x => x.TenantId == tenantId && x.ItemId == itemId)
            .OrderByDescending(x => x.IsPreferred)
            .ThenBy(x => x.Vendor!.Name)
            .ToListAsync();
    }

    public async Task<ItemApprovedVendor?> SetPreferredVendorAsync(int itemId, int vendorId, int? userId = null)
    {
        var tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException("TenantId is required for AVL operations");

        await ClearPreferredAsync(itemId, tenantId);

        var avl = await _db.ItemApprovedVendors
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ItemId == itemId && x.VendorId == vendorId);

        if (avl == null) return null;

        avl.IsPreferred = true;
        await _db.SaveChangesAsync();
        await LogAuditAsync("ITEM.AVL.PREFERRED.SET", itemId, $"Set vendor {vendorId} as preferred", userId);

        return avl;
    }

    public async Task<ItemApprovedVendor?> GetPreferredVendorAsync(int itemId)
    {
        var tenantId = _tenantContext.TenantId;
        return await _db.ItemApprovedVendors
            .Include(x => x.Vendor)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ItemId == itemId && x.IsPreferred);
    }

    private async Task ClearPreferredAsync(int itemId, int tenantId)
    {
        var preferred = await _db.ItemApprovedVendors
            .Where(x => x.TenantId == tenantId && x.ItemId == itemId && x.IsPreferred)
            .ToListAsync();

        foreach (var p in preferred)
        {
            p.IsPreferred = false;
        }
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
