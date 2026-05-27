// Sprint 14.1 PR-1 (2026-05-26) — ProductionMaterialStructure.
//
// PER-PRODUCTION-ORDER FROZEN BOM SNAPSHOT.
//
// THE PROBLEM: today ProductionOrder.MaterialStructureId points at the LIVE
// MaterialStructure entity (the engineering-controlled BOM). When that BOM
// gets revised AFTER a PO is released — a new line added, a component
// substituted, a quantity changed, a revision bumped — every in-flight
// PO that referenced it sees the change too. That's the SAP-MM trap:
// engineering controls the BOM, but production executes against a frozen
// copy as of release. The two views must diverge cleanly.
//
// THE FIX: at PO release, snapshot every MaterialStructureLine into its
// own ProductionMaterialStructure row. The snapshot survives subsequent
// engineering changes to the source. Cost rollups, kitting picks, MES
// material-issue, AS9100 §8.3 traceability, and ECR-ECO impact analysis
// all read from the SNAPSHOT, not from the source.
//
// WHAT THIS ENTITY SNAPSHOTS:
//   - Component identity: ChildItemId (RESTRICT — orphaning a snapshot
//     would erase the audit trail). Plus defensive denormalized copies
//     (ChildPartNumber, ChildRevision) that survive Item rename + revision
//     bumps post-release.
//   - Quantity per parent + UoM + scrap percent: frozen at release so
//     planned-vs-actual cost variance is deterministic.
//   - Issue method (Pull / Push / Backflush): frozen at release so MES
//     material-issue cadence is deterministic.
//   - Line kind (Component / CoProduct / ByProduct / Scrap / Packaging /
//     Intermediate): mirrors MaterialStructureLine.LineKind. Frozen.
//   - FrozenStandardCost (Item.StandardCost at snapshot) + FrozenExtendedCost
//     (Quantity * cost + scrap inflation) so the cost engine (Sprint 14.4)
//     can compute planned vs actual without re-resolving stale fields.
//   - Phantom flag — frozen so MRP explosion behavior is deterministic
//     post-release.
//   - SourceMaterialStructureLineId — FK to the source line for traceability.
//     SET NULL on source delete (snapshot survives if engineering retires
//     the BOM line).
//   - ChildItemFingerprintHash — SHA-256 of the relevant frozen Item fields
//     at snapshot time. Lets cost engine + AS9100 audit detect "this item
//     changed materially after PO release; flag for review."
//
// WHAT IT DOES NOT DO IN THIS PR:
//   - Snapshot the routing operations (separate cascade — Sprint 14.x).
//   - Snapshot ItemStandardCostElement 8-element split per component (the
//     scalar FrozenStandardCost handles the read path for now; element
//     split snapshot lands when cost engine wires up in Sprint 14.4).
//   - Snapshot active ItemSourcingRule rows per component (lands with
//     Sprint 14.x when MRP needs the deterministic vendor freeze).
//   - Snapshot CustomerItemXref active rows (lands with B8 PO Cockpit when
//     customer-side rendering needs the frozen customer-PN).
//
// LOCKS APPLIED PROPHYLACTICALLY (per B6 hard-locks):
//   - Tenant trio (CompanyId on entity, denormalized from ProductionOrder).
//   - Enum HasDefaultValue (BomIssueMethod.Pull) wired in AppDbContext so
//     legacy backfill + raw-SQL inserts land on industry-default Pull
//     semantics. Codex caught 2 P1s on PR #363 for exactly this — DO NOT
//     REPEAT. See [[feedback_b6_enum_defaults_must_match_model]].
//   - NULL-safe partial UNIQUE on (ProductionOrderId, Sequence) — captured
//     snapshots can't have duplicate sequence numbers within a PO.
//   - RowVersion concurrency token — snapshot capture can race with cost
//     engine reads.
//   - Realistic-mfg-only fixtures in tests (per HARD LOCK no-fake-data).
//   - All nullable FK columns get nav properties + HasOne config (CHERRY025
//     completeness).
//
// REFERENCES:
//   - reference_master_plan_audit_2026_05_24.md — 16-Wave spine; this is
//     Sprint 14.1 (per-PO snapshot tables) on the post-B6 cascade.
//   - feedback_b6_go_big_2026_05_26.md — drives GO BIG architecture choice
//     (frozen FK + denorm copy + fingerprint hash + service-only writes,
//     not a CRUD shortcut).
//   - feedback_b6_enum_defaults_must_match_model.md — enum DB default lock.
//   - feedback_lock16_e2e_after_every_addition.md — Lock 16 E2E playbook
//     after this lands on dev preview.
//   - Models/Production/MaterialStructure.cs — the SOURCE entity this
//     entity freezes.
//   - Models/Production/MaterialStructureLine.cs — LineKind enum reused
//     directly (no parallel enum — single source of truth).

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Revisions;

