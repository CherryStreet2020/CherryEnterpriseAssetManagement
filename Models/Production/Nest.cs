using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13a — Nest subtype of ProductionBatch.
    //
    // The cutting-specific satellite: sheet plan that gets sent to the
    // CNC / laser / plasma / water-jet / oxy-fuel cutter. Aggregates
    // parts (often spanning multiple production orders) onto one sheet
    // of stock, with proportional cost-allocation back via the parent's
    // Allocations collection.
    //
    // Why a subtype not the base table:
    //   Sheet-specific data (DXF file, kerf, utilization, pierce count)
    //   doesn't apply to a heat-treat or paint batch. Lifting common
    //   columns (number, status, schedule, equipment, operator,
    //   allocation method) to ProductionBatch keeps subtype tables thin
    //   and makes cross-batch reporting trivial.
    //
    // Relationship: 1:0..1 with ProductionBatch.
    //   - UNIQUE on ProductionBatchId, ON DELETE CASCADE
    //   - Set only when ProductionBatch.BatchType = Nest
    //
    // PR #119.13b will add Nest.StockReceiptId FK to the physical
    // sheet being cut (heat number / mill cert traceability lives on
    // the StockReceipt, not on the Item master per Dean's correction).
    //
    // Reference: ADR-013 §"Recommendation" item 4 + Fulcrum / JETCAM /
    // SigmaNest / Lantek / Hypertherm ProNest convergence per the
    // PR #119.13a research report.
    [Table("Nests")]
    public class Nest
    {
        public int Id { get; set; }

        public int ProductionBatchId { get; set; }
        public ProductionBatch? ProductionBatch { get; set; }

        // The sheet-stock SKU being consumed. Physical-lot traceability
        // (heat number, mill cert) ships in PR #119.13b via a
        // StockReceiptId FK to the physical sheet. For now the SKU FK
        // is sufficient to model "what kind of sheet is this nest
        // cutting from."
        public int? StockItemId { get; set; }
        public Item? StockItem { get; set; }

        // DXF file pointer to external storage (Box / SharePoint / S3).
        // We deliberately do NOT parse DXF in-app — every CAM vendor
        // (JETCAM, Lantek, SigmaNest, ProNest) puts nest metadata in
        // their own DB, not in the DXF itself.
        [StringLength(500)]
        [Display(Name = "DXF File URL")]
        public string? DxfFileUrl { get; set; }

        // The nesting software that produced this nest. Free-text 64
        // chars — every shop has a different mix (JETCAM at machine
        // shops, Lantek at fabs, SigmaNest at A&D). Enum here would
        // force a migration per customer.
        [StringLength(64)]
        [Display(Name = "Nesting Software")]
        public string? NestingSoftware { get; set; }

        [Display(Name = "Sheet Length (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? SheetLengthMm { get; set; }

        [Display(Name = "Sheet Width (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? SheetWidthMm { get; set; }

        // How many identical sheets this nest is cutting. For one-off
        // jobs SheetCount=1; for repetitive cuts the same DXF goes on
        // N sheets.
        public int SheetCount { get; set; } = 1;

        // Sheet-area utilization, 0-1.0. Set post-cut from CAM software.
        [Column(TypeName = "decimal(5,4)")]
        public decimal? Utilization { get; set; }

        // Revision counter — operators frequently re-program a held
        // nest. Increment on each re-issue.
        public int RevisionNumber { get; set; } = 1;

        // Pieces accounting. PiecesPlanned is derived from the
        // CutListLines pre-cut; PiecesCut comes back from the machine
        // post-cut. Stored rather than computed for fast reporting.
        [Display(Name = "Pieces Planned")]
        public int PiecesPlanned { get; set; }

        [Display(Name = "Pieces Cut")]
        public int? PiecesCut { get; set; }

        // Machine output captured post-cut. PierceCount drives plasma
        // consumable-cost allocation; CutPathLengthMm drives laser
        // assist-gas allocation; CuttingTimeSeconds drives machine-time
        // allocation.
        public int? PierceCount { get; set; }

        [Display(Name = "Cut Path Length (mm)")]
        [Column(TypeName = "decimal(12,2)")]
        public decimal? CutPathLengthMm { get; set; }

        [Display(Name = "Cutting Time (s)")]
        public int? CuttingTimeSeconds { get; set; }

        // Operator who ran the cut. AS9100 8.5.2 traceability — though
        // the operator FK lives on ProductionBatch.OperatorUserId, this
        // field captures the *cut-specific* operator when it differs
        // (e.g., programmer vs operator).
        public int? CutByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
