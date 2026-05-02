using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Integrations;

public interface IIntegrationMappingService
{
    Task<int?> GetInternalIdAsync(int integrationEndpointId, string mappingType, string externalId);
    Task<string?> GetInternalCodeAsync(int integrationEndpointId, string mappingType, string externalId);
    Task<IntegrationMapping?> GetMappingAsync(int integrationEndpointId, string mappingType, string externalId);
    Task<List<IntegrationMapping>> GetMappingsForEndpointAsync(int integrationEndpointId);
    Task<IntegrationMapping> CreateMappingAsync(int integrationEndpointId, string mappingType, string externalId, int? internalId, string? internalCode, string? notes, string createdBy);
    Task<bool> DeleteMappingAsync(int mappingId);
}

public class IntegrationMappingService : IIntegrationMappingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<IntegrationMappingService> _logger;
    private readonly ITenantContext _tenantContext;

    public IntegrationMappingService(AppDbContext db, ILogger<IntegrationMappingService> logger, ITenantContext tenantContext)
    {
        _db = db;
        _logger = logger;
        _tenantContext = tenantContext;
    }

    private int GetTenantId() => _tenantContext.TenantId ?? 1;

    private async Task<bool> EndpointBelongsToTenantAsync(int integrationEndpointId)
    {
        var tenantId = GetTenantId();
        return await _db.IntegrationEndpoints
            .AnyAsync(e => e.Id == integrationEndpointId && e.TenantId == tenantId);
    }

    public async Task<int?> GetInternalIdAsync(int integrationEndpointId, string mappingType, string externalId)
    {
        if (!await EndpointBelongsToTenantAsync(integrationEndpointId))
            return null;

        var mapping = await _db.IntegrationMappings
            .FirstOrDefaultAsync(m => m.IntegrationEndpointId == integrationEndpointId &&
                                      m.MappingType == mappingType &&
                                      m.ExternalId == externalId);
        return mapping?.InternalId;
    }

    public async Task<string?> GetInternalCodeAsync(int integrationEndpointId, string mappingType, string externalId)
    {
        if (!await EndpointBelongsToTenantAsync(integrationEndpointId))
            return null;

        var mapping = await _db.IntegrationMappings
            .FirstOrDefaultAsync(m => m.IntegrationEndpointId == integrationEndpointId &&
                                      m.MappingType == mappingType &&
                                      m.ExternalId == externalId);
        return mapping?.InternalCode;
    }

    public async Task<IntegrationMapping?> GetMappingAsync(int integrationEndpointId, string mappingType, string externalId)
    {
        if (!await EndpointBelongsToTenantAsync(integrationEndpointId))
            return null;

        return await _db.IntegrationMappings
            .FirstOrDefaultAsync(m => m.IntegrationEndpointId == integrationEndpointId &&
                                      m.MappingType == mappingType &&
                                      m.ExternalId == externalId);
    }

    public async Task<List<IntegrationMapping>> GetMappingsForEndpointAsync(int integrationEndpointId)
    {
        if (!await EndpointBelongsToTenantAsync(integrationEndpointId))
            return new List<IntegrationMapping>();

        return await _db.IntegrationMappings
            .Where(m => m.IntegrationEndpointId == integrationEndpointId)
            .OrderBy(m => m.MappingType)
            .ThenBy(m => m.ExternalId)
            .ToListAsync();
    }

    public async Task<IntegrationMapping> CreateMappingAsync(
        int integrationEndpointId,
        string mappingType,
        string externalId,
        int? internalId,
        string? internalCode,
        string? notes,
        string createdBy)
    {
        if (!await EndpointBelongsToTenantAsync(integrationEndpointId))
            throw new InvalidOperationException("Integration endpoint not found for this tenant");

        var mapping = new IntegrationMapping
        {
            IntegrationEndpointId = integrationEndpointId,
            MappingType = mappingType,
            ExternalId = externalId,
            InternalId = internalId,
            InternalCode = internalCode,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _db.IntegrationMappings.Add(mapping);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created integration mapping: {Type} {External} -> {Internal}",
            mappingType, externalId, internalId ?? (object?)internalCode);

        return mapping;
    }

    public async Task<bool> DeleteMappingAsync(int mappingId)
    {
        var tenantId = GetTenantId();
        var mapping = await _db.IntegrationMappings
            .Include(m => m.IntegrationEndpoint)
            .Where(m => m.Id == mappingId && m.IntegrationEndpoint != null && m.IntegrationEndpoint.TenantId == tenantId)
            .FirstOrDefaultAsync();
        if (mapping == null) return false;

        _db.IntegrationMappings.Remove(mapping);
        await _db.SaveChangesAsync();
        return true;
    }
}
