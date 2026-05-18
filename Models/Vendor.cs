using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Revisions;

namespace Abs.FixedAssets.Models
{
    public enum VendorStatus
    {
        Active = 0,
        Inactive = 1,
        OnHold = 2,
        Blocked = 3
    }

    public enum VendorType
    {
        Supplier = 0,
        Contractor = 1,
        ServiceProvider = 2,
        Manufacturer = 3,
        Distributor = 4
    }

    public enum PaymentTerms
    {
        Net30 = 0,
        Net45 = 1,
        Net60 = 2,
        Net90 = 3,
        DueOnReceipt = 4,
        Prepaid = 5,
        COD = 6
    }

    public class Vendor
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Vendor Code")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Vendor Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Legal Name")]
        public string? LegalName { get; set; }

        [Display(Name = "Vendor Type")]
        public VendorType VendorType { get; set; } = VendorType.Supplier;

        public VendorStatus Status { get; set; } = VendorStatus.Active;

        [StringLength(100)]
        [Display(Name = "Primary Contact")]
        public string? ContactName { get; set; }

        [StringLength(50)]
        public string? Phone { get; set; }

        [StringLength(20)]
        public string? Fax { get; set; }

        [StringLength(100)]
        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(200)]
        public string? Website { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? City { get; set; }

        [StringLength(50)]
        [Display(Name = "State/Province")]
        public string? State { get; set; }

        [StringLength(20)]
        [Display(Name = "Postal Code")]
        public string? PostalCode { get; set; }

        [StringLength(50)]
        public string? Country { get; set; } = "United States";

        [StringLength(50)]
        [Display(Name = "Tax ID / EIN")]
        public string? TaxId { get; set; }

        [Display(Name = "Payment Terms")]
        public PaymentTerms PaymentTerms { get; set; } = PaymentTerms.Net30;

        [Required, StringLength(3)]
        public string Currency { get; set; } = "USD";

        [Display(Name = "Credit Limit")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? CreditLimit { get; set; }

        [Display(Name = "Account Number")]
        [StringLength(50)]
        public string? AccountNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? DefaultGlAccountId { get; set; }
        [Display(Name = "Default GL Account")]
        public GlAccount? DefaultGlAccount { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        [Display(Name = "Preferred Vendor")]
        public bool IsPreferred { get; set; } = false;

        [Display(Name = "1099 Vendor")]
        public bool Is1099Vendor { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }

        // ADR-015 — Per-supplier default Attributes JSON merged into
        // every receipt against this supplier at PO-receipt time. E.g.
        // a steel mill's default `{"mill":"Nucor Steel — Decatur, AL"}`,
        // a pharma distributor's GFSI cert #, an electronics supplier's
        // default MSL level. Profile-shape; validated downstream against
        // the receipt's ReceiptProfile.JsonSchema at create time.
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "jsonb")]
        public string? DefaultReceiptAttributes { get; set; }

        // ADR-015 — Does this supplier send Advance Ship Notices?
        // Drives the receiving inbox (PO Inbox shows ASN-expected line
        // items differently from no-ASN PO lines).
        public bool SendsAsn { get; set; } = false;

        // ASN format: "EDI856", "EPCIS", "CSV", "NONE". Drives the ASN
        // ingestion pipeline (downstream sprint).
        [System.ComponentModel.DataAnnotations.StringLength(32)]
        public string? AsnFormat { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Asset>? Assets { get; set; }
        public ICollection<PurchaseOrder>? PurchaseOrders { get; set; }
        public ICollection<VendorInvoice>? Invoices { get; set; }
        public ICollection<VendorItemPart>? VendorItemParts { get; set; }
    }
}
