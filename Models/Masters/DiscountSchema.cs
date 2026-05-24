using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abs.FixedAssets.Models.Masters
{
    // =============================================================================
    // Sprint 13.5 PRA-9 — DiscountSchema (promotional / contract / scale discounts).
    //
    // Master Files Baseline cascade ship #7 of 10.
    //
    // SHAPES SUPPORTED:
    //   - Percent off    (e.g. 10% off all items in ItemGroup=FG)
    //   - Flat amount    (e.g. $5 off any order over $100)
    //   - Tiered volume  (e.g. 5% off 1-49 units, 10% off 50-99, 15% off 100+)
    //   - Price break    (e.g. flat $9.00/EA at 50 units, $8.50/EA at 100+)
    //   - BuyXGetY       (e.g. buy 10, get 1 free) — TiersJson carries the rule
    //
    // SCOPE (AppliesTo* fields) — what does this discount target?
    //   - AllItems (no AppliesToEntityId) — applies to every line on the order
    //   - Item     — specific SKU
    //   - ItemGroup — every Item in a group (RAW/WIP/FG/etc per PRA-7)
    //   - PriceListMaster — every line in a specific price list
    //   - Customer — only this customer's orders
    //   - CustomerTier — every customer in a tier (Wholesale/Retail/etc)
    //
    // STACKING — modern ERPs let multiple discounts apply, but the rules vary:
    //   - Exclusive — this discount, when matched, BLOCKS all others
    //   - CombinableWithAll — applies on top of every other matched discount
    //   - CombinableWithSameType — combines with other discounts of same Type
    //                              but not across types (e.g. all % discounts
    //                              stack but a % can't stack with a $-flat)
    // Priority lower wins for tie-breaking when multiple match.
    //
    // EFFECTIVE-DATING — every discount carries a window. Past-window rows
    // stay in the table for audit + future re-activation.
    //
    // CROSS-TENANT REFERENCE pattern: CompanyId NULL = system promotional
    // template (rare — most tenants define their own); CompanyId set = tenant.
    //
    // AUTHORITY:
    //   - docs/research/master-files-baseline-2026-05-24.md §6.7
    //   - SAP SD condition technique
    //   - Oracle Cloud QP_LIST_LINES (modifier rows)
    // =============================================================================
    [Table("DiscountSchemas")]
    public class DiscountSchema
    {
        public int Id { get; set; }

        public int? CompanyId { get; set; }

        [Required, StringLength(64)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // ---------------------------------------------------------------------
        // TYPE + VALUE.
        // ---------------------------------------------------------------------
        public DiscountType DiscountType { get; set; } = DiscountType.Percent;

        // The discount value. For Percent: stored as a fraction (0.10 = 10%).
        // For FlatAmount: the $ off. For PriceBreak: the override $/UOM.
        // For TieredVolume + BuyXGetY: ignored — TiersJson carries the data.
        [Column(TypeName = "numeric(18,6)")]
        public decimal? DiscountValue { get; set; }

        // Currency FK for FlatAmount / PriceBreak. NULL for Percent.
        public int? CurrencyId { get; set; }

        // Multi-tier rules stored as JSONB. Shape varies by DiscountType:
        //
        // TieredVolume:
        //   [{"qty_min":  1, "qty_max":  49, "pct": 0.05},
        //    {"qty_min": 50, "qty_max":  99, "pct": 0.10},
        //    {"qty_min":100, "qty_max":null, "pct": 0.15}]
        //
        // PriceBreak:
        //   [{"qty_min": 50,  "price": 9.00},
        //    {"qty_min": 100, "price": 8.50}]
        //
        // BuyXGetY:
        //   {"buy_qty": 10, "buy_item_id": null, "get_qty": 1, "get_item_id": null,
        //    "get_discount_pct": 1.0}     <- 1.0 = free
        [Column(TypeName = "jsonb")]
        public string? TiersJson { get; set; }

        // ---------------------------------------------------------------------
        // SCOPE — what this discount applies to.
        // ---------------------------------------------------------------------
        public DiscountAppliesToScope AppliesToScope { get; set; } = DiscountAppliesToScope.AllItems;

        // FK to the scope target — meaning depends on AppliesToScope:
        //   AllItems         → ignored (NULL)
        //   Item             → ItemId
        //   ItemGroup        → ItemGroupId (PRA-7)
        //   PriceListMaster  → PriceListMasterId
        //   Customer         → CustomerId
        //   CustomerTier     → unused (use AppliesToCustomerTier instead)
        public int? AppliesToEntityId { get; set; }

        // For AppliesToScope = CustomerTier. Carries the tier enum value.
        public CustomerTier? AppliesToCustomerTier { get; set; }

        // ---------------------------------------------------------------------
        // STACKING RULES.
        // ---------------------------------------------------------------------
        public DiscountStackingRule StackingRule { get; set; } = DiscountStackingRule.CombinableWithSameType;

        // Lower-wins tie-breaker when multiple discounts match the same order.
        // Default 100.
        public int Priority { get; set; } = 100;

        // ---------------------------------------------------------------------
        // GATES — minimum order to qualify.
        // ---------------------------------------------------------------------

        [Column(TypeName = "numeric(18,4)")]
        public decimal? MinPurchaseAmount { get; set; }

        [Column(TypeName = "numeric(18,4)")]
        public decimal? MinQuantity { get; set; }

        // Max applications per customer per period (e.g. one-time-per-customer
        // welcome coupon). NULL = unlimited.
        public int? MaxApplicationsPerCustomer { get; set; }

        // ---------------------------------------------------------------------
        // EFFECTIVE-DATING.
        // ---------------------------------------------------------------------
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;

        public DateTime? EffectiveToUtc { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; }

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(100)] public string? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        [StringLength(100)] public string? ModifiedBy { get; set; }
    }

    public enum DiscountType
    {
        Percent = 0,            // % off — value is fraction (0.10 = 10%)
        FlatAmount = 1,         // $ off
        TieredVolume = 2,       // % off per qty tier — TiersJson carries the tiers
        PriceBreak = 3,         // override $/UOM at qty break — TiersJson carries breaks
        BuyXGetY = 4,           // buy X get Y free/discounted — TiersJson carries rule
        FreeShipping = 5,       // waives shipping line
        Other = 99
    }

    public enum DiscountAppliesToScope
    {
        AllItems = 0,                   // applies to every line on the order
        Item = 1,                       // AppliesToEntityId = ItemId
        ItemGroup = 2,                  // AppliesToEntityId = ItemGroupId
        PriceListMaster = 3,            // AppliesToEntityId = PriceListMasterId
        Customer = 4,                   // AppliesToEntityId = CustomerId
        CustomerTier = 5,               // AppliesToCustomerTier set
        Other = 99
    }

    public enum DiscountStackingRule
    {
        Exclusive = 0,                  // blocks all other discounts when matched
        CombinableWithAll = 1,          // applies on top of every other match
        CombinableWithSameType = 2      // stacks within same DiscountType only
    }
}
