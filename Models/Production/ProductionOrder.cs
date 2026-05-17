using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // Human-facing identifier (e.g., "PO-2026-00042"). Generated via
        // NumberSequence (SAP NRIV pattern, PR #119.5) once a number-
        // sequence row for ProductionOrder is seeded — current MVP allows
        // the controller to assign on create.
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

        // Unit of measure for the produced item. Free-text 16 chars to
        // match how Item already represents UoM elsewhere in the model —
        // we'll consolidate to a UoM table in a later sprint.
        [StringLength(16)]
        public string? Uom { get; set; }

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

        // ADR-013 / PR #119.14 — MaterialStructure FK.
        // Which Bom or Recipe is this order producing? SET NULL on
        // structure delete — order history survives administrative
        // structure cleanup (rare; usually Status -> Retired).
        public int? MaterialStructureId { get; set; }
        public MaterialStructure? MaterialStructure { get; set; }

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
