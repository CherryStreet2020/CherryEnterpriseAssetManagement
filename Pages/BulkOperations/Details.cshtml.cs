using System.Collections.Generic;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.BulkOperations
{
    [Authorize(Policy = "AccountantOrAdmin")]
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;

        public DetailsModel(AppDbContext context,
            IModuleGuardService moduleGuard,
            ITenantContext tenantContext)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _tenantContext = tenantContext;
        }

        public BulkOperation Operation { get; set; } = null!;
        public List<Asset> AffectedAssets { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Bulk Operations" });

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var operation = await _context.Set<BulkOperation>().Where(o => o.Id == id && visibleIds.Contains(o.CompanyId ?? 0)).OrderBy(o => o.Id).FirstOrDefaultAsync();
            
            if (operation == null)
            {
                return NotFound();
            }

            Operation = operation;

            if (!string.IsNullOrEmpty(operation.AssetIds))
            {
                var assetIds = operation.AssetIds.Split(',')
                    .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                    .Where(id => id > 0)
                    .ToList();

                AffectedAssets = await _context.Assets
                    .Where(a => assetIds.Contains(a.Id) && visibleIds.Contains(a.CompanyId ?? 0))
                    .OrderBy(a => a.AssetNumber)
                    .ToListAsync();
            }

            ViewData["ReturnUrl"] = ReturnUrl;
            ViewData["Breadcrumbs"] = new List<(string Label, string Href)>
            {
                ("Assets", "/BulkOperations"),
                ("Bulk Operations", "/BulkOperations"),
                ("Detail", "")
            };
            ViewData["ShowBackLink"] = true;
            ViewData["BackLinkFallback"] = "/BulkOperations";
            ViewData["BackLinkLabel"] = "Back to results";

            return Page();
        }
    }
}
