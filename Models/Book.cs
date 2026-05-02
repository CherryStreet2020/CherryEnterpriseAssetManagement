using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class Book
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DepreciationMethod Method { get; set; } = DepreciationMethod.StraightLine;
        public int? MethodLookupValueId { get; set; }
        public LookupValue? MethodLookupValue { get; set; }

        public DepreciationConvention Convention { get; set; } = DepreciationConvention.MidMonth;
        public int? ConventionLookupValueId { get; set; }
        public LookupValue? ConventionLookupValue { get; set; }

        public int? UsefulLifeOverrideMonths { get; set; }

        public int? DefaultPolicyId { get; set; }
        public DepreciationPolicy? DefaultPolicy { get; set; }

        [StringLength(50)]
        public string? GlAccountDepExp { get; set; }

        [StringLength(50)]
        public string? GlAccountAccumDep { get; set; }

        [StringLength(50)]
        public string? GlAccountGainOnDisposal { get; set; }

        [StringLength(50)]
        public string? GlAccountLossOnDisposal { get; set; }

        [StringLength(50)]
        public string? GlAccountAssetClearing { get; set; }

        [StringLength(50)]
        public string? GlAccountCIP { get; set; }

        public BookType BookType { get; set; } = BookType.Financial;

        public int? BookTypeLookupValueId { get; set; }
        public LookupValue? BookTypeLookupValue { get; set; }

        public TaxJurisdiction TaxJurisdiction { get; set; } = TaxJurisdiction.USA;
        public int? TaxJurisdictionLookupValueId { get; set; }
        public LookupValue? TaxJurisdictionLookupValue { get; set; }

        public bool IsPrimaryBook { get; set; } = false;

        public bool CalculateOnlyNoPosting { get; set; } = false;

        public bool AllowManualDepreciation { get; set; } = false;

        public bool TrackBudgetVsActual { get; set; } = false;

        public DepreciationFrequency CalculationFrequency { get; set; } = DepreciationFrequency.Monthly;
        public int? FrequencyLookupValueId { get; set; }
        public LookupValue? FrequencyLookupValue { get; set; }

        public bool RequireApprovalToPost { get; set; } = false;

        public bool AutoPostOnPeriodClose { get; set; } = false;

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public ICollection<BookGlAccount>? BookGlAccounts { get; set; }
        public ICollection<AssetBookSettings>? AssetBookSettings { get; set; }
        public ICollection<PolicyCategoryDefault>? PolicyCategoryDefaults { get; set; }
    }
}