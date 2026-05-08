using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
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
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IModuleGuardService _moduleGuard;
        private readonly ITenantContext _tenantContext;
        private readonly IOutboxWriter _outbox;
        public IndexModel(AppDbContext db, IModuleGuardService moduleGuard, ITenantContext tenantContext, IOutboxWriter outbox)
        { _db = db; _moduleGuard = moduleGuard; _tenantContext = tenantContext; _outbox = outbox; }

        [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
        [BindProperty(SupportsGet = true)] public DateTime? To   { get; set; }
        [BindProperty(SupportsGet = true)] public string? Source { get; set; }
        [BindProperty(SupportsGet = true)] public string? Search { get; set; }

        public List<JournalEntry> Items { get; private set; } = new();

        public List<Book> Books { get; private set; } = new();
        [BindProperty] public int GenerateBookId { get; set; }
        [BindProperty] public DateTime GenerateMonth { get; set; }
            = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("finance"))
                return RedirectToPage("/ModuleDisabled", new { module = "Journals" });

            Books = await _db.Books.Where(b => _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0)).OrderBy(b => b.Code).ToListAsync();

            Items = await BuildQuery().ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnGetExportAsync()
        {
            var rows = await BuildQuery().ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Batch,PostingDate,Reference,Source,Description,Lines,TotalDebit,TotalCredit");

            string Csv(string? s) => string.IsNullOrEmpty(s) ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

            foreach (var j in rows)
            {
                var lines = j.Lines ?? new List<JournalLine>();
                decimal totDr = lines.Sum(l => l.Debit);
                decimal totCr = lines.Sum(l => l.Credit);

                sb.AppendLine(string.Join(",",
                    Csv(j.Batch),
                    j.PostingDate.ToString("MM/dd/yy", CultureInfo.InvariantCulture),
                    Csv(j.Reference),
                    Csv(j.Source),
                    Csv(j.Description),
                    lines.Count.ToString(CultureInfo.InvariantCulture),
                    totDr.ToString("0.00", CultureInfo.InvariantCulture),
                    totCr.ToString("0.00", CultureInfo.InvariantCulture)
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"journals_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", fileName);
        }

        public async Task<IActionResult> OnPostGenerateAsync()
        {
            if (GenerateBookId <= 0)
            {
                TempData["FlashError"] = "Please select a Book.";
                return RedirectToPage(new { From, To, Source, Search });
            }

            try
            {
                var companyId = _tenantContext.CompanyId ?? 1;
                var entry = await JournalGenerator.GenerateMonthlyAsync(
                    _db,
                    GenerateBookId,
                    GenerateMonth,
                    createdBy: User?.Identity?.Name ?? "system",
                    companyId: companyId);

                TempData["Flash"] = $"Journal created for {GenerateMonth:yyyy-MM}.";

                var book = await _db.Books.AsNoTracking().FirstAsync(b => b.Id == GenerateBookId);
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
                        CreatedBy: User?.Identity?.Name ?? "system"),
                    correlationId: $"dep-post-{entry.Id}"
                );
            }
            catch (Exception ex)
            {
                TempData["FlashError"] = ex.Message;
            }

            return RedirectToPage(new { From, To, Source, Search });
        }

        private IQueryable<JournalEntry> BuildQuery()
        {
            var q = _db.JournalEntries
                .Include(j => j.Lines)
                .OrderByDescending(j => j.PostingDate)
                .ThenByDescending(j => j.Id)
                .AsQueryable();

            if (From.HasValue)
            {
                var fromDate = From.Value.Date;
                q = q.Where(j => j.PostingDate >= fromDate);
            }

            if (To.HasValue)
            {
                var toExclusive = To.Value.Date.AddDays(1);
                q = q.Where(j => j.PostingDate < toExclusive);
            }

            if (!string.IsNullOrWhiteSpace(Source))
            {
                var src = Source.Trim();
                q = q.Where(j => j.Source != null && j.Source.Contains(src));
            }

            if (!string.IsNullOrWhiteSpace(Search))
            {
                var s = Search.Trim();
                q = q.Where(j =>
                    (j.Batch != null && j.Batch.Contains(s)) ||
                    (j.Reference != null && j.Reference.Contains(s)) ||
                    (j.Description != null && j.Description.Contains(s)));
            }

            return q;
        }
    }
}
