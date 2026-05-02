using System.Security.Cryptography;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.Integrations;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ITenantContext _tenantContext;

    public IndexModel(AppDbContext db, IWebHostEnvironment env, ITenantContext tenantContext)
    {
        _db = db;
        _env = env;
        _tenantContext = tenantContext;
    }

    public List<IntegrationEndpoint> Endpoints { get; set; } = new();
    public bool IsLabEnvironment => _env.IsDevelopment();
    public string? SuccessMessage { get; set; }
    public string? NewSecret { get; set; }

    public async Task OnGetAsync()
    {
        if (!IsLabEnvironment) return;

        Endpoints = await _db.IntegrationEndpoints
            .Where(e => e.TenantId == _tenantContext.TenantId || e.TenantId == null)
            .OrderBy(e => e.Name)
            .ToListAsync();

        if (TempData.ContainsKey("SuccessMessage"))
            SuccessMessage = TempData["SuccessMessage"]?.ToString();

        if (TempData.ContainsKey("NewSecret"))
            NewSecret = TempData["NewSecret"]?.ToString();
    }

    public async Task<IActionResult> OnPostCreateAsync(string name, string integrationKey, string? allowedEventTypes, string? description)
    {
        if (!IsLabEnvironment) return RedirectToPage();

        var existingKey = await _db.IntegrationEndpoints.AnyAsync(e => e.IntegrationKey == integrationKey);
        if (existingKey)
        {
            TempData["SuccessMessage"] = "Integration key already exists. Choose a different key.";
            return RedirectToPage();
        }

        var secret = GenerateSecret();
        var endpoint = new IntegrationEndpoint
        {
            Name = name,
            IntegrationKey = integrationKey.ToLowerInvariant(),
            Secret = secret,
            AllowedEventTypesCsv = allowedEventTypes ?? "",
            Description = description,
            TenantId = _tenantContext.TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "Admin",
            IsActive = true
        };

        _db.IntegrationEndpoints.Add(endpoint);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Integration endpoint '{name}' created successfully.";
        TempData["NewSecret"] = secret;

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int endpointId)
    {
        if (!IsLabEnvironment) return RedirectToPage();

        var endpoint = await _db.IntegrationEndpoints
            .Where(e => (e.TenantId == _tenantContext.TenantId || e.TenantId == null) && e.Id == endpointId)
            .FirstOrDefaultAsync();
        if (endpoint != null)
        {
            endpoint.IsActive = !endpoint.IsActive;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Endpoint '{endpoint.Name}' is now {(endpoint.IsActive ? "active" : "inactive")}.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRegenerateSecretAsync(int endpointId)
    {
        if (!IsLabEnvironment) return RedirectToPage();

        var endpoint = await _db.IntegrationEndpoints
            .Where(e => (e.TenantId == _tenantContext.TenantId || e.TenantId == null) && e.Id == endpointId)
            .FirstOrDefaultAsync();
        if (endpoint != null)
        {
            var secret = GenerateSecret();
            endpoint.Secret = secret;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Secret regenerated for '{endpoint.Name}'.";
            TempData["NewSecret"] = secret;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int endpointId)
    {
        if (!IsLabEnvironment) return RedirectToPage();

        var endpoint = await _db.IntegrationEndpoints
            .Where(e => (e.TenantId == _tenantContext.TenantId || e.TenantId == null) && e.Id == endpointId)
            .FirstOrDefaultAsync();
        if (endpoint != null)
        {
            _db.IntegrationEndpoints.Remove(endpoint);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Endpoint '{endpoint.Name}' deleted.";
        }

        return RedirectToPage();
    }

    private static string GenerateSecret()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
