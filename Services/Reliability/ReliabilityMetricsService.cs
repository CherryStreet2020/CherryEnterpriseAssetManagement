using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services.Reliability
{
    /// <summary>
    /// PR #110: Per-asset reliability metrics. Powers the new
    /// /Reports/AssetReliability page, the asset-detail reliability tiles,
    /// and the upcoming sortable Asset Register columns.
    ///
    /// Computes three numbers per asset over a configurable window:
    ///   - MTBF (Mean Time Between Failures) — average gap, in hours,
    ///     between consecutive Corrective WO CompletedDate values. Requires
    ///     at least 2 completed corrective WOs in the window to produce a
    ///     value; otherwise null.
    ///   - Availability % — (windowHours − totalDowntimeHours) / windowHours.
    ///     Downtime = sum of (CompletedDate − StartedAt) across all WOs that
    ///     completed in the window. Clamped to [0, 100].
    ///   - SpendInWindow — sum of WO-LBR + WO-ISS-OP JE debits minus
    ///     WO-RTN / WO-RTN-OP refunds, attributed to this asset's WOs whose
    ///     CompletedDate fell in the window. Same source-of-truth as the
    ///     Pareto report (PR #112) and dashboard tiles (PR #108).
    ///
    /// Single-pass implementation: pulls all relevant WOs + JEs once, then
    /// computes per-asset rollups in memory. Quadratic in the number of WOs
    /// per asset but with realistic per-asset counts (<100) that's fine.
    /// </summary>
    public class ReliabilityMetricsService
    {
        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;

        public ReliabilityMetricsService(AppDbContext db, ITenantContext tenant)
        {
            _db = db;
            _tenant = tenant;
        }

        public sealed record AssetReliabilityRow(
            int AssetId,
            string AssetNumber,
            string Description,
            int? SiteId,
            int CorrectiveWoCount,
            decimal? MtbfHours,
            decimal AvailabilityPercent,
            decimal SpendInWindow);

        public async Task<List<AssetReliabilityRow>> ComputeAsync(DateTime startDate, DateTime endDate)
        {
            var endInclusive = endDate.Date.AddDays(1).AddTicks(-1);
            var windowHours = (decimal)(endInclusive - startDate.Date).TotalHours;
            if (windowHours <= 0m) windowHours = 1m; // guard against zero-window
            var visibleIds = _tenant.VisibleCompanyIds;

            // 1. Pull all WOs in window across visible companies.
            //    We need: AssetId, CompletedDate, StartedAt, Type, header rollup.
            var wos = await (
                from m in _db.MaintenanceEvents
                join a in _db.Assets on m.AssetId equals a.Id
                where m.Status == MaintenanceStatus.Completed
                  && m.CompletedDate.HasValue
                  && m.CompletedDate >= startDate
                  && m.CompletedDate <= endInclusive
                  && a.CompanyId.HasValue && visibleIds.Contains(a.CompanyId.Value)
                select new
                {
                    WoId = m.Id,
                    AssetId = a.Id,
                    AssetNumber = a.AssetNumber,
                    Description = a.Description ?? "",
                    a.SiteId,
                    Type = m.Type,
                    StartedAt = m.StartedAt,
                    CompletedDate = m.CompletedDate!.Value,
                    HeaderLabor = m.LaborCost ?? 0m,
                    HeaderMaterials = m.MaterialsCost ?? 0m
                }).ToListAsync();

            if (wos.Count == 0)
                return new List<AssetReliabilityRow>();

            // 2. Build a per-WO cost lookup from the JE table. Same parsing as
            //    the Pareto report — extract the WO id from the Reference prefix.
            var woIds = wos.Select(w => w.WoId).ToHashSet();
            var jeRows = await (
                from j in _db.JournalEntries
                join l in _db.JournalLines on j.Id equals l.JournalEntryId
                where l.Debit > 0m
                  && (j.Source == "WO-LBR" || j.Source == "WO-ISS" || j.Source == "WO-ISS-OP"
                      || j.Source == "WO-RTN" || j.Source == "WO-RTN-OP")
                  && j.Reference != null
                select new { j.Reference, j.Source, l.Debit }).ToListAsync();

            var costByWo = new Dictionary<int, decimal>();
            foreach (var row in jeRows)
            {
                if (row.Reference is null) continue;
                var parts = row.Reference.Split('-');
                int idx = (row.Source == "WO-ISS-OP" || row.Source == "WO-RTN-OP") ? 3 : 2;
                if (parts.Length <= idx) continue;
                if (!int.TryParse(parts[idx], out var woId)) continue;
                if (!woIds.Contains(woId)) continue;
                var signed = (row.Source == "WO-RTN" || row.Source == "WO-RTN-OP") ? -row.Debit : row.Debit;
                costByWo.TryGetValue(woId, out var existing);
                costByWo[woId] = existing + signed;
            }

            // 3. Roll up per asset.
            var perAsset = wos
                .GroupBy(w => w.AssetId)
                .Select(g =>
                {
                    var first = g.First();
                    var correctiveCloses = g
                        .Where(w => w.Type == MaintenanceType.Corrective)
                        .Select(w => w.CompletedDate)
                        .OrderBy(d => d)
                        .ToList();

                    // MTBF: avg gap (hours) between consecutive corrective closes
                    decimal? mtbf = null;
                    if (correctiveCloses.Count >= 2)
                    {
                        decimal sumGapHours = 0m;
                        for (int i = 1; i < correctiveCloses.Count; i++)
                        {
                            sumGapHours += (decimal)(correctiveCloses[i] - correctiveCloses[i - 1]).TotalHours;
                        }
                        mtbf = Math.Round(sumGapHours / (correctiveCloses.Count - 1), 1);
                    }

                    // Availability: window hours - downtime hours
                    // Downtime per WO = CompletedDate - StartedAt (when both set)
                    decimal downtimeHours = 0m;
                    foreach (var w in g)
                    {
                        if (w.StartedAt.HasValue)
                        {
                            var dt = (decimal)(w.CompletedDate - w.StartedAt.Value).TotalHours;
                            if (dt > 0m) downtimeHours += dt;
                        }
                    }
                    var availability = windowHours <= 0m
                        ? 100m
                        : Math.Max(0m, Math.Min(100m, Math.Round(100m * (windowHours - downtimeHours) / windowHours, 2)));

                    // Spend = sum of JE-sourced costs (fallback to header rollup
                    // for any WO whose JEs haven't posted — matches Pareto report).
                    decimal spend = 0m;
                    foreach (var w in g)
                    {
                        var je = costByWo.GetValueOrDefault(w.WoId, 0m);
                        spend += je > 0m ? je : (w.HeaderLabor + w.HeaderMaterials);
                    }

                    return new AssetReliabilityRow(
                        AssetId: first.AssetId,
                        AssetNumber: first.AssetNumber,
                        Description: first.Description,
                        SiteId: first.SiteId,
                        CorrectiveWoCount: correctiveCloses.Count,
                        MtbfHours: mtbf,
                        AvailabilityPercent: availability,
                        SpendInWindow: spend);
                })
                .ToList();

            return perAsset;
        }

        /// <summary>
        /// Single-asset variant used by the asset-detail tiles. Same window
        /// semantics; returns null if no WOs landed in the period.
        /// </summary>
        public async Task<AssetReliabilityRow?> ComputeForAssetAsync(int assetId, DateTime startDate, DateTime endDate)
        {
            var all = await ComputeAsync(startDate, endDate);
            return all.FirstOrDefault(r => r.AssetId == assetId);
        }
    }
}
