// =============================================================================
// Sprint 13.5 PRA-5e.1 — Shared GlPostingHelpers extension method.
//
// Cleanup-pass extraction of the inline ResolveAccountAndKeyAsync helper that
// got copied verbatim into 3 posting services during PRA-5c/5d/5e:
//
//   - Services/AccountsPayable/ApPostingService.cs       (PRA-5c)
//   - Services/Receiving/ReceivingPostingService.cs      (PRA-5d)
//   - Services/Cip/CipCapitalizationService.cs           (PRA-5e)
//
// Per the cleanup-pass discipline (premature abstraction at 2 copies,
// mandatory at 3), the 3rd copy in PRA-5e triggered this extraction.
//
// PATTERN: extension method on IGlAccountResolver so call sites stay
// concise — `_glResolver.ResolveAccountAndKeyAsync(...)` reads as if the
// method is on the interface itself, while keeping the helper out of the
// production cascade implementation (which lives in
// Services/IGlAccountResolver.cs).
//
// BEHAVIOR (identical to the 3 inline copies):
//   1. Resolve legacy account-number string via existing ResolveAsync cascade.
//   2. Resolve AccountingKeyId via PRA-5b ResolveAccountingKeyAsync.
//   3. If step 2 throws GlAccountResolutionException, swallow + optionally log
//      a warning, return AccountingKeyId NULL (DEF-008 fallback path).
//
// CALL SITES post-extraction:
//   var (account, keyId) = await _glResolver.ResolveAccountAndKeyAsync(
//       companyId, kind, glContext, _logger, "invoice={invoice.Number} line=...");
//
// The ILogger parameter is optional. Services pass their existing
// ILogger<T> field for the warning-log path; tests or services without a
// logger can pass null.
//
// AUTHORITY
//   - PRA-5c shipped 2026-05-24 (PR #326) — helper origin
//   - PRA-5d shipped 2026-05-24 (PR #327) — 2nd inline copy
//   - PRA-5e shipped 2026-05-24 (PR #328) — 3rd inline copy → extraction trigger
//   - PRA-5b shipped 2026-05-24 (PR #325) — AccountingKey foundation
//   - PRA-5e.2 shipped 2026-05-24 (PR #329) — COA seed (proves the helper works)
//   - memory: project_pra5c/5d/5e/5e2_shipped.md
// =============================================================================

using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Posting
{
    /// <summary>
    /// Extension methods that compose the existing
    /// <see cref="IGlAccountResolver"/> cascade with the PRA-5b
    /// AccountingKey resolution into one dual-write call. Use these
    /// helpers from any posting service that needs to stamp both the
    /// legacy <c>Account</c> string AND the new <c>AccountingKeyId</c>
    /// onto a JournalLine.
    /// </summary>
    public static class GlPostingHelpers
    {
        /// <summary>
        /// DEF-008 dual-write helper. Resolves the legacy account-number
        /// string via the existing <see cref="IGlAccountResolver.ResolveAsync"/>
        /// cascade AND the new <see cref="AccountingKey"/> Id via the PRA-5b
        /// <see cref="IGlAccountResolver.ResolveAccountingKeyAsync"/> extension.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the AccountingKey resolution throws
        /// <see cref="GlAccountResolutionException"/> (orphan path — the
        /// resolved account-number string has no matching
        /// <see cref="Abs.FixedAssets.Models.GlAccount"/> row in COA), the
        /// exception is caught and the returned <c>accountingKeyId</c> is
        /// NULL. The legacy <c>account</c> string is still returned so the
        /// caller can stamp it on the JournalLine and the JE stays balanced.
        /// This is the DEF-008 back-compat guarantee.
        /// </para>
        /// <para>
        /// If <paramref name="logger"/> is non-null, the orphan case logs
        /// a single warning describing the failed resolution. Callers
        /// without a logger (tests, edge cases) can pass null.
        /// </para>
        /// <para>
        /// First-iteration segment context is CompanyId-only (an empty
        /// <see cref="AccountingKeyResolveContext"/>). Future overloads
        /// will accept a populated context for SiteId / CostCenterId /
        /// DepartmentId / ProjectId / InterCoPartnerCompanyId enrichment
        /// per posting purpose.
        /// </para>
        /// </remarks>
        /// <param name="resolver">The cascade resolver (already DI-injected
        /// into the calling posting service).</param>
        /// <param name="companyId">Owning Company.Id for the post.</param>
        /// <param name="kind">The <see cref="GlAccountKind"/> being resolved.</param>
        /// <param name="glContext">Optional context for the legacy cascade
        /// (AssetId, BookId, PurchaseOrderLineId, etc.). Pass null when
        /// no override hints are available.</param>
        /// <param name="logger">Optional <see cref="ILogger"/> for warning
        /// on the orphan-fallback path. Pass null to suppress logging.</param>
        /// <param name="logContext">Optional human-readable context string
        /// included in the warning log (e.g. <c>"invoice=INV-123 line=5"</c>).
        /// </param>
        /// <param name="ct">Cancellation token forwarded to the resolver.</param>
        /// <returns>
        /// A tuple of (legacy account-number string, AccountingKeyId int?).
        /// <c>account</c> is always non-null when this method returns
        /// (otherwise <see cref="IGlAccountResolver.ResolveAsync"/> threw).
        /// <c>accountingKeyId</c> is NULL only on the orphan-fallback path.
        /// </returns>
        public static async Task<(string account, int? accountingKeyId)> ResolveAccountAndKeyAsync(
            this IGlAccountResolver resolver,
            int companyId,
            GlAccountKind kind,
            GlResolveContext? glContext = null,
            ILogger? logger = null,
            string? logContext = null,
            CancellationToken ct = default)
        {
            var account = await resolver.ResolveAsync(companyId, kind, glContext);
            int? keyId = null;
            try
            {
                keyId = await resolver.ResolveAccountingKeyAsync(
                    companyId, kind, new AccountingKeyResolveContext(), glContext, ct);
            }
            catch (GlAccountResolutionException ex)
            {
                logger?.LogWarning(
                    ex,
                    "AccountingKey resolution failed for kind={Kind} ctx={Ctx}; legacy Account={Account} only",
                    kind, logContext ?? "", account);
            }
            return (account, keyId);
        }
    }
}
