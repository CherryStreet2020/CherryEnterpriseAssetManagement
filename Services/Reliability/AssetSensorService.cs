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
    // Sprint 2 PR #117.1 — Sensor ingestion + lookup service.
    //
    // Single write path for sensor data. Every reading lands in
    // AssetSensorReadings (source of truth). The cached Asset.Current*
    // columns get updated atomically in the same transaction so the
    // Plant Floor view stays fast without N+1 joins.
    //
    // RecordReadingAsync also marks IsOutOfSpec when the reading is
    // outside the asset's expected operating range — AssetHealthService
    // counts these to compute HealthScore without re-evaluating
    // thresholds on every score calc.
    //
    // GetLatest / GetHistory: read-side queries.
    public sealed record SensorThresholds(decimal Min, decimal Max);

    public interface IAssetSensorService
    {
        Task<AssetSensorReading> RecordReadingAsync(
            int assetId,
            SensorReadingType type,
            decimal value,
            string unit,
            DateTime readingAt,
            string source = "demo");

        Task<IReadOnlyList<AssetSensorReading>> RecordBatchAsync(IEnumerable<AssetSensorReading> readings);

        // PR #117.3: chunked bulk insert path so the seeder can persist
        // hundreds of thousands of readings without blowing past Replit's
        // HTTP timeout or the EF change-tracker memory ceiling. Caller
        // sets IsOutOfSpec; this method does NOT update the Asset.Current*
        // cache (the seeder does its own final cache pass).
        Task<int> RecordBatchChunkedAsync(
            IEnumerable<AssetSensorReading> readings,
            int chunkSize = 25_000);

        Task<AssetSensorReading?> GetLatestAsync(int assetId, SensorReadingType type);

        Task<IReadOnlyList<AssetSensorReading>> GetHistoryAsync(
            int assetId,
            SensorReadingType type,
            DateTime sinceUtc);

        SensorThresholds GetExpectedRange(SensorReadingType type, Asset asset);
    }

    public class AssetSensorService : IAssetSensorService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AssetSensorService> _logger;

        public AssetSensorService(AppDbContext db, ILogger<AssetSensorService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<AssetSensorReading> RecordReadingAsync(
            int assetId, SensorReadingType type, decimal value, string unit,
            DateTime readingAt, string source = "demo")
        {
            var asset = await _db.Assets.FirstOrDefaultAsync(a => a.Id == assetId)
                ?? throw new InvalidOperationException($"Asset {assetId} not found.");

            var range = GetExpectedRange(type, asset);
            var isOutOfSpec = value < range.Min || value > range.Max;

            var reading = new AssetSensorReading
            {
                AssetId = assetId,
                ReadingType = type,
                Value = value,
                Unit = unit,
                ReadingAt = readingAt,
                Source = source,
                IsOutOfSpec = isOutOfSpec,
                CreatedAt = DateTime.UtcNow
            };
            _db.AssetSensorReadings.Add(reading);

            UpdateAssetCache(asset, type, value, readingAt);
            await _db.SaveChangesAsync();
            return reading;
        }

        public async Task<int> RecordBatchChunkedAsync(IEnumerable<AssetSensorReading> readings, int chunkSize = 25_000)
        {
            // PR #117.3 — bulk path that survives Replit's HTTP timeout.
            // PR #117.2's seeder generated ~1.4M readings and shoved them
            // into ONE SaveChangesAsync, which silently aborted before the
            // request returned — leaving the asset metadata rewritten but
            // zero readings persisted. This method:
            //   1) yields after every chunk so the request stays alive
            //   2) clears the change tracker between chunks so memory
            //      doesn't grow unbounded
            //   3) logs progress every chunk so a stalled seed is visible
            //   4) does NOT update the Asset.Current* cache here — that's
            //      now the caller's responsibility (the seeder does a
            //      single small cache pass at the end).
            int total = 0;
            var bucket = new List<AssetSensorReading>(chunkSize);
            try
            {
                foreach (var r in readings)
                {
                    bucket.Add(r);
                    if (bucket.Count >= chunkSize)
                    {
                        await FlushAsync(bucket);
                        total += bucket.Count;
                        _logger.LogInformation("RecordBatchChunkedAsync: persisted {Count} readings (total {Total}).", bucket.Count, total);
                        bucket.Clear();
                    }
                }
                if (bucket.Count > 0)
                {
                    await FlushAsync(bucket);
                    total += bucket.Count;
                    _logger.LogInformation("RecordBatchChunkedAsync: persisted final {Count} readings (total {Total}).", bucket.Count, total);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RecordBatchChunkedAsync: failure after {Total} readings.", total);
                throw;
            }
            return total;
        }

        private async Task FlushAsync(List<AssetSensorReading> bucket)
        {
            _db.AssetSensorReadings.AddRange(bucket);
            await _db.SaveChangesAsync();
            // Detach to keep the change tracker small across chunks.
            foreach (var r in bucket)
                _db.Entry(r).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }

        public async Task<IReadOnlyList<AssetSensorReading>> RecordBatchAsync(IEnumerable<AssetSensorReading> readings)
        {
            // For seeders / bulk ingestion. Caller is responsible for
            // setting IsOutOfSpec correctly (or leaving it false). The
            // cache is updated to the latest reading per (asset, type)
            // in the batch only — the heavy update path.
            //
            // PR #117.3 — prefer RecordBatchChunkedAsync for large batches.
            // This method still works for small batches (e.g. test fixtures).
            var list = readings.ToList();
            if (list.Count == 0) return list;

            _db.AssetSensorReadings.AddRange(list);

            // Resolve latest per (asset, type) in the batch.
            var latestPerKey = list
                .GroupBy(r => (r.AssetId, r.ReadingType))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingAt).First());

            var assetIds = latestPerKey.Keys.Select(k => k.AssetId).Distinct().ToList();
            var assets = await _db.Assets.Where(a => assetIds.Contains(a.Id)).ToListAsync();
            foreach (var asset in assets)
            {
                foreach (var type in new[] { SensorReadingType.Temperature, SensorReadingType.Vibration, SensorReadingType.Pressure })
                {
                    if (latestPerKey.TryGetValue((asset.Id, type), out var r))
                    {
                        UpdateAssetCache(asset, type, r.Value, r.ReadingAt);
                    }
                }
            }

            await _db.SaveChangesAsync();
            return list;
        }

        public async Task<AssetSensorReading?> GetLatestAsync(int assetId, SensorReadingType type)
        {
            return await _db.AssetSensorReadings
                .Where(r => r.AssetId == assetId && r.ReadingType == type)
                .OrderByDescending(r => r.ReadingAt)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<AssetSensorReading>> GetHistoryAsync(int assetId, SensorReadingType type, DateTime sinceUtc)
        {
            return await _db.AssetSensorReadings
                .Where(r => r.AssetId == assetId && r.ReadingType == type && r.ReadingAt >= sinceUtc)
                .OrderBy(r => r.ReadingAt)
                .ToListAsync();
        }

        public SensorThresholds GetExpectedRange(SensorReadingType type, Asset asset)
        {
            // Thresholds keyed off the asset's class hint. For best-in-class
            // we'd store these per-asset (or per AssetCategory); demo-grade
            // is class-keyed.
            var hint = (asset.AssetType ?? asset.Description ?? "").ToLowerInvariant();

            return type switch
            {
                SensorReadingType.Temperature => hint switch
                {
                    var s when s.Contains("welding") || s.Contains("welder") => new SensorThresholds(60m, 220m),
                    var s when s.Contains("cnc") || s.Contains("lathe") || s.Contains("mill") || s.Contains("turning") || s.Contains("machining") => new SensorThresholds(60m, 175m),
                    var s when s.Contains("press") || s.Contains("stamping") => new SensorThresholds(60m, 195m),
                    var s when s.Contains("robot") => new SensorThresholds(60m, 165m),
                    var s when s.Contains("hvac") => new SensorThresholds(50m,  95m),
                    _ => new SensorThresholds(60m, 165m)
                },
                SensorReadingType.Vibration => new SensorThresholds(0m, 4.5m),  // ISO 10816 zone B/C boundary as a single threshold
                SensorReadingType.Pressure => hint switch
                {
                    var s when s.Contains("hydraulic") || s.Contains("press") || s.Contains("stamping") => new SensorThresholds(800m, 3000m),
                    var s when s.Contains("pneumatic") || s.Contains("compressor") => new SensorThresholds(80m, 145m),
                    var s when s.Contains("hvac") => new SensorThresholds(15m,  45m),
                    _ => new SensorThresholds(40m, 120m)
                },
                _ => new SensorThresholds(decimal.MinValue, decimal.MaxValue)
            };
        }

        private static void UpdateAssetCache(Asset asset, SensorReadingType type, decimal value, DateTime at)
        {
            switch (type)
            {
                case SensorReadingType.Temperature: asset.CurrentTemperature = Math.Round(value, 1); break;
                case SensorReadingType.Vibration:   asset.CurrentVibration   = Math.Round(value, 3); break;
                case SensorReadingType.Pressure:    asset.CurrentPressure    = Math.Round(value, 2); break;
            }
            if (asset.SensorReadingsLastUpdated == null || at > asset.SensorReadingsLastUpdated)
                asset.SensorReadingsLastUpdated = at;
        }
    }
}
