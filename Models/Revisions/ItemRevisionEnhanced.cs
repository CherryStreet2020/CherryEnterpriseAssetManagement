using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Revisions
{
    public class ItemManufacturerPart
    {
        public int Id { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public int ManufacturerId { get; set; }
        public Manufacturer? Manufacturer { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Manufacturer Part Number")]
        public string MfrPartNumber { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(50)]
        [Display(Name = "Lifecycle Status")]
        public string? LifecycleStatus { get; set; }

        [StringLength(500)]
        [Display(Name = "Datasheet URL")]
        public string? DatasheetUrl { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }

        public ICollection<VendorItemPart>? VendorItemParts { get; set; }
    }

    public class VendorItemPart
    {
        public int Id { get; set; }

        public int VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Vendor Part Number")]
        public string VendorPartNumber { get; set; } = string.Empty;

        public int? ItemManufacturerPartId { get; set; }
        [Display(Name = "Manufacturer Part")]
        public ItemManufacturerPart? ItemManufacturerPart { get; set; }

        [StringLength(20)]
        [Display(Name = "Vendor UOM")]
        public string? VendorUom { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Pack Quantity")]
        public decimal? PackQty { get; set; }

        [Display(Name = "Lead Time (Days)")]
        public int? LeadTimeDays { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Min Order Quantity")]
        public decimal? MinOrderQty { get; set; }

        [Display(Name = "Preferred")]
        public bool Preferred { get; set; } = false;

        [Column(TypeName = "decimal(18,4)")]
        [Display(Name = "Unit Price")]
        public decimal? UnitPrice { get; set; }

        [Display(Name = "Price Effective Date")]
        public DateTime? PriceEffectiveDate { get; set; }

        [StringLength(500)]
        [Display(Name = "Product Page URL")]
        public string? ProductPageUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "Catalog URL")]
        public string? CatalogUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "Datasheet URL")]
        public string? DatasheetUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "External Image URL")]
        public string? ExternalImageUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "Enrichment Notes")]
        public string? EnrichmentNotes { get; set; }

        [StringLength(100)]
        [Display(Name = "Extracted MPN")]
        public string? ExtractedMpn { get; set; }

        [StringLength(100)]
        [Display(Name = "Extracted SKU")]
        public string? ExtractedSku { get; set; }

        public DateTime? LastEnrichedUtc { get; set; }

        [StringLength(50)]
        [Display(Name = "Enrich Status")]
        public string? LastEnrichStatus { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAtUtc { get; set; }

        [StringLength(100)]
        public string? CreatedBy { get; set; }
    }
}
