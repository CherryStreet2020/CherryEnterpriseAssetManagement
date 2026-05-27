using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Masters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Abs.FixedAssets.Services
{
    /// <summary>
    /// Optional context inputs that change how
    /// <see cref="IGlAccountResolver.ResolveAsync"/> walks the cascade.
    /// All fields are optional; the resolver picks up only what it needs.
    /// </summary>
    public sealed record GlResolveContext(
        int? AssetId = null,
        int? BookId = null,
        int? PurchaseOrderLineId = null,
        int? VendorInvoiceLineId = null,
        int? WorkOrderId = null,
        int? CipProjectId = null);

    /// <summary>
    /// Sprint 13.5 PRA-5b — segment context for AccountingKey resolution.
    /// All segments other than CompanyId + AccountId are optional; the
    /// resolver mints an AccountingKey row keyed on whatever subset the
    /// caller can supply. NULL segments serialize as empty string in the
    /// canonical hash form (matching the SQL backfill in
    /// 20260524250000_AddAccountingKeyPRA5b.cs).
    /// </summary>
    /// <param name="SiteId">Location.Id of the physical site for the post; NULL for corporate overhead.</param>
    /// <param name="CostCenterId">CostCenter.Id; required when <c>GlAccount.RequiresCostCenter</c>.</param>
    /// <param name="DepartmentId">Department.Id; required when <c>GlAccount.RequiresDepartment</c>.</param>
    /// <param name="ProjectId">CustomerProject.Id; required for project-tracked accounts.</param>
    /// <param name="InterCoPartnerCompanyId">Counterparty Company.Id for intercompany pairs.</param>
    /// <param name="VerticalOverride">Force a specific IndustryVertical; defaults to <c>Company.IndustryVertical</c>.</param>
    public sealed record AccountingKeyResolveContext(
        int? SiteId = null,
        int? CostCenterId = null,
        int? DepartmentId = null,
        int? ProjectId = null,
        int? InterCoPartnerCompanyId = null,
        IndustryVertical? VerticalOverride = null);

    /// <summary>
    /// Resolves the GL account string for a posting purpose. See
    /// <c>docs/adr/ADR-003-central-gl-account-resolver.md</c> §D-3-2 for
    /// the cascade order.
    /// </summary>
    public interface IGlAccountResolver
    {
        /// <summary>Resolves the GL account string for a posting purpose.
        /// Throws <see cref="GlAccountResolutionException"/> with the
        /// cascade history when nothing resolves — fail fast, never post
        /// to a wrong-because-blank account.</summary>
        Task<string> ResolveAsync(int companyId, GlAccountKind kind, GlResolveContext? context = null);

        /// <summary>
        /// Sprint 13.5 PRA-5b — resolves (and creates on-demand) the
        /// 8-segment <see cref="AccountingKey"/> row for a posting purpose
        /// and returns its <c>Id</c>. Posting services call this in addition
        /// to <see cref="ResolveAsync"/> so the resulting <c>JournalLine</c>
        /// can carry both the legacy <c>Account</c> varchar(50) string AND
        /// the new <c>AccountingKeyId</c> FK (DEF-008 dual-write).
        /// </summary>
        /// <remarks>
        /// Internally: walks the existing <see cref="ResolveAsync"/> cascade
        /// to get the account-number string, looks up the matching
        /// <see cref="GlAccount.Id"/> in the given company (or system-wide
        /// fallback), denormalizes <see cref="IndustryVertical"/> from
        /// <see cref="Company"/>, computes the sha256 hash of the canonical
        /// 8-segment string, then performs a find-or-insert against the
        /// <c>AccountingKeys</c> table. Concurrent inserts of the same hash
        /// race-cleanly — the second loser catches the duplicate-key error
        /// and re-reads the winner's Id.
        ///
        /// Returns the <see cref="AccountingKey.Id"/>. Throws
        /// <see cref="GlAccountResolutionException"/> if the account string
        /// resolves but no matching <see cref="GlAccount"/> row exists in the
        /// given company (or as a system-wide row).
        /// </remarks>
        Task<int> ResolveAccountingKeyAsync(
            int companyId,
            GlAccountKind kind,
            AccountingKeyResolveContext keyContext,
            GlResolveContext? glContext = null,
            CancellationToken ct = default);
    }

    /// <summary>
    /// Thrown when <see cref="IGlAccountResolver.ResolveAsync"/> exhausts
    /// the cascade without a match. Carries the diagnostic detail so the
    /// surfacing page handler can include it in the user-facing error.
    /// </summary>
    public sealed class GlAccountResolutionException : Exception
    {
        public int CompanyId { get; }
        public GlAccountKind Kind { get; }
        public IReadOnlyList<string> CascadeHistory { get; }

        public GlAccountResolutionException(int companyId, GlAccountKind kind, IReadOnlyList<string> cascadeHistory)
            : base($"GL account resolution failed for CompanyId={companyId}, Kind={kind}. " +
                   $"Cascade: {string.Join(" → ", cascadeHistory)}. " +
                   "Check that CompanyGlAccountConfigs has been seeded for this company " +
                   "(MasterDataBootstrapService.SeedTenantAsync) and that the industry default " +
                   "covers this AccountKind.")
        {
            CompanyId = companyId;
            Kind = kind;
            CascadeHistory = cascadeHistory;
        }
    }

    /// <summary>
    /// Default implementation of <see cref="IGlAccountResolver"/>.
    /// Cascade order (ADR-003 §D-3-2):
    ///   1. Per-entity override (Asset.GLAssetAccount, BookGlAccount, etc.)
    ///   2. Per-book defaults (Book.GlAccountDepExp, Book.GlAccountAccumDep)
    ///   3. Per-company config row (CompanyGlAccountConfigs)
    ///   4. Industry-default constants (IndustryDefaults)
    /// </summary>
    public class GlAccountResolver : IGlAccountResolver
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public GlAccountResolver(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<string> ResolveAsync(int companyId, GlAccountKind kind, GlResolveContext? context = null)
        {
            context ??= new GlResolveContext();
            var cascade = new List<string>();

            // 1. Per-entity override — strongest signal.
            var entityOverride = await TryResolveEntityOverrideAsync(kind, context);
            if (!string.IsNullOrWhiteSpace(entityOverride))
            {
                cascade.Add($"per-entity={entityOverride}");
                return entityOverride;
            }
            cascade.Add("per-entity=(none)");

            // 2. Per-book defaults.
            if (context.BookId.HasValue)
            {
                var bookDefault = await TryResolveBookDefaultAsync(context.BookId.Value, kind);
                if (!string.IsNullOrWhiteSpace(bookDefault))
                {
                    cascade.Add($"per-book={bookDefault}");
                    return bookDefault;
                }
                cascade.Add("per-book=(none)");
            }

            // 3. Per-company config.
            var companyConfig = await GetCompanyConfigCachedAsync(companyId, kind);
            if (!string.IsNullOrWhiteSpace(companyConfig))
            {
                cascade.Add($"per-company={companyConfig}");
                return companyConfig;
            }
            cascade.Add("per-company=(none)");

            // 4. Industry default.
            var industryDefault = IndustryDefaults.For(kind);
            if (!string.IsNullOrWhiteSpace(industryDefault))
            {
                cascade.Add($"industry-default={industryDefault}");
                return industryDefault!;
            }
            cascade.Add("industry-default=(none)");

            throw new GlAccountResolutionException(companyId, kind, cascade);
        }

        private async Task<string?> TryResolveEntityOverrideAsync(GlAccountKind kind, GlResolveContext ctx)
        {
            // Asset-level overrides first — they trump book defaults.
            if (ctx.AssetId.HasValue && (kind == GlAccountKind.AssetCost
                || kind == GlAccountKind.AccumulatedDepreciation
                || kind == GlAccountKind.DepreciationExpense))
            {
                var asset = await _db.Assets.AsNoTracking()
                    .Where(a => a.Id == ctx.AssetId.Value)
                    .Select(a => new { a.GLAssetAccount, a.GLAccumDepAccount, a.GLDepExpenseAccount })
                    .FirstOrDefaultAsync();
                if (asset != null)
                {
                    return kind switch
                    {
                        GlAccountKind.AssetCost => asset.GLAssetAccount,
                        GlAccountKind.AccumulatedDepreciation => asset.GLAccumDepAccount,
                        GlAccountKind.DepreciationExpense => asset.GLDepExpenseAccount,
                        _ => null
                    };
                }
            }

            // BookGlAccount per (Book, AssetId? = null) — depreciation accounts.
            if (ctx.BookId.HasValue && (kind == GlAccountKind.AccumulatedDepreciation
                || kind == GlAccountKind.DepreciationExpense))
            {
                var bgl = await _db.BookGlAccounts.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.BookId == ctx.BookId.Value);
                if (bgl != null)
                {
                    return kind switch
                    {
                        GlAccountKind.AccumulatedDepreciation => bgl.AccumulatedDepreciation,
                        GlAccountKind.DepreciationExpense => bgl.DepreciationExpense,
                        _ => null
                    };
                }
            }

            return null;
        }

        private async Task<string?> TryResolveBookDefaultAsync(int bookId, GlAccountKind kind)
        {
            var book = await _db.Books.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookId);
            if (book == null) return null;

            return kind switch
            {
                GlAccountKind.AccumulatedDepreciation => book.GlAccountAccumDep,
                GlAccountKind.DepreciationExpense => book.GlAccountDepExp,
                _ => null
            };
        }

        private async Task<string?> GetCompanyConfigCachedAsync(int companyId, GlAccountKind kind)
        {
            var key = (companyId, kind);
            if (_cache.TryGetValue(key, out string? cached)) return cached;

            var row = await _db.Set<CompanyGlAccountConfig>().AsNoTracking()
                .Where(c => c.CompanyId == companyId && c.AccountKind == kind)
                .Select(c => c.GlAccount)
                .FirstOrDefaultAsync();

            _cache.Set(key, row, CacheTtl);
            return row;
        }

        // =====================================================================
        // Sprint 13.5 PRA-5b — ResolveAccountingKeyAsync implementation.
        //
        // CALLED BY: every posting service after the legacy ResolveAsync call,
        // to obtain the AccountingKeyId for the JournalLine. JournalLine
        // carries BOTH the legacy Account string AND the new AccountingKeyId
        // FK (DEF-008 dual-write).
        //
        // CANONICAL HASH FORM (matches the SQL backfill in
        // 20260524250000_AddAccountingKeyPRA5b.cs):
        //   "{CompanyId}|{SiteId|''}|{AccountId}|{CostCenterId|''}
        //    |{DepartmentId|''}|{ProjectId|''}|{InterCoPartnerCompanyId|''}
        //    |{(short)IndustryVertical|''}"
        // NULL segments serialize as empty string. The hash is sha256-hex.
        //
        // FIND-OR-INSERT WITH RACE TOLERANCE:
        //   1. Look up by (CompanyId, AccountingKeyHash) — partial UNIQUE index.
        //   2. If found, return the cached Id.
        //   3. Else INSERT; if a concurrent insert raced ahead and we hit the
        //      duplicate-key, re-read and return the winner's Id.
        //
        // CACHED: in-memory by (companyId, hash) with the same 10-min TTL as
        // the GlAccount cascade. New AccountingKey rows are immutable; the
        // cache can hold the resolved Id forever in practice.
        // =====================================================================

        private static readonly TimeSpan KeyCacheTtl = TimeSpan.FromMinutes(10);

        public async Task<int> ResolveAccountingKeyAsync(
            int companyId,
            GlAccountKind kind,
            AccountingKeyResolveContext keyContext,
            GlResolveContext? glContext = null,
            CancellationToken ct = default)
        {
            if (companyId <= 0)
                throw new ArgumentOutOfRangeException(nameof(companyId), "AccountingKey requires a positive companyId — JournalEntry must resolve through Book.CompanyId.");

            // Step 1 — walk the existing cascade to get the account-number string.
            // Throws GlAccountResolutionException if nothing resolves (fail-fast,
            // never post to a blank account).
            var accountNumber = await ResolveAsync(companyId, kind, glContext);

            // Step 2 — resolve account-number string to GlAccount.Id.
            // Same scope rule as the SQL backfill: company-owned row OR
            // system-wide row (CompanyId IS NULL). Company-owned wins when
            // both exist for the same account number.
            var accountId = await _db.Set<GlAccount>().AsNoTracking()
                .Where(a => a.AccountNumber == accountNumber
                    && (a.CompanyId == companyId || a.CompanyId == null))
                .OrderByDescending(a => a.CompanyId.HasValue) // company-owned first
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync(ct);

            if (accountId is null or 0)
                throw new GlAccountResolutionException(
                    companyId, kind,
                    new[] {
                        $"per-entity=(skipped)",
                        $"resolved-account-number={accountNumber}",
                        $"gl-account-lookup=NOT-FOUND for (CompanyId={companyId} OR NULL, AccountNumber={accountNumber})",
                    });

            // Step 3 — denormalize IndustryVertical from Company unless caller forces override.
            var vertical = keyContext.VerticalOverride;
            if (vertical is null)
            {
                vertical = await _db.Set<Company>().AsNoTracking()
                    .Where(c => c.Id == companyId)
                    .Select(c => (IndustryVertical?)c.IndustryVertical)
                    .FirstOrDefaultAsync(ct);
            }

            // Step 4 — build canonical hash string + hex.
            var canonical = BuildCanonicalKeyString(
                companyId,
                keyContext.SiteId,
                accountId.Value,
                keyContext.CostCenterId,
                keyContext.DepartmentId,
                keyContext.ProjectId,
                keyContext.InterCoPartnerCompanyId,
                vertical);
            var hashHex = Sha256Hex(canonical);

            // Step 5 — in-memory cache check (Id is immutable once minted).
            var cacheKey = ("AK", companyId, hashHex);
            if (_cache.TryGetValue<int>(cacheKey, out var cachedId))
                return cachedId;

            // Step 6 — find-or-insert against AccountingKeys table.
            var existing = await _db.Set<AccountingKey>().AsNoTracking()
                .Where(k => k.CompanyId == companyId && k.AccountingKeyHash == hashHex)
                .Select(k => (int?)k.Id)
                .FirstOrDefaultAsync(ct);

            if (existing.HasValue)
            {
                _cache.Set(cacheKey, existing.Value, KeyCacheTtl);
                return existing.Value;
            }

            var newRow = new AccountingKey
            {
                CompanyId = companyId,
                SiteId = keyContext.SiteId,
                AccountId = accountId.Value,
                CostCenterId = keyContext.CostCenterId,
                DepartmentId = keyContext.DepartmentId,
                ProjectId = keyContext.ProjectId,
                InterCoPartnerCompanyId = keyContext.InterCoPartnerCompanyId,
                IndustryVertical = vertical,
                AccountingKeyHash = hashHex,
                AccountingKeyString = $"Co={companyId}|Site={keyContext.SiteId?.ToString() ?? ""}" +
                    $"|Acct={accountId.Value}|CC={keyContext.CostCenterId?.ToString() ?? ""}" +
                    $"|Dept={keyContext.DepartmentId?.ToString() ?? ""}|Proj={keyContext.ProjectId?.ToString() ?? ""}" +
                    $"|ICP={keyContext.InterCoPartnerCompanyId?.ToString() ?? ""}|Vert={(vertical.HasValue ? ((short)vertical.Value).ToString() : "")}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "resolver",
            };

            _db.Set<AccountingKey>().Add(newRow);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Concurrent insert raced ahead and hit the (CompanyId, Hash)
                // partial UNIQUE. Detach the loser, re-read the winner.
                _db.Entry(newRow).State = EntityState.Detached;
                var winner = await _db.Set<AccountingKey>().AsNoTracking()
                    .Where(k => k.CompanyId == companyId && k.AccountingKeyHash == hashHex)
                    .Select(k => (int?)k.Id)
                    .FirstOrDefaultAsync(ct);
                if (winner.HasValue)
                {
                    _cache.Set(cacheKey, winner.Value, KeyCacheTtl);
                    return winner.Value;
                }
                throw; // unexpected — re-throw the original
            }

            _cache.Set(cacheKey, newRow.Id, KeyCacheTtl);
            return newRow.Id;
        }

        /// <summary>
        /// Builds the canonical 8-segment string used as input to the sha256
        /// hash. NULL segments serialize as empty string (NOT "NULL" / "0")
        /// so the form is unambiguous. The SQL backfill in
        /// 20260524250000_AddAccountingKeyPRA5b.cs produces the IDENTICAL
        /// string so backfill rows and runtime-resolved rows share hashes.
        /// </summary>
        public static string BuildCanonicalKeyString(
            int companyId,
            int? siteId,
            int accountId,
            int? costCenterId,
            int? departmentId,
            int? projectId,
            int? interCoPartnerCompanyId,
            IndustryVertical? vertical)
        {
            return string.Concat(
                companyId.ToString(),
                "|", siteId?.ToString() ?? "",
                "|", accountId.ToString(),
                "|", costCenterId?.ToString() ?? "",
                "|", departmentId?.ToString() ?? "",
                "|", projectId?.ToString() ?? "",
                "|", interCoPartnerCompanyId?.ToString() ?? "",
                "|", vertical.HasValue ? ((short)vertical.Value).ToString() : ""
            );
        }

        /// <summary>
        /// SHA-256 of the UTF-8 bytes of <paramref name="input"/>, lowercase
        /// hex. 64-character output. Matches Postgres
        /// <c>encode(digest(..., 'sha256'), 'hex')</c>.
        /// </summary>
        public static string Sha256Hex(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = SHA256.HashData(bytes);
            var sb = new StringBuilder(64);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Industry-default chart-of-accounts fallback. See ADR-003 §D-3-4.
        /// New <see cref="GlAccountKind"/> values MUST add a default here OR
        /// have a per-company config seeded for every tenant.
        /// </summary>
        public static class IndustryDefaults
        {
            public static string? For(GlAccountKind kind) => kind switch
            {
                GlAccountKind.AssetCost => "1500",
                GlAccountKind.AccumulatedDepreciation => "1510",
                GlAccountKind.DepreciationExpense => "6500",
                GlAccountKind.GainOnDisposal => "4500",
                GlAccountKind.LossOnDisposal => "6510",
                GlAccountKind.Inventory => "1300",
                GlAccountKind.GrAccrued => "2150",
                GlAccountKind.DirectExpense => "6000",
                GlAccountKind.WipExpense => "1410",
                GlAccountKind.AccountsPayable => "2000",
                GlAccountKind.Cash => "1110",
                GlAccountKind.PurchasePriceVariance => "5900",
                // PR #102 (B-09): tax + freight on the AP side. 1290 sits next
                // to inventory/receivables (current assets, recoverable VAT
                // treatment). 6300 is freight-in expense, slotted before the
                // 6200-series maintenance accounts. Per-company override via
                // CompanyGlAccountConfigs works the same as every other Kind.
                GlAccountKind.SalesTaxRecoverable => "1290",
                GlAccountKind.FreightExpense => "6300",
                GlAccountKind.CipPending => "1400",
                GlAccountKind.MaintenanceLabor => "6200",
                GlAccountKind.MaintenanceMaterials => "6210",
                GlAccountKind.MaintenanceOutsideVendor => "6220",
                // PR #92: liability bucket the labor JE credits until the
                // payroll subsystem clears it to Cash. Slotted next to the
                // GR-Accrued account (2150) so the trial balance puts the
                // two accrual liabilities side by side.
                GlAccountKind.AccruedLabor => "2160",
                // Sprint 14.4 PR-1: Production WIP, FG, COGS, scrap, rework,
                // variance, and inter-site transfer defaults. Account numbers
                // follow the 1400-series (WIP assets), 1500-series (FG assets),
                // 5000-series (COGS/variance), 6400-series (scrap/rework expense).
                GlAccountKind.ProductionWipMaterial => "1420",
                GlAccountKind.ProductionWipLabor => "1421",
                GlAccountKind.ProductionWipOverhead => "1422",
                GlAccountKind.ProductionWipSubcontract => "1423",
                GlAccountKind.ProductionWipOutsideProcessing => "1424",
                GlAccountKind.FinishedGoodsInventory => "1500",
                GlAccountKind.CostOfGoodsSold => "5000",
                GlAccountKind.ScrapExpense => "6400",
                GlAccountKind.ReworkExpense => "6410",
                GlAccountKind.MaterialUsageVariance => "5910",
                GlAccountKind.LaborRateVariance => "5920",
                GlAccountKind.LaborEfficiencyVariance => "5921",
                GlAccountKind.OverheadVolumeVariance => "5930",
                GlAccountKind.OverheadSpendingVariance => "5931",
                GlAccountKind.InterSiteWipTransferOut => "1430",
                GlAccountKind.InterSiteWipTransferIn => "1431",
                _ => null
            };
        }
    }
}
