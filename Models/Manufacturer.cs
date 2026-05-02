using System.ComponentModel.DataAnnotations;
using Abs.FixedAssets.Models.Revisions;

namespace Abs.FixedAssets.Models
{
    public class Manufacturer
    {
        public int Id { get; set; }

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        [Required, StringLength(20)]
        [Display(Name = "Manufacturer Code")]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Website { get; set; }

        [StringLength(100)]
        public string? Country { get; set; }

        [StringLength(100)]
        public string? ContactName { get; set; }

        [StringLength(100)]
        public string? ContactEmail { get; set; }

        [StringLength(30)]
        public string? ContactPhone { get; set; }

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool Active { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Asset>? Assets { get; set; }
        public ICollection<ItemManufacturerPart>? ManufacturerParts { get; set; }
    }
}
