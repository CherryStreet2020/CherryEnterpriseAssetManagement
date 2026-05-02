using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Books
{
    public class EditModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public EditModel(AppDbContext context, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _context = context;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        [BindProperty]
        public Book Book { get; set; } = default!;

        public List<PolicyOption> Policies { get; set; } = new();
        public List<SelectListItem> BookTypeOptions { get; set; } = new();
        public List<SelectListItem> DepreciationMethodOptions { get; set; } = new();
        public List<SelectListItem> DepreciationConventionOptions { get; set; } = new();
        public List<SelectListItem> TaxJurisdictionOptions { get; set; } = new();
        public List<SelectListItem> CalculationFrequencyOptions { get; set; } = new();

        public class PolicyOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }


        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Finance" });

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var entity = await _context.Books
                .Include(b => b.DefaultPolicy)
                .Include(b => b.Company)
                .FirstOrDefaultAsync(b => b.Id == id && visibleIds.Contains(b.CompanyId ?? 0));

            if (entity == null)
                return NotFound();

            Book = entity;

            await LoadLookups();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadLookups();
                return Page();
            }

            if (Book.BookTypeLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, Book.BookTypeLookupValueId.Value);
                if (lv != null && int.TryParse(lv.Code, out var enumVal))
                    Book.BookType = (BookType)enumVal;
            }

            if (Book.MethodLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, Book.MethodLookupValueId.Value);
                if (lv != null && int.TryParse(lv.Code, out var enumVal))
                    Book.Method = (DepreciationMethod)enumVal;
            }

            if (Book.ConventionLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, Book.ConventionLookupValueId.Value);
                if (lv != null && int.TryParse(lv.Code, out var enumVal))
                    Book.Convention = (DepreciationConvention)enumVal;
            }

            if (Book.TaxJurisdictionLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, Book.TaxJurisdictionLookupValueId.Value);
                if (lv != null && int.TryParse(lv.Code, out var enumVal))
                    Book.TaxJurisdiction = (TaxJurisdiction)enumVal;
            }

            if (Book.FrequencyLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, Book.FrequencyLookupValueId.Value);
                if (lv != null && int.TryParse(lv.Code, out var enumVal))
                    Book.CalculationFrequency = (DepreciationFrequency)enumVal;
            }

            var visibleIds = _tenantContext.VisibleCompanyIds;
            var existing = await _context.Books.FirstOrDefaultAsync(b => b.Id == Book.Id && visibleIds.Contains(b.CompanyId ?? 0));
            if (existing == null) return NotFound();

            Book.CompanyId = existing.CompanyId;

            _context.Entry(existing).State = EntityState.Detached;
            _context.Attach(Book).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            TempData["Status"] = "Book saved successfully.";
            return RedirectToPage("./Index");
        }

        private async Task LoadLookups()
        {
            Policies = await _context.DepreciationPolicies
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new PolicyOption { Id = p.Id, Name = p.Name })
                .ToListAsync();

            BookTypeOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "BookType", Book.BookTypeLookupValueId, "");
            DepreciationMethodOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "DepreciationMethod", Book.MethodLookupValueId, "");
            DepreciationConventionOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "DepreciationConvention", Book.ConventionLookupValueId, "");
            TaxJurisdictionOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "TaxJurisdiction", Book.TaxJurisdictionLookupValueId, "");
            CalculationFrequencyOptions = await _lookupService.GetSelectListByIdAsync(_tenantContext.TenantId, _tenantContext.CompanyId, "DepreciationFrequency", Book.FrequencyLookupValueId, "");
        }
    }
}
