using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Assets
{
    public class DeleteModel : PageModel
    {
        private readonly Abs.FixedAssets.Data.AppDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public DeleteModel(Abs.FixedAssets.Data.AppDbContext context, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _tenantContext = tenantContext;
        }

        [BindProperty]
        public Asset Asset { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Assets" });

            if (id == null)
            {
                return NotFound();
            }

            var asset = await _context.Assets.Where(m => m.Id == id && _tenantContext.VisibleCompanyIds.Contains(m.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || m.SiteId == _tenantContext.SiteId.Value)).OrderBy(m => m.Id).FirstOrDefaultAsync();

            if (asset is not null)
            {
                Asset = asset;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var asset = await _context.Assets.Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).FirstOrDefaultAsync();
            if (asset != null)
            {
                Asset = asset;
                _context.Assets.Remove(Asset);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
