using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class CcaClass
    {
        public int Id { get; set; }

        [Required]
        public int ClassNumber { get; set; }

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(7,4)")]
        public decimal Rate { get; set; }

        public bool IsDecliningBalance { get; set; } = true;

        public bool HalfYearRuleApplies { get; set; } = true;

        public bool IsAcceleratedInvestmentIncentive { get; set; } = false;

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool Active { get; set; } = true;

        public ICollection<AssetTaxSettings> AssetTaxSettings { get; set; } = new List<AssetTaxSettings>();
        public ICollection<CcaClassBalance> ClassBalances { get; set; } = new List<CcaClassBalance>();
    }
}
