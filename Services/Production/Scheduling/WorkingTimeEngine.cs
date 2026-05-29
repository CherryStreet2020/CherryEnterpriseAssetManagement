// Theme B11 Wave R4-11 (2026-05-29) — WorkingTimeEngine.
//
// The pure (no-DB) working-time core the finite scheduler runs on. It shares the
// SAME working-interval semantics as R4-10's IResourceLoadService calendar engine
// (WorkDayMask bit 0 = Sunday, work-day window in the calendar's IANA TZ, DST-safe
// local→UTC conversion, holidays drop/halve days) and adds the two operations a
// scheduler needs that a load profiler does not:
//
//   • SubtractWorkingMinutes — walk BACKWARD from an end instant, consuming N
//     minutes of *working* time, and return the start instant. This is how the
//     backward scheduler floors an operation into real working windows instead of
//     the stub's "480 mins = exactly 8 wall-clock hours, even at 02:00 Saturday."
//   • AddWorkingMinutes — the forward analogue.
//
// NOTE (intentional, documented): the working-interval builder duplicates ~40 lines
// of R4-10's just-shipped, E2E-proven ResourceLoadService engine rather than
// refactoring that green file. The two have different entry points (load = total
// hours over a fixed window; scheduler = minute-walk over a rolling horizon). Once
// both stabilize they should be unified here; for now isolation beats DRY so R4-11
// cannot regress R4-10.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Abs.FixedAssets.Services.Production.Scheduling
{
    /// <summary>A calendar resolved to the scalars the engine needs (DB-free).</summary>
    public sealed record WorkingCalendarSpec(
        string Code, string TimeZone, short WorkDayMask, TimeSpan WorkDayStart, TimeSpan WorkDayEnd);

    /// <summary>A non-working or half-working date on the calendar.</summary>
    public sealed record WorkingHoliday(DateTime Date, bool IsHalfDay);

    public static class WorkingTimeEngine
    {
        public readonly record struct Interval(DateTime Start, DateTime End)
        {
            public double Minutes => (End - Start).TotalMinutes;
        }

        public static TimeZoneInfo ResolveTimeZone(string ianaId, out string? note)
        {
            note = null;
            try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
            catch
            {
                note = $"Time zone '{ianaId}' not found — using UTC.";
                return TimeZoneInfo.Utc;
            }
        }

        /// <summary>
        /// Build the working INTERVALS (UTC, ascending, non-overlapping) inside [fromUtc,toUtc]
        /// for a calendar: each working weekday (per WorkDayMask, bit 0 = Sunday) contributes its
        /// WorkDayStart..WorkDayEnd window in the calendar's local time, converted to UTC and
        /// clipped to the window. Full holidays drop the day; half-day holidays halve it.
        /// </summary>
        public static List<Interval> BuildWorkingIntervals(
            WorkingCalendarSpec cal, IEnumerable<WorkingHoliday> holidays, TimeZoneInfo tz,
            DateTime fromUtc, DateTime toUtc)
        {
            var result = new List<Interval>();
            var hl = holidays as ICollection<WorkingHoliday> ?? holidays.ToList();
            var halfDays = hl.Where(h => h.IsHalfDay).Select(h => h.Date.Date).ToHashSet();
            var fullDays = hl.Where(h => !h.IsHalfDay).Select(h => h.Date.Date).ToHashSet();

            var localFrom = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), tz).Date.AddDays(-1);
            var localTo = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc), tz).Date.AddDays(1);

            for (var day = localFrom; day <= localTo; day = day.AddDays(1))
            {
                var bit = 1 << (int)day.DayOfWeek; // Sunday=0 → bit 0
                if ((cal.WorkDayMask & bit) == 0) continue;
                if (fullDays.Contains(day.Date)) continue;

                var startLocal = day + cal.WorkDayStart;
                var endLocal = day + cal.WorkDayEnd;
                if (endLocal <= startLocal) continue;
                if (halfDays.Contains(day.Date))
                    endLocal = startLocal + TimeSpan.FromTicks((endLocal - startLocal).Ticks / 2);

                var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), tz);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), tz);

                var clipped = Clip(startUtc, endUtc, fromUtc, toUtc);
                if (clipped != null) result.Add(clipped.Value);
            }
            result.Sort((a, b) => a.Start.CompareTo(b.Start));
            return result;
        }

        public static Interval? Clip(DateTime s, DateTime e, DateTime fromUtc, DateTime toUtc)
        {
            var cs = s > fromUtc ? s : fromUtc;
            var ce = e < toUtc ? e : toUtc;
            return ce > cs ? new Interval(cs, ce) : (Interval?)null;
        }

        public static double TotalMinutes(List<Interval> xs) => xs.Sum(i => i.Minutes);

        /// <summary>
        /// Walk BACKWARD from <paramref name="end"/>, consuming <paramref name="minutes"/> of working
        /// time inside <paramref name="intervals"/> (ascending). Returns the working-time start instant.
        /// <paramref name="ranOut"/> is true if the intervals didn't hold enough working time (the
        /// remainder is then charged as wall-clock time before the earliest interval — a safe,
        /// flagged fallback so the scheduler never throws).
        /// </summary>
        public static DateTime SubtractWorkingMinutes(List<Interval> intervals, DateTime end, decimal minutes, out bool ranOut)
        {
            ranOut = false;
            if (minutes <= 0m) return end;
            var remaining = (double)minutes;
            // Walk intervals from latest to earliest, counting only the portion at/-before `end`.
            for (int i = intervals.Count - 1; i >= 0; i--)
            {
                var iv = intervals[i];
                var segEnd = iv.End < end ? iv.End : end;
                if (segEnd <= iv.Start) continue;           // interval is entirely after `end`
                var avail = (segEnd - iv.Start).TotalMinutes;
                if (avail >= remaining)
                    return segEnd.AddMinutes(-remaining);    // consumed within this interval
                remaining -= avail;                          // consume whole interval, keep walking back
            }
            // Not enough working time in the horizon — charge the remainder as wall-clock before the earliest start.
            ranOut = true;
            var anchor = intervals.Count > 0 ? intervals[0].Start : end;
            return anchor.AddMinutes(-remaining);
        }

        /// <summary>Forward analogue of <see cref="SubtractWorkingMinutes"/> — walk forward from start.</summary>
        public static DateTime AddWorkingMinutes(List<Interval> intervals, DateTime start, decimal minutes, out bool ranOut)
        {
            ranOut = false;
            if (minutes <= 0m) return start;
            var remaining = (double)minutes;
            foreach (var iv in intervals)
            {
                var segStart = iv.Start > start ? iv.Start : start;
                if (segStart >= iv.End) continue;            // interval entirely before `start`
                var avail = (iv.End - segStart).TotalMinutes;
                if (avail >= remaining)
                    return segStart.AddMinutes(remaining);
                remaining -= avail;
            }
            ranOut = true;
            var anchor = intervals.Count > 0 ? intervals[^1].End : start;
            return anchor.AddMinutes(remaining);
        }
    }
}
