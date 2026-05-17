using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class Attachment
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public int? TenantId { get; set; }

        public int? AssetId { get; set; }
        public Asset? Asset { get; set; }

        public int? WorkOrderId { get; set; }
        public WorkOrder? WorkOrder { get; set; }

        public int? CipProjectId { get; set; }
        public CipProject? CipProject { get; set; }

        public int? CipCostId { get; set; }
        public CipCost? CipCost { get; set; }

        public int? AssetTransferId { get; set; }
        public AssetTransfer? AssetTransfer { get; set; }

        public int? CapitalImprovementId { get; set; }
        public CapitalImprovement? CapitalImprovement { get; set; }

        public AttachmentSource Source { get; set; } = AttachmentSource.Asset;

        [Required, StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, StringLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        public long FileSize { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public AttachmentCategory Category { get; set; } = AttachmentCategory.Other;
        public int? CategoryLookupValueId { get; set; }
        public LookupValue? CategoryLookupValue { get; set; }

        [StringLength(100)]
        public string? UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public string FilePath => $"/uploads/{StoredFileName}";
    }

    public enum AttachmentSource
    {
        Asset = 0,
        WorkOrder = 1,
        CipProject = 2,
        CipCost = 3,
        AssetTransfer = 4,
        CapitalImprovement = 5,
        Disposal = 6
    }

    public enum AttachmentCategory
    {
        Other = 0,
        Invoice = 1,
        Receipt = 2,
        Photo = 3,
        Manual = 4,
        Warranty = 5,
        Contract = 6,
        WorkOrder = 7,
        PurchaseOrder = 8,
        Certificate = 9,
        Diagram = 10
    }
}
