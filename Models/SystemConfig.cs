using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class NumberingSequence
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(20)]
        public string Prefix { get; set; } = string.Empty;

        [StringLength(20)]
        public string Suffix { get; set; } = string.Empty;

        public int NextNumber { get; set; } = 1;

        public int NumberLength { get; set; } = 6;

        public bool PadWithZeros { get; set; } = true;

        public bool IncludeYear { get; set; } = false;

        public bool IncludeMonth { get; set; } = false;

        public bool ResetYearly { get; set; } = false;

        public bool ResetMonthly { get; set; } = false;

        public int? LastResetYear { get; set; }

        public int? LastResetMonth { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }

    public class PaymentTerm
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public int DueDays { get; set; } = 30;

        public decimal DiscountPercent { get; set; } = 0;

        public int DiscountDays { get; set; } = 0;

        public bool IsDefault { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public class UOMDefinition
    {
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Description { get; set; }

        public UOMType Type { get; set; } = UOMType.Count;

        public bool IsBaseUnit { get; set; } = true;

        public int? BaseUnitId { get; set; }

        public decimal ConversionFactor { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum UOMType
    {
        Count = 0,
        Length = 1,
        Area = 2,
        Volume = 3,
        Weight = 4,
        Time = 5,
        Other = 99
    }

    public class Currency
    {
        public int Id { get; set; }

        [Required]
        [StringLength(3)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [StringLength(5)]
        public string Symbol { get; set; } = "$";

        public int DecimalPlaces { get; set; } = 2;

        public bool IsBaseCurrency { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public class TaxCode
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public decimal Rate { get; set; } = 0;

        public TaxType Type { get; set; } = TaxType.Sales;

        [StringLength(50)]
        public string? TaxAuthority { get; set; }

        public int? GlAccountId { get; set; }

        public bool IsRecoverable { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum TaxType
    {
        Sales = 0,
        Purchase = 1,
        Use = 2,
        VAT = 3,
        GST = 4,
        HST = 5,
        PST = 6,
        Excise = 7
    }

    public class ShippingMethod
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Carrier { get; set; }

        // Sprint 13.5 PRA-1 — FK to first-class Carrier master. Existing
        // free-text Carrier column stays for back-compat. Service layer
        // resolves text → CarrierId where possible.
        public int? CarrierId { get; set; }
        public Carrier? CarrierRef { get; set; }

        public int EstimatedDays { get; set; } = 5;

        public decimal? DefaultCost { get; set; }

        public bool IsDefault { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public class ApprovalWorkflow
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public WorkflowType Type { get; set; } = WorkflowType.PurchaseOrder;

        public decimal ThresholdAmount { get; set; } = 0;

        public int RequiredApprovals { get; set; } = 1;

        [StringLength(500)]
        public string? ApproverRoles { get; set; }

        [StringLength(500)]
        public string? ApproverUserIds { get; set; }

        public bool RequireSequentialApproval { get; set; } = false;

        public bool AutoApproveIfBelowThreshold { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum WorkflowType
    {
        PurchaseOrder = 0,
        PurchaseRequisition = 1,
        WorkOrder = 2,
        AssetDisposal = 3,
        AssetTransfer = 4,
        CapitalProject = 5,
        Invoice = 6,
        JournalEntry = 7
    }
}
