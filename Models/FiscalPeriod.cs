using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class FiscalYear
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required]
        public int Year { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Fiscal Year Name")]
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        public FiscalYearStatus Status { get; set; } = FiscalYearStatus.Open;

        public bool IsShortYear { get; set; } = false;

        [Display(Name = "Number of Periods")]
        public int NumberOfPeriods { get; set; } = 12;

        public AccountingPeriodType PeriodType { get; set; } = AccountingPeriodType.Standard12Month;

        [Display(Name = "Adjustment Period")]
        public bool HasAdjustmentPeriod { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }

        [StringLength(100)]
        public string? ClosedBy { get; set; }

        public ICollection<FiscalPeriod> Periods { get; set; } = new List<FiscalPeriod>();
    }

    public class FiscalPeriod
    {
        public int Id { get; set; }

        public int FiscalYearId { get; set; }
        public FiscalYear? FiscalYear { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        [Required]
        [Display(Name = "Period Number")]
        public int PeriodNumber { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Period Name")]
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        public PeriodStatus Status { get; set; } = PeriodStatus.Open;

        public bool IsAdjustmentPeriod { get; set; } = false;

        [Display(Name = "Days in Period")]
        public int DaysInPeriod { get; set; }

        [Display(Name = "Depreciation Calculated")]
        public bool DepreciationCalculated { get; set; } = false;

        [Display(Name = "Depreciation Posted")]
        public bool DepreciationPosted { get; set; } = false;

        public DateTime? ClosedAt { get; set; }

        [StringLength(100)]
        public string? ClosedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum FiscalYearStatus
    {
        Future = 0,
        Open = 1,
        Closed = 2,
        Locked = 3
    }

    public class DepreciationRun
    {
        public int Id { get; set; }

        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int FiscalPeriodId { get; set; }
        public FiscalPeriod? FiscalPeriod { get; set; }

        public int BookId { get; set; }
        public Book? Book { get; set; }

        [Display(Name = "Run Date")]
        public DateTime RunDate { get; set; } = DateTime.UtcNow;

        public DepreciationRunStatus Status { get; set; } = DepreciationRunStatus.Draft;

        [Display(Name = "Assets Processed")]
        public int AssetsProcessed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Depreciation")]
        public decimal TotalDepreciation { get; set; }

        [Display(Name = "Posted Date")]
        public DateTime? PostedDate { get; set; }

        [StringLength(100)]
        public string? PostedBy { get; set; }

        [Display(Name = "Reversed Date")]
        public DateTime? ReversedDate { get; set; }

        [StringLength(100)]
        public string? ReversedBy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public ICollection<DepreciationRunDetail> Details { get; set; } = new List<DepreciationRunDetail>();
    }

    public class DepreciationRunDetail
    {
        public int Id { get; set; }

        public int DepreciationRunId { get; set; }
        public DepreciationRun? DepreciationRun { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Beginning Book Value")]
        public decimal BeginningBookValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Depreciation Amount")]
        public decimal DepreciationAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Ending Book Value")]
        public decimal EndingBookValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "YTD Depreciation")]
        public decimal YtdDepreciation { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Life-to-Date Depreciation")]
        public decimal LtdDepreciation { get; set; }

        public DepreciationMethod MethodUsed { get; set; }

        [Display(Name = "Remaining Life (Months)")]
        public int RemainingLifeMonths { get; set; }

        [StringLength(200)]
        public string? Notes { get; set; }
    }
}
