// Sprint 15.4 PR-18 — Vendor Performance / Scorecard service.
//
// Spec ref: docs/research/purchasing-subcontracting-supply-demand-dean-research.txt
//   §21 tab 13 (Supplier Performance) + §25 KPIs (Supplier OTD / quality PPM /
//   purchase price variance / supplier NCR count).
// Cascade ref: docs/research/purchasing-cascade-design-2026-05-28.md PR-18.
//
// This is a MOSTLY-READ aggregate service. The one write is RecomputeAsync,
// which derives the four scorecard metrics from existing facts and freezes a
// SupplierPerformance snapshot. Everything else reads the current snapshot.
//
// Metric derivation (all tenant-scoped, all windowed by GoodsReceipt.ReceiptDate):
//   * OnTimeDeliveryPct — receipt events delivered on/before the line's
//     required date (PO line RequiredDate, falling back to PO header
//     RequiredDate then PromiseDate). Receipts with no comparable date are
//     excluded from the basis rather than counted on-time.
//   * QualityPPM — Σ QuantityRejected / Σ QuantityReceived * 1,000,000.
//   * PriceVariancePct — Σ(qty·UnitPrice) vs Σ(qty·Item.StandardCost) over
//     received lines whose item has a standard cost > 0. Positive = above
//     standard (unfavorable).
//   * NcrCount — CorrectiveActionRequests with VendorId == vendor in the window.
//
// PR-20 dependency (locked): GetCompositeInputsAsync returns the three ranker
// inputs (OTD %, quality PPM, price variance %) off the Rolling90Days snapshot
// so the quote ranker composes with one read.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services.Purchasing;

// === Result records ===

/// <summary>Outcome of <see cref="ISupplierPerformanceService.RecomputeAsync"/>.</summary>
public sealed record RecomputeSupplierPerformanceResult(
    int SupplierPerformanceId,
    int VendorId,
    SupplierPerformancePeriod PeriodType,
    decimal? OnTimeDeliveryPct,
    decimal? QualityPPM,
    decimal? PriceVariancePct,
    int NcrCount,
    int ReceiptEventsTotal,
    string? Message);

/// <summary>
/// One row in the supplier scorecard — the current snapshot for a vendor over
/// a period, flattened for the §21 tab 13 grid and the admin probe. VendorName
/// is denormalized for display so callers don't re-join Vendor.
/// </summary>
public sealed record SupplierScorecardRow(
    int VendorId,
    string VendorName,
    SupplierPerformancePeriod PeriodType,
    decimal? OnTimeDeliveryPct,
    decimal? QualityPPM,
    decimal? PriceVariancePct,
    int NcrCount,
    int ReceiptEventsTotal,
    decimal QuantityReceivedTotal,
    System.DateTime? ComputedAtUtc);

/// <summary>The three composite-score inputs PR-20's quote ranker consumes.</summary>
public sealed record SupplierCompositeInputs(
    int VendorId,
    decimal? OnTimeDeliveryPct,
    decimal? QualityPPM,
    decimal? PriceVariancePct,
    bool HasCurrentSnapshot);

public interface ISupplierPerformanceService
{
    /// <summary>
    /// Recompute and freeze the scorecard for one supplier + period. Derives
    /// OTD / PPM / price variance / NCR count from receipts, POs, item standard
    /// cost, and CARs in the window, flips any prior IsCurrent snapshot for the
    /// (Vendor, PeriodType) pair to false, and inserts a fresh IsCurrent
    /// snapshot. Tenant-scoped: refuses vendors outside VisibleCompanyIds.
    /// </summary>
    Task<Result<RecomputeSupplierPerformanceResult>> RecomputeAsync(
        int vendorId,
        SupplierPerformancePeriod period,
        System.DateTime nowUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Recompute the given period for every vendor that has had purchasing
    /// activity (a PO) in tenant scope. Returns the number of snapshots
    /// written. Drives the "Recompute All" probe button + a future scheduled job.
    /// </summary>
    Task<Result<int>> RecomputeAllAsync(
        SupplierPerformancePeriod period,
        System.DateTime nowUtc,
        CancellationToken ct = default);

    /// <summary>Return the current snapshot for a vendor + period (null if never computed).</summary>
    Task<SupplierPerformance?> GetCurrentAsync(
        int vendorId,
        SupplierPerformancePeriod period,
        CancellationToken ct = default);

    /// <summary>
    /// Return the current scorecard rows for all vendors with a snapshot in the
    /// given period, ranked worst-OTD-first so the buyer sees risk at the top.
    /// Drives §21 tab 13 + the probe scorecard read.
    /// </summary>
    Task<IReadOnlyList<SupplierScorecardRow>> GetScorecardAsync(
        SupplierPerformancePeriod period,
        CancellationToken ct = default);

    /// <summary>
    /// PR-20 composition hook. Returns the three ranker inputs off the
    /// Rolling90Days current snapshot. HasCurrentSnapshot=false (with null
    /// metrics) when the vendor has never been computed — the ranker then
    /// falls back to its price+lead-time-only formula.
    /// </summary>
    Task<SupplierCompositeInputs> GetCompositeInputsAsync(
        int vendorId,
        CancellationToken ct = default);
}
