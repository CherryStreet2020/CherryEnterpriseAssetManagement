// PR #101: ApiKey is tenant-scoped. CreateApiKeyAsync requires the issuing
// admin's TenantId and accepts an optional CompanyId narrowing. ValidateKeyAsync
// returns the full ApiKey row including those fields; AssetsApiController applies
// them via _tenantContext before any data query. See Models/ApiKey.cs for the
// schema and docs/api/v1-tenant-scoping.md for the breaking-change notes.
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Abs.FixedAssets.Services;

public class ApiService
{
    private readonly AppDbContext _context;

    public ApiService(AppDbContext context)
    {
        _context = context;
    }

    // PR #101: tenantId is now required. CompanyId is optional — null means the
    // key may read every company visible to the tenant, a specific value
    // restricts it to that one company. Callers must pass the issuing admin's
    // current tenant (from ITenantContext.TenantId). There is no default; if a
    // caller cannot determine the tenant it should not be issuing a key.
    public async Task<(ApiKey key, string rawKey)> CreateApiKeyAsync(string name, int tenantId, int? companyId = null, string? createdBy = null, DateTime? expiresAt = null)
    {
        var rawKey = GenerateApiKey();
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey.Substring(0, 8);

        var apiKey = new ApiKey
        {
            Name = name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            CreatedBy = createdBy,
            ExpiresAt = expiresAt,
            Scopes = "assets:read,assets:write,journals:read",
            TenantId = tenantId,
            CompanyId = companyId
        };

        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync();

        return (apiKey, rawKey);
    }

    public async Task<ApiKey?> ValidateKeyAsync(string rawKey)
    {
        var keyHash = HashKey(rawKey);
        var apiKey = await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash && k.IsActive);

        if (apiKey == null) return null;

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            return null;
        }

        apiKey.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return apiKey;
    }

    public async Task<List<ApiKey>> GetAllKeysAsync()
    {
        return await _context.ApiKeys
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> RevokeKeyAsync(int keyId)
    {
        var key = await _context.ApiKeys.FindAsync(keyId);
        if (key == null) return false;

        key.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    private string GenerateApiKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return "cfa_" + Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "").Substring(0, 40);
    }

    private string HashKey(string key)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLower();
    }
}

public class AssetDto
{
    public int Id { get; set; }
    public string AssetNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime InServiceDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal NetBookValue => AcquisitionCost - AccumulatedDepreciation;
    public string? Location { get; set; }
    public string? Department { get; set; }
    public string Status { get; set; } = "Active";

    public static AssetDto FromAsset(Asset asset) => new()
    {
        Id = asset.Id,
        AssetNumber = asset.AssetNumber,
        Description = asset.Description,
        Model = asset.Model,
        SerialNumber = asset.SerialNumber,
        InServiceDate = asset.InServiceDate,
        AcquisitionCost = asset.AcquisitionCost,
        AccumulatedDepreciation = asset.AccumulatedDepreciation,
        Location = asset.LocationRef?.Name,
        Department = asset.Department,
        Status = asset.Status.ToString()
    };
}

public class CreateAssetRequest
{
    public string AssetNumber { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime InServiceDate { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal SalvageValue { get; set; }
    public int UsefulLifeMonths { get; set; }
    public string? Location { get; set; }
    public string? Department { get; set; }
}

public class UpdateAssetRequest
{
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? Department { get; set; }
    public string? Status { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public int? TotalCount { get; set; }
}
