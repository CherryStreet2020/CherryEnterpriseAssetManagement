using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class PartialDisposal
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        [DataType(DataType.Date)]
        public DateTime DisposalDate { get; set; }

        [Column(TypeName = "decimal(5,4)")]
        public decimal PercentageDisposed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalCostDisposed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedDepreciationDisposed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BookValueDisposed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SaleProceeds { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GainLoss { get; set; }

        public DisposalReason Reason { get; set; } = DisposalReason.Sale;
        public int? ReasonLookupValueId { get; set; }
        public LookupValue? ReasonLookupValue { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(100)]
        public string? Buyer { get; set; }

        [StringLength(50)]
        public string? ReferenceNumber { get; set; }

        public int? JournalEntryId { get; set; }

        [StringLength(100)]
        public string? ProcessedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BulkOperation
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public int? TenantId { get; set; }

        public BulkOperationType OperationType { get; set; }

        [DataType(DataType.Date)]
        public DateTime OperationDate { get; set; } = DateTime.UtcNow;

        public int AssetsAffected { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? NewLocation { get; set; }

        [StringLength(100)]
        public string? NewDepartment { get; set; }

        public AssetStatus? NewStatus { get; set; }

        [StringLength(100)]
        public string? ProcessedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? AssetIds { get; set; }
    }

    public enum DisposalReason
    {
        Sale = 0,
        Scrapped = 1,
        Donated = 2,
        TradeIn = 3,
        Stolen = 4,
        Destroyed = 5,
        Other = 6
    }

    public enum BulkOperationType
    {
        Transfer = 0,
        StatusChange = 1,
        DepreciationAdjustment = 2,
        LocationChange = 3,
        DepartmentChange = 4
    }
}
