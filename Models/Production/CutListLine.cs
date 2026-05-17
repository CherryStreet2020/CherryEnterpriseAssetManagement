using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13b — CutListLine.
    //
    // The to-cut queue. One row per part-quantity request — e.g.,
    // "10 of part FT-1042 in 1/4 A36 for ProductionOrder PO-2026-0042."
    //
    // Cut-list "header" is intentionally NOT modeled — no vendor surveyed
    // (SigmaNest / ProNest / Lantek / Fulcrum) has a CutList parent
    // entity. The lookup pattern is:
    //   "show me the cut list for ProductionOrder X"
    //   -> SELECT * FROM CutListLines WHERE SourceProductionOrderId = X
    //
    // Or "queue for nesting":
    //   -> SELECT * FROM CutListLines WHERE NestId IS NULL
    //      AND Status IN ('Planned', 'Reserved') ORDER BY Priority, DueDate
    //
    // Material lives on the Item OR (more specifically) on the
    // MaterialMaster FK here. Material varchar field from earlier
    // sketches was a denormalization bug — caught by research.
    //
    // GrainDirection lives per-line (not per-material) because it's a
    // per-part override. Anisotropic stock (wood, some composites,
    // rolled steel for bend ops) needs the line to choose direction.
    //
    // Reference: PR #119.13a research report Q1 (no CutList parent),
    // Q3 (material taxonomy), Q4 (grain direction per-line override).
    [Table("CutListLines")]
    public class CutListLine
    {
        public int Id { get; set; }

        // The part SKU to be cut. RESTRICT — can't delete a SKU with
        // open cut-list lines.
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        // The nest this line is currently assigned to. SET NULL — if a
        // nest is cancelled, the line goes back to the queue.
        public int? NestId { get; set; }
        public Nest? Nest { get; set; }

        // The order driving this cut. SET NULL — if a production order
        // is deleted, the cut-list line becomes orphan (recoverable for
        // a stock-replenishment line).
        public int? SourceProductionOrderId { get; set; }
        public ProductionOrder? SourceProductionOrder { get; set; }

        // Material identity. Optional — Item.MaterialMasterId is the
        // default; this column overrides for cases where the same SKU
        // can be cut from multiple materials (rare but happens with
        // dimensional lumber).
        public int? MaterialMasterId { get; set; }
        public MaterialMaster? MaterialMaster { get; set; }

        [Display(Name = "Quantity")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; } = 1;

        // Per-line dimension overrides — when the same SKU has variable
        // dimensions (custom lengths) or when the line is for a
        // sub-component cut from a larger part.
        [Display(Name = "Length (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? LengthMm { get; set; }

        [Display(Name = "Width (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? WidthMm { get; set; }

        [Display(Name = "Thickness (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? ThicknessMm { get; set; }

        // Per-part grain orientation. For isotropic materials default
        // NotApplicable — UI hides the column. For anisotropic
        // (wood panels, rolled metal for bending) the column is
        // mandatory.
        public GrainDirection GrainDirection { get; set; } = GrainDirection.NotApplicable;

        // Chain / common-line cut grouping. Free text 32 chars —
        // parts with the same group can share cut edges, which the
        // CAM software uses to reduce pierces and cut path length.
        // Optional, only meaningful for laser / plasma.
        [StringLength(32)]
        public string? CommonLineGroup { get; set; }

        // Dispatch priority. Lower = ahead in the queue. Default 50
        // gives room to bump up (10-49) or down (51-99) without
        // re-numbering the queue.
        public int Priority { get; set; } = 50;

        public CutListLineStatus Status { get; set; } = CutListLineStatus.Planned;

        public DateTime? DueDate { get; set; }

        // Set when the part is cut. Drives ledger postings and
        // downstream Operation status flips.
        public DateTime? CutAt { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }

    public enum GrainDirection
    {
        NotApplicable = 0,    // isotropic material; column hidden in UI
        WithGrain = 1,
        AgainstGrain = 2,
        Either = 3,           // explicitly "operator may choose"
    }

    public enum CutListLineStatus
    {
        Planned = 0,
        Reserved = 1,         // earmarked for an upcoming nest
        Nested = 2,           // assigned to a nest, awaiting cut
        Cut = 3,
        Issued = 4,           // part has been issued to the consuming op
        Cancelled = 5,
    }
}
