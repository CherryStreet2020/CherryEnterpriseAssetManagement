using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin,Accountant,Viewer")]
    public class VendorsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public VendorsModel(AppDbContext db, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _db = db;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public List<Vendor> Vendors { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("vendors"))
                return RedirectToPage("/ModuleDisabled", new { module = "Vendor Management" });

            Vendors = await _db.Vendors
                .Where(v => v.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(v.CompanyId ?? 0))
                .OrderBy(v => v.SortOrder)
                .ThenBy(v => v.Code)
                .ToListAsync();

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
            
            return Page();
        }
    }
}
