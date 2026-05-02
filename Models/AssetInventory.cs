using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models
{
    public class AssetInventory
    {
        public int Id { get; set; }

        public int AssetId { get; set; }
        public Asset? Asset { get; set; }

        [StringLength(100)]
        public string? BarcodeNumber { get; set; }

        [StringLength(50)]
        public string? BarcodeType { get; set; } = "Code128";

        [DataType(DataType.Date)]
        public DateTime? LastScanDate { get; set; }

        [StringLength(100)]
        public string? LastScanLocation { get; set; }

        [StringLength(100)]
        public string? LastScannedBy { get; set; }

        public AssetCondition Condition { get; set; } = AssetCondition.Good;

        [StringLength(500)]
        public string? ConditionNotes { get; set; }

        [StringLength(500)]
        public string? PhotoPath { get; set; }

        public bool IsReconciled { get; set; } = true;

        [DataType(DataType.Date)]
        public DateTime? LastReconciledDate { get; set; }

        public int? LastInventoryListId { get; set; }
        public InventoryList? LastInventoryList { get; set; }
    }

    public class InventoryList
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }
        public int? TenantId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public InventoryStatus Status { get; set; } = InventoryStatus.Draft;

        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [DataType(DataType.Date)]
        public DateTime? StartedDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CompletedDate { get; set; }

        [StringLength(100)]
        public string? AssignedTo { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }

        public int TotalAssets { get; set; }
        public int ScannedAssets { get; set; }
        public int MissingAssets { get; set; }
        public int FoundAssets { get; set; }

        public ICollection<InventoryScan>? Scans { get; set; }
    }

    public class InventoryScan
    {
        public int Id { get; set; }

        public int InventoryListId { get; set; }
        public InventoryList? InventoryList { get; set; }

        public int? AssetId { get; set; }
        public Asset? Asset { get; set; }

        [StringLength(100)]
        public string? ScannedBarcode { get; set; }

        [DataType(DataType.Date)]
        public DateTime ScanDate { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string? ScannedBy { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }

        public ScanResult Result { get; set; } = ScanResult.Found;

        public AssetCondition Condition { get; set; } = AssetCondition.Good;

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(500)]
        public string? PhotoPath { get; set; }
    }

    public enum InventoryStatus
    {
        Draft = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3,
        Reconciled = 4
    }

    public enum ScanResult
    {
        Found = 0,
        Missing = 1,
        Unrecognized = 2,
        Duplicate = 3,
        LocationMismatch = 4
    }
}
