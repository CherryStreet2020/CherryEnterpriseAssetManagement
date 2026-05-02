using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class DepreciationPolicy
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DepreciationMethod Method { get; set; } = DepreciationMethod.StraightLine;

        public DepreciationConvention Convention { get; set; } = DepreciationConvention.HalfYear;

        public int DefaultUsefulLifeMonths { get; set; } = 60;

        public decimal DefaultSalvagePercent { get; set; } = 0;

        public decimal DefaultSalvageAmount { get; set; } = 0;

        public SalvageValueType SalvageType { get; set; } = SalvageValueType.Percentage;

        public bool SwitchToStraightLine { get; set; } = true;

        public int? SwitchToSLInYear { get; set; }

        public AveragingMethod AveragingMethod { get; set; } = AveragingMethod.Monthly;

        public decimal? DecliningBalanceRate { get; set; }

        public bool ApplySection179 { get; set; } = false;

        public decimal? DefaultSection179Percent { get; set; }

        public bool ApplyBonusDepreciation { get; set; } = false;

        public decimal? DefaultBonusPercent { get; set; }

        public decimal MinimumBookValue { get; set; } = 0.01m;

        public bool AllowNegativeDepreciation { get; set; } = false;

        public DepreciationRounding Rounding { get; set; } = DepreciationRounding.ToCents;

        public ProrateMethod FirstYearProrate { get; set; } = ProrateMethod.ByConvention;

        public ProrateMethod LastYearProrate { get; set; } = ProrateMethod.ByConvention;

        public DepreciationFrequency Frequency { get; set; } = DepreciationFrequency.Monthly;

        public bool DepreciateInServiceMonth { get; set; } = true;

        public bool DepreciateInDisposalMonth { get; set; } = false;

        public bool CalculateToEndOfLife { get; set; } = true;

        public bool TrackUnitsOfProduction { get; set; } = false;

        public int? EstimatedTotalUnits { get; set; }

        public int? CcaClassId { get; set; }
        public CcaClass? CcaClass { get; set; }

        public int? MacrsRecoveryPeriodYears { get; set; }

        public MacrsPropertyType? MacrsPropertyType { get; set; }

        public bool MacrsUseADS { get; set; } = false;

        public BookType ApplicableBookType { get; set; } = BookType.Financial;

        public TaxJurisdiction TaxJurisdiction { get; set; } = TaxJurisdiction.USA;

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public bool IsSystemPolicy { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public DateTime? ModifiedAt { get; set; }

        [StringLength(100)]
        public string? ModifiedBy { get; set; }

        public ICollection<AssetCategory>? AssetCategories { get; set; }
    }

    public class UsefulLifeTable
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public TaxJurisdiction Jurisdiction { get; set; } = TaxJurisdiction.USA;

        public LifeTableSource Source { get; set; } = LifeTableSource.IRS;

        public bool IsActive { get; set; } = true;

        public ICollection<UsefulLifeEntry>? Entries { get; set; }
    }

    public class UsefulLifeEntry
    {
        public int Id { get; set; }

        public int UsefulLifeTableId { get; set; }
        public UsefulLifeTable? UsefulLifeTable { get; set; }

        [Required, StringLength(50)]
        public string AssetClassCode { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string AssetClassName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int GaapLifeMonths { get; set; }

        public int? TaxLifeMonths { get; set; }

        public int? MacrsRecoveryYears { get; set; }

        public int? CcaClassNumber { get; set; }

        public decimal? CcaRate { get; set; }

        public DepreciationMethod RecommendedMethod { get; set; } = DepreciationMethod.StraightLine;

        public DepreciationConvention RecommendedConvention { get; set; } = DepreciationConvention.HalfYear;

        [StringLength(100)]
        public string? IrsAssetClass { get; set; }

        [StringLength(100)]
        public string? CraAssetClass { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public class PolicyCategoryDefault
    {
        public int Id { get; set; }

        public int DepreciationPolicyId { get; set; }
        public DepreciationPolicy? DepreciationPolicy { get; set; }

        public int AssetCategoryId { get; set; }
        public AssetCategory? AssetCategory { get; set; }

        public int BookId { get; set; }
        public Book? Book { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int Priority { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }

    public enum SalvageValueType
    {
        Percentage = 0,
        FixedAmount = 1,
        None = 2
    }

    public enum AveragingMethod
    {
        Monthly = 0,
        Daily = 1,
        Annual = 2,
        Quarterly = 3
    }

    public enum DepreciationRounding
    {
        ToCents = 0,
        ToDollars = 1,
        NoRounding = 2,
        ToThousands = 3
    }

    public enum ProrateMethod
    {
        ByConvention = 0,
        FullPeriod = 1,
        NoPeriod = 2,
        ExactDays = 3
    }

    public enum DepreciationFrequency
    {
        Monthly = 0,
        Quarterly = 1,
        SemiAnnually = 2,
        Annually = 3,
        Daily = 4
    }

    public enum MacrsPropertyType
    {
        PersonalProperty = 0,
        RealProperty = 1,
        QualifiedImprovementProperty = 2,
        ListedProperty = 3,
        FarmProperty = 4
    }

    public enum LifeTableSource
    {
        IRS = 0,
        CRA = 1,
        Company = 2,
        Industry = 3,
        IFRS = 4
    }
}
