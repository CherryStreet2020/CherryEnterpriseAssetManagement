using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public enum InvoiceStatus
    {
        Draft = 0,
        PendingApproval = 1,
        Approved = 2,
        PartiallyPaid = 3,
        Paid = 4,
        OnHold = 5,
        Voided = 6
    }

    public enum InvoiceMatchStatus
    {
        NotMatched = 0,
        PartialMatch = 1,
        FullyMatched = 2,
        Exception = 3
    }

    public class VendorInvoice
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Invoice Number")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Required]
        public int VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        [Display(Name = "Match Status")]
        public InvoiceMatchStatus MatchStatus { get; set; } = InvoiceMatchStatus.NotMatched;

        [Required]
        [Display(Name = "Invoice Date")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [Display(Name = "Received Date")]
        [DataType(DataType.Date)]
        public DateTime ReceivedDate { get; set; } = DateTime.Today;

        [Display(Name = "Due Date")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        public PaymentTerms PaymentTerms { get; set; } = PaymentTerms.Net30;

        [Required, StringLength(3)]
        public string Currency { get; set; } = "USD";

        [Display(Name = "Subtotal")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Display(Name = "Tax Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Display(Name = "Shipping")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingAmount { get; set; }

        [Display(Name = "Total")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        [Display(Name = "Amount Paid")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Display(Name = "Balance Due")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceDue { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(500)]
        [Display(Name = "Internal Notes")]
        public string? InternalNotes { get; set; }

        public int? ApprovedById { get; set; }
        [Display(Name = "Approved By")]
        public User? ApprovedBy { get; set; }

        [Display(Name = "Approved Date")]
        public DateTime? ApprovedAt { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<VendorInvoiceLine> Lines { get; set; } = new List<VendorInvoiceLine>();
        public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
    }

    public class VendorInvoiceLine
    {
        public int Id { get; set; }

        public int VendorInvoiceId { get; set; }
        public VendorInvoice? VendorInvoice { get; set; }

        public int LineNumber { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; } = 1;

        [Display(Name = "Unit Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Line Total")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        public int? PurchaseOrderLineId { get; set; }
        [Display(Name = "PO Line")]
        public PurchaseOrderLine? PurchaseOrderLine { get; set; }

        public int? GoodsReceiptLineId { get; set; }
        [Display(Name = "Receipt Line")]
        public GoodsReceiptLine? GoodsReceiptLine { get; set; }

        public int? GlAccountId { get; set; }
        [Display(Name = "GL Account")]
        public GlAccount? GlAccount { get; set; }

        public int? CostCenterId { get; set; }
        [Display(Name = "Cost Center")]
        public CostCenter? CostCenter { get; set; }

        public int? CipProjectId { get; set; }
        [Display(Name = "CIP Project")]
        public CipProject? CipProject { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class InvoicePayment
    {
        public int Id { get; set; }

        public int VendorInvoiceId { get; set; }
        public VendorInvoice? VendorInvoice { get; set; }

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Display(Name = "Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(50)]
        [Display(Name = "Payment Method")]
        public string? PaymentMethod { get; set; }

        [StringLength(50)]
        [Display(Name = "Reference Number")]
        public string? ReferenceNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