namespace Abs.FixedAssets.Models.Production
{
    /// <summary>
    /// How a component is issued from inventory to a production order.
    /// SAP MM issue type / Oracle WMS pick mode / D365 flushing principle.
    /// </summary>
    public enum BomIssueMethod
    {
        /// <summary>
        /// Pull — issue from stock when the operation actually needs the
        /// component (just-in-time per-operation pull). Industry default
        /// for discrete + project mfg.
        /// </summary>
        Pull = 0,

        /// <summary>
        /// Push — issue at PO release / kit creation (entire BOM hits the
        /// floor as a kit). Common for short-cycle assembly + clean-room
        /// kitting where component picking off the line is expensive.
        /// </summary>
        Push = 1,

        /// <summary>
        /// Backflush — issue automatically when the parent PO reports
        /// completion (qty consumed = QtyCompleted * QtyPer). Common for
        /// high-volume repetitive mfg where per-operation picks would add
        /// expensive paperwork.
        /// </summary>
        Backflush = 2,
    }

    // B8 PR-PRO-2 enums (2026-05-27) — per PRO Cockpit spec §2.

    public enum BomLineStatus
    {
        NotRequiredYet = 0, Required = 1, Short = 2, Reserved = 3,
        Picked = 4, Staged = 5, PartiallyIssued = 6, Issued = 7,
        OverIssued = 8, Backflushed = 9, Consumed = 10, Returned = 11,
        Transferred = 12, Substituted = 13, Scrapped = 14, Cancelled = 15, Closed = 16,
    }

    public enum SupplyType
    {
        Pull = 0, Push = 1, Backflush = 2, Bulk = 3, Supplier = 4, Floorstock = 5,
    }

    public enum IssueTiming
    {
        AtOperationStart = 0, AtRelease = 1, AtOperationComplete = 2, AtFinalCompletion = 3,
    }

    public enum CostBucket
    {
        Material = 0, Subcontract = 1, Tooling = 2, Burden = 3,
    }

    /// <summary>
    /// Per-ProductionOrder frozen BOM line. Captured at PO release by
    /// <c>IPoSnapshotService.CaptureAsync</c>. Survives subsequent engineering
    /// changes to the source <see cref="MaterialStructure"/> /
    /// <see cref="MaterialStructureLine"/>.
    /// </summary>
    [Table("ProductionMaterialStructures")]
    public class ProductionMaterialStructure
    {
        public int Id { get; set; }

        // ===== Identity + parent linkage ===================================

        /// <summary>
        /// FK to the <see cref="ProductionOrder"/> this snapshot row belongs to.
        /// CASCADE on PO delete — the snapshot doesn't outlive its order.
        /// </summary>
        [Required]
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        /// <summary>
        /// FK back to the source <see cref="MaterialStructureLine"/> at snapshot
        /// time. SET NULL on source delete — the snapshot survives if
        /// engineering retires the source line (we still need the audit trail).
        /// </summary>
        public int? SourceMaterialStructureLineId { get; set; }
        public MaterialStructureLine? SourceMaterialStructureLine { get; set; }

        /// <summary>
        /// FK to the source <see cref="MaterialStructure"/> at snapshot. Allows
        /// "show me every snapshot that froze BOM #N" queries from the source
        /// side without joining through ProductionOrder. SET NULL on source
        /// delete.
        /// </summary>
        public int? SourceMaterialStructureId { get; set; }
        public MaterialStructure? SourceMaterialStructure { get; set; }

        // ===== Tenant trio (denormalized from ProductionOrder for fast
        // tenant filtering, mirrors Sprint 13.5 PR #5c.2 pattern) =============

        [Required]
        public int CompanyId { get; set; }

        public int? LocationId { get; set; }

        // ===== Frozen identity of the component ============================

        /// <summary>
        /// FK to the child <see cref="Item"/> consumed by this snapshot row.
        /// RESTRICT on Item delete — orphaning a snapshot would erase the
        /// component audit trail. (If an Item must be deleted, every
        /// ProductionOrder that snapshotted it must be closed first.)
        /// </summary>
        [Required]
        public int ChildItemId { get; set; }
        public Item? ChildItem { get; set; }

