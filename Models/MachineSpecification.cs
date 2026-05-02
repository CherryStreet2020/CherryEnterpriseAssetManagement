using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class MachineSpecification
    {
        public int Id { get; set; }

        [Required]
        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        [StringLength(20)]
        public string? MachineTypeCode { get; set; }
        public int? MachineTypeLookupValueId { get; set; }

        [StringLength(100)]
        public string? CncControlSystem { get; set; }
        public bool FiveAxisCapable { get; set; }
        public bool SyncFeedTapping { get; set; }
        public bool CoolantThroughSpindle { get; set; }
        public int? AtcPocketCount { get; set; }

        [StringLength(20)]
        public string? SpindleTaper { get; set; }
        public int? SpindleTaperLookupValueId { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? SpindleDiameterMm { get; set; }
        public int? MaxSpindleSpeedRpm { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal? SpindleMotorHp { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? XAxisTravelMm { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? YAxisTravelMm { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? ZAxisTravelMm { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? WAxisTravelMm { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal? MaxSwingDiameterMm { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? MaxBetweenColumnsMm { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? MaxHeightTableToRamMm { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? MaxCuttingDiameterMm { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? MaxCuttingLengthMm { get; set; }

        [StringLength(50)]
        public string? TableSize { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? TableWeightCapacityKg { get; set; }
        [Column(TypeName = "decimal(12,2)")]
        public decimal? MachineWeightCapacityKg { get; set; }

        [StringLength(500)]
        public string? EquippedHeads { get; set; }
        [StringLength(100)]
        public string? ProbingSystem { get; set; }
        [StringLength(500)]
        public string? DetailsSketchLink { get; set; }
        [StringLength(500)]
        public string? Comments { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedAt { get; set; }

        public int TenantId { get; set; } = 1;
        public int? CompanyId { get; set; }
    }
}
