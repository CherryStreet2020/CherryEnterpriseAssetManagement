using System;

namespace Abs.FixedAssets.Services
{
    public enum AsOfMode
    {
        Today = 0,
        MonthEnd = 1
    }

    public static class NbvHelper
    {
        /// <summary>
        /// Resolves the effective "as of" date based on the chosen mode.
        /// </summary>
        public static DateTime ResolveAsOf(DateTime? asOf, AsOfMode mode)
        {
            var d = (asOf ?? DateTime.Today).Date;
            if (mode == AsOfMode.MonthEnd)
            {
                return new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
            }
            return d;
        }
    }
}