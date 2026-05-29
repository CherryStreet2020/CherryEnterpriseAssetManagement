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

    // ====================================================================
    // B6 Foundation Sprint PR-FS-7 (2026-05-26) — Item Master expansion enums.
    // ====================================================================

    /// <summary>
    /// MRP planning policy. Drives MRP run mode + acquisition behavior.
    /// SAP MRP type / Oracle planning code / D365 coverage group equivalent.
    /// </summary>
    public enum PlanningPolicy
    {
        /// <summary>Make-to-stock — replenish to ROP/EOQ; demand-anonymous.</summary>
        MakeToStock = 0,

        /// <summary>Make-to-order — production starts only when a SO line lands.</summary>
        MakeToOrder = 1,

        /// <summary>Engineer-to-order — design + production both demand-triggered.</summary>
        EngineerToOrder = 2,

        /// <summary>Assemble-to-order — subassemblies kept in stock, final assembly to SO.</summary>
        AssembleToOrder = 3,

        /// <summary>Configure-to-order — product configurator drives BOM at SO time.</summary>
        ConfigureToOrder = 4,

        /// <summary>Purchase-to-order — buy externally only when SO/PO demand exists.</summary>
        PurchaseToOrder = 5,

        /// <summary>Purchase-to-stock — keep stock from blanket vendor agreements.</summary>
        PurchaseToStock = 6,

        /// <summary>Blanket release — release against a pre-negotiated blanket PO.</summary>
        BlanketRelease = 7,

        /// <summary>Phantom — exploded through, never actually stocked.</summary>
        Phantom = 8,

        /// <summary>No-plan — manual replenishment only.</summary>
        Manual = 99,
    }

    /// <summary>
    /// Make-vs-buy preference. Substrate for Theme B7's Make-or-Buy decision
    /// service. SAP procurement type / Oracle make-or-buy code / D365 make-or-buy.
    /// </summary>
    public enum MakeBuyCode
    {
        /// <summary>Make internally (use Item's Routing + BOM).</summary>
        Make = 0,

        /// <summary>Buy externally (use ItemSourcingRule's vendor).</summary>
        Buy = 1,

        /// <summary>Either Make OR Buy — decision service routes per capacity/cost/lead-time at run time.</summary>
        MakeOrBuy = 2,

        /// <summary>Phantom — never actually produced or procured; explodes through.</summary>
        Phantom = 3,
    }

    /// <summary>
    /// Theme B7 — when the Item Master must exist relative to the Production Order.
    /// Orthogonal to <see cref="PlanningPolicy"/> (which says HOW demand is planned);
    /// SourcePattern says WHEN the master exists. Disrupts SAP/Oracle/Epicor, which
    /// force a material master before you can build. See
    /// docs/research/po-as-standard-make-or-buy-dean-research.md §2.4.
    /// </summary>
    public enum SourcePattern
    {
        /// <summary>Classic — Item Master + standard BOM/Routing required at PO release. (MTS/BTS/repeat.) Global default.</summary>
        StandardFirst = 0,

        /// <summary>ETO — build from the PO; Item Master optional, crystallized at ship from actuals. PO carries ItemId == null.</summary>
        PoFirst = 1,

        /// <summary>Master exists but the PO may diverge; crystallize an as-built variant/rev at ship.</summary>
        Hybrid = 2,
    }

    /// <summary>
    /// Theme B7 — make/buy duality policy, richer than SAP procurement type E/F/X.
    /// Overlays <see cref="MakeBuyCode"/>; <c>Inherit</c> (value 0, the default) means
    /// "derive the policy from MakeBuyCode" so this is additive and non-breaking.
    /// Consumed by IMakeBuyDecisionService. See spec §4.2.
    /// </summary>
    public enum MakeBuyPolicy
    {
        /// <summary>Inherit from <see cref="MakeBuyCode"/> (default — no behavior change).</summary>
        Inherit = 0,

        /// <summary>Always make internally.</summary>
        MakeOnly = 1,

        /// <summary>Always buy externally.</summary>
        BuyOnly = 2,

        /// <summary>Either path — decision service routes per capacity/cost/lead-time.</summary>
        MakeOrBuy = 3,

        /// <summary>Make by default; buy the overflow when internal capacity is short.</summary>
        MakeWithBuyOverflow = 4,

        /// <summary>Buy by default; make as the backup when the vendor cannot deliver.</summary>
        BuyWithMakeBackup = 5,
    }

    /// <summary>
    /// Theme B7 — per-item default preference when the path is open. Value 0
    /// (<c>LetSystemDecide</c>) is the semantic default so no DB override is needed.
    /// A per-PO decision can still override this. See spec §4.2.
    /// </summary>
    public enum DefaultSourcePreference
    {
        /// <summary>Let IMakeBuyDecisionService decide from live capacity/cost/lead-time (default).</summary>
        LetSystemDecide = 0,

        /// <summary>Prefer making internally when feasible.</summary>
        Make = 1,

        /// <summary>Prefer buying externally when feasible.</summary>
        Buy = 2,
    }

    /// <summary>
    /// MRP lot-sizing rule. Determines the qty produced/purchased per supply order.
    /// SAP lot-size key / Oracle lot-for-lot vs. fixed lot / D365 reorder policy.
    /// </summary>
    public enum LotSizingRule
    {
        /// <summary>Lot-for-Lot — supply qty matches net requirement exactly.</summary>
        LotForLot = 0,

        /// <summary>Fixed Order Quantity — every supply order is the same qty.</summary>
        FixedOrderQuantity = 1,

        /// <summary>Fixed Period Requirements — supply order covers N periods of net demand.</summary>
        FixedPeriodRequirements = 2,

        /// <summary>Economic Order Quantity — minimize total ordering + carrying cost.</summary>
        EOQ = 3,

        /// <summary>Min Order Qty — supply qty = MAX(net requirement, MinOrderQty).</summary>
        MinOrderQty = 4,

        /// <summary>Max Order Qty — supply qty = MIN(net requirement, MaxOrderQty), split if larger.</summary>
        MaxOrderQty = 5,

        /// <summary>Part Period Balancing — heuristic balancing ordering vs. carrying cost.</summary>
        PartPeriodBalancing = 6,

        /// <summary>Wagner-Within optimal — dynamic-programming optimal lot sizes.</summary>
        WagnerWithin = 7,
    }

    /// <summary>
    /// Item lifecycle stage. Drives engineering + sales + manufacturing visibility.
    /// SAP material status / Oracle item lifecycle / D365 lifecycle state.
    /// </summary>
    public enum LifecycleStage
    {
        /// <summary>Concept — initial proposal, not yet engineered.</summary>
        Concept = 0,

        /// <summary>Design — engineering in progress.</summary>
        Design = 1,

        /// <summary>Prototype — first builds for design validation.</summary>
        Prototype = 2,

        /// <summary>Sample — FAI / customer-evaluation samples.</summary>
        Sample = 3,

        /// <summary>Released — engineering released to manufacturing.</summary>
        Released = 4,

        /// <summary>Production — actively manufactured.</summary>
        Production = 5,

        /// <summary>End of Life — phase-out underway; new orders restricted.</summary>
        EndOfLife = 6,

        /// <summary>Obsolete — no longer offered; historical record only.</summary>
        Obsolete = 7,
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

        // ====================================================================
        // B6 Foundation Sprint PR-FS-1 (2026-05-26) — ItemGroupId wire-up.
        //
        // Foundational classification onto Models/Masters/ItemGroup.cs that
        // drives the PRA-7 PostingProfile + PRA-5b AccountingKey cascade.
        // Without this FK the PostingProfile resolution (which keys on
        // ItemGroupId × TransactionType × Warehouse) has no Item-side
        // driver — that infrastructure has been shipped since PRA-7 on
        // 2026-05-24 and unused.
        //
        // NULLABLE in this PR. Service-layer requires Source=Internal Items
        // to supply ItemGroupId on create going forward. Existing 151 Items
        // on dev have ItemGroupId=NULL until a separate backfill seeder PR
        // populates them — kept as a separate ship per BIC discipline
        // (one concern per PR).
        //
        // Tightening to NOT NULL is a future cleanup PR after every Item
        // is classified.
        //
        // See:
        //   docs/research/b6-foundation-sprint-design-2026-05-26.md
        //   docs/research/item-master-vs-production-order-snapshot-2026-05-26.md
        //   feedback memory: feedback_b6_go_big_2026_05_26.md
        // ====================================================================
        public int? ItemGroupId { get; set; }
        [Display(Name = "Item Group")]
        public Abs.FixedAssets.Models.Masters.ItemGroup? ItemGroup { get; set; }

        // ====================================================================
        // LEGACY UOM (pre-Sprint-13.5-PRA-4) — kept for back-compat. New code
        // should READ via the StockUomId / PurchaseUomId / SalesUomId FKs below
        // and the IUomService.ConvertAsync helper. Removal of these legacy
        // fields will happen in a dedicated cleanup PR after the FK fields are
        // populated everywhere AND every read path has migrated.
        // ====================================================================
        public UnitOfMeasure UOM { get; set; } = UnitOfMeasure.Each;

        [StringLength(20)]
        [Display(Name = "Stock UOM (legacy)")]
        public string StockUOM { get; set; } = "EA";

        [StringLength(20)]
        [Display(Name = "Purchase UOM (legacy)")]
        public string PurchaseUOM { get; set; } = "EA";

        [Display(Name = "Purchase/Stock Conversion (legacy)")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal PurchaseConversion { get; set; } = 1;

        // ====================================================================
        // Sprint 13.5 PRA-4 — UOM master FKs (10 new columns, all NULLABLE).
        //
        // The new master UOM table replaces the two parallel enums
        // (Models.Item.UnitOfMeasure inventory + Models.Telemetry.UnitOfMeasure
        // sensors) with a unified Masters.UnitOfMeasureMaster table — one row
        // per UOM, affine factor + offset to its category's base unit, ISO/
        // UNECE/UCUM codes, decimal precision per UOM.
        //
        // ALL FKs ARE NULLABLE in this PR. The PRA-4 migration backfills
        // StockUomId from the legacy UOM enum so existing rows have a value
        // immediately. Other FKs (PurchaseUomId / SalesUomId / etc.) default
        // to NULL = "same as Stock" — service-layer resolver handles the
        // fallback.
        //
        // NOT-NULLing StockUomId is a SEPARATE migration after every read
        // path has migrated (PRA-4.x cleanup or later sprint).
        //
        // AUTHORITY:
        //   - docs/research/master-files-baseline-2026-05-24.md §5
        //   - memory: reference_master_files_baseline.md
        // ====================================================================

        [Display(Name = "Stock UOM"), Column("StockUomId")]
        public int? StockUomId { get; set; }

        [Display(Name = "Purchase UOM"), Column("PurchaseUomId")]
        public int? PurchaseUomId { get; set; }

        [Display(Name = "Purchase Pack UOM"), Column("PurchasePackUomId")]
        public int? PurchasePackUomId { get; set; }

        [Display(Name = "Sales UOM"), Column("SalesUomId")]
        public int? SalesUomId { get; set; }

        [Display(Name = "Sales Pack UOM"), Column("SalesPackUomId")]
        public int? SalesPackUomId { get; set; }

        [Display(Name = "Price UOM"), Column("PriceUomId")]
        public int? PriceUomId { get; set; }

        [Display(Name = "Reporting UOM"), Column("ReportingUomId")]
        public int? ReportingUomId { get; set; }

        [Display(Name = "Weight UOM"), Column("WeightUomId")]
        public int? WeightUomId { get; set; }

        [Display(Name = "Volume UOM"), Column("VolumeUomId")]
        public int? VolumeUomId { get; set; }

        [Display(Name = "Dimension UOM"), Column("DimensionUomId")]
        public int? DimensionUomId { get; set; }

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

        // ADR-015 — Default ReceiptProfile for receipts of this SKU.
        // Drives auto-selection of the receipt profile at receiving
        // time. Steel parts get the STEEL profile, drug SKUs get
        // PHARMA, etc. Null falls back to a tenant-default or DISCRETE.
        public int? DefaultReceiptProfileId { get; set; }
        public Abs.FixedAssets.Models.Production.ReceiptProfile? DefaultReceiptProfile { get; set; }

        // ADR-015 — Per-item default Attributes JSON merged into every
        // receipt against this SKU at PO-receipt time. E.g. a controlled
        // substance SKU's `{"deaSchedule":"II"}`, a moisture-sensitive
        // electronic component's `{"mslLevel":3}`. Profile-shape;
        // validated against DefaultReceiptProfile.JsonSchema at create.
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "jsonb")]
        public string? DefaultReceiptAttributes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        [StringLength(50)]
        public string? UpdatedBy { get; set; }

        // ====================================================================
        // B6 Foundation Sprint PR-FS-7 (2026-05-26) — Item Master 18-column expansion.
        //
        // Closes out the Foundation Sprint substrate. Brings the Item Master from
        // mid-market depth to tier-1 BIC parity across:
        //   - MRP planning policy + lot sizing + planner code (5 fields)
        //   - Make-vs-Buy duality + sellable/phantom/kitting flags (5 fields)
        //   - Quality + AS9100 + InspectionPlan linkage (4 fields)
        //   - International trade compliance — ECCN / Schedule B / Intrastat / EAR99 (4 fields)
        //   - Costing freeze + ItemFamily + LifecycleStage (4 fields — 2 are paired)
        //
        // The IsSellable flag is the key driver — the ItemGroupResolver consults it
        // to tighten the Part+Internal default from SUBASSY → FG when IsSellable=true
        // (closes the loop on the PR-FS-1.5.1 hotfix lesson).
        //
        // All fields are NULLABLE / DEFAULT FALSE so the migration is non-breaking.
        // Existing rows retain semantic defaults; the resolver only changes behavior
        // when IsSellable is explicitly TRUE.
        // ====================================================================

        // ----- Planning + Sourcing (5) ----------------------------------

        [Display(Name = "Planning Policy")]
        public PlanningPolicy PlanningPolicy { get; set; } = PlanningPolicy.MakeToStock;

        [Display(Name = "Make/Buy Code")]
        public MakeBuyCode MakeBuyCode { get; set; } = MakeBuyCode.Buy;

        [Display(Name = "Lot Sizing Rule")]
        public LotSizingRule LotSizingRule { get; set; } = LotSizingRule.LotForLot;

        [StringLength(20)]
        [Display(Name = "MRP Planner Code")]
        public string? MrpPlannerCode { get; set; }

        /// <summary>
        /// Sellable Item — appears on customer Sales Orders. Drives the
        /// ItemGroupResolver default classification: Part+Internal+IsSellable=true
        /// → FG; else SUBASSY. (Closes the PR-FS-1.5.1 hotfix lesson.)
        /// </summary>
        [Display(Name = "Is Sellable")]
        public bool IsSellable { get; set; } = false;

        // ----- Theme B7 — PO-as-Standard + Make-or-Buy duality (7) ------
        // See docs/research/po-as-standard-make-or-buy-dean-research.md §2.4 + §4.2
        // and docs/research/b7-cascade-design.md (Wave A PR-1). All fields are
        // non-breaking: SourcePattern + MakeBuyPolicy + DefaultSourcePreference
        // default to value-0 semantics (StandardFirst / Inherit / LetSystemDecide),
        // so existing rows keep classic behavior with no migration backfill needed
        // beyond the EF zero-default.

        /// <summary>
        /// B7 — when the Item Master must exist relative to the Production Order.
        /// StandardFirst (default) = master required at release; PoFirst = build
        /// from the PO, crystallize the master at ship. Carve-out guard:
        /// stocking / MTS / BTS items cannot be PoFirst (see
        /// <see cref="ValidateSourcePatternCarveout(SourcePattern, bool, PlanningPolicy, out string)"/>).
        /// </summary>
        [Display(Name = "Source Pattern")]
        public SourcePattern SourcePattern { get; set; } = SourcePattern.StandardFirst;

        /// <summary>
        /// B7 — make/buy duality policy (richer than MakeBuyCode). Inherit (default)
        /// derives from <see cref="MakeBuyCode"/>; consumed by IMakeBuyDecisionService.
        /// </summary>
        [Display(Name = "Make/Buy Policy")]
        public MakeBuyPolicy MakeBuyPolicy { get; set; } = MakeBuyPolicy.Inherit;

        /// <summary>
        /// B7 — per-item default preference when the path is open (overridable per-PO).
        /// </summary>
        [Display(Name = "Default Source Preference")]
        public DefaultSourcePreference DefaultSourcePreference { get; set; } = DefaultSourcePreference.LetSystemDecide;

        /// <summary>
        /// B7 — engineering source-control flag (AS9100 flight-safety). When true the
        /// make-or-buy decision service hard-gates to MAKE (or an approved-source buy).
        /// </summary>
        [Display(Name = "Is Source Controlled")]
        public bool IsSourceControlled { get; set; } = false;

        /// <summary>B7 — reason/authority for the source-control flag (audit trail).</summary>
        [StringLength(500)]
        [Display(Name = "Source Control Reason")]
        public string? SourceControlReason { get; set; }

        /// <summary>
        /// B7 — make-or-buy break-even quantity. Below this qty the service leans BUY
        /// (fixed make investment amortizes worse); above, MAKE. Cached; recomputed on
        /// cost change. BE = FixedMakeInvestment / (BuyUnit − VarMakeUnit).
        /// </summary>
        [Display(Name = "Make Break-Even Qty")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? MakeBreakEvenQty { get; set; }

        /// <summary>B7 — tooling/fixture capex for the make path (break-even numerator).</summary>
        [Display(Name = "Fixed Make Investment")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? FixedMakeInvestment { get; set; }

        /// <summary>
        /// B7 carve-out guard — a PoFirst item must NOT be a stocking / replenished part.
        /// MTS/BTS/blanket items need an Item Master + reorder policy up front, so they
        /// stay StandardFirst (spec §2.3). Returns false + an error message when the
        /// combination is invalid. Pure/static for reuse in the Item write path and tests.
        /// </summary>
        public static bool ValidateSourcePatternCarveout(
            SourcePattern sourcePattern,
            bool isStocked,
            PlanningPolicy planningPolicy,
            out string error)
        {
            error = string.Empty;
            if (sourcePattern != SourcePattern.PoFirst)
                return true;

            if (isStocked)
            {
                error = "PoFirst is only valid for non-stocking ETO parts. " +
                        "This item is flagged IsStocked = true (make-to-stock / buy-to-stock), " +
                        "which requires an Item Master + reorder policy at release — keep it StandardFirst.";
                return false;
            }

            if (planningPolicy is PlanningPolicy.MakeToStock
                or PlanningPolicy.PurchaseToStock
                or PlanningPolicy.BlanketRelease)
            {
                error = $"PoFirst is incompatible with PlanningPolicy.{planningPolicy} " +
                        "(a replenished/stock policy). PO-as-Standard is for engineer/make/purchase-to-order — keep it StandardFirst.";
                return false;
            }

            return true;
        }

        // ----- BOM Behavior (2) -----------------------------------------

        /// <summary>
        /// Phantom assembly — exploded through in MRP, never actually stocked.
        /// </summary>
        [Display(Name = "Is Phantom")]
        public bool IsPhantom { get; set; } = false;

        [Display(Name = "Requires Kitting")]
        public bool RequiresKitting { get; set; } = false;

        // ----- Quality / Compliance (4) ---------------------------------

        /// <summary>
        /// AS9100 §8.3 critical characteristic flag — drives 100% inspection
        /// requirements + FAI re-trigger on engineering change.
        /// </summary>
        [Display(Name = "AS9100 Critical")]
        public bool AS9100Critical { get; set; } = false;

        /// <summary>
        /// Key Characteristic — measurement-required dimension per AS9145 or
        /// customer-mandated KC list.
        /// </summary>
        [Display(Name = "Key Characteristic")]
        public bool KeyCharacteristic { get; set; } = false;

        /// <summary>
        /// Default Inspection Plan for receipts of this Item. FK placeholder
        /// (the InspectionPlan entity ships in a later sprint; column is in
        /// place now so service code can pre-wire the reference).
        /// </summary>
        [Display(Name = "Inspection Plan Id")]
        public int? InspectionPlanId { get; set; }

        [Display(Name = "Requires First Article Inspection")]
        public bool RequiresFai { get; set; } = false;

        // ----- International Trade (4) ----------------------------------

        /// <summary>
        /// Export Control Classification Number — US BIS regulation. 5-char
        /// alphanumeric (e.g. "EAR99", "5A992.c"). Drives export-license screening.
        /// </summary>
        [StringLength(20)]
        [Display(Name = "ECCN")]
        public string? ECCN { get; set; }

        /// <summary>
        /// US Census Schedule B harmonized code for exports. 10 digits.
        /// </summary>
        [StringLength(20)]
        [Display(Name = "Schedule B Code")]
        public string? ScheduleB { get; set; }

        /// <summary>
        /// EU Intrastat reporting code (HS Code for intra-EU trade). 8 digits.
        /// </summary>
        [StringLength(20)]
        [Display(Name = "Intrastat Code")]
        public string? IntrastatCode { get; set; }

        /// <summary>
        /// EAR99 catch-all flag — Export Administration Regulations
        /// "no-license-required" pre-screen. When TRUE, simplified export
        /// processing applies. False means full ECCN screening required.
        /// </summary>
        [Display(Name = "EAR99 No-License")]
        public bool EAR99 { get; set; } = false;

        // ----- Costing freeze (2) ---------------------------------------

        /// <summary>
        /// Frozen standard cost — locked value that overrides the
        /// ItemStandardCostElement rollup (PR-FS-3) when set. Used for
        /// cost-freeze windows around fiscal close + audit periods.
        /// </summary>
        [Display(Name = "Frozen Standard Cost")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? FrozenStandardCost { get; set; }

        [Display(Name = "Frozen Standard Cost Effective At")]
        public DateTime? FrozenStandardCostEffectiveAtUtc { get; set; }

        // ----- Lifecycle + Family (2) -----------------------------------

        /// <summary>
        /// Free-form Item Family tag (e.g., "Bearings", "Cutting Tools",
        /// "Bar Stock", "Hydraulics"). For analytics + filtering. Will
        /// promote to a FK entity in a later sprint if/when the family
        /// catalog needs hierarchy.
        /// </summary>
        [StringLength(50)]
        [Display(Name = "Item Family")]
        public string? ItemFamily { get; set; }

        [Display(Name = "Lifecycle Stage")]
        public LifecycleStage LifecycleStage { get; set; } = LifecycleStage.Production;

        // ====================================================================
        // (end PR-FS-7 expansion)
        // ====================================================================

        public ICollection<ItemVendor>? ItemVendors { get; set; }
        public ICollection<ItemInventory>? Inventory { get; set; }
        public ICollection<ItemRevision>? Revisions { get; set; }
        public ICollection<PMTemplateItem>? PMTemplateItems { get; set; }
        public ICollection<ItemImage>? Images { get; set; }
        public ICollection<ItemCompanyStocking>? CompanyStockingSettings { get; set; }
        public ICollection<ItemManufacturerPart>? ManufacturerParts { get; set; }
        public ICollection<VendorItemPart>? VendorItemParts { get; set; }

        // B6 Foundation Sprint PR-FS-2 (2026-05-26) — per-Site override rows.
        // SAP MARC equivalent. See Models/Masters/ItemSite.cs.
        public ICollection<Abs.FixedAssets.Models.Masters.ItemSite>? SiteOverrides { get; set; }

        // B6 Foundation Sprint PR-FS-3 (2026-05-26) — cost composition split.
        // SAP Cost Component Split equivalent. See Models/Masters/ItemStandardCostElement.cs.
        public ICollection<Abs.FixedAssets.Models.Masters.ItemStandardCostElement>? StandardCostElements { get; set; }

        // B6 Foundation Sprint PR-FS-4 (2026-05-26) — inventory valuation layers
        // (FIFO/LIFO/Average). SAP MM "stock with values" equivalent. See
        // Models/Masters/CostLayer.cs.
        public ICollection<Abs.FixedAssets.Models.Masters.CostLayer>? CostLayers { get; set; }

        // B6 Foundation Sprint PR-FS-5 (2026-05-26) — multi-source AVL rules
        // (SAP S/4 Source List / Oracle Approved Supplier List equivalent). See
        // Models/Masters/ItemSourcingRule.cs.
        public ICollection<Abs.FixedAssets.Models.Masters.ItemSourcingRule>? SourcingRules { get; set; }

        // B6 Foundation Sprint PR-FS-6 (2026-05-26) — customer-PN cross-references
        // (SAP CMIR / Oracle Customer Item Cross Reference equivalent). See
        // Models/Masters/CustomerItemXref.cs.
        public ICollection<Abs.FixedAssets.Models.Masters.CustomerItemXref>? CustomerXrefs { get; set; }
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
