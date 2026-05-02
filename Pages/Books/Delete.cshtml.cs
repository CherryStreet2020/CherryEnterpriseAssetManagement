using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Books
{
    public class DeleteModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;
        public DeleteModel(AppDbContext context, ITenantContext tenantContext,
            IModuleGuardService moduleGuard) {
            _moduleGuard = moduleGuard; _context = context; _tenantContext = tenantContext; }

        [BindProperty] public Book Book { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Finance" });

            var entity = await _context.Books.AsNoTracking().Where(b => b.Id == id && _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0)).OrderBy(b => b.Id).FirstOrDefaultAsync();
            if (entity == null) return NotFound();
            Book = entity;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var entity = await _context.Books.Where(b => b.Id == Book.Id && _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0)).FirstOrDefaultAsync();
            if (entity == null) return RedirectToPage("./Index");
            _context.Books.Remove(entity);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}
