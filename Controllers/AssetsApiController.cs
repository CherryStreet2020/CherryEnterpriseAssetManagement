using System.Globalization;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace Abs.FixedAssets.Controllers;

[ApiController]
[Route("api/v1/assets")]
public class AssetsApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ApiService _apiService;
    private readonly ITenantContext _tenantContext;

    public AssetsApiController(AppDbContext context, ApiService apiService, ITenantContext tenantContext)
    {
        _context = context;
        _apiService = apiService;
        _tenantContext = tenantContext;
    }

    // PR #101: validates the X-API-Key header AND binds the request's tenant
    // context to the key's TenantId/CompanyId. Returns (key, null) on success
    // or (null, errorResult) which the caller must return verbatim. Keys
    // issued before PR #101 carry TenantId == 0 and are refused — admins must
    // re-issue them. Every query in this controller MUST go through this gate
    // and scope by _tenantContext.VisibleCompanyIds; querying _context.Assets
    // without that filter is the cross-tenant leak this PR closes.
    private async Task<(ApiKey? key, IActionResult? error)> RequireApiKeyWithTenantScope()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
            return (null, Unauthorized(new ApiResponse<object> { Success = false, Message = "Invalid or missing API key" }));

        var key = await _apiService.ValidateKeyAsync(apiKeyHeader.ToString());
        if (key == null)
            return (null, Unauthorized(new ApiResponse<object> { Success = false, Message = "Invalid or missing API key" }));

        if (key.TenantId == 0)
            return (null, StatusCode(403, new ApiResponse<object>
            {
                Success = false,
                Message = "This API key was issued before tenant scoping was enforced. Re-issue the key in your admin console — pre-#101 keys do not carry a tenant binding and are refused for safety."
            }));

        // Set tenant context for downstream queries to scope correctly.
        _tenantContext.SetContext(key.TenantId, key.CompanyId, null);
        if (key.CompanyId.HasValue)
            _tenantContext.SetHierarchyContext(key.CompanyId, new List<int> { key.CompanyId.Value });
        else
            _tenantContext.SetHierarchyContext(null, new List<int>());

        return (key, null);
    }

    [HttpGet]
    public async Task<IActionResult> GetAssets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? location = null,
        [FromQuery] string? status = null)
    {
        var (apiKey, error) = await RequireApiKeyWithTenantScope();
        if (error != null) return error;

        var visible = _tenantContext.VisibleCompanyIds;
        var tenantId = apiKey!.TenantId;
        var hasCompanyScope = apiKey.CompanyId.HasValue;
        var query = hasCompanyScope
            ? _context.Assets.Where(a => a.CompanyId.HasValue && visible.Contains(a.CompanyId.Value))
            : _context.Assets.Where(a => a.CompanyId.HasValue
                && _context.Companies.Any(c => c.Id == a.CompanyId.Value && c.TenantId == tenantId));

        if (!string.IsNullOrEmpty(location))
            query = query.Where(a => a.LocationRef != null && a.LocationRef.Name == location);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AssetStatus>(status, out var assetStatus))
            query = query.Where(a => a.Status == assetStatus);

        var totalCount = await query.CountAsync();
        var assets = await query
            .OrderBy(a => a.AssetNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => AssetDto.FromAsset(a))
            .ToListAsync();

        return Ok(new ApiResponse<List<AssetDto>>
        {
            Success = true,
            Data = assets,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsset(int id)
    {
        var (apiKey, error) = await RequireApiKeyWithTenantScope();
        if (error != null) return error;

        var visible = _tenantContext.VisibleCompanyIds;
        var tenantId = apiKey!.TenantId;
        var hasCompanyScope = apiKey.CompanyId.HasValue;
        var asset = hasCompanyScope
            ? await _context.Assets
                .Where(a => a.Id == id && a.CompanyId.HasValue && visible.Contains(a.CompanyId.Value))
                .FirstOrDefaultAsync()
            : await _context.Assets
                .Where(a => a.Id == id && a.CompanyId.HasValue
                    && _context.Companies.Any(c => c.Id == a.CompanyId.Value && c.TenantId == tenantId))
                .FirstOrDefaultAsync();

        if (asset == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Asset not found" });
        }

        Response.Headers[HeaderNames.ETag] = FormatETag(asset.RowVersion);
        return Ok(new ApiResponse<AssetDto>
        {
            Success = true,
            Data = AssetDto.FromAsset(asset)
        });
    }

    // ETag = base64 of the 4-byte RowVersion (xmin). Returns "" when null.
    internal static string FormatETag(byte[]? rowVersion)
    {
        if (rowVersion == null || rowVersion.Length == 0) return "\"\"";
        return "\"" + Convert.ToBase64String(rowVersion) + "\"";
    }

    internal static bool TryParseETag(string? raw, out byte[] value)
    {
        value = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(2);
        trimmed = trimmed.Trim('"').Trim();
        if (trimmed.Length == 0) return false;

        try
        {
            var bytes = Convert.FromBase64String(trimmed);
            if (bytes.Length == 4) { value = bytes; return true; }
        }
        catch (FormatException) { }

        // Back-compat with the pre-base64 hardening contract: a quoted decimal xmin.
        if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u))
        {
            value = new[]
            {
                (byte)((u >> 24) & 0xFF),
                (byte)((u >> 16) & 0xFF),
                (byte)((u >> 8)  & 0xFF),
                (byte)( u        & 0xFF)
            };
            return true;
        }
        return false;
    }

    private static bool RowVersionEquals(byte[]? a, byte[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsset([FromBody] CreateAssetRequest request)
    {
        var (apiKey, error) = await RequireApiKeyWithTenantScope();
        if (error != null) return error;

        if (string.IsNullOrEmpty(request.AssetNumber) || string.IsNullOrEmpty(request.Description))
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "AssetNumber and Description are required" });
        }

        // AssetNumber uniqueness is scoped per-company. Check existing only
        // within the key's visible companies — otherwise a key for Tenant B
        // could be denied creating "FA-1001" because Tenant A already has one.
        var visible = _tenantContext.VisibleCompanyIds;
        var tenantId = apiKey!.TenantId;
        var hasCompanyScope = apiKey.CompanyId.HasValue;
        var existing = hasCompanyScope
            ? await _context.Assets.AnyAsync(a => a.AssetNumber == request.AssetNumber
                && a.CompanyId.HasValue && visible.Contains(a.CompanyId.Value))
            : await _context.Assets.AnyAsync(a => a.AssetNumber == request.AssetNumber
                && a.CompanyId.HasValue
                && _context.Companies.Any(c => c.Id == a.CompanyId.Value && c.TenantId == tenantId));
        if (existing)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Asset number already exists" });
        }

        var asset = new Asset
        {
            AssetNumber = request.AssetNumber,
            Description = request.Description,
            Model = request.Model,
            SerialNumber = request.SerialNumber,
            InServiceDate = request.InServiceDate,
            AcquisitionCost = request.AcquisitionCost,
            SalvageValue = request.SalvageValue,
            UsefulLifeMonths = request.UsefulLifeMonths,
            Department = request.Department,
            Status = AssetStatus.Active,
            // PR #101: stamp the asset with the key's company scope so it
            // lands inside the same boundary the key reads from.
            CompanyId = apiKey.CompanyId
        };

        _context.Assets.Add(asset);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAsset), new { id = asset.Id }, new ApiResponse<AssetDto>
        {
            Success = true,
            Message = "Asset created successfully",
            Data = AssetDto.FromAsset(asset)
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAsset(int id, [FromBody] UpdateAssetRequest request)
    {
        var (apiKey, error) = await RequireApiKeyWithTenantScope();
        if (error != null) return error;

        var ifMatch = Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return StatusCode(StatusCodes.Status428PreconditionRequired, new ApiResponse<object>
            {
                Success = false,
                Message = "If-Match header is required. GET the resource first and send the returned ETag."
            });
        }
        if (!TryParseETag(ifMatch, out var expectedRowVersion))
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Malformed If-Match header." });
        }

        var visible = _tenantContext.VisibleCompanyIds;
        var tenantId = apiKey!.TenantId;
        var hasCompanyScope = apiKey.CompanyId.HasValue;
        var asset = hasCompanyScope
            ? await _context.Assets
                .Where(a => a.Id == id && a.CompanyId.HasValue && visible.Contains(a.CompanyId.Value))
                .FirstOrDefaultAsync()
            : await _context.Assets
                .Where(a => a.Id == id && a.CompanyId.HasValue
                    && _context.Companies.Any(c => c.Id == a.CompanyId.Value && c.TenantId == tenantId))
                .FirstOrDefaultAsync();
        if (asset == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Asset not found" });
        }

        // Enforce If-Match even when the body is a no-op (EF would skip the UPDATE
        // and never raise the concurrency exception). The EF token still guards
        // races between this read and SaveChanges.
        if (!RowVersionEquals(asset.RowVersion, expectedRowVersion))
        {
            Response.Headers[HeaderNames.ETag] = FormatETag(asset.RowVersion);
            return Conflict(new
            {
                error = "concurrency",
                message = "If-Match precondition failed. The asset has been modified since you fetched it. Re-fetch the asset and retry with the new ETag.",
                current = AssetDto.FromAsset(asset)
            });
        }

        if (!string.IsNullOrEmpty(request.Description))
            asset.Description = request.Description;
        if (request.Department != null)
            asset.Department = request.Department;
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<AssetStatus>(request.Status, out var status))
            asset.Status = status;

        _context.Entry(asset).Property(a => a.RowVersion).OriginalValue = expectedRowVersion;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await _context.Entry(asset).ReloadAsync();
            Response.Headers[HeaderNames.ETag] = FormatETag(asset.RowVersion);
            return Conflict(new
            {
                error = "concurrency",
                message = "The asset was modified by another client between read and save. Re-fetch the asset and retry with the new ETag.",
                current = AssetDto.FromAsset(asset)
            });
        }

        Response.Headers[HeaderNames.ETag] = FormatETag(asset.RowVersion);
        return Ok(new ApiResponse<AssetDto>
        {
            Success = true,
            Message = "Asset updated successfully",
            Data = AssetDto.FromAsset(asset)
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsset(int id)
    {
        var (apiKey, error) = await RequireApiKeyWithTenantScope();
        if (error != null) return error;

        var visible = _tenantContext.VisibleCompanyIds;
        var tenantId = apiKey!.TenantId;
        var hasCompanyScope = apiKey.CompanyId.HasValue;
        var asset = hasCompanyScope
            ? await _context.Assets
                .Where(a => a.Id == id && a.CompanyId.HasValue && visible.Contains(a.CompanyId.Value))
                .FirstOrDefaultAsync()
            : await _context.Assets
                .Where(a => a.Id == id && a.CompanyId.HasValue
                    && _context.Companies.Any(c => c.Id == a.CompanyId.Value && c.TenantId == tenantId))
                .FirstOrDefaultAsync();
        if (asset == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Asset not found" });
        }

        asset.Status = AssetStatus.Disposed;
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Asset marked as disposed"
        });
    }
}
