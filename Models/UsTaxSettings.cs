using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class UsTaxSettings
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        public MacrsPropertyClass PropertyClass { get; set; } = MacrsPropertyClass.SevenYear;

        public MacrsConvention Convention { get; set; } = MacrsConvention.HalfYear;

        public bool UseADS { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Section179Amount { get; set; } = 0;

        public bool Section179Elected { get; set; } = false;

        [Column(TypeName = "decimal(5,2)")]
        public decimal BonusDepreciationPercent { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BonusDepreciationAmount { get; set; } = 0;

        public bool QualifiedImprovementProperty { get; set; } = false;

        public bool ListedProperty { get; set; } = false;

        [Column(TypeName = "decimal(5,2)")]
        public decimal BusinessUsePercent { get; set; } = 100;

        [DataType(DataType.Date)]
        public DateTime? PlacedInServiceDate { get; set; }

        public int TaxYear { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DepreciableBasis { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedTaxDepreciation { get; set; } = 0;

        [StringLength(200)]
        public string? Notes { get; set; }
    }

    public enum MacrsPropertyClass
    {
        ThreeYear = 3,
        FiveYear = 5,
        SevenYear = 7,
        TenYear = 10,
        FifteenYear = 15,
        TwentyYear = 20,
        TwentySevenAndHalfYear = 27,  // Residential rental
        ThirtyNineYear = 39,          // Nonresidential real property
        FortyYear = 40                // ADS recovery period for certain property
    }

    public enum MacrsConvention
    {
        HalfYear = 0,      // Most personal property
        MidQuarter = 1,    // When >40% placed in service in Q4
        MidMonth = 2       // Real property
    }

    public class Section179Limits
    {
        public int Id { get; set; }
        public int TaxYear { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxDeduction { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PhaseoutThreshold { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SuvLimit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AutoDepreciationCap { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TruckDepreciationCap { get; set; }
    }

    public class BonusDepreciationRates
    {
        public int Id { get; set; }
        public int TaxYear { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal Rate { get; set; }

        [StringLength(200)]
        public string? Notes { get; set; }
    }
}
