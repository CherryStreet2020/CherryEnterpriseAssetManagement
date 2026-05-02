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
    public class TenantsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public TenantsModel(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        public List<Tenant> Tenants { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public int CurrentTenantId => _tenantContext.TenantId ?? 1;

        public async Task OnGetAsync()
        {
            Tenants = await _db.Tenants
                .Include(t => t.Companies)
                .OrderBy(t => t.Code)
                .ToListAsync();

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(string code, string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Code and Name are required.";
                return RedirectToPage();
            }

            if (await _db.Tenants.AnyAsync(t => t.Code.ToUpper() == code.ToUpper()))
            {
                TempData["Error"] = "A tenant with this code already exists.";
                return RedirectToPage();
            }

            var tenant = new Tenant
            {
                Code = code.ToUpper(),
                Name = name,
                Description = description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "System"
            };

            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Tenant '{code}' created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string code, string name, string? description, bool isActive)
        {
            var tenant = await _db.Tenants
                .Where(t => t.Id == id)
                .FirstOrDefaultAsync();
            if (tenant == null)
            {
                TempData["Error"] = "Tenant not found.";
                return RedirectToPage();
            }

            tenant.Code = code.ToUpper();
            tenant.Name = name;
            tenant.Description = description;
            tenant.IsActive = isActive;
            tenant.ModifiedAt = DateTime.UtcNow;
            tenant.ModifiedBy = User.Identity?.Name ?? "System";

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Tenant '{code}' updated successfully.";
            return RedirectToPage();
        }
    }
}
