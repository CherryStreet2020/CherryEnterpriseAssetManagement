using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public enum ReceiptStatus
    {
        Draft = 0,
        Received = 1,
        PartiallyReceived = 2,
        Inspecting = 3,
        Accepted = 4,
        Rejected = 5
    }

    public class GoodsReceipt
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Receipt Number")]
        public string ReceiptNumber { get; set; } = string.Empty;

        [Required]
        public int PurchaseOrderId { get; set; }
        [Display(Name = "Purchase Order")]
        public PurchaseOrder? PurchaseOrder { get; set; }

        public ReceiptStatus Status { get; set; } = ReceiptStatus.Draft;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        [Required]
        [Display(Name = "Receipt Date")]
        [DataType(DataType.Date)]
        public DateTime ReceiptDate { get; set; } = DateTime.Today;

        [StringLength(100)]
        [Display(Name = "Received By")]
        public string? ReceivedBy { get; set; }

        [StringLength(100)]
        [Display(Name = "Shipping Carrier")]
        public string? ShippingCarrier { get; set; }

        [StringLength(100)]
        [Display(Name = "Tracking Number")]
        public string? TrackingNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Packing Slip #")]
        public string? PackingSlipNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Receiving Location")]
        public string? ReceivingLocation { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
    }

    public class GoodsReceiptLine
    {
        public int Id { get; set; }

        public int GoodsReceiptId { get; set; }
        public GoodsReceipt? GoodsReceipt { get; set; }

        public int PurchaseOrderLineId { get; set; }
        [Display(Name = "PO Line")]
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        public int LineNumber { get; set; }

        [Display(Name = "Quantity Received")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReceived { get; set; }

        [Display(Name = "Quantity Accepted")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityAccepted { get; set; }

        [Display(Name = "Quantity Rejected")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityRejected { get; set; }

        [StringLength(500)]
        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        [StringLength(100)]
        [Display(Name = "Storage Location")]
        public string? StorageLocation { get; set; }

        [Display(Name = "Receiving Location")]
        public int? ReceivingLocationId { get; set; }
        public Location? ReceivingLocation { get; set; }

        [StringLength(50)]
        [Display(Name = "Lot Number")]
        public string? LotNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? CipProjectId { get; set; }
        [Display(Name = "CIP Project")]
        public CipProject? CipProject { get; set; }

        public bool IsInvoiced { get; set; } = false;
    }
}
