using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.WorkOrders
{
    // ADR-012 v0.2 / PR #119.5 — Atomic number generator (SAP NRIV pattern).
    //
    // One row per (Classification, Year, TenantId). Holds the current
    // counter for that bucket. The service hands out the NEXT number
    // atomically via SELECT FOR UPDATE inside a short Postgres
    // transaction so concurrent ingests never collide.
    //
    // Why per-year buckets? Industry convention is "PM-2026-001234,
    // AFE-2026-005, NCR-2026-0042, ECO-2026-12-001" — year-segmented
    // counters that reset on Jan 1 of each new year. The per-year row
    // pattern lets us roll over cleanly without locking the entire
    // table.
    //
    // Why per-tenant rows? Multi-tenant deployments can't share a
    // counter (tenant A shouldn't see "PM-2026-1234" from tenant B's
    // shop floor). TenantId NULL is the single-tenant fallback used
    // by the current Replit deploy.
    //
    // xmin RowVersion gives us optimistic-concurrency belt-and-
    // suspenders on top of SELECT FOR UPDATE — if two services
    // somehow both grab the row, the second one's SaveChangesAsync
    // throws DbUpdateConcurrencyException and retries.
    [Table("NumberSequence")]
    public class NumberSequence
    {
        public int Id { get; set; }

        public WorkOrderClassification Classification { get; set; }

        // Calendar year the counter applies to. New year = new row.
        public int Year { get; set; }

        // The type-specific prefix prepended to the generated number.
        // Stored on the row (not derived from classification) so admins
        // can override per tenant if their industry uses different
        // conventions (e.g. "WO-" for everything, "JOB-" for the
        // legacy IBM-shop crowd).
        [Required, StringLength(8)]
        public string Prefix { get; set; } = string.Empty;

        // The last value handed out. NextAsync increments this and
        // returns Prefix-Year-CurrentValue. Stored as int (max ~2.1B
        // per year per tenant per classification — plenty of headroom
        // for even the busiest plant).
        public int CurrentValue { get; set; } = 0;

        // Number-of-digits zero-pad on the counter portion. PM-2026-1234
        // would use Padding=4; PM-2026-001234 uses Padding=6. Default 4
        // matches the most common industry convention.
        public int Padding { get; set; } = 4;

        // Optional separator between Prefix and Year (default '-').
        // Some tenants prefer no separator: "PM2026-001234". Stored so
        // admins can tweak per-classification without code change.
        [StringLength(2)]
        public string YearSeparator { get; set; } = "-";

        // Optional separator between Year and Counter.
        [StringLength(2)]
        public string CounterSeparator { get; set; } = "-";

        // NULL = global default (single-tenant). Non-NULL = per-tenant
        // override. The service merges by precedence (tenant wins).
        public int? TenantId { get; set; }

        // Optimistic concurrency belt-and-suspenders on top of
        // SELECT FOR UPDATE.
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
