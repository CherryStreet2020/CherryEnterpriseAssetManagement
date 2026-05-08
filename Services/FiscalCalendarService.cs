using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abs.FixedAssets.Services
{
    /// <summary>
    /// Idempotent fiscal-calendar coverage. Every Company is guaranteed
    /// a calendar-year FiscalYear row (Jan–Dec) plus 12 monthly
    /// FiscalPeriod rows for the years
    /// <c>[asOfDate.Year - yearsBack, asOfDate.Year + yearsAhead]</c>.
    /// Run at startup so the PeriodGuard validator never blocks a
    /// posting just because the calendar wasn't materialized.
    ///
    /// Closes DEF-004 from the 2026-05-08 E2E run report. Coverage is
    /// re-checked every startup; admins can also invoke via
    /// <c>/Admin/FiscalCalendar</c> to generate ad-hoc years (DEF-006).
    /// </summary>
    public interface IFiscalCalendarService
    {
        Task<int> EnsureCoverageAsync(int companyId, DateTime asOfDate, int yearsBack = 1, int yearsAhead = 2);
        Task<int> EnsureCoverageForAllCompaniesAsync(DateTime asOfDate, int yearsBack = 1, int yearsAhead = 2);
        Task<FiscalYear> GenerateYearAsync(int companyId, int year);
    }

    public class FiscalCalendarService : IFiscalCalendarService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<FiscalCalendarService> _logger;

        public FiscalCalendarService(AppDbContext db, ILogger<FiscalCalendarService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> EnsureCoverageAsync(int companyId, DateTime asOfDate, int yearsBack = 1, int yearsAhead = 2)
        {
            if (yearsBack < 0) throw new ArgumentOutOfRangeException(nameof(yearsBack));
            if (yearsAhead < 0) throw new ArgumentOutOfRangeException(nameof(yearsAhead));

            var firstYear = asOfDate.Year - yearsBack;
            var lastYear = asOfDate.Year + yearsAhead;
            int rowsCreated = 0;

            for (int year = firstYear; year <= lastYear; year++)
            {
                rowsCreated += await EnsureYearAsync(companyId, year);
            }

            return rowsCreated;
        }

        public async Task<int> EnsureCoverageForAllCompaniesAsync(DateTime asOfDate, int yearsBack = 1, int yearsAhead = 2)
        {
            var companyIds = await _db.Companies
                .Where(c => c.IsActive)
                .Select(c => c.Id)
                .ToListAsync();

            int total = 0;
            foreach (var id in companyIds)
            {
                total += await EnsureCoverageAsync(id, asOfDate, yearsBack, yearsAhead);
            }
            return total;
        }

        public async Task<FiscalYear> GenerateYearAsync(int companyId, int year)
        {
            await EnsureYearAsync(companyId, year);
            return await _db.FiscalYears
                .Include(fy => fy.Periods)
                .FirstAsync(fy => fy.CompanyId == companyId && fy.Year == year);
        }

        /// <summary>Creates the FiscalYear + 12 monthly FiscalPeriods if
        /// either is missing. Returns the count of new rows persisted.</summary>
        private async Task<int> EnsureYearAsync(int companyId, int year)
        {
            int rowsCreated = 0;

            var fy = await _db.FiscalYears
                .Include(f => f.Periods)
                .FirstOrDefaultAsync(f => f.CompanyId == companyId && f.Year == year);

            if (fy == null)
            {
                fy = new FiscalYear
                {
                    CompanyId = companyId,
                    Year = year,
                    Name = $"FY {year}",
                    StartDate = new DateTime(year, 1, 1),
                    EndDate = new DateTime(year, 12, 31),
                    Status = year < DateTime.UtcNow.Year ? FiscalYearStatus.Closed
                              : year == DateTime.UtcNow.Year ? FiscalYearStatus.Open
                              : FiscalYearStatus.Future,
                    NumberOfPeriods = 12,
                    PeriodType = AccountingPeriodType.Standard12Month,
                    HasAdjustmentPeriod = false,
                    CreatedAt = DateTime.UtcNow
                };
                _db.FiscalYears.Add(fy);
                await _db.SaveChangesAsync();
                rowsCreated++;
                _logger.LogInformation(
                    "FiscalCalendarService: created FiscalYear {Year} for company {CompanyId}",
                    year, companyId);
            }

            // Fill any of the 12 monthly periods that are missing. Idempotent:
            // a partial fill (e.g., from an aborted prior run) is finished off.
            var existingPeriods = fy.Periods?.ToDictionary(p => p.PeriodNumber) ?? new Dictionary<int, FiscalPeriod>();
            for (int month = 1; month <= 12; month++)
            {
                if (existingPeriods.ContainsKey(month)) continue;

                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                var period = new FiscalPeriod
                {
                    FiscalYearId = fy.Id,
                    CompanyId = companyId,
                    PeriodNumber = month,
                    Name = $"{start:MMM yyyy}",
                    StartDate = start,
                    EndDate = end,
                    Status = PeriodStatus.Open,
                    DaysInPeriod = (end - start).Days + 1,
                    CreatedAt = DateTime.UtcNow
                };
                _db.FiscalPeriods.Add(period);
                rowsCreated++;
            }

            if (rowsCreated > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation(
                    "FiscalCalendarService: ensured {Count} period rows for company {CompanyId} year {Year}",
                    rowsCreated, companyId, year);
            }

            return rowsCreated;
        }
    }
}
