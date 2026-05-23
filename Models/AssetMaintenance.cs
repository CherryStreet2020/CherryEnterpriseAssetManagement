using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    // ADR-012 v0.2 / PR #119.7 — Renamed from `MaintenanceEvent`.
    //
    // The class is the unified WorkOrder header — spans Maintenance,
    // CIP, Quality, HSE, Engineering classifications (Production is a
    // sibling table, not a subtype, per the v0.2 decision).
    //
    // The [Table("WorkOrders")] attribute pairs the new class name
    // with the renamed DB table. PR #119.7's migration runs
    // `ALTER TABLE "MaintenanceEvents" RENAME TO "WorkOrders"` + renames
    // every FK constraint + every index. Migration history files keep
    // referring to "MaintenanceEvents" because they're immutable
    // snapshots of what the schema was at THAT migration's point in time.
    //
    // The class lives in `Abs.FixedAssets.Models` (root namespace), not
    // `Abs.FixedAssets.Models.WorkOrders`, because it's a grand entity
    // (like Asset) — the WorkOrders sub-namespace holds the satellite
    // tables (CipWorkOrderDetails, QualityWorkOrderDetails, etc.) and
    // the configuration tables (WorkOrderFieldVisibility, StatusProfile,
    // Approval, NumberSequence).
    [Table("WorkOrders")]
    public class WorkOrder
    {
        public int Id { get; set; }

        // Sprint 13.5 PR #5c.2 — Direct tenant scoping (defensive denormalization
        // from Asset.CompanyId). The historical pattern scoped WorkOrders through
        // Asset.CompanyId, and that continues to work — but a direct CompanyId
        // column lets tenant-filter queries skip the Asset JOIN, hardens against
        // an Asset record having its CompanyId mistakenly nulled, and satisfies
        // the BIC checklist rule that user-mutable entities should carry CompanyId
        // NOT NULL directly. Backfilled from Assets table in migration 20260524120000.
        // CHECK >= 0 (grace period — never expected to be 0 in practice since
        // AssetId is NOT NULL and Assets all have CompanyId).
        public int CompanyId { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        // ADR-012 / PR #119.1 — Unified Work Orders.
        //
        // Classification is the top-level discriminator across Maintenance,
        // Production, Quality, Engineering, HSE, and Project work orders.
        // (Originally proposed as "Category" in the ADR; renamed to
        // Classification in PR #119.1.1 to avoid collision with the existing
        // WorkOrderCategory enum used by the WorkOrderType lookup table —
        // which is a different concept entirely: it categorizes maintenance
        // flavors on lookup-master rows.)
        //
        // Existing rows backfill to Maintenance (lossless). The .NET class
        // name MaintenanceEvent is retained for backward compatibility —
        // it's now a misnomer but a clean rename is deferred to a future
        // sprint with a freeze window. Conceptually this IS the
        // WorkOrder header table.
        //
        // The legacy MaintenanceType enum is now the sub-type WITHIN
        // Classification=Maintenance. For non-Maintenance classifications
        // the Type column defaults to Other and is ignored by the UI;
        // per-classification detail lives in the satellite tables shipped
        // in PR #119.2 (Production/Quality/Engineering/HseWorkOrderDetails).
        public WorkOrderClassification Classification { get; set; } = WorkOrderClassification.Maintenance;

        public MaintenanceType Type { get; set; } = MaintenanceType.Preventative;

        public int? TypeLookupValueId { get; set; }
        public LookupValue? TypeLookupValue { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime ScheduledDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CompletedDate { get; set; }

        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        public int? PriorityLookupValueId { get; set; }
        public LookupValue? PriorityLookupValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LaborCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PartsCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaterialsCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OutsideVendorCost { get; set; }

        [StringLength(100)]
        public string? Vendor { get; set; }

        [StringLength(100)]
        public string? TechnicianName { get; set; }

        public int? TechnicianId { get; set; }
        public Technician? Technician { get; set; }

        [StringLength(50)]
        public string? WorkOrderNumber { get; set; }

        [StringLength(50)]
        public string? PurchaseOrderNumber { get; set; }

        public decimal? DowntimeHours { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LaborHours { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OvertimeHours { get; set; }

        public WorkOrderApprovalStatus ApprovalStatus { get; set; } = WorkOrderApprovalStatus.NotRequired;

        public int? CipProjectId { get; set; }
        public CipProject? CipProject { get; set; }

        // S1-2: explicit FK linkage to the PM occurrence and template-asset
        // assignment that generated this WO. Replaces the brittle
        // CustomField1 = "PMTA:N" string hack that conflated PMOccurrence.Id
        // with PMTemplateAsset.Id (different tables, different namespaces) —
        // see docs/audit-2026-05-08-followup/STRUCTURAL_AUDIT.md.
        //
        // FK-only (no navigation property): PMOccurrence already has a
        // WorkOrder navigation pointing back here, and EF's convention
        // would pair the two as a one-to-one if both sides had navs.
        // Service code looks up via _context.Set<PMOccurrence>() by Id —
        // no Include is needed because tenancy is enforced through the
        // event's Asset.CompanyId rather than via this FK.
        public int? PMOccurrenceId { get; set; }

        public int? PMTemplateAssetId { get; set; }

        public int? ApprovedById { get; set; }
        public User? ApprovedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public int? RequestedById { get; set; }
        public User? RequestedBy { get; set; }

        public DateTime? RequestedAt { get; set; }

        // PR #104 (B-16): FailureCode was a free-text string from day one.
        // Operators typed inconsistently ("BRG-WEAR", "bearing wear", "bearing-wear")
        // which broke the Pareto / Weibull aggregation that needs canonical codes.
        // Kept the string field for backward compatibility with existing reports
        // and CloseoutService.GenerateCloseoutSummary; new code reads/writes via
        // the FK to the seeded FailureCode master (24 standard rows). The text
        // column is treated as a denormalized display label going forward, set
        // alongside the FK at write time. Backfill in the 20260516 migration
        // matches on Code (case-insensitive) and stamps FailureCodeId where
        // an unambiguous master row exists.
        [StringLength(500)]
        public string? FailureCode { get; set; }

        public int? FailureCodeId { get; set; }
        public FailureCode? FailureCodeRef { get; set; }

        [StringLength(500)]
        public string? RootCause { get; set; }

        [StringLength(500)]
        public string? CorrectiveAction { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(1000)]
        public string? Resolution { get; set; }

        public int? RecurrenceIntervalDays { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NextScheduledDate { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CompletedBy { get; set; }

        [StringLength(2000)]
        public string? ResolutionSummary { get; set; }

        [StringLength(2000)]
        public string? LessonsLearned { get; set; }

        public DateTime? ClosedAt { get; set; }

        [StringLength(100)]
        public string? ClosedBy { get; set; }

        public DateTime? StartedAt { get; set; }

        [StringLength(100)]
        public string? StartedBy { get; set; }

        [StringLength(500)]
        public string? HoldReason { get; set; }

        public string? CustomField1 { get; set; }
        public string? CustomField2 { get; set; }
        public string? CustomField3 { get; set; }
        public string? CustomField4 { get; set; }
        public string? CustomField5 { get; set; }
        public string? CustomField6 { get; set; }
        public string? CustomField7 { get; set; }
        public string? CustomField8 { get; set; }
        public string? CustomField9 { get; set; }
        public string? CustomField10 { get; set; }

        public ICollection<WorkOrderOperation>? Operations { get; set; }

        // S1-8 / S2-8: optimistic concurrency via PG xmin. See
        // Data/XminRowVersionExtensions.cs.
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // ADR-012 / PR #119.1 — Unified Work Orders: ERP / external-system linkage.
        //
        // Set when this work order was created from (or synced to) an external
        // system: SAP PP, Oracle EBS, Dynamics 365, Plex, Epicor, manual CSV
        // import, etc. NULL for native Cherry-created WOs.
        //
        // ExternalSource is free text in PR #119.1; a lookup-table migration
        // (PR #119.4) will harden this once we have 3+ live integrations to
        // seed the dropdown with.
        [StringLength(64)]
        public string? ExternalWorkOrderId { get; set; }

        [StringLength(32)]
        public string? ExternalSource { get; set; }

        // ADR-012 v0.2 / PR #119.6 — Revision tracking for re-issued WOs.
        //
        // Pattern:
        //   Master (original) WO: Revision = 0, MasterWorkOrderId = NULL
        //   Revision N derived:   Revision = N, MasterWorkOrderId = master.Id
        //
        // Critical for:
        //   - CIP / AFE supplements (AFE-2026-005 r0, r1, r2 as scope grows)
        //   - Engineering ECO/MOC re-issues (ECO-2026-12-001 r0 → r1 with
        //     additional PHA findings per 29 CFR 1910.119)
        //   - Maintenance re-issued correctives (rare but happens when scope
        //     expands mid-job — new revision rather than editing the original)
        //
        // The original WO stays editable until its first revision is created;
        // after that, the original is read-only and revisions accumulate
        // forward. Re-issue UI ships in Phase F.
        //
        // Self-FK uses ON DELETE SET NULL: deleting a master orphans its
        // revisions rather than cascade-deleting them. Each orphan becomes
        // its own master (Revision stays at its old value, MasterWorkOrderId
        // becomes NULL). Most-forgiving option — admins can re-link by hand;
        // cascade deletes would silently wipe historical audit trail.
        public short Revision { get; set; } = 0;

        public int? MasterWorkOrderId { get; set; }
        public WorkOrder? MasterWorkOrder { get; set; }
    }

    public class MaintenanceSchedule
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public MaintenanceType Type { get; set; } = MaintenanceType.Preventative;

        public RecurrenceType Recurrence { get; set; } = RecurrenceType.Monthly;

        public int IntervalValue { get; set; } = 1;

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? LastGeneratedDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? NextDueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal EstimatedCost { get; set; }

        [StringLength(100)]
        public string? AssignedVendor { get; set; }

        public bool IsActive { get; set; } = true;

        public int LeadTimeDays { get; set; } = 7;
    }

    public enum MaintenanceType
    {
        Preventative = 0,
        Corrective = 1,
        Predictive = 2,
        Emergency = 3,
        Inspection = 4,
        Calibration = 5,
        Upgrade = 6,
        Other = 7
    }

    public enum MaintenanceStatus
    {
        Scheduled = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3,
        Overdue = 4,
        OnHold = 5
    }

    public enum MaintenancePriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    public enum RecurrenceType
    {
        Daily = 0,
        Weekly = 1,
        BiWeekly = 2,
        Monthly = 3,
        Quarterly = 4,
        SemiAnnually = 5,
        Annually = 6,
        Custom = 7
    }

    public enum WorkOrderApprovalStatus
    {
        NotRequired = 0,
        PendingApproval = 1,
        Approved = 2,
        Rejected = 3,
        Cancelled = 4
    }

    // ADR-012 v0.2 — Unified Work Orders top-level discriminator.
    //
    // Sequence is stable; never renumber. The gap at 1 is intentional: v0.1
    // briefly defined Production=1 here but v0.2 lifted Production out to
    // its own sibling table (ProductionOrder) per ADR-012 v0.2 §"Six
    // structural commitments" — production's event cadence, state machine,
    // and KPI surface (OEE) don't fit a maintenance-shaped header. Leaving
    // the gap avoids analytics churn for any tooling that ever inspected
    // historical enum values.
    //
    // Adding a new classification appends to the end (next free = 6) and
    // ships a follow-up migration adding any satellite table per the
    // four-config-table pattern (FieldVisibility / StatusProfile /
    // Approval / NumberSequence).
    public enum WorkOrderClassification
    {
        // PM, Corrective, Predictive, Emergency, Inspection, Calibration,
        // Upgrade. Sub-type lives in MaintenanceEvent.Type. No satellite —
        // the existing header fields ARE the maintenance shape. This is the
        // default for every existing row backfilled by the PR #119.1
        // migration.
        Maintenance = 0,

        // (gap — formerly Production in v0.1; now lives in the sibling
        //  ProductionOrder table per ADR-012 v0.2. Do NOT reuse value 1.)

        // NCR, deviation, CAPA, audit finding, customer complaint.
        // Per-classification fields in QualityWorkOrderDetails (PR #119.9).
        Quality = 2,

        // ECO, MOC, design change, BOM revision, procedure update.
        // Per-classification fields in EngineeringWorkOrderDetails
        // (PR #119.10). Carries revision tracking + PSSR sign-off chain.
        Engineering = 3,

        // Safety inspection, hazard report, near-miss, incident, JSA.
        // Per-classification fields in HseWorkOrderDetails (PR #119.11)
        // including OSHA 300/301 + ANSI Z10 severity × likelihood.
        HSE = 4,

        // Capital project work — AFE-tracked cost rollup that reclassifies
        // to the fixed-asset ledger on substantial completion. Per ASC
        // 360-10 + ASC 835-20. Per-classification fields in
        // CipWorkOrderDetails (PR #119.8). Renamed from "Project" in v0.2
        // to match the precise accounting term; tenant-level customizable
        // via WorkOrderFieldVisibility config table if a customer needs a
        // non-CIP "Project" label.
        CIP = 5,
    }
}