        /// <summary>
        /// Defensive denormalized copy of <c>Item.PartNumber</c> at snapshot
        /// time. Survives Item rename — the snapshot, packing slip,
        /// receiving paperwork, and AS9100 FAI report all keep showing the
        /// part number that was on the BOM the day of release.
        /// </summary>
        [Required]
        [StringLength(50)]
        [Display(Name = "Child Part Number (frozen)")]
        public string ChildPartNumber { get; set; } = string.Empty;

        /// <summary>
        /// Defensive denormalized copy of <c>Item.Revision</c> (or
        /// <c>Item.CurrentReleasedRevision.RevisionCode</c>) at snapshot time.
        /// Survives subsequent engineering revisions.
        /// </summary>
        [StringLength(16)]
        [Display(Name = "Child Revision (frozen)")]
        public string? ChildRevision { get; set; }

        /// <summary>
        /// FK to the specific <see cref="ItemRevision"/> frozen at release.
        /// When non-null, gives the cost engine + ECR/ECO impact analysis a
        /// definitive answer to "which revision was on the floor when this
        /// PO ran?". SET NULL on revision delete (snapshot survives revision
        /// archival).
        /// </summary>
        public int? ChildItemRevisionId { get; set; }
        public ItemRevision? ChildItemRevision { get; set; }

        /// <summary>
        /// SHA-256 of the relevant frozen Item fields at snapshot time
        /// (PartNumber + Description + Revision + StandardCost + UOM +
        /// 18 PR-FS-7 expansion fields). Lets the cost engine + AS9100
        /// audit detect "this Item changed materially after PO release;
        /// flag for review." Lower-case hex, 64 chars.
        /// </summary>
        [StringLength(64)]
        [Display(Name = "Child Item Fingerprint")]
        public string? ChildItemFingerprintHash { get; set; }

        // ===== Frozen quantity + UoM + scrap ===============================

        /// <summary>
        /// Position in the snapshot (1-based ordering). Mirrors the source
        /// <c>MaterialStructureLine.Sequence</c> at snapshot time; deterministic
        /// across re-captures of the same PO.
        /// </summary>
        [Required]
        public int Sequence { get; set; } = 10;

        /// <summary>
        /// Quantity of <see cref="ChildItem"/> consumed per ONE unit of the
        /// parent. Frozen at release.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Quantity Per Parent (frozen)")]
        public decimal QuantityPer { get; set; }

        /// <summary>
        /// Frozen unit-of-measure. Free-text 16 chars to match the existing
        /// shape on MaterialStructureLine (consolidation to UomId comes with
        /// the broader Sprint 14.x UoM cleanup).
        /// </summary>
        [StringLength(16)]
        [Display(Name = "UoM (frozen)")]
        public string? Uom { get; set; }

        /// <summary>
        /// Expected scrap percentage frozen at release. Drives planner-side
        /// inflation: planned consumed = QuantityPer * (1 + ScrapPercent/100).
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        [Display(Name = "Scrap % (frozen)")]
        public decimal? ScrapPercent { get; set; }

        /// <summary>
        /// For recipe-shaped parents — which phase this line applies to. Null
        /// = applies across phases. Mirrors <c>MaterialStructureLine.PhaseSequence</c>.
        /// </summary>
        [Display(Name = "Phase Sequence (frozen)")]
        public int? PhaseSequence { get; set; }

        // ===== Frozen behavioral flags =====================================

        /// <summary>
        /// What kind of line this is — Component / CoProduct / ByProduct /
        /// Scrap / Packaging / Intermediate. Reuses the source enum directly
        /// (single source of truth).
        /// </summary>
        [Required]
        [Display(Name = "Line Kind (frozen)")]
        public LineKind LineKind { get; set; } = LineKind.Component;

        /// <summary>
        /// How this component issues from inventory (Pull / Push / Backflush).
        /// Frozen at release so MES material-issue cadence is deterministic
        /// post-release. Pull is the industry default.
        /// </summary>
        [Required]
        [Display(Name = "Issue Method (frozen)")]
        public BomIssueMethod IssueMethod { get; set; } = BomIssueMethod.Pull;

