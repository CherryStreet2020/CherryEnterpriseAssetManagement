// TENANT SCOPING EXCEPTION: AuditLog and PeriodLock are system-level entities without CompanyId/TenantId.
// Audit logs record actions across all tenants for compliance and debugging.
// Period locks apply to accounting periods at the system level.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services;

public class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync<T>(string action, T? before, T? after, string? username = null, string? description = null, string? ipAddress = null) where T : class
    {
        var entry = new AuditLog
        {
            EntityType = typeof(T).Name,
            EntityId = GetEntityId(after ?? before),
            Action = action,
            BeforeJson = before != null ? JsonSerializer.Serialize(before, new JsonSerializerOptions { WriteIndented = true }) : null,
            AfterJson = after != null ? JsonSerializer.Serialize(after, new JsonSerializerOptions { WriteIndented = true }) : null,
            Username = username,
            Description = description,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        };

        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync();
    }

    public async Task LogCreateAsync<T>(T entity, string? username = null, string? description = null) where T : class
    {
        await LogAsync("Create", null, entity, username, description ?? $"Created {typeof(T).Name}");
    }

    public async Task LogUpdateAsync<T>(T before, T after, string? username = null, string? description = null) where T : class
    {
        await LogAsync("Update", before, after, username, description ?? $"Updated {typeof(T).Name}");
    }

    public async Task LogDeleteAsync<T>(T entity, string? username = null, string? description = null) where T : class
    {
        await LogAsync("Delete", entity, null, username, description ?? $"Deleted {typeof(T).Name}");
    }

    public async Task<List<AuditLog>> GetLogsAsync(string? entityType = null, int? entityId = null, int limit = 100)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(l => l.EntityType == entityType);

        if (entityId.HasValue)
            query = query.Where(l => l.EntityId == entityId);

        return await query
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<bool> IsPeriodLockedAsync(int period)
    {
        var periodLock = await _db.PeriodLocks.Where(p => p.Period == period && p.IsLocked).OrderBy(p => p.Id).FirstOrDefaultAsync();
        return periodLock != null;
    }

    public async Task<bool> LockPeriodAsync(int period, string? username = null, string? reason = null)
    {
        var existing = await _db.PeriodLocks.Where(p => p.Period == period).OrderBy(p => p.Id).FirstOrDefaultAsync();
        if (existing != null)
        {
            existing.IsLocked = true;
            existing.LockedAt = DateTime.UtcNow;
            existing.LockedBy = username;
            existing.Reason = reason;
        }
        else
        {
            _db.PeriodLocks.Add(new PeriodLock
            {
                Period = period,
                LockedAt = DateTime.UtcNow,
                LockedBy = username,
                Reason = reason,
                IsLocked = true
            });
        }

        await _db.SaveChangesAsync();
        await LogAsync<PeriodLock>("LockPeriod", null, null, username, $"Locked period {period}");
        return true;
    }

    public async Task<bool> UnlockPeriodAsync(int period, string? username = null)
    {
        var existing = await _db.PeriodLocks.Where(p => p.Period == period).OrderBy(p => p.Id).FirstOrDefaultAsync();
        if (existing != null)
        {
            existing.IsLocked = false;
            await _db.SaveChangesAsync();
            await LogAsync<PeriodLock>("UnlockPeriod", null, null, username, $"Unlocked period {period}");
            return true;
        }
        return false;
    }

    public async Task<List<PeriodLock>> GetLockedPeriodsAsync()
    {
        return await _db.PeriodLocks
            .Where(p => p.IsLocked)
            .OrderByDescending(p => p.Period)
            .ToListAsync();
    }

    private static int? GetEntityId<T>(T? entity) where T : class
    {
        if (entity == null) return null;
        var idProperty = typeof(T).GetProperty("Id");
        if (idProperty != null)
        {
            var value = idProperty.GetValue(entity);
            if (value is int intValue) return intValue;
        }
        return null;
    }
}
