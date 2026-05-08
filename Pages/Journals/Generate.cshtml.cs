using System;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Webhooks;
using Abs.FixedAssets.Services.Webhooks.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Journals
{
    [Authorize(Policy = "AccountantOrAdmin")]
    public class GenerateModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;
        private readonly IOutboxWriter _outbox;
        public GenerateModel(AppDbContext db, ITenantContext tenantContext,
            IModuleGuardService moduleGuard, IOutboxWriter outbox) {
            _moduleGuard = moduleGuard; _db = db; _tenantContext = tenantContext; _outbox = outbox; }

        public SelectListItem[] BookOptions { get; private set; } = Array.Empty<SelectListItem>();

        [BindProperty(SupportsGet = true)]
        public int? BookId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? Month { get; set; }

        public string? Error { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Finance" });


            BookOptions = await _db.Books
                .Where(b => _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0))
                .OrderBy(b => b.Code)
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = $"{b.Code} — {b.Name}" })
                .ToArrayAsync();

            Month ??= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (BookId is null)
            {
                Error = "Pick a book.";
                await OnGetAsync();
                return Page();
            }
            var month = Month ?? DateTime.Today;

            try
            {
                var companyId = _tenantContext.CompanyId ?? 1;
                var entry = await JournalGenerator.GenerateMonthlyAsync(_db, BookId.Value, month, createdBy: "web", companyId: companyId);
                TempData["Flash"] = $"Journal \"{entry.Batch}\" generated successfully for {month:yyyy-MM} with {entry.Lines?.Count ?? 0} line(s).";

                var book = await _db.Books.AsNoTracking().FirstAsync(b => b.Id == BookId.Value);
                var totalDr = entry.Lines?.Sum(l => l.Debit) ?? 0m;
                await _outbox.EnqueueAsync(
                    companyId,
                    siteId: null,
                    new DepreciationPostedV1(
                        JournalEntryId: entry.Id,
                        BookId: book.Id,
                        BookCode: book.Code,
                        CompanyId: book.CompanyId,
                        Period: entry.Period,
                        PostingDate: entry.PostingDate,
                        TotalDepreciation: totalDr,
                        Batch: entry.Batch,
                        LineCount: entry.Lines?.Count ?? 0,
                        CreatedBy: User?.Identity?.Name ?? "web"),
                    correlationId: $"dep-post-{entry.Id}"
                );

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                await OnGetAsync();
                return Page();
            }
        }

        public class SelectListItem
        {
            public string? Value { get; set; }
            public string? Text { get; set; }
        }
    }
}
