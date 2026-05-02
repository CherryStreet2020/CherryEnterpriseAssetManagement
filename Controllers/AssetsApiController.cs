using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        return Ok(new ApiResponse<AssetDto>
        {
            Success = true,
            Data = AssetDto.FromAsset(asset)
        });
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

        var asset = await _context.Assets.FindAsync(id);
        if (asset == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Asset not found" });
        }

        if (!string.IsNullOrEmpty(request.Description))
            asset.Description = request.Description;
        if (request.Department != null)
            asset.Department = request.Department;
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<AssetStatus>(request.Status, out var status))
            asset.Status = status;

        await _context.SaveChangesAsync();

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
