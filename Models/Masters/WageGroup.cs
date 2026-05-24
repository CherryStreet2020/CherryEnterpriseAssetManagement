using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-8 — WageGroup (hourly band classification).
    //
    // Master Files Baseline cascade ship #6 of 10.
    //
    // Classifies employees into pay bands. Drives LaborRate lookup at posting
    // time: Employee.DefaultWageGroupId → LaborRate(WageGroup, effective-date)
    // → $/hr. Per-employee LaborRate rows override the group default.
    //
    // FLSA (Fair Labor Standards Act, US) classification matters because
    // exempt employees don't earn overtime; non-exempt do. The IsExempt flag
    // drives whether OvertimeRate applies in LaborRate.
    //
    // CROSS-TENANT REFERENCE pattern (mirrors PRA-6/PRA-7 masters):
    //   CompanyId NULL = system template (10 industry-baseline rows shipped)
    //   CompanyId set  = tenant override / extension
    //
    // UNIQUE per (Code) for system + (CompanyId, Code) for tenant.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.6
    //   - memory: reference_master_files_baseline.md
    // =============================================================================
    [Table("WageGroups")]
    public class WageGroup
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        // Stable code (e.g. "OP-LVL1", "OP-LVL2", "SETUP", "LEAD", "QC",
        // "MAINT", "HR-EXEMPT", "HR-NONEXEMPT", "CONTRACT").
        [Required, StringLength(32)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public WageGroupType GroupType { get; set; } = WageGroupType.Hourly;

        // FLSA exempt — true means no overtime accrual; false means OT applies
        // above the configured weekly threshold (typically 40hr in US).
        public bool IsExempt { get; set; } = false;

        // Standard weekly hours used for OT threshold calc + utilization KPI.
        public int StandardWeeklyHours { get; set; } = 40;

        // ---------------------------------------------------------------------
        // GL ROUTING DEFAULTS — group-level fallbacks for PostingProfile
        // labor resolution. Most tenants will use Department-level defaults
        // (PRA-8 also adds Default*GlAccountId columns to Department) but
        // group-level overrides catch the case where a tenant wants e.g.
        // contractor labor posted to a different account than employee labor
        // regardless of department.
        // ---------------------------------------------------------------------
        public int? DefaultLaborGlAccountId { get; set; }
        public int? DefaultOhGlAccountId { get; set; }
        public int? DefaultAbsorbedGlAccountId { get; set; }

        // Optional default rate placeholder — real rates come from LaborRate
        // (effective-dated). This is informational / UI-display only.
        [Column(TypeName = "numeric(12,4)")]
        public decimal? IndicativeRatePerHour { get; set; }

        // Currency for the indicative rate (NULL = inherit Company.FunctionalCurrency).
        public int? CurrencyId { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    // =============================================================================
    // WageGroupType — payroll category.
    // =============================================================================
    public enum WageGroupType
    {
        Hourly = 0,                 // Paid per hour worked
        Salaried = 1,               // Paid per pay period (often exempt)
        PieceRate = 2,              // Paid per unit produced
        Commission = 3,             // Sales-incentive based
        Contractor = 4,             // 1099 / external — no payroll relationship
        Stipend = 5,                // Fixed periodic payment (interns, some apprentices)
        Other = 99
    }
}
