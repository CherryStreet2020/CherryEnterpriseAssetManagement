using System;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Abs.FixedAssets.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Abs.FixedAssets.Tests;

/// <summary>
/// Regression tests for DEF-004 (2026-05-08 E2E run): without
/// FiscalCalendarService running at startup, every JE-posting flow
/// (Improve, Dispose, Run Depreciation, AP approve, GR posting,
/// CIP capitalization) is wedged because PeriodGuard rejects the
/// posting date. The service must guarantee coverage idempotently
/// for every active company over [today − 1y, today + 2y].
/// </summary>
public class FiscalCalendarServiceTests
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
            .UseInMemoryDatabase($"fiscal-cal-{dbName}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestAppDbContext(opts);
    }

    private static FiscalCalendarService NewService(AppDbContext db) =>
        new(db, NullLogger<FiscalCalendarService>.Instance);

    private static async Task<int> SeedCompanyAsync(AppDbContext db, string code = "C-1", bool active = true)
    {
        var c = new Company { CompanyCode = code, Name = $"{code} Co", IsActive = active };
        db.Companies.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    [Fact]
    public async Task EnsureCoverage_FreshDb_CreatesYearsAndPeriodsForFullWindow()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var asOf = new DateTime(2026, 5, 8);

        var rows = await NewService(db).EnsureCoverageAsync(companyId, asOf, yearsBack: 1, yearsAhead: 2);

        // 4 years × (1 FY + 12 periods) = 52 rows.
        Assert.Equal(4 * (1 + 12), rows);

        var years = await db.FiscalYears.Where(fy => fy.CompanyId == companyId).OrderBy(fy => fy.Year).ToListAsync();
        Assert.Equal(new[] { 2025, 2026, 2027, 2028 }, years.Select(y => y.Year).ToArray());

        var periods = await db.FiscalPeriods.Where(p => p.CompanyId == companyId).ToListAsync();
        Assert.Equal(48, periods.Count);
        Assert.All(periods, p => Assert.Equal(PeriodStatus.Open, p.Status));
    }

    [Fact]
    public async Task EnsureCoverage_RunTwice_IsIdempotent()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var asOf = new DateTime(2026, 5, 8);

        var first = await NewService(db).EnsureCoverageAsync(companyId, asOf);
        Assert.True(first > 0);

        var second = await NewService(db).EnsureCoverageAsync(companyId, asOf);
        Assert.Equal(0, second); // nothing new to materialize

        Assert.Equal(4, await db.FiscalYears.CountAsync(fy => fy.CompanyId == companyId));
        Assert.Equal(48, await db.FiscalPeriods.CountAsync(p => p.CompanyId == companyId));
    }

    [Fact]
    public async Task EnsureCoverage_PartiallyMaterialized_FillsMissingPeriodsOnly()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);

        // Seed FY 2026 manually with only 6 periods (months 1-6) to simulate
        // an aborted prior run.
        var fy = new FiscalYear
        {
            CompanyId = companyId, Year = 2026, Name = "FY 2026",
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 12, 31),
            Status = FiscalYearStatus.Open, NumberOfPeriods = 12, CreatedAt = DateTime.UtcNow
        };
        db.FiscalYears.Add(fy);
        await db.SaveChangesAsync();
        for (int m = 1; m <= 6; m++)
        {
            db.FiscalPeriods.Add(new FiscalPeriod
            {
                FiscalYearId = fy.Id, CompanyId = companyId, PeriodNumber = m,
                Name = $"P{m}", StartDate = new DateTime(2026, m, 1),
                EndDate = new DateTime(2026, m, DateTime.DaysInMonth(2026, m)),
                Status = PeriodStatus.Open, DaysInPeriod = 30
            });
        }
        await db.SaveChangesAsync();

        var asOf = new DateTime(2026, 5, 8);
        var rows = await NewService(db).EnsureCoverageAsync(companyId, asOf, yearsBack: 0, yearsAhead: 0);

        // FY 2026 already exists; should fill periods 7-12 only.
        Assert.Equal(6, rows);
        Assert.Equal(12, await db.FiscalPeriods.CountAsync(p => p.CompanyId == companyId && p.FiscalYearId == fy.Id));
    }

    [Fact]
    public async Task EnsureCoverage_PeriodCoversToday_PeriodGuardAllowsPosting()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var today = new DateTime(2026, 5, 8);
        await NewService(db).EnsureCoverageAsync(companyId, today);

        // Drive PeriodGuard.CanPostAsync against the materialized periods —
        // the original DEF-004 symptom was that it rejected today.
        var guard = new PeriodGuard(db);
        var result = await guard.CanPostAsync(companyId, today);

        Assert.True(result.IsAllowed);
        Assert.NotNull(result.Period);
        Assert.Equal(5, result.Period!.PeriodNumber);
        Assert.Equal("May 2026", result.Period.Name);
    }

    [Fact]
    public async Task EnsureCoverageForAllCompanies_OnlyCoversActiveCompanies()
    {
        await using var db = NewDb();
        var active1 = await SeedCompanyAsync(db, "ACTIVE-1", active: true);
        var active2 = await SeedCompanyAsync(db, "ACTIVE-2", active: true);
        var inactive = await SeedCompanyAsync(db, "INACTIVE-1", active: false);

        var asOf = new DateTime(2026, 5, 8);
        await NewService(db).EnsureCoverageForAllCompaniesAsync(asOf);

        Assert.Equal(4, await db.FiscalYears.CountAsync(fy => fy.CompanyId == active1));
        Assert.Equal(4, await db.FiscalYears.CountAsync(fy => fy.CompanyId == active2));
        Assert.Equal(0, await db.FiscalYears.CountAsync(fy => fy.CompanyId == inactive));
    }

    [Fact]
    public async Task GenerateYearAsync_MaterializesSpecificYear()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);

        var fy = await NewService(db).GenerateYearAsync(companyId, 2030);

        Assert.NotNull(fy);
        Assert.Equal(2030, fy.Year);
        Assert.Equal(12, await db.FiscalPeriods.CountAsync(p => p.FiscalYearId == fy.Id));

        // Idempotent: re-running returns the same row, no duplicates.
        var fy2 = await NewService(db).GenerateYearAsync(companyId, 2030);
        Assert.Equal(fy.Id, fy2.Id);
        Assert.Equal(1, await db.FiscalYears.CountAsync(f => f.CompanyId == companyId && f.Year == 2030));
        Assert.Equal(12, await db.FiscalPeriods.CountAsync(p => p.FiscalYearId == fy.Id));
    }

    [Fact]
    public async Task EnsureYear_PeriodEndDates_HandleShortMonthsCorrectly()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);

        await NewService(db).GenerateYearAsync(companyId, 2024); // leap year

        var feb = await db.FiscalPeriods
            .Where(p => p.CompanyId == companyId)
            .Where(p => p.StartDate.Month == 2 && p.StartDate.Year == 2024)
            .SingleAsync();
        Assert.Equal(new DateTime(2024, 2, 29), feb.EndDate); // leap day captured
        Assert.Equal(29, feb.DaysInPeriod);

        var apr = await db.FiscalPeriods
            .Where(p => p.CompanyId == companyId)
            .Where(p => p.StartDate.Month == 4 && p.StartDate.Year == 2024)
            .SingleAsync();
        Assert.Equal(new DateTime(2024, 4, 30), apr.EndDate);
        Assert.Equal(30, apr.DaysInPeriod);
    }

    [Fact]
    public async Task EnsureCoverage_PastYearStatusIsClosed_FutureYearStatusIsFuture()
    {
        await using var db = NewDb();
        var companyId = await SeedCompanyAsync(db);
        var thisYear = DateTime.UtcNow.Year;

        // Generate one prior, current, and one future year.
        await NewService(db).GenerateYearAsync(companyId, thisYear - 1);
        await NewService(db).GenerateYearAsync(companyId, thisYear);
        await NewService(db).GenerateYearAsync(companyId, thisYear + 1);

        var prior = await db.FiscalYears.SingleAsync(fy => fy.CompanyId == companyId && fy.Year == thisYear - 1);
        var current = await db.FiscalYears.SingleAsync(fy => fy.CompanyId == companyId && fy.Year == thisYear);
        var future = await db.FiscalYears.SingleAsync(fy => fy.CompanyId == companyId && fy.Year == thisYear + 1);

        Assert.Equal(FiscalYearStatus.Closed, prior.Status);
        Assert.Equal(FiscalYearStatus.Open, current.Status);
        Assert.Equal(FiscalYearStatus.Future, future.Status);
    }
}
