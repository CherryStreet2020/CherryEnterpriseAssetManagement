using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;

namespace Abs.FixedAssets.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DepreciationBackfillModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly DepreciationBackfillService _backfill;

        public DepreciationBackfillModel(AppDbContext db, DepreciationBackfillService backfill)
        {
            _db = db;
            _backfill = backfill;
        }

        public int TotalAssets { get; set; }
        public int AssetsWithSettings { get; set; }
        public int AssetsWithDepreciation { get; set; }
        public int TotalBooks { get; set; }
        public int TotalBookGlMappings { get; set; }
        public decimal TotalAccumulatedDepreciation { get; set; }
        public decimal TotalAcquisitionCost { get; set; }

        public DepreciationBackfillReport? LastReport { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        [BindProperty]
        public DateTime AsOfDate { get; set; } = DateTime.UtcNow.Date;

        [BindProperty]
        public bool CreateMissingBooks { get; set; } = true;

        [BindProperty]
        public bool CreateMissingGlMappings { get; set; } = true;

        [BindProperty]
        public bool CreateMissingAssetBookSettings { get; set; } = true;

        [BindProperty]
        public bool ComputeHistoricDepreciation { get; set; } = true;

        public async Task OnGetAsync()
        {
            await LoadStatsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                LastReport = await _backfill.RunAsync(
                    asOfDate: AsOfDate,
                    createMissingBooks: CreateMissingBooks,
                    createMissingGlMappings: CreateMissingGlMappings,
                    createMissingAssetBookSettings: CreateMissingAssetBookSettings,
                    computeHistoricDepreciation: ComputeHistoricDepreciation,
                    actor: User.Identity?.Name ?? "admin");

                SuccessMessage = $"Backfill complete in {LastReport.Duration.TotalSeconds:0.0}s. " +
                                 $"Created {LastReport.GaapBooksCreated} books, {LastReport.BookGlMappingsCreated} GL mappings, " +
                                 $"{LastReport.AssetBookSettingsCreated} settings rows. Recomputed {LastReport.AssetsRecomputed} assets " +
                                 $"with ${LastReport.TotalAccumulatedDepreciationStamped:N0} total accumulated depreciation.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Backfill failed: {ex.Message}";
            }

            await LoadStatsAsync();
            return Page();
        }

        private async Task LoadStatsAsync()
        {
            TotalAssets = await _db.Assets.CountAsync(a => a.Active);
            TotalBooks = await _db.Books.CountAsync();
            TotalBookGlMappings = await _db.BookGlAccounts.CountAsync();
            AssetsWithSettings = await _db.AssetBookSettings.Select(s => s.AssetId).Distinct().CountAsync();
            AssetsWithDepreciation = await _db.Assets.CountAsync(a => a.AccumulatedDepreciation > 0);
            TotalAccumulatedDepreciation = await _db.Assets.SumAsync(a => (decimal?)a.AccumulatedDepreciation) ?? 0;
            TotalAcquisitionCost = await _db.Assets.SumAsync(a => (decimal?)a.AcquisitionCost) ?? 0;
        }
    }
}
