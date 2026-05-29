// Sprint 15.4 PR-18 — ISupplierPerformanceService implementation.
//
// Patterns adopted (Wave 1-4 reinforcement):
//   - ITenantContext.VisibleCompanyIds gating on every read/write.
//   - Result.Success / Result.Failure return shape.
//   - IsCurrent snapshot management: one IsCurrent row per (Vendor, PeriodType);
//     RecomputeAsync flips the prior to false and inserts a fresh snapshot,
//     guarded by a filtered unique index (concurrent recompute loser hits
//     23505 → clean retry message, mirrors PR-16/17).
//   - Cross-service transaction enlistment (Session 20 lock): when called
//     inside an outer transaction, enlist rather than nest.
//
// All metrics are derived read-only from existing facts, then frozen. Nothing
// here mutates receipts/POs/CARs — the snapshot is the only write.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Models.Engineering;
using Abs.FixedAssets.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Purchasing;

public class SupplierPerformanceService : ISupplierPerformanceService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SupplierPerformanceService> _logger;

    public SupplierPerformanceService(
        AppDbContext db,
        ITenantContext tenantContext,
        ILogger<SupplierPerformanceService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1) RecomputeAsync — the one write. Derive + freeze a snapshot.
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<RecomputeSupplierPerformanceResult>> RecomputeAsync(
        int vendorId, SupplierPerformancePeriod period, DateTime nowUtc,
        CancellationToken ct = default)
    {
        var vendor = await _db.Set<Vendor>()
            .Where(v => v.Id == vendorId)
            .FirstOrDefaultAsync(ct);
        if (vendor == null)
            return Result.Failure<RecomputeSupplierPerformanceResult>(
                $"Vendor {vendorId} not found.");

        if (vendor.CompanyId == null ||
            !_tenantContext.VisibleCompanyIds.Contains(vendor.CompanyId.Value))
            return Result.Failure<RecomputeSupplierPerformanceResult>(
                $"Vendor {vendorId} is out of tenant scope.");

        var companyId = vendor.CompanyId.Value;

        // GoodsReceipt.ReceiptDate is date-grain (defaults to DateTime.Today)
        // stored in a timestamptz column, so window the query on date-floored
        // UTC bounds with an EXCLUSIVE end-of-day upper bound. This avoids the
        // sub-day boundary skew that a raw `<= nowUtc` (mid-day) comparison
        // would cause — a receipt logged "today" is fully included. Kind is
        // preserved as Utc so Npgsql binds against the timestamptz column.
        var windowStart = DateTime.SpecifyKind(
            ComputeWindowStart(period, nowUtc).Date, DateTimeKind.Utc);
        var windowEndExclusive = DateTime.SpecifyKind(
            nowUtc.Date.AddDays(1), DateTimeKind.Utc);

        // ─── Pull the receipt-line facts for this vendor in the window ──────
        // Scope to the vendor via the PO and to the tenant via the PO company.
        // Grain: one row per GoodsReceiptLine (each is a delivered commitment).
        var rows = await (
            from grl in _db.Set<GoodsReceiptLine>()
            join gr in _db.Set<GoodsReceipt>() on grl.GoodsReceiptId equals gr.Id
            join pol in _db.Set<PurchaseOrderLine>() on grl.PurchaseOrderLineId equals pol.Id
            join po in _db.Set<PurchaseOrder>() on gr.PurchaseOrderId equals po.Id
            where po.VendorId == vendorId
                && po.CompanyId == companyId
                && gr.ReceiptDate >= windowStart
                && gr.ReceiptDate < windowEndExclusive
            select new ReceiptFact
            {
                ReceiptDate = gr.ReceiptDate,
                RequiredDate = pol.RequiredDate ?? po.RequiredDate ?? po.PromiseDate,
                QuantityReceived = grl.QuantityReceived,
                QuantityRejected = grl.QuantityRejected,
                UnitPrice = pol.UnitPrice,
                ItemId = pol.ItemId,
            }).ToListAsync(ct);

        // Standard cost lookup for the involved items (PPV basis).
        var itemIds = rows.Where(r => r.ItemId != null)
            .Select(r => r.ItemId!.Value).Distinct().ToList();
        var stdCosts = itemIds.Count == 0
            ? new Dictionary<int, decimal>()
            : await _db.Set<Item>()
                .Where(i => itemIds.Contains(i.Id))
                .Select(i => new { i.Id, i.StandardCost })
                .ToDictionaryAsync(x => x.Id, x => x.StandardCost, ct);

        // ─── OTD ─────────────────────────────────────────────────────────
        // Basis = receipt lines that have a comparable required/promise date.
        // On-time = received on or before that date (date-grain comparison).
        var otdBasis = rows.Where(r => r.RequiredDate.HasValue).ToList();
        var onTime = otdBasis.Count(r => r.ReceiptDate.Date <= r.RequiredDate!.Value.Date);
        decimal? otdPct = otdBasis.Count > 0
            ? Round4((decimal)onTime / otdBasis.Count * 100m)
            : null;

        // ─── Quality PPM ───────────────────────────────────────────────────
        var qtyReceived = rows.Sum(r => r.QuantityReceived);
        var qtyRejected = rows.Sum(r => r.QuantityRejected);
        decimal? ppm = qtyReceived > 0m
            ? Round4(qtyRejected / qtyReceived * 1_000_000m)
            : null;

        // ─── Purchase price variance ───────────────────────────────────────
        // Basis = received lines whose item has a standard cost > 0.
        var ppvBasis = rows
            .Where(r => r.ItemId != null
                && stdCosts.TryGetValue(r.ItemId.Value, out var sc) && sc > 0m)
            .ToList();
        var stdBasisAmount = ppvBasis.Sum(r => r.QuantityReceived * stdCosts[r.ItemId!.Value]);
        var actualAmount = ppvBasis.Sum(r => r.QuantityReceived * r.UnitPrice);
        decimal? ppvPct = stdBasisAmount > 0m
            ? Round4((actualAmount - stdBasisAmount) / stdBasisAmount * 100m)
            : null;

        // ─── NCR count ─────────────────────────────────────────────────────
        // CARs raised against this supplier in the window (by CreatedAt so
        // drafts with no IssuedAtUtc still count).
        var ncrCount = await _db.Set<CorrectiveActionRequest>()
            .CountAsync(c => c.VendorId == vendorId
                && c.CompanyId == companyId
                && c.CreatedAt >= windowStart
                && c.CreatedAt < windowEndExclusive, ct);

        // ─── Freeze the snapshot (flip prior IsCurrent → insert fresh) ──────
        var existingTx = _db.Database.CurrentTransaction;
        var tx = existingTx ?? await _db.Database.BeginTransactionAsync(ct);
        var ownsTx = existingTx == null;
        try
        {
            var priorCurrent = await _db.Set<SupplierPerformance>()
                .Where(s => s.VendorId == vendorId
                    && s.PeriodType == period
                    && s.IsCurrent)
                .ToListAsync(ct);
            foreach (var prior in priorCurrent)
            {
                prior.IsCurrent = false;
                prior.UpdatedAt = nowUtc;
            }

            var snapshot = new SupplierPerformance
            {
                CompanyId = companyId,
                VendorId = vendorId,
                PeriodType = period,
                PeriodStartUtc = windowStart,
                PeriodEndUtc = nowUtc,
                ComputedAtUtc = nowUtc,
                ReceiptEventsTotal = otdBasis.Count,
                ReceiptEventsOnTime = onTime,
                OnTimeDeliveryPct = otdPct,
                QuantityReceivedTotal = qtyReceived,
                QuantityRejectedTotal = qtyRejected,
                QualityPPM = ppm,
                NcrCount = ncrCount,
                PriceVarianceBasisLineCount = ppvBasis.Count,
                StandardCostBasisAmount = stdBasisAmount,
                ActualCostAmount = actualAmount,
                PriceVariancePct = ppvPct,
                IsCurrent = true,
                CreatedAt = nowUtc,
            };
            _db.Add(snapshot);

            await _db.SaveChangesAsync(ct);
            if (ownsTx) await tx.CommitAsync(ct);

            return Result.Success(new RecomputeSupplierPerformanceResult(
                snapshot.Id, vendorId, period, otdPct, ppm, ppvPct, ncrCount,
                otdBasis.Count,
                $"Snapshot frozen for {period} ({windowStart:yyyy-MM-dd} → {nowUtc:yyyy-MM-dd})."));
        }
        catch (DbUpdateException dbex)
        {
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            if (IsPostgresUniqueViolation(dbex))
            {
                // Concurrent RecomputeAsync for the same (Vendor, PeriodType)
                // raced; the filtered unique index rejected the second insert.
                return Result.Failure<RecomputeSupplierPerformanceResult>(
                    "A concurrent recompute for this supplier/period just landed. Retry.");
            }
            _logger.LogError(dbex,
                "RecomputeAsync failed for vendor {VendorId} period {Period}.",
                vendorId, period);
            return Result.Failure<RecomputeSupplierPerformanceResult>(
                "Failed to persist supplier performance snapshot.");
        }
        catch
        {
            if (ownsTx) await SafeRollbackAsync(tx, ct);
            throw;
        }
        finally
        {
            if (ownsTx) await tx.DisposeAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2) RecomputeAllAsync — every vendor with PO activity in scope.
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<Result<int>> RecomputeAllAsync(
        SupplierPerformancePeriod period, DateTime nowUtc, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        if (visible == null || visible.Count == 0)
            return Result.Failure<int>("No tenant scope — cannot recompute.");

        var vendorIds = await _db.Set<PurchaseOrder>()
            .Where(po => po.CompanyId != null
                && visible.Contains(po.CompanyId.Value)
                && po.VendorId != 0)
            .Select(po => po.VendorId)
            .Distinct()
            .ToListAsync(ct);

        var written = 0;
        foreach (var vendorId in vendorIds)
        {
            var r = await RecomputeAsync(vendorId, period, nowUtc, ct);
            if (r.IsSuccess) written++;
        }
        return Result.Success(written);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3) Reads
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<SupplierPerformance?> GetCurrentAsync(
        int vendorId, SupplierPerformancePeriod period, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        return await _db.Set<SupplierPerformance>()
            .Where(s => s.VendorId == vendorId
                && s.PeriodType == period
                && s.IsCurrent
                && s.CompanyId != null
                && visible.Contains(s.CompanyId.Value))
            .OrderByDescending(s => s.ComputedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<SupplierScorecardRow>> GetScorecardAsync(
        SupplierPerformancePeriod period, CancellationToken ct = default)
    {
        var visible = _tenantContext.VisibleCompanyIds;
        var rows = await (
            from s in _db.Set<SupplierPerformance>()
            join v in _db.Set<Vendor>() on s.VendorId equals v.Id
            where s.PeriodType == period
                && s.IsCurrent
                && s.CompanyId != null
                && visible.Contains(s.CompanyId.Value)
            select new SupplierScorecardRow(
                s.VendorId,
                v.Name,
                s.PeriodType,
                s.OnTimeDeliveryPct,
                s.QualityPPM,
                s.PriceVariancePct,
                s.NcrCount,
                s.ReceiptEventsTotal,
                s.QuantityReceivedTotal,
                s.ComputedAtUtc))
            .ToListAsync(ct);

        // Worst-first: lowest OTD at the top so risk surfaces. Nulls (no basis)
        // sort last — they are "unknown", not "bad".
        return rows
            .OrderBy(r => r.OnTimeDeliveryPct.HasValue ? 0 : 1)
            .ThenBy(r => r.OnTimeDeliveryPct ?? decimal.MaxValue)
            .ThenByDescending(r => r.NcrCount)
            .ToList();
    }

    public async Task<SupplierCompositeInputs> GetCompositeInputsAsync(
        int vendorId, CancellationToken ct = default)
    {
        // PR-20 ranker reads the Rolling90Days window.
        var snap = await GetCurrentAsync(
            vendorId, SupplierPerformancePeriod.Rolling90Days, ct);
        if (snap == null)
            return new SupplierCompositeInputs(vendorId, null, null, null, false);

        return new SupplierCompositeInputs(
            vendorId,
            snap.OnTimeDeliveryPct,
            snap.QualityPPM,
            snap.PriceVariancePct,
            true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static DateTime ComputeWindowStart(SupplierPerformancePeriod period, DateTime nowUtc) =>
        period switch
        {
            SupplierPerformancePeriod.Rolling30Days => nowUtc.AddDays(-30),
            SupplierPerformancePeriod.Rolling90Days => nowUtc.AddDays(-90),
            SupplierPerformancePeriod.YearToDate => new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => nowUtc.AddDays(-90),
        };

    private static decimal Round4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

    private static async Task SafeRollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct)
    {
        try { await tx.RollbackAsync(ct); } catch { /* already rolled back / disposed */ }
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex)
    {
        for (var e = ex.InnerException; e != null; e = e.InnerException)
        {
            var sqlStateProp = e.GetType().GetProperty("SqlState");
            if (sqlStateProp?.GetValue(e) is string sqlState && sqlState == "23505")
                return true;
        }
        return false;
    }

    /// <summary>Internal projection shape for receipt-line facts.</summary>
    private sealed class ReceiptFact
    {
        public DateTime ReceiptDate { get; init; }
        public DateTime? RequiredDate { get; init; }
        public decimal QuantityReceived { get; init; }
        public decimal QuantityRejected { get; init; }
        public decimal UnitPrice { get; init; }
        public int? ItemId { get; init; }
    }
}
