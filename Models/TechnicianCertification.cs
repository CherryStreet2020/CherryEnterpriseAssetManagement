using System.ComponentModel.DataAnnotations;

namespace Abs.FixedAssets.Models
{
    public class TechnicianCertification
    {
        public int Id { get; set; }

        public int TechnicianId { get; set; }
        public Technician? Technician { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = "";

        [StringLength(50)]
        public string? CertificateNumber { get; set; }

        [StringLength(100)]
        public string? IssuingAuthority { get; set; }

        public DateTime? IssueDate { get; set; }
        public DateTime? ExpirationDate { get; set; }

        public bool IsRequired { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? TenantId { get; set; }
    }
}
