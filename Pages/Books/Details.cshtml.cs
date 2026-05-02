using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Books
{
    public class DetailsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        public DetailsModel(AppDbContext context, IModuleGuardService moduleGuard, ITenantContext tenantContext)
        {
            _context = context;
            _moduleGuard = moduleGuard;
            _tenantContext = tenantContext;
        }

        public Book Book { get; private set; } = default!;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Finance" });

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var entity = await _context.Books.Where(b => b.Id == id && visibleIds.Contains(b.CompanyId ?? 0)).OrderBy(b => b.Id).FirstOrDefaultAsync();
            if (entity == null) return NotFound();
            Book = entity;

            ViewData["ReturnUrl"] = ReturnUrl;
            ViewData["Breadcrumbs"] = new List<(string Label, string Href)>
            {
                ("Finance", "/Books"),
                ("Books", "/Books"),
                ("Detail", "")
            };
            ViewData["ShowBackLink"] = true;
            ViewData["BackLinkFallback"] = "/Books";
            ViewData["BackLinkLabel"] = "Back to results";

            return Page();
        }
    }
}