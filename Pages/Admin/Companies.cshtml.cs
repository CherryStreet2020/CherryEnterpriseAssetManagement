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
    public class CompaniesModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;

        public CompaniesModel(AppDbContext db, ITenantContext tenantContext)
        {
            _db = db;
            _tenantContext = tenantContext;
        }

        public List<Company> Companies { get; set; } = new();
        public List<Tenant> Tenants { get; set; } = new();
        public Dictionary<int, int> CompanySiteCounts { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public int CurrentCompanyId => _tenantContext.CompanyId ?? 1;

        public async Task OnGetAsync()
        {
            Companies = await _db.Companies
                .Include(c => c.Tenant)
                .Include(c => c.ParentCompany)
                .OrderBy(c => c.TenantId)
                .ThenBy(c => c.CompanyCode)
                .ToListAsync();

            var ordered = new List<Company>();
            void AddWithChildren(int? parentId, int depth)
            {
                foreach (var c in Companies.Where(c => c.ParentCompanyId == parentId).OrderBy(c => c.CompanyCode))
                {
                    c.HierarchyLevel = depth;
                    ordered.Add(c);
                    AddWithChildren(c.Id, depth + 1);
                }
            }
            AddWithChildren(null, 0);
            var orphans = Companies.Where(c => !ordered.Contains(c)).ToList();
            foreach (var o in orphans) o.HierarchyLevel = 0;
            ordered.AddRange(orphans);
            Companies = ordered;

            Tenants = await _db.Tenants
                .Where(t => t.IsActive)
                .OrderBy(t => t.Code)
                .ToListAsync();

            var siteCounts = await _db.Sites
                .GroupBy(s => s.CompanyId)
                .Select(g => new { CompanyId = g.Key, Count = g.Count() })
                .ToListAsync();
            CompanySiteCounts = siteCounts.ToDictionary(x => x.CompanyId, x => x.Count);

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(string companyCode, string name, int? tenantId, string? currency, string? taxId, string? address, int? parentCompanyId)
        {
            if (string.IsNullOrWhiteSpace(companyCode) || string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Code and Name are required.";
                return RedirectToPage();
            }

            if (await _db.Companies.AnyAsync(c => c.CompanyCode != null && c.CompanyCode.ToUpper() == companyCode.ToUpper()))
            {
                TempData["Error"] = "A company with this code already exists.";
                return RedirectToPage();
            }

            var company = new Company
            {
                CompanyCode = companyCode.ToUpper(),
                Name = name,
                TenantId = tenantId ?? _tenantContext.TenantId ?? 1,
                ParentCompanyId = parentCompanyId,
                Currency = currency ?? "USD",
                TaxId = taxId,
                Address = address,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Companies.Add(company);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Company '{companyCode}' created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string companyCode, string name, int? tenantId, string? currency, string? taxId, string? address, bool isActive, int? parentCompanyId)
        {
            var company = await _db.Companies
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync();
            if (company == null)
            {
                TempData["Error"] = "Company not found.";
                return RedirectToPage();
            }

            company.CompanyCode = companyCode.ToUpper();
            company.Name = name;
            company.TenantId = tenantId ?? _tenantContext.TenantId ?? 1;
            company.ParentCompanyId = parentCompanyId;
            company.Currency = currency ?? "USD";
            company.TaxId = taxId;
            company.Address = address;
            company.IsActive = isActive;
            company.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Company '{companyCode}' updated successfully.";
            return RedirectToPage();
        }
    }
}
