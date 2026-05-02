using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Abs.FixedAssets.Pages.Books
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;
        public CreateModel(AppDbContext context, ITenantContext tenantContext,
            IModuleGuardService moduleGuard) {
            _moduleGuard = moduleGuard; _context = context; _tenantContext = tenantContext; }

        [BindProperty] public Book Book { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Finance" });

     return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            var visibleIds = _tenantContext.VisibleCompanyIds;
            if (_tenantContext.CompanyId.HasValue && !visibleIds.Contains(_tenantContext.CompanyId.Value))
                return Forbid();
            Book.CompanyId = _tenantContext.CompanyId;
            _context.Books.Add(Book);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}
