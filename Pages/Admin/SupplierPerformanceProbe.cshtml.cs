// Sprint 15.4 PR-18 — admin probe for ISupplierPerformanceService.
// Mostly-read service (per cascade design), so the action surface skews to
// reads:
//   Writes (2): Recompute (single vendor+period), Recompute All (period)
//   Reads  (3): Load Current snapshot, Load Scorecard, Load Composite Inputs
//               (the PR-20 ranker hook)
// Lock 16 corollary satisfied — the two recompute buttons exercise the INSERT
// + IsCurrent-flip path that froze the bug-prone xmin/filtered-index logic.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services.Purchasing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Pages.Admin;

[Authorize(Roles = "Admin")]
[Abs.FixedAssets.ControlPlane.ControlPlaneExempt(
    "Admin diagnostic probe. AppDbContext used for read-only count + option " +
    "queries. All writes flow through ISupplierPerformanceService.")]
public sealed class SupplierPerformanceProbeModel : PageModel
{
    private readonly ISupplierPerformanceService _service;
    private readonly AppDbContext _db;
    private readonly ILogger<SupplierPerformanceProbeModel> _logger;

    public SupplierPerformanceProbeModel(
        ISupplierPerformanceService service,
        AppDbContext db,
        ILogger<SupplierPerformanceProbeModel> logger)
    {
        _service = service;
        _db = db;
        _logger = logger;
    }

    // ── Recompute (single) ──
    [BindProperty] public int RecomputeVendorId { get; set; } = 1;
    [BindProperty] public SupplierPerformancePeriod RecomputePeriod { get; set; }
        = SupplierPerformancePeriod.Rolling90Days;

    // ── Recompute all ──
    [BindProperty] public SupplierPerformancePeriod RecomputeAllPeriod { get; set; }
        = SupplierPerformancePeriod.Rolling90Days;

    // ── Reads ──
    [BindProperty] public int CurrentVendorId { get; set; } = 1;
    [BindProperty] public SupplierPerformancePeriod CurrentPeriod { get; set; }
        = SupplierPerformancePeriod.Rolling90Days;
    [BindProperty] public SupplierPerformancePeriod ScorecardPeriod { get; set; }
        = SupplierPerformancePeriod.Rolling90Days;
    [BindProperty] public int CompositeVendorId { get; set; } = 1;

    // ── Output ──
    public string? Outcome { get; private set; }
    public bool OutcomeIsError { get; private set; }

    public int TotalSnapshots { get; private set; }
    public int CurrentSnapshots { get; private set; }
    public int Rolling30Count { get; private set; }
    public int Rolling90Count { get; private set; }
    public int YtdCount { get; private set; }

    public SupplierPerformance? LastCurrent { get; private set; }
    public IReadOnlyList<SupplierScorecardRow>? LastScorecard { get; private set; }
    public SupplierCompositeInputs? LastComposite { get; private set; }

    public IReadOnlyList<VendorOption> Vendors { get; private set; }
        = Array.Empty<VendorOption>();

    public sealed record VendorOption(int Id, string Display);

    private void Set(bool ok, string? msg)
    {
        OutcomeIsError = !ok;
        Outcome = msg;
    }

    public async Task OnGetAsync(CancellationToken ct) => await LoadStatsAsync(ct);

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        TotalSnapshots = await _db.Set<SupplierPerformance>().CountAsync(ct);
        CurrentSnapshots = await _db.Set<SupplierPerformance>()
            .CountAsync(s => s.IsCurrent, ct);
        Rolling30Count = await _db.Set<SupplierPerformance>()
            .CountAsync(s => s.IsCurrent && s.PeriodType == SupplierPerformancePeriod.Rolling30Days, ct);
        Rolling90Count = await _db.Set<SupplierPerformance>()
            .CountAsync(s => s.IsCurrent && s.PeriodType == SupplierPerformancePeriod.Rolling90Days, ct);
        YtdCount = await _db.Set<SupplierPerformance>()
            .CountAsync(s => s.IsCurrent && s.PeriodType == SupplierPerformancePeriod.YearToDate, ct);

