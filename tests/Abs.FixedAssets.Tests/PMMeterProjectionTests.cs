using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Abs.FixedAssets.Services.Maintenance;
using Abs.FixedAssets.Services.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// S2-2 — projection of meter-crossing dates for meter-driven PM templates.
/// PMSchedulerService.ProjectMeterCrossingDateAsync uses linear velocity
/// across the lookback window of MeterReading rows. These tests pin the
/// math so future tuning (regression, seasonal weighting, etc.) doesn't
/// accidentally regress the linear baseline.
/// </summary>
public class PMMeterProjectionTests
{
    private sealed class TestAppDbContext : AppDbContext
    {
        public TestAppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);
            mb.Entity<LookupValue>().Ignore(x => x.Metadata);
            mb.Entity<Asset>().Ignore(a => a.RowVersion);
        }
    }

    private static AppDbContext NewDb([System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pm-meter-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public int? TenantId { get; init; } = 1;
        public int? CompanyId { get; init; } = 1;
        public int? SiteId { get; init; }
        public int? AssignedCompanyId { get; init; }
        public int? AssignedSiteId { get; init; }
        public List<int> VisibleCompanyIds { get; init; } = new() { 1 };
        public List<int> VisibleSiteIds { get; init; } = new();
        public bool IsResolved => true;
        public string? ResolutionError => null;
        public void SetContext(int? tenantId, int? companyId, int? siteId) { }
        public void SetHierarchyContext(int? assignedCompanyId, List<int> visibleCompanyIds) { }
        public void SetSiteHierarchyContext(int? assignedSiteId, List<int> visibleSiteIds) { }
        public void SetError(string error) { }
    }

    private static PMSchedulerService NewScheduler(AppDbContext db)
    {
        var tenant = new StubTenantContext();
        var outbox = new OutboxWriter(db, tenant, NullLogger<OutboxWriter>.Instance);
        return new PMSchedulerService(db, tenant, NullLogger<PMSchedulerService>.Instance, outbox);
    }

    [Fact]
    public async Task Projection_LinearVelocity_ReturnsExpectedDate()
    {
        using var db = NewDb();
        var asOf = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        // 30 days of readings, advancing 10 hours/day from 100 → 400.
        for (int day = 0; day <= 30; day++)
        {
            db.MeterReadings.Add(new MeterReading
            {
                AssetId = 42,
                MeterType = MeterType.Hours,
                Reading = 100m + day * 10m,
                ReadingDate = asOf.AddDays(-30 + day)
            });
        }
        await db.SaveChangesAsync();

        var svc = NewScheduler(db);
        var result = await svc.ProjectMeterCrossingDateAsync(
            assetId: 42,
            meterType: MeterType.Hours,
            targetReading: 500m,    // need 100 more hours, at 10/day → 10 days
            asOfUtc: asOf);

        Assert.Null(result.UnprojectableReason);
        Assert.Equal(10m, result.Velocity);
        Assert.Equal(31, result.ReadingsUsed);
        Assert.Equal(400m, result.LatestReading);
        Assert.NotNull(result.ProjectedCrossingUtc);
        // Latest reading was on asOf; project 100/10 = 10 days forward.
        Assert.Equal(asOf.AddDays(10).Date, result.ProjectedCrossingUtc!.Value.Date);
    }

    [Fact]
    public async Task Projection_TargetAlreadyCrossed_ReturnsLatestReadingDate()
    {
        using var db = NewDb();
        var asOf = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        db.MeterReadings.Add(new MeterReading
        {
            AssetId = 99,
            MeterType = MeterType.Hours,
            Reading = 600m,
            ReadingDate = asOf.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var svc = NewScheduler(db);
        var result = await svc.ProjectMeterCrossingDateAsync(
            assetId: 99,
            meterType: MeterType.Hours,
            targetReading: 500m,    // already past
            asOfUtc: asOf);

        Assert.Null(result.UnprojectableReason);
        Assert.NotNull(result.ProjectedCrossingUtc);
        Assert.Equal(asOf.AddDays(-1).Date, result.ProjectedCrossingUtc!.Value.Date);
    }

    [Fact]
    public async Task Projection_NoReadings_ReturnsUnprojectableWithReason()
    {
        using var db = NewDb();
        var svc = NewScheduler(db);

        var result = await svc.ProjectMeterCrossingDateAsync(
            assetId: 1,
            meterType: MeterType.Hours,
            targetReading: 100m);

        Assert.Null(result.ProjectedCrossingUtc);
        Assert.Equal("No meter readings in lookback window.", result.UnprojectableReason);
        Assert.Equal(0, result.ReadingsUsed);
    }

    [Fact]
    public async Task Projection_ZeroVelocity_ReturnsUnprojectableWithReason()
    {
        using var db = NewDb();
        var asOf = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        // Two readings, same value (asset hasn't moved).
        db.MeterReadings.Add(new MeterReading { AssetId = 7, MeterType = MeterType.Hours, Reading = 250m, ReadingDate = asOf.AddDays(-20) });
        db.MeterReadings.Add(new MeterReading { AssetId = 7, MeterType = MeterType.Hours, Reading = 250m, ReadingDate = asOf.AddDays(-1) });
        await db.SaveChangesAsync();

        var svc = NewScheduler(db);
        var result = await svc.ProjectMeterCrossingDateAsync(
            assetId: 7,
            meterType: MeterType.Hours,
            targetReading: 300m,
            asOfUtc: asOf);

        Assert.Null(result.ProjectedCrossingUtc);
        Assert.Equal(0m, result.Velocity);
        Assert.StartsWith("Meter velocity is zero", result.UnprojectableReason ?? "");
    }

    [Fact]
    public async Task Projection_SingleReading_ReturnsUnprojectableNeedsTwo()
    {
        using var db = NewDb();
        var asOf = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        db.MeterReadings.Add(new MeterReading { AssetId = 5, MeterType = MeterType.Hours, Reading = 50m, ReadingDate = asOf.AddDays(-5) });
        await db.SaveChangesAsync();

        var svc = NewScheduler(db);
        var result = await svc.ProjectMeterCrossingDateAsync(
            assetId: 5,
            meterType: MeterType.Hours,
            targetReading: 100m,
            asOfUtc: asOf);

        Assert.Null(result.ProjectedCrossingUtc);
        Assert.Equal(1, result.ReadingsUsed);
        Assert.Equal("Need at least 2 readings to compute velocity.", result.UnprojectableReason);
    }

    [Fact]
    public async Task Projection_ScopedByMeterType()
    {
        using var db = NewDb();
        var asOf = new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc);

        // Hours readings — should be used.
        db.MeterReadings.Add(new MeterReading { AssetId = 3, MeterType = MeterType.Hours, Reading = 100m, ReadingDate = asOf.AddDays(-10) });
        db.MeterReadings.Add(new MeterReading { AssetId = 3, MeterType = MeterType.Hours, Reading = 200m, ReadingDate = asOf });
        // Miles readings on the same asset — must be ignored.
        db.MeterReadings.Add(new MeterReading { AssetId = 3, MeterType = MeterType.Miles, Reading = 5000m, ReadingDate = asOf.AddDays(-10) });
        db.MeterReadings.Add(new MeterReading { AssetId = 3, MeterType = MeterType.Miles, Reading = 7000m, ReadingDate = asOf });
        await db.SaveChangesAsync();

        var svc = NewScheduler(db);
        var result = await svc.ProjectMeterCrossingDateAsync(
            assetId: 3,
            meterType: MeterType.Hours,
            targetReading: 250m,
            asOfUtc: asOf);

        Assert.Null(result.UnprojectableReason);
        Assert.Equal(2, result.ReadingsUsed);
        Assert.Equal(10m, result.Velocity); // (200-100)/10 days
    }
}
