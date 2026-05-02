using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class ProjectManager
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        public int? DepartmentId { get; set; }
        public Department? DepartmentRef { get; set; }

        public int? CostCenterId { get; set; }
        public CostCenter? CostCenter { get; set; }

        [StringLength(100)]
        public string? Title { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool Active { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<CipProject>? Projects { get; set; }
    }
}
