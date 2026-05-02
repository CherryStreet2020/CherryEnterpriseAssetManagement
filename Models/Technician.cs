using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class Technician
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Email { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [StringLength(100)]
        public string? Specialty { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        public int? DepartmentId { get; set; }
        public Department? DepartmentRef { get; set; }

        public int? CostCenterId { get; set; }
        public CostCenter? CostCenter { get; set; }

        public decimal? HourlyRate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool Active { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<MaintenanceEvent>? MaintenanceEvents { get; set; }

        [StringLength(30)]
        public string? EmployeeId { get; set; }

        [StringLength(500)]
        public string? PhotoPath { get; set; }

        [StringLength(100)]
        public string? Title { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? SiteId { get; set; }
        public Site? Site { get; set; }

        public int? SupervisorTechnicianId { get; set; }
        public Technician? Supervisor { get; set; }

        [StringLength(50)]
        public string? PrimaryCraft { get; set; }

        [StringLength(50)]
        public string? SecondaryCraft { get; set; }

        public int ProficiencyLevel { get; set; }

        [StringLength(30)]
        public string? ShiftPattern { get; set; }

        public TimeOnly? ShiftStart { get; set; }
        public TimeOnly? ShiftEnd { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? OvertimeRate { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DoubleTimeRate { get; set; }

        public DateTime? HireDate { get; set; }

        [StringLength(100)]
        public string? EmergencyContactName { get; set; }

        [StringLength(30)]
        public string? EmergencyContactPhone { get; set; }

        public int? TenantId { get; set; }

        public ICollection<TechnicianCertification>? Certifications { get; set; }
        public ICollection<TechnicianSkill>? Skills { get; set; }
    }
}
