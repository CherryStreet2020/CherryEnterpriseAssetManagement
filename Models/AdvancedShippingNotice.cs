using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models;

// =============================================================================
// CherryAI EAM — Advanced Shipping Notice (Sprint 12A PR #6)
//
// First-class ASN domain entity. Replaces the placeholder "ASN:" prefix on
// StockReceipt.SourcePoNumber that Sprint 11 PR #6 used as a stop-gap.
//
// What an ASN is, in operator terms:
//   The vendor sends advance notice before a truck arrives. The notice
//   declares: which PO(s), what items + quantities, lot/serial info, ETA,
//   carrier, tracking. When the truck shows up, the receiver scans the ASN
//   barcode (or types the AsnNumber), and the receive workflow pre-fills
//   from the declared manifest. ~10% of receipts in a typical mid-market
//   plant; higher with strong supplier integration.
//
// Why this is a thin slice (PR #6 scope):
//   The full EDI 856 X12 parser + AS2 trading-partner handshake +
//   automated ASN ingestion lives in Sprint 21 (MCP server + Agentic AI
//   Launch Package per the 116-reckoning lock 2026-05-19). For now this
//   PR adds the *destination schema* + seed data so the cockpit ASN
//   Queue tab can render real data. The /Receiving/ByAsn page (Sprint 11
//   PR #6) already takes an AsnId query param; PR #6 makes that param
//   point at a real entity instead of a string.
//
// Why this design (vs alternatives I considered):
//   - One ASN can cover multiple POs in reality. Modeled as nullable
//     SourcePoNumber on the ASN header (most-common single-PO case) and
//     line-level RefPoLineId on AsnLine for multi-PO shipments.
//   - ExpectedArrivalDate is the ETA driving the cockpit's ByTimeLens
//     bucket assignment (Overdue / Today / This Week / Later).
//   - Status enum sized for the operator workflow, not EDI compliance.
//     Real EDI 856 has many more status codes; we map them into these
//     5 buckets at ingestion time when Sprint 21 lands.
// =============================================================================

public enum AsnStatus
{
    // Vendor declared the shipment; truck hasn't left yet OR no tracking signal.
    Expected = 0,

    // Vendor confirmed truck departed; carrier/tracking known and active.
    InTransit = 1,

    // Truck arrived at the dock; receipt workflow not yet started.
    Arrived = 2,

    // ReceiveByAsnAsync has been called for one or more lines; partial or full.
    Receiving = 3,

    // All declared lines received (sum of AsnLine.ReceivedQuantity ==
    // ExpectedQuantity for every line). Terminal.
    Received = 4,

    // Vendor or buyer cancelled the shipment. Terminal.
    Cancelled = 5,
}

public class AdvancedShippingNotice
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    [Display(Name = "ASN Number")]
    // The vendor's identifier for this shipment. Often a barcode value
    // scanned at the dock. Unique within (VendorId, AsnNumber) — same
    // vendor cannot send two ASNs with the same number.
    public string AsnNumber { get; set; } = string.Empty;

    [Required]
    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    // Destination site. Nullable because some vendors send ASNs against
    // a buying-org's master receiving account, not a specific site.
    [Display(Name = "Ship To Site")]
    public int? ShipToSiteId { get; set; }
    public Site? ShipToSite { get; set; }

    [Display(Name = "Ship Date")]
    public DateTime? ShipDate { get; set; }

    [Display(Name = "Expected Arrival")]
    public DateTime? ExpectedArrivalDate { get; set; }

    [Display(Name = "Status")]
    public AsnStatus Status { get; set; } = AsnStatus.Expected;

    [StringLength(50)]
    [Display(Name = "Carrier")]
    public string? Carrier { get; set; }

    [StringLength(80)]
    [Display(Name = "Tracking #")]
    public string? TrackingNumber { get; set; }

    // Single-PO case (most common). For multi-PO shipments, leave this
    // null and link at the line level via AsnLine.RefPoNumber.
    [StringLength(20)]
    [Display(Name = "PO Number")]
    public string? SourcePoNumber { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Set when the ASN reaches Status = Received. Null until then.
    public DateTime? ReceivedAt { get; set; }

    public ICollection<AsnLine> Lines { get; set; } = new List<AsnLine>();
}

public class AsnLine
{
    public int Id { get; set; }

    public int AsnId { get; set; }
    public AdvancedShippingNotice? Asn { get; set; }

    public int LineNumber { get; set; }

    // Reference into the Item Master. Nullable for non-Item-Master parts
    // or until matched (mirrors PurchaseOrderLine pattern).
    public int? ItemId { get; set; }
    public Item? Item { get; set; }

    [Required, StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Part Number")]
    public string? PartNumber { get; set; }

    // PO reference at line level — supports multi-PO ASNs. When the ASN
    // header carries SourcePoNumber, this can be null (inherits parent).
    [StringLength(20)]
    public string? RefPoNumber { get; set; }
    [StringLength(40)]
    public string? RefPoLineId { get; set; }

    [StringLength(20)]
    [Display(Name = "Unit of Measure")]
    public string Uom { get; set; } = "EA";

    [Column(TypeName = "decimal(18,4)")]
    [Display(Name = "Expected Quantity")]
    public decimal ExpectedQuantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    [Display(Name = "Received Quantity")]
    public decimal ReceivedQuantity { get; set; }

    // Optional lot/serial info pre-declared by the vendor. When present,
    // ReceiveByAsnAsync pre-fills these into the StockReceipt without
    // operator entry.
    [StringLength(80)]
    public string? LotNumber { get; set; }

    [StringLength(120)]
    public string? SerialNumber { get; set; }

    public DateTime? ExpirationDate { get; set; }

    // Steel-specific heat number; populated only when ASN comes from a
    // mill / service center vendor. Maps into StockReceipt.Attributes via
    // the ReceiptProfile JSON contract.
    [StringLength(60)]
    public string? HeatNumber { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
