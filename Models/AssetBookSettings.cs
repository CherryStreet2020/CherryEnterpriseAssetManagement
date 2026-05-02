using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class AssetBookSettings
    {
        public int Id { get; set; }

        [Required]
        public int AssetId { get; set; }

        [ForeignKey("AssetId")]
        public Asset Asset { get; set; } = null!;

        [Required]
        public int BookId { get; set; }

        [ForeignKey("BookId")]
        public Book Book { get; set; } = null!;

        public DepreciationMethod? MethodOverride { get; set; }

        public DepreciationConvention? ConventionOverride { get; set; }

        public int? UsefulLifeMonthsOverride { get; set; }

        public decimal? SalvageValueOverride { get; set; }

        public DateTime? InServiceDateOverride { get; set; }

        public decimal? CostBasisOverride { get; set; }

        public decimal? Section179Deduction { get; set; }

        public decimal? BonusDepreciationPercent { get; set; }

        public bool IsExcludedFromBook { get; set; } = false;

        [StringLength(500)]
        public string? Notes { get; set; }

        public decimal AccumulatedDepreciation { get; set; } = 0;

        public decimal BookValue { get; set; } = 0;

        public DateTime? LastDepreciationDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public DepreciationMethod EffectiveMethod => MethodOverride ?? Book?.Method ?? DepreciationMethod.StraightLine;

        [NotMapped]
        public DepreciationConvention EffectiveConvention => ConventionOverride ?? Book?.Convention ?? DepreciationConvention.MidMonth;

        [NotMapped]
        public int EffectiveUsefulLifeMonths => UsefulLifeMonthsOverride ?? Book?.UsefulLifeOverrideMonths ?? Asset?.UsefulLifeMonths ?? 60;

        [NotMapped]
        public decimal EffectiveSalvageValue => SalvageValueOverride ?? Asset?.SalvageValue ?? 0;

        [NotMapped]
        public decimal EffectiveCostBasis => CostBasisOverride ?? Asset?.AcquisitionCost ?? 0;

        [NotMapped]
        public DateTime EffectiveInServiceDate => InServiceDateOverride ?? Asset?.InServiceDate ?? DateTime.MinValue;
    }
}
