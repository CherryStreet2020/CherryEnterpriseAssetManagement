using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Production
{
    // ADR-013 / PR #119.13b — StockReceipt physical-lot record.
    //
    // ONE ROW PER PHYSICAL SHEET (or per receipt lot) that arrives
    // from the supplier. The industry-specific traceability payload
    // (heat number / mill cert / NDC / GTIN / harvest date / etc.)
    // lives in the Attributes jsonb column, shape-validated against
    // the receipt's ProfileId via JsonSchema.Net.
    //
    // ADR-015 Migration PR #3 (2026-05-19) DROPPED the 8 legacy
    // steel-specific columns (HeatNumber, MillCertUrl, Mill,
    // Length/Width/Thickness, UsableLength/Width). They live in
    // Attributes now under the STEEL profile.
    //
    // Why a separate table from Items:
    //   - Item is the SKU master ("1/4-inch A36 plate, 48x96 sheet").
    //     Same Item row for every sheet of that spec.
    //   - StockReceipt is the physical lot — each receipt has its own
    //     traceability payload.
    //   - Two sheets of the same SKU do NOT share heat number.
    //
    // Reference: ADR-015 industry-agnostic-receipt-schema +
    // dynamic-razor-form-spec.md §8.
    [Table("StockReceipts")]
    public class StockReceipt
    {
        public int Id { get; set; }

        // ADR-015 — Industry profile that defines the shape of Attributes.
        // Nullable on the model for the dual-write window; NOT NULL in the
        // DB after Migration PR #2 added the constraint.
        public int? ProfileId { get; set; }
        public ReceiptProfile? Profile { get; set; }

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

        // ---- Universal traceability fields (every profile) ----

        // Supplier-side lot identifier. Universal across verticals.
        [StringLength(64)]
        public string? LotNumber { get; set; }

        // Universal tracking dimension. Used by pharma (DSCSA), electronics
        // (date code), automotive (PPAP), medical device (UDI-PI), oil & gas
        // (downhole serial). Nullable because not every receipt is serial-
        // tracked.
        [StringLength(128)]
        [Display(Name = "Serial #")]
        public string? SerialNumber { get; set; }

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

        // ADR-015 — Industry-specific payload, validated at service layer
        // against the ProfileId's JsonSchema via ReceiptAttributesValidator
        // (JsonSchema.Net Draft 2020-12).
        //
        // Migration PR #3 (2026-05-19) made this the only home for the 8
        // formerly-typed steel fields: heatNumber, mill, millCertUrl,
        // lengthMm, widthMm, thicknessMm, usableLengthMm, usableWidthMm.
        // Other profiles encode their fields here too (PHARMA: ndc,
        // expirationDate, gtin; FOOD: traceabilityLotCode, harvestDate; etc.).
        [Column(TypeName = "jsonb")]
        public string? Attributes { get; set; }

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
