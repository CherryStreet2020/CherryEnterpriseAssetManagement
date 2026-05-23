using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // Sprint 13.5 PRA-2 — Holiday master.
    //
    // Per-calendar non-working dates. Service layer subtracts these from
    // WorkCalendar.WorkDayMask when computing business-day arithmetic.
    //
    // PRA-2 v1 is INSTANCE-BASED, not RULE-BASED — each holiday is one
    // dated row. Rule-based recurrence ("3rd Monday in January") is the
    // PRA-2.1 polish PR. Reason: the per-year instance pattern is what
    // every customer audit actually wants to see; the recurrence rule
    // is what they want to see ONCE on the admin screen. v1 ships the
    // audit view first; the admin "generate next 5 years" button comes
    // when a customer asks.
    //
    // Optional SubdivisionId: when set, the holiday only applies to
    // jobs / shipments in that state (e.g. "Cesar Chavez Day" → CA only;
    // "Patriots' Day" → MA + ME). NULL = applies to all subdivisions
    // under the calendar's tenant (federal / national holidays).
    [Table("Holidays")]
    public class Holiday
    {
        public long Id { get; set; }

        public int WorkCalendarId { get; set; }
        public WorkCalendar? WorkCalendar { get; set; }

        // The actual observed date. For holidays that fall on weekends
        // and slide to Mon/Fri (per US federal "in lieu" rule), the
        // OBSERVED date goes here. NominalDate captures the original.
        [DataType(DataType.Date)]
        [Required]
        public DateTime ObservedDate { get; set; }

        // The "true" date of the holiday (e.g. July 4 always, even
        // when observed on July 3 or July 5). NULL when there's no
        // distinction. Drives the AS9100 / audit view that needs the
        // canonical date for traceability.
        [DataType(DataType.Date)]
        public DateTime? NominalDate { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // State / province scoping — when set, the holiday only applies
        // to addresses in that subdivision. NULL = applies country-wide
        // under the calendar's company.
        public int? SubdivisionId { get; set; }
        public Subdivision? Subdivision { get; set; }

        // Discriminator for filtering: federal vs state vs religious vs
        // company-specific (e.g. plant shutdown week). Drives the UI
        // tooltip on the calendar grid.
        public HolidayCategory Category { get; set; } = HolidayCategory.Federal;

        // Half-day vs full-day. Half-days subtract half a business day
        // from lead-time math.
        public bool IsHalfDay { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum HolidayCategory : short
    {
        Federal = 0,
        State = 1,
        Religious = 2,
        CompanyShutdown = 3,
        Customer = 4,
        Other = 99
    }
}
