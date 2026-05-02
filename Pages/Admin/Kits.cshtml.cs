using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class KitsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ITenantContext _tenantContext;

        public KitsModel(AppDbContext context, ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        public List<Kit> Kits { get; set; } = new();
        public List<ItemCategory> Categories { get; set; } = new();

        public async Task OnGetAsync()
        {
            Kits = await _context.Kits
                .Where(k => k.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(k.CompanyId ?? 0))
                .Include(k => k.Category)
                .Include(k => k.Items)
                .OrderBy(k => k.Name)
                .ToListAsync();

            Categories = await _context.ItemCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostAddAsync(string KitNumber, string Name, string? Description, int? CategoryId)
        {
            var kit = new Kit
            {
                KitNumber = KitNumber,
                Name = Name,
                Description = Description,
                CategoryId = CategoryId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CompanyId = _tenantContext.CompanyId
            };

            _context.Kits.Add(kit);
            await _context.SaveChangesAsync();

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var kit = await _context.Kits
                .Where(k => (k.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(k.CompanyId ?? 0)) && k.Id == id)
                .FirstOrDefaultAsync();
            if (kit != null)
            {
                _context.Kits.Remove(kit);
                await _context.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
