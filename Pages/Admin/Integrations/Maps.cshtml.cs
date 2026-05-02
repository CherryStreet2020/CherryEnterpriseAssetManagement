using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.Integrations;

public class MapsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILookupService _lookupService;
    private readonly ITenantContext _tenantContext;

    public MapsModel(AppDbContext db, IWebHostEnvironment env, ILookupService lookupService, ITenantContext tenantContext)
    {
        _db = db;
        _env = env;
        _lookupService = lookupService;
        _tenantContext = tenantContext;
    }

    public List<IntegrationEndpoint> Endpoints { get; set; } = new();
    public List<IntegrationMapping> Mappings { get; set; } = new();
    public int? SelectedEndpointId { get; set; }
    public IntegrationEndpoint? SelectedEndpoint { get; set; }
    public bool IsLabEnvironment => _env.IsDevelopment();
    public string? SuccessMessage { get; set; }
    public List<SelectListItem> EntityTypeOptions { get; set; } = new();

    public async Task OnGetAsync(int? endpointId)
    {
        if (!IsLabEnvironment) return;

        Endpoints = await _db.IntegrationEndpoints
            .Where(e => e.TenantId == _tenantContext.TenantId || e.TenantId == null)
            .OrderBy(e => e.Name).ToListAsync();

        if (endpointId.HasValue)
        {
            SelectedEndpointId = endpointId;
            SelectedEndpoint = await _db.IntegrationEndpoints
                .Where(e => (e.TenantId == _tenantContext.TenantId || e.TenantId == null) && e.Id == endpointId)
                .FirstOrDefaultAsync();
            Mappings = await _db.IntegrationMappings
                .Where(m => m.IntegrationEndpointId == endpointId)
                .OrderBy(m => m.MappingType)
                .ThenBy(m => m.ExternalId)
                .ToListAsync();
        }

        EntityTypeOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "IntegrationEntityType", null, "");

        if (TempData.ContainsKey("SuccessMessage"))
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
    }

    public async Task<IActionResult> OnPostCreateAsync(
        int endpointId,
        string mappingType,
        string externalId,
        int? internalId,
        string? internalCode,
        string? notes)
    {
        if (!IsLabEnvironment) return RedirectToPage();

        var mapping = new IntegrationMapping
        {
            IntegrationEndpointId = endpointId,
            MappingType = mappingType,
            ExternalId = externalId,
            InternalId = internalId,
            InternalCode = internalCode,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "Admin"
        };

        _db.IntegrationMappings.Add(mapping);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Mapping added: {mappingType} {externalId} -> {internalId ?? (object?)internalCode}";
        return RedirectToPage(new { endpointId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int mappingId, int? endpointId)
    {
        if (!IsLabEnvironment) return RedirectToPage();

        var mapping = await _db.IntegrationMappings
            .Where(m => m.Id == mappingId)
            .FirstOrDefaultAsync();
        if (mapping != null)
        {
            _db.IntegrationMappings.Remove(mapping);
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Mapping deleted.";
        }

        return RedirectToPage(new { endpointId });
    }
}
