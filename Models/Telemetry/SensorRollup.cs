using System;

namespace Abs.FixedAssets.Models.Telemetry
{
    // Sprint 2 PR #118.1 — Continuous-aggregate rollup view entities.
    //
    // TimescaleDB continuous aggregates are materialized views that
    // incrementally refresh as new SensorEvent rows arrive. We surface
    // them to EF as keyless read-only view entities (HasNoKey + ToView
    // in OnModelCreating).
    //
    // Three grains, same shape:
    //   SensorRollupMinute   - bucket_size = 1 minute
    //   SensorRollupHour     - bucket_size = 1 hour
    //   SensorRollupDay      - bucket_size = 1 day
    //
    // The Plant Floor view's sparkline tiles read Minute. Asset detail
    // charts read Hour. OEE (PR #126) and Weibull (PR #131) read Day.
    // We NEVER aggregate the raw SensorEvent hypertable in app code;
    // every chart hits a rollup.
    //
    // OosCount and the per-NE-107-state counts let dashboards reason
    // about data quality alongside the value itself.

    public sealed record SensorRollupMinute(
        int AssetId,
        SensorReadingType ReadingType,
        DateTime BucketStart,
        decimal AvgValue,
        decimal MinValue,
        decimal MaxValue,
        decimal? StdDev,
        long SampleCount,
        long OosCount,
        long UncertainCount,
        long FailureCount,
        long MaintenanceCount);

    public sealed record SensorRollupHour(
        int AssetId,
        SensorReadingType ReadingType,
        DateTime BucketStart,
        decimal AvgValue,
        decimal MinValue,
        decimal MaxValue,
        decimal? StdDev,
        long SampleCount,
        long OosCount,
        long UncertainCount,
        long FailureCount,
        long MaintenanceCount);

    public sealed record SensorRollupDay(
        int AssetId,
        SensorReadingType ReadingType,
        DateTime BucketStart,
        decimal AvgValue,
        decimal MinValue,
        decimal MaxValue,
        decimal? StdDev,
        long SampleCount,
        long OosCount,
        long UncertainCount,
        long FailureCount,
        long MaintenanceCount);
}
