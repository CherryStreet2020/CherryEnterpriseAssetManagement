using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abs.FixedAssets.Data;
using Abs.FixedAssets.Models.Masters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Abs.FixedAssets.Pages.Admin.WorkCalendars;

[Abs.FixedAssets.ControlPlane.ControlPlaneExempt("Read-only admin lookup. AppDbContext used only for projection of the WorkCalendar + Holiday masters.")]
// Sprint 13.5 PR #4 — /Admin/WorkCalendars
// Read-only admin view of work calendars seeded in PRA-2. Each calendar
// has child Holiday rows that drive business-day arithmetic across the
// app. Per-tenant editing deferred — system calendars cover ~80% of
// cases per PRA-2 design.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) { _db = db; }

    public IReadOnlyList<CalendarRow> Calendars { get; private set; } = new List<CalendarRow>();
    public int TotalCount { get; private set; }

    public async Task OnGetAsync()
    {
        var rows = await _db.WorkCalendars
            .AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .Select(c => new CalendarRow
            {
                Id = c.Id,
                CompanyId = c.CompanyId,
                Code = c.Code,
                Name = c.Name,
                TimeZone = c.TimeZone,
                WorkDayMask = c.WorkDayMask,
                IsDefault = c.IsDefault,
                IsActive = c.IsActive,
                HolidayCount = _db.Holidays.Count(h => h.WorkCalendarId == c.Id && h.IsActive),
            })
            .ToListAsync();
        Calendars = rows;
        TotalCount = rows.Count;
    }

    public sealed class CalendarRow
    {
        public int Id { get; set; }
        public int? CompanyId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TimeZone { get; set; } = string.Empty;
        public short WorkDayMask { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int HolidayCount { get; set; }

        public string WorkDayPattern()
        {
            // bit 0 = Sun ... bit 6 = Sat
            var days = new[] { "Su", "M", "T", "W", "Th", "F", "Sa" };
            var on = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 7; i++)
            {
                if ((WorkDayMask & (1 << i)) != 0) { on.Add(days[i]); }
            }
            return string.Join("·", on);
        }
    }
}
