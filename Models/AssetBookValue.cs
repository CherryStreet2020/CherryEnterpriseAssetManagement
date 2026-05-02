using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class AssetBookValue
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset Asset { get; set; } = null!;

        public int BookId { get; set; }
        public Book Book { get; set; } = null!;

        [DataType(DataType.Date)]
        public DateTime? DepreciationStartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? LastDepreciationRun { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AccumulatedDepreciation { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal YtdDepreciation { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentCost { get; set; } = 0m;
    }
}
