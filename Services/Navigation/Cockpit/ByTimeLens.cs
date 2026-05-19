using System;
using System.Collections.Generic;
using System.Linq;

namespace Abs.FixedAssets.Services.Navigation.Cockpit;

// ADR-018 §D4 — the default Cockpit lens.
//
// Four buckets, comparing each row's RequiredAt against TodayLocal:
//   "overdue"   → RequiredAt.Value.Date  <  TodayLocal              (tone: danger)
//   "today"     → RequiredAt.Value.Date  == TodayLocal              (tone: warning)
//   "this-week" → TodayLocal < RequiredAt.Value.Date <= +7d         (tone: info)
//   "later"     → RequiredAt.Value.Date > TodayLocal + 7d
//                 OR RequiredAt is null                             (tone: neutral)
//
// "TodayLocal" is interpreted in the lens's configured TimeZoneInfo — important
// because dock workers cross UTC midnight long before their local midnight, and
// a PO required "today" on the East Coast must NOT show up as "yesterday/overdue"
// at 11pm Pacific. The lens takes a clock function so tests can pin time.
//
// Empty buckets are skipped (matches the legacy Pages/Receiving/Index.cshtml
// behavior). Rows inside each bucket are sorted ascending by RequiredAt (nulls
// last, which only matters in the "later" bucket).
//
// Stateless and thread-safe — safe to register as a singleton.
public sealed class ByTimeLens<TRow> : ICockpitLens<TRow> where TRow : ICockpitQueueRow
{
    private readonly Func<DateTime> _clock;          // returns local now
    private readonly TimeZoneInfo _tz;               // used only to derive "TodayLocal"

    public string Code => "by-time";
    public string Label => "By required date";

    // Default constructor: local time, system TZ. Production wiring.
    public ByTimeLens() : this(() => DateTime.Now, TimeZoneInfo.Local) { }

    // Test-only constructor: pin the clock + TZ.
    public ByTimeLens(Func<DateTime> clock, TimeZoneInfo tz)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _tz = tz ?? throw new ArgumentNullException(nameof(tz));
    }

    public IReadOnlyList<CockpitGroup<TRow>> Group(IReadOnlyList<TRow> rows)
    {
        if (rows == null || rows.Count == 0)
        {
            return Array.Empty<CockpitGroup<TRow>>();
        }

        var todayLocal = TodayLocal();
        var weekEndLocal = todayLocal.AddDays(7);

        var overdue   = new List<TRow>();
        var today     = new List<TRow>();
        var thisWeek  = new List<TRow>();
        var later     = new List<TRow>();

        foreach (var row in rows)
        {
            if (!row.RequiredAt.HasValue)
            {
                later.Add(row);
                continue;
            }

            var rowDateLocal = ToLocalDate(row.RequiredAt.Value);

            if (rowDateLocal < todayLocal)         overdue.Add(row);
            else if (rowDateLocal == todayLocal)   today.Add(row);
            else if (rowDateLocal <= weekEndLocal) thisWeek.Add(row);
            else                                   later.Add(row);
        }

        // Sort ascending within each bucket; null RequiredAt sorts last
        // (only possible in "later").
        static int Cmp(TRow a, TRow b)
            => Nullable.Compare(a.RequiredAt, b.RequiredAt);

        overdue.Sort(Cmp);
        today.Sort(Cmp);
        thisWeek.Sort(Cmp);
        later.Sort(Cmp);

        var groups = new List<CockpitGroup<TRow>>(4);
        if (overdue.Count  > 0) groups.Add(new CockpitGroup<TRow>("overdue",   $"Overdue ({overdue.Count})",   "danger",  "fa-exclamation-triangle", overdue));
        if (today.Count    > 0) groups.Add(new CockpitGroup<TRow>("today",     $"Due Today ({today.Count})",   "warning", "fa-clock",                today));
        if (thisWeek.Count > 0) groups.Add(new CockpitGroup<TRow>("this-week", $"This Week ({thisWeek.Count})","info",    "fa-calendar-week",        thisWeek));
        if (later.Count    > 0) groups.Add(new CockpitGroup<TRow>("later",     $"Upcoming ({later.Count})",    "neutral", "fa-calendar-alt",         later));
        return groups;
    }

    private DateTime TodayLocal()
    {
        var now = _clock();
        if (now.Kind == DateTimeKind.Utc)
        {
            now = TimeZoneInfo.ConvertTimeFromUtc(now, _tz);
        }
        return now.Date;
    }

    private DateTime ToLocalDate(DateTime instant)
    {
        if (instant.Kind == DateTimeKind.Utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(instant, _tz).Date;
        }
        // Unspecified or Local — treat as already in target tz.
        return instant.Date;
    }
}
