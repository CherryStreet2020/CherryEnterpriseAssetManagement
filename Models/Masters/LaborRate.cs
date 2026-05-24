using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-8 — LaborRateMaster (effective-dated rate matrix).
    //
    // Master Files Baseline cascade ship #6 of 10.
    //
    // Resolves the actual $/hr for an Employee or WageGroup at a given point
    // in time. Effective-dated so payroll history survives rate changes.
    //
    // RESOLUTION ORDER (most-specific wins, look-up at posting time):
    //   1. LaborRateMaster WHERE CompanyId = tenant AND EmployeeId = X
    //                AND EffectiveFromUtc <= now AND (EffectiveToUtc IS NULL
    //                                                  OR EffectiveToUtc > now)
    //   2. LaborRateMaster WHERE CompanyId = tenant AND EmployeeId IS NULL
    //                AND WageGroupId = (employee.DefaultWageGroupId)
    //                AND EffectiveFromUtc <= now AND (EffectiveToUtc IS NULL ...)
    //   3. WageGroup.IndicativeRatePerHour fallback
    //   4. Hard error (mis-configured tenant payroll)
    //
    // CROSS-TENANT — operational data, CompanyId NOT NULL. No system templates;
    // every rate row is tenant-specific by definition.
    //
    // UNIQUE: there is no hard UNIQUE because date ranges can adjoin (current
    // row's To = next row's From). Application-layer enforces non-overlap by
    // closing the previous row's EffectiveToUtc before opening a new one.
    //
    // OVERTIME / DOUBLETIME: stored explicitly because the simple "1.5x base"
    // doesn't always hold (union contracts, weekend premiums, holiday rates).
    // NULL means "no overtime applicable" (typical for exempt salaried).
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.6
    //   - memory: reference_master_files_baseline.md
    // =============================================================================
    [Table("LaborRateMasters")]
    public class LaborRateMaster
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }

        // NULL = group-level rate (applies to all employees in the WageGroup
        // unless they have a per-employee override row).
        // SET = per-employee override.
        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        // REQUIRED — every rate row carries a WageGroup ref, even for per-
        // employee overrides (lets us re-classify an employee to a new group
        // without losing the historical "which group did they belong to when
        // earning this rate" link).
        public int WageGroupId { get; set; }
        public WageGroup? WageGroup { get; set; }

        // ---------------------------------------------------------------------
        // RATES.
        // ---------------------------------------------------------------------
        [Column(TypeName = "numeric(14,4)")]
        public decimal BaseRatePerHour { get; set; }

        // OT rate (typically 1.5x base for US non-exempt). NULL = no OT (exempt
        // salaried or contractor).
        [Column(TypeName = "numeric(14,4)")]
        public decimal? OvertimeRatePerHour { get; set; }

        // Double-time / 2x rate (Saturday in some unions, Sunday/holiday in
        // many; California 7th-consecutive-day in some industries).
        [Column(TypeName = "numeric(14,4)")]
        public decimal? DoubleTimeRatePerHour { get; set; }

        // Optional shift differential adders (night shift, weekend). Applied
        // additively to base/OT at posting time.
        [Column(TypeName = "numeric(14,4)")]
        public decimal? ShiftDifferentialPerHour { get; set; }

        // Currency FK (NULL = inherit Company.FunctionalCurrency).
        public int? CurrencyId { get; set; }

        // ---------------------------------------------------------------------
        // EFFECTIVE-DATING.
        // ---------------------------------------------------------------------
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        // NULL = open-ended (current rate). When a new rate row opens, the
        // previous current row gets its EffectiveToUtc closed to the new
        // EffectiveFromUtc by the service layer.
        public DateTime? EffectiveToUtc { get; set; }

        // ---------------------------------------------------------------------
        // PAYROLL TRACEABILITY.
        // ---------------------------------------------------------------------

        // Optional reference to the payroll event that originated the change
        // (e.g. promotion PO, merit increase batch, contract renewal). Free-
        // form so tenants can wire their own identifier.
        [StringLength(100)]
        public string? SourceReference { get; set; }

        [StringLength(500)]
        public string? ChangeReason { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }
}
