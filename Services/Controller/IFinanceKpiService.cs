using System.Threading;
using System.Threading.Tasks;

namespace Abs.FixedAssets.Services.Controller;

// Sprint 12.7 PR #4 — Controller Control Center KPI band hydration service.
//
// Powers the 4-tile hero KPI band on /Controller (Lock 3 — the third leg of
// the Cockpit canvas alongside Queue and Preview). Pure read service —
// AsNoTracking() throughout. Zero DbContext mutation (Lock 15 compliant).
//
// Tile shape — 4 metrics chosen from the Sprint 12.7 PR #1 placeholders so
// the band slots in 1-for-1 without changing the band primitive contract:
//
//   1. Cash position           — GL balance across all CashAndReceivables
//                                accounts (sum of Debit − Credit on
//                                JournalLines whose GL Account maps to a
//                                CashAndReceivables-categorised GlAccount).
//   2. AP due this week        — sum of (Total − AmountPaid) on VendorInvoices
//                                where Status IN (Approved, PartiallyPaid) and
//                                DueDate ≤ today + 7d. Tone escalates by $.
//   3. Open POs                — count + total of PurchaseOrders where
//                                Status IN (Approved, Sent, PartiallyReceived).
//   4. WIP balance             — sum of CipProject.TotalCosts where
//                                Status = Active. Informational only.
//
// Why not 6 metrics ("Cash · AR aging · AP aging · open POs · WIP · unrealized
// gains" from the original PR #1 PR boundary doc)?
//
//   - AR aging needs a CustomerInvoice / Invoice (sales-side) entity that
//     doesn't exist yet in IndustryOS — ABS is AP-side only today.
//   - Unrealized FX gains need an FX revaluation engine that's queued for
//     Sprint 15+.
//
//   Honest scope: ship the 4 we can compute today; PR #5+ extends when
//   those entities + engines land. The KpiBand primitive is HeroMode=4
//   tiles anyway, so 4 lines up cleanly without surface change.
//
// Tenant scoping — caller passes the active CompanyId; service filters
// every query through it. When CompanyId is null (admin / cross-tenant
// view) the service aggregates across the platform. Matches the existing
// IControllerCockpitService pattern of "service does its own scoping".
//
// Lock 15 compliance:
//   - Constructor injects AppDbContext for read-only queries.
//   - No raw SQL — typed LINQ throughout.
//   - No magic strings for account numbers — categorisation reads
//     GlAccount.Category (the GlAccountCategory enum) on the JOIN side
//     so swapping account-number conventions doesn't break the tile.
//   - No AccountingKey integer literals.
//
// Failure shape — never throws on expected failures. On DB errors the
// service returns a band with all tiles set to "—" + tone "neutral"
// + SubText explaining the limitation. Razor renders the band identically
// so the page header / tabs / Cockpit shell stay present.
public interface IFinanceKpiService
{
    /// <summary>
    /// Hydrate the 4 Finance KPI tiles for the Controller Control Center
    /// hero band. Returns a FinanceKpiBand record the page model maps
    /// 1-for-1 into <see cref="Pages.Shared.Primitives.Cockpit.CockpitKpiTileViewModel"/>.
    /// </summary>
    /// <param name="companyId">Active tenant CompanyId. When null,
    /// aggregates across all companies (admin / system view).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<FinanceKpiBand> GetBandAsync(int? companyId, CancellationToken ct);
}

/// <summary>
/// Four-tile finance band result. Each tile maps directly into a
/// <see cref="Pages.Shared.Primitives.Cockpit.CockpitKpiTileViewModel"/>
/// — the band primitive renders them in this order under HeroMode.
/// </summary>
public sealed record FinanceKpiBand(
    FinanceKpiTile CashPosition,
    FinanceKpiTile ApDueThisWeek,
    FinanceKpiTile OpenPos,
    FinanceKpiTile WipBalance);

/// <summary>
/// One Finance KPI tile. Maps 1-for-1 to the Cockpit band primitive's
/// CockpitKpiTileViewModel. <see cref="Tone"/> values are from the band
/// primitive's palette: <c>neutral · info · warning · danger · success
/// · brand</c>.
/// </summary>
/// <param name="Label">Tile label (ALL CAPS by convention — band CSS
/// transforms but pass naturally-cased text per Lock 1).</param>
/// <param name="Value">Headline value, pre-formatted (e.g. <c>"$2.4M"</c>,
/// <c>"43"</c>). Renders large. When data is unavailable, use <c>"—"</c>.</param>
/// <param name="SubText">One-line context shown below the value
/// (e.g. <c>"3 invoices · 2 critical"</c>). NULL hides the row.</param>
/// <param name="Tone">Tile colour tone: <c>neutral · info · warning ·
/// danger · success · brand</c>.</param>
public sealed record FinanceKpiTile(
    string Label,
    string Value,
    string? SubText,
    string Tone);
