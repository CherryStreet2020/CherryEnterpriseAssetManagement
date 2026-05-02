using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.API;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ApiService _apiService;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;

    public IndexModel(ApiService apiService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
    {
            _moduleGuard = moduleGuard;
        _apiService = apiService;
        _tenantContext = tenantContext;
    }

    public List<ApiKey> ApiKeys { get; set; } = new();
    public string? NewKeyValue { get; set; }

    public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("api"))
                return RedirectToPage("/ModuleDisabled", new { module = "API" });


        ApiKeys = await _apiService.GetAllKeysAsync();
    
            return Page();
        }

    public async Task<IActionResult> OnPostCreateKeyAsync(string keyName)
    {
        var (key, rawKey) = await _apiService.CreateApiKeyAsync(keyName, User.Identity?.Name);
        NewKeyValue = rawKey;
        ApiKeys = await _apiService.GetAllKeysAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeKeyAsync(int keyId)
    {
        await _apiService.RevokeKeyAsync(keyId);
        return RedirectToPage();
    }
}
