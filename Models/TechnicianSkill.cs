using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class TechnicianSkill
    {
        public int Id { get; set; }

        public int TechnicianId { get; set; }
        public Technician? Technician { get; set; }

        [Required, StringLength(100)]
        public string SkillName { get; set; } = "";

        [StringLength(50)]
        public string? Category { get; set; }

        public int ProficiencyLevel { get; set; }

        public bool IsCertified { get; set; }

        public DateTime? LastAssessedDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? TenantId { get; set; }
    }
}
