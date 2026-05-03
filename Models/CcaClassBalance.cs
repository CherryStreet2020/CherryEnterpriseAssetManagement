using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class CcaClassBalance
    {
        public int Id { get; set; }

        [Required]
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        public int CcaClassId { get; set; }
        public CcaClass CcaClass { get; set; } = null!;

        [Required]
        public int FiscalYear { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OpeningUcc { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Additions { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Dispositions { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal HalfYearAdjustment { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseForCca { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CcaClaimed { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ClosingUcc { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? Recapture { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TerminalLoss { get; set; }

        public bool IsPosted { get; set; } = false;

        public DateTime? PostedDate { get; set; }

        [StringLength(100)]
        public string? PostedBy { get; set; }

        public int? DaysInFiscalPeriod { get; set; }

        public bool IsShortFiscalPeriod { get; set; } = false;
    }
}
