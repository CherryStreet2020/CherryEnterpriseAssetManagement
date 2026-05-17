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
    //   - Released:   Approved + scheduled. Materials reserved. If any
    //                 Operation has AutoGeneratePR=true and IsExternal=true,
    //                 a purchase requisition fires at this transition (wired
    //                 in a later PR).
    //   - InProgress: At least one Operation moved to InProgress and at
    //                 least one material pick has been recorded.
    //   - Completed:  All required Operations completed, all required
    //                 yield + scrap reported, final quantity confirmed.
    //   - Cancelled:  Terminal abort state. Material reservations released.
    //   - OnHold:     Paused — e.g., waiting on an external operation
    //                 vendor, a quality dispo, or an engineering change.
    public enum ProductionOrderStatus
    {
        Planned = 0,
        Released = 1,
        InProgress = 2,
        OnHold = 3,
        Completed = 4,
        Cancelled = 5,
    }
}
