using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class CcaBackfillModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly CcaBackfillService _backfill;

        public CcaBackfillModel(AppDbContext db, CcaBackfillService backfill)
        {
            _db = db;
            _backfill = backfill;
        }

        public class CompanyOption
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string CompanyCode { get; set; } = string.Empty;
        }

        public class CcaClassOption
        {
            public int Id { get; set; }
            public int ClassNumber { get; set; }
            public string ShortDescription { get; set; } = string.Empty;
        }

        public List<CompanyOption> CanadianCompanies { get; set; } = new();
        public List<CcaClassOption> CcaClassOptions { get; set; } = new();
        public List<CcaBackfillPreviewRow> PreviewRows { get; set; } = new();
        public string SelectedCompanyName { get; set; } = string.Empty;
        public int ExistingBalanceCount { get; set; }
        public decimal ExistingTotalCcaClaimed { get; set; }

        public CcaBackfillReport? LastReport { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CompanyId { get; set; }

        [BindProperty]
        public int ThroughFiscalYear { get; set; } = DateTime.UtcNow.Year;

        [BindProperty]
        public bool CreateMissingTaxSettings { get; set; } = true;

        [BindProperty]
        public bool ComputeBalances { get; set; } = true;

        [BindProperty]
        public Dictionary<int, int> OverrideClassByAssetId { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadDropdownsAsync();
            if (CompanyId == 0 && CanadianCompanies.Count > 0)
                CompanyId = CanadianCompanies[0].Id;
            await LoadPreviewAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDropdownsAsync();

            if (CompanyId == 0)
            {
                ErrorMessage = "Pick a Canadian company first.";
                await LoadPreviewAsync();
                return Page();
            }

            try
            {
                LastReport = await _backfill.RunAsync(
                    companyId: CompanyId,
                    throughFiscalYear: ThroughFiscalYear,
                    overrideClassByAssetId: OverrideClassByAssetId,
                    createMissingTaxSettings: CreateMissingTaxSettings,
                    computeBalances: ComputeBalances,
                    actor: User.Identity?.Name ?? "admin");

                SuccessMessage = $"Backfill complete in {LastReport.Duration.TotalSeconds:0.0}s. " +
                                 $"Mapped {LastReport.AssetsMapped} new assets " +
                                 $"({LastReport.AssetsAlreadyMapped} already mapped). " +
                                 $"Computed {LastReport.ClassYearsComputed} (class, year) balances " +
                                 $"totalling ${LastReport.TotalCcaClaimed:N0} CCA claimed.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Backfill failed: {ex.Message}";
            }

            await LoadPreviewAsync();
            return Page();
        }

        private async Task LoadDropdownsAsync()
        {
            CanadianCompanies = await _db.Companies
                .Where(c => c.IsActive && c.Country != null && c.Country.ToUpper().Contains("CANADA"))
                .OrderBy(c => c.Name)
                .Select(c => new CompanyOption { Id = c.Id, Name = c.Name, CompanyCode = c.CompanyCode ?? "" })
                .ToListAsync();

            var classes = await _db.CcaClasses.AsNoTracking()
                .Where(c => c.Active)
                .OrderBy(c => c.ClassNumber)
                .ToListAsync();
            CcaClassOptions = classes.Select(c => new CcaClassOption
            {
                Id = c.Id,
                ClassNumber = c.ClassNumber,
                ShortDescription = TruncateForDropdown(c.Description)
            }).ToList();
        }

        private async Task LoadPreviewAsync()
        {
            if (CompanyId == 0) return;

            var company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == CompanyId);
            SelectedCompanyName = company?.Name ?? $"Company {CompanyId}";

            PreviewRows = await _backfill.GetPreviewAsync(CompanyId);

            // CcaClassBalance is now company-scoped — filter directly.
            var balances = await _db.CcaClassBalances.AsNoTracking()
                .Where(b => b.CompanyId == CompanyId)
                .ToListAsync();
            ExistingBalanceCount = balances.Count;
            ExistingTotalCcaClaimed = balances.Sum(b => b.CcaClaimed);
        }

        private static string TruncateForDropdown(string desc)
        {
            if (string.IsNullOrEmpty(desc)) return string.Empty;
            return desc.Length > 60 ? desc.Substring(0, 57) + "…" : desc;
        }
    }
}
