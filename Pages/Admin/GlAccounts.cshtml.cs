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
    public class GlAccountsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;

        public GlAccountsModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext)
        {
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public List<GlAccount> GlAccounts { get; set; } = new();
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        public int? CategoryFilter { get; set; }
        public int? TypeFilter { get; set; }
        public List<SelectListItem> NormalBalanceOptions { get; set; } = new();
        public List<SelectListItem> ActiveInactiveOptions { get; set; } = new();
        public List<SelectListItem> AccountTypeOptions { get; set; } = new();

        public async Task OnGetAsync(int? category, int? type)
        {
            CategoryFilter = category;
            TypeFilter = type;

            var query = _db.GlAccounts
                .Where(a => a.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0));

            if (category.HasValue)
                query = query.Where(a => (int)a.Category == category.Value);

            if (type.HasValue)
                query = query.Where(a => (int)a.AccountType == type.Value);

            GlAccounts = await query.OrderBy(a => a.AccountNumber).ToListAsync();

            NormalBalanceOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, null, "GlNormalBalance", null, "-- Select --");
            ActiveInactiveOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, null, "ActiveInactive", null, "");
            AccountTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, null, "GlAccountType", null, "All Types");

            SuccessMessage = TempData["Success"]?.ToString();
            ErrorMessage = TempData["Error"]?.ToString();
        }

        public async Task<IActionResult> OnPostCreateAsync(string accountNumber, string name, string? description, int accountTypeLookupValueId, int category, int normalBalance)
        {
            if (await _db.GlAccounts.AnyAsync(a => a.AccountNumber == accountNumber))
            {
                TempData["Error"] = "An account with this number already exists.";
                return RedirectToPage();
            }

            var resolvedAccountType = GlAccountType.Asset;
            int? resolvedAccountTypeLvId = accountTypeLookupValueId > 0 ? accountTypeLookupValueId : (int?)null;
            var accountTypeLv = await _lookupService.GetValueByIdAsync(null, null, accountTypeLookupValueId);
            if (accountTypeLv != null)
            {
                resolvedAccountTypeLvId = accountTypeLv.Id;
                if (int.TryParse(accountTypeLv.Code, out var enumVal))
                    resolvedAccountType = (GlAccountType)enumVal;
            }

            var account = new GlAccount
            {
                AccountNumber = accountNumber,
                Name = name,
                Description = description,
                AccountType = resolvedAccountType,
                AccountTypeLookupValueId = resolvedAccountTypeLvId,
                Category = (GlAccountCategory)category,
                NormalBalance = (NormalBalance)normalBalance,
                IsActive = true,
                CompanyId = _tenantContext.CompanyId ?? 1
            };

            _db.GlAccounts.Add(account);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"GL Account {accountNumber} created successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateAsync(int id, string accountNumber, string name, string? description, int accountTypeLookupValueId, int category, int normalBalance, bool isActive)
        {
            var account = await _db.GlAccounts
                .Where(a => (a.CompanyId == null || _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0)) && a.Id == id)
                .FirstOrDefaultAsync();
            if (account == null)
            {
                TempData["Error"] = "Account not found.";
                return RedirectToPage();
            }

            var resolvedAccountType = GlAccountType.Asset;
            int? resolvedAccountTypeLvId = accountTypeLookupValueId > 0 ? accountTypeLookupValueId : (int?)null;
            var accountTypeLv = await _lookupService.GetValueByIdAsync(null, null, accountTypeLookupValueId);
            if (accountTypeLv != null)
            {
                resolvedAccountTypeLvId = accountTypeLv.Id;
                if (int.TryParse(accountTypeLv.Code, out var enumVal))
                    resolvedAccountType = (GlAccountType)enumVal;
            }

            account.AccountNumber = accountNumber;
            account.Name = name;
            account.Description = description;
            account.AccountType = resolvedAccountType;
            account.AccountTypeLookupValueId = resolvedAccountTypeLvId;
            account.Category = (GlAccountCategory)category;
            account.NormalBalance = (NormalBalance)normalBalance;
            account.IsActive = isActive;
            account.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"GL Account {accountNumber} updated successfully.";
            return RedirectToPage();
        }
    }
}
