using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class CcaTransaction
    {
        public int Id { get; set; }

        public int CcaClassId { get; set; }
        public CcaClass CcaClass { get; set; } = null!;

        public int? AssetId { get; set; }
        public Asset? Asset { get; set; }

        public int FiscalYear { get; set; }

        public CcaTransactionType TransactionType { get; set; }

        [DataType(DataType.Date)]
        public DateTime TransactionDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? AvailableForUseDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CapitalCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Proceeds { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AdjustedCostBase { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAddition { get; set; }

        public bool SubjectToHalfYearRule { get; set; } = true;

        public bool IsAcceleratedIncentiveEligible { get; set; } = false;

        [StringLength(200)]
        public string? Description { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }

    public enum CcaTransactionType
    {
        Addition = 0,
        Disposition = 1,
        Adjustment = 2,
        Recapture = 3,
        TerminalLoss = 4
    }
}
