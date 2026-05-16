using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Journals
{
    /// <summary>
    /// PR #114: Manual journal-entry form. Lets Accountants + Admins post a
    /// balanced multi-line JE with Source="MANUAL". Pre-PR the system only
    /// produced JEs from automation paths (GR / WO / AP / Capitalize / etc.) —
    /// any kind of correction, accrual, reclassification, or audit adjustment
    /// had to be done in raw SQL.
    ///
    /// Guardrails:
    ///   - Σ Debits must equal Σ Credits before SaveChanges (PR #84 belt and
    ///     suspenders — its persistence guard already enforces this, but
    ///     we surface a clean error before round-tripping to the DB).
    ///   - Empty rows (account blank or both debit + credit zero) are skipped.
    ///   - Description is required.
    ///   - PostingDate defaults to today; Period is derived as yyyyMM.
    ///   - AuditLog row written on every successful post.
    /// </summary>
    [Authorize(Policy = "AccountantOrAdmin")]
    public class ManualModel : PageModel
    {
        private readonly AppDbContext _db;

        public ManualModel(AppDbContext db)
        {
            _db = db;
        }

        public class LineInput
        {
            public string? Account { get; set; }
            public string? Description { get; set; }
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        [BindProperty] public DateTime PostingDate { get; set; } = DateTime.UtcNow.Date;
        [BindProperty] public string? Description { get; set; }
        [BindProperty] public string? Reference { get; set; }
        [BindProperty] public List<LineInput> Lines { get; set; } = new();

        public List<GlAccount> AccountOptions { get; private set; } = new();
        public string? ErrorMessage { get; private set; }
        public int? CreatedJeId { get; private set; }
        public decimal SubmittedDebitTotal { get; private set; }
        public decimal SubmittedCreditTotal { get; private set; }

        public async Task OnGetAsync()
        {
            await LoadAccountsAsync();
            // Render 8 empty rows by default — enough for most real entries
            // without scrolling. Operators can leave unused ones blank.
            if (Lines.Count == 0)
                Lines = Enumerable.Range(0, 8).Select(_ => new LineInput()).ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadAccountsAsync();

            if (string.IsNullOrWhiteSpace(Description))
            {
                ErrorMessage = "Description is required.";
                return Page();
            }

            // Filter out empty rows (operator-friendly: leaving blanks is fine).
            var realLines = Lines
                .Where(l => !string.IsNullOrWhiteSpace(l.Account) && (l.Debit != 0m || l.Credit != 0m))
                .ToList();
            if (realLines.Count < 2)
            {
                ErrorMessage = "At least 2 non-empty lines are required.";
                return Page();
            }

            // Per-line validation: each row must have either Debit or Credit
            // but not both (PR #84 / PR #102 convention).
            foreach (var line in realLines)
            {
                if (line.Debit > 0m && line.Credit > 0m)
                {
                    ErrorMessage = $"Line for account {line.Account} has both Debit and Credit set; pick one.";
                    return Page();
                }
                if (line.Debit < 0m || line.Credit < 0m)
                {
                    ErrorMessage = $"Line for account {line.Account} has a negative amount; use the opposite column instead.";
                    return Page();
                }
            }

            SubmittedDebitTotal = realLines.Sum(l => l.Debit);
            SubmittedCreditTotal = realLines.Sum(l => l.Credit);
            if (Math.Abs(SubmittedDebitTotal - SubmittedCreditTotal) > 0.005m)
            {
                ErrorMessage = $"Entry is unbalanced: Debits {SubmittedDebitTotal:C} vs Credits {SubmittedCreditTotal:C} (Δ {SubmittedDebitTotal - SubmittedCreditTotal:C}). Fix and resubmit.";
                return Page();
            }

            // Build the JE. Reference defaults to MANUAL-{yyyyMMddHHmmss} when
            // the operator doesn't supply one. Batch shares the reference so
            // the PR #93 spend report and the PR #113 Trial Balance can group
            // the lines back to their origin.
            var ticks = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var jeReference = string.IsNullOrWhiteSpace(Reference) ? $"MANUAL-{ticks}" : Reference.Trim();

            var je = new JournalEntry
            {
                BookId = null,
                Batch = jeReference,
                Period = int.Parse(PostingDate.ToString("yyyyMM")),
                PostingDate = PostingDate.Date,
                Source = "MANUAL",
                Reference = jeReference,
                Description = Description.Trim(),
                CreatedUtc = DateTime.UtcNow,
                Lines = new List<JournalLine>()
            };
            int lineNo = 1;
            foreach (var line in realLines)
            {
                je.Lines.Add(new JournalLine
                {
                    LineNo = lineNo++,
                    Account = line.Account!.Trim(),
                    Description = (line.Description ?? Description).Trim(),
                    Debit = line.Debit,
                    Credit = line.Credit
                });
            }
            _db.JournalEntries.Add(je);

            // AuditLog the manual post. Every manual JE leaves a trail that
            // SOX-style internal-controls testing can sample (same shape as
            // the PR #105 override-approve audit).
            _db.AuditLogs.Add(new AuditLog
            {
                EntityType = "JournalEntry",
                EntityId = null, // Id not known until SaveChanges; serialized in description
                Action = "ManualJEPosted",
                Username = User.Identity?.Name,
                Description = $"Manual JE {jeReference} for {SubmittedDebitTotal:C} ({realLines.Count} lines): {Description}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                Timestamp = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            CreatedJeId = je.Id;

            // Reset the form so the user can post another. Keep posting date
            // and description for repeated work but clear the lines.
            Lines = Enumerable.Range(0, 8).Select(_ => new LineInput()).ToList();
            return Page();
        }

        private async Task LoadAccountsAsync()
        {
            AccountOptions = await _db.GlAccounts
                .Where(a => a.IsActive && a.AllowManualEntry)
                .OrderBy(a => a.AccountNumber)
                .ToListAsync();
        }
    }
}
