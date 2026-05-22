using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Projects
{
    // ============================================================
    // Sprint 13.5 PR #1 / ADR-026 — Customer-Project foundation.
    //
    // Schema research: docs/research_project_job_hierarchy_patterns.md
    // (11-ERP survey: SAP S/4HANA PS, Oracle Project Mfg, D365 Project Ops,
    // IFS Cloud, Epicor Kinetic, Acumatica, SYSPRO, Infor LN, JobBOSS²,
    // Global Shop, Made2Manage; plus ISA-95 + EVM standards).
    //
    // Dominant industry pattern adopted:
    //   1. CustomerProject is a separate top-level entity (not embedded in
    //      ProductionOrder). Named CustomerProject (not Project) to avoid
    //      collision with existing CipProject (capital improvement).
    //   2. Link from ProductionOrder -> CustomerProject is a NULLABLE FK on
    //      the ProductionOrder header. Both ends independently optional:
    //      jobs can exist without a project (default for job-shop work);
    //      projects can exist without jobs (commitment-only, quote, services).
    //   3. ProjectMember M:N supports joint-venture / pass-through scenarios.
    //      99% of projects have exactly one PrimaryCustomer; the rare
    //      multi-party case lives in ProjectMember without touching the
    //      FK structure.
    //   4. Program is a portfolio bucket above CustomerProject. Created
    //      empty for v1 — reserved for v2 EVM / DCAA / portfolio rollup.
    //   5. ProjectPhase is a flat-but-tree-capable WBS via nullable
    //      ParentPhaseId. UI assumes one level until a customer asks for
    //      deeper.
    //
    // Seven customer modes this schema serves (ADR-026):
    //   - Pure job-shop (>70% of ABS work)       — no project rows
    //   - Standard project with jobs             — Mode=Standard
    //   - Engineer-to-order                      — Mode=EngineerToOrder
    //   - Commitment-only / long-term agreement  — Mode=CommitmentOnly, no jobs
    //   - Recurring production                   — no project rows
    //   - Joint-venture                          — ProjectMember x 2+
    //   - Internal R&D / capital                 — PrimaryCustomerId NULL
    //
    // Naming discipline:
    //   - CustomerProject  != CipProject (capital improvement, internal capex)
    //   - ProductionOrder  != WorkOrder  (WorkOrder is maintenance domain)
    //   - "Program"        is in the Abs.FixedAssets.Models.Projects
    //                      sub-namespace to keep the Models.Projects.Program
    //                      class out of the way of the global Program class
    //                      from Program.cs (top-level statements).
    //
    // Reference: ADR-026 (ships in Sprint 13.5 PR #8) +
    //   research_project_job_hierarchy_patterns.md
    //   project_abs_customer_profile.md
    //   project_engineer_to_order_pattern.md
    // ============================================================

    // ----------------------------------------------------------------
    // Program — portfolio bucket above CustomerProject.
    //
    // Created in v1, intentionally unused on the UI. Reserved for v2 EVM /
    // DCAA / portfolio rollup once a defense / aerospace customer requires
    // CWBS-style structure. Zero v1 cost; refusing to add later would be
    // a multi-sprint migration.
    //
    // Tenant-scoped via CompanyId (SET NULL on company delete — history
    // survives). UNIQUE (CompanyId, Code).
    // ----------------------------------------------------------------
    [Table("Programs")]
    public class Program
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public ProgramStatus Status { get; set; } = ProgramStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<CustomerProject>? Projects { get; set; }
    }

    // ----------------------------------------------------------------
    // CustomerProject — the customer-facing project entity.
    //
    // Commitment-bearing. Can exist with zero ProductionOrders (commitment-
    // only, quote-stage, service-only). Has its own customer commit date,
    // quoted contract value, target margin, and ownership.
    //
    // The chain-of-evidence story for a customer (machine event -> GL)
    // is scoped to a CustomerProject row.
    // ----------------------------------------------------------------
    [Table("CustomerProjects")]
    public class CustomerProject
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        // Optional portfolio bucket (v2 EVM use).
        public int? ProgramId { get; set; }
        public Program? Program { get; set; }

        // Most projects have exactly one external customer. Internal R&D
        // and capital projects have NONE (PrimaryCustomerId NULL).
        // Joint-venture / pass-through projects supplement this via
        // ProjectMember rows.
        public int? PrimaryCustomerId { get; set; }
        public Customer? PrimaryCustomer { get; set; }

        // Human-facing identifier (e.g., "PRJ-2026-Q2-WEIR").
        // UNIQUE (CompanyId, Code) per industry convention.
        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public CustomerProjectStatus Status { get; set; } = CustomerProjectStatus.Active;

        // Drives UI specialization (CommitmentOnly hides Jobs tab;
        // EngineerToOrder enables Engineering tab; ServiceOnly disables
        // Manufacturing).
        public CustomerProjectMode Mode { get; set; } = CustomerProjectMode.Standard;

        // Default cost rollup behavior. Per-job override available via
        // ProductionOrder.ProjectPostingMode when set.
        public CustomerProjectCostingMode CostingMode { get; set; } = CustomerProjectCostingMode.Aggregate;

        // Drives whether AR uses milestone billing, POC accruals, or
        // simple completed-contract revenue posting.
        public CustomerProjectRevenueMode RevenueMode { get; set; } = CustomerProjectRevenueMode.CompletedContract;

        // The customer-committed total value of the project. Nullable
        // because internal / R&D projects do not have a contract value.
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ContractValue { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "CAD";

        [DataType(DataType.Date)]
        public DateTime? TargetStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? TargetEndDate { get; set; }

        public DateTime? ClosedAt { get; set; }

        [StringLength(100)]
        public string? ProjectManagerName { get; set; }

        public int? ProjectManagerId { get; set; }
        public ProjectManager? ProjectManager { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public ICollection<ProjectMember>? Members { get; set; }
        public ICollection<ProjectPhase>? Phases { get; set; }
    }

    // ----------------------------------------------------------------
    // ProjectMember — M:N junction for joint-venture / pass-through.
    //
    // 99% of projects have exactly one ProjectMember row (Role=Primary,
    // CustomerId=PrimaryCustomer). Joint-venture, prime/sub, and pass-
    // through projects use 2+ rows.
    //
    // UNIQUE (CustomerProjectId, CustomerId, Role) — same customer can
    // appear once per role.
    // ----------------------------------------------------------------
    [Table("ProjectMembers")]
    public class ProjectMember
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Primary;

        // Percentage share — used for joint ventures. Nullable so the
        // default Primary-only case stays clean.
        [Column(TypeName = "decimal(7,4)")]
        public decimal? SharePct { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // ----------------------------------------------------------------
    // ProjectPhase — flat-but-tree-capable WBS.
    //
    // v1 UI treats the list as flat. ParentPhaseId is reserved so a
    // customer can deepen the tree later without a schema migration.
    //
    // UNIQUE (CustomerProjectId, Code).
    // ----------------------------------------------------------------
    [Table("ProjectPhases")]
    public class ProjectPhase
    {
        public int Id { get; set; }

        public int CustomerProjectId { get; set; }
        public CustomerProject? Project { get; set; }

        // Self-FK for future tree-deepening. NULL = top-level phase.
        public int? ParentPhaseId { get; set; }
        public ProjectPhase? ParentPhase { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }

    // ----------------------------------------------------------------
    // Enums
    // ----------------------------------------------------------------

    public enum ProgramStatus
    {
        Active = 0,
        OnHold = 1,
        Closed = 2,
        Cancelled = 3
    }

    public enum CustomerProjectStatus
    {
        // Quote stage. Project exists but is not yet a binding commitment.
        Quote = 0,
        // Active commitment. Jobs may be released against it.
        Active = 1,
        OnHold = 2,
        Closed = 3,
        Cancelled = 4
    }

    public enum CustomerProjectMode
    {
        // Standard project with jobs (default).
        Standard = 0,
        // Engineer-to-order. Jobs may release before BOM is complete.
        EngineerToOrder = 1,
        // Pure service engagement (no manufacturing).
        ServiceOnly = 2,
        // Commitment-only / long-term agreement (no jobs expected).
        CommitmentOnly = 3
    }

    public enum CustomerProjectCostingMode
    {
        // Costs roll up from linked jobs only.
        Aggregate = 0,
        // Direct cost postings to the project (no aggregation from jobs).
        Direct = 1,
        // Both: jobs AND direct postings contribute.
        Both = 2
    }

    public enum CustomerProjectRevenueMode
    {
        // Revenue posts on final delivery / project close (default).
        CompletedContract = 0,
        // Revenue posts as ProjectMilestone rows complete (future PR).
        Milestone = 1,
        // Periodic accrual: costToDate / estimatedTotalCost * contractValue.
        PercentageOfCompletion = 2,
        // Revenue posts as billable transactions accrue.
        TimeAndMaterial = 3
    }

    public enum ProjectMemberRole
    {
        Primary = 0,
        JointVenture = 1,
        Subcustomer = 2,
        EndCustomer = 3,
        PassThrough = 4
    }

    // ----------------------------------------------------------------
    // ProjectPostingMode (stamped on ProductionOrder header when linked
    // to a CustomerProject). Borrowed directly from D365 — the single
    // most useful idea in this design space.
    // ----------------------------------------------------------------
    public enum ProjectPostingMode
    {
        // Job runs normally, finished item is consumed into the project
        // as an item transaction on completion.
        FinishedItem = 0,
        // Costs post directly to the project as they hit the job (project
        // is the cost object).
        Consumed = 1
    }
}
