using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Abs.FixedAssets.Models.Revisions;

namespace Abs.FixedAssets.Models
{
    public enum ItemStatus
    {
        Active = 0,
        Inactive = 1,
        Obsolete = 2,
        PendingApproval = 3,
        Discontinued = 4
    }

    public enum ItemType
    {
        Part = 0,
        Consumable = 1,
        Tool = 2,
        Safety = 3,
        Lubricant = 4,
        Chemical = 5,
        Electrical = 6,
        Mechanical = 7,
        Hydraulic = 8,
        Pneumatic = 9,
        Filter = 10,
        Bearing = 11,
        Belt = 12,
        Seal = 13,
        Fastener = 14,
        Kit = 15,
        Service = 16
    }

    public enum CostMethod
    {
        Standard = 0,
        Average = 1,
        FIFO = 2,
        LIFO = 3,
        LastPurchase = 4
    }

    public enum TrackingType
    {
        None = 0,
        LotNumber = 1,
        SerialNumber = 2,
        Both = 3
    }

    public enum UnitOfMeasure
    {
        Each = 0,
        Box = 1,
        Case = 2,
        Pack = 3,
        Pair = 4,
        Set = 5,
        Kit = 6,
        Roll = 7,
        Feet = 8,
        Meter = 9,
        Inch = 10,
        Gallon = 11,
        Liter = 12,
        Quart = 13,
        Pint = 14,
        Ounce = 15,
        Pound = 16,
        Kilogram = 17,
        Gram = 18,
        Dozen = 19,
        Hundred = 20,
        Thousand = 21
    }

    public enum ItemMasterSource
    {
        Internal = 0,
        ExternalERP = 1,
        Synced = 2
    }

    public enum ABCClassification
    {
        A = 0,  // High value, tight control (top 20% by value)
        B = 1,  // Medium value, moderate control
        C = 2,  // Low value, simple control
        Unclassified = 3
    }

    public enum BarcodeType
    {
        None = 0,
        Code128 = 1,
        Code39 = 2,
        QRCode = 3,
        DataMatrix = 4,
        EAN13 = 5,
        UPC = 6
    }

    public enum ReorderMethod
    {
        Manual = 0,
        MinMax = 1,
        ReorderPoint = 2,
        EOQ = 3,
        Kanban = 4
    }

    public enum StockPolicy
    {
        Stock = 0,
        Nonstock = 1,
        CriticalSpare = 2
    }

    public class ItemCategory
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public int? ParentCategoryId { get; set; }
        public ItemCategory? ParentCategory { get; set; }

        public int? DefaultGlAccountId { get; set; }
        [Display(Name = "Inventory GL Account")]
        public GlAccount? DefaultGlAccount { get; set; }

