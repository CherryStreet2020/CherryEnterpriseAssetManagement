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

        // ------------------------------------------------------------
        // Sprint 13.5 PR #1.5 — AI / EVM / Aero-Def field expansion.
        // Research: docs/research/customerproject-field-set.md
        // ------------------------------------------------------------

        // AI risk-score: 0..100. Computed by ProjectRiskService from
        // structured signals (overdue jobs, EVM variance, amendments).
        // Per research §2.4: never LLM-computed (LLM scoring is non-
        // deterministic and audit-hostile). NULL = not yet scored.
        // CHECK ck_customerprojects_riskscore_range enforces 0..100.
        public short? RiskScore { get; set; }

        // Three-state visual tone, decoupled from the numeric threshold
        // so we can re-tune score→tone without bulk recomputation.
        // 0=Green, 1=Amber, 2=Red. NULL=Unknown. Linear pattern.
        // CHECK ck_customerprojects_risktone_range enforces 0..2.
        public RiskTone? RiskTone { get; set; }

        // LLM-generated 1-3 sentence narrative. Rendered at top of
        // project detail and read out by voice. Refreshed by background
        // worker (ProjectAiService) via outbox; never on request path.
        public string? AiSummaryText { get; set; }

        // Versioned model identifier (e.g. "anthropic/claude-opus-4-7-1m@2026-05").
        // Lets us re-summarize on model upgrade without losing audit trail.
        [StringLength(64)]
        public string? AiSummaryModel { get; set; }

        // Staleness signal — UI fades the summary after N hours.
        public DateTime? AiSummaryGeneratedAt { get; set; }

        // Idempotent backoff for the AI refresh worker. If set in the
        // future, worker skips. Prevents thundering-herd LLM calls when
        // many events fire. Pattern matches ADR-014 D5 IdempotencyMediator.
        public DateTime? AiRefreshLockedUntil { get; set; }

        // EVM/POC: denominator for cost-based percent-of-completion and
        // EAC headline. Set at project baseline; mutated ONLY by
        // ProjectAmendments. ASC 606 input-method requirement.
        // CHECK ck_customerprojects_estimatedtotalcost_nonneg.
        [Column(TypeName = "decimal(18,4)")]
        public decimal? EstimatedTotalCost { get; set; }

        // Cached service-computed percent complete (0..100). Refreshed
        // by ProjectEvmRollupService. UI never recomputes inline.
        // CHECK ck_customerprojects_percentcomplete_range enforces 0..100.
        // Per research §3.4: never user-edited freely (ASC 606 fraud risk).
        [Column(TypeName = "decimal(5,2)")]
        public decimal? PercentComplete { get; set; }

        // Forecast end date. DISTINCT from TargetEndDate (which is the
        // customer-committed date). Linear "off track" requires both;
        // (ProjectedEndDate > TargetEndDate) drives the voice query
        // "is job X going to slip?".
        [DataType(DataType.Date)]
        public DateTime? ProjectedEndDate { get; set; }

        // Staleness signal for the EVM rollup. Admin endpoint can detect
        // stale rollups; UI shows "EVM as of …".
        public DateTime? LastEvmRollupAt { get; set; }

        // The customer-issued PO number — different from our internal
        // Code. Required on FAI report headers, AIA G701 change orders,
        // and every gov/aerospace contract.
        [StringLength(100)]
        public string? CustomerPoNumber { get; set; }

        // Contract type per FAR Part 16 / DCAA pattern. Drives revenue
        // posting rules (Sprint 14 AR). Needed earlier as a queryable
        // filter. CHECK ck_customerprojects_contracttype_range 0..5.
        public ContractType? ContractType { get; set; }

        // Quality program required by the customer. Drives FAI requirement
        // in Sprint 14 Quality. Per research §4.3: do NOT add a separate
        // RequiresFai bool — QualityProgram != None implies FAI per AS9100
        // 8.5.1.3. CHECK ck_customerprojects_qualityprogram_range 0..5.
        public QualityProgram? QualityProgram { get; set; }

        // Export-control jurisdiction marker. Per research §4.3: stores
        // the marker ONLY, never classified or ITAR-controlled technical
        // data. Drives future RLS row-filter (Sprint 15) so non-cleared
        // users can't see ITAR rows. Service layer enforces
        // Company.ProjectExportControlRequired = true → must be set
        // explicitly on create. CHECK ck_customerprojects_exportcontrol_range 0..3.
        public ExportControl ExportControl { get; set; } = ExportControl.None;

        public ICollection<ProjectMember>? Members { get; set; }
        public ICollection<ProjectPhase>? Phases { get; set; }
        public ICollection<ProjectAmendment>? Amendments { get; set; }
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

        // ============================================================
        // B9 Wave 3 PR-7 — WBS hardening. ProjectPhase IS the WBS backbone
        // (one self-nesting tree via ParentPhaseId; no parallel ProjectWBS).
        // These fields turn a phase node into a controllable WBS element:
        // classification, ownership, a cost bucket, baseline/forecast/actual
        // schedule, weighted progress (100%-rule), and a set-once baseline
        // stamp. Child entity — tenant-scoped THROUGH the parent project;
        // it carries no CompanyId of its own.
        // ============================================================

        // WBS classification (spec §5 "WBS types"). Default Phase.
        public WbsType WbsType { get; set; } = WbsType.Phase;

        // Depth in the WBS tree; root = 1. Maintained by the service on add.
        public int WbsLevel { get; set; } = 1;

        // Responsible owner — REQUIRED on every leaf before the project WBS
        // can be baselined (B9 §20 validation). Free text in v1; a later PR
        // can FK this to a User / Department master.
        [StringLength(200)]
        public string? ResponsibleOwner { get; set; }

        [StringLength(64)]
        public string? ResponsibleDepartment { get; set; }

        // Cost bucket — the control account this WBS element rolls into plus
        // the four cost columns (spec §5 WBS dictionary). A leaf MUST have a
        // non-null PlannedCost (the "cost bucket") to baseline.
        [StringLength(64)]
        public string? ControlAccount { get; set; }

        public decimal? PlannedCost { get; set; }
        public decimal? ActualCost { get; set; }
        public decimal? CommittedCost { get; set; }
        public decimal? ForecastCost { get; set; }

        // 100%-rule weight — this element's share of its PARENT's scope.
        // Siblings under one parent must sum to 100 to baseline; roots
        // (ParentPhaseId == null) must sum to 100 across the project.
        // 0..100, CHECK enforced.
        public decimal? WeightPercent { get; set; }

        // Progress 0..100, CHECK enforced. A leaf's value is entered directly;
        // a parent's value is the weighted roll-up of its children (computed
        // in IProjectWbsService, not persisted on parents).
        public decimal? PercentComplete { get; set; }

        // Schedule (spec §5): baseline is frozen at baseline; forecast is the
        // live plan; actual is stamped as work happens.
        public DateTime? BaselineStart { get; set; }
        public DateTime? BaselineFinish { get; set; }
        public DateTime? ForecastStart { get; set; }
        public DateTime? ForecastFinish { get; set; }
        public DateTime? ActualStart { get; set; }
        public DateTime? ActualFinish { get; set; }

        // Lifecycle status of this WBS element.
        public ProjectPhaseStatus Status { get; set; } = ProjectPhaseStatus.NotStarted;

        // Customer-visible WBS element (spec §5 customer-visible flag) — drives
        // what a customer portal / statement surfaces.
        public bool CustomerVisible { get; set; } = false;

        // Set-once baseline stamp. Once IsBaselined, BaselineStart/Finish are
        // frozen; re-baselining requires an explicit allowRebaseline call.
        public bool IsBaselined { get; set; } = false;
        public DateTime? BaselinedAt { get; set; }
        [StringLength(100)]
        public string? BaselinedBy { get; set; }

        // xmin concurrency token (baseline + roll-up mutate this node).
        public byte[]? RowVersion { get; set; }
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
    // B9 Wave 3 PR-7 — WBS classification + lifecycle (spec §5).
    // ----------------------------------------------------------------

    // WBS element type (spec §5 "WBS types"). Default Phase.
    public enum WbsType
    {
        Phase = 0,          // Engineering, Procurement, Manufacturing, Install
        Deliverable = 1,    // Machine A, Conveyor B, Control Panel C
        WorkPackage = 2,    // lowest controllable unit of work
        Contract = 3,       // contract line 1, 2, 3
        Cost = 4,           // labor, material, subcontract
        Location = 5,       // plant 1, line 2, cell 3
        System = 6,         // mechanical, electrical, controls
        Manufacturing = 7,  // job group, assembly group
        Service = 8,        // install, commissioning, warranty
        Billing = 9         // deposit, milestone, final payment
    }

    // Lifecycle status of a WBS element / phase.
    public enum ProjectPhaseStatus
    {
        NotStarted = 0,
        InProgress = 1,
        Complete = 2,
        OnHold = 3,
        Cancelled = 4
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

    // ================================================================
    // Sprint 13.5 PR #1.5 enums — see research_customerproject_field_set.md
    // ================================================================

    // Visual risk tone — decoupled from the numeric RiskScore so the
    // tone threshold can be re-tuned without bulk recomputation.
    // Linear "On track / At risk / Off track" pattern.
    public enum RiskTone : short
    {
        Green = 0,
        Amber = 1,
        Red = 2
    }

    // FAR Part 16 contract types + commercial. Service layer maps to
    // revenue-recognition rules in Sprint 14 AR.
    public enum ContractType : short
    {
        // No contract type declared / not applicable.
        None = 0,
        // Firm Fixed Price — bedrock commercial / aero-def.
        FirmFixedPrice = 1,
        // Cost Plus Fixed Fee — requires CAS-compliant cost accounting.
        CostPlusFixedFee = 2,
        // Cost Plus Incentive Fee — requires CAS + incentive math.
        CostPlusIncentiveFee = 3,
        // Time & Materials.
        TimeAndMaterials = 4,
        // Indefinite Delivery / Indefinite Quantity — ordering framework.
        Idiq = 5
    }

    // Customer-required quality program. Drives FAI requirement
    // (Sprint 14 / PR #1.75) and audit posture.
    public enum QualityProgram : short
    {
        None = 0,
        Iso9001 = 1,
        As9100 = 2,        // Aerospace manufacturers
        As9120 = 3,        // Aerospace distributors
        Iatf16949 = 4,     // Automotive
        Custom = 5         // Customer-specific standard
    }

    // Export-control jurisdiction marker. STORES THE MARKER ONLY —
    // never classified or ITAR-controlled technical data. Drives
    // future RLS row-filter (Sprint 15) for non-cleared users.
    public enum ExportControl : short
    {
        None = 0,                 // Not export-controlled
        Ear99 = 1,                // EAR but unrestricted
        EarControlled = 2,        // Has ECCN; license may be required
        Itar = 3                  // USML; full DDTC compliance
    }
}
