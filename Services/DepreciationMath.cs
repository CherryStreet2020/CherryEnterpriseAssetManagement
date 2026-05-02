using System;

namespace Abs.FixedAssets.Services
{
    /// <summary>
    /// Lightweight straight-line depreciation math used by the preview report.
    /// (No conventions; assumes month-based straight-line.)
    /// </summary>
    public static class DepreciationMath
    {
        public static decimal MonthlyStraightLine(decimal cost, decimal salvage, int usefulLifeMonths)
        {
            if (usefulLifeMonths <= 0) return 0m;
            var baseAmt = cost - salvage;
            if (baseAmt <= 0m) return 0m;
            return Math.Round(baseAmt / usefulLifeMonths, 2, MidpointRounding.AwayFromZero);
        }

        public static decimal AccumulatedThrough(DateTime inServiceDate, DateTime asOfMonthEnd, decimal monthly, int usefulLifeMonths)
        {
            if (monthly <= 0m || usefulLifeMonths <= 0) return 0m;

            var startMonth = new DateTime(inServiceDate.Year, inServiceDate.Month, 1);
            var endMonth = new DateTime(asOfMonthEnd.Year, asOfMonthEnd.Month, 1);

            if (endMonth < startMonth) return 0m;

            var months = ((endMonth.Year - startMonth.Year) * 12) + (endMonth.Month - startMonth.Month) + 1;
            months = Math.Min(months, usefulLifeMonths);
            if (months < 0) months = 0;

            return Math.Round(monthly * months, 2, MidpointRounding.AwayFromZero);
        }

        public static DateTime MonthEnd(DateTime anyDate)
        {
            var first = new DateTime(anyDate.Year, anyDate.Month, 1);
            return first.AddMonths(1).AddDays(-1);
        }
    }
}