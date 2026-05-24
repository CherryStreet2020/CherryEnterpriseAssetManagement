using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-8 — Employee (org-wide HR master).
    //
    // Master Files Baseline cascade ship #6 of 10. Closes the "Employee master
    // missing" gap from docs/research/master-files-baseline-2026-05-24.md §6.6.
    //
    // POSITION IN THE STACK:
    //   Employee (org-wide HR master) — THIS table
    //     ├── Technician (1:0..1 satellite — existing EAM maintenance side)
    //     │       Technician.EmployeeId FK added in this PR
    //     ├── DepartmentId FK — primary department assignment
    //     ├── DefaultWageGroupId FK — feeds LaborRate resolution
    //     └── ManagerId self-ref — org chart hierarchy
    //
    // OPERATIONAL DATA — CompanyId is NOT NULL. Employee is always tenant-
    // specific (no system templates). On a multi-company tenant, an employee
    // is modeled per CompanyId; intercompany allocations are a downstream
    // accounting concern, not a schema concern.
    //
    // UNIQUE per (CompanyId, EmployeeNumber).
    //
    // SENSITIVE DATA NOTE: PII fields (SSN-equivalent national-ID, full DOB,
    // salary detail) are deliberately OUT of this schema. PRA-8 ships the
    // operational shape; payroll/PII fields land in a future restricted-
    // access satellite (EmployeePayrollDetail) gated by role + audit log.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.6
    //   - memory: reference_master_files_baseline.md
    //   - memory: reference_bic_entity_checklist.md
    //   - memory: feedback_no_shortcuts_multi_tenant_lineage.md
    // =============================================================================
    [Table("Employees")]
    public class Employee
    {
        public int Id { get; set; }

        // Tenant-owned — never NULL.
        public int CompanyId { get; set; }

        // Stable HR identifier (tenant-scoped). UPPERCASE/hyphen format common
        // (e.g. "EMP-2026-00123") but tenant-configurable.
        [Required, StringLength(32)]
        public string EmployeeNumber { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        // Optional middle name / preferred name override for UI display.
        [StringLength(100)]
        public string? MiddleName { get; set; }

        [StringLength(150)]
        public string? PreferredName { get; set; }

        // Computed full name for search/sort convenience. Persisted (not
        // computed column) so it's indexable + survives partial updates.
        [Required, StringLength(300)]
        public string FullName { get; set; } = string.Empty;

        // ---------------------------------------------------------------------
        // CONTACT — work email/phone only. Personal contact lives on the
        // future EmployeePayrollDetail satellite.
        // ---------------------------------------------------------------------
        [StringLength(200)]
        [EmailAddress]
        public string? WorkEmail { get; set; }

        [StringLength(40)]
        public string? WorkPhone { get; set; }

        // ---------------------------------------------------------------------
        // EMPLOYMENT.
        // ---------------------------------------------------------------------
        [Required, StringLength(150)]
        public string JobTitle { get; set; } = string.Empty;

        public EmployeeType EmployeeType { get; set; } = EmployeeType.FullTime;

        public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

        public DateTime HireDate { get; set; } = DateTime.UtcNow;

        public DateTime? TerminationDate { get; set; }

        // ---------------------------------------------------------------------
        // ORG STRUCTURE.
        // ---------------------------------------------------------------------

        // Department FK (existing entity in Models/GlAccount.cs).
        public int? DepartmentId { get; set; }

        // Org-chart self-reference.
        public int? ManagerId { get; set; }
        public Employee? Manager { get; set; }

        // Primary site of work.
        public int? SiteId { get; set; }

        // Optional fine-grained work location (e.g. specific bay/cell).
        public int? LocationId { get; set; }

        // ---------------------------------------------------------------------
        // PAYROLL / WAGE — links to PRA-8 sibling tables.
        // ---------------------------------------------------------------------

        // Default WageGroup — drives LaborRate lookup at posting time.
        public int? DefaultWageGroupId { get; set; }
        public WageGroup? DefaultWageGroup { get; set; }

        // Per-employee GL override — when set, beats Department/group defaults.
        // Typical use: cross-cost-center transfers, special-project labor capture.
        public int? DefaultLaborGlAccountId { get; set; }

        // Currency override (NULL = inherit from Company.FunctionalCurrency).
        public int? DefaultCurrencyId { get; set; }

        // ---------------------------------------------------------------------
        // FLAGS.
        // ---------------------------------------------------------------------

        // True for employees that operate the floor (issuable to ProductionOrder
        // operator slot). False for office / admin / management.
        public bool IsProductionResource { get; set; } = false;

        // True for employees with an EAM maintenance role — wires through to
        // Technician satellite via Technician.EmployeeId FK.
        public bool IsMaintenanceResource { get; set; } = false;

        public bool IsActive { get; set; } = true;

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    // =============================================================================
    // EmployeeType — payroll classification driving FLSA + benefits eligibility.
    // =============================================================================
    public enum EmployeeType
    {
        FullTime = 0,
        PartTime = 1,
        Temporary = 2,
        Seasonal = 3,
        Contractor = 4,         // 1099 in US; not on payroll
        Intern = 5,
        Apprentice = 6,
        Other = 99
    }

    // =============================================================================
    // EmployeeStatus — lifecycle.
    // =============================================================================
    public enum EmployeeStatus
    {
        Active = 0,
        OnLeave = 1,            // FMLA / parental / medical / military
        Suspended = 2,
        Terminated = 3,
        Retired = 4,
        Deceased = 5,
        Other = 99
    }
}
