using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class LaborType
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public LaborCategory Category { get; set; } = LaborCategory.Regular;

        public decimal MultiplierRate { get; set; } = 1.0m;

        public bool IsBillable { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public enum LaborCategory
    {
        Regular = 0,
        Overtime = 1,
        DoubleTime = 2,
        Holiday = 3,
        OnCall = 4,
        Training = 5,
        Travel = 6,
        Administrative = 7
    }

    public class LaborRate
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public int? CraftId { get; set; }

        public Craft? Craft { get; set; }

        public int? SkillId { get; set; }

        public Skill? Skill { get; set; }

        public decimal StandardRate { get; set; } = 50.00m;

        public decimal OvertimeRate { get; set; } = 75.00m;

        public decimal DoubleTimeRate { get; set; } = 100.00m;

        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpirationDate { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;
    }

    public class Craft
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public decimal DefaultHourlyRate { get; set; } = 50.00m;

        public bool RequiresCertification { get; set; } = false;

        [StringLength(500)]
        public string? RequiredCertifications { get; set; }

        public bool IsInternal { get; set; } = true;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public ICollection<LaborRate>? LaborRates { get; set; }
    }

    public class Skill
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public SkillLevel Level { get; set; } = SkillLevel.Intermediate;

        public int? CraftId { get; set; }

        public Craft? Craft { get; set; }

        public bool RequiresTraining { get; set; } = false;

        public int? TrainingHoursRequired { get; set; }

        public bool RequiresCertification { get; set; } = false;

        [StringLength(255)]
        public string? CertificationName { get; set; }

        public int? CertificationValidityMonths { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public ICollection<LaborRate>? LaborRates { get; set; }
    }

    public enum SkillLevel
    {
        Entry = 0,
        Intermediate = 1,
        Advanced = 2,
        Expert = 3,
        Master = 4
    }
}
