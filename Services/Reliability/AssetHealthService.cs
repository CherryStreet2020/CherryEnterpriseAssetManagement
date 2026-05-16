using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services.Reliability
{
    // Sprint 2 PR #117.1 — Health score computed from data, not random.
    //
    // The HealthScore is a 0-100 number with three input signals,
    // each contributing a separate penalty deducted from a starting
    // 100. Higher = healthier.
    //
    // 1. RECENT SENSOR BREACHES (last 7 days, capped 40 points)
    //    Each AssetSensorReading with IsOutOfSpec=true contributes
    //    a penalty proportional to how far OOS it was. Capped so a
    //    single very-bad sensor can't blow the whole score.
    //
    // 2. CORRECTIVE WO FREQUENCY (last 90 days, capped 35 points)
    //    Each corrective WorkOrder in the window contributes 6
    //    points. 6 corrective WOs in 90 days = ~capped.
    //
    // 3. OVERDUE WO COUNT (capped 25 points)
    //    Each currently-overdue WO contributes 10 points. 2.5+
    //    overdue WOs caps the penalty.
    //
    // Score is rounded to two decimals and written to
    // Asset.PredictiveHealthScore + .HealthScoreLastCalculated.
    //
    // The breakdown is returned so the Plant Floor detail view can
    // surface WHY an asset is red instead of just "trust me".
    public sealed record HealthBreakdown(
        decimal Score,
        decimal SensorPenalty,
        decimal CorrectivePenalty,
        decimal OverduePenalty,
        int OutOfSpecReadingCount,
        int CorrectiveWoCount,
        int OverdueWoCount);

    public interface IAssetHealthService
    {
        Task<HealthBreakdown> ComputeAsync(int assetId);
        Task<int> RecomputeAllAsync();
    }

    public class AssetHealthService : IAssetHealthService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AssetHealthService> _logger;

        private const decimal SensorPenaltyCap = 40m;
        private const decimal CorrectivePenaltyCap = 35m;
        private const decimal OverduePenaltyCap = 25m;
        private const decimal CorrectivePerEvent = 6m;
        private const decimal OverduePerEvent = 10m;

        public AssetHealthService(AppDbContext db, ILogger<AssetHealthService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<HealthBreakdown> ComputeAsync(int assetId)
        {
            var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId)
                ?? throw new InvalidOperationException($"Asset {assetId} not found.");

            var now = DateTime.UtcNow;
            var sevenDays = now.AddDays(-7);
            var ninetyDays = now.AddDays(-90);

            var sensorBreaches = await _db.AssetSensorReadings
                .Where(r => r.AssetId == assetId
                         && r.IsOutOfSpec
                         && r.ReadingAt >= sevenDays)
                .CountAsync();

            var correctiveCount = await _db.MaintenanceEvents
                .Where(m => m.AssetId == assetId
                         && m.Type == MaintenanceType.Corrective
                         && m.CompletedDate != null
                         && m.CompletedDate >= ninetyDays)
                .CountAsync();

            var overdueCount = await _db.MaintenanceEvents
                .Where(m => m.AssetId == assetId
                         && (m.Status == MaintenanceStatus.Overdue
                          || (m.ScheduledDate < now
                              && (m.Status == MaintenanceStatus.Scheduled
                               || m.Status == MaintenanceStatus.InProgress
                               || m.Status == MaintenanceStatus.OnHold))))
                .CountAsync();

            // Each OOS reading is worth ~2 points, capped at the sensor cap.
            // Demo-grade scaling; real systems would weight by deviation.
            var sensorPenalty = Math.Min(SensorPenaltyCap, sensorBreaches * 2m);
            var correctivePenalty = Math.Min(CorrectivePenaltyCap, correctiveCount * CorrectivePerEvent);
            var overduePenalty = Math.Min(OverduePenaltyCap, overdueCount * OverduePerEvent);

            var score = Math.Max(0m, 100m - sensorPenalty - correctivePenalty - overduePenalty);
            score = Math.Round(score, 2);

            asset.PredictiveHealthScore = score;
            asset.HealthScoreLastCalculated = now;
            await _db.SaveChangesAsync();

            return new HealthBreakdown(
                Score: score,
                SensorPenalty: sensorPenalty,
                CorrectivePenalty: correctivePenalty,
                OverduePenalty: overduePenalty,
                OutOfSpecReadingCount: sensorBreaches,
                CorrectiveWoCount: correctiveCount,
                OverdueWoCount: overdueCount);
        }

        public async Task<int> RecomputeAllAsync()
        {
            var ids = await _db.Assets
                .Where(a => a.Active)
                .Select(a => a.Id)
                .ToListAsync();
            int n = 0;
            foreach (var id in ids)
            {
                await ComputeAsync(id);
                n++;
            }
            _logger.LogInformation("AssetHealthService: recomputed HealthScore for {Count} assets.", n);
            return n;
        }
    }
}
