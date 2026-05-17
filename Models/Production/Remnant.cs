using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13b — Remnant child of StockReceipt.
    //
    // Re-usable offcut. The piece of stock left over after a nest is
    // cut from a partial sheet. SigmaNest, ProNest, Lantek, Cabinet
    // Vision all model this as a first-class entity — and per the
    // PR #119.13a research report, "v1 always forgets remnants -> v2
    // painful schema migration" is the #1 documented regret in metal-
    // fab ERP. We ship it on day one.
    //
    // Why ParentReceiptId is RESTRICT not CASCADE:
    //   Heat number traceability. If a remnant exists, the parent
    //   StockReceipt cannot be deleted — the remnant inherits the
    //   heat number from the receipt and must keep its provenance.
    //
    // Why ParentNestId is SET NULL:
    //   Operationally, when a Nest gets re-programmed or cancelled,
    //   the physical offcut still exists in the rack. Decouple the
    //   remnant from its creating nest so we don't lose the offcut.
    //
    // Material taxonomy concerns (wood vs metal):
    //   Cabinet shops have a configurable "Offcut Threshold" — pieces
    //   below a certain size go to scrap, above go to remnant. This is
    //   shop policy, not schema — Phase F renderer config will gate
    //   the Remnant-vs-Scrapped decision at remnant-creation time.
    //
    // Reference: PR #119.13a research report Q2 (remnant tracking is
    // the #1 metal-fab regret pattern) + Q10 (heat number provenance
    // mandatory for ASME / AWS / AS9100).
    [Table("Remnants")]
    public class Remnant
    {
        public int Id { get; set; }

        // Human-facing identifier — "REM-2026-00042".
        [Required]
        [StringLength(32)]
        [Display(Name = "Remnant #")]
        public string RemnantNumber { get; set; } = string.Empty;

        // The receipt this remnant came from. RESTRICT — provenance
        // is non-negotiable for traceability.
        public int ParentReceiptId { get; set; }
        public StockReceipt? ParentReceipt { get; set; }

        // The nest that produced this remnant. SET NULL on nest
        // delete — the physical offcut survives administrative cleanup.
        public int? ParentNestId { get; set; }
        public Nest? ParentNest { get; set; }

        // Material identity. Inherited from parent receipt; denormalized
        // here for fast lookups.
        public int? MaterialMasterId { get; set; }
        public MaterialMaster? MaterialMaster { get; set; }

        // Heat number INHERITS from parent receipt. Denormalized for
        // fast audit queries — "find all current stock from heat X"
        // joins both StockReceipts and Remnants on HeatNumber.
        [StringLength(64)]
        [Display(Name = "Heat #")]
        public string? HeatNumber { get; set; }

        // Physical dimensions of the offcut.
        [Display(Name = "Length (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? LengthMm { get; set; }

        [Display(Name = "Width (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? WidthMm { get; set; }

        [Display(Name = "Thickness (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? ThicknessMm { get; set; }

        // Storage location — where this remnant is racked.
        public int? LocationId { get; set; }
        public Location? Location { get; set; }

        public RemnantStatus Status { get; set; } = RemnantStatus.Available;

        // Set when a future nest consumes this remnant. SET NULL on
        // that nest delete.
        public int? ConsumedByNestId { get; set; }
        public Nest? ConsumedByNest { get; set; }

        public DateTime? ConsumedAt { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }
    }

    public enum RemnantStatus
    {
        Available = 0,
        Reserved = 1,
        Consumed = 2,
        Scrapped = 3,
    }
}
