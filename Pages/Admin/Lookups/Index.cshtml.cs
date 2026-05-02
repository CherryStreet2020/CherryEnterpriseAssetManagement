using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Seeding.Pipelines;

namespace Abs.FixedAssets.Pages.Admin.Lookups;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILookupService _lookupService;

    public IndexModel(AppDbContext db, ITenantContext tenantContext, ILookupService lookupService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lookupService = lookupService;
    }

    public List<LookupTypeViewModel> LookupTypes { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        var tenantId = _tenantContext.TenantId;
        LookupTypes = await _db.LookupTypes
            .Where(lt => lt.TenantId == tenantId)
            .Include(lt => lt.Values)
            .OrderBy(lt => lt.Key)
            .Select(lt => new LookupTypeViewModel
            {
                Id = lt.Id,
                Key = lt.Key,
                Name = lt.Name,
                IsSystem = lt.IsSystem,
                IsActive = lt.IsActive,
                ValueCount = lt.Values.Count(v => v.IsActive),
                TotalValueCount = lt.Values.Count
            })
            .ToListAsync();

        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
    }

    public async Task<IActionResult> OnPostCreateTypeAsync(string key, string name)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "Key and Name are required.";
            return RedirectToPage();
        }

        var tenantId = _tenantContext.TenantId;
        var exists = await _db.LookupTypes.AnyAsync(lt => lt.TenantId == tenantId && lt.CompanyId == null && lt.Key == key);
        if (exists)
        {
            TempData["ErrorMessage"] = $"Lookup type with key '{key}' already exists.";
            return RedirectToPage();
        }

        var lookupType = new LookupType
        {
            TenantId = tenantId,
            CompanyId = null,
            Key = key,
            Name = name,
            IsSystem = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.LookupTypes.Add(lookupType);
        await _db.SaveChangesAsync();

        await LookupBaselineEnforcer.EnforceForLookupTypeAsync(_db, lookupType.Id, lookupType.Key);

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            EntityType = "LookupType",
            EntityId = lookupType.Id,
            Action = "Created",
            Timestamp = DateTime.UtcNow,
            Username = User.Identity?.Name ?? "System",
            Description = $"Created lookup type: {key} ({name})"
        });
        await _db.SaveChangesAsync();

        _lookupService.InvalidateCache();
        TempData["SuccessMessage"] = $"Lookup type '{name}' created successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var type = await _db.LookupTypes
            .Where(lt => (lt.TenantId == _tenantContext.TenantId || lt.TenantId == null) && lt.Id == id)
            .FirstOrDefaultAsync();
        if (type == null) return NotFound();

        type.IsActive = !type.IsActive;
        type.UpdatedAt = DateTime.UtcNow;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            EntityType = "LookupType",
            EntityId = id,
            Action = type.IsActive ? "Activated" : "Deactivated",
            Timestamp = DateTime.UtcNow,
            Username = User.Identity?.Name ?? "System",
            Description = $"{(type.IsActive ? "Activated" : "Deactivated")} lookup type: {type.Key}"
        });
        await _db.SaveChangesAsync();

        _lookupService.InvalidateCache();
        TempData["SuccessMessage"] = $"Lookup type '{type.Name}' {(type.IsActive ? "activated" : "deactivated")}.";
        return RedirectToPage();
    }

    public class LookupTypeViewModel
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public bool IsActive { get; set; }
        public int ValueCount { get; set; }
        public int TotalValueCount { get; set; }
    }
}
