using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13b — StockReceipt physical-lot record.
    //
    // ONE ROW PER PHYSICAL SHEET (or per receipt lot) that arrives
    // from the supplier. This is where heat number / mill cert / source
    // PO data lives — NOT on the Item master.
    //
    // Why a separate table from Items:
    //   - Item is the SKU master ("1/4-inch A36 plate, 48x96 sheet").
    //     Same Item row for every sheet of that spec.
    //   - StockReceipt is the physical lot — each receipt has its own
    //     heat number, mill cert, source PO, received date.
    //   - Two sheets of the same SKU do NOT share heat number.
    //
    // Where the data comes from (per Dean's correction):
    //   - HeatNumber, LotNumber: read off the mill cert PDF at receiving.
    //     Can be operator-typed or scanned/OCR'd.
    //   - MillCertUrl: PDF uploaded to Box/SharePoint at receiving;
    //     URL stored here.
    //   - Mill: copied from the PO line at receipt.
    //   - SourcePoNumber + SourcePoLineId: PO/ERP-driven. Either pushed
    //     in via integration event or filled at the receiving screen.
    //
    // Regulatory drivers:
    //   - ASME Section IX: heat number traceability per weld.
    //   - AWS D1.1: certified mill test report per structural steel.
    //   - AS9100 8.5.2: 20-40 year retention of heat-number genealogy.
    //   - NADCAP AC7102: heat-treat input lot traceability.
    //
    // Consumption flow:
    //   1. Receipt arrives -> StockReceipt row created (Status=Available).
    //   2. Nest is built consuming this sheet -> Nest.StockReceiptId
    //      links the cut to the specific physical sheet.
    //   3. As parts are cut, QuantityRemaining decrements.
    //   4. If a usable offcut remains, a Remnant row is created with
    //      ParentReceiptId pointing here (heat number inherits).
    //   5. When QuantityRemaining = 0, Status -> FullyConsumed.
    //
    // Reference: PR #119.13a research report Q2 (remnant tracking) +
    // Q10 (heat number genealogy is the #1 audit lookup).
    [Table("StockReceipts")]
    public class StockReceipt
    {
        public int Id { get; set; }

        // Human-facing identifier — "RCPT-2026-00042".
        [Required]
        [StringLength(32)]
        [Display(Name = "Receipt #")]
        public string ReceiptNumber { get; set; } = string.Empty;

        // The SKU this receipt is satisfying. Required + RESTRICT on
        // delete — you can't delete a SKU that has receipts referencing it.
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        // Optional FK to MaterialMaster — usually inferred from the
        // Item, kept here for fast joins + when the receipt has a more
        // specific material than the Item master records.
        public int? MaterialMasterId { get; set; }
        public MaterialMaster? MaterialMaster { get; set; }

        // ---- Traceability fields (the whole point of this table) ----

        // Heat number from the mill. ASME / AWS / aerospace traceability
        // join key. Indexed for "find all parts cut from heat X" audits.
        [StringLength(64)]
        [Display(Name = "Heat #")]
        public string? HeatNumber { get; set; }

        // Supplier-side lot identifier (often distinct from heat number).
        [StringLength(64)]
        public string? LotNumber { get; set; }

        // Pointer to the mill test report PDF in external storage.
        [StringLength(500)]
        [Display(Name = "Mill Cert URL")]
        public string? MillCertUrl { get; set; }

        // Mill that produced the stock.
        [StringLength(128)]
        public string? Mill { get; set; }

        // Source PO traceability — ties back to ERP / purchasing.
        [StringLength(64)]
        [Display(Name = "Source PO #")]
        public string? SourcePoNumber { get; set; }

        [StringLength(64)]
        [Display(Name = "Source PO Line")]
        public string? SourcePoLineId { get; set; }

        // ---- Receipt event ----

        [Display(Name = "Received At")]
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

        // The receiver. Audit trail; required for AS9100 receiving inspection.
        public int? ReceivedByUserId { get; set; }
        public User? ReceivedByUser { get; set; }

        // Storage location.
        public int? LocationId { get; set; }
        public Location? Location { get; set; }

        // ---- Physical dimensions ----

        [Display(Name = "Length (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? LengthMm { get; set; }

        [Display(Name = "Width (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? WidthMm { get; set; }

        [Display(Name = "Thickness (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? ThicknessMm { get; set; }

        // Usable dimensions decrement as the sheet is cut. When a nest
        // partially consumes a sheet, these reflect what's left.
        [Display(Name = "Usable Length (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? UsableLengthMm { get; set; }

        [Display(Name = "Usable Width (mm)")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal? UsableWidthMm { get; set; }

        // ---- Quantity tracking ----

        // How many units arrived. Decimal because process verticals
        // measure in kg / L.
        [Display(Name = "Quantity Received")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReceived { get; set; }

        // How much is left. Decrements as the receipt is consumed.
        [Display(Name = "Quantity Remaining")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityRemaining { get; set; }

        // UoM ("ea", "kg", "L"). Free text 16 chars — matches the
        // Item / ProductionOrder convention.
        [StringLength(16)]
        public string? Uom { get; set; }

        // ---- Status ----

        public StockReceiptStatus Status { get; set; } = StockReceiptStatus.Available;

        // Quarantine reason — set when Status transitions to Quarantined.
        [StringLength(500)]
        public string? QuarantineReason { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        // Optimistic concurrency via PG xmin.
        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }

    public enum StockReceiptStatus
    {
        Available = 0,
        Reserved = 1,           // earmarked for an upcoming nest / job
        PartiallyConsumed = 2,  // some cut, some remaining
        FullyConsumed = 3,
        Quarantined = 4,        // failed receiving inspection / suspect heat
        Scrapped = 5,
        Returned = 6,           // sent back to supplier
    }
}
