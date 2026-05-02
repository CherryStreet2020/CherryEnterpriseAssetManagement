using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.API;

[Authorize(Roles = "Admin,Accountant")]
public class ImportModel : PageModel
{
    private readonly ImportService _importService;
    private readonly ITenantContext _tenantContext;
    private readonly IModuleGuardService _moduleGuard;

    public ImportModel(ImportService importService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
    {
            _moduleGuard = moduleGuard;
        _importService = importService;
        _tenantContext = tenantContext;
    }

    public ImportResult? Result { get; set; }

    public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("api"))
                return RedirectToPage("/ModuleDisabled", new { module = "API" });


        return Page();
        }

    public async Task<IActionResult> OnPostAsync(IFormFile csvFile)
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            Result = new ImportResult { Errors = { "Please select a CSV file" } };
            return Page();
        }

        using var stream = csvFile.OpenReadStream();
        Result = await _importService.ImportAssetsFromCsvAsync(stream);

        return Page();
    }
}
