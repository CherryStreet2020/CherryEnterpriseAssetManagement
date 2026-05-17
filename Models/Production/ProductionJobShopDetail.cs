using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.12 — ProductionJobShopDetail satellite.
    //
    // Holds JobShop-only fields for ProductionOrder.Type=JobShop.
    // Covers CIP machine shops, fabricators, custom-fab job-shop work.
    // Same 1:0..1 shape as the four ADR-012 v0.2 Phase D WorkOrder
    // classification satellites (CipWorkOrderDetails, etc.).
    //
    // Relationship: 1:0..1 with ProductionOrder.
    //   - UNIQUE on ProductionOrderId, ON DELETE CASCADE
    //   - No nav on the ProductionOrder side besides JobShopDetail (the
    //     renderer in Phase F reads this by ProductionOrderId)
    //
    // Field sourcing:
    //   - CutListId / NestPlanId: Forward refs to entities arriving in
    //     PR #119.13 (MaterialStructure + Nest + CutListLine). Stored as
    //     nullable int? placeholders now so the column exists when the
    //     entities land. FK + ON DELETE SET NULL added by PR #119.13's
    //     migration — keeping FK creation out of this PR avoids ordering
    //     dependencies in the migration sequencer.
    //   - DrawingNumber: External engineering drawing identifier (Onshape,
    //     SolidWorks PDM, AutoCAD title block).
    //   - DrawingRevision: Revision letter / number; one job-shop job is
    //     pinned to one drawing rev.
    //   - HasOutsideOperations: rolled-up bool — true when any
    //     WorkOrderOperation on this order has IsExternal=true.
    //     Materialized so the scheduler can filter "orders with outside
    //     ops" without a per-row scan.
    //   - OutsideOperationCount: count of external operations. Convenience
    //     for the renderer; recomputed by the order-release path.
    //   - MaterialIssueMethod: how raw stock gets issued to the floor —
    //     pull (operator-driven), push (backflush), or manual.
    //   - SerializedOutput: true when the produced item is serial-tracked
    //     (CIP equipment, A&D parts). Drives the "lot vs serial" UI gate
    //     in Phase F.
    //   - QualityHoldOnCompletion: true when the order must wait on a
    //     QA disposition before posting completed quantities to inventory
    //     (typical for A&D / FDA-shadow work even when not explicitly
    //     under AS9100). Pairs with the Quality satellite via a forward
    //     ref added later.
    //   - InspectionNotes: shop-floor inspection guidance free text.
    //   - PriorityRank: job-shop dispatch list rank, lower = ahead in the
    //     queue. Indexed; nullable so unpriortized orders sort last.
    //
    // Reference: ADR-013 §"Phase E ship plan" PR #119.12 + §"Where every
    // vendor agrees — Cut-list / nesting" (Fulcrum / JETCAM / Lantek
    // pattern).
    [Table("ProductionJobShopDetails")]
    public class ProductionJobShopDetail
    {
        public int Id { get; set; }

        public int ProductionOrderId { get; set; }
        public ProductionOrder? ProductionOrder { get; set; }

        // Forward refs to PR #119.13 entities. Columns exist now; FKs +
        // ON DELETE SET NULL will be added by that PR's migration.
        public int? CutListId { get; set; }
        public int? NestPlanId { get; set; }

        [StringLength(64)]
        public string? DrawingNumber { get; set; }

        [StringLength(16)]
        public string? DrawingRevision { get; set; }

        public bool HasOutsideOperations { get; set; }

        public int OutsideOperationCount { get; set; }

        public MaterialIssueMethod MaterialIssueMethod { get; set; } =
            MaterialIssueMethod.Pull;

        public bool SerializedOutput { get; set; }

        public bool QualityHoldOnCompletion { get; set; }

        [StringLength(1000)]
        public string? InspectionNotes { get; set; }

        public int? PriorityRank { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    // ADR-013 / PR #119.12 — How raw stock gets issued to a JobShop order.
    //
    //   - Pull:      Operator scans a request at the workstation; stock
    //                room releases.
    //   - PushBackflush: System backflushes the BOM components when
    //                operations report yield. SAP-PP "Repetitive" pattern;
    //                useful for high-volume job-shops with stable BOMs.
    //   - Manual:    Pre-pick / kit issued by a planner before the order
    //                releases. Default for ETO-flavored job-shop work.
    public enum MaterialIssueMethod
    {
        Pull = 0,
        PushBackflush = 1,
        Manual = 2,
    }
}