        public int? ExpenseGlAccountId { get; set; }
        [Display(Name = "Expense GL Account")]
        public GlAccount? ExpenseGlAccount { get; set; }

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }

        public ICollection<Item>? Items { get; set; }
        public ICollection<ItemCategory>? ChildCategories { get; set; }
    }

    public class Item
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Part Number")]
        public string PartNumber { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "Extended Description")]
        public string? ExtendedDescription { get; set; }

        [StringLength(10)]
        public string? Revision { get; set; }

        [Display(Name = "Require Revision Control")]
        public bool RequireRevisionControl { get; set; } = false;

        public ItemType Type { get; set; } = ItemType.Part;
        public int? TypeLookupValueId { get; set; }
        public LookupValue? TypeLookupValue { get; set; }

        public ItemStatus Status { get; set; } = ItemStatus.Active;
        public int? StatusLookupValueId { get; set; }
        public LookupValue? StatusLookupValue { get; set; }

        public int? CategoryId { get; set; }
        [Display(Name = "Item Category")]
        public ItemCategory? Category { get; set; }

        public UnitOfMeasure UOM { get; set; } = UnitOfMeasure.Each;

        [StringLength(20)]
        [Display(Name = "Stock UOM")]
        public string StockUOM { get; set; } = "EA";

        [StringLength(20)]
        [Display(Name = "Purchase UOM")]
        public string PurchaseUOM { get; set; } = "EA";

        [Display(Name = "Purchase/Stock Conversion")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal PurchaseConversion { get; set; } = 1;

        public CostMethod CostMethod { get; set; } = CostMethod.Average;
        public int? CostMethodLookupValueId { get; set; }
        public LookupValue? CostMethodLookupValue { get; set; }

        [Display(Name = "Standard Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal StandardCost { get; set; }

        [Display(Name = "Average Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal AverageCost { get; set; }

        [Display(Name = "Last Purchase Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal LastPurchaseCost { get; set; }

        [Display(Name = "List Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ListPrice { get; set; }

        public TrackingType TrackingType { get; set; } = TrackingType.None;
        public int? TrackingTypeLookupValueId { get; set; }
        public LookupValue? TrackingTypeLookupValue { get; set; }

        [Display(Name = "Minimum Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal MinQuantity { get; set; } = 0;

        [Display(Name = "Maximum Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal MaxQuantity { get; set; } = 0;

        [Display(Name = "Reorder Point")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReorderPoint { get; set; } = 0;

        [Display(Name = "Reorder Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReorderQuantity { get; set; } = 0;

        [Display(Name = "Safety Stock")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal SafetyStock { get; set; } = 0;

        [Display(Name = "Lead Time (Days)")]
        public int LeadTimeDays { get; set; } = 0;

        // DEF-008: best-in-class item-location preference. This proper FK
        // replaces the free-form DefaultLocation string for receive-flow
        // defaulting. The string is retained for backward-compat display
        // but new code should consume DefaultLocationId.
        [Display(Name = "Default Location")]
        public int? DefaultLocationId { get; set; }
        public Location? DefaultLocationRef { get; set; }

        [StringLength(50)]
        [Display(Name = "Default Location (legacy text)")]
        public string? DefaultLocation { get; set; }

        [StringLength(20)]
        [Display(Name = "Warehouse")]
        public string? Warehouse { get; set; }

        [StringLength(20)]
        public string? Aisle { get; set; }

        [StringLength(20)]
        public string? Rack { get; set; }

        [StringLength(20)]
        public string? Shelf { get; set; }

        [StringLength(20)]
        public string? Bin { get; set; }

        public int? PrimaryVendorId { get; set; }
        [Display(Name = "Primary Vendor")]
        public Vendor? PrimaryVendor { get; set; }

        [StringLength(50)]
        [Display(Name = "Vendor Part #")]
        public string? VendorPartNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Manufacturer Part #")]
        public string? ManufacturerPartNumber { get; set; }

        public int? ManufacturerId { get; set; }
        public Manufacturer? Manufacturer { get; set; }

        [Display(Name = "Is Stocked")]
        public bool IsStocked { get; set; } = true;

        [Display(Name = "Is Purchasable")]
        public bool IsPurchasable { get; set; } = true;

        [Display(Name = "Is Critical Spare")]
        public bool IsCriticalSpare { get; set; } = false;

        [Display(Name = "Taxable")]
        public bool IsTaxable { get; set; } = true;

        [Display(Name = "Hazardous Material")]
        public bool IsHazmat { get; set; } = false;

        [StringLength(50)]
        [Display(Name = "Hazmat Class")]
        public string? HazmatClass { get; set; }

        [Display(Name = "Shelf Life (Days)")]
        public int? ShelfLifeDays { get; set; }

        [Display(Name = "Weight (lbs)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? Weight { get; set; }

        [StringLength(100)]
        [Display(Name = "Dimensions")]
        public string? Dimensions { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(200)]
        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "Image Path")]
        public string? ImagePath { get; set; }

        [StringLength(500)]
        [Display(Name = "External Image URL")]
        public string? ExternalImageUrl { get; set; }

        [StringLength(200)]
        [Display(Name = "Specification URL")]
        public string? SpecUrl { get; set; }

        // Barcode fields
        public BarcodeType BarcodeType { get; set; } = BarcodeType.Code128;

        [StringLength(100)]
        [Display(Name = "Barcode")]
        public string? Barcode { get; set; }

        [StringLength(100)]
        [Display(Name = "Alternate Barcode")]
        public string? AlternateBarcode { get; set; }

        // ABC Classification for inventory control
        [Display(Name = "ABC Classification")]
        public ABCClassification ABCClass { get; set; } = ABCClassification.Unclassified;

        // Reorder automation
        [Display(Name = "Reorder Method")]
        public ReorderMethod ReorderMethod { get; set; } = ReorderMethod.ReorderPoint;

        [Display(Name = "Auto-Reorder Enabled")]
        public bool AutoReorderEnabled { get; set; } = false;

        [Display(Name = "Economic Order Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? EOQ { get; set; }

        [Display(Name = "Annual Usage")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal AnnualUsage { get; set; } = 0;

        [Display(Name = "Average Daily Usage")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal AverageDailyUsage { get; set; } = 0;

        [Display(Name = "Carrying Cost %")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? CarryingCostPercent { get; set; }

        [Display(Name = "Ordering Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? OrderingCost { get; set; }

        // Procurement fields (v2-lite)
        [Display(Name = "Min Order Qty")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MinOrderQty { get; set; }

        [Display(Name = "Order Multiple")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? OrderMultiple { get; set; }

        [Display(Name = "Pack Qty")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? PackQty { get; set; }

        [Display(Name = "Last Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? LastPrice { get; set; }

        [StringLength(10)]
        [Display(Name = "Currency")]
        public string? CurrencyCode { get; set; }

        [Display(Name = "Price Effective Date")]
        public DateTime? PriceEffectiveDate { get; set; }

        [Display(Name = "Contract")]
        public bool ContractFlag { get; set; } = false;

        [StringLength(50)]
        [Display(Name = "Contract Ref")]
        public string? ContractRef { get; set; }

        [Display(Name = "Stock Policy")]
        public StockPolicy StockPolicy { get; set; } = StockPolicy.Stock;

        // Alternate parts
        [StringLength(200)]
        [Display(Name = "Alternate Part Numbers")]
        public string? AlternatePartNumbers { get; set; }

        [StringLength(100)]
        [Display(Name = "Supersedes Part #")]
        public string? SupersedesPartNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Superseded By Part #")]
        public string? SupersededByPartNumber { get; set; }

        // Warranty
        [Display(Name = "Warranty Period (Months)")]
        public int? WarrantyMonths { get; set; }

        [StringLength(200)]
        [Display(Name = "Warranty Terms")]
        public string? WarrantyTerms { get; set; }

        // Commodity/UNSPSC codes
        [StringLength(20)]
        [Display(Name = "Commodity Code")]
        public string? CommodityCode { get; set; }

        [StringLength(20)]
        [Display(Name = "UNSPSC Code")]
        public string? UNSPSCCode { get; set; }

        // Buyer assignment for requisitions
        public int? DefaultBuyerId { get; set; }

        [StringLength(100)]
        [Display(Name = "Default Buyer")]
        public string? DefaultBuyerName { get; set; }

        // Physical attributes
        [Display(Name = "Length")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? Length { get; set; }

        [Display(Name = "Width")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? Width { get; set; }

        [Display(Name = "Height")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? Height { get; set; }

        [StringLength(20)]
        [Display(Name = "Dimension UOM")]
        public string? DimensionUOM { get; set; } = "in";

        // Temperature/storage requirements
        [StringLength(100)]
        [Display(Name = "Storage Requirements")]
        public string? StorageRequirements { get; set; }

        [Display(Name = "Min Storage Temp")]
        public int? MinStorageTemp { get; set; }

        [Display(Name = "Max Storage Temp")]
        public int? MaxStorageTemp { get; set; }

        // Certifications/compliance
        [StringLength(200)]
        [Display(Name = "Certifications")]
        public string? Certifications { get; set; }

        [Display(Name = "FDA Regulated")]
        public bool IsFDARegulated { get; set; } = false;

        [Display(Name = "OSHA Compliance Required")]
        public bool IsOSHACompliance { get; set; } = false;

        // Country of origin for customs
        [StringLength(50)]
        [Display(Name = "Country of Origin")]
        public string? CountryOfOrigin { get; set; }

        [StringLength(20)]
        [Display(Name = "HTS Code")]
        public string? HTSCode { get; set; }

        public ItemMasterSource Source { get; set; } = ItemMasterSource.Internal;

        [StringLength(50)]
        [Display(Name = "External ID")]
        public string? ExternalId { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public int? CurrentReleasedRevisionId { get; set; }
        [Display(Name = "Current Released Revision")]
        public ItemRevision? CurrentReleasedRevision { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        public ICollection<ItemVendor>? ItemVendors { get; set; }
        public ICollection<ItemInventory>? Inventory { get; set; }
        public ICollection<ItemRevision>? Revisions { get; set; }
        public ICollection<PMTemplateItem>? PMTemplateItems { get; set; }
        public ICollection<ItemImage>? Images { get; set; }
        public ICollection<ItemCompanyStocking>? CompanyStockingSettings { get; set; }
        public ICollection<ItemManufacturerPart>? ManufacturerParts { get; set; }
        public ICollection<VendorItemPart>? VendorItemParts { get; set; }
    }

    /// <summary>
    /// Per-company stocking settings for shared Item Master catalog.
    /// Following Maximo pattern: Item Master is shared, stocking decisions are per-company/site.
    /// </summary>
    public class ItemCompanyStocking
    {
        public int Id { get; set; }

        [Required]
        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Required]
        public int CompanyId { get; set; }
        public Company? Company { get; set; }

        [Display(Name = "Is Stocked")]
        public bool IsStocked { get; set; } = true;

        [Display(Name = "Is Purchasable")]
        public bool IsPurchasable { get; set; } = true;

        [Display(Name = "Is Critical Spare")]
        public bool IsCriticalSpare { get; set; } = false;

        [Display(Name = "Minimum Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal MinQuantity { get; set; } = 0;

        [Display(Name = "Maximum Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal MaxQuantity { get; set; } = 0;

        [Display(Name = "Reorder Point")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReorderPoint { get; set; } = 0;

        [Display(Name = "Reorder Quantity")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal ReorderQuantity { get; set; } = 0;

        [Display(Name = "Safety Stock")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal SafetyStock { get; set; } = 0;

        [Display(Name = "Lead Time (Days)")]
        public int LeadTimeDays { get; set; } = 0;

        public int? PreferredVendorId { get; set; }
        [Display(Name = "Preferred Vendor")]
        public Vendor? PreferredVendor { get; set; }

        [Display(Name = "Reorder Method")]
        public ReorderMethod ReorderMethod { get; set; } = ReorderMethod.ReorderPoint;

        [Display(Name = "Auto Reorder Enabled")]
        public bool AutoReorderEnabled { get; set; } = false;

        [Display(Name = "ABC Classification")]
        public ABCClassification ABCClass { get; set; } = ABCClassification.Unclassified;

        // DEF-008: per-company override for receive-flow location defaulting.
        // Cascade: ItemCompanyStocking.DefaultLocationId → Item.DefaultLocationId → null.
        [Display(Name = "Default Location")]
        public int? DefaultLocationId { get; set; }
        public Location? DefaultLocationRef { get; set; }

        [StringLength(20)]
        [Display(Name = "Warehouse")]
        public string? DefaultWarehouse { get; set; }

        [StringLength(20)]
        public string? DefaultAisle { get; set; }

        [StringLength(20)]
        public string? DefaultRack { get; set; }

        [StringLength(20)]
        public string? DefaultShelf { get; set; }

        [StringLength(20)]
        public string? DefaultBin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }
    }

    public class ItemVendor
    {
        public int Id { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public int VendorId { get; set; }
        public Vendor? Vendor { get; set; }

        [StringLength(50)]
        [Display(Name = "Vendor Part Number")]
        public string? VendorPartNumber { get; set; }

        [Display(Name = "Unit Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "Minimum Order Qty")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal MinOrderQty { get; set; } = 1;

        [Display(Name = "Lead Time (Days)")]
        public int LeadTimeDays { get; set; } = 0;

        [Display(Name = "Is Preferred")]
        public bool IsPreferred { get; set; } = false;

        [Display(Name = "Last Order Date")]
        public DateTime? LastOrderDate { get; set; }

        // Vendor URLs for direct ordering
        [StringLength(500)]
        [Display(Name = "Product Page URL")]
        public string? ProductPageUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "Order URL")]
        public string? OrderUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "Catalog Page URL")]
        public string? CatalogPageUrl { get; set; }

        // Pricing tiers
        [Display(Name = "Price Break Qty 1")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? PriceBreakQty1 { get; set; }

        [Display(Name = "Price Break 1")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? PriceBreak1 { get; set; }

        [Display(Name = "Price Break Qty 2")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? PriceBreakQty2 { get; set; }

        [Display(Name = "Price Break 2")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? PriceBreak2 { get; set; }

        [Display(Name = "Price Break Qty 3")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? PriceBreakQty3 { get; set; }

        [Display(Name = "Price Break 3")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? PriceBreak3 { get; set; }

        // Contract info
        [StringLength(50)]
        [Display(Name = "Contract Number")]
        public string? ContractNumber { get; set; }

        [Display(Name = "Contract Price")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ContractPrice { get; set; }

        [Display(Name = "Contract Start")]
        public DateTime? ContractStartDate { get; set; }

        [Display(Name = "Contract End")]
        public DateTime? ContractEndDate { get; set; }

        // Vendor stock status
        [Display(Name = "Vendor Stock Available")]
        public bool? VendorStockAvailable { get; set; }

        [Display(Name = "Last Stock Check")]
        public DateTime? LastStockCheckDate { get; set; }

        [StringLength(200)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public class ItemImage
    {
        public int Id { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "File Name")]
        public string FileName { get; set; } = string.Empty;

        [Required, StringLength(500)]
        [Display(Name = "File Path")]
        public string FilePath { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Content Type")]
        public string ContentType { get; set; } = "image/jpeg";

        [Display(Name = "File Size (bytes)")]
        public long FileSize { get; set; }

        [StringLength(200)]
        [Display(Name = "Alt Text")]
        public string? AltText { get; set; }

        [StringLength(500)]
        [Display(Name = "Caption")]
        public string? Caption { get; set; }

        [Display(Name = "Is Primary")]
        public bool IsPrimary { get; set; } = false;

        [Display(Name = "Sort Order")]
        public int SortOrder { get; set; } = 0;

        [StringLength(500)]
        [Display(Name = "External URL")]
        public string? ExternalUrl { get; set; }

        [Display(Name = "Is External")]
        public bool IsExternal { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? CreatedBy { get; set; }
    }

    public class ItemRevision
    {
        public int Id { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        [Required, StringLength(10)]
        [Display(Name = "Revision Code")]
        public string RevisionCode { get; set; } = "A";

        public RevisionStatus Status { get; set; } = RevisionStatus.Draft;
        public int? StatusLookupValueId { get; set; }

        [StringLength(500)]
        [Display(Name = "Change Reason")]
        public string? ChangeReason { get; set; }

        public int? SupersedesItemRevisionId { get; set; }
        [Display(Name = "Supersedes")]
        public ItemRevision? SupersedesRevision { get; set; }

        [Display(Name = "Effective From")]
        public DateTime? EffectiveFromUtc { get; set; }

        [Display(Name = "Effective To")]
        public DateTime? EffectiveToUtc { get; set; }

        [StringLength(100)]
        [Display(Name = "Created By")]
        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        [Display(Name = "Approved By")]
        public string? ApprovedByUserId { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }

        public DateTime? ReleasedAtUtc { get; set; }
        public DateTime? ObsoletedAtUtc { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Display(Name = "Revision")]
        [Obsolete("Use RevisionCode instead")]
        public string Revision 
        { 
            get => RevisionCode; 
            set => RevisionCode = value; 
        }

        [Obsolete("Use ChangeReason instead")]
        public string? ChangeDescription
        {
            get => ChangeReason;
            set => ChangeReason = value;
        }

        [Display(Name = "Effective Date")]
        [Obsolete("Use EffectiveFromUtc instead")]
        public DateTime EffectiveDate
        {
            get => EffectiveFromUtc ?? CreatedAtUtc;
            set => EffectiveFromUtc = value;
        }

        [Display(Name = "Superseded Date")]
        [Obsolete("Use EffectiveToUtc instead")]
        public DateTime? SupersededDate
        {
            get => EffectiveToUtc;
            set => EffectiveToUtc = value;
        }

        [StringLength(50)]
        [Display(Name = "Changed By")]
        [Obsolete("Use CreatedByUserId instead")]
        public string? ChangedBy
        {
            get => CreatedByUserId;
            set => CreatedByUserId = value;
        }

        [StringLength(50)]
        [Display(Name = "Approved By")]
        [Obsolete("Use ApprovedByUserId instead")]
        public string? ApprovedBy
        {
            get => ApprovedByUserId;
            set => ApprovedByUserId = value;
        }

        [Obsolete("Use ApprovedAtUtc instead")]
        public DateTime? ApprovedDate
        {
            get => ApprovedAtUtc;
            set => ApprovedAtUtc = value;
        }

        [Display(Name = "Is Current")]
        [Obsolete("Use Status == Released and check Item.CurrentReleasedRevisionId instead")]
        public bool IsCurrent
        {
            get => Status == RevisionStatus.Released;
            set { }
        }

        [Obsolete("Use CreatedAtUtc instead")]
        public DateTime CreatedAt
        {
            get => CreatedAtUtc;
            set => CreatedAtUtc = value;
        }
    }

    public class ItemInventory
    {
        public int Id { get; set; }

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public int? LocationId { get; set; }
        public Location? Location { get; set; }

        [StringLength(50)]
        [Display(Name = "Warehouse")]
        public string? Warehouse { get; set; }

        [StringLength(20)]
        public string? Bin { get; set; }

        [Display(Name = "Quantity On Hand")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityOnHand { get; set; } = 0;

        [Display(Name = "Quantity Reserved")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityReserved { get; set; } = 0;

        [Display(Name = "Quantity Available")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;

        [Display(Name = "Quantity On Order")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal QuantityOnOrder { get; set; } = 0;

        [StringLength(50)]
        [Display(Name = "Lot Number")]
        public string? LotNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        [Display(Name = "Expiration Date")]
        public DateTime? ExpirationDate { get; set; }

        [Display(Name = "Last Count Date")]
        public DateTime? LastCountDate { get; set; }

        [Display(Name = "Last Receipt Date")]
        public DateTime? LastReceiptDate { get; set; }

        [Display(Name = "Last Issue Date")]
        public DateTime? LastIssueDate { get; set; }

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public enum TransactionType
    {
        Receipt = 0,
        Issue = 1,
        Adjust = 2,
        Transfer = 3,
        Return = 4,
        Scrap = 5,
        CycleCount = 6,
        PhysicalCount = 7
    }

    public class ItemTransaction
    {
        public int Id { get; set; }

        // Industrial transaction numbers (SAP MM IBLNR, Oracle EBS TXN_REF,
        // Maximo MATRECTRANS) are typically 30-50 chars. Was 20 — too short
        // for our format "{type}-{compositekey}-{timestamp}" which routinely
        // overflowed and threw 22001 at SaveChanges.
        [Required, StringLength(60)]
        [Display(Name = "Transaction #")]
        public string TransactionNumber { get; set; } = string.Empty;

        public int ItemId { get; set; }
        public Item? Item { get; set; }

        public TransactionType Type { get; set; }
        public int? TypeLookupValueId { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Quantity { get; set; }

        [Display(Name = "Unit Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal UnitCost { get; set; }

        [Display(Name = "Total Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal TotalCost => Quantity * UnitCost;

        public int? FromLocationId { get; set; }
        [Display(Name = "From Location")]
        public Location? FromLocation { get; set; }

        public int? ToLocationId { get; set; }
        [Display(Name = "To Location")]
        public Location? ToLocation { get; set; }

        [StringLength(50)]
        [Display(Name = "From Bin")]
        public string? FromBin { get; set; }

        [StringLength(50)]
        [Display(Name = "To Bin")]
        public string? ToBin { get; set; }

        [StringLength(50)]
        [Display(Name = "Lot Number")]
        public string? LotNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Serial Number")]
        public string? SerialNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "Reference Type")]
        public string? ReferenceType { get; set; }

        [StringLength(50)]
        [Display(Name = "Reference #")]
        public string? ReferenceNumber { get; set; }

        public int? WorkOrderId { get; set; }

        public int? PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Transacted By")]
        public string TransactedBy { get; set; } = string.Empty;

        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        public int? CompanyId { get; set; }
        public Company? Company { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
