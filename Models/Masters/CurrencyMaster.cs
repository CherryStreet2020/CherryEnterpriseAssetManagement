using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-6 — Currency master (ISO 4217).
    //
    // Replaces the flat 3-char `Currency` string field on Customer / Vendor
    // (which has no shape and can hold any garbage). ISO 4217 master table
    // carries the metadata every multi-currency ERP needs: IsoCode, Name,
    // Symbol, DecimalPlaces (JPY = 0, KRW = 0, BHD = 3, everything else = 2),
    // RoundingRule (HalfEven / HalfUp / Floor / Ceiling — JE rounding policy).
    //
    // CROSS-TENANT REFERENCE pattern (mirrors UnitOfMeasureMaster):
    //   - CompanyId NULL  = system row (ISO catalog, seeded ~30 rows)
    //   - CompanyId set   = tenant-specific extension (rare — custom internal
    //                       point system, voucher currency, etc.)
    //
    // UNIQUE: IsoCode (CompanyId IS NULL) is globally unique — the ISO catalog
    // has one row per code. Tenant overrides UNIQUE on (CompanyId, Code) when
    // CompanyId IS NOT NULL.
    //
    // FOREWARD-LOOKING: Customer.Currency / Vendor.Currency / Company.Currency
    // are still strings today (DEF-008 pattern — keep the legacy string for
    // back-compat, add a `CurrencyId int? FK` in a follow-up cleanup PR).
    // This PR ships the master table only; FK threading is deferred to PRA-6.x.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.1
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    //   - ISO 4217:2015 (currency codes + minor unit / decimals)
    // =============================================================================
    [Table("CurrencyMasters")]
    public class CurrencyMaster
    {
        public int Id { get; set; }

        // NULL = system row. INT > 0 = tenant-specific extension.
        public int? CompanyId { get; set; }

        // ISO 4217 alpha-3 code (e.g. "USD", "CAD", "EUR"). UPPERCASE.
        // For system rows, this is the canonical ISO code and is globally
        // unique across all system rows.
        [Required, StringLength(3)]
        [Display(Name = "ISO Code")]
        public string IsoCode { get; set; } = string.Empty;

        // ISO 4217 numeric code (e.g. 840 for USD, 124 for CAD).
        // Stored as string so leading zeros survive ("008" for ALL).
        [StringLength(3)]
        [Display(Name = "ISO Numeric")]
        public string? NumericCode { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        // Display glyph (e.g. "$", "€", "¥", "C$", "₹"). Optional.
        [StringLength(8)]
        public string? Symbol { get; set; }

        // ISO 4217 minor unit precision:
        //   0 = JPY, KRW, CLP, ISK, VND, RWF (no fractional unit)
        //   2 = most fiat (USD, EUR, GBP, CAD, AUD, ...)
        //   3 = BHD, JOD, KWD, OMR, TND (1/1000 of a dinar)
        //   4 = a few crypto-adjacent / accounting-internal cases
        public int DecimalPlaces { get; set; } = 2;

        // Rounding policy for JE posting + invoice line totals. Defaults to
        // banker's rounding (HalfEven) which the accounting world calls
        // "round-half-to-even" and uses by default.
        [StringLength(16)]
        public string RoundingRule { get; set; } = "HalfEven";

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