        // Vendors that have at least one PO — the recompute candidates.
        var vendorIds = await _db.Set<PurchaseOrder>()
            .Select(p => p.VendorId).Distinct().Take(50).ToListAsync(ct);
        Vendors = await _db.Set<Vendor>()
            .Where(v => vendorIds.Contains(v.Id))
            .OrderBy(v => v.Name)
            .Select(v => new VendorOption(v.Id, $"#{v.Id} — {v.Name}"))
            .ToListAsync(ct);
    }

    // W1) RECOMPUTE (single vendor + period)
    public async Task<IActionResult> OnPostRecomputeAsync(CancellationToken ct)
    {
        var r = await _service.RecomputeAsync(
            RecomputeVendorId, RecomputePeriod, DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Snapshot #{r.Value!.SupplierPerformanceId} for vendor {r.Value.VendorId} ({r.Value.PeriodType}): " +
              $"OTD={Fmt(r.Value.OnTimeDeliveryPct)}%, PPM={Fmt(r.Value.QualityPPM)}, " +
              $"PriceVar={Fmt(r.Value.PriceVariancePct)}%, NCRs={r.Value.NcrCount}, " +
              $"receipt events={r.Value.ReceiptEventsTotal}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // W2) RECOMPUTE ALL (period)
    public async Task<IActionResult> OnPostRecomputeAllAsync(CancellationToken ct)
    {
        var r = await _service.RecomputeAllAsync(RecomputeAllPeriod, DateTime.UtcNow, ct);
        Set(r.IsSuccess, r.IsSuccess
            ? $"Recomputed {r.Value} supplier snapshot(s) for {RecomputeAllPeriod}."
            : r.Error);
        await LoadStatsAsync(ct);
        return Page();
    }

    // R1) LOAD CURRENT (read)
    public async Task<IActionResult> OnPostLoadCurrentAsync(CancellationToken ct)
    {
        LastCurrent = await _service.GetCurrentAsync(CurrentVendorId, CurrentPeriod, ct);
        Set(true, LastCurrent == null
            ? $"Vendor {CurrentVendorId} has no {CurrentPeriod} snapshot yet — run a recompute."
            : $"Vendor {CurrentVendorId} {CurrentPeriod}: OTD={Fmt(LastCurrent.OnTimeDeliveryPct)}%, " +
              $"PPM={Fmt(LastCurrent.QualityPPM)}, PriceVar={Fmt(LastCurrent.PriceVariancePct)}%, " +
              $"NCRs={LastCurrent.NcrCount}, computed {LastCurrent.ComputedAtUtc:yyyy-MM-dd HH:mm} UTC.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // R2) LOAD SCORECARD (read)
    public async Task<IActionResult> OnPostLoadScorecardAsync(CancellationToken ct)
    {
        LastScorecard = await _service.GetScorecardAsync(ScorecardPeriod, ct);
        Set(true, $"{ScorecardPeriod} scorecard: {LastScorecard.Count} supplier(s) with a current snapshot.");
        await LoadStatsAsync(ct);
        return Page();
    }

    // R3) LOAD COMPOSITE INPUTS (read — PR-20 ranker hook)
    public async Task<IActionResult> OnPostLoadCompositeAsync(CancellationToken ct)
    {
        LastComposite = await _service.GetCompositeInputsAsync(CompositeVendorId, ct);
        Set(true, LastComposite.HasCurrentSnapshot
            ? $"PR-20 inputs for vendor {CompositeVendorId}: OTD={Fmt(LastComposite.OnTimeDeliveryPct)}%, " +
              $"PPM={Fmt(LastComposite.QualityPPM)}, PriceVar={Fmt(LastComposite.PriceVariancePct)}%."
            : $"Vendor {CompositeVendorId} has no Rolling90Days snapshot — ranker would fall back to price+lead-time only.");
        await LoadStatsAsync(ct);
        return Page();
    }

    private static string Fmt(decimal? v) => v.HasValue ? v.Value.ToString("N2") : "—";
}
