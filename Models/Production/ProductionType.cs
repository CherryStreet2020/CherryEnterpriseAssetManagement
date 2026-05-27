namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.12 — Production work-method discriminator.
    //
    // Mirrors Oracle Fusion's WORK_METHOD column on
    // WIS_WORK_DEFINITIONS / WIE_WORK_ORDERS. Each value selects which
    // 1:0..1 satellite (ProductionJobShopDetail, ProductionProcessDetail,
    // etc.) carries the per-type fields and which status profile +
    // regulatory rules apply at runtime.
    //
    // This is intentionally a flat enum (not a hierarchy). When a new
    // vertical needs its own per-type fields, the answer is "add a
    // satellite," not "subclass WorkOrder." See ADR-013 §"Option C —
    // Hybrid."
    //
    // Values:
    //   - JobShop:            Custom one-off / low-volume make-to-order
    //                         work. CIP machine shops, fabricators,
    //                         job-shop CNC. Cut lists, nests,
    //                         outside-processing flags live in
    //                         ProductionJobShopDetail.
    //   - RepetitiveDiscrete: High-volume, rate-based / takt-time
    //                         discrete production. Auto OEM, line
    //                         assembly. Satellite added in a later PR.
    //   - CapitalETO:         Engineer-to-order capital equipment. Multi-
    //                         month project orders with revisions and
    //                         milestone gates. Satellite added later.
    //   - ProcessBatch:       Recipe-driven batch production (chem,
    //                         food, pharma). Co-products, by-products,
    //                         phase timing. Satellite is
    //                         ProductionProcessDetail (PR #119.14).
    //   - Hybrid:             Bulk-then-pack flow (cosmetics, packaged
    //                         food). Recipe upstream + packaging BOM
    //                         downstream. Reserved now to avoid an enum
    //                         migration later; first true hybrid
    //                         customer will populate the satellite.
    //
    // Reference: ADR-013 §"Recommendation" item 1.
    public enum ProductionType
    {
        JobShop = 0,
        RepetitiveDiscrete = 1,
        CapitalETO = 2,
        ProcessBatch = 3,
        Hybrid = 4,

        // B8 PR-PO-1 (2026-05-27) — 7 additional order-type variants per
        // PO Cockpit spec §1. Each represents a distinct lifecycle + cost
        // treatment. Satellites for these (ReworkDetail, PrototypeDetail,
        // etc.) will be added as 1:0..1 tables in later PRs as each variant
        // gets its first customer-facing UI. Matches SAP PP order categories
        // PP01/PP02/PP03/PP04/PP05 + Oracle Fusion work method variants.
        Rework = 5,         // Re-process defective or out-of-spec material
        Repair = 6,         // Restore a returned / damaged item to spec
        Prototype = 7,      // First-article or pre-production trial run
        Engineering = 8,    // Engineering sample / test coupon / destructive test
        Service = 9,        // Field-service rebuild / overhaul (MRO context)
        Teardown = 10,      // Disassembly for inspection / harvesting components
        Disassembly = 11,   // Planned component recovery (reverse BOM explosion)
    }

    // ADR-013 / PR #119.12 — ProductionOrder status machine.
    //
    // Intentionally distinct from WorkOrder's status machine: production
    // has different release / pick / start / yield gates than maintenance
    // or quality work. Mirrors ISA-95 Job Order states + Oracle Fusion
    // production-order lifecycle.
    //
    //   - Planned:    Initial draft. No material reservation, no scheduling
    //                 commit, fully editable.
    //   - Firmed:     MRP-confirmed. Quantities/dates locked by the planner
    //                 but not yet released to the floor. Materials may be
    //                 soft-reserved. Editable by planners only.
    //                 (B8 PR-PO-1 addition — SAP "FIRM" / Oracle "Firmed".)
    //   - Released:   Approved + scheduled. Materials reserved. If any
    //                 Operation has AutoGeneratePR=true and IsExternal=true,
    //                 a purchase requisition fires at this transition (wired
    //                 in a later PR). BOM snapshot captured by IPoSnapshotService.
    //   - InProgress: At least one Operation moved to InProgress and at
    //                 least one material pick has been recorded.
    //   - OnHold:     Paused — e.g., waiting on an external operation
    //                 vendor, a quality dispo, or an engineering change.
    //                 HoldReason + HoldReasonNotes populated on entry.
    //   - Completed:  All required Operations completed, all required
    //                 yield + scrap reported, final quantity confirmed.
    //   - Cancelled:  Terminal abort state. Material reservations released.
    //   - Closed:     Post-Completed financial close. All costs posted,
    //                 WIP cleared, variances written off. Immutable.
    //                 (B8 PR-PO-1 addition — SAP "CLSD" / Oracle "Closed".)
    public enum ProductionOrderStatus
    {
        Planned = 0,
        Released = 1,
        InProgress = 2,
        OnHold = 3,
        Completed = 4,
        Cancelled = 5,
        Firmed = 6,     // B8 PR-PO-1: between Planned and Released
        Closed = 7,     // B8 PR-PO-1: post-Completed financial close
    }

    // B8 PR-PO-1 (2026-05-27) — HoldReason enum.
    // Categorizes WHY a ProductionOrder is placed OnHold. The spec §1
    // lists 6 mandatory categories; each maps to a different resolution
    // workflow (e.g., Material hold → expedite PO; Quality hold → MRB
    // disposition; Engineering hold → ECR/ECO approval).
    public enum HoldReason
    {
        Material = 0,       // Waiting for material (shortage, late delivery, quarantine)
        Quality = 1,        // Quality hold (NCR, inspection failure, MRB pending)
        Engineering = 2,    // Engineering change pending (ECR/ECO in review)
        Customer = 3,       // Customer-initiated hold (spec change, approval pending)
        Credit = 4,         // Credit hold on the customer account
        Capacity = 5,       // Resource capacity constraint (machine down, labor unavailable)
    }

    // B8 PR-PO-1 (2026-05-27) — LotSerialRequirementType enum.
    // Specifies lot/serial tracking requirements for the finished good
    // produced by this order. Drives whether the completion transaction
    // demands lot assignment, serial assignment, both, or neither.
    // SAP equivalent: MARC-XCHAR (serial) + MARC-XCHPF (batch/lot).
    public enum LotSerialRequirementType
    {
        None = 0,           // No lot/serial tracking on the produced FG
        Lot = 1,            // Lot-controlled (e.g., batch number assigned on completion)
        Serial = 2,         // Serial-controlled (e.g., each unit gets a unique serial)
        Both = 3,           // Both lot AND serial (e.g., aerospace AS9100 traceability)
    }
}
