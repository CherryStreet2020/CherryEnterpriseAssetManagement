using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
    public class GlAccountsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public GlAccountsModel(AppDbContext db, ITenantContext tenantContext,
            IModuleGuardService moduleGuard) {
            _moduleGuard = moduleGuard; _db = db; _tenantContext = tenantContext; }

        [BindProperty(SupportsGet = true)]
        public int BookId { get; set; }

        public Book? Book { get; private set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<GlAccount> AssetAccounts { get; set; } = new();
        public List<GlAccount> AccumDepAccounts { get; set; } = new();
        public List<GlAccount> ExpenseAccounts { get; set; } = new();
        public List<GlAccount> CipAccounts { get; set; } = new();
        public List<GlAccount> GainAccounts { get; set; } = new();
        public List<GlAccount> LossAccounts { get; set; } = new();
        public List<GlAccount> ClearingAccounts { get; set; } = new();

        public class InputModel
        {
            [Display(Name = "Fixed Asset Account")]
            public int? AssetAccountId { get; set; }

            [Display(Name = "Accumulated Depreciation")]
            public int? AccumDepAccountId { get; set; }

            [Display(Name = "Depreciation Expense")]
            public int? DepExpAccountId { get; set; }

            [Display(Name = "CIP Account")]
            public int? CipAccountId { get; set; }

            [Display(Name = "Gain on Disposal")]
            public int? GainAccountId { get; set; }

            [Display(Name = "Loss on Disposal")]
            public int? LossAccountId { get; set; }

            [Display(Name = "Clearing Account")]
            public int? ClearingAccountId { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Finance" });

            Book = await _db.Books.Where(b => b.Id == BookId && _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0)).OrderBy(b => b.Id).FirstOrDefaultAsync();
            if (Book == null) return NotFound();

            await LoadAccountLists();

            var existing = await _db.BookGlAccounts.AsNoTracking()
                              .Where(x => x.BookId == BookId).OrderBy(x => x.Id).FirstOrDefaultAsync();

            if (existing != null)
            {
                Input = new InputModel
                {
                    AssetAccountId = ParseAccountId(existing.Asset),
                    AccumDepAccountId = ParseAccountId(existing.AccumulatedDepreciation),
                    DepExpAccountId = ParseAccountId(existing.DepreciationExpense),
                    CipAccountId = ParseAccountId(existing.CIP),
                    GainAccountId = ParseAccountId(existing.GainOnDisposal),
                    LossAccountId = ParseAccountId(existing.LossOnDisposal),
                    ClearingAccountId = ParseAccountId(existing.Clearing)
                };
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Book = await _db.Books.Where(b => b.Id == BookId && _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0)).OrderBy(b => b.Id).FirstOrDefaultAsync();
            if (Book == null) return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadAccountLists();
                return Page();
            }

            var existing = await _db.BookGlAccounts
                                    .FirstOrDefaultAsync(x => x.BookId == BookId);

            if (existing == null)
            {
                existing = new BookGlAccount
                {
                    BookId = BookId
                };
                _db.BookGlAccounts.Add(existing);
            }

            existing.Asset = await GetAccountNumber(Input.AssetAccountId);
            existing.AccumulatedDepreciation = await GetAccountNumber(Input.AccumDepAccountId);
            existing.DepreciationExpense = await GetAccountNumber(Input.DepExpAccountId);
            existing.GainOnDisposal = await GetAccountNumber(Input.GainAccountId);
            existing.LossOnDisposal = await GetAccountNumber(Input.LossAccountId);
            existing.Clearing = await GetAccountNumber(Input.ClearingAccountId);
            existing.CIP = await GetAccountNumber(Input.CipAccountId);

            await _db.SaveChangesAsync();

            TempData["Status"] = "GL Accounts saved successfully.";
            return RedirectToPage("./Edit", new { id = BookId });
        }

        private async Task LoadAccountLists()
        {
            var allAccounts = await _db.GlAccounts
                .Where(a => a.IsActive)
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();

            AssetAccounts = allAccounts
                .Where(a => a.AccountType == GlAccountType.Asset)
                .ToList();

            AccumDepAccounts = allAccounts
                .Where(a => a.AccountType == GlAccountType.ContraAsset ||
                           a.Category == GlAccountCategory.AccumulatedDepreciation)
                .ToList();

            ExpenseAccounts = allAccounts
                .Where(a => a.AccountType == GlAccountType.Expense ||
                           a.Category == GlAccountCategory.DepreciationExpense)
                .ToList();

            CipAccounts = allAccounts
                .Where(a => a.AccountType == GlAccountType.Asset &&
                           (a.Category == GlAccountCategory.WorkInProgress ||
                            a.Name.Contains("CIP") ||
                            a.Name.Contains("Construction")))
                .ToList();

            GainAccounts = allAccounts
                .Where(a => a.AccountType == GlAccountType.Revenue ||
                           a.Category == GlAccountCategory.RevenueAndGains)
                .ToList();

            LossAccounts = allAccounts
                .Where(a => a.AccountType == GlAccountType.Expense ||
                           a.Category == GlAccountCategory.AssetLosses)
                .ToList();

            ClearingAccounts = allAccounts
                .Where(a => a.AccountType == GlAccountType.Asset ||
                           a.AccountType == GlAccountType.Liability)
                .ToList();
        }

        private int? ParseAccountId(string? accountNumber)
        {
            if (string.IsNullOrEmpty(accountNumber)) return null;

            var account = _db.GlAccounts
                .Where(a => a.AccountNumber == accountNumber)
                .OrderBy(a => a.Id)
                .FirstOrDefault();

            return account?.Id;
        }

        private async Task<string?> GetAccountNumber(int? accountId)
        {
            if (!accountId.HasValue) return null;

            var account = await _db.GlAccounts
                .Where(a => a.Id == accountId.Value)
                .OrderBy(a => a.Id)
                .FirstOrDefaultAsync();

            return account?.AccountNumber;
        }
    }
}
