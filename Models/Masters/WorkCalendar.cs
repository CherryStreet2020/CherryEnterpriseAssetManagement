using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // Sprint 13.5 PRA-2 — WorkCalendar master.
    //
    // Defines the schedule a tenant operates on — work days, work hours,
    // and (via Holiday rows) which dates are skipped. Powers downstream:
    //   - ETO due-date math (PR #4 schedule slip badge)
    //   - Receiving dock-hours validation (PR #5b polish)
    //   - Production schedule respect (Sprint 16 Scheduling CC)
    //   - SLA / lead-time arithmetic across services
    //
    // CompanyId nullable: NULL = system calendar (every tenant can fork
    // by creating their own row with CompanyId set). Same pattern as
    // PRA-1 Carriers — tenants can either use the system calendar
    // out-of-the-box or override it.
    //
    // The Standard Business Week (Mon-Fri 8am-5pm in tenant TZ) ships
    // as the seeded default. Per-tenant overrides expected (3-shift
    // operations, 4x10 schedules, holiday-shutdown traditions).
    [Table("WorkCalendars")]
    public class WorkCalendar
    {
        public int Id { get; set; }

        // NULL = system calendar. Tenants fork by creating their own row.
        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required, StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // IANA TZ identifier ("America/New_York"). PRA-2 v1 stores
        // tenant TZ on the calendar itself rather than dragging it from
        // session context — keeps schedule math deterministic.
        [Required, StringLength(64)]
        [Display(Name = "Time Zone (IANA)")]
        public string TimeZone { get; set; } = "America/New_York";

        // Workweek pattern. Seven bool flags rather than an enum so
        // exotic schedules (Sun+Mon off, weekend-only) Just Work without
        // a schema change. Stored as smallint bitfield for compact
        // serialization. Bit 0 = Sun, bit 1 = Mon ... bit 6 = Sat.
        // Default 0b0111110 = 62 = Mon-Fri.
        public short WorkDayMask { get; set; } = 62;

        // Default workday hours (TZ-local). Service layer composes with
        // WorkDayMask to compute "business minutes between A and B".
        public TimeSpan WorkDayStart { get; set; } = new TimeSpan(8, 0, 0);
        public TimeSpan WorkDayEnd { get; set; } = new TimeSpan(17, 0, 0);

        // Promote one calendar per tenant as the default the system uses
        // when no explicit calendar is specified on a job / shipment.
        public bool IsDefault { get; set; } = false;

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Holiday>? Holidays { get; set; }
    }
}
