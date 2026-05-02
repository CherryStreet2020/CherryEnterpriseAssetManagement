using System;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Services
{
    public class PeriodCheckResult
    {
        public bool IsAllowed { get; set; }
        public string? Reason { get; set; }
        public FiscalPeriod? Period { get; set; }
    }

    public interface IPeriodGuard
    {
        Task<PeriodCheckResult> CanPostAsync(int companyId, DateTime postingDate);
        Task EnsureCanPostAsync(int companyId, DateTime postingDate);
    }

    public class PeriodGuard : IPeriodGuard
    {
        private readonly AppDbContext _db;

        public PeriodGuard(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PeriodCheckResult> CanPostAsync(int companyId, DateTime postingDate)
        {
            var period = await _db.FiscalPeriods
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.CompanyId == companyId &&
                    postingDate >= p.StartDate &&
                    postingDate <= p.EndDate);

            if (period == null)
            {
                return new PeriodCheckResult
                {
                    IsAllowed = false,
                    Reason = $"No fiscal period defined for {postingDate:yyyy-MM-dd} on company {companyId}. Set up the calendar in Admin → Fiscal Calendar before posting.",
                    Period = null
                };
            }

            if (period.Status == PeriodStatus.Open)
                return new PeriodCheckResult { IsAllowed = true, Period = period };

            return new PeriodCheckResult
            {
                IsAllowed = false,
                Reason = $"Fiscal period '{period.Name}' is {period.Status} for {postingDate:yyyy-MM-dd}. Re-open the period or choose a different posting date.",
                Period = period
            };
        }

        public async Task EnsureCanPostAsync(int companyId, DateTime postingDate)
        {
            var result = await CanPostAsync(companyId, postingDate);
            if (!result.IsAllowed)
                throw new InvalidOperationException(result.Reason ?? "Posting blocked by period guard.");
        }
    }
}
