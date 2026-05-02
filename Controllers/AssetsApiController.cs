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

    public AssetsApiController(AppDbContext context, ApiService apiService)
    {
        _context = context;
        _apiService = apiService;
    }

    private async Task<ApiKey?> ValidateApiKey()
    {
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            return null;
        }
        return await _apiService.ValidateKeyAsync(apiKeyHeader.ToString());
    }

    [HttpGet]
    public async Task<IActionResult> GetAssets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? location = null,
        [FromQuery] string? status = null)
    {
        var apiKey = await ValidateApiKey();
        if (apiKey == null)
        {
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Invalid or missing API key" });
        }

        var query = _context.Assets.AsQueryable();

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
        var apiKey = await ValidateApiKey();
        if (apiKey == null)
        {
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Invalid or missing API key" });
        }

        var asset = await _context.Assets.FindAsync(id);
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

    private static string FormatETag(uint rowVersion) => "\"" + rowVersion.ToString(CultureInfo.InvariantCulture) + "\"";

    private static bool TryParseETag(string? raw, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(2);
        trimmed = trimmed.Trim('"');
        return uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsset([FromBody] CreateAssetRequest request)
    {
        var apiKey = await ValidateApiKey();
        if (apiKey == null)
        {
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Invalid or missing API key" });
        }

        if (string.IsNullOrEmpty(request.AssetNumber) || string.IsNullOrEmpty(request.Description))
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "AssetNumber and Description are required" });
        }

        var existing = await _context.Assets.AnyAsync(a => a.AssetNumber == request.AssetNumber);
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
            Status = AssetStatus.Active
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
        var apiKey = await ValidateApiKey();
        if (apiKey == null)
        {
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Invalid or missing API key" });
        }

        // Optimistic concurrency: clients must send the current row's version via If-Match.
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

        var asset = await _context.Assets.FindAsync(id);
        if (asset == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Asset not found" });
        }

        // Explicit precondition check: enforce If-Match even when the request body
        // would result in a no-op update (EF would otherwise skip the UPDATE and
        // never raise DbUpdateConcurrencyException, allowing a stale ETag to slip
        // through). The EF concurrency token below is kept as a second safety net
        // for true races between this read and SaveChanges.
        if (asset.RowVersion != expectedRowVersion)
        {
            Response.Headers[HeaderNames.ETag] = FormatETag(asset.RowVersion);
            return Conflict(new ApiResponse<AssetDto>
            {
                Success = false,
                Message = "If-Match precondition failed. The asset has been modified since you fetched it. Re-fetch the asset and retry with the new ETag.",
                Data = AssetDto.FromAsset(asset)
            });
        }

        if (!string.IsNullOrEmpty(request.Description))
            asset.Description = request.Description;
        if (request.Department != null)
            asset.Department = request.Department;
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<AssetStatus>(request.Status, out var status))
            asset.Status = status;

        // Force EF to use the client-supplied xmin in the WHERE clause.
        _context.Entry(asset).Property(a => a.RowVersion).OriginalValue = expectedRowVersion;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await _context.Entry(asset).ReloadAsync();
            Response.Headers[HeaderNames.ETag] = FormatETag(asset.RowVersion);
            return Conflict(new ApiResponse<AssetDto>
            {
                Success = false,
                Message = "The asset was modified by another client between read and save. Re-fetch the asset and retry with the new ETag.",
                Data = AssetDto.FromAsset(asset)
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
        var apiKey = await ValidateApiKey();
        if (apiKey == null)
        {
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Invalid or missing API key" });
        }

        var asset = await _context.Assets.FindAsync(id);
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
