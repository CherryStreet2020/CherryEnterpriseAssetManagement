using System.ComponentModel.DataAnnotations;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Lookups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Assets
{
    public class DisposeModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly ILookupService _lookupService;
        private readonly ITenantContext _tenantContext;
        private readonly IModuleGuardService _moduleGuard;

        public DisposeModel(AppDbContext db, ILookupService lookupService, ITenantContext tenantContext,
            IModuleGuardService moduleGuard)
        {
            _moduleGuard = moduleGuard;
            _db = db;
            _lookupService = lookupService;
            _tenantContext = tenantContext;
        }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }
        public decimal CurrentBookValue => (Asset?.AcquisitionCost ?? 0) - (Asset?.AccumulatedDepreciation ?? 0);

        [BindProperty]
        [Required]
        [DataType(DataType.Date)]
        public DateTime DisposalDate { get; set; } = DateTime.Today;

        [BindProperty]
        [Required]
        public string DisposalType { get; set; } = "Sale";

        [BindProperty]
        public int? DisposalReasonLookupValueId { get; set; }

        [BindProperty]
        [Range(0, double.MaxValue)]
        public decimal Proceeds { get; set; }

        [BindProperty]
        [Range(0, double.MaxValue)]
        public decimal DisposalExpense { get; set; }

        [BindProperty]
        public string? Notes { get; set; }

        [BindProperty]
        public bool CreateJournalEntry { get; set; } = true;

        [BindProperty]
        public int BookId { get; set; }

        public List<SelectListItem> BookOptions { get; set; } = new();
        public List<SelectListItem> DisposalReasonOptions { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!await _moduleGuard.IsModuleEnabledAsync("assets"))
                return RedirectToPage("/ModuleDisabled", new { module = "Assets" });

            AssetId = id;
            Asset = await _db.Assets.Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).FirstOrDefaultAsync();

            if (Asset == null)
                return NotFound();

            if (Asset.Status == AssetStatus.Disposed)
            {
                ErrorMessage = "This asset has already been disposed.";
            }

            await LoadBooksAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            AssetId = id;
            Asset = await _db.Assets.Where(a => a.Id == id && _tenantContext.VisibleCompanyIds.Contains(a.CompanyId ?? 0) && (!_tenantContext.SiteId.HasValue || a.SiteId == _tenantContext.SiteId.Value)).FirstOrDefaultAsync();

            if (Asset == null)
                return NotFound();

            if (Asset.Status == AssetStatus.Disposed)
            {
                ErrorMessage = "This asset has already been disposed.";
                await LoadBooksAsync();
                return Page();
            }

            if (!ModelState.IsValid)
            {
                await LoadBooksAsync();
                return Page();
            }

            if (DisposalReasonLookupValueId.HasValue)
            {
                var lv = await _lookupService.GetValueByIdAsync(
                    _tenantContext.TenantId, _tenantContext.CompanyId, DisposalReasonLookupValueId.Value);
                if (lv != null) DisposalType = lv.Code;
            }

            var netProceeds = Proceeds - DisposalExpense;
            var gainLoss = netProceeds - CurrentBookValue;

            Asset.Status = AssetStatus.Disposed;
            Asset.DisposalDate = DisposalDate;
            Asset.DisposalProceeds = Proceeds;
            Asset.GainLossOnDisposal = gainLoss;
            Asset.Active = false;

            if (CreateJournalEntry && BookId > 0)
            {
                var book = await _db.Books.Where(b => b.Id == BookId && _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0)).FirstOrDefaultAsync();
                if (book != null)
                {
                    var glAccounts = await _db.BookGlAccounts.Where(g => g.BookId == BookId).OrderBy(g => g.Id).FirstOrDefaultAsync();
                    
                    var entry = new JournalEntry
                    {
                        Batch = $"DISP-{DateTime.UtcNow:yyyyMMdd}-{Asset.AssetNumber}",
                        Period = int.Parse(DisposalDate.ToString("yyyyMM")),
                        PostingDate = DisposalDate,
                        BookId = BookId,
                        Reference = Asset.AssetNumber,
                        Source = "Disposal",
                        Description = $"Disposal of {Asset.AssetNumber} - {Asset.Description}"
                    };

                    var lines = new List<JournalLine>();
                    int lineNo = 1;

                    lines.Add(new JournalLine
                    {
                        LineNo = lineNo++,
                        Account = glAccounts?.AccumulatedDepreciation ?? "1500",
                        Description = $"Remove accumulated depreciation - {Asset.AssetNumber}",
                        Debit = Asset.AccumulatedDepreciation,
                        Credit = 0
                    });

                    if (Proceeds > 0)
                    {
                        lines.Add(new JournalLine
                        {
                            LineNo = lineNo++,
                            Account = "1000",
                            Description = $"Cash/AR from disposal (gross) - {Asset.AssetNumber}",
                            Debit = Proceeds,
                            Credit = 0
                        });
                    }

                    if (DisposalExpense > 0)
                    {
                        lines.Add(new JournalLine
                        {
                            LineNo = lineNo++,
                            Account = "6510",
                            Description = $"Disposal expense - {Asset.AssetNumber}",
                            Debit = DisposalExpense,
                            Credit = 0
                        });
                    }

                    lines.Add(new JournalLine
                    {
                        LineNo = lineNo++,
                        Account = glAccounts?.Asset ?? "1400",
                        Description = $"Remove asset cost - {Asset.AssetNumber}",
                        Debit = 0,
                        Credit = Asset.AcquisitionCost
                    });

                    if (gainLoss > 0)
                    {
                        lines.Add(new JournalLine
                        {
                            LineNo = lineNo++,
                            Account = glAccounts?.GainOnDisposal ?? "4500",
                            Description = $"Gain on disposal - {Asset.AssetNumber}",
                            Debit = 0,
                            Credit = gainLoss
                        });
                    }
                    else if (gainLoss < 0)
                    {
                        lines.Add(new JournalLine
                        {
                            LineNo = lineNo++,
                            Account = glAccounts?.LossOnDisposal ?? "6500",
                            Description = $"Loss on disposal - {Asset.AssetNumber}",
                            Debit = Math.Abs(gainLoss),
                            Credit = 0
                        });
                    }

                    entry.Lines = lines;
                    _db.JournalEntries.Add(entry);
                }
            }

            await _db.SaveChangesAsync();

            return RedirectToPage("/Assets/Asset", new { id = Asset.Id, mode = "view" });
        }

        private async Task LoadBooksAsync()
        {
            var books = await _db.Books.Where(b => _tenantContext.VisibleCompanyIds.Contains(b.CompanyId ?? 0)).OrderBy(b => b.Code).ToListAsync();
            BookOptions = books.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = $"{b.Code} - {b.Name}"
            }).ToList();

            if (BookOptions.Any())
                BookId = int.Parse(BookOptions.First().Value);

            DisposalReasonOptions = await _lookupService.GetSelectListByIdAsync(
                _tenantContext.TenantId, _tenantContext.CompanyId,
                "DisposalReason", DisposalReasonLookupValueId, "");
        }
    }
}
