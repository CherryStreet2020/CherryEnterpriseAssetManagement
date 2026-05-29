using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Projects;
using Abs.FixedAssets.Models.Revisions;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.12 — ProductionOrder header.
    //
    // Sibling to WorkOrder (NOT a subtype). Production has a different
    // status machine, OEE / yield concerns, event cadence, and audit
    // surface than maintenance / quality / engineering work. Mixing them
    // on one header is the SAP-PP02 trap — visible at the AUFK / AFKO
    // split for a reason.
    //
    // The discriminator is `Type` (ProductionType). Per-type fields land
    // on a 1:0..1 satellite (UNIQUE on ProductionOrderId, ON DELETE
    // CASCADE), exactly the same shape as the ADR-012 v0.2 Phase D
    // classification satellites on WorkOrder.
    //
    // What this PR ships:
    //   - This header
    //   - ProductionType enum
    //   - ProductionJobShopDetail satellite (cut-list ref, nest plan
    //     ref, outside-process flags) — unlocks the working CIP
    //     machine-shop path end-to-end
    //   - WorkOrderOperation extensions: IsExternal, VendorId,
    //     AutoGeneratePR (auto-fire-on-release wiring lands later)
    //
    // What it explicitly does NOT ship (PRs #119.13 and #119.14):
    //   - MaterialStructure (Bom / Recipe) polymorphic pair
    //   - Nest, CutListLine, NestWorkOrderAllocation entities
    //   - ProductionProcessDetail satellite (recipe, batch, co-/by-
    //     products, phase timing)
    //   - RegulatoryProfile config (FDA, AS9100, REACH gates)
    //
    // Reference: ADR-013 §"Phase E ship plan."
    [Table("ProductionOrders")]
    public class ProductionOrder
    {
        public int Id { get; set; }

        // Sprint 13.5 PR #5c.2 — Direct tenant scoping (defensive denormalization
        // from Location.CompanyId for fast tenant filtering + leak-prevention).
        // Backfilled in migration 20260524120000_TenantScopingHardeningPr5c2:
        //   primary path: po.LocationId → Location.CompanyId
        //   fallback:     po.CustomerProjectId → CustomerProject.CompanyId
        // Grace-period CHECK >= 0 — orphan unsited/unprojected orders stay at 0
        // until PR #5c.4 seeder fills them and a follow-up tightens to > 0.
        // UNIQUE (CompanyId, OrderNumber) replaces the global OrderNumber UNIQUE
        // (P0 cross-tenant leak — every tenant collided on each other's numbers).
        public int CompanyId { get; set; }

        // Human-facing identifier (e.g., "PRO-2026-00042"). Generated via
        // NumberSequence (SAP NRIV pattern, PR #119.5) once a number-
        // sequence row for ProductionOrder is seeded — current MVP allows
        // the controller to assign on create. Default prefix "PRO-" is
        // locked per ADR-025 sibling rename (2026-05-20) — avoids the
        // PurchaseOrder "PO-" collision. Per-tenant overrides expected:
        // EVS/ABS metal-fab will likely use "JO-" (Job Order), FSC food
        // will likely use "BO-" (Batch Order). The override is a single
        // row in NumberSequence with the tenant-scoped Prefix value.
        [Required]
        [StringLength(32)]
        [Display(Name = "Production Order #")]
        public string OrderNumber { get; set; } = string.Empty;

        // Production work-method discriminator. Drives which satellite
        // table holds the per-type fields and which status profile
        // applies at runtime.
        public ProductionType Type { get; set; } = ProductionType.JobShop;

        // Status machine — see ProductionOrderStatus comments.
        public ProductionOrderStatus Status { get; set; } = ProductionOrderStatus.Planned;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        // The principal-material item being produced. ON DELETE RESTRICT
        // — refusing to delete an item that still has open production
        // orders is the safer default than orphaning history.
        public int? ItemId { get; set; }
        public Item? Item { get; set; }

        // Plant / facility where the order runs. Locations are already
        // FK-from-elsewhere, ON DELETE RESTRICT for the same reason.
        public int? LocationId { get; set; }
        public Location? Location { get; set; }

        // Customer for make-to-order / ETO / job-shop work. Most repetitive-
        // discrete and process-batch orders are make-to-stock (no customer).
        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        // Target quantity to produce. Decimal because process / batch
        // orders can run in fractional kg / L.
        [Display(Name = "Quantity Ordered")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityOrdered { get; set; } = 0;

        [Display(Name = "Quantity Completed")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityCompleted { get; set; } = 0;

        [Display(Name = "Quantity Scrapped")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityScrapped { get; set; } = 0;

        // B8 PR-PO-1 (2026-05-27) — QuantityReleased + QuantityRework.
        // QuantityReleased = how many units were formally released to the
        // floor (may differ from QuantityOrdered if partial release is used).
        // QuantityRework = how many units were sent through rework operations.
        [Display(Name = "Quantity Released")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReleased { get; set; } = 0;

        [Display(Name = "Quantity Rework")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityRework { get; set; } = 0;

        // Unit of measure for the produced item. Free-text 16 chars to
        // match how Item already represents UoM elsewhere in the model —
        // we'll consolidate to a UoM table in a later sprint.
        [StringLength(16)]
        public string? Uom { get; set; }

        // ----- B8 PR-PO-1 — header field expansion per PO Cockpit spec §1 -----
        //
        // Adds: PlannerUserId + SupervisorUserId (FK to Users), HoldReason
        // enum + notes, 4 freeze flags, PromiseDate, LotSerialRequirement,
        // WorkInstructionsRevision, DrawingRevision. Together with the
        // 2 new ProductionOrderStatus states (Firmed + Closed) and 7 new
        // ProductionType variants, this closes every header-level gap from
        // the spec. Config ID / Option Set deferred per spec note.
        //
        // Matches SAP S/4HANA AUFK / Oracle Fusion WIE_WORK_ORDERS /
        // D365 SCM ProdTable / Infor CSI ProductionOrder / Epicor Kinetic
        // JobHead field sets — but with cleaner nullable semantics.

        // Planner responsible for scheduling + material planning. FK to Users.
        // NULL = unassigned (valid for draft/Planned orders).
        [Display(Name = "Planner")]
        public int? PlannerUserId { get; set; }
        public User? Planner { get; set; }

        // Floor supervisor responsible for execution + labor allocation.
        [Display(Name = "Supervisor")]
        public int? SupervisorUserId { get; set; }
        public User? Supervisor { get; set; }

        // Hold reason — populated when Status transitions to OnHold.
        // NULL when not on hold. The HoldReason enum categorizes the
        // constraint; HoldReasonNotes captures the free-text explanation.
        [Display(Name = "Hold Reason")]
        public HoldReason? HoldReason { get; set; }

        [Display(Name = "Hold Reason Notes")]
        [StringLength(1000)]
        public string? HoldReasonNotes { get; set; }

        // Freeze flags — when TRUE, the corresponding dimension is locked
        // against edits even by planners. Only admin/supervisor override
        // can clear a freeze. Matches SAP PP order freeze semantics.
        [Display(Name = "Freeze BOM")]
        public bool FreezeBom { get; set; } = false;

        [Display(Name = "Freeze Routing")]
        public bool FreezeRouting { get; set; } = false;

        [Display(Name = "Freeze Schedule")]
        public bool FreezeSchedule { get; set; } = false;

        [Display(Name = "Freeze Cost")]
        public bool FreezeCost { get; set; } = false;

        // Promise date = customer-facing commitment date (may differ from
        // ScheduledEnd which is the internal production target).
        [Display(Name = "Promise Date")]
        public DateTime? PromiseDate { get; set; }

        // Lot/serial tracking requirement for the finished good produced.
        // Drives whether the completion transaction demands lot assignment,
        // serial assignment, both, or neither.
        [Display(Name = "Lot/Serial Requirement")]
        public LotSerialRequirementType LotSerialRequirement { get; set; }
            = LotSerialRequirementType.None;

        // Work instructions + drawing revision currently in force for this
        // order. Populated at release from the Item's current revision or
        // from the linked DocumentVersion via IDocumentService. Updated
        // if an ECO mid-flight changes the revision (with audit trail).
        [Display(Name = "Work Instructions Revision")]
        [StringLength(32)]
        public string? WorkInstructionsRevision { get; set; }

        [Display(Name = "Drawing Revision")]
        [StringLength(32)]
        public string? DrawingRevision { get; set; }

        // ----- Sprint 12.8 PR #1 — cost-accumulation columns -----
        //
        // Five nullable decimal(18,2) cost buckets per ADR-028 §3.
        //
        // NULL semantics: NULL = "not yet populated by the cost engine"
        // (different from 0, which means "engine ran and found zero").
        // Sprint 0.5 + Sprint 14 will wire the engines that populate
        // these on order completion (material issue × unit cost,
        // LaborEntry.DurationMins × wage rate, OH absorption, vendor
        // invoice for subcontract). For the Sprint 12.8 demo, the ABS
        // scenario seeder stamps pre-computed values for the hero
        // scenario (Rolls-Royce Trent XWB engine bracket assembly).
        //
        // Why on the header: cost rollup at the ProductionOrder level
        // is what the CFO and the cost accountant care about. Per-
        // operation cost lives on ProductionOperation when that's
        // wired (Sprint 14). Header totals are the read path for
        // dashboards + walkthroughs + the eventual child→parent
        // cost-rollup service.
        //
        // CHECK constraints: deferred until the cost engine lands.
        // Stamping negative values is invalid but the seeder won't
        // try; tightening later is non-breaking.

        [Display(Name = "Material Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaterialCost { get; set; }

        [Display(Name = "Labor Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? LaborCost { get; set; }

        [Display(Name = "Overhead Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? OverheadCost { get; set; }

        [Display(Name = "Subcontract Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? SubcontractCost { get; set; }

        // Sum of the four buckets above. Held explicitly (not computed)
        // so it can be read in a single column scan and so the rollup
        // service can stamp it after summing child orders, not just
        // this order's own four buckets.
        [Display(Name = "Actual Cost")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualCost { get; set; }

        [Display(Name = "Scheduled Start")]
        public DateTime? ScheduledStart { get; set; }

        [Display(Name = "Scheduled End")]
        public DateTime? ScheduledEnd { get; set; }

        [Display(Name = "Actual Start")]
        public DateTime? ActualStart { get; set; }

        [Display(Name = "Actual End")]
        public DateTime? ActualEnd { get; set; }

        // Priority — int rather than enum so the existing PriorityLookup
        // pattern can be wired in later without an enum migration.
        public int Priority { get; set; } = 50;

        // Revision-chain self-FK, mirrors WorkOrder revision pattern from
        // ADR-012 v0.2 / PR #119.6. SET NULL on master delete.
        public int? MasterProductionOrderId { get; set; }
        public ProductionOrder? MasterProductionOrder { get; set; }
        public int Revision { get; set; } = 0;

        // ----- Sprint 12.8 PR #1 — multi-level BOM parent-child self-FK -----
        //
        // ADR-028: parent-child semantics for executing a multi-level
        // BOM as nested ProductionOrders. INTENTIONALLY SEPARATE from
        // MasterProductionOrderId above — that one is the revision
        // chain (PRO v1 → v2 → v3 of the SAME order), this one is
        // the assembly hierarchy (parent assembly PRO ↔ sub-assembly
        // child PROs).
        //
        // Conflating these is the SAP PP02 trap that the audit doc
        // warns about: revision and assembly hierarchy have different
        // cascade semantics, different cost-rollup direction, and
        // different lifecycle.
        //
        // FK behavior: SET NULL on parent delete (the row above does
        // the same for MasterProductionOrderId). A deleted parent
        // leaves orphaned children that admin tooling can re-parent
        // or close out — safer than CASCADE on accumulated cost
        // history.
        //
        // CHECK constraint enforced at the DB layer: a row cannot be
        // its own parent. See AppDbContext.OnModelCreating.
        //
        // For Sprint 12.8 demo: the ABS scenario seeder INSERTs 10
        // ProductionOrders, sets the 9 children's ParentProductionOrderId
        // to the parent's Id, and CustomerProjectId on all 10 to the
        // same project for the visible tree view.
        //
        // For Sprint 0.5 + Sprint 14 (post-demo): the future BOM-
        // explosion service will read MaterialStructure.Lines, recursively
        // walk into each line's Item.MaterialStructures, and INSERT
        // child ProductionOrders with this FK populated on parent
        // release. The seeder hand-crafts the same shape the engine
        // will eventually produce.
        public int? ParentProductionOrderId { get; set; }
        public ProductionOrder? Parent { get; set; }

        // Inverse nav. Sized via EF Core convention. Eagerly loaded
        // only when the caller explicitly Includes it — most queries
        // do not need it.
        public ICollection<ProductionOrder>? Children { get; set; }

        // ADR-013 / PR #119.14 — MaterialStructure FK.
        // Which Bom or Recipe is this order producing? SET NULL on
        // structure delete — order history survives administrative
        // structure cleanup (rare; usually Status -> Retired).
        //
        // **IMPORTANT — POST Sprint 14.1 PR-1**: this FK points at the
        // LIVE source MaterialStructure. The FROZEN-AT-RELEASE BOM lines
        // live on ProductionMaterialStructures via the MaterialSnapshot
        // nav below. Engineering changes to MaterialStructureLines
        // post-release do NOT flow into in-flight PROs that have already
        // captured a snapshot. The cost engine + MES material-issue +
        // AS9100 §8.3 traceability + ECR-ECO impact analysis read from
        // the SNAPSHOT, not from this FK. Reads that want the live
        // engineering view (e.g., "what does the BOM say today?") follow
        // this FK; reads that want production reality (e.g., "what was
        // on the floor when we ran this PO?") follow MaterialSnapshot.
        public int? MaterialStructureId { get; set; }
        public MaterialStructure? MaterialStructure { get; set; }

        // ----- Sprint 14.1 PR-1 — per-PO snapshot freeze -----
        //
        // The four columns below + the MaterialSnapshot nav collection
        // capture the "frozen at release" state of the Item revision and
        // the BOM lines this PRO is executing against. Populated by
        // IPoSnapshotService.CaptureAsync at release; immutable thereafter
        // unless an admin explicitly clears the snapshot (recovery only).
        //
        // See: Models/Production/ProductionMaterialStructure.cs
        //      Services/Production/IPoSnapshotService.cs (Sprint 14.1 PR-1)
        //      memory: reference_master_plan_audit_2026_05_24.md Wave 14.1

        /// <summary>
        /// Frozen FK to the <see cref="ItemRevision"/> in force when the
        /// PRO was released. Captured by IPoSnapshotService.CaptureAsync from
        /// <c>Item.CurrentReleasedRevisionId</c>. SET NULL on revision delete
        /// so PRO history survives revision archival.
        /// </summary>
        [Display(Name = "Source Item Revision (frozen)")]
        public int? SourceItemRevisionId { get; set; }
        public ItemRevision? SourceItemRevision { get; set; }

        /// <summary>
        /// Defensive denormalized copy of <c>MaterialStructure.Revision</c>
        /// at release. Survives subsequent BOM revisions.
        /// </summary>
        [StringLength(16)]
        [Display(Name = "Source MS Revision (frozen)")]
        public string? SourceMaterialStructureRevision { get; set; }

        /// <summary>
        /// When the snapshot was captured (UTC). Null until release-time
        /// CaptureAsync runs. Reads use this as the "is this PRO snapshotted
        /// yet?" sentinel.
        /// </summary>
        [Display(Name = "Snapshot Captured At")]
        public DateTime? SnapshotCapturedAtUtc { get; set; }

        /// <summary>
        /// Who triggered the snapshot capture (release operator, batch job,
        /// admin recovery). Max 100 chars.
        /// </summary>
        [StringLength(100)]
        [Display(Name = "Snapshot Captured By")]
        public string? SnapshotCapturedBy { get; set; }

        /// <summary>
        /// The frozen BOM lines for this PRO. Populated by
        /// IPoSnapshotService.CaptureAsync at release. Read by the cost
        /// engine (Sprint 14.4), MES material-issue (B8 PO Cockpit), and
        /// AS9100 §8.3 traceability + ECR-ECO impact analysis.
        /// </summary>
        public ICollection<ProductionMaterialStructure>? MaterialSnapshot { get; set; }

        // Sprint 13.5 PR #1 / ADR-026 — Customer-project foundation.
        //
        // Nullable link to a CustomerProject. The vast majority of
        // ProductionOrders run with no project at all (pure job-shop
        // mode) — this column stays NULL for those. When set, the
        // project's cost rollup and revenue recognition rules apply.
        //
        // FK uses SET NULL on project delete so production-order history
        // survives a project being archived. Indexed via partial index
        // (WHERE CustomerProjectId IS NOT NULL) in the migration to keep
        // job-shop-mode lookups cheap.
        //
        // See research_project_job_hierarchy_patterns.md §4 for the
        // dominant-pattern derivation (matches SAP S/4HANA PS, Oracle
        // Project Mfg, D365 Project Ops, IFS Cloud, Epicor Kinetic,
        // Acumatica, Infor LN — all of which model the Project link as
        // a nullable header FK on the manufacturing-order side).
        public int? CustomerProjectId { get; set; }
        public CustomerProject? CustomerProject { get; set; }

        // Optional WBS-phase pegging within the project. Customers who
        // do not care about WBS leave this NULL. Independent of
        // CustomerProjectId so a job can be project-linked without
        // committing to a phase.
        public int? ProjectPhaseId { get; set; }
        public ProjectPhase? ProjectPhase { get; set; }

        // D365-style posting-mode override per ProductionOrder.
        //   FinishedItem — produce normally, consume finished good into
        //                  the project on completion.
        //   Consumed     — post costs directly to the project as they
        //                  hit the job (project becomes the cost object).
        // NULL when CustomerProjectId is also NULL; required to be set
        // (by ICustomerProjectService) when linking a job to a project.
        // CHECK constraint enforced in the migration.
        public ProjectPostingMode? ProjectPostingMode { get; set; }

        // ----- Theme B7 Wave A PR-2 — master-optional (PoFirst) identity -----
        //
        // PO-as-Standard / ETO: a PoFirst order builds from the PO itself; the
        // Item Master is OPTIONAL at release (ItemId == null) and crystallizes
        // at ship into CrystallizedItemId (Wave B). The order's frozen
        // BOM + Routing IS the standard during the build. This is the claim no
        // incumbent (SAP/Oracle/Epicor/Infor/Plex/IFS) can make — they force a
        // material master before you can release.
        //
        // In lieu of an Item Master, a PoFirst order carries its own as-planned
        // identity: drawing number + rev are MANDATORY at release (AS9100
        // §8.5.2 — production must run against a defined, revision-controlled
        // configuration), part number + description are optional descriptors.
        // See docs/research/po-as-standard-make-or-buy-dean-research.md §2/§6
        // and docs/research/b7-cascade-design.md (Wave A PR-2).
        //
        // NOTE distinct from DrawingRevision/WorkInstructionsRevision above:
        // those are the IN-FORCE revisions populated at release from the Item's
        // current revision or a linked DocumentVersion (StandardFirst path).
        // The AsPlanned* fields are the order's OWN identity when there is no
        // Item to inherit from (PoFirst path).

        /// <summary>
        /// B7 — true when this order builds master-optional (PO-as-Standard / ETO).
        /// A PoFirst order may carry <see cref="ItemId"/> == null at release and
        /// crystallizes its master at ship (see <see cref="CrystallizedItemId"/>).
        /// Defaults false (classic StandardFirst — master required).
        /// </summary>
        [Display(Name = "Is PO-First (master-optional)")]
        public bool IsPoFirst { get; set; } = false;

        /// <summary>
        /// B7 — as-planned part number the PoFirst order carries in lieu of an
        /// Item Master. Optional descriptor (drawing number + rev are the
        /// release-mandatory identity).
        /// </summary>
        [StringLength(64)]
        [Display(Name = "As-Planned Part #")]
        public string? AsPlannedPartNumber { get; set; }

        /// <summary>
        /// B7 — as-planned drawing number. MANDATORY at release for a PoFirst
        /// order (AS9100 §8.5.2 configuration anchor).
        /// </summary>
        [StringLength(64)]
        [Display(Name = "As-Planned Drawing #")]
        public string? AsPlannedDrawingNumber { get; set; }

        /// <summary>
        /// B7 — as-planned drawing revision. MANDATORY at release for a PoFirst
        /// order — production runs against a revision-controlled configuration.
        /// </summary>
        [StringLength(16)]
        [Display(Name = "As-Planned Drawing Rev")]
        public string? AsPlannedDrawingRev { get; set; }

        /// <summary>B7 — free-text as-planned description for the PoFirst build.</summary>
        [StringLength(500)]
        [Display(Name = "As-Planned Description")]
        public string? AsPlannedDescription { get; set; }

        /// <summary>
        /// B7 — set by Wave B crystallization at ship: the minted/linked Item
        /// Master that this PoFirst order's as-built BOM + Routing + actual cost
        /// crystallized into. NULL until crystallized; SET NULL on item delete so
        /// the order's history survives master archival. Distinct from
        /// <see cref="ItemId"/> (the principal-material FK, which stays null for
        /// PoFirst orders).
        /// </summary>
        [Display(Name = "Crystallized Item")]
        public int? CrystallizedItemId { get; set; }
        public Item? CrystallizedItem { get; set; }

        /// <summary>
        /// B7 release guard — a PoFirst (master-optional) order MUST carry an
        /// as-planned drawing number + rev before it can be released. A
        /// StandardFirst order is unaffected (returns true). Pure/static for
        /// reuse in the release write path and unit tests — mirrors
        /// <see cref="Item.ValidateSourcePatternCarveout"/>.
        /// </summary>
        public static bool ValidatePoFirstReleaseReadiness(
            bool isPoFirst,
            string? asPlannedDrawingNumber,
            string? asPlannedDrawingRev,
            out string error)
        {
            error = string.Empty;
            if (!isPoFirst)
                return true; // StandardFirst — release gate handled elsewhere.

            if (string.IsNullOrWhiteSpace(asPlannedDrawingNumber)
                || string.IsNullOrWhiteSpace(asPlannedDrawingRev))
            {
                error = "A PO-First (master-optional) order requires an as-planned " +
                        "drawing number AND revision before release — production must " +
                        "run against a defined, revision-controlled configuration " +
                        "(AS9100 §8.5.2). Set AsPlannedDrawingNumber + AsPlannedDrawingRev first.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// B7 create/edit guard — a PoFirst (master-optional) order builds from
        /// the PO; it must NOT carry a principal <see cref="ItemId"/> (the master
        /// crystallizes at ship into <see cref="CrystallizedItemId"/>). Returns
        /// false + message when both are set. Pure/static for reuse + tests.
        /// </summary>
        public static bool ValidatePoFirstHasNoPrincipalItem(
            bool isPoFirst,
            int? itemId,
            out string error)
        {
            error = string.Empty;
            if (isPoFirst && itemId.HasValue)
            {
                error = "A PO-First (master-optional) order builds from the PO itself — " +
                        "leave the principal Item null. The Item Master crystallizes at " +
                        "ship into CrystallizedItemId (Wave B). If you need a master at " +
                        "release, use a StandardFirst order instead.";
                return false;
            }
            return true;
        }

        // Audit fields — same convention as WorkOrder.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        // Optimistic concurrency via PG xmin. See
        // Data/XminRowVersionExtensions.cs. Wired in AppDbContext via
        // e.MapXminRowVersion(x => x.RowVersion). Matches WorkOrder.
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // ----- Navs -----

        // 1:0..1 to ProductionJobShopDetail (UNIQUE on ProductionOrderId
        // in the migration). Optional — only present when Type=JobShop.
        public ProductionJobShopDetail? JobShopDetail { get; set; }

        // Operations linkage is intentionally NOT modeled in this PR.
        // WorkOrderOperation gets the IsExternal / VendorId / AutoGeneratePR
        // extension columns here so the SAP PP02 outside-processing pattern
        // works for the existing maintenance / quality / engineering WO
        // path. Whether ProductionOrders reuse WorkOrderOperation via a
        // nullable ProductionOrderId column, or get their own
        // ProductionOrderOperation table, is an ADR-014 decision — defer.
    }
}
