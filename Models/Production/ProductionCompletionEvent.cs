// B8 PR-PRO-6 (2026-05-27) — Complete + Scrap + Rework event entities.
//
// Three atomic event types that capture the DETAIL behind operation
// state transitions. The existing ProductionOperationTransaction records
// WHAT happened (Complete/PartialComplete/FinalComplete); these events
// record WHY, HOW MUCH of each category, and WHAT TO DO ABOUT IT.
//
// COMPLETE EVENT: Atomic posting of good + scrap + rework + reject
// quantities in a single operation. Triggers auto-advance (PR-PRO-5),
// backflush materials, FG receipt on final op, and cost posting.
//
// SCRAP EVENT: Root-cause classified scrap with 5-dimensional analysis
// (where detected, where caused, why, by whom, what to do). Drives
// the scrap-by-operation and scrap-by-reason dashboards in PR-PRO-12.
//
// REWORK EVENT: Rework decision with routing, instructions, disposition,
// and cost treatment. Links to WipMove (send-back) and NCR/CAR.
//
// Industry comparison:
//   SAP PP: CO11N confirmation + separate COGI error handling
//   Oracle: Completion + Move + Scrap as separate transactions
//   Epicor: Completion with scrap reason built-in (closest)
//   Plex: Integrated completion/scrap/rework in one screen
//   IndustryOS: ONE atomic posting with preview-before-post,
//     auto-advance, backflush, 5-dimensional scrap analysis,
//     and closed-loop rework routing. All in one event.
//
// AS9100 §8.5.1 + §8.7 (nonconforming output control).

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ────────────────────────────────────────────
    // ENUMS
    // ────────────────────────────────────────────

    /// <summary>
    /// Disposition of scrapped material.
    /// </summary>
    public enum ScrapDisposition
    {
        Scrap = 0,              // Write off — material destroyed / recycled
        Rework = 1,             // Can be reworked to spec
        UseAsIs = 2,            // Accept with deviation/concession
        Mrb = 3,                // Send to Material Review Board
        ReturnToVendor = 4,     // Supplier defect — RTV
        CustomerCharge = 5,     // Charge scrap cost to customer (customer-supplied material)
    }

    /// <summary>
    /// Who/what is responsible for the scrap.
    /// </summary>
    public enum ScrapResponsibleArea
    {
        Machine = 0,            // Machine malfunction / tool wear
        Labor = 1,              // Operator error
        Vendor = 2,             // Incoming material defect
        Engineering = 3,        // Design / drawing / spec error
        Material = 4,           // Raw material defect (internal stock)
        Process = 5,            // Process parameters out of spec
        Tooling = 6,            // Tooling wear / breakage
        Environment = 7,        // Temperature / humidity / contamination
    }

    /// <summary>
    /// Cost treatment for scrap and rework.
    /// </summary>
    public enum CostTreatment
    {
        AbsorbToJob = 0,        // Scrap cost stays on the production order WIP
        ScrapAccount = 1,       // Scrap cost posted to a dedicated scrap GL account
        CustomerCharge = 2,     // Scrap cost charged to the customer
        VendorChargeback = 3,   // Scrap cost charged back to the vendor
        WarrantyAbsorb = 4,     // Absorbed under warranty provisions
    }

    /// <summary>
    /// Rework routing approach.
    /// </summary>
    public enum ReworkRoutingType
    {
        ReturnToExistingOp = 0,     // Send back to an existing operation on this PRO
        InsertNewReworkOp = 1,      // Insert a new rework operation into the routing
        SeparateReworkOrder = 2,    // Create a child rework production order
        ExternalRework = 3,         // Send to outside vendor for rework
    }

    // ────────────────────────────────────────────
    // ENTITIES
    // ────────────────────────────────────────────

    /// <summary>
    /// Atomic completion posting — good + scrap + rework + reject in one event.
    /// One event per completion action (may be partial or final).
    /// </summary>
    [Table("ProductionCompletionEvents")]
    public class ProductionCompletionEvent
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        [Required] [StringLength(48)]
        public string CompletionNumber { get; set; } = string.Empty;

        // ----- Links -----
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }
        public int OperationId { get; set; }
        public ProductionOperation? Operation { get; set; }

        /// <summary>FK to the triggering ProductionOperationTransaction (Complete/PartialComplete/FinalComplete).</summary>
        public int? TransactionId { get; set; }
        public ProductionOperationTransaction? Transaction { get; set; }

        /// <summary>FK to the auto-advance WipMove created by this completion (if any).</summary>
        public int? WipMoveId { get; set; }
        public ProductionWipMove? WipMove { get; set; }

        // ----- Quantities -----
        public decimal GoodQuantity { get; set; }
        public decimal ScrapQuantity { get; set; }
        public decimal ReworkQuantity { get; set; }
        public decimal RejectQuantity { get; set; }

        /// <summary>True if this completion finishes all remaining qty on the operation.</summary>
        public bool CompleteRemaining { get; set; }

        /// <summary>True if this is the final operation (triggers FG receipt).</summary>
        public bool IsFinalOperation { get; set; }

        /// <summary>Quantity to auto-advance to the next operation.</summary>
        public decimal MoveQuantityToNextOp { get; set; }

        // ----- Employee / Resource -----
        [StringLength(120)]
        public string? EmployeeName { get; set; }
        public int? EmployeeId { get; set; }
        public int? ResourceWorkCenterId { get; set; }

        // ----- Material handling -----
        public bool BackflushMaterials { get; set; }
        public bool AutoIssuePullMaterials { get; set; }

        // ----- Quality -----
        public bool InspectionRequired { get; set; }

        // ----- Lot / Serial -----
        [StringLength(500)]
        public string? LotNumbers { get; set; }
        [StringLength(500)]
        public string? SerialNumbers { get; set; }

        // ----- Cost snapshot -----
        public decimal? LaborCostPosted { get; set; }
        public decimal? MaterialCostPosted { get; set; }
        public decimal? OverheadCostPosted { get; set; }

        // ----- Audit -----
        [StringLength(4000)]
        public string? Notes { get; set; }
        public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? CompletedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// Scrap event with 5-dimensional root cause analysis.
    /// </summary>
    [Table("ProductionScrapEvents")]
    public class ProductionScrapEvent
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        [Required] [StringLength(48)]
        public string ScrapNumber { get; set; } = string.Empty;

        // ----- Links -----
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        /// <summary>Operation where the scrap was DETECTED.</summary>
        public int DetectedAtOperationId { get; set; }
        public ProductionOperation? DetectedAtOperation { get; set; }

        /// <summary>Operation that CAUSED the scrap (may differ from detected).</summary>
        public int? CausedAtOperationId { get; set; }
        public ProductionOperation? CausedAtOperation { get; set; }

        // ----- Quantity -----
        public decimal ScrapQuantity { get; set; }
        [StringLength(20)]
        public string? ScrapUom { get; set; }

        // ----- 5-dimensional classification -----
        public int? ScrapReasonCodeId { get; set; }
        public int? DefectCodeId { get; set; }
        public int? CauseCodeId { get; set; }
        public ScrapResponsibleArea ResponsibleArea { get; set; } = ScrapResponsibleArea.Machine;
        public ScrapDisposition Disposition { get; set; } = ScrapDisposition.Scrap;

        // ----- Scope -----
        /// <summary>True if this is component/material scrap (not operation/FG scrap).</summary>
        public bool IsComponentScrap { get; set; }
        /// <summary>True if this is finished-good or operation-level scrap.</summary>
        public bool IsOperationScrap { get; set; } = true;

        /// <summary>True if replacement material/production is required.</summary>
        public bool ReplacementRequired { get; set; }

        // ----- Cost treatment -----
        public CostTreatment CostTreatment { get; set; } = CostTreatment.AbsorbToJob;
        public decimal? ScrapCost { get; set; }

        // ----- Quality linkage -----
        public bool NcrRequired { get; set; }
        public int? NcrId { get; set; }
        public bool SupervisorApprovalRequired { get; set; }
        public bool SupervisorApproved { get; set; }
        [StringLength(120)]
        public string? ApprovedBy { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }

        // ----- Lot / Serial -----
        [StringLength(500)]
        public string? LotNumbers { get; set; }
        [StringLength(500)]
        public string? SerialNumbers { get; set; }

        // ----- Audit -----
        [StringLength(4000)]
        public string? Notes { get; set; }
        public DateTime ScrapRecordedAtUtc { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? RecordedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// Rework decision event with routing and disposition.
    /// </summary>
    [Table("ProductionReworkEvents")]
    public class ProductionReworkEvent
    {
        public int Id { get; set; }
        public int? TenantId { get; set; }
        public int CompanyId { get; set; }

        [Required] [StringLength(48)]
        public string ReworkNumber { get; set; } = string.Empty;

        // ----- Links -----
        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        /// <summary>Operation where the defect was found / rework decision made.</summary>
        public int SourceOperationId { get; set; }
        public ProductionOperation? SourceOperation { get; set; }

        /// <summary>Destination operation for rework (existing op on this PRO).</summary>
        public int? ReworkOperationId { get; set; }
        public ProductionOperation? ReworkOperation { get; set; }

        /// <summary>FK to the WipMove that sends material back for rework.</summary>
        public int? WipMoveId { get; set; }
        public ProductionWipMove? WipMove { get; set; }

        // ----- Quantity -----
        public decimal ReworkQuantity { get; set; }

        // ----- Routing -----
        public ReworkRoutingType RoutingType { get; set; } = ReworkRoutingType.ReturnToExistingOp;

        [StringLength(4000)]
        public string? ReworkInstructions { get; set; }

        public int? ReworkReasonCodeId { get; set; }

        // ----- Material -----
        public bool ReworkMaterialRequired { get; set; }
        public bool RemoveDefectiveComponent { get; set; }

        // ----- Labor -----
        public decimal AdditionalLaborPlannedMins { get; set; }
        public int? AssignedWorkCenterId { get; set; }
        public DateTime? DueDate { get; set; }

        // ----- Quality -----
        public bool QualityHold { get; set; }
        public bool ReinspectRequired { get; set; }
        public bool ScrapAfterFailedReworkAllowed { get; set; }
        public bool ReturnToOriginalFlow { get; set; } = true;

        // ----- Cost treatment -----
        public CostTreatment CostTreatment { get; set; } = CostTreatment.AbsorbToJob;
        public decimal? EstimatedReworkCost { get; set; }

        // ----- NCR linkage -----
        public int? NcrId { get; set; }
        public int? CarId { get; set; }

        // ----- Audit -----
        [StringLength(4000)]
        public string? Notes { get; set; }
        public DateTime ReworkDecisionAtUtc { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? DecidedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(120)]
        public string? CreatedBy { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}
