using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Posting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services
{
    /// <summary>
    /// PR #102 (B-10): Posts the journal entry that pairs every Capital
    /// Improvement event. Pre-fix, both <c>Pages/Assets/Improve.cshtml.cs</c>
    /// and <c>Pages/WorkOrders/Details.cshtml.cs::OnPostCapitalizeAsync</c>
    /// incremented <see cref="Asset.AcquisitionCost"/> in place without
    /// producing any GL entry. Trial balance silently drifted on every
    /// improvement: the asset's cost basis grew while no offsetting credit
    /// hit the books.
    ///
    /// JE shape:
    ///   DR <see cref="GlAccountKind.AssetCost"/> (1500-series, resolved per
    ///       asset's own GL configuration when present)
    ///   CR <see cref="GlAccountKind.CipPending"/> (1400)
    ///
    /// The CR side defaults to CipPending — the canonical "improvement
    /// accumulator" account most ERPs use. A follow-up PR can branch the
    /// credit (AP for outside-vendor improvements, Cash for direct buys,
    /// CIP for in-flight project rollups) once the funding-source signal
    /// is wired through the form.
    ///
    /// Period guard mirrors Asset Improve / Dispose / CapImp on the modern
    /// WO surface — improvements into closed periods refuse cleanly with a
    /// user-readable reason. Caller (Razor page handler) is expected to
    /// have run its own period guard first; this is belt + suspenders.
    /// </summary>
    public interface ICapitalImprovementPostingService
    {
        Task<int?> PostImprovementJeAsync(
            int improvementId,
            int assetId,
            int companyId,
            decimal amount,
            DateTime improvementDate,
            string? description = null);
    }

    public sealed class CapitalImprovementPostingService : ICapitalImprovementPostingService
    {
        private readonly AppDbContext _db;
        private readonly IGlAccountResolver _glResolver;
        private readonly IPeriodGuard _periodGuard;
        private readonly ILogger<CapitalImprovementPostingService> _logger;

        public CapitalImprovementPostingService(
            AppDbContext db,
            IGlAccountResolver glResolver,
            IPeriodGuard periodGuard,
            ILogger<CapitalImprovementPostingService> logger)
        {
            _db = db;
            _glResolver = glResolver;
            _periodGuard = periodGuard;
            _logger = logger;
        }

        public async Task<int?> PostImprovementJeAsync(
            int improvementId,
            int assetId,
            int companyId,
            decimal amount,
            DateTime improvementDate,
            string? description = null)
        {
            if (amount <= 0m)
                return null; // zero-cost improvement is a metadata edit, not a financial event

            if (companyId <= 0)
                return null; // can't resolve GL accounts without a company; caller should have ensured this

            // Period guard. Same posture as Asset Improve / Dispose. Refuse a
            // posting into a closed period — caller gets a runtime exception
            // so the Razor page handler can surface the reason on TempData.
            var periodCheck = await _periodGuard.CanPostAsync(companyId, improvementDate);
            if (!periodCheck.IsAllowed)
            {
                throw new InvalidOperationException(
                    periodCheck.Reason
                    ?? $"Cannot post improvement: fiscal period for {improvementDate:yyyy-MM-dd} is closed.");
            }

            // Idempotency: existing JE with this Reference means we've already
            // posted. Most call sites guard via the IsCapitalized check upstream,
            // but defending here too means a stuck Razor double-POST can't
            // duplicate the entry.
            var jeReference = $"IMPR-{improvementId}";
            var existingJeId = await _db.JournalEntries
                .Where(j => j.Reference == jeReference && j.Source == "CIP-IMPR")
                .Select(j => (int?)j.Id)
                .FirstOrDefaultAsync();
            if (existingJeId.HasValue)
                return existingJeId;

            var ctx = new GlResolveContext(AssetId: assetId);
            // PRA-5f: dual-write Account + AccountingKeyId via shared
            // GlPostingHelpers.ResolveAccountAndKeyAsync (extracted in PRA-5e.1
            // from the inline copies in Ap/Receiving/Cip posting services).
            // On AccountingKey resolution failure, helper logs a warning and
            // returns NULL keyId — JE still posts with legacy Account string.
            var (drAccount, drAccountKeyId) = await _glResolver.ResolveAccountAndKeyAsync(
                companyId,
                GlAccountKind.AssetCost,
                ctx,
                logger: _logger,
                logContext: $"cap-impr improvement={improvementId} asset");
            var (crAccount, crAccountKeyId) = await _glResolver.ResolveAccountAndKeyAsync(
                companyId,
                GlAccountKind.CipPending,
                ctx,
                logger: _logger,
                logContext: $"cap-impr improvement={improvementId} cip-pending");

            var je = new JournalEntry
            {
                BookId = null, // improvements are not book-scoped; depreciation downstream picks up the new basis per book via the snapshot recompute
                Batch = jeReference,
                Period = improvementDate.Year * 100 + improvementDate.Month,
                PostingDate = improvementDate.Date,
                Source = "CIP-IMPR",
                Reference = jeReference,
                Description = string.IsNullOrWhiteSpace(description)
                    ? $"Capital improvement #{improvementId}"
                    : $"Capital improvement #{improvementId} — {description}",
                CreatedUtc = DateTime.UtcNow,
                Lines = new List<JournalLine>
                {
                    new JournalLine
                    {
                        LineNo = 1,
                        Account = drAccount,
                        AccountingKeyId = drAccountKeyId,
                        Description = $"Improvement to asset {assetId}",
                        Debit = amount,
                        Credit = 0m
                    },
                    new JournalLine
                    {
                        LineNo = 2,
                        Account = crAccount,
                        AccountingKeyId = crAccountKeyId,
                        Description = $"CIP-Pending settlement (improvement #{improvementId})",
                        Debit = 0m,
                        Credit = amount
                    }
                }
            };
            _db.JournalEntries.Add(je);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "CapitalImprovementPostingService: improvement {Id} on asset {AssetId} → JE {JeId}, ${Amount}",
                improvementId, assetId, je.Id, amount);

            return je.Id;
        }
    }
}
