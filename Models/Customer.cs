using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Projects;

namespace Abs.FixedAssets.Models
{
    public class Customer
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required, StringLength(20)]
        public string CustomerCode { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? ContactName { get; set; }

        [StringLength(100)]
        public string? ContactEmail { get; set; }

        [StringLength(20)]
        public string? ContactPhone { get; set; }

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(50)]
        public string? StateProvince { get; set; }

        [StringLength(20)]
        public string? PostalCode { get; set; }

        [StringLength(50)]
        public string? Country { get; set; }

        [StringLength(50)]
        public string? TaxId { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "USD";

        public int? PaymentTermId { get; set; }

        public bool IsActive { get; set; } = true;

        // ============================================================
        // Sprint 13.5 PRA-1 — defaults inheritance for CustomerProject
        // create form (PR #1.5 added these on CustomerProject; mirror
        // on Customer so project-create has something to copy from).
        // CHECK constraints match the CustomerProject ranges set in PR #1.5.
        // ============================================================

        public QualityProgram? DefaultQualityProgram { get; set; }
        public ExportControl? DefaultExportControl { get; set; }
        public ContractType? DefaultContractType { get; set; }
        public CustomerProjectRevenueMode? DefaultRevenueMode { get; set; }

        // Sprint 13.5 PRA-1 — regulator-issued identifiers
        [StringLength(10), Display(Name = "CAGE Code")]
        public string? CageCode { get; set; }

        [StringLength(13), Display(Name = "DUNS Number")]
        public string? DunsNumber { get; set; }

        // Sprint 13.5 PRA-1 — commercial details
        [Column(TypeName = "decimal(18,2)"), Display(Name = "Credit Limit")]
        public decimal? CreditLimit { get; set; }

        public int? TaxCodeId { get; set; }

        // Sprint 13.5 PRA-1 — bill-to address block (separate from
        // ship-to address above). Many commercial customers have
        // billing addresses distinct from delivery.
        [StringLength(200), Display(Name = "Bill-To Address")]
        public string? BillToAddress { get; set; }

        [StringLength(100), Display(Name = "Bill-To City")]
        public string? BillToCity { get; set; }

        [StringLength(50), Display(Name = "Bill-To State/Province")]
        public string? BillToStateProvince { get; set; }

        [StringLength(20), Display(Name = "Bill-To Postal Code")]
        public string? BillToPostalCode { get; set; }

        [StringLength(50), Display(Name = "Bill-To Country")]
        public string? BillToCountry { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<CustomerInvoice> Invoices { get; set; } = new List<CustomerInvoice>();
    }

    public class CustomerInvoice
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Required, StringLength(30)]
        public string InvoiceNumber { get; set; } = string.Empty;

        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        public DateTime? DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceDue { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Draft";

        public int? StatusLookupValueId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(50)]
        public string? PurchaseOrderRef { get; set; }

        public int? SiteId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<CustomerInvoiceLine> Lines { get; set; } = new List<CustomerInvoiceLine>();
    }

    public class CustomerInvoiceLine
    {
        public int Id { get; set; }

        public int CustomerInvoiceId { get; set; }
        public CustomerInvoice? Invoice { get; set; }

        public int LineNumber { get; set; }

        [StringLength(200)]
        public string? Description { get; set; }

        public int? ItemId { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LineTotal { get; set; }

        [StringLength(20)]
        public string? UOM { get; set; }

        public int? GlAccountId { get; set; }
    }
}
