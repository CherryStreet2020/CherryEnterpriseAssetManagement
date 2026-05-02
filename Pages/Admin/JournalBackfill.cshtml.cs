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
    public class JournalBackfillModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly HistoricJournalBackfillService _backfill;

        public JournalBackfillModel(AppDbContext db, HistoricJournalBackfillService backfill)
        {
            _db = db;
            _backfill = backfill;
        }

        public int TotalBooks { get; set; }
        public int ExistingDepJournals { get; set; }
        public decimal TotalAccumulatedDepreciation { get; set; }
        public decimal PostedDebit { get; set; }
        public decimal PostedCredit { get; set; }

        public HistoricJournalBackfillReport? Preview { get; set; }
        public HistoricJournalBackfillReport? LastReport { get; set; }
        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        [BindProperty]
        public DateTime AsOfDate { get; set; } = PreviousMonthEnd(DateTime.UtcNow.Date);

        public async Task OnGetAsync()
        {
            await LoadStatsAsync();
        }

        public async Task<IActionResult> OnPostAsync(string action)
        {
            try
            {
                if (string.Equals(action, "preview", StringComparison.OrdinalIgnoreCase))
                {
                    Preview = await _backfill.PreviewAsync(AsOfDate);
                    var totalToCreate = Preview.PerBook.Sum(p => p.MonthsToCreate);
                    SuccessMessage = $"Preview complete in {Preview.Duration.TotalSeconds:0.0}s. Would create {totalToCreate:N0} journal entries across {Preview.BooksScanned} books (no changes made).";
                }
                else
                {
                    LastReport = await _backfill.RunAsync(AsOfDate);
                    if (LastReport.Aborted)
                    {
                        ErrorMessage = $"Backfill ABORTED — entire sweep rolled back. {string.Join(" | ", LastReport.Errors)}";
                    }
                    else
                    {
                        var balanced = LastReport.TotalDebit == LastReport.TotalCredit ? "balanced" : "UNBALANCED";
                        SuccessMessage = $"Backfill complete in {LastReport.Duration.TotalSeconds:0.0}s. Created {LastReport.JournalsCreated:N0} journal entries totaling ${LastReport.TotalDebit:N2} debit / ${LastReport.TotalCredit:N2} credit ({balanced}). Skipped {LastReport.JournalsSkippedExisting:N0} months that were already posted; {LastReport.JournalsZeroAmount:N0} of the new entries were $0 (months past full depreciation).";
                    }
                }
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
            TotalBooks = await _db.Books.CountAsync(b => b.IsActive && b.BookType == BookType.Financial);
            ExistingDepJournals = await _db.JournalEntries.CountAsync(j => j.Source == "DEP");
            TotalAccumulatedDepreciation = await _db.AssetBookSettings.SumAsync(s => (decimal?)s.AccumulatedDepreciation) ?? 0m;
            PostedDebit = await _db.JournalLines
                .Where(l => l.JournalEntry != null && l.JournalEntry.Source == "DEP")
                .SumAsync(l => (decimal?)l.Debit) ?? 0m;
            PostedCredit = await _db.JournalLines
                .Where(l => l.JournalEntry != null && l.JournalEntry.Source == "DEP")
                .SumAsync(l => (decimal?)l.Credit) ?? 0m;
        }

        private static DateTime PreviousMonthEnd(DateTime today)
        {
            var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
            return firstOfThisMonth.AddDays(-1);
        }
    }
}