        /// <summary>
        /// Phantom flag frozen from <c>Item.IsPhantom</c> at snapshot. Phantom
        /// lines are exploded through MRP / never stocked. Frozen so MRP
        /// explosion behavior is deterministic post-release.
        /// </summary>
        [Display(Name = "Is Phantom (frozen)")]
        public bool IsPhantom { get; set; } = false;

        // ===== Frozen cost =================================================

        /// <summary>
        /// Frozen <c>Item.StandardCost</c> at snapshot. Drives planned-vs-actual
        /// cost variance for the parent PO. Null when the Item had no
        /// standard cost set at release.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Frozen Standard Cost (per UoM)")]
        public decimal? FrozenStandardCost { get; set; }

        /// <summary>
        /// FrozenExtendedCost = QuantityPer * (1 + ScrapPercent/100) *
        /// FrozenStandardCost. Computed at snapshot time so cost engine reads
        /// are O(1). Null when FrozenStandardCost is null.
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Frozen Extended Cost")]
        public decimal? FrozenExtendedCost { get; set; }

        // ===== Frozen passthrough ==========================================

        /// <summary>
        /// jsonb passthrough of <c>MaterialStructureLine.TypeSpecificProperties</c>
        /// at snapshot time (reference designator, alternate group, recipe
        /// scaling override, etc.). Free-form preserves vertical extensions.
        /// </summary>
        [Column(TypeName = "jsonb")]
        public string? TypeSpecificProperties { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // ===== B8 PR-PRO-2 — Execution quantities (10 cols) ================
        [Column(TypeName = "decimal(18,4)")] public decimal IssuedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal PickedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal StagedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal ReservedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal ConsumedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal ReturnedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal ScrappedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal ShortQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal OverIssuedQuantity { get; set; }
        [Column(TypeName = "decimal(18,4)")] public decimal TransferableQuantity { get; set; }

        // ===== BOM line status + flags ======================================
        public BomLineStatus LineStatus { get; set; } = BomLineStatus.NotRequiredYet;
        public bool IsCritical { get; set; }
        public bool IsLongLead { get; set; }
        public bool IsCustomerSupplied { get; set; }
        public bool IsConsigned { get; set; }
        public bool IsHazardous { get; set; }
        public bool IsLotControlled { get; set; }
        public bool IsSerialControlled { get; set; }
        public bool IsHeatCertRequired { get; set; }
        public bool IsShelfLifeControlled { get; set; }

        // ===== Supply + timing + op linkage =================================
        public SupplyType SupplyType { get; set; } = SupplyType.Pull;
        public IssueTiming IssueTiming { get; set; } = IssueTiming.AtOperationStart;
        public int? ConsumingOperationSequence { get; set; }
        public int? BackflushOperationSequence { get; set; }
        [StringLength(32)] public string? KitGroup { get; set; }

        // ===== Lot/serial tracking ==========================================
        [StringLength(50)] public string? ReservedLotNumber { get; set; }
        [StringLength(50)] public string? IssuedLotNumber { get; set; }
        [StringLength(50)] public string? IssuedSerialNumber { get; set; }
        [StringLength(50)] public string? HeatNumber { get; set; }
        [StringLength(50)] public string? VendorLot { get; set; }
        [StringLength(50)] public string? CertificateNumber { get; set; }

        // ===== Substitution =================================================
        public bool SubstituteAllowed { get; set; }
        public int? AlternateBomLineId { get; set; }
        public ProductionMaterialStructure? AlternateBomLine { get; set; }
        [StringLength(200)] public string? SubstituteReason { get; set; }
        [StringLength(50)] public string? SubstitutionAuthReference { get; set; }

        // ===== Per-line cost ================================================
        public CostBucket CostBucket { get; set; } = CostBucket.Material;
        public bool CustomerChargeable { get; set; }

        // ===== Audit + concurrency =========================================

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        [Display(Name = "Captured By")]
        public string? CapturedBy { get; set; }

        // Concurrency token — mapped to Postgres' built-in `xmin` system
        // column via MapXminRowVersion in AppDbContext (project convention,
        // see Data/XminRowVersionExtensions.cs). No real DDL column needed
        // for this — xmin exists on every PG row by default.
        //
        // PR-14.1-1.1 hotfix: switched from a bytea + IsRowVersion() column
        // (which doesn't get auto-populated by Postgres and threw
        // 23502 NOT NULL violations on every INSERT) to the xmin pattern
        // used by 17 other entities in the codebase.
        public byte[]? RowVersion { get; set; }
    }
}
