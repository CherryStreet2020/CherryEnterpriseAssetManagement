using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class ItemCategoriesModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public ItemCategoriesModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<ItemCategory> Categories { get; set; } = new();
        public List<GlAccount> GlAccounts { get; set; } = new();
        public List<SelectListItem> ActiveInactiveOptions { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            ActiveInactiveOptions = await _lookupService.GetSelectListAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "ActiveInactive", null, "");
            Categories = await _db.ItemCategories
                .Include(c => c.ParentCategory)
                .Include(c => c.DefaultGlAccount)
                .Include(c => c.ExpenseGlAccount)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();

            GlAccounts = await _db.GlAccounts
                .OrderBy(g => g.AccountNumber)
                .ToListAsync();

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(
            string code, string name, string? description,
            int? parentCategoryId, int? defaultGlAccountId, int? expenseGlAccountId)
        {
            if (await _db.ItemCategories.AnyAsync(c => c.Code == code))
            {
                TempData["Error"] = "A category with this code already exists.";
                return RedirectToPage();
            }

            var maxSortOrder = await _db.ItemCategories.MaxAsync(c => (int?)c.SortOrder) ?? 0;

            var category = new ItemCategory
            {
                Code = code,
                Name = name,
                Description = description,
                ParentCategoryId = parentCategoryId,
                DefaultGlAccountId = defaultGlAccountId,
                ExpenseGlAccountId = expenseGlAccountId,
                IsActive = true,
                SortOrder = maxSortOrder + 10
            };

            _db.ItemCategories.Add(category);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Category {code} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(
            int id, string code, string name, string? description,
            int? parentCategoryId, int? defaultGlAccountId, int? expenseGlAccountId, bool isActive)
        {
            var category = await _db.ItemCategories
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync();
            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToPage();
            }

            if (parentCategoryId == id)
            {
                TempData["Error"] = "A category cannot be its own parent.";
                return RedirectToPage();
            }

            category.Code = code;
            category.Name = name;
            category.Description = description;
            category.ParentCategoryId = parentCategoryId;
            category.DefaultGlAccountId = defaultGlAccountId;
            category.ExpenseGlAccountId = expenseGlAccountId;
            category.IsActive = isActive;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Category {code} updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var category = await _db.ItemCategories
                .Include(c => c.ChildCategories)
                .Include(c => c.Items)
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync();

            if (category == null)
            {
                TempData["Error"] = "Category not found.";
                return RedirectToPage();
            }

            if (category.ChildCategories?.Any() == true)
            {
                TempData["Error"] = "Cannot delete a category that has child categories. Please delete or reassign child categories first.";
                return RedirectToPage();
            }

            if (category.Items?.Any() == true)
            {
                TempData["Error"] = "Cannot delete a category that has items. Please reassign items to another category first.";
                return RedirectToPage();
            }

            var code = category.Code;
            _db.ItemCategories.Remove(category);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Category {code} deleted successfully.";
            return RedirectToPage();
        }
    }
}
