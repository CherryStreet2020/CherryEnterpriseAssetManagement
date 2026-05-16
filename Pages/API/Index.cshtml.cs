using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.API;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApiService _apiService;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;
    private readonly AppDbContext _db;

    public IndexModel(ApiService apiService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard, AppDbContext db)
    {
            _moduleGuard = moduleGuard;
        _apiService = apiService;
        _tenantContext = tenantContext;
        _db = db;
    }

    public List<ApiKey> ApiKeys { get; set; } = new();
    public string? NewKeyValue { get; set; }
    // PR #101: company-scope dropdown options for new keys. Empty value =
    // null = "every company visible to this tenant". Specific value =
    // restrict the key to that single company.
    public List<SelectListItem> CompanyOptions { get; set; } = new();
    public Dictionary<int, string> CompanyNames { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("api"))
                return RedirectToPage("/ModuleDisabled", new { module = "API" });


        ApiKeys = await _apiService.GetAllKeysAsync();
        await LoadCompanyOptionsAsync();

            return Page();
        }

    public async Task<IActionResult> OnPostCreateKeyAsync(string keyName, int? companyId)
    {
        // PR #101: bind the new key to the issuing admin's tenant. If the
        // tenant context has not been resolved we refuse to issue rather
        // than fall back to TenantId == 0 (which is the pre-#101 sentinel
        // that gets rejected at validation time).
        if (!_tenantContext.TenantId.HasValue)
        {
            ModelState.AddModelError(string.Empty, "Unable to resolve current tenant; cannot issue an API key.");
            ApiKeys = await _apiService.GetAllKeysAsync();
            await LoadCompanyOptionsAsync();
            return Page();
        }

        var (key, rawKey) = await _apiService.CreateApiKeyAsync(
            keyName,
            tenantId: _tenantContext.TenantId.Value,
            companyId: companyId,
            createdBy: User.Identity?.Name);
        NewKeyValue = rawKey;
        ApiKeys = await _apiService.GetAllKeysAsync();
        await LoadCompanyOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeKeyAsync(int keyId)
    {
        await _apiService.RevokeKeyAsync(keyId);
        return RedirectToPage();
    }

    private async Task LoadCompanyOptionsAsync()
    {
        if (!_tenantContext.TenantId.HasValue)
        {
            CompanyOptions = new List<SelectListItem>();
            return;
        }
        var tenantId = _tenantContext.TenantId.Value;
        var companies = await _db.Companies
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        CompanyNames = companies.ToDictionary(c => c.Id, c => c.Name);
        CompanyOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "", Text = "All companies visible to this tenant" }
        };
        CompanyOptions.AddRange(companies.Select(c => new SelectListItem
        {
            Value = c.Id.ToString(),
            Text = c.Name
        }));
    }
}
