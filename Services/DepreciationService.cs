using System;
using System.Collections.Generic;
using Abs.FixedAssets.Models;

namespace Abs.FixedAssets.Services
{
    public class DepreciationRow
    {
        public int PeriodNumber { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal BeginNBV { get; set; }
        public decimal DepreciationAmount { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public decimal EndingBookValue { get; set; }
        public DepreciationMethod MethodUsed { get; set; }
        public bool SwitchedToSL { get; set; }
    }

    public class DepreciationService
    {
        private static DateTime MonthStart(DateTime dt) =>
            new DateTime(dt.Year, dt.Month, 1);

        private static DateTime MonthEnd(DateTime dt) =>
            new DateTime(dt.Year, dt.Month, DateTime.DaysInMonth(dt.Year, dt.Month));

        private static int InclusiveMonthCount(DateTime startMonth, DateTime endMonth)
        {
            var months = (endMonth.Year - startMonth.Year) * 12 + (endMonth.Month - startMonth.Month) + 1;
            return months < 0 ? 0 : months;
        }

        public List<DepreciationRow> BuildSchedule(Asset asset, DateTime asOfMonthEnd)
        {
            return BuildScheduleWithSettings(asset, asOfMonthEnd, null);
        }

        public List<DepreciationRow> BuildScheduleWithSettings(
            Asset asset,
            DateTime asOfMonthEnd,
            AssetBookSettings? bookSettings,
            bool switchToSLWhenBeneficial = true,
            Dictionary<int, decimal>? unitsPerPeriod = null)
        {
            var rows = new List<DepreciationRow>();

            if (asset == null) return rows;

            if (bookSettings != null && bookSettings.IsExcludedFromBook)
                return rows;

            var cost = bookSettings?.EffectiveCostBasis ?? asset.AcquisitionCost;
            var salvage = bookSettings?.EffectiveSalvageValue ?? asset.SalvageValue;
            var life = bookSettings?.EffectiveUsefulLifeMonths ?? asset.UsefulLifeMonths;
            var method = bookSettings?.EffectiveMethod ?? DepreciationMethod.StraightLine;
            var convention = bookSettings?.EffectiveConvention ?? DepreciationConvention.MidMonth;
            var inServiceDate = bookSettings?.EffectiveInServiceDate ?? asset.InServiceDate;

            decimal section179 = bookSettings?.Section179Deduction ?? 0;
            decimal bonusPercent = bookSettings?.BonusDepreciationPercent ?? 0;

            if (life <= 0 || cost <= 0m) return rows;

            decimal adjustedCost = cost - section179;
            if (adjustedCost <= 0) return rows;

            decimal bonusDeduction = adjustedCost * (bonusPercent / 100m);
            adjustedCost -= bonusDeduction;

            var depreciableBasis = Math.Max(0m, adjustedCost - salvage);
            if (depreciableBasis <= 0m) return rows;

            var inServiceStart = MonthStart(inServiceDate);
            var stopMonthEnd = MonthEnd(asOfMonthEnd);

            if (inServiceStart > stopMonthEnd) return rows;

            var totalMonths = InclusiveMonthCount(inServiceStart, stopMonthEnd);
            var monthsToCompute = Math.Min(totalMonths, life);

            decimal accum = section179 + bonusDeduction;
            decimal beginNbv = cost - accum;
            int lifeYears = life / 12;
            if (lifeYears < 1) lifeYears = 1;

            int sydSum = (lifeYears * (lifeYears + 1)) / 2;

            for (int i = 0; i < monthsToCompute; i++)
            {
                var ps = inServiceStart.AddMonths(i);
                var pe = MonthEnd(ps);
                int periodMonth = i + 1;
                int periodYear = (i / 12) + 1;
                int remainingMonths = life - i;
                int remainingYears = lifeYears - periodYear + 1;
                if (remainingYears < 1) remainingYears = 1;

                decimal periodDep = 0m;
                bool switchedToSL = false;
                DepreciationMethod usedMethod = method;

                decimal remainingBasis = beginNbv - salvage;
                if (remainingBasis < 0) remainingBasis = 0;

                bool isFirstMonth = (periodMonth == 1);
                bool isLastMonth = (periodMonth == life);
                bool isFirstYear = (periodYear == 1);

                switch (method)
                {
                    case DepreciationMethod.NoDepreciation:
                        periodDep = 0m;
                        break;

                    case DepreciationMethod.StraightLine:
                    case DepreciationMethod.Amortization:
                        decimal monthlySlDep = depreciableBasis / life;
                        periodDep = ApplyFirstLastMonthConvention(monthlySlDep, convention, isFirstMonth, isLastMonth, inServiceDate);
                        break;

                    case DepreciationMethod.DoubleDecliningBalance:
                        decimal annualDdbDep = CalculateDDB(remainingBasis, lifeYears);
                        decimal monthlyDdbDep = annualDdbDep / 12m;
                        decimal slRemaining = remainingMonths > 0 ? remainingBasis / remainingMonths : 0;

                        if (switchToSLWhenBeneficial && slRemaining > monthlyDdbDep)
                        {
                            periodDep = slRemaining;
                            switchedToSL = true;
                            usedMethod = DepreciationMethod.StraightLine;
                        }
                        else
                        {
                            periodDep = monthlyDdbDep;
                        }

                        if (isFirstYear)
                            periodDep = ApplyFirstYearConvention(periodDep, convention, i % 12, inServiceDate);
                        break;

                    case DepreciationMethod.DecliningBalance150:
                        decimal annual150Dep = CalculateDecliningBalance(remainingBasis, lifeYears, 1.5m);
                        decimal monthly150Dep = annual150Dep / 12m;
                        decimal sl150Remaining = remainingMonths > 0 ? remainingBasis / remainingMonths : 0;

                        if (switchToSLWhenBeneficial && sl150Remaining > monthly150Dep)
                        {
                            periodDep = sl150Remaining;
                            switchedToSL = true;
                            usedMethod = DepreciationMethod.StraightLine;
                        }
                        else
                        {
                            periodDep = monthly150Dep;
                        }

                        if (isFirstYear)
                            periodDep = ApplyFirstYearConvention(periodDep, convention, i % 12, inServiceDate);
                        break;

                    case DepreciationMethod.SumOfYearsDigits:
                        decimal annualSyd = CalculateSYD(depreciableBasis, lifeYears, remainingYears, sydSum);
                        periodDep = annualSyd / 12m;

                        if (isFirstYear)
                            periodDep = ApplyFirstYearConvention(periodDep, convention, i % 12, inServiceDate);
                        break;

                    case DepreciationMethod.UnitsOfProduction:
                        if (unitsPerPeriod != null && unitsPerPeriod.TryGetValue(periodMonth, out decimal periodUnits))
                        {
                            decimal totalUnits = 0;
                            foreach (var u in unitsPerPeriod.Values)
                                totalUnits += u;

                            if (totalUnits > 0)
                            {
                                decimal ratePerUnit = depreciableBasis / totalUnits;
                                periodDep = ratePerUnit * periodUnits;
                            }
                        }
                        else
                        {
                            periodDep = depreciableBasis / life;
                        }
                        break;

                    case DepreciationMethod.MACRS:
                    case DepreciationMethod.MACRS3Year:
                    case DepreciationMethod.MACRS5Year:
                    case DepreciationMethod.MACRS7Year:
                    case DepreciationMethod.MACRS10Year:
                    case DepreciationMethod.MACRS15Year:
                    case DepreciationMethod.MACRS20Year:
                    case DepreciationMethod.MACRS27_5Year:
                    case DepreciationMethod.MACRS39Year:
                        int macrsLifeYears = ResolveMacrsLifeYears(method, lifeYears);
                        periodDep = CalculateMACRSMonthly(cost, macrsLifeYears, periodYear, i % 12 + 1);
                        break;

                    case DepreciationMethod.CCA:
                        throw new InvalidOperationException(
                            $"Asset '{asset.AssetNumber}' is configured with the CCA method but CCA is calculated by CcaService (Canadian tax book), not by DepreciationService. " +
                            "Switch the GAAP book to a different method (e.g. StraightLine) and configure CCA on the tax book via AssetTaxSettings.");

                    case DepreciationMethod.ADS:
                    case DepreciationMethod.GroupComposite:
                    case DepreciationMethod.Component:
                    case DepreciationMethod.IFRSRevaluation:
                    case DepreciationMethod.CustomSchedule:
                        throw new NotImplementedException(
                            $"Depreciation method '{method}' is not yet implemented for asset '{asset.AssetNumber}'. " +
                            "Choose StraightLine, DoubleDecliningBalance, DecliningBalance150, SumOfYearsDigits, UnitsOfProduction, MACRS, Amortization, or NoDepreciation.");

                    default:
                        throw new NotSupportedException(
                            $"Unsupported depreciation method '{method}' (value {(int)method}) on asset '{asset.AssetNumber}'.");
                }

                periodDep = Math.Max(0, Math.Min(periodDep, remainingBasis));
                periodDep = decimal.Round(periodDep, 2, MidpointRounding.AwayFromZero);

                accum += periodDep;
                var endNbv = beginNbv - periodDep;

                rows.Add(new DepreciationRow
                {
                    PeriodNumber = i + 1,
                    PeriodStart = ps,
                    PeriodEnd = pe,
                    BeginNBV = decimal.Round(beginNbv, 2),
                    DepreciationAmount = periodDep,
                    AccumulatedDepreciation = decimal.Round(accum, 2),
                    EndingBookValue = decimal.Round(endNbv, 2),
                    MethodUsed = usedMethod,
                    SwitchedToSL = switchedToSL
                });

                beginNbv = endNbv;

                if (beginNbv <= salvage + 0.01m)
                    break;
            }

            return rows;
        }

        private decimal ApplyFirstLastMonthConvention(decimal baseDep, DepreciationConvention convention, bool isFirstMonth, bool isLastMonth, DateTime inServiceDate)
        {
            if (isFirstMonth)
            {
                switch (convention)
                {
                    case DepreciationConvention.HalfYear:
                    case DepreciationConvention.ModifiedHalfYear:
                        return baseDep * 0.5m;
                    case DepreciationConvention.HalfMonth:
                        return inServiceDate.Day <= 15 ? baseDep : baseDep * 0.5m;
                    case DepreciationConvention.MidMonth:
                        return baseDep * 0.5m;
                    case DepreciationConvention.MidQuarter:
                    {
                        // Treat asset as placed in service at midpoint of the quarter:
                        // first month = full, second = half, third = none. Then resume normally next quarter.
                        int quarterMonth = ((inServiceDate.Month - 1) % 3) + 1;
                        return quarterMonth switch
                        {
                            1 => baseDep,
                            2 => baseDep * 0.5m,
                            _ => 0m
                        };
                    }
                    case DepreciationConvention.ActualDays:
                        int daysRemaining = DateTime.DaysInMonth(inServiceDate.Year, inServiceDate.Month) - inServiceDate.Day + 1;
                        int totalDays = DateTime.DaysInMonth(inServiceDate.Year, inServiceDate.Month);
                        return baseDep * ((decimal)daysRemaining / totalDays);
                    case DepreciationConvention.FirstDayOfMonth:
                    case DepreciationConvention.FullMonth:
                    case DepreciationConvention.FullYear:
                    case DepreciationConvention.NoProrate:
                        return baseDep;
                    case DepreciationConvention.NextMonth:
                        return 0m; // Skip first month; depreciation begins the following month
                    case DepreciationConvention.LastDayOfMonth:
                        return inServiceDate.Day == DateTime.DaysInMonth(inServiceDate.Year, inServiceDate.Month) ? baseDep : 0m;
                    default:
                        return baseDep;
                }
            }
            else if (isLastMonth)
            {
                switch (convention)
                {
                    case DepreciationConvention.HalfYear:
                    case DepreciationConvention.ModifiedHalfYear:
                    case DepreciationConvention.HalfMonth:
                    case DepreciationConvention.MidMonth:
                        return baseDep * 0.5m;
                    case DepreciationConvention.MidQuarter:
                        return baseDep * 0.5m;
                    default:
                        return baseDep;
                }
            }
            return baseDep;
        }

        private static int ResolveMacrsLifeYears(DepreciationMethod method, int fallbackLifeYears)
        {
            return method switch
            {
                DepreciationMethod.MACRS3Year => 3,
                DepreciationMethod.MACRS5Year => 5,
                DepreciationMethod.MACRS7Year => 7,
                DepreciationMethod.MACRS10Year => 10,
                DepreciationMethod.MACRS15Year => 15,
                DepreciationMethod.MACRS20Year => 20,
                DepreciationMethod.MACRS27_5Year => 28,
                DepreciationMethod.MACRS39Year => 39,
                _ => fallbackLifeYears
            };
        }

        private decimal ApplyFirstYearConvention(decimal monthlyDep, DepreciationConvention convention, int monthInYear, DateTime inServiceDate)
        {
            if (monthInYear == 0)
            {
                switch (convention)
                {
                    case DepreciationConvention.HalfYear:
                        return monthlyDep * 0.5m;
                    case DepreciationConvention.MidMonth:
                    case DepreciationConvention.HalfMonth:
                        return monthlyDep * 0.5m;
                    default:
                        return monthlyDep;
                }
            }
            return monthlyDep;
        }

        private decimal CalculateDDB(decimal remainingBasis, int lifeYears)
        {
            if (lifeYears <= 0) return 0;
            decimal rate = 2.0m / lifeYears;
            return remainingBasis * rate;
        }

        private decimal CalculateDecliningBalance(decimal remainingBasis, int lifeYears, decimal multiplier)
        {
            if (lifeYears <= 0) return 0;
            decimal rate = multiplier / lifeYears;
            return remainingBasis * rate;
        }

        private decimal CalculateSYD(decimal depreciableBasis, int lifeYears, int remainingYears, int sumOfYears)
        {
            if (sumOfYears <= 0) return 0;
            return depreciableBasis * remainingYears / sumOfYears;
        }

        private decimal CalculateMACRSMonthly(decimal cost, int lifeYears, int yearNumber, int monthInYear)
        {
            decimal[] macrs3Year = { 33.33m, 44.45m, 14.81m, 7.41m };
            decimal[] macrs5Year = { 20.00m, 32.00m, 19.20m, 11.52m, 11.52m, 5.76m };
            decimal[] macrs7Year = { 14.29m, 24.49m, 17.49m, 12.49m, 8.93m, 8.92m, 8.93m, 4.46m };
            decimal[] macrs10Year = { 10.00m, 18.00m, 14.40m, 11.52m, 9.22m, 7.37m, 6.55m, 6.55m, 6.56m, 6.55m, 3.28m };
            decimal[] macrs15Year = { 5.00m, 9.50m, 8.55m, 7.70m, 6.93m, 6.23m, 5.90m, 5.90m, 5.91m, 5.90m, 5.91m, 5.90m, 5.91m, 5.90m, 5.91m, 2.95m };
            decimal[] macrs20Year = { 3.750m, 7.219m, 6.677m, 6.177m, 5.713m, 5.285m, 4.888m, 4.522m, 4.462m, 4.461m, 4.462m, 4.461m, 4.462m, 4.461m, 4.462m, 4.461m, 4.462m, 4.461m, 4.462m, 4.461m, 2.231m };

            decimal[] rates;
            if (lifeYears <= 3)
                rates = macrs3Year;
            else if (lifeYears <= 5)
                rates = macrs5Year;
            else if (lifeYears <= 7)
                rates = macrs7Year;
            else if (lifeYears <= 10)
                rates = macrs10Year;
            else if (lifeYears <= 15)
                rates = macrs15Year;
            else
                rates = macrs20Year;

            if (yearNumber < 1 || yearNumber > rates.Length)
                return 0;

            decimal annualDep = cost * (rates[yearNumber - 1] / 100m);
            return annualDep / 12m;
        }

        public decimal CalculateMonthlyDepreciation(
            decimal cost,
            decimal salvage,
            int lifeMonths,
            DepreciationMethod method,
            int currentMonth,
            decimal? currentNBV = null,
            int? lifeYears = null)
        {
            if (lifeMonths <= 0 || cost <= 0) return 0;

            decimal depreciableBasis = cost - salvage;
            if (depreciableBasis <= 0) return 0;

            decimal nbv = currentNBV ?? cost;
            int years = lifeYears ?? (lifeMonths / 12);
            if (years < 1) years = 1;

            switch (method)
            {
                case DepreciationMethod.StraightLine:
                    return depreciableBasis / lifeMonths;

                case DepreciationMethod.DoubleDecliningBalance:
                    decimal remainingBasis = nbv - salvage;
                    if (remainingBasis <= 0) return 0;
                    decimal ddbAnnual = CalculateDDB(remainingBasis, years);
                    return ddbAnnual / 12;

                case DepreciationMethod.DecliningBalance150:
                    decimal rem150 = nbv - salvage;
                    if (rem150 <= 0) return 0;
                    decimal db150Annual = CalculateDecliningBalance(rem150, years, 1.5m);
                    return db150Annual / 12;

                case DepreciationMethod.SumOfYearsDigits:
                    int currentYear = (currentMonth - 1) / 12 + 1;
                    int remainingYears = years - currentYear + 1;
                    if (remainingYears < 1) remainingYears = 1;
                    int sydSum = (years * (years + 1)) / 2;
                    decimal sydAnnual = CalculateSYD(depreciableBasis, years, remainingYears, sydSum);
                    return sydAnnual / 12;

                default:
                    return depreciableBasis / lifeMonths;
            }
        }
    }
}
