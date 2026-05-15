using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
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
                GlAccountKind.CipPending => "1400",
                GlAccountKind.MaintenanceLabor => "6200",
                GlAccountKind.MaintenanceMaterials => "6210",
                GlAccountKind.MaintenanceOutsideVendor => "6220",
                // PR #92: liability bucket the labor JE credits until the
                // payroll subsystem clears it to Cash. Slotted next to the
                // GR-Accrued account (2150) so the trial balance puts the
                // two accrual liabilities side by side.
                GlAccountKind.AccruedLabor => "2160",
                _ => null
            };
        }
    }
}
