using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Abs.FixedAssets.Services.Seeding.Pipelines;
using System.Text.Json;

namespace Abs.FixedAssets.Pages.Admin.Lookups;

[Authorize(Roles = "Admin")]
public class EditValuesModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILookupService _lookupService;

    public EditValuesModel(AppDbContext db, ITenantContext tenantContext, ILookupService lookupService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _lookupService = lookupService;
    }

    public LookupType? LookupType { get; set; }
    public List<LookupValue> Values { get; set; } = new();
    public HashSet<string> BaselineCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int typeId)
    {
        LookupType = await _db.LookupTypes
            .Where(lt => (lt.TenantId == _tenantContext.TenantId || lt.TenantId == null) && lt.Id == typeId)
            .FirstOrDefaultAsync();
        if (LookupType == null) return NotFound();

        Values = await _db.LookupValues
            .Where(lv => lv.LookupTypeId == typeId)
            .OrderBy(lv => lv.SortOrder)
            .ThenBy(lv => lv.Name)
            .ToListAsync();

        var baselines = LookupBaselineLoader.Load();
        var rule = baselines.FirstOrDefault(b => b.LookupKey.Equals(LookupType.Key, StringComparison.OrdinalIgnoreCase));
        if (rule != null)
            BaselineCodes = new HashSet<string>(rule.Values.Select(v => v.Code), StringComparer.OrdinalIgnoreCase);

        SuccessMessage = TempData["SuccessMessage"] as string;
        ErrorMessage = TempData["ErrorMessage"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostAddValueAsync(int typeId, string code, string name, int sortOrder, string? metadata)
    {
        var lookupType = await _db.LookupTypes
            .Where(lt => (lt.TenantId == _tenantContext.TenantId || lt.TenantId == null) && lt.Id == typeId)
            .FirstOrDefaultAsync();
        if (lookupType == null) return NotFound();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            TempData["ErrorMessage"] = "Code and Name are required.";
            return RedirectToPage(new { typeId });
        }

        var exists = await _db.LookupValues.AnyAsync(lv => lv.LookupTypeId == typeId && lv.Code == code);
        if (exists)
        {
            TempData["ErrorMessage"] = $"Value with code '{code}' already exists.";
            return RedirectToPage(new { typeId });
        }

        JsonDocument? metaDoc = null;
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            try { metaDoc = JsonDocument.Parse(metadata); }
            catch { TempData["ErrorMessage"] = "Invalid JSON in metadata."; return RedirectToPage(new { typeId }); }
        }

        var value = new LookupValue
        {
            LookupTypeId = typeId,
            Code = code,
            Name = name,
            SortOrder = sortOrder,
            IsActive = true,
            Metadata = metaDoc,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.LookupValues.Add(value);

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            EntityType = "LookupValue",
            EntityId = typeId,
            Action = "Created",
            Timestamp = DateTime.UtcNow,
            Username = User.Identity?.Name ?? "System",
            Description = $"Added lookup value: {code} ({name}) to {lookupType.Key}"
        });
        await _db.SaveChangesAsync();

        _lookupService.InvalidateCache();
        TempData["SuccessMessage"] = $"Value '{name}' added successfully.";
        return RedirectToPage(new { typeId });
    }

    public async Task<IActionResult> OnPostUpdateValueAsync(int typeId, int valueId, string name, int sortOrder, string? metadata)
    {
        var value = await _db.LookupValues.Include(v => v.LookupType).FirstOrDefaultAsync(v => v.Id == valueId);
        if (value == null) return NotFound();
        if (value.LookupType.TenantId != _tenantContext.TenantId) return Forbid();

        value.Name = name;
        value.SortOrder = sortOrder;
        value.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(metadata))
        {
            try { value.Metadata = JsonDocument.Parse(metadata); }
            catch { TempData["ErrorMessage"] = "Invalid JSON in metadata."; return RedirectToPage(new { typeId }); }
        }
        else
        {
            value.Metadata = null;
        }

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            EntityType = "LookupValue",
            EntityId = typeId,
            Action = "Updated",
            Timestamp = DateTime.UtcNow,
            Username = User.Identity?.Name ?? "System",
            Description = $"Updated lookup value: {value.Code} ({name})"
        });
        await _db.SaveChangesAsync();

        _lookupService.InvalidateCache();
        TempData["SuccessMessage"] = $"Value '{name}' updated.";
        return RedirectToPage(new { typeId });
    }

    public async Task<IActionResult> OnPostToggleValueActiveAsync(int typeId, int valueId)
    {
        var value = await _db.LookupValues.Include(v => v.LookupType).FirstOrDefaultAsync(v => v.Id == valueId);
        if (value == null) return NotFound();
        if (value.LookupType.TenantId != _tenantContext.TenantId) return Forbid();

        var baselines = LookupBaselineLoader.Load();
        var rule = baselines.FirstOrDefault(b => b.LookupKey.Equals(value.LookupType.Key, StringComparison.OrdinalIgnoreCase));
        bool isBaselineCode = rule?.Values.Any(v => v.Code.Equals(value.Code, StringComparison.OrdinalIgnoreCase)) == true;

        if (isBaselineCode && value.IsActive)
        {
            TempData["ErrorMessage"] = $"Cannot deactivate '{value.Name}' — it is a required baseline value.";
            return RedirectToPage(new { typeId });
        }

        if (value.LookupType.IsSystem && value.IsActive)
        {
            value.IsActive = false;
        }
        else
        {
            value.IsActive = !value.IsActive;
        }
        value.UpdatedAt = DateTime.UtcNow;

        await _db.AuditLogs.AddAsync(new AuditLog
        {
            EntityType = "LookupValue",
            EntityId = typeId,
            Action = value.IsActive ? "Activated" : "Deactivated",
            Timestamp = DateTime.UtcNow,
            Username = User.Identity?.Name ?? "System",
            Description = $"{(value.IsActive ? "Activated" : "Deactivated")} lookup value: {value.Code}"
        });
        await _db.SaveChangesAsync();

        _lookupService.InvalidateCache();
        TempData["SuccessMessage"] = $"Value '{value.Name}' {(value.IsActive ? "activated" : "deactivated")}.";
        return RedirectToPage(new { typeId });
    }
}
