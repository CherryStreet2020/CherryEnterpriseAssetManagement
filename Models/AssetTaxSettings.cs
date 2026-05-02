using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class AssetTaxSettings
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset Asset { get; set; } = null!;

        public int CcaClassId { get; set; }
        public CcaClass CcaClass { get; set; } = null!;

        [DataType(DataType.Date)]
        public DateTime? AvailableForUseDate { get; set; }

        public bool AvailableForUseOverride { get; set; } = false;

        public bool EligibleForAcceleratedIncentive { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CapitalCost { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Proceeds { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DisposalDate { get; set; }

        public DisposalType? DisposalType { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public enum DisposalType
    {
        Sale = 0,
        Scrap = 1,
        Abandon = 2,
        TradeIn = 3,
        Theft = 4,
        Destruction = 5
    }
}
